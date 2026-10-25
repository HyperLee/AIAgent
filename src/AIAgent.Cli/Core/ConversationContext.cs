namespace AIAgent.Cli.Core;

internal sealed class ConversationContext
{
    internal const int MaxCompletedTurns = 5;
    private readonly List<ContextCompletedTurn> _turns = [];
    private readonly List<ContextPin> _pins = [];
    private long _turnSequence;
    private long _pinSequence;
    internal object DecisionGate { get; } = new();
    internal long Revision { get; private set; }
    internal long PinsRevision { get; private set; }
    internal ContextSummary? Summary { get; private set; }
    internal int CompletedTurnCount { get { lock (DecisionGate) return _turns.Count; } }
    internal ContextSnapshot Capture()
    { lock (DecisionGate) return new(Revision, PinsRevision, Summary, _turns, _pins); }

    internal IReadOnlyList<LlmMessage> BuildMessages(LlmMessage currentUserMessage)
    {
        if (currentUserMessage is null || currentUserMessage.Kind != LlmMessageKind.User || string.IsNullOrWhiteSpace(currentUserMessage.UserText))
            throw new ArgumentException("目前訊息無效");
        lock (DecisionGate) return Array.AsReadOnly(_turns.SelectMany(turn => turn.Messages).Append(currentUserMessage).ToArray());
    }

    internal void CommitCompletedTurn(IReadOnlyList<LlmMessage> messages)
    {
        if (messages is null || !ToolCallPairing.ValidTurn(messages))
            throw new ArgumentException("完整回合無效");
        lock (DecisionGate)
        {
            if (_turns.Count >= MaxCompletedTurns) throw new InvalidOperationException("原始回合容量不足");
            var id = checked(_turnSequence + 1); var revision = checked(Revision + 1);
            var turn = new ContextCompletedTurn(id, messages);
            _turns.Add(turn); _turnSequence = id; Revision = revision;
        }
    }

    internal ContextPin NextPin(string text) => new("n" + checked(_pinSequence + 1).ToString(System.Globalization.CultureInfo.InvariantCulture), text);
    internal void AddPin(ContextPin pin)
    {
        var next = checked(_pinSequence + 1); var revision = checked(Revision + 1); var pinsRevision = checked(PinsRevision + 1);
        _pins.Add(pin); _pinSequence = next; Revision = revision; PinsRevision = pinsRevision;
    }
    internal void RemovePin(ContextPin pin)
    {
        var revision = checked(Revision + 1); var pinsRevision = checked(PinsRevision + 1);
        _pins.Remove(pin); Revision = revision; PinsRevision = pinsRevision;
    }
    internal void Publish(ContextSummary summary, int prefixCount)
    {
        var revision = checked(Revision + 1);
        _turns.RemoveRange(0, prefixCount); Summary = summary; Revision = revision;
    }
    internal void Clear()
    {
        lock (DecisionGate)
        {
            var revision = checked(Revision + 1); var pinsRevision = checked(PinsRevision + 1);
            _turns.Clear(); _pins.Clear(); Summary = null; Revision = revision; PinsRevision = pinsRevision;
        }
    }
    public override string ToString() => "[ConversationContext]";
}