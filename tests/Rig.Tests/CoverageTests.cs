namespace Rig.Tests;

[TestClass]
public sealed class CoverageTests
{
    [TestMethod]
    public void MeetsMinimum_gates_line_coverage()
    {
        // first arg = line rate (0–1), second = required percent
        CoverageVerb.MeetsMinimum(0.75, null).Should().BeTrue();   // no gate
        CoverageVerb.MeetsMinimum(0.75, 70).Should().BeTrue();     // 75 >= 70
        CoverageVerb.MeetsMinimum(0.75, 80).Should().BeFalse();    // 75 < 80
        CoverageVerb.MeetsMinimum(0.80, 80).Should().BeTrue();     // boundary
        CoverageVerb.MeetsMinimum(null, 80).Should().BeFalse();    // unreadable → can't meet
    }

    [TestMethod]
    public void Collector_respects_explicit_config()
    {
        CoverageVerb.DetectCollector(null, "mtp").Should().Be(CoverageVerb.CollectorMode.Mtp);
        CoverageVerb.DetectCollector(null, "xplat").Should().Be(CoverageVerb.CollectorMode.VsTest);
    }

    [TestMethod]
    public void Collector_auto_detects_mtp_from_project_signals()
    {
        using var t = new TempDir();
        var mtp = t.Write("Mtp/Mtp.csproj", """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><EnableMicrosoftTestingPlatform>true</EnableMicrosoftTestingPlatform></PropertyGroup>
            </Project>
            """);
        var vstest = t.Write("Vs/Vs.csproj", """
            <Project Sdk="Microsoft.NET.Sdk"><PropertyGroup/></Project>
            """);

        CoverageVerb.DetectCollector(mtp, configured: null).Should().Be(CoverageVerb.CollectorMode.Mtp);
        CoverageVerb.DetectCollector(vstest, configured: null).Should().Be(CoverageVerb.CollectorMode.VsTest);
    }

    [TestMethod]
    public void Collect_args_differ_by_mode()
    {
        var mtp = CoverageVerb.BuildCollectArgs(CoverageVerb.CollectorMode.Mtp, "/r/T/T.csproj", "/r/res", settings: null);
        mtp.Should().ContainInConsecutiveOrder("--coverage", "--coverage-output-format", "cobertura");

        var vstest = CoverageVerb.BuildCollectArgs(CoverageVerb.CollectorMode.VsTest, "/r/T/T.csproj", "/r/res", settings: "cov.runsettings");
        vstest.Should().Contain("--collect:\"XPlat Code Coverage\"");
        vstest.Should().ContainInConsecutiveOrder("--settings", "cov.runsettings");
    }

    [TestMethod]
    public void Collect_args_include_filter_when_scoped()
    {
        var args = CoverageVerb.BuildCollectArgs(
            CoverageVerb.CollectorMode.Mtp, "/r/T/T.csproj", "/r/res", settings: null, filter: "FullyQualifiedName~Foo");
        args.Should().ContainInConsecutiveOrder("--filter", "FullyQualifiedName~Foo");
    }

    [TestMethod]
    public void ReadRates_parses_line_and_branch_from_the_cobertura_root()
    {
        using var t = new TempDir();
        var path = t.Write("c.cobertura.xml",
            """<?xml version="1.0"?><coverage line-rate="0.42" branch-rate="0.25" version="1.9" timestamp="0"><packages/></coverage>""");

        var (line, branch) = CoverageVerb.ReadRates(path);
        line.Should().Be(0.42);
        branch.Should().Be(0.25);
    }

    [TestMethod]
    public void Renders_html_from_cobertura_in_process()
    {
        using var t = new TempDir();
        var cobertura = t.Write("coverage.cobertura.xml", """
            <?xml version="1.0"?>
            <coverage line-rate="0.5" branch-rate="0" version="1.9" timestamp="0"
                      lines-covered="1" lines-valid="2" branches-covered="0" branches-valid="0">
              <sources><source>/src</source></sources>
              <packages>
                <package name="P" line-rate="0.5" branch-rate="0" complexity="0">
                  <classes>
                    <class name="C" filename="C.cs" line-rate="0.5" branch-rate="0" complexity="0">
                      <methods/>
                      <lines>
                        <line number="1" hits="1"/>
                        <line number="2" hits="0"/>
                      </lines>
                    </class>
                  </classes>
                </package>
              </packages>
            </coverage>
            """);
        var outDir = t.Dir("report");

        var index = CoverageVerb.Render(cobertura, outDir, full: false, license: null);

        index.Should().NotBeNull();
        File.Exists(index!).Should().BeTrue();
        File.ReadAllText(index!).Should().Contain("<html", "the rendered report should be HTML");
    }
}
