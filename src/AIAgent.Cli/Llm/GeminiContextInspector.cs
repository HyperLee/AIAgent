using System.Text.Json;

using AIAgent.Cli.Core;
using AIAgent.Cli.Tools;
using AIAgent.Cli.Workspace;

namespace AIAgent.Cli.Llm;

public sealed partial class GeminiClient
{
    bool ILlmRuntimePolicyValidator.ValidatePolicy(string instruction, IReadOnlyList<LlmToolDefinition> tools)
    {
        lock (gate)
        {
            if (disposal is not null || sdk is null || policy is null) return false;
            try
            {
                var request = new LlmRequest(instruction, [LlmMessage.FromUserText("本機驗證")], tools);
                var contents = GeminiProtocol.Convert(request, owner, policy);
                var config = GeminiProtocol.Config(request);
                var inspected = JsonSerializer.SerializeToElement(new
                {
                    contents,
                    config,
                    wireFields = new[] { "systemInstruction", "generationConfig", "candidateCount", "maxOutputTokens", "functionDeclarations" }
                });
                return !GeminiJson.IsSensitive(inspected, policy, ContentBoundary.ModelInput);
            }
            catch (Exception) { return false; }
        }
    }
    private Action<long, bool>? _requestReport;
    internal ILlmContextInspector CreateContextInspector() => new Inspector(this);
    ContextRequestMeasure ILlmContextInspector.Measure(LlmRequest request) => CreateContextInspector().Measure(request);
    IDisposable ILlmContextTelemetry.ObserveRequest(Action<long, bool> report) => ((ILlmContextTelemetry)CreateContextInspector()).ObserveRequest(report);
    ContextTurnProjection ILlmContextInspector.ProjectCompletedTurn(long turnId, IReadOnlyList<LlmMessage> messages) =>
        CreateContextInspector().ProjectCompletedTurn(turnId, messages);
    private sealed class Inspector(GeminiClient client) : ILlmContextInspector, ILlmContextTelemetry, ILlmRequestCapacityInspector
    {
        public Task<ContextRequestMeasure> MeasureActualRequestAsync(LlmRequest request, CancellationToken cancellationToken) =>
            client.MeasureActualRequestAsync(request, cancellationToken);

        public ContextRequestMeasure Measure(LlmRequest request)
        {
            lock (client.gate)
            {
                if (client.disposal is not null || client.sdk is null || client.policy is null || !client.session.IsModelSessionCurrent) return new(ContextMeasureStatus.Unavailable);
                try
                {
                    var contents = GeminiProtocol.Convert(request, client.owner, client.policy);
                    var config = GeminiProtocol.Config(request);
                    var bytes = JsonSerializer.SerializeToUtf8Bytes(new { contents, config });
                    if (!ContextDocuments.Safe(bytes, client.policy)) return new(ContextMeasureStatus.Unavailable, requiresContextReset: true);
                    return new(ContextMeasureStatus.Measured, bytes.LongLength);
                }
                catch (GeminiBoundaryException) { return new(ContextMeasureStatus.Unavailable, requiresContextReset: true); }
                catch (Exception) { return new(ContextMeasureStatus.Unavailable); }
            }
        }
        public ContextTurnProjection ProjectCompletedTurn(long turnId, IReadOnlyList<LlmMessage> messages)
        {
            lock (client.gate)
            {
                if (client.disposal is not null || client.sdk is null || client.policy is null || !client.session.IsModelSessionCurrent) throw new ContextInspectionException();
                try
                {
                    var contents = GeminiProtocol.Convert(new("", messages, []), client.owner, client.policy, true);
                    var visible = string.Concat((contents[^1].Parts ?? []).Where(p => p.Thought != true).Select(p => p.Text));
                    var tools = new List<JsonElement>();
                    foreach (var message in messages.Where(m => m.Kind == LlmMessageKind.ToolResults))
                        foreach (var result in message.Results)
                            if (GeminiContextTools.Project(result) is { } metadata) tools.Add(metadata);
                    var projection = new ContextTurnProjection(turnId, messages[0].UserText!, visible, tools);
                    if (!ContextDocuments.Safe(ContextDocuments.Source(null, [projection]), client.policy)) throw new ContextInspectionException();
                    return projection;
                }
                catch (GeminiBoundaryException) { throw new ContextInspectionException(); }
            }
        }
        public IDisposable ObserveRequest(Action<long, bool> report)
        {
            lock (client.gate)
            {
                if (client.disposal is not null || client._requestReport is not null) throw new ContextInspectionException();
                client._requestReport = report; return new ReportLease(client);
            }
        }
        public override string ToString() => "[GeminiContextInspector]";
    }
    private sealed class ReportLease(GeminiClient client) : IDisposable
    {
        public void Dispose() { lock (client.gate) client._requestReport = null; }
    }
}

internal static class GeminiContextTools
{
    private static readonly HashSet<string> Registered = new(StringComparer.Ordinal)
    { "delegate_readonly", "list_files", "read_file", "create_file", "replace_text", "search_files", "search_text", "read_file_range", "submit_plan", "update_task", "run_dotnet_task", "git_status", "git_diff", "read_git_file_range", McpProfile.DocsName, McpProfile.CodeName, McpProfile.FetchName };
    internal static JsonElement? Project(LlmToolResult result)
    {
        if (!Registered.Contains(result.Name)) return null;
        var bytes = ContextDocuments.Write(ContextDocuments.MaxSourceBytes, w =>
        {
            w.WriteStartObject(); w.WriteString("name", result.Name);
            var root = result.Response;
            void Copy(JsonElement source, string name, string? target = null)
            {
                if (!source.TryGetProperty(name, out var value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Object or JsonValueKind.Array) return;
                if (name is "path" or "project_path" && (value.ValueKind != JsonValueKind.String ||
                    !WorkspacePathRules.IsCanonical(value.GetString()) || WorkspacePathRules.IsSensitive(value.GetString()!))) throw new ContextInspectionException();
                w.WritePropertyName(target ?? name); value.WriteTo(w);
            }
            Copy(root, "status"); Copy(root, "error_code");
            if (result.Name == "delegate_readonly")
            {
                if (root.TryGetProperty("data", out var batch) && batch.ValueKind == JsonValueKind.Object)
                {
                    Copy(batch, "batch_id"); Copy(batch, "state");
                    if (batch.TryGetProperty("children", out var children) && children.ValueKind == JsonValueKind.Array)
                    {
                        if (children.GetArrayLength() > 2) throw new ContextInspectionException();
                        w.WriteStartArray("children");
                        foreach (var child in children.EnumerateArray())
                        {
                            var role = DelegationJson.Text(child, "role"); var state = DelegationJson.Text(child, "state"); var code = DelegationJson.Text(child, "error_code");
                            if (role is not ("explore" or "review") || state is not ("completed" or "failed" or "cancelled" or "timed_out") || code is null || !ToolErrorCodes.IsValid(code)) throw new ContextInspectionException();
                            w.WriteStartObject(); foreach (var field in new[] { "agent_id", "role", "state", "error_code" }) Copy(child, field); w.WriteEndObject();
                        }
                        w.WriteEndArray();
                    }
                }
            }
            else if (result.Name is McpProfile.DocsName or McpProfile.CodeName or McpProfile.FetchName)
            {
                if (root.TryGetProperty("data", out var external) && external.ValueKind == JsonValueKind.Object)
                    foreach (var field in new[] { "source", "server_id", "remote_tool", "catalog_revision" }) Copy(external, field);
            }
            else if (result.Name is not ("submit_plan" or "update_task") && root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object)
            {
                Copy(data, "path"); Copy(data, "byte_count"); Copy(data, "truncated");
                if (result.Name is "git_status" or "git_diff" or "read_git_file_range")
                    foreach (var field in new[] { "snapshot_id", "selection", "layer", "side", "content_byte_count", "total_lines", "has_more" }) Copy(data, field);
                if (data.TryGetProperty("entries", out var entries) && entries.ValueKind == JsonValueKind.Array) w.WriteNumber("returned_entries", entries.GetArrayLength());
                if (result.Name == "run_dotnet_task")
                {
                    Copy(data, "project_path", "path");
                    foreach (var name in new[] { "task", "start_state", "end_state", "exit_code", "cleanup_confirmed", "validation" }) Copy(data, name);
                }
            }
            w.WriteEndObject();
        });
        using var document = JsonDocument.Parse(bytes); return document.RootElement.Clone();
    }
}