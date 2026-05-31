namespace Rig;

/// <summary>
/// Runs a custom verb declared in <c>.rig.json</c> <c>commands</c>. String form
/// goes through the platform shell (pipes / &amp;&amp; / expansion); array form
/// is exec'd directly. Per-command <c>env</c> and <c>cwd</c> are applied; extra
/// CLI args are appended.
/// </summary>
internal static class CommandVerb
{
    public static int Execute(RigSession session, string name, CommandDef def, string[] passthrough)
    {
        var spec = def.Resolve();
        if (spec is null)
        {
            Ui.Error($"Command '{name}' has no command defined for this OS.");
            return 1;
        }

        var cwd = string.IsNullOrEmpty(def.Cwd)
            ? session.Root
            : Path.GetFullPath(Path.Combine(session.Root, def.Cwd));
        var env = session.BuildEnv(def.Env);

        if (spec.IsShell)
        {
            var command = spec.Shell!;
            if (passthrough.Length > 0)
                command += " " + string.Join(' ', passthrough.Select(Exec.QuoteIfNeeded));
            Ui.Shell(command);
            return Exec.RunShell(command, cwd, env);
        }

        var argv = spec.Argv ?? [];
        if (argv.Length == 0)
        {
            Ui.Error($"Command '{name}' has an empty argv.");
            return 1;
        }
        var rest = argv.Skip(1).Concat(passthrough).ToArray();
        Ui.Command(argv[0], rest);
        return Exec.Run(argv[0], rest, cwd, env);
    }
}
