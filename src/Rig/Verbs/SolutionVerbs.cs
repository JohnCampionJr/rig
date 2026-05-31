namespace Rig;

/// <summary>
/// The thin `dotnet &lt;verb&gt; [solution] [forwarded]` wrappers that round out the
/// everyday loop: <c>restore</c>, <c>clean</c>, <c>format</c>. They share
/// <see cref="OnSolution"/> — the solution is auto-discovered, extra args forward.
/// </summary>
internal static class SolutionVerbs
{
    public static int OnSolution(RigSession session, string dotnetVerb, string[] forwarded, string? configuration = null)
    {
        ProjectDiscovery.WarnMultipleSolutions(session.Root, session.Config.Solution);
        var solution = ProjectDiscovery.FindSolution(session.Root, session.Config.Solution);

        var args = new List<string> { dotnetVerb };
        if (solution is not null) args.Add(solution);
        if (!string.IsNullOrEmpty(configuration)) { args.Add("-c"); args.Add(configuration); }
        args.AddRange(forwarded);

        Ui.Command("dotnet", args);
        return Exec.Run("dotnet", args, session.Root, session.BuildEnv());
    }
}

/// <summary>`rig restore` — `dotnet restore` on the discovered solution.</summary>
internal static class RestoreVerb
{
    public static int Execute(RigSession session, string[] forwarded) =>
        SolutionVerbs.OnSolution(session, "restore", forwarded);
}

/// <summary>`rig clean` — `dotnet clean` on the discovered solution (MSBuild-aware;
/// distinct from `rebuild`, which deletes bin/obj outright).</summary>
internal static class CleanVerb
{
    public static int Execute(RigSession session, string[] forwarded, string? configuration = null) =>
        SolutionVerbs.OnSolution(session, "clean", forwarded, configuration);
}

/// <summary>`rig format` — `dotnet format` on the discovered solution.</summary>
internal static class FormatVerb
{
    public static int Execute(RigSession session, string[] forwarded) =>
        SolutionVerbs.OnSolution(session, "format", forwarded);
}
