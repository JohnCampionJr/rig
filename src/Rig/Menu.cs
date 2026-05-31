using System.CommandLine;

using Spectre.Console;

namespace Rig;

/// <summary>The bare-`rig` interactive menu. Picking a verb re-enters the same
/// parser (single dispatch path), so the menu never duplicates verb logic.</summary>
internal static class Menu
{
    public static int Run(RootCommand root)
    {
        // Menu-driven invocations build their own arg lists — never inherit any
        // post-`--` tokens captured for the original bare-`rig` invocation.
        Cli.PassThrough = [];

        // No TTY (piped / CI) — a prompt would throw; show help instead.
        if (!AnsiConsole.Profile.Capabilities.Interactive)
            return root.Parse(["--help"]).Invoke();

        AnsiConsole.Write(new FigletText("rig").Color(Color.Aqua));

        var caps = ProbeCapabilities();
        var (runnable, defaultProject) = LoadRunnable();
        var verbs = root.Subcommands
            .Select(c => c.Name)
            .Where(n => n is not "completion" and not "init" and not "add") // shell/arg-required verbs aren't menu items
            .Append("watch") // synthetic: opens a sub-menu of watchable verbs
            .Append("quit")
            .ToList();

        // Loop: unavailable rows are greyed with a reason and re-prompt on pick.
        while (true)
        {
            var pick = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("What would you like to do?")
                    .PageSize(20)
                    .UseConverter(v => Label(v, caps))
                    .AddChoices(verbs));

            if (pick == "quit") return 0;

            if (pick == "watch")
            {
                var chosen = WatchSubmenu(caps);
                if (chosen is null) continue; // back
                return root.Parse([chosen, "--watch"]).Invoke();
            }

            var reason = caps?.Unavailable(pick);
            if (reason is not null)
            {
                Ui.Warn($"{pick} is unavailable: {reason}.");
                continue;
            }

            // run/publish target a project — surface the choices (default marked)
            // instead of silently firing the default.
            if (pick is "run" or "publish")
            {
                var project = ProjectSubmenu($"{(pick == "run" ? "Run" : "Publish")} which project?", runnable, defaultProject);
                if (project is null) continue; // back / none
                return root.Parse([pick, project]).Invoke();
            }

            return root.Parse(ArgsFor(pick)).Invoke();
        }
    }

    // Sub-menu of runnable projects (shared by run/publish). The configured default
    // is marked; a single project skips the menu. Returns the project, or null
    // (go back / none available).
    private static string? ProjectSubmenu(string title, IReadOnlyList<string> projects, string? defaultProject)
    {
        if (projects.Count == 0) { Ui.Warn("No runnable projects found."); return null; }
        if (projects.Count == 1) return projects[0];

        const string back = "← back";
        bool IsDefault(string p) => defaultProject is not null &&
            (string.Equals(p, defaultProject, StringComparison.OrdinalIgnoreCase) ||
             string.Equals(ShortName(p), defaultProject, StringComparison.OrdinalIgnoreCase));

        var pick = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title(title)
                .PageSize(20)
                .UseConverter(p => p == back ? "[grey]← back[/]" : IsDefault(p) ? $"{p} [grey](default)[/]" : p)
                .AddChoices([.. projects, back]));

        return pick == back ? null : pick;
    }

    private static string ShortName(string name) =>
        name.Contains('.') ? name[(name.LastIndexOf('.') + 1)..] : name;

    private static (List<string> Runnable, string? Default) LoadRunnable()
    {
        try
        {
            var s = RigSession.Load(Directory.GetCurrentDirectory());
            var runnable = ProjectDiscovery.Discover(s.Root, s.Config.Solution, s.Config.Exclude)
                .Where(p => p.IsRunnable).Select(p => p.Name).ToList();
            return (runnable, s.Config.DefaultProject);
        }
        catch { return ([], null); }
    }

    // Sub-menu for `dotnet watch`: the verbs that make sense to watch. Unavailable
    // ones are greyed with a reason (consistent with the main menu), not dropped.
    // Returns the chosen verb, or null to go back.
    private static string? WatchSubmenu(Capabilities? caps)
    {
        string[] choices = ["run", "test", "build", "back"];
        while (true)
        {
            var pick = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("Watch which? [grey](dotnet watch — re-runs on change)[/]")
                    .UseConverter(v => v == "back" ? "[grey]back[/]" : Label(v, caps))
                    .AddChoices(choices));

            if (pick == "back") return null;

            var reason = caps?.Unavailable(pick);
            if (reason is not null)
            {
                Ui.Warn($"{pick} is unavailable: {reason}.");
                continue;
            }
            return pick;
        }
    }

    // A picked verb re-enters the parser; a few verbs gather an extra option here
    // (the menu is interactive, so we can ask) rather than running with defaults.
    private static string[] ArgsFor(string verb)
    {
        if (verb == "coverage")
            return AnsiConsole.Confirm("Open the coverage report when done?", defaultValue: true)
                ? ["coverage", "--open"]
                : ["coverage"];

        if (verb == "rebuild")
            return AnsiConsole.Confirm("Dry run (preview what would be deleted)?", defaultValue: false)
                ? ["rebuild", "--dry-run"]
                : ["rebuild"];

        return [verb];
    }

    private static string Label(string verb, Capabilities? caps)
    {
        if (verb == "quit") return "[grey]quit[/]";
        var reason = caps?.Unavailable(verb);
        return reason is null ? verb : $"[grey]{verb} ({reason})[/]";
    }

    private static Capabilities? ProbeCapabilities()
    {
        try { return Capabilities.Probe(RigSession.Load(Directory.GetCurrentDirectory())); }
        catch { return null; }
    }
}
