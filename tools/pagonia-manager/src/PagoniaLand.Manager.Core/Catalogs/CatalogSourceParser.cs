using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;

namespace PagoniaLand.Manager;

/// <summary>
/// Parses catalog source specs into <see cref="CatalogSource"/> records.
/// Recognised forms:
/// <list type="bullet">
///   <item><c>gh:&lt;owner&gt;/&lt;repo&gt;[#&lt;ref&gt;][/&lt;path&gt;]</c> — GitHub-hosted catalog. Path defaults to <c>catalog.yaml</c>.</item>
///   <item><c>https://example.com/path/catalog.yaml</c> — raw HTTP(S) URL for self-hosted catalogs (GitLab Pages, S3, generic web host).</item>
///   <item><c>http://example.com/path/catalog.yaml</c> — plain HTTP. Surfaces a warning unless <c>state.yaml.allowInsecureCatalogSources: true</c>.</item>
///   <item><c>file:///absolute/path/catalog.yaml</c> — explicit file URL.</item>
///   <item><c>file:./relative/path/catalog.yaml</c> — relative file URL resolved against the working directory.</item>
///   <item><c>file:absolute-or-relative-path</c> — short form of the above two.</item>
/// </list>
/// </summary>
public static class CatalogSourceParser
{
    private const string GitHubPrefix = "gh:";
    private const string FileUrlPrefix = "file://";
    private const string FileShortPrefix = "file:";
    private const string HttpsPrefix = "https://";
    private const string HttpPrefix = "http://";
    private const string DefaultRef = "HEAD";
    private const string DefaultCatalogFileName = "catalog.yaml";

    public static bool TryParse(string? spec, [NotNullWhen(true)] out CatalogSource? source)
    {
        source = null;
        if (string.IsNullOrWhiteSpace(spec))
        {
            return false;
        }

        spec = spec.Trim();

        if (spec.StartsWith(GitHubPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return TryParseGitHub(spec[GitHubPrefix.Length..], out source);
        }

        if (spec.StartsWith(HttpsPrefix, StringComparison.OrdinalIgnoreCase)
            || spec.StartsWith(HttpPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return TryParseUrl(spec, out source);
        }

        if (spec.StartsWith(FileUrlPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return TryParseFile(spec[FileUrlPrefix.Length..], out source);
        }

        if (spec.StartsWith(FileShortPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return TryParseFile(spec[FileShortPrefix.Length..], out source);
        }

        return false;
    }

    private static bool TryParseUrl(string spec, [NotNullWhen(true)] out CatalogSource? source)
    {
        source = null;
        if (!Uri.TryCreate(spec, UriKind.Absolute, out var uri))
        {
            return false;
        }
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
        {
            return false;
        }
        if (string.IsNullOrEmpty(uri.Host))
        {
            return false;
        }
        // SSRF guard: no legitimate catalog lives on loopback or a link-local address
        // (the 169.254.169.254 cloud-metadata endpoint being the classic target). Refuse
        // those; private LAN ranges stay allowed for legitimate internal mirrors.
        if (IsBlockedHost(uri))
        {
            return false;
        }
        source = new UrlCatalogSource(uri);
        return true;
    }

    private static bool IsBlockedHost(Uri uri)
    {
        if (uri.IsLoopback)
        {
            return true;
        }
        if (IPAddress.TryParse(uri.Host, out var ip))
        {
            if (IPAddress.IsLoopback(ip))
            {
                return true;
            }
            var bytes = ip.GetAddressBytes();
            if (ip.AddressFamily == AddressFamily.InterNetwork && bytes[0] == 169 && bytes[1] == 254)
            {
                return true; // IPv4 link-local 169.254/16
            }
            if (ip.AddressFamily == AddressFamily.InterNetworkV6 && ip.IsIPv6LinkLocal)
            {
                return true; // IPv6 link-local fe80::/10
            }
        }
        return false;
    }

    private static bool TryParseGitHub(string rest, [NotNullWhen(true)] out CatalogSource? source)
    {
        // Layout: <owner>/<repo>[#<ref>][/<path>]
        // Mirrors RemoteSourceParser's gh: layout but the "mod-spec" segment
        // here is interpreted as a file path inside the repo (with a
        // catalog.yaml default).
        source = null;

        var slash = rest.IndexOf('/');
        if (slash <= 0 || slash == rest.Length - 1)
        {
            return false;
        }

        var owner = rest[..slash];
        var remainder = rest[(slash + 1)..];

        string repo;
        string? refSpec = null;
        string? pathSpec = null;

        var hash = remainder.IndexOf('#');
        if (hash >= 0)
        {
            repo = remainder[..hash];
            var afterHash = remainder[(hash + 1)..];
            var slashAfterHash = afterHash.IndexOf('/');
            if (slashAfterHash >= 0)
            {
                refSpec = afterHash[..slashAfterHash];
                pathSpec = afterHash[(slashAfterHash + 1)..];
            }
            else
            {
                refSpec = afterHash;
            }
        }
        else
        {
            var slashAfterRepo = remainder.IndexOf('/');
            if (slashAfterRepo >= 0)
            {
                repo = remainder[..slashAfterRepo];
                pathSpec = remainder[(slashAfterRepo + 1)..];
            }
            else
            {
                repo = remainder;
            }
        }

        if (!IsValidOwnerOrRepo(owner) || !IsValidOwnerOrRepo(repo))
        {
            return false;
        }

        if (refSpec is { Length: 0 } || pathSpec is { Length: 0 })
        {
            return false;
        }

        // Defence in depth before any fetch: reject path-traversal / absolute /
        // scheme'd path specs at parse time, the same grounds RemoteSourceParser
        // rejects a base path on. Otherwise 'gh:o/r#ref/../../etc' would flow
        // into the raw.githubusercontent.com URL unsanitised.
        if (pathSpec is not null && !IsValidRepoPath(pathSpec))
        {
            return false;
        }

        source = new GitHubCatalogSource(owner, repo, refSpec ?? DefaultRef, pathSpec ?? DefaultCatalogFileName);
        return true;
    }

    private static bool IsValidRepoPath(string value)
    {
        // A safe repo-relative file path: no absolute paths, no backslashes, no
        // drive/scheme colon, no '.'/'..' segments. Mirrors
        // RemoteSourceParser.IsValidBasePath.
        if (value.Length == 0 || value[0] == '/' || value[^1] == '/'
            || value.Contains('\\') || value.Contains(':'))
        {
            return false;
        }
        foreach (var segment in value.Split('/'))
        {
            if (segment.Length == 0 || segment == "." || segment == "..")
            {
                return false;
            }
            foreach (var c in segment)
            {
                if (!(char.IsLetterOrDigit(c) || c == '-' || c == '_' || c == '.'))
                {
                    return false;
                }
            }
        }
        return true;
    }

    private static bool TryParseFile(string rest, [NotNullWhen(true)] out CatalogSource? source)
    {
        source = null;
        if (string.IsNullOrWhiteSpace(rest))
        {
            return false;
        }

        // Strip a leading "/" that might come from the file:// triple-slash
        // form on Windows (file:///C:/path -> /C:/path).
        if (rest.StartsWith('/') && rest.Length > 1 && rest[2] == ':')
        {
            rest = rest[1..];
        }

        // Normalise to an absolute path. Relative paths resolve against the
        // current working directory at parse time. Callers that need a
        // workspace-relative resolve (e.g. a parent catalog.yaml referencing
        // file:./catalogs/sub.yaml) resolve the path themselves before parse.
        var absolute = Path.GetFullPath(rest);
        source = new FileCatalogSource(absolute);
        return true;
    }

    /// <summary>
    /// Resolve a <c>file:</c> reference embedded inside another catalog
    /// against that parent catalog's location. Lets a parent at
    /// <c>/x/y/parent.yaml</c> declare <c>file:./sub/child.yaml</c> and have
    /// it resolve to <c>/x/y/sub/child.yaml</c> regardless of where the
    /// process's working directory points.
    /// </summary>
    public static bool TryParseRelativeTo(string spec, string parentDirectory, [NotNullWhen(true)] out CatalogSource? source)
    {
        source = null;
        if (string.IsNullOrWhiteSpace(spec))
        {
            return false;
        }

        var trimmed = spec.Trim();

        // gh:, http(s)://, and file:// (absolute URL form) ignore the parent dir.
        if (trimmed.StartsWith(GitHubPrefix, StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith(HttpsPrefix, StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith(HttpPrefix, StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith(FileUrlPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return TryParse(trimmed, out source);
        }

        if (trimmed.StartsWith(FileShortPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var pathPart = trimmed[FileShortPrefix.Length..];
            if (string.IsNullOrWhiteSpace(pathPart)) { return false; }
            var resolved = Path.IsPathRooted(pathPart)
                ? pathPart
                : Path.Combine(parentDirectory, pathPart);
            source = new FileCatalogSource(Path.GetFullPath(resolved));
            return true;
        }

        return false;
    }

    private static bool IsValidOwnerOrRepo(string value)
    {
        if (string.IsNullOrEmpty(value)) { return false; }
        foreach (var c in value)
        {
            if (!(char.IsLetterOrDigit(c) || c == '-' || c == '_' || c == '.')) { return false; }
        }
        return true;
    }
}
