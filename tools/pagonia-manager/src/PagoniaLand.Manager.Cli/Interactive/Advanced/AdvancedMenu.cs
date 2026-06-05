using Spectre.Console;

namespace PagoniaLand.Manager.Cli.Interactive;

internal static class AdvancedMenu
{
    public static void Run(SessionState session)
    {
        while (true)
        {
            AnsiConsole.Clear();
            AnsiConsole.Write(new Rule("[bold aqua]Advanced[/]").LeftJustified());
            AnsiConsole.MarkupLine("[dim]Full CLI surface, organised by category. Use this when you know what you want to do and the main-menu wizards don't cover it.[/]");
            AnsiConsole.WriteLine();

            var pick = AdvancedHelpers.NavSelect(
                "[bold]Category[/]",
                "Store", "Mods", "Profiles", "Active Profile", "Tweaks", "Collections", "Catalogs", "Game Ops", "Schema Validate", "Back");

            switch (pick)
            {
                case "Store": AdvancedStore.Run(session); break;
                case "Mods": AdvancedMods.Run(session); break;
                case "Profiles": AdvancedProfiles.Run(session); break;
                case "Active Profile": AdvancedActiveProfile.Run(session); break;
                case "Tweaks": AdvancedTweaks.Run(session); break;
                case "Collections": AdvancedCollections.Run(session); break;
                case "Catalogs": AdvancedCatalogs.Run(session); break;
                case "Game Ops": AdvancedGameOps.Run(session); break;
                case "Schema Validate": AdvancedSchemaValidate.Run(session); break;
                default: return;
            }
        }
    }
}
