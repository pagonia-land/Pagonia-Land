namespace PagoniaLand.Catalog;

/// <summary>One search-index entry, matching the online catalog browser's item shape.</summary>
public sealed record SearchIndexItem(
    string Type,
    string Title,
    string Subtitle,
    string Package,
    string Guid,
    string File,
    string Terms,
    IReadOnlyDictionary<string, string> Fields);

/// <summary>A search index in the catalog browser's format: a timestamp, a count, and the items.</summary>
public sealed record SearchIndexDocument(string GeneratedAt, int ItemCount, IReadOnlyList<SearchIndexItem> Items);
