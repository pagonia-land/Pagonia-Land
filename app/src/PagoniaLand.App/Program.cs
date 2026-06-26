using Avalonia;

namespace PagoniaLand.App;

/// <summary>Entry point for the "Pagonia Land" desktop app.</summary>
internal static class Program
{
    // Avalonia requires an STA thread; do not use any Avalonia/UI types before AppMain runs.
    [STAThread]
    public static void Main(string[] args) =>
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
