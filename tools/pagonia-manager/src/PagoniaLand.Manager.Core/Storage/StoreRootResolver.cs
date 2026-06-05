namespace PagoniaLand.Manager;

public static class StoreRootResolver
{
    public const string EnvironmentVariableName = "PAGONIA_MANAGER_STORE";

    public enum ResolutionSource
    {
        Flag,
        EnvironmentVariable,
        PlatformDefault
    }

    public sealed record Resolution(string Root, ResolutionSource Source);

    public static Resolution Resolve(string? flagPath = null, Func<string, string?>? environmentReader = null)
    {
        if (!string.IsNullOrWhiteSpace(flagPath))
        {
            return new Resolution(Path.GetFullPath(flagPath), ResolutionSource.Flag);
        }

        var reader = environmentReader ?? Environment.GetEnvironmentVariable;
        var fromEnv = reader(EnvironmentVariableName);
        if (!string.IsNullOrWhiteSpace(fromEnv))
        {
            return new Resolution(Path.GetFullPath(fromEnv), ResolutionSource.EnvironmentVariable);
        }

        return new Resolution(DefaultPlatformPath(), ResolutionSource.PlatformDefault);
    }

    public static string DefaultPlatformPath()
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(local, "PagoniaLand", "Manager");
    }
}
