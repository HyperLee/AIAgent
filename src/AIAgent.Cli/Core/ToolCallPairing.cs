using System.Text.Json;

namespace AIAgent.Cli.Core;

/// <summary>只驗證 SDK 無關的投影，不讀取或重建原生模型紀錄。</summary>
internal static class ToolCallPairing
{
    internal static bool ValidCalls(IReadOnlyList<LlmToolCall> calls)
    {
        var previous = -1;
        foreach (var call in calls)
        {
            if (call is null || call.PartIndex <= previous || string.IsNullOrWhiteSpace(call.Name) ||
                call.Arguments.ValueKind != JsonValueKind.Object) return false;
            previous = call.PartIndex;
        }
        return true;
    }

    internal static bool MatchesProjection(LlmMessage model, IReadOnlyList<LlmToolCall> calls) =>
        model.Kind == LlmMessageKind.Model && model.Protocol is not null && ValidCalls(calls) &&
        model.ModelCalls.Count == calls.Count && model.ModelCalls.Zip(calls).All(pair =>
            pair.First is not null && SameIdentity(pair.First, pair.Second) &&
            JsonElement.DeepEquals(pair.First.Arguments, pair.Second.Arguments));

    internal static bool MatchesResults(IReadOnlyList<LlmToolCall> calls, IReadOnlyList<LlmToolResult> results) =>
        calls.Count > 0 && ValidCalls(calls) && calls.Count == results.Count && calls.Zip(results).All(pair =>
            pair.Second is not null && pair.First.PartIndex == pair.Second.PartIndex &&
            string.Equals(pair.First.Name, pair.Second.Name, StringComparison.Ordinal) &&
            string.Equals(pair.First.CallId, pair.Second.CallId, StringComparison.Ordinal) &&
            pair.Second.Response.ValueKind == JsonValueKind.Object);

    private static bool SameIdentity(LlmToolCall a, LlmToolCall b) => a.PartIndex == b.PartIndex &&
        string.Equals(a.Name, b.Name, StringComparison.Ordinal) && string.Equals(a.CallId, b.CallId, StringComparison.Ordinal);

    internal static bool ValidTurn(IReadOnlyList<LlmMessage> messages)
    {
        if (messages.Count < 2 || messages[0] is not { Kind: LlmMessageKind.User } user ||
            string.IsNullOrWhiteSpace(user.UserText)) return false;
        for (var index = 1; index < messages.Count; index += 2)
        {
            if (messages[index] is not { Kind: LlmMessageKind.Model, Protocol: not null } model || !ValidCalls(model.ModelCalls)) return false;
            if (model.ModelCalls.Count == 0) return index == messages.Count - 1;
            if (index + 1 >= messages.Count || messages[index + 1] is not { Kind: LlmMessageKind.ToolResults } results ||
                !MatchesResults(model.ModelCalls, results.Results)) return false;
        }
        return false;
    }
}