using System.Diagnostics.CodeAnalysis;

namespace PagoniaLand.Manager;

/// <summary>
/// Parses remote-install source specs into <see cref="RemoteSource"/> records.
/// Two shorthand forms are recognised for GitHub-hosted repos:
/// <list type="bullet">
///   <item><c>gh:&lt;owner&gt;/&lt;repo&gt;[#&lt;ref&gt;][/&lt;mod-id-or-path&gt;]</c></item>
///   <item><c>https://github.com/&lt;owner&gt;/&lt;repo&gt;/tree/&lt;ref&gt;[/&lt;mod-id-or-path&gt;]</c></item>
/// </list>
/// The long URL form is the one a user gets from clicking "Copy link" on
/// a folder in the GitHub web UI; the short form is what they'd type by
/// hand. Both produce the same <see cref="GitHubSource"/> record.
/// </summary>
public static class RemoteSourceParser
{
    private const string GitHubShortPrefix = "gh:";
    private const string GitHubLongPrefix = "https://github.com/";
    private const string ModIoPrefix = "modio:";
    private const string DefaultRef = "HEAD";

    /// <summary>
    /// Try to parse <paramref name="spec"/> as a remote source. Returns true
    /// on a successful parse and sets <paramref name="source"/>; returns false
    /// and leaves <paramref name="source"/> null when the spec is not a
    /// recognised remote form (in which case the caller falls back to
    /// treating <paramref name="spec"/> as a local path).
    /// </summary>
    public static bool TryParse(string? spec, [NotNullWhen(true)] out RemoteSource? source)
    {
        source = null;
        if (string.IsNullOrWhiteSpace(spec))
        {
            return false;
        }

        if (spec.StartsWith(GitHubShortPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return TryParseShortForm(spec[GitHubShortPrefix.Length..], out source);
        }

        if (spec.StartsWith(ModIoPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return TryParseModIo(spec[ModIoPrefix.Length..], out source);
        }

        if (spec.StartsWith(GitHubLongPrefix, StringComparison.OrdinalIgnoreCase))
        {
            // GitHub long-form (.../tree/<ref>/...) takes priority. If parsing
            // fails (e.g. a github.com URL that's actually an archive download
            // like /archive/refs/heads/main.zip) we fall through to direct-URL
            // parsing below — that catches the .zip-suffix path while still
            // letting genuine /tree/ URLs land in the GitHubSource branch.
            if (TryParseLongForm(spec[GitHubLongPrefix.Length..], out source))
            {
                return true;
            }
        }

        // Direct-URL fallthrough. Accepts https:// and http:// URLs that
        // point at a downloadable archive (.zip suffix on the URL path,
        // before any query string). Any other URL — repo landing pages,
        // documentation links, etc. — fails to parse and the caller falls back
        // to treating the spec as a local path.
        if (spec.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            || spec.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
        {
            return TryParseDirectUrl(spec, out source);
        }

        return false;
    }

    private static bool TryParseDirectUrl(string spec, [NotNullWhen(true)] out RemoteSource? source)
    {
        source = null;
        if (!Uri.TryCreate(spec, UriKind.Absolute, out var uri))
        {
            return false;
        }
        if (uri.Scheme is not ("http" or "https"))
        {
            return false;
        }

        // .zip-suffix gate. Without it, every web URL the user types looks
        // like a candidate direct-URL source, including obvious mistakes
        // (repo landing pages, blog posts, etc.). The path is checked
        // pre-query-string so mod.io's signed download URLs
        // (...mod.zip?signature=...) still parse cleanly.
        var path = uri.AbsolutePath;
        if (!path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        source = new DirectUrlSource(spec, IsHttp: string.Equals(uri.Scheme, "http", StringComparison.OrdinalIgnoreCase));
        return true;
    }

    private static readonly char[] RepoDelimiters = { ':', '#', '/' };

    private static bool TryParseShortForm(string rest, [NotNullWhen(true)] out RemoteSource? source)
    {
        // Layout: <owner>/<repo>[:<base-path>][#<ref>][/<mod-spec>]
        // The base-path is an optional repo-relative directory holding the
        // repo's index.yaml (the catalog indexPath's hand-typed equivalent).
        // The ':' delimiter is chosen so it never collides with the repo name
        // or the '#ref' segment.
        source = null;

        var slash = rest.IndexOf('/');
        if (slash <= 0 || slash == rest.Length - 1)
        {
            return false;
        }

        var owner = rest[..slash];
        var remainder = rest[(slash + 1)..];

        string repo;
        string? basePath = null;
        string? refSpec = null;
        string? modSpec = null;

        var repoEnd = remainder.IndexOfAny(RepoDelimiters);
        if (repoEnd < 0)
        {
            repo = remainder;
        }
        else
        {
            repo = remainder[..repoEnd];
            var tail = remainder[repoEnd..];

            if (tail[0] == ':')
            {
                // Base-path + mod-spec are both '/'-delimited paths, so the split
                // can't be "first '/'". With an explicit '#ref' the base ends
                // there; without one, the base is the directory and the mod-spec
                // is the FINAL '/'-segment (a base implies an index.yaml subtree,
                // so the mod-spec is a single-token id — a base never coexists
                // with a multi-segment path mod-spec in practice). This keeps the
                // ergonomic 'gh:owner/repo:official-mods/<mod-id>' form working
                // while still supporting a nested base path.
                var afterColon = tail[1..];
                var hash = afterColon.IndexOf('#');
                if (hash >= 0)
                {
                    basePath = afterColon[..hash];
                    tail = afterColon[hash..]; // starts with '#'
                }
                else
                {
                    var lastSlash = afterColon.LastIndexOf('/');
                    if (lastSlash >= 0)
                    {
                        basePath = afterColon[..lastSlash];
                        modSpec = afterColon[(lastSlash + 1)..];
                    }
                    else
                    {
                        basePath = afterColon;
                    }
                    tail = string.Empty;
                }
            }

            if (tail.Length > 0 && tail[0] == '#')
            {
                // The ref runs until the next '/' (which begins the mod-spec)
                // or end-of-string.
                var afterHash = tail[1..];
                var end = afterHash.IndexOf('/');
                refSpec = end < 0 ? afterHash : afterHash[..end];
                tail = end < 0 ? string.Empty : afterHash[end..];
            }

            if (tail.Length > 0 && tail[0] == '/')
            {
                modSpec = tail[1..];
            }
        }

        if (!IsValidOwnerOrRepo(owner) || !IsValidOwnerOrRepo(repo))
        {
            return false;
        }

        // A bare 'repo:', 'repo#', or trailing '/' leaves a zero-length segment —
        // a user typo, not a valid empty value.
        if (basePath is { Length: 0 } || refSpec is { Length: 0 } || modSpec is { Length: 0 })
        {
            return false;
        }

        if (basePath is not null && !IsValidBasePath(basePath))
        {
            return false;
        }

        if (modSpec is not null && !IsSafeModSpec(modSpec))
        {
            return false;
        }

        source = new GitHubSource(owner, repo, refSpec ?? DefaultRef, modSpec, basePath ?? string.Empty);
        return true;
    }

    private static bool IsSafeModSpec(string value)
    {
        // Reject path traversal at parse time, before any fetch. RemoteFetcher
        // also guards the joined path, but only after a network SHA-resolution
        // round-trip — this catches 'owner/repo/../../evil' early and keeps the
        // mod-spec consistent with the base-path defence. Deliberately narrow
        // (traversal / absolute / backslash only) so legitimate dotted-kebab mod
        // ids and nested 'mods/<id>' specs still parse.
        if (value.Length == 0 || value[0] == '/' || value.Contains('\\'))
        {
            return false;
        }
        foreach (var segment in value.Split('/'))
        {
            if (segment == "..")
            {
                return false;
            }
        }
        return true;
    }

    private static bool IsValidBasePath(string value)
    {
        // A safe repo-relative directory: no absolute paths, no backslashes, no
        // drive/scheme colon, no '.'/'..' segments. Mirrors the catalog schema's
        // indexPath pattern so a hand-typed spec is rejected on the same grounds
        // a malformed catalog entry would be (defence in depth before any fetch).
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

    private static bool TryParseLongForm(string rest, [NotNullWhen(true)] out RemoteSource? source)
    {
        // Layout: <owner>/<repo>/tree/<ref>[/<mod-spec>]
        // The "/tree/" segment is the marker GitHub uses for branch/tag refs in
        // the web UI; the URL a user copies from the address bar always has it.
        source = null;

        var parts = rest.Split('/');
        if (parts.Length < 4 || parts[2] != "tree")
        {
            return false;
        }

        var owner = parts[0];
        var repo = parts[1];
        var refSpec = parts[3];
        var modSpec = parts.Length > 4 ? string.Join('/', parts, 4, parts.Length - 4) : null;

        if (!IsValidOwnerOrRepo(owner) || !IsValidOwnerOrRepo(repo) || string.IsNullOrEmpty(refSpec))
        {
            return false;
        }

        if (modSpec is { Length: 0 })
        {
            modSpec = null;
        }

        if (modSpec is not null && !IsSafeModSpec(modSpec))
        {
            return false;
        }

        source = new GitHubSource(owner, repo, refSpec, modSpec);
        return true;
    }

    private static bool IsValidOwnerOrRepo(string value)
    {
        // GitHub owner / repo names: alphanumerics, hyphens, underscores, dots.
        // Cannot be empty. We don't enforce GitHub's exact rule set here — the
        // raw-content host will reject anything truly invalid; we just want to
        // catch obvious garbage before issuing a network request.
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        foreach (var c in value)
        {
            if (!(char.IsLetterOrDigit(c) || c == '-' || c == '_' || c == '.'))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryParseModIo(string rest, [NotNullWhen(true)] out RemoteSource? source)
    {
        // Layout: <game>/<mod-id>[#<version>]
        //   <game>   = numeric mod.io game id ("1234") OR slug
        //              ("pioneers-of-pagonia"); slugs resolve to numeric ids
        //              in ModIoFetcher, which surfaces
        //              manager.modIoUnknownGameAlias on lookup failure.
        //   <mod-id> = numeric mod.io mod id. Slug support for mods is a
        //              future extension; mod.io's primary key is the numeric
        //              id and the API doesn't accept slugs in the
        //              /mods/{mod-id} endpoint.
        //   <version> = optional. Null means "install the latest modfile".
        source = null;

        var slash = rest.IndexOf('/');
        if (slash <= 0 || slash == rest.Length - 1)
        {
            return false;
        }

        var game = rest[..slash];
        var remainder = rest[(slash + 1)..];

        string modId;
        string? version = null;

        var hash = remainder.IndexOf('#');
        if (hash >= 0)
        {
            modId = remainder[..hash];
            version = remainder[(hash + 1)..];
            if (version.Length == 0) { return false; }
        }
        else
        {
            modId = remainder;
        }

        if (!IsValidModIoSegment(game) || !IsValidModIoSegment(modId))
        {
            return false;
        }

        source = new ModIoSource(game, modId, version);
        return true;
    }

    private static bool IsValidModIoSegment(string value)
    {
        // mod.io permits alphanumerics, hyphens, and underscores in slugs;
        // numeric ids are pure digits. The intersection is what we accept here —
        // anything outside (slashes, spaces, query-string chars) gets rejected
        // before a network round-trip.
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }
        foreach (var c in value)
        {
            if (!(char.IsLetterOrDigit(c) || c == '-' || c == '_'))
            {
                return false;
            }
        }
        return true;
    }
}
