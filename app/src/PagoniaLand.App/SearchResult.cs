using Avalonia.Media.Imaging;

namespace PagoniaLand.App;

/// <summary>A global-search hit: a catalog row plus which domain tab it lives in (for the jump).</summary>
public sealed record SearchResult(CatalogRow Row, int Tab, string Domain)
{
    public string Name => Row.Name;

    public Bitmap? Icon => Row.Icon;
}
