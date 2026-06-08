namespace Rig.Tests;

[TestClass]
public sealed class RootResolverTests
{
    [TestMethod]
    public void Rig_json_wins_even_over_a_closer_solution()
    {
        using var t = new TempDir();
        // parent has .rig.json; child has a solution; start in a deeper dir.
        t.Write(".rig.json", "{}");
        t.Write("child/App.slnx", "<Solution/>");
        var start = t.Dir("child", "sub");

        var ctx = RootResolver.Resolve(start);

        ctx.Anchor.Should().Be(AnchorKind.RigJson);
        ctx.Root.Should().Be(t.Path);
        ctx.ConfigPath.Should().Be(Path.Combine(t.Path, ".rig.json"));
    }

    [TestMethod]
    public void Falls_back_to_nearest_solution_when_no_config()
    {
        using var t = new TempDir();
        t.Write("repo/App.sln", "Microsoft Visual Studio Solution File");
        var start = t.Dir("repo", "src");

        var ctx = RootResolver.Resolve(start);

        ctx.Anchor.Should().Be(AnchorKind.Solution);
        ctx.Root.Should().Be(Path.Combine(t.Path, "repo"));
        ctx.ConfigPath.Should().BeNull();
    }

    [TestMethod]
    public void Falls_back_to_git_root_when_no_config_or_solution()
    {
        using var t = new TempDir();
        t.Dir(".git");
        var start = t.Dir("a", "b");

        var ctx = RootResolver.Resolve(start);

        ctx.Anchor.Should().Be(AnchorKind.Git);
        ctx.Root.Should().Be(t.Path);
    }

    [TestMethod]
    public void A_git_boundary_stops_the_climb_so_a_solution_above_the_repo_is_ignored()
    {
        using var t = new TempDir();
        t.Write("Outer.sln", "Microsoft Visual Studio Solution File"); // outside the repo
        t.Dir("repo", ".git");                                         // the repo boundary
        var start = t.Dir("repo", "src");

        var ctx = RootResolver.Resolve(start);

        ctx.Anchor.Should().Be(AnchorKind.Git);
        ctx.Root.Should().Be(Path.Combine(t.Path, "repo"));
    }

    [TestMethod]
    public void A_solution_inside_the_repo_still_wins_over_the_git_root()
    {
        using var t = new TempDir();
        t.Dir(".git");
        t.Write("src/App.slnx", "<Solution/>"); // below the git root, in a subdir
        var start = t.Dir("src", "sub");

        var ctx = RootResolver.Resolve(start);

        ctx.Anchor.Should().Be(AnchorKind.Solution);
        ctx.Root.Should().Be(Path.Combine(t.Path, "src"));
    }

    [TestMethod]
    public void Detects_git_when_dot_git_is_a_file_worktree()
    {
        using var t = new TempDir();
        t.Write(".git", "gitdir: /somewhere/else");
        var start = t.Dir("nested");

        RootResolver.Resolve(start).Anchor.Should().Be(AnchorKind.Git);
    }
}
