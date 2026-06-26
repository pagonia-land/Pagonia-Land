namespace PagoniaLand.Catalog.Model;

/// <summary>
/// The parsed GameDatabase as a queryable model: every entity (with its XML element) plus a
/// GUID lookup for name resolution. Domain projection builders take this and produce the
/// catalogs the app renders.
/// </summary>
public sealed class GameDatabase
{
    private readonly Dictionary<string, GameEntity> _byGuid;

    public GameDatabase(IReadOnlyList<GameEntity> entities)
    {
        Entities = entities;
        _byGuid = new Dictionary<string, GameEntity>(StringComparer.Ordinal);
        foreach (var entity in entities)
        {
            // First occurrence wins — matches the analyzer's definition map.
            _byGuid.TryAdd(entity.Guid, entity);
        }
    }

    public IReadOnlyList<GameEntity> Entities { get; }

    /// <summary>GUID → entity (first occurrence), the resolution map.</summary>
    public IReadOnlyDictionary<string, GameEntity> ByGuid => _byGuid;

    /// <summary>
    /// Resolve a GUID to its entity's name, or the empty string if it does not resolve
    /// (an unknown GUID, a null reference, or null/blank input).
    /// </summary>
    public string ResolveName(string? guid) =>
        !string.IsNullOrWhiteSpace(guid) && _byGuid.TryGetValue(guid.Trim(), out var entity)
            ? entity.Name
            : string.Empty;
}
