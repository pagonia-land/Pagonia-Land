namespace PagoniaLand.Paker;

/// <summary>
/// Selects which entries an `unpack` or `pack` invocation actually processes.
///
/// Filter axes are AND-composed:
///   * index range:  Start &lt;= i &lt;= End  (both inclusive, both optional)
///   * compression: CompressedOnly = process only entries with compressed=true;
///                  UncompressedOnly = process only entries with compressed=false.
///                  The two are mutually exclusive — set at most one.
///   * filename:    FilenameContains is a case-sensitive substring; null/empty
///                  means "any filename".
///
/// <para>
/// Wiring note for plpaker compatibility: plpaker's <c>main.cpp</c> swaps
/// <c>-c</c> and <c>-d</c> when forwarding them to its filter; we map
/// <c>-c</c>/<c>--compress</c> to <see cref="CompressedOnly"/> and
/// <c>-d</c>/<c>--decompress</c> to <see cref="UncompressedOnly"/>, which is
/// what the flag names actually say.
/// </para>
/// </summary>
public sealed record PakFilter(
    bool CompressedOnly = false,
    bool UncompressedOnly = false,
    int? Start = null,
    int? End = null,
    string? FilenameContains = null)
{
    /// <summary>A filter that matches every entry.</summary>
    public static PakFilter All { get; } = new();

    /// <summary>True if no axis is set — every entry passes <see cref="Matches"/>.</summary>
    public bool IsUnrestricted =>
        !CompressedOnly
        && !UncompressedOnly
        && !Start.HasValue
        && !End.HasValue
        && string.IsNullOrEmpty(FilenameContains);

    public bool Matches(int index, bool compressed, string filename)
    {
        if (Start.HasValue && index < Start.Value) return false;
        if (End.HasValue && index > End.Value) return false;
        if (CompressedOnly && !compressed) return false;
        if (UncompressedOnly && compressed) return false;
        if (!string.IsNullOrEmpty(FilenameContains)
            && !filename.Contains(FilenameContains, StringComparison.Ordinal)) return false;
        return true;
    }

    public bool Matches(int index, PakEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return Matches(index, entry.Compressed, entry.Filename);
    }

    public bool Matches(int index, PakInfoEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return Matches(index, entry.Compressed, entry.Filename);
    }
}
