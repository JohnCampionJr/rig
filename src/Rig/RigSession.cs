namespace Rig;

/// <summary>
/// The resolved working context for a `rig` invocation: the repo root, the
/// loaded <see cref="RigConfig"/>, and the layered environment. Verbs receive
/// this and never touch root-resolution / config-loading themselves.
/// </summary>
internal sealed class RigSession
{
    public string Root { get; }
    public RigConfig Config { get; }
    public bool UseDotEnv { get; }

    private readonly Lazy<IReadOnlyDictionary<string, string>> _fileEnv;

    public RigSession(string root, RigConfig config, bool useDotEnv = true)
    {
        Root = root;
        Config = config;
        UseDotEnv = useDotEnv;
        _fileEnv = new Lazy<IReadOnlyDictionary<string, string>>(
            () => useDotEnv ? DotEnv.Load(root) : new Dictionary<string, string>(EnvStack.Comparer));
    }

    /// <summary>The <c>.env</c>/<c>.env.local</c> layer (empty when disabled).</summary>
    public IReadOnlyDictionary<string, string> FileEnv => _fileEnv.Value;

    /// <summary>
    /// The complete environment for a spawned process, or <c>null</c> when there
    /// are no overrides at all (so the child inherits the ambient env unchanged).
    /// Precedence: <c>.env</c> &lt; ambient &lt; <c>.rig.json</c> env &lt; command env.
    /// </summary>
    public IReadOnlyDictionary<string, string>? BuildEnv(IReadOnlyDictionary<string, string>? commandEnv = null)
    {
        var file = FileEnv;
        var config = Config.Env;
        var hasOverrides = file.Count > 0 || (config is { Count: > 0 }) || (commandEnv is { Count: > 0 });
        if (!hasOverrides) return null;
        return EnvStack.Merge(file, EnvStack.Ambient(), config, commandEnv);
    }

    /// <summary>Resolve the root + config from a start directory.</summary>
    public static RigSession Load(string startDir, bool useDotEnv = true)
    {
        var ctx = RootResolver.Resolve(startDir);
        return new RigSession(ctx.Root, RigConfig.Load(ctx.ConfigPath), useDotEnv);
    }
}
