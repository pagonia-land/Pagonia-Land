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

    // Visible hint on free-text prompts: submitting an empty line cancels the
    // prompt and returns the caller to its previous menu — the discoverable
    // alternative to Ctrl+C. Worded "cancel" rather than "back" because the
    // shell already prints "...return to the menu" afterwards, so "back" twice
    // reads redundant.
    public const string BackHint = "[dim](empty to cancel)[/]";

    // Cancellable free-text prompt. Returns false when the user submits an empty
    // line (= cancel); otherwise sets value to the trimmed entry. An optional
    // validator runs on non-empty input only, so empty always means "cancel" even
    // for otherwise-required fields.
    public static bool TryPromptText(string label, out string value, Func<string, ValidationResult>? validate = null)
    {
        var prompt = new TextPrompt<string>($"{label} {BackHint}").AllowEmpty();
        if (validate is not null)
        {
            prompt = prompt.Validate(s => string.IsNullOrWhiteSpace(s) ? ValidationResult.Success() : validate(s));
        }
        value = (AnsiConsole.Prompt(prompt) ?? string.Empty).Trim();
        return value.Length > 0;
    }

    // Path variant of TryPromptText: empty = back, otherwise the path must exist
    // (optionally as a directory / file).
    public static bool TryPromptExistingPath(string label, out string value, bool mustBeDirectory = false, bool mustBeFile = false)
    {
        return TryPromptText(label, out value, p =>
        {
            if (mustBeDirectory && !Directory.Exists(p)) return ValidationResult.Error("not a directory");
            if (mustBeFile && !File.Exists(p)) return ValidationResult.Error("not a file");
            if (!mustBeDirectory && !mustBeFile && !File.Exists(p) && !Directory.Exists(p))
                return ValidationResult.Error("path does not exist");
            return ValidationResult.Success();
        });
    }

    public static bool Confirm(string question, bool defaultValue)
        => AnsiConsole.Prompt(new ConfirmationPrompt(question) { DefaultValue = defaultValue });

    // Renders a navigation SelectionPrompt where the trailing "Back" entry is set
    // apart from the action items above it. The separator is a blank, non-selectable
    // group header (Spectre skips group headers during navigation), so the cursor
    // jumps straight from the last action item to "Back" — the blank line can't be
    // landed on. Returns the chosen item.
    // Remembers the last pick per menu (keyed by title) so re-showing a menu after
    // an action lands the cursor back on the entry you used, not at the top.
    private static readonly Dictionary<string, string> LastNavChoice = new(StringComparer.Ordinal);

    public static string NavSelect(string titleMarkup, params string[] items)
    {
        if (items.Length <= 1)
        {
            return items.Length == 1 ? items[0] : string.Empty;
        }

        var prompt = new SelectionPrompt<string>()
            .Title(titleMarkup)
            .HighlightStyle(new Style(foreground: Color.Aqua))
            // Wrap top<->bottom so up on the first item lands on the last and vice versa.
            .WrapAround()
            // Fit the whole menu (action items + blank header + "Back") on one page so
            // the back-out action is never hidden below a scroll fold — the default page
            // size is 10, which the extra rows can push a 10-item menu past. Clamped to
            // 25 so an unusually long dynamic list still pages (min 3 per Spectre).
            .PageSize(Math.Clamp(items.Length + 2, 3, 25))
            // Escape choice display text so a label carrying markup metacharacters
            // (e.g. a profile name with '[' / ']') renders literally instead of
            // throwing. Display-only; the returned value stays the original string.
            .UseConverter(Markup.Escape);

        prompt.AddChoices(items[..^1]);
        // Blank group header → a non-selectable spacer line; "Back" is its only child.
        prompt.AddChoiceGroup(" ", items[^1]);
        // ESC backs out of the menu — returns the trailing "Back" entry, so the
        // existing `choice == Back` / `default: return` handlers catch it unchanged.
        prompt.AddCancelResult(items[^1]);

        // Restore the previous selection for this menu when it's still a valid choice
        // (dynamic lists may have changed; then Spectre falls back to the first item).
        if (LastNavChoice.TryGetValue(titleMarkup, out var last) && items.Contains(last))
        {
            prompt.DefaultValue = last;
        }

        var choice = AnsiConsole.Prompt(prompt);
        LastNavChoice[titleMarkup] = choice;
        return choice;
    }

    // Single-select from a (potentially long) data list with type-to-filter search.
    // Use for dynamic lists — installed mods, profiles, subscriptions, tweaks,
    // collections, load order — where the user may have many entries. NOT for the
    // short fixed nav menus (those use NavSelect / MainMenu, no search).
    public static string Pick(string titleMarkup, IEnumerable<string> items)
    {
        var list = items as IReadOnlyList<string> ?? items.ToList();
        // Guard the degenerate sizes Spectre handles poorly: an empty choice
        // list throws an opaque error, and a single item + EnableSearch/WrapAround
        // is an untested corner with no real choice to make. Zero is a caller bug
        // (use TryPick for lists that can legitimately be empty); one short-circuits.
        if (list.Count == 0)
        {
            throw new InvalidOperationException(
                $"Pick('{titleMarkup}') was called with no items — use TryPick for lists that can be empty.");
        }
        if (list.Count == 1)
        {
            return list[0];
        }
        return AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title(titleMarkup)
                .HighlightStyle(new Style(foreground: Color.Aqua))
                .WrapAround()
                .PageSize(15)
                .EnableSearch()
                .SearchPlaceholderText("(type to filter)")
                // Escape choice display text: items are user data (profile names,
                // catalog canonicals) that may contain Spectre markup metacharacters
                // like '[' / ']', which would otherwise throw on render. Display-only —
                // the returned value is the original unescaped string.
                .UseConverter(Markup.Escape)
                .AddChoices(list));
    }

    /// <summary>
    /// Like <see cref="Pick"/> but returns <c>false</c> (rather than throwing)
    /// when the choice list is empty. Use at call sites where the candidate list
    /// can legitimately collapse to zero — e.g. picking a reorder anchor when the
    /// load order, after excluding the moved mod, has no other entries.
    /// </summary>
    public static bool TryPick(string titleMarkup, IEnumerable<string> items, out string choice)
    {
        var list = items as IReadOnlyList<string> ?? items.ToList();
        if (list.Count == 0)
        {
            choice = string.Empty;
            return false;
        }
        choice = Pick(titleMarkup, list);
        return true;
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
    public static bool TryPromptGameRoot(SessionState session, out string path)
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
                path = resolved.Path!;
                return true;
            }
        }

        // Both layouts work end-to-end since the live-install path earlier work:
        //   * Live install ( <root>/pak/*.pak ) - paks are extracted on plan,
        //     rebuilt + atomically written back on deploy.
        //   * Extracted layout ( <root>/core/gdb/*.gd.xml ) - loose XMLs are
        //     patched in place; same shape as the repo's local game-gdb/.
        // The prompt names both so users don't second-guess what's expected.
        if (!TryPromptExistingPath(
                "[bold]Path to the game install[/] (live [aqua]pak/[/] folder or extracted [aqua]game-gdb/[/]-style layout):",
                out path, mustBeDirectory: true))
        {
            return false;
        }
        session.GameRoot = path;
        GameRootResolver.SetStoredDefault(layout, path);
        return true;
    }

    // Runs `action` while showing the plain-ASCII dot ticker (StagePrinter), the
    // same indeterminate-progress style the deploy / rollback stages use. We do NOT
    // use Spectre's AnsiConsole.Status() spinner here: its animation relies on ANSI
    // cursor-up/line-clear that some Windows terminals don't render, leaving a frozen
    // glyph that looks like a hung program. The action populates a ref-captured slot
    // via closure — we can't `out` across the lambda boundary.
    public static void Spin(string label, Action action)
    {
        using var printer = new StagePrinter();
        printer.Start(label);
        try { action(); }
        finally { printer.Stop(); }
    }
}
