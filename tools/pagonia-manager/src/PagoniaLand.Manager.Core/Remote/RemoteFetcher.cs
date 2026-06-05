using System.Diagnostics.CodeAnalysis;
using YamlDotNet.RepresentationModel;
using YamlDotNet.Serialization;

namespace PagoniaLand.Manager;

/// <summary>
/// Orchestrates the manager-side fetch for a remote mod source. Takes a
/// parsed <see cref="GitHubSource"/>, resolves the ref to a commit SHA,
/// downloads <c>index.yaml</c> (if present), locates the mod folder,
/// downloads <c>mod.yaml</c> + every patch file it references, and
/// returns a temp directory laid out exactly like the existing local
/// install pipeline expects. The caller (CLI) then hands that temp
/// directory off to <see cref="ModInstaller"/> unchanged.
/// </summary>
public sealed class RemoteFetcher
{
    // YamlDotNet reflects over these via Deserialize<RepoIndex>() when reading a
    // repo's index.yaml (catalog browse + install --from gh:). Pin them so the
    // AOT trimmer keeps their members — otherwise the published binary throws
    // "Exception during deserialization" even though it works under JIT. Same
    // rooting pattern as ProfileStore / CatalogFetcher.
    private const DynamicallyAccessedMemberTypes Shape =
        DynamicallyAccessedMemberTypes.PublicConstructors
        | DynamicallyAccessedMemberTypes.PublicProperties
        | DynamicallyAccessedMemberTypes.PublicFields;

    private readonly IRemoteContentFetcher _fetcher;

    [DynamicDependency(Shape, typeof(RepoIndex))]
    [DynamicDependency(Shape, typeof(RepoIndexRepo))]
    [DynamicDependency(Shape, typeof(RepoIndexMod))]
    [DynamicDependency(Shape, typeof(RepoIndexCollection))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors, typeof(List<RepoIndexMod>))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors, typeof(List<RepoIndexCollection>))]
    public RemoteFetcher(IRemoteContentFetcher fetcher)
    {
        _fetcher = fetcher;
    }

    public RemoteFetchResult FetchMod(GitHubSource source, CancellationToken cancellationToken = default)
    {
        // The CLI surface is synchronous; we bridge into the async fetcher here
        // rather than push async/await up through the rest of the manager.
        return FetchModAsync(source, cancellationToken).GetAwaiter().GetResult();
    }

    public async Task<RemoteFetchResult> FetchModAsync(GitHubSource source, CancellationToken cancellationToken = default)
    {
        var diagnostics = new List<ManagerDiagnostic>();

        if (source.ModSpec is null)
        {
            diagnostics.Add(Error(ManagerDiagnosticCodes.RemoteFetchFailed,
                $"Source 'gh:{source.Owner}/{source.Repo}' does not name a mod. " +
                "Append the mod id (or repo-relative path), e.g. " +
                $"'gh:{source.Owner}/{source.Repo}/<mod-id>'."));
            return RemoteFetchResult.Failure(diagnostics);
        }

        string commitSha;
        try
        {
            var resolved = await _fetcher.ResolveCommitShaAsync(source.Owner, source.Repo, source.Ref, cancellationToken).ConfigureAwait(false);
            if (resolved is null)
            {
                diagnostics.Add(Error(ManagerDiagnosticCodes.RemoteFetchFailed,
                    $"Could not resolve ref '{source.Ref}' in {source.Owner}/{source.Repo}: ref not found."));
                return RemoteFetchResult.Failure(diagnostics);
            }
            commitSha = resolved;
        }
        catch (Exception ex)
        {
            diagnostics.Add(Error(ManagerDiagnosticCodes.RemoteFetchFailed,
                $"Resolving ref '{source.Ref}' in {source.Owner}/{source.Repo} failed: {ex.Message}"));
            return RemoteFetchResult.Failure(diagnostics);
        }

        diagnostics.Add(Info(ManagerDiagnosticCodes.RemoteResolvedToCommit,
            $"Resolved gh:{source.Owner}/{source.Repo}#{source.Ref} to commit {Shorten(commitSha)}."));

        // Locate the mod folder. With an index.yaml present, ModSpec is looked
        // up as a mod id first, then as a literal path. Without one, ModSpec is
        // interpreted as a folder path verbatim.
        string modFolder;
        string? resolvedModId;
        try
        {
            (modFolder, resolvedModId) = await ResolveModFolderAsync(source, commitSha, diagnostics, cancellationToken).ConfigureAwait(false);
        }
        catch (RemoteFetchAbortException ex)
        {
            diagnostics.AddRange(ex.Diagnostics);
            return RemoteFetchResult.Failure(diagnostics);
        }

        // The folder the index (or the literal ModSpec) names is relative to the
        // repo's index — i.e. to source.BasePath when the repo hosts its tree in
        // a subdirectory. Join the base on here so every downstream fetch + the
        // traversal guard see the real repo path. Base absent = byte-for-byte the
        // old behaviour (JoinRepoPath returns the folder unchanged).
        var repoModFolder = JoinRepoPath(source.BasePath, modFolder);

        // Reject any path-traversal segments. The schema's relativePath def
        // catches this in a published index.yaml, but a repo without an
        // index.yaml that we treat as a path-only fetch wouldn't go through
        // schema validation. Defence in depth — applied to the base-joined path.
        if (repoModFolder.Contains("..", StringComparison.Ordinal))
        {
            diagnostics.Add(Error(ManagerDiagnosticCodes.RemoteFetchFailed,
                $"Refusing to fetch mod folder '{repoModFolder}': contains '..' traversal."));
            return RemoteFetchResult.Failure(diagnostics);
        }

        // Fetch mod.yaml.
        var modYamlPath = JoinRepoPath(repoModFolder, "mod.yaml");
        var modYamlUrl = RawUrl(source.Owner, source.Repo, commitSha, modYamlPath);
        RemoteFetchedContent? modYaml;
        try
        {
            modYaml = await _fetcher.TryFetchAsync(modYamlUrl, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            diagnostics.Add(Error(ManagerDiagnosticCodes.RemoteFetchFailed,
                $"Fetching mod.yaml at '{modYamlPath}' failed: {ex.Message}"));
            return RemoteFetchResult.Failure(diagnostics);
        }
        if (modYaml is null)
        {
            diagnostics.Add(Error(ManagerDiagnosticCodes.RemoteFetchFailed,
                $"Remote mod.yaml not found at '{modYamlPath}' on {source.Owner}/{source.Repo}@{Shorten(commitSha)}."));
            return RemoteFetchResult.Failure(diagnostics);
        }

        // Enumerate the patch files mod.yaml references. We walk the YAML tree
        // generically rather than deserialising the full ManifestFile model so
        // a malformed-but-decodable mod.yaml can still report which patches it
        // claims to ship; ManifestValidator catches structural problems later
        // when the local pipeline runs against the temp dir.
        List<string> patchPaths;
        try
        {
            patchPaths = EnumeratePatchPaths(modYaml.Text);
        }
        catch (Exception ex)
        {
            diagnostics.Add(Error(ManagerDiagnosticCodes.RemoteIndexMalformed,
                $"Could not parse mod.yaml at '{modYamlPath}': {ex.Message}"));
            return RemoteFetchResult.Failure(diagnostics);
        }

        // Stage everything into a temp dir laid out as a normal local mod folder.
        var tempDir = Path.Combine(Path.GetTempPath(), $"pagonia-remote-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(tempDir);
            File.WriteAllBytes(Path.Combine(tempDir, "mod.yaml"), modYaml.Bytes);

            foreach (var patchRelative in patchPaths)
            {
                if (patchRelative.Contains("..", StringComparison.Ordinal))
                {
                    diagnostics.Add(Error(ManagerDiagnosticCodes.RemoteFetchFailed,
                        $"Refusing to fetch patch path '{patchRelative}' (mod.yaml lists a '..' traversal)."));
                    TryDeleteDir(tempDir);
                    return RemoteFetchResult.Failure(diagnostics);
                }

                var patchUrlPath = JoinRepoPath(repoModFolder, patchRelative);
                var patchUrl = RawUrl(source.Owner, source.Repo, commitSha, patchUrlPath);
                RemoteFetchedContent? patch;
                try
                {
                    patch = await _fetcher.TryFetchAsync(patchUrl, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    diagnostics.Add(Error(ManagerDiagnosticCodes.RemoteFetchFailed,
                        $"Fetching patch '{patchRelative}' failed: {ex.Message}"));
                    TryDeleteDir(tempDir);
                    return RemoteFetchResult.Failure(diagnostics);
                }
                if (patch is null)
                {
                    diagnostics.Add(Error(ManagerDiagnosticCodes.RemoteFetchFailed,
                        $"Patch file '{patchRelative}' listed in mod.yaml not found at '{patchUrlPath}'."));
                    TryDeleteDir(tempDir);
                    return RemoteFetchResult.Failure(diagnostics);
                }

                var destPath = Path.Combine(tempDir, patchRelative.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
                File.WriteAllBytes(destPath, patch.Bytes);
            }
        }
        catch (Exception ex)
        {
            diagnostics.Add(Error(ManagerDiagnosticCodes.RemoteFetchFailed,
                $"Writing remote fetch into temp dir failed: {ex.Message}"));
            TryDeleteDir(tempDir);
            return RemoteFetchResult.Failure(diagnostics);
        }

        // The resolved source string is what the sidecar records and what
        // `pagonia-manager list` shows as the mod's provenance. We pin the SHA
        // (not the user-typed ref) so the trail stays accurate when the branch
        // moves on the remote, and carry the base path through the ':' segment
        // so a later re-install / profile export round-trips the subdirectory
        // form. modIdForSource stays base-relative (the id or index-relative
        // folder); the base is added exactly once, here.
        var modIdForSource = resolvedModId ?? modFolder;
        var resolvedSource = $"gh:{source.Owner}/{source.Repo}{BaseSegment(source.BasePath)}#{commitSha}/{modIdForSource}";

        return RemoteFetchResult.Ok(tempDir, resolvedSource, commitSha, repoModFolder, diagnostics);
    }

    private async Task<(string ModFolder, string? ModId)> ResolveModFolderAsync(
        GitHubSource source,
        string commitSha,
        List<ManagerDiagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        // index.yaml lives under the repo's base path (empty = repo root). The
        // mod folders it lists stay base-relative; FetchMod joins the base back
        // on for the actual fetch.
        var indexUrl = RawUrl(source.Owner, source.Repo, commitSha, JoinRepoPath(source.BasePath, "index.yaml"));
        RemoteFetchedContent? indexContent;
        try
        {
            indexContent = await _fetcher.TryFetchAsync(indexUrl, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            throw new RemoteFetchAbortException(new[]
            {
                Error(ManagerDiagnosticCodes.RemoteFetchFailed,
                    $"Fetching index.yaml failed: {ex.Message}")
            });
        }

        if (indexContent is null)
        {
            // No index.yaml — interpret ModSpec as a literal repo-relative path
            // (relative to the base path). This supports the single-mod-repo
            // case where authors don't ship a catalog.
            return (source.ModSpec!, ModId: null);
        }

        RepoIndex index;
        try
        {
            var deserializer = new DeserializerBuilder()
                .IgnoreUnmatchedProperties()
                .Build();
            index = deserializer.Deserialize<RepoIndex>(indexContent.Text)
                ?? throw new InvalidOperationException("Parsed index.yaml as null.");
        }
        catch (Exception ex)
        {
            throw new RemoteFetchAbortException(new[]
            {
                Error(ManagerDiagnosticCodes.RemoteIndexMalformed,
                    $"Could not parse index.yaml at {source.Owner}/{source.Repo}@{Shorten(commitSha)}: {ex.Message}")
            });
        }

        // Try mod id first; fall back to path-equality; finally give up with a
        // structured diagnostic so the user sees what the repo actually offered.
        var byId = index.Mods.FirstOrDefault(m => string.Equals(m.Id, source.ModSpec, StringComparison.Ordinal));
        if (byId is not null)
        {
            return (byId.Path, byId.Id);
        }

        var byPath = index.Mods.FirstOrDefault(m => string.Equals(m.Path, source.ModSpec, StringComparison.Ordinal));
        if (byPath is not null)
        {
            return (byPath.Path, byPath.Id);
        }

        var available = index.Mods.Count == 0
            ? "(no mods listed in index.yaml)"
            : string.Join(", ", index.Mods.Select(m => m.Id));
        throw new RemoteFetchAbortException(new[]
        {
            Error(ManagerDiagnosticCodes.ModNotInRepoIndex,
                $"Mod '{source.ModSpec}' not in {source.Owner}/{source.Repo}'s index.yaml. Available: {available}.")
        });
    }

    private static List<string> EnumeratePatchPaths(string modYamlText)
    {
        // Walk a generic YamlStream so the patches array is found whether it
        // sits at the manifest root (`patches:`) or nested inside per-package
        // patchSets (`patchSets[*].patches`). Ignore anything we don't
        // recognise — the manager-side ManifestValidator catches structural
        // problems once the temp dir is fed back through the local pipeline.
        var stream = new YamlStream();
        using var reader = new StringReader(modYamlText);
        stream.Load(reader);
        var result = new List<string>();
        if (stream.Documents.Count == 0)
        {
            return result;
        }

        if (stream.Documents[0].RootNode is not YamlMappingNode root)
        {
            return result;
        }

        CollectPatchPaths(root, result);
        return result;
    }

    private static void CollectPatchPaths(YamlNode node, List<string> sink)
    {
        switch (node)
        {
            case YamlMappingNode mapping:
                foreach (var (key, value) in mapping.Children)
                {
                    if (key is YamlScalarNode keyScalar && keyScalar.Value == "patches" && value is YamlSequenceNode seq)
                    {
                        foreach (var item in seq.Children)
                        {
                            if (item is YamlScalarNode pathScalar && pathScalar.Value is { Length: > 0 } p)
                            {
                                sink.Add(p);
                            }
                        }
                    }
                    else
                    {
                        CollectPatchPaths(value, sink);
                    }
                }
                break;
            case YamlSequenceNode sequence:
                foreach (var item in sequence.Children)
                {
                    CollectPatchPaths(item, sink);
                }
                break;
        }
    }

    private static string RawUrl(string owner, string repo, string sha, string path)
        => $"https://raw.githubusercontent.com/{owner}/{repo}/{sha}/{path}";

    private static string JoinRepoPath(string folder, string file)
    {
        if (string.IsNullOrEmpty(folder)) { return file; }
        return $"{folder.TrimEnd('/')}/{file}";
    }

    /// <summary>The ':' base-path segment for a resolved <c>gh:</c> source string
    /// (empty when the repo's index is at the root), so provenance round-trips the
    /// subdirectory form.</summary>
    private static string BaseSegment(string basePath)
        => basePath.Length > 0 ? $":{basePath}" : string.Empty;

    private static string Shorten(string sha) => sha.Length >= 8 ? sha[..8] : sha;

    private static void TryDeleteDir(string dir)
    {
        try { if (Directory.Exists(dir)) { Directory.Delete(dir, recursive: true); } }
        catch { /* best effort; cleanup failures don't affect correctness */ }
    }

    private static ManagerDiagnostic Error(string code, string message)
        => new(ManagerDiagnosticSeverity.Error, code, message, null);

    private static ManagerDiagnostic Info(string code, string message)
        => new(ManagerDiagnosticSeverity.Info, code, message, null);

    /// <summary>Internal flow-control: an abort path inside the resolver bubbles
    /// up its own diagnostic batch without the orchestrator needing to thread
    /// success flags through every branch.</summary>
    private sealed class RemoteFetchAbortException : Exception
    {
        public IReadOnlyList<ManagerDiagnostic> Diagnostics { get; }
        public RemoteFetchAbortException(IReadOnlyList<ManagerDiagnostic> diagnostics)
        {
            Diagnostics = diagnostics;
        }
    }

    // ----- Collection fetch path -------------------------------------------

    public RemoteCollectionFetchResult FetchCollection(GitHubSource source, CancellationToken cancellationToken = default)
        => FetchCollectionAsync(source, cancellationToken).GetAwaiter().GetResult();

    public async Task<RemoteCollectionFetchResult> FetchCollectionAsync(GitHubSource source, CancellationToken cancellationToken = default)
    {
        var diagnostics = new List<ManagerDiagnostic>();

        if (source.ModSpec is null)
        {
            diagnostics.Add(Error(ManagerDiagnosticCodes.RemoteFetchFailed,
                $"Source 'gh:{source.Owner}/{source.Repo}' does not name a collection. " +
                $"Append the collection id (or repo-relative path), e.g. 'gh:{source.Owner}/{source.Repo}/<collection-id>'."));
            return RemoteCollectionFetchResult.Failure(diagnostics);
        }

        // 1. Resolve the user-supplied ref to a concrete commit SHA so the
        // lockfile pins exact code, not a moving branch.
        string commitSha;
        try
        {
            var resolved = await _fetcher.ResolveCommitShaAsync(source.Owner, source.Repo, source.Ref, cancellationToken).ConfigureAwait(false);
            if (resolved is null)
            {
                diagnostics.Add(Error(ManagerDiagnosticCodes.RemoteFetchFailed,
                    $"Could not resolve ref '{source.Ref}' in {source.Owner}/{source.Repo}: ref not found."));
                return RemoteCollectionFetchResult.Failure(diagnostics);
            }
            commitSha = resolved;
        }
        catch (Exception ex)
        {
            diagnostics.Add(Error(ManagerDiagnosticCodes.RemoteFetchFailed,
                $"Resolving ref '{source.Ref}' in {source.Owner}/{source.Repo} failed: {ex.Message}"));
            return RemoteCollectionFetchResult.Failure(diagnostics);
        }
        diagnostics.Add(Info(ManagerDiagnosticCodes.RemoteResolvedToCommit,
            $"Resolved gh:{source.Owner}/{source.Repo}#{source.Ref} to commit {Shorten(commitSha)}."));

        // 2. Locate the .collection.yaml inside the repo. With index.yaml,
        // ModSpec is looked up as a collection id first then as a literal
        // path. Without one, ModSpec is treated as the path verbatim.
        string collectionFilePath;
        string? resolvedCollectionId;
        RepoIndex? repoIndex;
        try
        {
            (collectionFilePath, resolvedCollectionId, repoIndex) = await ResolveCollectionPathAsync(source, commitSha, diagnostics, cancellationToken).ConfigureAwait(false);
        }
        catch (RemoteFetchAbortException ex)
        {
            diagnostics.AddRange(ex.Diagnostics);
            return RemoteCollectionFetchResult.Failure(diagnostics);
        }

        // The collection path the index (or literal ModSpec) names is relative
        // to the repo's base path; join it on for the fetch + the guard, but
        // keep collectionFilePath (base-relative) for the file name + source
        // string. Base absent = unchanged.
        var repoCollectionPath = JoinRepoPath(source.BasePath, collectionFilePath);
        if (repoCollectionPath.Contains("..", StringComparison.Ordinal))
        {
            diagnostics.Add(Error(ManagerDiagnosticCodes.RemoteFetchFailed,
                $"Refusing to fetch collection path '{repoCollectionPath}': contains '..' traversal."));
            return RemoteCollectionFetchResult.Failure(diagnostics);
        }

        // 3. Fetch the collection.yaml.
        var collectionUrl = RawUrl(source.Owner, source.Repo, commitSha, repoCollectionPath);
        RemoteFetchedContent? collectionYaml;
        try
        {
            collectionYaml = await _fetcher.TryFetchAsync(collectionUrl, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            diagnostics.Add(Error(ManagerDiagnosticCodes.RemoteFetchFailed,
                $"Fetching collection at '{collectionFilePath}' failed: {ex.Message}"));
            return RemoteCollectionFetchResult.Failure(diagnostics);
        }
        if (collectionYaml is null)
        {
            diagnostics.Add(Error(ManagerDiagnosticCodes.RemoteFetchFailed,
                $"Remote collection not found at '{collectionFilePath}' on {source.Owner}/{source.Repo}@{Shorten(commitSha)}."));
            return RemoteCollectionFetchResult.Failure(diagnostics);
        }

        // 4. Parse the collection: pull out the mods[] list with their optional
        // source: field. We don't fully model the collection schema here —
        // CollectionResolver does that downstream; we just need enough to
        // know what to fetch.
        List<CollectionFetchEntry> entries;
        try
        {
            entries = ParseCollectionEntries(collectionYaml.Text);
        }
        catch (Exception ex)
        {
            diagnostics.Add(Error(ManagerDiagnosticCodes.RemoteIndexMalformed,
                $"Could not parse remote collection at '{collectionFilePath}': {ex.Message}"));
            return RemoteCollectionFetchResult.Failure(diagnostics);
        }

        // 5. Set up the temp tree:
        //   <tempDir>/<collection-file-name>            <-- the manifest
        //   <tempDir>/mods/<mod-id>/mod.yaml + patches/ <-- one folder per mod
        var tempDir = Path.Combine(Path.GetTempPath(), $"pagonia-remote-collection-{Guid.NewGuid():N}");
        var modSources = new Dictionary<string, string>(StringComparer.Ordinal);
        try
        {
            Directory.CreateDirectory(tempDir);
            var collectionFileName = Path.GetFileName(collectionFilePath);
            var collectionLocalPath = Path.Combine(tempDir, collectionFileName);
            File.WriteAllBytes(collectionLocalPath, collectionYaml.Bytes);

            var modsRoot = Path.Combine(tempDir, "mods");
            Directory.CreateDirectory(modsRoot);

            // 6. Fetch each mod referenced by the collection.
            foreach (var entry in entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var fetchedSource = await FetchCollectionModAsync(source, commitSha, repoIndex, entry, modsRoot, diagnostics, cancellationToken).ConfigureAwait(false);
                if (fetchedSource is null)
                {
                    TryDeleteDir(tempDir);
                    return RemoteCollectionFetchResult.Failure(diagnostics);
                }
                modSources[entry.Id] = fetchedSource;
            }

            var resolvedCollectionSource = $"gh:{source.Owner}/{source.Repo}{BaseSegment(source.BasePath)}#{commitSha}/{resolvedCollectionId ?? collectionFilePath}";
            return RemoteCollectionFetchResult.Ok(tempDir, collectionLocalPath, modsRoot, resolvedCollectionSource, commitSha, modSources, diagnostics);
        }
        catch (Exception ex)
        {
            diagnostics.Add(Error(ManagerDiagnosticCodes.RemoteFetchFailed,
                $"Writing remote collection fetch into temp dir failed: {ex.Message}"));
            TryDeleteDir(tempDir);
            return RemoteCollectionFetchResult.Failure(diagnostics);
        }
    }

    private async Task<(string CollectionPath, string? CollectionId, RepoIndex? Index)> ResolveCollectionPathAsync(
        GitHubSource source,
        string commitSha,
        List<ManagerDiagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        // index.yaml lives under the repo's base path (empty = root); the
        // collection + mod paths it lists stay base-relative and get the base
        // joined back on at fetch time.
        var indexUrl = RawUrl(source.Owner, source.Repo, commitSha, JoinRepoPath(source.BasePath, "index.yaml"));
        RemoteFetchedContent? indexContent;
        try
        {
            indexContent = await _fetcher.TryFetchAsync(indexUrl, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            throw new RemoteFetchAbortException(new[]
            {
                Error(ManagerDiagnosticCodes.RemoteFetchFailed, $"Fetching index.yaml failed: {ex.Message}"),
            });
        }

        if (indexContent is null)
        {
            // No index — treat ModSpec as a (base-relative) path to the
            // .collection.yaml. Authors of single-collection repos can skip the
            // index.yaml.
            return (source.ModSpec!, CollectionId: null, Index: null);
        }

        RepoIndex repoIndex;
        try
        {
            var deserializer = new DeserializerBuilder().IgnoreUnmatchedProperties().Build();
            repoIndex = deserializer.Deserialize<RepoIndex>(indexContent.Text)
                ?? throw new InvalidOperationException("Parsed index.yaml as null.");
        }
        catch (Exception ex)
        {
            throw new RemoteFetchAbortException(new[]
            {
                Error(ManagerDiagnosticCodes.RemoteIndexMalformed,
                    $"Could not parse index.yaml at {source.Owner}/{source.Repo}@{Shorten(commitSha)}: {ex.Message}"),
            });
        }

        var byId = repoIndex.Collections.FirstOrDefault(c => string.Equals(c.Id, source.ModSpec, StringComparison.Ordinal));
        if (byId is not null)
        {
            return (byId.Path, byId.Id, repoIndex);
        }
        var byPath = repoIndex.Collections.FirstOrDefault(c => string.Equals(c.Path, source.ModSpec, StringComparison.Ordinal));
        if (byPath is not null)
        {
            return (byPath.Path, byPath.Id, repoIndex);
        }

        var available = repoIndex.Collections.Count == 0
            ? "(no collections listed in index.yaml)"
            : string.Join(", ", repoIndex.Collections.Select(c => c.Id));
        throw new RemoteFetchAbortException(new[]
        {
            Error(ManagerDiagnosticCodes.ModNotInRepoIndex,
                $"Collection '{source.ModSpec}' not in {source.Owner}/{source.Repo}'s index.yaml. Available: {available}."),
        });
    }

    private async Task<string?> FetchCollectionModAsync(
        GitHubSource collectionSource,
        string collectionCommitSha,
        RepoIndex? collectionRepoIndex,
        CollectionFetchEntry entry,
        string modsRoot,
        List<ManagerDiagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        // Decide where to fetch from.
        //   - source: gh:owner/repo[#ref]   -> cross-repo fetch (recursive)
        //   - source: empty / "local" / "" -> same-repo via this repo's index.yaml
        //   - source: relative path         -> same-repo treat as path
        //   - source: http(s)://            -> warn + treat as same-repo lookup (existing
        //                                       collectionRemoteSourceUnsupported semantics)
        string destDir = Path.Combine(modsRoot, SanitizeForFolderName(entry.Id));
        Directory.CreateDirectory(destDir);

        var rawSource = entry.Source?.Trim() ?? string.Empty;

        if (rawSource.Length > 0 && RemoteSourceParser.TryParse(rawSource, out var crossRepo) && crossRepo is GitHubSource crossRepoGh)
        {
            // Cross-repo fetch. The cross-repo's ref might be HEAD or a tag;
            // we resolve it to a SHA at fetch time exactly like the top-level
            // FetchMod does, so the lockfile pins the exact commit.
            string crossSha;
            try
            {
                var resolved = await _fetcher.ResolveCommitShaAsync(crossRepoGh.Owner, crossRepoGh.Repo, crossRepoGh.Ref, cancellationToken).ConfigureAwait(false);
                if (resolved is null)
                {
                    diagnostics.Add(Error(ManagerDiagnosticCodes.RemoteFetchFailed,
                        $"Cross-repo source for mod '{entry.Id}': could not resolve ref '{crossRepoGh.Ref}' in {crossRepoGh.Owner}/{crossRepoGh.Repo}."));
                    return null;
                }
                crossSha = resolved;
            }
            catch (Exception ex)
            {
                diagnostics.Add(Error(ManagerDiagnosticCodes.RemoteFetchFailed,
                    $"Cross-repo source for mod '{entry.Id}': ref resolution failed: {ex.Message}"));
                return null;
            }

            diagnostics.Add(Info(ManagerDiagnosticCodes.CrossRepoSourceResolved,
                $"Cross-repo source for mod '{entry.Id}' resolved to {crossRepoGh.Owner}/{crossRepoGh.Repo}@{Shorten(crossSha)}."));

            // Resolve the mod folder inside the cross-repo. ModSpec is required;
            // if missing, fall back to the entry's id.
            var crossModSpec = crossRepoGh.ModSpec ?? entry.Id;
            string crossModFolder;
            try
            {
                crossModFolder = await ResolveCrossRepoModFolderAsync(crossRepoGh, crossSha, crossModSpec, diagnostics, cancellationToken).ConfigureAwait(false);
            }
            catch (RemoteFetchAbortException ex)
            {
                diagnostics.AddRange(ex.Diagnostics);
                return null;
            }

            if (!await FetchModFilesIntoAsync(crossRepoGh.Owner, crossRepoGh.Repo, crossSha, crossModFolder, destDir, entry.Id, diagnostics, cancellationToken).ConfigureAwait(false))
            {
                return null;
            }
            return $"gh:{crossRepoGh.Owner}/{crossRepoGh.Repo}{BaseSegment(crossRepoGh.BasePath)}#{crossSha}/{crossModSpec}";
        }

        if (rawSource.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || rawSource.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            // Non-github URL sources aren't supported by the GitHub-only
            // fetcher; surface a warning but try same-repo resolution so
            // the install still proceeds when a matching mod exists locally
            // in the collection's repo.
            diagnostics.Add(new ManagerDiagnostic(
                ManagerDiagnosticSeverity.Warning,
                ManagerDiagnosticCodes.CollectionRemoteSourceUnsupported,
                $"Mod '{entry.Id}' declares non-github remote source '{rawSource}'; falling back to same-repo lookup.",
                null));
        }

        // Same-repo path. Look up the mod folder in the collection's repo
        // index.yaml; fall back to interpreting rawSource as a path if no
        // index is present.
        string modFolderInCollectionRepo;
        if (collectionRepoIndex is not null)
        {
            var match = collectionRepoIndex.Mods.FirstOrDefault(m => string.Equals(m.Id, entry.Id, StringComparison.Ordinal));
            if (match is null)
            {
                diagnostics.Add(Error(ManagerDiagnosticCodes.ModNotInRepoIndex,
                    $"Mod '{entry.Id}' (referenced by collection) is not in {collectionSource.Owner}/{collectionSource.Repo}'s index.yaml."));
                return null;
            }
            modFolderInCollectionRepo = match.Path;
        }
        else
        {
            // No index, and the entry's source didn't name a remote → fall back
            // to a conventional "mods/<id>" path. Authors who skip index.yaml
            // are expected to follow that layout.
            modFolderInCollectionRepo = (rawSource.Length > 0 && !rawSource.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                ? rawSource
                : $"mods/{entry.Id}";
        }

        // Same-repo mod folders are base-relative (from this repo's index.yaml,
        // or the conventional fallback); join the collection repo's base on for
        // the fetch, and carry it through the provenance string.
        var repoModFolderInCollectionRepo = JoinRepoPath(collectionSource.BasePath, modFolderInCollectionRepo);
        if (!await FetchModFilesIntoAsync(collectionSource.Owner, collectionSource.Repo, collectionCommitSha, repoModFolderInCollectionRepo, destDir, entry.Id, diagnostics, cancellationToken).ConfigureAwait(false))
        {
            return null;
        }
        return $"gh:{collectionSource.Owner}/{collectionSource.Repo}{BaseSegment(collectionSource.BasePath)}#{collectionCommitSha}/{entry.Id}";
    }

    private async Task<string> ResolveCrossRepoModFolderAsync(
        GitHubSource crossSource,
        string crossSha,
        string modSpec,
        List<ManagerDiagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        // A cross-repo source may itself carry a base path (gh:other/repo:sub/mod);
        // honour it for the index fetch and join it onto the returned folder so
        // the caller fetches from the right subtree.
        var indexUrl = RawUrl(crossSource.Owner, crossSource.Repo, crossSha, JoinRepoPath(crossSource.BasePath, "index.yaml"));
        var indexContent = await _fetcher.TryFetchAsync(indexUrl, cancellationToken).ConfigureAwait(false);
        if (indexContent is null)
        {
            // No cross-repo index — treat modSpec as a (base-relative) folder path.
            return JoinRepoPath(crossSource.BasePath, modSpec);
        }

        RepoIndex idx;
        try
        {
            idx = new DeserializerBuilder().IgnoreUnmatchedProperties().Build().Deserialize<RepoIndex>(indexContent.Text)
                ?? throw new InvalidOperationException("Parsed cross-repo index.yaml as null.");
        }
        catch (Exception ex)
        {
            throw new RemoteFetchAbortException(new[]
            {
                Error(ManagerDiagnosticCodes.RemoteIndexMalformed,
                    $"Could not parse index.yaml at {crossSource.Owner}/{crossSource.Repo}@{Shorten(crossSha)}: {ex.Message}"),
            });
        }

        var byId = idx.Mods.FirstOrDefault(m => string.Equals(m.Id, modSpec, StringComparison.Ordinal));
        if (byId is not null) { return JoinRepoPath(crossSource.BasePath, byId.Path); }
        var byPath = idx.Mods.FirstOrDefault(m => string.Equals(m.Path, modSpec, StringComparison.Ordinal));
        if (byPath is not null) { return JoinRepoPath(crossSource.BasePath, byPath.Path); }

        throw new RemoteFetchAbortException(new[]
        {
            Error(ManagerDiagnosticCodes.ModNotInRepoIndex,
                $"Cross-repo source mod '{modSpec}' not in {crossSource.Owner}/{crossSource.Repo}'s index.yaml."),
        });
    }

    /// <summary>
    /// Fetches mod.yaml + every patch file it references from a known
    /// (owner, repo, sha, modFolder) coordinate into <paramref name="destDir"/>.
    /// Returns false on any failure (with diagnostics appended); the caller
    /// is responsible for cleaning up the temp tree.
    /// </summary>
    private async Task<bool> FetchModFilesIntoAsync(
        string owner,
        string repo,
        string sha,
        string modFolder,
        string destDir,
        string modIdForDiagnostics,
        List<ManagerDiagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        if (modFolder.Contains("..", StringComparison.Ordinal))
        {
            diagnostics.Add(Error(ManagerDiagnosticCodes.RemoteFetchFailed,
                $"Refusing to fetch mod '{modIdForDiagnostics}': mod folder '{modFolder}' contains '..' traversal."));
            return false;
        }

        var modYamlPath = JoinRepoPath(modFolder, "mod.yaml");
        var modYaml = await _fetcher.TryFetchAsync(RawUrl(owner, repo, sha, modYamlPath), cancellationToken).ConfigureAwait(false);
        if (modYaml is null)
        {
            diagnostics.Add(Error(ManagerDiagnosticCodes.RemoteFetchFailed,
                $"mod.yaml for '{modIdForDiagnostics}' not found at '{modYamlPath}' on {owner}/{repo}@{Shorten(sha)}."));
            return false;
        }

        File.WriteAllBytes(Path.Combine(destDir, "mod.yaml"), modYaml.Bytes);

        List<string> patchPaths;
        try
        {
            patchPaths = EnumeratePatchPaths(modYaml.Text);
        }
        catch (Exception ex)
        {
            diagnostics.Add(Error(ManagerDiagnosticCodes.RemoteIndexMalformed,
                $"Could not parse mod.yaml for '{modIdForDiagnostics}': {ex.Message}"));
            return false;
        }

        foreach (var rel in patchPaths)
        {
            if (rel.Contains("..", StringComparison.Ordinal))
            {
                diagnostics.Add(Error(ManagerDiagnosticCodes.RemoteFetchFailed,
                    $"Refusing to fetch patch '{rel}' for mod '{modIdForDiagnostics}' (contains '..' traversal)."));
                return false;
            }
            var patchUrl = RawUrl(owner, repo, sha, JoinRepoPath(modFolder, rel));
            var patch = await _fetcher.TryFetchAsync(patchUrl, cancellationToken).ConfigureAwait(false);
            if (patch is null)
            {
                diagnostics.Add(Error(ManagerDiagnosticCodes.RemoteFetchFailed,
                    $"Patch '{rel}' for mod '{modIdForDiagnostics}' not found."));
                return false;
            }
            var dest = Path.Combine(destDir, rel.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            File.WriteAllBytes(dest, patch.Bytes);
        }
        return true;
    }

    private static List<CollectionFetchEntry> ParseCollectionEntries(string collectionYamlText)
    {
        var stream = new YamlStream();
        using var reader = new StringReader(collectionYamlText);
        stream.Load(reader);
        var entries = new List<CollectionFetchEntry>();
        if (stream.Documents.Count == 0 || stream.Documents[0].RootNode is not YamlMappingNode root)
        {
            return entries;
        }
        if (!root.Children.TryGetValue(new YamlScalarNode("mods"), out var modsNode) || modsNode is not YamlSequenceNode modsSeq)
        {
            return entries;
        }
        foreach (var modNode in modsSeq.Children)
        {
            if (modNode is not YamlMappingNode m) { continue; }
            var id = (m.Children.TryGetValue(new YamlScalarNode("id"), out var idNode) && idNode is YamlScalarNode idScalar) ? idScalar.Value ?? string.Empty : string.Empty;
            var version = (m.Children.TryGetValue(new YamlScalarNode("version"), out var vNode) && vNode is YamlScalarNode vScalar) ? vScalar.Value ?? string.Empty : string.Empty;
            var source = (m.Children.TryGetValue(new YamlScalarNode("source"), out var sNode) && sNode is YamlScalarNode sScalar) ? sScalar.Value : null;
            if (!string.IsNullOrWhiteSpace(id))
            {
                entries.Add(new CollectionFetchEntry(id, version, source));
            }
        }
        return entries;
    }

    private static string SanitizeForFolderName(string id)
    {
        // Mod ids in our schema are already restricted to [a-z0-9._-]; sanitise
        // defensively in case a malformed collection ships something exotic.
        var chars = id.Select(c => char.IsLetterOrDigit(c) || c == '.' || c == '-' || c == '_' ? c : '-').ToArray();
        return new string(chars);
    }
}

internal sealed record CollectionFetchEntry(string Id, string Version, string? Source);

/// <summary>
/// Result of <see cref="RemoteFetcher.FetchCollection"/>. On success, the temp
/// dir holds a collection-installable layout: the <c>*.collection.yaml</c>
/// manifest at the root, and a <c>mods/&lt;id&gt;/</c> folder per referenced
/// mod with its <c>mod.yaml</c> and patch files. <see cref="ModSources"/>
/// maps each mod id to the resolved <c>gh:...#&lt;sha&gt;/&lt;id&gt;</c>
/// origin so the lockfile post-processor can fill in the
/// <c>source</c> field per mod.
/// </summary>
public sealed class RemoteCollectionFetchResult
{
    public bool Success { get; init; }
    public string? TempDirectory { get; init; }
    public string? CollectionFilePath { get; init; }
    public string? ModsRoot { get; init; }
    public string? ResolvedCollectionSource { get; init; }
    public string? CollectionCommitSha { get; init; }
    public IReadOnlyDictionary<string, string> ModSources { get; init; } = new Dictionary<string, string>();
    public IReadOnlyList<ManagerDiagnostic> Diagnostics { get; init; } = Array.Empty<ManagerDiagnostic>();

    public static RemoteCollectionFetchResult Ok(
        string tempDir,
        string collectionPath,
        string modsRoot,
        string resolvedSource,
        string sha,
        IReadOnlyDictionary<string, string> modSources,
        IReadOnlyList<ManagerDiagnostic> diagnostics)
        => new()
        {
            Success = true,
            TempDirectory = tempDir,
            CollectionFilePath = collectionPath,
            ModsRoot = modsRoot,
            ResolvedCollectionSource = resolvedSource,
            CollectionCommitSha = sha,
            ModSources = modSources,
            Diagnostics = diagnostics,
        };

    public static RemoteCollectionFetchResult Failure(IReadOnlyList<ManagerDiagnostic> diagnostics)
        => new() { Success = false, Diagnostics = diagnostics };
}

/// <summary>
/// Result of <see cref="RemoteFetcher.FetchMod"/>. On success, the temp dir
/// holds a locally-installable mod tree (<c>mod.yaml</c> at root plus the
/// patch files it references); on failure, only diagnostics are populated.
/// </summary>
public sealed class RemoteFetchResult
{
    public bool Success { get; init; }
    public string? TempDirectory { get; init; }
    public string? ResolvedSource { get; init; }
    public string? CommitSha { get; init; }
    public string? ModFolder { get; init; }
    public IReadOnlyList<ManagerDiagnostic> Diagnostics { get; init; } = Array.Empty<ManagerDiagnostic>();

    public static RemoteFetchResult Ok(string tempDir, string resolvedSource, string sha, string modFolder, IReadOnlyList<ManagerDiagnostic> diagnostics)
        => new() { Success = true, TempDirectory = tempDir, ResolvedSource = resolvedSource, CommitSha = sha, ModFolder = modFolder, Diagnostics = diagnostics };

    public static RemoteFetchResult Failure(IReadOnlyList<ManagerDiagnostic> diagnostics)
        => new() { Success = false, Diagnostics = diagnostics };
}
