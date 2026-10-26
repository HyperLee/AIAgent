using System.Text.Json;

namespace AIAgent.Cli.Core;

internal static class WorkHistoryProjection
{
    internal static byte[] Memory(ContextSummary? summary, IReadOnlyList<ContextPin> pins, WorkSavedPlan? history, string? workId)
    {
        var memory = ContextDocuments.Memory(summary, pins);
        if (history is null) return memory;
        using var parsed = JsonDocument.Parse(memory);
        return ContextDocuments.Write(ContextDocuments.MaxMemoryBytes, writer =>
        {
            writer.WriteStartObject();
            foreach (var field in parsed.RootElement.EnumerateObject()) field.WriteTo(writer);
            writer.WriteStartObject("saved_work_plan"); writer.WriteString("source", "saved_work_plan");
            writer.WriteString("work_id", workId); writer.WriteBoolean("untrusted_history", true);
            writer.WritePropertyName("reported_plan"); WorkSaveCodec.WriteSavedPlan(writer, history);
            writer.WriteEndObject(); writer.WriteEndObject();
        });
    }
}