using System.Globalization;

using Spectre.Console;

namespace Rig;

/// <summary>
/// `rig setup` — an interactive walkthrough that shows what rig auto-detects and
/// then lets you set the few things it can't infer, writing them to either the
/// repo's <c>.rig.json</c> or your user-wide <c>~/.rig.json</c>. All writes go
/// through <see cref="ConfigWriter"/> (comments preserved). The orchestration is
/// interactive-only; the write/merge logic it calls is unit-tested separately.
/// </summary>
internal static class SetupVerb
{
    public static int Execute(RigSession session)
    {
        if (!AnsiConsole.Profile.Capabilities.Interactive)
        {
            Ui.Error("rig setup is interactive. Edit .rig.json directly, or use `rig default <project>`.");
            return 1;
        }

        AnsiConsole.Write(new Rule("[aqua]rig setup[/]").LeftJustified());
        ShowDiscovered(session);

        // Choose the target file.
        var globalPath = RigSession.GlobalConfigPath();
        const string repoChoice = "This repo (.rig.json) — committed, shared with the team";
        const string globalChoice = "All my repos (~/.rig.json) — personal, applies everywhere";
        var choice = AnsiConsole.Prompt(new SelectionPrompt<string>()
            .Title("Where should these preferences live?")
            .AddChoices(repoChoice, globalChoice));
        var isGlobal = choice == globalChoice;
        var targetPath = isGlobal ? globalPath : Path.Combine(session.Root, RootResolver.ConfigFileName);
        if (targetPath is null)
        {
            Ui.Error("Couldn't resolve a home directory for the global config.");
            return 1;
        }

        // Seed prompt defaults from the *target file's own* values, not the merged
        // view, so we don't present an inherited value as if it lived here.
        var current = RigConfig.Load(targetPath);
        var cov = current.Coverage;

        var changes = new List<(string Display, Func<bool> Apply)>();
        void AddString(string[] path, string value, string display) =>
            changes.Add(($"{string.Join('.', path)} = {display}", () => ConfigWriter.SetString(targetPath, path, value)));
        void AddBool(string[] path, bool value) =>
            changes.Add(($"{string.Join('.', path)} = {(value ? "true" : "false")}", () => ConfigWriter.SetBool(targetPath, path, value)));
        void AddNumber(string[] path, double value) =>
            changes.Add(($"{string.Join('.', path)} = {value.ToString("0.#", CultureInfo.InvariantCulture)}", () => ConfigWriter.SetNumber(targetPath, path, value)));

        // Default project — only meaningful per-repo, and only when ambiguous.
        if (!isGlobal)
        {
            var runnable = ProjectDiscovery.Discover(session.Root, session.Config.Solution, session.Config.Exclude)
                .Where(p => p.IsRunnable).Select(p => p.Name).ToList();
            if (runnable.Count > 1)
            {
                const string skip = "— skip —";
                var pick = AnsiConsole.Prompt(new SelectionPrompt<string>()
                    .Title($"Default project for [aqua]rig run[/] [grey](current: {current.DefaultProject ?? "none"})[/]")
                    .AddChoices([.. runnable, skip]));
                if (pick != skip && pick != current.DefaultProject) AddString(["defaultProject"], pick, pick);
            }
        }

        // Coverage Pro license (recommended global — kept out of source control).
        var licenseSet = !string.IsNullOrWhiteSpace(cov?.License);
        var license = AnsiConsole.Prompt(new TextPrompt<string>(
                $"ReportGenerator Pro license [grey](blank to skip{(licenseSet ? "; currently set" : "")})[/]:")
            .Secret().AllowEmpty());
        if (!string.IsNullOrWhiteSpace(license))
        {
            if (!isGlobal)
                Ui.Warn("A license in the repo's .rig.json is committed; ~/.rig.json keeps it private.");
            AddString(["dotnet", "coverage", "license"], license, "•••• (hidden)");
        }

        // Auto-open the report.
        var openNow = cov?.Open ?? false;
        var open = AnsiConsole.Confirm("Always open the coverage report when it's done?", openNow);
        if (open != openNow) AddBool(["coverage", "open"], open);

        // Full multi-file report by default.
        var fullNow = cov?.Full ?? false;
        var full = AnsiConsole.Confirm("Default to the full multi-file coverage report?", fullNow);
        if (full != fullNow) AddBool(["coverage", "full"], full);

        // Minimum line-coverage gate.
        var minLabel = cov?.Min is { } m ? $"; current {m:0.#}" : "";
        var minStr = AnsiConsole.Prompt(new TextPrompt<string>(
                $"Minimum line-coverage % gate [grey](blank to skip{minLabel})[/]:").AllowEmpty());
        if (double.TryParse(minStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var min) && min > 0)
            AddNumber(["coverage", "min"], min);

        // Preview + confirm.
        if (changes.Count == 0)
        {
            Ui.Info("Nothing to change — you're all set.");
            return 0;
        }

        AnsiConsole.WriteLine();
        Ui.Info($"Will write to {targetPath}:");
        foreach (var (display, _) in changes)
            AnsiConsole.MarkupLine($"  [green]+[/] {Markup.Escape(display)}");

        if (!AnsiConsole.Confirm("Apply these changes?"))
        {
            Ui.Info("No changes written.");
            return 0;
        }

        var written = changes.Count(c => c.Apply());
        if (written < changes.Count)
        {
            Ui.Error($"Couldn't update {targetPath} in place — it has content that can't be safely edited. " +
                     "Fix the file (or remove it) and re-run.");
            return 1;
        }
        Ui.Success($"Wrote {written} setting(s) to {targetPath}");
        return 0;
    }

    private static void ShowDiscovered(RigSession session)
    {
        var projects = ProjectDiscovery.Discover(session.Root, session.Config.Solution, session.Config.Exclude);
        var runnable = projects.Where(p => p.IsRunnable).Select(p => p.Name).ToList();
        var testProject = TestVerb.ResolveTestProject(session, projects);

        Ui.Info("Auto-detected (no need to configure these):");
        AnsiConsole.MarkupLine($"  [grey]runnable:[/] {Markup.Escape(runnable.Count > 0 ? string.Join(", ", runnable) : "(none)")}");
        AnsiConsole.MarkupLine($"  [grey]test project:[/] {Markup.Escape(testProject is null ? "(none)" : Path.GetRelativePath(session.Root, testProject))}");
        AnsiConsole.WriteLine();
    }
}
