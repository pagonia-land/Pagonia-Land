using YamlDotNet.Serialization;

namespace PagoniaLand.Manager;

public sealed class InstallSidecar
{
    [YamlMember(Alias = "installedAt")]
    public string InstalledAt { get; init; } = string.Empty;

    [YamlMember(Alias = "sourcePath")]
    public string SourcePath { get; init; } = string.Empty;

    [YamlMember(Alias = "sourceType")]
    public string SourceType { get; init; } = string.Empty;

    [YamlMember(Alias = "manifestName")]
    public string ManifestName { get; init; } = string.Empty;

    /// <summary>
    /// Origin of the install in transport-neutral shorthand. Empty for local
    /// folder / zip installs; for remote installs this is the resolved source
    /// e.g. <c>gh:owner/repo#&lt;commit-sha&gt;/&lt;mod-id&gt;</c>. The SHA is
    /// pinned at fetch time so the sidecar names the exact commit installed,
    /// even after the branch moves on the remote.
    /// </summary>
    [YamlMember(Alias = "source")]
    public string Source { get; init; } = string.Empty;
}
