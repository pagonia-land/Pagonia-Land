namespace PagoniaLand.Patcher;

public static class PathUtilities
{
    /// <summary>
    /// Normalise a mod-patch <c>target.file</c> path for joining with the
    /// patcher's <c>--game</c> directory. The first segment is always a pak
    /// name (<c>core/</c>, <c>dlc1/</c>, <c>decorations1/</c>, <c>tools/</c>);
    /// we just swap the separator to the host's.
    /// </summary>
    public static string ToGameRelativeFile(string targetFile)
        => targetFile.Replace('/', Path.DirectorySeparatorChar);
}
