namespace PagoniaLand.Paker;

/// <summary>
/// Fixed offsets and marker bytes from the Pioneers of Pagonia
/// <c>&lt;modulename&gt;.gd.bin</c> layout. The format is inferred empirically
/// from the four shipped indexes: <c>core/core.gd.bin</c>, <c>dlc1/dlc1.gd.bin</c>,
/// <c>decorations1/decorations1.gd.bin</c>, and <c>tools/tools.gd.bin</c>.
///
/// Layout:
/// <code>
///   [byte 0] 0x03
///   [byte 1] varies (0x00 or 0x01 observed; semantics unknown)
///   [byte 2] 0x02
///   [byte 3] (entries.Count - 1), low byte
///   [bytes 4..6] 0x00 0x00 0x00
///   [entries] each: uint32 LE length (UTF-16 code units), then 2*length bytes UTF-16 LE
/// </code>
///
/// Byte 3 matches <c>entries.Count - 1</c> across all four shipped indexes
/// (core: 0x2A=42 for 43 entries, dlc1: 0x0E=14 for 15, decorations1: 0x01=1
/// for 2, tools: 0x01=1 for 2). Whether the field is meant as a count, a
/// highest-index, or something else that coincidentally equals N-1 is
/// unverified — we expose <see cref="ComputeHeaderByte3"/> for callers that
/// build fresh indexes and want to set the field consistently.
///
/// Byte 1 varies (0x00 across the three "newer" shipped indexes, 0x01 in
/// tools.gd.bin). Semantics unknown — possibly a minor format version.
///
/// We preserve the seven header bytes verbatim so reads round-trip
/// byte-identically; when building a fresh index from scratch (see
/// <see cref="GdBinIndex.CreateEmpty"/> / <see cref="GdBinIndex.WithComputedHeader"/>)
/// we use a safe default header.
/// </summary>
public static class GdBinFormatConstants
{
    /// <summary>Length of the fixed header at the start of every <c>.gd.bin</c>.</summary>
    public const int HeaderSize = 7;

    /// <summary>The 0x03 byte that must appear at position 0.</summary>
    public const byte HeaderByte0 = 0x03;

    /// <summary>The 0x02 byte that must appear at position 2.</summary>
    public const byte HeaderByte2 = 0x02;

    /// <summary>
    /// Default seven-byte header used when constructing a fresh <c>.gd.bin</c>.
    /// Matches the shape of <c>decorations1.gd.bin</c> — the simplest shipped
    /// index, with byte[3]=0x01 corresponding to a two-entry list. Callers
    /// adding entries should follow up with
    /// <see cref="GdBinIndex.WithComputedHeader"/> so byte[3] tracks the entry
    /// count.
    /// </summary>
    public static ReadOnlySpan<byte> DefaultHeader => [0x03, 0x00, 0x02, 0x01, 0x00, 0x00, 0x00];

    /// <summary>
    /// Compute the byte that occupies position 3 of the header for a given
    /// entry count, matching the pattern <c>entries.Count - 1</c> observed in
    /// every shipped index. For an empty list returns 0x00 — that case isn't
    /// represented in any shipped pak, so the engine's tolerance is untested.
    /// </summary>
    public static byte ComputeHeaderByte3(int entryCount)
        => entryCount <= 0 ? (byte)0x00 : (byte)((entryCount - 1) & 0xFF);
}
