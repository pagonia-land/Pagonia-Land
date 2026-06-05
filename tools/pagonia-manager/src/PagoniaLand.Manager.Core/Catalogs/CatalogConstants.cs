namespace PagoniaLand.Manager;

/// <summary>
/// Catalog-related constants shared across the Core and the CLI.
/// </summary>
public static class CatalogConstants
{
    /// <summary>
    /// The official Pagonia Land catalog, self-hosted in this repository under
    /// <c>catalog/official.yaml</c> and pointing at the in-repo
    /// <c>official-mods/</c> tree via the repo entry's <c>indexPath</c>. New
    /// stores are seeded with this subscription at init time so a fresh install
    /// has somewhere to browse. It is an ordinary entry in
    /// <c>state.yaml.subscribedCatalogs</c> — opt out with
    /// <c>catalog remove</c> like any other subscription.
    /// <para>Intentionally carries no <c>#ref</c>: the official catalog is a
    /// rolling pointer resolved against the default branch (HEAD), so it always
    /// reflects the latest curated list. Per-mod fetches still pin a commit SHA,
    /// so reproducibility is unaffected by the catalog itself moving.</para>
    /// </summary>
    public const string OfficialCatalogSource = "gh:pagonia-land/Pagonia-Land/catalog/official.yaml";
}
