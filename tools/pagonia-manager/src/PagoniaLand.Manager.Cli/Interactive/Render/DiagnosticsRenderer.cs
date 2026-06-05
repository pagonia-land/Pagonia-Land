using PagoniaLand.Manager;
using Spectre.Console;

namespace PagoniaLand.Manager.Cli.Interactive;

// Single rendering path for ManagerDiagnostic lists, so every wizard surfaces
// errors / warnings / info with the same color + symbol convention. Severity
// maps:  Error -> red [x]   Warning -> yellow [!]   Info -> dim [i].
internal static class DiagnosticsRenderer
{
    public static void Render(IReadOnlyList<ManagerDiagnostic> diagnostics)
    {
        if (diagnostics.Count == 0)
        {
            return;
        }

        foreach (var diagnostic in diagnostics)
        {
            var (marker, color) = diagnostic.Severity switch
            {
                ManagerDiagnosticSeverity.Error => ("[x]", "red"),
                ManagerDiagnosticSeverity.Warning => ("[!]", "yellow"),
                _ => ("[i]", "dim"),
            };

            // [[ ]] are Spectre's literal-bracket escapes — without them, codes like
            // "manager.profileEmpty" land inside unescaped [ ] and Spectre tries to
            // parse the code as a style name, which crashes the wizard.
            var line = $"  [{color}]{Markup.Escape(marker)}[/] [dim][[{Markup.Escape(diagnostic.Code)}]][/] {Markup.Escape(diagnostic.Message)}";
            if (!string.IsNullOrEmpty(diagnostic.Path))
            {
                line += $" [dim](at {Markup.Escape(diagnostic.Path)})[/]";
            }
            AnsiConsole.MarkupLine(line);
        }
    }

    public static bool HasErrors(IReadOnlyList<ManagerDiagnostic> diagnostics)
        => diagnostics.Any(d => d.Severity == ManagerDiagnosticSeverity.Error);
}
