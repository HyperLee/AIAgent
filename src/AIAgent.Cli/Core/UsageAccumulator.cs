namespace AIAgent.Cli.Core;

internal sealed record UsageFieldSnapshot(long KnownTotal, bool HasKnownValue, long MissingRequestCount, bool Overflowed);
internal sealed record UsageSnapshot(long DispatchedRequests, bool CounterOverflowed, UsageFieldSnapshot Prompt,
    UsageFieldSnapshot Output, UsageFieldSnapshot Thought, UsageFieldSnapshot Cached, UsageFieldSnapshot Total);

internal sealed class UsageAccumulator
{
    private readonly Dictionary<ModelProfile, ProfileUsage> _profiles = [];
    internal void BeginGeneration(ModelProfile? profile, GenerationPurpose purpose)
    {
        if (profile is null || !ModelCatalog.Profiles.Contains(profile)) return;
        if (!_profiles.TryGetValue(profile, out var value)) _profiles.Add(profile, value = new());
        value.Begin(purpose);
    }
    internal void RecordAttributed(LlmResult result, ModelProfile? profile, GenerationPurpose purpose)
    {
        Record(result);
        if (profile is not null && _profiles.TryGetValue(profile, out var value)) value.Record(result, purpose);
    }
    internal IReadOnlyList<ProfileUsageSnapshot> ProfileSnapshots() => Array.AsReadOnly(ModelCatalog.Profiles.Where(_profiles.ContainsKey)
        .Select(p => _profiles[p].Snapshot(p)).ToArray());

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

    private sealed class ProfileUsage
    {
        private long _started;
        private bool _overflow;
        private readonly UsageAccumulator _usage = new();
        private readonly Dictionary<GenerationPurpose, PurposeUsage> _purposes = [];
        internal void Begin(GenerationPurpose purpose)
        {
            Increment(ref _started, ref _overflow);
            if (!_purposes.TryGetValue(purpose, out var value)) _purposes.Add(purpose, value = new());
            value.Begin();
        }
        internal void Record(LlmResult result, GenerationPurpose purpose) { _usage.Record(result); _purposes[purpose].Usage.Record(result); }
        internal ProfileUsageSnapshot Snapshot(ModelProfile profile) => new(profile, _started, _overflow, _usage.Snapshot(),
            Array.AsReadOnly(_purposes.Select(p => new PurposeUsageSnapshot(p.Key, p.Value.Started, p.Value.Overflow, p.Value.Usage.Snapshot())).ToArray()));
    }
    private sealed class PurposeUsage
    {
        internal long Started;
        internal bool Overflow;
        internal UsageAccumulator Usage { get; } = new();
        internal void Begin() => Increment(ref Started, ref Overflow);
    }
}

internal sealed class ProfileUsageSnapshot(ModelProfile profile, long generationsStarted, bool overflowed, UsageSnapshot usage, IReadOnlyList<PurposeUsageSnapshot> purposes)
{
    internal ModelProfile Profile { get; } = profile;
    internal long GenerationsStarted { get; } = generationsStarted;
    internal bool Overflowed { get; } = overflowed;
    internal UsageSnapshot Usage { get; } = usage;
    internal IReadOnlyList<PurposeUsageSnapshot> Purposes { get; } = purposes;
    public override string ToString() => "[ProfileUsageSnapshot]";
}
internal sealed record PurposeUsageSnapshot(GenerationPurpose Purpose, long GenerationsStarted, bool Overflowed, UsageSnapshot Usage);