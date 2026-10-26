namespace AIAgent.Cli.Workspace;

internal sealed class MacWorkSaveStore : IWorkSaveStore
{
    internal MacWorkSaveStore() { }
    internal MacWorkSaveStore(string testHome) { }
    public bool Stopped => false;
    public Task<string> PreflightSaveAsync(CancellationToken token) => Task.FromResult("store_unavailable");
    public Task<IReadOnlyList<WorkStoreFile>> ListAsync(CancellationToken token) => throw new WorkStoreException("store_unavailable");
    public Task<WorkStoreFile> ReadAsync(string workId, CancellationToken token) => throw new WorkStoreException("store_unavailable");
    public Task<WorkStoreMutation> PublishAsync(string workId, ReadOnlyMemory<byte> bytes, CancellationToken token) => Task.FromResult(new WorkStoreMutation("store_unavailable"));
    public Task<WorkStoreMutation> DeleteAsync(WorkStoreFile expected, CancellationToken token) => Task.FromResult(new WorkStoreMutation("store_unavailable"));
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}