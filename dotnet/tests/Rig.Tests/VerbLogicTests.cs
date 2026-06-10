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

    // ---- RunVerb.BuildRunArgs / TestVerb.BuildTestArgs ----

    [TestMethod]
    public void Run_args_place_framework_and_launch_profile_before_the_forwarding_boundary()
    {
        var args = RunVerb.BuildRunArgs("/r/App/App.csproj", configuration: "Release",
            framework: "net10.0", launchProfile: "https", forwarded: ["--urls", "http://*:0"], watch: false);

        args.Should().ContainInConsecutiveOrder("run", "--project", "/r/App/App.csproj");
        args.Should().ContainInConsecutiveOrder("--framework", "net10.0");
        args.Should().ContainInConsecutiveOrder("--launch-profile", "https");
        // forwarded args live after `--`, and the framework flags come before it
        args.IndexOf("--framework").Should().BeLessThan(args.IndexOf("--"));
        args.Skip(args.IndexOf("--") + 1).Should().ContainInOrder("--urls", "http://*:0");
    }

    [TestMethod]
    public void Run_args_omit_unset_options_and_prepend_watch()
    {
        var args = RunVerb.BuildRunArgs("/r/App/App.csproj", configuration: null,
            framework: null, launchProfile: null, forwarded: [], watch: true);

        args.Should().Equal("watch", "run", "--project", "/r/App/App.csproj");
        args.Should().NotContain("--framework").And.NotContain("--launch-profile").And.NotContain("--");
    }

    [TestMethod]
    public void Test_args_include_framework_and_filter()
    {
        var args = TestVerb.BuildTestArgs(TestPlatform.Runner.VsTest, "/r/T/T.csproj", filter: "FullyQualifiedName~Foo",
            framework: "net10.0", forwarded: ["--blame"], watch: false);

        args.Should().ContainInConsecutiveOrder("test", "/r/T/T.csproj");
        args.Should().ContainInConsecutiveOrder("--framework", "net10.0");
        args.Should().ContainInConsecutiveOrder("--filter", "FullyQualifiedName~Foo");
        args.Should().Contain("--blame");
    }

    [TestMethod]
    public void Test_args_pass_the_project_positionally_for_vstest()
    {
        var args = TestVerb.BuildTestArgs(TestPlatform.Runner.VsTest, "/r/T/T.csproj", filter: null, framework: null, forwarded: [], watch: false);
        // Classic VSTest `dotnet test` has no `--project` switch — positional only.
        args.Should().Equal("test", "/r/T/T.csproj");
        args.Should().NotContain("--project");
    }

    [TestMethod]
    public void Test_args_pass_the_project_via_flag_for_mtp()
    {
        var args = TestVerb.BuildTestArgs(TestPlatform.Runner.Mtp, "/r/T/T.csproj", filter: "FullyQualifiedName~Foo",
            framework: null, forwarded: [], watch: false);
        // The MTP `dotnet test` parser names the project with `--project`; the same
        // `--filter` expression rides along unchanged.
        args.Should().ContainInConsecutiveOrder("test", "--project", "/r/T/T.csproj");
        args.Should().ContainInConsecutiveOrder("--filter", "FullyQualifiedName~Foo");
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

    [TestMethod]
    public void Rebuild_within_root_guards_the_recursive_delete()
    {
        var root = Path.Combine(Path.GetTempPath(), "rigroot");
        RebuildVerb.IsWithinRoot(root, Path.Combine(root, "src", "App", "bin")).Should().BeTrue();
        RebuildVerb.IsWithinRoot(root, root).Should().BeFalse();                       // never the root itself
        RebuildVerb.IsWithinRoot(root, Path.Combine(root, "..", "evil")).Should().BeFalse(); // `..` escape
        RebuildVerb.IsWithinRoot(root, Path.Combine(Path.GetTempPath(), "other")).Should().BeFalse();
    }

    // ---- Exec.WinCmdArguments (Windows .cmd delegation hardening) ----

    [TestMethod]
    public void Win_cmd_arguments_caret_escape_metacharacters()
    {
        var line = Exec.WinCmdArguments("rig-node.cmd", ["[suggest:5]", "a & echo pwned"]);
        line.Should().StartWith("/d /s /c \"");
        line.Should().Contain("^^^&");        // the ampersand is caret-escaped…
        line.Should().NotContain(" & echo");  // …so it can't run as its own command

        // Exact escaping must match the Node tool's winCmdInvocation (whose output
        // is validated against real cmd.exe by a win32 integration test).
        Exec.WinCmdArguments("x.cmd", ["a&b"]).Should().Be("/d /s /c \"x.cmd ^^^\"a^^^&b^^^\"\"");
    }

    // ---- KillVerb.ResolvePatterns ----

    [TestMethod]
    public void Kill_prefers_configured_match()
    {
        var cfg = new RigConfig { Kill = new KillConfig { Match = ["MyApp"] }, DefaultProject = "Other" };
        KillVerb.ResolvePatterns(cfg, [Exe("App")]).Should().BeEquivalentTo("MyApp");
    }

    [TestMethod]
    public void Kill_with_no_arg_sweeps_every_runnable_project()
    {
        // The "stop everything I started" sweep — all runnables, libs/tests excluded.
        KillVerb.ResolvePatterns(new RigConfig(), [Exe("App"), Exe("MicaSpike"), Lib("Core")])
            .Should().BeEquivalentTo("App", "MicaSpike");
        // defaultProject no longer narrows a bare kill.
        KillVerb.ResolvePatterns(new RigConfig { DefaultProject = "App" }, [Exe("App"), Exe("MicaSpike")])
            .Should().BeEquivalentTo("App", "MicaSpike");
        KillVerb.ResolvePatterns(new RigConfig(), [Lib("OnlyLib")])
            .Should().BeEmpty();
    }

    [TestMethod]
    public void Kill_with_arg_targets_the_named_project()
    {
        var projects = new[] { Exe("Acme.App"), Exe("Acme.MicaSpike"), Lib("Core") };

        // short-name exact, then substring; a raw non-match is honored as-is.
        KillVerb.ResolvePatterns(new RigConfig(), projects, "MicaSpike")
            .Should().BeEquivalentTo("Acme.MicaSpike");
        KillVerb.ResolvePatterns(new RigConfig(), projects, "Acme")
            .Should().BeEquivalentTo("Acme.App", "Acme.MicaSpike");
        KillVerb.ResolvePatterns(new RigConfig(), projects, "ghost")
            .Should().BeEquivalentTo("ghost");

        // config kill.match still wins over an arg.
        KillVerb.ResolvePatterns(new RigConfig { Kill = new KillConfig { Match = ["Pinned"] } }, projects, "MicaSpike")
            .Should().BeEquivalentTo("Pinned");
    }

    [TestMethod]
    public void Kill_pattern_is_the_project_name_not_the_assembly_name()
    {
        var app = new ProjectInfo("App", "App/App.csproj", "/r/App/App.csproj",
            "Exe", "net8.0", IsTest: false, AssemblyName: "AcmeApp");

        // Both platforms match the full command line, so the (narrower) project name
        // is the target — present in the `dotnet run --project` cmdline and the
        // apphost path — never the AssemblyName.
        KillVerb.ResolvePatterns(new RigConfig { DefaultProject = "App" }, [app])
            .Should().BeEquivalentTo("App");
        KillVerb.ResolvePatterns(new RigConfig(), [app])
            .Should().BeEquivalentTo("App");
    }

    // ---- KillVerb command-line matching (Windows CIM) ----

    [TestMethod]
    public void Kill_parses_tab_delimited_pid_and_command_line()
    {
        // PID<tab>CommandLine, CRLF endings, a command-line-less system process, and
        // a blank line — exactly the shape the CIM query emits.
        var output = "1001\tC:\\dotnet.exe run --project C:\\src\\App\\App.csproj\r\n" +
                     "1002\tC:\\src\\App\\bin\\Debug\\net8.0\\App.exe\r\n" +
                     "4\t\r\n" +
                     "\r\n";
        var procs = KillVerb.ParseProcessList(output);

        procs.Should().HaveCount(3);
        procs[0].Should().Be((1001, "C:\\dotnet.exe run --project C:\\src\\App\\App.csproj"));
        procs[2].Should().Be((4, string.Empty)); // system process, empty command line
    }

    [TestMethod]
    public void Kill_matches_driver_and_apphost_but_not_self_or_unrelated()
    {
        var procs = new List<(int, string)>
        {
            (1001, "C:\\dotnet.exe run --project C:\\src\\App\\App.csproj"), // the run/watch driver
            (1002, "C:\\src\\App\\bin\\Debug\\net8.0\\App.exe"),             // the apphost
            (1003, "C:\\dotnet.exe run --project C:\\src\\Other\\Other.csproj"),
            (4,    ""),                                                       // system process
            (777,  "rig kill App"),                                          // ourselves
        };

        var matched = KillVerb.MatchProcesses(procs, "App", selfPid: 777)
            .Select(p => p.Pid).ToList();

        matched.Should().BeEquivalentTo([1001, 1002]); // driver + apphost; not Other, system, or self
    }

    // ---- KillVerb --port PID parsing ----

    [TestMethod]
    public void Kill_parses_lsof_pids_unique_sorted_dropping_self()
    {
        // `lsof -ti` emits one PID per line; ports can be shared (fork model) so dupes appear.
        KillVerb.ParsePids("3201\n3187\n3201\n", selfPid: 999)
            .Should().Equal(3187, 3201);
        // Our own PID and junk tokens are dropped.
        KillVerb.ParsePids("777 888 \n abc -1 0", selfPid: 777)
            .Should().Equal(888);
        KillVerb.ParsePids("", selfPid: 1).Should().BeEmpty();
    }

    [TestMethod]
    public void Kill_parses_netstat_listening_pids_only()
    {
        // `netstat -ano -p tcp | findstr :3000` — LISTENING rows carry the owning PID last;
        // ESTABLISHED rows (a client connection) must not be killed.
        var output =
            "  TCP    0.0.0.0:3000      0.0.0.0:0        LISTENING       4321\r\n" +
            "  TCP    [::]:3000         [::]:0           LISTENING       4321\r\n" +
            "  TCP    127.0.0.1:3000    127.0.0.1:54012  ESTABLISHED     8888\r\n";
        KillVerb.ParseNetstatPids(output, selfPid: 999).Should().Equal(4321);
        KillVerb.ParseNetstatPids(output, selfPid: 4321).Should().BeEmpty();
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
    public void Publish_args_reflect_configuration_self_contained_and_single_file()
    {
        var args = PublishVerb.BuildArgs("/r/App/App.csproj", "Debug", "win-x64",
            selfContained: false, singleFile: true, "/r/dist/win-x64");

        args.Should().ContainInOrder("publish", "/r/App/App.csproj", "-c", "Debug", "-r", "win-x64");
        args.Should().ContainInConsecutiveOrder("--self-contained", "false");
        args.Should().Contain("-p:PublishSingleFile=true");
    }

    // ---- AddVerb.ResolveTarget ----

    [TestMethod]
    public void Add_targets_default_then_sole_then_ambiguous()
    {
        // default project wins (and add spans libs too, not just runnables)
        AddVerb.ResolveTarget([Exe("App"), Lib("Core")], query: null, defaultProject: "Core")
            .Selected!.Name.Should().Be("Core");

        // single project → chosen with no prompt
        AddVerb.ResolveTarget([Lib("OnlyLib")], null, null).Selected!.Name.Should().Be("OnlyLib");

        // several, no default → ambiguous (caller prompts / errors)
        var ambiguous = AddVerb.ResolveTarget([Exe("App"), Lib("Core")], null, null);
        ambiguous.Selected.Should().BeNull();
        ambiguous.Ambiguous.Should().HaveCount(2);

        // explicit query that doesn't match → error
        AddVerb.ResolveTarget([Exe("App")], "nope", null).Error.Should().NotBeNull();
    }

    // ---- ni-parity dependency verbs (RemoveVerb / GlobalVerb / DlxVerb) ----

    [TestMethod]
    public void Remove_builds_dotnet_remove_package_args()
    {
        RemoveVerb.BuildArgs("/repo/App/App.csproj", "Newtonsoft.Json", [])
            .Should().ContainInConsecutiveOrder("remove", "/repo/App/App.csproj", "package", "Newtonsoft.Json");
        // RemoveVerb reuses AddVerb.ResolveTarget (covered above) for project selection.
        RemoveVerb.BuildArgs("/p.csproj", "Pkg", ["--interactive"]).Should().EndWith("--interactive");
    }

    [TestMethod]
    public void Global_builds_dotnet_tool_install_global_args()
    {
        GlobalVerb.BuildArgs("dotnet-ef", [])
            .Should().ContainInConsecutiveOrder("tool", "install", "--global", "dotnet-ef");
        GlobalVerb.BuildArgs("dotnet-ef", ["--version", "9.0.0"]).Should().ContainInConsecutiveOrder("--version", "9.0.0");
    }

    [TestMethod]
    public void Dlx_builds_dnx_args_tool_first()
    {
        DlxVerb.BuildArgs("dotnetsay", []).Should().ContainSingle().Which.Should().Be("dotnetsay");
        DlxVerb.BuildArgs("dotnetsay", ["hello", "world"])
            .Should().ContainInConsecutiveOrder("dotnetsay", "hello", "world");
    }

    [TestMethod]
    public void Dlx_dnx_availability_check_does_not_throw()
    {
        // Pure PATH probe — result depends on the machine, but it must never throw.
        var act = () => DlxVerb.DnxAvailable();
        act.Should().NotThrow();
    }

    // ---- UpdateVerb (version comparison) ----

    [TestMethod]
    public void Update_latest_stable_ignores_prereleases()
    {
        UpdateVerb.LatestStable(["0.1.0", "1.0.0", "1.1.0", "1.2.0-beta", "0.9.0"]).Should().Be("1.1.0");
        UpdateVerb.LatestStable(["2.0.0-rc1", "1.5.0"]).Should().Be("1.5.0");
        UpdateVerb.LatestStable(["1.0.0-alpha"]).Should().BeNull(); // only prereleases
        UpdateVerb.LatestStable([]).Should().BeNull();
    }

    [TestMethod]
    public void Update_is_newer_compares_and_treats_unknown_current_as_outdated()
    {
        UpdateVerb.IsNewer("1.0.0", "1.1.0").Should().BeTrue();
        UpdateVerb.IsNewer("1.1.0", "1.1.0").Should().BeFalse();
        UpdateVerb.IsNewer("1.2.0", "1.1.0").Should().BeFalse();
        UpdateVerb.IsNewer("1.1.0+abc123", "1.2.0").Should().BeTrue(); // build metadata stripped
        UpdateVerb.IsNewer(null, "1.1.0").Should().BeTrue();           // unknown current → offer update
        UpdateVerb.IsNewer("1.0.0", "garbage").Should().BeFalse();     // unparseable latest → no
    }

    [TestMethod]
    public void Update_sibling_args_always_carry_self_only()
    {
        // The cross-update hands off with --self-only so the sibling never bounces
        // back and re-updates us (infinite mutual recursion).
        UpdateVerb.SiblingArgs(check: false).Should().Equal("self-update", "--self-only");
        UpdateVerb.SiblingArgs(check: true).Should().Equal("self-update", "--check", "--self-only");
    }

    // ---- OutdatedVerb.BuildArgs ----

    [TestMethod]
    public void Outdated_args_default_to_outdated_lens_with_solution()
    {
        var args = OutdatedVerb.BuildArgs("/r/App.slnx", vulnerable: false, deprecated: false,
            transitive: false, prerelease: false, forwarded: []);
        args.Should().ContainInConsecutiveOrder("list", "/r/App.slnx", "package", "--outdated");
    }

    [TestMethod]
    public void Outdated_lenses_are_mutually_exclusive_and_prerelease_is_outdated_only()
    {
        // vulnerable wins over the default outdated; prerelease is dropped (not valid there)
        var vuln = OutdatedVerb.BuildArgs(null, vulnerable: true, deprecated: false,
            transitive: true, prerelease: true, forwarded: []);
        vuln.Should().Contain("--vulnerable").And.Contain("--include-transitive");
        vuln.Should().NotContain("--outdated").And.NotContain("--include-prerelease");

        // prerelease applies on the default outdated lens
        OutdatedVerb.BuildArgs(null, false, false, false, prerelease: true, forwarded: [])
            .Should().Contain("--include-prerelease");
    }

    [TestMethod]
    public void Test_and_build_args_carry_configuration()
    {
        TestVerb.BuildTestArgs(TestPlatform.Runner.VsTest, "/r/T/T.csproj", filter: null, framework: null, forwarded: [], watch: false, configuration: "Release")
            .Should().ContainInConsecutiveOrder("-c", "Release");
    }
}
