using Spectre.Console;

namespace Rig;

/// <summary>
/// Dependency verbs that round out @antfu/ni parity with the Node tool:
/// <c>uninstall</c> (mirror of <c>add</c>), <c>global</c> (a global .NET tool),
/// and <c>dlx</c> (a one-off tool run via <c>dnx</c>). The .NET CLI has no native
/// "upgrade all packages" or frozen-restore verb, so <c>upgrade</c>/<c>ci</c> stay
/// Node-only (see consistency.md). Command assembly is split into pure
/// <c>BuildArgs</c> helpers so it can be unit-tested without spawning.
/// </summary>
internal static class RemoveVerb
{
    /// <summary><c>dotnet remove &lt;project&gt; package &lt;pkg&gt; [forwarded…]</c>. Pure.</summary>
    public static List<string> BuildArgs(string projectFullPath, string package, string[] forwarded)
    {
        var args = new List<string> { "remove", projectFullPath, "package", package };
        args.AddRange(forwarded);
        return args;
    }

    /// <summary>`rig uninstall &lt;package&gt; [project]` — the symmetric twin of
    /// <see cref="AddVerb"/>; project resolution is shared (default → sole → prompt).</summary>
    public static int Execute(RigSession session, string? package, string? projectQuery, string[] forwarded)
    {
        if (string.IsNullOrWhiteSpace(package))
        {
            Ui.Error("Usage: rig uninstall <package> [project]");
            return 1;
        }

        var projects = ProjectDiscovery.Discover(session.Root, session.Config.Solution, session.Config.Exclude);
        var resolution = AddVerb.ResolveTarget(projects, projectQuery, session.Config.DefaultProject);
        if (resolution.Error is not null) { Ui.Error(resolution.Error); return 1; }

        var target = resolution.Selected;
        if (target is null) // ambiguous
        {
            if (!AnsiConsole.Profile.Capabilities.Interactive)
            {
                Ui.Error("Multiple projects — name one (e.g. `rig uninstall <pkg> <project>`):");
                foreach (var p in resolution.Ambiguous) Ui.Info($"  • {p.Name}");
                return 1;
            }
            target = AnsiConsole.Prompt(new SelectionPrompt<ProjectInfo>()
                .Title($"Remove [aqua]{Markup.Escape(package)}[/] from which project?")
                .UseConverter(p => p.Name)
                .AddChoices(resolution.Ambiguous));
        }

        var args = BuildArgs(target.FullPath, package, forwarded);
        Ui.Command("dotnet", args);
        return Exec.Run("dotnet", args, session.Root, session.BuildEnv());
    }
}

/// <summary>`rig global &lt;tool&gt;` — `dotnet tool install --global &lt;tool&gt;` (Node's
/// `rig global` / ni's `ni -g`). The .NET analogue of a global package is a global
/// tool; extra args (e.g. <c>--version 1.2.3</c>) forward to the install.</summary>
internal static class GlobalVerb
{
    /// <summary><c>dotnet tool install --global &lt;tool&gt; [forwarded…]</c>. Pure.</summary>
    public static List<string> BuildArgs(string tool, string[] forwarded)
    {
        var args = new List<string> { "tool", "install", "--global", tool };
        args.AddRange(forwarded);
        return args;
    }

    public static int Execute(RigSession session, string? tool, string[] forwarded)
    {
        if (string.IsNullOrWhiteSpace(tool))
        {
            Ui.Error("Usage: rig global <tool>");
            return 1;
        }

        var args = BuildArgs(tool, forwarded);
        Ui.Command("dotnet", args);
        return Exec.Run("dotnet", args, session.Root, session.BuildEnv());
    }
}

/// <summary>`rig dlx &lt;tool&gt; [args]` — run a tool once without installing it, via
/// <c>dnx</c> (Node's `rig dlx` / ni's `nlx`). <c>dnx</c> ships with the .NET 10 SDK.</summary>
internal static class DlxVerb
{
    /// <summary>The <c>dnx</c> argv: the tool spec then any forwarded args. Pure.</summary>
    public static List<string> BuildArgs(string tool, string[] forwarded)
    {
        var args = new List<string> { tool };
        args.AddRange(forwarded);
        return args;
    }

    /// <summary>Is the <c>dnx</c> launcher resolvable on PATH? (ships with .NET 10.) Pure-ish.</summary>
    public static bool DnxAvailable()
    {
        var path = Environment.GetEnvironmentVariable("PATH") ?? "";
        var exts = OperatingSystem.IsWindows() ? new[] { ".exe", ".cmd", ".bat", "" } : new[] { "" };
        foreach (var dir in path.Split(Path.PathSeparator))
        {
            if (string.IsNullOrEmpty(dir)) continue;
            foreach (var ext in exts)
                if (File.Exists(Path.Combine(dir, "dnx" + ext))) return true;
        }
        return false;
    }

    public static int Execute(RigSession session, string? tool, string[] forwarded)
    {
        if (string.IsNullOrWhiteSpace(tool))
        {
            Ui.Error("Usage: rig dlx <tool> [args]");
            return 1;
        }

        if (!DnxAvailable())
        {
            Ui.Error("`dnx` was not found on PATH — it ships with the .NET 10 SDK.");
            Ui.Info("Install .NET 10 (https://dotnet.microsoft.com/download) to use `rig dlx`.");
            return 1;
        }

        var args = BuildArgs(tool, forwarded);
        Ui.Command("dnx", args);
        return Exec.Run("dnx", args, session.Root, session.BuildEnv());
    }
}
