using System.Text.Json;
using System.Text.Json.Nodes;

namespace Rig;

/// <summary>
/// Lets `rig` manage its own <c>.rig.json</c> so users never hand-edit for the
/// common case (e.g. remembering a default project). For an existing file the
/// value is spliced in place via <see cref="JsoncEditor"/>, preserving comments,
/// formatting, and key order; only a brand-new/empty/unparseable file is written
/// fresh (nothing to preserve there).
/// </summary>
internal static class ConfigWriter
{
    public static string SetString(string root, string property, string value)
    {
        var path = Path.Combine(root, RootResolver.ConfigFileName);

        // Existing file: splice in place to preserve comments / formatting / order.
        if (File.Exists(path))
        {
            var text = File.ReadAllText(path);
            if (JsoncEditor.TrySetTopLevelString(text, property, value, out var edited))
            {
                File.WriteAllText(path, edited);
                return path;
            }
        }

        // New / empty / unparseable file: write a fresh minimal document.
        var obj = new JsonObject
        {
            ["$schema"] = "tools/Rig/src/Rig/rig.schema.json",
            [property] = value,
        };
        File.WriteAllText(path, obj.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + "\n");
        return path;
    }
}
