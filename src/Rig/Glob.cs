using System.Text.RegularExpressions;

namespace Rig;

/// <summary>
/// Minimal, dependency-free glob matching for config patterns: <c>*</c> matches
/// any run of characters, <c>?</c> a single one. Case-insensitive, anchored
/// (the whole input must match). Used by <c>exclude</c> project filtering.
/// </summary>
internal static class Glob
{
    public static bool IsMatch(string pattern, string input)
    {
        var regex = "^" + Regex.Escape(pattern).Replace("\\*", ".*").Replace("\\?", ".") + "$";
        return Regex.IsMatch(input, regex, RegexOptions.IgnoreCase);
    }
}
