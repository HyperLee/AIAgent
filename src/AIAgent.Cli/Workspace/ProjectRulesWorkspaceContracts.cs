using AIAgent.Cli.Core;

namespace AIAgent.Cli.Workspace;

internal interface IProjectRulesWorkspace
{
    AgentExecutionPolicy ExecutionPolicy { get; }
    Task<ProjectRulesReadResult> ReadProjectRulesAsync(string scope, WorkspaceOperationLease lease, CancellationToken token);
    Task<WorkspaceErrorCode> CheckProjectRulesPathAsync(string scope, string path, bool directory,
        ICollection<ProjectRuleObservation> observations, WorkspaceOperationLease lease, CancellationToken token);
    WorkspaceErrorCode RevalidateProjectRulesPaths(IReadOnlyCollection<ProjectRuleObservation> observations, WorkspaceOperationLease lease);
    void ClearProjectRuleSnapshots();
}