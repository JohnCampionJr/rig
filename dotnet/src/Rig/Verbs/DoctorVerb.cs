using System.Text.Json;

using Spectre.Console;

namespace Rig;

/// <summary>
/// `rig doctor` — flag environment problems before a verb fails confusingly:
/// the .NET SDK (and whether it satisfies a <c>global.json</c> pin), restore
/// state, the solution/project layout, and the test project. Mirrors the Node
/// `rig doctor`. Exit code is non-zero only on an error-level finding, so it's
/// usable as a CI / pre-push gate.
/// </summary>
internal static class DoctorVerb
{
    private enum Level { Ok, Warn, Error }

    public static int Execute(RigSession session)
    {
        var severity = Level.Ok;
        void Bump(Level l) { if (l > severity) severity = l; }

        void Line(Level level, string label, string detail)
        {
            var mark = level switch
            {
                Level.Ok => "[green]✓[/]",
                Level.Warn => "[yellow]![/]",
                _ => "[red]✗[/]",
            };
            AnsiConsole.MarkupLine($"  {mark} {Markup.Escape(label.PadRight(10))} [grey]{Markup.Escape(detail)}[/]");
            Bump(level);
        }

        AnsiConsole.Write(new Rule("[aqua]rig doctor[/]").LeftJustified());

        // .NET SDK present, and new enough for a global.json pin.
        var (code, output) = Exec.Capture("dotnet", ["--version"], session.Root);
        var sdk = output.Trim();
        if (code != 0 || sdk.Length == 0)
        {
            Line(Level.Error, "sdk", "dotnet not found on PATH");
        }
        else
        {
            var pin = ReadSdkPin(session.Root);
            if (pin is null)
                Line(Level.Ok, "sdk", sdk);
            else if (SdkSatisfies(sdk, pin))
                Line(Level.Ok, "sdk", $"{sdk} (global.json pins {pin})");
            else
                Line(Level.Error, "sdk", $"{sdk} — global.json pins {pin}");
        }

        var projects = ProjectDiscovery.Discover(session.Root, session.Config.Solution, session.Config.Exclude);

        // Restore state — the .NET analog of node_modules: a project is restored
        // when MSBuild has written its obj/project.assets.json.
        if (projects.Count > 0)
        {
            var restored = projects.Count(p => File.Exists(
                Path.Combine(Path.GetDirectoryName(p.FullPath)!, "obj", "project.assets.json")));
            if (restored == 0)
                Line(Level.Warn, "restore", "not restored — run `rig restore`");
            else if (restored < projects.Count)
                Line(Level.Warn, "restore", $"{restored}/{projects.Count} projects restored — run `rig restore`");
            else
                Line(Level.Ok, "restore", "packages restored");
        }

        // Layout: the chosen solution + project count (and a nudge when several
        // solutions are present and none is pinned).
        var solution = ProjectDiscovery.FindSolution(session.Root, session.Config.Solution);
        if (projects.Count == 0)
        {
            Line(Level.Warn, "layout", "no projects found");
        }
        else if (solution is null)
        {
            Line(Level.Ok, "layout", $"no solution — {projects.Count} loose project(s)");
        }
        else
        {
            var candidates = string.IsNullOrEmpty(session.Config.Solution)
                ? ProjectDiscovery.SolutionCandidates(session.Root).Count
                : 1;
            var name = Path.GetFileName(solution);
            if (candidates > 1)
                Line(Level.Warn, "layout", $"{name} of {candidates} solutions — set dotnet.solution to pin");
            else
                Line(Level.Ok, "layout", $"{name}, {projects.Count} project(s)");
        }

        // Test project (informational — many app-only repos have none).
        var testProject = TestVerb.ResolveTestProject(session, projects);
        Line(Level.Ok, "tests", testProject is null ? "(none)" : Path.GetRelativePath(session.Root, testProject));

        AnsiConsole.WriteLine();
        switch (severity)
        {
            case Level.Ok: Ui.Success("all good"); break;
            case Level.Warn: Ui.Warn("some warnings"); break;
            default: Ui.Error("problems found"); break;
        }
        return severity == Level.Error ? 1 : 0;
    }

    /// <summary>The <c>sdk.version</c> pinned by the nearest <c>global.json</c> at
    /// or above <paramref name="root"/>, or null when none pins one. Tolerant: a
    /// missing/garbled file is treated as no pin.</summary>
    internal static string? ReadSdkPin(string root)
    {
        for (var dir = root; dir is not null; dir = Path.GetDirectoryName(dir))
        {
            var path = Path.Combine(dir, "global.json");
            if (!File.Exists(path)) continue;
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(path));
                if (doc.RootElement.TryGetProperty("sdk", out var s) &&
                    s.TryGetProperty("version", out var v) &&
                    v.ValueKind == JsonValueKind.String)
                    return v.GetString();
            }
            catch { /* unreadable global.json → treat as no pin */ }
            return null; // the nearest global.json wins, pin or not
        }
        return null;
    }

    /// <summary>Whether an installed SDK version satisfies a <c>global.json</c>
    /// pin. A heuristic: same-or-newer major is fine (rollForward usually handles
    /// the rest); an unparseable side defers to "satisfied" rather than crying
    /// wolf. Pure — unit tested.</summary>
    internal static bool SdkSatisfies(string installed, string? pinned)
    {
        if (string.IsNullOrWhiteSpace(pinned)) return true;
        var have = Major(installed);
        var need = Major(pinned);
        if (have is null || need is null) return true;
        return have >= need;
    }

    private static int? Major(string version)
    {
        var dot = version.IndexOf('.');
        var head = dot < 0 ? version : version[..dot];
        return int.TryParse(head, out var n) ? n : null;
    }
}
