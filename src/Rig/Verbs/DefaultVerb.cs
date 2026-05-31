using Spectre.Console;

namespace Rig;

/// <summary>
/// `rig default [project]` — show or set `defaultProject` without running
/// anything. With a name it validates against the runnable projects and
/// persists; with no name it prompts interactively (or prints the current value
/// in a non-interactive shell). Writes go through <see cref="ConfigWriter"/>, so
/// comments are preserved.
/// </summary>
internal static class DefaultVerb
{
    public static int Execute(RigSession session, string? query)
    {
        var projects = ProjectDiscovery.Discover(session.Root, session.Config.Solution, session.Config.Exclude);
        var runnable = projects.Where(p => p.IsRunnable).ToList();

        if (query is null)
            return NoArg(session, runnable);

        var resolution = RunVerb.Resolve(projects, query, defaultProject: null);
        if (resolution.Error is not null)
        {
            Ui.Error(resolution.Error);
            foreach (var p in runnable) Ui.Info($"  • {p.Name}");
            return 1;
        }

        var target = resolution.Selected;
        if (target is null) // ambiguous
        {
            if (!AnsiConsole.Profile.Capabilities.Interactive)
            {
                Ui.Error($"'{query}' matches multiple projects:");
                foreach (var p in resolution.Ambiguous) Ui.Info($"  • {p.Name}");
                return 1;
            }
            target = Pick("Which project?", resolution.Ambiguous);
        }

        return Persist(session, target.Name);
    }

    private static int NoArg(RigSession session, List<ProjectInfo> runnable)
    {
        if (!AnsiConsole.Profile.Capabilities.Interactive)
        {
            var current = session.Config.DefaultProject;
            Ui.Info(current is null ? "No default project set." : $"defaultProject = {current}");
            return 0;
        }

        if (runnable.Count == 0)
        {
            Ui.Error("No runnable projects found.");
            return 1;
        }

        return Persist(session, Pick("Set default project", runnable).Name);
    }

    private static ProjectInfo Pick(string title, IReadOnlyList<ProjectInfo> choices) =>
        AnsiConsole.Prompt(new SelectionPrompt<ProjectInfo>()
            .Title(title)
            .UseConverter(p => p.Name)
            .AddChoices(choices));

    private static int Persist(RigSession session, string name)
    {
        var path = ConfigWriter.SetString(session.Root, "defaultProject", name);
        Ui.Success($"Set defaultProject = {name} in {Path.GetFileName(path)}");
        return 0;
    }
}
