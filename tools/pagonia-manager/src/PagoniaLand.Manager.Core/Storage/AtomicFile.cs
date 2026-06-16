namespace PagoniaLand.Manager;

// Writes go to "<dest>.tmp" in the same directory, then File.Move(overwrite:true).
// That rename is atomic on a same-volume local filesystem (always the case here,
// since the temp sits beside the destination). On networked/non-NTFS targets the
// rename may degrade to a non-atomic copy+delete, so callers on exotic mounts get
// "last write wins" rather than a hard atomicity guarantee.
public static class AtomicFile
{
    public const string TempSuffix = ".tmp";

    public static void WriteAllText(string path, string contents)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = path + TempSuffix;
        File.WriteAllText(tempPath, contents);
        File.Move(tempPath, path, overwrite: true);
    }

    public static void WriteAllBytes(string path, byte[] contents)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = path + TempSuffix;
        File.WriteAllBytes(tempPath, contents);
        File.Move(tempPath, path, overwrite: true);
    }

    /// <summary>
    /// Streaming variant of <see cref="WriteAllBytes"/>: opens a FileStream at
    /// <c>path.tmp</c>, hands it to <paramref name="writer"/>, then atomically
    /// renames over the destination. Use this for outputs that would exceed
    /// the .NET 2 GB single-array limit — Pak rebuilds in the live-install path hit that
    /// the moment <c>core.pak</c> grew past 2 GB.
    /// </summary>
    public static void WriteStreamed(string path, Action<Stream> writer)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = path + TempSuffix;
        using (var stream = File.Create(tempPath))
        {
            writer(stream);
        }
        File.Move(tempPath, path, overwrite: true);
    }

    /// <summary>
    /// Streaming atomic copy — like <c>File.Copy</c> but writes to <c>dest.tmp</c>
    /// first and renames over <paramref name="dest"/>. No 2 GB ceiling; safe for
    /// crashing mid-copy (the partial <c>.tmp</c> remains and is cleaned up by
    /// <see cref="CleanupLeftoverTempFiles"/>, the destination is untouched).
    /// </summary>
    public static void CopyAtomic(string source, string dest)
    {
        var directory = Path.GetDirectoryName(dest);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = dest + TempSuffix;
        try
        {
            using (var inStream = File.OpenRead(source))
            using (var outStream = File.Create(tempPath))
            {
                inStream.CopyTo(outStream);
            }
            File.Move(tempPath, dest, overwrite: true);
        }
        catch
        {
            // Delete the partial .tmp eagerly on any mid-copy failure before rethrowing (mirrors
            // PakRebuilder). CleanupLeftoverTempFiles is the backstop, but not every caller runs it.
            try { if (File.Exists(tempPath)) { File.Delete(tempPath); } } catch { /* best effort */ }
            throw;
        }
    }

    public static IEnumerable<string> EnumerateFilesIgnoringTemp(string directory, string searchPattern = "*")
    {
        if (!Directory.Exists(directory))
        {
            return [];
        }

        return Directory.EnumerateFiles(directory, searchPattern)
            .Where(file => !file.EndsWith(TempSuffix, StringComparison.Ordinal));
    }

    public static int CleanupLeftoverTempFiles(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return 0;
        }

        var removed = 0;
        foreach (var tempFile in Directory.EnumerateFiles(directory, "*" + TempSuffix, SearchOption.AllDirectories))
        {
            try
            {
                File.Delete(tempFile);
                removed++;
            }
            catch (IOException)
            {
            }
        }

        return removed;
    }
}
