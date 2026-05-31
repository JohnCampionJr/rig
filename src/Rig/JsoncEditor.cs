using System.Text;
using System.Text.Json;

namespace Rig;

/// <summary>
/// Sets a single top-level string property in a JSONC document while preserving
/// comments, formatting, and key order. System.Text.Json's DOM can't round-trip
/// comments, so instead of re-serializing we locate the exact byte span with
/// <see cref="Utf8JsonReader"/> (which tokenizes JSONC) and splice the raw UTF-8
/// — leaving everything else byte-for-byte intact.
/// </summary>
internal static class JsoncEditor
{
    public static bool TrySetTopLevelString(string text, string property, string value, out string result)
    {
        result = text;
        var bytes = Encoding.UTF8.GetBytes(text);
        var options = new JsonReaderOptions
        {
            CommentHandling = JsonCommentHandling.Allow,
            AllowTrailingCommas = true,
        };

        var depth = 0;
        var awaitingValue = false;
        long valueStart = -1, valueEnd = -1;
        long afterRootBrace = -1, firstMemberStart = -1;

        try
        {
            var reader = new Utf8JsonReader(bytes, options);
            while (reader.Read())
            {
                if (awaitingValue)
                {
                    valueStart = reader.TokenStartIndex;
                    if (reader.TokenType is JsonTokenType.StartObject or JsonTokenType.StartArray)
                        reader.Skip();
                    valueEnd = reader.BytesConsumed;
                    break; // matched: we have the value span
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

        var newValue = JsonSerializer.Serialize(value); // quoted + escaped

        if (valueStart >= 0)
        {
            result = Splice(bytes, valueStart, valueEnd, newValue);
            return true;
        }

        // Property absent — insert it right after the opening brace, *before* any
        // leading comment, so existing comments stay attached to their own member.
        if (firstMemberStart >= 0 && afterRootBrace >= 0)
        {
            var indent = IndentBefore(bytes, firstMemberStart);
            var insertion = $"\n{indent}\"{property}\": {newValue},";
            result = Splice(bytes, afterRootBrace, afterRootBrace, insertion);
            return true;
        }
        if (afterRootBrace >= 0) // empty object {}
        {
            result = Splice(bytes, afterRootBrace, afterRootBrace, $"\n  \"{property}\": {newValue}\n");
            return true;
        }

        return false; // not a JSON object
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
