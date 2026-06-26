using PagoniaLand.Paker;

var reader = new PakReader();
var packer = new PakPacker();
var patcher = new PakPatcher();
var gdBinReader = new GdBinReader();
var locaReader = new LocaReader();
var classifier = new PakClassifier();

if (args is ["--version"] or ["-v"])
{
    Console.WriteLine($"{PakerInfo.ProductName} {PakerInfo.Version}");
    return PakerExitCodes.Success;
}

if (args.Length >= 1 && args[0] == "list")
{
    var parsed = FilterArgumentParser.Parse(args[1..]);
    if (!parsed.Success)
    {
        Console.Error.WriteLine($"Error: {parsed.Error}");
        return PakerExitCodes.Usage;
    }
    if (!parsed.Filter.IsUnrestricted)
    {
        Console.Error.WriteLine("Error: filter flags are not supported on `list`. Use them on `unpack` or `pack`.");
        return PakerExitCodes.Usage;
    }
    if (parsed.Positional.Count < 1)
    {
        PrintUsage();
        return PakerExitCodes.Usage;
    }
    var pakPath = parsed.Positional[0];
    var outputDir = parsed.Positional.Count >= 2 ? parsed.Positional[1] : DefaultOutputDir(pakPath);
    return RunList(pakPath, outputDir, parsed.JsonReportPath);
}

if (args.Length >= 1 && args[0] == "unpack")
{
    var parsed = FilterArgumentParser.Parse(args[1..]);
    if (!parsed.Success)
    {
        Console.Error.WriteLine($"Error: {parsed.Error}");
        return PakerExitCodes.Usage;
    }
    if (parsed.Positional.Count < 1)
    {
        PrintUsage();
        return PakerExitCodes.Usage;
    }
    var pakPath = parsed.Positional[0];
    var outputDir = parsed.Positional.Count >= 2 ? parsed.Positional[1] : DefaultOutputDir(pakPath);
    return RunUnpack(pakPath, outputDir, parsed.Filter, parsed.JsonReportPath, ResolveJobs(parsed.Jobs));
}

if (args.Length >= 1 && args[0] == "pack")
{
    var parsed = FilterArgumentParser.Parse(args[1..]);
    if (!parsed.Success)
    {
        Console.Error.WriteLine($"Error: {parsed.Error}");
        return PakerExitCodes.Usage;
    }
    if (parsed.Positional.Count < 1)
    {
        PrintUsage();
        return PakerExitCodes.Usage;
    }
    var pakInfoPath = parsed.Positional[0];
    var outputPak = parsed.Positional.Count >= 2 ? parsed.Positional[1] : DefaultPackOutput(pakInfoPath);
    return RunPack(pakInfoPath, outputPak, parsed.Filter, parsed.JsonReportPath, ResolveJobs(parsed.Jobs));
}

if (args.Length >= 1 && args[0] == "patch")
{
    var parsed = FilterArgumentParser.Parse(args[1..]);
    if (!parsed.Success)
    {
        Console.Error.WriteLine($"Error: {parsed.Error}");
        return PakerExitCodes.Usage;
    }
    if (!parsed.Filter.IsUnrestricted)
    {
        Console.Error.WriteLine("Error: filter flags are not supported on `patch`. Use them on `unpack` or `pack`.");
        return PakerExitCodes.Usage;
    }
    if (parsed.Positional.Count < 2)
    {
        PrintUsage();
        return PakerExitCodes.Usage;
    }
    if (parsed.Positional.Count < 3 && parsed.Deletions.Count == 0)
    {
        Console.Error.WriteLine("Error: patch needs at least one replacement file or --delete <path>.");
        return PakerExitCodes.Usage;
    }
    var inputPak = parsed.Positional[0];
    var outputPak = parsed.Positional[1];
    var replacements = parsed.Positional.Skip(2).ToArray();
    return RunPatch(inputPak, outputPak, replacements, parsed.Deletions, parsed.JsonReportPath, ResolveJobs(parsed.Jobs), registerGdBinAdds: !parsed.NoGdBinRegister);
}

if (args.Length >= 3 && args[0] == "compress")
{
    return RunCompressOrDecompress(args[1], args[2], compress: true);
}

if (args.Length >= 3 && args[0] == "decompress")
{
    return RunCompressOrDecompress(args[1], args[2], compress: false);
}

if (args.Length >= 2 && args[0] == "gdbin" && args[1] == "info")
{
    var parsed = FilterArgumentParser.Parse(args[2..]);
    if (!parsed.Success)
    {
        Console.Error.WriteLine($"Error: {parsed.Error}");
        return PakerExitCodes.Usage;
    }
    if (!parsed.Filter.IsUnrestricted)
    {
        Console.Error.WriteLine("Error: filter flags are not supported on `gdbin info`.");
        return PakerExitCodes.Usage;
    }
    if (parsed.Positional.Count < 1)
    {
        PrintUsage();
        return PakerExitCodes.Usage;
    }
    return RunGdBinInfo(parsed.Positional[0], parsed.JsonReportPath);
}

if (args.Length >= 2 && args[0] == "loca" && args[1] == "info")
{
    var parsed = FilterArgumentParser.Parse(args[2..]);
    if (!parsed.Success)
    {
        Console.Error.WriteLine($"Error: {parsed.Error}");
        return PakerExitCodes.Usage;
    }
    if (!parsed.Filter.IsUnrestricted)
    {
        Console.Error.WriteLine("Error: filter flags are not supported on `loca info`.");
        return PakerExitCodes.Usage;
    }
    if (parsed.Positional.Count < 1)
    {
        PrintUsage();
        return PakerExitCodes.Usage;
    }
    return RunLocaInfo(parsed.Positional[0], parsed.JsonReportPath);
}

if (args.Length >= 1 && args[0] == "classify")
{
    var parsed = FilterArgumentParser.Parse(args[1..]);
    if (!parsed.Success)
    {
        Console.Error.WriteLine($"Error: {parsed.Error}");
        return PakerExitCodes.Usage;
    }
    if (!parsed.Filter.IsUnrestricted)
    {
        Console.Error.WriteLine("Error: filter flags are not supported on `classify`.");
        return PakerExitCodes.Usage;
    }
    if (parsed.Positional.Count < 1)
    {
        PrintUsage();
        return PakerExitCodes.Usage;
    }
    return RunClassify(parsed.Positional[0], parsed.JsonReportPath);
}

PrintUsage();
return PakerExitCodes.Usage;

int RunList(string pakPath, string outputDir, string? jsonReportPath)
{
    var diagnostics = new List<PakDiagnostic>();

    if (!File.Exists(pakPath))
    {
        Console.Error.WriteLine($"Error: pak file not found: {pakPath}");
        WriteListReport(jsonReportPath, pakPath, success: false, version: 0, entries: Array.Empty<PakListEntryReport>(), pakInfoPath: null, diagnostics);
        return PakerExitCodes.Error;
    }

    using var pakStream = File.OpenRead(pakPath);
    var result = reader.OpenIndex(pakStream);
    diagnostics.AddRange(result.Diagnostics);
    PrintDiagnostics(result.Diagnostics);

    if (!result.Success || result.Index is null)
    {
        WriteListReport(jsonReportPath, pakPath, success: false, version: 0, entries: Array.Empty<PakListEntryReport>(), pakInfoPath: null, diagnostics);
        return PakerExitCodes.Error;
    }

    Directory.CreateDirectory(outputDir);
    var pakInfo = PakReader.BuildPakInfo(result.Index);
    var json = PakReader.SerializePakInfo(pakInfo);
    var pakInfoPath = Path.Combine(outputDir, "pakinfo.json");
    File.WriteAllText(pakInfoPath, json);

    Console.WriteLine($"Pak: {pakPath}");
    Console.WriteLine($"Version: {pakInfo.Version}");
    Console.WriteLine($"Entries: {pakInfo.Count}");
    Console.WriteLine($"Wrote: {pakInfoPath}");

    var entryReports = result.Index.Entries
        .Select((e, i) => new PakListEntryReport(i, e.Compressed, e.Filename, e.BeginOffset, e.Size, e.SizeInPak))
        .ToList();
    WriteListReport(jsonReportPath, pakPath, success: true, version: result.Index.Version, entries: entryReports, pakInfoPath, diagnostics);

    return PakerExitCodes.Success;
}

int RunUnpack(string pakPath, string outputDir, PakFilter filter, string? jsonReportPath, int jobs)
{
    var diagnostics = new List<PakDiagnostic>();
    var entryReports = new List<PakUnpackEntryReport>();

    if (!File.Exists(pakPath))
    {
        Console.Error.WriteLine($"Error: pak file not found: {pakPath}");
        WriteUnpackReport(jsonReportPath, pakPath, outputDir, filter, 0, 0, 0, 0, entryReports, diagnostics, success: false);
        return PakerExitCodes.Error;
    }

    using var pakStream = File.OpenRead(pakPath);
    var result = reader.OpenIndex(pakStream);
    diagnostics.AddRange(result.Diagnostics);
    PrintDiagnostics(result.Diagnostics);

    if (!result.Success || result.Index is null)
    {
        WriteUnpackReport(jsonReportPath, pakPath, outputDir, filter, 0, 0, 0, 0, entryReports, diagnostics, success: false);
        return PakerExitCodes.Error;
    }

    Directory.CreateDirectory(outputDir);

    // Resolve filter outcomes up front so the parallel loop only has to do the actual
    // I/O. The report rows are pre-sized so workers can drop into their slot lock-free.
    var rows = new PakUnpackEntryReport?[result.Index.Entries.Count];
    var skipped = 0;
    var failed = 0;
    var preplanned = new List<(int Index, PakEntry Entry, string OutputPath)>(result.Index.Entries.Count);

    for (var i = 0; i < result.Index.Entries.Count; i++)
    {
        var entry = result.Index.Entries[i];
        if (!filter.Matches(i, entry))
        {
            skipped++;
            rows[i] = new PakUnpackEntryReport(i, entry.Filename, entry.Compressed, "skipped", OutputPath: null);
            continue;
        }

        var safeRelative = ToSafeRelativePath(entry.Filename);
        if (safeRelative is null)
        {
            Console.Error.WriteLine($"Error: refusing to extract entry with unsafe path '{entry.Filename}'.");
            failed++;
            rows[i] = new PakUnpackEntryReport(i, entry.Filename, entry.Compressed, "failed", OutputPath: null);
            continue;
        }

        var outputPath = Path.Combine(outputDir, safeRelative);
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        preplanned.Add((i, entry, outputPath));
    }

    var written = 0;

    // Each worker opens its own read-only FileStream over the pak so seeks
    // don't race. Output files are all distinct, so writes never contend.
    var parallelOptions = new System.Threading.Tasks.ParallelOptions { MaxDegreeOfParallelism = jobs };
    System.Threading.Tasks.Parallel.ForEach<
        (int Index, PakEntry Entry, string OutputPath),
        FileStream?>(
        preplanned,
        parallelOptions,
        localInit: () => null,
        body: (item, state, _, localPakStream) =>
        {
            localPakStream ??= File.Open(pakPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            try
            {
                using var outFile = new FileStream(item.OutputPath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 1 << 20, FileOptions.SequentialScan);
                reader.ExtractEntry(localPakStream, item.Entry, outFile);
                System.Threading.Interlocked.Increment(ref written);
                rows[item.Index] = new PakUnpackEntryReport(item.Index, item.Entry.Filename, item.Entry.Compressed, "extracted", item.OutputPath);
            }
            catch (Exception exception) when (exception is IOException or InvalidDataException or EndOfStreamException)
            {
                Console.Error.WriteLine($"Error: failed to extract '{item.Entry.Filename}': {exception.Message}");
                System.Threading.Interlocked.Increment(ref failed);
                rows[item.Index] = new PakUnpackEntryReport(item.Index, item.Entry.Filename, item.Entry.Compressed, "failed", OutputPath: null);
            }
            return localPakStream;
        },
        localFinally: localPakStream => localPakStream?.Dispose());

    entryReports.AddRange(rows.Select(r => r ?? throw new InvalidOperationException("Internal: unfilled report row.")));

    Console.WriteLine($"Pak: {pakPath}");
    Console.WriteLine($"Entries: {result.Index.Entries.Count}");
    Console.WriteLine($"Extracted: {written}");
    if (skipped > 0) Console.WriteLine($"Skipped (filter): {skipped}");
    if (failed > 0) Console.WriteLine($"Failed: {failed}");

    var success = failed == 0;
    WriteUnpackReport(jsonReportPath, pakPath, outputDir, filter, result.Index.Entries.Count, written, skipped, failed, entryReports, diagnostics, success);

    if (!success) return PakerExitCodes.Error;
    Console.WriteLine($"Output: {outputDir}");
    return PakerExitCodes.Success;
}

int RunPack(string pakInfoPath, string outputPak, PakFilter filter, string? jsonReportPath, int jobs)
{
    if (!File.Exists(pakInfoPath))
    {
        Console.Error.WriteLine($"Error: pakinfo file not found: {pakInfoPath}");
        WritePackReport(jsonReportPath, pakInfoPath, outputPak, filter, entryCount: 0, packedCount: 0, success: false, Array.Empty<PakDiagnostic>());
        return PakerExitCodes.Error;
    }

    var diagnostics = packer.Pack(pakInfoPath, outputPak, filter, jobs);
    PrintDiagnostics(diagnostics);

    var (entryCount, packedCount) = ExtractPackCounts(diagnostics);
    var success = !diagnostics.Any(d => d.Severity == PakDiagnosticSeverity.Error);

    WritePackReport(jsonReportPath, pakInfoPath, outputPak, filter, entryCount, packedCount, success, diagnostics);

    if (!success) return PakerExitCodes.Error;

    Console.WriteLine($"Pakinfo: {pakInfoPath}");
    Console.WriteLine($"Output: {outputPak}");
    return PakerExitCodes.Success;
}

int RunPatch(string inputPak, string outputPak, IReadOnlyList<string> replacements, IReadOnlyList<string> deletions, string? jsonReportPath, int jobs, bool registerGdBinAdds)
{
    var result = patcher.PatchAndReport(inputPak, outputPak, replacements, deletions, jobs, registerGdBinAdds);
    PrintDiagnostics(result.Diagnostics);

    var success = result.Success;
    var (entryCount, replacedCount, addedCount, deletedCount) = ExtractPatchCounts(result.Diagnostics);

    var replacementReports = replacements
        .Select(file => new PakPatchReplacementReport(EntryName: file.Replace('\\', '/'), SourcePath: file))
        .ToList();
    var deletionReports = deletions
        .Select(d => d.Replace('\\', '/'))
        .ToList();
    var gdbinReports = result.GdbinUpdates
        .Select(u => new PakPatchGdBinUpdateReport(u.EntryName, u.Added))
        .ToList();

    WritePatchReport(jsonReportPath, inputPak, outputPak, entryCount, replacedCount, addedCount, deletedCount, replacementReports, deletionReports, gdbinReports, result.Diagnostics, success);

    if (!success) return PakerExitCodes.Error;

    Console.WriteLine($"Input:  {inputPak}");
    Console.WriteLine($"Output: {outputPak}");
    Console.WriteLine($"Replaced: {replacedCount}");
    if (addedCount > 0) Console.WriteLine($"Added:    {addedCount}");
    if (deletedCount > 0) Console.WriteLine($"Deleted:  {deletedCount}");
    if (gdbinReports.Count > 0)
    {
        foreach (var update in gdbinReports)
        {
            Console.WriteLine($"Gdbin:    {update.EntryName} (+{update.Added.Count})");
        }
    }
    return PakerExitCodes.Success;
}

int RunCompressOrDecompress(string inputPath, string outputPath, bool compress)
{
    if (!File.Exists(inputPath))
    {
        Console.Error.WriteLine($"Error: input file not found: {inputPath}");
        return PakerExitCodes.Error;
    }

    var outputDir = Path.GetDirectoryName(Path.GetFullPath(outputPath));
    if (!string.IsNullOrEmpty(outputDir))
    {
        Directory.CreateDirectory(outputDir);
    }

    try
    {
        using var input = File.OpenRead(inputPath);
        using var output = File.Create(outputPath);
        if (compress)
        {
            GzipCompressor.Compress(input, output);
        }
        else
        {
            GzipCompressor.Decompress(input, output);
        }
    }
    catch (Exception exception) when (exception is IOException or InvalidDataException)
    {
        Console.Error.WriteLine($"Error: {(compress ? "compress" : "decompress")} failed for '{inputPath}': {exception.Message}");
        return PakerExitCodes.Error;
    }

    Console.WriteLine($"{(compress ? "Compressed" : "Decompressed")}: {inputPath} -> {outputPath}");
    return PakerExitCodes.Success;
}

int RunClassify(string pakPath, string? jsonReportPath)
{
    var diagnostics = new List<PakDiagnostic>();

    if (!File.Exists(pakPath))
    {
        Console.Error.WriteLine($"Error: pak file not found: {pakPath}");
        WriteClassifyReport(jsonReportPath, pakPath, success: false,
            name: null, moduleFolder: null,
            dependencies: Array.Empty<string>(), gdbScopes: Array.Empty<string>(),
            popmapCount: 0, overridesAtRoot: Array.Empty<string>(), diagnostics);
        return PakerExitCodes.Error;
    }

    using var pakStream = File.OpenRead(pakPath);
    var result = classifier.Classify(pakStream);
    diagnostics.AddRange(result.Diagnostics);
    PrintDiagnostics(result.Diagnostics);

    // classify exits 0 whenever the pak parsed (even with no module found); only a
    // hard pak-parse error trips the non-zero code, surfaced via Success=false.
    if (!result.Success)
    {
        WriteClassifyReport(jsonReportPath, pakPath, success: false,
            name: result.Name, moduleFolder: result.ModuleFolder,
            dependencies: result.Dependencies, gdbScopes: result.GdbScopes,
            popmapCount: result.PopmapCount, overridesAtRoot: result.OverridesAtRoot, diagnostics);
        return PakerExitCodes.Error;
    }

    Console.WriteLine($"Pak: {pakPath}");
    Console.WriteLine($"Module: {result.ModuleFolder ?? "(none)"}");
    Console.WriteLine($"Name: {result.Name ?? "(unknown)"}");
    Console.WriteLine($"Dependencies: {(result.Dependencies.Count == 0 ? "(none)" : string.Join(", ", result.Dependencies))}");
    Console.WriteLine($"GdbScopes: {(result.GdbScopes.Count == 0 ? "(none)" : string.Join(", ", result.GdbScopes))}");
    Console.WriteLine($"Popmaps: {result.PopmapCount}");
    if (result.OverridesAtRoot.Count > 0)
    {
        Console.WriteLine($"OverridesAtRoot: {string.Join(", ", result.OverridesAtRoot)}");
    }

    WriteClassifyReport(jsonReportPath, pakPath, success: true,
        name: result.Name, moduleFolder: result.ModuleFolder,
        dependencies: result.Dependencies, gdbScopes: result.GdbScopes,
        popmapCount: result.PopmapCount, overridesAtRoot: result.OverridesAtRoot, diagnostics);
    return PakerExitCodes.Success;
}

static void WriteClassifyReport(
    string? jsonReportPath, string pak, bool success,
    string? name, string? moduleFolder,
    IReadOnlyList<string> dependencies, IReadOnlyList<string> gdbScopes, int popmapCount,
    IReadOnlyList<string> overridesAtRoot, IReadOnlyList<PakDiagnostic> diagnostics)
{
    if (string.IsNullOrWhiteSpace(jsonReportPath)) return;
    var report = new PakClassifyReport(
        pak, success, name, moduleFolder, dependencies,
        gdbScopes, popmapCount, overridesAtRoot,
        PakReportDiagnostic.FromAll(diagnostics));
    WriteReportFile(jsonReportPath, PakClassifyReport.Serialize(report));
}

int RunGdBinInfo(string gdbinPath, string? jsonReportPath)
{
    var diagnostics = new List<PakDiagnostic>();

    if (!File.Exists(gdbinPath))
    {
        Console.Error.WriteLine($"Error: gd.bin file not found: {gdbinPath}");
        WriteGdBinInfoReport(jsonReportPath, gdbinPath, success: false, headerBytes: Array.Empty<byte>(), entries: Array.Empty<string>(), hasTrailingTerminator: false, diagnostics);
        return PakerExitCodes.Error;
    }

    using var stream = File.OpenRead(gdbinPath);
    var result = gdBinReader.Read(stream);
    diagnostics.AddRange(result.Diagnostics);
    PrintDiagnostics(result.Diagnostics);

    if (!result.Success || result.Index is null)
    {
        WriteGdBinInfoReport(jsonReportPath, gdbinPath, success: false, headerBytes: Array.Empty<byte>(), entries: Array.Empty<string>(), hasTrailingTerminator: false, diagnostics);
        return PakerExitCodes.Error;
    }

    Console.WriteLine($"Gdbin: {gdbinPath}");
    Console.WriteLine($"Entries: {result.Index.Entries.Count}");
    Console.WriteLine($"Header: {string.Join(' ', GdBinInfoReport.HeaderBytesToHex(result.Index.HeaderBytes))}");
    Console.WriteLine($"EditorTerminator: {(result.Index.HasTrailingTerminator ? "yes" : "no")}");
    for (var i = 0; i < result.Index.Entries.Count; i++)
    {
        Console.WriteLine($"  [{i}] {result.Index.Entries[i]}");
    }

    WriteGdBinInfoReport(jsonReportPath, gdbinPath, success: true, result.Index.HeaderBytes, result.Index.Entries, result.Index.HasTrailingTerminator, diagnostics);
    return PakerExitCodes.Success;
}

static void WriteGdBinInfoReport(
    string? jsonReportPath, string gdbin, bool success,
    IReadOnlyList<byte> headerBytes, IReadOnlyList<string> entries, bool hasTrailingTerminator,
    IReadOnlyList<PakDiagnostic> diagnostics)
{
    if (string.IsNullOrWhiteSpace(jsonReportPath)) return;
    var report = new GdBinInfoReport(
        gdbin, success, entries.Count,
        GdBinInfoReport.HeaderBytesToHex(headerBytes), entries, hasTrailingTerminator,
        PakReportDiagnostic.FromAll(diagnostics));
    WriteReportFile(jsonReportPath, GdBinInfoReport.Serialize(report));
}

int RunLocaInfo(string locaPath, string? jsonReportPath)
{
    var diagnostics = new List<PakDiagnostic>();

    if (!File.Exists(locaPath))
    {
        Console.Error.WriteLine($"Error: loca file not found: {locaPath}");
        WriteLocaInfoReport(jsonReportPath, locaPath, success: false, strings: Array.Empty<string>(), diagnostics);
        return PakerExitCodes.Error;
    }

    using var stream = File.OpenRead(locaPath);
    var result = locaReader.Read(stream);
    diagnostics.AddRange(result.Diagnostics);
    PrintDiagnostics(result.Diagnostics);

    if (!result.Success || result.Strings is null)
    {
        WriteLocaInfoReport(jsonReportPath, locaPath, success: false, strings: Array.Empty<string>(), diagnostics);
        return PakerExitCodes.Error;
    }

    Console.WriteLine($"Loca: {locaPath}");
    Console.WriteLine($"Strings: {result.Strings.Count}");
    for (var i = 0; i < result.Strings.Count; i++)
    {
        Console.WriteLine($"  [{i}] {result.Strings[i]}");
    }

    WriteLocaInfoReport(jsonReportPath, locaPath, success: true, result.Strings, diagnostics);
    return PakerExitCodes.Success;
}

static void WriteLocaInfoReport(
    string? jsonReportPath, string loca, bool success,
    IReadOnlyList<string> strings, IReadOnlyList<PakDiagnostic> diagnostics)
{
    if (string.IsNullOrWhiteSpace(jsonReportPath)) return;
    var report = new LocaInfoReport(
        loca, success, strings.Count, strings,
        PakReportDiagnostic.FromAll(diagnostics));
    WriteReportFile(jsonReportPath, LocaInfoReport.Serialize(report));
}

static void WriteListReport(
    string? jsonReportPath, string pak, bool success, uint version,
    IReadOnlyList<PakListEntryReport> entries, string? pakInfoPath,
    IReadOnlyList<PakDiagnostic> diagnostics)
{
    if (string.IsNullOrWhiteSpace(jsonReportPath)) return;
    var report = new PakListReport(pak, success, version, entries.Count, pakInfoPath, entries, PakReportDiagnostic.FromAll(diagnostics));
    WriteReportFile(jsonReportPath, PakListReport.Serialize(report));
}

static void WriteUnpackReport(
    string? jsonReportPath, string pak, string outputDir, PakFilter filter,
    int entryCount, int extracted, int skipped, int failed,
    IReadOnlyList<PakUnpackEntryReport> entries,
    IReadOnlyList<PakDiagnostic> diagnostics, bool success)
{
    if (string.IsNullOrWhiteSpace(jsonReportPath)) return;
    var report = new PakUnpackReport(pak, success, outputDir, entryCount, extracted, skipped, failed,
        PakReportFilter.From(filter), entries, PakReportDiagnostic.FromAll(diagnostics));
    WriteReportFile(jsonReportPath, PakUnpackReport.Serialize(report));
}

static void WritePackReport(
    string? jsonReportPath, string pakInfo, string output, PakFilter filter,
    int entryCount, int packedCount, bool success,
    IReadOnlyList<PakDiagnostic> diagnostics)
{
    if (string.IsNullOrWhiteSpace(jsonReportPath)) return;
    var report = new PakPackReport(pakInfo, output, success, entryCount, packedCount,
        PakReportFilter.From(filter), PakReportDiagnostic.FromAll(diagnostics));
    WriteReportFile(jsonReportPath, PakPackReport.Serialize(report));
}

static void WritePatchReport(
    string? jsonReportPath, string input, string output, int entryCount, int replaced, int added, int deleted,
    IReadOnlyList<PakPatchReplacementReport> replacements,
    IReadOnlyList<string> deletions,
    IReadOnlyList<PakPatchGdBinUpdateReport> gdbinUpdates,
    IReadOnlyList<PakDiagnostic> diagnostics, bool success)
{
    if (string.IsNullOrWhiteSpace(jsonReportPath)) return;
    var copied = Math.Max(0, entryCount - replaced - deleted);
    var report = new PakPatchReport(input, output, success, entryCount, replaced, copied, added, deleted, replacements, deletions, gdbinUpdates, PakReportDiagnostic.FromAll(diagnostics));
    WriteReportFile(jsonReportPath, PakPatchReport.Serialize(report));
}

static void WriteReportFile(string path, string content)
{
    var directory = Path.GetDirectoryName(Path.GetFullPath(path));
    if (!string.IsNullOrEmpty(directory))
    {
        Directory.CreateDirectory(directory);
    }
    File.WriteAllText(path, content);
}

static (int EntryCount, int PackedCount) ExtractPackCounts(IReadOnlyList<PakDiagnostic> diagnostics)
{
    // The info-level pakPackWritten diagnostic carries the "packed N of M" / "packed N" wording.
    // Parsing it back here keeps PakPacker's signature compact; the alternative would be a
    // dedicated return type just for the CLI.
    var written = diagnostics.FirstOrDefault(d => d.Code == DiagnosticCodes.PakPackWritten);
    if (written is null) return (0, 0);
    var match = System.Text.RegularExpressions.Regex.Match(written.Message, @"Packed (\d+) of (\d+) entries");
    if (match.Success) return (int.Parse(match.Groups[2].Value), int.Parse(match.Groups[1].Value));
    match = System.Text.RegularExpressions.Regex.Match(written.Message, @"Packed (\d+) entries");
    if (match.Success) return (int.Parse(match.Groups[1].Value), int.Parse(match.Groups[1].Value));
    return (0, 0);
}

static (int EntryCount, int ReplacedCount, int AddedCount, int DeletedCount) ExtractPatchCounts(IReadOnlyList<PakDiagnostic> diagnostics)
{
    var written = diagnostics.FirstOrDefault(d => d.Code == DiagnosticCodes.PakPatchWritten);
    if (written is null) return (0, 0, 0, 0);
    var match = System.Text.RegularExpressions.Regex.Match(
        written.Message,
        @"Patched (\d+) of (\d+) entries \((\d+) added, (\d+) deleted\)");
    if (match.Success)
    {
        return (
            int.Parse(match.Groups[2].Value),
            int.Parse(match.Groups[1].Value),
            int.Parse(match.Groups[3].Value),
            int.Parse(match.Groups[4].Value));
    }
    return (0, 0, 0, 0);
}

static int ResolveJobs(int? requested)
    => requested ?? Math.Max(1, Environment.ProcessorCount);

static string DefaultOutputDir(string pakPath)
{
    var directory = Path.GetDirectoryName(pakPath);
    var name = Path.GetFileNameWithoutExtension(pakPath);
    return string.IsNullOrWhiteSpace(directory) ? name : Path.Combine(directory, name);
}

static string DefaultPackOutput(string pakInfoPath)
{
    // plpaker convention: pack <pakinfo.json> at <dir>/pakinfo.json produces <dir>.pak as a sibling.
    var fullDir = Path.GetDirectoryName(Path.GetFullPath(pakInfoPath));
    if (string.IsNullOrEmpty(fullDir))
    {
        return Path.ChangeExtension(pakInfoPath, ".pak");
    }
    var dirName = Path.GetFileName(fullDir);
    var parent = Path.GetDirectoryName(fullDir);
    return string.IsNullOrEmpty(parent) ? dirName + ".pak" : Path.Combine(parent, dirName + ".pak");
}

static string? ToSafeRelativePath(string filename)
{
    if (string.IsNullOrWhiteSpace(filename)) return null;

    var normalised = filename.Replace('\\', '/');
    if (normalised.StartsWith('/')) return null;
    if (Path.IsPathRooted(normalised)) return null;

    foreach (var segment in normalised.Split('/'))
    {
        if (segment == "." || segment == "..") return null;
    }

    return normalised.Replace('/', Path.DirectorySeparatorChar);
}

static void PrintDiagnostics(IEnumerable<PakDiagnostic> diagnostics)
{
    foreach (var diagnostic in diagnostics)
    {
        var stream = diagnostic.Severity == PakDiagnosticSeverity.Error ? Console.Error : Console.Out;
        stream.WriteLine($"{diagnostic.Severity}: {diagnostic.Code}: {diagnostic.Message}");
    }
}

static void PrintUsage()
{
    Console.WriteLine(PakerInfo.ProductName);
    Console.WriteLine();
    Console.WriteLine("Usage:");
    Console.WriteLine("  pagonia-paker --version");
    Console.WriteLine("  pagonia-paker list       [--json <report>] <pak> [<output-dir>]");
    Console.WriteLine("  pagonia-paker unpack     [filters] [--json <report>] <pak> [<output-dir>]");
    Console.WriteLine("  pagonia-paker pack       [filters] [--json <report>] <pakinfo.json> [<output.pak>]");
    Console.WriteLine("  pagonia-paker patch      [--json <report>] [--delete <path> ...] <input.pak> <output.pak> [<file> ...]");
    Console.WriteLine("  pagonia-paker compress   <input> <output>");
    Console.WriteLine("  pagonia-paker decompress <input> <output>");
    Console.WriteLine("  pagonia-paker gdbin info [--json <report>] <gdbin>");
    Console.WriteLine("  pagonia-paker loca info  [--json <report>] <loca>");
    Console.WriteLine("  pagonia-paker classify   [--json <report>] <pak>");
    Console.WriteLine();
    Console.WriteLine("Filters (apply to unpack and pack; AND-composed; entry indices are 0-based):");
    Console.WriteLine("  -c, --compress             only entries marked compressed=true");
    Console.WriteLine("  -d, --decompress           only entries marked compressed=false");
    Console.WriteLine("  -s, --start=<n>            only entries with index >= n");
    Console.WriteLine("  -e, --end=<n>              only entries with index <= n");
    Console.WriteLine("  -f, --filter=<substring>   only entries whose filename contains <substring>");
    Console.WriteLine();
    Console.WriteLine("Patch operations (positional paths classify by base-pak presence):");
    Console.WriteLine("  <file>                     replace if base pak has the entry; add as new entry otherwise");
    Console.WriteLine("  --delete <path>            omit that entry from the output pak (may repeat)");
    Console.WriteLine("  --no-gdbin-register        do NOT auto-update <m>/<m>.gd.bin when adding a new *.gd.xml under <m>/");
    Console.WriteLine();
    Console.WriteLine("Reports:");
    Console.WriteLine("  --json <report.json>       write a machine-readable report to <report.json>");
    Console.WriteLine();
    Console.WriteLine("Parallelism (applies to unpack, pack, and patch):");
    Console.WriteLine("  -j, --jobs=<n>             cap worker threads at n (default: processor count, min: 1)");
    Console.WriteLine();
    Console.WriteLine("Exit codes: 0 success, 1 error, 64 usage.");
}
