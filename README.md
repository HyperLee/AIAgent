# AIAgent

以 .NET 10 Console 分階段開發的 Gemini Agent。目前已實作子規格 01～06、08 的產品功能，以及第 07 份的整合測試、提交／產物掃描器與選用 hook。具備啟動與隱藏 Key、Gemini 用戶端、最近 5 個完整回合、用量累計、受限檔案工具、遞迴檔名／文字搜尋、指定行範圍讀取、單檔建立／精確替換及逐次核准；每回合最多五次生成。

離線自動測試、macOS PTY 與 VS Code F5 偵錯已通過；第 07 份本機 Release 掃描仍有 BUD-S003 阻擋，真人終端機、真實 Gemini 與人工分發檢查尚未驗證，第三階段整體驗收尚未完成。日期、基線與證據範圍見[最新驗證摘要](#latest-validation)。

開發依據為 [總規格](SPEC.md)、[01：啟動與 API Key](specs/01-bootstrap-and-key.md)、[02：Gemini 用戶端](specs/02-gemini-client.md)、[03：CLI 與 Context](specs/03-cli-and-context.md)、[04：工作區與唯讀工具](specs/04-workspace-and-read-tools.md)、[05：Agent 工具循環](specs/05-agent-loop.md)、[06：檔案編輯與核准](specs/06-file-edit-and-approval.md)、[07：預算與整體驗收](specs/07-budget-and-acceptance.md) 與 [08：程式碼搜尋與局部讀檔](specs/08-code-search-and-partial-read.md)。規格中的「本次只新增文件」描述撰寫當時的交付；目前已依開發要求實作，既有規格保持原文。

## 開發進度

| 子規格 | 能力 | 功能實作 | 自動驗證 | 整體驗收待辦 |
|---|---|---|---|---|
| 01：啟動與 API Key | 啟動參數、工作區確認、隱藏 Key 與非機密設定。 | 已實作。 | 離線與 PTY 回歸通過。 | 真人終端機。 |
| 02：Gemini 用戶端 | 模型契約、SDK 轉接、請求限制、錯誤分類與資料保護。 | 已實作。 | 真實 SDK 配假 HTTP handler 回歸通過。 | 真實 Gemini。 |
| 03：CLI 與 Context | 文字聊天、指令、5 回合歷史、取消、換 Key 與用量累計。 | 已實作。 | 離線與 PTY 回歸通過；F5 基本操作通過。 | 真人終端機與真實 Gemini。 |
| 04：工作區與唯讀工具 | 工作區邊界、第一層列檔、完整 UTF-8 讀取、結果限量、快照及清除串接。 | 已實作並接入正式聊天。 | 離線與工具整合回歸通過。 | 真實 Gemini 列檔／讀檔。 |
| 05：工具循環 | 工具 Schema、登錄、派送、配對與每回合 5 次生成上限。 | 已實作。 | 離線與 PTY 回歸通過。 | 真人終端機與真實 Gemini 工具往返。 |
| 06：檔案編輯與核准 | 建立、唯一精確替換、本機差異、逐筆核准、衝突、發布與讀回核對。 | 已實作。 | 離線與 PTY 回歸通過。 | 真人與真實 Gemini 寫入／核准。 |
| 07：整體驗收 | 限制／用量整合、Git index／發布目錄掃描、選用 hook 與人工清單。 | 整合測試、掃描器及選用 hook 已實作。 | MSTest、Python 與 PTY 回歸通過。 | Release 掃描受阻；真人／服務／人工分發檢查未完成。 |
| 08：程式碼搜尋與局部讀檔 | 安全檔名 glob、跨檔字面搜尋及指定行範圍讀取。 | 已實作並接入七工具。 | 證據見第 08 份驗證紀錄。 | 真人終端機與真實 Gemini；第 07 份門檻保留。 |

子規格 01～03 對應總規格第一階段「可以聊天」，04～05 對應第二階段「可以讀檔」，06～07 對應第三階段「可以修改」。目前正式依序提供 `list_files`、`read_file`、`create_file`、`replace_text`、`search_files`、`search_text`、`read_file_range`；每筆寫入須預覽後明確核准，不提供刪除檔案。第 07 份不新增模型工具或產品參數；上表的自動驗證不等於各階段所有人工／服務／發布門檻均已通過。

<a id="latest-validation"></a>

## 第 08 份程式碼搜尋與局部讀檔

第 08 份實作範圍為 FUT-01，與既有四工具共用同一工作區服務、敏感政策、取消世代及單一進行中操作限制。只接受原生工具呼叫，不新增本機搜尋指令、外部程序、套件或索引。

| 工具 | 參數與用途 |
|---|---|
| `search_files` | 必填 `path`、`pattern`；遞迴搜尋安全普通檔案名稱，不讀內容，沒有單檔大小或編碼限制。 |
| `search_text` | 必填 `path`、`query`；`file_pattern`、`case_sensitive`、`context_lines` 省略時為 `**/*`、true、2；附近行數可為 0～3。 |
| `read_file_range` | 必填 `path`、`start_line`、`end_line`；行號從 1 開始，請求跨度最多 200 行；終點可夾限至 EOF。 |

搜尋起點必須是目錄，根目錄用 `.`；範圍讀取目標必須是普通檔案。glob 的 `*`／`?` 以 Unicode scalar 匹配同區段，整段 `**` 匹配零個以上路徑區段；沒有 `/` 的樣式匹配各深度 basename，有 `/` 時錨定搜尋起點。比對區分大小寫，其他合法字元按字面處理；不支援正規式、brace 或字元集合。query 是 1～1024 UTF-16 單位的單行字面文字，可含空白與 Tab，不做 Unicode 正規化。

搜尋固定略過 `bin`、`obj`、`node_modules`、`.vs`、`.idea`、`dist`、`coverage`、`TestResults`、`logs`、`.cache` 子目錄，名稱採 OrdinalIgnoreCase；明確指定為起點時允許搜尋其內容。同名普通檔案不略過，也不解析 `.gitignore`。原敏感路徑與連結限制仍優先適用。

每次搜尋最多取得 8192 個項目、深度 32、保留排序前 200 筆；文字搜尋另限 512 個候選及 8388608 實際讀取 bytes。達工作量上限保守標示截斷，不為證明完整取得下一項。文字搜尋與範圍讀取只支援最多 131072 原始 bytes 的 UTF-8 文字，均先完整檢查內容；文字搜尋只略過過大、非法 UTF-8 與二進位候選。其他 I/O、路徑變動或敏感內容使整次無資料。

三工具的完整 JSON 結果最多 65536 bytes。搜尋容量超限會移除排序尾端的完整命中，可能回傳「空集合但已截斷」；範圍結果超限直接拒絕，不切短行。範圍內容保留原始換行、不含起始 BOM；片段 bytes、整檔 bytes 及 JSON bytes 分別計算。CLI 只顯示安全路徑、計數、範圍及片段 bytes，不顯示 query、pattern 或命中原文。

**搜尋或範圍讀取不建立修改快照。** `search_text` 開始處理候選、`read_file_range` 開始合法讀取時，會移除該檔舊快照；要修改仍須重新完整 `read_file`，再預覽及逐次核准。每回合五次生成與 262144 bytes 實際模型請求上限不變；結果已在本機取得不代表 Gemini 已收到。

### 第 08 份驗證紀錄（2026-10-23）

本次以 HEAD `a50b4a0` 加上尚未提交的工作區增量驗證；平台為 macOS 26.7.1 arm64、.NET SDK 10.0.100、Python 3.9.6。只使用合成 Key、可控制檔案系統、隔離真實檔案與假 HTTP handler，測試工作區保留，不遞迴清理。本次新增 41 個 MSTest，連同原有 289 個共 330 個；下表均為本次實際結果。

| 命令 | 退出碼與結果 |
|---|---|
| `dotnet restore tests/AIAgent.Cli.Tests/AIAgent.Cli.Tests.csproj` | 0；固定依賴還原成功。 |
| `dotnet build tests/AIAgent.Cli.Tests/AIAgent.Cli.Tests.csproj --no-restore` | 0；產品、TerminalHost、測試建置成功，0 警告／0 錯誤。 |
| `dotnet test --project tests/AIAgent.Cli.Tests/AIAgent.Cli.Tests.csproj --no-build` | 0；330 通過、0 失敗、0 略過，含既有 01～07 回歸。 |
| `python3 -B -m unittest discover -s tests -p 'test_sensitive_content.py'` | 0；33 通過、0 失敗、0 略過。 |
| `dotnet run --project src/AIAgent.Cli/AIAgent.Cli.csproj --no-build -- --help` | 0；只顯示用法，無認證或生成。 |
| `dotnet format src/AIAgent.Cli/AIAgent.Cli.csproj --no-restore --verify-no-changes` | 0；審查修正的換行格式修正後重新檢查通過。 |
| `python3 -B tests/terminal_smoke.py` | 0；50 組通過，含原 47 組與 3 組 SEARCH-AC-033，不是真人操作。 |
| `git diff --check`；另逐檔檢查 12 個未追蹤檔案 | 0；空白、C# CRLF／無末尾換行及本機文件連結查核通過。 |

獨立唯讀程式審查找出 1 項重要競態：已觀察父目錄在再次定位時消失／變連結／變普通檔案，不得回報為初次查找錯誤而保留快照。新增文字搜尋與範圍讀取的回歸測試先重現失敗，補強失敗路徑重驗後通過；最終整套 330 個測試通過。沒有未處理的審查項目。

| 案例 | 自動驗證內容 |
|---|---|
| SEARCH-AC-001～003、030～031 | 七工具及嚴格 Schema、所有權與摘要、參數拒絕、短 Key、CLI 摘要。 |
| SEARCH-AC-004～009、016～021 | 路徑、敏感／忽略／連結、Unicode glob 與文字、完整 UTF-8／BOM、分行、附近行及範圍 bytes。 |
| SEARCH-AC-010～015 | 列舉／候選／讀取／深度／結果筆數上限及完整 JSON 65535／65536／65537 bytes。 |
| SEARCH-AC-022～027、032 | 無命中及格式排除候選重驗、根／目錄交換、快照、取消／Clear／並行／Dispose、來源 bytes 不變及既有回歸。 |
| SEARCH-AC-028～029 | 真實 ChatApplication／SDK 配假 handler、多工具原序、簽章、第 5 次工具全不執行及含歷史／七工具／多結果／既有寫入的實際 body 262144／262145 bytes。 |
| SEARCH-AC-033 | 離線 PTY 的七工具 help、三種摘要、busy 輸入丟棄、Ctrl+C 繼續聊天及 Ctrl+D 等待收尾。 |
| SEARCH-AC-034～035 | 真人終端機與真實 Gemini **未執行**；不以自動測試替代。 |

第 07 份 Release 的 BUD-S003 阻擋，以及真人／服務／人工分發與 x64 待辦仍保留；本次未 publish、提交或推送，未啟用或放寬 hook／掃描器。

<a id="stage07-validation"></a>

## 第 07 份既有驗證摘要（2026-10-23）

以下引用本對話於 2026-10-23 先前實際執行的查核及偵錯設定驗收；本次 README 維護只核對文件，沒有重新執行整套產品驗收。平台為 macOS arm64、.NET SDK 10.0.100、Python 3.9.6。離線產品與 Release 查核基線為 `5fc252c`；F5 驗收使用該基線加上兩份偵錯設定，其內容已提交為 `4df5214`。下表的 index 掃描在 `4df5214` 執行。

| 證據層級 | 命令／情境 | 該次實際結果 |
|---|---|---|
| 離線建置 | 測試專案的 restore／build，連同產品與 TerminalHost。 | 退出 0；0 警告、0 錯誤。還原使用暫存 NuGet 套件與 HTTP 快取，只是工具環境設定。 |
| 離線產品 | `dotnet test --project tests/AIAgent.Cli.Tests/AIAgent.Cli.Tests.csproj --no-build` | 退出 0；289 通過、0 失敗、0 略過。 |
| 離線掃描測試 | `python3 -B -m unittest discover -s tests -p 'test_sensitive_content.py'` | 退出 0；33 通過、0 失敗、0 略過。 |
| 自動終端機 | `python3 -B tests/terminal_smoke.py` | 退出 0；47 組 PTY 通過，不是真人操作。 |
| Debug 啟動用法 | `dotnet run --project src/AIAgent.Cli/AIAgent.Cli.csproj --no-build -- --help` | 退出 0；未提示 Key、未呼叫模型。 |
| 格式／空白 | `dotnet format src/AIAgent.Cli/AIAgent.Cli.csproj --no-restore --verify-no-changes` 與 `git diff --check` | 均退出 0。 |
| VS Code GUI 自動操作 | F5 執行 `build AIAgent.Cli`，命中 `Program.cs` 第 11 行中斷點；在整合終端機確認根工作區並隱藏輸入假 Key，執行 `/help`、`/clear`、`/exit`。 | 建置成功；假 Key 無回顯；指令結果正確，退出碼 0 且終端機回顯恢復。暫時中斷點已移除，未呼叫 Gemini。 |
| Git index | `python3 -B scripts/check_sensitive_content.py staged` | 退出 0；130 檔、0 命中。只覆蓋該次 index，不含本次尚未暫存的 README 修訂。 |
| Release 產物 | 以全新隔離目錄執行 framework-dependent、非單檔的 Release publish，再執行 artifacts 掃描。 | publish 退出 0；掃描退出 1／blocked，14 檔、1 命中，item=000005、BUD-S003。 |
| Release 啟動用法 | 同一產物的 `AIAgent.Cli.dll --help`。 | 未執行；掃描非零後依 BUD-070 停止該次流程，BUD-AC-043 未通過。 |
| 真人／真實服務／分發 | BUD-AC-046～049；x64 平台。 | 真人終端機、真實 Gemini、人工提交／分享／分發檢查及 x64 均未驗證；第三階段整體驗收未完成。 |

Release 命中位於相依套件 `Google.Apis.Auth.dll` 的 UTF-16 私鑰起始標記字面值；固定外形規則命中不等於已確認存在真實或可用的私鑰。目前規格沒有相依套件豁免，因此仍停止分發，不略過該 DLL 或調寬規則。上述 F5 證據由 GUI 工具自動操作取得，不替代真人輸入法、完整差異／核准或真實 Gemini 驗收。

## 建置與啟動

需要 .NET SDK 10.0.100（`global.json` 允許同系列修補版本）。產品使用 `net10.0`，固定引用 `Google.GenAI 1.26.1`；測試沿用 `MSTest.Sdk/4.5.1` 與 Microsoft.Testing.Platform。產品、MSTest 與終端機測試 host 皆啟用可空值分析及警告視為錯誤。

從儲存庫根目錄依序執行：

```sh
dotnet restore tests/AIAgent.Cli.Tests/AIAgent.Cli.Tests.csproj
dotnet build tests/AIAgent.Cli.Tests/AIAgent.Cli.Tests.csproj --no-restore
dotnet test --project tests/AIAgent.Cli.Tests/AIAgent.Cli.Tests.csproj --no-build
dotnet run --project src/AIAgent.Cli/AIAgent.Cli.csproj --no-build -- --help
```

目前支援 `--help`、`--workspace <path>`，未指定工作區時採啟動目錄。正常啟動需使用互動終端機：

```sh
dotnet run --project src/AIAgent.Cli/AIAgent.Cli.csproj --no-build
```

程式讀取應用程式輸出目錄的 `appsettings.json`，確認工作區後，以無回顯方式讀取 Key，在記憶體中建立 session 與模型用戶端，再進入聊天。只有一般訊息會啟動回合，模型可直接回答或透過工具續接，最多五次生成；啟動、建構、指令與換 Key 都不發出驗證生成，Key 是否有效由正常請求的服務端結果反映。

設定只包含非機密模型名稱：

```json
{
  "Gemini": {
    "Model": "gemini-3.5-flash-lite"
  }
}
```

模型名稱先去除首尾空白，限定 1～128 個 ASCII 小寫英文字母、數字、`.`、`-`、`_`，首字元須為小寫英文字母或數字；控制字元一律拒絕。`null`、未知或重複設定欄位、錯誤型別及格式均在工作區確認與 Key 輸入前失敗，退出碼為 2。`--help` 不讀設定。

Key 僅由隱藏輸入取得，不接受參數、環境變數、設定檔或工作區檔案來源，不持久化或顯示。請勿把真實 Key 放入程式、文件、測試或命令列。

<a id="vscode-debugging"></a>

## VS Code 偵錯

以 VS Code 開啟儲存庫根目錄 `AIAgent`，並確認可使用 .NET SDK 10.0.100 或 `global.json` 允許的修補版本，以及提供 `coreclr` 偵錯器的 Microsoft C# 延伸模組。專案已提供 [launch.json](.vscode/launch.json) 與 [tasks.json](.vscode/tasks.json)，無須自行輸入組態或任務名稱。

1. 在程式碼設定中斷點，按 **F5** 啟動唯一的 `Debug AIAgent.Cli` 組態。
2. VS Code 先執行預設建置任務 `build AIAgent.Cli`，以 Debug 組態建置 `src/AIAgent.Cli/AIAgent.Cli.csproj`，包含必要的套件還原。
3. 建置成功後以 `coreclr` 啟動 `src/AIAgent.Cli/bin/Debug/net10.0/AIAgent.Cli.dll`，互動顯示於**整合終端機**。
4. 在終端機確認工作區，再隱藏輸入 Key，進入聊天；可先使用 `/help`、`/clear`、`/exit` 檢查本機操作。這些指令不呼叫模型，真實聊天仍須可用 Key 與網路。

設定中的 `cwd` 為 `${workspaceFolder}`、`args` 為空陣列，因此預設工作區就是專案根目錄。工作區確認仍依產品流程執行；若要操作其他工作區，可依前述 CLI 的 `--workspace <path>` 方式另行啟動。Key 不放入偵錯參數、環境變數或兩份 JSON。

`preLaunchTask` 與任務 label 一致；**Cmd+Shift+B** 可單獨執行同一個預設建置任務。偵錯設定關閉例外及模組載入訊息記錄；本機操作與 Key 輸入仍由產品終端機流程管理。2026-10-23 已實際驗證的 F5 範圍見[最新驗證摘要](#latest-validation)。

## 聊天操作

提示為 `你> `。**輸入期間不回顯；Enter 後顯示安全訊息**。不顯示星號或輸入長度；已知指令只顯示操作結果。一般訊息含當前 Key 時整筆阻擋，原文不顯示或送出。

| 操作 | 行為 |
|---|---|
| 一般文字 | 保留原始首尾空白，啟動最多五次生成的回合；生成及工具處理期間不接受下一則訊息。 |
| 空白 | 不送出，重新提示。 |
| `/help` | 顯示聊天指令與目前能力；啟動參數 `--help` 只顯示啟動用法。 |
| `/clear` | 清除對話、快照、核准記憶體及最近本機結果，保留 Key、工作區、模型及程序累計；不復原已寫檔案。 |
| `/exit`、EOF、Ctrl+D | 正常退出；部分輸入會丟棄。 |
| 等待輸入／認證選擇／Key 輸入時 Ctrl+C | 退出 130。 |
| 生成／工具處理／核准中 Ctrl+C | 取消並等待本回合收尾，再回到輸入；不取消後續聊天。 |
| 生成／工具處理／核准中 Ctrl+D | 取消並等待收尾，正常退出。 |

指令在 Trim 後區分大小寫且完整匹配；`/CLEAR`、`/help extra`、`/tmp/a` 皆為未知指令，不送模型。生成、工具處理、取消及收尾期間的普通按鍵（包含 `/exit`）會丟棄，不能排隊；外部 SIGINT 依相同聊天狀態路由。

每行最多 16384 個 UTF-16 單位，補充平面字元計 2。支援中文、Unicode scalar 刪除、Backspace／Delete；導覽鍵不移動游標。控制字元、非法代理字元及 Unicode 行／段分隔字元會使整行無效，超限後刪除也不恢復有效。CRLF 視為一次 Enter；沒有多行編輯或輸入歷史。

只有合法完成的回合才保存：使用者訊息、零或多組完整模型呼叫／工具結果，以及最終模型回答；最多保留 5 個完整回合。第 6 次請求仍包含前 5 回合，成功提交後才淘汰最舊回合。拒答、取消、截斷與一般失敗不加入歷史；截斷明示「回答未完整產生」。敏感模型紀錄、非法協定／配對或無法安全回填結果時，清除全部 Context 與讀取快照並通知。`/clear` 只釋放記憶體參考，不清除終端機捲動紀錄。

認證失敗時先完成舊政策下的錯誤與用量顯示、釋放舊 client，再詢問 `重新輸入 Key？[y/N]`。隱藏輸入 y／yes 後重新讀取 Key；成功切換新的 session／client 時清空對話、保留累計並等待新的訊息，不重送失敗需求。模型、權限、配額、網路與逾時錯誤各自分類，不誤當成 Key 問題。

每回合顯示生成方法呼叫數、回合傳輸嘗試數、「本次用量」與「程序累計」，欄位依序為輸入／輸出／思考／快取／總計。明確零值保留，缺值或負值標示不可得；部分資料顯示已知小計及缺值次數。溢位保留最後可表示的小計並永久標示不完整。Total 採 API 原值，thought／cached 不再加進 Total。傳輸嘗試不證明 Google 已收到或計費，本機統計不是正式帳單。

參數或設定錯誤退出 2；終端機、I/O、不可恢復本機或清理失敗退出 1。清理失敗優先於正常／取消退出；明確程序取消優先於 EOF。API 回合失敗本身通常回到輸入，沒有自動重試。

## 模組與後續使用方式

| 模組 | 目前責任 |
|---|---|
| Core | `ChatApplication` 狀態機、`AgentRunner`、完整回合驗證、`ConversationContext` 與用量累計；新增契約為 internal，不依賴 SDK 型別。 |
| Llm | session 綁定的 `GeminiClient`、SDK 轉換、受控 HTTP 傳輸、協定保存、回應與錯誤分類。 |
| Tools | 固定七工具 Schema、Registry、各工具嚴格參數解析與安全結果轉接。 |
| Workspace | 依固定工作區及當前政策驗證路徑、種類、連結與內容；有界列檔／讀取、記憶體快照與候選、核准後安全發布／核對及明確 JSON 投影。 |
| Security | 當前 Key 的 Ordinal 完整子字串檢查、整次拒絕、顯示遮蔽與已知憑證欄位遮蔽。 |
| Configuration／Cli | 嚴格設定、固定工作區、隱藏輸入、單一終端機讀取者、busy 控制觀察及安全純文字顯示。 |

`AgentRunner` 建立 `LlmRequest(SystemInstruction, Messages, Tools)`，以 `ILlmClient.GenerateAsync(request, cancellationToken)` 取得 `LlmResult`。文字訊息使用 `LlmMessage.FromUserText`；工具結果使用 `FromToolResults`。正式請求固定依序提供上述七工具，所有工作區 I/O 都經過同一服務。

`Completed` 與合法 `ToolCalls` 才會提供可續傳的 `ModelMessage`。模型紀錄完整保存 Content、part 順序、工具 ID、thought parts 與簽章；顯示文字只串接非 thought 的 text parts。模型訊息不可公開序列化、列印內容或跨 client／session 續用。

工具呼叫保留原始 `PartIndex` 及可空 `CallId`。後續呼叫者須先附上完整 `ModelMessage`，再提供包含全部對應結果的工具訊息；轉接器依原呼叫順序組成一份 user Content。遺漏、重複、錯名、錯 ID 或錯位置的結果均在傳輸前拒絕。Gemini 轉接層不執行工具或自行開始下一步。

## 請求與資料保護

- 固定 Gemini Developer API、官方 HTTPS 主機及 `v1beta` Generate Content；Key 僅出現在 `x-goog-api-key` 標頭。顯式設定覆寫 SDK 的環境來源與全域 Base URL；不提供自訂端點。
- 每次生成最多一次傳輸嘗試，單一候選、輸出上限 8192 tokens、整次期限 120 秒；SDK 逾時為 120000 毫秒。關閉重新導向、自動重試與自動續傳。
- SDK 轉換完成後，以實際 UTF-8 JSON body 檢查 262144 位元組上限；262145 即拒絕，不裁切原文或歷史。
- 檢查應用指示、訊息、工具 Schema 的欄位名稱與值、參數及結果；傳輸前再次檢查實際 JSON。命中當前 Key 時整次阻擋，不遮蔽後繼續傳送。
- 輸出先檢查敏感字串與跨 text parts 組合文字。敏感輸出回傳 `SensitiveOutput`、空工具呼叫、無模型紀錄並要求清除 Context，仍保留已取得的純數值用量。
- 只交接固定安全訊息及分類。原始 HTTP body、SDK 例外、標頭與協定資料不進入日誌或核心結果。錯誤 body 最多讀取 65536 位元組加一個超限判斷位元組；超限或無效內容只採一般 HTTP 分類。

Key 比對沿用既有精確規則，不承諾辨識任意編碼或轉換後的秘密。簽章只保存，不解密；敏感紀錄不藉由修改簽章繼續使用。

`RequestDispatched` 只代表已交給最內層傳輸開始嘗試，不證明 Google 已收到或計費。`Usage` 的五個欄位使用可空 `long`；缺值或負值為不可得，明確零值保留，Total 採 API 原值，不補算或重複加總 thought／cached。

同一 client 一次只接受一個生成。外部取消優先於逾時；取消後不發布遲到成功。`DisposeAsync` 停止新請求、取消並等待進行中操作後釋放資源；釋放造成的中止以 `ClientUnavailable` 收尾。session 由呼叫者擁有，必須先釋放 client 再釋放 session。

## 自動驗收對照

SDK 案例走真實 SDK 序列化與解析，在最內層以假 handler 攔截官方 URI，絕不連線至 Gemini。所有測試只使用無認證功能的假 Key；請求與回應僅留在記憶體，斷言訊息不傾印它們。

| 第 02 份驗收 | 對應測試與證據 |
|---|---|
| GEM-AC-001 | `LlmContractTests` 核心介面無 SDK 型別；固定套件成功還原、編譯，假傳輸驗證 Generate Content。 |
| GEM-AC-002 | `ConfigurationTests.GEMAC002`：模型格式、長度、首尾空白及設定失敗先於其他服務。 |
| GEM-AC-003 | `GeminiClientTests.GEMAC003`、`GeminiLifetimeTests.GEMAC003`：認證位置、官方 URI、端點拒絕、產品 handler 關閉重新導向。 |
| GEM-AC-004 | `GeminiClientTests.GEMAC004`：衝突環境與 SDK 全域端點；序列化執行並還原原程序狀態。 |
| GEM-AC-005 | 生成 body 的候選／輸出限制；可控時間驗證 120 秒期限及 SDK 的 120000 毫秒設定。 |
| GEM-AC-006 | 實際 body 的 262144／262145 邊界，含中文、跳脫、Schema 與歷史簽章。 |
| GEM-AC-007 | 429、500、503、302、連線失敗及 CONTINUATION 均最多一次傳輸；建構與釋放不增加請求。 |
| GEM-AC-008 | 快照、JSON 所有權、非法請求、並行拒絕、完成後可再呼叫與訊息序列化拒絕。 |
| GEM-AC-009 | 多個文字與 thought parts 的顯示投影；下一次請求仍帶完整內容及簽章。 |
| GEM-AC-010 | 所有工具呼叫、原始位置、有值及缺省 ID、參數與文字混合狀態。 |
| GEM-AC-011 | 測試顯式執行兩次工具結果交接再取得文字；驗證完整 Content、簽章、ID 與結果順序。 |
| GEM-AC-012 | 同名無 ID 呼叫配對；遺漏、重複、錯名、錯 ID 及錯位置均不送出。 |
| GEM-AC-013 | 無工具卻收到呼叫、空名稱、非 object 參數與自由文字不推斷工具。 |
| GEM-AC-014 | 無候選、空白、僅 thought、PromptFeedback 與各指定拒絕原因。 |
| GEM-AC-015 | MAX_TOKENS／CONTINUATION 含工具資料時不發布呼叫或模型紀錄、不續傳。 |
| GEM-AC-016 | 損壞 JSON、錯誤根型別、多候選、缺少／未知結束原因與阻擋 enum、非預期 part、同 part 多種 payload，以及不支援的部分工具呼叫。 |
| GEM-AC-017 | 邏輯輸入、工具 Schema／結果與最終 JSON 解碼後欄位名稱／值的敏感阻擋。 |
| GEM-AC-018 | 敏感文字、跨 parts 文字、工具名稱／ID／巢狀參數及協定 metadata；保留數值用量。 |
| GEM-AC-019 | 假 SDK／網路例外、損壞 JSON、錯誤 metadata 的結果及 stdout／stderr 不外洩；沿用既有遞迴憑證欄位遮蔽測試。 |
| GEM-AC-020 | HTTP 分類、可信 ErrorInfo 白名單、未知來源與 reason、65536／65537 位元組及無效錯誤 body。 |
| GEM-AC-021 | 呼叫前取消、傳輸中及讀取回應時取消、期限、同時取消優先與遲到成功拒絕。 |
| GEM-AC-022 | 重複釋放、進行中取消等待、釋放後拒絕、session 所有權與跨 client 紀錄拒絕。 |
| GEM-AC-023 | 完整／部分／缺失用量、零與負值；Empty、Refused、Truncated 及敏感輸出的用量保留。 |
| GEM-AC-024 | 合成消費端驗證超過 int 的 long、已送出／未送出與逐欄缺值；以 checked 確認溢位交接預期。第 02 份子規格實作時尚未新增產品累計器；第 03 份子規格已加入 `UsageAccumulator`。 |
| GEM-AC-025 | 完整既有測試回歸，更新模型必填案例；工作區、隱藏輸入、嚴格 JSON、排除與遮蔽規則保留。 |

## 第 02 份子規格歷史驗證紀錄（2026-10-20）

2026-10-20 的原始基線為 29 個測試、5 個失敗；已修復符號連結與 `..` 的解析順序、祖先目錄列舉要求、無效 Unicode 設定分類及重新輸入 Key 的提示遮蔽。原失敗測試保留。

本機驗證使用暫存 NuGet 快取，避免既有使用者快取的寫入限制。此環境設定僅影響套件還原，不是產品設定來源：

```sh
export NUGET_PACKAGES=/tmp/aiagent-stage02-plan/packages
export NUGET_HTTP_CACHE_PATH=/tmp/aiagent-stage02-plan/http-cache
```

獨立程式碼審查另找出 3 個異常回應問題：不支援的部分工具呼叫、同一 part 的多種 payload，以及未知阻擋原因。新增回歸測試先重現失敗，再完成修復；指定的「未指定阻擋原因」則維持無明確阻擋的行為。

| 實際執行命令 | 退出碼與結果 |
|---|---|
| `dotnet restore tests/AIAgent.Cli.Tests/AIAgent.Cli.Tests.csproj` | 0；固定版本成功還原。 |
| `dotnet build tests/AIAgent.Cli.Tests/AIAgent.Cli.Tests.csproj --no-restore` | 0；0 警告、0 錯誤。 |
| `dotnet test --project tests/AIAgent.Cli.Tests/AIAgent.Cli.Tests.csproj --no-build` | 0；61 通過、0 失敗、0 略過。 |
| `dotnet run --project src/AIAgent.Cli/AIAgent.Cli.csproj --no-build -- --help` | 0；顯示用法，沒有 Key 提示或模型請求。 |
| `python3 tests/terminal_smoke.py` | 0；13 組 PTY 檢查通過，涵蓋無回顯、512／513 長度、重試、Ctrl+C／EOF、復原與重新導向。 |
| `git diff --check` | 0；另對全部新增檔案執行 no-index 空白檢查。 |

另確認三份既有規格、Git 排除規則、SDK 選擇與測試專案設定未改動，README 區塊及本機連結有效。PTY 是自動模擬終端機的證據，不等於使用者親自在 macOS 終端機操作；上述測試也不等於真實 Gemini 服務可用。

## 第 03 份子規格驗收對照

| 驗收 | 測試與涵蓋內容 |
|---|---|
| CLI-AC-001～002 | ChatCoreTests／ChatApplicationTests：internal 契約、固定 ToString、session 一次移交、指令及零啟動生成；既有啟動 help 測試保留。 |
| CLI-AC-003～006 | ChatTerminalTests／ChatApplicationTests：無回顯、敏感輸入、16384／16385、代理配對與刪除、CRLF、busy 丟棄及單次生成。 |
| CLI-AC-007～010 | ChatCoreTests／ChatApplicationTests：7 回合、原訊息身分、不可變快照、失敗不提交、手動及強制清除。 |
| CLI-AC-011～013 | ChatSdkTests／ChatApplicationTests 與既有 GEMAC006：真實 SDK／假 handler 的 parts／簽章續傳、敏感清除、非法結果、工具定義、實際 body 邊界及錯誤分類。 |
| CLI-AC-014～017 | ChatApplicationTests：可控取消與遲到完成、提交點、程序停止優先序、各種本機失敗、client 建構／釋放與 session 順序。 |
| CLI-AC-018～023 | ChatApplicationTests／ChatTerminalTests／ChatSdkTests：認證收尾與切換、原值重新輸入、512／513、舊／新政策、CRLF 交接、完整顯示區塊控制字元與遮蔽。 |
| CLI-AC-024～028 | ChatCoreTests／ChatApplicationTests／ChatTerminalTests：五欄缺值、零與負值、傳輸次數、long／計數器溢位、跨清除及換 Key 保留累計，以及完整既有回歸。 |
| CLI-AC-029～032 | terminal_smoke.py：13 組既有案例加 11 組聊天 PTY，涵蓋 Unicode、隱藏輸入、busy、按鍵／SIGINT、EOF、I/O 故障、認證選擇及新 Key 退出復原。 |
| CLI-AC-033 | 真人 macOS 終端機操作尚未驗證；PTY 不能替代中文輸入法等真人操作。 |
| CLI-AC-034～035 | 真實 Gemini 聊天及認證失敗後換 Key 尚未驗證；由使用者日後自行隱藏輸入 Key 操作。 |

測試 host 位於 `tests/AIAgent.Cli.TerminalHost/`，由 MSTest 專案的 ProjectReference 一併建置。它只在測試組裝注入假 client，使用真正的聊天與終端機元件；認證情境透過測試管線精確放行回應。正式產品沒有假模型參數、環境 Key 或自訂端點。

## 第 03 份子規格歷史驗證紀錄（2026-10-20）

以下保留 2026-10-20 第 03 份子規格實作完成時的歷史驗證紀錄。當時後續的 README 維護只檢查文件；第 05 份實作及後續查核的驗證各自另列，不沿用下表作為新結果。

當時新增測試先重現缺少契約／功能，另重現並修正無效認證選擇誤判、提前取消的顯示政策、CRLF 跨 Key 提示及 client 部分建構的釋放問題。獨立唯讀審查另發現顯示使用者訊息期間中斷仍會進入生成的競爭；新增回歸先重現 1 次非預期呼叫，再將停止檢查與狀態切換放入同一鎖內修正。

| 命令／類別 | 當時記錄結果 |
|---|---|
| `dotnet restore tests/AIAgent.Cli.Tests/AIAgent.Cli.Tests.csproj` | 退出 0；固定版本成功還原。 |
| `dotnet build tests/AIAgent.Cli.Tests/AIAgent.Cli.Tests.csproj --no-restore` | 退出 0；建置 3 個專案，0 警告、0 錯誤。 |
| `dotnet test --project tests/AIAgent.Cli.Tests/AIAgent.Cli.Tests.csproj --no-build` | 退出 0；107 通過、0 失敗、0 略過，全程離線。 |
| `dotnet run --project src/AIAgent.Cli/AIAgent.Cli.csproj --no-build -- --help` | 退出 0；只顯示啟動用法。 |
| `python3 tests/terminal_smoke.py` | 24 組 PTY 通過。 |
| `dotnet format src/AIAgent.Cli/AIAgent.Cli.csproj --verify-no-changes` | 退出 0。 |
| `git diff --check` | 退出 0；README 連結／區塊、保護檔案及 C# 換行另行檢查通過。 |
| 真人終端機／真實 Gemini | 未執行。 |

當時依已確認範圍修正既有產品 C# 的換行、檔尾、using 及空白，未放寬 `.editorconfig`；新增 `.gitattributes` 的 C# CRLF 屬性，使 Git 正規化與既有格式規則一致，未改寫 Git 設定或索引。未新增產品套件、設定或後續工具；未安裝 hooks、提交、推送或改寫歷史。暫存測試工作區保留，不使用批次刪除。

## 第 04 份工作區唯讀服務

`IWorkspaceFilesFactory.Create(session)` 為當前 session 建立一份內部服務，建構不掃描或讀取工作區。`ListFilesAsync(path, cancellationToken)` 與 `ReadFileAsync(path, cancellationToken)` 共用路徑、敏感資料與生命週期檢查；服務本身不管理工具 Schema、派送或模型循環；第 05 份負責上述協調。

| 限制 | 固定值與行為 |
|---|---|
| 相對路徑 | 原始輸入最多 4096 UTF-16 單位；根使用 `.`；不 Trim、不展開變數或 `~`，不允許 `..`、絕對路徑或反斜線。 |
| 列檔 | 只列第一層，最多掃描 4096 筆、回傳 200 筆；依完整相對 Path 的 Ordinal 排序。 |
| 檔案 | 最多 131072 原始 bytes（含 BOM）；最多實讀 131073 bytes 偵測增長，完整讀取或整次拒絕。 |
| 文字 | 嚴格 UTF-8；支援開頭 UTF-8 BOM，保留原 bytes、換行與檔尾；拒絕非法編碼及規格列出的控制字元。 |
| 工具資料投影 | 完整 Response object 最多 196608 UTF-8 JSON bytes；列檔只移除尾端完整項目，讀檔超限不截斷內容。既有 Gemini 262144-byte 請求上限仍獨立生效。 |
| 讀取快照 | 每份服務最多 20 個 Ordinal 相對路徑、原始 bytes 合計 1048576；成功重讀更新順序，查詢／列檔不刷新。 |

敏感名稱、連結與特殊項目在列檔中隱藏，`SafetyFiltered` 固定為 true，不提供隱藏數量；`Truncated` 表示項目、掃描或結果容量限制導致不完整。掃描達限時只保證已觀察集合的排序前綴。

讀取前先使同路徑舊快照失效，成功時才發布新 bytes、BOM 與 SHA-256。`/clear`、強制 Context 重設、接受替換 Key、退出與服務釋放共同清除快照；世代檢查防止清除後的遲到操作補回成功。呼叫前已取消則不做 I/O、不移除既有快照；並行第二操作立即拒絕，釋放會取消並等待活動操作。

每次操作重新檢查固定根與工作區內沿途種類、連結及狀態。macOS 轉接器區分普通檔案、目錄、連結與 FIFO／socket 等特殊項目；不以試讀辨識種類。應用層前後檢查無法保證抵抗精確交換後還原、硬連結其他名稱或掛載變動，並非作業系統沙盒。當前只實測 macOS arm64；其他架構／平台未驗證。

當前 Key 出現在 path、名稱、內容或投影的實際欄位名稱／字串值時整次拒絕，不回傳遮蔽後的成功內容。明確 writer 不匯出完整路徑、快照或政策；若短 Key 也命中固定失敗投影，`TryCreateResponse` 回傳 false，留給第 05 份停止送出。敏感名稱規則與當前 Key 比對不等於通用秘密掃描。

## 第 04 份子規格驗收對照

| 驗收案例 | 主要測試檔與驗證內容 |
|---|---|
| WS-AC-001～002、020、028 | `WorkspaceContractTests`、`WorkspaceFilesTests`：工廠無 I/O、資料所有權、固定安全結果、JSON 欄位與短 Key。 |
| WS-AC-003～008 | `WorkspacePathTests`、`WorkspaceBoundaryTests`、`WorkspaceReadIntegrationTests`：路徑、敏感清單、根邊界、真實內外／壞／循環連結及 FIFO／socket。 |
| WS-AC-009～012 | `WorkspaceFilesTests`、`WorkspaceBoundaryTests`：第一層列檔、Ordinal 排序、200／4096 邊界、容量截斷、例外及資源關閉。 |
| WS-AC-013～020 | `WorkspaceFilesTests`、`WorkspaceBoundaryTests`、`WorkspaceReadIntegrationTests`：UTF-8／BOM／換行、控制字元、大小／JSON 精確邊界、敏感內容及狀態變更。 |
| WS-AC-021～024、027 | `WorkspaceFilesTests`、`WorkspaceBoundaryTests`：Ordinal 快照、容量／淘汰、失效、Clear／取消、並行、釋放等待及清理失敗。 |
| WS-AC-025～026、030 | `ChatWorkspaceTests` 及完整既有回歸：真實 ChatApplication 的清除／換 Key／建構失敗清理；第 05 份接入兩個唯讀工具，第 06 份擴為目前四工具，清除／換 Key 仍不額外生成，既有 Gemini 請求限制保留。 |
| WS-AC-029 | `WorkspaceReadIntegrationTests`：真實暫存工作區逐檔比較 bytes 與項目，檔案內假指令不觸發額外操作。 |
| WS-AC-031～034 | 既有 PTY 回歸由後續保留；列檔／讀檔／寫入的離線整合已由第 05～07 份承接。真人終端機與真實 Gemini 仍未驗證，最新範圍見上方摘要。 |

## 第 04 份子規格歷史驗證紀錄（2026-10-21）

當時在 macOS arm64、.NET SDK 10.0.100 執行，以下為該次實際結果；53 個新增測試加上原有 107 個，合計 160 個。完整產品驗收對應 WS-AC-001～030；PTY 單獨對應 WS-AC-031，不將真人或後續工具流程計入。

| 實際命令 | 退出碼與結果 |
|---|---|
| `dotnet restore tests/AIAgent.Cli.Tests/AIAgent.Cli.Tests.csproj` | 0；依賴已還原。 |
| `dotnet build tests/AIAgent.Cli.Tests/AIAgent.Cli.Tests.csproj --no-restore` | 0；0 警告、0 錯誤。 |
| `dotnet test --project tests/AIAgent.Cli.Tests/AIAgent.Cli.Tests.csproj --no-build` | 0；160 通過、0 失敗、0 略過，全部離線。 |
| `dotnet run --project src/AIAgent.Cli/AIAgent.Cli.csproj --no-build -- --help` | 0；維持啟動用法，沒有新增檔案指令。 |
| `python3 tests/terminal_smoke.py` | 0；既有 24 組 PTY 通過，包含聊天、清除、換 Key、取消／退出及終端機復原。 |
| `dotnet format src/AIAgent.Cli/AIAgent.Cli.csproj --verify-no-changes` | 0。 |
| `git diff --check` | 0；新檔另做 no-index 空白檢查；變更 C# 為 CRLF、無檔尾換行。 |
| 保護檔案及文件檢查 | 總規格、01～04 規格、設定、專案／套件版本、格式規則及應用指示均未修改；README 連結存在、程式碼區塊閉合。 |
| WS-AC-032：真人終端機 | 未執行，不以 PTY 或測試 host 代替。 |
| WS-AC-033～034：真實 Gemini 工具往返及寫入交接 | 留待第 05～07 份；本次未驗證。 |

一次獨立唯讀程式審查指出兩項需修正問題，均先加入失敗回歸再修正：前置 `./` 不得繞過磁碟機語法拒絕；根目錄於初次子項目檢查、開啟、列舉或讀取失敗期間消失時，須回報 WorkspaceUnavailable 並清空所有快照。同時驗證 Clear 與根失效競爭仍以舊世代取消收尾。修正後重新執行上表完整驗收通過，無待處理的審查項目。

第 04 份實作當時沒有新增產品套件、設定、工具派送或寫入方法；測試資料只使用假值，隔離暫存工作區保留，不批次刪除。本次未提交、推送或改寫歷史。

## 第 05 份 Agent 工具循環

第 05 份實作當時只登錄 `list_files`、`read_file`，兩者皆要求唯一的 string `path`，根目錄使用 `.`；第 06 份加入建立與替換，第 08 份再擴充三項搜尋／範圍工具，目前正式登錄七工具。以下說明第 05 份建立並沿用至今的循環行為，寫入增量見下一節。模型可直接聊天；要操作檔案時，必須提出原生 function call。Markdown、檔案文字或模型附帶文字不會觸發隱藏操作。

Registry 逐一驗證名稱及參數，沿用工作區的路徑、UTF-8、敏感檔案、快照及容量限制。額外、重複或大小寫變體欄位會拒絕；原始 HTTP 回應的重複 JSON 欄位在 SDK dictionary 轉換前拒絕，不採最後值。原始模型 Content、thought signatures、ID 與 part 順序保留，全部結果依原呼叫順序集中回填，逆序或缺失配對不續送。

`AgentRunner` 只協調當前回合。每回合最多五次 `GenerateAsync`，不是五個工具；同批工具依序執行。第五次仍要求工具時，先做安全與協定檢查，再將該批全部標記 `generation_limit`，不開始該批 I/O，也不送第六次生成。一般參數／檔案失敗可交回模型修正；取消、工作區失效、敏感資料或非法配對會停止後續操作。完整 SDK body 仍限制為 262144 UTF-8 JSON bytes，不裁切結果、歷史或簽章以規避限制。120 秒期限適用每次生成，不是整個多步回合的期限。

busy 範圍涵蓋生成、工具、配對、用量及收尾。Ctrl+C 取消同一回合並等待已開始操作結束；Ctrl+D／EOF 等待收尾後正常退出。工具已發布成功時，稍後取消仍保留其本機成功狀態及快照；共同清除仍會丟棄快照。最終模型回答是候選，CLI 在取消／停止決策點通過後才提交。輸出或清理失敗退出 1，不能被同時取消蓋掉。

本機操作結果先於最終回答或模型錯誤顯示。讀取列出安全相對路徑及實際 bytes，列檔列出安全回傳筆數與截斷提示；未知名稱不回顯，失敗不回顯請求 path。讀取完成後模型失敗，仍保留本次已讀取的事實，不自動重讀。只在記憶體保存最新安全操作摘要，不保存額外參數、全文、CallId 或可續傳紀錄。

每次模型結果由 Runner 向回合與程序累計各記帳一次。生成次數與傳輸嘗試數分開；未傳輸的本機拒絕仍占一次方法呼叫，但不增加 token 缺值。回合統計每次重建，程序統計跨 `/clear`、強制清除與換 Key 保留。五欄各自處理缺值及溢位，Total 不補算。

### 第 05 份驗收對照

| 驗收 | 主要證據 |
|---|---|
| LOOP-AC-001～005、013 | AgentContractTests、ToolRegistryTests、AgentSdkTests、AgentChatTests：internal 契約、所有權、正式 Schema、原始重複欄位、真實字面路徑與明確投影。 |
| LOOP-AC-006～012、014～018 | AgentRunnerTests、AgentBoundaryTests、AgentAcceptanceTests、AgentLifecycleTests：純聊天、多步／同名呼叫、完整簽章、配對拒絕、可修正失敗、服務失效與第五次停止。 |
| LOOP-AC-019～025 | AgentContractTests、AgentLifecycleTests、AgentAcceptanceTests 與既有 ChatApplicationTests／ChatWorkspaceTests：整回合裁切、清除、取消與發布競爭、busy 收尾、換 Key 及釋放。 |
| LOOP-AC-026～029、035 | AgentSdkTests、AgentBoundaryTests、AgentChatTests、AgentAcceptanceTests：真實 SDK 敏感回應、整批預檢、真實敏感檔案、短 Key、控制字元與只讀資料。 |
| LOOP-AC-030～034 | AgentAcceptanceTests、AgentChatTests、ToolRegistryTests 及既有用量測試：工具後模型失敗、真實列檔截斷、逐欄統計／溢位，以及含真實工具結果的 262144／262145 body 邊界。 |
| LOOP-AC-036 | 完整 MSTest 回歸、help、格式及文件檢查；只更新階段增量造成的預期。 |
| LOOP-AC-037 | macOS PTY 的工具續接、clear 重讀、受控遲到取消、Ctrl+C／SIGINT／EOF、工具後認證與終端機復原。 |
| LOOP-AC-038～039 | 真人終端機與真實 Gemini 未驗證，GUI 自動偵錯不替代這兩項。 |
| LOOP-AC-040 | 寫入／預覽／確認／衝突已由第 06 份實作及離線驗證；第 07 份已提供整合測試與掃描器，Release 及人工交付門檻仍未完成。 |

### 第 05 份歷史驗證紀錄（2026-10-21）

以下保留 2026-10-21 第 05 份實作後在 macOS arm64、.NET SDK 10.0.100 的歷史結果，不作本次 README 維護的新驗證。原有 160 項回歸與新增 56 項測試案例合計 216 項；這是測試執行數，與 LOOP-AC-001～036 的驗收編號數分開。下列未新增寫入工具、未提交等敘述亦指當時工作範圍。

| 命令／類別 | 退出碼與實際結果 |
|---|---|
| `dotnet restore tests/AIAgent.Cli.Tests/AIAgent.Cli.Tests.csproj` | 0；固定依賴成功還原。 |
| `dotnet build tests/AIAgent.Cli.Tests/AIAgent.Cli.Tests.csproj --no-restore` | 0；3 個專案，0 警告、0 錯誤。 |
| `dotnet test --project tests/AIAgent.Cli.Tests/AIAgent.Cli.Tests.csproj --no-build` | 0；216 通過、0 失敗、0 略過；使用假模型／handler 與暫存工作區，無真實 Gemini 呼叫。 |
| `dotnet run --project src/AIAgent.Cli/AIAgent.Cli.csproj --no-build -- --help` | 0；只顯示啟動用法。 |
| `python3 tests/terminal_smoke.py` | 0；原有 24 組與新增 6 組，合計 30 組 PTY 通過。 |
| `dotnet format src/AIAgent.Cli/AIAgent.Cli.csproj --verify-no-changes` | 0；變更測試檔另外以相同格式規則檢查。 |
| `git diff --check` | 0；全部新增檔案另做 no-index 空白檢查。 |
| 保護範圍／文件檢查 | 總規格、01～04 規格、原工作區實作、設定、專案／套件版本及格式規則未改動；原有未追蹤的 05 規格保留原文。README 本機連結、程式碼區塊及空白通過。 |
| LOOP-AC-038：真人終端機 | 未執行，不以 PTY 代替。 |
| LOOP-AC-039：真實 Gemini | 未執行；不取得或儲存真實 Key。 |
| LOOP-AC-040：後續寫入／發布 | 不屬本階段完成項目。 |

獨立唯讀審查發現兩項重要競爭問題，均先用可控測試重現，再修正並重跑上述完整驗證：認證交接決策後的 client／busy 觀察器清理期間取消，不得仍開啟 Key 讀取；工具不可恢復失敗不得被同時取消或安全投影失敗蓋掉。認證交接在完成舊政策下結果與用量後，於同一鎖內切換至 Key 選擇階段；之後取消退出 130 且零選單／Key 讀取。不可恢復本機失敗維持退出 1，同時保留安全操作紀錄及必要重設。無待處理的審查項目。

實作決策沿用已確認計畫：在目前 checkout 工作，不提交或推送；實際回應 JSON 的重複欄位統一在 SDK 轉換前拒絕；生成前取消零呼叫，提交後 busy 取消不回溯成功歷史。未新增套件、設定、後續規格或持久化工具鏈，沒有寫入工具或批次刪除，隔離測試資料保留。

## 第 06 份檔案編輯與逐次核准

`create_file(path, content)` 只建立不存在的檔案，父目錄必須已存在；不覆蓋既有項目或建立目錄。空內容可建立 0-byte 檔案，明確輸入的開頭 U+FEFF 會成為 BOM，不補換行。

`replace_text(path, old_text, new_text)` 要求同一活動工作區有成功 `read_file` 快照。舊文字必須非空且 Ordinal 精確匹配一處，重疊匹配也計入；新文字可以為空。保留 BOM 與未修改部分的精確 bytes，不做 Unicode、換行或整檔格式化。快照缺失時須由模型明確重新讀檔，不從歷史重建；成功或未確認寫入後不自動發布新快照。

每次操作由本機產生完整單一 hunk，列出操作、相對路徑、原始／候選 bytes 與 BOM，保留前後各三行脈絡。CRLF／LF／CR／EOF 有明確標記；Tab、反斜線、方括號及 Unicode 格式字元會跳脫。完整預覽與首次提示最多 65536 UTF-8 bytes，超限即拒絕，不裁切變更。各編輯參數另有 131072 UTF-16 單位與 131072 UTF-8 bytes 上限，完整檔案含 BOM 最多 131072 bytes。

提示為 `套用此變更？[y/N]`，輸入不回顯。只有該次預覽顯示完畢、清除舊輸入並啟用確認讀取後的 y／yes 可同意；大小寫及首尾空白可接受。n／no／空白拒絕，其他值重問同一候選。核准期間 `/clear`、`/exit` 只是無效選擇；使用 Ctrl+C 取消回合，Ctrl+D／EOF 正常停止。拒絕會立即停止本回合其餘工具及生成，不請模型換種寫法再次核准；同批多筆仍各自核准。

核准後重新比對根、沿途身分、目標、完整原始 bytes 與快照保存的 Device／Identity／完整 mode。使用同目錄、排他建立的一份暫存，寫入期間為 0600；新檔發布後亦為 0600，替換只在完整寫入及 flush 後於自有 handle 設定原檔的 0777 權限位元。帶 setuid／setgid／sticky 的原檔拒絕。新檔以 macOS 排他 rename 發布，替換用原生 rename，沒有 copy／link fallback、原目標先刪除或備份。

發布成功且目標身分、mode、全部 bytes 與關閉均通過，才顯示「已寫入並核對」。發布結果不明時顯示「寫入結果未確認；停止後續操作」，最多一次安全有界查核，不重試、不反向還原。核准前不建立暫存；發布前取消會清理已持有的單一暫存。發布後取消仍完成查核及清理，已核對事實不因取消、模型失敗或 Response 無法安全建立而消失。先寫 A 再拒絕 B 時，A 仍已寫入。

清理只處理本次精確持有且身分仍相符的暫存路徑；不刪換成他人項目的路徑。清理失敗保留寫入事實、退出 1，僅提示需人工處理，不列出暫存名稱或自動掃描殘留。`/clear` 與取消均不復原檔案。替換不保證保留 owner／group、ACL、xattr、resource fork、建立時間或硬連結關係；flush 不等於父目錄同步或斷電持久性。仍有應用層 TOCTOU／硬連結／掛載限制。

### 第 06 份離線驗收對照

| 驗收 | 測試與證據 |
|---|---|
| EDIT-AC-001～005 | `EditContractTests`、`EditBoundaryTests`、`EditLifecycleTests`、`EditChatTests`：internal 契約、合法狀態、四工具順序／Schema、嚴格參數及字面路徑。既有 wire 重複欄位與敏感整批檢查回歸保留。 |
| EDIT-AC-006～012 | `EditWorkspaceTests`、`EditBoundaryTests`、`EditLifecycleTests`：真實建立／替換、BOM／換行／mode、重疊與 NoChange、編碼／控制字元、完整候選上限、65536／65537 預覽邊界、hunk／脈絡／EOF 與跳脫。 |
| EDIT-AC-013～017 | `EditApprovalTests`、`EditWorkspaceTests`、`EditChatTests` 及 PTY：單一讀取者、舊輸入丟棄、選擇、同 session 綁定、拒絕停止、取消／Clear 及寫入後快照失效。快照淘汰／換 Key／釋放沿用既有回歸。 |
| EDIT-AC-018～024 | `EditBoundaryTests`、`EditFaultTests`、`EditLifecycleTests`：相同 bytes 的 inode／mode 衝突、非法基準失效、發布前 metadata 重驗、真正排他 rename、暫存故障、特殊 mode 及發布後核對失敗。 |
| EDIT-AC-025～030、040 | `EditFaultTests`、`EditAgentIntegrationTests`、`EditChatTests`：安全投影失敗保留 Verified、NotApplied／Verified／Unconfirmed 各自的清理失敗、取消／Clear／Dispose、並行拒絕與核准 I/O／busy 恢復失敗優先序。 |
| EDIT-AC-031～035、039 | `EditAgentIntegrationTests`、`EditLifecycleTests`：第五次零寫入、真實 Chat＋SDK 假 handler＋工作區往返、同批 read→replace／逐筆核准、寫後模型失敗、五完整回合及實際 SDK body 262144／262145 bytes。 |
| EDIT-AC-036～038、041 | 新增 Key 拼接／固定預覽短 Key／格式字元案例，加上完整 01～05 的敏感資料、配對、取消、用量、help、格式與空白回歸。 |
| EDIT-AC-042 | 測試 host 與 PTY：y／yes、拒絕／空白／無效選擇、預輸入、CRLF、Ctrl+C／SIGINT／EOF、預覽期間丟棄及明確同步的發布前後取消。 |
| EDIT-AC-043～044 | 真人終端機與真實 Gemini 未執行；GUI 自動偵錯未涵蓋完整寫入／核准人工驗收。 |
| EDIT-AC-045 | 第 07 份已提供限制整合測試、提交／產物掃描器及選用 hook；Release 掃描仍受阻，真人／服務／人工分發門檻未完成。 |

### 第 06 份歷史驗證紀錄（2026-10-22）

以下保留 2026-10-22 第 06 份在 macOS arm64、.NET SDK 10.0.100 的歷史驗證，不作本次 README 維護的新驗證；測試使用假 Key、假模型／HTTP handler、可控制 I/O 與保留的隔離工作區。驗收編號與測試方法／PTY 組數分開計算。下列「第 07 份尚未實作」及未新增 hooks、未提交等敘述均指第 06 份驗收當時，目前狀態以上方最新摘要為準。

| 命令／類別 | 退出碼與實際結果 |
|---|---|
| `dotnet restore tests/AIAgent.Cli.Tests/AIAgent.Cli.Tests.csproj` | 0；固定依賴成功還原。 |
| `dotnet build tests/AIAgent.Cli.Tests/AIAgent.Cli.Tests.csproj --no-restore` | 0；3 個專案，0 警告、0 錯誤。 |
| `dotnet test --project tests/AIAgent.Cli.Tests/AIAgent.Cli.Tests.csproj --no-build` | 0；269 通過、0 失敗、0 略過，含既有 216 項與新增 53 項執行案例。 |
| `dotnet run --project src/AIAgent.Cli/AIAgent.Cli.csproj --no-build -- --help` | 0；啟動用法及階段能力提示通過。 |
| `python3 tests/terminal_smoke.py` | 0；原有 30 組與新增 13 組，合計 43 組 PTY 通過；工具進入、預覽、暫存及發布均使用明確同步點。 |
| `dotnet format src/AIAgent.Cli/AIAgent.Cli.csproj --verify-no-changes` | 0；變更測試及 test host 另外以相同格式規則檢查。 |
| `git diff --check` | 0；全部新增檔案另做 no-index 空白檢查。 |
| 變更範圍／文件檢查 | 總規格與 01～06 規格、設定、專案／套件版本、公開 LLM 契約、格式及排除規則未改動；變更 C# 皆為 CRLF 且無檔尾換行，README 本機連結與程式碼區塊通過。 |
| EDIT-AC-043：真人終端機 | 未執行，不以 PTY 代替。 |
| EDIT-AC-044：真實 Gemini | 未執行，不取得或儲存真實 Key。 |
| EDIT-AC-045：第 07 份交付 | 尚未實作，不列為本階段完成。 |

獨立唯讀審查發現三項重要問題，均先建立失敗回歸再修正：已有快照的沿途目錄在候選建立前失效時，必須清除基準並停止本回合；發布前最後一次暫存檢查須比較完整身分、長度、修改時間與 mode；核准讀取遇到 EOF 後的 Ctrl+C 不得覆蓋正常停止。修正後重新執行上述完整驗收，無待處理審查項目。PTY 亦補上工具實際進入的同步點，避免以進度顯示誤判工具已受理。

既有規格、設定、SDK／測試套件、公開 LLM 契約與 Git 排除規則保留；沒有新增認證來源或 hooks，沒有提交、推送或批次清理。x64、真人終端機與真實 Gemini 未驗證，不以假 handler 或 PTY 代替。

## 第 07 份預算整合與開發掃描

「預算」沿用各模組的請求次數、bytes、筆數及回合限制。沒有新增計數 API、價格／免費額度估算、金額扣抵、模型切換、重試或摘要。Runner 對每次返回結果各記程序與回合用量一次；只有 `RequestDispatched=true` 才更新五欄及缺值計數。生成次數與傳輸嘗試分開，Total 採 API 原值，不從其他欄相加或補算。clear、重設及換 Key 保留程序累計，下一回合建立獨立累計。

| 邊界 | 固定限制與單位 |
|---|---|
| 生成／歷史 | 每回合最多 5 次生成；最近 5 個完整成功回合。 |
| 輸入／產品 path | 16384／4096 UTF-16 單位，補充平面字元計 2。 |
| 原始檔／完整候選 | 含 BOM 最多 131072 bytes；各編輯參數另限 131072 UTF-16 單位及 UTF-8 bytes。 |
| 列檔／結果／快照 | 回傳 200 筆、取得 4096 個直接子項目；完整結果 JSON 196608 bytes；快照 20 份／1048576 bytes。 |
| 預覽／暫存 | 完整預覽與首次提示 65536 UTF-8 bytes、前後各最多 3 行脈絡；核准後每次至多一份自有候選暫存。 |
| SDK 請求／輸出／期限 | 實際完整 body 262144 UTF-8 JSON bytes；8192 output tokens、1 candidate、每次生成 120 秒；錯誤分類最多讀 65536 bytes 加一個哨兵 byte。 |

### 獨立離線掃描

需要 Python 3.9 以上；staged 另需支援本規格唯讀選項的 Git。從儲存庫工作目錄執行：

```sh
python3 scripts/check_sensitive_content.py --help
python3 scripts/check_sensitive_content.py staged
python3 scripts/check_sensitive_content.py artifacts --path "/example/new-publish-directory"
python3 -B -m unittest discover -s tests -p 'test_sensitive_content.py'
```

掃描不是模型工具，不接入產品啟動或聊天；不需要 Key、不連網、不修改 index／設定／原檔、不建立掃描內容副本或清理殘留。沒有 allowlist、忽略註解或自動修復。測試只使用合成資料與隔離 Git，資料保留，不使用遞迴清理。

- `staged` 掃完整有效 index 的原始 blob，涵蓋未變動的已追蹤檔案與強制加入；不讀工作樹版本、HEAD 內容、歷史或未追蹤檔。支援巢狀呼叫、linked worktree、相對／絕對替代 index 及 commit 暫時 index。前後 entries 必須相同，變動不重掃。
- `artifacts` 掃指定發布目錄內的全部普通檔案，包括 DLL、PDB、相依套件、隱藏與非 UTF-8 檔案。根與沿途不跟隨連結；目錄集合、檔案身分、長度與時間前後核對。敏感普通檔仍完整掃；敏感目錄記錄命中但不進入，結果為未完整。連結、特殊項目及壓縮後綴亦非零停止。
- 產物根至少須有普通檔案 `AIAgent.Cli.dll`、`AIAgent.Cli.deps.json`、`AIAgent.Cli.runtimeconfig.json`、`appsettings.json`；存在不代表依賴齊全或互動功能已通過。

規則固定為 BUD-S001 敏感相對名稱、BUD-S002 指定的 Google Key 外形、BUD-S003 指定私鑰起始標記、BUD-S004 候選暫存名稱、BUD-S005 不合規的 `.env.example`。普通與二進位內容都檢查連續 ASCII／UTF-16LE／BE 特徵，涵蓋兩種位移及跨區塊。完整清單見規格第 7 節；不在來源或報告中放入可命中的完整合成憑證。

只有 staged 的完全小寫 `.env.example` 可使用規格限定的嚴格 UTF-8 賦值格式；值只能是空值或精確佔位文字。外層敏感目錄、其他內容規則仍生效；artifacts 仍阻擋此檔。這不改變產品工具對 `.env.example` 的拒絕。

掃描上限為 10000 項、單檔 64 MiB、總量 1 GiB、相對 path 4096 OS bytes、64 層深度；相同 blob／硬連結的不同名稱分別計量。區塊 65536 bytes、重疊 256 bytes。範例檔另限 131072 bytes、每實體行含行尾 4096 bytes、200 筆賦值；非法編碼賦值也占額度。Git 子程序各限 30 秒，診斷有界排空；不啟動 filter、hook、pager、credential helper 或 lazy fetch，並關閉繼承的 trace 目標。

| 退出碼 | 結果 |
|---|---|
| 0 | `passed`：觀察範圍完整且固定規則無命中。 |
| 1 | `blocked`：觀察範圍完整且有命中。 |
| 2 | `incomplete` 或參數錯誤：包含超限、異動、依賴／I/O／協定錯誤、取消或輸出失敗；已有命中也不能降成 1。 |

診斷只含 ASCII 固定碼、六位 item 序號、數值與摘要，不列 path、OID、內容、命中片段、原始 stderr 或 traceback。item 依本次原始相對 path bytes 排序，變更清單後不保證對應同一項目。參數解析失敗只印固定錯誤碼；stdout 失效時非零退出，不為補印錯誤再次輸出。

### 選用 pre-commit hook

提供可執行的 `.githooks/pre-commit`，只執行 staged 檢查。產品、建置、測試及掃描不自動安裝；掃描非零、缺 Python 或腳本均中止正常提交。Git 本身可能更新 index 的 stat cache；hook 不更改準備提交的 mode／OID／stage／path。

啟用前先在本機檢查有效設定及既有 hook，不將可能含個人路徑的輸出貼入報告：

```sh
git config --show-origin --get-all core.hooksPath
git rev-parse --git-path hooks/pre-commit
```

`git config --get-all` 的退出 1 表示沒有該設定；其他錯誤須先處理。檢視上述位置是否已有 pre-commit。若已有任何自訂設定或 hook，保留原內容，由使用者人工整合獨立 staged 命令；不要直接覆蓋。**只有確認無衝突後，由使用者自行執行：**

```sh
git config --local core.hooksPath .githooks
```

停用亦由使用者處理：只有設定確由這次啟用建立且目前仍為 `.githooks` 時，才移除對應本機設定；已有其他設定則還原原值，不盲目 unset。hook 可被 `--no-verify`、改設定或未安裝繞過；沒有 CI、pre-push 或遠端保證。未啟用仍須直接掃描及人工檢查。

### 發布與人工驗收

每次使用全新隔離目錄，將下列佔位路徑換成實際位置；每一步成功才執行下一步：

```sh
dotnet publish src/AIAgent.Cli/AIAgent.Cli.csproj -c Release --no-restore --no-self-contained -p:UseAppHost=false -o "/example/new-publish-directory"
python3 scripts/check_sensitive_content.py artifacts --path "/example/new-publish-directory"
dotnet "/example/new-publish-directory/AIAgent.Cli.dll" --help
```

這些命令只做本機產物驗收，不上傳、封裝或簽章；失敗保留產物並停止分發。任何內容改動須重查，另封裝後不能沿用目錄通過宣稱包內全部已掃。固定規則不識別所有秘密、分割／混淆／加密內容、圖片文字或壓縮內部，也不保證回答正確或作業系統沙盒安全。

人工門檻分開記錄日期、OS／架構、終端機／模型與通過／失敗／未驗證項目：

- 真人終端機：以假 Key／隔離假檔確認中文輸入法、貼上／刪除、長行折行、完整差異／行尾、不回顯、拒絕／取消後續回合、clear 與退出恢復。
- 真實 Gemini：使用者自行隱藏輸入真實 Key，依規格第 12.3 節逐項驗證聊天超過五回合、clear、列檔／讀檔、建檔、BOM／行尾替換、拒絕、外部衝突、取消及認證切換。只記安全分類與數值；未觸發分支填未驗證，不保存 Key 或原始 SDK／終端機內容。
- 提交／分享／分發：另檢查實際 index、未追蹤／未暫存內容、文件、PR／Issue、截圖、錄影、附件及最終包裝；掃描通過不能取代人工判斷。疑似憑證洩漏先停止分享，由使用者撤銷／更換並檢查用量，再另行確認歷史與副本處置。

### 第 07 份驗收對照

| 驗收 | 實作／測試與證據邊界 |
|---|---|
| BUD-AC-001～008 | `BudgetWorkflowTests`／`BudgetUsageTests`：真實 SDK 設定、本機指令、第五次整批停止、用量逐欄／缺值／溢位、clear／敏感重設／換 Key、完整工具歷史；保留既有逾時及用量計數溢位回歸。 |
| BUD-AC-009～012 | `BudgetBoundaryTests`／`BudgetWorkflowTests`：UTF-16、檔案／候選／參數、完整預覽、列檔／結果／快照，以及含歷史／簽章／四工具／真實寫入的 SDK body 邊界。 |
| BUD-AC-013～020 | `BudgetWorkflowTests` 加既有 `Edit*`／`Agent*`：真實磁碟與 SDK 假 handler、建立／讀取／替換、逐筆拒絕、本機結果、發布取消／故障、單一路徑清理、敏感整批；既有短 Key／拼接／認證收尾測試均保留。 |
| BUD-AC-021～033 | Python `unittest`：嚴格 CLI、完整 index／原始 blob、特殊 bytes 名稱、部分暫存雙向、刪除／重新命名、連結／未合併／gitlink／缺失物件、linked worktree／替代 index、trace／replace、編碼跨塊、範例文法與限額、異動／子程序／輸出故障。 |
| BUD-AC-034～039 | Python 真實發布目錄／Git commit 與可控競態：必要檔／二進位、敏感目錄／連結／FIFO／壓縮後綴、換身分／根／集合、唯讀、opt-in hook、安全及失敗提交、缺依賴、Git 暫時 index。 |
| BUD-AC-040～042、044 | 本節獨立命令／hook／發布限制與人工清單、完整回歸及分層結果紀錄；不把文件表格列數當測試數。 |
| BUD-AC-043 | **未通過**：2026-10-23 全新 Release 產物掃描命中 `Google.Apis.Auth.dll` 的 BUD-S003，退出 1／blocked；依規格未繼續執行該產物的 `--help`，詳見最新摘要。 |
| BUD-AC-045 | 保留全部原 PTY 組；新增中文／emoji／BOM／混合行尾的完整預覽、無效核准、第二筆拒絕／空白／Ctrl+C／EOF，核對第一筆 Verified／磁碟 bytes／用量及後續回合。原有發布前後同步取消仍執行。 |
| BUD-AC-046～048 | 真人終端機、真實 Gemini 與人工提交／分享／分發檢查尚未驗證；不得以離線、PTY 或 GUI 自動操作代替。 |
| BUD-AC-049 | **未完成**：Release 阻擋與必要人工／服務證據尚未補齊，不宣稱第三階段整體完成。 |
| BUD-AC-050 | 規格原撰寫階段的文件驗收；不是本次新增產品測試數。 |

### 第 07 份歷史驗證紀錄（2026-10-22）

以下保留 2026-10-22 第 07 份的歷史驗證，不作本次 README 維護的新驗證。當時基線為 `b8918f7` 加本機未提交變更；平台 macOS arm64，.NET SDK 10.0.100、Python 3.9.6、Git 2.54.0。第 06 份以上各節保留各自當時紀錄，不作第 07 份當次結果。以下只記安全分類與數量，未保存原始請求、憑證、命中內容或終端機轉錄；末段未修改產品、未提交等敘述亦限當時工作範圍。

| 當次命令／類別 | 退出碼與實際結果 |
|---|---|
| `dotnet restore tests/AIAgent.Cli.Tests/AIAgent.Cli.Tests.csproj` | 0；固定依賴還原成功。 |
| `dotnet build tests/AIAgent.Cli.Tests/AIAgent.Cli.Tests.csproj --no-restore` | 0；產品、測試與 host，0 警告、0 錯誤。 |
| `dotnet test --project tests/AIAgent.Cli.Tests/AIAgent.Cli.Tests.csproj --no-build` | 0；289 通過、0 失敗、0 略過，包含原 269 與新增 20 項 BUDAC 測試。 |
| `dotnet run --project src/AIAgent.Cli/AIAgent.Cli.csproj --no-build -- --help` | 0；啟動用法通過，沒有模型請求。 |
| `python3 -B -m unittest discover -s tests -p 'test_sensitive_content.py'` | 0；33 通過、0 失敗、0 略過；實際 Git／hook／產物與受控故障皆離線。`-B` 避免測試匯入留下 bytecode。 |
| `python3 tests/terminal_smoke.py` | 0；47 組通過，保留原 43 組並新增 4 組 BUD-AC-045；不是真人操作。 |
| `dotnet format src/AIAgent.Cli/AIAgent.Cli.csproj --verify-no-changes` | 0；新增 Budget 測試另以 test project 的 whitespace／include／verify-no-changes 檢查通過。 |
| `git diff --check`、新檔 no-index 空白與 C# 行尾檢查 | 0；新 C# 及變更 host 為 CRLF、無檔尾換行。 |
| `python3 scripts/check_sensitive_content.py staged`（本專案） | 0；當時實際 index 115 檔、0 命中。未暫存的本次變更不在此結果內。 |
| 同一 staged 命令（隔離 Git 的當前來源複本） | 0；128 檔、0 命中，包含本次新增／修改內容；不修改本專案 index／HEAD／hook 設定，也不等於真正提交前的 index 驗收。 |
| `dotnet publish src/AIAgent.Cli/AIAgent.Cli.csproj -c Release --no-restore --no-self-contained -p:UseAppHost=false -o /private/tmp/aiagent-stage07-release-LHENCq` | 0；全新本機 framework-dependent、非單檔輸出。 |
| `python3 scripts/check_sensitive_content.py artifacts --path /private/tmp/aiagent-stage07-release-LHENCq` | **1／blocked**；14 檔、1 命中，item=000005、BUD-S003。相依 DLL 含規定外形的連續 UTF-16 標記；不代表已確認有真實私鑰，但規格沒有 vendor 豁免，不能放行。產物保留。 |
| 同一 Release 輸出 DLL 的 `--help` | **未執行**；前一步非零，依 BUD-070 停止該次產物驗收。BUD-AC-043 尚未通過。 |
| BUD-AC-046／047／048 | 真人終端機、真實 Gemini、人工提交／分享／最終分發檢查均未驗證；x64 未驗證，未對外發布。 |
| BUD-AC-049 | 未完成：本機產物阻擋與必要人工／服務證據未齊，不宣稱第 07 份所有驗收或第三階段整體完成。 |

獨立唯讀審查找出 hook 在掃描器前直接呼叫 Git 會繼承 trace、繞過受限子程序層；先以隔離回歸重現，再移除多餘前置呼叫。Git 以工作樹根作為正常 pre-commit 的工作目錄，hook 以引用過的相對路徑呼叫 Python，由同一受限執行層完成定位。修正後 33 項 Python 回歸通過，含實際 `git commit --only` 暫時 index 及 hook trace 測試；無待處理的審查問題。

沒有修改總規格、01～07 規格、C# 產品實作、SDK／套件／設定、公開 LLM 契約、工具 Schema 或排除規則；沒有提交、推送、自動安裝 hook 或批次清理。掃描器及自動回歸已實作，但現行固定規則下的 Release 產物尚未取得通過結果；若需調整此分發限制，須另行確認規格，不能以目錄忽略或刪除相依 DLL 繞過。

## 後續階段

目前七工具、限制／用量整合測試、開發掃描器及選用 hook 均已實作，離線與 PTY 回歸已通過；第三階段整體完成仍須處理 Release 掃描阻擋，並補齊真人終端機、真實 Gemini 與人工提交／分享／分發檢查。第 08 份搜尋增量見上方專節；後續摘要、金額預算、App 或其他平台須另訂規格。
