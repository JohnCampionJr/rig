using System.Text;
using System.Text.Json;

namespace Rig;

/// <summary>
/// Sets a single property (top-level, or one level deep — e.g. <c>coverage.license</c>)
/// in a JSONC document while preserving comments, formatting, and key order.
/// System.Text.Json's DOM can't round-trip comments, so instead of re-serializing
/// we locate the exact byte span with <see cref="Utf8JsonReader"/> (which tokenizes
/// JSONC) and splice the raw UTF-8 — leaving everything else byte-for-byte intact.
/// The value is supplied pre-serialized (a raw JSON literal: <c>"foo"</c>, <c>true</c>,
/// <c>80</c>), so the same machinery handles strings, bools, and numbers.
/// </summary>
internal static class JsoncEditor
{
    private static readonly JsonReaderOptions ReaderOptions = new()
    {
        CommentHandling = JsonCommentHandling.Allow,
        AllowTrailingCommas = true,
    };

    /// <summary>Set a string property, preserving comments. Back-compat entry point.</summary>
    public static bool TrySetTopLevelString(string text, string property, string value, out string result) =>
        TrySet(text, [property], JsonSerializer.Serialize(value), out result);

    /// <summary>Set <paramref name="path"/> (depth 1 or 2) to a raw JSON value.</summary>
    public static bool TrySet(string text, IReadOnlyList<string> path, string rawValue, out string result)
    {
        result = text;
        return path.Count switch
        {
            1 => SetTopLevel(text, path[0], rawValue, out result),
            2 => SetNested(text, path[0], path[1], rawValue, out result),
            _ => false, // only depths 1–2 are supported (all rig keys fit)
        };
    }

    private static bool SetTopLevel(string text, string property, string rawValue, out string result)
    {
        result = text;
        var bytes = Encoding.UTF8.GetBytes(text);

        var depth = 0;
        var awaitingValue = false;
        long valueStart = -1, valueEnd = -1;
        long afterRootBrace = -1, firstMemberStart = -1;

        try
        {
            var reader = new Utf8JsonReader(bytes, ReaderOptions);
            while (reader.Read())
            {
                if (awaitingValue)
                {
                    valueStart = reader.TokenStartIndex;
                    if (reader.TokenType is JsonTokenType.StartObject or JsonTokenType.StartArray)
                        reader.Skip();
                    valueEnd = reader.BytesConsumed;
                    break;
                }

                switch (reader.TokenType)
                {
                    case JsonTokenType.StartObject:
                    case JsonTokenType.StartArray:
                        if (reader.TokenType == JsonTokenType.StartObject && depth == 0)
                            afterRootBrace = reader.TokenStartIndex + 1;
                        depth++;
                        break;
                    case JsonTokenType.EndObject:
                    case JsonTokenType.EndArray:
                        depth--;
                        break;
                    case JsonTokenType.PropertyName when depth == 1:
                        if (firstMemberStart < 0) firstMemberStart = reader.TokenStartIndex;
                        if (reader.ValueTextEquals(property)) awaitingValue = true;
                        break;
                }
            }
        }
        catch (JsonException)
        {
            return false; // malformed — let the caller fall back
        }

        if (valueStart >= 0)
        {
            result = Splice(bytes, valueStart, valueEnd, rawValue);
            return true;
        }

        // Property absent — insert it right after the opening brace, *before* any
        // leading comment, so existing comments stay attached to their own member.
        if (firstMemberStart >= 0 && afterRootBrace >= 0)
        {
            var indent = IndentBefore(bytes, firstMemberStart);
            result = Splice(bytes, afterRootBrace, afterRootBrace, $"\n{indent}\"{property}\": {rawValue},");
            return true;
        }
        if (afterRootBrace >= 0) // empty object {}
        {
            result = Splice(bytes, afterRootBrace, afterRootBrace, $"\n  \"{property}\": {rawValue}\n");
            return true;
        }

        return false; // not a JSON object
    }

    private static bool SetNested(string text, string parent, string child, string rawValue, out string result)
    {
        result = text;
        var bytes = Encoding.UTF8.GetBytes(text);

        var depth = 0;
        var awaitingParentValue = false;
        var awaitingChildValue = false;
        var insideParent = false;
        var parentMatched = false;
        var parentIsObject = false;

        long afterRootBrace = -1, rootFirstMember = -1;
        long parentContentStart = -1, parentFirstMember = -1;
        long childValueStart = -1, childValueEnd = -1;

        try
        {
            var reader = new Utf8JsonReader(bytes, ReaderOptions);
            while (reader.Read())
            {
                if (awaitingChildValue)
                {
                    childValueStart = reader.TokenStartIndex;
                    if (reader.TokenType is JsonTokenType.StartObject or JsonTokenType.StartArray)
                        reader.Skip();
                    childValueEnd = reader.BytesConsumed;
                    awaitingChildValue = false;
                    continue;
                }

                if (awaitingParentValue)
                {
                    awaitingParentValue = false;
                    if (reader.TokenType == JsonTokenType.StartObject)
                    {
                        parentIsObject = true;
                        parentContentStart = reader.TokenStartIndex + 1;
                        insideParent = true;
                        depth++; // entering the parent object; count it ourselves
                        continue;
                    }
                    // Parent exists but isn't an object — refuse to clobber it.
                    if (reader.TokenType is JsonTokenType.StartArray)
                        reader.Skip();
                    continue;
                }

                switch (reader.TokenType)
                {
                    case JsonTokenType.StartObject:
                    case JsonTokenType.StartArray:
                        if (reader.TokenType == JsonTokenType.StartObject && depth == 0)
                            afterRootBrace = reader.TokenStartIndex + 1;
                        depth++;
                        break;
                    case JsonTokenType.EndObject:
                    case JsonTokenType.EndArray:
                        depth--;
                        if (insideParent && depth == 1) insideParent = false;
                        break;
                    case JsonTokenType.PropertyName when depth == 1:
                        if (rootFirstMember < 0) rootFirstMember = reader.TokenStartIndex;
                        if (reader.ValueTextEquals(parent)) { parentMatched = true; awaitingParentValue = true; }
                        break;
                    case JsonTokenType.PropertyName when depth == 2 && insideParent:
                        if (parentFirstMember < 0) parentFirstMember = reader.TokenStartIndex;
                        if (reader.ValueTextEquals(child)) awaitingChildValue = true;
                        break;
                }
            }
        }
        catch (JsonException)
        {
            return false;
        }

        // Child already present → replace its value span.
        if (childValueStart >= 0)
        {
            result = Splice(bytes, childValueStart, childValueEnd, rawValue);
            return true;
        }

        // Parent object present, child absent → insert the child member.
        if (parentMatched && parentIsObject && parentContentStart >= 0)
        {
            if (parentFirstMember >= 0)
            {
                var indent = IndentBefore(bytes, parentFirstMember);
                result = Splice(bytes, parentContentStart, parentContentStart, $"\n{indent}\"{child}\": {rawValue},");
            }
            else // empty object: "coverage": {}
            {
                result = Splice(bytes, parentContentStart, parentContentStart, $" \"{child}\": {rawValue} ");
            }
            return true;
        }

        // Parent present but not an object — don't risk clobbering it.
        if (parentMatched) return false;

        // Parent absent → insert a fresh "parent": { "child": value } at top level.
        if (rootFirstMember >= 0 && afterRootBrace >= 0)
        {
            var indent = IndentBefore(bytes, rootFirstMember);
            result = Splice(bytes, afterRootBrace, afterRootBrace, $"\n{indent}\"{parent}\": {{ \"{child}\": {rawValue} }},");
            return true;
        }
        if (afterRootBrace >= 0) // empty root {}
        {
            result = Splice(bytes, afterRootBrace, afterRootBrace, $"\n  \"{parent}\": {{ \"{child}\": {rawValue} }}\n");
            return true;
        }

        return false;
    }

    private static string Splice(byte[] src, long start, long end, string insert)
    {
        var ins = Encoding.UTF8.GetBytes(insert);
        var output = new byte[start + ins.Length + (src.Length - end)];
        Array.Copy(src, 0, output, 0, (int)start);
        Array.Copy(ins, 0, output, (int)start, ins.Length);
        Array.Copy(src, (int)end, output, (int)start + ins.Length, (int)(src.Length - end));
        return Encoding.UTF8.GetString(output);
    }

    // The run of spaces/tabs immediately preceding an offset (the member's indent).
    private static string IndentBefore(byte[] bytes, long offset)
    {
        var i = (int)offset - 1;
        var end = i;
        while (i >= 0 && (bytes[i] == (byte)' ' || bytes[i] == (byte)'\t')) i--;
        var len = end - i;
        return len <= 0 ? "  " : Encoding.UTF8.GetString(bytes, i + 1, len);
    }
}
