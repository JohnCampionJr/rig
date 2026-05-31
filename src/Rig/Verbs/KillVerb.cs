namespace Rig;

/// <summary>
/// `rig kill` — terminate processes matching `kill.match` patterns (or, when
/// unconfigured, the default run-project's name). Pattern-based so it also
/// catches strays (a hung test host, an IDE-launched instance). "No match" is
/// success. Pattern resolution (pure) is <see cref="ResolvePatterns"/>.
/// </summary>
internal static class KillVerb
{
    private const StringComparison OIC = StringComparison.OrdinalIgnoreCase;

    public static IReadOnlyList<string> ResolvePatterns(
        RigConfig config, IReadOnlyList<ProjectInfo> projects)
    {
        if (config.Kill?.Match is { Count: > 0 } match) return match;

        ProjectInfo? project = null;
        if (config.DefaultProject is { } dp)
        {
            project = projects.FirstOrDefault(p =>
                string.Equals(p.Name, dp, OIC) || string.Equals(p.ShortName, dp, OIC));
            if (project is null) return [dp]; // honor the config string if unmatched
        }
        project ??= projects.FirstOrDefault(p => p.IsRunnable);
        if (project is null) return [];

        // `pkill -f` matches the full command line, so the *project name* (present in
        // the `dotnet run --project` cmdline and the apphost path) is targeted — and
        // notably narrower than a short AssemblyName. `taskkill /IM` needs the *image*
        // name, which is the AssemblyName.
        return [OperatingSystem.IsWindows() ? project.OutputName : project.Name];
    }

    public static int Execute(RigSession session, IReadOnlyList<ProjectInfo> projects)
    {
        var patterns = ResolvePatterns(session.Config, projects);
        if (patterns.Count == 0)
        {
            Ui.Warn("Nothing to kill: no kill.match patterns and no default project to infer from.");
            return 0;
        }

        var worst = 0;
        foreach (var pattern in patterns)
        {
            int rc;
            bool killed;
            if (OperatingSystem.IsWindows())
            {
                var image = pattern.EndsWith(".exe", OIC) ? pattern : pattern + ".exe";
                Ui.Command("taskkill", ["/F", "/IM", image]);
                rc = Exec.Run("taskkill", ["/F", "/IM", image], session.Root, suppressMissing: true);
                killed = rc == 0;
                if (rc is not 0 and not 128) worst = rc; // 128 = no such process
            }
            else
            {
                Ui.Command("pkill", ["-f", pattern]);
                rc = Exec.Run("pkill", ["-f", pattern], session.Root, suppressMissing: true);
                killed = rc == 0;
                if (rc is not 0 and not 1) worst = rc; // 1 = no process matched
            }

            if (killed) Ui.Success($"killed process(es) matching '{pattern}'");
            else Ui.Info($"no process matched '{pattern}'");
        }
        return worst;
    }
}
