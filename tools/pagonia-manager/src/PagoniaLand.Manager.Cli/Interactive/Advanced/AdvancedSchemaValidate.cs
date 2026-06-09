using PagoniaLand.Manager;
using Spectre.Console;

namespace PagoniaLand.Manager.Cli.Interactive;

internal static class AdvancedSchemaValidate
{
    public static void Run(SessionState _)
    {
        AdvancedHelpers.Header("Schema Validate");

        // "Back" lets the user leave the page without being forced to pick a
        // kind + a path first — matches every other Advanced submenu.
        string[] choices = [.. ManagerReportKinds.All, "Back"];
        var kind = AdvancedHelpers.NavSelect("[bold]Report kind[/]", choices);
        if (kind == "Back")
        {
            return;
        }

        if (!AdvancedHelpers.TryPromptExistingPath("[bold]Report file[/] [dim](.json)[/]:", out var path, mustBeFile: true)) { return; }

        var diagnostics = new ManagerSchemaValidator().ValidateReport(kind, path);
        DiagnosticsRenderer.Render(diagnostics);

        var ok = !DiagnosticsRenderer.HasErrors(diagnostics);
        AnsiConsole.MarkupLine(ok ? "[green]Schema validation passed.[/]" : "[red]Schema validation failed.[/]");

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[dim]Press any key...[/]");
        Console.ReadKey(intercept: true);
    }
}
