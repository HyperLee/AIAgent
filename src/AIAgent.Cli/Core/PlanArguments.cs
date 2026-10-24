using System.Text.Json;

using AIAgent.Cli.Processes;
using AIAgent.Cli.Security;
using AIAgent.Cli.Workspace;

namespace AIAgent.Cli.Core;

/// <summary>純記憶體驗證；不藉由計畫檢查目標存在性。</summary>
internal static class PlanArguments
{
    private static bool SafeArguments(JsonElement value, ISensitiveDataPolicy policy, out string error)
    {
        error = "invalid_arguments";
        try
        {
            if (!SensitiveJson.Contains(value, policy, ContentBoundary.ToolArguments)) return true;
            error = "sensitive_arguments"; return false;
        }
        catch (InvalidOperationException) { return false; }
    }
    internal static bool Fields(JsonElement value, string[] allowed, string[] required, out Dictionary<string, JsonElement> fields)
    {
        fields = new(StringComparer.Ordinal);
        if (value.ValueKind != JsonValueKind.Object) return false;
        foreach (var property in value.EnumerateObject())
            if (!allowed.Contains(property.Name, StringComparer.Ordinal) || !fields.TryAdd(property.Name, property.Value)) return false;
        return required.All(fields.ContainsKey);
    }

    internal static bool Text(JsonElement value, int maximum, bool trim, ISensitiveDataPolicy policy, out string text)
    {
        text = "";
        if (value.ValueKind != JsonValueKind.String) return false;
        string raw;
        try { raw = value.GetString()!; }
        catch (InvalidOperationException) { return false; }
        if (raw.Length is 0 || raw.Length > maximum || !ValidUnicode(raw) ||
            !policy.CanSendOrUse(raw, ContentBoundary.ToolArguments)) return false;
        text = trim ? raw.Trim() : raw;
        return text.Length != 0 && policy.CanSendOrUse(text, ContentBoundary.ToolArguments);
    }

    internal static bool ValidUnicode(string text)
    {
        for (var index = 0; index < text.Length; index++)
        {
            var c = text[index];
            if (char.IsControl(c) || c is '\u2028' or '\u2029') return false;
            if (char.IsHighSurrogate(c))
            {
                if (++index == text.Length || !char.IsLowSurrogate(text[index])) return false;
            }
            else if (char.IsLowSurrogate(c)) return false;
        }
        return true;
    }

    internal static bool Integer(JsonElement value, long minimum, out long number)
    {
        number = 0;
        if (value.ValueKind != JsonValueKind.Number) return false;
        var raw = value.GetRawText();
        return raw.Length > 0 && raw.All(char.IsAsciiDigit) && (raw.Length == 1 || raw[0] != '0') &&
            value.TryGetInt64(out number) && number >= minimum;
    }

    internal static bool Submit(JsonElement value, ISensitiveDataPolicy policy, out long revision, out string objective,
        out IReadOnlyList<PlanTaskSnapshot> tasks, out string error)
    {
        revision = 0; objective = ""; tasks = []; error = "invalid_arguments";
        if (!SafeArguments(value, policy, out var sensitiveError)) { error = sensitiveError; return false; }
        if (!Fields(value, ["expected_revision", "objective", "tasks"], ["expected_revision", "objective", "tasks"], out var fields) ||
            !Integer(fields["expected_revision"], 0, out revision) || !Text(fields["objective"], 2048, true, policy, out objective) ||
            fields["tasks"].ValueKind != JsonValueKind.Array || fields["tasks"].GetArrayLength() is < 1 or > PlanLimits.MaxTasks) return false;
        var parsed = new List<PlanTaskSnapshot>();
        foreach (var item in fields["tasks"].EnumerateArray())
        {
            error = "invalid_arguments";
            if (!Fields(item, ["task_id", "kind", "title", "acceptance", "requirements", "requirements_version"], ["kind", "title", "acceptance", "requirements"], out var task) ||
                !Text(task["kind"], 32, false, policy, out var kindName) ||
                !Text(task["title"], 256, true, policy, out var title) || !Text(task["acceptance"], 1024, true, policy, out var acceptance)) return false;
            var kind = kindName switch
            {
                "analysis" => PlanTaskKind.Analysis,
                "read" => PlanTaskKind.Read,
                "write" => PlanTaskKind.Write,
                "validation" => PlanTaskKind.Validation,
                _ => (PlanTaskKind)(-1)
            };
            if (!Enum.IsDefined(kind)) return false;
            var id = "";
            if (task.TryGetValue("task_id", out var idValue) && !Text(idValue, 32, false, policy, out id)) return false;
            var version = 1L;
            if (task.TryGetValue("requirements_version", out var versionValue) &&
                (!Integer(versionValue, 1, out version) || version > 2)) return false;
            if (version == 2 && kind != PlanTaskKind.Validation) return false;
            if (!Requirements(task["requirements"], kind, (int)version, policy, out var requirements, out error)) return false;
            parsed.Add(new(id, kind, title, acceptance, requirements, requirementsVersion: (int)version));
        }
        tasks = parsed.AsReadOnly(); error = "none"; return true;
    }

    private static bool Requirements(JsonElement value, PlanTaskKind kind, int version, ISensitiveDataPolicy policy,
        out IReadOnlyList<TaskRequirement> requirements, out string error)
    {
        requirements = []; error = "invalid_arguments";
        if (value.ValueKind != JsonValueKind.Array || value.GetArrayLength() > PlanLimits.MaxRequirements ||
            (kind == PlanTaskKind.Analysis || kind == PlanTaskKind.Validation && version == 1 ? value.GetArrayLength() != 0 : value.GetArrayLength() == 0)) return false;
        var parsed = new List<TaskRequirement>();
        foreach (var item in value.EnumerateArray())
        {
            var process = kind == PlanTaskKind.Validation && version == 2;
            if (!Fields(item, process ? ["tool", "path", "task"] : ["tool", "path", "start_line", "end_line"],
                process ? ["tool", "path", "task"] : ["tool", "path"], out var fields) ||
                !Text(fields["tool"], 32, false, policy, out var tool) || !Text(fields["path"], 4096, false, policy, out var rawPath)) return false;
            var write = tool is "create_file" or "replace_text";
            if (process ? tool != "run_dotnet_task" : kind == PlanTaskKind.Write ? !write : tool is not ("list_files" or "read_file" or "search_files" or "search_text" or "read_file_range")) return false;
            if (!WorkspacePathRules.TryNormalize(rawPath, out var path, out _)) { error = "invalid_path"; return false; }
            if (WorkspacePathRules.IsSensitive(path)) { error = "sensitive_path"; return false; }
            if (!policy.CanSendOrUse(path, ContentBoundary.ToolArguments)) { error = "sensitive_arguments"; return false; }
            int? start = null; int? end = null;
            if (tool == "read_file_range")
            {
                if (!fields.TryGetValue("start_line", out var startValue) || !fields.TryGetValue("end_line", out var endValue) ||
                    !Integer(startValue, 1, out var from) || !Integer(endValue, 1, out var to) || from > int.MaxValue || to > int.MaxValue ||
                    from > to || to - from >= WorkspaceLimits.MaxRangeLines) return false;
                start = (int)from; end = (int)to;
            }
            else if (fields.ContainsKey("start_line") || fields.ContainsKey("end_line")) return false;
            DotnetTaskKind? task = null;
            if (process)
            {
                if (!Text(fields["task"], 5, false, policy, out var name) || name is not ("build" or "test") ||
                    !path.EndsWith(".csproj", StringComparison.Ordinal) || rawPath.EndsWith('/') ||
                    path.IndexOfAny(['*', '?', '[', ']']) >= 0) return false;
                task = name == "build" ? DotnetTaskKind.Build : DotnetTaskKind.Test;
            }
            var requirement = new TaskRequirement(tool, path, start, end, task);
            if (parsed.Any(requirement.Matches)) return false;
            parsed.Add(requirement);
        }
        requirements = parsed.AsReadOnly(); error = "none"; return true;
    }

    internal static bool Update(JsonElement value, ISensitiveDataPolicy policy, out string planId, out long revision,
        out string taskId, out PlanTaskState state, out string summary, out string error)
    {
        planId = taskId = summary = ""; revision = 0; state = PlanTaskState.Pending; error = "invalid_arguments";
        if (!SafeArguments(value, policy, out var sensitiveError)) { error = sensitiveError; return false; }
        if (!Fields(value, ["plan_id", "expected_revision", "task_id", "status", "summary"],
            ["plan_id", "expected_revision", "task_id", "status", "summary"], out var fields) ||
            !Text(fields["plan_id"], 32, false, policy, out planId) || !Integer(fields["expected_revision"], 1, out revision) ||
            !Text(fields["task_id"], 32, false, policy, out taskId) || !Text(fields["status"], 32, false, policy, out var status) ||
            !Text(fields["summary"], 2048, true, policy, out summary)) return false;
        state = status switch
        {
            "in_progress" => PlanTaskState.InProgress,
            "completed" => PlanTaskState.Completed,
            "blocked" => PlanTaskState.Blocked,
            _ => PlanTaskState.Pending
        };
        if (state == PlanTaskState.Pending) return false;
        error = "none"; return true;
    }
}