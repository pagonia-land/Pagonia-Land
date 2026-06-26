namespace PagoniaLand.Paker;

/// <summary>
/// Notes on the Pioneers of Pagonia compiled-localization layout
/// (<c>&lt;modulename&gt;/localization/loca_&lt;lang&gt;.bin</c>, e.g.
/// <c>loca_en_us.bin</c>). The format is inferred empirically from two test
/// paks produced by the 1.4.0 Pagonia Editor ("package gdb with dependency"
/// and "package map with dlc1 and gdb"), so treat it as a best-effort decode
/// rather than a guaranteed spec.
///
/// Layout:
/// <code>
///   [no header, no count field]
///   [strings] each: a .NET 7-bit-encoded-int length prefix (byte count of the
///                   UTF-8 payload), then that many UTF-8 bytes
/// </code>
///
/// The strings are the exact shape <see cref="System.IO.BinaryWriter.Write(string)"/>
/// emits: a length-prefixed UTF-8 string where the prefix is a 7-bit-encoded
/// (LEB128-style) integer holding the <em>byte</em> length, not the character
/// count. In both sample files every prefix fit in a single byte
/// (e.g. <c>0x12</c>=18 for "MY Festival Ground", <c>0x26</c>=38 for
/// "My Animal Farm detail view description"); the multi-byte continuation form
/// is implemented for strings ≥ 128 bytes but has not been observed in the wild.
///
/// There is no magic number, version, or entry count — like the
/// <c>.gd.bin</c> index, the file is a bare sequence read until end-of-stream.
/// That also means there is nothing to validate a candidate file <em>is</em> a
/// loca blob beyond "every length prefix lands inside the stream and every
/// payload decodes as UTF-8". The strings appear to be value-only and
/// positionally ordered; no embedded keys were seen, so the engine presumably
/// resolves them by index from the GameDatabase side.
/// </summary>
public static class LocaFormatConstants
{
    /// <summary>
    /// Maximum number of bytes a 7-bit-encoded length prefix may occupy. .NET
    /// caps its string-length prefix at five bytes (35 bits), and so do we, to
    /// keep a corrupt prefix from spinning the decode loop.
    /// </summary>
    public const int MaxLengthPrefixBytes = 5;
}
