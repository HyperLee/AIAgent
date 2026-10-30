using System.Text.Json;

using AIAgent.Cli.Core;

namespace AIAgent.Cli.Tests;

[TestClass]
public sealed class DelegationContractTests
{
    [TestMethod]
    public void MULTIAC005_WholeArgumentsAreStrictAndPreserveOriginalObjective()
    {
        using var valid = JsonDocument.Parse("""{"tasks":[{"role":"explore","objective":"  inspect  ","scope_path":".","acceptance":"cite sources"}]}""");
        Assert.AreEqual("none", DelegationJson.ParseTasks(valid.RootElement, out var tasks));
        Assert.AreEqual("  inspect  ", tasks![0].Objective);
        foreach (var json in new[] { "{\"tasks\":[]}", "{\"tasks\":null}", "{\"tasks\":[],\"tasks\":[]}", "{\"tasks\":[],\"extra\":1}" })
        { using var doc = JsonDocument.Parse(json); Assert.AreEqual("invalid_arguments", DelegationJson.ParseTasks(doc.RootElement, out _)); }
    }

    [TestMethod]
    public void MULTIAC005_UnicodeAndCharacterBoundariesRejectWithoutNormalizing()
    {
        foreach (var value in new[] { " ", "line\nline", "\tbad", new string('x', 2049) })
        { using var doc = JsonDocument.Parse(JsonSerializer.Serialize(new { tasks = new[] { new { role = "explore", objective = value, scope_path = ".", acceptance = "done" } } })); Assert.AreEqual("invalid_arguments", DelegationJson.ParseTasks(doc.RootElement, out _)); }
        Assert.IsTrue(DelegationJson.ValidText(new string('x', 2048), 2048));
        Assert.IsFalse(DelegationJson.ValidText("\ud800", 2048));
    }
}