using System.Text.Json;
using AIAgent.Cli.Core;
using AIAgent.Cli.Security;
using Google.GenAI.Types;

namespace AIAgent.Cli.Llm;

internal sealed class GeminiModelContent(Content content, object owner, IReadOnlyList<LlmToolCall> calls)
{
    internal Content Content { get; } = content;
    internal object Owner { get; } = owner;
    internal IReadOnlyList<LlmToolCall> Calls { get; } = calls;
    public override string ToString() => "[GeminiModelContent]";
}

internal static class GeminiProtocol
{
    internal static List<Content> Convert(LlmRequest request, object owner, ISensitiveDataPolicy policy)
    {
        if (request is null || request.SystemInstruction is null || request.Messages.Count == 0) throw Invalid();
        var contents = new List<Content>();
        IReadOnlyList<LlmToolCall> pending = [];
        var previous = LlmMessageKind.Model;
        foreach (var message in request.Messages)
        {
            if (message is null) throw Invalid();
            switch (message.Kind)
            {
                case LlmMessageKind.User:
                    if (pending.Count != 0 || string.IsNullOrWhiteSpace(message.UserText)) throw Invalid();
                    Check(message.UserText, policy, ContentBoundary.ModelInput);
                    contents.Add(new Content { Role = "user", Parts = [new Part { Text = message.UserText }] });
                    break;
                case LlmMessageKind.Model:
                    if (contents.Count == 0 || previous == LlmMessageKind.Model || pending.Count != 0 || message.Protocol is not GeminiModelContent original || !ReferenceEquals(original.Owner, owner)) throw Invalid();
                    var cloned = JsonSerializer.Deserialize<Content>(JsonSerializer.SerializeToUtf8Bytes(original.Content)) ?? throw Invalid();
                    Check(JsonSerializer.SerializeToElement(cloned), policy, ContentBoundary.ModelInput);
                    contents.Add(cloned); pending = original.Calls;
                    break;
                case LlmMessageKind.ToolResults:
                    if (previous != LlmMessageKind.Model || pending.Count == 0 || message.Results.Count != pending.Count || message.Results.Any(r => r is null)) throw Invalid();
                    var parts = new List<Part>();
                    foreach (var call in pending)
                    {
                        var matches = message.Results.Where(r => r.PartIndex == call.PartIndex && r.Name == call.Name && r.CallId == call.CallId).ToArray();
                        if (matches.Length != 1 || matches[0].Response.ValueKind != JsonValueKind.Object) throw Invalid();
                        Check(matches[0].Response, policy, ContentBoundary.ToolResult);
                        Check(matches[0].Name, policy, ContentBoundary.ToolResult);
                        if (matches[0].CallId is { } id) Check(id, policy, ContentBoundary.ToolResult);
                        parts.Add(new Part { FunctionResponse = new FunctionResponse { Id = call.CallId, Name = call.Name, Response = Dictionary(matches[0].Response) } });
                    }
                    contents.Add(new Content { Role = "user", Parts = parts }); pending = [];
                    break;
                default: throw Invalid();
            }
            previous = message.Kind;
        }
        if (previous == LlmMessageKind.Model || pending.Count != 0) throw Invalid();
        Check(request.SystemInstruction, policy, ContentBoundary.ModelInput);
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var tool in request.Tools)
        {
            if (tool is null || string.IsNullOrWhiteSpace(tool.Name) || tool.Description is null || tool.Parameters.ValueKind != JsonValueKind.Object || !names.Add(tool.Name)) throw Invalid();
            Check(tool.Name, policy, ContentBoundary.ToolArguments); Check(tool.Description, policy, ContentBoundary.ToolArguments); Check(tool.Parameters, policy, ContentBoundary.ToolArguments);
        }
        return contents;
    }

    internal static GenerateContentConfig Config(LlmRequest request) => new()
    {
        SystemInstruction = new Content { Parts = [new Part { Text = request.SystemInstruction }] },
        CandidateCount = GeminiLimits.CandidateCount, MaxOutputTokens = GeminiLimits.MaxOutputTokens,
        AutomaticContinuation = false,
        Tools = request.Tools.Count == 0 ? null : [new Tool { FunctionDeclarations = request.Tools.Select(t => new FunctionDeclaration { Name = t.Name, Description = t.Description, ParametersJsonSchema = t.Parameters }).ToList() }]
    };

    private static Dictionary<string, object> Dictionary(JsonElement value) => value.EnumerateObject().ToDictionary(p => p.Name, p => (object)p.Value.Clone(), StringComparer.Ordinal);
    private static void Check(string value, ISensitiveDataPolicy policy, ContentBoundary boundary) { if (!policy.CanSendOrUse(value, boundary)) throw new GeminiBoundaryException(LlmErrorCode.SensitiveInput); }
    private static void Check(JsonElement value, ISensitiveDataPolicy policy, ContentBoundary boundary) { if (GeminiJson.IsSensitive(value, policy, boundary)) throw new GeminiBoundaryException(LlmErrorCode.SensitiveInput); }
    private static GeminiBoundaryException Invalid() => new(LlmErrorCode.InvalidRequest);
}
