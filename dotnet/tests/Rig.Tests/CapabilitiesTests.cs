namespace Rig.Tests;

[TestClass]
public sealed class CapabilitiesTests
{
    [TestMethod]
    public void Run_and_publish_unavailable_without_runnable_projects()
    {
        var caps = new Capabilities(HasSolution: true, RunnableProjects: 0, HasTestProject: true);
        caps.Unavailable("run").Should().NotBeNull();
        caps.Unavailable("publish").Should().NotBeNull();
        caps.Unavailable("test").Should().BeNull();
    }

    [TestMethod]
    public void Test_and_coverage_unavailable_without_a_test_project()
    {
        var caps = new Capabilities(HasSolution: true, RunnableProjects: 2, HasTestProject: false);
        caps.Unavailable("test").Should().NotBeNull();
        caps.Unavailable("coverage").Should().NotBeNull();
        caps.Unavailable("run").Should().BeNull();
    }

    [TestMethod]
    public void Build_rebuild_kill_and_custom_are_always_available()
    {
        var caps = new Capabilities(HasSolution: false, RunnableProjects: 0, HasTestProject: false);
        caps.Unavailable("build").Should().BeNull();
        caps.Unavailable("rebuild").Should().BeNull();
        caps.Unavailable("kill").Should().BeNull();
        caps.Unavailable("deploy").Should().BeNull(); // custom verb
    }

    [TestMethod]
    public void Probe_on_an_empty_dir_reports_nothing_available()
    {
        using var t = new TempDir();
        var caps = Capabilities.Probe(new RigSession(t.Path, new RigConfig()));

        caps.HasSolution.Should().BeFalse();
        caps.RunnableProjects.Should().Be(0);
        caps.HasTestProject.Should().BeFalse();
    }
}
