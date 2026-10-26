using System.Globalization;
using System.Text;
using System.Text.Json;

using AIAgent.Cli.Security;
using AIAgent.Cli.Workspace;

namespace AIAgent.Cli.Core;

/// <summary>工作文件的嚴格白名單格式；不接觸檔案系統或恢復操作授權。</summary>
internal static class WorkSaveCodec
{
    internal const int MaxDocumentBytes = 65536;
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private const string ReservedId = "xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx";
    private static readonly string[] RootFields = ["format_version", "work_id", "title", "created_utc", "workspace", "scope_hint", "rules_refs", "summary", "pins", "plan", "file_refs"];
    private static readonly string[] TaskFields = ["kind", "title", "acceptance", "requirements_version", "requirements", "reported_state", "reported_basis", "summary", "block_reason"];
    private static readonly HashSet<string> BlockReasons = new(StringComparer.Ordinal)
    {
        "capability_unavailable", "turn_paused", "turn_cancelled", "generation_limit", "model_outcome", "operation_failed",
        "approval_declined", "file_conflict", "write_unconfirmed", "mode_changed", "model_reported", "resource_limit",
        "workspace_unavailable", "local_failure", "process_start_failed", "process_failed", "process_timed_out", "process_output_limit",
        "validation_inconclusive", "process_unconfirmed", "rules_changed", "rules_unavailable", "rules_scope_mismatch", "rules_limit"
    };

    internal static bool ValidId(string? value) => value is { Length: 32 } && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');

    internal static WorkSavedPlan? FromPlan(PlanSnapshot? plan) => plan is null ? null : new(plan.Objective,
        plan.Tasks.Select(t => new WorkSavedTask(t.Kind, t.Title, t.Acceptance, t.RequirementsVersion, t.Requirements,
            t.State, t.CompletionBasis, t.Summary, t.BlockReason)).ToArray());

    internal static byte[] Write(WorkSaveDocument document, WorkPersistencePolicy policy)
    {
        try
        {
            var bytes = ContextDocuments.Write(MaxDocumentBytes, w => WriteDocument(w, document));
            _ = Parse(bytes, document.WorkId, policy);
            return bytes;
        }
        catch (ContextCapacityException) { throw Error("work_capacity"); }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or EncoderFallbackException or NullReferenceException)
        { throw Error("work_format_invalid"); }
    }

    internal static WorkSaveDocument Parse(ReadOnlyMemory<byte> bytes, string expectedId, WorkPersistencePolicy policy)
    {
        try
        {
            if (bytes.Length > MaxDocumentBytes) throw Error("work_capacity");
            if (!ValidId(expectedId) || bytes.IsEmpty || bytes.Span.StartsWith(new byte[] { 239, 187, 191 })) throw Error("work_format_invalid");
            if (!policy.IsSafeRaw(bytes.Span)) throw Error("work_sensitive_content");
            _ = StrictUtf8.GetString(bytes.Span);
            using var json = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 16 });
            CheckUnicode(json.RootElement);
            if (!policy.IsSafe(bytes.Span) || !policy.IsSafeJson(json.RootElement)) throw Error("work_sensitive_content");
            var fields = Fields(json.RootElement, RootFields);
            Version(fields["format_version"]);
            var id = Text(fields["work_id"], 32);
            if (id != expectedId) throw Error("work_format_invalid");
            var title = Text(fields["title"], 80);
            var created = Text(fields["created_utc"], 20);
            if (!DateTime.TryParseExact(created, "yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var timestamp) ||
                timestamp.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture) != created) throw Error("work_format_invalid");
            var workspaceFields = Fields(fields["workspace"], ["root", "device", "identity"]);
            var root = Text(workspaceFields["root"], 4096);
            if (!root.StartsWith('/') || root.Contains('\\') || Path.GetFullPath(root) != root || root.Length > 1 && root.EndsWith('/'))
                throw Error("work_format_invalid");
            var workspace = new WorkWorkspaceIdentity(root, UInt64(workspaceFields["device"], false), UInt64(workspaceFields["identity"], true));
            var scope = RelativePath(fields["scope_hint"]);
            var rules = Rules(fields["rules_refs"], scope);
            var summary = Summary(fields["summary"], policy);
            var pins = Pins(fields["pins"], summary, id, policy);
            var plan = Plan(fields["plan"], policy);
            var references = References(fields["file_refs"], plan);
            var result = new WorkSaveDocument(id, title, created, workspace, scope, rules, summary, pins, plan, references);
            var importedSummary = result.Summary is { } imported ? new ContextSummary(0, 0, imported.Goals, imported.Decisions, imported.OpenQuestions, imported.Background, id) : null;
            var importedPins = result.Pins.Select((pin, index) => new ContextPin("n" + (index + 1).ToString(CultureInfo.InvariantCulture), pin, id)).ToArray();
            if (!policy.IsSafe(WorkHistoryProjection.Memory(importedSummary, importedPins, result.Plan, id))) throw Error("work_sensitive_content");
            var canonical = ContextDocuments.Write(MaxDocumentBytes, w => WriteDocument(w, result));
            if (!policy.IsSafe(canonical)) throw Error("work_sensitive_content");
            return result;
        }
        catch (ContextCapacityException) { throw Error("work_capacity"); }
        catch (Exception ex) when (ex is JsonException or ArgumentException or InvalidOperationException or DecoderFallbackException or EncoderFallbackException or OverflowException)
        { throw Error("work_format_invalid"); }
    }

    private static WorkDocumentException Error(string code) => new(code);

    private static Dictionary<string, JsonElement> Fields(JsonElement value, string[] names)
    {
        if (!PlanArguments.Fields(value, names, names, out var fields)) throw Error("work_format_invalid");
        return fields;
    }

    private static void Version(JsonElement value)
    {
        var raw = value.GetRawText();
        if (value.ValueKind != JsonValueKind.Number || raw.Length == 0 || !raw.All(char.IsAsciiDigit) || raw.Length > 1 && raw[0] == '0')
            throw Error("work_format_invalid");
        if (raw != "1") throw Error("work_version_unsupported");
    }

    private static string Text(JsonElement value, int maximum)
    {
        if (value.ValueKind != JsonValueKind.String) throw Error("work_format_invalid");
        var text = value.GetString()!;
        if (text.Length is 0 || text.Length > maximum || string.IsNullOrWhiteSpace(text) || !PlanArguments.ValidUnicode(text))
            throw Error("work_format_invalid");
        return text;
    }

    private static string? NullableText(JsonElement value, int maximum) => value.ValueKind == JsonValueKind.Null ? null : Text(value, maximum);

    private static void CheckUnicode(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.String) _ = value.GetString();
        else if (value.ValueKind == JsonValueKind.Array) foreach (var child in value.EnumerateArray()) CheckUnicode(child);
        else if (value.ValueKind == JsonValueKind.Object)
            foreach (var property in value.EnumerateObject()) { _ = property.Name; CheckUnicode(property.Value); }
    }

    private static ulong UInt64(JsonElement value, bool nonzero)
    {
        var text = Text(value, 20);
        if (!text.All(char.IsAsciiDigit) || text.Length > 1 && text[0] == '0' ||
            !ulong.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var number) || nonzero && number == 0)
            throw Error("work_format_invalid");
        return number;
    }

    private static string RelativePath(JsonElement value)
    {
        var path = Text(value, 4096);
        if (!WorkspacePathRules.IsCanonical(path) || WorkspacePathRules.IsSensitive(path) ||
            path.Split('/').Any(segment => segment.Equals(".aiagent", StringComparison.OrdinalIgnoreCase))) throw Error("work_format_invalid");
        return path;
    }

    private static string Hash(JsonElement value)
    {
        var hash = Text(value, 64);
        if (hash.Length != 64 || hash.Any(c => c is not (>= '0' and <= '9' or >= 'a' and <= 'f'))) throw Error("work_format_invalid");
        return hash;
    }

    private static IReadOnlyList<WorkRuleReference> Rules(JsonElement value, string scope)
    {
        if (value.ValueKind != JsonValueKind.Array) throw Error("work_format_invalid");
        if (value.GetArrayLength() > 8) throw Error("work_capacity");
        var rules = new List<WorkRuleReference>();
        var previousDepth = -1;
        foreach (var item in value.EnumerateArray())
        {
            var fields = Fields(item, ["path", "sha256"]);
            var path = RelativePath(fields["path"]);
            if (path != "AGENTS.md" && !path.EndsWith("/AGENTS.md", StringComparison.Ordinal)) throw Error("work_format_invalid");
            var directory = path == "AGENTS.md" ? "." : path[..^10];
            var depth = directory == "." ? 0 : directory.Count(c => c == '/') + 1;
            if (directory != "." && scope != directory && !scope.StartsWith(directory + "/", StringComparison.Ordinal) || depth <= previousDepth)
                throw Error("work_format_invalid");
            previousDepth = depth;
            rules.Add(new(path, Hash(fields["sha256"])));
        }
        return rules.AsReadOnly();
    }

    private static ContextSummary? Summary(JsonElement value, WorkPersistencePolicy policy)
    {
        if (value.ValueKind == JsonValueKind.Null) return null;
        var fields = Fields(value, ["version", "goals", "decisions", "open_questions", "background"]);
        Version(fields["version"]);
        if (StrictUtf8.GetByteCount(value.GetRawText()) > ContextDocuments.MaxSummaryBytes) throw Error("work_capacity");
        var collections = new List<string[]>();
        foreach (var name in new[] { "goals", "decisions", "open_questions", "background" })
        {
            if (fields[name].ValueKind != JsonValueKind.Array) throw Error("work_format_invalid");
            if (fields[name].GetArrayLength() > 16) throw Error("work_capacity");
            collections.Add(fields[name].EnumerateArray().Select(item => Text(item, 1024)).ToArray());
        }
        if (collections.All(items => items.Length == 0)) throw Error("work_format_invalid");
        var content = new ContextSummary(0, 0, collections[0], collections[1], collections[2], collections[3]);
        _ = ContextDocuments.Write(ContextDocuments.MaxSummaryBytes, w => ContextDocuments.WriteSummary(w, content));
        var result = ContextDocuments.ParseSummary(value.GetRawText(), 0, 0, policy.CurrentPolicy);
        if (result is null) throw Error("work_format_invalid");
        var canonical = ContextDocuments.Write(ContextDocuments.MaxSummaryBytes, w => ContextDocuments.WriteSummary(w, result));
        if (!policy.IsSafe(canonical)) throw Error("work_sensitive_content");
        return result;
    }

    private static IReadOnlyList<string> Pins(JsonElement value, ContextSummary? summary, string workId, WorkPersistencePolicy policy)
    {
        if (value.ValueKind != JsonValueKind.Array) throw Error("work_format_invalid");
        if (value.GetArrayLength() > 16) throw Error("work_capacity");
        var pins = value.EnumerateArray().Select(item => Text(item, 1024)).ToArray();
        var saved = ContextDocuments.Write(ContextDocuments.MaxPinsBytes, w =>
        { w.WriteStartObject(); w.WriteStartArray("pins"); foreach (var pin in pins) w.WriteStringValue(pin); w.WriteEndArray(); w.WriteEndObject(); });
        var assigned = pins.Select((pin, index) => new ContextPin("n" + (index + 1).ToString(CultureInfo.InvariantCulture), pin, workId)).ToArray();
        var imported = ContextDocuments.Pins(assigned);
        var importedSummary = summary is null ? null : new ContextSummary(0, 0, summary.Goals, summary.Decisions, summary.OpenQuestions, summary.Background, workId);
        var memory = ContextDocuments.Memory(importedSummary, assigned);
        if (!policy.IsSafe(saved) || !policy.IsSafe(imported) || !policy.IsSafe(memory)) throw Error("work_sensitive_content");
        return Array.AsReadOnly(pins);
    }

    private static WorkSavedPlan? Plan(JsonElement value, WorkPersistencePolicy policy)
    {
        if (value.ValueKind == JsonValueKind.Null) return null;
        try
        {
            var fields = Fields(value, ["objective", "tasks"]);
            var objective = Text(fields["objective"], 2048);
            var array = fields["tasks"];
            if (array.ValueKind != JsonValueKind.Array || array.GetArrayLength() is < 1 or > PlanLimits.MaxTasks) throw Error("work_plan_invalid");
            var definitions = new List<Dictionary<string, JsonElement>>();
            foreach (var item in array.EnumerateArray()) definitions.Add(Fields(item, TaskFields));
            var arguments = ContextDocuments.Write(MaxDocumentBytes, w =>
            {
                w.WriteStartObject(); w.WriteNumber("expected_revision", 0); w.WriteString("objective", objective); w.WriteStartArray("tasks");
                foreach (var task in definitions)
                {
                    w.WriteStartObject();
                    foreach (var name in new[] { "kind", "title", "acceptance", "requirements_version", "requirements" })
                    { w.WritePropertyName(name); task[name].WriteTo(w); }
                    w.WriteEndObject();
                }
                w.WriteEndArray(); w.WriteEndObject();
            });
            using var definition = JsonDocument.Parse(arguments);
            if (!PlanArguments.Submit(definition.RootElement, policy.CurrentPolicy, out _, out var normalized, out var parsed, out _) || normalized != objective)
                throw Error("work_plan_invalid");
            var tasks = new List<WorkSavedTask>();
            for (var index = 0; index < parsed.Count; index++)
            {
                var source = definitions[index]; var task = parsed[index];
                if (task.Title != Text(source["title"], 256) || task.Acceptance != Text(source["acceptance"], 1024) ||
                    !task.Requirements.Select(r => r.Path).SequenceEqual(source["requirements"].EnumerateArray().Select(r => r.GetProperty("path").GetString()), StringComparer.Ordinal))
                    throw Error("work_plan_invalid");
                var state = Text(source["reported_state"], 32) switch
                {
                    "pending" => PlanTaskState.Pending,
                    "in_progress" => PlanTaskState.InProgress,
                    "completed" => PlanTaskState.Completed,
                    "blocked" => PlanTaskState.Blocked,
                    _ => (PlanTaskState)(-1)
                };
                var basis = Text(source["reported_basis"], 32) switch
                {
                    "none" => TaskCompletionBasis.None,
                    "local_operation" => TaskCompletionBasis.LocalOperation,
                    "model_assessment" => TaskCompletionBasis.ModelAssessment,
                    _ => (TaskCompletionBasis)(-1)
                };
                var summary = NullableText(source["summary"], 2048);
                var reason = NullableText(source["block_reason"], 64);
                if (!Enum.IsDefined(state) || !Enum.IsDefined(basis) ||
                    (state == PlanTaskState.Completed ? basis != (task.Kind == PlanTaskKind.Analysis ? TaskCompletionBasis.ModelAssessment : TaskCompletionBasis.LocalOperation) : basis != TaskCompletionBasis.None) ||
                    (state == PlanTaskState.Blocked ? reason is null || !BlockReasons.Contains(reason) : reason is not null)) throw Error("work_plan_invalid");
                tasks.Add(new(task.Kind, task.Title, task.Acceptance, task.RequirementsVersion, task.Requirements, state, basis, summary, reason));
            }
            var result = new WorkSavedPlan(objective, tasks);
            ValidatePlanCapacity(result, policy);
            return result;
        }
        catch (WorkDocumentException ex) when (ex.Code == "work_format_invalid") { throw Error("work_plan_invalid"); }
    }

    private static void ValidatePlanCapacity(WorkSavedPlan plan, WorkPersistencePolicy policy)
    {
        var snapshot = new PlanSnapshot(ReservedId, 1, plan.Objective, PlanApprovalState.Draft,
            plan.Tasks.Select(t => new PlanTaskSnapshot(ReservedId, t.Kind, t.Title, t.Acceptance, t.Requirements,
                t.ReportedState, t.Summary, t.BlockReason, t.ReportedBasis, requirementsVersion: t.RequirementsVersion)).ToArray());
        var actual = PlanProjection.State(AgentMode.Plan, snapshot);
        var prospective = PlanProjection.State(AgentMode.Plan, snapshot, true);
        var display = PlanProjection.Display(AgentMode.Plan, snapshot, 0, true);
        var upperDisplay = PlanProjection.Display(AgentMode.Plan, snapshot, 256, true, true);
        if (actual.Length > PlanLimits.MaxProjectionBytes || prospective.Length > PlanLimits.MaxProjectionBytes ||
            StrictUtf8.GetByteCount(display) > PlanLimits.MaxDisplayBytes || StrictUtf8.GetByteCount(upperDisplay) > PlanLimits.MaxDisplayBytes)
            throw Error("work_capacity");
        if (!policy.IsSafe(actual) || !policy.IsSafe(prospective) || !policy.IsSafe(display) || !policy.IsSafe(upperDisplay)) throw Error("work_sensitive_content");
    }

    private static IReadOnlyList<WorkFileReference> References(JsonElement value, WorkSavedPlan? plan)
    {
        if (value.ValueKind != JsonValueKind.Array) throw Error("work_format_invalid");
        if (value.GetArrayLength() > 160) throw Error("work_capacity");
        var references = new List<WorkFileReference>();
        foreach (var item in value.EnumerateArray())
        {
            var fields = Fields(item, ["path", "observation", "byte_count", "sha256"]);
            var path = RelativePath(fields["path"]);
            var observation = Text(fields["observation"], 32);
            if (observation is not ("missing" or "file" or "directory" or "unavailable" or "outside_scope")) throw Error("work_format_invalid");
            long? count = null; string? hash = null;
            if (observation == "file")
            {
                if (!PlanArguments.Integer(fields["byte_count"], 0, out var number) || number > 131072) throw Error("work_format_invalid");
                count = number; hash = Hash(fields["sha256"]);
            }
            else if (fields["byte_count"].ValueKind != JsonValueKind.Null || fields["sha256"].ValueKind != JsonValueKind.Null) throw Error("work_format_invalid");
            references.Add(new(path, observation, count, hash));
        }
        var expected = plan?.Tasks.SelectMany(t => t.Requirements).Select(r => r.Path).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray() ?? [];
        if (!references.Select(r => r.Path).SequenceEqual(expected, StringComparer.Ordinal)) throw Error("work_format_invalid");
        return references.AsReadOnly();
    }

    private static void WriteDocument(Utf8JsonWriter w, WorkSaveDocument document)
    {
        w.WriteStartObject(); w.WriteNumber("format_version", 1); w.WriteString("work_id", document.WorkId);
        w.WriteString("title", document.Title); w.WriteString("created_utc", document.CreatedUtc);
        w.WriteStartObject("workspace"); w.WriteString("root", document.Workspace.Root);
        w.WriteString("device", document.Workspace.Device.ToString(CultureInfo.InvariantCulture));
        w.WriteString("identity", document.Workspace.Identity.ToString(CultureInfo.InvariantCulture)); w.WriteEndObject();
        w.WriteString("scope_hint", document.ScopeHint); w.WriteStartArray("rules_refs");
        foreach (var rule in document.RulesRefs) { w.WriteStartObject(); w.WriteString("path", rule.Path); w.WriteString("sha256", rule.Sha256); w.WriteEndObject(); }
        w.WriteEndArray(); w.WritePropertyName("summary");
        if (document.Summary is null) w.WriteNullValue(); else ContextDocuments.WriteSummary(w, document.Summary);
        w.WriteStartArray("pins"); foreach (var pin in document.Pins) w.WriteStringValue(pin); w.WriteEndArray();
        w.WritePropertyName("plan");
        WriteSavedPlan(w, document.Plan);
        w.WriteStartArray("file_refs");
        foreach (var reference in document.FileRefs)
        {
            w.WriteStartObject(); w.WriteString("path", reference.Path); w.WriteString("observation", reference.Observation);
            if (reference.ByteCount is { } count) w.WriteNumber("byte_count", count); else w.WriteNull("byte_count");
            w.WriteString("sha256", reference.Sha256); w.WriteEndObject();
        }
        w.WriteEndArray(); w.WriteEndObject();
    }
    internal static void WriteSavedPlan(Utf8JsonWriter w, WorkSavedPlan? savedPlan)
    {
        if (savedPlan is not { } plan) w.WriteNullValue();
        else
        {
            w.WriteStartObject(); w.WriteString("objective", plan.Objective); w.WriteStartArray("tasks");
            foreach (var task in plan.Tasks)
            {
                w.WriteStartObject(); w.WriteString("kind", PlanProjection.Name(task.Kind)); w.WriteString("title", task.Title);
                w.WriteString("acceptance", task.Acceptance); w.WriteNumber("requirements_version", task.RequirementsVersion); w.WriteStartArray("requirements");
                foreach (var requirement in task.Requirements)
                {
                    w.WriteStartObject(); w.WriteString("tool", requirement.ToolName); w.WriteString("path", requirement.Path);
                    if (requirement.StartLine is { } start) w.WriteNumber("start_line", start);
                    if (requirement.EndLine is { } end) w.WriteNumber("end_line", end);
                    if (requirement.Task is { } process) w.WriteString("task", PlanProjection.Name(process));
                    w.WriteEndObject();
                }
                w.WriteEndArray(); w.WriteString("reported_state", PlanProjection.Name(task.ReportedState));
                w.WriteString("reported_basis", PlanProjection.Name(task.ReportedBasis)); w.WriteString("summary", task.Summary);
                w.WriteString("block_reason", task.BlockReason); w.WriteEndObject();
            }
            w.WriteEndArray(); w.WriteEndObject();
        }
    }

}