using AIAgent.Cli.Cli;
using AIAgent.Cli.Core;

namespace AIAgent.Cli.Tests;

[TestClass]
public sealed class WorkCommandTests
{
    private const string Id = "0123456789abcdef0123456789abcdef";

    [TestMethod]
    public void SAVEAC002_ExactCommandsAndOriginalTitle()
    {
        var save = WorkCommands.Parse("  /work save   中文 😀  ");
        Assert.AreEqual(WorkCommandKind.Save, save.Kind);
        Assert.AreEqual("  中文 😀  ", save.Value);
        Assert.AreEqual(WorkCommandKind.List, WorkCommands.Parse(" /work list ").Kind);
        Assert.AreEqual(WorkCommandKind.Show, WorkCommands.Parse("/work show " + Id).Kind);
        Assert.AreEqual(WorkCommandKind.Delete, WorkCommands.Parse("/work delete " + Id).Kind);
        Assert.AreEqual(WorkCommandKind.Save, WorkCommands.Parse("/work save " + new string('x', 80)).Kind);
        foreach (var raw in new[] { "/Work list", "/work  list", "/work list x", "/work load " + Id,
            "/work show " + Id.ToUpperInvariant(), "/work show ../" + Id, "/work show " + Id + " x",
            "/work save ", "/work save   ", "/work save a\nb", "/work save \ud800", "/work save " + new string('x', 81) })
            Assert.AreEqual(WorkCommandKind.Invalid, WorkCommands.Parse(raw).Kind);
    }

    [TestMethod]
    public async Task SAVEAC002_ResumeOptionsStayInternalAndBootstrapOrderIsPreserved()
    {
        foreach (var args in new[] { new[] { "--resume", Id }, new[] { "--resume", Id, "--workspace", "x" }, new[] { "--workspace", "x", "--resume", Id } })
        {
            var fake = new BootstrapFake();
            using var result = await fake.Create().StartAsync(args, default);
            Assert.AreEqual(BootstrapStatus.Ready, result.Status);
            Assert.AreEqual(Id, result.Session!.ResumeWorkId);
            Assert.AreEqual("settings,terminal,resolve,confirm,revalidate,key", string.Join(',', fake.Calls));
        }
        foreach (var args in new[] { new[] { "--help", "--resume", Id }, new[] { "--resume" }, new[] { "--resume", Id, "--resume", Id },
            new[] { "--resume", Id.ToUpperInvariant() }, new[] { "--resume", "latest" }, new[] { "--workspace", "x", "extra" } })
        {
            var fake = new BootstrapFake(); using var result = await fake.Create().StartAsync(args, default);
            Assert.AreEqual(2, result.ExitCode); Assert.AreEqual(0, fake.Calls.Count);
        }
    }
}