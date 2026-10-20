namespace AIAgent.Cli.Core;

internal sealed record UsageFieldSnapshot(long KnownTotal, bool HasKnownValue, long MissingRequestCount, bool Overflowed);
internal sealed record UsageSnapshot(long DispatchedRequests, bool CounterOverflowed, UsageFieldSnapshot Prompt,
    UsageFieldSnapshot Output, UsageFieldSnapshot Thought, UsageFieldSnapshot Cached, UsageFieldSnapshot Total);

internal sealed class UsageAccumulator
{
    private long _dispatchedRequests;
    private bool _counterOverflowed;
    private readonly Field _prompt = new();
    private readonly Field _output = new();
    private readonly Field _thought = new();
    private readonly Field _cached = new();
    private readonly Field _total = new();

    internal void Record(LlmResult result)
    {
        if (!result.RequestDispatched) return;
        Increment(ref _dispatchedRequests, ref _counterOverflowed);
        _prompt.Add(result.Usage?.PromptTokens);
        _output.Add(result.Usage?.OutputTokens);
        _thought.Add(result.Usage?.ThoughtTokens);
        _cached.Add(result.Usage?.CachedTokens);
        _total.Add(result.Usage?.TotalTokens);
    }

    internal UsageSnapshot Snapshot() => new(_dispatchedRequests, _counterOverflowed,
        _prompt.Snapshot(), _output.Snapshot(), _thought.Snapshot(), _cached.Snapshot(), _total.Snapshot());

    private static void Increment(ref long value, ref bool overflowed)
    {
        try { value = checked(value + 1); }
        catch (OverflowException) { overflowed = true; }
    }

    private sealed class Field
    {
        internal long Missing;
        private long _knownTotal;
        private bool _hasKnownValue;
        private bool _totalOverflowed;
        private bool _missingOverflowed;
        internal void Add(long? value)
        {
            if (value is null or < 0) { Increment(ref Missing, ref _missingOverflowed); return; }
            _hasKnownValue = true;
            if (_totalOverflowed) return;
            try { _knownTotal = checked(_knownTotal + value.Value); }
            catch (OverflowException) { _totalOverflowed = true; }
        }
        internal UsageFieldSnapshot Snapshot() => new(_knownTotal, _hasKnownValue, Missing, _totalOverflowed || _missingOverflowed);
    }
}