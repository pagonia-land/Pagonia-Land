using Spectre.Console;

namespace PagoniaLand.Manager.Cli.Interactive;

internal static class InteractiveShell
{
    public static int Run()
    {
        RenderBanner();
        var session = new SessionState();

        try
        {
            EnsureStoreInitialised(session);
        }
        catch (Exception ex) when (IsCancellation(ex))
        {
            // Ctrl+C at the first-run init prompt — exit cleanly, same as the main menu.
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[dim]Cancelled. Bye.[/]");
            return ManagerExitCodes.Success;
        }

        while (true)
        {
            string choice;
            try
            {
                choice = MainMenu.Prompt();
            }
            catch (Exception promptEx) when (IsCancellation(promptEx))
            {
                // Ctrl+C at the main prompt — exit cleanly.
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine("[dim]Cancelled. Bye.[/]");
                return ManagerExitCodes.Success;
            }

            try
            {
                switch (choice)
                {
                    case MainMenu.Quit:
                        AnsiConsole.MarkupLine("[dim]Bye.[/]");
                        return ManagerExitCodes.Success;

                    // ---- Mods cluster ----
                    case MainMenu.InstallMod:
                        InstallModWizard.Run(session);
                        WaitForKey();
                        break;

                    case MainMenu.BrowseCatalogs:
                        BrowseCatalogsWizard.Run(session);
                        WaitForKey();
                        break;

                    case MainMenu.ManageActiveProfile:
                        ManageActiveProfileWizard.Run(session);
                        break;

                    // ---- Game cluster ----
                    case MainMenu.PlanDeploy:
                        PlanDeployWizard.Run(session);
                        WaitForKey();
                        break;

                    case MainMenu.Rollback:
                        RollbackWizard.Run(session);
                        WaitForKey();
                        break;

                    case MainMenu.DeployHistory:
                        DeployHistoryWizard.Run(session);
                        WaitForKey();
                        break;

                    // ---- Status & Settings cluster ----
                    case MainMenu.Status:
                        StatusDashboard.Render(session);
                        WaitForKey();
                        break;

                    case MainMenu.Settings:
                        SettingsWizard.Run(session);
                        break;

                    case MainMenu.CleanBackups:
                        CleanBackupsWizard.Run(session);
                        break;

                    // ---- Bottom row ----
                    case MainMenu.Advanced:
                        AdvancedMenu.Run(session);
                        break;

                    default:
                        AnsiConsole.MarkupLine($"[red]Unknown choice: {choice}[/]");
                        break;
                }
            }
            catch (Exception wizardEx) when (IsCancellation(wizardEx))
            {
                // Ctrl+C inside a wizard prompt — return to the main menu, don't exit.
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine("[dim]Cancelled. Returning to the main menu.[/]");
            }
            catch (Exception wizardEx)
            {
                // Anything else inside a wizard (state.yaml mid-write, malformed
                // YAML, transient IO failure, ...) — render the failure as a
                // diagnostic and keep the shell alive. Without this catch, a
                // single read-error here would kill the whole interactive
                // session and force the user to relaunch.
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine($"[red]Unexpected error in '{Markup.Escape(choice)}': {Markup.Escape(wizardEx.Message)}[/]");
                AnsiConsole.MarkupLine("[dim]Returning to the main menu.[/]");
                WaitForKey();
            }
        }
    }

    // First-run guard: when the resolved store has no state.yaml yet, nothing
    // useful works (every store-touching wizard would bounce off a
    // "store not initialised" message). Surface that up front and offer to
    // initialise it right here, so a first-time user isn't left guessing.
    private static void EnsureStoreInitialised(SessionState session)
    {
        var resolution = StoreRootResolver.Resolve(session.StoreOverride);
        var layout = new StoreLayout(resolution.Root);
        if (new StoreStateReader().Exists(layout))
        {
            return;
        }

        var sourceLabel = resolution.Source switch
        {
            StoreRootResolver.ResolutionSource.Flag => "--store flag",
            StoreRootResolver.ResolutionSource.EnvironmentVariable => $"{StoreRootResolver.EnvironmentVariableName} env var",
            StoreRootResolver.ResolutionSource.PlatformDefault => "platform default",
            _ => "resolved",
        };

        AnsiConsole.Write(new Rule("[bold yellow]First-time setup[/]").LeftJustified());
        AnsiConsole.MarkupLine("[yellow]No manager store found here yet.[/]");
        AnsiConsole.MarkupLine("The store holds your installed mods, profiles, and deploy history. It needs to be initialised [bold]once[/] before you can install mods or manage profiles.");
        AnsiConsole.MarkupLine($"[dim]Location ({sourceLabel}):[/] [aqua]{Markup.Escape(layout.Root)}[/]");
        AnsiConsole.WriteLine();

        if (!AdvancedHelpers.Confirm("Initialise the store here now?", defaultValue: true))
        {
            AnsiConsole.MarkupLine("[dim]Skipped. Most actions stay unavailable until you initialise it via[/] [aqua]Advanced → Store → init[/][dim], or run[/] [aqua]pagonia-manager store init[/] [dim]on the CLI.[/]");
            AnsiConsole.WriteLine();
            return;
        }

        try
        {
            var result = new StoreInitializer().Initialize(layout, seedDefaultCatalog: true);
            AnsiConsole.MarkupLine($"[green]Store initialised[/] at [aqua]{Markup.Escape(layout.Root)}[/].");
            if (result.CreatedDefaultProfile)
            {
                AnsiConsole.MarkupLine("[dim]Created a 'default' profile and set it active. You're ready to install a mod.[/]");
            }
            if (result.SeededDefaultCatalog)
            {
                AnsiConsole.MarkupLine("[dim]Subscribed you to the official catalog — browse it under [/][aqua]Browse community catalogs[/][dim], or drop it any time via [/][aqua]Settings → Catalog subscriptions[/][dim].[/]");
            }
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Could not initialise the store: {Markup.Escape(ex.Message)}[/]");
            AnsiConsole.MarkupLine("[dim]You can retry via[/] [aqua]Advanced → Store → init[/][dim].[/]");
        }
        AnsiConsole.WriteLine();
    }

    private static bool IsCancellation(Exception ex)
        => ex is OperationCanceledException
        // Spectre throws an internal type containing "CancellationRequested" on Ctrl+C
        // during a prompt; the public API doesn't expose it. Fall back to a name match.
        || ex.GetType().Name.Contains("CancellationRequested");

    private static void WaitForKey()
    {
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[dim]Press any key to return to the menu...[/]");
        Console.ReadKey(intercept: true);
        AnsiConsole.WriteLine();
    }

    private static void RenderBanner()
    {
        AnsiConsole.Write(
            new FigletText("Pagonia Land")
                .LeftJustified()
                .Color(Color.Aqua));
        AnsiConsole.MarkupLine($"[bold]Manager[/] [aqua]{ManagerInfo.Version}[/]");
        AnsiConsole.MarkupLine("[dim]Interactive mode. Use arrow keys to navigate, Enter to select, Ctrl+C to quit at any time.[/]");
        AnsiConsole.MarkupLine("[dim]Pass --help or any CLI arg to skip this and use scripted mode instead.[/]");
        AnsiConsole.WriteLine();
    }
}
