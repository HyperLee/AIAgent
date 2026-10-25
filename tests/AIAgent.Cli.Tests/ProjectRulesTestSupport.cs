using AIAgent.Cli.Core;
using AIAgent.Cli.Security;
using AIAgent.Cli.Tools;
using AIAgent.Cli.Workspace;

namespace AIAgent.Cli.Tests;

// Existing isolated core tests bind an explicit empty source fixture. Product rule tests use the real reader.
internal static class TestRulesRegistry
{
    internal static ToolRegistry Create(IReadOnlyList<ITool> tools, ISensitiveDataPolicy policy, AgentExecutionPolicy? executionPolicy = null)
    {
        var execution = executionPolicy ?? tools.Select(t => t.ProjectRulesWorkspace?.ExecutionPolicy).FirstOrDefault(e => e is not null) ?? new(new PlanStore(policy));
        execution.Rules ??= new(new EmptyProjectRulesReader(execution), policy, execution);
        return new(tools, policy, execution);
    }
    internal sealed class EmptyProjectRulesReader(AgentExecutionPolicy execution) : IProjectRulesWorkspace
    {
        public AgentExecutionPolicy ExecutionPolicy => execution;
        public Task<ProjectRulesReadResult> ReadProjectRulesAsync(string scope, WorkspaceOperationLease lease, CancellationToken token)
        { token.ThrowIfCancellationRequested(); return Task.FromResult(new ProjectRulesReadResult(WorkspaceErrorCode.None, new(scope, [], []))); }
        public Task<WorkspaceErrorCode> CheckProjectRulesPathAsync(string scope, string path, bool directory,
            ICollection<ProjectRuleObservation> observations, WorkspaceOperationLease lease, CancellationToken token) => Task.FromResult(WorkspaceErrorCode.None);
        public WorkspaceErrorCode RevalidateProjectRulesPaths(IReadOnlyCollection<ProjectRuleObservation> observations, WorkspaceOperationLease lease) => WorkspaceErrorCode.None;
        public void ClearProjectRuleSnapshots() { }
    }
}

internal sealed class TestWorkspaceFilesFactory : IWorkspaceFilesFactory
{
    private readonly AIAgent.Cli.Workspace.WorkspaceFilesFactory _inner = new();
    public FileChangeApprovalBridge ApprovalBridge => _inner.ApprovalBridge;
    public IWorkspaceFiles Create(BootstrapSession session, AgentExecutionPolicy? executionPolicy = null) =>
        session.Workspace.RootPath is "/ws" or "/unused-workspace"
            ? new WorkspaceFiles(new("/ws"), session.Policy, new ControlledWorkspaceFileSystem(), ApprovalBridge.ForSession(session), executionPolicy)
            : _inner.Create(session, executionPolicy);
}