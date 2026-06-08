namespace Rig.Tests;

[TestClass]
public sealed class RigConfigTests
{
    [TestMethod]
    public void Unknown_keys_are_detected_with_a_suggestion()
    {
        var unknown = RigConfig.UnknownKeys("""
            { "defualtProject": "App", "test": {}, "totallyMadeUp": 1 }
            """);

        unknown.Should().HaveCount(2);
        unknown.Should().ContainSingle(u => u.Key == "defualtProject").Which.Suggestion.Should().Be("defaultProject");
        // a far-off key still reports, but without a (misleading) suggestion
        unknown.Should().ContainSingle(u => u.Key == "totallyMadeUp").Which.Suggestion.Should().BeNull();
    }

    [TestMethod]
    public void Known_keys_produce_no_warnings()
    {
        RigConfig.UnknownKeys("""{ "$schema": "x", "solution": "a.slnx", "aliases": {} }""")
            .Should().BeEmpty();
    }

    [TestMethod]
    public void Merge_lets_the_repo_win_per_key_and_unions_dictionaries()
    {
        var global = RigConfig.Parse("""
            {
              "defaultProject": "GlobalApp",
              "env": { "SHARED": "g", "ONLY_GLOBAL": "g" },
              "aliases": { "coverage": "cov", "publish": "ship" }
            }
            """);
        var repo = RigConfig.Parse("""
            {
              "defaultProject": "RepoApp",
              "env": { "SHARED": "r", "ONLY_REPO": "r" },
              "aliases": { "coverage": "c" }
            }
            """);

        var merged = RigConfig.Merge(global, repo);

        merged.DefaultProject.Should().Be("RepoApp");                 // repo wins
        merged.Env!["SHARED"].Should().Be("r");                       // repo wins per key
        merged.Env["ONLY_GLOBAL"].Should().Be("g");                   // global preserved
        merged.Env["ONLY_REPO"].Should().Be("r");                     // repo added
        merged.Aliases!["coverage"].Should().Be("c");                 // repo override
        merged.Aliases["publish"].Should().Be("ship");                // global-only kept
    }

    [TestMethod]
    public void Merge_unions_exclude_lists_and_repo_quiet_wins()
    {
        var global = RigConfig.Parse("""{ "exclude": ["*Bench"], "quiet": true }""");
        var repo = RigConfig.Parse("""{ "exclude": ["*.Demo", "*Bench"] }""");

        var merged = RigConfig.Merge(global, repo);

        merged.Exclude.Should().BeEquivalentTo("*Bench", "*.Demo"); // union, de-duped
        merged.Quiet.Should().BeTrue();                            // inherited from global (repo unset)
    }

    [TestMethod]
    public void Merge_blank_repo_license_does_not_shadow_the_global_one()
    {
        // The repo's scaffolded `coverage.license: ""` must fall through to the
        // real key set once in ~/.rig.json — the whole point of a global config.
        var global = RigConfig.Parse("""{ "coverage": { "license": "PRO-KEY" } }""");
        var repo = RigConfig.Parse("""{ "coverage": { "license": "", "collector": "mtp" } }""");

        var merged = RigConfig.Merge(global, repo);

        merged.Coverage!.License.Should().Be("PRO-KEY"); // blank "" treated as unset
        merged.Coverage.Collector.Should().Be("mtp");    // repo's real value still wins
    }

    [TestMethod]
    public void Empty_whitespace_or_malformed_config_degrades_to_defaults_without_throwing()
    {
        RigConfig.Parse("").DefaultProject.Should().BeNull();
        RigConfig.Parse("   \n ").DefaultProject.Should().BeNull();

        using var t = new TempDir();
        RigConfig.Load(t.Write(".rig.json", "")).DefaultProject.Should().BeNull();     // 0-byte file
        RigConfig.Load(t.Write("bad.json", "{ not json")).DefaultProject.Should().BeNull(); // malformed
    }

    [TestMethod]
    public void Missing_file_yields_defaults()
    {
        var cfg = RigConfig.Load("/no/such/file.json");
        cfg.Should().NotBeNull();
        cfg.DefaultProject.Should().BeNull();
        cfg.Commands.Should().BeNull();
    }

    [TestMethod]
    public void Parses_full_schema_with_jsonc_comments_and_trailing_commas()
    {
        var cfg = RigConfig.Parse("""
            {
              // a JSONC comment
              "$schema": "ignored",
              "solution": "App.slnx",
              "defaultProject": "App.Desktop",
              "test": {
                "project": "tests/App.Tests/App.Tests.csproj",
                "envPresets": { "log": { "APP_LOG": "1" } }
              },
              "coverage": { "settings": "cov.runsettings", "collector": "auto", "license": "KEY", "open": true, "full": false, "min": 80 },
              "kill": { "match": ["App.Desktop"] },
              "rebuild": { "skip": ["vendor", "node_modules"] },
              "publish": { "rid": "osx-arm64", "selfContained": true, "singleFile": false, "output": "dist/{rid}" },
              "env": { "GLOBAL": "g" },
            }
            """);

        cfg.Solution.Should().Be("App.slnx");
        cfg.DefaultProject.Should().Be("App.Desktop");
        cfg.Test!.Project.Should().Be("tests/App.Tests/App.Tests.csproj");
        cfg.Test.EnvPresets!["log"]["APP_LOG"].Should().Be("1");
        cfg.Coverage!.License.Should().Be("KEY");
        cfg.Coverage.Collector.Should().Be("auto");
        cfg.Coverage.Open.Should().BeTrue();
        cfg.Coverage.Full.Should().BeFalse();
        cfg.Coverage.Min.Should().Be(80);
        cfg.Kill!.Match.Should().ContainSingle().Which.Should().Be("App.Desktop");
        cfg.Rebuild!.Skip.Should().BeEquivalentTo("vendor", "node_modules");
        cfg.Publish!.Rid.Should().Be("osx-arm64");
        cfg.Publish.SelfContained.Should().BeTrue();
        cfg.Env!["GLOBAL"].Should().Be("g");
    }

    [TestMethod]
    public void Folds_the_dotnet_namespace_and_top_level_envPresets_onto_canonical_fields()
    {
        var cfg = RigConfig.Parse("""
            {
              "defaultProject": "App.Desktop",
              "envPresets": { "log": { "APP_LOG": "1" } },
              "coverage": { "open": true, "min": 80 },
              "dotnet": {
                "solution": "App.slnx",
                "test": { "project": "tests/App.Tests/App.Tests.csproj" },
                "coverage": { "settings": "cov.runsettings", "collector": "auto", "license": "KEY" },
                "rebuild": { "skip": ["vendor"] },
                "publish": { "rid": "osx-arm64", "selfContained": true }
              }
            }
            """);

        // dotnet.* folds onto the canonical top-level fields verbs read.
        cfg.Solution.Should().Be("App.slnx");
        cfg.Test!.Project.Should().Be("tests/App.Tests/App.Tests.csproj");
        cfg.Coverage!.Settings.Should().Be("cov.runsettings");
        cfg.Coverage.Collector.Should().Be("auto");
        cfg.Coverage.License.Should().Be("KEY");
        // shared coverage knobs stay top-level.
        cfg.Coverage.Open.Should().BeTrue();
        cfg.Coverage.Min.Should().Be(80);
        cfg.Rebuild!.Skip.Should().BeEquivalentTo("vendor");
        cfg.Publish!.Rid.Should().Be("osx-arm64");
        cfg.Publish.SelfContained.Should().BeTrue();
        // top-level envPresets folds onto test.envPresets.
        cfg.Test.EnvPresets!["log"]["APP_LOG"].Should().Be("1");
        // the transient namespace is consumed.
        cfg.Dotnet.Should().BeNull();
    }

    [TestMethod]
    public void Dotnet_namespace_wins_over_legacy_top_level_keys()
    {
        var cfg = RigConfig.Parse("""
            {
              "solution": "Legacy.slnx",
              "dotnet": { "solution": "New.slnx" }
            }
            """);

        cfg.Solution.Should().Be("New.slnx");
    }

    [TestMethod]
    public void A_node_namespace_is_ignored_not_flagged_as_unknown()
    {
        var json = """{ "node": { "anything": true }, "defaultProject": "App" }""";

        RigConfig.UnknownKeys(json).Should().BeEmpty();
        RigConfig.Parse(json).DefaultProject.Should().Be("App");
    }

    [TestMethod]
    public void Command_string_form_is_a_shell_command()
    {
        var cfg = RigConfig.Parse("""{ "commands": { "deploy": "./deploy.sh --prod" } }""");

        var cmd = cfg.Commands!["deploy"];
        cmd.Resolve()!.IsShell.Should().BeTrue();
        cmd.Resolve()!.Shell.Should().Be("./deploy.sh --prod");
    }

    [TestMethod]
    public void Command_array_form_bypasses_the_shell()
    {
        var cfg = RigConfig.Parse("""{ "commands": { "fmt": ["dotnet", "csharpier", "."] } }""");

        var spec = cfg.Commands!["fmt"].Resolve()!;
        spec.IsShell.Should().BeFalse();
        spec.Argv.Should().BeEquivalentTo("dotnet", "csharpier", ".");
    }

    [TestMethod]
    public void Command_object_form_with_description_env_and_cwd()
    {
        var cfg = RigConfig.Parse("""
            {
              "commands": {
                "release": {
                  "description": "Cut a release",
                  "command": "./release.sh",
                  "env": { "CI": "true" },
                  "cwd": "scripts"
                }
              }
            }
            """);

        var def = cfg.Commands!["release"];
        def.Description.Should().Be("Cut a release");
        def.Cwd.Should().Be("scripts");
        def.Env!["CI"].Should().Be("true");
        def.Resolve()!.Shell.Should().Be("./release.sh");
    }

    [TestMethod]
    public void Command_object_resolves_per_os_override()
    {
        var cfg = RigConfig.Parse("""
            {
              "commands": {
                "package": {
                  "os": {
                    "macos": "./build-mac.sh",
                    "windows": ["pwsh", "build.ps1"],
                    "linux": "./build-linux.sh"
                  }
                }
              }
            }
            """);

        var resolved = cfg.Commands!["package"].Resolve()!;

        if (OperatingSystem.IsMacOS())
            resolved.Shell.Should().Be("./build-mac.sh");
        else if (OperatingSystem.IsWindows())
            resolved.Argv.Should().BeEquivalentTo("pwsh", "build.ps1");
        else
            resolved.Shell.Should().Be("./build-linux.sh");
    }
}
