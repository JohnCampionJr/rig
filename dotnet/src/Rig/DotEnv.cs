using System.Text;

namespace Rig;

/// <summary>
/// Dep-free <c>.env</c> reader. Supported subset (v1): <c>KEY=VALUE</c>,
/// blank lines, full-line <c>#</c> comments, optional <c>export </c> prefix,
/// single- and double-quoted values (double quotes honour <c>\n \t \r \" \\</c>
/// escapes; single quotes are literal), and inline <c>#</c> comments on unquoted
/// values when preceded by whitespace. No <c>${VAR}</c> expansion yet.
/// </summary>
internal static class DotEnv
{
    /// <summary>Parse <c>.env</c> text into a map. Later duplicate keys win.</summary>
    public static Dictionary<string, string> Parse(string content)
    {
        var result = new Dictionary<string, string>(EnvStack.Comparer);
        foreach (var rawLine in content.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line[0] == '#') continue;
            if (line.StartsWith("export ", StringComparison.Ordinal))
                line = line["export ".Length..].TrimStart();

            var eq = line.IndexOf('=');
            if (eq <= 0) continue; // no key, or leading '='

            var key = line[..eq].Trim();
            if (!IsValidKey(key)) continue;

            result[key] = ParseValue(line[(eq + 1)..].Trim());
        }
        return result;
    }

    /// <summary>
    /// Load <c>.env</c> then <c>.env.local</c> from <paramref name="dir"/>.
    /// <c>.env.local</c> overrides <c>.env</c>. Missing files are skipped.
    /// </summary>
    public static Dictionary<string, string> Load(string dir)
    {
        var map = new Dictionary<string, string>(EnvStack.Comparer);
        foreach (var name in new[] { ".env", ".env.local" })
        {
            var path = Path.Combine(dir, name);
            if (!File.Exists(path)) continue;
            foreach (var kv in Parse(File.ReadAllText(path)))
                map[kv.Key] = kv.Value;
        }
        return map;
    }

    private static bool IsValidKey(string key)
    {
        if (key.Length == 0) return false;
        if (!(char.IsLetter(key[0]) || key[0] == '_')) return false;
        foreach (var c in key)
            if (!(char.IsLetterOrDigit(c) || c == '_')) return false;
        return true;
    }

    private static string ParseValue(string raw)
    {
        if (raw.Length == 0) return string.Empty;

        var q = raw[0];
        if (q == '\'') // single quotes are literal — first closing quote wins
        {
            var end = raw.IndexOf('\'', 1);
            return end < 0 ? raw[1..] : raw[1..end];
        }
        if (q == '"') // double quotes honour escapes, incl. an escaped \" inside
        {
            var end = ClosingDoubleQuote(raw);
            return Unescape(end < 0 ? raw[1..] : raw[1..end]);
        }

        var comment = InlineCommentIndex(raw);
        return (comment < 0 ? raw : raw[..comment]).Trim();
    }

    // Index of the closing double-quote, skipping any backslash-escaped char.
    private static int ClosingDoubleQuote(string s)
    {
        for (var i = 1; i < s.Length; i++)
        {
            if (s[i] == '\\') { i++; continue; } // skip the escaped char
            if (s[i] == '"') return i;
        }
        return -1;
    }

    private static int InlineCommentIndex(string s)
    {
        for (var i = 1; i < s.Length; i++)
            if (s[i] == '#' && char.IsWhiteSpace(s[i - 1])) return i;
        return -1;
    }

    private static string Unescape(string s)
    {
        var sb = new StringBuilder(s.Length);
        for (var i = 0; i < s.Length; i++)
        {
            if (s[i] == '\\' && i + 1 < s.Length)
            {
                var n = s[++i];
                sb.Append(n switch { 'n' => '\n', 't' => '\t', 'r' => '\r', '"' => '"', '\\' => '\\', _ => n });
            }
            else
            {
                sb.Append(s[i]);
            }
        }
        return sb.ToString();
    }
}
