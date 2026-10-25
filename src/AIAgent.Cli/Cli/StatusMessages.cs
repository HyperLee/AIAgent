using AIAgent.Cli.Core;

namespace AIAgent.Cli.Cli;

internal static class StatusMessages
{
    internal const string Usage = "用法：AIAgent.Cli [--workspace <path>] | --help\n聊天內 /help 查看操作；/context、/compact、/budget 管理摘要、原文備註及生成額度（預設回合5／程序100）；摘要會消耗生成及API用量，reset不退款。受控 Debug 建置須逐次核准。SDK 10.0.100 的 test 因私人 IPC 限制不支援。";
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