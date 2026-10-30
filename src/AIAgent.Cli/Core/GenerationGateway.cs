namespace AIAgent.Cli.Core;

// One request event updates program and owning turn once; per-child totals are projections.
internal sealed class GenerationGateway(object gate, UsageAccumulator program, UsageAccumulator turn, ModelProfile? profile)
{
    private long _sequence;
    private readonly HashSet<long> _recorded = [];
    internal async Task<LlmResult> GenerateAsync(ILlmClient client, LlmRequest request, GenerationPurpose purpose,
        CancellationToken token, Func<bool> begin, UsageAccumulator? child = null)
    {
        long id;
        lock (gate)
        {
            token.ThrowIfCancellationRequested();
            if (!begin()) throw new DelegationFailure("delegation_budget_insufficient");
            id = checked(++_sequence); program.BeginGeneration(profile, purpose);
        }
        var result = await client.GenerateAsync(request, token);
        if (result is not null) lock (gate)
            {
                if (_recorded.Add(id)) { program.RecordAttributed(result, profile, purpose); turn.Record(result); child?.Record(result); }
            }
        return result!;
    }
    internal UsageSnapshot Snapshot(UsageAccumulator value) { lock (gate) return value.Snapshot(); }
    public override string ToString() => "[GenerationGateway]";
}