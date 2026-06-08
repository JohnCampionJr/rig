namespace Rig.Tests;

[TestClass]
public sealed class TestEnumerationTests
{
    // ---- pure classification predicate ----

    [TestMethod]
    public void Detects_mstest_via_type_attribute()
    {
        TestEnumeration.IsTestClass(
            ["Microsoft.VisualStudio.TestTools.UnitTesting.TestClassAttribute"], [])
            .Should().BeTrue();
    }

    [TestMethod]
    public void Detects_nunit_fixture_and_test_method()
    {
        TestEnumeration.IsTestClass(["NUnit.Framework.TestFixtureAttribute"], []).Should().BeTrue();
        TestEnumeration.IsTestClass([], ["NUnit.Framework.TestAttribute"]).Should().BeTrue();
    }

    [TestMethod]
    public void Detects_xunit_fact_and_theory_methods()
    {
        TestEnumeration.IsTestClass([], ["Xunit.FactAttribute"]).Should().BeTrue();
        TestEnumeration.IsTestClass([], ["Xunit.TheoryAttribute"]).Should().BeTrue();
    }

    [TestMethod]
    public void Plain_class_is_not_a_test_class()
    {
        TestEnumeration.IsTestClass(["System.SerializableAttribute"], ["System.ObsoleteAttribute"])
            .Should().BeFalse();
    }

    // ---- integration: enumerate this very test assembly (net8, MSTest) ----

    [TestMethod]
    public void Enumerates_real_mstest_classes_from_this_assembly()
    {
        var dll = typeof(TestEnumerationTests).Assembly.Location;

        var classes = TestEnumeration.Enumerate(dll);

        classes.Should().Contain("Rig.Tests.TestEnumerationTests");
        classes.Should().Contain("Rig.Tests.DotEnvTests");
        classes.Should().NotContain("Rig.Tests.TempDir"); // plain helper, not a test class
    }

    // ---- cross-TFM verification gate (Phase 2 risk #1) ----
    // Host is net8; this proves a net8 host can read a higher-TFM (e.g. net10)
    // test DLL via MetadataLoadContext. Host-agnostic: only runs when
    // RIG_GATE_TEST_DLL points at such an assembly, otherwise inconclusive.

    [TestMethod]
    public void Cross_tfm_gate_reads_a_higher_tfm_assembly()
    {
        var dll = Environment.GetEnvironmentVariable("RIG_GATE_TEST_DLL");
        if (string.IsNullOrEmpty(dll) || !File.Exists(dll))
        {
            Assert.Inconclusive("Set RIG_GATE_TEST_DLL to a higher-TFM test DLL to exercise this gate.");
            return;
        }

        var classes = TestEnumeration.Enumerate(dll);
        classes.Should().NotBeEmpty("a net8 host should enumerate test classes from a higher-TFM assembly");
    }
}
