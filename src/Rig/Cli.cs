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

    public static RigSession Session(ParseResult parse) =>
        RigSession.Load(Directory.GetCurrentDirectory(), useDotEnv: !parse.GetValue(NoEnv));

    /// <summary>Tokens the parser didn't consume (post-<c>--</c> args and
    /// unrecognized flags) — forwarded verbatim to the spawned tool.</summary>
    public static string[] Forwarded(ParseResult parse) => parse.UnmatchedTokens.ToArray();
}
