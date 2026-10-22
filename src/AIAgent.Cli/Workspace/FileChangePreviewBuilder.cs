using System.Globalization;
using System.Text;

using AIAgent.Cli.Core;
using AIAgent.Cli.Security;

namespace AIAgent.Cli.Workspace;

/// <summary>有界、線性的單一 hunk；投影僅供顯示，不是可執行 patch。</summary>
internal static class FileChangePreviewBuilder
{
    internal const string Prompt = "套用此變更？[y/N] ";
    internal static bool TryCreate(FileChangeKind kind, string path, byte[] original, byte[] candidate,
        ISensitiveDataPolicy policy, CancellationToken token, out FileChangePreview? preview, out WorkspaceErrorCode error)
    {
        preview = null; error = WorkspaceErrorCode.None;
        try
        {
            var oldText = Decode(original, out var oldBom);
            var newText = Decode(candidate, out var newBom);
            if (!policy.CanSendOrUse(oldText, ContentBoundary.WriteCandidate) || !policy.CanSendOrUse(newText, ContentBoundary.WriteCandidate) ||
                !policy.CanSendOrUse(path, ContentBoundary.ToolArguments)) { error = WorkspaceErrorCode.SensitiveContent; return false; }
            var output = new BoundedText(token);
            var quoted = "\"" + Escape(path, true) + "\"";
            output.Append("[預覽] " + (kind == FileChangeKind.Create ? "建立檔案" : "精確替換") + "；目標：" + quoted + "\n");
            output.Append("原始：" + original.Length.ToString(CultureInfo.InvariantCulture) + " bytes；UTF-8 BOM：" + (oldBom ? "有" : "無") + "\n");
            output.Append("候選：" + candidate.Length.ToString(CultureInfo.InvariantCulture) + " bytes；UTF-8 BOM：" + (newBom ? "有" : "無") + "\n");
            output.Append(kind == FileChangeKind.Create ? "--- /dev/null\n" : "--- " + quoted + "（原始）\n");
            output.Append("+++ " + quoted + "（預計）\n");
            var oldLines = Lines(oldText, token); var newLines = Lines(newText, token);
            var prefix = 0;
            while (prefix < oldLines.Count && prefix < newLines.Count && oldLines[prefix] == newLines[prefix]) { token.ThrowIfCancellationRequested(); prefix++; }
            var suffix = 0;
            while (suffix < oldLines.Count - prefix && suffix < newLines.Count - prefix && oldLines[^(suffix + 1)] == newLines[^(suffix + 1)])
            { token.ThrowIfCancellationRequested(); suffix++; }
            var before = Math.Min(prefix, WorkspaceLimits.PreviewContextLines); var after = Math.Min(suffix, WorkspaceLimits.PreviewContextLines);
            var start = prefix - before;
            var oldCount = before + oldLines.Count - prefix - suffix + after;
            var newCount = before + newLines.Count - prefix - suffix + after;
            if (kind == FileChangeKind.Create && newLines.Count == 0) output.Append("建立空文字檔\n");
            else
            {
                output.Append(FormattableString.Invariant($"@@ -{(oldCount == 0 ? start : start + 1)},{oldCount} +{(newCount == 0 ? start : start + 1)},{newCount} @@\n"));
                for (var i = start; i < prefix; i++) AddLine(output, ' ', oldLines[i]);
                for (var i = prefix; i < oldLines.Count - suffix; i++) AddLine(output, '-', oldLines[i]);
                for (var i = prefix; i < newLines.Count - suffix; i++) AddLine(output, '+', newLines[i]);
                for (var i = 0; i < after; i++) AddLine(output, ' ', oldLines[oldLines.Count - suffix + i]);
            }
            output.Append(Prompt);
            var block = output.ToString();
            if (!policy.CanSendOrUse(block, ContentBoundary.WriteCandidate)) { error = WorkspaceErrorCode.SensitiveContent; return false; }
            preview = new(kind, path, original.LongLength, candidate.LongLength, oldBom, newBom, block, output.ByteCount);
            return true;
        }
        catch (PreviewLimitException) { error = WorkspaceErrorCode.PreviewTooLarge; return false; }
    }

    internal static string Decode(byte[] bytes, out bool bom)
    {
        bom = bytes.AsSpan().StartsWith(new byte[] { 239, 187, 191 });
        return new UTF8Encoding(false, true).GetString(bytes, bom ? 3 : 0, bytes.Length - (bom ? 3 : 0));
    }
    private static List<Line> Lines(string text, CancellationToken token)
    {
        var result = new List<Line>(); var start = 0;
        for (var i = 0; i < text.Length; i++)
        {
            token.ThrowIfCancellationRequested();
            if (text[i] is not ('\r' or '\n')) continue;
            var end = i; var ending = text[i] == '\r' ? "CR" : "LF";
            if (text[i] == '\r' && i + 1 < text.Length && text[i + 1] == '\n') { ending = "CRLF"; i++; }
            result.Add(new(text[start..end], ending)); start = i + 1;
        }
        if (start < text.Length) result.Add(new(text[start..], "EOF"));
        return result;
    }
    private static void AddLine(BoundedText output, char prefix, Line line)
    {
        output.Append(prefix.ToString());
        foreach (var rune in line.Text.EnumerateRunes()) output.Append(EscapeRune(rune, false));
        output.Append(" [" + line.Ending + "]\n");
    }
    internal static string EscapePath(string path) => Escape(path, true);
    private static string Escape(string text, bool path)
    {
        var result = new StringBuilder();
        foreach (var rune in text.EnumerateRunes()) result.Append(EscapeRune(rune, path));
        return result.ToString();
    }
    private static string EscapeRune(Rune rune, bool path) => rune.Value switch
    {
        '\\' => "\\\\",
        '\t' => "\\t",
        '[' => "\\u{005B}",
        ']' => "\\u{005D}",
        '"' when path => "\\\"",
        _ => Rune.GetUnicodeCategory(rune) is UnicodeCategory.Format or UnicodeCategory.LineSeparator or UnicodeCategory.ParagraphSeparator
            ? "\\u{" + rune.Value.ToString("X4", CultureInfo.InvariantCulture) + "}" : rune.ToString()
    };
    private readonly record struct Line(string Text, string Ending);
    private sealed class PreviewLimitException : Exception;
    private sealed class BoundedText(CancellationToken token)
    {
        private readonly StringBuilder _text = new();
        internal int ByteCount { get; private set; }
        internal void Append(string text)
        {
            token.ThrowIfCancellationRequested();
            var bytes = Encoding.UTF8.GetByteCount(text);
            if (ByteCount + bytes > WorkspaceLimits.MaxPreviewBytes) throw new PreviewLimitException();
            _text.Append(text); ByteCount += bytes;
        }
        public override string ToString() => _text.ToString();
    }
}