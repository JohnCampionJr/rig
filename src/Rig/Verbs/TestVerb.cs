using Spectre.Console;

namespace Rig;

/// <summary>
/// `rig test [name | ~expr | =expr | --filter expr] [--log]` — runs `dotnet
/// test` on the discovered test project. A bare name resolves against the
/// enumerated test classes (multi-match → picker); the `~ = !~ !=` shorthands
/// map to `FullyQualifiedName{op}{expr}`. `--log` applies the `test.envPresets`
/// "log" bundle. Filter/shorthand parsing is pure (<see cref="ShorthandFilter"/>).
/// </summary>
internal static class TestVerb
{
    private const StringComparison OIC = StringComparison.OrdinalIgnoreCase;

    /// <summary>Maps a leading MSTest filter operator to a full filter, or null
    /// when the token isn't shorthand.</summary>
    public static string? ShorthandFilter(string token)
    {
        if (token.Length == 0) return null;
        if (token[0] is '~' or '=') return "FullyQualifiedName" + token;
        if (token.Length > 1 && token[0] == '!' && token[1] is '~' or '=') return "FullyQualifiedName" + token;
        return null;
    }

    public static string? ResolveTestProject(RigSession session, IReadOnlyList<ProjectInfo> projects)
    {
        var configured = session.Config.Test?.Project;
        if (!string.IsNullOrEmpty(configured))
            return Path.IsPathRooted(configured) ? configured : Path.Combine(session.Root, configured);

        return projects.FirstOrDefault(p => p.IsTest)?.FullPath;
    }

    public static int Execute(RigSession session, string? nameOrFilter, bool log, string? explicitFilter, string[] forwarded, bool watch = false, string? framework = null)
    {
        ProjectDiscovery.WarnMultipleSolutions(session.Root, session.Config.Solution);
        var projects = ProjectDiscovery.Discover(session.Root, session.Config.Solution);
        var testProject = ResolveTestProject(session, projects);
        if (testProject is null)
        {
            Ui.Error("No test project found. Add one (IsTestProject / Microsoft.NET.Test.Sdk / *Tests), or set test.project in .rig.json.");
            return 1;
        }

        var filter = explicitFilter ?? FilterForName(nameOrFilter, testProject);
        var args = BuildTestArgs(testProject, filter, framework, forwarded, watch);

        IReadOnlyDictionary<string, string>? commandEnv = null;
        if (log)
        {
            if (session.Config.Test?.EnvPresets is { } presets && presets.TryGetValue("log", out var preset))
                commandEnv = preset;
            else
                Ui.Warn("--log: no test.envPresets.log defined in .rig.json; nothing applied.");
        }

        Ui.Command("dotnet", args);
        return Exec.Run("dotnet", args, session.Root, session.BuildEnv(commandEnv));
    }

    /// <summary>The `dotnet [watch] test …` argument list (pure, so it's testable).
    /// Filter resolution stays in <see cref="Execute"/> (it can prompt); the caller
    /// passes the already-resolved <paramref name="filter"/>.</summary>
    public static List<string> BuildTestArgs(string testProject, string? filter, string? framework, string[] forwarded, bool watch)
    {
        var args = new List<string> { "test", "--project", testProject };
        if (!string.IsNullOrEmpty(framework)) { args.Add("--framework"); args.Add(framework); }
        if (filter is not null) { args.Add("--filter"); args.Add(filter); }
        args.AddRange(forwarded);
        if (watch) args.Insert(0, "watch"); // dotnet watch test …
        return args;
    }

    private static string? FilterForName(string? nameOrFilter, string? testProject)
    {
        if (string.IsNullOrEmpty(nameOrFilter)) return null;

        var shorthand = ShorthandFilter(nameOrFilter);
        if (shorthand is not null) return shorthand;

        var classes = testProject is not null
            ? TestEnumeration.Enumerate(FindTestAssembly(testProject))
            : [];

        var matches = classes
            .Where(c => c.Contains(nameOrFilter, OIC) || ShortName(c).Contains(nameOrFilter, OIC))
            .ToList();

        if (matches.Count == 1)
        {
            Ui.Info($"Matched test class: {matches[0]}");
            return $"FullyQualifiedName~{ShortName(matches[0])}";
        }
        if (matches.Count > 1)
        {
            var picks = AnsiConsole.Prompt(
                new MultiSelectionPrompt<string>()
                    .Title($"'{nameOrFilter}' matches {matches.Count} test classes")
                    .PageSize(20)
                    .InstructionsText("[grey](space to toggle, enter to accept)[/]")
                    .AddChoices(matches));
            return string.Join("|", picks.Select(p => $"FullyQualifiedName~{ShortName(p)}"));
        }

        // No discovered match — let the test platform resolve it (could be a method).
        return $"FullyQualifiedName~{nameOrFilter}";
    }

    /// <summary>Locate the built test assembly for a csproj: prefer Debug, then
    /// Release, globbing any TFM. Builds the project once if nothing is found.</summary>
    private static string FindTestAssembly(string csprojPath)
    {
        var dir = Path.GetDirectoryName(csprojPath)!;
        var name = Path.GetFileNameWithoutExtension(csprojPath);

        var found = Probe(dir, name);
        if (found is not null) return found;

        Ui.Info("Building test project to enumerate test classes…");
        Exec.Run("dotnet", ["build", csprojPath, "-v", "quiet", "-nologo"], dir);
        return Probe(dir, name) ?? string.Empty;
    }

    /// <summary>Locate an already-built test assembly without triggering a build
    /// (used by completion, which must stay fast). Returns null if not built.</summary>
    internal static string? TryBuiltAssembly(string csprojPath)
    {
        var dir = Path.GetDirectoryName(csprojPath)!;
        var name = Path.GetFileNameWithoutExtension(csprojPath);
        return Probe(dir, name);
    }

    private static string? Probe(string projectDir, string name)
    {
        var bin = Path.Combine(projectDir, "bin");
        if (!Directory.Exists(bin)) return null;

        foreach (var config in new[] { "Debug", "Release" })
        {
            var configDir = Path.Combine(bin, config);
            if (!Directory.Exists(configDir)) continue;
            var hit = Directory.EnumerateFiles(configDir, name + ".dll", SearchOption.AllDirectories).FirstOrDefault();
            if (hit is not null) return hit;
        }
        return null;
    }

    private static string ShortName(string fullName) =>
        fullName.Contains('.') ? fullName[(fullName.LastIndexOf('.') + 1)..] : fullName;
}
