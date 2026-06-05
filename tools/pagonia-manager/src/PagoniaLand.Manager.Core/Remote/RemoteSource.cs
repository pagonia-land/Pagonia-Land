namespace PagoniaLand.Manager;

/// <summary>
/// A parsed remote-install source spec. Subtypes describe the per-transport
/// coordinates needed to fetch a mod. Today only GitHub repositories are
/// modelled; mod.io and direct-URL adapters land in later steps and add their
/// own subtypes here.
/// </summary>
public abstract record RemoteSource;

/// <summary>
/// A GitHub repository coordinate parsed from either
/// <c>gh:&lt;owner&gt;/&lt;repo&gt;[:&lt;base-path&gt;][#&lt;ref&gt;][/&lt;mod-id-or-path&gt;]</c>
/// or
/// <c>https://github.com/&lt;owner&gt;/&lt;repo&gt;/tree/&lt;ref&gt;[/&lt;mod-id-or-path&gt;]</c>.
/// <para>
/// <see cref="Ref"/> may be a branch, tag, or commit SHA — anything the GitHub
/// raw-content host accepts. <see cref="ModSpec"/> is the path component
/// after the ref; the fetcher tries to interpret it as a mod id first
/// (looked up in <c>index.yaml</c>) and falls back to a literal folder path.
/// A null <see cref="ModSpec"/> means the spec named a repo but no mod
/// inside it — the caller treats that as a user error.
/// </para>
/// <para>
/// <see cref="BasePath"/> is an optional repo-relative directory holding the
/// repo's <c>index.yaml</c> (and under which mod folders + patch paths resolve).
/// Empty = the index is at the repo root, the common case. It lets one repo host
/// a mod-distribution tree in a subfolder; the catalog repo-entry <c>indexPath</c>
/// field is its source. The short-form delimiter is <c>:</c>, attached to the
/// repo coordinate before the ref (<c>gh:owner/repo:sub/dir#ref/mod-id</c>), so
/// it never collides with the <c>#ref</c> or trailing <c>/mod-spec</c> segments.
/// </para>
/// </summary>
public sealed record GitHubSource(string Owner, string Repo, string Ref, string? ModSpec, string BasePath = "") : RemoteSource;

/// <summary>
/// An arbitrary HTTP(S) URL pointing at a downloadable mod ZIP. The URL
/// itself is the source identity — no auth, no commit-SHA pinning, no
/// registry lookup. Covers "modder posted a link in Discord" + serves as
/// the ZIP-download primitive the mod.io adapter reuses.
/// </summary>
/// <param name="Url">Original URL string as the user typed it.</param>
/// <param name="IsHttp">True when scheme is plain <c>http://</c>; surfaces an insecure-source warning unless explicitly opted in.</param>
public sealed record DirectUrlSource(string Url, bool IsHttp) : RemoteSource;

/// <summary>
/// A mod.io coordinate parsed from <c>modio:&lt;game&gt;/&lt;mod-id&gt;[#&lt;version&gt;]</c>.
/// <see cref="Game"/> is either a numeric mod.io game id ("1234") or a
/// human-readable slug ("pioneers-of-pagonia"); slug → numeric resolution
/// happens in <see cref="ModIoFetcher"/>.
/// <see cref="Version"/> is optional; null means "install latest modfile".
/// </summary>
public sealed record ModIoSource(string Game, string ModId, string? Version) : RemoteSource;
