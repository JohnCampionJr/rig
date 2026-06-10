using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Rig;

/// <summary>
/// Process execution helpers. Built-in verbs use <see cref="Run"/> with an
/// explicit argument list (no shell — args are tool-controlled, no injection
/// surface). Custom commands in string form use <see cref="RunShell"/>
/// (<c>/bin/sh -c</c> / <c>cmd /c</c>) so pipes / &amp;&amp; / expansion behave
/// like npm scripts.
///
/// When an <paramref name="env"/> is supplied it must be the *complete* merged
/// environment (see <see cref="EnvStack"/>) — it replaces the child's
/// environment wholesale, so ambient vars must already be folded in.
/// </summary>
internal static class Exec
{
    /// <summary>When set (via <c>--dry-run</c>/<c>-n</c>), <see cref="Run"/> and
    /// <see cref="RunShell"/> echo their command (the caller does that) but skip
    /// the actual spawn, returning success. Read-only <see cref="Capture"/> still
    /// runs — it never mutates anything.</summary>
    public static bool DryRun { get; set; }

    public static int Run(string file, IEnumerable<string> args, string cwd,
        IReadOnlyDictionary<string, string>? env = null, bool suppressMissing = false)
    {
        if (DryRun) return 0;
        var psi = new ProcessStartInfo { FileName = file, WorkingDirectory = cwd, UseShellExecute = false };
        foreach (var a in args) psi.ArgumentList.Add(a);
        ApplyEnv(psi, env);
        return Start(psi, file, suppressMissing);
    }

    public static int RunShell(string command, string cwd, IReadOnlyDictionary<string, string>? env = null)
    {
        if (DryRun) return 0;
        var (file, args) = ShellInvocation(command);
        var psi = new ProcessStartInfo { FileName = file, WorkingDirectory = cwd, UseShellExecute = false };
        foreach (var a in args) psi.ArgumentList.Add(a);
        ApplyEnv(psi, env);
        return Start(psi, file, suppressMissing: false);
    }

    /// <summary>Run a process and capture stdout (read-only — ignores
    /// <see cref="DryRun"/>). Returns (exit code, stdout); (-1, "") if it can't
    /// start. Used for non-destructive queries like listing kill targets.</summary>
    public static (int Code, string Output) Capture(string file, IEnumerable<string> args, string cwd)
    {
        var psi = new ProcessStartInfo
        {
            FileName = file, WorkingDirectory = cwd, UseShellExecute = false,
            RedirectStandardOutput = true, RedirectStandardError = true,
        };
        foreach (var a in args) psi.ArgumentList.Add(a);
        try
        {
            using var p = Process.Start(psi);
            if (p is null) return (-1, string.Empty);
            var output = p.StandardOutput.ReadToEnd();
            p.WaitForExit();
            return (p.ExitCode, output);
        }
        catch (System.ComponentModel.Win32Exception) { return (-1, string.Empty); }
    }

    /// <summary>The platform shell invocation for a command string. On Windows the
    /// shell is the absolute <see cref="ComSpec"/> so a <c>cmd.exe</c> planted in the
    /// working directory can't be picked up ahead of the real one.</summary>
    public static (string File, string[] Args) ShellInvocation(string command) =>
        OperatingSystem.IsWindows() ? (ComSpec(), ["/c", command]) : ("/bin/sh", ["-c", command]);

    /// <summary>Absolute path to <c>cmd.exe</c> (from <c>%ComSpec%</c>, else the
    /// system dir). Spawning a bare <c>cmd.exe</c> lets Windows search the current
    /// directory first — resolving it absolutely closes that hijack.</summary>
    public static string ComSpec() =>
        Environment.GetEnvironmentVariable("ComSpec") is { Length: > 0 } c
            ? c
            : Path.Combine(Environment.SystemDirectory, "cmd.exe");

    private static readonly Regex CmdMeta = new(@"([()\[\]%!^""`<>&|;, *?])", RegexOptions.Compiled);

    /// <summary>The <c>cmd.exe</c> argument string to run a <c>.cmd</c>/<c>.bat</c>
    /// shim with metacharacter-safe, caret-escaped arguments, so a forwarded arg
    /// can't break out and inject (cmd re-parses the line before the shim runs).
    /// Mirrors the Node tool's <c>winCmdInvocation</c>; pure, unit-tested.</summary>
    public static string WinCmdArguments(string file, IEnumerable<string> args)
    {
        static string EscMeta(string s) => CmdMeta.Replace(s, "^$1");
        static string EscArg(string arg)
        {
            var s = Regex.Replace(arg, @"(\\*)""", "$1$1\\\"");
            s = Regex.Replace(s, @"(\\*)$", "$1$1");
            s = "\"" + s + "\"";
            return EscMeta(EscMeta(s)); // double-escape for the .cmd/.bat case
        }
        var shellCommand = string.Join(' ', new[] { EscMeta(file) }.Concat(args.Select(EscArg)));
        return $"/d /s /c \"{shellCommand}\"";
    }

    private static void ApplyEnv(ProcessStartInfo psi, IReadOnlyDictionary<string, string>? env)
    {
        if (env is null) return; // inherit the ambient environment unchanged
        psi.Environment.Clear();
        foreach (var (k, v) in env) psi.Environment[k] = v;
    }

    private static int Start(ProcessStartInfo psi, string file, bool suppressMissing)
    {
        try
        {
            using var p = Process.Start(psi)
                ?? throw new InvalidOperationException($"Process.Start returned null for {file}");
            p.WaitForExit();
            return p.ExitCode;
        }
        catch (System.ComponentModel.Win32Exception) when (suppressMissing)
        {
            return 0; // file not found on PATH — caller treats as a no-op
        }
    }

    public static string QuoteIfNeeded(string s) => s.Contains(' ') ? $"\"{s}\"" : s;

    /// <summary>Quote an argument for safe interpolation into a *shell* command
    /// string (string-form custom commands). POSIX: single-quote wrap with the
    /// <c>'\''</c> trick so quotes / <c>$</c> / <c>;</c> / backticks stay literal.
    /// Windows <c>cmd</c> has no robust universal quoting, so fall back to the
    /// display quoting there.</summary>
    public static string ShellArg(string s) =>
        OperatingSystem.IsWindows() ? QuoteIfNeeded(s) : "'" + s.Replace("'", "'\\''") + "'";

    public static void OpenPath(string path)
    {
        if (OperatingSystem.IsWindows())
            Run(ComSpec(), ["/c", "start", "", path], ".", suppressMissing: true);
        else if (OperatingSystem.IsMacOS())
            Run("open", [path], ".", suppressMissing: true);
        else
            Run("xdg-open", [path], ".", suppressMissing: true);
    }
}

/// <summary>
/// Builds the environment for a spawned process from the layered stack.
/// Precedence, low → high (later wins): <c>.env</c> / <c>.env.local</c> &lt;
/// ambient shell env &lt; <c>.rig.json</c> <c>env</c> &lt; per-command <c>env</c>.
/// Env-var names are case-insensitive on Windows, case-sensitive elsewhere.
/// </summary>
internal static class EnvStack
{
    public static readonly StringComparer Comparer =
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    public static IReadOnlyDictionary<string, string> Ambient()
    {
        var d = new Dictionary<string, string>(Comparer);
        foreach (System.Collections.DictionaryEntry e in Environment.GetEnvironmentVariables())
            d[(string)e.Key] = e.Value?.ToString() ?? string.Empty;
        return d;
    }

    public static Dictionary<string, string> Merge(
        IReadOnlyDictionary<string, string>? fileEnv,
        IReadOnlyDictionary<string, string>? ambient,
        IReadOnlyDictionary<string, string>? configEnv,
        IReadOnlyDictionary<string, string>? commandEnv)
    {
        var r = new Dictionary<string, string>(Comparer);
        Overlay(r, fileEnv);
        Overlay(r, ambient);
        Overlay(r, configEnv);
        Overlay(r, commandEnv);
        return r;
    }

    private static void Overlay(Dictionary<string, string> into, IReadOnlyDictionary<string, string>? src)
    {
        if (src is null) return;
        foreach (var (k, v) in src) into[k] = v;
    }
}
