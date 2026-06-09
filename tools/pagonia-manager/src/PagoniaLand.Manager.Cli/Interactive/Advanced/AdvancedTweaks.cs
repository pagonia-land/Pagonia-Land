using PagoniaLand.Manager;
using Spectre.Console;

namespace PagoniaLand.Manager.Cli.Interactive;

// Advanced mirror of the `tweak list / set / reset` CLI verbs, scoped to the
// active profile's enabled mods. The main-menu "Configure this mod (tweaks)"
// wizard is the friendlier, type-matched path; this is the raw verb catalog so
// Advanced stays a 1:1 reflection of the CLI surface.
internal static class AdvancedTweaks
{
    public static void Run(SessionState session)
    {
        while (true)
        {
            var pick = AdvancedHelpers.NavSelect("[bold]Tweaks[/] [dim](active profile)[/]", "list", "set", "reset", "Back");

            var layout = session.GetLayout();
            switch (pick)
            {
                case "list": RunList(layout); break;
                case "set": RunSet(layout); break;
                case "reset": RunReset(layout); break;
                default: return;
            }
        }
    }

    // Tweaks are stored per-profile, so the picker is the active profile's
    // enabled mods. Returns null (after a message) when there's nothing to pick.
    private static string? PickMod(StoreLayout layout, string verb)
    {
        var show = new ActiveProfileService().Show(layout);
        var mods = show.Profile?.EnabledMods.Select(m => m.Id).Distinct(StringComparer.Ordinal).ToList() ?? [];
        if (mods.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No enabled mods in the active profile.[/]");
            Pause();
            return null;
        }

        return AdvancedHelpers.Pick($"{verb} tweaks for which mod:", mods);
    }

    private static void RunList(StoreLayout layout)
    {
        AdvancedHelpers.Header("Tweaks → list");
        var modId = PickMod(layout, "List");
        if (modId is null) return;

        var r = new TweakOverrideService().Read(layout, profileName: null, modId);
        DiagnosticsRenderer.Render(r.Diagnostics);
        if (!r.Success) { Pause(); return; }
        if (r.Tweaks.Count == 0)
        {
            AnsiConsole.MarkupLine("[dim](this mod declares no tweaks)[/]");
            Pause();
            return;
        }

        var t = new Table().Border(TableBorder.Rounded)
            .AddColumn("Tweak").AddColumn("Value").AddColumn("Origin").AddColumn("Type").AddColumn("Default");
        foreach (var tw in r.Tweaks)
        {
            t.AddRow(
                $"[aqua]{Markup.Escape(tw.Declaration.Id)}[/]",
                Markup.Escape(tw.Value ?? string.Empty),
                Markup.Escape(tw.Origin.ToString()),
                Markup.Escape(tw.Declaration.Type.ToString()),
                Markup.Escape(tw.Declaration.Default ?? string.Empty));
        }
        AnsiConsole.Write(t);
        Pause();
    }

    private static void RunSet(StoreLayout layout)
    {
        AdvancedHelpers.Header("Tweaks → set");
        var modId = PickMod(layout, "Set");
        if (modId is null) return;

        var svc = new TweakOverrideService();
        var read = svc.Read(layout, profileName: null, modId);
        if (!read.Success || read.Tweaks.Count == 0)
        {
            DiagnosticsRenderer.Render(read.Diagnostics);
            AnsiConsole.MarkupLine("[yellow]This mod declares no tweaks to set.[/]");
            Pause();
            return;
        }

        var tweakId = AdvancedHelpers.Pick("Set which tweak:", read.Tweaks.Select(t => t.Declaration.Id));
        if (!AdvancedHelpers.TryPromptText($"New value for '[aqua]{Markup.Escape(tweakId)}[/]':", out var value)) { return; }
        var r = svc.Set(layout, profileName: null, modId, tweakId, value);
        DiagnosticsRenderer.Render(r.Diagnostics);
        if (r.Success)
            AnsiConsole.MarkupLine($"[green]Set[/] {Markup.Escape(modId)}:{Markup.Escape(tweakId)} = [aqua]{Markup.Escape(value)}[/]");
        Pause();
    }

    private static void RunReset(StoreLayout layout)
    {
        AdvancedHelpers.Header("Tweaks → reset");
        var modId = PickMod(layout, "Reset");
        if (modId is null) return;

        var svc = new TweakOverrideService();
        var read = svc.Read(layout, profileName: null, modId);
        if (!read.Success || read.Tweaks.Count == 0)
        {
            DiagnosticsRenderer.Render(read.Diagnostics);
            AnsiConsole.MarkupLine("[yellow]This mod declares no tweaks to reset.[/]");
            Pause();
            return;
        }

        const string All = "(all tweaks)";
        var choices = new List<string> { All };
        choices.AddRange(read.Tweaks.Select(t => t.Declaration.Id));
        var pick = AdvancedHelpers.Pick("Reset which:", choices);
        var tweakId = pick == All ? null : pick;

        var r = svc.Reset(layout, profileName: null, modId, tweakId);
        DiagnosticsRenderer.Render(r.Diagnostics);
        var scope = tweakId is null ? "all tweaks" : $"'{tweakId}'";
        if (r.Success)
        {
            AnsiConsole.MarkupLine(r.Mutated
                ? $"[green]Reset[/] {scope} for {Markup.Escape(modId)}"
                : $"[dim]No stored override to reset for {scope}.[/]");
        }
        Pause();
    }

    private static void Pause()
    {
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[dim]Press any key...[/]");
        Console.ReadKey(intercept: true);
    }
}
