namespace Rig.Tests;

[TestClass]
public sealed class GlobTests
{
    [TestMethod]
    [DataRow("*Bench", "App.Bench", true)]
    [DataRow("*.Demo", "Acme.Foundation.Demo", true)]
    [DataRow("*Spike", "MicaSpike", true)]
    [DataRow("samples/*", "samples/Foo/Foo.csproj", true)]
    [DataRow("App?", "App1", true)]
    [DataRow("App?", "App", false)]      // ? requires exactly one char
    [DataRow("*.Demo", "Demo.App", false)]
    [DataRow("Exact", "Exact", true)]
    [DataRow("Exact", "Exactly", false)] // anchored: full match only
    public void Matches_with_star_and_question_anchored_and_case_insensitive(string pattern, string input, bool expected)
    {
        Glob.IsMatch(pattern, input).Should().Be(expected);
        Glob.IsMatch(pattern.ToUpperInvariant(), input).Should().Be(expected); // case-insensitive
    }
}
