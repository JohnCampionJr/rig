using Spectre.Console;

namespace Rig;

/// <summary>
/// <c>rig cd [query]</c> — print a project directory to stdout so the rig shell
/// wrapper (installed by <c>rig completion</c>) can <c>cd</c> to it. With a
/// query, the best fuzzy match; a query that matches nothing falls back to the
/// picker. Matching is path-aware (name, short name, relative path, or directory
/// basename) and forgiving (exact → prefix → substring → subsequence). The
/// picker and all messages render to <b>stderr</b>, so stdout carries only the
/// path (the wrapper captures it via <c>$(...)</c>, where stdout isn't a TTY).
/// </summary>
internal static class CdVerb
{
    internal sealed record Target(string Name, string Dir, string Rel);

    public static int Execute(RigSession session, string? query)
    {
        var err = AnsiConsole.Create(new AnsiConsoleSettings { Out = new AnsiConsoleOutput(Console.Error) });
        var targets = BuildTargets(session);

        if (!string.IsNullOrWhiteSpace(query))
        {
            var best = Rank(targets, query).FirstOrDefault();
            if (best is not null)
            {
                Console.Out.WriteLine(best.Dir);
                return 0;
            }
            if (!err.Profile.Capabilities.Interactive)
            {
                err.MarkupLine($"[red]✗ no project matches \"{Markup.Escape(query)}\".[/]");
                return 1;
            }
            err.MarkupLine($"[yellow]! no project matches \"{Markup.Escape(query)}\" — pick one:[/]");
        }
        else if (!err.Profile.Capabilities.Interactive)
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

    static List<Target> BuildTargets(RigSession session)
    {
        var projects = ProjectDiscovery.Discover(session.Root, session.Config.Solution, session.Config.Exclude);
        var targets = new List<Target> { new("(root)", session.Root, ".") };
        foreach (var p in projects)
        {
            var dir = Path.GetDirectoryName(p.FullPath) ?? session.Root;
            targets.Add(new(p.Name, dir, Path.GetRelativePath(session.Root, dir)));
        }
        return targets;
    }

    /// <summary>Targets matching <paramref name="query"/>, best first. Pure.</summary>
    internal static List<Target> Rank(IReadOnlyList<Target> targets, string query)
    {
        var q = query.Trim().ToLowerInvariant();
        if (q.Length == 0) return targets.ToList();
        return targets
            .Select(t => (t, r: Rank(t, q)))
            .Where(x => x.r.Best > 0)
            .OrderByDescending(x => x.r.Best)
            .ThenByDescending(x => x.r.ByName)     // a name match beats a path-only match
            .ThenByDescending(x => x.t.Dir.Length) // then deepest on ties
            .ThenBy(x => x.t.Name.Length)          // then the closest (shortest) name
            .Select(x => x.t)
            .ToList();
    }

    // Best tier across the name/short-name fields and the path fields, plus
    // whether a name field was at least as good (so `cd web` prefers Foo.Web
    // over Foo.Web.Tests, which only matches on its `web` directory basename).
    static (int Best, bool ByName) Rank(Target t, string q)
    {
        var nameTier = Math.Max(FieldScore(t.Name, q), FieldScore(ShortName(t.Name), q));
        var pathTier = Math.Max(FieldScore(t.Rel, q), FieldScore(Path.GetFileName(t.Dir), q));
        return (Math.Max(nameTier, pathTier), nameTier > 0 && nameTier >= pathTier);
    }

    static int FieldScore(string field, string q)
    {
        var h = field.ToLowerInvariant();
        if (h == q) return 100;
        if (h.StartsWith(q, StringComparison.Ordinal)) return 80;
        if (h.Contains(q, StringComparison.Ordinal)) return 60;
        return IsSubsequence(q, h) ? 40 : 0;
    }

    static bool IsSubsequence(string needle, string haystack)
    {
        if (needle.Length == 0) return true;
        var i = 0;
        foreach (var c in haystack)
        {
            if (c == needle[i] && ++i == needle.Length) return true;
        }
        return false;
    }

    static string ShortName(string n) => n.Contains('.') ? n[(n.LastIndexOf('.') + 1)..] : n;
}
