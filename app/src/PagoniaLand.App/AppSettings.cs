using System;
using System.IO;
using System.Text.Json;

namespace PagoniaLand.App;

/// <summary>
/// Small persisted preferences — currently just the last install path the user generated from,
/// so the next launch prefills it. Stored under <c>%LOCALAPPDATA%/PagoniaLand</c>; best-effort
/// (a read/write failure is ignored).
/// </summary>
public sealed class AppSettings
{
    public string? LastPath { get; set; }

    /// <summary>The install fingerprint of the last generated catalog — to detect a game update.</summary>
    public string? LastFingerprint { get; set; }

    // Remembered window placement (normal-state bounds + whether it was maximized).
    public int? WindowX { get; set; }
    public int? WindowY { get; set; }
    public double? WindowWidth { get; set; }
    public double? WindowHeight { get; set; }
    public bool WindowMaximized { get; set; }

    // The origin of the screen the window was maximized on, so it re-maximizes onto that monitor.
    public int? MaximizedScreenX { get; set; }
    public int? MaximizedScreenY { get; set; }

    private static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PagoniaLand", "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllBytes(FilePath)) ?? new AppSettings();
            }
        }
        catch
        {
            // Ignore corrupt/unreadable settings — fall back to defaults.
        }

        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllBytes(FilePath, JsonSerializer.SerializeToUtf8Bytes(this));
        }
        catch
        {
            // Best-effort; never surface a settings-write failure.
        }
    }
}
