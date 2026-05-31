using System.CommandLine;

namespace Rig;

/// <summary>Shared CLI plumbing: the global options and session construction
/// used by every command's action.</summary>
internal static class Cli
{
    /// <summary>Recursive so it's available on every subcommand.</summary>
    public static readonly Option<bool> NoEnv = new("--no-env")
    {
        Description = "Do not load .env / .env.local",
        Recursive = true,
    };

    public static readonly Option<bool> Quiet = new("--quiet", "-q")
    {
        Description = "Suppress the → command echo",
        Recursive = true,
    };

    public static RigSession Session(ParseResult parse)
    {
        var session = RigSession.Load(Directory.GetCurrentDirectory(), useDotEnv: !parse.GetValue(NoEnv));
        // Set once, here: every verb's action funnels through Session before it
        // echoes a command. The flag wins; the config pref is the default.
        Ui.Quiet = parse.GetValue(Quiet) || session.Config.Quiet == true;
        return session;
    }

    /// <summary>Tokens the parser didn't consume (post-<c>--</c> args and
    /// unrecognized flags) — forwarded verbatim to the spawned tool.</summary>
    public static string[] Forwarded(ParseResult parse) => parse.UnmatchedTokens.ToArray();
}
