namespace Rig.Tests;

/// <summary>
/// Phase 0 smoke test: proves the test harness (MSTest on Microsoft.Testing.Platform)
/// and AwesomeAssertions are wired correctly and the project references the tool
/// under test. Real coverage of discovery/config/verbs arrives with Phases 1–3.
/// </summary>
[TestClass]
public sealed class SmokeTests
{
    [TestMethod]
    public void Harness_and_assertions_are_wired()
    {
        var answer = 40 + 2;
        answer.Should().Be(42);
    }
}
