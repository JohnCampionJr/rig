using System.Text.Json;

namespace Rig;

/// <summary>
/// Which <c>dotnet test</c> CLI grammar applies. The SDK ships two distinct
/// command parsers and the choice is made <em>solely</em> by <c>global.json</c>:
/// a <c>test.runner</c> of <c>Microsoft.Testing.Platform</c> selects the MTP
/// parser; anything else (or no <c>global.json</c>) keeps the classic VSTest
/// parser. A project's own MTP opt-in props (<c>EnableMicrosoftTestingPlatform</c>,
/// <c>EnableMSTestRunner</c>, …) do <em>not</em> switch the CLI grammar — verified
/// against the SDK: with every MTP prop set but no <c>global.json</c>, <c>dotnet
/// test --project …</c> still fails with <c>MSB1001: Unknown switch</c>.
///
/// The two grammars differ only in how the project is named, and (for coverage)
/// how collection is requested:
/// <list type="bullet">
///   <item>VSTest — positional project, <c>--collect:"XPlat Code Coverage"</c></item>
///   <item>MTP — <c>--project</c>, <c>-- --coverage</c></item>
/// </list>
/// The <c>--filter &lt;expr&gt;</c> form (<c>FullyQualifiedName~Foo</c>,
/// <c>TestCategory=…</c>) is shared by both runners, so once the project arg is
/// right the same filter expression works everywhere.
/// </summary>
internal static class TestPlatform
{
    public enum Runner { VsTest, Mtp }

    private const StringComparison OIC = StringComparison.OrdinalIgnoreCase;
    private const string MtpRunner = "Microsoft.Testing.Platform";

    /// <summary>Resolve the runner. An explicit override wins (<c>mtp</c> →
    /// MTP; <c>xplat</c>/<c>vstest</c> → VSTest); otherwise the nearest
    /// <c>global.json</c> at or above <paramref name="root"/> decides.</summary>
    public static Runner Detect(string root, string? configured)
    {
        if (string.Equals(configured, "mtp", OIC)) return Runner.Mtp;
        if (string.Equals(configured, "xplat", OIC) || string.Equals(configured, "vstest", OIC))
            return Runner.VsTest;
        return UsesMtpCli(root) ? Runner.Mtp : Runner.VsTest;
    }

    /// <summary>True when the nearest <c>global.json</c> opts into the
    /// Microsoft.Testing.Platform <c>dotnet test</c> parser. Tolerant: a
    /// missing/garbled file (or no <c>test.runner</c>) means classic VSTest.</summary>
    internal static bool UsesMtpCli(string root)
    {
        for (var dir = root; dir is not null; dir = Path.GetDirectoryName(dir))
        {
            var path = Path.Combine(dir, "global.json");
            if (!File.Exists(path)) continue;
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(path));
                if (doc.RootElement.TryGetProperty("test", out var test) &&
                    test.TryGetProperty("runner", out var runner) &&
                    runner.ValueKind == JsonValueKind.String)
                    return string.Equals(runner.GetString(), MtpRunner, OIC);
            }
            catch { /* unreadable global.json → classic VSTest */ }
            return false; // the nearest global.json wins, runner pin or not
        }
        return false;
    }
}
