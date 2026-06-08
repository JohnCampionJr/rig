using Spectre.Console;

namespace Rig;

/// <summary>
/// <c>rig cd [query]</c> — print a project directory to stdout so the rig shell
/// wrapper (installed by <c>rig completion</c>) can <c>cd</c> to it. With a
/// query, the best fuzzy match (deepest directory on ties); without one, an
/// interactive picker. The picker and all messages render to <b>stderr</b>, so
/// stdout carries only the path (the wrapper captures it via <c>$(...)</c>,
/// where stdout isn't a TTY).
/// </summary>
internal static class CdVerb
{
    const StringComparison OIC = StringComparison.OrdinalIgnoreCase;

    public static int Execute(RigSession session, string? query)
    {
        // A console bound to stderr: prompts + errors go here, never to stdout.
        var err = AnsiConsole.Create(new AnsiConsoleSettings { Out = new AnsiConsoleOutput(Console.Error) });

        var projects = ProjectDiscovery.Discover(session.Root, session.Config.Solution, session.Config.Exclude);
        var targets = new List<Target> { new("(root)", session.Root, ".") };
        foreach (var p in projects)
        {
            var dir = Path.GetDirectoryName(p.FullPath) ?? session.Root;
            targets.Add(new(p.Name, dir, Path.GetRelativePath(session.Root, dir)));
        }

        if (!string.IsNullOrWhiteSpace(query))
        {
            var match = FindTarget(targets, query.Trim());
            if (match is null)
            {
                err.MarkupLine($"[red]✗ no project matches \"{Markup.Escape(query)}\".[/]");
                return 1;
            }
            Console.Out.WriteLine(match.Dir);
            return 0;
        }

        if (!err.Profile.Capabilities.Interactive)
        {
            err.MarkupLine("[red]✗ rig cd: name a project, or run in a terminal to pick one.[/]");
            return 1;
        }

        var pick = err.Prompt(
            new SelectionPrompt<Target>()
                .Title("cd to which project?")
                .PageSize(20)
                .UseConverter(t => t.Rel == "."
                    ? Markup.Escape(t.Name)
                    : $"{Markup.Escape(t.Name)}  [grey]{Markup.Escape(t.Rel)}[/]")
                .AddChoices(targets));
        Console.Out.WriteLine(pick.Dir);
        return 0;
    }

    // Exact (full name or short name) → substring; deepest directory on ties.
    static Target? FindTarget(List<Target> targets, string q)
    {
        static string Short(string n) => n.Contains('.') ? n[(n.LastIndexOf('.') + 1)..] : n;
        var exact = targets.Where(t => string.Equals(t.Name, q, OIC) || string.Equals(Short(t.Name), q, OIC)).ToList();
        var matches = exact.Count > 0 ? exact : targets.Where(t => t.Name.Contains(q, OIC)).ToList();
        return matches.Count == 0 ? null : matches.OrderByDescending(t => t.Dir.Length).First();
    }

    sealed record Target(string Name, string Dir, string Rel);
}
