namespace Rig.Tests;

[TestClass]
public sealed class InfoInitTests
{
    [TestMethod]
    public void Init_creates_a_template_when_absent()
    {
        using var t = new TempDir();
        var rc = InitVerb.Execute(new RigSession(t.Path, new RigConfig()));

        rc.Should().Be(0);
        var path = Path.Combine(t.Path, ".rig.json");
        File.Exists(path).Should().BeTrue();

        // the scaffold is valid JSONC and parses to an (otherwise-empty) config
        var text = File.ReadAllText(path);
        text.Should().Contain("$schema");
        RigConfig.Load(path).DefaultProject.Should().BeNull();
    }

    [TestMethod]
    public void Init_refuses_to_overwrite_an_existing_file()
    {
        using var t = new TempDir();
        t.Write(".rig.json", """{ "defaultProject": "Keep" }""");

        InitVerb.Execute(new RigSession(t.Path, new RigConfig())).Should().Be(1);

        // untouched
        RigConfig.Load(Path.Combine(t.Path, ".rig.json")).DefaultProject.Should().Be("Keep");
    }

    [TestMethod]
    public void Info_runs_and_succeeds_on_an_empty_repo()
    {
        using var t = new TempDir();
        InfoVerb.Execute(new RigSession(t.Path, new RigConfig())).Should().Be(0);
    }
}
