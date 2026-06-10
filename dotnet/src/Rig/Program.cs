using System.CommandLine;

using Rig;

using Spectre.Console;

// rig entry point: build the command tree, resolve an unambiguous verb prefix,
// then parse + invoke. The wiring lives in *Command.cs; the logic in *Verb.cs.

// Other-awareness: in a Node project, hand off to the Node tool (rig-node) so a
// single `rig` works in either ecosystem, whichever tool wins on PATH.
if (Dispatcher.MaybeDelegate(args) is int delegated)
    return delegated;

// Split off everything after the first `--` ourselves: those tokens are forwarded
// verbatim and must never be seen by the parser (otherwise the first one binds to
// a verb's optional positional, e.g. `rig run -- migrate` → project "migrate").
var sep = Array.IndexOf(args, "--");
var head = sep < 0 ? args : args[..sep];
Cli.PassThrough = sep < 0 ? [] : args[(sep + 1)..];

// Shell completion: build the command tree WITHOUT verbs that can't apply here
// (no test project → no test/coverage; no runnable → no run/publish), so the
// generated suggestions are filtered — matching the menu and the Node tool.
var forCompletion = head.Length > 0 && head[0].StartsWith("[suggest", StringComparison.Ordinal);
var root = forCompletion ? BuildRoot(CompletionCaps()) : BuildRoot();

var verbs = root.Subcommands.Select(c => c.Name).ToList();
// `watch`/`w` is a leading modifier (→ --watch on the target verb), then resolve
// any unambiguous verb prefix.
var rewritten = PrefixResolver.Resolve(PrefixResolver.ExpandWatch(head), verbs);

// An unknown leading verb gets a friendly nudge (mirroring the Node tool) instead
// of System.CommandLine's "Unrecognized command or argument" + full help dump.
// Option-like tokens (`-…`), directives (`[suggest]` → `[…]`), and bare `rig`
// fall through to normal parsing / completion / the menu.
if (rewritten.Length > 0 && rewritten[0].Length > 0 && rewritten[0][0] != '-' && rewritten[0][0] != '['
    && !root.Subcommands.SelectMany(c => c.Aliases.Prepend(c.Name))
            .Contains(rewritten[0], StringComparer.OrdinalIgnoreCase))
{
    Ui.Error($"unknown verb \"{rewritten[0]}\". Run `rig` for the menu or `rig --help`.");
    return 1;
}

try
{
    return root.Parse(rewritten).Invoke();
}
catch (Exception ex)
{
    AnsiConsole.WriteException(ex, ExceptionFormats.ShortenEverything);
    return 1;
}

// Capabilities for the completion path — permissive on any failure so a hiccup
// never hides verbs from tab-completion.
static Rig.Capabilities CompletionCaps()
{
    try { return Rig.Capabilities.Probe(RigSession.Load(Directory.GetCurrentDirectory())); }
    catch { return Rig.Capabilities.All; }
}

// `caps` non-null (completion path) drops verbs that can't apply here, so the
// suggestions are filtered; null (normal path) registers them all, keeping the
// CLI's own clean "no test project found" / "no runnable" messages.
static RootCommand BuildRoot(Rig.Capabilities? caps = null)
{
    var root = new RootCommand("rig — a general-purpose .NET dev launcher");
    root.Options.Add(Cli.NoEnv);
    root.Options.Add(Cli.Quiet);
    root.Options.Add(Cli.DryRun);

    // System.CommandLine wires `--version` but not a short `-v` — add it, and
    // swap its action so the output is the cleaned, ecosystem-tagged line
    // ("1.4.0 (.NET)") rather than the bare "1.4.0+<git-sha>" default.
    foreach (var opt in root.Options)
        if (opt.Name.Contains("version", StringComparison.OrdinalIgnoreCase))
        {
            if (!opt.Aliases.Contains("-v"))
                opt.Aliases.Add("-v");
            opt.Action = new RigVersionAction();
        }

    Command[] builtins =
    [
        new RunCommand(),
        new BuildCommand(),
        new RebuildCommand(),
        new RestoreCommand(),
        new CleanCommand(),
        new FormatCommand(),
        new TestCommand(),
        new CoverageCommand(),
        new AddCommand(),
        new RemoveCommand(),
        new GlobalCommand(),
        new DlxCommand(),
        new OutdatedCommand(),
        new KillCommand(),
        new PublishCommand(),
        new DefaultCommand(),
        new InfoCommand(),
        new CdCommand(),
        new DoctorCommand(),
        new InitCommand(),
        new SetupCommand(),
        new UpdateCommand(),
        new CompletionCommand(),
    ];
    foreach (var c in builtins)
        if (caps?.Unavailable(c.Name) is null) // null caps → keep all
            root.Subcommands.Add(c);

    // Built-in verbs ship curated default aliases (in their constructors). The
    // repo's .rig.json can override any of them and name custom verbs' aliases.
    var config = TryLoadConfig();
    AddCustomCommands(root, config);
    ApplyAliasOverrides(root, config);

    // Bare `rig` → interactive menu (which re-enters this same parser).
    root.SetAction(_ => Menu.Run(root));
    return root;
}

static RigConfig? TryLoadConfig()
{
    try { return RigSession.Load(Directory.GetCurrentDirectory()).Config; }
    catch { return null; } // a malformed .rig.json shouldn't break the CLI
}

static void AddCustomCommands(RootCommand root, RigConfig? config)
{
    if (config?.Commands is null) return;
    foreach (var (name, def) in config.Commands)
    {
        if (root.Subcommands.Any(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase)))
            continue; // built-ins win over a same-named custom verb
        root.Subcommands.Add(new CustomCommand(name, def));
    }
}

// Replace a verb's alias(es) with the configured one. `aliases` maps verb name →
// short alias and applies to built-ins and custom verbs alike.
static void ApplyAliasOverrides(RootCommand root, RigConfig? config)
{
    if (config?.Aliases is not { Count: > 0 } aliases) return;
    foreach (var c in root.Subcommands)
    {
        if (!aliases.TryGetValue(c.Name, out var alias) || string.IsNullOrWhiteSpace(alias)) continue;
        c.Aliases.Clear();
        c.Aliases.Add(alias);
    }
}
