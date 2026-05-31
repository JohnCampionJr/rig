namespace Rig.Tests;

[TestClass]
public sealed class RigConfigTests
{
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
              "coverage": { "settings": "cov.runsettings", "collector": "auto", "license": "KEY" },
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
        cfg.Kill!.Match.Should().ContainSingle().Which.Should().Be("App.Desktop");
        cfg.Rebuild!.Skip.Should().BeEquivalentTo("vendor", "node_modules");
        cfg.Publish!.Rid.Should().Be("osx-arm64");
        cfg.Publish.SelfContained.Should().BeTrue();
        cfg.Env!["GLOBAL"].Should().Be("g");
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
