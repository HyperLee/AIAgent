using System.Runtime.Versioning;

using AIAgent.Cli.Workspace;

namespace AIAgent.Cli.Tests;

[SupportedOSPlatform("macos")]
public sealed partial class WorkStoreNativeTests
{
    [DataRow("before_flush", (int)WorkPublicationState.NotPublished, false)]
    [DataRow("before_readback", (int)WorkPublicationState.PublishedUnconfirmed, true)]
    [DataRow("before_close", (int)WorkPublicationState.PublishedVerified, true)]
    [TestMethod]
    public async Task SAVEAC016_InjectedNativeFailurePreservesActualPublicationState(string phase, int state, bool stopped)
    {
        var home = CreateHome(); var armed = false;
        await using var store = new MacWorkSaveStore(home, boundary => { if (armed && boundary == phase) throw new IOException("synthetic fault"); });
        Assert.AreEqual("none", await store.PreflightSaveAsync(default)); armed = true;
        var result = await store.PublishAsync(WorkId, new byte[] { 65, 66 }, default);
        Assert.AreEqual((WorkPublicationState)state, result.PublicationState); Assert.AreEqual(stopped, store.Stopped);
        Assert.AreEqual(state != (int)WorkPublicationState.NotPublished, File.Exists(Path.Combine(StorePath(home), WorkId + ".json")));
        if (phase == "before_close") Assert.IsTrue(result.CleanupFailed);
        if (!stopped) Assert.IsFalse(Directory.EnumerateFiles(StorePath(home)).Any(path => path.EndsWith(".tmp", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task SAVEAC016_CancellationBeforeAndAfterPublicationSettlesDifferentKnownResults()
    {
        foreach (var phase in new[] { "before_publish", "after_publish" })
        {
            using var cancel = new CancellationTokenSource(); var home = CreateHome();
            await using var store = new MacWorkSaveStore(home, boundary => { if (boundary == phase) cancel.Cancel(); });
            Assert.AreEqual("none", await store.PreflightSaveAsync(default));
            var result = await store.PublishAsync(WorkId, new byte[] { 65 }, cancel.Token);
            Assert.AreEqual(phase == "before_publish" ? WorkPublicationState.NotPublished : WorkPublicationState.PublishedVerified, result.PublicationState);
            Assert.IsFalse(store.Stopped);
            if (phase == "after_publish") Assert.AreEqual("none", result.ErrorCode);
        }
    }

    [TestMethod]
    public async Task SAVEAC016_FailedOwnTemporaryCleanupStopsWithoutTouchingOtherFiles()
    {
        var home = CreateHome();
        await using var store = new MacWorkSaveStore(home, boundary =>
        { if (boundary is "before_flush" or "before_temp_unlink") throw new IOException("synthetic fault"); });
        Assert.AreEqual("none", await store.PreflightSaveAsync(default));
        var unrelated = Path.Combine(StorePath(home), "unrelated"); File.WriteAllText(unrelated, "keep");
        var result = await store.PublishAsync(WorkId, new byte[] { 65 }, default);
        Assert.AreEqual(WorkPublicationState.NotPublished, result.PublicationState); Assert.IsTrue(result.CleanupFailed); Assert.IsTrue(store.Stopped);
        Assert.IsNotNull(result.TemporaryName); Assert.IsTrue(File.Exists(Path.Combine(StorePath(home), result.TemporaryName)));
        Assert.AreEqual("keep", File.ReadAllText(unrelated));
    }

    [TestMethod]
    public async Task SAVEAC006_DeleteReplacementAfterUnlinkIsNeverDeletedAgain()
    {
        var home = CreateHome(); var target = Path.Combine(StorePath(home), WorkId + ".json");
        await using var store = new MacWorkSaveStore(home, boundary =>
        { if (boundary == "after_delete_unlink") File.WriteAllText(target, "replacement"); });
        Assert.AreEqual("none", await store.PreflightSaveAsync(default));
        Assert.AreEqual(WorkPublicationState.PublishedVerified, (await store.PublishAsync(WorkId, new byte[] { 65 }, default)).PublicationState);
        var preview = await store.ReadAsync(WorkId, default); var result = await store.DeleteAsync(preview, default);
        Assert.AreEqual(WorkDeleteState.DeleteUnconfirmed, result.DeleteState); Assert.IsTrue(store.Stopped);
        Assert.AreEqual("replacement", File.ReadAllText(target));
    }
}