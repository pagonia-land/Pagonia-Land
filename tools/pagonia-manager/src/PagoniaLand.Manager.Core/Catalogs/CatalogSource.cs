namespace PagoniaLand.Manager;

/// <summary>
/// A parsed catalog source — where the manager fetches a <c>catalog.yaml</c>
/// from. Transport-agnostic by design: GitHub-hosted catalogs are the primary
/// case, but local file paths are a first-class subtype because tests, offline
/// workshops, and LAN-hosted classroom catalogs all need them. Future
/// adapters (raw HTTP, mod.io) plug in as additional subtypes.
/// </summary>
public abstract record CatalogSource
{
    /// <summary>
    /// Stable canonical string used for cycle-detection visited-set lookup
    /// + cache directory naming + display. Two source instances that resolve
    /// to the same canonical string represent the same catalog.
    /// </summary>
    public abstract string Canonical { get; }
}

/// <summary>
/// A GitHub-hosted catalog at
/// <c>gh:&lt;Owner&gt;/&lt;Repo&gt;[#&lt;Ref&gt;][/&lt;Path&gt;]</c>.
/// Defaults: <see cref="Ref"/> = "HEAD", <see cref="Path"/> = "catalog.yaml".
/// </summary>
public sealed record GitHubCatalogSource(string Owner, string Repo, string Ref, string Path) : CatalogSource
{
    public override string Canonical
    {
        get
        {
            // Include the ref only when it's pinned to something other than the
            // default HEAD. A plainly-typed `gh:owner/repo/path` and the same
            // with an explicit `#HEAD` then canonicalise identically, and the
            // displayed form matches what the user actually typed (no injected
            // `#HEAD`). A pinned branch / tag / SHA stays explicit so two
            // subscriptions on different refs of the same repo+path remain
            // distinct catalogs (which they are). The path always carries the
            // ref/path boundary unambiguously: a ref only ever follows '#'.
            var refSegment = string.Equals(Ref, "HEAD", StringComparison.Ordinal) ? string.Empty : $"#{Ref}";
            return $"gh:{Owner}/{Repo}{refSegment}/{Path}";
        }
    }
}

/// <summary>
/// A local-file catalog. Primary use cases: tests, offline workshops, LAN
/// catalogs, the bundled <c>examples/mod-catalog-example/</c>. The path is
/// normalised to an absolute, posix-separator form for the canonical string
/// so the same file referenced two different ways (forward vs back slashes,
/// relative vs absolute) only counts once.
/// </summary>
public sealed record FileCatalogSource(string AbsolutePath) : CatalogSource
{
    public override string Canonical => $"file://{AbsolutePath.Replace('\\', '/')}";
}

/// <summary>
/// A catalog served over raw HTTP(S) — for catalog publishers who self-host
/// outside GitHub (corporate intranet, GitLab Pages, S3, generic web host).
/// No commit-SHA pinning is possible (the URL itself is the canonical
/// identity), so reproducibility relies on the host serving consistent
/// content. Authors who care about reproducibility should version their URL
/// (e.g. <c>catalog-v1.yaml</c>).
/// </summary>
public sealed record UrlCatalogSource(Uri SourceUri) : CatalogSource
{
    public override string Canonical
    {
        get
        {
            // Uri.AbsoluteUri lowercases scheme + host and strips default
            // ports (:80, :443) for free. We additionally strip a trailing
            // slash from non-root paths so .../catalog and .../catalog/
            // dedup to the same canonical in the aggregator's visited-set.
            var raw = SourceUri.AbsoluteUri;
            if (raw.EndsWith('/') && SourceUri.AbsolutePath.Length > 1)
            {
                raw = raw[..^1];
            }
            return raw;
        }
    }

    public bool IsInsecure => string.Equals(SourceUri.Scheme, "http", StringComparison.OrdinalIgnoreCase);
}
