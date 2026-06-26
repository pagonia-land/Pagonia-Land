using System.IO.Compression;
using System.IO.Hashing;

namespace PagoniaLand.Paker;

/// <summary>
/// Builds a fresh `.pak` from an existing one by mixing three kinds of edits:
/// <list type="bullet">
///   <item>
///     <description><b>Replace</b> — a positional file path that matches an
///     existing entry in the input pak. The new bytes go to disk; the entry
///     keeps its <see cref="PakEntry.Compressed"/> flag.</description>
///   </item>
///   <item>
///     <description><b>Add</b> — a positional file path that does NOT match an
///     existing entry. The new bytes are appended to the end of the pak with a
///     <see cref="PakEntry.Compressed"/> flag chosen by file extension
///     (text-shaped files are gzip-packed, everything else is stored
///     verbatim).</description>
///   </item>
///   <item>
///     <description><b>Delete</b> — a path passed via the <c>deletions</c>
///     list. The entry must exist in the input pak; it is omitted from the
///     output.</description>
///   </item>
/// </list>
/// Existing entries that are not deleted and not replaced are copied verbatim
/// (no re-compression), so the bulk of a patched pak stays byte-identical to
/// the input.
/// </summary>
public sealed class PakPatcher
{
    private readonly PakReader _reader = new();
    private readonly PakWriter _writer = new();

    public IReadOnlyList<PakDiagnostic> Patch(string inputPakPath, string outputPakPath, IReadOnlyList<string> replacementFiles)
        => PatchAndReport(inputPakPath, outputPakPath, replacementFiles, deletions: Array.Empty<string>(), jobs: 1, registerGdBinAdds: true).Diagnostics;

    public IReadOnlyList<PakDiagnostic> Patch(string inputPakPath, string outputPakPath, IReadOnlyList<string> replacementFiles, int jobs)
        => PatchAndReport(inputPakPath, outputPakPath, replacementFiles, deletions: Array.Empty<string>(), jobs, registerGdBinAdds: true).Diagnostics;

    public IReadOnlyList<PakDiagnostic> Patch(string inputPakPath, string outputPakPath, IReadOnlyList<string> replacementFiles, IReadOnlyList<string> deletions, int jobs)
        => PatchAndReport(inputPakPath, outputPakPath, replacementFiles, deletions, jobs, registerGdBinAdds: true).Diagnostics;

    public IReadOnlyList<PakDiagnostic> Patch(string inputPakPath, string outputPakPath, IReadOnlyList<string> replacementFiles, IReadOnlyList<string> deletions, int jobs, bool registerGdBinAdds)
        => PatchAndReport(inputPakPath, outputPakPath, replacementFiles, deletions, jobs, registerGdBinAdds).Diagnostics;

    /// <summary>
    /// Patch with replace, add, and delete operations.
    /// <paramref name="replacementFiles"/> entries that match an existing pak
    /// entry name become Replace; entries that don't match become Add.
    /// <paramref name="deletions"/> entries must match an existing pak entry
    /// name and are omitted from the output. <paramref name="jobs"/> must be
    /// at least 1.
    ///
    /// When <paramref name="registerGdBinAdds"/> is <c>true</c> (default), any
    /// added <c>*.gd.xml</c> whose path falls under a module namespace already
    /// owning a <c>&lt;m&gt;/&lt;m&gt;.gd.bin</c> in the input pak triggers an
    /// implicit replace of that index: the existing index is read, the new
    /// XML paths are appended, byte[3] of the header is recomputed, and the
    /// rebuilt index is written in place. Callers that want the bare-bones
    /// behaviour (raw entry only, no index update) pass <c>false</c>.
    ///
    /// Unlike the thin <see cref="Patch(string,string,IReadOnlyList{string})"/>
    /// overloads, this method returns the structured list of gdbin updates
    /// alongside the diagnostics so the CLI's JSON report can include them
    /// without parsing diagnostic text.
    /// </summary>
    public PakPatchResult PatchAndReport(string inputPakPath, string outputPakPath, IReadOnlyList<string> replacementFiles, IReadOnlyList<string> deletions, int jobs, bool registerGdBinAdds)
    {
        ArgumentNullException.ThrowIfNull(inputPakPath);
        ArgumentNullException.ThrowIfNull(outputPakPath);
        ArgumentNullException.ThrowIfNull(replacementFiles);
        ArgumentNullException.ThrowIfNull(deletions);
        if (jobs < 1) throw new ArgumentOutOfRangeException(nameof(jobs));

        var diagnostics = new List<PakDiagnostic>();
        PakPatchResult Failure() => new(diagnostics, Array.Empty<PakPatchGdBinUpdate>());

        if (!File.Exists(inputPakPath))
        {
            diagnostics.Add(Error(DiagnosticCodes.PatchInputMissing, $"Input pak not found: '{inputPakPath}'."));
            return Failure();
        }

        using var inputStream = File.Open(inputPakPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var read = _reader.OpenIndex(inputStream);
        diagnostics.AddRange(read.Diagnostics);
        if (!read.Success || read.Index is null) return Failure();
        var inputIndex = read.Index;
        var entryNames = new HashSet<string>(inputIndex.Entries.Select(e => e.Filename), StringComparer.Ordinal);

        // Classify positional paths into Replace (entry exists in base pak)
        // vs Add (entry does not exist in base pak). Both share the same
        // duplicate / file-on-disk validation.
        var replaceSources = new Dictionary<string, string>(StringComparer.Ordinal);
        var addSources = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var file in replacementFiles)
        {
            if (string.IsNullOrWhiteSpace(file))
            {
                diagnostics.Add(Error(DiagnosticCodes.PatchSourceMissing, "An empty replacement path was provided."));
                continue;
            }

            var entryName = file.Replace('\\', '/');
            if (replaceSources.ContainsKey(entryName) || addSources.ContainsKey(entryName))
            {
                diagnostics.Add(Error(DiagnosticCodes.PatchDuplicateSource, $"Replacement '{entryName}' was provided more than once."));
                continue;
            }
            if (!File.Exists(file))
            {
                diagnostics.Add(Error(DiagnosticCodes.PatchSourceMissing, $"Replacement file '{file}' does not exist on disk."));
                continue;
            }

            if (entryNames.Contains(entryName))
            {
                replaceSources[entryName] = file;
            }
            else
            {
                addSources[entryName] = file;
            }
        }

        // Validate deletions reference existing entries; also surface a
        // conflict if the same path appears in both deletions and any
        // positional list.
        var deleteSet = new HashSet<string>(StringComparer.Ordinal);
        foreach (var path in deletions)
        {
            if (string.IsNullOrWhiteSpace(path)) continue;
            var normalised = path.Replace('\\', '/');
            if (!deleteSet.Add(normalised)) continue;
            if (!entryNames.Contains(normalised))
            {
                diagnostics.Add(Error(DiagnosticCodes.PatchDeleteTargetMissing, $"Pak '{inputPakPath}' has no entry named '{normalised}' to delete."));
                continue;
            }
            if (replaceSources.ContainsKey(normalised) || addSources.ContainsKey(normalised))
            {
                diagnostics.Add(Error(DiagnosticCodes.PatchAddConflictsWithExisting, $"Pak entry '{normalised}' is both being deleted and replaced/added; pick one."));
            }
        }

        if (diagnostics.Any(d => d.Severity == PakDiagnosticSeverity.Error)) return Failure();

        // Auto-register any newly-added *.gd.xml in the module's existing
        // <m>/<m>.gd.bin so the engine actually loads it. The output is a
        // dict of in-pak entry name -> (rebuilt index bytes, paths added),
        // consumed below as an implicit replace.
        var gdbinUpdates = new Dictionary<string, GdBinAutoUpdate>(StringComparer.Ordinal);
        if (registerGdBinAdds)
        {
            CollectGdBinUpdates(
                inputStream, inputIndex, addSources, replaceSources, deleteSet,
                gdbinUpdates, diagnostics);
            if (diagnostics.Any(d => d.Severity == PakDiagnosticSeverity.Error)) return Failure();
        }

        // Output is opened only after every validation passes so we never
        // leave a half-written pak on disk.
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPakPath))!);
        using var outputStream = new FileStream(
            outputPakPath, FileMode.Create, FileAccess.Write, FileShare.None,
            bufferSize: 1 << 20, FileOptions.SequentialScan);

        // Layout: existing entries (minus deletes), each replaced or copied
        // verbatim, then any new adds appended at the end. The index records
        // them in that order.
        var keptEntryCount = inputIndex.Entries.Count(e => !deleteSet.Contains(e.Filename));
        var totalEntryCount = keptEntryCount + addSources.Count;
        var encoded = new (byte[] Bytes, long Uncompressed, bool Compressed, string Filename)[totalEntryCount];

        // Build the operation list paired with output index.
        var existingOps = new List<(int OutputIndex, PakEntry Entry, string? ReplaceSource)>(keptEntryCount);
        var outIdx = 0;
        foreach (var entry in inputIndex.Entries)
        {
            if (deleteSet.Contains(entry.Filename)) continue;
            replaceSources.TryGetValue(entry.Filename, out var src);
            existingOps.Add((outIdx, entry, src));
            outIdx++;
        }

        // Ordered list of adds so the resulting pak is deterministic.
        var addOps = addSources
            .OrderBy(kv => kv.Key, StringComparer.Ordinal)
            .Select((kv, i) => (OutputIndex: keptEntryCount + i, EntryName: kv.Key, Source: kv.Value))
            .ToList();

        Exception? encodeError = null;
        string? encodeErrorPath = null;
        var replacedCount = 0;
        var addedCount = 0;

        var parallelOptions = new System.Threading.Tasks.ParallelOptions { MaxDegreeOfParallelism = jobs };

        // Existing entries (replace or verbatim copy).
        System.Threading.Tasks.Parallel.ForEach<
            (int OutputIndex, PakEntry Entry, string? ReplaceSource),
            FileStream?>(
            existingOps,
            parallelOptions,
            localInit: () => null,
            body: (item, state, _, localPakStream) =>
            {
                if (state.IsExceptional || state.ShouldExitCurrentIteration) return localPakStream;
                try
                {
                    if (item.ReplaceSource is not null)
                    {
                        var (bytes, uncompressed) = EncodeForEntry(item.ReplaceSource, item.Entry.Compressed);
                        encoded[item.OutputIndex] = (bytes, uncompressed, item.Entry.Compressed, item.Entry.Filename);
                        System.Threading.Interlocked.Increment(ref replacedCount);
                    }
                    else if (gdbinUpdates.TryGetValue(item.Entry.Filename, out var gdbinUpdate))
                    {
                        // Implicit replace driven by auto-registration. The new
                        // bytes come from memory; respect the original entry's
                        // compression flag so we don't accidentally store the
                        // updated index uncompressed in a pak that was
                        // gzip-packing it.
                        var (bytes, uncompressed) = EncodeBytesForEntry(gdbinUpdate.RebuiltBytes, item.Entry.Compressed);
                        encoded[item.OutputIndex] = (bytes, uncompressed, item.Entry.Compressed, item.Entry.Filename);
                    }
                    else
                    {
                        localPakStream ??= File.Open(inputPakPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                        var (bytes, uncompressed) = ReadVerbatim(localPakStream, item.Entry);
                        encoded[item.OutputIndex] = (bytes, uncompressed, item.Entry.Compressed, item.Entry.Filename);
                    }
                }
                catch (Exception ex) when (ex is IOException or InvalidDataException or EndOfStreamException)
                {
                    encodeError = ex;
                    encodeErrorPath = item.ReplaceSource ?? inputPakPath;
                    state.Stop();
                }
                return localPakStream;
            },
            localFinally: localPakStream => localPakStream?.Dispose());

        if (encodeError is not null)
        {
            diagnostics.Add(Error(DiagnosticCodes.PackSourceUnreadable, $"Failed to read '{encodeErrorPath}' during patch: {encodeError.Message}"));
            return Failure();
        }

        // New adds. Each is encoded independently of the existing ops above.
        System.Threading.Tasks.Parallel.ForEach(
            addOps,
            parallelOptions,
            (item, state) =>
            {
                if (state.IsExceptional || state.ShouldExitCurrentIteration) return;
                try
                {
                    var compressed = ShouldCompressByExtension(item.EntryName);
                    var (bytes, uncompressed) = EncodeForEntry(item.Source, compressed);
                    encoded[item.OutputIndex] = (bytes, uncompressed, compressed, item.EntryName);
                    System.Threading.Interlocked.Increment(ref addedCount);
                }
                catch (Exception ex) when (ex is IOException or InvalidDataException)
                {
                    encodeError = ex;
                    encodeErrorPath = item.Source;
                    state.Stop();
                }
            });

        if (encodeError is not null)
        {
            diagnostics.Add(Error(DiagnosticCodes.PackSourceUnreadable, $"Failed to read '{encodeErrorPath}' during patch: {encodeError.Message}"));
            return Failure();
        }

        var newEntries = new List<PakEntry>(totalEntryCount);
        var crc = new Crc32();

        for (var i = 0; i < totalEntryCount; i++)
        {
            var (bytes, uncompressedSize, compressed, filename) = encoded[i];
            var beginOffset = outputStream.Position;
            crc.Append(bytes);
            outputStream.Write(bytes);
            newEntries.Add(new PakEntry(compressed, filename, beginOffset, uncompressedSize));
            encoded[i] = default;
        }

        diagnostics.AddRange(_writer.WriteIndex(outputStream, newEntries, inputIndex.Version, crc));

        if (addedCount > 0)
        {
            diagnostics.Add(new PakDiagnostic(
                PakDiagnosticSeverity.Info,
                DiagnosticCodes.PakEntryAdded,
                $"Added {addedCount} new entr{(addedCount == 1 ? "y" : "ies")} to '{outputPakPath}'."));
        }
        foreach (var kv in gdbinUpdates)
        {
            var update = kv.Value;
            diagnostics.Add(new PakDiagnostic(
                PakDiagnosticSeverity.Info,
                DiagnosticCodes.PakPatchGdBinUpdated,
                $"Registered {update.AddedXmls.Count} new XML path{(update.AddedXmls.Count == 1 ? string.Empty : "s")} in '{kv.Key}': {string.Join(", ", update.AddedXmls)}.",
                Path: kv.Key));
        }
        if (deleteSet.Count > 0)
        {
            diagnostics.Add(new PakDiagnostic(
                PakDiagnosticSeverity.Info,
                DiagnosticCodes.PakEntryDeleted,
                $"Omitted {deleteSet.Count} entr{(deleteSet.Count == 1 ? "y" : "ies")} from '{outputPakPath}'."));
        }

        diagnostics.Add(new PakDiagnostic(
            PakDiagnosticSeverity.Info,
            DiagnosticCodes.PakPatchWritten,
            $"Patched {replacedCount} of {inputIndex.Entries.Count} entries ({addedCount} added, {deleteSet.Count} deleted); wrote '{outputPakPath}'."));

        var gdbinUpdateReports = gdbinUpdates
            .OrderBy(kv => kv.Key, StringComparer.Ordinal)
            .Select(kv => new PakPatchGdBinUpdate(kv.Key, kv.Value.AddedXmls))
            .ToList();

        return new PakPatchResult(diagnostics, gdbinUpdateReports);
    }

    private static (byte[] Bytes, long Uncompressed) EncodeForEntry(string sourcePath, bool compressed)
    {
        using var sourceStream = File.OpenRead(sourcePath);
        var uncompressed = sourceStream.Length;
        var initialCapacity = compressed
            ? Math.Max(4096, (int)Math.Min(uncompressed / 8, int.MaxValue))
            : (int)Math.Min(uncompressed, int.MaxValue);
        using var memory = new MemoryStream(initialCapacity);
        if (compressed)
        {
            using (var gzip = new GZipStream(memory, CompressionLevel.Optimal, leaveOpen: true))
            {
                sourceStream.CopyTo(gzip);
            }
        }
        else
        {
            sourceStream.CopyTo(memory);
        }
        return (memory.ToArray(), uncompressed);
    }

    private static (byte[] Bytes, long Uncompressed) EncodeBytesForEntry(byte[] source, bool compressed)
    {
        if (!compressed) return (source, source.Length);
        using var memory = new MemoryStream(Math.Max(4096, source.Length / 8));
        using (var gzip = new GZipStream(memory, CompressionLevel.Optimal, leaveOpen: true))
        {
            gzip.Write(source);
        }
        return (memory.ToArray(), source.Length);
    }

    /// <summary>
    /// Carries the rebuilt <c>.gd.bin</c> bytes for one module's index along
    /// with the list of XML paths that were newly registered. The diagnostic
    /// reads the path list back out to mention every addition by name.
    /// </summary>
    private sealed record GdBinAutoUpdate(byte[] RebuiltBytes, IReadOnlyList<string> AddedXmls);

    private void CollectGdBinUpdates(
        FileStream inputStream,
        PakIndex inputIndex,
        Dictionary<string, string> addSources,
        Dictionary<string, string> replaceSources,
        HashSet<string> deleteSet,
        Dictionary<string, GdBinAutoUpdate> gdbinUpdates,
        List<PakDiagnostic> diagnostics)
    {
        // Group added paths by the top-level module folder. Only *.gd.xml are
        // tracked — that's what the engine reads from the GameDatabase index.
        var addsByModule = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var entryName in addSources.Keys)
        {
            if (!entryName.EndsWith(".gd.xml", StringComparison.OrdinalIgnoreCase)) continue;
            var firstSlash = entryName.IndexOf('/');
            if (firstSlash <= 0) continue;
            var module = entryName[..firstSlash];
            if (!addsByModule.TryGetValue(module, out var list))
            {
                list = new List<string>();
                addsByModule[module] = list;
            }
            list.Add(entryName);
        }

        if (addsByModule.Count == 0) return;

        var entriesByName = inputIndex.Entries.ToDictionary(e => e.Filename, StringComparer.Ordinal);
        var gdBinReader = new GdBinReader();
        var gdBinWriter = new GdBinWriter();

        foreach (var (module, addedXmls) in addsByModule)
        {
            var gdbinEntryName = $"{module}/{module}.gd.bin";
            if (!entriesByName.TryGetValue(gdbinEntryName, out var gdbinEntry))
            {
                // Module has no .gd.bin in the base pak — nothing to update.
                // The modder still gets the raw XML entry; if they meant the
                // engine to see it, they need to ship a .gd.bin themselves.
                continue;
            }
            if (replaceSources.ContainsKey(gdbinEntryName))
            {
                // User is overriding the index explicitly. Trust their version.
                continue;
            }
            if (deleteSet.Contains(gdbinEntryName))
            {
                // User wants the index gone. Don't conjure one back.
                continue;
            }

            byte[] existingBytes;
            try
            {
                using var extracted = new MemoryStream();
                _reader.ExtractEntry(inputStream, gdbinEntry, extracted);
                existingBytes = extracted.ToArray();
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or EndOfStreamException)
            {
                diagnostics.Add(Error(
                    DiagnosticCodes.PatchGdBinUpdateFailed,
                    $"Failed to read existing index '{gdbinEntryName}' from input pak: {ex.Message}"));
                continue;
            }

            var readResult = gdBinReader.Read(new MemoryStream(existingBytes, writable: false));
            if (!readResult.Success || readResult.Index is null)
            {
                diagnostics.AddRange(readResult.Diagnostics
                    .Where(d => d.Severity == PakDiagnosticSeverity.Error)
                    .Select(d => d with { Path = gdbinEntryName }));
                diagnostics.Add(Error(
                    DiagnosticCodes.PatchGdBinUpdateFailed,
                    $"Existing index '{gdbinEntryName}' failed to decode; cannot auto-register added XML paths."));
                continue;
            }

            // Sort the new paths so a given input set always produces the same
            // index bytes. Existing entries stay in their original order.
            addedXmls.Sort(StringComparer.Ordinal);

            var updated = readResult.Index;
            foreach (var xml in addedXmls)
            {
                updated = updated.WithEntryAdded(xml);
            }
            updated = updated.WithComputedHeader();

            using var rebuilt = new MemoryStream();
            gdBinWriter.Write(rebuilt, updated);

            gdbinUpdates[gdbinEntryName] = new GdBinAutoUpdate(rebuilt.ToArray(), addedXmls);
        }
    }

    private static (byte[] Bytes, long Uncompressed) ReadVerbatim(FileStream pakStream, PakEntry entry)
    {
        // Validate before the cast: a corrupt index can make SizeInPak negative or exceed
        // int range, where checked((int)...) would throw an OverflowException the surrounding
        // catch filters don't expect. InvalidDataException is caught and reported cleanly.
        if (entry.SizeInPak < 0 || entry.SizeInPak > int.MaxValue)
        {
            throw new InvalidDataException(
                $"Entry '{entry.Filename}' has an invalid in-pak size ({entry.SizeInPak}); the pak index is corrupt.");
        }

        pakStream.Seek(entry.BeginOffset, SeekOrigin.Begin);
        var size = (int)entry.SizeInPak;
        var bytes = new byte[size];
        pakStream.ReadExactly(bytes);
        return (bytes, entry.Size);
    }

    /// <summary>
    /// The text-shaped file extensions newly-added entries are gzip-compressed for — the
    /// game-database / sandbox / config files plus the per-module JSON metadata that shipped paks
    /// gzip too; everything else is stored verbatim. This is the single source of truth for the
    /// compression-by-extension heuristic; <c>scripts/sandbox-pack.ps1</c> hardcodes the same set,
    /// and a paker test asserts the two stay identical so a pak built either way has one layout.
    /// </summary>
    public static readonly IReadOnlySet<string> CompressibleExtensions =
        new HashSet<string>(StringComparer.Ordinal) { ".xml", ".yaml", ".yml", ".txt", ".json" };

    private static bool ShouldCompressByExtension(string entryName)
    {
        var ext = Path.GetExtension(entryName.AsSpan()).ToString().ToLowerInvariant();
        return CompressibleExtensions.Contains(ext);
    }

    private static PakDiagnostic Error(string code, string message)
        => new(PakDiagnosticSeverity.Error, code, message);
}
