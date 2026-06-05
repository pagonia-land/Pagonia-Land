using System.IO.Compression;
using System.IO.Hashing;
using System.Security.Cryptography;
using PagoniaLand.Paker;

namespace PagoniaLand.Manager;

/// <summary>
/// Outcome of <see cref="PakRebuilder.Rebuild"/>. On success the new pak has
/// been written to disk and both byte counts + SHA-256s are populated; on
/// failure the output path is wiped and <see cref="Diagnostics"/> explains
/// what went wrong.
/// </summary>
public sealed record PakRebuildResult(
    bool Success,
    long OriginalByteSize,
    long NewByteSize,
    string OriginalSha256,
    string NewSha256,
    int EntriesTotal,
    int EntriesReplaced,
    IReadOnlyList<PakDiagnostic> Diagnostics);

/// <summary>
/// Builds a new <c>.pak</c> that mirrors an existing one entry-for-entry except
/// for a caller-supplied set of replacement files. Original entry order, sizes
/// (where unchanged), and compression flags are preserved; replaced entries get
/// their bytes from disk and are re-compressed to match the original entry's
/// <c>Compressed</c> flag.
/// <para>Implementation streams both input and output — real Pioneers of Pagonia
/// paks can exceed 2 GB, which is the .NET single-array allocation ceiling, so
/// any approach that loads the whole pak into memory (or builds the rebuilt
/// pak in a <see cref="MemoryStream"/>) hits <c>IOException: The file is too
/// long</c> the moment <c>core.pak</c> crosses that threshold.</para>
/// <para>Used by <see cref="DeployService"/> during live-install deploy: after
/// the patcher writes modified XMLs into a staging tree, every pak that owns
/// at least one touched file is rebuilt against the original + staging bytes
/// and atomically written into <c>&lt;gameRoot&gt;/pak/</c>. The original is
/// backed up first (handled by the caller) so rollback can restore byte-for-byte.</para>
/// </summary>
public sealed class PakRebuilder
{
    // Tuned for sequential I/O of multi-GB paks; the OS page cache will keep
    // hot ranges anyway, so the per-call buffer is just to avoid trip-syscalls
    // on every byte. Same number PakReader uses for its CRC streaming pass.
    private const int CopyBufferSize = 81920;

    /// <summary>
    /// Read <paramref name="originalPakPath"/>'s index, write a new pak at
    /// <paramref name="outputPakPath"/> where each entry's data is either
    /// streamed verbatim from the original (preserving the original gzip bytes
    /// — no re-compression of unchanged entries) or replaced from
    /// <paramref name="replacements"/> (re-compressed to match the original
    /// entry's <c>Compressed</c> flag if true). Both the original read and the
    /// new write use FileStreams + an 80 KB shuttle buffer; nothing ever sits
    /// in memory at full pak size. The output goes through
    /// <see cref="AtomicFile.WriteStreamed"/> so a crash leaves the live pak
    /// either old or new, never half-written.
    /// </summary>
    /// <param name="replacements">Map from in-pak entry name
    /// (e.g. <c>"core/gdb/buildings.gd.xml"</c>) to the absolute path on disk of
    /// the replacement bytes. Keys not present in the original pak are silently
    /// ignored — they belong to a different pak.</param>
    public PakRebuildResult Rebuild(
        string originalPakPath,
        string outputPakPath,
        IReadOnlyDictionary<string, string> replacements)
    {
        return RebuildInternal(originalPakPath, outputPakPath,
            getReplacementBytes: name => replacements.TryGetValue(name, out var path) ? File.ReadAllBytes(path) : null);
    }

    /// <summary>overload — replacement bytes already in memory,
    /// no disk read needed. Used by <see cref="DeployService"/>'s sparse-apply
    /// fast path where <see cref="PatchApplier.ApplySparse"/> produced the
    /// patched bytes directly without intermediate staging.</summary>
    /// <param name="replacementBytes">Map from in-pak entry name to the
    /// replacement bytes already loaded in memory. Same key semantics as the
    /// path-based overload — unknown entries are silently skipped (they
    /// belong to a different pak).</param>
    public PakRebuildResult Rebuild(
        string originalPakPath,
        string outputPakPath,
        IReadOnlyDictionary<string, byte[]> replacementBytes)
    {
        return RebuildInternal(originalPakPath, outputPakPath,
            getReplacementBytes: name => replacementBytes.TryGetValue(name, out var b) ? b : null);
    }

    private PakRebuildResult RebuildInternal(
        string originalPakPath,
        string outputPakPath,
        Func<string, byte[]?> getReplacementBytes)
    {
        var diagnostics = new List<PakDiagnostic>();
        var originalSize = new FileInfo(originalPakPath).Length;
        var originalSha = ComputeFileSha256(originalPakPath);

        var reader = new PakReader();
        var writer = new PakWriter();
        var crc = new Crc32();
        var newEntries = new List<PakEntry>();
        var replaced = 0;
        var entriesTotal = 0;

        try
        {
            // First open: read the index only, then close the handle. Live-deploy
            // calls Rebuild with originalPakPath == outputPakPath, and on Windows
            // AtomicFile.WriteStreamed's final File.Move(tmp, dest, overwrite:true)
            // fails with a sharing violation if a read handle is still open on
            // dest. So we keep this scope tight and re-open inside the write
            // callback below, where its using-scope guarantees it's closed
            // before the move runs.
            PakIndex index;
            {
                using var indexProbeStream = File.OpenRead(originalPakPath);
                var openResult = reader.OpenIndex(indexProbeStream);
                if (!openResult.Success || openResult.Index is null)
                {
                    diagnostics.AddRange(openResult.Diagnostics);
                    diagnostics.Add(new PakDiagnostic(
                        PakDiagnosticSeverity.Error,
                        "manager.pakRebuildFailed",
                        $"Could not open original pak '{Path.GetFileName(originalPakPath)}' for rebuild."));
                    return new PakRebuildResult(
                        Success: false,
                        OriginalByteSize: originalSize,
                        NewByteSize: 0,
                        OriginalSha256: originalSha,
                        NewSha256: string.Empty,
                        EntriesTotal: 0,
                        EntriesReplaced: 0,
                        Diagnostics: diagnostics);
                }
                index = openResult.Index;
            }

            entriesTotal = index.Entries.Count;

            AtomicFile.WriteStreamed(outputPakPath, outStream =>
            {
                // Inner using-scope: this stream is disposed at the end of the
                // callback, before AtomicFile's File.Move runs. Same-path rebuilds
                // (live deploy) therefore release the original handle before the
                // overwrite, avoiding Windows sharing-violation failures.
                using var originalStream = File.OpenRead(originalPakPath);
                var copyBuffer = new byte[CopyBufferSize];
                newEntries.Capacity = index.Entries.Count;

                foreach (var entry in index.Entries)
                {
                    var entryBeginOffset = outStream.Position;
                    long uncompressedSize;
                    long sizeInPak;

                    var newBytes = getReplacementBytes(entry.Filename);
                    if (newBytes is not null)
                    {
                        uncompressedSize = newBytes.LongLength;

                        if (entry.Compressed)
                        {
                            // Compress to an in-memory buffer first so we know the
                            // on-disk byte count before writing the gzip bytes through
                            // to the output stream. Patched XMLs are kilobytes, so the
                            // per-entry buffer cost is irrelevant.
                            using var compressedBuf = new MemoryStream();
                            using (var gzip = new GZipStream(compressedBuf, CompressionLevel.Optimal, leaveOpen: true))
                            {
                                gzip.Write(newBytes, 0, newBytes.Length);
                            }
                            var compressed = compressedBuf.GetBuffer();
                            var compressedLength = (int)compressedBuf.Length;
                            outStream.Write(compressed, 0, compressedLength);
                            crc.Append(compressed.AsSpan(0, compressedLength));
                            sizeInPak = compressedLength;
                        }
                        else
                        {
                            outStream.Write(newBytes, 0, newBytes.Length);
                            crc.Append(newBytes);
                            sizeInPak = newBytes.LongLength;
                        }
                        replaced++;
                    }
                    else
                    {
                        // Raw-copy preserves the original gzip-or-not bytes exactly —
                        // no re-compression, no decode/encode roundtrip. Stream in
                        // CopyBufferSize chunks; the entry can be hundreds of MB.
                        originalStream.Seek(entry.BeginOffset, SeekOrigin.Begin);
                        var remaining = entry.SizeInPak;
                        while (remaining > 0)
                        {
                            var toRead = (int)Math.Min(copyBuffer.Length, remaining);
                            var n = originalStream.Read(copyBuffer, 0, toRead);
                            if (n == 0)
                            {
                                throw new EndOfStreamException(
                                    $"original pak ended {remaining} bytes early while copying entry '{entry.Filename}'");
                            }
                            outStream.Write(copyBuffer, 0, n);
                            crc.Append(copyBuffer.AsSpan(0, n));
                            remaining -= n;
                        }
                        sizeInPak = entry.SizeInPak;
                        uncompressedSize = entry.Size;
                    }

                    newEntries.Add(new PakEntry(
                        Compressed: entry.Compressed,
                        Filename: entry.Filename,
                        BeginOffset: entryBeginOffset,
                        Size: uncompressedSize)
                    {
                        SizeInPak = sizeInPak,
                    });
                }

                // WriteIndex appends index bytes to the rolling CRC + writes them to
                // outStream + writes the 12-byte footer. Total stream cost: data +
                // index + footer, all measured via outStream.Position below.
                writer.WriteIndex(outStream, newEntries, index.Version, rollingCrc: crc);
            });

            // SHA the just-written file with the same streaming approach used
            // for the original. The .tmp -> rename happened above, so reads of
            // outputPakPath see the committed file.
            var newSize = new FileInfo(outputPakPath).Length;
            var newSha = ComputeFileSha256(outputPakPath);

            return new PakRebuildResult(
                Success: true,
                OriginalByteSize: originalSize,
                NewByteSize: newSize,
                OriginalSha256: originalSha,
                NewSha256: newSha,
                EntriesTotal: entriesTotal,
                EntriesReplaced: replaced,
                Diagnostics: diagnostics);
        }
        catch (Exception ex)
        {
            // AtomicFile leaves the destination untouched on a mid-write crash
            // (the .tmp is the one that got partial bytes; CleanupLeftoverTempFiles
            // handles it later). Defensively delete any leftover .tmp here too so
            // re-runs of the same deploy don't accumulate junk.
            var tempPath = outputPakPath + AtomicFile.TempSuffix;
            if (File.Exists(tempPath))
            {
                try { File.Delete(tempPath); } catch { /* ignore */ }
            }
            diagnostics.Add(new PakDiagnostic(
                PakDiagnosticSeverity.Error,
                "manager.pakRebuildFailed",
                $"Rebuilding '{Path.GetFileName(originalPakPath)}' failed: {ex.Message}"));
            return new PakRebuildResult(
                Success: false,
                OriginalByteSize: originalSize,
                NewByteSize: 0,
                OriginalSha256: originalSha,
                NewSha256: string.Empty,
                EntriesTotal: entriesTotal,
                EntriesReplaced: replaced,
                Diagnostics: diagnostics);
        }
    }

    /// <summary>Stream a file through SHA-256 without loading it whole — required
    /// for pak files larger than the 2 GB single-array allocation limit.</summary>
    private static string ComputeFileSha256(string path)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        using var stream = File.OpenRead(path);
        var buffer = new byte[CopyBufferSize];
        int n;
        while ((n = stream.Read(buffer, 0, buffer.Length)) > 0)
        {
            hash.AppendData(buffer, 0, n);
        }
        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }
}
