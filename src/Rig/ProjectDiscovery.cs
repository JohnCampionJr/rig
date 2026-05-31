using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Rig;

/// <summary>A project discovered from the solution (or a csproj scan).</summary>
internal sealed record ProjectInfo(
    string Name,
    string RelPath,
    string FullPath,
    string? OutputType,
    string? Tfm,
    bool IsTest,
    string? AssemblyName = null)
{
    /// <summary>The process/output name (AssemblyName when set, else the project name).</summary>
    public string OutputName => string.IsNullOrEmpty(AssemblyName) ? Name : AssemblyName;

    public string ShortName => Name.Contains('.') ? Name[(Name.LastIndexOf('.') + 1)..] : Name;

    public bool IsRunnable => !IsTest &&
        (string.Equals(OutputType, "Exe", StringComparison.OrdinalIgnoreCase) ||
         string.Equals(OutputType, "WinExe", StringComparison.OrdinalIgnoreCase));
}

/// <summary>
/// Convention-first project discovery. Locates a solution (config override →
/// first <c>*.slnx</c> → first <c>*.sln</c>) and reads each project's
/// <c>OutputType</c>, target framework, and test signals straight from the
/// csproj. When no solution exists, falls back to scanning <c>*.csproj</c>
/// under the root (skipping <c>bin</c>/<c>obj</c>).
/// </summary>
internal static class ProjectDiscovery
{
    private const StringComparison OIC = StringComparison.OrdinalIgnoreCase;

    public static IReadOnlyList<ProjectInfo> Discover(string root, string? configuredSolution)
    {
        var solution = FindSolution(root, configuredSolution);
        IEnumerable<string> csprojs = solution is not null
            ? SolutionProjects(solution)
            : ScanForProjects(root);

        var projects = new List<ProjectInfo>();
        foreach (var path in csprojs)
        {
            if (!File.Exists(path)) continue;
            projects.Add(LoadProject(path, root));
        }
        return projects.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static bool _warnedMultiSolution;

    /// <summary>Warn once when the root has several solutions and none is
    /// configured (we pick the first). Call only from user-facing verbs — never
    /// from discovery/completion, which must stay silent.</summary>
    public static void WarnMultipleSolutions(string root, string? configuredSolution)
    {
        if (_warnedMultiSolution || !string.IsNullOrEmpty(configuredSolution)) return;
        var solutions = SolutionCandidates(root);
        if (solutions.Count <= 1) return;
        _warnedMultiSolution = true;
        Ui.Warn($"Multiple solutions found ({string.Join(", ", solutions)}); using {solutions[0]}. " +
                "Set \"solution\" in .rig.json to choose.");
    }

    /// <summary>Solution file names at the root, *.slnx preferred (matches
    /// <see cref="FindSolution"/>'s precedence).</summary>
    public static IReadOnlyList<string> SolutionCandidates(string root) =>
        Directory.EnumerateFiles(root, "*.slnx")
            .Concat(Directory.EnumerateFiles(root, "*.sln"))
            .Select(Path.GetFileName)
            .Where(n => n is not null)
            .Cast<string>()
            .ToList();

    public static string? FindSolution(string root, string? configuredSolution)
    {
        if (!string.IsNullOrEmpty(configuredSolution))
        {
            var full = Path.IsPathRooted(configuredSolution)
                ? configuredSolution
                : Path.Combine(root, configuredSolution);
            return File.Exists(full) ? full : null;
        }

        var slnx = Directory.EnumerateFiles(root, "*.slnx").FirstOrDefault();
        if (slnx is not null) return slnx;
        return Directory.EnumerateFiles(root, "*.sln").FirstOrDefault();
    }

    public static IReadOnlyList<string> SolutionProjects(string solutionPath)
    {
        var dir = Path.GetDirectoryName(solutionPath)!;
        var rels = solutionPath.EndsWith(".slnx", OIC)
            ? ParseSlnx(solutionPath)
            : ParseSln(solutionPath);

        return rels
            .Where(r => r.EndsWith(".csproj", OIC))
            .Select(r => Path.GetFullPath(Path.Combine(dir, Normalize(r))))
            .Distinct()
            .ToList();
    }

    private static IEnumerable<string> ParseSlnx(string path)
    {
        XDocument doc;
        try { doc = XDocument.Load(path); }
        catch { return []; }

        return doc.Descendants("Project")
            .Select(e => e.Attribute("Path")?.Value)
            .Where(p => !string.IsNullOrEmpty(p))
            .Cast<string>()
            .ToList();
    }

    // Classic .sln: lines of the form
    //   Project("{TYPE-GUID}") = "Name", "relative\path.csproj", "{PROJECT-GUID}"
    private static readonly Regex SlnProjectLine = new(
        "^Project\\(\"\\{[^}]+\\}\"\\)\\s*=\\s*\"[^\"]*\"\\s*,\\s*\"([^\"]+)\"",
        RegexOptions.Compiled | RegexOptions.Multiline);

    private static IEnumerable<string> ParseSln(string path)
    {
        var text = File.ReadAllText(path);
        foreach (Match m in SlnProjectLine.Matches(text))
            yield return m.Groups[1].Value;
    }

    public static ProjectInfo LoadProject(string csprojFullPath, string root)
    {
        var name = Path.GetFileNameWithoutExtension(csprojFullPath);
        var rel = Path.GetRelativePath(root, csprojFullPath);

        string? outputType = null, tfm = null, assemblyName = null;
        var isTestProp = false;
        var enableMSTest = false;
        var refsTestSdk = false;

        try
        {
            var doc = XDocument.Load(csprojFullPath);
            outputType = First(doc, "OutputType");
            tfm = First(doc, "TargetFramework")
                  ?? First(doc, "TargetFrameworks")?.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();

            assemblyName = First(doc, "AssemblyName");
            if (assemblyName is not null && assemblyName.Contains('$')) assemblyName = null; // unevaluated MSBuild prop

            isTestProp = IsTrue(First(doc, "IsTestProject"));
            enableMSTest = IsTrue(First(doc, "EnableMSTestRunner"));

            refsTestSdk = doc.Descendants("PackageReference")
                .Select(e => e.Attribute("Include")?.Value)
                .Any(inc => string.Equals(inc, "Microsoft.NET.Test.Sdk", OIC));
        }
        catch
        {
            // Unparseable csproj — treat as a non-test library with unknown TFM.
        }

        var isTest = isTestProp || enableMSTest || refsTestSdk ||
                     name.EndsWith("Tests", OIC) || name.EndsWith(".Tests", OIC);

        return new ProjectInfo(name, rel, csprojFullPath, outputType, tfm, isTest, assemblyName);
    }

    private static IEnumerable<string> ScanForProjects(string root)
    {
        foreach (var path in Directory.EnumerateFiles(root, "*.csproj", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(root, path);
            if (rel.Contains($"bin{Path.DirectorySeparatorChar}", OIC) ||
                rel.Contains($"obj{Path.DirectorySeparatorChar}", OIC))
                continue;
            yield return path;
        }
    }

    private static string? First(XDocument doc, string element) =>
        doc.Descendants(element).Select(e => e.Value).FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

    private static bool IsTrue(string? value) => string.Equals(value?.Trim(), "true", OIC);

    private static string Normalize(string rel) =>
        rel.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
}
