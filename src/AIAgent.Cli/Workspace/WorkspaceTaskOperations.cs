using AIAgent.Cli.Core;
using AIAgent.Cli.Tools;

namespace AIAgent.Cli.Workspace;

internal sealed partial class WorkspaceFiles
{
    private void BeginTaskOperation(WorkspaceOperation operation, string tool, string path, int? start = null, int? end = null, bool write = false)
    {
        if (_delegationScope is not null) return;
        var error = _executionPolicy.Plans.Reserve(new(tool, path, start, end), write, out var reservation);
        if (error != "none") throw Rejected(error switch
        {
            "mode_restricted" => WorkspaceErrorCode.ModeRestricted,
            "plan_not_approved" => WorkspaceErrorCode.PlanNotApproved,
            "task_required" => WorkspaceErrorCode.TaskRequired,
            "task_scope_mismatch" => WorkspaceErrorCode.TaskScopeMismatch,
            "evidence_limit" => WorkspaceErrorCode.EvidenceLimit,
            "plan_too_large" => WorkspaceErrorCode.PlanTooLarge,
            "sensitive_content" => WorkspaceErrorCode.SensitiveContent,
            _ => WorkspaceErrorCode.Unavailable
        });
        operation.TaskReservation = reservation;
    }

    private void RecordTaskOperation<T>(WorkspaceOperation operation, T result)
    {
        var plans = _executionPolicy.Plans; var reservation = operation.TaskReservation;
        if (reservation is null) return;
        switch (result)
        {
            case WorkspaceWriteResult value:
                plans.Record(reservation, WorkspaceToolProjection.Status(value.Status), WorkspaceToolProjection.Code(value.ErrorCode),
                    writeState: value.WriteState, cleanupFailed: value.CleanupFailed, bytes: value.VerifiedByteCount);
                break;
            case WorkspaceReadResult value:
                plans.Record(reservation, WorkspaceToolProjection.Status(value.Status), WorkspaceToolProjection.Code(value.ErrorCode), bytes: value.Data?.ByteCount);
                break;
            case WorkspaceListResult value:
                plans.Record(reservation, WorkspaceToolProjection.Status(value.Status), WorkspaceToolProjection.Code(value.ErrorCode),
                    value.Data?.Truncated ?? false, count: value.Data?.Entries.Count);
                break;
            case WorkspaceFileSearchResult value:
                plans.Record(reservation, WorkspaceToolProjection.Status(value.Status), WorkspaceToolProjection.Code(value.ErrorCode),
                    value.Data?.Truncated ?? false, count: value.Data?.Matches.Count);
                break;
            case WorkspaceTextSearchResult value:
                plans.Record(reservation, WorkspaceToolProjection.Status(value.Status), WorkspaceToolProjection.Code(value.ErrorCode),
                    value.Data?.Truncated ?? false, count: value.Data?.Matches.Count);
                break;
            case WorkspaceRangeReadResult value:
                var data = value.Data;
                plans.Record(reservation, WorkspaceToolProjection.Status(value.Status), WorkspaceToolProjection.Code(value.ErrorCode),
                    bytes: data?.ContentByteCount, discovery: data is null ? null : new(DiscoveryKind.LineRange,
                        data.StartLine is { } first ? data.EndLine!.Value - first + 1 : 0,
                        data.StartLine, data.EndLine, data.TotalLines, data.ContentByteCount));
                break;
        }
    }
}