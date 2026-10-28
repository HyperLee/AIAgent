using System.Text;

using AIAgent.Cli.Core;
using AIAgent.Cli.Workspace;

namespace AIAgent.Cli.Tests;

[TestClass]
public sealed class McpSettlementTests
{
    [TestMethod]
    public async Task MCPAC018_LastFactAndVerifiedWriteSurviveMcpChangeBeforeSettlement()
    {
        McpCoordinator? mcp = null;
        await using var f = new McpFixture(fileSystem: new AfterPublishFileSystem(() => mcp!.MarkStale())); mcp = f.Mcp;
        File.WriteAllText(f.Root + "/a", "A"); await f.Initialize(); await mcp.ConnectAsync(default);
        f.Plans.SetMode(AgentMode.Plan); Assert.IsTrue(f.Plans.Submit(PlanFixture.Submission()).Success);
        Assert.IsTrue(f.Plans.Approve(f.Plans.ApprovalPreview()!)); Assert.IsTrue(ProjectRulesLifecycleTests.Update(f.Plans, "in_progress").Success);
        for (var i = 0; i < 254; i++) Assert.AreEqual(WorkspaceStatus.Success, (await f.Files.ReadFileAsync("a", default)).Status);
        f.Plans.SetMode(AgentMode.Plan); Assert.IsTrue(f.Plans.Submit(PlanFixture.Submission(expected: 1, kind: "write", tool: "replace_text", path: "a")).Success);
        Assert.IsTrue(f.Plans.Approve(f.Plans.ApprovalPreview()!)); Assert.IsTrue(ProjectRulesLifecycleTests.Update(f.Plans, "in_progress").Success);
        await f.Files.ReadFileAsync("a", default);
        var write = await f.Files.ReplaceTextAsync("a", "A", "B", default);
        Assert.AreEqual(FileWriteState.Verified, write.WriteState); Assert.AreEqual("B", File.ReadAllText(f.Root + "/a"));
        Assert.AreEqual(256, f.Plans.Facts.Count); Assert.AreEqual(FileWriteState.Verified, f.Plans.Facts[^1].WriteState);
        Assert.AreEqual(PlanApprovalState.Draft, f.Plans.Snapshot!.ApprovalState); Assert.AreEqual("mcp_changed", f.Plans.Snapshot.Tasks[0].BlockReason);
        Assert.AreEqual(0L, f.Plans.EvidenceEpoch); Assert.IsTrue(Encoding.UTF8.GetByteCount(f.Plans.Display()!) <= 65536);
    }

    private sealed class AfterPublishFileSystem(Action published) : IWorkspaceWriteFileSystem
    {
        private readonly MacWorkspaceFileSystem _inner = new();
        public bool HasExactName(string path) => _inner.HasExactName(path);
        public WorkspaceNodeState Inspect(string path) => _inner.Inspect(path);
        public IEnumerable<string> EnumerateNames(string path) => _inner.EnumerateNames(path);
        public IWorkspaceReadFile OpenRead(string path) => _inner.OpenRead(path);
        public UnixFileMode GetMode(string path) => _inner.GetMode(path);
        public IWorkspaceStagedFile CreateStagedFile(string path) => _inner.CreateStagedFile(path);
        public void PublishNew(string staged, string target) { _inner.PublishNew(staged, target); published(); }
        public void PublishReplacement(string staged, string target) { _inner.PublishReplacement(staged, target); published(); }
        public void DeleteOwnedStagedFile(string path) => _inner.DeleteOwnedStagedFile(path);
    }
}