namespace Rig.Tests;

/// <summary>
/// End-to-end coverage of the orchestration layer: scaffolds a real (trivial)
/// project and runs a verb all the way through to a live `dotnet` invocation.
/// Slower than the unit tests (spawns the SDK), but it exercises Exec + discovery
/// + arg-building together, which the pure-logic tests can't.
/// </summary>
[TestClass]
public sealed class IntegrationTests
{
    [TestMethod]
    public void Build_runs_dotnet_end_to_end_and_produces_output()
    {
        using var t = new TempDir();
        t.Write("App/App.csproj", """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <OutputType>Exe</OutputType>
                <TargetFramework>net8.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);
        t.Write("App/Program.cs", "class Program { static void Main() { } }");
        t.Write("App.slnx", """<Solution><Project Path="App/App.csproj" /></Solution>""");

        var rc = BuildVerb.Execute(new RigSession(t.Path, new RigConfig()), []);

        rc.Should().Be(0, "a trivial console project should build via `rig build`");
        Directory.Exists(Path.Combine(t.Path, "App", "bin")).Should().BeTrue();
    }

    // ---- Custom commands: spawn a real process and verify the wiring ----

    private static RigSession Bare(TempDir t, RigConfig? cfg = null) =>
        new(t.Path, cfg ?? new RigConfig(), useDotEnv: false);

    [TestMethod]
    public void Custom_shell_command_propagates_the_exit_code()
    {
        using var t = new TempDir();
        var cfg = RigConfig.Parse("""{ "commands": { "boom": "exit 3" } }""");

        var rc = CommandVerb.Execute(Bare(t, cfg), "boom", cfg.Commands!["boom"], []);

        rc.Should().Be(3, "a custom shell command's exit code becomes rig's exit code");
    }

    [TestMethod]
    public void Custom_shell_command_appends_passthrough_args()
    {
        using var t = new TempDir();
        var cfg = RigConfig.Parse("""{ "commands": { "code": "exit" } }""");

        // `exit` + passthrough `4` → the shell runs `exit 4`
        var rc = CommandVerb.Execute(Bare(t, cfg), "code", cfg.Commands!["code"], ["4"]);

        rc.Should().Be(4);
    }

    [TestMethod]
    public void Custom_argv_command_execs_directly_and_propagates_exit_code()
    {
        using var t = new TempDir();
        var (file, shArgs) = Exec.ShellInvocation("exit 5");
        var def = new CommandDef { Command = new CommandSpec { Argv = [file, .. shArgs] } };

        var rc = CommandVerb.Execute(Bare(t), "x", def, []);

        rc.Should().Be(5, "argv form bypasses the shell yet still propagates the exit code");
    }

    [TestMethod]
    public void Custom_command_env_reaches_the_child_process()
    {
        using var t = new TempDir();
        // a per-OS expression that exits with the value of an env var rig injects
        var expr = OperatingSystem.IsWindows() ? "exit %RIG_TC%" : "exit $RIG_TC";
        var (file, shArgs) = Exec.ShellInvocation(expr);
        var def = new CommandDef
        {
            Command = new CommandSpec { Argv = [file, .. shArgs] },
            Env = new Dictionary<string, string> { ["RIG_TC"] = "6" },
        };

        var rc = CommandVerb.Execute(Bare(t), "x", def, []);

        rc.Should().Be(6, "per-command env is merged into the spawned process environment");
    }

    [TestMethod]
    public void Custom_command_with_no_spec_for_this_os_errors_cleanly()
    {
        using var t = new TempDir();
        var def = new CommandDef { Os = new Dictionary<string, CommandSpec> { ["plan9"] = new CommandSpec { Shell = "true" } } };

        var rc = CommandVerb.Execute(Bare(t), "x", def, []);

        rc.Should().Be(1, "no entry for the current OS is a clean error, not a crash");
    }
}
