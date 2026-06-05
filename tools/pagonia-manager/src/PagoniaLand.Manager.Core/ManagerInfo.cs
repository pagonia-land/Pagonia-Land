using System.Reflection;

namespace PagoniaLand.Manager;

public static class ManagerInfo
{
    public const string ProductName = "Pagonia Land Manager";
    public const string CommandName = "pagonia-manager";

    public static string Version { get; } = ReadVersion();

    private static string ReadVersion()
    {
        var assembly = typeof(ManagerInfo).Assembly;
        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();

        if (informational is not null && !string.IsNullOrWhiteSpace(informational.InformationalVersion))
        {
            return informational.InformationalVersion;
        }

        return assembly.GetName().Version?.ToString(3) ?? "0.0.0";
    }
}
