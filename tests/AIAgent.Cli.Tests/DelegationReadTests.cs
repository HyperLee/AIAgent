using System.Text;
using System.Text.Json;

using AIAgent.Cli.Core;
using AIAgent.Cli.Tools;
using AIAgent.Cli.Workspace;

namespace AIAgent.Cli.Tests;

[TestClass]
public sealed class DelegationReadTests
{
    [TestMethod]
    public async Task MULTIAC023_ListLedgerKeepsObservedIdentityInsteadOfRecapturingReplacement()
    {
        using var session = WorkspaceFixtures.Session(); var fs = new ControlledWorkspaceFileSystem(); fs.File("a.cs", [65]);
        var execution = new AgentExecutionPolicy(new PlanStore(session.Policy));
        await using var parent = new WorkspaceFiles(session.Workspace, session.Policy, fs, executionPolicy: execution);
        await execution.Rules!.EnsureInitializedAsync(default); await using var child = parent.CreateDelegationReadSession(".");
        var inspections = 0; fs.OnInspect = path => { if (path == "/ws/a.cs" && ++inspections == 2) fs.Nodes[path].State = fs.Nodes[path].State with { Identity = 987 }; };
        var result = await child.Files.ListFilesAsync(".", default); Assert.IsTrue(WorkspaceResponseWriter.TryCreateResponse(result, session.Policy, out var response, out _));
        child.Ledger.AddPending("list_files", response); child.Ledger.MarkDispatched();
        Assert.AreEqual("delegation_source_changed", await child.ValidateAsync(default)); Assert.AreEqual(0, fs.Opened);
    }

    [TestMethod]
    public async Task MULTIAC023_ReplacedChildDirectoryInvalidatesPreviouslyDeliveredFile()
    {
        using var session = WorkspaceFixtures.Session(); var fs = new ControlledWorkspaceFileSystem(); fs.Directory("src"); fs.File("src/a.cs", [65]);
        var policy = new AgentExecutionPolicy(new PlanStore(session.Policy));
        await using var parent = new WorkspaceFiles(session.Workspace, session.Policy, fs, executionPolicy: policy);
        await policy.Rules!.EnsureInitializedAsync(default); await parent.ValidateDelegationScopeAsync("src", default);
        await using var child = parent.CreateDelegationReadSession("src"); var result = await child.Files.ReadFileAsync("src/a.cs", default);
        Assert.IsTrue(WorkspaceResponseWriter.TryCreateResponse(result, session.Policy, out var response, out _));
        child.Ledger.AddPending("read_file", response); child.Ledger.MarkDispatched();
        fs.Nodes["/ws/src"].State = fs.Nodes["/ws/src"].State with { Identity = 987 };
        Assert.AreEqual("delegation_source_changed", await child.ValidateAsync(default));
    }

    [TestMethod]
    public async Task MULTIAC010_ChildFullReadCannotCreateOrInvalidateParentWriteBaseline()
    {
        using var session = WorkspaceFixtures.Session(); var fs = new ControlledWorkspaceFileSystem(); fs.File("a.cs", Encoding.UTF8.GetBytes("first\nsecond"));
        var plans = new PlanStore(session.Policy); var policy = new AgentExecutionPolicy(plans);
        await using var parent = new WorkspaceFiles(session.Workspace, session.Policy, fs, executionPolicy: policy);
        Assert.AreEqual("none", await policy.Rules!.EnsureInitializedAsync(default));
        await using var child = parent.CreateDelegationReadSession(".");
        var read = await child.Files.ReadFileAsync("a.cs", default);
        Assert.AreEqual(WorkspaceStatus.Success, read.Status); Assert.AreEqual(0, parent.Snapshots.Count);
        var ledger = child.Ledger;
        using var response = JsonDocument.Parse("""{"status":"success","data":{"path":"a.cs","content":"first\nsecond"}}""");
        ledger.AddPending("read_file", response.RootElement); ledger.MarkDispatched();
        Assert.IsTrue(ledger.Delivered("a.cs", 1, 2));
        Assert.AreEqual("none", await child.ValidateAsync(default));
        fs.Nodes["/ws/a.cs"].Bytes = Encoding.UTF8.GetBytes("other\nsecond");
        Assert.AreEqual("delegation_source_changed", await child.ValidateAsync(default));
        Assert.AreEqual(0, parent.Snapshots.Count);
    }

    [TestMethod]
    public async Task MULTIAC009_ChildScopeBlocksSiblingAndUnloadedRulesAndDisposalPreservesParent()
    {
        using var session = WorkspaceFixtures.Session(); var fs = new ControlledWorkspaceFileSystem(); fs.Directory("src"); fs.Directory("tests"); fs.File("tests/a.cs", [65]);
        var policy = new AgentExecutionPolicy(new PlanStore(session.Policy));
        await using var parent = new WorkspaceFiles(session.Workspace, session.Policy, fs, executionPolicy: policy);
        await policy.Rules!.EnsureInitializedAsync(default);
        await using (var child = parent.CreateDelegationReadSession("src"))
            Assert.AreEqual(WorkspaceErrorCode.RulesScopeMismatch, (await child.Files.ReadFileAsync("tests/a.cs", default)).ErrorCode);
        Assert.IsFalse(policy.WorkspaceOperations.IsDisposed);
        Assert.AreEqual(WorkspaceStatus.Success, (await parent.ReadFileAsync("tests/a.cs", default)).Status);
    }
}