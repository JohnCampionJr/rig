namespace Rig.Tests;

[TestClass]
public sealed class ConventionTests
{
    private static ProjectInfo ProjectAt(string fullCsproj) =>
        new(Path.GetFileNameWithoutExtension(fullCsproj), fullCsproj, fullCsproj, "Exe", "net8.0", IsTest: false);

    // ---- ConfigWriter ----

    [TestMethod]
    public void ConfigWriter_creates_file_with_property()
    {
        using var t = new TempDir();
        var path = ConfigWriter.SetString(t.Path, "defaultProject", "App.Desktop");

        File.Exists(path).Should().BeTrue();
        var cfg = RigConfig.Load(path);
        cfg.DefaultProject.Should().Be("App.Desktop");
    }

    [TestMethod]
    public void ConfigWriter_updates_existing_file_preserving_other_keys()
    {
        using var t = new TempDir();
        t.Write(".rig.json", """{ "defaultProject": "Old", "commands": { "deploy": "./d.sh" } }""");

        ConfigWriter.SetString(t.Path, "defaultProject", "New");

        var cfg = RigConfig.Load(Path.Combine(t.Path, ".rig.json"));
        cfg.DefaultProject.Should().Be("New");
        cfg.Commands.Should().ContainKey("deploy"); // untouched
    }

    [TestMethod]
    public void ConfigWriter_preserves_comments_on_existing_file()
    {
        using var t = new TempDir();
        t.Write(".rig.json", "{\n  // keep this comment\n  \"defaultProject\": \"Old\"\n}\n");

        ConfigWriter.SetString(t.Path, "defaultProject", "New");

        var text = File.ReadAllText(Path.Combine(t.Path, ".rig.json"));
        text.Should().Contain("// keep this comment");
        RigConfig.Load(Path.Combine(t.Path, ".rig.json")).DefaultProject.Should().Be("New");
    }

    [TestMethod]
    public void Default_setter_persists_a_matched_runnable_project()
    {
        using var t = new TempDir();
        t.Write("App/App.csproj", """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net8.0</TargetFramework></PropertyGroup>
            </Project>
            """);
        t.Write("App.slnx", """<Solution><Project Path="App/App.csproj" /></Solution>""");

        var session = new RigSession(t.Path, new RigConfig());
        var rc = DefaultVerb.Execute(session, "App");

        rc.Should().Be(0);
        RigConfig.Load(Path.Combine(t.Path, ".rig.json")).DefaultProject.Should().Be("App");
    }

    [TestMethod]
    public void Default_setter_rejects_an_unknown_project()
    {
        using var t = new TempDir();
        t.Write("App/App.csproj", """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net8.0</TargetFramework></PropertyGroup>
            </Project>
            """);
        t.Write("App.slnx", """<Solution><Project Path="App/App.csproj" /></Solution>""");

        var session = new RigSession(t.Path, new RigConfig());
        DefaultVerb.Execute(session, "Nope").Should().Be(1);
        File.Exists(Path.Combine(t.Path, ".rig.json")).Should().BeFalse(); // nothing written
    }

    // ---- rebuild scoped to discovered projects ----

    [TestMethod]
    public void Rebuild_targets_only_discovered_project_bin_obj_not_vendored_trees()
    {
        using var t = new TempDir();
        t.Write("App/App.csproj", "<Project/>");
        t.Dir("App", "bin");
        t.Dir("App", "obj");
        t.Dir("vendor", "bin");   // not a discovered project → must be left alone
        t.Dir("vendor", "obj");

        var projects = new[] { ProjectAt(Path.Combine(t.Path, "App", "App.csproj")) };
        var targets = RebuildVerb.TargetDirs(t.Path, projects, skip: []);

        targets.Should().Contain(d => d.EndsWith(Path.Combine("App", "bin")));
        targets.Should().Contain(d => d.EndsWith(Path.Combine("App", "obj")));
        targets.Should().NotContain(d => d.Contains("vendor"));
    }

    [TestMethod]
    public void Rebuild_dry_run_deletes_nothing()
    {
        using var t = new TempDir();
        t.Write("App/App.csproj", "<Project/>");
        t.Write("App.slnx", """<Solution><Project Path="App/App.csproj" /></Solution>""");
        var bin = t.Dir("App", "bin");

        var rc = RebuildVerb.Execute(new RigSession(t.Path, new RigConfig()), [], dryRun: true);

        rc.Should().Be(0);
        Directory.Exists(bin).Should().BeTrue("dry run must not delete anything");
    }

    [TestMethod]
    public void Solution_candidates_list_slnx_before_sln()
    {
        using var t = new TempDir();
        t.Write("A.sln", "x");
        t.Write("B.slnx", "<Solution/>");

        var candidates = ProjectDiscovery.SolutionCandidates(t.Path);
        candidates.Should().HaveCount(2);
        candidates[0].Should().Be("B.slnx"); // *.slnx preferred (matches FindSolution)
    }

    // ---- runsettings auto-discovery ----

    [TestMethod]
    public void Finds_single_runsettings_next_to_test_project()
    {
        using var t = new TempDir();
        var testDir = t.Dir("tests", "App.Tests");
        var rs = t.Write("tests/App.Tests/CodeCoverage.runsettings", "<RunSettings/>");

        CoverageVerb.FindRunsettings(testDir, t.Path).Should().Be(rs);
    }

    [TestMethod]
    public void Ambiguous_runsettings_returns_null()
    {
        using var t = new TempDir();
        var dir = t.Dir("tests");
        t.Write("tests/a.runsettings", "<RunSettings/>");
        t.Write("tests/b.runsettings", "<RunSettings/>");

        CoverageVerb.FindRunsettings(dir, t.Path).Should().BeNull();
    }

    [TestMethod]
    public void No_runsettings_returns_null()
    {
        using var t = new TempDir();
        CoverageVerb.FindRunsettings(t.Path, t.Path).Should().BeNull();
    }
}
