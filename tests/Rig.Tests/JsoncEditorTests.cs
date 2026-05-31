namespace Rig.Tests;

[TestClass]
public sealed class JsoncEditorTests
{
    [TestMethod]
    public void Replaces_existing_value_and_preserves_comments()
    {
        var src = """
            {
              // pick the app to run
              "defaultProject": "Old", // trailing note
              "commands": { "deploy": "./d.sh" }
            }
            """;

        JsoncEditor.TrySetTopLevelString(src, "defaultProject", "New", out var result).Should().BeTrue();

        result.Should().Contain("\"defaultProject\": \"New\"");
        result.Should().NotContain("\"Old\"");
        result.Should().Contain("// pick the app to run");   // leading comment kept
        result.Should().Contain("// trailing note");          // trailing comment kept
        result.Should().Contain("\"deploy\": \"./d.sh\"");    // other keys kept

        // still valid + correct after the edit
        RigConfig.Parse(result).DefaultProject.Should().Be("New");
    }

    [TestMethod]
    public void Inserts_when_absent_keeping_comments_and_members()
    {
        var src = """
            {
              // top comment
              "commands": { "deploy": "./d.sh" }
            }
            """;

        JsoncEditor.TrySetTopLevelString(src, "defaultProject", "App", out var result).Should().BeTrue();

        result.Should().Contain("// top comment");
        // The comment must stay attached to "commands", not get stolen by the
        // newly-inserted key: defaultProject is inserted *before* the comment.
        result.IndexOf("defaultProject", StringComparison.Ordinal)
            .Should().BeLessThan(result.IndexOf("// top comment", StringComparison.Ordinal));
        result.IndexOf("// top comment", StringComparison.Ordinal)
            .Should().BeLessThan(result.IndexOf("commands", StringComparison.Ordinal));

        var cfg = RigConfig.Parse(result);
        cfg.DefaultProject.Should().Be("App");
        cfg.Commands.Should().ContainKey("deploy");
    }

    [TestMethod]
    public void Does_not_match_a_nested_property_of_the_same_name()
    {
        var src = """
            {
              "test": { "defaultProject": "INNER" },
              "defaultProject": "OUTER"
            }
            """;

        JsoncEditor.TrySetTopLevelString(src, "defaultProject", "CHANGED", out var result).Should().BeTrue();

        result.Should().Contain("\"INNER\"");      // nested untouched
        result.Should().Contain("\"CHANGED\"");
        result.Should().NotContain("\"OUTER\"");
    }

    [TestMethod]
    public void Inserts_into_an_empty_object()
    {
        JsoncEditor.TrySetTopLevelString("{}", "defaultProject", "App", out var result).Should().BeTrue();
        RigConfig.Parse(result).DefaultProject.Should().Be("App");
    }

    [TestMethod]
    public void Handles_unicode_without_corrupting_byte_offsets()
    {
        // An em-dash before the target ensures byte offsets != char offsets.
        var src = """
            {
              "note": "build — bundle",
              "defaultProject": "Old"
            }
            """;

        JsoncEditor.TrySetTopLevelString(src, "defaultProject", "New", out var result).Should().BeTrue();
        result.Should().Contain("build — bundle");           // unicode intact
        RigConfig.Parse(result).DefaultProject.Should().Be("New");
    }

    [TestMethod]
    public void Returns_false_on_malformed_input()
    {
        JsoncEditor.TrySetTopLevelString("{ not json", "x", "y", out _).Should().BeFalse();
    }

    // ---- Nested (depth-2) + typed writes ----

    [TestMethod]
    public void Replaces_a_nested_value_and_keeps_sibling_keys_and_comments()
    {
        var src = """
            {
              // coverage prefs
              "coverage": { "license": "OLD", "collector": "mtp" },
              "defaultProject": "App"
            }
            """;

        JsoncEditor.TrySet(src, ["coverage", "license"], "\"NEW\"", out var result).Should().BeTrue();

        result.Should().Contain("\"license\": \"NEW\"");
        result.Should().NotContain("OLD");
        result.Should().Contain("\"collector\": \"mtp\"");   // sibling kept
        result.Should().Contain("// coverage prefs");         // comment kept
        var cov = RigConfig.Parse(result).Coverage!;
        cov.License.Should().Be("NEW");
        cov.Collector.Should().Be("mtp");
    }

    [TestMethod]
    public void Inserts_a_nested_key_into_an_existing_parent_object()
    {
        var src = """
            {
              "coverage": { "license": "KEY" }
            }
            """;

        JsoncEditor.TrySet(src, ["coverage", "open"], "true", out var result).Should().BeTrue();

        var cov = RigConfig.Parse(result).Coverage!;
        cov.License.Should().Be("KEY"); // existing sibling intact
        cov.Open.Should().BeTrue();
    }

    [TestMethod]
    public void Creates_the_parent_object_when_absent()
    {
        var src = """
            {
              "defaultProject": "App"
            }
            """;

        JsoncEditor.TrySet(src, ["coverage", "min"], "80", out var result).Should().BeTrue();

        var cfg = RigConfig.Parse(result);
        cfg.DefaultProject.Should().Be("App"); // untouched
        cfg.Coverage!.Min.Should().Be(80);
    }

    [TestMethod]
    public void Creates_nested_key_in_an_empty_root_and_empty_parent()
    {
        JsoncEditor.TrySet("{}", ["coverage", "open"], "true", out var fromEmptyRoot).Should().BeTrue();
        RigConfig.Parse(fromEmptyRoot).Coverage!.Open.Should().BeTrue();

        JsoncEditor.TrySet("""{ "coverage": {} }""", ["coverage", "full"], "true", out var fromEmptyParent).Should().BeTrue();
        RigConfig.Parse(fromEmptyParent).Coverage!.Full.Should().BeTrue();
    }

    [TestMethod]
    public void Refuses_to_clobber_a_non_object_parent()
    {
        // "coverage" is a string here — merging a child would destroy it, so bail.
        JsoncEditor.TrySet("""{ "coverage": "oops" }""", ["coverage", "open"], "true", out _).Should().BeFalse();
    }
}
