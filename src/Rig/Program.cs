using System.CommandLine;

using Rig;

using Spectre.Console;

// rig entry point: build the command tree, resolve an unambiguous verb prefix,
// then parse + invoke. The wiring lives in *Command.cs; the logic in *Verb.cs.

var root = BuildRoot();

// Split off everything after the first `--` ourselves: those tokens are forwarded
// verbatim and must never be seen by the parser (otherwise the first one binds to
// a verb's optional positional, e.g. `rig run -- migrate` → project "migrate").
var sep = Array.IndexOf(args, "--");
var head = sep < 0 ? args : args[..sep];
Cli.PassThrough = sep < 0 ? [] : args[(sep + 1)..];

var verbs = root.Subcommands.Select(c => c.Name).ToList();
// `watch`/`w` is a leading modifier (→ --watch on the target verb), then resolve
// any unambiguous verb prefix.
var rewritten = PrefixResolver.Resolve(PrefixResolver.ExpandWatch(head), verbs);

try
{
    return root.Parse(rewritten).Invoke();
}
catch (Exception ex)
{
    AnsiConsole.WriteException(ex, ExceptionFormats.ShortenEverything);
    return 1;
}

static RootCommand BuildRoot()
{
    var root = new RootCommand("rig — a general-purpose .NET dev launcher");
    root.Options.Add(Cli.NoEnv);
    root.Options.Add(Cli.Quiet);
    root.Options.Add(Cli.DryRun);

    // System.CommandLine wires `--version` but not a short `-v` — add it.
    foreach (var opt in root.Options)
        if (opt.Name.Contains("version", StringComparison.OrdinalIgnoreCase) && !opt.Aliases.Contains("-v"))
            opt.Aliases.Add("-v");

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
        new OutdatedCommand(),
        new KillCommand(),
        new PublishCommand(),
        new DefaultCommand(),
        new InfoCommand(),
        new InitCommand(),
        new SetupCommand(),
        new CompletionCommand(),
    ];
    foreach (var c in builtins) root.Subcommands.Add(c);

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
