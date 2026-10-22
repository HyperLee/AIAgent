using System.Reflection;
using System.Text.Json;

using AIAgent.Cli.Tools;
using AIAgent.Cli.Workspace;

namespace AIAgent.Cli.Tests;

[TestClass]
public sealed class EditContractTests
{
    [TestMethod]
    public void EDITAC002_WriteContractsDoNotExposeRawData()
    {
        var assembly = typeof(WorkspaceReadSnapshot).Assembly;
        foreach (var name in new[] { "FileChangePreview", "WorkspaceWriteData", "WorkspaceWriteResult" })
        {
            var type = assembly.GetType("AIAgent.Cli.Workspace." + name);
            Assert.IsNotNull(type, "EDIT-AC-002-missing-contract");
            Assert.IsTrue(type.IsSealed && !type.IsPublic, "EDIT-AC-002-internal-sealed");
            Assert.AreEqual(0, type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Length, "EDIT-AC-002-no-serialization");
        }
        foreach (var name in new[] { "OriginalDevice", "OriginalIdentity", "OriginalMode" })
            Assert.IsNotNull(typeof(WorkspaceReadSnapshot).GetProperty(name, BindingFlags.NonPublic | BindingFlags.Instance), "EDIT-AC-018-read-metadata");
        Assert.IsNotNull(typeof(IWorkspaceFiles).GetMethod("CreateFileAsync"), "EDIT-AC-001-create-entry");
        Assert.IsNotNull(typeof(IWorkspaceFiles).GetMethod("ReplaceTextAsync"), "EDIT-AC-001-replace-entry");
    }
    [TestMethod]
    public void EDITAC002_ImpossibleWriteStatesAreRejected()
    {
        Assert.Throws<ArgumentException>(() => new ToolExecutionResult(ToolStatus.Success, "none", null, writeState: FileWriteState.Unconfirmed), "EDIT-AC-002-unconfirmed-success");
        Assert.Throws<ArgumentException>(() => WorkspaceWriteResult.End(WorkspaceStatus.Failed, WorkspaceErrorCode.WriteUnconfirmed), "EDIT-AC-002-unconfirmed-code");
        Assert.Throws<ArgumentException>(() => new WorkspaceWriteData("../outside", 1, false), "EDIT-AC-002-verified-path");
        Assert.Throws<ArgumentException>(() => new FileChangePreview((FileChangeKind)9, "a", 0, 1, false, false, "x", 1), "EDIT-AC-002-preview-kind");
        var verified = WorkspaceWriteResult.Success(new("a", 1, false)).WithCleanupFailure();
        Assert.IsTrue(verified.Status == WorkspaceStatus.Failed && verified.WriteState == FileWriteState.Verified && verified.CleanupFailed && verified.Data is null &&
            verified.VerifiedPath == "a" && verified.VerifiedByteCount == 1, "EDIT-AC-026-verified-cleanup");
    }

}