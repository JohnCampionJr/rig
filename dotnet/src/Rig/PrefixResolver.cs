namespace Rig;

/// <summary>
/// Pre-parse convenience: an unambiguous *prefix* of a verb name is rewritten to
/// the full name before the parser sees it (`rig cove` → `rig coverage`). This is
/// unadvertised — the curated short forms are real command aliases (see the
/// `*Command` constructors), which System.CommandLine resolves and shows in help.
/// Exact names, option-looking tokens, and ambiguous/unknown tokens pass through.
/// </summary>
internal static class PrefixResolver
{
    private const StringComparison OIC = StringComparison.OrdinalIgnoreCase;

    /// <summary>
    /// Expands a leading <c>watch</c>/<c>w</c> modifier into a <c>--watch</c> flag on
    /// the target verb: <c>rig watch run</c> / <c>rig w r</c> → <c>rig run --watch</c>.
    /// Reusing the verb's own parsing means <c>-c</c>, forwarded args, and prefix
    /// resolution all still apply. Bare <c>watch</c> → empty (falls through to the menu).
    /// Only run/test/build define <c>--watch</c>; other targets will error, as expected.
    /// </summary>
    public static string[] ExpandWatch(string[] args)
    {
        if (args.Length == 0) return args;
        if (!string.Equals(args[0], "watch", OIC) && !string.Equals(args[0], "w", OIC)) return args;

        var rest = args[1..];
        return rest.Length == 0 ? rest : [.. rest, "--watch"];
    }

    public static string[] Resolve(string[] args, IReadOnlyCollection<string> verbs)
    {
        if (args.Length == 0) return args;

        var token = args[0];
        if (token.Length == 0 || token[0] == '-') return args;
        if (verbs.Any(v => string.Equals(v, token, OIC))) return args; // exact name

        var matches = verbs.Where(v => v.StartsWith(token, OIC)).Distinct().ToList();
        if (matches.Count != 1) return args; // ambiguous / none → parser handles it (or a native alias does)

        var rewritten = (string[])args.Clone();
        rewritten[0] = matches[0];
        return rewritten;
    }
}
