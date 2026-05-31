using System.Xml.Linq;

using Palmmedia.ReportGenerator.Core;

namespace Rig;

/// <summary>
/// `rig coverage [name] [--full] [--open]` — runs tests with coverage, then
/// renders HTML in-process via the bundled ReportGenerator.Core (no install).
/// Collection is runner-aware: MTP test hosts use <c>--coverage</c>; everything
/// else uses VSTest <c>--collect:"XPlat Code Coverage"</c>. Both produce
/// Cobertura. Pure bits (<see cref="DetectCollector"/>, <see cref="Render"/>)
/// are unit-tested.
/// </summary>
internal static class CoverageVerb
{
    private const StringComparison OIC = StringComparison.OrdinalIgnoreCase;

    public enum CollectorMode { Mtp, VsTest }

    public static CollectorMode DetectCollector(string? testProjectPath, string? configured)
    {
        if (string.Equals(configured, "mtp", OIC)) return CollectorMode.Mtp;
        if (string.Equals(configured, "xplat", OIC)) return CollectorMode.VsTest;

        // auto: MTP if the project opts into Microsoft.Testing.Platform.
        if (testProjectPath is not null && File.Exists(testProjectPath))
        {
            try
            {
                var doc = XDocument.Load(testProjectPath);
                bool On(string el) => string.Equals(
                    doc.Descendants(el).FirstOrDefault()?.Value?.Trim(), "true", OIC);
                if (On("EnableMicrosoftTestingPlatform") || On("UseMicrosoftTestingPlatform") || On("EnableMSTestRunner"))
                    return CollectorMode.Mtp;
            }
            catch { /* fall through to VSTest */ }
        }
        return CollectorMode.VsTest;
    }

    public static List<string> BuildCollectArgs(
        CollectorMode mode, string? testProjectPath, string resultsDir, string? settings, string? filter = null)
    {
        var args = new List<string> { "test" };
        if (testProjectPath is not null) { args.Add("--project"); args.Add(testProjectPath); }
        if (!string.IsNullOrEmpty(filter)) { args.Add("--filter"); args.Add(filter); }

        if (mode == CollectorMode.Mtp)
        {
            args.Add("--");
            args.Add("--coverage");
            args.Add("--coverage-output-format");
            args.Add("cobertura");
            if (!string.IsNullOrEmpty(settings)) { args.Add("--coverage-settings"); args.Add(settings); }
        }
        else
        {
            args.Add("--collect:\"XPlat Code Coverage\"");
            args.Add("--results-directory");
            args.Add(resultsDir);
            if (!string.IsNullOrEmpty(settings)) { args.Add("--settings"); args.Add(settings); }
        }
        return args;
    }

    /// <summary>Render a Cobertura report to HTML in-process. Returns the index
    /// path on success. A Pro license, if supplied, is exported for the engine.</summary>
    public static string? Render(string coberturaPath, string targetDir, bool full, string? license)
    {
        if (!File.Exists(coberturaPath)) return null;
        if (!string.IsNullOrWhiteSpace(license))
            Environment.SetEnvironmentVariable("REPORTGENERATOR_LICENSE", license);

        var config = new ReportConfigurationBuilder().Create(new Dictionary<string, string>
        {
            ["reports"] = coberturaPath,
            ["targetdir"] = targetDir,
            ["reporttypes"] = full ? "Html" : "HtmlInline_AzurePipelines",
            ["verbosity"] = "Warning",
        });

        var ok = new Generator().GenerateReport(config);
        var index = Path.Combine(targetDir, "index.html");
        return ok && File.Exists(index) ? index : null;
    }

    /// <summary>Line coverage (0–1) meets the minimum %, or no minimum is set.</summary>
    internal static bool MeetsMinimum(double? lineRate, double? minPercent) =>
        minPercent is not { } min || (lineRate is { } rate && rate * 100 >= min);

    /// <summary>Fold the per-repo/global <c>coverage</c> defaults into the CLI
    /// flags: a passed flag always wins, config supplies the default otherwise.</summary>
    public static (bool Full, bool Open, double? Min) ResolveOptions(
        bool cliFull, bool cliOpen, double? cliMin, CoverageConfig? config) =>
        (cliFull || config?.Full == true,
         cliOpen || config?.Open == true,
         cliMin ?? config?.Min);

    public static int Execute(RigSession session, string? name, bool full, bool open, double? min = null)
    {
        (full, open, min) = ResolveOptions(full, open, min, session.Config.Coverage);
        ProjectDiscovery.WarnMultipleSolutions(session.Root, session.Config.Solution);
        var projects = ProjectDiscovery.Discover(session.Root, session.Config.Solution, session.Config.Exclude);
        var testProject = TestVerb.ResolveTestProject(session, projects);
        if (testProject is null)
        {
            Ui.Error("No test project found to measure coverage. Add one, or set test.project in .rig.json.");
            return 1;
        }

        // Work under obj/rig/ — gitignored in every .NET repo, so no root clutter.
        var workDir = Path.Combine(session.Root, "obj", "rig");
        var resultsDir = Path.Combine(workDir, "coverage-results");
        var mode = DetectCollector(testProject, session.Config.Coverage?.Collector);
        var settings = ResolveSettings(session, testProject);
        var filter = name is null ? null : (TestVerb.ShorthandFilter(name) ?? $"FullyQualifiedName~{name}");
        var args = BuildCollectArgs(mode, testProject, resultsDir, settings, filter);

        Ui.Command("dotnet", args);
        if (Exec.DryRun) return 0; // nothing to report on; stop after showing the command
        var rc = Exec.Run("dotnet", args, session.Root, session.BuildEnv());
        if (rc != 0) return rc;

        var cobertura = FindNewestCobertura(session.Root, resultsDir, testProject);
        if (cobertura is null)
        {
            Ui.Error("Coverage ran but no Cobertura report was found.");
            return 1;
        }

        var targetDir = Path.Combine(workDir, "coverage-report");
        var index = Render(cobertura, targetDir, full, session.Config.Coverage?.License);
        if (index is null)
        {
            Ui.Error("ReportGenerator did not produce a report.");
            return 1;
        }

        var (line, branch) = ReadRates(cobertura);
        if (line is not null)
            Ui.Success($"Coverage: line {Pct(line.Value)} · branch {Pct(branch ?? 0)}");
        Ui.Success($"Report: {index}");
        if (open) Exec.OpenPath(index);

        // Threshold gate (line coverage). Non-zero exit if below — useful in CI / pre-push.
        if (min is { } threshold)
        {
            if (line is not { } lineRate)
            {
                Ui.Error($"--min {threshold:0.#}: could not read line coverage from the report.");
                return 1;
            }
            if (!MeetsMinimum(line, min))
            {
                Ui.Error($"Line coverage {Pct(lineRate)} is below the required minimum of {threshold:0.#}%.");
                return 1;
            }
            Ui.Success($"Line coverage {Pct(lineRate)} meets the {threshold:0.#}% minimum.");
        }
        return 0;
    }

    /// <summary>The overall line/branch rates from a Cobertura root (0–1), or null.</summary>
    internal static (double? Line, double? Branch) ReadRates(string coberturaPath)
    {
        try
        {
            var root = XDocument.Load(coberturaPath).Root;
            return (Rate(root, "line-rate"), Rate(root, "branch-rate"));
        }
        catch { return (null, null); }
    }

    private static double? Rate(XElement? element, string attr) =>
        double.TryParse(element?.Attribute(attr)?.Value,
            System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v)
            ? v : null;

    private static string Pct(double rate) =>
        (rate * 100).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "%";

    private static string? ResolveSettings(RigSession session, string? testProject)
    {
        var configured = session.Config.Coverage?.Settings;
        if (!string.IsNullOrEmpty(configured))
            return Path.IsPathRooted(configured) ? configured : Path.Combine(session.Root, configured);

        // Convention: a single *.runsettings next to the test project (then root).
        var testDir = testProject is not null ? Path.GetDirectoryName(testProject) : null;
        return FindRunsettings(testDir, session.Root);
    }

    /// <summary>Single <c>*.runsettings</c> in the test-project dir, else the root.
    /// Returns null when absent or ambiguous (multiple in a dir).</summary>
    internal static string? FindRunsettings(string? testProjectDir, string root)
    {
        foreach (var dir in new[] { testProjectDir, root }.Where(d => !string.IsNullOrEmpty(d)).Distinct())
        {
            if (!Directory.Exists(dir)) continue;
            var hits = Directory.EnumerateFiles(dir!, "*.runsettings").ToList();
            if (hits.Count == 1) return hits[0];
            if (hits.Count > 1) return null; // ambiguous → require explicit config
        }
        return null;
    }

    private static string? FindNewestCobertura(string root, string resultsDir, string? testProject)
    {
        var roots = new List<string> { resultsDir };
        if (testProject is not null)
            roots.Add(Path.Combine(Path.GetDirectoryName(testProject)!, "bin"));
        roots.Add(root);

        string? newest = null;
        DateTime newestTime = DateTime.MinValue;
        foreach (var dir in roots.Where(Directory.Exists).Distinct())
        {
            foreach (var f in Directory.EnumerateFiles(dir, "*.cobertura.xml", SearchOption.AllDirectories))
            {
                var t = File.GetLastWriteTimeUtc(f);
                if (t > newestTime) { newestTime = t; newest = f; }
            }
            if (newest is not null) break;
        }
        return newest;
    }
}
