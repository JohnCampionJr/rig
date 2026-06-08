namespace Rig.Tests;

[TestClass]
public sealed class PrefixResolverTests
{
    private static readonly string[] Verbs =
        ["run", "build", "rebuild", "test", "coverage", "kill", "publish", "completion"];

    [TestMethod]
    public void Unambiguous_prefix_is_rewritten()
    {
        PrefixResolver.Resolve(["t", "Foo"], Verbs).Should().BeEquivalentTo("test", "Foo");
        PrefixResolver.Resolve(["pub"], Verbs).Should().BeEquivalentTo("publish");
    }

    [TestMethod]
    public void Ambiguous_prefix_is_left_alone()
    {
        // "co" matches both coverage and completion
        PrefixResolver.Resolve(["co"], Verbs).Should().BeEquivalentTo("co");
        // "re" vs "run"/"rebuild" — "re" is only rebuild, but "r" is ambiguous
        PrefixResolver.Resolve(["r"], Verbs).Should().BeEquivalentTo("r");
    }

    [TestMethod]
    public void Exact_match_and_options_pass_through()
    {
        PrefixResolver.Resolve(["test"], Verbs).Should().BeEquivalentTo("test");
        PrefixResolver.Resolve(["--help"], Verbs).Should().BeEquivalentTo("--help");
        PrefixResolver.Resolve([], Verbs).Should().BeEmpty();
    }

    [TestMethod]
    public void Unknown_token_passes_through_for_the_parser_to_handle()
    {
        PrefixResolver.Resolve(["zzz"], Verbs).Should().BeEquivalentTo("zzz");
    }

    [TestMethod]
    public void ExpandWatch_turns_a_leading_watch_modifier_into_a_watch_flag()
    {
        PrefixResolver.ExpandWatch(["w", "r"]).Should().Equal("r", "--watch");
        PrefixResolver.ExpandWatch(["watch", "run", "App"]).Should().Equal("run", "App", "--watch");
        PrefixResolver.ExpandWatch(["watch", "test", "Foo", "-c", "X"]).Should().Equal("test", "Foo", "-c", "X", "--watch");
    }

    [TestMethod]
    public void ExpandWatch_bare_watch_is_empty_and_non_watch_passes_through()
    {
        PrefixResolver.ExpandWatch(["watch"]).Should().BeEmpty();
        PrefixResolver.ExpandWatch(["w"]).Should().BeEmpty();
        PrefixResolver.ExpandWatch(["run", "App"]).Should().Equal("run", "App");
        PrefixResolver.ExpandWatch([]).Should().BeEmpty();
    }

    [TestMethod]
    public void Longer_unambiguous_prefixes_still_resolve_as_a_convenience()
    {
        // Curated short forms (r/c) are native aliases, resolved by the parser;
        // PrefixResolver still handles longer unambiguous prefixes.
        PrefixResolver.Resolve(["cove"], Verbs).Should().BeEquivalentTo("coverage");
        PrefixResolver.Resolve(["reb"], Verbs).Should().BeEquivalentTo("rebuild");
    }
}
