using AIAgent.Cli.Core;

namespace AIAgent.Cli.Workspace;

internal interface ISkillsWorkspace
{
    Task<SkillReadResult> ReadSkillCatalogAsync(string scope, WorkspaceOperationLease lease, CancellationToken token);
    Task<SkillReadResult> ReadSkillAsync(string scope, string id, WorkspaceOperationLease lease, CancellationToken token);
    Task<SkillReadResult> ValidateSkillSourceAsync(SkillSource source, WorkspaceOperationLease lease, CancellationToken token);
    void ClearSkillSnapshots();
}