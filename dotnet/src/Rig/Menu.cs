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
        var (runnable, defaultProject, customCommands) = LoadRunnable();

        // Curated top level: the everyday loop plus grouped sub-menus (▸) for the
        // long tail, so the menu stays short instead of listing every verb.
        var present = root.Subcommands.Select(c => c.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        string[] primary = ["run", "build", "test", "coverage", "format", "kill", "publish"];
        var groups = new List<string> { "watch" };
        if (customCommands.Count > 0) groups.Add("commands");      // surface custom config commands
        groups.AddRange(["maintenance", "config", "quit"]);
        var top = primary.Where(present.Contains).Concat(groups).ToList();

        while (true)
        {
            var pick = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("What would you like to do?")
                    .PageSize(20)
                    .UseConverter(v => Label(v, caps))
                    .AddChoices(top));

            if (pick == "quit") return 0;

            if (pick == "watch")
            {
                var chosen = WatchSubmenu(caps);
                if (chosen is not null) return root.Parse([chosen, "--watch"]).Invoke();
                continue; // back
            }
            if (pick == "commands")
            {
                if (Category(root, "commands", [.. customCommands], caps, runnable, defaultProject) is { } code) return code;
                continue;
            }
            if (pick == "maintenance")
            {
                if (Category(root, "maintenance", ["restore", "outdated", "clean", "rebuild"], caps, runnable, defaultProject) is { } code) return code;
                continue;
            }
            if (pick == "config")
            {
                if (Category(root, "config", ["info", "doctor", "setup", "default", "init", "update"], caps, runnable, defaultProject) is { } code) return code;
                continue;
            }

            if (Dispatch(root, pick, caps, runnable, defaultProject) is { } rc) return rc;
        }
    }

    // Run a chosen verb: gate on availability, surface the project/kill picker for
    // verbs that target a project, else re-enter the parser. Returns the exit code
    // when something ran, or null to re-prompt (unavailable, or backed out).
    private static int? Dispatch(RootCommand root, string verb, Capabilities? caps,
        IReadOnlyList<string> runnable, string? defaultProject)
    {
        var reason = caps?.Unavailable(verb);
        if (reason is not null) { Ui.Warn($"{verb} is unavailable: {reason}."); return null; }

        if (verb is "run" or "publish")
        {
            var project = ProjectSubmenu($"{(verb == "run" ? "Run" : "Publish")} which project?", runnable, defaultProject);
            return project is null ? null : root.Parse([verb, project]).Invoke();
        }
        if (verb == "kill")
        {
            var args = KillSubmenu(runnable, defaultProject);
            return args is null ? null : root.Parse(args).Invoke();
        }
        return root.Parse(ArgsFor(verb)).Invoke();
    }

    // A grouped sub-menu (Maintenance ▸, Config ▸): pick a verb and dispatch it, or
    // go back. Returns the exit code when a verb ran; null on back.
    private static int? Category(RootCommand root, string title, string[] verbs, Capabilities? caps,
        IReadOnlyList<string> runnable, string? defaultProject)
    {
        var present = root.Subcommands.Select(c => c.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var choices = verbs.Where(present.Contains).Append("back").ToList();
        while (true)
        {
            var pick = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title($"{title} [grey]▸[/]")
                    .PageSize(20)
                    .UseConverter(v => v == "back" ? "[grey]← back[/]" : Label(v, caps))
                    .AddChoices(choices));

            if (pick == "back") return null;
            if (Dispatch(root, pick, caps, runnable, defaultProject) is { } code) return code;
            // unavailable / backed out of a picker → re-prompt this category
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

    // Kill's sub-menu: every runnable project (bare `rig kill`) or one specific
    // project. With 0–1 runnables there's nothing to choose — delegate to bare
    // `kill` (which uses kill.match config / the sole runnable / warns). Returns the
    // parse args, or null to go back.
    private static string[]? KillSubmenu(IReadOnlyList<string> projects, string? defaultProject)
    {
        if (projects.Count <= 1) return ["kill"];

        const string all = "all runnable projects";
        const string back = "← back";
        bool IsDefault(string p) => defaultProject is not null &&
            (string.Equals(p, defaultProject, StringComparison.OrdinalIgnoreCase) ||
             string.Equals(ShortName(p), defaultProject, StringComparison.OrdinalIgnoreCase));

        var pick = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("Kill which? [grey](terminates matching processes)[/]")
                .PageSize(20)
                .UseConverter(p => p == back ? "[grey]← back[/]"
                    : p == all ? all
                    : IsDefault(p) ? $"{p} [grey](default)[/]" : p)
                .AddChoices([all, .. projects, back]));

        return pick == back ? null : pick == all ? ["kill"] : ["kill", pick];
    }

    private static string ShortName(string name) =>
        name.Contains('.') ? name[(name.LastIndexOf('.') + 1)..] : name;

    private static (List<string> Runnable, string? Default, List<string> Commands) LoadRunnable()
    {
        try
        {
            var s = RigSession.Load(Directory.GetCurrentDirectory());
            var runnable = ProjectDiscovery.Discover(s.Root, s.Config.Solution, s.Config.Exclude)
                .Where(p => p.IsRunnable).Select(p => p.Name).ToList();
            var commands = s.Config.Commands?.Keys.ToList() ?? [];
            return (runnable, s.Config.DefaultProject, commands);
        }
        catch { return ([], null, []); }
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
                    .UseConverter(v => v == "back" ? "[grey]← back[/]" : Label(v, caps))
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

    // Per-row hints (parity with the Node menu, which hints every row) so the two
    // tools feel like the same product. Static, action-describing — not the live
    // resolved command (kept simple); unavailable rows show their reason instead.
    private static readonly Dictionary<string, string> Hints = new(StringComparer.OrdinalIgnoreCase)
    {
        ["run"] = "run a project",
        ["build"] = "build the solution",
        ["test"] = "run tests",
        ["coverage"] = "tests + coverage",
        ["format"] = "dotnet format",
        ["kill"] = "stop app processes",
        ["publish"] = "publish a project",
        ["restore"] = "dotnet restore",
        ["outdated"] = "newer versions",
        ["clean"] = "remove build outputs",
        ["rebuild"] = "clean + build",
        ["info"] = "what rig discovered",
        ["doctor"] = "check the environment",
        ["setup"] = "set preferences",
        ["default"] = "set the default project",
        ["init"] = "scaffold .rig.json",
        ["update"] = "update rig itself",
    };

    private static string Label(string verb, Capabilities? caps)
    {
        if (verb == "quit") return "[grey]quit[/]";
        if (verb is "watch" or "commands" or "maintenance" or "config") // grouped sub-menus
            return $"{verb} [grey]▸[/]";
        var reason = caps?.Unavailable(verb);
        if (reason is not null) return $"[grey]{verb} ({reason})[/]";
        return Hints.TryGetValue(verb, out var hint) ? $"{verb.PadRight(9)} [grey]{hint}[/]" : verb;
    }

    private static Capabilities? ProbeCapabilities()
    {
        try { return Capabilities.Probe(RigSession.Load(Directory.GetCurrentDirectory())); }
        catch { return null; }
    }
}
