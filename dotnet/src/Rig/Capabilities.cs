namespace Rig;

/// <summary>
/// What the current repo supports, used to degrade gracefully: verbs surface a
/// one-line reason instead of a confusing failure, and the menu + completion hide
/// verbs that can't apply (no test project → no test/coverage; no runnable → no
/// run/publish). <see cref="Unavailable"/> is pure and unit-tested.
/// </summary>
internal sealed record Capabilities(bool HasSolution, int RunnableProjects, bool HasTestProject)
{
    /// <summary>Everything available — the permissive fallback when probing fails,
    /// so a transient hiccup never hides verbs.</summary>
    public static readonly Capabilities All = new(true, 1, true);

    public static Capabilities Probe(RigSession session)
    {
        try
        {
            var projects = ProjectDiscovery.Discover(session.Root, session.Config.Solution, session.Config.Exclude);
            var hasSolution = ProjectDiscovery.FindSolution(session.Root, session.Config.Solution) is not null;
            var runnable = projects.Count(p => p.IsRunnable);
            var hasTest = TestVerb.ResolveTestProject(session, projects) is not null;
            return new Capabilities(hasSolution, runnable, hasTest);
        }
        catch
        {
            return All;
        }
    }

    /// <summary>Reason a built-in verb can't run here, or null if it can.</summary>
    public string? Unavailable(string verb) => verb switch
    {
        "run" or "publish" or "default" => RunnableProjects == 0 ? "no runnable projects found" : null,
        "test" or "coverage" => !HasTestProject ? "no test project found" : null,
        _ => null, // build/rebuild/kill/custom always offered
    };
}
