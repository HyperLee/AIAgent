namespace AIAgent.Cli.Core;

internal sealed class ConversationContext
{
    internal const int MaxCompletedTurns = 5;
    private readonly Queue<IReadOnlyList<LlmMessage>> _turns = new();
    internal int CompletedTurnCount => _turns.Count;

    internal IReadOnlyList<LlmMessage> BuildMessages(LlmMessage currentUserMessage)
    {
        if (currentUserMessage is null || currentUserMessage.Kind != LlmMessageKind.User || string.IsNullOrWhiteSpace(currentUserMessage.UserText))
            throw new ArgumentException("目前訊息無效");
        return Array.AsReadOnly(_turns.SelectMany(turn => turn).Append(currentUserMessage).ToArray());
    }

    internal void CommitCompletedTurn(IReadOnlyList<LlmMessage> messages)
    {
        if (messages is null || !ToolCallPairing.ValidTurn(messages))
            throw new ArgumentException("完整回合無效");
        _turns.Enqueue(Array.AsReadOnly(messages.ToArray()));
        while (_turns.Count > MaxCompletedTurns) _turns.Dequeue();
    }

    internal void Clear() => _turns.Clear();
    public override string ToString() => "[ConversationContext]";
}