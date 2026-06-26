namespace PagoniaLand.Catalog.Assets;

/// <summary>What a single pak contributes to the catalog — for the "Sources" overview.</summary>
public sealed record PakSummary(
    string Name,
    long SizeBytes,
    int Entries,
    int GameDatabaseFiles,
    int Images,
    int Textures)
{
    /// <summary>Image + texture assets (icons, art) the pak carries.</summary>
    public int Assets => Images + Textures;
}
