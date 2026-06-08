namespace Rig;

/// <summary>
/// `rig init` — scaffold a commented <c>.rig.json</c> at the repo root. Almost
/// everything is optional (rig is convention-first), so the template is mostly
/// commented examples. Refuses to overwrite an existing file.
/// </summary>
internal static class InitVerb
{
    public static int Execute(RigSession session)
    {
        var path = Path.Combine(session.Root, RootResolver.ConfigFileName);
        if (File.Exists(path))
        {
            Ui.Warn($".rig.json already exists at {Path.GetRelativePath(session.Root, path)} — leaving it untouched.");
            return 1;
        }

        File.WriteAllText(path, Template);
        Ui.Success($"Created {Path.GetRelativePath(session.Root, path)}. Most fields are optional — rig auto-discovers the rest.");
        return 0;
    }

    private const string Template = """
        {
          // rig configuration — all fields optional; rig is convention-first.
          // Solution, test project, coverage runsettings, and rebuild targets are
          // auto-discovered. Docs: https://github.com/JohnCampionJr/rig
          "$schema": "https://raw.githubusercontent.com/JohnCampionJr/rig/main/rig.schema.json",

          // Shared keys (the same in the .NET and Node rig) live at the top level;
          // .NET-only settings go under "dotnet". Both are optional.

          // Default project for `rig run` when several are runnable (or use `rig default`):
          // "defaultProject": "MyApp",

          // Named env presets applied by a flag, e.g. `rig test --log`:
          // "envPresets": { "log": { "MYAPP_LOG": "1" } },

          // Custom verbs (npm-scripts style). String = shell; array = argv; object = per-OS:
          // "commands": { "deploy": "./deploy.sh" },

          // Override a verb's short alias (built-ins have sensible defaults):
          // "aliases": { "coverage": "cov" },

          // Hide projects from the run/default/publish pickers (name or path globs):
          // "exclude": ["*Bench", "*.Demo", "*Spike"],

          // .NET-specific settings:
          "dotnet": {
            // Pin the solution / test project when auto-discovery is ambiguous:
            // "solution": "MyApp.slnx",
            // "test": { "project": "tests/MyApp.Tests/MyApp.Tests.csproj" },

            // ReportGenerator Pro license. Blank = the free engine. A real key works
            // here, but since this file is committed, prefer the REPORTGENERATOR_LICENSE
            // env var / .env for secrets.
            "coverage": { "license": "" }
          }
        }

        """;
}
