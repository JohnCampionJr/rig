using System.Diagnostics;

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
    public static int Run(string file, IEnumerable<string> args, string cwd,
        IReadOnlyDictionary<string, string>? env = null, bool suppressMissing = false)
    {
        var psi = new ProcessStartInfo { FileName = file, WorkingDirectory = cwd, UseShellExecute = false };
        foreach (var a in args) psi.ArgumentList.Add(a);
        ApplyEnv(psi, env);
        return Start(psi, file, suppressMissing);
    }

    public static int RunShell(string command, string cwd, IReadOnlyDictionary<string, string>? env = null)
    {
        var (file, args) = ShellInvocation(command);
        var psi = new ProcessStartInfo { FileName = file, WorkingDirectory = cwd, UseShellExecute = false };
        foreach (var a in args) psi.ArgumentList.Add(a);
        ApplyEnv(psi, env);
        return Start(psi, file, suppressMissing: false);
    }

    /// <summary>The platform shell invocation for a command string.</summary>
    public static (string File, string[] Args) ShellInvocation(string command) =>
        OperatingSystem.IsWindows() ? ("cmd", ["/c", command]) : ("/bin/sh", ["-c", command]);

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
            Run("cmd", ["/c", "start", "", path], ".", suppressMissing: true);
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
