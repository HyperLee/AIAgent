namespace AIAgent.Cli.Core;

/// <summary>每筆外送的累計主動預算；確認及工作區重驗不持有計時器。</summary>
internal sealed class McpNetworkBudget(TimeProvider clock)
{
    private TimeSpan _used;
    internal TimeSpan Used => _used;
    internal async Task<T> RunAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken token)
    {
        var remaining = TimeSpan.FromSeconds(30) - _used;
        if (remaining <= TimeSpan.Zero) throw new McpFailure("mcp_timed_out");
        using var deadline = new CancellationTokenSource(remaining, clock);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, deadline.Token);
        var start = clock.GetTimestamp();
        try
        {
            var result = await operation(linked.Token);
            if (deadline.IsCancellationRequested && !token.IsCancellationRequested) throw new McpFailure("mcp_timed_out");
            token.ThrowIfCancellationRequested(); return result;
        }
        catch (OperationCanceledException) when (deadline.IsCancellationRequested && !token.IsCancellationRequested)
        { throw new McpFailure("mcp_timed_out"); }
        finally { _used = TimeSpan.FromTicks(checked(_used.Ticks + clock.GetElapsedTime(start).Ticks)); }
    }
}