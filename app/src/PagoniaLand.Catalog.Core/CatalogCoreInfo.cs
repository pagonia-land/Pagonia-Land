using System.Reflection;

namespace PagoniaLand.Catalog;

/// <summary>
/// Stable identity for the catalog engine that backs the "Pagonia Land" desktop app.
/// The engine reads a local GameDatabase (the extracted <c>*.gd.xml</c> set) and builds a
/// queryable model the app renders; it never publishes anything (content policy: bulk
/// game-derived data stays strictly local).
/// </summary>
public static class CatalogCoreInfo
{
    /// <summary>Product name surfaced in diagnostics. The shipping app is branded "Pagonia Land".</summary>
    public const string ProductName = "Pagonia Land Catalog";

    /// <summary>The informational version stamped on the assembly (e.g. <c>0.4.0-dev</c>).</summary>
    public static string Version =>
        typeof(CatalogCoreInfo).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? "0.0.0";
}
