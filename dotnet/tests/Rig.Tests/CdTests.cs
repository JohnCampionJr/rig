namespace Rig.Tests;

[TestClass]
public sealed class CdTests
{
    private static CdVerb.Target T(string name, string dir, string rel) => new(name, dir, rel);

    private static List<CdVerb.Target> Sample() =>
    [
        T("(root)", "/repo", "."),
        T("Acme.Web", "/repo/src/web", "src/web"),
        T("Acme.Api", "/repo/src/api", "src/api"),
        T("Acme.Web.Tests", "/repo/tests/web", "tests/web"),
    ];

    [TestMethod]
    public void Exact_short_name_wins()
    {
        CdVerb.Rank(Sample(), "api").First().Name.Should().Be("Acme.Api");
    }

    [TestMethod]
    public void No_match_returns_empty()
    {
        CdVerb.Rank(Sample(), "zzz").Should().BeEmpty();
    }

    [TestMethod]
    public void Path_aware_matches_the_directory_basename()
    {
        var targets = new List<CdVerb.Target> { T("Acme.Core", "/repo/libs/dashboard", "libs/dashboard") };
        CdVerb.Rank(targets, "dashboard").First().Dir.Should().Be("/repo/libs/dashboard");
    }

    [TestMethod]
    public void Subsequence_matches()
    {
        var targets = new List<CdVerb.Target> { T("Acme.Web", "/repo/apps/web", "apps/web") };
        CdVerb.Rank(targets, "aw").First().Name.Should().Be("Acme.Web"); // "aw" ⊂ "apps/web"
    }

    [TestMethod]
    public void Name_match_outranks_a_path_only_match()
    {
        // both Acme.Web and Acme.Web.Tests have a "web" directory; the one that
        // also matches by name wins.
        CdVerb.Rank(Sample(), "web").First().Name.Should().Be("Acme.Web");
    }
}
