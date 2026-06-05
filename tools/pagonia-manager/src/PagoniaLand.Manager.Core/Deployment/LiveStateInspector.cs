namespace PagoniaLand.Manager;

/// <summary>One live target that no longer matches what the manager last wrote
/// there. <see cref="ActualSha256"/> is <c>null</c> when the live file has gone
/// missing entirely.</summary>
public sealed record LiveStateDrift(
    string RelativePath,
    string ExpectedSha256,
    string? ActualSha256);

/// <summary>
/// Compares the live game files a deploy manifest recorded writing against the
/// hashes that manifest recorded for them. A mismatch means something other than
/// the manager — the user by hand, a second mod tool, a partial Steam patch —
/// changed the file after we deployed it.
///
/// <para>This reads back the post-deploy hashes the manifest already stores
/// (<see cref="DeployRebuiltPakEntry.NewSha256"/>,
/// <see cref="DeployAddedFileEntry.DeployedSha256"/>,
/// <see cref="DeployFileEntry.DeployedSha256"/>) — no new bookkeeping. Hashing
/// streams (paks can exceed 2 GB) via <see cref="FileHashing"/>.</para>
/// </summary>
public sealed class LiveStateInspector
{
    /// <summary>Hash each live target named in <paramref name="manifest"/> and
    /// return the ones whose bytes differ from the recorded post-deploy hash (or
    /// that have gone missing). An empty list means the live state still matches
    /// exactly what the manager last deployed.</summary>
    public IReadOnlyList<LiveStateDrift> Inspect(string gameRoot, DeployManifest manifest)
    {
        var drifted = new List<LiveStateDrift>();

        // RebuiltPaks -> live-install deploy (whole paks under <game>/pak/).
        foreach (var pak in manifest.RebuiltPaks)
        {
            Check(gameRoot, pak.TargetRelativePath, pak.NewSha256, drifted);
        }

        // AddedFiles -> Pattern B overlay paks under <game>/mods/ (both modes).
        foreach (var added in manifest.AddedFiles)
        {
            Check(gameRoot, added.RelativePath, added.DeployedSha256, drifted);
        }

        // ModifiedFiles -> extracted-layout loose XML written into the game tree.
        foreach (var modified in manifest.ModifiedFiles)
        {
            Check(gameRoot, modified.RelativePath, modified.DeployedSha256, drifted);
        }

        return drifted;
    }

    private static void Check(
        string gameRoot, string relativePath, string expectedSha256, List<LiveStateDrift> drifted)
    {
        // No recorded hash (older manifest field absent) -> nothing to compare against.
        if (string.IsNullOrWhiteSpace(expectedSha256) || string.IsNullOrWhiteSpace(relativePath))
        {
            return;
        }

        var targetPath = Path.Combine(gameRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(targetPath))
        {
            drifted.Add(new LiveStateDrift(relativePath, expectedSha256, null));
            return;
        }

        var actual = FileHashing.ComputeFileSha256(targetPath);
        if (!string.Equals(actual, expectedSha256, StringComparison.OrdinalIgnoreCase))
        {
            drifted.Add(new LiveStateDrift(relativePath, expectedSha256, actual));
        }
    }
}
