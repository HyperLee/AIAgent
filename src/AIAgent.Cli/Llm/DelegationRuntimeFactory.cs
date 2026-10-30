using System.Text.Json.Serialization;

using AIAgent.Cli.Core;

namespace AIAgent.Cli.Llm;

// This capability is retained by the trusted parent coordinator, never by a child loop.
[JsonConverter(typeof(DelegationOpaqueConverter))]
internal sealed class DelegationRuntimeFactory(ChatRuntimeFactory factory, BootstrapSession session) : IDelegationRuntimeFactory
{
    public async Task<DelegationRuntime> CreateAsync(DelegationChildSpec spec, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!session.IsModelSessionCurrent || session.ModelSession is { } lease && lease.Generation != spec.Stamp.ModelSessionGeneration)
            throw new DelegationFailure("delegation_source_changed");
        var client = factory.CreateClient(session);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var inspector = factory.CreateContextInspector(client) ?? throw new DelegationFailure("context_capacity");
            return new(client, inspector);
        }
        catch { await client.DisposeAsync(); throw; }
    }
    public override string ToString() => "[DelegationRuntimeFactory]";
}