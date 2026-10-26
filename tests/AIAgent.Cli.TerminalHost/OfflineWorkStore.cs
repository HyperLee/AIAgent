using AIAgent.Cli.Core;
using AIAgent.Cli.Security;
using AIAgent.Cli.Workspace;

// Only the offline PTY host has this in-memory storage adapter.
internal sealed class OfflineWorkStore : IWorkSaveStore
{
    internal const string ResumeId = "0123456789abcdef0123456789abcdef";
    private readonly Dictionary<string, WorkStoreFile> _files = new(StringComparer.Ordinal);
    private ulong _identity;
    public bool Stopped => false;
    internal OfflineWorkStore(string scenario, BootstrapSession session)
    {
        Console.WriteLine("OFFLINE_WORK_STORE_OPEN");
        if (!scenario.StartsWith("work-resume", StringComparison.Ordinal)) return;
        var state = new MacWorkspaceFileSystem().Inspect(session.Workspace.RootPath);
        var completed = scenario.Contains("completed", StringComparison.Ordinal);
        var task = new WorkSavedTask(PlanTaskKind.Analysis, "歷史任務", "重新評估", 1, [], completed ? PlanTaskState.Completed : PlanTaskState.Blocked,
            completed ? TaskCompletionBasis.ModelAssessment : TaskCompletionBasis.None, "歷史轉述", completed ? null : "model_reported");
        var document = new WorkSaveDocument(ResumeId, "離線續接", "2026-10-26T08:00:00Z", new(session.Workspace.RootPath, state.Device, state.Identity),
            scenario.Contains("scope", StringComparison.Ordinal) ? "src" : ".", [], new(0, 0, ["保留歷史目標"], [], [], []), ["原文備註"], new("離線目標", [task]), []);
        var bytes = WorkSaveCodec.Write(document, new WorkPersistencePolicy(session.Policy));
        _files[ResumeId] = Entry(ResumeId, bytes);
    }
    private WorkStoreFile Entry(string id, byte[] bytes) => new(id, bytes, new(WorkspaceNodeKind.File, bytes.Length, Identity: ++_identity), 1, 2);
    public Task<string> PreflightSaveAsync(CancellationToken token) { token.ThrowIfCancellationRequested(); return Task.FromResult("none"); }
    public Task<IReadOnlyList<WorkStoreFile>> ListAsync(CancellationToken token) => Task.FromResult<IReadOnlyList<WorkStoreFile>>(_files.Values.ToArray());
    public Task<WorkStoreFile> ReadAsync(string workId, CancellationToken token) => Task.FromResult(_files.TryGetValue(workId, out var file) ? file : throw new WorkStoreException("work_not_found"));
    public Task<WorkStoreMutation> PublishAsync(string workId, ReadOnlyMemory<byte> bytes, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (_files.ContainsKey(workId)) return Task.FromResult(new WorkStoreMutation("work_conflict"));
        _files[workId] = Entry(workId, bytes.ToArray());
        return Task.FromResult(new WorkStoreMutation("none", WorkPublicationState.PublishedVerified));
    }
    public Task<WorkStoreMutation> DeleteAsync(WorkStoreFile expected, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (!_files.TryGetValue(expected.WorkId, out var actual) || !expected.Matches(actual)) return Task.FromResult(new WorkStoreMutation("work_conflict"));
        _files.Remove(expected.WorkId); return Task.FromResult(new WorkStoreMutation("none", deleteState: WorkDeleteState.DeletedVerified));
    }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}