using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Rig;

/// <summary>
/// Lets `rig` manage a <c>.rig.json</c> (the repo's or the user-wide one) so users
/// never hand-edit for the common case. For an existing file the value is spliced
/// in place via <see cref="JsoncEditor"/>, preserving comments, formatting, and key
/// order; only a brand-new/empty/unparseable file is written fresh (nothing to
/// preserve there). Values are typed — string, bool, or number.
/// </summary>
internal static class ConfigWriter
{
    public const string SchemaUrl = "https://raw.githubusercontent.com/JohnCampionJr/rig/main/rig.schema.json";

    /// <summary>Set a top-level string in the repo's <c>.rig.json</c>; returns the path.</summary>
    public static string SetString(string root, string property, string value)
    {
        var path = Path.Combine(root, RootResolver.ConfigFileName);
        Set(path, [property], JsonSerializer.Serialize(value));
        return path;
    }

    public static bool SetString(string filePath, IReadOnlyList<string> path, string value) =>
        Set(filePath, path, JsonSerializer.Serialize(value));

    public static bool SetBool(string filePath, IReadOnlyList<string> path, bool value) =>
        Set(filePath, path, value ? "true" : "false");

    public static bool SetNumber(string filePath, IReadOnlyList<string> path, double value) =>
        Set(filePath, path, value.ToString(CultureInfo.InvariantCulture));

    /// <summary>Splice <paramref name="path"/> = <paramref name="rawValue"/> (a raw
    /// JSON literal) into the file, preserving comments where possible. Returns
    /// false (writing nothing) when an existing, non-empty file can't be edited in
    /// place — we never overwrite a file that has real content to lose.</summary>
    public static bool Set(string filePath, IReadOnlyList<string> path, string rawValue)
    {
        var existing = File.Exists(filePath) ? File.ReadAllText(filePath) : null;
        if (!string.IsNullOrWhiteSpace(existing))
        {
            // A real file: splice in place, or refuse rather than clobber it.
            if (JsoncEditor.TrySet(existing, path, rawValue, out var edited))
            {
                File.WriteAllText(filePath, edited);
                return true;
            }
            return false;
        }

        // No file (or an empty/whitespace one): safe to write a fresh document.
        var root = new JsonObject { ["$schema"] = SchemaUrl };
        JsonObject node = root;
        for (var i = 0; i < path.Count - 1; i++)
        {
            var child = new JsonObject();
            node[path[i]] = child;
            node = child;
        }
        node[path[^1]] = JsonNode.Parse(rawValue);

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(filePath))!);
        File.WriteAllText(filePath, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + "\n");
        return true;
    }
}
