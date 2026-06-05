using System.Globalization;
using PagoniaLand.Manager;
using PagoniaLand.Patcher;
using Spectre.Console;

namespace PagoniaLand.Manager.Cli.Interactive;

// Interactive "Configure this mod (tweaks)" flow, reached from Manage Active
// Profile. Lists the active profile's enabled mods that declare at least one
// tweak; selecting one opens a per-tweak edit loop whose prompt type matches
// the declared type (confirmation for boolean, selection for enum, typed text
// for number/integer), with reset-to-default + back at every level. All reads
// and writes go through TweakOverrideService so validation + origin tracking
// stay identical to the `tweak` CLI verbs.
internal static class ConfigureModWizard
{
    private const string ResetAll = "Reset ALL tweaks to defaults";
    private const string Back = "Back";

    /// <summary>True when at least one mod enabled in the active profile declares a tweak —
    /// used to hide the menu entry when there's nothing to configure.</summary>
    public static bool HasConfigurableMods(StoreLayout layout)
        => ConfigurableMods(layout).Count > 0;

    public static void Run(StoreLayout layout)
    {
        AnsiConsole.WriteLine();
        var configurable = ConfigurableMods(layout);
        if (configurable.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No enabled mod in the active profile declares any tweaks.[/]");
            Pause();
            return;
        }

        var modId = AdvancedHelpers.NavSelect("Configure which mod?", configurable.Append(Back).ToArray());

        if (modId == Back)
        {
            return;
        }

        ConfigureMod(layout, modId);
    }

    private static void ConfigureMod(StoreLayout layout, string modId)
    {
        var service = new TweakOverrideService();

        while (true)
        {
            AnsiConsole.Clear();
            AnsiConsole.Write(new Rule($"[bold aqua]Configure {Markup.Escape(modId)}[/]").LeftJustified());

            var read = service.Read(layout, profileName: null, modId);
            if (!read.Success)
            {
                DiagnosticsRenderer.Render(read.Diagnostics);
                Pause();
                return;
            }

            RenderTweakTable(read.Tweaks);

            // Map a display label back to the tweak id so the picker can show
            // the current value + origin without losing the id.
            var labels = read.Tweaks.ToDictionary(
                t => $"{t.Declaration.Id} = {t.Value}  [{t.Origin}]",
                t => t.Declaration.Id);

            var pick = AdvancedHelpers.NavSelect("Edit which tweak?", labels.Keys.Append(ResetAll).Append(Back).ToArray());

            if (pick == Back)
            {
                return;
            }

            if (pick == ResetAll)
            {
                var reset = service.Reset(layout, profileName: null, modId, tweakId: null);
                DiagnosticsRenderer.Render(reset.Diagnostics);
                AnsiConsole.MarkupLine(reset.Mutated
                    ? "[green]All tweaks reset to their defaults.[/]"
                    : "[dim]Nothing to reset — all tweaks already at defaults.[/]");
                Pause();
                continue;
            }

            var tweak = read.Tweaks.First(t => t.Declaration.Id == labels[pick]);
            EditTweak(layout, service, modId, tweak);
        }
    }

    private static void EditTweak(StoreLayout layout, TweakOverrideService service, string modId, TweakValueView tweak)
    {
        const string SetValue = "Set a new value";
        const string ResetOne = "Reset to default";
        const string Cancel = "Cancel";

        var action = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title($"[bold]{Markup.Escape(tweak.Declaration.Id)}[/] — {Markup.Escape(tweak.Declaration.Label)} (current: [aqua]{Markup.Escape(tweak.Value)}[/])")
                .HighlightStyle(new Style(foreground: Color.Aqua))
                .AddChoices(SetValue, ResetOne, Cancel));

        switch (action)
        {
            case ResetOne:
                var reset = service.Reset(layout, profileName: null, modId, tweak.Declaration.Id);
                DiagnosticsRenderer.Render(reset.Diagnostics);
                AnsiConsole.MarkupLine(reset.Mutated
                    ? $"[green]'{Markup.Escape(tweak.Declaration.Id)}' reset to default ({Markup.Escape(tweak.Declaration.Default)}).[/]"
                    : "[dim]No stored override to reset.[/]");
                Pause();
                return;

            case Cancel:
                return;
        }

        var value = PromptForValue(tweak.Declaration);
        if (value is null)
        {
            return; // user backed out of the value prompt
        }

        var result = service.Set(layout, profileName: null, modId, tweak.Declaration.Id, value);
        DiagnosticsRenderer.Render(result.Diagnostics);
        if (result.Success)
        {
            AnsiConsole.MarkupLine($"[green]Set '{Markup.Escape(tweak.Declaration.Id)}' = {Markup.Escape(value)}.[/]");
        }
        Pause();
    }

    // Prompt for a new value whose input shape matches the declared type. Returns
    // null when the user cancels (enum/number empty input). TweakOverrideService.Set
    // is the authoritative validator; the client-side checks here are only for nicer
    // immediate feedback on numeric range.
    private static string? PromptForValue(TweakDeclaration declaration)
    {
        switch (declaration.Type)
        {
            case "boolean":
                var on = AnsiConsole.Prompt(
                    new ConfirmationPrompt($"Enable '{Markup.Escape(declaration.Id)}'?") { DefaultValue = string.Equals(declaration.Default, "true", StringComparison.OrdinalIgnoreCase) });
                return on ? "true" : "false";

            case "enum":
                var choice = AnsiConsole.Prompt(
                    new SelectionPrompt<string>()
                        .Title($"Value for '{Markup.Escape(declaration.Id)}':")
                        .HighlightStyle(new Style(foreground: Color.Aqua))
                        .AddChoices(declaration.Values.Select(v => v.Value)));
                return choice;

            case "integer":
                return AnsiConsole.Prompt(
                    new TextPrompt<int>($"New integer for '{Markup.Escape(declaration.Id)}'{RangeHint(declaration)}:")
                        .Validate(n => InRange(declaration, n)
                            ? ValidationResult.Success()
                            : ValidationResult.Error($"Out of range {RangeHint(declaration)}")))
                    .ToString(CultureInfo.InvariantCulture);

            case "number":
                // TextPrompt<string> (an AOT-proven instantiation) + manual parse,
                // rather than TextPrompt<double>, keeps the generic-prompt surface
                // to the types Spectre's AOT annotations already cover here.
                return AnsiConsole.Prompt(
                    new TextPrompt<string>($"New number for '{Markup.Escape(declaration.Id)}'{RangeHint(declaration)}:")
                        .Validate(raw =>
                            !double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var n)
                                ? ValidationResult.Error("Not a number")
                                : InRange(declaration, n)
                                    ? ValidationResult.Success()
                                    : ValidationResult.Error($"Out of range {RangeHint(declaration)}")));

            default:
                // Unknown declared type — fall back to free text; Set still validates.
                return AnsiConsole.Prompt(new TextPrompt<string>($"New value for '{Markup.Escape(declaration.Id)}':").AllowEmpty());
        }
    }

    private static bool InRange(TweakDeclaration declaration, double value)
        => (declaration.Min is not { } min || value >= min)
        && (declaration.Max is not { } max || value <= max);

    private static string RangeHint(TweakDeclaration declaration)
    {
        if (declaration.Min is null && declaration.Max is null)
        {
            return string.Empty;
        }
        var min = declaration.Min?.ToString(CultureInfo.InvariantCulture) ?? "-∞";
        var max = declaration.Max?.ToString(CultureInfo.InvariantCulture) ?? "+∞";
        return $" ({min}..{max})";
    }

    private static void RenderTweakTable(IReadOnlyList<TweakValueView> tweaks)
    {
        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn("Tweak");
        table.AddColumn("Value");
        table.AddColumn("Origin");
        table.AddColumn("Default");

        foreach (var t in tweaks)
        {
            table.AddRow(
                Markup.Escape(t.Declaration.Id),
                Markup.Escape(t.Value),
                Markup.Escape(t.Origin),
                Markup.Escape(t.Declaration.Default));
        }

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();
    }

    // Enabled mods (active profile) that declare at least one tweak.
    private static IReadOnlyList<string> ConfigurableMods(StoreLayout layout)
    {
        var show = new ActiveProfileService().Show(layout);
        if (show.Profile is null)
        {
            return [];
        }

        var service = new TweakOverrideService();
        return show.Profile.EnabledMods
            .Where(m =>
            {
                var read = service.Read(layout, profileName: null, m.Id);
                return read.Success && read.Tweaks.Count > 0;
            })
            .Select(m => m.Id)
            .ToList();
    }

    private static void Pause()
    {
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[dim]Press any key...[/]");
        Console.ReadKey(intercept: true);
    }
}
