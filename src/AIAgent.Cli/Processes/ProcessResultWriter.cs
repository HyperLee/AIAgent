using System.Text.Json;

using AIAgent.Cli.Core;
using AIAgent.Cli.Security;
using AIAgent.Cli.Tools;

namespace AIAgent.Cli.Processes;

internal static class ProcessResultWriter
{
    internal static ToolExecutionResult Create(ToolStatus status, string error, ProcessExecutionSummary summary,
        ProcessOutputCollector? output, ISensitiveDataPolicy policy, bool stop = false)
    {
        var parts = output is null ? new[] { "", "", "", "" } : new[] { output.Tail(false), output.Tail(true), output.Head(false), output.Head(true) };
        // Checking the escaped combined fields is mandatory even when each stream was safe.
        if (output?.State is not (null or "safe")) Array.Fill(parts, "");
        var stdoutTruncated = summary.StdoutTruncated; var stderrTruncated = summary.StderrTruncated;
        byte[] Bytes() => Serialize(status, error, summary, parts[2] + parts[0], parts[3] + parts[1], stdoutTruncated, stderrTruncated);
        var bytes = Bytes();
        for (var i = 0; bytes.Length > 65536 && i < parts.Length; i++)
        {
            var original = parts[i]; if (original.Length == 0) continue;
            if (i is 0 or 2) stdoutTruncated = true; else stderrTruncated = true;
            parts[i] = ""; bytes = Bytes();
            if (bytes.Length > 65536) continue;
            var low = 0; var high = original.Length;
            while (low < high)
            {
                var middle = low + (high - low + 1) / 2;
                var boundary = middle < original.Length && char.IsLowSurrogate(original[middle]) ? middle - 1 : middle;
                parts[i] = original[..boundary];
                if (Bytes().Length <= 65536) low = middle; else high = middle - 1;
            }
            if (low < original.Length && char.IsLowSurrogate(original[low])) low--;
            parts[i] = original[..low]; bytes = Bytes();
        }
        using var document = JsonDocument.Parse(bytes);
        var response = bytes.Length <= 65536 && PlanProjection.SafeJson(bytes, policy, ContentBoundary.ToolResult)
            ? (JsonElement?)document.RootElement : null;
        return new(status, error, response, stopAfterOperation: stop || status != ToolStatus.Success,
            processSummary: summary, processCleanupFailed: summary.CleanupFailed);
    }
    private static byte[] Serialize(ToolStatus status, string error, ProcessExecutionSummary summary,
        string stdout, string stderr, bool stdoutTruncated, bool stderrTruncated)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject(); writer.WriteString("status", PlanProjection.Name(status)); writer.WriteString("error_code", error);
            writer.WriteStartObject("data"); writer.WriteString("task", PlanProjection.Name(summary.Task)); writer.WriteString("project_path", summary.ProjectPath);
            writer.WriteString("start_state", PlanProjection.Name(summary.StartState)); writer.WriteString("end_state", PlanProjection.Name(summary.EndState));
            if (summary.ExitCode is { } exit) writer.WriteNumber("exit_code", exit); else writer.WriteNull("exit_code");
            writer.WriteNumber("elapsed_ms", summary.ElapsedMilliseconds); writer.WriteBoolean("cleanup_confirmed", summary.CleanupConfirmed);
            writer.WriteString("validation", PlanProjection.Name(summary.ValidationOutcome));
            if (summary.Tests is { } tests)
            {
                writer.WriteStartObject("tests"); writer.WriteNumber("total", tests.Total); writer.WriteNumber("passed", tests.Passed);
                writer.WriteNumber("failed", tests.Failed); writer.WriteNumber("skipped", tests.Skipped); writer.WriteEndObject();
            }
            else writer.WriteNull("tests");
            writer.WriteString("stdout", stdout); writer.WriteString("stderr", stderr);
            writer.WriteNumber("stdout_bytes", summary.StdoutBytes); writer.WriteNumber("stderr_bytes", summary.StderrBytes);
            writer.WriteBoolean("stdout_truncated", stdoutTruncated); writer.WriteBoolean("stderr_truncated", stderrTruncated);
            writer.WriteString("output_state", summary.OutputState); writer.WriteEndObject(); writer.WriteEndObject();
        }
        return stream.ToArray();
    }
}