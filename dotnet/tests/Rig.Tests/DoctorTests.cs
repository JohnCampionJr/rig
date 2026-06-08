namespace Rig.Tests;

[TestClass]
public sealed class DoctorTests
{
    [TestMethod]
    public void SdkSatisfies_is_true_for_same_or_newer_major()
    {
        DoctorVerb.SdkSatisfies("9.0.100", "9.0.300").Should().BeTrue();  // same major
        DoctorVerb.SdkSatisfies("10.0.100", "9.0.100").Should().BeTrue(); // newer major
    }

    [TestMethod]
    public void SdkSatisfies_is_false_when_installed_major_is_older()
    {
        DoctorVerb.SdkSatisfies("8.0.400", "9.0.100").Should().BeFalse();
    }

    [TestMethod]
    public void SdkSatisfies_defers_to_satisfied_when_a_pin_is_absent_or_unparseable()
    {
        DoctorVerb.SdkSatisfies("9.0.100", null).Should().BeTrue();
        DoctorVerb.SdkSatisfies("9.0.100", "").Should().BeTrue();
        DoctorVerb.SdkSatisfies("not-a-version", "9.0.100").Should().BeTrue();
    }

    [TestMethod]
    public void ReadSdkPin_returns_the_pinned_version_or_null()
    {
        using var pinned = new TempDir();
        pinned.Write("global.json", """{ "sdk": { "version": "9.0.100", "rollForward": "latestMinor" } }""");
        DoctorVerb.ReadSdkPin(pinned.Path).Should().Be("9.0.100");

        using var unpinned = new TempDir();
        unpinned.Write("global.json", """{ "test": { "runner": "Microsoft.Testing.Platform" } }""");
        DoctorVerb.ReadSdkPin(unpinned.Path).Should().BeNull();

        using var none = new TempDir();
        DoctorVerb.ReadSdkPin(none.Path).Should().BeNull();
    }

    [TestMethod]
    public void ReadSdkPin_finds_a_global_json_in_an_ancestor_directory()
    {
        using var t = new TempDir();
        t.Write("global.json", """{ "sdk": { "version": "8.0.0" } }""");
        var nested = t.Dir("src", "App");
        DoctorVerb.ReadSdkPin(nested).Should().Be("8.0.0");
    }
}
