using System.Runtime.InteropServices;

using AIAgent.Cli.Workspace;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AIAgent.Cli.Tests;

[TestClass]
public sealed class WorkStoreTests
{
    [TestMethod]
    public void SAVEAC001_StoreSnapshotsCloneTheirBytesAndMatchEveryObservedIdentity()
    {
        byte[] source = [1, 2, 3];
        var state = new WorkspaceNodeState(WorkspaceNodeKind.File, 3, 5, 6, 7, 8, 1, 9, 384);
        var file = new WorkStoreFile(new string('a', 32), source, state, 10, 11);
        source[0] = 99;
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, file.Bytes.ToArray());
        Assert.IsTrue(MemoryMarshal.TryGetArray(file.Bytes, out var exposed));
        exposed.Array![0] = 98;
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, file.Bytes.ToArray());
        Assert.IsTrue(file.Matches(new(file.WorkId, [1, 2, 3], state, 10, 11)));
        Assert.IsFalse(file.Matches(new(new string('b', 32), [1, 2, 3], state, 10, 11)));
        Assert.IsFalse(file.Matches(new(file.WorkId, [1, 2, 4], state, 10, 11)));
        Assert.IsFalse(file.Matches(new(file.WorkId, [1, 2, 3], state with { Identity = 12 }, 10, 11)));
        Assert.IsFalse(file.Matches(new(file.WorkId, [1, 2, 3], state, 12, 11)));
        Assert.IsFalse(file.Matches(new(file.WorkId, [1, 2, 3], state, 10, 12)));
        Assert.AreEqual("[WorkStoreFile]", file.ToString());
    }

    [TestMethod]
    public void SAVEAC016_StoreFailuresAndMutationDiagnosticsUseFixedMarkers()
    {
        var failure = new WorkStoreException("store_unsafe");
        Assert.AreEqual("store_unsafe", failure.Code);
        Assert.AreEqual("[WorkStoreException]", failure.ToString());
        var mutation = new WorkStoreMutation("work_cleanup_failed", WorkPublicationState.PublishedVerified,
            WorkDeleteState.None, true, true, ".aiagent-work-" + new string('b', 32) + ".tmp");
        Assert.AreEqual(WorkPublicationState.PublishedVerified, mutation.PublicationState);
        Assert.AreEqual(WorkDeleteState.None, mutation.DeleteState);
        Assert.IsTrue(mutation.CleanupFailed);
        Assert.IsTrue(mutation.StoreStopped);
        Assert.AreEqual("[WorkStoreMutation]", mutation.ToString());
    }
}