using Spectre.Console;

namespace Rig;

/// <summary>
/// `rig run [project] [-- forwarded...]` — runs a runnable project via
/// `dotnet run`. Project selection (pure, testable) lives in <see cref="Resolve"/>;
/// <see cref="Execute"/> is the orchestration (prompt on ambiguity, print, spawn).
/// </summary>
internal static class RunVerb
{
    private const StringComparison OIC = StringComparison.OrdinalIgnoreCase;

    public sealed record Resolution(ProjectInfo? Selected, IReadOnlyList<ProjectInfo> Ambiguous, string? Error);

    public static Resolution Resolve(IReadOnlyList<ProjectInfo> projects, string? query, string? defaultProject)
    {
        var runnable = projects.Where(p => p.IsRunnable).ToList();
        if (runnable.Count == 0) return new Resolution(null, [], "No runnable projects found.");

        if (query is null)
        {
            if (defaultProject is not null)
            {
                var preferred = runnable.FirstOrDefault(p => NameMatches(p, defaultProject));
                if (preferred is not null) return new Resolution(preferred, [], null);
            }
            return runnable.Count == 1
                ? new Resolution(runnable[0], [], null)
                : new Resolution(null, runnable, null); // ambiguous → caller decides
        }

        var matches = FindMatches(runnable, query);
        return matches.Count switch
        {
            0 => new Resolution(null, [], $"No project matches '{query}'."),
            1 => new Resolution(matches[0], [], null),
            _ => new Resolution(null, matches, null),
        };
    }

    public static int Execute(RigSession session, string? query, string[] forwarded,
        bool remember = false, bool watch = false, string? configuration = null,
        string? framework = null, string? launchProfile = null)
    {
        ProjectDiscovery.WarnMultipleSolutions(session.Root, session.Config.Solution);
        var projects = ProjectDiscovery.Discover(session.Root, session.Config.Solution);
        var resolution = Resolve(projects, query, session.Config.DefaultProject);

        if (resolution.Error is not null)
        {
            Ui.Error(resolution.Error);
            foreach (var p in projects.Where(p => p.IsRunnable)) Ui.Info($"  • {p.Name}");
            return 1;
        }

        var wasAmbiguous = resolution.Selected is null;
        var project = resolution.Selected ?? AnsiConsole.Prompt(
            new SelectionPrompt<ProjectInfo>()
                .Title("Which project?")
                .UseConverter(p => p.Name)
                .AddChoices(resolution.Ambiguous));

        // Let rig persist the default so the user never hand-edits .rig.json:
        // explicit via --remember, or an offer after an ambiguous pick.
        if (remember || (wasAmbiguous && AnsiConsole.Profile.Capabilities.Interactive
                && AnsiConsole.Confirm($"Remember [green]{project.Name}[/] as the default project?", defaultValue: false)))
        {
            var path = ConfigWriter.SetString(session.Root, "defaultProject", project.Name);
            Ui.Success($"Set defaultProject = {project.Name} in {Path.GetFileName(path)}");
        }

        var args = BuildRunArgs(project.FullPath, configuration, framework, launchProfile, forwarded, watch);
        Ui.Command("dotnet", args);
        return Exec.Run("dotnet", args, session.Root, session.BuildEnv());
    }

    /// <summary>The `dotnet [watch] run …` argument list (pure, so it's testable).
    /// Framework / launch-profile slot in before the `--` forwarding boundary.</summary>
    public static List<string> BuildRunArgs(string projectFullPath, string? configuration,
        string? framework, string? launchProfile, string[] forwarded, bool watch)
    {
        var args = new List<string> { "run", "--project", projectFullPath };
        if (!string.IsNullOrEmpty(configuration)) { args.Add("-c"); args.Add(configuration); }
        if (!string.IsNullOrEmpty(framework)) { args.Add("--framework"); args.Add(framework); }
        if (!string.IsNullOrEmpty(launchProfile)) { args.Add("--launch-profile"); args.Add(launchProfile); }
        if (forwarded.Length > 0)
        {
            args.Add("--");
            args.AddRange(forwarded);
        }
        if (watch) args.Insert(0, "watch"); // dotnet watch run …
        return args;
    }

    private static bool NameMatches(ProjectInfo p, string q) =>
        string.Equals(p.Name, q, OIC) || string.Equals(p.ShortName, q, OIC);

    private static List<ProjectInfo> FindMatches(IReadOnlyList<ProjectInfo> projects, string query)
    {
        var q = query.Trim();
        var exact = projects.Where(p => NameMatches(p, q)).ToList();
        return exact.Count > 0 ? exact : projects.Where(p => p.Name.Contains(q, OIC)).ToList();
    }
}
