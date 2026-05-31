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

        var globalConfig = RigSession.GlobalConfigPath();
        var hasGlobal = globalConfig is not null && File.Exists(globalConfig);
        // Re-load each layer on its own to attribute settings to local vs global.
        var repoCfg = RigConfig.Load(ctx.ConfigPath);
        var globalCfg = hasGlobal ? RigConfig.Load(globalConfig) : new RigConfig();

        string Rel(string? p) => string.IsNullOrEmpty(p) ? "(none)" : Path.GetRelativePath(session.Root, p);
        static bool Set(string? s) => !string.IsNullOrWhiteSpace(s);
        static bool Any<T>(Dictionary<string, T>? d) => d is { Count: > 0 };

        // A " (local/global)" provenance marker — only when a global config exists
        // (otherwise every config value is trivially local and the tag is noise).
        string Origin(bool local, bool global) => !hasGlobal ? "" : (local, global) switch
        {
            (true, true) => "  [grey](local+global)[/]",
            (true, false) => "  [grey](local)[/]",
            (false, true) => "  [grey](global)[/]",
            _ => "",
        };

        var grid = new Grid().AddColumn().AddColumn();
        void Row(string label, string value, string origin = "") =>
            grid.AddRow($"[grey]{label}[/]", Markup.Escape(value) + origin);

        Row("root", session.Root);
        Row("anchor", ctx.Anchor switch
        {
            AnchorKind.RigJson => ".rig.json",
            AnchorKind.Solution => "solution file",
            AnchorKind.Git => ".git",
            _ => "current directory",
        });
        Row("config", ctx.ConfigPath is null ? "(none — all defaults)" : Rel(ctx.ConfigPath));
        Row("global config", hasGlobal ? globalConfig! : "(none)");
        Row("solution", solution is null ? "(none — scanning *.csproj)" : Rel(solution),
            Origin(Set(repoCfg.Solution), Set(globalCfg.Solution)));
        Row("runnable", runnable.Count == 0 ? "(none)" : string.Join(", ", runnable));
        Row("default project", session.Config.DefaultProject ?? "(prompt when ambiguous)",
            Origin(Set(repoCfg.DefaultProject), Set(globalCfg.DefaultProject)));
        Row("test project", Rel(testProject));
        Row("coverage runsettings", Rel(runsettings));
        Row("coverage collector", collector == CoverageVerb.CollectorMode.Mtp ? "MTP (--coverage)" : "VSTest (XPlat)");
        Row("coverage license", Set(session.Config.Coverage?.License) ? "set (Pro)" : "(none — free engine)",
            Origin(Set(repoCfg.Coverage?.License), Set(globalCfg.Coverage?.License)));
        Row("env files", EnvSummary(session));
        Row("custom commands", session.Config.Commands is { Count: > 0 } c ? string.Join(", ", c.Keys) : "(none)",
            Origin(Any(repoCfg.Commands), Any(globalCfg.Commands)));
        Row("alias overrides", session.Config.Aliases is { Count: > 0 } a
            ? string.Join(", ", a.Select(kv => $"{kv.Value}→{kv.Key}"))
            : "(built-in defaults)",
            Origin(Any(repoCfg.Aliases), Any(globalCfg.Aliases)));

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
