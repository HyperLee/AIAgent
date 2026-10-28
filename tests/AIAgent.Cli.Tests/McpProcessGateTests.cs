using AIAgent.Cli.Core;
using AIAgent.Cli.Mcp;
using AIAgent.Cli.Processes;
using AIAgent.Cli.Security;
using AIAgent.Cli.Workspace;

namespace AIAgent.Cli.Tests;

[TestClass]
public sealed class McpProcessGateTests
{
    [TestMethod]
    public async Task MCPAC016_ProcessFinalSourceAwaitCannotStartUsingRevokedMcpStamp()
    {
        var root = await NativeBuildFixture.CreateAsync(false); using var session = WorkspaceFixtures.Session(root);
        var execution = new AgentExecutionPolicy(new(session.Policy));
        await using var files = new WorkspaceFiles(session.Workspace, session.Policy, new MacWorkspaceFileSystem(), executionPolicy: execution);
        var reader = new ObservedRules(files.ProjectRulesWorkspace!);
        execution.Rules = new(reader, session.Policy, execution);
        await execution.Rules.TryReloadAsync(default);
        await using var mcp = new McpCoordinator(execution, session.Policy, new McpContentGuard(session.Policy, []),
            (_, _) => new(McpApprovalDecision.Declined), () => files.Snapshots.Clear(), guard => new McpReadClient(guard, new McpHttpFixture()));
        Assert.AreEqual("none", (await mcp.ConnectAsync(default)).ErrorCode);
        reader.AfterFinalRead = mcp.MarkStale;
        await using var tasks = new ControlledDotnetTasksFactory().Create(session.Workspace, session.Policy, execution, files,
            (_, _) => { reader.Armed = true; return new(ProcessApprovalDecision.Approved); }, _ => { });
        var result = await tasks.RunAsync(DotnetTaskKind.Build, "Fixture.csproj", default);
        Assert.AreEqual("mcp_changed", result.ErrorCode); Assert.AreEqual(ProcessStartState.NotStarted, result.Summary!.StartState);
        Assert.AreEqual(2, reader.ReadsAfterApproval);
    }

    private sealed class ObservedRules(IProjectRulesWorkspace inner) : IProjectRulesWorkspace
    {
        internal bool Armed { get; set; }
        internal int ReadsAfterApproval { get; private set; }
        internal Action? AfterFinalRead { get; set; }
        public AgentExecutionPolicy ExecutionPolicy => inner.ExecutionPolicy;
        public async Task<ProjectRulesReadResult> ReadProjectRulesAsync(string scope, WorkspaceOperationLease lease, CancellationToken token)
        {
            var result = await inner.ReadProjectRulesAsync(scope, lease, token);
            if (Armed && ++ReadsAfterApproval == 2) AfterFinalRead?.Invoke();
            return result;
        }
        public Task<WorkspaceErrorCode> CheckProjectRulesPathAsync(string scope, string path, bool directory, ICollection<ProjectRuleObservation> observations, WorkspaceOperationLease lease, CancellationToken token) => inner.CheckProjectRulesPathAsync(scope, path, directory, observations, lease, token);
        public WorkspaceErrorCode RevalidateProjectRulesPaths(IReadOnlyCollection<ProjectRuleObservation> observations, WorkspaceOperationLease lease) => inner.RevalidateProjectRulesPaths(observations, lease);
        public void ClearProjectRuleSnapshots() => inner.ClearProjectRuleSnapshots();
    }
}