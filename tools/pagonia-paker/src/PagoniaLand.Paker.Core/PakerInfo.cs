using System.Reflection;

namespace PagoniaLand.Paker;

public static class PakerInfo
{
    public const string ProductName = "Pagonia Land Paker";
    public const string CommandName = "pagonia-paker";

    public static string Version { get; } = ReadVersion();

    private static string ReadVersion()
    {
        var assembly = typeof(PakerInfo).Assembly;
        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();

        if (informational is not null && !string.IsNullOrWhiteSpace(informational.InformationalVersion))
        {
            return informational.InformationalVersion;
        }

        return assembly.GetName().Version?.ToString(3) ?? "0.0.0";
    }
}
