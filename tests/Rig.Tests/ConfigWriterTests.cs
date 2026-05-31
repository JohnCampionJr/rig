namespace Rig.Tests;

[TestClass]
public sealed class ConfigWriterTests
{
    [TestMethod]
    public void Fresh_file_is_written_with_schema_and_a_nested_value()
    {
        using var t = new TempDir();
        var path = Path.Combine(t.Path, ".rig.json");

        ConfigWriter.SetString(path, ["coverage", "license"], "PRO-KEY");

        var text = File.ReadAllText(path);
        text.Should().Contain(ConfigWriter.SchemaUrl);          // correct (non-stale) $schema
        RigConfig.Parse(text).Coverage!.License.Should().Be("PRO-KEY");
    }

    [TestMethod]
    public void Subsequent_writes_splice_into_the_existing_file()
    {
        using var t = new TempDir();
        var path = Path.Combine(t.Path, ".rig.json");

        ConfigWriter.SetString(path, ["coverage", "license"], "PRO-KEY");
        ConfigWriter.SetBool(path, ["coverage", "open"], true);
        ConfigWriter.SetNumber(path, ["coverage", "min"], 80);
        ConfigWriter.SetString(path, ["defaultProject"], "App");

        var cfg = RigConfig.Parse(File.ReadAllText(path));
        cfg.DefaultProject.Should().Be("App");
        cfg.Coverage!.License.Should().Be("PRO-KEY"); // earlier writes survive later ones
        cfg.Coverage.Open.Should().BeTrue();
        cfg.Coverage.Min.Should().Be(80);
    }

    [TestMethod]
    public void SetString_by_root_returns_the_repo_config_path()
    {
        using var t = new TempDir();
        var returned = ConfigWriter.SetString(t.Path, "defaultProject", "App");

        returned.Should().Be(Path.Combine(t.Path, ".rig.json"));
        RigConfig.Parse(File.ReadAllText(returned)).DefaultProject.Should().Be("App");
    }
}
