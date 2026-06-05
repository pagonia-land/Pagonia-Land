namespace PagoniaLand.Manager;

/// <summary>
/// Transport-level abstraction over the HTTP GETs the manager issues to
/// fetch a remote mod. The interface exists so tests can run the full
/// remote-install pipeline without touching the network: the test impl
/// (<c>InMemoryRemoteContentFetcher</c> in the test project) returns
/// canned responses from a dictionary; production uses
/// <see cref="HttpRemoteContentFetcher"/> against GitHub.
/// </summary>
public interface IRemoteContentFetcher
{
    /// <summary>
    /// Issue a single GET against <paramref name="url"/>. Returns the response
    /// body on 2xx, null on 404. Other transport / HTTP errors throw — they
    /// indicate a problem the caller needs to surface as a remote-fetch
    /// diagnostic rather than silently treat as missing.
    /// </summary>
    Task<RemoteFetchedContent?> TryFetchAsync(string url, CancellationToken cancellationToken);

    /// <summary>
    /// Resolve a branch / tag / commit-ish <paramref name="ref"/> to a full
    /// commit SHA on GitHub. Returns null when the ref doesn't exist on the
    /// repo (e.g. a typo'd branch name). Lets the manager pin a fetched
    /// install to the exact commit it actually read, so the sidecar's
    /// <c>Source</c> field doesn't lie when a branch moves.
    /// </summary>
    Task<string?> ResolveCommitShaAsync(string owner, string repo, string @ref, CancellationToken cancellationToken);

    /// <summary>
    /// Stream the response body of a GET against <paramref name="url"/> into
    /// <paramref name="destination"/>. Returns true on 2xx, false on 404,
    /// throws on other transport / HTTP errors. Used by the direct-URL ZIP
    /// fetcher and by the mod.io adapter so multi-GB archives don't have
    /// to materialise in memory before being written to disk.
    /// </summary>
    Task<bool> TryStreamFetchAsync(string url, Stream destination, CancellationToken cancellationToken);
}

/// <summary>
/// Result of a single content fetch. Carries both the text and the raw bytes
/// because the manager hashes archives by bytes but parses YAML by text.
/// </summary>
public sealed record RemoteFetchedContent(string Text, byte[] Bytes);
