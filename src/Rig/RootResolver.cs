namespace Rig;

internal enum AnchorKind { RigJson, Solution, Git, Cwd }

/// <summary>Where <c>rig</c> decided the repo root is, and why.</summary>
internal sealed record RepoContext(string Root, string? ConfigPath, AnchorKind Anchor);

/// <summary>
/// Resolves the repo root by walking up from a start directory. Precedence
/// (category, not distance): the nearest <c>.rig.json</c> wins; else the nearest
/// <c>*.slnx</c>/<c>*.sln</c>; else the nearest <c>.git</c>; else the start
/// directory. Explicit config therefore wins even over a closer solution.
/// </summary>
internal static class RootResolver
{
    public const string ConfigFileName = ".rig.json";

    public static RepoContext Resolve(string startDir)
    {
        var start = Path.GetFullPath(startDir);
        string? rigDir = null, solutionDir = null, gitDir = null;

        for (var d = new DirectoryInfo(start); d is not null; d = d.Parent)
        {
            rigDir ??= File.Exists(Path.Combine(d.FullName, ConfigFileName)) ? d.FullName : null;
            solutionDir ??= HasSolution(d.FullName) ? d.FullName : null;
            gitDir ??= HasGit(d.FullName) ? d.FullName : null;
        }

        if (rigDir is not null)
            return new RepoContext(rigDir, Path.Combine(rigDir, ConfigFileName), AnchorKind.RigJson);
        if (solutionDir is not null)
            return new RepoContext(solutionDir, null, AnchorKind.Solution);
        if (gitDir is not null)
            return new RepoContext(gitDir, null, AnchorKind.Git);
        return new RepoContext(start, null, AnchorKind.Cwd);
    }

    private static bool HasSolution(string dir)
    {
        foreach (var f in Directory.EnumerateFiles(dir))
            if (f.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase) ||
                f.EndsWith(".sln", StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }

    // `.git` is a directory for normal clones, a file for worktrees/submodules.
    private static bool HasGit(string dir)
    {
        var git = Path.Combine(dir, ".git");
        return Directory.Exists(git) || File.Exists(git);
    }
}
