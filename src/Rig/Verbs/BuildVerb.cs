namespace Rig;

/// <summary>`rig build [args...]` — `dotnet build` on the discovered solution
/// (or the cwd when there's no solution). Extra args are forwarded.</summary>
internal static class BuildVerb
{
    public static int Execute(RigSession session, string[] forwarded, bool watch = false)
    {
        ProjectDiscovery.WarnMultipleSolutions(session.Root, session.Config.Solution);
        var solution = ProjectDiscovery.FindSolution(session.Root, session.Config.Solution);

        var args = new List<string> { "build" };
        if (solution is not null) args.Add(solution);
        args.AddRange(forwarded);
        if (watch) args.Insert(0, "watch"); // dotnet watch build …

        Ui.Command("dotnet", args);
        return Exec.Run("dotnet", args, session.Root, session.BuildEnv());
    }
}
