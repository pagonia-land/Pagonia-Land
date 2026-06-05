using System.IO.Compression;
using System.IO.Hashing;
using System.Text.Json;

namespace PagoniaLand.Paker;

/// <summary>
/// Writes a fresh `.pak` archive from a <c>pakinfo.json</c> description plus
/// the source files it references. Source paths are resolved relative to the
/// directory that contains the pakinfo file, matching how plpaker behaves.
///
/// On read we don't trust the on-disk <c>size_compressed</c> field — gzip
/// re-compression is rarely byte-identical across library versions, so we
/// recompute begin offsets and compressed sizes from scratch and the result is
/// always round-trippable via <see cref="PakReader"/>.
/// </summary>
public sealed class PakPacker
{
    private readonly PakWriter _writer = new();

    /// <summary>
    /// Pack the archive described by <paramref name="pakInfoPath"/> into
    /// <paramref name="outputPakPath"/>. Returns diagnostics for every problem
    /// encountered; the output file is only written when no errors are reported.
    /// </summary>
    public IReadOnlyList<PakDiagnostic> Pack(string pakInfoPath, string outputPakPath)
        => Pack(pakInfoPath, outputPakPath, PakFilter.All, jobs: 1);

    /// <summary>
    /// Pack the entries from <paramref name="pakInfoPath"/> that match
    /// <paramref name="filter"/> into <paramref name="outputPakPath"/>. Entries
    /// outside the filter are skipped; the on-disk layout is rewritten so begin
    /// offsets remain monotonic.
    /// </summary>
    public IReadOnlyList<PakDiagnostic> Pack(string pakInfoPath, string outputPakPath, PakFilter filter)
        => Pack(pakInfoPath, outputPakPath, filter, jobs: 1);

    /// <summary>
    /// Pack with parallel encoding. Up to <paramref name="jobs"/> entries are
    /// compressed concurrently in memory and then written to the output stream
    /// sequentially so begin offsets stay monotonic. <paramref name="jobs"/>
    /// must be at least 1.
    /// </summary>
    public IReadOnlyList<PakDiagnostic> Pack(string pakInfoPath, string outputPakPath, PakFilter filter, int jobs)
        => Pack(pakInfoPath, outputPakPath, filter, jobs, CancellationToken.None);

    /// <summary>
    /// Cancellable overload of <see cref="Pack(string,string,PakFilter,int)"/>. The
    /// token is checked between entries in the source-validation loop, fed into the
    /// parallel encode via <see cref="System.Threading.Tasks.ParallelOptions"/>, and
    /// checked between entries in the sequential write loop — so a caller (a GUI
    /// Cancel button driving a deploy off a background thread) can interrupt a large
    /// pack mid-operation. A cancellation throws before
    /// <paramref name="outputPakPath"/> is finalised; the half-written file is left
    /// for the caller's staging cleanup.
    /// </summary>
    public IReadOnlyList<PakDiagnostic> Pack(string pakInfoPath, string outputPakPath, PakFilter filter, int jobs, CancellationToken cancellationToken)
    {
        if (jobs < 1) throw new ArgumentOutOfRangeException(nameof(jobs));
        ArgumentNullException.ThrowIfNull(pakInfoPath);
        ArgumentNullException.ThrowIfNull(outputPakPath);
        ArgumentNullException.ThrowIfNull(filter);

        cancellationToken.ThrowIfCancellationRequested();

        var diagnostics = new List<PakDiagnostic>();

        PakInfo? pakInfo;
        try
        {
            using var jsonStream = File.OpenRead(pakInfoPath);
            pakInfo = JsonSerializer.Deserialize(jsonStream, PakInfoJsonContext.Default.PakInfo);
        }
        catch (JsonException ex)
        {
            diagnostics.Add(Error(DiagnosticCodes.PakInfoJsonInvalid, $"'{pakInfoPath}' is not valid pakinfo JSON: {ex.Message}"));
            return diagnostics;
        }
        catch (IOException ex)
        {
            diagnostics.Add(Error(DiagnosticCodes.PakInfoJsonInvalid, $"Could not read '{pakInfoPath}': {ex.Message}"));
            return diagnostics;
        }

        if (pakInfo is null || pakInfo.Entries is null)
        {
            diagnostics.Add(Error(DiagnosticCodes.PakInfoEmpty, $"'{pakInfoPath}' parsed to an empty document."));
            return diagnostics;
        }

        var sourceDir = Path.GetDirectoryName(Path.GetFullPath(pakInfoPath))
                        ?? Directory.GetCurrentDirectory();

        // Apply the filter against the original indices (so -s/-e refer to positions in
        // pakinfo.json, not positions after filtering) and remember which survived.
        var selectedIndices = new List<int>(pakInfo.Entries.Count);
        for (var i = 0; i < pakInfo.Entries.Count; i++)
        {
            if (filter.Matches(i, pakInfo.Entries[i])) selectedIndices.Add(i);
        }

        // Validate every selected source file up front so we don't write a partial archive.
        var resolvedSources = new string[selectedIndices.Count];
        for (var slot = 0; slot < selectedIndices.Count; slot++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var originalIndex = selectedIndices[slot];
            var entry = pakInfo.Entries[originalIndex];
            if (string.IsNullOrWhiteSpace(entry.Filename))
            {
                diagnostics.Add(Error(DiagnosticCodes.PackEntryFilenameEmpty, $"Entry at index {originalIndex} has an empty filename."));
                continue;
            }

            var relative = entry.Filename.Replace('/', Path.DirectorySeparatorChar);
            var source = Path.Combine(sourceDir, relative);
            if (!File.Exists(source))
            {
                diagnostics.Add(Error(DiagnosticCodes.PackSourceMissing, $"Source file for entry '{entry.Filename}' not found at '{source}'."));
                continue;
            }

            resolvedSources[slot] = source;
        }

        if (diagnostics.Any(d => d.Severity == PakDiagnosticSeverity.Error))
        {
            return diagnostics;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPakPath))!);
        // A larger FileStream write buffer (1 MB) cuts syscall count for big paks.
        using var outStream = new FileStream(
            outputPakPath, FileMode.Create, FileAccess.Write, FileShare.None,
            bufferSize: 1 << 20, FileOptions.SequentialScan);

        var pakEntries = new List<PakEntry>(selectedIndices.Count);
        var crc = new Crc32();

        // Encode every entry into a pooled byte[] in parallel (up to `jobs`-wide),
        // then write the buffers to disk in entry order so begin offsets stay
        // monotonic. With jobs=1 the loop degenerates back to a fully sequential
        // encode/write — the result is byte-identical to the pre-parallel path.
        var encoded = new (byte[] Bytes, long Uncompressed)[selectedIndices.Count];
        Exception? encodeError = null;
        string? encodeErrorPath = null;

        var parallelOptions = new System.Threading.Tasks.ParallelOptions
        {
            MaxDegreeOfParallelism = jobs,
            CancellationToken = cancellationToken,
        };
        System.Threading.Tasks.Parallel.For(0, selectedIndices.Count, parallelOptions, (slot, state) =>
        {
            if (state.IsExceptional || state.ShouldExitCurrentIteration) return;
            var entry = pakInfo.Entries[selectedIndices[slot]];
            var sourcePath = resolvedSources[slot];
            try
            {
                encoded[slot] = EncodeEntry(sourcePath, entry.Compressed);
            }
            catch (IOException ex)
            {
                encodeError = ex;
                encodeErrorPath = sourcePath;
                state.Stop();
            }
        });

        if (encodeError is not null)
        {
            diagnostics.Add(Error(DiagnosticCodes.PackSourceUnreadable, $"Failed to read '{encodeErrorPath}': {encodeError.Message}"));
            return diagnostics;
        }

        for (var slot = 0; slot < selectedIndices.Count; slot++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var entry = pakInfo.Entries[selectedIndices[slot]];
            var (bytes, uncompressed) = encoded[slot];
            var beginOffset = outStream.Position;
            crc.Append(bytes);
            outStream.Write(bytes);
            pakEntries.Add(new PakEntry(entry.Compressed, entry.Filename, beginOffset, uncompressed));
            encoded[slot] = default; // release the buffer to the GC as soon as we've drained it
        }

        diagnostics.AddRange(_writer.WriteIndex(outStream, pakEntries, (uint)pakInfo.Version, crc));

        diagnostics.Add(new PakDiagnostic(
            PakDiagnosticSeverity.Info,
            DiagnosticCodes.PakPackWritten,
            filter.IsUnrestricted
                ? $"Packed {pakEntries.Count} entries into '{outputPakPath}'."
                : $"Packed {pakEntries.Count} of {pakInfo.Entries.Count} entries into '{outputPakPath}' (filtered)."));

        return diagnostics;
    }

    private static (byte[] Bytes, long Uncompressed) EncodeEntry(string sourcePath, bool compressed)
    {
        using var sourceStream = File.OpenRead(sourcePath);
        var uncompressed = sourceStream.Length;

        // Pre-size the MemoryStream to limit reallocations. For uncompressed entries
        // we know the exact size; for compressed we start at 1/8th of the source as
        // a rough estimate for gzip-friendly content and let it grow as needed.
        var initialCapacity = compressed ? Math.Max(4096, (int)Math.Min(uncompressed / 8, int.MaxValue)) : (int)Math.Min(uncompressed, int.MaxValue);
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

    private static PakDiagnostic Error(string code, string message)
        => new(PakDiagnosticSeverity.Error, code, message);
}
