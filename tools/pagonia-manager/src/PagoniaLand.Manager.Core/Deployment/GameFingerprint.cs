using System.Security.Cryptography;
using System.Text;

namespace PagoniaLand.Manager;

public static class GameFingerprint
{
    public const int FingerprintLength = 16;

    // SHA-256 of: absolute game-root path (case-folded on Windows) + system.json content if present.
    // Truncated to a 16-char hex prefix for readable folder names.
    //
    // Intentionally lightweight: stays stable across game updates that don't touch system.json,
    // distinguishes different installs at different paths, and works for test fixtures (the
    // path alone produces a stable, distinct fingerprint).
    //
    // Consequence to be aware of: because the path is part of the hash, moving the install to a
    // new folder changes its fingerprint, so the old path's deploy history/backups become orphaned
    // (recoverable via `deploys list-orphans` / `deploys clean`, never silently deleted).
    public static string Compute(string gameRoot)
    {
        if (string.IsNullOrWhiteSpace(gameRoot))
        {
            throw new ArgumentException("Game root must not be empty.", nameof(gameRoot));
        }

        var absolute = Path.GetFullPath(gameRoot);
        var canonicalPath = OperatingSystem.IsWindows()
            ? absolute.ToLowerInvariant()
            : absolute;

        using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        sha.AppendData(Encoding.UTF8.GetBytes(canonicalPath));
        sha.AppendData([0]);

        var systemJsonPath = Path.Combine(absolute, GameLayoutConstants.SystemFingerprintFile);
        if (File.Exists(systemJsonPath))
        {
            sha.AppendData(File.ReadAllBytes(systemJsonPath));
        }

        var hex = Convert.ToHexString(sha.GetHashAndReset()).ToLowerInvariant();
        return hex[..FingerprintLength];
    }
}
