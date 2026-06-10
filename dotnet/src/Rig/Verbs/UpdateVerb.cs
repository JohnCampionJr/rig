using System.Diagnostics;
using System.Reflection;
using System.Text.Json;

namespace Rig;

/// <summary>
/// `rig self-update [--check] [--self-only]` — update the rig tool itself to the
/// latest published version (or, with <c>--check</c>, just report whether one's
/// available). On macOS/Linux the running binary can be replaced in place; on
/// Windows rig.exe is locked while running, so it hands off to a detached helper
/// that waits for this process to exit (releasing the lock), then updates — in a
/// new window so the result is visible. Version logic (pure) is
/// <see cref="LatestStable"/> / <see cref="IsNewer"/>.
///
/// The two tools ship in lockstep, so by default this also updates the sibling
/// Node tool (<c>rignode</c>) when it's installed — handed off with
/// <c>--self-only</c> (see <see cref="SiblingArgs"/>) so it can't bounce back.
/// <c>--self-only</c> updates just this ecosystem.
/// </summary>
internal static class UpdateVerb
{
    private const string PackageId = "rig";
    private const string FlatIndex = "https://api.nuget.org/v3-flatcontainer/rig/index.json";

    public static int Execute(RigSession session, bool check, bool selfOnly)
    {
        var selfCode = UpdateSelf(session, check);

        // Keep the lockstep pair in sync: after our own ecosystem, hand off to the
        // sibling's self-update (always with --self-only, so it never bounces back).
        if (selfOnly) return selfCode;

        var siblingCode = UpdateSibling(check);
        return selfCode != 0 ? selfCode : siblingCode;
    }

    /// <summary>The args for handing off to the sibling tool's self-update. Always
    /// carries <c>--self-only</c> so the sibling never cross-updates back to us.</summary>
    public static string[] SiblingArgs(bool check) =>
        check ? ["self-update", "--check", "--self-only"] : ["self-update", "--self-only"];

    private static int UpdateSibling(bool check)
    {
        var tool = Dispatcher.FindNodeTool();
        if (tool is null)
        {
            Ui.Info("Node rig (rignode) isn't installed — nothing else to update.");
            return 0;
        }

        var args = SiblingArgs(check);
        Ui.Info(check ? "Checking the Node rig…" : "Updating the Node rig…");
        Ui.Command(Path.GetFileName(tool), args);
        if (Exec.DryRun) return 0;
        return Dispatcher.RunNodeTool(tool, args);
    }

    private static int UpdateSelf(RigSession session, bool check)
    {
        var current = CurrentVersion();

        List<string> versions;
        try { versions = FetchVersions(); }
        catch (Exception ex)
        {
            Ui.Error($"Couldn't reach nuget.org to check for updates: {ex.Message}");
            return 1;
        }

        var latest = LatestStable(versions);
        if (latest is null) { Ui.Warn($"No published versions of {PackageId} were found."); return 0; }

        if (!IsNewer(current, latest))
        {
            Ui.Success($"rig is up to date (v{current ?? "?"}; latest is v{latest}).");
            return 0;
        }

        Ui.Info($"A newer rig is available: v{current ?? "?"} → v{latest}.");
        if (check)
        {
            Ui.Info("Run `rig self-update` to install it.");
            return 0;
        }
        return RunUpdate(session);
    }

    /// <summary>The highest stable (non-prerelease) version in the list, or null.</summary>
    public static string? LatestStable(IEnumerable<string> versions) =>
        versions
            .Where(v => !v.Contains('-')) // drop prereleases (e.g. 1.2.0-beta)
            .Select(v => (raw: v, ver: Version.TryParse(v, out var p) ? p : null))
            .Where(t => t.ver is not null)
            .OrderByDescending(t => t.ver)
            .Select(t => t.raw)
            .FirstOrDefault();

    /// <summary>True if <paramref name="latest"/> is newer than <paramref name="current"/>.
    /// An unknown/unparseable current version is treated as "update available".</summary>
    public static bool IsNewer(string? current, string latest)
    {
        if (!Version.TryParse(Core(latest), out var l)) return false;
        if (current is null || !Version.TryParse(Core(current), out var c)) return true;
        return l > c;

        // Strip build metadata / prerelease so "1.1.0+sha" and "1.1.0" compare equal.
        static string Core(string v) => v.Split('+', '-')[0];
    }

    public static string? CurrentVersion() =>
        Assembly.GetEntryAssembly()?
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion?.Split('+')[0];

    private static List<string> FetchVersions()
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        var json = http.GetStringAsync(FlatIndex).GetAwaiter().GetResult();
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("versions").EnumerateArray()
            .Select(e => e.GetString())
            .Where(s => s is not null).Cast<string>()
            .ToList();
    }

    private static int RunUpdate(RigSession session)
    {
        string[] args = ["tool", "update", "--global", PackageId];
        Ui.Command("dotnet", args);

        if (!OperatingSystem.IsWindows())
            // Replacing a running binary is fine on Unix — update in place.
            return Exec.Run("dotnet", args, session.Root);

        if (Exec.DryRun) return 0;

        // Windows: rig.exe is locked while we run. Hand off to a detached helper
        // that waits for THIS process to exit (no race), then updates — in a window
        // so the user sees the result. We exit immediately to release the lock.
        var pid = Environment.ProcessId;
        var script =
            $"Wait-Process -Id {pid} -ErrorAction SilentlyContinue; " +
            $"dotnet tool update --global {PackageId}; " +
            "Write-Host ''; Write-Host 'Press any key to close...'; " +
            "$null = $Host.UI.RawUI.ReadKey('NoEcho,IncludeKeyDown')";
        Process.Start(new ProcessStartInfo
        {
            FileName = "powershell",
            Arguments = $"-NoProfile -Command \"{script}\"",
            UseShellExecute = true,                   // detach from our console/process
            WindowStyle = ProcessWindowStyle.Normal,  // visible so the result is seen
        });
        Ui.Info("Updating rig in a new window (it waits for this process to exit first)…");
        return 0;
    }
}
