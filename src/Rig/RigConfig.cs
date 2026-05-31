using System.Text.Json;
using System.Text.Json.Serialization;

namespace Rig;

/// <summary>
/// The <c>.rig.json</c> model. Tolerant: a missing file yields all-default
/// values, unknown properties are ignored, and JSONC (comments + trailing
/// commas) is accepted. See docs/rig.md for the schema.
/// </summary>
internal sealed class RigConfig
{
    public string? Solution { get; set; }
    public string? DefaultProject { get; set; }
    public TestConfig? Test { get; set; }
    public CoverageConfig? Coverage { get; set; }
    public KillConfig? Kill { get; set; }
    public RebuildConfig? Rebuild { get; set; }
    public PublishConfig? Publish { get; set; }
    public Dictionary<string, string>? Env { get; set; }
    public Dictionary<string, CommandDef>? Commands { get; set; }

    /// <summary>Verb name → curated short alias, overriding the built-in default
    /// and naming custom verbs' aliases (e.g. <c>"coverage": "c"</c>).</summary>
    public Dictionary<string, string>? Aliases { get; set; }

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static RigConfig Load(string? path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return new RigConfig();
        return Parse(File.ReadAllText(path));
    }

    public static RigConfig Parse(string json) =>
        JsonSerializer.Deserialize<RigConfig>(json, Options) ?? new RigConfig();

    private static readonly string[] KnownKeys =
        ["$schema", "solution", "defaultProject", "test", "coverage", "kill", "rebuild", "publish", "env", "commands", "aliases"];

    /// <summary>
    /// Top-level keys in the JSON that rig doesn't recognize (typos). System.Text.Json
    /// silently ignores them, so this surfaces them (with a "did you mean" guess) for
    /// <c>rig info</c>. Never throws.
    /// </summary>
    public static IReadOnlyList<(string Key, string? Suggestion)> UnknownKeys(string json)
    {
        var result = new List<(string, string?)>();
        try
        {
            using var doc = JsonDocument.Parse(json,
                new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return result;

            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (KnownKeys.Any(k => string.Equals(k, prop.Name, StringComparison.OrdinalIgnoreCase))) continue;
                result.Add((prop.Name, ClosestKey(prop.Name)));
            }
        }
        catch { /* malformed JSON is the loader's problem, not ours */ }
        return result;
    }

    private static string? ClosestKey(string key)
    {
        var (best, bestDistance) = (default(string), int.MaxValue);
        foreach (var known in KnownKeys)
        {
            var d = Levenshtein(key.ToLowerInvariant(), known.ToLowerInvariant());
            if (d < bestDistance) (best, bestDistance) = (known, d);
        }
        return bestDistance <= 3 ? best : null; // only suggest a plausibly-close match
    }

    private static int Levenshtein(string a, string b)
    {
        var d = new int[a.Length + 1, b.Length + 1];
        for (var i = 0; i <= a.Length; i++) d[i, 0] = i;
        for (var j = 0; j <= b.Length; j++) d[0, j] = j;
        for (var i = 1; i <= a.Length; i++)
            for (var j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);
            }
        return d[a.Length, b.Length];
    }
}

internal sealed class TestConfig
{
    public string? Project { get; set; }

    /// <summary>Named env bundles, e.g. <c>"log": { "FOO": "1" }</c>, applied by
    /// a matching flag (<c>rig test --log</c>).</summary>
    public Dictionary<string, Dictionary<string, string>>? EnvPresets { get; set; }
}

internal sealed class CoverageConfig
{
    public string? Settings { get; set; }
    public string? Collector { get; set; } // auto | mtp | xplat
    public string? License { get; set; }    // ReportGenerator Pro key → REPORTGENERATOR_LICENSE
}

internal sealed class KillConfig
{
    public List<string>? Match { get; set; }
}

internal sealed class RebuildConfig
{
    public List<string>? Skip { get; set; }
}

internal sealed class PublishConfig
{
    public string? Rid { get; set; }
    public bool? SelfContained { get; set; }
    public bool? SingleFile { get; set; }
    public string? Output { get; set; }
}

/// <summary>A shell command (single string) or an explicit argv array
/// (bypasses the shell).</summary>
[JsonConverter(typeof(CommandSpecConverter))]
internal sealed class CommandSpec
{
    public string? Shell { get; set; }
    public string[]? Argv { get; set; }

    public bool IsShell => Shell is not null;
}

/// <summary>
/// A custom command entry. Accepts three JSON shapes: a bare string
/// (shell command), a string array (argv), or an object with
/// <c>description</c>/<c>command</c>/<c>os</c>/<c>env</c>/<c>cwd</c>.
/// </summary>
[JsonConverter(typeof(CommandDefConverter))]
internal sealed class CommandDef
{
    public string? Description { get; set; }
    public CommandSpec? Command { get; set; }
    public Dictionary<string, CommandSpec>? Os { get; set; } // macos | windows | linux
    public Dictionary<string, string>? Env { get; set; }
    public string? Cwd { get; set; }

    /// <summary>The command for the current OS: an <c>os</c> entry if present,
    /// otherwise the top-level <see cref="Command"/>.</summary>
    public CommandSpec? Resolve()
    {
        if (Os is not null)
        {
            var key = OperatingSystem.IsWindows() ? "windows" : OperatingSystem.IsMacOS() ? "macos" : "linux";
            foreach (var kv in Os)
                if (string.Equals(kv.Key, key, StringComparison.OrdinalIgnoreCase))
                    return kv.Value;
        }
        return Command;
    }
}

internal sealed class CommandSpecConverter : JsonConverter<CommandSpec>
{
    public override CommandSpec Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.String:
                return new CommandSpec { Shell = reader.GetString() };
            case JsonTokenType.StartArray:
                var argv = new List<string>();
                while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                    argv.Add(reader.GetString() ?? string.Empty);
                return new CommandSpec { Argv = argv.ToArray() };
            default:
                throw new JsonException("A command must be a string or an array of strings.");
        }
    }

    public override void Write(Utf8JsonWriter writer, CommandSpec value, JsonSerializerOptions options) =>
        throw new NotSupportedException();
}

internal sealed class CommandDefConverter : JsonConverter<CommandDef>
{
    public override CommandDef Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.String:
            case JsonTokenType.StartArray:
                return new CommandDef { Command = JsonSerializer.Deserialize<CommandSpec>(ref reader, options) };

            case JsonTokenType.StartObject:
                var def = new CommandDef();
                while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
                {
                    if (reader.TokenType != JsonTokenType.PropertyName) continue;
                    var name = reader.GetString()!;
                    reader.Read();
                    switch (name.ToLowerInvariant())
                    {
                        case "description": def.Description = reader.GetString(); break;
                        case "command": def.Command = JsonSerializer.Deserialize<CommandSpec>(ref reader, options); break;
                        case "os": def.Os = JsonSerializer.Deserialize<Dictionary<string, CommandSpec>>(ref reader, options); break;
                        case "env": def.Env = JsonSerializer.Deserialize<Dictionary<string, string>>(ref reader, options); break;
                        case "cwd": def.Cwd = reader.GetString(); break;
                        default: reader.Skip(); break;
                    }
                }
                return def;

            default:
                throw new JsonException("A command entry must be a string, array, or object.");
        }
    }

    public override void Write(Utf8JsonWriter writer, CommandDef value, JsonSerializerOptions options) =>
        throw new NotSupportedException();
}
