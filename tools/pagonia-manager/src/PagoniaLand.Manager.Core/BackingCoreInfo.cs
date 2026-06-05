using PagoniaLand.Paker;
using PagoniaLand.Patcher;

namespace PagoniaLand.Manager;

public static class BackingCoreInfo
{
    public static string PatcherProductName => PatcherInfo.ProductName;
    public static string PatcherCommandName => PatcherInfo.CommandName;
    public static string PatcherVersion => PatcherInfo.Version;

    public static string PakerProductName => PakerInfo.ProductName;
    public static string PakerCommandName => PakerInfo.CommandName;
    public static string PakerVersion => PakerInfo.Version;
}
