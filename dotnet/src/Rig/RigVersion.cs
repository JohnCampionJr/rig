using System.CommandLine;
using System.CommandLine.Invocation;
using System.Reflection;

namespace Rig;

/// <summary>
/// The product version and the `--version` display line. The line carries an
/// ecosystem tag ("(.NET)") so it's obvious which rig answered — the Node tool
/// prints "(node)". When `rig` delegates across ecosystems, the tool that runs
/// prints its own tag, so the line always names the implementation in play.
/// </summary>
public static class RigVersion
{
    /// <summary>Just the number, with the "+&lt;git-sha&gt;" build suffix stripped.</summary>
    public static string Number { get; } = Resolve();

    /// <summary>What `rig --version` prints, e.g. "1.4.0 (.NET)".</summary>
    public static string Display => $"{Number} (.NET)";

    static string Resolve()
    {
        var asm = typeof(RigVersion).Assembly;
        var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (string.IsNullOrEmpty(info))
            return asm.GetName().Version?.ToString() ?? "0.0.0";
        // AssemblyInformationalVersion is "1.4.0+<sha>" by default — keep the number.
        var plus = info.IndexOf('+');
        return plus >= 0 ? info[..plus] : info;
    }
}

/// <summary>Replaces System.CommandLine's default `--version` output (a bare,
/// git-sha-suffixed number) with the tagged, cleaned <see cref="RigVersion.Display"/>.</summary>
public sealed class RigVersionAction : SynchronousCommandLineAction
{
    public override int Invoke(ParseResult parseResult)
    {
        Console.WriteLine(RigVersion.Display);
        return 0;
    }
}
