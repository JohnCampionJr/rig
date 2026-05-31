namespace Rig.Tests;

[TestClass]
public sealed class ProjectDiscoveryTests
{
    private static string ExeCsproj(string tfm = "net8.0") => $"""
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <OutputType>Exe</OutputType>
            <TargetFramework>{tfm}</TargetFramework>
          </PropertyGroup>
        </Project>
        """;

    private static string LibCsproj() => """
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup><TargetFramework>net8.0</TargetFramework></PropertyGroup>
        </Project>
        """;

    private static string TestCsproj() => """
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup><TargetFramework>net8.0</TargetFramework></PropertyGroup>
          <ItemGroup>
            <PackageReference Include="Microsoft.NET.Test.Sdk" Version="18.0.0" />
          </ItemGroup>
        </Project>
        """;

    [TestMethod]
    public void Discovers_and_classifies_from_an_slnx()
    {
        using var t = new TempDir();
        t.Write("App/App.csproj", ExeCsproj("net9.0"));
        t.Write("Lib/Lib.csproj", LibCsproj());
        t.Write("App.Tests/App.Tests.csproj", TestCsproj());
        t.Write("App.slnx", """
            <Solution>
              <Project Path="App/App.csproj" />
              <Project Path="Lib/Lib.csproj" />
              <Project Path="App.Tests/App.Tests.csproj" />
            </Solution>
            """);

        var projects = ProjectDiscovery.Discover(t.Path, configuredSolution: null);

        projects.Should().HaveCount(3);

        var app = projects.Single(p => p.Name == "App");
        app.IsRunnable.Should().BeTrue();
        app.IsTest.Should().BeFalse();
        app.Tfm.Should().Be("net9.0");

        projects.Single(p => p.Name == "Lib").IsRunnable.Should().BeFalse();

        var tests = projects.Single(p => p.Name == "App.Tests");
        tests.IsTest.Should().BeTrue();          // via Microsoft.NET.Test.Sdk reference
        tests.IsRunnable.Should().BeFalse();
    }

    [TestMethod]
    public void Parses_classic_sln_project_lines()
    {
        using var t = new TempDir();
        t.Write("App/App.csproj", ExeCsproj());
        t.Write("App.sln", """
            Microsoft Visual Studio Solution File, Format Version 12.00
            Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "App", "App\App.csproj", "{11111111-1111-1111-1111-111111111111}"
            EndProject
            Global
            EndGlobal
            """);

        var projects = ProjectDiscovery.Discover(t.Path, configuredSolution: null);

        projects.Should().ContainSingle();
        projects[0].Name.Should().Be("App");
        projects[0].IsRunnable.Should().BeTrue();
    }

    [TestMethod]
    public void Test_project_detected_by_name_convention()
    {
        using var t = new TempDir();
        // A *Tests project with no test-sdk reference still classifies as a test.
        t.Write("Foo.Tests/Foo.Tests.csproj", LibCsproj());
        t.Write("App.slnx", """
            <Solution><Project Path="Foo.Tests/Foo.Tests.csproj" /></Solution>
            """);

        var projects = ProjectDiscovery.Discover(t.Path, null);
        projects.Single().IsTest.Should().BeTrue();
    }

    [TestMethod]
    public void Falls_back_to_scanning_csproj_when_no_solution()
    {
        using var t = new TempDir();
        t.Write("App/App.csproj", ExeCsproj());
        // a bin/ artifact that must be ignored
        t.Write("App/bin/Debug/Ghost.csproj", ExeCsproj());

        var projects = ProjectDiscovery.Discover(t.Path, null);

        projects.Should().ContainSingle();
        projects[0].Name.Should().Be("App");
    }

    [TestMethod]
    public void Configured_solution_override_is_honoured()
    {
        using var t = new TempDir();
        t.Write("App/App.csproj", ExeCsproj());
        t.Write("custom.slnx", """<Solution><Project Path="App/App.csproj" /></Solution>""");

        var projects = ProjectDiscovery.Discover(t.Path, configuredSolution: "custom.slnx");
        projects.Should().ContainSingle().Which.Name.Should().Be("App");
    }
}
