using System.Buffers.Binary;
using System.IO.Compression;
using System.IO.Hashing;
using System.Text;
using System.Text.Json.Nodes;
using Json.Schema;
using PagoniaLand.Paker;

var reader = new PakReader();
var writer = new PakWriter();

var tests = new (string Name, Func<bool> Run)[]
{
    ("product name is stable", () => PakerInfo.ProductName == "Pagonia Land Paker"),
    ("command name is stable", () => PakerInfo.CommandName == "pagonia-paker"),
    ("version is present", () => !string.IsNullOrWhiteSpace(PakerInfo.Version)),
    ("exit codes are stable", ExitCodesAreStable),
    ("empty pak index round-trips", EmptyIndexRoundTrips),
    ("multi-entry pak index round-trips", MultiEntryIndexRoundTrips),
    ("long filename round-trips with marker", LongFilenameRoundTrips),
    ("truncated footer reports a diagnostic", TruncatedFooterReportsDiagnostic),
    ("invalid index offset reports a diagnostic", InvalidIndexOffsetReportsDiagnostic),
    ("crc mismatch reports a diagnostic", CrcMismatchReportsDiagnostic),
    ("corrupt filename length reports a diagnostic, not an unbounded allocation", CorruptFilenameLengthReportsDiagnostic),
    ("compression-by-extension list matches scripts/sandbox-pack.ps1", CompressionExtensionListMatchesSandboxScript),
    ("uncompressed entry extracts byte-identical", UncompressedEntryExtracts),
    ("compressed entry extracts decompressed payload", CompressedEntryExtracts),
    ("compressed entry refuses to inflate past its declared size (decompression bomb)", CompressedEntryRejectsDecompressionBomb),
    ("pakinfo.json round-trips through the source-gen context", PakInfoJsonRoundTrip),
    ("gzip compress + decompress round-trips arbitrary bytes", GzipRoundTrip),
    ("gzip decompress refuses to exceed its output cap (decompression bomb)", GzipDecompressHonoursOutputCap),
    ("pack writes an archive that PakReader can extract", PackProducesReadableArchive),
    ("pack reports a diagnostic for invalid pakinfo json", PackInvalidJsonDiagnostic),
    ("pack reports a diagnostic for a missing source file", PackMissingSourceDiagnostic),
    ("patch replaces named entries and keeps the rest verbatim", PatchReplacesNamedEntries),
    ("patch reports a diagnostic for a missing source file", PatchMissingSourceDiagnostic),
    ("patch treats unmatched positional path as an add", PatchUnmatchedPathBecomesAdd),
    ("patch deletes an existing entry from the output pak", PatchDeletesExistingEntry),
    ("patch rejects --delete pointing at a missing entry", PatchDeleteMissingTargetDiagnostic),
    ("patch leaves the input archive untouched on disk", PatchLeavesInputUntouched),
    ("filter parser reads short and long flag forms", FilterParserShortAndLongForms),
    ("filter parser rejects --compress with --decompress", FilterParserRejectsContradictoryFlags),
    ("filter parser rejects negative integer arguments", FilterParserRejectsNegativeIntegers),
    ("filter Matches AND-composes index, compression, and filename", FilterMatchesComposesAxes),
    ("pack honours the filter and skips non-matching entries", PackHonoursFilter),
    ("filter parser reads --json long and equals forms", FilterParserReadsJsonFlag),
    ("list report serialises with expected fields", ListReportSerialisesWithExpectedFields),
    ("unpack report serialises filter and entry rows", UnpackReportSerialisesFilterAndEntries),
    ("pack report serialises pakinfo path and filter", PackReportSerialisesPakInfoAndFilter),
    ("patch report serialises replacements and counts", PatchReportSerialisesReplacementsAndCounts),
    ("filter parser reads -j and --jobs forms", FilterParserReadsJobsFlag),
    ("pack with jobs=4 produces same entry contents as jobs=1", PackParallelEqualsSequential),
    ("patch with jobs=4 produces same entry contents as jobs=1", PatchParallelEqualsSequential),
    ("gdbin reader rejects an empty stream", GdBinReaderRejectsEmptyStream),
    ("gdbin reader rejects a wrong magic byte", GdBinReaderRejectsBadMagic),
    ("gdbin read+write round-trips a synthetic two-entry index", GdBinTwoEntryRoundTrip),
    ("gdbin read+write round-trips a 43-entry core-shaped index", GdBin43EntryRoundTrip),
    ("gdbin reader rejects a truncated entry length", GdBinReaderRejectsTruncatedLength),
    ("gdbin reader rejects a truncated entry path", GdBinReaderRejectsTruncatedPath),
    ("gdbin reader rejects non-ASCII content that isn't UTF-16 boundary-aligned", GdBinReaderRejectsOddPathByteCount),
    ("gdbin reader rejects an oversized char count without a giant allocation", GdBinReaderRejectsOversizedCharCount),
    ("gdbin ComputeHeaderByte3 matches the four shipped indexes", GdBinComputeHeaderByte3MatchesShipped),
    ("gdbin WithComputedHeader recomputes byte[3] from entry count", GdBinWithComputedHeaderRecomputes),
    ("gdbin reader tolerates the 1.4.0 editor terminator on a single-entry index + round-trips", GdBinReaderToleratesEditorTerminator),
    ("gdbin reader reads an editor empty index (header + terminator only) as 0 entries", GdBinReaderReadsEditorEmptyIndex),
    ("gdbin WithComputedHeader counts the terminator as a record", GdBinWithComputedHeaderCountsTerminator),
    ("loca reader decodes a .NET BinaryWriter value-only blob", LocaReaderDecodesBinaryWriterBlob),
    ("loca reader treats a 0-byte file as an empty blob", LocaReaderEmptyStreamIsEmptyBlob),
    ("loca reader decodes a multi-byte 7-bit length prefix (>=128 bytes)", LocaReaderDecodesMultiByteLengthPrefix),
    ("loca reader rejects a truncated payload", LocaReaderRejectsTruncatedPayload),
    ("loca reader rejects a truncated multi-byte length prefix", LocaReaderRejectsTruncatedLengthPrefix),
    ("loca reader rejects an oversized length prefix without a giant allocation", LocaReaderRejectsOversizedLengthPrefix),
    ("loca reader rejects invalid UTF-8 bytes", LocaReaderRejectsInvalidUtf8),
    ("patch auto-registers an added *.gd.xml in <m>/<m>.gd.bin", PatchAutoRegistersAddedGdXml),
    ("patch leaves <m>/<m>.gd.bin alone when no *.gd.xml is added", PatchLeavesGdBinAloneWhenNoXmlAdded),
    ("patch defers to a user-provided <m>/<m>.gd.bin replacement", PatchUserReplacementOfGdBinWins),
    ("patch --no-gdbin-register skips auto-registration", PatchNoGdBinRegisterFlagSkips),
    ("patch leaves the pak unchanged when added XML has no matching module .gd.bin", PatchSkipsAutoRegisterWhenNoIndexExists),
    ("classify: pak with files.json + .gd.bin under <m>/ is module", ClassifyModulePak),
    ("classify: pak with <m>.gd.bin at root is still module (tools.pak shape)", ClassifyModuleWithRootGdBin),
    ("classify: popmap-only pak has no gd content and counts popmaps", ClassifyUserMapPak),
    ("classify: pak with manifest but no gd.bin and no popmap is overlay", ClassifyOverlayPak),
    ("classify: overlay pak surfaces root-level overrides", ClassifyOverlaySurfacesOverridesAtRoot),
    ("classify: pak with no manifest is unknown", ClassifyUnknownPakWithoutManifest),
    ("classify: editor map is map-scoped, empty module gd.bin not global", ClassifyEditorMapIsMapScoped),
    ("classify: editor empty global index (header + terminator) is not global", ClassifyEditorEmptyGlobalIndexNotGlobal),
    ("classify regression: editor GDB mod with DLC deps is global (EE 'package gdb with dependency' shape)", ClassifyEditorGdbModWithDependenciesIsGlobal),
    ("classify regression: editor map mod is map-scoped only + popmap + deps (EE 'package map with dlc1 and gdb' shape)", ClassifyEditorMapModFullShapeIsMapScopedOnly),
    ("classify: global + map-scoped gd content reports both scopes", ClassifyGlobalAndMapScopedTogether),
    ("classify: pak with multiple modules picks first + warns", ClassifyMultipleModulesPicksFirstWarns),
    ("classify: manifest dependencies surfaced from JSON", ClassifyExtractsManifestDependencies),
    ("schema roundtrip: pak-list-report", SchemaRoundtripPakListReport),
    ("schema roundtrip: pak-unpack-report", SchemaRoundtripPakUnpackReport),
    ("schema roundtrip: pak-pack-report", SchemaRoundtripPakPackReport),
    ("schema roundtrip: pak-patch-report", SchemaRoundtripPakPatchReport),
    ("schema roundtrip: pak-classify-report", SchemaRoundtripPakClassifyReport),
    ("schema roundtrip: gdbin-info-report", SchemaRoundtripGdBinInfoReport),
    ("schema roundtrip: loca-info-report", SchemaRoundtripLocaInfoReport),
    ("pack: a cancelled token aborts Pack before writing the output pak", PackHonoursCancellationToken),
};

var failed = 0;

foreach (var test in tests)
{
    try
    {
        if (test.Run())
        {
            Console.WriteLine($"PASS {test.Name}");
            continue;
        }

        failed++;
        Console.Error.WriteLine($"FAIL {test.Name}");
    }
    catch (Exception exception)
    {
        failed++;
        Console.Error.WriteLine($"FAIL {test.Name}: {exception.GetType().Name}: {exception.Message}");
    }
}

if (failed == 0)
{
    Console.WriteLine($"All {tests.Length} tests passed.");
    return 0;
}

Console.Error.WriteLine($"{failed} of {tests.Length} test(s) failed.");
return 1;

bool ExitCodesAreStable()
    => PakerExitCodes.Success == 0
        && PakerExitCodes.Error == 1
        && PakerExitCodes.Usage == 64;

bool EmptyIndexRoundTrips()
{
    var stream = new MemoryStream();
    writer.WriteIndex(stream, Array.Empty<PakEntry>(), version: 1);

    stream.Position = 0;
    var result = reader.OpenIndex(stream);

    return result.Success
        && result.Index!.Version == 1
        && result.Index.Entries.Count == 0;
}

bool MultiEntryIndexRoundTrips()
{
    var entries = new PakEntry[]
    {
        new(Compressed: false, Filename: "core/gdb/buildings.gd.xml", BeginOffset: 0, Size: 1024),
        new(Compressed: true, Filename: "core/gdb/resources.gd.xml", BeginOffset: 1024, Size: 512),
        new(Compressed: true, Filename: "dlc1/maps/meadowsong.gd.xml", BeginOffset: 1536, Size: 8192),
    };

    var stream = new MemoryStream();
    writer.WriteIndex(stream, entries, version: 1);

    stream.Position = 0;
    var result = reader.OpenIndex(stream);

    if (!result.Success || result.Index!.Entries.Count != entries.Length)
    {
        return false;
    }

    for (var i = 0; i < entries.Length; i++)
    {
        var expected = entries[i];
        var actual = result.Index.Entries[i];
        if (expected.Compressed != actual.Compressed
            || expected.Filename != actual.Filename
            || expected.BeginOffset != actual.BeginOffset
            || expected.Size != actual.Size)
        {
            return false;
        }
    }

    return true;
}

bool LongFilenameRoundTrips()
{
    // A filename with >= 128 UTF-8 bytes triggers the 0x01 long-filename marker.
    var longName = "core/very/deeply/nested/path/" + new string('x', 200) + ".gd.xml";
    var entries = new[]
    {
        new PakEntry(Compressed: false, Filename: longName, BeginOffset: 42, Size: 7),
    };

    var stream = new MemoryStream();
    writer.WriteIndex(stream, entries, version: 1);

    stream.Position = 0;
    var result = reader.OpenIndex(stream);

    return result.Success
        && result.Index!.Entries.Count == 1
        && result.Index.Entries[0].Filename == longName
        && result.Index.Entries[0].BeginOffset == 42
        && result.Index.Entries[0].Size == 7;
}

bool TruncatedFooterReportsDiagnostic()
{
    var stream = new MemoryStream(new byte[5]);  // less than the 12-byte footer
    var result = reader.OpenIndex(stream);

    return !result.Success
        && result.Diagnostics.Any(d => d.Code == "pakFooterTruncated");
}

bool InvalidIndexOffsetReportsDiagnostic()
{
    // Write a footer that claims the index begins past the footer itself.
    var stream = new MemoryStream();
    stream.Write(new byte[32]);  // 32 bytes of dummy "data"
    var footerStart = stream.Length;
    Span<byte> footer = stackalloc byte[12];
    BinaryPrimitives.WriteUInt32LittleEndian(footer[..4], 0);  // CRC (will be checked against streamed bytes)
    BinaryPrimitives.WriteInt64LittleEndian(footer[4..12], footerStart + 100);  // way past the footer
    stream.Write(footer);

    stream.Position = 0;
    var result = reader.OpenIndex(stream);

    return !result.Success
        && result.Diagnostics.Any(d => d.Code == "pakIndexOffsetInvalid");
}

bool CorruptFilenameLengthReportsDiagnostic()
{
    // A corrupt per-entry filename length (0xFFFFFFFF) must not drive an unbounded byte[]
    // allocation / OverflowException out of OpenIndex; it must surface the clean PakEntryTruncated
    // every other corrupt-index path produces. The value passes the long-filename marker gate
    // (marker 0x01) so it reaches the allocation site the guard protects.
    using var pak = new MemoryStream();
    pak.Write(Encoding.UTF8.GetBytes("data"));               // a data blob
    var indexBegin = pak.Position;
    Span<byte> u32 = stackalloc byte[4];
    BinaryPrimitives.WriteUInt32LittleEndian(u32, 1); pak.Write(u32);          // version
    BinaryPrimitives.WriteUInt32LittleEndian(u32, 1); pak.Write(u32);          // count = 1
    pak.WriteByte(0);                                          // compressed flag
    BinaryPrimitives.WriteUInt32BigEndian(u32, 0xFFFFFFFFu); pak.Write(u32);   // corrupt filename length
    pak.WriteByte(0x01);                                       // long-filename marker, so we reach the alloc
    pak.Write(new byte[20]);                                   // pad so count*minEntryBytes fits the index area
    var footerStart = pak.Position;

    var crc = new Crc32();
    crc.Append(pak.GetBuffer().AsSpan(0, (int)footerStart));
    Span<byte> crcBytes = stackalloc byte[4];
    crc.GetCurrentHash(crcBytes);
    pak.Write(crcBytes);                                       // footer CRC (LE) over data + index
    Span<byte> i64 = stackalloc byte[8];
    BinaryPrimitives.WriteInt64LittleEndian(i64, indexBegin); pak.Write(i64);  // footer index-begin

    pak.Position = 0;
    var result = reader.OpenIndex(pak);
    return !result.Success && result.Diagnostics.Any(d => d.Code == "pakEntryTruncated");
}

bool CompressionExtensionListMatchesSandboxScript()
{
    // The compression-by-extension heuristic lives in PakPatcher.CompressibleExtensions (the single
    // source of truth) but scripts/sandbox-pack.ps1 hardcodes the same set. Assert they stay
    // identical, so editing one without the other fails CI instead of silently diverging pak layout.
    var dir = AppContext.BaseDirectory;
    string? scriptPath = null;
    for (var probe = new DirectoryInfo(dir); probe is not null; probe = probe.Parent)
    {
        var candidate = Path.Combine(probe.FullName, "scripts", "sandbox-pack.ps1");
        if (File.Exists(candidate)) { scriptPath = candidate; break; }
    }
    if (scriptPath is null) { return false; }

    var script = File.ReadAllText(scriptPath);
    var marker = script.IndexOf("$compressFor", StringComparison.Ordinal);
    if (marker < 0) { return false; }
    var open = script.IndexOf("@(", marker, StringComparison.Ordinal);
    var close = open < 0 ? -1 : script.IndexOf(')', open);
    if (open < 0 || close < 0) { return false; }

    var scriptExts = script[(open + 2)..close]
        .Split(',')
        .Select(part => part.Trim().Trim('"', '\''))
        .Where(part => part.Length > 0)
        .ToHashSet(StringComparer.Ordinal);

    return scriptExts.SetEquals(PakPatcher.CompressibleExtensions);
}

bool UncompressedEntryExtracts()
{
    var payload = Encoding.UTF8.GetBytes("hello world\nuncompressed bytes go here");
    var pak = BuildPak(("greeting.txt", payload, Compressed: false));

    var extracted = ExtractFirstEntry(pak);
    return extracted.SequenceEqual(payload);
}

bool CompressedEntryExtracts()
{
    var payload = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("compress me ", 200)));
    var pak = BuildPak(("blob.dat", payload, Compressed: true));

    var extracted = ExtractFirstEntry(pak);
    return extracted.SequenceEqual(payload);
}

bool CompressedEntryRejectsDecompressionBomb()
{
    // A corrupt/malicious index can under-declare an entry's uncompressed Size so a
    // small compressed blob inflates far beyond it. ExtractEntry must cap output at
    // the declared Size and refuse the overflow rather than stream unbounded bytes.
    var payload = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("A", 5000)));
    var pak = BuildPak(("bomb.dat", payload, Compressed: true));

    using var pakStream = new MemoryStream(pak, writable: false);
    var result = reader.OpenIndex(pakStream);
    if (!result.Success || result.Index is null || result.Index.Entries.Count == 0) { return false; }

    // Claim only 16 uncompressed bytes while the gzip member really holds 5000.
    var tampered = result.Index.Entries[0] with { Size = 16 };

    using var outStream = new MemoryStream();
    try
    {
        reader.ExtractEntry(pakStream, tampered, outStream);
        return false; // should have thrown
    }
    catch (InvalidDataException)
    {
        // Output must be capped at the declared size, not the full 5000.
        return outStream.Length <= 16;
    }
}

bool PakInfoJsonRoundTrip()
{
    var entries = new[]
    {
        new PakEntry(Compressed: false, Filename: "a/one.txt", BeginOffset: 0, Size: 16),
        new PakEntry(Compressed: true, Filename: "a/two.txt", BeginOffset: 16, Size: 32),
    };

    var index = new PakIndex(Version: 1, entries);
    var info = PakReader.BuildPakInfo(index);
    var json = PakReader.SerializePakInfo(info);

    return info.Count == 2
        && json.Contains("\"version\": 1", StringComparison.Ordinal)
        && json.Contains("\"count\": 2", StringComparison.Ordinal)
        && json.Contains("\"compressed\": false", StringComparison.Ordinal)
        && json.Contains("\"compressed\": true", StringComparison.Ordinal)
        && json.Contains("\"filename\": \"a/one.txt\"", StringComparison.Ordinal)
        && json.Contains("\"size_compressed\":", StringComparison.Ordinal);
}

byte[] ExtractFirstEntry(byte[] pak)
{
    using var pakStream = new MemoryStream(pak, writable: false);
    var result = reader.OpenIndex(pakStream);
    if (!result.Success || result.Index is null || result.Index.Entries.Count == 0)
    {
        throw new InvalidOperationException("Test pak did not open as expected.");
    }

    using var outStream = new MemoryStream();
    reader.ExtractEntry(pakStream, result.Index.Entries[0], outStream);
    return outStream.ToArray();
}

byte[] BuildPak(params (string Name, byte[] Payload, bool Compressed)[] entries)
{
    using var pakStream = new MemoryStream();
    var pakEntries = new List<PakEntry>(entries.Length);
    var crc = new Crc32();

    foreach (var (name, payload, compressed) in entries)
    {
        var beginOffset = pakStream.Position;

        if (compressed)
        {
            // Write the gzip-format compressed bytes into the pak.
            using (var gzip = new GZipStream(pakStream, CompressionLevel.Optimal, leaveOpen: true))
            {
                gzip.Write(payload, 0, payload.Length);
            }
            var sizeInPak = pakStream.Position - beginOffset;
            // Per plpaker's on-disk format, `Size` is the UNCOMPRESSED payload size.
            // The reader recomputes the in-pak (gzip) byte count from begin-offset deltas.
            pakEntries.Add(new PakEntry(Compressed: true, Filename: name, BeginOffset: beginOffset, Size: payload.Length));

            // Roll the CRC over the bytes we just wrote.
            var written = pakStream.GetBuffer().AsSpan((int)beginOffset, (int)sizeInPak);
            crc.Append(written);
        }
        else
        {
            pakStream.Write(payload, 0, payload.Length);
            pakEntries.Add(new PakEntry(Compressed: false, Filename: name, BeginOffset: beginOffset, Size: payload.Length));
            crc.Append(payload);
        }
    }

    writer.WriteIndex(pakStream, pakEntries, version: 1, rollingCrc: crc);
    return pakStream.ToArray();
}

bool CrcMismatchReportsDiagnostic()
{
    var entries = new[]
    {
        new PakEntry(Compressed: false, Filename: "core/probe.xml", BeginOffset: 0, Size: 16),
    };

    var stream = new MemoryStream();
    writer.WriteIndex(stream, entries, version: 1);

    // Flip one byte inside the index region (between offset 0 and stream.Length - 16).
    var buffer = stream.ToArray();
    buffer[4] ^= 0xFF;
    var corrupted = new MemoryStream(buffer);

    var result = reader.OpenIndex(corrupted);

    return !result.Success
        && result.Diagnostics.Any(d => d.Code == "pakIndexCrcMismatch");
}

bool GzipRoundTrip()
{
    var payload = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("the quick brown fox 0123456789 ", 1000)));

    var compressedStream = new MemoryStream();
    using (var input = new MemoryStream(payload, writable: false))
    {
        GzipCompressor.Compress(input, compressedStream);
    }
    // Sanity: gzip wrapper should shrink obviously repetitive text.
    if (compressedStream.Length >= payload.Length) return false;

    compressedStream.Position = 0;
    var decompressedStream = new MemoryStream();
    GzipCompressor.Decompress(compressedStream, decompressedStream);

    return decompressedStream.ToArray().SequenceEqual(payload);
}

bool GzipDecompressHonoursOutputCap()
{
    // A highly compressible payload (lots of zeros) inflates well past a small cap; Decompress
    // must abort with InvalidDataException rather than stream the whole thing into output.
    var payload = new byte[200_000]; // all zeros → compresses tiny, decompresses to 200 KB
    var compressedStream = new MemoryStream();
    using (var input = new MemoryStream(payload, writable: false))
    {
        GzipCompressor.Compress(input, compressedStream);
    }
    compressedStream.Position = 0;

    var decompressedStream = new MemoryStream();
    try
    {
        GzipCompressor.Decompress(compressedStream, decompressedStream, maxOutputBytes: 1024);
        return false; // should have thrown
    }
    catch (InvalidDataException)
    {
        // Output is bounded near the cap, not the full 200 KB.
        return decompressedStream.Length <= 1024 + 81920;
    }
}

bool PackProducesReadableArchive()
{
    var tempDir = Path.Combine(Path.GetTempPath(), "pagonia-paker-tests-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(tempDir);
    try
    {
        var rawPayload = Encoding.UTF8.GetBytes("hello, raw entry\n");
        var gzipPayload = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("pack me ", 200)));

        var rawSource = Path.Combine(tempDir, "raw.txt");
        File.WriteAllBytes(rawSource, rawPayload);

        var gzipDir = Path.Combine(tempDir, "nested");
        Directory.CreateDirectory(gzipDir);
        var gzipSource = Path.Combine(gzipDir, "blob.bin");
        File.WriteAllBytes(gzipSource, gzipPayload);

        var pakInfo = new PakInfo(
            Version: 1,
            Count: 2,
            Entries: new[]
            {
                new PakInfoEntry(Index: 0, Pos: 0, Compressed: false, Filename: "raw.txt",
                    Begin: 0, End: rawPayload.Length, Size: rawPayload.Length, SizeCompressed: rawPayload.Length),
                new PakInfoEntry(Index: 1, Pos: 1, Compressed: true, Filename: "nested/blob.bin",
                    Begin: 0, End: 0, Size: gzipPayload.Length, SizeCompressed: 0),
            });

        var pakInfoPath = Path.Combine(tempDir, "pakinfo.json");
        File.WriteAllText(pakInfoPath, PakReader.SerializePakInfo(pakInfo));

        var outputPak = Path.Combine(tempDir, "out.pak");
        var diagnostics = new PakPacker().Pack(pakInfoPath, outputPak);
        if (diagnostics.Any(d => d.Severity == PakDiagnosticSeverity.Error)) return false;

        using var pakStream = File.OpenRead(outputPak);
        var read = reader.OpenIndex(pakStream);
        if (!read.Success || read.Index!.Entries.Count != 2) return false;

        using var rawOut = new MemoryStream();
        reader.ExtractEntry(pakStream, read.Index.Entries[0], rawOut);
        if (!rawOut.ToArray().SequenceEqual(rawPayload)) return false;

        using var gzipOut = new MemoryStream();
        reader.ExtractEntry(pakStream, read.Index.Entries[1], gzipOut);
        return gzipOut.ToArray().SequenceEqual(gzipPayload);
    }
    finally
    {
        try { Directory.Delete(tempDir, recursive: true); } catch { /* best effort */ }
    }
}

bool PackHonoursCancellationToken()
{
    var tempDir = Path.Combine(Path.GetTempPath(), "pagonia-paker-tests-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(tempDir);
    try
    {
        File.WriteAllText(Path.Combine(tempDir, "raw.txt"), "hello");
        var pakInfo = new PakInfo(
            Version: 1,
            Count: 1,
            Entries: new[]
            {
                new PakInfoEntry(Index: 0, Pos: 0, Compressed: false, Filename: "raw.txt",
                    Begin: 0, End: 5, Size: 5, SizeCompressed: 5),
            });
        var pakInfoPath = Path.Combine(tempDir, "pakinfo.json");
        File.WriteAllText(pakInfoPath, PakReader.SerializePakInfo(pakInfo));
        var outputPak = Path.Combine(tempDir, "out.pak");

        using var cts = new CancellationTokenSource();
        cts.Cancel();
        try
        {
            new PakPacker().Pack(pakInfoPath, outputPak, PakFilter.All, jobs: 1, cts.Token);
            return false; // must have thrown
        }
        catch (OperationCanceledException)
        {
            // Cancelled at the top of Pack, before the output stream was created.
            return !File.Exists(outputPak);
        }
    }
    finally
    {
        try { Directory.Delete(tempDir, recursive: true); } catch { /* best effort */ }
    }
}

bool PackInvalidJsonDiagnostic()
{
    var tempDir = Path.Combine(Path.GetTempPath(), "pagonia-paker-tests-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(tempDir);
    try
    {
        var pakInfoPath = Path.Combine(tempDir, "pakinfo.json");
        File.WriteAllText(pakInfoPath, "{ this is not valid json }");

        var diagnostics = new PakPacker().Pack(pakInfoPath, Path.Combine(tempDir, "out.pak"));
        return diagnostics.Any(d => d.Code == "pakInfoJsonInvalid")
            && !File.Exists(Path.Combine(tempDir, "out.pak"));
    }
    finally
    {
        try { Directory.Delete(tempDir, recursive: true); } catch { /* best effort */ }
    }
}

bool PackMissingSourceDiagnostic()
{
    var tempDir = Path.Combine(Path.GetTempPath(), "pagonia-paker-tests-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(tempDir);
    try
    {
        var pakInfo = new PakInfo(
            Version: 1,
            Count: 1,
            Entries: new[]
            {
                new PakInfoEntry(Index: 0, Pos: 0, Compressed: false, Filename: "does-not-exist.bin",
                    Begin: 0, End: 0, Size: 0, SizeCompressed: 0),
            });
        var pakInfoPath = Path.Combine(tempDir, "pakinfo.json");
        File.WriteAllText(pakInfoPath, PakReader.SerializePakInfo(pakInfo));

        var outputPak = Path.Combine(tempDir, "out.pak");
        var diagnostics = new PakPacker().Pack(pakInfoPath, outputPak);
        return diagnostics.Any(d => d.Code == "packSourceMissing")
            && !File.Exists(outputPak);
    }
    finally
    {
        try { Directory.Delete(tempDir, recursive: true); } catch { /* best effort */ }
    }
}

bool PatchReplacesNamedEntries()
{
    var tempDir = Path.Combine(Path.GetTempPath(), "pagonia-paker-tests-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(tempDir);
    try
    {
        // Build an input pak with three entries: raw before, compressed middle, raw after.
        var beforePayload = Encoding.UTF8.GetBytes("entry before the patched one\n");
        var middlePayload = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("original middle ", 50)));
        var afterPayload = Encoding.UTF8.GetBytes("entry after the patched one\n");

        var inputPak = BuildPak(
            ("data/before.txt", beforePayload, Compressed: false),
            ("data/middle.xml", middlePayload, Compressed: true),
            ("data/after.txt", afterPayload, Compressed: false));

        var inputPakPath = Path.Combine(tempDir, "input.pak");
        File.WriteAllBytes(inputPakPath, inputPak);

        // The CLI contract is that the replacement file's path equals the entry name.
        // To exercise that, put a replacement file at data/middle.xml under tempDir
        // and call the patcher from tempDir-as-cwd via an absolute path.
        var replacementDir = Path.Combine(tempDir, "data");
        Directory.CreateDirectory(replacementDir);
        var newMiddle = Encoding.UTF8.GetBytes("patched middle content");
        var replacementPath = Path.Combine(replacementDir, "middle.xml");
        File.WriteAllBytes(replacementPath, newMiddle);

        var previousCwd = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(tempDir);
        try
        {
            var outputPak = Path.Combine(tempDir, "patched.pak");
            var diagnostics = new PakPatcher().Patch(inputPakPath, outputPak, new[] { "data/middle.xml" });
            if (diagnostics.Any(d => d.Severity == PakDiagnosticSeverity.Error)) return false;

            using var pakStream = File.OpenRead(outputPak);
            var read = reader.OpenIndex(pakStream);
            if (!read.Success || read.Index!.Entries.Count != 3) return false;

            // Verify the original two entries decompress to the original bytes.
            using var beforeOut = new MemoryStream();
            reader.ExtractEntry(pakStream, read.Index.Entries[0], beforeOut);
            if (!beforeOut.ToArray().SequenceEqual(beforePayload)) return false;

            using var afterOut = new MemoryStream();
            reader.ExtractEntry(pakStream, read.Index.Entries[2], afterOut);
            if (!afterOut.ToArray().SequenceEqual(afterPayload)) return false;

            // The patched middle entry should now hold the new content (still compressed).
            if (!read.Index.Entries[1].Compressed) return false;
            using var middleOut = new MemoryStream();
            reader.ExtractEntry(pakStream, read.Index.Entries[1], middleOut);
            return middleOut.ToArray().SequenceEqual(newMiddle);
        }
        finally
        {
            Directory.SetCurrentDirectory(previousCwd);
        }
    }
    finally
    {
        try { Directory.Delete(tempDir, recursive: true); } catch { /* best effort */ }
    }
}

bool PatchMissingSourceDiagnostic()
{
    var tempDir = Path.Combine(Path.GetTempPath(), "pagonia-paker-tests-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(tempDir);
    try
    {
        var payload = Encoding.UTF8.GetBytes("only entry");
        var inputPak = BuildPak(("only.txt", payload, Compressed: false));
        var inputPakPath = Path.Combine(tempDir, "input.pak");
        File.WriteAllBytes(inputPakPath, inputPak);

        var outputPak = Path.Combine(tempDir, "patched.pak");
        var diagnostics = new PakPatcher().Patch(
            inputPakPath, outputPak, new[] { Path.Combine(tempDir, "does-not-exist.txt") });

        return diagnostics.Any(d => d.Code == "patchSourceMissing")
            && !File.Exists(outputPak);
    }
    finally
    {
        try { Directory.Delete(tempDir, recursive: true); } catch { /* best effort */ }
    }
}

bool PatchUnmatchedPathBecomesAdd()
{
    var tempDir = Path.Combine(Path.GetTempPath(), "pagonia-paker-tests-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(tempDir);
    try
    {
        var existing = Encoding.UTF8.GetBytes("the existing entry");
        var newPayload = Encoding.UTF8.GetBytes("brand new add content");
        var inputPak = BuildPak(("real.txt", existing, Compressed: false));
        var inputPakPath = Path.Combine(tempDir, "input.pak");
        File.WriteAllBytes(inputPakPath, inputPak);

        // The source for a non-matching entry-name lives on disk under that
        // very name relative to cwd (the documented patch contract).
        var addPath = Path.Combine(tempDir, "ghost.txt");
        File.WriteAllBytes(addPath, newPayload);

        var outputPak = Path.Combine(tempDir, "patched.pak");
        var previousCwd = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(tempDir);
        try
        {
            var diagnostics = new PakPatcher().Patch(inputPakPath, outputPak, new[] { "ghost.txt" });
            if (diagnostics.Any(d => d.Severity == PakDiagnosticSeverity.Error)) return false;
            if (!diagnostics.Any(d => d.Code == "pakEntryAdded")) return false;

            using var stream = File.OpenRead(outputPak);
            var read = reader.OpenIndex(stream);
            if (!read.Success || read.Index!.Entries.Count != 2) return false;

            using var extracted = new MemoryStream();
            var newEntry = read.Index.Entries.First(e => e.Filename == "ghost.txt");
            reader.ExtractEntry(stream, newEntry, extracted);
            return extracted.ToArray().SequenceEqual(newPayload);
        }
        finally
        {
            Directory.SetCurrentDirectory(previousCwd);
        }
    }
    finally
    {
        try { Directory.Delete(tempDir, recursive: true); } catch { /* best effort */ }
    }
}

bool PatchDeletesExistingEntry()
{
    var tempDir = Path.Combine(Path.GetTempPath(), "pagonia-paker-tests-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(tempDir);
    try
    {
        var keep = Encoding.UTF8.GetBytes("keep me");
        var drop = Encoding.UTF8.GetBytes("drop me");
        var inputPak = BuildPak(("keep.txt", keep, Compressed: false), ("drop.txt", drop, Compressed: false));
        var inputPakPath = Path.Combine(tempDir, "input.pak");
        File.WriteAllBytes(inputPakPath, inputPak);

        var outputPak = Path.Combine(tempDir, "patched.pak");
        var diagnostics = new PakPatcher().Patch(
            inputPakPath, outputPak, replacementFiles: Array.Empty<string>(), deletions: new[] { "drop.txt" }, jobs: 1);
        if (diagnostics.Any(d => d.Severity == PakDiagnosticSeverity.Error)) return false;
        if (!diagnostics.Any(d => d.Code == "pakEntryDeleted")) return false;

        using var stream = File.OpenRead(outputPak);
        var read = reader.OpenIndex(stream);
        return read.Success
            && read.Index!.Entries.Count == 1
            && read.Index.Entries[0].Filename == "keep.txt";
    }
    finally
    {
        try { Directory.Delete(tempDir, recursive: true); } catch { /* best effort */ }
    }
}

bool PatchDeleteMissingTargetDiagnostic()
{
    var tempDir = Path.Combine(Path.GetTempPath(), "pagonia-paker-tests-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(tempDir);
    try
    {
        var payload = Encoding.UTF8.GetBytes("only entry");
        var inputPak = BuildPak(("real.txt", payload, Compressed: false));
        var inputPakPath = Path.Combine(tempDir, "input.pak");
        File.WriteAllBytes(inputPakPath, inputPak);

        var outputPak = Path.Combine(tempDir, "patched.pak");
        var diagnostics = new PakPatcher().Patch(
            inputPakPath, outputPak, replacementFiles: Array.Empty<string>(),
            deletions: new[] { "does-not-exist.bin" }, jobs: 1);

        return diagnostics.Any(d => d.Code == "patchDeleteTargetMissing")
            && !File.Exists(outputPak);
    }
    finally
    {
        try { Directory.Delete(tempDir, recursive: true); } catch { /* best effort */ }
    }
}

bool FilterParserShortAndLongForms()
{
    // Mix short, long, equals-form, and a separator-style flag-value pair around two positional args.
    var args = new[] { "-c", "--start=2", "-e", "5", "input.pak", "-f", "tools/", "output.pak" };
    var result = FilterArgumentParser.Parse(args);
    if (!result.Success) return false;

    return result.Filter.CompressedOnly
        && !result.Filter.UncompressedOnly
        && result.Filter.Start == 2
        && result.Filter.End == 5
        && result.Filter.FilenameContains == "tools/"
        && result.Positional.SequenceEqual(new[] { "input.pak", "output.pak" });
}

bool FilterParserRejectsContradictoryFlags()
{
    var result = FilterArgumentParser.Parse(new[] { "-c", "-d", "input.pak" });
    return !result.Success && result.Error!.Contains("mutually exclusive");
}

bool FilterParserRejectsNegativeIntegers()
{
    var result = FilterArgumentParser.Parse(new[] { "--start=-1", "input.pak" });
    return !result.Success && result.Error!.Contains("--start");
}

bool FilterMatchesComposesAxes()
{
    var filter = new PakFilter(
        CompressedOnly: true,
        Start: 1,
        End: 3,
        FilenameContains: "tools/");

    // Index 0 is out of range
    if (filter.Matches(0, compressed: true, "tools/manifest.json")) return false;
    // Index 4 is out of range
    if (filter.Matches(4, compressed: true, "tools/manifest.json")) return false;
    // Uncompressed entry rejected by --compress filter
    if (filter.Matches(2, compressed: false, "tools/manifest.json")) return false;
    // Wrong filename substring
    if (filter.Matches(2, compressed: true, "core/gdb/buildings.gd.xml")) return false;
    // Hits every axis
    return filter.Matches(2, compressed: true, "tools/manifest.json");
}

bool PackHonoursFilter()
{
    var tempDir = Path.Combine(Path.GetTempPath(), "pagonia-paker-tests-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(tempDir);
    try
    {
        var alphaPayload = Encoding.UTF8.GetBytes("alpha payload");
        var betaPayload = Encoding.UTF8.GetBytes("beta payload");
        var gammaPayload = Encoding.UTF8.GetBytes("gamma payload");

        File.WriteAllBytes(Path.Combine(tempDir, "alpha.txt"), alphaPayload);
        File.WriteAllBytes(Path.Combine(tempDir, "beta.txt"), betaPayload);
        File.WriteAllBytes(Path.Combine(tempDir, "gamma.txt"), gammaPayload);

        var pakInfo = new PakInfo(
            Version: 1,
            Count: 3,
            Entries: new[]
            {
                new PakInfoEntry(0, 0, Compressed: false, "alpha.txt", 0, 0, alphaPayload.Length, alphaPayload.Length),
                new PakInfoEntry(1, 1, Compressed: false, "beta.txt", 0, 0, betaPayload.Length, betaPayload.Length),
                new PakInfoEntry(2, 2, Compressed: false, "gamma.txt", 0, 0, gammaPayload.Length, gammaPayload.Length),
            });

        var pakInfoPath = Path.Combine(tempDir, "pakinfo.json");
        File.WriteAllText(pakInfoPath, PakReader.SerializePakInfo(pakInfo));

        var outputPak = Path.Combine(tempDir, "out.pak");
        var filter = new PakFilter(FilenameContains: "beta");
        var diagnostics = new PakPacker().Pack(pakInfoPath, outputPak, filter);
        if (diagnostics.Any(d => d.Severity == PakDiagnosticSeverity.Error)) return false;

        using var pakStream = File.OpenRead(outputPak);
        var read = reader.OpenIndex(pakStream);
        if (!read.Success || read.Index!.Entries.Count != 1) return false;
        if (read.Index.Entries[0].Filename != "beta.txt") return false;

        using var extracted = new MemoryStream();
        reader.ExtractEntry(pakStream, read.Index.Entries[0], extracted);
        return extracted.ToArray().SequenceEqual(betaPayload);
    }
    finally
    {
        try { Directory.Delete(tempDir, recursive: true); } catch { /* best effort */ }
    }
}

bool PatchLeavesInputUntouched()
{
    var tempDir = Path.Combine(Path.GetTempPath(), "pagonia-paker-tests-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(tempDir);
    try
    {
        var payload = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("preserve me ", 20)));
        var inputPak = BuildPak(("target.bin", payload, Compressed: true));
        var inputPakPath = Path.Combine(tempDir, "input.pak");
        File.WriteAllBytes(inputPakPath, inputPak);
        var inputHashBefore = System.Security.Cryptography.SHA256.HashData(inputPak);

        var replacementPath = Path.Combine(tempDir, "target.bin");
        File.WriteAllBytes(replacementPath, Encoding.UTF8.GetBytes("new content"));

        var previousCwd = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(tempDir);
        try
        {
            var outputPak = Path.Combine(tempDir, "patched.pak");
            var diagnostics = new PakPatcher().Patch(inputPakPath, outputPak, new[] { "target.bin" });
            if (diagnostics.Any(d => d.Severity == PakDiagnosticSeverity.Error)) return false;

            var inputHashAfter = System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(inputPakPath));
            return inputHashBefore.SequenceEqual(inputHashAfter);
        }
        finally
        {
            Directory.SetCurrentDirectory(previousCwd);
        }
    }
    finally
    {
        try { Directory.Delete(tempDir, recursive: true); } catch { /* best effort */ }
    }
}

bool FilterParserReadsJsonFlag()
{
    var resultLong = FilterArgumentParser.Parse(new[] { "--json", "report.json", "in.pak" });
    if (!resultLong.Success) return false;
    if (resultLong.JsonReportPath != "report.json") return false;
    if (!resultLong.Positional.SequenceEqual(new[] { "in.pak" })) return false;

    var resultEq = FilterArgumentParser.Parse(new[] { "in.pak", "--json=out/report.json" });
    if (!resultEq.Success) return false;
    if (resultEq.JsonReportPath != "out/report.json") return false;
    if (!resultEq.Positional.SequenceEqual(new[] { "in.pak" })) return false;

    var resultMissing = FilterArgumentParser.Parse(new[] { "--json" });
    return !resultMissing.Success && resultMissing.Error!.Contains("--json");
}

bool ListReportSerialisesWithExpectedFields()
{
    var entries = new[]
    {
        new PakListEntryReport(0, Compressed: false, "tools.gd.bin", 0, 109, 109),
        new PakListEntryReport(1, Compressed: true, "tools/blob.bin", 109, 1024, 256),
    };
    var diagnostics = new[]
    {
        PakReportDiagnostic.From(new PakDiagnostic(PakDiagnosticSeverity.Info, "pakIndexRead", "ok")),
    };
    var report = new PakListReport("input.pak", Success: true, Version: 2, EntryCount: 2,
        PakInfoPath: "out/pakinfo.json", entries, diagnostics);

    var json = PakListReport.Serialize(report);
    return json.Contains("\"Pak\": \"input.pak\"", StringComparison.Ordinal)
        && json.Contains("\"Version\": 2", StringComparison.Ordinal)
        && json.Contains("\"EntryCount\": 2", StringComparison.Ordinal)
        && json.Contains("\"Filename\": \"tools/blob.bin\"", StringComparison.Ordinal)
        && json.Contains("\"SizeInPak\": 256", StringComparison.Ordinal)
        && json.Contains("\"Code\": \"pakIndexRead\"", StringComparison.Ordinal);
}

bool UnpackReportSerialisesFilterAndEntries()
{
    var filter = new PakFilter(CompressedOnly: true, Start: 1, FilenameContains: "tools/");
    var entries = new[]
    {
        new PakUnpackEntryReport(0, "tools.gd.bin", Compressed: false, "skipped", OutputPath: null),
        new PakUnpackEntryReport(1, "tools/blob.bin", Compressed: true, "extracted", "out/tools/blob.bin"),
    };
    var report = new PakUnpackReport("input.pak", Success: true, "out", EntryCount: 2,
        ExtractedCount: 1, SkippedCount: 1, FailedCount: 0,
        PakReportFilter.From(filter), entries, Array.Empty<PakReportDiagnostic>());

    var json = PakUnpackReport.Serialize(report);
    return json.Contains("\"CompressedOnly\": true", StringComparison.Ordinal)
        && json.Contains("\"FilenameContains\": \"tools/\"", StringComparison.Ordinal)
        && json.Contains("\"Status\": \"skipped\"", StringComparison.Ordinal)
        && json.Contains("\"Status\": \"extracted\"", StringComparison.Ordinal)
        && json.Contains("\"ExtractedCount\": 1", StringComparison.Ordinal);
}

bool PackReportSerialisesPakInfoAndFilter()
{
    var filter = new PakFilter(FilenameContains: "alpha");
    var report = new PakPackReport(
        PakInfo: "work/pakinfo.json", Output: "out.pak", Success: true,
        EntryCount: 3, PackedCount: 1, PakReportFilter.From(filter),
        Array.Empty<PakReportDiagnostic>());

    var json = PakPackReport.Serialize(report);
    return json.Contains("\"PakInfo\": \"work/pakinfo.json\"", StringComparison.Ordinal)
        && json.Contains("\"PackedCount\": 1", StringComparison.Ordinal)
        && json.Contains("\"FilenameContains\": \"alpha\"", StringComparison.Ordinal);
}

bool PatchReportSerialisesReplacementsAndCounts()
{
    var replacements = new[]
    {
        new PakPatchReplacementReport("tools/manifest.json", "./work/tools/manifest.json"),
    };
    var report = new PakPatchReport(
        Input: "input.pak", Output: "patched.pak", Success: true,
        EntryCount: 8, ReplacedCount: 1, CopiedCount: 7,
        AddedCount: 2, DeletedCount: 0,
        Replacements: replacements,
        Deletions: new[] { "tools/old.audio" },
        GdbinUpdates: Array.Empty<PakPatchGdBinUpdateReport>(),
        Diagnostics: Array.Empty<PakReportDiagnostic>());

    var json = PakPatchReport.Serialize(report);
    return json.Contains("\"Input\": \"input.pak\"", StringComparison.Ordinal)
        && json.Contains("\"ReplacedCount\": 1", StringComparison.Ordinal)
        && json.Contains("\"AddedCount\": 2", StringComparison.Ordinal)
        && json.Contains("\"CopiedCount\": 7", StringComparison.Ordinal)
        && json.Contains("\"EntryName\": \"tools/manifest.json\"", StringComparison.Ordinal)
        && json.Contains("\"tools/old.audio\"", StringComparison.Ordinal)
        && json.Contains("\"GdbinUpdates\": []", StringComparison.Ordinal);
}

bool FilterParserReadsJobsFlag()
{
    var resultLong = FilterArgumentParser.Parse(new[] { "--jobs", "8", "in.pak" });
    if (!resultLong.Success || resultLong.Jobs != 8) return false;

    var resultShort = FilterArgumentParser.Parse(new[] { "-j", "4", "in.pak" });
    if (!resultShort.Success || resultShort.Jobs != 4) return false;

    var resultEq = FilterArgumentParser.Parse(new[] { "--jobs=2", "in.pak" });
    if (!resultEq.Success || resultEq.Jobs != 2) return false;

    var resultZero = FilterArgumentParser.Parse(new[] { "--jobs=0", "in.pak" });
    return !resultZero.Success && resultZero.Error!.Contains("--jobs");
}

bool PackParallelEqualsSequential()
{
    var tempDir = Path.Combine(Path.GetTempPath(), "pagonia-paker-tests-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(tempDir);
    try
    {
        // Build a mid-size pakinfo with a mix of compressed and uncompressed entries so the
        // parallel encode actually has work to spread across cores.
        var entries = new List<PakInfoEntry>();
        for (var i = 0; i < 20; i++)
        {
            var name = $"data/entry-{i:D2}.bin";
            var payload = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat($"row-{i} ", 500)));
            var full = Path.Combine(tempDir, name.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllBytes(full, payload);
            entries.Add(new PakInfoEntry(i, i, Compressed: i % 2 == 0, name, 0, 0, payload.Length, payload.Length));
        }
        var pakInfo = new PakInfo(1, entries.Count, entries);
        var pakInfoPath = Path.Combine(tempDir, "pakinfo.json");
        File.WriteAllText(pakInfoPath, PakReader.SerializePakInfo(pakInfo));

        var seqPak = Path.Combine(tempDir, "seq.pak");
        var parPak = Path.Combine(tempDir, "par.pak");
        if (new PakPacker().Pack(pakInfoPath, seqPak, PakFilter.All, jobs: 1).Any(d => d.Severity == PakDiagnosticSeverity.Error)) return false;
        if (new PakPacker().Pack(pakInfoPath, parPak, PakFilter.All, jobs: 4).Any(d => d.Severity == PakDiagnosticSeverity.Error)) return false;

        // The gzip output is deterministic for the same input + level, so the two pak files
        // must be byte-identical.
        var seqHash = System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(seqPak));
        var parHash = System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(parPak));
        return seqHash.SequenceEqual(parHash);
    }
    finally
    {
        try { Directory.Delete(tempDir, recursive: true); } catch { /* best effort */ }
    }
}

bool PatchParallelEqualsSequential()
{
    var tempDir = Path.Combine(Path.GetTempPath(), "pagonia-paker-tests-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(tempDir);
    try
    {
        var inputPak = BuildPak(
            ("data/alpha.txt", Encoding.UTF8.GetBytes("alpha"), Compressed: false),
            ("data/beta.bin", Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("beta ", 100))), Compressed: true),
            ("data/gamma.txt", Encoding.UTF8.GetBytes("gamma"), Compressed: false),
            ("data/delta.bin", Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("delta ", 100))), Compressed: true));
        var inputPakPath = Path.Combine(tempDir, "input.pak");
        File.WriteAllBytes(inputPakPath, inputPak);

        Directory.CreateDirectory(Path.Combine(tempDir, "data"));
        var replacement = Path.Combine(tempDir, "data", "beta.bin");
        File.WriteAllBytes(replacement, Encoding.UTF8.GetBytes("patched beta payload"));

        var previousCwd = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(tempDir);
        try
        {
            var seqPak = Path.Combine(tempDir, "seq.pak");
            var parPak = Path.Combine(tempDir, "par.pak");
            if (new PakPatcher().Patch(inputPakPath, seqPak, new[] { "data/beta.bin" }, jobs: 1).Any(d => d.Severity == PakDiagnosticSeverity.Error)) return false;
            if (new PakPatcher().Patch(inputPakPath, parPak, new[] { "data/beta.bin" }, jobs: 4).Any(d => d.Severity == PakDiagnosticSeverity.Error)) return false;

            var seqHash = System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(seqPak));
            var parHash = System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(parPak));
            return seqHash.SequenceEqual(parHash);
        }
        finally
        {
            Directory.SetCurrentDirectory(previousCwd);
        }
    }
    finally
    {
        try { Directory.Delete(tempDir, recursive: true); } catch { /* best effort */ }
    }
}

bool GdBinReaderRejectsEmptyStream()
{
    var result = new GdBinReader().Read(new MemoryStream(Array.Empty<byte>()));
    return !result.Success
        && result.Diagnostics.Any(d => d.Code == DiagnosticCodes.GdBinHeaderInvalid);
}

bool GdBinReaderRejectsBadMagic()
{
    // byte[0] should be 0x03; here we pass 0x04.
    var bytes = new byte[] { 0x04, 0x00, 0x02, 0x00, 0x00, 0x00, 0x00 };
    var result = new GdBinReader().Read(new MemoryStream(bytes));
    return !result.Success
        && result.Diagnostics.Any(d => d.Code == DiagnosticCodes.GdBinHeaderInvalid);
}

bool GdBinTwoEntryRoundTrip()
{
    var bytes = BuildGdBinBytes(
        headerByte1: 0x01, headerByte3: 0x01,
        "tools/gdb/magmaview.gd.xml",
        "core/audio/core.guids");

    var read = new GdBinReader().Read(new MemoryStream(bytes));
    if (!read.Success || read.Index!.Entries.Count != 2) return false;
    if (read.Index.Entries[0] != "tools/gdb/magmaview.gd.xml") return false;
    if (read.Index.Entries[1] != "core/audio/core.guids") return false;

    var rewritten = new MemoryStream();
    new GdBinWriter().Write(rewritten, read.Index);
    return rewritten.ToArray().SequenceEqual(bytes);
}

bool GdBin43EntryRoundTrip()
{
    // Build a core.gd.bin-shaped fixture: 43 entries, headerByte3=0x2A.
    var paths = new string[43];
    for (var i = 0; i < paths.Length; i++) paths[i] = $"core/gdb/file{i:D2}.gd.xml";
    var bytes = BuildGdBinBytes(headerByte1: 0x00, headerByte3: 0x2A, paths);

    var read = new GdBinReader().Read(new MemoryStream(bytes));
    if (!read.Success || read.Index!.Entries.Count != 43) return false;
    for (var i = 0; i < paths.Length; i++)
    {
        if (read.Index.Entries[i] != paths[i]) return false;
    }
    if (read.Index.HeaderBytes[3] != 0x2A) return false;

    var rewritten = new MemoryStream();
    new GdBinWriter().Write(rewritten, read.Index);
    return rewritten.ToArray().SequenceEqual(bytes);
}

bool GdBinReaderRejectsTruncatedLength()
{
    // Valid header + only 2 bytes of the next uint32 length field.
    var bytes = new byte[] { 0x03, 0x00, 0x02, 0x00, 0x00, 0x00, 0x00, 0x05, 0x00 };
    var result = new GdBinReader().Read(new MemoryStream(bytes));
    return !result.Success
        && result.Diagnostics.Any(d => d.Code == DiagnosticCodes.GdBinEntryTruncated);
}

bool GdBinReaderRejectsTruncatedPath()
{
    // Header + length=5 (10 bytes expected) + only 4 bytes of UTF-16 follow.
    var bytes = new byte[]
    {
        0x03, 0x00, 0x02, 0x00, 0x00, 0x00, 0x00,
        0x05, 0x00, 0x00, 0x00,
        (byte)'a', 0x00, (byte)'b', 0x00,
    };
    var result = new GdBinReader().Read(new MemoryStream(bytes));
    return !result.Success
        && result.Diagnostics.Any(d => d.Code == DiagnosticCodes.GdBinEntryTruncated);
}

bool GdBinReaderRejectsOddPathByteCount()
{
    // Length-field claims 3 UTF-16 code units (6 bytes), but only 5 bytes follow.
    var bytes = new byte[]
    {
        0x03, 0x00, 0x02, 0x00, 0x00, 0x00, 0x00,
        0x03, 0x00, 0x00, 0x00,
        (byte)'a', 0x00, (byte)'b', 0x00, (byte)'c',
    };
    var result = new GdBinReader().Read(new MemoryStream(bytes));
    return !result.Success
        && result.Diagnostics.Any(d => d.Code == DiagnosticCodes.GdBinEntryTruncated);
}

bool GdBinReaderRejectsOversizedCharCount()
{
    // Header + a char count of 0x10000000 (268M code units = 512 MB) with NO path bytes after it.
    // The count passes the int.MaxValue/2 overflow guard, so without the remaining-bytes check the
    // reader would allocate ~512 MB from an 11-byte stream. It must reject early as truncated.
    var bytes = new byte[]
    {
        0x03, 0x00, 0x02, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x10, // charCount = 0x10000000, little-endian
    };
    var result = new GdBinReader().Read(new MemoryStream(bytes));
    return !result.Success
        && result.Diagnostics.Any(d => d.Code == DiagnosticCodes.GdBinEntryTruncated);
}

bool GdBinComputeHeaderByte3MatchesShipped()
{
    // (entries.Count - 1) low byte, validated against the four shipped indexes.
    return GdBinFormatConstants.ComputeHeaderByte3(43) == 0x2A     // core
        && GdBinFormatConstants.ComputeHeaderByte3(15) == 0x0E     // dlc1
        && GdBinFormatConstants.ComputeHeaderByte3(2)  == 0x01     // decorations1 / tools
        && GdBinFormatConstants.ComputeHeaderByte3(0)  == 0x00     // empty edge case
        && GdBinFormatConstants.ComputeHeaderByte3(257) == 0x00;   // wraps at byte boundary (256 -> 0x00)
}

bool GdBinWithComputedHeaderRecomputes()
{
    // Start from the default header (byte[3]=0x01), append two paths -> total 2 entries -> byte[3] still 0x01.
    // Append a third -> byte[3] becomes 0x02.
    var index = GdBinIndex.CreateEmpty()
        .WithEntryAdded("mod/gdb/a.gd.xml")
        .WithEntryAdded("mod/gdb/b.gd.xml")
        .WithComputedHeader();
    if (index.HeaderBytes[3] != 0x01) return false;

    index = index.WithEntryAdded("mod/gdb/c.gd.xml").WithComputedHeader();
    return index.HeaderBytes[3] == 0x02
        && index.Entries.Count == 3
        // Other header bytes preserved.
        && index.HeaderBytes[0] == 0x03
        && index.HeaderBytes[2] == 0x02
        && index.HeaderBytes[6] == 0x00;
}

static byte[] BuildGdBinBytes(byte headerByte1, byte headerByte3, params string[] entries)
{
    var stream = new MemoryStream();
    stream.WriteByte(0x03);
    stream.WriteByte(headerByte1);
    stream.WriteByte(0x02);
    stream.WriteByte(headerByte3);
    stream.WriteByte(0x00);
    stream.WriteByte(0x00);
    stream.WriteByte(0x00);

    Span<byte> lengthBuf = stackalloc byte[4];
    foreach (var entry in entries)
    {
        var bytes = Encoding.Unicode.GetBytes(entry);
        BinaryPrimitives.WriteUInt32LittleEndian(lengthBuf, (uint)(bytes.Length / 2));
        stream.Write(lengthBuf);
        stream.Write(bytes);
    }
    return stream.ToArray();
}

// The 1.4.0 Pagonia Editor closes every gd.bin with a zero-length terminator
// record (00 00 00 00) — shipped paks omit it. This appends one to BuildGdBinBytes.
static byte[] BuildEditorGdBinBytes(byte headerByte1, byte headerByte3, params string[] entries)
{
    var body = BuildGdBinBytes(headerByte1, headerByte3, entries);
    var withTerminator = new byte[body.Length + 4];
    Array.Copy(body, withTerminator, body.Length); // last 4 bytes stay zero = terminator
    return withTerminator;
}

bool GdBinReaderToleratesEditorTerminator()
{
    // Editor single-entry index: header (byte[3]=0x01 = 1 entry + terminator - 1) + one
    // path + the 00 00 00 00 terminator. Must read as one entry, flag the terminator,
    // and re-serialise byte-identically.
    var bytes = BuildEditorGdBinBytes(
        headerByte1: 0x00, headerByte3: 0x01,
        "package gdb no dependencies/gdb/my gamedata.gd.xml");

    var read = new GdBinReader().Read(new MemoryStream(bytes));
    if (!read.Success || read.Index is null) return false;
    if (read.Index.Entries.Count != 1) return false;
    if (read.Index.Entries[0] != "package gdb no dependencies/gdb/my gamedata.gd.xml") return false;
    if (!read.Index.HasTrailingTerminator) return false;

    var rewritten = new MemoryStream();
    new GdBinWriter().Write(rewritten, read.Index);
    return rewritten.ToArray().SequenceEqual(bytes);
}

bool GdBinReaderReadsEditorEmptyIndex()
{
    // The map-only mod's module-level index: header (byte[3]=0x00) + bare terminator.
    var bytes = BuildEditorGdBinBytes(headerByte1: 0x00, headerByte3: 0x00);
    if (bytes.Length != 11) return false; // 7-byte header + 4-byte terminator

    var read = new GdBinReader().Read(new MemoryStream(bytes));
    if (!read.Success || read.Index is null) return false;
    if (read.Index.Entries.Count != 0) return false;
    if (!read.Index.HasTrailingTerminator) return false;

    var rewritten = new MemoryStream();
    new GdBinWriter().Write(rewritten, read.Index);
    return rewritten.ToArray().SequenceEqual(bytes);
}

bool GdBinWithComputedHeaderCountsTerminator()
{
    // With a terminator, byte[3] = (entries + 1) - 1 = entries. 1 entry -> 0x01.
    var withTerminator = (GdBinIndex.CreateEmpty() with { HasTrailingTerminator = true })
        .WithEntryAdded("mod/gdb/a.gd.xml")
        .WithComputedHeader();
    if (withTerminator.HeaderBytes[3] != 0x01) return false;

    // Without it, the shipped rule holds: 1 entry -> byte[3] = 0x00.
    var noTerminator = GdBinIndex.CreateEmpty()
        .WithEntryAdded("mod/gdb/a.gd.xml")
        .WithComputedHeader();
    return noTerminator.HeaderBytes[3] == 0x00;
}

bool LocaReaderDecodesBinaryWriterBlob()
{
    // Build the blob with the canonical .NET writer the engine's exporter uses:
    // BinaryWriter.Write(string) emits a 7-bit-length-prefixed UTF-8 string.
    // Decoding it back must reproduce the exact strings, in order — this is the
    // shape observed in the two 1.4.0-editor test paks.
    var expected = new[] { "MY Festival Ground", "Wookieetreibers Festiveground", "ümläut & symbols ✓" };
    var blob = BuildLocaBytes(expected);

    var result = new LocaReader().Read(new MemoryStream(blob));
    return result.Success
        && result.Strings is not null
        && result.Strings.SequenceEqual(expected);
}

bool LocaReaderEmptyStreamIsEmptyBlob()
{
    // No header / no count field: a 0-byte file is a valid, empty loca.
    var result = new LocaReader().Read(new MemoryStream(Array.Empty<byte>()));
    return result.Success
        && result.Strings is { Count: 0 };
}

bool LocaReaderDecodesMultiByteLengthPrefix()
{
    // A 200-byte string forces a two-byte 7-bit length prefix (0xC8 0x01).
    var big = new string('x', 200);
    var blob = BuildLocaBytes(new[] { "short", big });
    // "short" = 1 prefix byte (0x05) + 5 payload bytes, so the big string's
    // two-byte prefix (0xC8 0x01 = 200) starts at offset 6.
    if (blob[6] != 0xC8 || blob[7] != 0x01) return false;

    var result = new LocaReader().Read(new MemoryStream(blob));
    return result.Success
        && result.Strings is not null
        && result.Strings.Count == 2
        && result.Strings[1] == big;
}

bool LocaReaderRejectsTruncatedPayload()
{
    // Length prefix says 5 bytes, only 2 follow.
    var bytes = new byte[] { 0x05, (byte)'a', (byte)'b' };
    var result = new LocaReader().Read(new MemoryStream(bytes));
    return !result.Success
        && result.Diagnostics.Any(d => d.Code == DiagnosticCodes.LocaEntryTruncated);
}

bool LocaReaderRejectsTruncatedLengthPrefix()
{
    // A lone continuation byte (high bit set) with nothing after it.
    var bytes = new byte[] { 0x80 };
    var result = new LocaReader().Read(new MemoryStream(bytes));
    return !result.Success
        && result.Diagnostics.Any(d => d.Code == DiagnosticCodes.LocaEntryTruncated);
}

bool LocaReaderRejectsOversizedLengthPrefix()
{
    // A corrupt 5-byte prefix FF FF FF FF 07 decodes to 0x7FFFFFFF (~2 GB). Without the
    // remaining-bytes guard the reader would do `new byte[2147483647]` from a 5-byte stream and
    // crash with OutOfMemory. It must reject it cleanly as truncated instead.
    var bytes = new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0x07 };
    var result = new LocaReader().Read(new MemoryStream(bytes));
    return !result.Success
        && result.Diagnostics.Any(d => d.Code == DiagnosticCodes.LocaEntryTruncated);
}

bool LocaReaderRejectsInvalidUtf8()
{
    // Length 1, then 0xFF which is not a valid standalone UTF-8 byte.
    var bytes = new byte[] { 0x01, 0xFF };
    var result = new LocaReader().Read(new MemoryStream(bytes));
    return !result.Success
        && result.Diagnostics.Any(d => d.Code == DiagnosticCodes.LocaStringDecodingFailed);
}

static byte[] BuildLocaBytes(IEnumerable<string> strings)
{
    var stream = new MemoryStream();
    // Default BinaryWriter encoding is UTF-8; Write(string) prepends a
    // 7-bit-encoded byte-length prefix — exactly the loca on-disk shape.
    using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
    {
        foreach (var s in strings) writer.Write(s);
    }
    return stream.ToArray();
}

bool PatchAutoRegistersAddedGdXml()
{
    var tempDir = Path.Combine(Path.GetTempPath(), "pagonia-paker-tests-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(tempDir);
    try
    {
        // Input pak: a module "mod" with a .gd.bin listing one existing XML.
        var existingXmlBytes = Encoding.UTF8.GetBytes("<entities/>\n");
        var initialGdBin = BuildGdBinBytes(0x00, 0x00, "mod/gdb/existing.gd.xml");
        var inputPak = BuildPak(
            ("mod/mod.gd.bin", initialGdBin, Compressed: true),
            ("mod/gdb/existing.gd.xml", existingXmlBytes, Compressed: true));
        var inputPakPath = Path.Combine(tempDir, "input.pak");
        File.WriteAllBytes(inputPakPath, inputPak);

        // Source file laid out under the pak entry name relative to cwd.
        var addedDir = Path.Combine(tempDir, "mod", "gdb");
        Directory.CreateDirectory(addedDir);
        File.WriteAllBytes(Path.Combine(addedDir, "added.gd.xml"), Encoding.UTF8.GetBytes("<entities/>\n"));

        var outputPak = Path.Combine(tempDir, "patched.pak");
        var previousCwd = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(tempDir);
        try
        {
            var result = new PakPatcher().PatchAndReport(
                inputPakPath, outputPak,
                replacementFiles: new[] { "mod/gdb/added.gd.xml" },
                deletions: Array.Empty<string>(),
                jobs: 1,
                registerGdBinAdds: true);
            if (!result.Success) return false;
            if (result.GdbinUpdates.Count != 1) return false;
            if (result.GdbinUpdates[0].EntryName != "mod/mod.gd.bin") return false;
            if (result.GdbinUpdates[0].Added.Count != 1) return false;
            if (result.GdbinUpdates[0].Added[0] != "mod/gdb/added.gd.xml") return false;

            // Decode the rebuilt .gd.bin from the output pak and assert it
            // lists both the original entry and the newly added one in that
            // order (existing entries first, new entries sorted ordinally
            // after them).
            using var outStream = File.OpenRead(outputPak);
            var outRead = reader.OpenIndex(outStream);
            if (!outRead.Success || outRead.Index!.Entries.Count != 3) return false;

            var gdbinEntry = outRead.Index.Entries.First(e => e.Filename == "mod/mod.gd.bin");
            using var extracted = new MemoryStream();
            reader.ExtractEntry(outStream, gdbinEntry, extracted);
            extracted.Position = 0;
            var rebuilt = new GdBinReader().Read(extracted);
            if (!rebuilt.Success || rebuilt.Index!.Entries.Count != 2) return false;
            if (rebuilt.Index.Entries[0] != "mod/gdb/existing.gd.xml") return false;
            if (rebuilt.Index.Entries[1] != "mod/gdb/added.gd.xml") return false;
            // byte[3] tracks (entries.Count - 1) = 1 after the rebuild.
            if (rebuilt.Index.HeaderBytes[3] != 0x01) return false;

            // Diagnostic surface: one pakPatchGdbinUpdated with the module entry name on Path.
            return result.Diagnostics.Any(d => d.Code == DiagnosticCodes.PakPatchGdBinUpdated && d.Path == "mod/mod.gd.bin");
        }
        finally
        {
            Directory.SetCurrentDirectory(previousCwd);
        }
    }
    finally
    {
        try { Directory.Delete(tempDir, recursive: true); } catch { /* best effort */ }
    }
}

bool PatchLeavesGdBinAloneWhenNoXmlAdded()
{
    var tempDir = Path.Combine(Path.GetTempPath(), "pagonia-paker-tests-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(tempDir);
    try
    {
        // Same module pak shape as the happy-path test, then add a non-XML
        // entry (an icon at mod/icon.png) and assert the index is untouched.
        var existingXmlBytes = Encoding.UTF8.GetBytes("<entities/>\n");
        var initialGdBin = BuildGdBinBytes(0x00, 0x00, "mod/gdb/existing.gd.xml");
        var inputPak = BuildPak(
            ("mod/mod.gd.bin", initialGdBin, Compressed: true),
            ("mod/gdb/existing.gd.xml", existingXmlBytes, Compressed: true));
        var inputPakPath = Path.Combine(tempDir, "input.pak");
        File.WriteAllBytes(inputPakPath, inputPak);

        var modDir = Path.Combine(tempDir, "mod");
        Directory.CreateDirectory(modDir);
        File.WriteAllBytes(Path.Combine(modDir, "icon.png"), new byte[] { 0x89, 0x50, 0x4E, 0x47 });

        var outputPak = Path.Combine(tempDir, "patched.pak");
        var previousCwd = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(tempDir);
        try
        {
            var result = new PakPatcher().PatchAndReport(
                inputPakPath, outputPak,
                replacementFiles: new[] { "mod/icon.png" },
                deletions: Array.Empty<string>(),
                jobs: 1,
                registerGdBinAdds: true);
            if (!result.Success) return false;
            if (result.GdbinUpdates.Count != 0) return false;

            using var outStream = File.OpenRead(outputPak);
            var outRead = reader.OpenIndex(outStream);
            if (!outRead.Success) return false;

            var gdbinEntry = outRead.Index!.Entries.First(e => e.Filename == "mod/mod.gd.bin");
            using var extracted = new MemoryStream();
            reader.ExtractEntry(outStream, gdbinEntry, extracted);
            return extracted.ToArray().SequenceEqual(initialGdBin);
        }
        finally
        {
            Directory.SetCurrentDirectory(previousCwd);
        }
    }
    finally
    {
        try { Directory.Delete(tempDir, recursive: true); } catch { /* best effort */ }
    }
}

bool PatchUserReplacementOfGdBinWins()
{
    var tempDir = Path.Combine(Path.GetTempPath(), "pagonia-paker-tests-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(tempDir);
    try
    {
        var existingXmlBytes = Encoding.UTF8.GetBytes("<entities/>\n");
        var initialGdBin = BuildGdBinBytes(0x00, 0x00, "mod/gdb/existing.gd.xml");
        var inputPak = BuildPak(
            ("mod/mod.gd.bin", initialGdBin, Compressed: true),
            ("mod/gdb/existing.gd.xml", existingXmlBytes, Compressed: true));
        var inputPakPath = Path.Combine(tempDir, "input.pak");
        File.WriteAllBytes(inputPakPath, inputPak);

        // User ships their own (intentionally different) .gd.bin AND a new
        // XML. Auto-register must NOT step on the user's index.
        var userOwnedGdBin = BuildGdBinBytes(0x00, 0x02, "mod/gdb/existing.gd.xml", "mod/gdb/user_choice.gd.xml", "mod/gdb/some_other.gd.xml");
        var modDir = Path.Combine(tempDir, "mod");
        Directory.CreateDirectory(modDir);
        File.WriteAllBytes(Path.Combine(modDir, "mod.gd.bin"), userOwnedGdBin);
        var gdbDir = Path.Combine(modDir, "gdb");
        Directory.CreateDirectory(gdbDir);
        File.WriteAllBytes(Path.Combine(gdbDir, "added.gd.xml"), Encoding.UTF8.GetBytes("<entities/>\n"));

        var outputPak = Path.Combine(tempDir, "patched.pak");
        var previousCwd = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(tempDir);
        try
        {
            var result = new PakPatcher().PatchAndReport(
                inputPakPath, outputPak,
                replacementFiles: new[] { "mod/mod.gd.bin", "mod/gdb/added.gd.xml" },
                deletions: Array.Empty<string>(),
                jobs: 1,
                registerGdBinAdds: true);
            if (!result.Success) return false;
            if (result.GdbinUpdates.Count != 0) return false;

            using var outStream = File.OpenRead(outputPak);
            var outRead = reader.OpenIndex(outStream);
            if (!outRead.Success) return false;
            var gdbinEntry = outRead.Index!.Entries.First(e => e.Filename == "mod/mod.gd.bin");
            using var extracted = new MemoryStream();
            reader.ExtractEntry(outStream, gdbinEntry, extracted);
            return extracted.ToArray().SequenceEqual(userOwnedGdBin);
        }
        finally
        {
            Directory.SetCurrentDirectory(previousCwd);
        }
    }
    finally
    {
        try { Directory.Delete(tempDir, recursive: true); } catch { /* best effort */ }
    }
}

bool PatchNoGdBinRegisterFlagSkips()
{
    var tempDir = Path.Combine(Path.GetTempPath(), "pagonia-paker-tests-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(tempDir);
    try
    {
        var existingXmlBytes = Encoding.UTF8.GetBytes("<entities/>\n");
        var initialGdBin = BuildGdBinBytes(0x00, 0x00, "mod/gdb/existing.gd.xml");
        var inputPak = BuildPak(
            ("mod/mod.gd.bin", initialGdBin, Compressed: true),
            ("mod/gdb/existing.gd.xml", existingXmlBytes, Compressed: true));
        var inputPakPath = Path.Combine(tempDir, "input.pak");
        File.WriteAllBytes(inputPakPath, inputPak);

        var addedDir = Path.Combine(tempDir, "mod", "gdb");
        Directory.CreateDirectory(addedDir);
        File.WriteAllBytes(Path.Combine(addedDir, "added.gd.xml"), Encoding.UTF8.GetBytes("<entities/>\n"));

        var outputPak = Path.Combine(tempDir, "patched.pak");
        var previousCwd = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(tempDir);
        try
        {
            var result = new PakPatcher().PatchAndReport(
                inputPakPath, outputPak,
                replacementFiles: new[] { "mod/gdb/added.gd.xml" },
                deletions: Array.Empty<string>(),
                jobs: 1,
                registerGdBinAdds: false);
            if (!result.Success) return false;
            if (result.GdbinUpdates.Count != 0) return false;

            using var outStream = File.OpenRead(outputPak);
            var outRead = reader.OpenIndex(outStream);
            if (!outRead.Success) return false;
            var gdbinEntry = outRead.Index!.Entries.First(e => e.Filename == "mod/mod.gd.bin");
            using var extracted = new MemoryStream();
            reader.ExtractEntry(outStream, gdbinEntry, extracted);
            return extracted.ToArray().SequenceEqual(initialGdBin);
        }
        finally
        {
            Directory.SetCurrentDirectory(previousCwd);
        }
    }
    finally
    {
        try { Directory.Delete(tempDir, recursive: true); } catch { /* best effort */ }
    }
}

bool PatchSkipsAutoRegisterWhenNoIndexExists()
{
    var tempDir = Path.Combine(Path.GetTempPath(), "pagonia-paker-tests-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(tempDir);
    try
    {
        // Pak has NO mod/mod.gd.bin. Adding mod/gdb/new.gd.xml should
        // succeed silently (just an Add) — no auto-register, no error.
        var manifest = Encoding.UTF8.GetBytes("{\"Name\":\"mod\",\"Dependencies\":[\"core\"]}");
        var inputPak = BuildPak(("mod/manifest.json", manifest, Compressed: true));
        var inputPakPath = Path.Combine(tempDir, "input.pak");
        File.WriteAllBytes(inputPakPath, inputPak);

        var addedDir = Path.Combine(tempDir, "mod", "gdb");
        Directory.CreateDirectory(addedDir);
        File.WriteAllBytes(Path.Combine(addedDir, "new.gd.xml"), Encoding.UTF8.GetBytes("<entities/>\n"));

        var outputPak = Path.Combine(tempDir, "patched.pak");
        var previousCwd = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(tempDir);
        try
        {
            var result = new PakPatcher().PatchAndReport(
                inputPakPath, outputPak,
                replacementFiles: new[] { "mod/gdb/new.gd.xml" },
                deletions: Array.Empty<string>(),
                jobs: 1,
                registerGdBinAdds: true);
            if (!result.Success) return false;
            if (result.GdbinUpdates.Count != 0) return false;

            using var outStream = File.OpenRead(outputPak);
            var outRead = reader.OpenIndex(outStream);
            if (!outRead.Success || outRead.Index!.Entries.Count != 2) return false;
            return outRead.Index.Entries.Any(e => e.Filename == "mod/gdb/new.gd.xml");
        }
        finally
        {
            Directory.SetCurrentDirectory(previousCwd);
        }
    }
    finally
    {
        try { Directory.Delete(tempDir, recursive: true); } catch { /* best effort */ }
    }
}

static byte[] MakeManifestJson(string name, params string[] dependencies)
{
    var depsList = dependencies.Length == 0 ? "" : "\"" + string.Join("\",\"", dependencies) + "\"";
    var json = $"{{\"Name\":\"{name}\",\"Summary\":\"smoke\",\"Author\":\"tests\",\"Image\":\"\",\"Dependencies\":[{depsList}]}}";
    return Encoding.UTF8.GetBytes(json);
}

bool ClassifyModulePak()
{
    var manifest = MakeManifestJson("mymod", "core");
    var filesJson = Encoding.UTF8.GetBytes("{\"Files\":[{\"Key\":\"GameDatabase\",\"Paths\":[\"mymod/mymod.gd.bin\"]}]}");
    var gdBin = BuildGdBinBytes(0x00, 0x00, "mymod/gdb/buildings.gd.xml"); // 1 entry → byte[3]=0 (the realistic single-entry value), real global content
    var pak = BuildPak(
        ("mymod/manifest.json", manifest, Compressed: true),
        ("mymod/files.json", filesJson, Compressed: true),
        ("mymod/mymod.gd.bin", gdBin, Compressed: false),
        ("mymod/memory.bin", new byte[28], Compressed: false),
        ("mymod/gdb/buildings.gd.xml", Encoding.UTF8.GetBytes("<EntityGroup/>"), Compressed: true));

    var result = new PakClassifier().Classify(new MemoryStream(pak));
    return result.Success
        && result.ModuleFolder == "mymod"
        && result.Name == "mymod"
        && result.GdbScopes.SequenceEqual(new[] { "global" })
        && result.PopmapCount == 0
        && result.Dependencies.Count == 1
        && result.Dependencies[0] == "core"
        && result.OverridesAtRoot.Count == 0;
}

bool ClassifyModuleWithRootGdBin()
{
    // tools.pak ships <m>.gd.bin at the pak root rather than under <m>/.
    // files.json is still under <m>/. Classifier must accept either location.
    var manifest = MakeManifestJson("tools", "core");
    var filesJson = Encoding.UTF8.GetBytes("{\"Files\":[{\"Key\":\"GameDatabase\",\"Paths\":[\"tools.gd.bin\"]}]}");
    var gdBin = BuildGdBinBytes(0x01, 0x00, "tools/gdb/probe.gd.xml"); // 1 entry → byte[3]=0 (the realistic single-entry value), real global content
    var pak = BuildPak(
        ("tools.gd.bin", gdBin, Compressed: false),
        ("tools/files.json", filesJson, Compressed: true),
        ("tools/manifest.json", manifest, Compressed: true),
        ("tools/memory.bin", new byte[28], Compressed: false),
        ("tools/gdb/probe.gd.xml", Encoding.UTF8.GetBytes("<EntityGroup/>"), Compressed: true));

    var result = new PakClassifier().Classify(new MemoryStream(pak));
    return result.Success
        && result.ModuleFolder == "tools"
        && result.GdbScopes.SequenceEqual(new[] { "global" })
        // tools.gd.bin at root is part of the module skeleton, not an override.
        && !result.OverridesAtRoot.Contains("tools.gd.bin")
        && !result.OverridesAtRoot.Contains("files.json");
}

bool ClassifyUserMapPak()
{
    var manifest = MakeManifestJson("my-map", "core");
    var pak = BuildPak(
        ("my-map/manifest.json", manifest, Compressed: true),
        ("my-map/memory.bin", new byte[28], Compressed: false),
        ("my-map/images/preview.image", new byte[] { 0x89, 0x50 }, Compressed: false),
        ("my-map/usermaps/island.popmap", new byte[] { 0x01, 0x02, 0x03 }, Compressed: true),
        ("my-map/usermaps/valley.popmap", new byte[] { 0x04, 0x05, 0x06 }, Compressed: true));

    var result = new PakClassifier().Classify(new MemoryStream(pak));
    return result.Success
        && result.ModuleFolder == "my-map"
        && result.Name == "my-map"
        && result.GdbScopes.Count == 0
        && result.PopmapCount == 2;
}

bool ClassifyOverlayPak()
{
    // Manifest + memory.bin only, nothing else. Asset-only overlay.
    var manifest = MakeManifestJson("ovr", "core");
    var pak = BuildPak(
        ("ovr/manifest.json", manifest, Compressed: true),
        ("ovr/memory.bin", new byte[28], Compressed: false));

    var result = new PakClassifier().Classify(new MemoryStream(pak));
    return result.Success
        && result.ModuleFolder == "ovr"
        && result.GdbScopes.Count == 0
        && result.PopmapCount == 0
        && result.OverridesAtRoot.Count == 0;
}

bool ClassifyOverlaySurfacesOverridesAtRoot()
{
    // System.pak from mod.io: manifest under <m>/ + override files at the
    // pak root (system.json, system.copy.txt).
    var manifest = MakeManifestJson("system", "core");
    var systemJson = Encoding.UTF8.GetBytes("{\"CameraMaxDistance\":330}");
    var systemCopy = Encoding.UTF8.GetBytes("system.json");
    var pak = BuildPak(
        ("system.json", systemJson, Compressed: true),
        ("system.copy.txt", systemCopy, Compressed: false),
        ("system/manifest.json", manifest, Compressed: true),
        ("system/memory.bin", new byte[28], Compressed: false));

    var result = new PakClassifier().Classify(new MemoryStream(pak));
    return result.Success
        && result.ModuleFolder == "system"
        && result.OverridesAtRoot.SequenceEqual(new[] { "system.copy.txt", "system.json" });
}

bool ClassifyUnknownPakWithoutManifest()
{
    // No <m>/manifest.json anywhere. Classifier returns 'unknown' but the
    // pak still parses, so Success == true.
    var pak = BuildPak(
        ("loose.txt", Encoding.UTF8.GetBytes("just some text"), Compressed: false),
        ("data/blob.bin", new byte[] { 1, 2, 3 }, Compressed: false));

    var result = new PakClassifier().Classify(new MemoryStream(pak));
    return result.Success
        && result.ModuleFolder is null
        && result.Name is null
        && result.GdbScopes.Count == 0
        && result.PopmapCount == 0
        // loose.txt sits at the pak root — surfaced as a candidate override
        // so downstream tooling can investigate even without a manifest.
        && result.OverridesAtRoot.Contains("loose.txt");
}

bool ClassifyEditorMapIsMapScoped()
{
    // A published editor map: an EMPTY module-level gd.bin (the editor emits one
    // even for a map-only mod) plus a map-scoped usermaps/<map>.gd.bin that has
    // content, and the popmap. Scope must be ["map-scoped"] — the empty module
    // gd.bin must NOT register as "global". This is the published Example Mod shape.
    var manifest = MakeManifestJson("example mod", "core");
    var filesJson = Encoding.UTF8.GetBytes("{\"Files\":[{\"Key\":\"GameDatabase\",\"Paths\":[\"example mod/example mod.gd.bin\"]}]}");
    var emptyModuleGdBin = BuildGdBinBytes(0x00, 0x00);                              // count=0 → empty skeleton
    var mapGdBin = BuildGdBinBytes(0x00, 0x00, "example mod/usermaps/ecm.gd.xml");   // 1 entry → byte[3]=0 (the realistic single-entry value), the map's database
    var pak = BuildPak(
        ("example mod/manifest.json", manifest, Compressed: true),
        ("example mod/files.json", filesJson, Compressed: true),
        ("example mod/example mod.gd.bin", emptyModuleGdBin, Compressed: false),
        ("example mod/memory.bin", new byte[28], Compressed: false),
        ("example mod/usermaps/ecm.gd.bin", mapGdBin, Compressed: false),
        ("example mod/usermaps/ecm.popmap", new byte[] { 1, 2, 3 }, Compressed: true));

    var result = new PakClassifier().Classify(new MemoryStream(pak));
    return result.Success
        && result.GdbScopes.SequenceEqual(new[] { "map-scoped" })
        && result.PopmapCount == 1;
}

bool ClassifyEditorEmptyGlobalIndexNotGlobal()
{
    // Regression: the real 1.4.0-editor map pak ships its module-level (global) index
    // as header + a zero-length terminator (11 bytes). The old "any byte past the
    // 7-byte header = content" rule mis-read those 4 terminator bytes as content and
    // reported a false `global` scope. With the realistic editor shape the global index
    // is empty, so the pak must classify as ["map-scoped"] only.
    var manifest = MakeManifestJson("example mod", "core");
    var filesJson = Encoding.UTF8.GetBytes("{\"Files\":[{\"Key\":\"GameDatabase\",\"Paths\":[\"example mod/example mod.gd.bin\"]}]}");
    var emptyEditorGdBin = BuildEditorGdBinBytes(0x00, 0x00);                            // header + terminator, 0 real entries
    var mapGdBin = BuildEditorGdBinBytes(0x00, 0x01, "example mod/usermaps/ecm.gd.xml"); // 1 entry + terminator, map-scoped
    var pak = BuildPak(
        ("example mod/manifest.json", manifest, Compressed: true),
        ("example mod/files.json", filesJson, Compressed: true),
        ("example mod/example mod.gd.bin", emptyEditorGdBin, Compressed: false),
        ("example mod/memory.bin", new byte[28], Compressed: false),
        ("example mod/usermaps/ecm.gd.bin", mapGdBin, Compressed: false),
        ("example mod/usermaps/ecm.popmap", new byte[] { 1, 2, 3 }, Compressed: true));

    var result = new PakClassifier().Classify(new MemoryStream(pak));
    return result.Success
        && result.GdbScopes.SequenceEqual(new[] { "map-scoped" })
        && result.PopmapCount == 1;
}

bool ClassifyEditorGdbModWithDependenciesIsGlobal()
{
    // Synthetic stand-in for EE's editor pak "package gdb with dependency" (the real
    // 91 KB binary is kept local per the content policy): a globally-active GDB mod
    // depending on both DLCs, whose module-level index carries one entry plus the
    // 1.4.0 editor's terminator, and which ships a localization blob. Classify must
    // report a single `global` scope, no popmaps, and surface every declared dependency.
    const string mod = "package gdb with dependency";
    var manifest = MakeManifestJson(mod, "core", "decorations1", "dlc1");
    var filesJson = Encoding.UTF8.GetBytes(
        $"{{\"Files\":[{{\"Key\":\"GameDatabase\",\"Paths\":[\"{mod}/{mod}.gd.bin\"]}},{{\"Key\":\"Localization\",\"Paths\":[\"{mod}/localization\"]}}]}}");
    var moduleGdBin = BuildEditorGdBinBytes(0x00, 0x01, $"{mod}/gdb/my_gdb.gd.xml"); // 1 entry + terminator
    var pak = BuildPak(
        ($"{mod}/manifest.json", manifest, Compressed: true),
        ($"{mod}/files.json", filesJson, Compressed: true),
        ($"{mod}/{mod}.gd.bin", moduleGdBin, Compressed: false),
        ($"{mod}/gdb/my_gdb.gd.xml", Encoding.UTF8.GetBytes("<EntityGroup/>"), Compressed: true),
        ($"{mod}/localization/loca_en_us.bin", new byte[] { 0x05, (byte)'H', (byte)'e', (byte)'l', (byte)'l', (byte)'o' }, Compressed: false),
        ($"{mod}/memory.bin", new byte[28], Compressed: false));

    var result = new PakClassifier().Classify(new MemoryStream(pak));
    return result.Success
        && result.GdbScopes.SequenceEqual(new[] { "global" })
        && result.PopmapCount == 0
        && result.Dependencies.Contains("core")
        && result.Dependencies.Contains("decorations1")
        && result.Dependencies.Contains("dlc1");
}

bool ClassifyEditorMapModFullShapeIsMapScopedOnly()
{
    // Synthetic stand-in for EE's editor pak "package map with dlc1 and gdb" (the real
    // 3 MB binary stays local per the content policy): an EMPTY module-level index
    // (header + terminator), a map-scoped usermaps gd.bin with content, a popmap, and
    // DLC dependencies. Classify must report `map-scoped` ONLY (not a false `global`
    // from the terminator), exactly one popmap, and the declared dependencies.
    const string mod = "package map with dlc1 and gdb";
    var manifest = MakeManifestJson(mod, "dlc1", "core", "decorations1");
    var filesJson = Encoding.UTF8.GetBytes(
        $"{{\"Files\":[{{\"Key\":\"GameDatabase\",\"Paths\":[\"{mod}/{mod}.gd.bin\"]}}]}}");
    var emptyGlobal = BuildEditorGdBinBytes(0x00, 0x00);                                // header + terminator, 0 entries
    var mapGdBin = BuildEditorGdBinBytes(0x00, 0x01, $"{mod}/usermaps/my database.gd.xml"); // 1 entry + terminator
    var pak = BuildPak(
        ($"{mod}/manifest.json", manifest, Compressed: true),
        ($"{mod}/files.json", filesJson, Compressed: true),
        ($"{mod}/{mod}.gd.bin", emptyGlobal, Compressed: false),
        ($"{mod}/memory.bin", new byte[28], Compressed: false),
        ($"{mod}/usermaps/my database.gd.bin", mapGdBin, Compressed: false),
        ($"{mod}/usermaps/map with dlc1 and gdb modding.popmap", new byte[] { 1, 2, 3 }, Compressed: true));

    var result = new PakClassifier().Classify(new MemoryStream(pak));
    return result.Success
        && result.GdbScopes.SequenceEqual(new[] { "map-scoped" })
        && result.PopmapCount == 1
        && result.Dependencies.Contains("dlc1")
        && result.Dependencies.Contains("core")
        && result.Dependencies.Contains("decorations1");
}

bool ClassifyGlobalAndMapScopedTogether()
{
    // A mod that changes the GameDatabase globally AND ships a map with its own
    // map-scoped database: both scopes reported, global first.
    var manifest = MakeManifestJson("bothmod", "core");
    var filesJson = Encoding.UTF8.GetBytes("{\"Files\":[{\"Key\":\"GameDatabase\",\"Paths\":[\"bothmod/bothmod.gd.bin\"]}]}");
    var moduleGdBin = BuildGdBinBytes(0x00, 0x00, "bothmod/gdb/rules.gd.xml"); // 1 entry → byte[3]=0 (realistic), global
    var mapGdBin = BuildGdBinBytes(0x00, 0x00, "bothmod/usermaps/m.gd.xml");   // 1 entry → byte[3]=0 (realistic), map-scoped
    var pak = BuildPak(
        ("bothmod/manifest.json", manifest, Compressed: true),
        ("bothmod/files.json", filesJson, Compressed: true),
        ("bothmod/bothmod.gd.bin", moduleGdBin, Compressed: false),
        ("bothmod/memory.bin", new byte[28], Compressed: false),
        ("bothmod/usermaps/m.gd.bin", mapGdBin, Compressed: false),
        ("bothmod/usermaps/m.popmap", new byte[] { 9 }, Compressed: true));

    var result = new PakClassifier().Classify(new MemoryStream(pak));
    return result.Success
        && result.GdbScopes.SequenceEqual(new[] { "global", "map-scoped" });
}

bool ClassifyMultipleModulesPicksFirstWarns()
{
    var manifestA = MakeManifestJson("aaa", "core");
    var manifestB = MakeManifestJson("bbb", "core");
    var pak = BuildPak(
        ("aaa/manifest.json", manifestA, Compressed: true),
        ("aaa/memory.bin", new byte[28], Compressed: false),
        ("bbb/manifest.json", manifestB, Compressed: true),
        ("bbb/memory.bin", new byte[28], Compressed: false));

    var result = new PakClassifier().Classify(new MemoryStream(pak));
    return result.Success
        && result.ModuleFolder == "aaa"  // alphabetically first
        && result.Diagnostics.Any(d => d.Code == DiagnosticCodes.ClassifyMultipleModules
                                       && d.Severity == PakDiagnosticSeverity.Warning);
}

bool ClassifyExtractsManifestDependencies()
{
    var manifest = MakeManifestJson("depmod", "core", "dlc1", "decorations1");
    var pak = BuildPak(
        ("depmod/manifest.json", manifest, Compressed: true),
        ("depmod/memory.bin", new byte[28], Compressed: false));

    var result = new PakClassifier().Classify(new MemoryStream(pak));
    return result.Success
        && result.Name == "depmod"
        && result.Dependencies.Count == 3
        && result.Dependencies[0] == "core"
        && result.Dependencies[1] == "dlc1"
        && result.Dependencies[2] == "decorations1";
}

// --- Schema roundtrip tests ----------------------------------------------------------------------
//
// Each test produces a realistic JSON report via the shipped reporter, then validates it against
// the public schema under schemas/paker/. Catches drift between report record changes and the
// schema file (the same kind of drift we found and fixed in an earlier patcher schema-sync fix, here applied to the
// output side rather than the input side).

bool SchemaRoundtripPakListReport()
{
    var report = new PakListReport(
        Pak: "sample.pak",
        Success: true,
        Version: 2u,
        EntryCount: 1,
        PakInfoPath: "sample/pakinfo.json",
        Entries: new[]
        {
            new PakListEntryReport(Index: 0, Compressed: false, Filename: "sample/file.bin", BeginOffset: 0L, Size: 100L, SizeInPak: 100L),
        },
        Diagnostics: new[]
        {
            new PakReportDiagnostic("Info", "pakListRead", "Read pak", "sample.pak"),
        });

    var json = PakListReport.Serialize(report);
    return ValidateAgainstSchema(json, "pak-list-report.schema.json");
}

bool SchemaRoundtripPakUnpackReport()
{
    var report = new PakUnpackReport(
        Pak: "sample.pak",
        Success: true,
        OutputDir: "out/",
        EntryCount: 2,
        ExtractedCount: 1,
        SkippedCount: 1,
        FailedCount: 0,
        Filter: new PakReportFilter(false, false, null, null, null),
        Entries: new[]
        {
            new PakUnpackEntryReport(0, "sample/a.bin", Compressed: false, Status: "extracted", OutputPath: "out/sample/a.bin"),
            new PakUnpackEntryReport(1, "sample/b.bin", Compressed: false, Status: "skipped", OutputPath: null),
        },
        Diagnostics: Array.Empty<PakReportDiagnostic>());

    var json = PakUnpackReport.Serialize(report);
    return ValidateAgainstSchema(json, "pak-unpack-report.schema.json");
}

bool SchemaRoundtripPakPackReport()
{
    var report = new PakPackReport(
        PakInfo: "sample/pakinfo.json",
        Output: "sample.pak",
        Success: true,
        EntryCount: 3,
        PackedCount: 3,
        Filter: new PakReportFilter(false, false, null, null, null),
        Diagnostics: new[]
        {
            new PakReportDiagnostic("Info", "pakPackWritten", "Wrote 3 entries", "sample.pak"),
        });

    var json = PakPackReport.Serialize(report);
    return ValidateAgainstSchema(json, "pak-pack-report.schema.json");
}

bool SchemaRoundtripPakPatchReport()
{
    var report = new PakPatchReport(
        Input: "in.pak",
        Output: "out.pak",
        Success: true,
        EntryCount: 4,
        ReplacedCount: 1,
        CopiedCount: 3,
        AddedCount: 0,
        DeletedCount: 0,
        Replacements: new[]
        {
            new PakPatchReplacementReport("sample/file.bin", "sample/file.bin"),
        },
        Deletions: Array.Empty<string>(),
        GdbinUpdates: Array.Empty<PakPatchGdBinUpdateReport>(),
        Diagnostics: new[]
        {
            new PakReportDiagnostic("Info", "pakPatchWritten", "Wrote out.pak", "out.pak"),
        });

    var json = PakPatchReport.Serialize(report);
    return ValidateAgainstSchema(json, "pak-patch-report.schema.json");
}

bool SchemaRoundtripPakClassifyReport()
{
    var report = new PakClassifyReport(
        Pak: "module.pak",
        Success: true,
        Name: "mymod",
        ModuleFolder: "mymod",
        Dependencies: new[] { "core" },
        GdbScopes: new[] { "global" },
        PopmapCount: 0,
        OverridesAtRoot: Array.Empty<string>(),
        Diagnostics: new[]
        {
            new PakReportDiagnostic("Info", "pakClassified", "Classified module", "mymod"),
        });

    var json = PakClassifyReport.Serialize(report);
    return ValidateAgainstSchema(json, "pak-classify-report.schema.json");
}

bool SchemaRoundtripGdBinInfoReport()
{
    var report = new GdBinInfoReport(
        Gdbin: "mymod/mymod.gd.bin",
        Success: true,
        EntryCount: 2,
        HeaderBytesHex: new[] { "0x03", "0x00", "0x02", "0x01", "0x00", "0x00", "0x00" },
        Entries: new[] { "mymod/gdb/a.gd.xml", "mymod/gdb/b.gd.xml" },
        HasTrailingTerminator: false,
        Diagnostics: new[]
        {
            new PakReportDiagnostic("Info", "gdbinRead", "Read 2 entries", "mymod/mymod.gd.bin"),
        });

    var json = GdBinInfoReport.Serialize(report);
    return ValidateAgainstSchema(json, "gdbin-info-report.schema.json");
}

bool SchemaRoundtripLocaInfoReport()
{
    var report = new LocaInfoReport(
        Loca: "mymod/localization/loca_en_us.bin",
        Success: true,
        StringCount: 2,
        Strings: new[] { "My Animal Farm", "Map exclusive" },
        Diagnostics: new[]
        {
            new PakReportDiagnostic("Info", "locaRead", "Read loca blob with 2 strings.", null),
        });

    var json = LocaInfoReport.Serialize(report);
    return ValidateAgainstSchema(json, "loca-info-report.schema.json");
}

bool ValidateAgainstSchema(string json, string schemaFileName)
{
    var schemaPath = Path.Combine(FindRepositoryRoot(), "schemas", "paker", schemaFileName);
    if (!File.Exists(schemaPath))
    {
        Console.Error.WriteLine($"Schema file not found: {schemaPath}");
        return false;
    }

    var schema = JsonSchema.FromFile(schemaPath);
    using var doc = System.Text.Json.JsonDocument.Parse(json);
    var results = schema.Evaluate(doc.RootElement, new EvaluationOptions { OutputFormat = OutputFormat.Hierarchical });

    if (results.IsValid)
    {
        return true;
    }

    // Surface every failure with location + keyword so a regression is debuggable from the test
    // output alone.
    DumpErrors(results, depth: 0);
    return false;
}

void DumpErrors(EvaluationResults result, int depth)
{
    if (result.IsValid)
    {
        return;
    }

    var indent = new string(' ', depth * 2);
    if (result.Errors is { Count: > 0 })
    {
        foreach (var (keyword, message) in result.Errors)
        {
            var location = result.InstanceLocation.ToString();
            var locationHint = string.IsNullOrEmpty(location) ? "(root)" : location;
            Console.Error.WriteLine($"{indent}schema error at {locationHint}: {message} [{keyword}]");
        }
    }

    foreach (var child in result.Details ?? [])
    {
        DumpErrors(child, depth + 1);
    }
}

static string FindRepositoryRoot()
{
    var directory = new DirectoryInfo(AppContext.BaseDirectory);
    while (directory is not null)
    {
        if (File.Exists(Path.Combine(directory.FullName, "README.md"))
            && Directory.Exists(Path.Combine(directory.FullName, "schemas")))
        {
            return directory.FullName;
        }
        directory = directory.Parent;
    }

    throw new InvalidOperationException("Could not find repository root.");
}
