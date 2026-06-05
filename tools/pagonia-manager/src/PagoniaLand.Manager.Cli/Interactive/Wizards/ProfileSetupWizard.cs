using PagoniaLand.Manager;
using Spectre.Console;

namespace PagoniaLand.Manager.Cli.Interactive;

internal static class ProfileSetupWizard
{
    /// <summary>
    /// Runs the new-profile wizard. Returns true when the wizard reached
    /// its terminal result panel (so the caller should Pause() to let the
    /// user read it) and false when an early-return rendered a self-
    /// explanatory notice (store-not-initialised) that already explains
    /// what the user should do next — pausing on top of that just adds a
    /// stray "Press any key..." prompt.
    /// </summary>
    public static bool Run(SessionState session)
    {
        AnsiConsole.Clear();
        AnsiConsole.Write(new Rule("[bold aqua]Create a new profile[/]").LeftJustified());
        AnsiConsole.WriteLine();

        var layout = session.GetLayout();
        if (!new StoreStateReader().Exists(layout))
        {
            AnsiConsole.MarkupLine("[yellow]Store not initialised. Use Advanced -> Store -> init first.[/]");
            return false;
        }

        var profileName = AnsiConsole.Prompt(
            new TextPrompt<string>("[bold]Profile name[/]:")
                .ValidationErrorMessage("[red]Invalid name.[/]")
                .Validate(name => ProfileNameValidator.IsValid(name, out var reason)
                    ? ValidationResult.Success()
                    : ValidationResult.Error(reason)));

        // Gather selections UPFRONT, then execute everything in one linear pass.
        // This lets the user back out (Esc) before any mutation happens.
        var installedMods = new ModLister().List(layout);
        var pickedMods = new List<string>();

        if (installedMods.Count == 0)
        {
            AnsiConsole.MarkupLine("[dim]No mods installed yet — profile will be created empty. Install some mods first to populate it later.[/]");
        }
        else
        {
            AnsiConsole.WriteLine();
            pickedMods = AnsiConsole.Prompt(
                new MultiSelectionPrompt<string>()
                    .Title($"[bold]Which mods should [aqua]{Markup.Escape(profileName)}[/] enable?[/]")
                    .NotRequired()
                    .PageSize(15)
                    .InstructionsText("[dim](space to toggle, enter to confirm; pick nothing to start empty)[/]")
                    .HighlightStyle(new Style(foreground: Color.Aqua))
                    .AddChoices(installedMods.Select(m => $"{m.Id}@{m.Version}")));
        }

        var switchToIt = AnsiConsole.Prompt(
            new ConfirmationPrompt($"Switch to [aqua]{Markup.Escape(profileName)}[/] as the active profile after setup?")
                { DefaultValue = true });

        AnsiConsole.WriteLine();

        // Execute: create -> use -> enable each pick -> optionally switch back.
        var lifecycle = new ProfileLifecycleService();
        var stateReader = new StoreStateReader();
        var priorActive = stateReader.Read(layout).ActiveProfile ?? StoreLayoutConstants.DefaultProfileName;

        var createResult = lifecycle.Create(layout, profileName);
        DiagnosticsRenderer.Render(createResult.Diagnostics);
        if (!createResult.Success)
        {
            AnsiConsole.MarkupLine("[red]Profile create failed.[/]");
            return true;
        }
        AnsiConsole.MarkupLine($"[green]Created[/] profile [aqua]{Markup.Escape(profileName)}[/].");

        if (pickedMods.Count > 0)
        {
            // To enable mods into the new profile, the active-profile services
            // operate on whatever profile is current — so switch in, enable, then
            // switch back if the user didn't want to keep it active.
            //
            // Critical: if this Use() fails (state.yaml unwritable, etc.), the
            // following Enable() calls operate on the PRIOR active profile and
            // silently pollute it. Abort the wizard with a diagnostic instead of
            // printing a green-success message for an empty new profile and a
            // polluted old one.
            var useResult = lifecycle.Use(layout, profileName);
            DiagnosticsRenderer.Render(useResult.Diagnostics);
            if (!useResult.Success)
            {
                AnsiConsole.MarkupLine($"[red]Failed to switch into[/] [aqua]{Markup.Escape(profileName)}[/]; mod picks were not enabled to avoid polluting the prior active profile.");
                return true;
            }

            var active = new ActiveProfileService();
            var enabledCount = 0;
            foreach (var pick in pickedMods)
            {
                var (id, version) = SplitIdVersion(pick);
                var enableResult = active.Enable(layout, id, version);
                DiagnosticsRenderer.Render(enableResult.Diagnostics);
                if (!enableResult.Success)
                {
                    AnsiConsole.MarkupLine($"[red]Failed to enable {Markup.Escape(id)}@{Markup.Escape(version)}[/]");
                }
                else if (enableResult.Mutated)
                {
                    enabledCount++;
                }
            }
            AnsiConsole.MarkupLine($"[green]Enabled[/] {enabledCount} of {pickedMods.Count} picked mod(s).");
        }

        if (!switchToIt)
        {
            // Switch back to whatever was active before, even if we needed it
            // briefly to populate enabled mods.
            if (!string.Equals(priorActive, profileName, StringComparison.Ordinal))
            {
                var switchBack = lifecycle.Use(layout, priorActive);
                DiagnosticsRenderer.Render(switchBack.Diagnostics);
                if (switchBack.Success)
                {
                    AnsiConsole.MarkupLine($"[dim]Switched back to[/] [aqua]{Markup.Escape(priorActive)}[/].");
                }
                else
                {
                    AnsiConsole.MarkupLine($"[red]Failed to switch back to[/] [aqua]{Markup.Escape(priorActive)}[/]; active profile is still [aqua]{Markup.Escape(profileName)}[/].");
                }
            }
        }
        else if (pickedMods.Count == 0)
        {
            // pickedMods.Count > 0 already switched into the new profile at
            // line 81 to populate enabled mods, so a second Use() here would
            // be a redundant write + may surface a 'profileAlreadyActive'
            // info diagnostic. Only run the switch when we never switched in
            // earlier (the empty-picks path).
            var switchIn = lifecycle.Use(layout, profileName);
            DiagnosticsRenderer.Render(switchIn.Diagnostics);
            if (switchIn.Success)
            {
                AnsiConsole.MarkupLine($"[bold green]Active profile is now[/] [aqua]{Markup.Escape(profileName)}[/].");
            }
            else
            {
                AnsiConsole.MarkupLine($"[red]Failed to switch to[/] [aqua]{Markup.Escape(profileName)}[/]; active profile is still [aqua]{Markup.Escape(priorActive)}[/].");
            }
        }
        else
        {
            // pickedMods.Count > 0 path: we already switched into the new
            // profile at line 81 to enable mods against it. No second Use()
            // needed — just confirm the success.
            AnsiConsole.MarkupLine($"[bold green]Active profile is now[/] [aqua]{Markup.Escape(profileName)}[/].");
        }

        return true;
    }

    private static (string Id, string Version) SplitIdVersion(string label)
    {
        var at = label.LastIndexOf('@');
        return at < 0 ? (label, string.Empty) : (label[..at], label[(at + 1)..]);
    }
}
