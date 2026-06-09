using Spectre.Console;

namespace PagoniaLand.Manager.Cli.Interactive;

// String-based menu items so SelectionPrompt<string> stays trim-safe without
// extra DynamicDependency on enum metadata. Items are grouped via Spectre's
// AddChoiceGroup so the user sees three task-oriented clusters (Mods, Game,
// Status & Settings) plus two bottom-row utilities (Advanced, Quit).
// The Prompt() return value is matched against the constants by the
// shell's dispatcher.
internal static class MainMenu
{
    // ---- Mods cluster ----
    public const string InstallMod = "Install a mod";
    public const string BrowseCatalogs = "Browse community catalogs";
    public const string ManageActiveProfile = "Manage active profile";

    // ---- Game cluster ----
    public const string PlanDeploy = "Plan + deploy to game";
    public const string Rollback = "Roll back last deploy";
    public const string DeployHistory = "View deploy history";

    // ---- Status & Settings cluster ----
    public const string Status = "Status dashboard";
    public const string Settings = "Settings";
    public const string CleanBackups = "Clean up old deploy backups";

    // ---- Bottom row ----
    public const string Advanced = "Advanced";
    public const string Quit = "Quit";

    // Cursor lands on whatever was picked last time, so returning from a wizard
    // drops you back on the same entry instead of resetting to the top.
    private static string? _lastChoice;

    public static string Prompt()
    {
        var prompt = new SelectionPrompt<string>()
            .Title("[bold]What would you like to do?[/]")
            .PageSize(15)
            .WrapAround()
            .HighlightStyle(new Style(foreground: Color.Aqua));

        prompt.AddChoiceGroup("[bold]Mods[/]", new[] { InstallMod, BrowseCatalogs, ManageActiveProfile });
        prompt.AddChoiceGroup("[bold]Game[/]", new[] { PlanDeploy, Rollback, DeployHistory });
        prompt.AddChoiceGroup("[bold]Status & Settings[/]", new[] { Status, Settings, CleanBackups });
        prompt.AddChoices(Advanced, Quit);

        if (_lastChoice is not null)
        {
            prompt.DefaultValue = _lastChoice;
        }

        _lastChoice = AnsiConsole.Prompt(prompt);
        return _lastChoice;
    }
}
