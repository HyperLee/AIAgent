using AIAgent.Cli.Core;

namespace AIAgent.Cli.Cli;

internal static class StatusMessages
{
    internal const string Usage = "用法：AIAgent.Cli [--workspace <path>] | --help\n聊天內 /help 查看操作；確認與 Key 就緒後自動載入工作區 AGENTS.md，規則可能送至 Gemini。/rules 檢視來源，/rules scope DIR 明確切換範圍，變動後 /rules reload 並重新確認。/context、/compact、/budget 管理摘要、原文備註及生成額度（預設回合5／程序100）；摘要會消耗生成及API用量，reset不退款。/review [all|staged|unstaged|untracked] 進行目前 Scope 的唯讀 Git 審查，差異可能送 Gemini 並消耗共用生成額度；/review show 零生成。採原始 bytes 視角並明示排除，審查不執行建置或測試。受控 Debug 建置須逐次核准。SDK 10.0.100 的 test 因私人 IPC 限制不支援。一般輸入不回顯；Enter送出、F2安全預覽、F3明確貼上收集、F4換行；多行貼上須先F3，完成後F3再另按Enter。本機指令只接受單行；Ctrl+P／N回取程序內安全歷史，Ctrl+U清空草稿。";
    internal static string For(BootstrapResult result) => result.Status switch
    {
        BootstrapStatus.Ready => "啟動檢查完成；聊天功能尚未接入",
        BootstrapStatus.Help => Usage,
        BootstrapStatus.Declined => "未使用此工作區；啟動已結束。",
        BootstrapStatus.EndOfInput => "輸入結束；啟動已結束。",
        BootstrapStatus.Cancelled => "啟動已取消。",
        _ => result.ErrorCode switch
        {
            BootstrapErrorCode.InvalidArguments => "啟動參數錯誤；請檢查參數名稱、數量及工作區值。" + Environment.NewLine + Usage,
            BootstrapErrorCode.InvalidSettings => "應用程式設定無效或無法讀取。",
            BootstrapErrorCode.WorkspaceUnavailable => "工作區不是可存取的既有目錄，或路徑無法解析。",
            BootstrapErrorCode.TerminalUnavailable => "需要支援無回顯輸入的互動終端機；請勿重新導向輸入或輸出。",
            _ => "啟動遇到輸入輸出問題，已停止。"
        }
    };
}