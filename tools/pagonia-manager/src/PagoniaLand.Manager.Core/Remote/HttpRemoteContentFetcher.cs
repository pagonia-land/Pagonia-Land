using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace PagoniaLand.Manager;

/// <summary>
/// Production <see cref="IRemoteContentFetcher"/> backed by
/// <see cref="HttpClient"/>. Wired up by the CLI when the user asks for a
/// real remote install; tests substitute the in-memory fake instead.
/// <para>
/// Owns an <see cref="HttpClient"/> instance; long-lived (the manager
/// process exits between installs, so connection-pool exhaustion isn't a
/// real concern). The User-Agent header is required by GitHub's API —
/// requests without it get rejected with 403.
/// </para>
/// </summary>
public sealed class HttpRemoteContentFetcher : IRemoteContentFetcher, IDisposable
{
    // Generous-but-finite cap on a single streamed download. Pak-bundled mods can
    // be large, but an unbounded stream from a misbehaving or malicious server
    // (endless body, download-level zip bomb) must not be able to fill the disk.
    private const long MaxDownloadBytes = 8L * 1024 * 1024 * 1024; // 8 GiB
    private const int CopyBufferBytes = 81920;

    private readonly HttpClient _client;

    public HttpRemoteContentFetcher(string userAgent)
    {
        _client = new HttpClient();
        // 120s caps any individual HTTP round-trip. The default HttpClient
        // timeout (100s) would let a dead server hang the interactive shell
        // for almost two minutes; 120s gives federated-catalog walks on
        // flaky-but-alive uplinks enough headroom while keeping a bounded
        // ceiling. With TryFetchAsync using ResponseContentRead the cap
        // bounds the WHOLE response (not just headers), so small catalog
        // YAMLs and mod.io JSON fit easily. The CancellationToken remains
        // the authoritative cancel mechanism when callers wire one through.
        _client.Timeout = TimeSpan.FromSeconds(120);
        _client.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent);
        // GitHub API responses for the commits endpoint are JSON by default;
        // pinning the Accept header to the v3 media type makes the surface
        // stable across future API rollouts.
        _client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
    }

    public async Task<RemoteFetchedContent?> TryFetchAsync(string url, CancellationToken cancellationToken)
    {
        using var response = await _client.GetAsync(url, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
        response.EnsureSuccessStatusCode();
        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        var text = Encoding.UTF8.GetString(bytes);
        return new RemoteFetchedContent(text, bytes);
    }

    public async Task<string?> ResolveCommitShaAsync(string owner, string repo, string @ref, CancellationToken cancellationToken)
    {
        // GitHub commits API: /repos/{owner}/{repo}/commits/{ref}. {ref} accepts
        // branch names, tag names, and commit SHAs (returns the same SHA in the
        // last case). For an unknown ref the API replies 404 — we surface that
        // as null and let the caller produce a remoteFetchFailed diagnostic.
        var url = $"https://api.github.com/repos/{owner}/{repo}/commits/{Uri.EscapeDataString(@ref)}";
        using var response = await _client.GetAsync(url, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.TryGetProperty("sha", out var shaEl) ? shaEl.GetString() : null;
    }

    public async Task<bool> TryStreamFetchAsync(string url, Stream destination, CancellationToken cancellationToken)
    {
        // HttpCompletionOption.ResponseHeadersRead lets us start writing to the
        // destination as soon as headers arrive, instead of buffering the whole
        // body in memory first. Critical for multi-GB pak-bundled mods.
        using var response = await _client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }
        response.EnsureSuccessStatusCode();

        // Manual bounded copy instead of CopyToAsync so we can enforce MaxDownloadBytes.
        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var buffer = new byte[CopyBufferBytes];
        long total = 0;
        int read;
        while ((read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
        {
            total += read;
            if (total > MaxDownloadBytes)
            {
                throw new IOException(
                    $"Download exceeded the {MaxDownloadBytes / (1024L * 1024 * 1024)} GiB size limit and was aborted.");
            }

            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
        }

        return true;
    }

    public void Dispose() => _client.Dispose();
}
