using System.Text;

namespace Rig;

/// <summary>
/// `rig kill [project]` — terminate processes matching `kill.match` patterns; or,
/// when a project is named, that project; or, with neither, *every* runnable
/// project (the "stop everything I started" sweep). Pattern-based so it also
/// catches strays (a hung test host, an IDE-launched instance, a `dotnet watch`
/// that keeps respawning the app). Matching is against the *full command line*
/// on both platforms — <c>pkill -f</c> on Unix, CIM <c>Win32_Process.CommandLine</c>
/// on Windows — so the `dotnet run`/`dotnet watch` driver (image <c>dotnet.exe</c>)
/// is caught alongside the apphost, not just the apphost. "No match" is success.
/// Pattern resolution (pure) is <see cref="ResolvePatterns"/>.
/// </summary>
internal static class KillVerb
{
    private const StringComparison OIC = StringComparison.OrdinalIgnoreCase;

    public static IReadOnlyList<string> ResolvePatterns(
        RigConfig config, IReadOnlyList<ProjectInfo> projects, string? query = null)
    {
        // Explicit config wins outright (it may name non-project processes too).
        if (config.Kill?.Match is { Count: > 0 } match) return match;

        if (query is not null)
        {
            // Resolve a named target like `rig run` does — exact Name/ShortName, then
            // substring — but honor a raw string if nothing matches, so you can still
            // `rig kill SomeExternalProc`.
            var named = FindMatches(projects, query);
            return named.Count > 0 ? named.Select(p => p.Name).ToList() : [query];
        }

        // No config, no arg → sweep every runnable project. Both platforms match the
        // full command line, so the *project name* — present in the `dotnet run
        // --project …/Foo.csproj` driver's cmdline and in the apphost path — is the
        // right, narrow target (narrower than a short AssemblyName, and it doesn't
        // depend on an apphost existing at all).
        return projects.Where(p => p.IsRunnable).Select(p => p.Name).ToList();
    }

    // Mirror of RunVerb's resolution: exact Name/ShortName match, else substring on Name.
    private static List<ProjectInfo> FindMatches(IReadOnlyList<ProjectInfo> projects, string query)
    {
        var q = query.Trim();
        var exact = projects.Where(p =>
            string.Equals(p.Name, q, OIC) || string.Equals(p.ShortName, q, OIC)).ToList();
        return exact.Count > 0 ? exact : projects.Where(p => p.Name.Contains(q, OIC)).ToList();
    }

    public static int Execute(RigSession session, IReadOnlyList<ProjectInfo> projects, string? query = null)
    {
        var patterns = ResolvePatterns(session.Config, projects, query);
        if (patterns.Count == 0)
        {
            Ui.Warn("Nothing to kill: no kill.match patterns and no runnable projects to infer from.");
            return 0;
        }

        return OperatingSystem.IsWindows()
            ? ExecuteWindows(session, patterns)
            : ExecuteUnix(session, patterns);
    }

    // ---- Unix: pkill -f matches the whole command line natively. ----

    private static int ExecuteUnix(RigSession session, IReadOnlyList<string> patterns)
    {
        if (Exec.DryRun)
        {
            foreach (var pattern in patterns)
            {
                var (code, output) = Exec.Capture("pgrep", ["-fl", pattern], session.Root);
                Report(pattern, code == 0 ? NonEmptyLines(output) : []);
            }
            return 0;
        }

        var worst = 0;
        foreach (var pattern in patterns)
        {
            Ui.Command("pkill", ["-f", pattern]);
            var rc = Exec.Run("pkill", ["-f", pattern], session.Root, suppressMissing: true);
            if (rc is not 0 and not 1) worst = rc; // 1 = no process matched
            if (rc == 0) Ui.Success($"killed process(es) matching '{pattern}'");
            else Ui.Info($"no process matched '{pattern}'");
        }
        return worst;
    }

    // ---- Windows: match the full command line (CIM), kill each match's tree. ----

    private static int ExecuteWindows(RigSession session, IReadOnlyList<string> patterns)
    {
        var processes = WindowsProcesses(session.Root);
        if (processes is null) // CIM/PowerShell unreachable — fall back to image-name kill.
            return ExecuteWindowsLegacy(session, patterns);

        var self = Environment.ProcessId;
        var alreadyKilled = new HashSet<int>();
        var worst = 0;

        foreach (var pattern in patterns)
        {
            var matches = MatchProcesses(processes, pattern, self);

            if (Exec.DryRun)
            {
                Report(pattern, matches.Select(p => $"{p.Pid}  {p.CommandLine}").ToList());
                continue;
            }

            var any = false;
            foreach (var (pid, _) in matches)
            {
                // A prior pattern's /T may have already taken this PID down in a tree.
                if (!alreadyKilled.Add(pid)) { any = true; continue; }

                var pidArg = pid.ToString();
                Ui.Command("taskkill", ["/F", "/T", "/PID", pidArg]);
                var rc = Exec.Run("taskkill", ["/F", "/T", "/PID", pidArg], session.Root, suppressMissing: true);
                if (rc == 0) any = true;
                else if (rc is not 128) worst = rc; // 128 = no such process (already gone, e.g. a /T child)
            }
            if (any) Ui.Success($"killed process(es) matching '{pattern}'");
            else Ui.Info($"no process matched '{pattern}'");
        }
        return worst;
    }

    /// <summary>Processes (PID + command line) whose command line contains
    /// <paramref name="pattern"/> (case-insensitive), excluding our own process and
    /// command-line-less system processes. Mirrors <c>pkill -f</c>'s substring match.</summary>
    public static IReadOnlyList<(int Pid, string CommandLine)> MatchProcesses(
        IReadOnlyList<(int Pid, string CommandLine)> processes, string pattern, int selfPid) =>
        processes.Where(p => p.Pid != selfPid
                          && p.CommandLine.Length > 0
                          && p.CommandLine.Contains(pattern, OIC))
                 .ToList();

    /// <summary>Parse the tab-delimited <c>PID&lt;tab&gt;CommandLine</c> lines emitted
    /// by the CIM query. Lines without a parseable PID are skipped; a missing command
    /// line (system processes) yields an empty string.</summary>
    public static IReadOnlyList<(int Pid, string CommandLine)> ParseProcessList(string output)
    {
        var result = new List<(int, string)>();
        foreach (var raw in output.Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            if (line.Length == 0) continue;
            var tab = line.IndexOf('\t');
            var pidText = (tab < 0 ? line : line[..tab]).Trim();
            if (!int.TryParse(pidText, out var pid)) continue;
            result.Add((pid, tab < 0 ? string.Empty : line[(tab + 1)..]));
        }
        return result;
    }

    // (PID, command line) for every process via CIM. null when PowerShell/CIM can't
    // be reached, so the caller can fall back to image-name matching.
    private static IReadOnlyList<(int Pid, string CommandLine)>? WindowsProcesses(string cwd)
    {
        // -EncodedCommand sidesteps every nested-quoting pitfall of passing a script
        // through ProcessStartInfo; tab-delimited output then parses cleanly even when
        // command lines contain spaces, quotes, or commas. Silencing the progress stream
        // keeps stdout clean and avoids the CLIXML noise CIM writes to stderr (which
        // Capture doesn't drain).
        const string script =
            "$ProgressPreference='SilentlyContinue'; " +
            "Get-CimInstance Win32_Process | ForEach-Object { \"$($_.ProcessId)`t$($_.CommandLine)\" }";
        var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));

        var (code, output) = Exec.Capture(
            "powershell", ["-NoProfile", "-NonInteractive", "-EncodedCommand", encoded], cwd);
        if (code != 0 && output.Length == 0) return null; // couldn't start / produced nothing usable
        return ParseProcessList(output);
    }

    // Pre-CIM behavior, kept as a safety net: taskkill by image name only.
    private static int ExecuteWindowsLegacy(RigSession session, IReadOnlyList<string> patterns)
    {
        if (Exec.DryRun)
        {
            foreach (var pattern in patterns)
            {
                var (code, output) = Exec.Capture("tasklist",
                    ["/FI", $"IMAGENAME eq {ImageName(pattern)}", "/NH"], session.Root);
                Report(pattern, code == 0 ? NonEmptyLines(output) : []);
            }
            return 0;
        }

        var worst = 0;
        foreach (var pattern in patterns)
        {
            var image = ImageName(pattern);
            Ui.Command("taskkill", ["/F", "/IM", image]);
            var rc = Exec.Run("taskkill", ["/F", "/IM", image], session.Root, suppressMissing: true);
            if (rc is not 0 and not 128) worst = rc; // 128 = no such process
            if (rc == 0) Ui.Success($"killed process(es) matching '{pattern}'");
            else Ui.Info($"no process matched '{pattern}'");
        }
        return worst;
    }

    private static string ImageName(string pattern) =>
        pattern.EndsWith(".exe", OIC) ? pattern : pattern + ".exe";

    private static List<string> NonEmptyLines(string output) =>
        output.Split('\n')
            .Select(l => l.Trim())
            .Where(l => l.Length > 0 && !l.StartsWith("INFO:", OIC)) // tasklist/pgrep "no tasks" notice
            .ToList();

    // Dry-run: show what *would* be killed, without killing anything.
    private static void Report(string pattern, IReadOnlyList<string> lines)
    {
        if (lines.Count == 0)
        {
            Ui.Info($"no process matches '{pattern}'");
            return;
        }
        Ui.Warn($"would kill {lines.Count} process(es) matching '{pattern}':");
        foreach (var line in lines) Ui.Info($"  {line}");
    }
}
