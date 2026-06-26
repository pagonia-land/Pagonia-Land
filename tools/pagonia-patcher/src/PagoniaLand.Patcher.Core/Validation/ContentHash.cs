using System.Security.Cryptography;
using System.Text;
using YamlDotNet.RepresentationModel;

namespace PagoniaLand.Patcher;

/// <summary>
/// The one canonicalisation for hashing a mod's content, shared by every party so author
/// (<c>index build</c>) and consumer (install verify) agree byte-for-byte. The bytes fed to SHA-256
/// are, for each file in a stable order: its forward-slashed relative path, a NUL separator, the file
/// bytes, a NUL separator. Result is lowercase hex.
///
/// <para>
/// Two scopes:
/// <list type="bullet">
///   <item><see cref="OfDirectory"/> hashes <b>every</b> file under a directory — used for the
///   collection lockfile's <c>archiveSha256</c> (a local resolve sees the whole mod folder).</item>
///   <item><see cref="OfModPayload"/> hashes only the <b>logical payload</b> a mod ships over the
///   wire — <c>mod.yaml</c> plus the patch files it references — so the value is independent of any
///   extra files a repo folder happens to carry (README, screenshots) that a <c>gh:</c> raw fetch
///   never transfers. This is the set the index mirror's <c>contentHash</c> advertises and the
///   consumer re-computes on the fetched tree.</item>
/// </list>
/// </para>
/// </summary>
public static class ContentHash
{
    public const string ModManifestFileName = "mod.yaml";

    /// <summary>SHA-256 over every file under <paramref name="directory"/> (recursive), excluding any
    /// whose file name is in <paramref name="excludeFileNames"/> (e.g. an install sidecar).</summary>
    public static string OfDirectory(string directory, IReadOnlySet<string>? excludeFileNames = null)
    {
        var files = Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
            .Where(f => excludeFileNames is null || !excludeFileNames.Contains(Path.GetFileName(f)))
            .Select(f => (Relative: Path.GetRelativePath(directory, f).Replace('\\', '/'), Full: f));
        return HashFiles(files);
    }

    /// <summary>
    /// SHA-256 over a mod's logical payload — <c>mod.yaml</c> and the patch files it references —
    /// rooted at <paramref name="modDirectory"/>. The same value whether computed on the repo source
    /// folder or on a freshly-fetched temp tree, since both carry exactly this set. Returns null when
    /// there's no readable <c>mod.yaml</c>.
    /// </summary>
    public static string? OfModPayload(string modDirectory)
    {
        var manifestPath = Path.Combine(modDirectory, ModManifestFileName);
        if (!File.Exists(manifestPath))
        {
            return null;
        }

        string manifestText;
        try
        {
            manifestText = File.ReadAllText(manifestPath);
        }
        catch
        {
            return null;
        }

        // mod.yaml is always part of the payload; add each referenced patch file that's present.
        var members = new List<(string Relative, string Full)> { (ModManifestFileName, manifestPath) };
        foreach (var patch in EnumeratePatchPaths(manifestText))
        {
            if (patch.Contains("..", StringComparison.Ordinal))
            {
                continue; // a traversal path is never a real payload member
            }
            var full = Path.Combine(modDirectory, patch.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(full))
            {
                members.Add((patch, full));
            }
        }

        return HashFiles(members);
    }

    private static string HashFiles(IEnumerable<(string Relative, string Full)> files)
    {
        using var sha256 = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var separator = new byte[] { 0 };
        var buffer = new byte[81920];

        foreach (var (relative, full) in files.OrderBy(f => f.Relative, StringComparer.OrdinalIgnoreCase))
        {
            sha256.AppendData(Encoding.UTF8.GetBytes(relative));
            sha256.AppendData(separator);

            using var fileStream = File.OpenRead(full);
            int read;
            while ((read = fileStream.Read(buffer, 0, buffer.Length)) > 0)
            {
                sha256.AppendData(buffer, 0, read);
            }

            sha256.AppendData(separator);
        }

        return Convert.ToHexString(sha256.GetHashAndReset()).ToLowerInvariant();
    }

    /// <summary>
    /// Every <c>patches:</c> entry anywhere in a mod.yaml (root <c>patches:</c> or nested in
    /// per-package <c>patchSets[*].patches</c>). A generic YAML walk — identical to the manager's
    /// remote fetcher, so the payload set matches what a <c>gh:</c> fetch transfers.
    /// </summary>
    public static List<string> EnumeratePatchPaths(string modYamlText)
    {
        var stream = new YamlStream();
        using var reader = new StringReader(modYamlText);
        stream.Load(reader);
        var result = new List<string>();
        if (stream.Documents.Count == 0 || stream.Documents[0].RootNode is not YamlMappingNode root)
        {
            return result;
        }

        CollectPatchPaths(root, result);
        return result;
    }

    private static void CollectPatchPaths(YamlNode node, List<string> sink)
    {
        switch (node)
        {
            case YamlMappingNode mapping:
                foreach (var (key, value) in mapping.Children)
                {
                    if (key is YamlScalarNode { Value: "patches" } && value is YamlSequenceNode seq)
                    {
                        foreach (var item in seq.Children)
                        {
                            if (item is YamlScalarNode { Value: { Length: > 0 } p })
                            {
                                sink.Add(p);
                            }
                        }
                    }
                    else
                    {
                        CollectPatchPaths(value, sink);
                    }
                }
                break;
            case YamlSequenceNode sequence:
                foreach (var item in sequence.Children)
                {
                    CollectPatchPaths(item, sink);
                }
                break;
        }
    }
}
