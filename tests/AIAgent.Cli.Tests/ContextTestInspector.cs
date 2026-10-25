using System.Text;

using AIAgent.Cli.Core;
using AIAgent.Cli.Llm;

namespace AIAgent.Cli.Tests;

internal sealed class ContextTestInspector(ScriptClient client) : ILlmContextInspector
{
    public ContextRequestMeasure Measure(LlmRequest request) => new(ContextMeasureStatus.Measured,
        Encoding.UTF8.GetByteCount(request.SystemInstruction) + request.Messages.Sum(m => m.UserText?.Length ?? 100));
    public ContextTurnProjection ProjectCompletedTurn(long turnId, IReadOnlyList<LlmMessage> messages)
    {
        if (!ToolCallPairing.ValidTurn(messages)) throw new ContextInspectionException();
        var result = client.Returned.LastOrDefault(r => ReferenceEquals(r.ModelMessage, messages[^1]));
        var tools = messages.SelectMany(m => m.Results).Select(GeminiContextTools.Project).Where(t => t.HasValue).Select(t => t!.Value).ToArray();
        return new(turnId, messages[0].UserText!, result?.SafeText ?? "answer", tools);
    }
}