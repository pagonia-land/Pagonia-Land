using System.Reflection;

namespace PagoniaLand.Patcher;

public static class PatcherInfo
{
    public const string ProductName = "Pagonia Land Patcher";
    public const string CommandName = "pagonia-patcher";

    public static string Version { get; } = ReadVersion();

    private static string ReadVersion()
    {
        var assembly = typeof(PatcherInfo).Assembly;
        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();

        if (informational is not null && !string.IsNullOrWhiteSpace(informational.InformationalVersion))
        {
            return informational.InformationalVersion;
        }

        return assembly.GetName().Version?.ToString(3) ?? "0.0.0";
    }
}
