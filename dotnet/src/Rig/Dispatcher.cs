using System.Diagnostics;

namespace Rig;

/// <summary>
/// Other-awareness: when the .NET <c>rig</c> is invoked in a Node project, it
/// hands off to the Node tool (<c>rignode</c>). Mirrors the Node tool's
/// delegation, so routing works regardless of which <c>rig</c> wins on PATH.
/// </summary>
public static class Dispatcher
{
    static readonly string[] DotnetExts = [".sln", ".slnx", ".csproj", ".fsproj"];

    static bool IsDotnetDir(string dir)
    {
        try
        {
            foreach (var file in Directory.EnumerateFiles(dir))
                if (DotnetExts.Any(ext => file.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
                    return true;
        }
        catch { /* unreadable dir → not .NET */ }
        return false;
    }

    /// <summary>Nearest project marker walking up: "dotnet", "node", or null.</summary>
    public static string? NearestEcosystem(string start)
    {
        for (var dir = new DirectoryInfo(start); dir is not null; dir = dir.Parent)
        {
            if (IsDotnetDir(dir.FullName)) return "dotnet";
            if (File.Exists(Path.Combine(dir.FullName, "package.json"))) return "node";
        }
        return null;
    }

    /// <summary>Locate the Node tool by its unique <c>rignode</c> name on PATH.</summary>
    public static string? FindNodeTool()
    {
        var overridePath = Environment.GetEnvironmentVariable("RIG_NODE_TOOL");
        if (!string.IsNullOrEmpty(overridePath))
            return File.Exists(overridePath) ? overridePath : null;

        string[] names = OperatingSystem.IsWindows()
            ? ["rignode.cmd", "rignode.exe", "rignode"]
            : ["rignode"];

        foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
        {
            if (string.IsNullOrEmpty(dir)) continue;
            foreach (var name in names)
            {
                var candidate = Path.Combine(dir, name);
                if (File.Exists(candidate)) return candidate;
            }
        }
        return null;
    }

    /// <summary>
    /// If the current directory is a Node project, hand off to the Node tool and
    /// return its exit code. Returns null when this is a .NET project (so the
    /// .NET tool runs normally — including the native <c>[suggest]</c> completion
    /// directive). In a Node project a <c>[suggest]</c> request is forwarded too:
    /// the Node rig speaks the same protocol, so its output passes straight
    /// through. A missing Node tool during completion stays silent (no nudge).
    /// </summary>
    public static int? MaybeDelegate(string[] args)
    {
        // A handoff sets RIG_NO_DELEGATE so we run natively (no bounce-back).
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("RIG_NO_DELEGATE")))
            return null;

        // .NET project → run natively (this includes the `[suggest]` directive).
        if (NearestEcosystem(Directory.GetCurrentDirectory()) != "node")
            return null;

        var isSuggest = args.Length > 0 && args[0].StartsWith("[suggest", StringComparison.Ordinal);

        var tool = FindNodeTool();
        if (tool is null)
        {
            // During completion, offer nothing rather than nudging into the shell.
            if (isSuggest) return 0;
            Console.Error.WriteLine("📁 Node project — the .NET rig doesn't handle these.");
            Console.Error.WriteLine("   Install the Node rig for `rig` to work here too:");
            Console.Error.WriteLine("   npm install -g @jcamp/rig");
            return 1;
        }
        return RunTool(tool, args);
    }

    /// <summary>Run an already-located Node tool (e.g. from <see cref="FindNodeTool"/>)
    /// with <paramref name="args"/>, returning its exit code. Runs with
    /// <c>RIG_NO_DELEGATE</c> set, so the Node tool executes natively instead of
    /// handing back to us.</summary>
    public static int RunNodeTool(string toolPath, string[] args) => RunTool(toolPath, args);

    static int RunTool(string path, string[] args)
    {
        ProcessStartInfo psi;
        var isCmd = OperatingSystem.IsWindows()
            && (path.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".bat", StringComparison.OrdinalIgnoreCase));

        if (isCmd)
        {
            psi = new ProcessStartInfo("cmd.exe") { UseShellExecute = false };
            psi.ArgumentList.Add("/c");
            psi.ArgumentList.Add(path);
        }
        else
        {
            psi = new ProcessStartInfo(path) { UseShellExecute = false };
        }
        foreach (var arg in args) psi.ArgumentList.Add(arg);
        psi.Environment["RIG_NO_DELEGATE"] = "1";

        using var process = Process.Start(psi)!;
        process.WaitForExit();
        return process.ExitCode;
    }
}
