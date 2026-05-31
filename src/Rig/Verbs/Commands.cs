using System.CommandLine;

namespace Rig;

// Thin System.CommandLine wiring — one Command subclass per verb. Each holds its
// options/arguments as fields, wires completion, and delegates to the Phase 2
// verb logic in its action. No business logic lives here.

internal sealed class RunCommand : Command
{
    private readonly Argument<string?> _project =
        new("project") { Arity = ArgumentArity.ZeroOrOne, HelpName = "project", Description = "Project (substring / short-name match)" };
    private readonly Option<bool> _remember =
        new("--remember") { Description = "Persist this project as defaultProject in .rig.json" };
    private readonly Option<bool> _watch =
        new("--watch", "-w") { Description = "Run under dotnet watch (hot reload)" };
    private readonly Option<string?> _configuration =
        new("--configuration", "-c") { Description = "Build configuration (e.g. Release)" };
    private readonly Option<string?> _framework =
        new("--framework", "-f") { Description = "Target framework for a multi-TFM project (e.g. -f net10.0)" };
    private readonly Option<string?> _launchProfile =
        new("--launch-profile") { Description = "launchSettings.json profile to run" };

    public RunCommand() : base("run", "Run a runnable project (args after -- are forwarded)")
    {
        Aliases.Add("r");
        TreatUnmatchedTokensAsErrors = false;
        _project.CompletionSources.Add(_ => Completions.RunnableProjects());
        Arguments.Add(_project);
        Options.Add(_remember);
        Options.Add(_watch);
        Options.Add(_configuration);
        Options.Add(_framework);
        Options.Add(_launchProfile);
        SetAction(pr => RunVerb.Execute(
            Cli.Session(pr), pr.GetValue(_project), Cli.Forwarded(pr),
            pr.GetValue(_remember), pr.GetValue(_watch), pr.GetValue(_configuration),
            pr.GetValue(_framework), pr.GetValue(_launchProfile)));
    }
}

internal sealed class BuildCommand : Command
{
    private readonly Option<bool> _watch = new("--watch", "-w") { Description = "Build under dotnet watch" };

    public BuildCommand() : base("build", "Build the solution")
    {
        Aliases.Add("b");
        TreatUnmatchedTokensAsErrors = false;
        Options.Add(_watch);
        SetAction(pr => BuildVerb.Execute(Cli.Session(pr), Cli.Forwarded(pr), pr.GetValue(_watch)));
    }
}

internal sealed class RebuildCommand : Command
{
    private readonly Option<bool> _dryRun = new("--dry-run") { Description = "List the bin/obj dirs that would be removed, without deleting" };

    public RebuildCommand() : base("rebuild", "Delete in-tree bin/obj, then build")
    {
        Aliases.Add("rb");
        TreatUnmatchedTokensAsErrors = false;
        Options.Add(_dryRun);
        SetAction(pr => RebuildVerb.Execute(Cli.Session(pr), Cli.Forwarded(pr), pr.GetValue(_dryRun)));
    }
}

internal sealed class TestCommand : Command
{
    private readonly Argument<string?> _name =
        new("name") { Arity = ArgumentArity.ZeroOrOne, HelpName = "name", Description = "Class/method name, or ~ = !~ != filter shorthand" };
    private readonly Option<bool> _log = new("--log") { Description = "Apply the test.envPresets 'log' bundle" };
    private readonly Option<string?> _filter = new("--filter") { Description = "Raw test-platform filter expression" };
    private readonly Option<bool> _watch = new("--watch", "-w") { Description = "Run under dotnet watch (re-run on change)" };
    private readonly Option<string?> _framework =
        new("--framework", "-f") { Description = "Target framework for a multi-TFM project (e.g. -f net10.0)" };

    public TestCommand() : base("test", "Run tests")
    {
        Aliases.Add("t");
        TreatUnmatchedTokensAsErrors = false;
        _name.CompletionSources.Add(_ => Completions.TestClasses());
        Arguments.Add(_name);
        Options.Add(_log);
        Options.Add(_filter);
        Options.Add(_watch);
        Options.Add(_framework);
        SetAction(pr => TestVerb.Execute(
            Cli.Session(pr), pr.GetValue(_name), pr.GetValue(_log), pr.GetValue(_filter),
            Cli.Forwarded(pr), pr.GetValue(_watch), pr.GetValue(_framework)));
    }
}

internal sealed class CoverageCommand : Command
{
    private readonly Argument<string?> _name = new("name")
        { Arity = ArgumentArity.ZeroOrOne, HelpName = "name", Description = "Scope coverage to a test class/method (substring or ~/= filter)" };
    private readonly Option<bool> _full = new("--full") { Description = "Full multi-file report (default: single-file inline)" };
    private readonly Option<bool> _open = new("--open") { Description = "Open the report when done" };
    private readonly Option<double?> _min = new("--min") { Description = "Fail (non-zero exit) if line coverage % is below this — e.g. --min 80" };

    public CoverageCommand() : base("coverage", "Run tests with coverage and render an HTML report")
    {
        Aliases.Add("c");
        _name.CompletionSources.Add(_ => Completions.TestClasses());
        Arguments.Add(_name);
        Options.Add(_full);
        Options.Add(_open);
        Options.Add(_min);
        SetAction(pr => CoverageVerb.Execute(
            Cli.Session(pr), pr.GetValue(_name), pr.GetValue(_full), pr.GetValue(_open), pr.GetValue(_min)));
    }
}

internal sealed class KillCommand : Command
{
    public KillCommand() : base("kill", "Terminate matching app/test processes")
    {
        Aliases.Add("k");
        SetAction(pr =>
        {
            var s = Cli.Session(pr);
            var projects = ProjectDiscovery.Discover(s.Root, s.Config.Solution);
            return KillVerb.Execute(s, projects);
        });
    }
}

internal sealed class PublishCommand : Command
{
    private readonly Argument<string?> _project = new("project")
        { Arity = ArgumentArity.ZeroOrOne, HelpName = "project", Description = "Project to publish (defaults to defaultProject / the sole runnable)" };

    public PublishCommand() : base("publish", "Self-contained dotnet publish")
    {
        Aliases.Add("pub");
        _project.CompletionSources.Add(_ => Completions.RunnableProjects());
        Arguments.Add(_project);
        SetAction(pr => PublishVerb.Execute(Cli.Session(pr), pr.GetValue(_project)));
    }
}

internal sealed class DefaultCommand : Command
{
    private readonly Argument<string?> _project =
        new("project") { Arity = ArgumentArity.ZeroOrOne, HelpName = "project", Description = "Project to set as default (omit to pick / show current)" };

    public DefaultCommand() : base("default", "Show or set the default run project")
    {
        Aliases.Add("def");
        _project.CompletionSources.Add(_ => Completions.RunnableProjects());
        Arguments.Add(_project);
        SetAction(pr => DefaultVerb.Execute(Cli.Session(pr), pr.GetValue(_project)));
    }
}

internal sealed class InfoCommand : Command
{
    public InfoCommand() : base("info", "Show what rig discovered/resolved for this repo")
    {
        Aliases.Add("i");
        SetAction(pr => InfoVerb.Execute(Cli.Session(pr)));
    }
}

internal sealed class InitCommand : Command
{
    public InitCommand() : base("init", "Scaffold a commented .rig.json")
    {
        SetAction(pr => InitVerb.Execute(Cli.Session(pr)));
    }
}

internal sealed class SetupCommand : Command
{
    public SetupCommand() : base("setup", "Interactive walkthrough to set local/global preferences")
    {
        SetAction(pr => SetupVerb.Execute(Cli.Session(pr)));
    }
}

internal sealed class CompletionCommand : Command
{
    private readonly Argument<string?> _shell = new("shell") { Arity = ArgumentArity.ZeroOrOne, Description = "zsh | bash | pwsh" };

    public CompletionCommand() : base("completion", "Print shell completion setup (zsh/bash/pwsh)")
    {
        Aliases.Add("comp");
        Arguments.Add(_shell);
        SetAction(pr => CompletionSetup.Print(pr.GetValue(_shell)));
    }
}

/// <summary>A custom verb declared in <c>.rig.json</c> <c>commands</c>;
/// one instance is constructed per entry and dispatches to <see cref="CommandVerb"/>.</summary>
internal sealed class CustomCommand : Command
{
    private readonly string _name;
    private readonly CommandDef _def;

    public CustomCommand(string name, CommandDef def) : base(name, def.Description ?? $"custom: {name}")
    {
        _name = name;
        _def = def;
        TreatUnmatchedTokensAsErrors = false;
        SetAction(pr => CommandVerb.Execute(Cli.Session(pr), _name, _def, Cli.Forwarded(pr)));
    }
}
