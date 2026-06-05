namespace PagoniaLand.Manager;

public static class ProfileNameValidator
{
    private static readonly char[] ForbiddenCharacters =
    [
        '/', '\\', ':', '*', '?', '"', '<', '>', '|',
    ];

    // Windows reserves these device names (case-insensitive, with or without an
    // extension). A profile named e.g. "CON" would map to "CON.profile.yaml", which
    // cannot be created or read on Windows. Refuse them on every platform so a
    // profile stays portable.
    private static readonly HashSet<string> ReservedDeviceNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    };

    public const int MaxLength = 64;

    public static bool IsValid(string? name, out string reason)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            reason = "profile name must not be empty.";
            return false;
        }

        if (name.Length > MaxLength)
        {
            reason = $"profile name must be at most {MaxLength} characters.";
            return false;
        }

        if (name != name.Trim())
        {
            reason = "profile name must not have leading or trailing whitespace.";
            return false;
        }

        if (name == "." || name == "..")
        {
            reason = "profile name must not be '.' or '..'.";
            return false;
        }

        if (name.StartsWith('.'))
        {
            reason = "profile name must not start with '.'.";
            return false;
        }

        if (name.IndexOfAny(ForbiddenCharacters) >= 0)
        {
            reason = $"profile name must not contain any of: {new string(ForbiddenCharacters)}.";
            return false;
        }

        // Compare the base name (before any extension) against the reserved set —
        // "CON" and "CON.backup" are both reserved on Windows.
        var baseName = name.Contains('.', StringComparison.Ordinal)
            ? name[..name.IndexOf('.', StringComparison.Ordinal)]
            : name;
        if (ReservedDeviceNames.Contains(baseName))
        {
            reason = $"profile name '{name}' uses a reserved Windows device name ({baseName}).";
            return false;
        }

        reason = string.Empty;
        return true;
    }
}
