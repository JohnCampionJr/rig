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

    public static readonly Option<bool> DryRun = new("--dry-run", "-n")
    {
        Description = "Print what would run (or change) without doing it",
        Recursive = true,
    };

    /// <summary>Tokens after the first <c>--</c>, split off before parsing (see
    /// Program.cs) and forwarded verbatim — kept off the parser so they can't bind
    /// to a verb's optional positional argument.</summary>
    public static string[] PassThrough = [];

    public static RigSession Session(ParseResult parse)
    {
        var session = RigSession.Load(Directory.GetCurrentDirectory(), useDotEnv: !parse.GetValue(NoEnv));
        // Set once, here: every verb's action funnels through Session before it
        // echoes/spawns. The flag wins; the config pref is the default.
        Ui.Quiet = parse.GetValue(Quiet) || session.Config.Quiet == true;
        Exec.DryRun = parse.GetValue(DryRun);
        return session;
    }

    /// <summary>Args forwarded to the spawned tool: unrecognized flags the parser
    /// didn't consume, then the explicit post-<c>--</c> <see cref="PassThrough"/>.</summary>
    public static string[] Forwarded(ParseResult parse) => [.. parse.UnmatchedTokens, .. PassThrough];
}
