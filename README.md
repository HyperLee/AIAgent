# AIAgent

以 .NET 10 Console 分階段開發的 Gemini Agent。目前已實作子規格 01～05：啟動與隱藏 Key、Gemini 用戶端、最近 5 個完整回合、用量累計、工作區唯讀服務，以及正式聊天的工具登錄與 Agent 循環。可透過自然語言要求受限列檔與 UTF-8 讀檔，每回合最多五次生成；真實 Gemini 與真人終端機尚未驗證。

開發依據為 [總規格](SPEC.md)、[01：啟動與 API Key](specs/01-bootstrap-and-key.md)、[02：Gemini 用戶端](specs/02-gemini-client.md)、[03：CLI 與 Context](specs/03-cli-and-context.md)、[04：工作區與唯讀工具](specs/04-workspace-and-read-tools.md) 與 [05：Agent 工具循環](specs/05-agent-loop.md)。規格中的「本次只新增文件」描述撰寫當時的交付；目前已依開發要求實作，既有規格保持原文。

## 開發進度

| 子規格 | 能力 | 目前狀態 |
|---|---|---|
| 01：啟動與 API Key | 啟動參數、工作區確認、隱藏 Key 與非機密設定。 | 已實作。 |
| 02：Gemini 用戶端 | 模型契約、SDK 轉接、請求限制、錯誤分類與資料保護。 | 已實作。 |
| 03：CLI 與 Context | 文字聊天、指令、5 回合歷史、取消、換 Key 與用量累計。 | 已實作；真人終端機與真實 Gemini 驗收尚未執行。 |
| 04：工作區與唯讀工具 | 工作區邊界、第一層列檔、完整 UTF-8 讀取、結果限量、快照及清除串接。 | 已實作，並由第 05 份接入正式聊天。 |
| 05：工具循環 | 工具 Schema、登錄、派送、配對與每回合 5 次生成上限。 | 已實作；離線與 PTY 證據見下方，真人／真實服務未驗證。 |
| 06～07 | 檔案寫入、差異預覽與確認、衝突處理及整體驗收。 | 尚未實作。 |

子規格 01～03 對應總規格第一階段「可以聊天」；04～05 已完成唯讀工具整合的離線驗證。正式只提供 `list_files`、`read_file`，不提供建立、修改或刪除檔案。第二階段的真人操作與真實 Gemini 往返仍需分別驗收；第三階段寫入與確認仍交第 06～07 份。

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

## 聊天操作

提示為 `你> `。**輸入期間不回顯；Enter 後顯示安全訊息**。不顯示星號或輸入長度；已知指令只顯示操作結果。一般訊息含當前 Key 時整筆阻擋，原文不顯示或送出。

| 操作 | 行為 |
|---|---|
| 一般文字 | 保留原始首尾空白，啟動最多五次生成的回合；生成及工具處理期間不接受下一則訊息。 |
| 空白 | 不送出，重新提示。 |
| `/help` | 顯示聊天指令與目前能力；啟動參數 `--help` 只顯示啟動用法。 |
| `/clear` | 清除對話、讀取快照及最近本機結果，保留 Key、工作區、模型及程序累計。 |
| `/exit`、EOF、Ctrl+D | 正常退出；部分輸入會丟棄。 |
| 等待輸入／認證選擇／Key 輸入時 Ctrl+C | 退出 130。 |
| 生成／工具處理中 Ctrl+C | 取消並等待本回合收尾，再回到輸入；不取消後續聊天。 |
| 生成／工具處理中 Ctrl+D | 取消並等待收尾，正常退出。 |

指令在 Trim 後區分大小寫且完整匹配；`/CLEAR`、`/help extra`、`/tmp/a` 皆為未知指令，不送模型。生成、工具處理、取消及收尾期間的普通按鍵（包含 `/exit`）會丟棄，不能排隊；外部 SIGINT 依相同聊天狀態路由。

每行最多 16384 個 UTF-16 單位，補充平面字元計 2。支援中文、Unicode scalar 刪除、Backspace／Delete；導覽鍵不移動游標。控制字元、非法代理字元及 Unicode 行／段分隔字元會使整行無效，超限後刪除也不恢復有效。CRLF 視為一次 Enter；沒有多行編輯或輸入歷史。

只有合法完成的回合才保存：使用者訊息、零或多組完整模型呼叫／工具結果，以及最終模型回答；最多保留 5 個完整回合。第 6 次請求仍包含前 5 回合，成功提交後才淘汰最舊回合。拒答、取消、截斷與一般失敗不加入歷史；截斷明示「回答未完整產生」。敏感模型紀錄、非法協定／配對或無法安全回填結果時，清除全部 Context 與讀取快照並通知。`/clear` 只釋放記憶體參考，不清除終端機捲動紀錄。

認證失敗時先完成舊政策下的錯誤與用量顯示、釋放舊 client，再詢問 `重新輸入 Key？[y/N]`。隱藏輸入 y／yes 後重新讀取 Key；成功切換新的 session／client 時清空對話、保留累計並等待新的訊息，不重送失敗需求。模型、權限、配額、網路與逾時錯誤各自分類，不誤當成 Key 問題。

每回合顯示生成方法呼叫數、回合傳輸嘗試數、「本次用量」與「程序累計」，欄位依序為輸入／輸出／思考／快取／總計。明確零值保留，缺值或負值標示不可得；部分資料顯示已知小計及缺值次數。溢位保留最後可表示的小計並永久標示不完整。Total 採 API 原值，thought／cached 不再加進 Total。傳輸嘗試不證明 Google 已收到或計費，本機統計不是正式帳單。

參數或設定錯誤退出 2；終端機、I/O、不可恢復本機或清理失敗退出 1。清理失敗優先於正常／取消退出；明確程序取消優先於 EOF。API 回合失敗本身通常回到輸入，沒有自動重試。

## 模組與後續使用方式

| 模組 | 本階段責任 |
|---|---|
| Core | `ChatApplication` 狀態機、`AgentRunner`、完整回合驗證、`ConversationContext` 與用量累計；新增契約為 internal，不依賴 SDK 型別。 |
| Llm | session 綁定的 `GeminiClient`、SDK 轉換、受控 HTTP 傳輸、協定保存、回應與錯誤分類。 |
| Tools | 固定唯讀 Schema、Registry、嚴格參數解析與安全結果轉接。 |
| Workspace | 依固定工作區及當前政策驗證路徑、種類、連結與內容；有界列檔／讀取、明確 JSON 投影及記憶體快照。 |
| Security | 當前 Key 的 Ordinal 完整子字串檢查、整次拒絕、顯示遮蔽與已知憑證欄位遮蔽。 |
| Configuration／Cli | 嚴格設定、固定工作區、隱藏輸入、單一終端機讀取者、busy 控制觀察及安全純文字顯示。 |

`AgentRunner` 建立 `LlmRequest(SystemInstruction, Messages, Tools)`，以 `ILlmClient.GenerateAsync(request, cancellationToken)` 取得 `LlmResult`。文字訊息使用 `LlmMessage.FromUserText`；工具結果使用 `FromToolResults`。正式請求固定依序提供 `list_files` 與 `read_file`，所有工作區 I/O 都經過既有服務。

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

以下保留 2026-10-20 第 03 份子規格實作完成時的歷史驗證紀錄。當時後續的 README 維護只檢查文件；本次第 05 份實作的驗證另列，不沿用下表作為新結果。

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
| WS-AC-025～026、030 | `ChatWorkspaceTests` 及完整既有回歸：真實 ChatApplication 的清除／換 Key／建構失敗清理；第 05 份改為兩個唯讀 Tools，清除／換 Key 仍不額外生成，既有 Gemini 請求限制保留。 |
| WS-AC-029 | `WorkspaceReadIntegrationTests`：真實暫存工作區逐檔比較 bytes 與項目，檔案內假指令不觸發額外操作。 |
| WS-AC-031～034 | 既有 PTY 另記；真人終端機未驗證。真實工具往返與寫入交接留待第 05～07 份。 |

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

正式只登錄 `list_files`、`read_file`，兩者皆要求唯一的 string `path`，根目錄使用 `.`。模型可直接聊天；要操作檔案時，必須提出原生 function call。Markdown、檔案文字或模型附帶文字不會觸發隱藏操作。

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
| LOOP-AC-038～040 | 真人終端機、真實 Gemini 未驗證；寫入／預覽／確認／衝突與最終發布留待第 06～07 份。 |

### 第 05 份實作驗證紀錄（2026-10-21）

以下為本次實作後在 macOS arm64、.NET SDK 10.0.100 的實際結果。原有 160 項回歸與新增 56 項測試案例合計 216 項；這是測試執行數，與 LOOP-AC-001～036 的驗收編號數分開。

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

## 後續階段

第 04～05 份已完成唯讀服務與工具循環的離線整合；第 06～07 份接續寫入確認、衝突與整體交付。本階段不提供寫入工具，也沒有新增本機讀檔指令。

**真實 Gemini API 未驗證。** CLI-AC-033～035 需使用者另外操作。自動測試與 PTY 不取得真實 Key，也不證明服務可用、回答正確或已讀寫檔案。
