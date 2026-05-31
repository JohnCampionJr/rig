namespace Rig.Tests;

[TestClass]
public sealed class VerbLogicTests
{
    private static ProjectInfo Exe(string name) =>
        new(name, $"{name}/{name}.csproj", $"/r/{name}/{name}.csproj", "Exe", "net8.0", IsTest: false);

    private static ProjectInfo Lib(string name) =>
        new(name, $"{name}/{name}.csproj", $"/r/{name}/{name}.csproj", null, "net8.0", IsTest: false);

    // ---- RunVerb.Resolve ----

    [TestMethod]
    public void Run_no_query_single_runnable_is_selected()
    {
        var res = RunVerb.Resolve([Exe("App"), Lib("Core")], query: null, defaultProject: null);
        res.Selected!.Name.Should().Be("App");
    }

    [TestMethod]
    public void Run_no_query_prefers_default_project()
    {
        var res = RunVerb.Resolve([Exe("App"), Exe("Tool")], query: null, defaultProject: "Tool");
        res.Selected!.Name.Should().Be("Tool");
    }

    [TestMethod]
    public void Run_no_query_multiple_runnables_is_ambiguous()
    {
        var res = RunVerb.Resolve([Exe("App"), Exe("Tool")], query: null, defaultProject: null);
        res.Selected.Should().BeNull();
        res.Ambiguous.Should().HaveCount(2);
    }

    [TestMethod]
    public void Run_query_matches_short_name_then_substring()
    {
        var projects = new[] { Exe("Acme.App"), Exe("Acme.Tool") };
        RunVerb.Resolve(projects, "App", null).Selected!.Name.Should().Be("Acme.App");      // short-name exact
        RunVerb.Resolve(projects, "Acme", null).Ambiguous.Should().HaveCount(2);             // substring → both
        RunVerb.Resolve(projects, "nope", null).Error.Should().NotBeNull();                  // no match
    }

    [TestMethod]
    public void Run_no_runnable_projects_is_an_error()
    {
        RunVerb.Resolve([Lib("Core")], null, null).Error.Should().NotBeNull();
    }

    // ---- RebuildVerb.IsSkipped ----

    [TestMethod]
    public void Rebuild_skip_matches_exact_and_prefix_segments()
    {
        string[] skip = ["vendor", "node_modules"];
        RebuildVerb.IsSkipped("vendor/bin", skip).Should().BeTrue();
        RebuildVerb.IsSkipped("vendor", skip).Should().BeTrue();
        RebuildVerb.IsSkipped("src/App/bin", skip).Should().BeFalse();
        RebuildVerb.IsSkipped("vendored/bin", skip).Should().BeFalse(); // not a path segment
    }

    // ---- KillVerb.ResolvePatterns ----

    [TestMethod]
    public void Kill_prefers_configured_match()
    {
        var cfg = new RigConfig { Kill = new KillConfig { Match = ["MyApp"] }, DefaultProject = "Other" };
        KillVerb.ResolvePatterns(cfg, [Exe("App")]).Should().BeEquivalentTo("MyApp");
    }

    [TestMethod]
    public void Kill_falls_back_to_default_then_first_runnable()
    {
        KillVerb.ResolvePatterns(new RigConfig { DefaultProject = "Foo" }, [Exe("App")])
            .Should().BeEquivalentTo("Foo");
        KillVerb.ResolvePatterns(new RigConfig(), [Exe("App")])
            .Should().BeEquivalentTo("App");
        KillVerb.ResolvePatterns(new RigConfig(), [Lib("OnlyLib")])
            .Should().BeEmpty();
    }

    [TestMethod]
    public void Kill_pattern_is_platform_aware_for_assembly_name()
    {
        var app = new ProjectInfo("App", "App/App.csproj", "/r/App/App.csproj",
            "Exe", "net8.0", IsTest: false, AssemblyName: "AcmeApp");

        // Windows taskkill /IM → the image (AssemblyName); Unix pkill -f → the
        // targeted project name (narrower than a short AssemblyName).
        var expected = OperatingSystem.IsWindows() ? "AcmeApp" : "App";
        KillVerb.ResolvePatterns(new RigConfig { DefaultProject = "App" }, [app])
            .Should().BeEquivalentTo(expected);
        KillVerb.ResolvePatterns(new RigConfig(), [app])
            .Should().BeEquivalentTo(expected);
    }

    // ---- PublishVerb ----

    [TestMethod]
    public void Publish_rid_and_output_defaults_and_overrides()
    {
        PublishVerb.ResolveOutput(null, "osx-arm64").Should().Be("dist/osx-arm64");
        PublishVerb.ResolveOutput(new PublishConfig { Output = "out/{rid}/app" }, "win-x64")
            .Should().Be("out/win-x64/app");
        PublishVerb.ResolveRid(new PublishConfig { Rid = "linux-x64" }).Should().Be("linux-x64");
    }

    [TestMethod]
    public void Publish_args_reflect_self_contained_and_single_file()
    {
        var args = PublishVerb.BuildArgs("/r/App/App.csproj",
            new PublishConfig { SelfContained = false, SingleFile = true }, "win-x64", "/r/dist/win-x64");

        args.Should().ContainInOrder("publish", "/r/App/App.csproj", "-c", "Release", "-r", "win-x64");
        args.Should().ContainInConsecutiveOrder("--self-contained", "false");
        args.Should().Contain("-p:PublishSingleFile=true");
    }
}
