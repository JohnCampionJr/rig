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

    public static List<string> BuildArgs(string projectPath, PublishConfig? cfg, string rid, string outputDir)
    {
        var selfContained = cfg?.SelfContained ?? true;
        var singleFile = cfg?.SingleFile ?? false;
        return
        [
            "publish", projectPath,
            "-c", "Release",
            "-r", rid,
            "--self-contained", selfContained ? "true" : "false",
            $"-p:PublishSingleFile={(singleFile ? "true" : "false")}",
            "-o", outputDir,
        ];
    }

    public static int Execute(RigSession session, string? query)
    {
        ProjectDiscovery.WarnMultipleSolutions(session.Root, session.Config.Solution);
        var projects = ProjectDiscovery.Discover(session.Root, session.Config.Solution);
        var resolution = RunVerb.Resolve(projects, query, session.Config.DefaultProject);
        if (resolution.Error is not null) { Ui.Error(resolution.Error); return 1; }
        if (resolution.Selected is null)
        {
            Ui.Error("Ambiguous project; pass a project name to `rig publish`.");
            foreach (var p in resolution.Ambiguous) Ui.Info($"  • {p.Name}");
            return 1;
        }

        var rid = ResolveRid(session.Config.Publish);
        var output = Path.Combine(session.Root, ResolveOutput(session.Config.Publish, rid));
        var args = BuildArgs(resolution.Selected.FullPath, session.Config.Publish, rid, output);

        Ui.Command("dotnet", args);
        var rc = Exec.Run("dotnet", args, session.Root, session.BuildEnv());
        if (rc == 0) Ui.Success($"Published: {output}");
        return rc;
    }
}
