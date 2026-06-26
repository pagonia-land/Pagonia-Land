using System.Diagnostics.CodeAnalysis;
using YamlDotNet.Serialization;

namespace PagoniaLand.Manager;

/// <summary>
/// Reads a mod-distribution repo's top-level <c>index.yaml</c> into a
/// <see cref="RepoIndex"/> — the listable catalogue of mods and collections the
/// repo offers — without fetching or installing any of them.
///
/// <para>
/// <see cref="RemoteFetcher"/> reads the same index inline while resolving a
/// single install target and lists the available ids only inside an error
/// message when a lookup misses. This service exposes the whole index as a
/// standalone read so a caller (the interactive browse flow) can show what a
/// repo contains <em>before</em> the user commits to one. It resolves the ref
/// to a commit SHA so the listing matches a pinned snapshot, and honours the
/// repo's base path (the catalog repo-entry <c>indexPath</c>, i.e. the
/// <c>gh:owner/repo:base</c> short form) so a mod tree hosted in a subdirectory
/// resolves correctly.
/// </para>
/// </summary>
public sealed class RepoIndexFetcher
{
    // YamlDotNet reflects over these via Deserialize<RepoIndex>(). Pin them so
    // the AOT trimmer keeps their members — same rooting pattern as
    // RemoteFetcher, which parses the same model from the same index.yaml.
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
    public RepoIndexFetcher(IRemoteContentFetcher fetcher)
    {
        _fetcher = fetcher;
    }

    /// <summary>Synchronous bridge over <see cref="FetchAsync"/> for the CLI
    /// surface, mirroring <see cref="RemoteFetcher.FetchMod"/>.</summary>
    public RepoIndexFetchResult Fetch(GitHubSource source, CancellationToken cancellationToken = default)
        => FetchAsync(source, cancellationToken).GetAwaiter().GetResult();

    public async Task<RepoIndexFetchResult> FetchAsync(GitHubSource source, CancellationToken cancellationToken = default)
    {
        var diagnostics = new List<ManagerDiagnostic>();

        // Pin the ref to a concrete commit so the listing the user browses is a
        // stable snapshot, not a moving branch — the same discipline the install
        // path applies before it fetches files.
        string commitSha;
        try
        {
            var resolved = await _fetcher.ResolveCommitShaAsync(source.Owner, source.Repo, source.Ref, cancellationToken).ConfigureAwait(false);
            if (resolved is null)
            {
                diagnostics.Add(Error(ManagerDiagnosticCodes.RemoteFetchFailed,
                    $"Could not resolve ref '{source.Ref}' in {source.Owner}/{source.Repo}: ref not found."));
                return RepoIndexFetchResult.Failure(diagnostics);
            }
            commitSha = resolved;
        }
        catch (Exception ex)
        {
            diagnostics.Add(Error(ManagerDiagnosticCodes.RemoteFetchFailed,
                $"Resolving ref '{source.Ref}' in {source.Owner}/{source.Repo} failed: {ex.Message}"));
            return RepoIndexFetchResult.Failure(diagnostics);
        }

        diagnostics.Add(Info(ManagerDiagnosticCodes.RemoteResolvedToCommit,
            $"Resolved gh:{source.Owner}/{source.Repo}#{source.Ref} to commit {Shorten(commitSha)}."));

        // index.yaml lives under the repo's base path (empty = repo root).
        var indexUrl = RawUrl(source.Owner, source.Repo, commitSha, JoinRepoPath(source.BasePath, "index.yaml"));
        RemoteFetchedContent? indexContent;
        try
        {
            indexContent = await _fetcher.TryFetchAsync(indexUrl, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            diagnostics.Add(Error(ManagerDiagnosticCodes.RemoteFetchFailed,
                $"Fetching index.yaml failed: {ex.Message}"));
            return RepoIndexFetchResult.Failure(diagnostics);
        }

        if (indexContent is null)
        {
            // A repo without an index.yaml is a single-mod / path-only repo: it
            // can be installed from with an explicit mod path, but there's
            // nothing to enumerate. A successful read with no index — distinct
            // from a failure — so the caller can say "this repo doesn't publish
            // a browsable catalogue" rather than show an error.
            return RepoIndexFetchResult.NoIndex(commitSha, diagnostics);
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
            diagnostics.Add(Error(ManagerDiagnosticCodes.RemoteIndexMalformed,
                $"Could not parse index.yaml at {source.Owner}/{source.Repo}@{Shorten(commitSha)}: {ex.Message}"));
            return RepoIndexFetchResult.Failure(diagnostics);
        }

        // Format-version gate: a newer-minor index reads (with an info note); a newer/retired
        // major or malformed indexFormatVersion is refused so the browse list can't be built
        // from a structure this build can't trust.
        if (!FormatVersionGate.TryAcceptRepoIndex(index.IndexFormatVersion, diagnostics))
        {
            return RepoIndexFetchResult.Failure(diagnostics);
        }

        return RepoIndexFetchResult.Ok(index, commitSha, diagnostics);
    }

    private static string RawUrl(string owner, string repo, string sha, string path)
        => $"https://raw.githubusercontent.com/{owner}/{repo}/{sha}/{path}";

    private static string JoinRepoPath(string folder, string file)
    {
        if (string.IsNullOrEmpty(folder)) { return file; }
        return $"{folder.TrimEnd('/')}/{file}";
    }

    private static string Shorten(string sha) => sha.Length >= 8 ? sha[..8] : sha;

    private static ManagerDiagnostic Error(string code, string message)
        => new(ManagerDiagnosticSeverity.Error, code, message, null);

    private static ManagerDiagnostic Info(string code, string message)
        => new(ManagerDiagnosticSeverity.Info, code, message, null);
}

/// <summary>
/// Result of <see cref="RepoIndexFetcher.Fetch"/>. Three outcomes:
/// <list type="bullet">
///   <item><see cref="Success"/> + <see cref="HasIndex"/> — <see cref="Index"/>
///   holds the parsed catalogue (mods + collections).</item>
///   <item><see cref="Success"/> + not <see cref="HasIndex"/> — the repo
///   resolved but ships no <c>index.yaml</c>; <see cref="Index"/> is null.</item>
///   <item>not <see cref="Success"/> — the ref didn't resolve, the fetch
///   errored, or the index didn't parse; see <see cref="Diagnostics"/>.</item>
/// </list>
/// <see cref="CommitSha"/> is set on both successful outcomes.
/// </summary>
public sealed class RepoIndexFetchResult
{
    public bool Success { get; init; }
    public bool HasIndex { get; init; }
    public RepoIndex? Index { get; init; }
    public string? CommitSha { get; init; }
    public IReadOnlyList<ManagerDiagnostic> Diagnostics { get; init; } = Array.Empty<ManagerDiagnostic>();

    public static RepoIndexFetchResult Ok(RepoIndex index, string commitSha, IReadOnlyList<ManagerDiagnostic> diagnostics)
        => new() { Success = true, HasIndex = true, Index = index, CommitSha = commitSha, Diagnostics = diagnostics };

    public static RepoIndexFetchResult NoIndex(string commitSha, IReadOnlyList<ManagerDiagnostic> diagnostics)
        => new() { Success = true, HasIndex = false, Index = null, CommitSha = commitSha, Diagnostics = diagnostics };

    public static RepoIndexFetchResult Failure(IReadOnlyList<ManagerDiagnostic> diagnostics)
        => new() { Success = false, HasIndex = false, Index = null, Diagnostics = diagnostics };
}
