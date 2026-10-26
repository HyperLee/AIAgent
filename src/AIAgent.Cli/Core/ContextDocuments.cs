using System.Text;
using System.Text.Json;

using AIAgent.Cli.Security;

namespace AIAgent.Cli.Core;

internal static class ContextDocuments
{
    internal const int MaxPinsBytes = 8192;
    internal const int MaxMemoryBytes = 32768;
    internal const int MaxSummaryBytes = 16384;
    internal const int MaxSourceBytes = 131072;
    internal const int MaxDisplayBytes = 65536;
    internal const int AutomaticThreshold = 196608;
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    internal static bool ValidText(string text)
    {
        if (text.Length is < 1 or > 1024 || string.IsNullOrWhiteSpace(text)) return false;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (char.IsControl(c) || c is '\u2028' or '\u2029') return false;
            if (char.IsHighSurrogate(c)) { if (++i >= text.Length || !char.IsLowSurrogate(text[i])) return false; }
            else if (char.IsLowSurrogate(c)) return false;
        }
        return true;
    }
    internal static byte[] Write(int limit, Action<Utf8JsonWriter> write)
    {
        using var stream = new BoundedStream(limit);
        using (var writer = new Utf8JsonWriter(stream)) { write(writer); writer.Flush(); }
        return stream.ToArray();
    }
    internal static bool Safe(byte[] bytes, ISensitiveDataPolicy policy)
    {
        using var json = JsonDocument.Parse(bytes);
        return policy.CanSendOrUse(StrictUtf8.GetString(bytes), ContentBoundary.ModelInput) &&
            !SensitiveJson.Contains(json.RootElement, policy, ContentBoundary.ModelInput);
    }
    internal static byte[] Pins(IReadOnlyList<ContextPin> pins) => Write(MaxPinsBytes, w =>
    { w.WriteStartObject(); WritePins(w, pins); w.WriteEndObject(); });
    private static void WritePins(Utf8JsonWriter w, IReadOnlyList<ContextPin> pins)
    {
        w.WriteStartArray("pins");
        foreach (var pin in pins) { w.WriteStartObject(); w.WriteString("id", pin.PinId); w.WriteString("text", pin.Text); if (pin.SourceWorkId is { } workId) { w.WriteString("source", "saved_work_pin"); w.WriteString("work_id", workId); w.WriteBoolean("untrusted_history", true); } w.WriteEndObject(); }
        w.WriteEndArray();
    }
    internal static void WriteSummary(Utf8JsonWriter w, ContextSummary summary)
    {
        w.WriteStartObject(); w.WriteNumber("version", 1);
        void Items(string name, IReadOnlyList<string> items)
        { w.WriteStartArray(name); foreach (var text in items) w.WriteStringValue(text); w.WriteEndArray(); }
        Items("goals", summary.Goals); Items("decisions", summary.Decisions);
        Items("open_questions", summary.OpenQuestions); Items("background", summary.Background); w.WriteEndObject();
    }
    internal static byte[] Memory(ContextSummary? summary, IReadOnlyList<ContextPin> pins) => Write(MaxMemoryBytes, w =>
    {
        w.WriteStartObject(); w.WriteNumber("version", 1); w.WritePropertyName("summary");
        if (summary is null) w.WriteNullValue();
        else
        {
            w.WriteStartObject(); w.WriteString("source", summary.SourceWorkId is null ? "model_summary" : "saved_work_summary");
            if (summary.SourceWorkId is { } workId) { w.WriteString("work_id", workId); w.WriteBoolean("untrusted_history", true); }
            if (summary.SourceWorkId is null || summary.CoveredTurnCount > 0) { w.WriteNumber("through_turn_id", summary.ThroughTurnId); w.WriteNumber("covered_turn_count", summary.CoveredTurnCount); }
            w.WritePropertyName("content"); WriteSummary(w, summary); w.WriteEndObject();
        }
        WritePins(w, pins); w.WriteEndObject();
    });
    internal static byte[] Source(ContextSummary? previous, IReadOnlyList<ContextTurnProjection> turns) => Write(MaxSourceBytes, w =>
    {
        w.WriteStartObject(); w.WriteNumber("version", 1); w.WritePropertyName("previous_summary");
        if (previous is null) w.WriteNullValue(); else WriteSummary(w, previous);
        w.WriteStartArray("turns");
        foreach (var turn in turns)
        {
            w.WriteStartObject(); w.WriteNumber("turn_id", turn.TurnId); w.WriteString("user_text", turn.UserText);
            w.WriteString("assistant_text", turn.AssistantText); w.WriteStartArray("tools");
            foreach (var tool in turn.Tools) tool.WriteTo(w);
            w.WriteEndArray(); w.WriteEndObject();
        }
        w.WriteEndArray(); w.WriteEndObject();
    });
    internal static ContextSummary? ParseSummary(string text, long throughTurnId, long covered, ISensitiveDataPolicy policy)
    {
        try
        {
            if (!policy.CanSendOrUse(text, ContentBoundary.ModelInput)) throw new ContextInspectionException();
            if (StrictUtf8.GetByteCount(text) > MaxSummaryBytes) return null;
            using var document = JsonDocument.Parse(text);
            var root = document.RootElement;
            if (SensitiveJson.Contains(root, policy, ContentBoundary.ModelInput)) throw new ContextInspectionException();
            if (root.ValueKind != JsonValueKind.Object) return null;
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in root.EnumerateObject()) if (!names.Add(property.Name)) return null;
            if (!names.SetEquals(["version", "goals", "decisions", "open_questions", "background"]) ||
                root.GetProperty("version").GetRawText() != "1") return null;
            var lists = new List<string[]>();
            foreach (var name in new[] { "goals", "decisions", "open_questions", "background" })
            {
                var array = root.GetProperty(name);
                if (array.ValueKind != JsonValueKind.Array || array.GetArrayLength() > 16) return null;
                var values = new List<string>();
                foreach (var item in array.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.String || item.GetString() is not { } value) return null;
                    if (!policy.CanSendOrUse(value, ContentBoundary.ModelInput)) throw new ContextInspectionException();
                    if (!ValidText(value)) return null;
                    values.Add(value);
                }
                lists.Add(values.ToArray());
            }
            if (lists.All(items => items.Length == 0)) return null;
            var summary = new ContextSummary(throughTurnId, covered, lists[0], lists[1], lists[2], lists[3]);
            var canonical = Write(MaxSummaryBytes, w => WriteSummary(w, summary));
            if (!Safe(canonical, policy)) throw new ContextInspectionException();
            return summary;
        }
        catch (Exception ex) when (ex is JsonException or EncoderFallbackException or ContextCapacityException or InvalidOperationException or ArgumentException) { return null; }
    }
    private sealed class BoundedStream(int limit) : MemoryStream
    {
        public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));
        public override void Write(ReadOnlySpan<byte> buffer)
        {
            if (buffer.Length > limit - Length) throw new ContextCapacityException();
            var end = checked(Position + buffer.Length);
            if (end > Length) SetLength(end);
            buffer.CopyTo(GetBuffer().AsSpan((int)Position, buffer.Length)); Position = end;
        }
    }
}