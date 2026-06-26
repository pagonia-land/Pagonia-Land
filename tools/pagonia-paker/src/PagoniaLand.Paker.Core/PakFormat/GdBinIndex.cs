namespace PagoniaLand.Paker;

/// <summary>
/// The contents of a <c>&lt;modulename&gt;.gd.bin</c> file: a fixed-length
/// header (seven bytes, see <see cref="GdBinFormatConstants"/>) plus a list of
/// in-pak paths to <c>*.gd.xml</c> files (and occasionally other resources)
/// the module contributes to the GameDatabase.
/// </summary>
/// <param name="HeaderBytes">
/// Exactly <see cref="GdBinFormatConstants.HeaderSize"/> bytes copied verbatim
/// from the source file. Two of the seven bytes vary between shipped indexes
/// in ways we don't fully understand, so we round-trip them rather than try to
/// reconstruct them.
/// </param>
/// <param name="Entries">
/// One entry per resource path the index lists. Order is preserved: the engine
/// loads paths in the order they appear here, so reorderings are not a no-op.
/// </param>
public sealed record GdBinIndex(
    IReadOnlyList<byte> HeaderBytes,
    IReadOnlyList<string> Entries)
{
    /// <summary>
    /// Whether the source index ended with a zero-length terminator record
    /// (<c>00 00 00 00</c>). The 1.4.0 Pagonia Editor emits one on every index
    /// it writes — after the last entry, or alone after the header for an empty
    /// index — while the shipped paks (core/dlc1/…) end cleanly at EOF without
    /// it. Tracked so <see cref="GdBinWriter"/> can round-trip an editor-emitted
    /// index byte-identically. Defaults to <c>false</c> (shipped / scaffold shape).
    /// </summary>
    public bool HasTrailingTerminator { get; init; }

    /// <summary>
    /// Build a fresh, empty index using <see cref="GdBinFormatConstants.DefaultHeader"/>.
    /// Use this when scaffolding a new mod pak from scratch; for round-tripping
    /// a shipped pak, prefer <see cref="GdBinReader.Read"/> which preserves the
    /// original seven-byte header.
    /// </summary>
    public static GdBinIndex CreateEmpty()
        => new(GdBinFormatConstants.DefaultHeader.ToArray(), []);

    /// <summary>
    /// Return a copy of this index with <paramref name="entryPath"/> appended.
    /// The original record is left unchanged; the new path goes at the end of
    /// the entry list because order is load-order. The header is NOT
    /// recomputed — chain <see cref="WithComputedHeader"/> after a batch of
    /// edits if byte[3] should track the new entry count.
    /// </summary>
    public GdBinIndex WithEntryAdded(string entryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entryPath);
        var next = new List<string>(Entries.Count + 1);
        next.AddRange(Entries);
        next.Add(entryPath);
        return this with { Entries = next };
    }

    /// <summary>
    /// Return a copy of this index whose byte[3] of the header equals
    /// <see cref="GdBinFormatConstants.ComputeHeaderByte3"/> for the current
    /// entry count. Other header bytes are preserved. Use this after appending
    /// or removing entries when writing back to disk; reads from shipped paks
    /// don't need it because the source already satisfies the invariant.
    /// </summary>
    public GdBinIndex WithComputedHeader()
    {
        if (HeaderBytes.Count != GdBinFormatConstants.HeaderSize) return this;
        var next = new byte[GdBinFormatConstants.HeaderSize];
        for (var i = 0; i < next.Length; i++) next[i] = HeaderBytes[i];
        // byte[3] tracks (record count - 1); the editor's terminator counts as a
        // record, so an index that carries one contributes +1 (see GdBinFormatConstants).
        var recordCount = Entries.Count + (HasTrailingTerminator ? 1 : 0);
        next[3] = GdBinFormatConstants.ComputeHeaderByte3(recordCount);
        return this with { HeaderBytes = next };
    }
}
