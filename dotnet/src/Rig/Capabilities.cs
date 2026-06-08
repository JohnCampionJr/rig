namespace Rig;

/// <summary>
/// What the current repo supports, used to degrade gracefully: verbs surface a
/// one-line reason instead of a confusing failure, and the menu greys out rows
/// that can't apply. <see cref="Unavailable"/> is pure and unit-tested.
/// </summary>
internal sealed record Capabilities(bool HasSolution, int RunnableProjects, bool HasTestProject)
{
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
            return new Capabilities(false, 0, false);
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
