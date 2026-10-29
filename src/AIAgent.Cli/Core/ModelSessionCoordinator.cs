namespace AIAgent.Cli.Core;

internal enum ModelApprovalDecision { Approved, Declined, Cancelled, EndOfInput, Unavailable }
internal sealed class ModelApprovalPreview(string displayBlock)
{
    internal string DisplayBlock { get; } = displayBlock;
    public override string ToString() => "[ModelApprovalPreview]";
}

internal sealed class ModelSessionCoordinator(object gate, ModelProfile profile)
{
    private long _generation;
    private ModelProfile _profile = profile;
    private bool _ready = true;
    internal long Generation { get { lock (gate) return _generation; } }
    internal ModelProfile Profile { get { lock (gate) return _profile; } }
    internal bool Ready { get { lock (gate) return _ready; } }
    internal ModelSessionLease Capture() { lock (gate) return new(this, _generation); }
    internal void BeginSwitch()
    { lock (gate) { var next = checked(_generation + 1); _ready = false; _generation = next; } }
    internal void Publish(ModelProfile value) { lock (gate) { _profile = value; _ready = true; } }
    internal bool IsCurrent(long generation) { lock (gate) return _ready && _generation == generation; }
    internal void Stop() { lock (gate) _ready = false; }
    public override string ToString() => "[ModelSessionCoordinator]";
}

internal sealed class ModelSessionLease(ModelSessionCoordinator owner, long generation)
{
    internal long Generation { get; } = generation;
    internal bool IsCurrent => owner.IsCurrent(Generation);
    public override string ToString() => "[ModelSessionLease]";
}