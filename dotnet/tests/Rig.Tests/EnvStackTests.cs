namespace Rig.Tests;

[TestClass]
public sealed class EnvStackTests
{
    private static Dictionary<string, string> Map(params (string, string)[] pairs)
    {
        var d = new Dictionary<string, string>(EnvStack.Comparer);
        foreach (var (k, v) in pairs) d[k] = v;
        return d;
    }

    [TestMethod]
    public void Precedence_is_file_then_ambient_then_config_then_command()
    {
        var fileEnv = Map(("A", "file"), ("B", "file"), ("C", "file"), ("D", "file"));
        var ambient = Map(("B", "ambient"), ("C", "ambient"), ("D", "ambient"));
        var config = Map(("C", "config"), ("D", "config"));
        var command = Map(("D", "command"));

        var merged = EnvStack.Merge(fileEnv, ambient, config, command);

        merged["A"].Should().Be("file");      // only in file
        merged["B"].Should().Be("ambient");    // ambient beats file (dotenv-style)
        merged["C"].Should().Be("config");     // config beats ambient
        merged["D"].Should().Be("command");    // per-command wins
    }

    [TestMethod]
    public void Null_layers_are_ignored()
    {
        var merged = EnvStack.Merge(null, Map(("X", "1")), null, null);
        merged.Should().ContainKey("X");
        merged["X"].Should().Be("1");
    }
}
