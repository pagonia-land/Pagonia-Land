using System.Security.Cryptography;

namespace PagoniaLand.Manager;

/// <summary>
/// Streaming SHA-256 over a file. Pak files can exceed the 2 GB single-array
/// limit, so callers that hash on-disk artifacts (deploy write-back, rollback
/// backup verification, live-state drift checks) stream rather than
/// <c>File.ReadAllBytes</c>. One implementation shared across those paths.
/// </summary>
internal static class FileHashing
{
    public static string ComputeFileSha256(string path)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        using var stream = File.OpenRead(path);
        var buffer = new byte[81920];
        int n;
        while ((n = stream.Read(buffer, 0, buffer.Length)) > 0)
        {
            hash.AppendData(buffer, 0, n);
        }
        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }
}
