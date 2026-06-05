using Spectre.Console;

namespace PagoniaLand.Manager.Cli.Interactive;

// Shared bits for Advanced-menu commands: prompts, spinner wrapper, section header.
// Cuts the per-command boilerplate roughly in half while keeping every command
// file readable on its own.
internal static class AdvancedHelpers
{
    public static void Header(string title)
    {
        AnsiConsole.Clear();
        AnsiConsole.Write(new Rule($"[bold aqua]Advanced → {title}[/]").LeftJustified());
        AnsiConsole.WriteLine();
    }

    public static string PromptText(string label, bool allowEmpty = false)
    {
        var prompt = new TextPrompt<string>(label);
        if (allowEmpty)
        {
            prompt = prompt.AllowEmpty();
        }
        return AnsiConsole.Prompt(prompt) ?? string.Empty;
    }

    public static string PromptExistingPath(string label, bool mustBeDirectory = false, bool mustBeFile = false)
    {
        return AnsiConsole.Prompt(
            new TextPrompt<string>(label)
                .ValidationErrorMessage("[red]Path is invalid.[/]")
                .Validate(p =>
                {
                    if (string.IsNullOrWhiteSpace(p)) return ValidationResult.Error("path required");
                    if (mustBeDirectory && !Directory.Exists(p)) return ValidationResult.Error("not a directory");
                    if (mustBeFile && !File.Exists(p)) return ValidationResult.Error("not a file");
                    if (!mustBeDirectory && !mustBeFile && !File.Exists(p) && !Directory.Exists(p))
                        return ValidationResult.Error("path does not exist");
                    return ValidationResult.Success();
                }));
    }

    public static bool Confirm(string question, bool defaultValue)
        => AnsiConsole.Prompt(new ConfirmationPrompt(question) { DefaultValue = defaultValue });

    // Renders a navigation SelectionPrompt where the trailing "Back" entry is set
    // apart from the action items above it. The separator is a blank, non-selectable
    // group header (Spectre skips group headers during navigation), so the cursor
    // jumps straight from the last action item to "Back" — the blank line can't be
    // landed on. Returns the chosen item.
    public static string NavSelect(string titleMarkup, params string[] items)
    {
        if (items.Length <= 1)
        {
            return items.Length == 1 ? items[0] : string.Empty;
        }

        var prompt = new SelectionPrompt<string>()
            .Title(titleMarkup)
            .HighlightStyle(new Style(foreground: Color.Aqua))
            // Fit the whole menu (action items + blank header + "Back") on one page so
            // the back-out action is never hidden below a scroll fold — the default page
            // size is 10, which the extra rows can push a 10-item menu past. Clamped to
            // 25 so an unusually long dynamic list still pages (min 3 per Spectre).
            .PageSize(Math.Clamp(items.Length + 2, 3, 25));

        prompt.AddChoices(items[..^1]);
        // Blank group header → a non-selectable spacer line; "Back" is its only child.
        prompt.AddChoiceGroup(" ", items[^1]);

        return AnsiConsole.Prompt(prompt);
    }

    /// <summary>
    /// Game-root prompt with three-tier default resolution.
    /// In order: in-memory session value > persisted state.yaml.defaultGameRoot
    /// > Windows Steam default (only if that directory actually exists). The
    /// first hit gets a one-key [y/n] confirm; a "no" or a no-default cascade
    /// falls through to a text prompt. Any path that ends up returned is also
    /// persisted as the new defaultGameRoot, so the next cold start of the
    /// wizard becomes a one-key confirm too. Used uniformly by PlanDeployWizard,
    /// RollbackWizard, and AdvancedGameOps so the question + reuse behaviour is
    /// identical across every entry point.
    /// </summary>
    public static string PromptGameRoot(SessionState session)
    {
        var layout = session.GetLayout();
        var resolved = GameRootResolver.Resolve(layout, session.GameRoot);

        if (resolved.HasPath)
        {
            var sourceLabel = resolved.Source switch
            {
                GameRootSource.Session => "Use game root from this session",
                GameRootSource.StoredDefault => "Use default game folder",
                GameRootSource.PlatformDefault => "Use detected Steam install",
                _ => "Use",
            };
            if (Confirm($"{sourceLabel}: [aqua]{Markup.Escape(resolved.Path!)}[/]?", defaultValue: true))
            {
                session.GameRoot = resolved.Path;
                // Promote a PlatformDefault confirm into a persisted default so the
                // next cold start gets the (faster) StoredDefault path instead of
                // re-detecting Steam each time.
                GameRootResolver.SetStoredDefault(layout, resolved.Path);
                return resolved.Path!;
            }
        }

        // Both layouts work end-to-end since the live-install path earlier work:
        //   * Live install ( <root>/pak/*.pak ) - paks are extracted on plan,
        //     rebuilt + atomically written back on deploy.
        //   * Extracted layout ( <root>/core/gdb/*.gd.xml ) - loose XMLs are
        //     patched in place; same shape as the repo's local game-gdb/.
        // The prompt names both so users don't second-guess what's expected.
        var path = PromptExistingPath(
            "[bold]Path to the game install[/] (live [aqua]pak/[/] folder or extracted [aqua]game-gdb/[/]-style layout):",
            mustBeDirectory: true);
        session.GameRoot = path;
        GameRootResolver.SetStoredDefault(layout, path);
        return path;
    }

    // Runs `action` under a Spectre Status spinner. The action populates the
    // ref-captured slot via closure — we can't `out` across the lambda boundary.
    public static void Spin(string label, Action action)
    {
        AnsiConsole.Status().Spinner(Spinner.Known.Dots).Start(label, _ => action());
    }
}
