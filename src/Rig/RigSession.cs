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

    /// <summary>Resolve the root + config from a start directory. The user-wide
    /// <c>~/.rig.json</c> (if present) is loaded first and the repo's config is
    /// layered on top, so per-repo settings win over personal defaults.</summary>
    public static RigSession Load(string startDir, bool useDotEnv = true)
    {
        var ctx = RootResolver.Resolve(startDir);
        var repo = RigConfig.Load(ctx.ConfigPath);
        var global = GlobalConfigPath();
        // Skip the global merge when it would be the very file we just loaded
        // (running rig inside the home dir that anchors the repo).
        var config = global is not null && !PathEquals(global, ctx.ConfigPath)
            ? RigConfig.Merge(RigConfig.Load(global), repo)
            : repo;
        return new RigSession(ctx.Root, config, useDotEnv);
    }

    /// <summary>The user-wide config path: <c>$RIG_GLOBAL_CONFIG</c> if set,
    /// otherwise <c>~/.rig.json</c>. Null only when there's no home directory.
    /// Existence isn't checked here — a missing file loads as empty.</summary>
    public static string? GlobalConfigPath()
    {
        var custom = Environment.GetEnvironmentVariable("RIG_GLOBAL_CONFIG");
        if (!string.IsNullOrEmpty(custom)) return custom;
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return string.IsNullOrEmpty(home) ? null : Path.Combine(home, ".rig.json");
    }

    private static bool PathEquals(string a, string? b) =>
        b is not null && string.Equals(Path.GetFullPath(a), Path.GetFullPath(b),
            OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal);
}
