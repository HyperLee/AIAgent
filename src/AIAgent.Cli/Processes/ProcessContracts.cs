using System.Text.Json;

using AIAgent.Cli.Core;
using AIAgent.Cli.Security;
using AIAgent.Cli.Tools;
using AIAgent.Cli.Workspace;

namespace AIAgent.Cli.Processes;

internal enum ProcessApprovalDecision { Approved, Declined, Cancelled, EndOfInput, Unavailable }

internal interface IControlledDotnetTasks : IAsyncDisposable
{
    Task<DotnetTaskResult> RunAsync(DotnetTaskKind task, string projectPath, CancellationToken cancellationToken);
}

internal sealed class DotnetTaskResult(ToolExecutionResult result)
{
    internal ToolExecutionResult ToolResult { get; } = result;
    internal ToolStatus Status => ToolResult.Status;
    internal string ErrorCode => ToolResult.ErrorCode;
    internal JsonElement? Response => ToolResult.Response;
    internal ProcessExecutionSummary? Summary => ToolResult.ProcessSummary;
    internal bool MustStop => ToolResult.StopAfterOperation;
    public override string ToString() => "[DotnetTaskResult]";
}

internal sealed class ProcessApprovalPreview(string displayBlock, object sessionIdentity, long generation,
    TaskExecutionContext? context, string approvalId)
{
    internal string DisplayBlock { get; } = displayBlock;
    internal object SessionIdentity { get; } = sessionIdentity;
    internal long Generation { get; } = generation;
    internal TaskExecutionContext? Context { get; } = context;
    internal string ApprovalId { get; } = approvalId;
    public override string ToString() => "[ProcessApprovalPreview]";
}

internal interface IProcessTurnLifecycle
{
    void BeginTurn();
}

internal interface IControlledDotnetTasksFactory
{
    bool IsAvailable { get; }
    bool Supports(DotnetTaskKind task) => Enum.IsDefined(task) && IsAvailable;
    IControlledDotnetTasks Create(ConfirmedWorkspace workspace, ISensitiveDataPolicy policy,
        AgentExecutionPolicy execution, IWorkspaceFiles files,
        Func<ProcessApprovalPreview, CancellationToken, ValueTask<ProcessApprovalDecision>> approve,
        Action<string> progress);
}

internal static class ProcessApprovalText
{
    internal const string Prompt = "確認此單次受控 .NET 任務？[y/N] ";
}