using Spectre.Console;

namespace Rig;

/// <summary>
/// `rig add &lt;package&gt; [project] [forwarded]` — `dotnet add &lt;project&gt; package
/// &lt;pkg&gt;`, but the project is auto-resolved (default → sole → prompt), so you skip
/// the `dotnet add` ceremony of always naming the project. The project is positional
/// (matching the Node tool); `--project/-p` is also accepted for back-compat. Extra args
/// (e.g. <c>--version 1.2.3</c>, <c>--prerelease</c>) forward to `dotnet add package`.
/// Project resolution (pure) is <see cref="ResolveTarget"/>.
/// </summary>
internal static class AddVerb
{
    private const StringComparison OIC = StringComparison.OrdinalIgnoreCase;

    public sealed record Resolution(ProjectInfo? Selected, IReadOnlyList<ProjectInfo> Ambiguous, string? Error);

    /// <summary>Pick the project to add to: an explicit query, else the default
    /// project, else the sole project, else ambiguous. Unlike `run`, this spans
    /// <em>all</em> projects (you add packages to libraries and tests too).</summary>
    public static Resolution ResolveTarget(IReadOnlyList<ProjectInfo> projects, string? query, string? defaultProject)
    {
        if (projects.Count == 0) return new Resolution(null, [], "No projects found.");

        if (!string.IsNullOrWhiteSpace(query))
        {
            var byName = projects.Where(p =>
                string.Equals(p.Name, query, OIC) || string.Equals(p.ShortName, query, OIC)).ToList();
            var matches = byName.Count > 0 ? byName : projects.Where(p => p.Name.Contains(query!, OIC)).ToList();
            return matches.Count switch
            {
                0 => new Resolution(null, [], $"No project matches '{query}'."),
                1 => new Resolution(matches[0], [], null),
                _ => new Resolution(null, matches, null),
            };
        }

        if (defaultProject is not null)
        {
            var preferred = projects.FirstOrDefault(p =>
                string.Equals(p.Name, defaultProject, OIC) || string.Equals(p.ShortName, defaultProject, OIC));
            if (preferred is not null) return new Resolution(preferred, [], null);
        }

        return projects.Count == 1
            ? new Resolution(projects[0], [], null)
            : new Resolution(null, projects, null);
    }

    public static int Execute(RigSession session, string? package, string? projectQuery, string[] forwarded)
    {
        if (string.IsNullOrWhiteSpace(package))
        {
            Ui.Error("Usage: rig add <package> [project] [-- --version 1.2.3]");
            return 1;
        }

        var projects = ProjectDiscovery.Discover(session.Root, session.Config.Solution, session.Config.Exclude);
        var resolution = ResolveTarget(projects, projectQuery, session.Config.DefaultProject);
        if (resolution.Error is not null) { Ui.Error(resolution.Error); return 1; }

        var target = resolution.Selected;
        if (target is null) // ambiguous
        {
            if (!AnsiConsole.Profile.Capabilities.Interactive)
            {
                Ui.Error("Multiple projects — name one (e.g. `rig add <pkg> <project>`):");
                foreach (var p in resolution.Ambiguous) Ui.Info($"  • {p.Name}");
                return 1;
            }
            // Esc/Backspace dismisses the picker → abort the add (parity with the
            // Node menu's cancel keys; Node's `add` returns 1 when backed out).
            if (!CancelKeyPrompt.TryShow(new SelectionPrompt<ProjectInfo>()
                    .Title($"Add [aqua]{Markup.Escape(package)}[/] to which project?")
                    .UseConverter(p => p.Name)
                    .AddChoices(resolution.Ambiguous), out var picked))
                return 1;
            target = picked;
        }

        var args = new List<string> { "add", target.FullPath, "package", package };
        args.AddRange(forwarded);
        Ui.Command("dotnet", args);
        return Exec.Run("dotnet", args, session.Root, session.BuildEnv());
    }
}
