using System.IO.Compression;
using System.Security.Cryptography;

namespace PagoniaLand.Manager;

/// <summary>
/// Downloads a mod ZIP from any HTTP(S) URL, hashes it on the fly,
/// extracts into a temp directory with a per-entry path-traversal guard,
/// and returns the temp directory path so the existing
/// <see cref="ModInstaller"/> can install it unchanged. The mod.io adapter
/// reuses the same machinery — mod.io's API returns a pre-signed direct
/// download URL, which is exactly what this fetcher expects.
/// </summary>
public sealed class DirectUrlFetcher
{
    private const int CopyBufferBytes = 81920;
    private readonly IRemoteContentFetcher _http;

    public DirectUrlFetcher(IRemoteContentFetcher http)
    {
        _http = http;
    }

    public DirectUrlFetchResult Fetch(DirectUrlSource source, CancellationToken cancellationToken = default)
        => FetchAsync(source, cancellationToken).GetAwaiter().GetResult();

    public async Task<DirectUrlFetchResult> FetchAsync(DirectUrlSource source, CancellationToken cancellationToken = default)
    {
        var diagnostics = new List<ManagerDiagnostic>();
        var tempRoot = Path.Combine(Path.GetTempPath(), $"pagonia-direct-url-{Guid.NewGuid():N}");
        var tempZipPath = Path.Combine(tempRoot, "archive.zip");
        var extractedRoot = Path.Combine(tempRoot, "extracted");
        string sha256Hex;
        long archiveLength;

        try
        {
            Directory.CreateDirectory(tempRoot);

            // Stream the response body into a temp file while hashing on the fly.
            // SHA-256 over the same bytes that land on disk — no second pass.
            using (var sha = SHA256.Create())
            using (var fileStream = new FileStream(tempZipPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, CopyBufferBytes, useAsync: true))
            using (var cryptoStream = new CryptoStream(fileStream, sha, CryptoStreamMode.Write))
            {
                bool found;
                try
                {
                    found = await _http.TryStreamFetchAsync(source.Url, cryptoStream, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    TryDelete(tempRoot);
                    diagnostics.Add(Error(ManagerDiagnosticCodes.DirectUrlFetchFailed,
                        $"Fetching '{source.Url}' failed: {ex.Message}"));
                    return DirectUrlFetchResult.Failure(diagnostics);
                }

                if (!found)
                {
                    TryDelete(tempRoot);
                    diagnostics.Add(Error(ManagerDiagnosticCodes.DirectUrlFetchFailed,
                        $"No content at '{source.Url}' (HTTP 404)."));
                    return DirectUrlFetchResult.Failure(diagnostics);
                }

                cryptoStream.FlushFinalBlock();
                sha256Hex = Convert.ToHexString(sha.Hash!).ToLowerInvariant();
                archiveLength = fileStream.Length;
            }

            diagnostics.Add(Info(ManagerDiagnosticCodes.DirectUrlFetched,
                $"Fetched {FormatSize(archiveLength)} from '{source.Url}' (sha256={sha256Hex[..16]}...)."));

            // Extract with a per-entry path-traversal guard. The .NET ZipFile
            // helpers added a built-in check in newer runtimes, but doing it
            // explicitly here keeps the behaviour stable across runtimes and
            // gives us a precise diagnostic instead of an opaque IO exception.
            Directory.CreateDirectory(extractedRoot);
            try
            {
                if (!TryExtractZipSafely(tempZipPath, extractedRoot, diagnostics))
                {
                    TryDelete(tempRoot);
                    return DirectUrlFetchResult.Failure(diagnostics);
                }
            }
            catch (Exception ex)
            {
                TryDelete(tempRoot);
                diagnostics.Add(Error(ManagerDiagnosticCodes.DirectUrlFetchFailed,
                    $"Extracting archive from '{source.Url}' failed: {ex.Message}"));
                return DirectUrlFetchResult.Failure(diagnostics);
            }
            finally
            {
                // Drop the ZIP itself — the extracted tree is all the installer
                // needs, and these can be hundreds of MBs on real mods.
                TryDeleteFile(tempZipPath);
            }

            // Nested-folder detection: many distributed mod ZIPs ship with a
            // single top-level folder (e.g. "my-mod-v1.0/mod.yaml"). If we see
            // that shape, treat the inner folder as the mod root so the
            // existing ModInstaller doesn't need a special "is the mod nested?"
            // mode.
            var modRoot = ResolveModRoot(extractedRoot);

            // Synthesise the canonical source identifier the install sidecar
            // records: `url:<url>#<sha>` — so a re-install months later can
            // compare the new archive's hash against this baseline and warn
            // on drift (manager.directUrlArchiveDrift).
            var resolvedSource = $"url:{source.Url}#{sha256Hex}";

            return DirectUrlFetchResult.Ok(tempRoot, modRoot, resolvedSource, sha256Hex, archiveLength, diagnostics);
        }
        catch (Exception ex)
        {
            TryDelete(tempRoot);
            diagnostics.Add(Error(ManagerDiagnosticCodes.DirectUrlFetchFailed,
                $"Direct-URL fetch from '{source.Url}' failed: {ex.Message}"));
            return DirectUrlFetchResult.Failure(diagnostics);
        }
    }

    private static bool TryExtractZipSafely(string zipPath, string destinationRoot, List<ManagerDiagnostic> diagnostics)
    {
        // Normalise the destination root once. The traversal guard compares
        // the canonical destination path against this prefix.
        var rootFull = Path.GetFullPath(destinationRoot);
        var rootWithSeparator = rootFull.EndsWith(Path.DirectorySeparatorChar)
            ? rootFull
            : rootFull + Path.DirectorySeparatorChar;

        using var archive = ZipFile.OpenRead(zipPath);
        foreach (var entry in archive.Entries)
        {
            // ZipArchive surfaces directory entries as entries with an empty
            // Name + non-empty FullName ending in '/'. ExtractToFile would
            // throw on them; skip explicitly.
            if (string.IsNullOrEmpty(entry.Name))
            {
                continue;
            }

            // Build the destination path. Path.Combine + GetFullPath
            // canonicalises any '..' segments embedded in entry.FullName so
            // the prefix check sees the post-traversal path, not the
            // pre-traversal one.
            var unsanitisedDest = Path.Combine(destinationRoot, entry.FullName.Replace('/', Path.DirectorySeparatorChar));
            string destFull;
            try
            {
                destFull = Path.GetFullPath(unsanitisedDest);
            }
            catch (Exception ex)
            {
                diagnostics.Add(Error(ManagerDiagnosticCodes.DirectUrlTraversalRefused,
                    $"Archive entry '{entry.FullName}' has an unresolvable path: {ex.Message}"));
                return false;
            }

            if (!destFull.StartsWith(rootWithSeparator, StringComparison.Ordinal)
                && !string.Equals(destFull, rootFull, StringComparison.Ordinal))
            {
                diagnostics.Add(Error(ManagerDiagnosticCodes.DirectUrlTraversalRefused,
                    $"Archive entry '{entry.FullName}' tries to escape the extraction root — refused."));
                return false;
            }

            var destDir = Path.GetDirectoryName(destFull);
            if (!string.IsNullOrEmpty(destDir))
            {
                Directory.CreateDirectory(destDir);
            }

            // overwrite: true so partial / interrupted extracts don't leave the
            // temp tree in a broken half-extracted state on retry. The temp
            // root is unique per fetch anyway, so no real overwrite happens in
            // a single happy-path run.
            entry.ExtractToFile(destFull, overwrite: true);
        }
        return true;
    }

    private static string ResolveModRoot(string extractedRoot)
    {
        // If the extracted tree has exactly one top-level subdirectory and no
        // top-level files, drill in once. That's the common shape from GitHub-
        // Release / mod.io ZIPs (which wrap the mod folder one level deep).
        var topEntries = Directory.GetFileSystemEntries(extractedRoot);
        if (topEntries.Length == 1 && Directory.Exists(topEntries[0]))
        {
            return topEntries[0];
        }
        return extractedRoot;
    }

    private static string FormatSize(long bytes)
    {
        return bytes switch
        {
            < 1024 => $"{bytes}B",
            < 1024 * 1024 => $"{bytes / 1024.0:F1}KB",
            < 1024L * 1024 * 1024 => $"{bytes / (1024.0 * 1024):F1}MB",
            _ => $"{bytes / (1024.0 * 1024 * 1024):F1}GB",
        };
    }

    private static void TryDelete(string dir)
    {
        try { if (Directory.Exists(dir)) { Directory.Delete(dir, recursive: true); } }
        catch { /* best effort */ }
    }

    private static void TryDeleteFile(string path)
    {
        try { if (File.Exists(path)) { File.Delete(path); } }
        catch { /* best effort */ }
    }

    private static ManagerDiagnostic Error(string code, string message)
        => new(ManagerDiagnosticSeverity.Error, code, message, null);

    private static ManagerDiagnostic Info(string code, string message)
        => new(ManagerDiagnosticSeverity.Info, code, message, null);
}

/// <summary>
/// Result of <see cref="DirectUrlFetcher.FetchAsync"/>. On success, the
/// <see cref="ModRootDirectory"/> is laid out as a normal local mod folder
/// (<c>mod.yaml</c> at root) ready for <see cref="ModInstaller.Install"/>.
/// On failure, only diagnostics are populated and the temp dir is already
/// cleaned up.
/// </summary>
public sealed class DirectUrlFetchResult
{
    public bool Success { get; init; }
    public string? TempRoot { get; init; }
    public string? ModRootDirectory { get; init; }
    public string? ResolvedSource { get; init; }
    public string? ArchiveSha256 { get; init; }
    public long ArchiveLength { get; init; }
    public IReadOnlyList<ManagerDiagnostic> Diagnostics { get; init; } = Array.Empty<ManagerDiagnostic>();

    public static DirectUrlFetchResult Ok(
        string tempRoot,
        string modRoot,
        string resolvedSource,
        string sha256,
        long length,
        IReadOnlyList<ManagerDiagnostic> diagnostics)
        => new()
        {
            Success = true,
            TempRoot = tempRoot,
            ModRootDirectory = modRoot,
            ResolvedSource = resolvedSource,
            ArchiveSha256 = sha256,
            ArchiveLength = length,
            Diagnostics = diagnostics,
        };

    public static DirectUrlFetchResult Failure(IReadOnlyList<ManagerDiagnostic> diagnostics)
        => new() { Success = false, Diagnostics = diagnostics };
}
