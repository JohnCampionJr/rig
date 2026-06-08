namespace Rig;

/// <summary>
/// `rig rebuild` — delete every in-tree <c>bin</c>/<c>obj</c> (honouring the
/// config skip-list), then build. Skip matching (pure, testable) is in
/// <see cref="IsSkipped"/>.
/// </summary>
internal static class RebuildVerb
{
    private const StringComparison OIC = StringComparison.OrdinalIgnoreCase;

    public static bool IsSkipped(string relativeDir, IEnumerable<string> skip)
    {
        var norm = relativeDir.Replace('\\', '/');
        foreach (var raw in skip)
        {
            var s = raw.Replace('\\', '/').Trim().TrimEnd('/');
            if (s.Length == 0) continue;
            if (norm.Equals(s, OIC) || norm.StartsWith(s + "/", OIC)) return true;
        }
        return false;
    }

    /// <summary>
    /// bin/obj to remove, scoped to the discovered solution projects (+ the root).
    /// Scoping is the convention that makes <c>rebuild.skip</c> unnecessary:
    /// vendored trees that aren't solution projects are never touched. The
    /// optional skip-list still filters further.
    /// </summary>
    public static IReadOnlyList<string> TargetDirs(
        string root, IReadOnlyList<ProjectInfo> projects, IReadOnlyList<string> skip)
    {
        var dirs = new List<string>();

        foreach (var p in projects)
        {
            var projectDir = Path.GetDirectoryName(p.FullPath);
            if (projectDir is not null) AddBinObj(dirs, projectDir);
        }
        AddBinObj(dirs, root);

        return dirs
            .Distinct()
            .Where(d => !IsSkipped(Path.GetRelativePath(root, d), skip))
            .ToList();
    }

    private static void AddBinObj(List<string> into, string dir)
    {
        foreach (var name in new[] { "bin", "obj" })
        {
            var d = Path.Combine(dir, name);
            if (Directory.Exists(d)) into.Add(d);
        }
    }

    public static int Execute(RigSession session, string[] forwarded, bool dryRun = false, string? configuration = null)
    {
        var projects = ProjectDiscovery.Discover(session.Root, session.Config.Solution, session.Config.Exclude);
        var skip = session.Config.Rebuild?.Skip ?? [];
        var targets = TargetDirs(session.Root, projects, skip);

        if (dryRun)
        {
            Ui.Info($"Dry run — would remove {targets.Count} bin/obj director{(targets.Count == 1 ? "y" : "ies")}:");
            foreach (var dir in targets) Ui.Info($"  {Path.GetRelativePath(session.Root, dir)}");
            return 0;
        }

        Ui.Command("rm -rf", targets.Select(d => Path.GetRelativePath(session.Root, d)));
        var removed = 0;
        foreach (var dir in targets)
        {
            try
            {
                if (Directory.Exists(dir)) { Directory.Delete(dir, recursive: true); removed++; }
            }
            catch (Exception ex)
            {
                Ui.Warn($"  skipped {dir}: {ex.Message}");
            }
        }
        Ui.Info($"removed {removed} bin/obj director{(removed == 1 ? "y" : "ies")}");
        return BuildVerb.Execute(session, forwarded, configuration: configuration);
    }
}
