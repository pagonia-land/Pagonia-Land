namespace PagoniaLand.Manager;

public sealed class ProfileSummary
{
    public string Name { get; init; } = string.Empty;
    public bool IsActive { get; init; }
    public bool IsDefault { get; init; }
    public int EnabledModCount { get; init; }
    public string? Collection { get; init; }
    public string FilePath { get; init; } = string.Empty;
}
