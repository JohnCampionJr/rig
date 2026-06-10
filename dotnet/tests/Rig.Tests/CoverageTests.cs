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
    public void ResolveOptions_lets_cli_flags_win_over_config_defaults()
    {
        var cfg = new CoverageConfig { Open = true, Full = true, Min = 70 };

        // config supplies the defaults when the CLI doesn't pass the flag
        CoverageVerb.ResolveOptions(cliFull: false, cliOpen: false, cliMin: null, cfg)
            .Should().Be((true, true, (double?)70));

        // an explicit --min overrides the config default; bool flags only add
        CoverageVerb.ResolveOptions(cliFull: false, cliOpen: false, cliMin: 90, cfg)
            .Should().Be((true, true, (double?)90));

        // no config → CLI values pass through untouched
        CoverageVerb.ResolveOptions(cliFull: true, cliOpen: false, cliMin: null, config: null)
            .Should().Be((true, false, (double?)null));
    }

    [TestMethod]
    public void Runner_respects_explicit_config()
    {
        TestPlatform.Detect(root: "/nowhere", "mtp").Should().Be(TestPlatform.Runner.Mtp);
        TestPlatform.Detect(root: "/nowhere", "xplat").Should().Be(TestPlatform.Runner.VsTest);
        TestPlatform.Detect(root: "/nowhere", "vstest").Should().Be(TestPlatform.Runner.VsTest);
    }

    [TestMethod]
    public void Runner_auto_detects_mtp_from_global_json()
    {
        // The CLI grammar is selected SOLELY by global.json's test.runner — csproj
        // MTP props do not switch it (verified against the SDK: MSB1001 without it).
        using var mtp = new TempDir();
        mtp.Write("global.json", """{ "test": { "runner": "Microsoft.Testing.Platform" } }""");
        TestPlatform.Detect(mtp.Path, configured: null).Should().Be(TestPlatform.Runner.Mtp);

        using var vstest = new TempDir();
        vstest.Write("global.json", """{ "sdk": { "version": "10.0.100" } }""");
        TestPlatform.Detect(vstest.Path, configured: null).Should().Be(TestPlatform.Runner.VsTest);

        using var none = new TempDir();
        TestPlatform.Detect(none.Path, configured: null).Should().Be(TestPlatform.Runner.VsTest);
    }

    [TestMethod]
    public void Collect_args_differ_by_runner()
    {
        // MTP: --project + coverage requested after the `--` boundary.
        var mtp = CoverageVerb.BuildCollectArgs(TestPlatform.Runner.Mtp, "/r/T/T.csproj", "/r/res", settings: null);
        mtp.Should().ContainInConsecutiveOrder("test", "--project", "/r/T/T.csproj");
        mtp.Should().ContainInConsecutiveOrder("--coverage", "--coverage-output-format", "cobertura");

        // VSTest: positional project (no --project) + the XPlat collector.
        var vstest = CoverageVerb.BuildCollectArgs(TestPlatform.Runner.VsTest, "/r/T/T.csproj", "/r/res", settings: "cov.runsettings");
        vstest.Should().ContainInConsecutiveOrder("test", "/r/T/T.csproj");
        vstest.Should().NotContain("--project");
        vstest.Should().Contain("--collect:\"XPlat Code Coverage\"");
        vstest.Should().ContainInConsecutiveOrder("--settings", "cov.runsettings");
    }

    [TestMethod]
    public void Collect_args_include_filter_when_scoped()
    {
        var args = CoverageVerb.BuildCollectArgs(
            TestPlatform.Runner.Mtp, "/r/T/T.csproj", "/r/res", settings: null, filter: "FullyQualifiedName~Foo");
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
