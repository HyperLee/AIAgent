using System.Globalization;
using System.Text;

using AIAgent.Cli.Core;
using AIAgent.Cli.Security;

namespace AIAgent.Cli.Cli;

internal static class ChatDraftPreview
{
    internal const int MaxBytes = 65536;
    internal const string Hint = "輸入不回顯；Enter 送出，F2 安全預覽，F3 貼上收集，F4 換行。";
    internal const string Invalid = "輸入無效或超過 16384 個 UTF-16 單位；本次未送出。";
    internal static string? Build(ChatDraftSnapshot draft, ISensitiveDataPolicy policy)
    {
        if (!ChatInputValidation.IsValid(draft.Text) || !policy.CanSendOrUse(draft.Text, ContentBoundary.ModelInput)) return null;
        var output = new StringBuilder(); var bytes = 0; var encoding = new UTF8Encoding(false, true);
        bool Append(string value)
        {
            var size = encoding.GetByteCount(value);
            if (size > MaxBytes - bytes) return false;
            bytes += size; output.Append(value); return true;
        }
        if (!Append("草稿預覽；尚未送出\n")) return null;
        var lines = draft.Text.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            if (!Append((i + 1).ToString(CultureInfo.InvariantCulture) + ": ")) return null;
            // Each source line is bounded by the draft; escapes are appended with the same byte budget.
            foreach (var rune in lines[i].EnumerateRunes())
                if (!Append(rune.Value switch { 9 => "\\t", 92 => "\\\\", _ => rune.ToString() })) return null;
            if (!Append("\n")) return null;
        }
        var line = draft.Text.AsSpan(0, draft.Cursor).Count('\n') + 1;
        var start = draft.Cursor == 0 ? 0 : draft.Text.LastIndexOf('\n', draft.Cursor - 1) + 1;
        var column = StringInfo.ParseCombiningCharacters(draft.Text[start..draft.Cursor]).Length + 1;
        if (!Append("游標：第 " + line.ToString(CultureInfo.InvariantCulture) + " 行，第 " + column.ToString(CultureInfo.InvariantCulture) + " 文字元素欄。\n" +
            "以上為預覽當時的草稿；繼續編輯不會更新此預覽。\n" + Hint + "\n你> \n")) return null;
        var block = output.ToString();
        return policy.CanSendOrUse(block, ContentBoundary.ModelInput) ? block : null;
    }
}