namespace Rig.Tests;

[TestClass]
public sealed class DotEnvTests
{
    [TestMethod]
    public void Double_quoted_value_keeps_an_escaped_quote()
    {
        // .env line:  KEY="a\"b c"   → value a"b c (the escaped quote isn't the end)
        var env = DotEnv.Parse("KEY=\"a\\\"b c\"");
        env["KEY"].Should().Be("a\"b c");
    }

    [TestMethod]
    public void Parses_basic_pairs_comments_and_blanks()
    {
        var env = DotEnv.Parse("""
            # a comment
            FOO=bar

            BAZ=qux
            """);

        env.Should().HaveCount(2);
        env["FOO"].Should().Be("bar");
        env["BAZ"].Should().Be("qux");
    }

    [TestMethod]
    public void Strips_export_prefix()
    {
        var env = DotEnv.Parse("export TOKEN=abc123");
        env["TOKEN"].Should().Be("abc123");
    }

    [TestMethod]
    public void Double_quotes_honour_escapes_single_quotes_are_literal()
    {
        var env = DotEnv.Parse("""
            D="line1\nline2"
            S='line1\nline2'
            """);

        env["D"].Should().Be("line1\nline2");
        env["S"].Should().Be("line1\\nline2");
    }

    [TestMethod]
    public void Strips_inline_comment_on_unquoted_value_only()
    {
        var env = DotEnv.Parse("""
            A=value # trailing comment
            B="value # not a comment"
            """);

        env["A"].Should().Be("value");
        env["B"].Should().Be("value # not a comment");
    }

    [TestMethod]
    public void Skips_invalid_keys()
    {
        var env = DotEnv.Parse("""
            1BAD=x
            good_KEY=y
            =novalue
            """);

        env.Should().ContainKey("good_KEY");
        env.Should().NotContainKey("1BAD");
        env.Should().HaveCount(1);
    }

    [TestMethod]
    public void Load_overlays_env_local_over_env()
    {
        using var t = new TempDir();
        t.Write(".env", "SHARED=base\nONLY_BASE=1");
        t.Write(".env.local", "SHARED=override\nONLY_LOCAL=2");

        var env = DotEnv.Load(t.Path);

        env["SHARED"].Should().Be("override");
        env["ONLY_BASE"].Should().Be("1");
        env["ONLY_LOCAL"].Should().Be("2");
    }

    [TestMethod]
    public void Load_returns_empty_when_no_files()
    {
        using var t = new TempDir();
        DotEnv.Load(t.Path).Should().BeEmpty();
    }
}
