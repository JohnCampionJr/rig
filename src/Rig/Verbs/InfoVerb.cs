using Spectre.Console;

namespace Rig;

/// <summary>
/// `rig info` — show what rig discovered/resolved for this repo, so the
/// convention-first inference is transparent ("why did it pick that?").
/// </summary>
internal static class InfoVerb
{
    public static int Execute(RigSession session)
    {
        var ctx = RootResolver.Resolve(session.Root);
        var projects = ProjectDiscovery.Discover(session.Root, session.Config.Solution);
        var runnable = projects.Where(p => p.IsRunnable).Select(p => p.Name).ToList();
        var testProject = TestVerb.ResolveTestProject(session, projects);
        var solution = ProjectDiscovery.FindSolution(session.Root, session.Config.Solution);
        var testDir = testProject is not null ? Path.GetDirectoryName(testProject) : null;
        var runsettings = CoverageVerb.FindRunsettings(testDir, session.Root);
        var collector = CoverageVerb.DetectCollector(testProject, session.Config.Coverage?.Collector);

        string Rel(string? p) => string.IsNullOrEmpty(p) ? "(none)" : Path.GetRelativePath(session.Root, p);

        var grid = new Grid().AddColumn().AddColumn();
        void Row(string label, string value) => grid.AddRow($"[grey]{label}[/]", Markup.Escape(value));

        Row("root", session.Root);
        Row("anchor", ctx.Anchor switch
        {
            AnchorKind.RigJson => ".rig.json",
            AnchorKind.Solution => "solution file",
            AnchorKind.Git => ".git",
            _ => "current directory",
        });
        Row("config", ctx.ConfigPath is null ? "(none — all defaults)" : Rel(ctx.ConfigPath));
        var globalConfig = RigSession.GlobalConfigPath();
        var hasGlobal = globalConfig is not null && File.Exists(globalConfig);
        Row("global config", hasGlobal ? globalConfig! : "(none)");
        Row("solution", solution is null ? "(none — scanning *.csproj)" : Rel(solution));
        Row("runnable", runnable.Count == 0 ? "(none)" : string.Join(", ", runnable));
        Row("default project", session.Config.DefaultProject ?? "(prompt when ambiguous)");
        Row("test project", Rel(testProject));
        Row("coverage runsettings", Rel(runsettings));
        Row("coverage collector", collector == CoverageVerb.CollectorMode.Mtp ? "MTP (--coverage)" : "VSTest (XPlat)");
        Row("env files", EnvSummary(session));
        Row("custom commands", session.Config.Commands is { Count: > 0 } c ? string.Join(", ", c.Keys) : "(none)");
        Row("alias overrides", session.Config.Aliases is { Count: > 0 } a
            ? string.Join(", ", a.Select(kv => $"{kv.Value}→{kv.Key}"))
            : "(built-in defaults)");

        AnsiConsole.Write(new Rule("[aqua]rig info[/]").LeftJustified());
        AnsiConsole.Write(grid);

        // Surface typo'd keys (System.Text.Json silently ignores them) in both files.
        if (ctx.ConfigPath is not null) WarnUnknownKeys(".rig.json", ctx.ConfigPath);
        if (hasGlobal) WarnUnknownKeys("~/.rig.json", globalConfig!);
        return 0;
    }

    private static void WarnUnknownKeys(string label, string path)
    {
        foreach (var (key, suggestion) in RigConfig.UnknownKeys(SafeRead(path)))
            Ui.Warn(suggestion is null
                ? $"unknown {label} key: \"{key}\""
                : $"unknown {label} key: \"{key}\" — did you mean \"{suggestion}\"?");
    }

    private static string SafeRead(string path)
    {
        try { return File.ReadAllText(path); } catch { return string.Empty; }
    }

    private static string EnvSummary(RigSession session)
    {
        if (!session.UseDotEnv) return "(disabled: --no-env)";
        var present = new[] { ".env", ".env.local" }
            .Where(n => File.Exists(Path.Combine(session.Root, n)))
            .ToList();
        return present.Count == 0
            ? "(none)"
            : $"{string.Join(", ", present)} — {session.FileEnv.Count} var(s)";
    }
}
