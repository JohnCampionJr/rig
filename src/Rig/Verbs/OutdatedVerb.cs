namespace Rig;

/// <summary>
/// `rig outdated` — `dotnet list package --outdated` on the discovered solution,
/// with `--vulnerable` / `--deprecated` as alternate lenses. `dotnet list package`
/// reads the restore assets, so rig restores first when any project hasn't been.
/// Reporting only — it changes nothing. Arg building (pure) is <see cref="BuildArgs"/>.
/// </summary>
internal static class OutdatedVerb
{
    /// <summary>The `dotnet list [solution] package …` argument list. The three
    /// report lenses are mutually exclusive (vulnerable &gt; deprecated &gt; outdated);
    /// <c>--prerelease</c> only applies to the default outdated lens.</summary>
    public static List<string> BuildArgs(string? solution, bool vulnerable, bool deprecated,
        bool transitive, bool prerelease, string[] forwarded)
    {
        var args = new List<string> { "list" };
        if (solution is not null) args.Add(solution);
        args.Add("package");

        if (vulnerable) args.Add("--vulnerable");
        else if (deprecated) args.Add("--deprecated");
        else args.Add("--outdated");

        if (transitive) args.Add("--include-transitive");
        if (prerelease && !vulnerable && !deprecated) args.Add("--include-prerelease");

        args.AddRange(forwarded);
        return args;
    }

    public static int Execute(RigSession session, bool vulnerable, bool deprecated,
        bool transitive, bool prerelease, string[] forwarded)
    {
        ProjectDiscovery.WarnMultipleSolutions(session.Root, session.Config.Solution);
        var solution = ProjectDiscovery.FindSolution(session.Root, session.Config.Solution);

        EnsureRestored(session, solution);

        var args = BuildArgs(solution, vulnerable, deprecated, transitive, prerelease, forwarded);
        Ui.Command("dotnet", args);
        return Exec.Run("dotnet", args, session.Root, session.BuildEnv());
    }

    // `dotnet list package` errors if a project isn't restored — so restore first
    // when any project lacks its assets file (skipped when everything's restored).
    private static void EnsureRestored(RigSession session, string? solution)
    {
        var projects = ProjectDiscovery.Discover(session.Root, session.Config.Solution, session.Config.Exclude);
        var needsRestore = projects.Count == 0 || projects.Any(p =>
        {
            var dir = Path.GetDirectoryName(p.FullPath);
            return dir is null || !File.Exists(Path.Combine(dir, "obj", "project.assets.json"));
        });
        if (!needsRestore) return;

        var restore = new List<string> { "restore" };
        if (solution is not null) restore.Add(solution);
        Ui.Command("dotnet", restore);
        Exec.Run("dotnet", restore, session.Root, session.BuildEnv());
    }
}
