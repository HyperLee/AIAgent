using System.Text.Json;

using AIAgent.Cli.Core;
using AIAgent.Cli.Mcp;
using AIAgent.Cli.Security;
using AIAgent.Cli.Tools;
using AIAgent.Cli.Workspace;

namespace AIAgent.Cli.Tests;

[TestClass]
public sealed class McpCoordinatorTests
{
    [TestMethod]
    public async Task MCPAC010_DisconnectedDisconnectDoesNotRevokeApprovedPlanOrVersion()
    {
        await using var f = new McpFixture(); await f.Initialize(); f.Plans.SetMode(AgentMode.Plan);
        Assert.IsTrue(f.Plans.Submit(PlanFixture.Submission()).Success); Assert.IsTrue(f.Plans.Approve(f.Plans.ApprovalPreview()!));
        var stamp = f.Mcp.Capture(); var generation = f.Plans.Generation;
        Assert.AreEqual("no_change", (await f.Mcp.DisconnectAsync(default)).ErrorCode);
        Assert.IsTrue(f.Mcp.IsCurrent(stamp)); Assert.AreEqual(generation, f.Plans.Generation);
        Assert.AreEqual(PlanApprovalState.Approved, f.Plans.Snapshot!.ApprovalState);
    }
    [TestMethod]
    public async Task MCPAC016_VersionOverflowStopsButStillClosesOwnedClient()
    {
        await using var f = new McpFixture(); await f.Initialize(); await f.Mcp.ConnectAsync(default);
        typeof(McpCoordinator).GetField("_generation", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.SetValue(f.Mcp, long.MaxValue);
        await f.Mcp.ResetAsync();
        Assert.IsTrue(f.Mcp.FatalFailure); Assert.AreEqual(McpStatus.Disconnected, f.Mcp.Capture().Status);
        Assert.IsTrue(f.Handlers.All(h => h.Closed));
    }

    [TestMethod]
    [DataRow("_revision")]
    [DataRow("_generation")]
    public async Task MCPAC016_ScopeInvalidationOverflowStillDisconnectsAndCloses(string counter)
    {
        await using var f = new McpFixture(); await f.Initialize(); await f.Mcp.ConnectAsync(default);
        typeof(McpCoordinator).GetField(counter, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.SetValue(f.Mcp, long.MaxValue);
        try { f.Mcp.RulesPublished("old", "new"); }
        finally { await f.Mcp.DrainDisconnectedAsync(); }
        Assert.IsTrue(f.Mcp.FatalFailure); Assert.AreEqual(McpStatus.Disconnected, f.Mcp.Capture().Status);
        Assert.IsTrue(f.Handlers.All(h => h.Closed));
    }

    [TestMethod]
    public async Task MCPAC012_NewReviewCanObserveStaleStatusWithoutExternalIo()
    {
        await using var f = new McpFixture(); await f.Initialize(); await f.Mcp.ConnectAsync(default); f.Mcp.MarkStale();
        var count = f.Handlers.Sum(h => h.Methods.Count);
        StringAssert.Contains(f.Mcp.BuildAppendix(true), "Stale");
        Assert.AreEqual(count, f.Handlers.Sum(h => h.Methods.Count));
    }
    [TestMethod]
    public async Task MCPAC010_ConnectAndUnchangedReloadAreExplicitAtomicAndNeverCallTools()
    {
        await using var f = new McpFixture(); await f.Initialize();
        var before = f.Contexts.Capture();
        Assert.AreEqual("none", (await f.Mcp.ConnectAsync(default)).ErrorCode);
        Assert.AreEqual(McpStatus.Ready, f.Mcp.Capture().Status); Assert.AreEqual(1L, f.Mcp.Capture().CatalogRevision);
        Assert.IsFalse(f.Contexts.IsCurrent(before));
        var stamp = f.Mcp.Capture(); var planGeneration = f.Plans.Generation;
        Assert.AreEqual("mcp_already_connected", (await f.Mcp.ConnectAsync(default)).ErrorCode);
        Assert.AreEqual("no_change", (await f.Mcp.ReloadAsync(default)).ErrorCode);
        Assert.IsTrue(f.Mcp.IsCurrent(stamp)); Assert.AreEqual(planGeneration, f.Plans.Generation);
        Assert.IsFalse(f.Handlers.SelectMany(h => h.Methods).Contains("tools/call"));
    }

    [TestMethod]
    public async Task MCPAC011_IdenticalCallsRequireFreshApprovalAndThirdCallDoesNotSend()
    {
        await using var f = new McpFixture(); await f.Initialize(); await f.Mcp.ConnectAsync(default);
        Assert.AreEqual(ToolStatus.Success, (await f.Invoke()).Status);
        Assert.AreEqual(ToolStatus.Success, (await f.Invoke()).Status);
        Assert.AreEqual("mcp_limit", (await f.Invoke()).ErrorCode);
        Assert.AreEqual(2, f.Previews.Count); Assert.AreEqual(2, f.Handlers.SelectMany(h => h.Methods).Count(m => m == "tools/call"));
        StringAssert.Contains(f.Previews[0], "https://learn.microsoft.com/api/mcp");
        StringAssert.Contains(f.Previews[0], "public C#"); Assert.AreEqual(0, f.Plans.Facts.Count);
    }

    [TestMethod]
    public async Task MCPAC011_DeclineDoesNotSendOrSpendCallOpportunity()
    {
        await using var f = new McpFixture(); await f.Initialize(); await f.Mcp.ConnectAsync(default);
        f.OnConfirm = () => McpApprovalDecision.Declined;
        Assert.AreEqual("approval_declined", (await f.Invoke()).ErrorCode);
        Assert.IsFalse(f.Handlers.SelectMany(h => h.Methods).Contains("tools/call"));
        f.OnConfirm = () => McpApprovalDecision.Approved;
        Assert.AreEqual(ToolStatus.Success, (await f.Invoke()).Status);
    }

    [TestMethod]
    public async Task MCPAC016_CatalogChangesAfterYesRevokePermitAndRequireExplicitReload()
    {
        await using var f = new McpFixture(); await f.Initialize(); await f.Mcp.ConnectAsync(default);
        f.OnConfirm = () => { f.Catalog = McpHttpFixture.Catalog.Replace("Search docs", "Changed description", StringComparison.Ordinal); return McpApprovalDecision.Approved; };
        var result = await f.Invoke();
        Assert.AreNotEqual(ToolStatus.Success, result.Status); Assert.AreEqual(McpStatus.Stale, f.Mcp.Capture().Status);
        Assert.IsFalse(f.Handlers.SelectMany(h => h.Methods).Contains("tools/call"));
        Assert.AreEqual("mcp_catalog_stale", (await f.Mcp.ConnectAsync(default)).ErrorCode);
        var revision = f.Mcp.Capture().CatalogRevision; f.Catalog = McpHttpFixture.Catalog;
        Assert.AreEqual("none", (await f.Mcp.ReloadAsync(default)).ErrorCode); Assert.AreEqual(revision + 1, f.Mcp.Capture().CatalogRevision);
    }

    [TestMethod]
    public async Task MCPAC018_ConnectionRevokesPlanApprovalButPreservesModeIdentityAndEvidenceEpoch()
    {
        await using var f = new McpFixture(); await f.Initialize(); f.Plans.SetMode(AgentMode.Plan);
        Assert.IsTrue(f.Plans.Submit(PlanFixture.Submission()).Success); var id = f.Plans.Snapshot!.PlanId; var revision = f.Plans.Snapshot.Revision;
        Assert.IsTrue(f.Plans.Approve(f.Plans.ApprovalPreview()!));
        Assert.AreEqual("none", (await f.Mcp.ConnectAsync(default)).ErrorCode);
        Assert.AreEqual(AgentMode.Execute, f.Plans.Mode); Assert.AreEqual(id, f.Plans.Snapshot.PlanId); Assert.AreEqual(revision, f.Plans.Snapshot.Revision);
        Assert.AreEqual(PlanApprovalState.Draft, f.Plans.Snapshot.ApprovalState); Assert.AreEqual(0L, f.Plans.EvidenceEpoch);
    }

    [TestMethod]
    public async Task MCPAC019_ScopeChangeClosesClientAndDoesNotImplicitlyConnectAgain()
    {
        await using var f = new McpFixture(); await f.Initialize(); await f.Mcp.ConnectAsync(default);
        Directory.CreateDirectory(f.Root + "/child");
        Assert.AreEqual("none", await f.Execution.Rules!.TryChangeScopeAsync("child", default));
        Assert.AreEqual(McpStatus.Disconnected, f.Mcp.Capture().Status);
        Assert.IsTrue(f.Handlers.All(h => h.Closed));
    }

    [TestMethod]
    public async Task MCPAC012_ReviewRejectsExternalCallsBeforeAnyMetadataRequest()
    {
        await using var f = new McpFixture(); await f.Initialize(); await f.Mcp.ConnectAsync(default);
        var count = f.Handlers.Sum(h => h.Methods.Count);
        using (f.Execution.EnterReview()) Assert.AreEqual("mode_restricted", (await f.Invoke()).ErrorCode);
        Assert.AreEqual(count, f.Handlers.Sum(h => h.Methods.Count));
    }
}

internal sealed class McpFixture : IAsyncDisposable
{
    internal string Root { get; } = WorkspaceFixtures.NewDirectory();
    internal BootstrapSession Session { get; }
    internal PlanStore Plans { get; }
    internal AgentExecutionPolicy Execution { get; }
    internal WorkspaceFiles Files { get; }
    internal ContextCoordinator Contexts { get; }
    internal McpCoordinator Mcp { get; }
    internal Action<string>? Observe { get; set; }
    internal string Catalog { get; set; } = McpHttpFixture.Catalog;
    internal List<McpHttpFixture> Handlers { get; } = [];
    internal List<McpReadClient> Clients { get; } = [];
    internal List<string> Previews { get; } = [];
    internal Func<McpApprovalDecision>? OnConfirm { get; set; }
    internal Func<McpApprovalPreview, CancellationToken, ValueTask<McpApprovalDecision>>? AsyncConfirm { get; set; }
    internal McpFixture(TimeProvider? clock = null, IWorkspaceReadFileSystem? fileSystem = null)
    {
        Session = WorkspaceFixtures.Session(Root); Plans = new(Session.Policy); Execution = new(Plans);
        Files = new(Session.Workspace, Session.Policy, fileSystem ?? new MacWorkspaceFileSystem(), new EditApproval(), Execution);
        Contexts = new(new(), Plans, Session.Policy); Contexts.BindRules(Execution.Rules); Contexts.BindSkills(Execution.Skills);
        Mcp = new(Execution, Session.Policy, new McpContentGuard(Session.Policy, ["SYNTHETIC_PROTECTED_INSTRUCTION"]),
            (preview, token) => { Previews.Add(preview.DisplayBlock); return AsyncConfirm?.Invoke(preview, token) ?? new(OnConfirm?.Invoke() ?? McpApprovalDecision.Approved); },
            () => Files.Snapshots.Clear(), guard =>
            {
                var handler = new McpHttpFixture { Override = (method, _) => { Observe?.Invoke(method); return method == "tools/list" ? Catalog : null; } };
                Handlers.Add(handler); var client = new McpReadClient(guard, handler, clock); Clients.Add(client); return client;
            }, clock);
        Plans.Mcp = Mcp; Contexts.BindMcp(Mcp);
    }
    internal async Task Initialize() { Assert.AreEqual("none", await Execution.Rules!.TryReloadAsync(default)); }
    internal Task<ToolExecutionResult> Invoke()
    {
        using var args = JsonDocument.Parse("{\"query\":\"public C#\"}");
        return Mcp.InvokeAsync(McpProfile.DocsName, args.RootElement, Mcp.Capture(), default);
    }
    public async ValueTask DisposeAsync() { await Mcp.DisposeAsync(); await Files.DisposeAsync(); Session.Dispose(); }
}