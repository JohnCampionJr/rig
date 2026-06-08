using System.CommandLine.Completions;

namespace Rig;

/// <summary>
/// Dynamic completion sources. All swallow exceptions — completion must never
/// throw — and never trigger a build (they only read what's already there).
/// </summary>
internal static class Completions
{
    public static IEnumerable<CompletionItem> RunnableProjects()
    {
        try
        {
            var s = RigSession.Load(Directory.GetCurrentDirectory());
            return ProjectDiscovery.Discover(s.Root, s.Config.Solution, s.Config.Exclude)
                .Where(p => p.IsRunnable)
                .Select(p => new CompletionItem(p.ShortName))
                .ToList();
        }
        catch { return []; }
    }

    public static IEnumerable<CompletionItem> AllProjects()
    {
        try
        {
            var s = RigSession.Load(Directory.GetCurrentDirectory());
            return ProjectDiscovery.Discover(s.Root, s.Config.Solution, s.Config.Exclude)
                .Select(p => new CompletionItem(p.ShortName))
                .ToList();
        }
        catch { return []; }
    }

    public static IEnumerable<CompletionItem> TestClasses()
    {
        try
        {
            var s = RigSession.Load(Directory.GetCurrentDirectory());
            var projects = ProjectDiscovery.Discover(s.Root, s.Config.Solution, s.Config.Exclude);
            var testProject = TestVerb.ResolveTestProject(s, projects);
            if (testProject is null) return [];

            var dll = TestVerb.TryBuiltAssembly(testProject);
            if (dll is null) return [];

            return TestEnumeration.Enumerate(dll)
                .Select(c => new CompletionItem(c.Contains('.') ? c[(c.LastIndexOf('.') + 1)..] : c))
                .ToList();
        }
        catch { return []; }
    }
}
