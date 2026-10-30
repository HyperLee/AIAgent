using AIAgent.Cli.Core;

namespace AIAgent.Cli.Cli;

internal static class StatusMessages
{
    internal const string Usage = "用法：AIAgent.Cli [--workspace <path>] [--resume <32位小寫十六進位ID>] | --help\n聊天內 /help 查看操作；/agents、/agents on、/agents off 管理同模型唯讀分工，最多兩子，共同生成成本，Ctrl+C 取消整批，清除或來源失效停用；/model、/model list 零網路顯示產品支援模型，/model use PROVIDER MODEL 經完整清除預覽、新確認與隱藏新 Key 後切換並進入 Plan；切換不復原檔案、不刪私人存檔、不清程序消耗。/mcp、/mcp tools 零網路查詢；/mcp connect learn 僅連 Microsoft Learn 的兩項搜尋與一項文件擷取，/mcp reload 明確重載、/mcp disconnect 停用。fetch 僅接受原樣 ASCII Learn 文章 URL，無 fragment／百分比／結尾斜線，query 限 view 及搭配 view 的 preserve-view=true；來源可能過時，版本與原站狀態未由本機驗證。每筆完整外送預覽後須新隱藏確認；目錄 Stale 時須重載或停用，clear／換 Key／續接不自動連線。/skills、list、show ID、use ID、reload、off 管理目前 Scope 下 skills/<id>/SKILL.md；手動單一選用，檢視不會啟用、啟用不會執行，內容可能送至目前供應商，來源變動須重載或停用並重新確認。clear／換 Key／退出會丟棄技能；/checkpoint list、show cN、restore cN、forget cN 管理程序內單檔修改歷史；restore 限 Execute／有效規則，完整新預覽後另確認。clear／換 Key／退出丟資料，容量滿停止新寫入；新建檔只交人工，不復原 index／程序副作用或全部 metadata。/work save TITLE、list、show ID、delete ID 管理私人目錄明文摘要；--resume ID 重啟續接後進入 Plan，不恢復授權或自動執行。只有 save 可能生成一次摘要並消耗 API 用量，確認後才保存。確認與 Key 就緒後自動載入工作區 AGENTS.md，規則可能送至目前供應商。/rules 檢視來源，/rules scope DIR 明確切換範圍，變動後 /rules reload 並重新確認。/context、/compact、/budget 管理摘要、原文備註及生成額度（預設回合5／程序100）；摘要會消耗生成及API用量，reset不退款。/review [all|staged|unstaged|untracked] 進行目前 Scope 的唯讀 Git 審查，差異可能送目前供應商 並消耗共用生成額度；/review show 零生成。採原始 bytes 視角並明示排除，審查不執行建置或測試。受控 Debug 建置須逐次核准。SDK 10.0.100 的 test 因私人 IPC 限制不支援。一般輸入不回顯；Enter送出、F2安全預覽、F3明確貼上收集、F4換行；多行貼上須先F3，完成後F3再另按Enter。本機指令只接受單行；Ctrl+P／N回取程序內安全歷史，Ctrl+U清空草稿。";
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