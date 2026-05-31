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
}
