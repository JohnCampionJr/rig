using System.Runtime.InteropServices;

namespace Rig;

/// <summary>
/// `rig publish` — the generic self-contained `dotnet publish` (promoted out of
/// the old `package` verb). RID / self-contained / single-file / output come
/// from <c>publish</c> config with sane defaults. Arg building (pure) is
/// <see cref="BuildArgs"/>.
/// </summary>
internal static class PublishVerb
{
    public static string ResolveRid(PublishConfig? cfg) =>
        !string.IsNullOrWhiteSpace(cfg?.Rid) ? cfg!.Rid! : RuntimeInformation.RuntimeIdentifier;

    public static string ResolveOutput(PublishConfig? cfg, string rid)
    {
        var template = string.IsNullOrWhiteSpace(cfg?.Output) ? "dist/{rid}" : cfg!.Output!;
        return template.Replace("{rid}", rid);
    }

    public static List<string> BuildArgs(string projectPath, string configuration, string rid, bool selfContained, bool singleFile, string outputDir) =>
    [
        "publish", projectPath,
        "-c", configuration,
        "-r", rid,
        "--self-contained", selfContained ? "true" : "false",
        $"-p:PublishSingleFile={(singleFile ? "true" : "false")}",
        "-o", outputDir,
    ];

    public static int Execute(RigSession session, string? query, string? configuration = null,
        string? rid = null, bool? selfContained = null, bool? singleFile = null, string? output = null)
    {
        ProjectDiscovery.WarnMultipleSolutions(session.Root, session.Config.Solution);
        var projects = ProjectDiscovery.Discover(session.Root, session.Config.Solution, session.Config.Exclude);
        var resolution = RunVerb.Resolve(projects, query, session.Config.DefaultProject);
        if (resolution.Error is not null) { Ui.Error(resolution.Error); return 1; }
        if (resolution.Selected is null)
        {
            Ui.Error("Ambiguous project; pass a project name to `rig publish`.");
            foreach (var p in resolution.Ambiguous) Ui.Info($"  • {p.Name}");
            return 1;
        }

        // Precedence (matches coverage): CLI flag > config > built-in default.
        var cfg = session.Config.Publish;
        var effRid = !string.IsNullOrWhiteSpace(rid) ? rid! : ResolveRid(cfg);
        var effConfig = !string.IsNullOrWhiteSpace(configuration) ? configuration!
            : !string.IsNullOrWhiteSpace(cfg?.Configuration) ? cfg!.Configuration! : "Release";
        var effSelfContained = selfContained ?? cfg?.SelfContained ?? true;
        var effSingleFile = singleFile ?? cfg?.SingleFile ?? false;
        var outputDir = Path.Combine(session.Root, !string.IsNullOrWhiteSpace(output)
            ? output!.Replace("{rid}", effRid)
            : ResolveOutput(cfg, effRid));
        var args = BuildArgs(resolution.Selected.FullPath, effConfig, effRid, effSelfContained, effSingleFile, outputDir);

        Ui.Command("dotnet", args);
        if (Exec.DryRun) return 0;
        var rc = Exec.Run("dotnet", args, session.Root, session.BuildEnv());
        if (rc == 0) Ui.Success($"Published: {outputDir}");
        return rc;
    }
}
