# 18：MCP 外部工具整合開發規格書

## 產品實作接續（2026-10-28）

本輪依使用者指示進行後續產品實作；下列 MCP-001／099 等「本次文件」限制保留為規格編製階段歷史，並非本輪產品變更清單。固定 SDK／協定、兩工具、安全傳輸與簽發權威、完整新確認、跨功能戳記及生命週期已接入正式組裝。公開 LLM／SAVE v1 與原生能力邊界保持，六筆上游及十三筆前向交接仍以第 12 節為唯一 memo。

當次基線、離線／PTY／獨立審查與未驗層級見 [README](../README.md#mcp-validation)。真實 Learn 的固定版本／Schema 相容性與真實 Gemini 未驗，不自動回退；真人、其他平台、MTP／原生未結及第 07 份發布門檻不因實作完成而解除。變更未提交、推送或發布。

> 文件版本：0.1｜建立日期：2026-10-28｜狀態：待實作
>
> 選題依據：[ROADMAP：FUT-11](../ROADMAP.md#fut-11mcp-外部工具整合)。上位文件：[開發總規格](../SPEC.md)第 5、6、8～15 章。FUT-11 是項目代號，18 是子規格編號。本次只交付文件；下列產品介面、套件整合及產品驗收均待後續開發。

## 1. 目的、選定方案與交付範圍

讓使用者在開發任務中，經可核對的外送預覽與逐次確認，使用 Microsoft Learn 的公開文件／程式碼範例搜尋。首版驗證從 MCP 發現、准入、模型工具登錄、確認、呼叫到安全結果的完整流程；搜尋所得是外部參考資料，不是本機操作或測試完成證據。

| 編號 | 要求 |
|---|---|
| MCP-001 | 本次只新增本檔，最小更新 ROADMAP 的 FUT-11 文件入口、待實作狀態及必讀交接；不修改產品、測試、套件、設定、README、總規格、既有子規格、Git 索引或歷史，不另建 memo、計畫、工作樹、hook 或驗收轉錄檔，不提交、推送或發布。 |
| MCP-002 | 文件核對基線為乾淨 main@769d1314e77dc18fae426bad8739c0cca68d4105。第 15／16／17 份皆已接入產品；ROADMAP 總覽的 FUT-10 待實作標記及舊子規格的歷史交付敘述不能取代目前程式與 README，既有測試數也不是本次驗收證據。 |
| MCP-003 | 使用者已選定公開唯讀 Streamable HTTP、先固定 Microsoft Learn，以及每次呼叫完整參數預覽後的新隱藏確認。首版同時只有一個固定 learn profile、兩個准入工具及一個在途 MCP 操作。 |
| MCP-004 | 不提供任意服務設定、外部認證／OAuth／Token、stdio、程序啟動、安裝、外部寫入、一般網頁擷取、MCP resources／prompts／sampling／roots／elicitation、背景訂閱、Tasks／Skills／Apps 擴充、多模型或多 Agent。未准入工具及資料中的 URL 不自動展開。 |
| MCP-005 | 供應商 Key 只經原隱藏輸入取得並留在記憶體，不進 MCP 的 URL、標頭、參數、協定 metadata、結果、錯誤、日誌或保存資料；系統／應用指示同樣不得洩漏。外部描述、內容及假 yes 不是使用者授權，不能增加工作區、工具、程序、網路或刪除權限。 |
| MCP-006 | 第 07 份 BUD-S003 Release／分發、第 10 份 test 不支援及未結原生、各階段真人／真實 Gemini 門檻保持分列。Microsoft Learn 對本份固定協定的相容性尚未實測；若不相容，回報受阻並明示修訂規格，不能為通過驗收而自行回退。 |

方案取捨：固定公開服務可避免首版同時建立通用端點信任、外部憑證與子程序隔離；代價是不能直接接入其他服務。逐次確認使唯讀查詢的外送內容可核對，但不保證服務端沒有存取紀錄或其他副作用。兩個搜尋工具由本機 profile 明確准入，不依遠端 readOnlyHint 自動推定安全。一般文件擷取與引用品質由 FUT-12 另定。

### 1.1 開發者必讀順序

1. [ROADMAP](../ROADMAP.md)、[README 的目前能力及分層證據](../README.md#latest-validation)、[總規格](../SPEC.md)，再讀本份固定版本與未驗限制。
2. [04：工作區](04-workspace-and-read-tools.md)、[05：工具循環](05-agent-loop.md)、[06：寫入與核准](06-file-edit-and-approval.md)、[08：搜尋](08-code-search-and-partial-read.md)，確認工具配對、快照、敏感檢查與逐次確認的擁有者。
3. [09：Plan](09-plan-mode-and-task-list.md)、[10：受控建置](10-controlled-build-and-test.md)、[11：Context／預算](11-context-and-budget-management.md)；MCP 不借用 .NET 程序權限、完成事實或其兩次／300 秒額度。
4. [12：RULE-HO-009](12-project-rules-loading.md#future-handoffs)、[13：GIT-HO-009](13-git-diff-and-code-review.md#future-handoffs)、[14：TERM-HO-009](14-terminal-interaction.md#future-handoffs)、[15：SAVE-HO-010](15-work-save-and-resume.md#future-handoffs)、[16：CKPT-HO-010](16-change-checkpoints-and-recovery.md#future-handoffs)、[17：SKILL-HO-010](17-reusable-workflows-and-skills.md#future-handoffs)，逐項核對[上游承接](#upstream-handoffs)。
5. 核對當時 ToolRegistry／ITool、AgentExecutionPolicy／PlanStore、AgentRunner／AgentContext、ChatApplication、ContextOperationStamp／ContextMetricStamp、GeminiProtocol／GeminiContextTools、Rules／Skills Coordinator、WorkSessionCoordinator 及 CheckpointRecoveryState；不得將實際指示、Key、原生歷史或真實協定轉錄附入文件。

## 2. 架構、最小內部介面與資料所有權

以下是後續產品的最小 internal 契約，不是本次加入的程式碼，也不是通用外掛架構。

| 元件／型別 | 責任及必要資料 |
|---|---|
| McpCoordinator | ChatApplication 與 AgentExecutionPolicy 共用單一實例；提供 Capture、ConnectAsync、ReloadAsync、DisconnectAsync、GetTools、InvokeAsync、MarkStale、Settle 及 DisposeAsync。只有本機 CLI 可連線、重載及斷線；模型只能要求已准入的 InvokeAsync。 |
| IMcpReadClient／McpReadClient | 專用 Mcp/ 轉接層擁有 SDK client、受限 HttpClient 及串流；提供 DiscoverAsync、ListToolsAsync、CallAsync 與 DisposeAsync。CallAsync 必須消費 Coordinator 發出的單次 McpCallPermit，不接受任意端點、標頭、SDK tool 物件或裸參數旁路。 |
| McpProfile | 固定 ServerId=learn、端點、協定、兩個遠端工具名稱、本機名稱映射及資源限制；為程式內建的非秘密政策，不從工作區、AGENTS、SKILL、存檔、環境或遠端文字載入。 |
| McpCatalogSnapshot | 不可變准入目錄：兩個工具的遠端／本機名稱、完整安全描述、准入 Schema、原始描述指紋、CatalogRevision、Scope 與規則身分；沒有可執行 SDK 物件或認證。 |
| McpOperationStamp | SessionGeneration、McpGeneration、CatalogRevision、Status、快照身分及 Rules／Scope 身分；實際呼叫候選另綁 Skills、Plan、RecoveryRevision、取消代次、工具名及完整參數。版本不是授權物件。 |
| ToolRegistrySnapshot | 原十三項定義加目前准入 MCP 定義的不可變有序集合與 MCP 戳記；同一請求／模型回應／整批派送使用同一快照，並在派送時核對目前能力。 |
| McpApprovalPreview／McpCallPermit | 預覽含固定目的地、工具來源、目錄版本、完整安全參數及外送說明；Permit 另含該次新確認、全套戳記及單次消費狀態，僅在記憶體有效。 |
| McpCallOutcome | 受控 status／error_code、NotSent／SentUnconfirmed／ResponseReceived、本機安全結果是否已採用、可空安全結果與數值統計；不含原始例外、標頭、協定物件或認證。 |

| 編號 | 契約要求 |
|---|---|
| MCP-007 | ChatApplication 擁有單一 Coordinator、輸入及資源生命週期；Coordinator 擁有准入、版本與確認；Mcp/ 擁有 SDK／HTTP 細節；Runner 擁有生成、工具配對及結果採用。原 Workspace、Plan、Context、Rules、Skills、Git、SAVE 及 checkpoint 保留各自所有權。 |
| MCP-008 | 固定使用 ModelContextProtocol.Core 2.3.0；後續產品開發才新增固定版本相依，不採浮動版本或預先加入其他 MCP 擴充套件。MCP SDK 型別不逸出 Mcp/，Google SDK 型別仍限 Llm/；不新增第二個模型 client 或 Microsoft.Extensions.AI 生成入口。 |
| MCP-009 | 公開 ILlmClient、LlmRequest、LlmResult、LlmMessage 及原十三項工具的輸入／成功資料契約保持。新增 internal ToolCapability.ExternalRead、MCP 停止原因、預覽及安全結果投影；不能用 WorkspaceRead、GitRead 或 DotnetTask 冒充外部能力。 |
| MCP-010 | 含文字／JSON／集合的新資料採密封型別、唯讀屬性、防禦性複製及固定 ToString 標記；JSON 克隆所有權，不一般序列化服務、快照、Permit 或 SDK 物件。 |
| MCP-011 | 同一 ChatApplication 只允許一個 MCP 命令或呼叫在途，第二個入口立即受控拒絕、不排隊。不得持決策鎖等待網路、終端機或工作區 I/O；不讓 SDK 回呼直接讀鍵、顯示遠端原文或修改應用狀態。 |
| MCP-012 | 呼叫期間借用原 WorkspaceOperationCoordinator 排他租約重驗目前規則／技能及工作區；已持租約者借用同一租約，不巢狀取得。遠端等待不授予檔案讀取權，取消／Dispose 等自有工作與串流關閉後才釋出租約。 |
| MCP-013 | MCP 不呼叫公開 ReadFileAsync，不建立或刷新 read_file 快照、Plan 操作事實、GitSnapshot 或 checkpoint。SDK 回傳 HTTP 成功、isError=false 或外部 Verified 字樣均不取得本機完成資格。 |
| MCP-014 | 原敏感政策延伸到 MCP 邊界及組合投影；Mcp/ 只能取得用途限定的檢查能力，不能取得 Key／完整指示的值、BootstrapSession、聊天 Context、工作區來源或模型原生紀錄。保護指示的比對留在 Core／Security，測試使用合成標記，不傾印實際指示。 |

## 3. 固定協定、連線與網路邊界

本份採 [2026-07-28 協定](https://modelcontextprotocol.io/specification/2026-07-28)、[Discovery](https://modelcontextprotocol.io/specification/2026-07-28/server/discover)及[Streamable HTTP](https://modelcontextprotocol.io/specification/2026-07-28/basic/transports/streamable-http)。文件中的「連線」指本機已接受 profile／目錄與持有 client 的狀態，不是協定 session。

| 編號 | 協定／目的地要求 |
|---|---|
| MCP-015 | ProtocolVersion 精確固定 2026-07-28、TransportMode 明確為 StreamableHttp，停用 AutoDetect、legacy SSE、initialize／initialized 回退及隱式協定協商降版。服務未支援或 SDK 無法遵守時為 mcp_protocol_unsupported，不改用另一個端點或手寫較寬鬆傳輸。 |
| MCP-016 | connect／reload 明確執行 server/discover，確認固定版本及 tools 能力，再取得完整有界 tools/list；請求攜最小 clientInfo（固定名稱 AIAgent、產品版本）與空 clientCapabilities。每個 RPC 的版本、必要 _meta 及 MCP-Protocol-Version／Mcp-Method／Mcp-Name 等標頭由固定 SDK 按該版規範產生並核對，不把本機名稱當遠端 tool name。 |
| MCP-017 | 只使用 server/discover、tools/list、tools/call；不開 GET 接收通道、協定 session、Last-Event-ID、訂閱或背景 keepalive。一般 HTTP 連線重用不是 MCP 授權／session；多餘 session 標頭不採用，服務若要求舊 session 流程即不支援。 |
| MCP-018 | 生產端點精確為 https://learn.microsoft.com/api/mcp，僅 HTTPS、預設 443、固定 host/path，無 userinfo、query、fragment 或替代 IP／主機。每次實際送出前核對目的地、正常 TLS 憑證及主機驗證，不允許 callback 放寬驗證或遠端決定下一站。 |
| MCP-019 | 專用 HttpClient 停用 AllowAutoRedirect、UseCookies、UseDefaultCredentials 及 UseProxy；不繼承環境／系統代理、瀏覽器登入或其他 HttpClient 的預設標頭。非預期重新導向、401／403／407 均受控失敗，不跟隨 Location、登入網址或認證挑戰。 |
| MCP-020 | 不傳 Authorization、Cookie、外部 Token、自訂應用標頭、工作區／使用者識別或對話 ID。只傳標準 HTTP、固定 SDK 必要協定 metadata 及已核准工具參數；遠端要求的額外標頭或由參數映射標頭的 Schema 擴充均不支援。測試可注入假 handler，但生產組裝不得提供可換端點／handler 的使用者旁路。 |
| MCP-021 | serverInfo 可缺省、其名稱／版本只為自報 metadata，不作端點身分、權限或白名單依據。instructions、工具 annotation、icon、URL、_meta 均是不可信資料；不合併為固定指示、規則或技能，不自動抓取任何參照。 |
| MCP-022 | clientCapabilities 不宣告 sampling、roots、elicitation 或擴充；不提供回呼與本機根目錄。遇 input_required、遠端發起 JSON-RPC request 或要求 client 輸入時停止，不提示使用者輸入憑證、不傳 inputResponses／requestState、不續跑 MRTR。 |
| MCP-023 | 支援每個 POST 回應的 application/json 及 text/event-stream；SDK 負責 JSON-RPC 配對及 SSE 解析，外層串流仍施加原始 bytes／時間上限。進度／log 文字不直接顯示、不中途進模型；SSE comment 可忽略但仍計 bytes，通知不能延長截止時間。 |
| MCP-024 | 禁止 SDK／HTTP resilience 自動重試、HeaderMismatch 後刷新重送、SSE 重連、工具失敗重播或偷偷降版；同一工具要求最多送一次 tools/call。對不相容服務只回報固定碼，不能為取得可用結果解除本節。 |

## 4. 工具發現、命名與 Schema 准入

Microsoft Learn 官方列有三項工具且提醒介面可變；本份刻意只准入其中兩項，並不宣稱下列合成 Schema 是目前真服務回報。[官方工具參考](https://learn.microsoft.com/en-us/training/support/mcp-developer-reference)

| 遠端名稱（Ordinal） | 模型可見本機名稱 |
|---|---|
| microsoft_docs_search | mcp__learn__microsoft_docs_search |
| microsoft_code_sample_search | mcp__learn__microsoft_code_sample_search |

| 編號 | 目錄／Schema 要求 |
|---|---|
| MCP-025 | tools/list 有界讀到無 nextCursor 才形成候選；最多 4 頁、32 個工具，重複名稱／cursor、循環、缺頁、超限或不合法結構整次拒絕，不發布部分目錄。下一頁只傳回該次 opaque cursor，不把它當 URL 或顯示／保存。 |
| MCP-026 | 候選必須含上表兩項且各一份，否則 mcp_catalog_invalid；其餘名稱不登錄、不產生定義或呼叫入口，只顯示排除數量。microsoft_docs_fetch 即使唯讀亦排除，不能藉它提前實作 FUT-12。 |
| MCP-027 | 描述、名稱、Schema 與整份原始目錄先做嚴格 UTF-8、JSON 重複欄位、控制字元、敏感及容量查核；不以 SDK dictionary 的最後值覆蓋取代原始查核。不下載 icons 或執行 description／instructions。 |
| MCP-028 | 首版 inputSchema 僅支援根 object、properties（1～16 個）、required 及 boolean additionalProperties；屬性值只支援 string 或 string/null 聯集、enum、minLength、maxLength。title、description、default 為惰性 annotation，default 不自動補值；$schema 僅接受缺省或 https://json-schema.org/draft/2020-12/schema。拒絕未知／重複關鍵字、$ref、組合／條件、pattern／format、巢狀物件／陣列、數值參數及 x-mcp-header 等擴充，不刪除關鍵字後繼續。 |
| MCP-029 | 屬性名為 1～64 ASCII 字元且符合 [A-Za-z_][A-Za-z0-9_]*；required 無重複且只指已定義屬性。enum 最多 32 個不同值，值須符合該屬性型別／長度；default 若有亦須合法。長度約束是非負整數且 min 不大於 max，JSON Schema 長度以 Unicode scalar 計。另施加本機每字串最多 1024 UTF-16 單位及參數 JSON 上限；所有參數均須在 properties，未知欄一律拒絕。送模型的 additionalProperties 固定 false，此為明示收緊，不放寬遠端約束。 |
| MCP-030 | 對准入兩項的完整原始工具描述做決定性的 JSON 正規化：object key 按 Ordinal 排序、保留 array 順序與值、不正規化文字，再以 SHA-256 計指紋。比較原始描述、准入後定義及固定 profile／協定／規則身分；描述、Schema、annotation 或其他該工具 metadata 改變皆視為變動。未准入工具的普通增減只改當次排除數，不撤銷目前准入；非法／敏感或超限仍整次失敗。 |
| MCP-031 | 首次 connect／reload 候選由兩次完整 tools/list 一致觀察後原子接受；有界重讀是查核，不是失敗重試。不一致即 Stale 或初次連線失敗，不取其中一份較舊結果。每次真正工具呼叫前、取得新 yes 後也各重取完整目錄，必須與 Ready 快照相同；不自動接受新 Schema。 |
| MCP-032 | 固定本機映射不接受遠端自訂別名、大小寫轉換、截斷、雜湊命名或覆蓋內建工具；兩個名稱長度分別為 33、40，均在本份 64 ASCII 上限內。外部同名 read_file／git_status／技能名稱不能取得內建身分。 |
| MCP-033 | ToolRegistry 以當前已接受快照追加兩項，保留內建十三項相對順序，外部追加順序固定 docs_search、code_sample_search。Ready 時全定義 15 項，Plan 11、Execute 14、Review 3；Disconnected 時為原 13／9／12／3。Stale 不提供可用的外部定義並阻擋新一般生成，直到明確 reload 或 disconnect。 |
| MCP-034 | 發給模型的定義含固定 external_mcp／learn 來源標記、安全遠端描述及准入 Schema，不傳 server instructions。一份模型結果只能按產生它的 RegistrySnapshot 解讀；舊歷史工具名稱及完整配對可保留，但不能用舊定義派送目前呼叫或從歷史重建登錄。 |

以下三個 JSON 均是合成範例，不是服務轉錄、可直接執行命令或已通過相容性的證據。Schema 示範本份允許的子集，不固定真服務參數名稱。

```json
{
  "type": "object",
  "properties": {
    "query": {
      "type": "string",
      "description": "Search public documentation.",
      "minLength": 1,
      "maxLength": 1024
    }
  },
  "required": ["query"],
  "additionalProperties": false
}
```

```json
{
  "name": "mcp__learn__microsoft_docs_search",
  "arguments": {"query": "C# cancellation token"}
}
```

## 5. CLI、服務狀態與操作呈現

| 本機指令 | 固定行為 |
|---|---|
| /mcp | 顯示 Disconnected／Ready／Stale、固定服務／端點、CatalogRevision、上次安全觀察及停止原因；零網路 I/O，不保證當下遠端仍相同。 |
| /mcp connect learn | 只由 Disconnected 啟動固定 profile 的 discovery 與雙重完整目錄查核；接受後 Ready，撤相關確認並等待新指示。不是呼叫工具的授權。 |
| /mcp tools | 顯示已接受的兩項本機／遠端名稱、完整安全描述、准入 Schema、版本／指紋及排除數；零網路 I/O，Stale 明標歷史快照，Disconnected 回 mcp_not_connected。 |
| /mcp reload | 在 Ready／Stale 下重新 discovery 及雙重目錄查核；成功才接受候選或 no_change，失敗保留不可呼叫的 Stale；Disconnected 回 mcp_not_connected。 |
| /mcp disconnect | 撤候選、取消並等自有工作關閉，丟 client／目錄／Permit，回 Disconnected；不改遠端資料、不刪本機檔案或開始模型回合。 |

| 編號 | CLI 要求 |
|---|---|
| MCP-035 | 遵守全域多行命令閘門後才解析；Trim 外層空白、名稱大小寫敏感、connect 與 learn 間恰一個 ASCII 空格。錯誤大小寫、URL／path／其他 ID、多餘參數或多行回 invalid_mcp_command，零網路／狀態變更，不轉成模型訊息。 |
| MCP-036 | 五個命令只在 AwaitingInput 受理；其他時間沿 busy 單一輸入讀取者與預輸入排空，不佇列後續命令。命令不入安全輸入歷史、Context 或工作存檔，零 GenerateAsync／API 用量，不重置預算。 |
| MCP-037 | connect 前展示固定目的地、只提供公開搜尋、查詢仍會外送、無認證及後續逐次確認；使用者明確輸入 connect learn 即授權最小 discovery metadata，無需再問一次 yes。connect／reload／disconnect 均不替使用者啟動下一回合。 |
| MCP-038 | Ready 再 connect 回 mcp_already_connected，Stale 再 connect 回 mcp_catalog_stale，零網路，指向 reload／disconnect。Disconnected 再 disconnect 回 no_change；Ready 的 reload 只有完整觀察與適用身分相同才 no_change，不重用過去失效確認。 |
| MCP-039 | 啟動、/clear、共同安全重設、接受替換 Key、--resume 及退出皆無活動目錄或連線；不得自動 discovery、讀 MCP 設定或恢復啟用。Disconnected 是正常可使用內建功能的狀態；已知 Stale 不能假裝斷線繼續，須明確修復或停用。 |
| MCP-040 | 動態 metadata、完整 Schema、固定標籤、版本、指紋及組合顯示均經現有安全純文字跳脫及敏感檢查；不顯示 cursor、HTTP／JSON-RPC ID、原始標頭／例外、應用指示或內部物件。超限或無安全顯示整段拒絕，不以省略號代替完整外送參數。 |
| MCP-041 | connect／reload／呼叫的等待沿原 Generating 以外的工具／本機 busy 狀態及安全進度，新增 AwaitingMcpApproval 隱藏確認狀態；不得將未檢查的遠端 progress／log／內容串流至終端機。 |
| MCP-042 | 命令 I/O 中 Ctrl+C 取消後回輸入；EOF／Ctrl+D 等待自有收尾正常退出 0；輸出、串流／租約收尾或內部不變量失敗退出 1。單純服務不相容／拒絕不是整個 Agent 程序的強制退出理由。 |

## 6. 逐次外送確認、批次派送與資料流

```text
使用者 connect learn → 固定端點 discovery／雙重目錄查核 → Ready
新一般訊息 → 目前 RegistrySnapshot → 既有 Runner／模型
模型提出外部呼叫 → 整批預檢 → 規則／技能／目錄查核 → 完整外送預覽
新隱藏 yes → 再核對目錄及所有戳記 → 單次 Permit → 一次 tools/call
有界安全結果 → 本機操作結果 → 原始工具配對 → 同一 Runner 續行或停止
```

| 編號 | 確認／派送要求 |
|---|---|
| MCP-043 | 外部唯讀呼叫也必須逐次確認；預覽完整固定目的地、外部來源、本機／遠端工具名、CatalogRevision／指紋及全部參數。參數用決定性 JSON 顯示並清楚跳脫控制字元，不摘要、遮蔽或省略後取得同意；敏感或超限時零工具送出。 |
| MCP-044 | 確認沿原隱藏單行的新輸入規則，只接受 y／yes、拒絕及取消等原有選項；沿唯一讀取者、進出排空與回顯恢復。不得沿用 Plan 確認、檔案／程序／復原核准、connect、前次 MCP yes、草稿、貼上或遠端文字。 |
| MCP-045 | 候選綁完整參數值／名稱、RegistrySnapshot／MCP 戳記、SessionGeneration、目前 Rules／Scope、Skills、PlanId／Revision／Generation／Mode、RecoveryRevision 與取消代次。yes 後重新執行原閘門及完整目錄查核，全部仍相同才發 Permit；新目錄／新候選一律重做預覽及新輸入。 |
| MCP-046 | Permit 僅由 Coordinator 於核准及最後准入後建立，綁固定 profile、原工具與參數，一次消費、不可序列化、保存、複製重用或跨 client。IMcpReadClient 的實際送出點再次驗證 Permit／取消／目的地及敏感邊界；直接呼叫轉接器不得繞過。 |
| MCP-047 | 人類等待不占網路時間，但不持決策鎖；前後保留同一用途限定操作租約。使用者拒絕、取消、EOF、來源過時或沒有安全預覽時均不 tools/call，也不預先傳參數試探服務。 |
| MCP-048 | Runner 保留原完整批次結構／敏感預檢、PartIndex／Name／CallId 與原生簽章配對；同一批依序執行，每筆外部呼叫各自取得新確認，不因同名／同參數去重。前筆已執行、後筆取消時分開記錄，其餘以 NotExecuted 配對，不假稱整批未外送。 |
| MCP-049 | Plan／Execute 的 ExternalRead 均可查詢，但既有 Rules／Skills／Plan 執行准入仍成立。Review 一律 mode_restricted、零 MCP I/O；/compact 與工作保存摘要不提供 MCP 工具，也不讓 SDK 透過 sampling 生成。 |
| MCP-050 | 實際送出僅含已核准參數與最小協定 metadata；不自動附使用者訊息、pins、規則／技能正文、差異、檔案、工作區根、模型歷史或固定指示。模型若要求傳這些受保護資料，仍先套用硬政策，不因人類確認而解除 Key／指示保護。 |
| MCP-051 | 執行前及模型結果採用前核對快照／戳記；若在本批中因其他已完成操作造成 Rules／Skills／Plan／Recovery 失效，先保留該本機操作的事實，再停止外送及剩餘工作。目錄版本不能取代原工作區、規則或技能重驗。 |
| MCP-052 | 外部呼叫普通參數錯誤走既有 invalid_arguments，零外送；已進入 MCP 流程的拒絕確認、連線／目錄／協定／結果／容量失敗則停止本回合其餘工具及生成，不讓模型當場重試。保留可安全配對的固定失敗結果；無安全模型紀錄則沿共同重設。 |

## 7. 安全結果、觀察狀態與固定限制

| 編號 | 結果要求 |
|---|---|
| MCP-053 | 只接受與目前請求正確配對、resultType=complete 的工具結果；缺省 resultType 按固定 SDK 的協定相容解析視為 complete，不據此切換協定。isError 缺省為 false；true 回 mcp_remote_error，不將其 content 當成功。未知 resultType、input_required、錯誤 ID、多個 final 或混合 request／response 拒絕。 |
| MCP-054 | 成功 content 僅接受 type=text 的項目，最多 32 項；可有可無 structuredContent，但若有須為安全 JSON object、深度最多 8，禁止重複欄位及非有限數值。圖片、音訊、嵌入 resource、resource_link 或其他種類整份回 mcp_result_unsupported，不部分保留文字或自行抓 URL。至少一項非空文字或非空 structuredContent；不重新解碼文字內的 JSON、HTML、命令或跳脫來觸發操作。 |
| MCP-055 | 明確 writer 產生下例 envelope；data 只含固定來源、服務、遠端工具、目錄版本、原次序安全 text 與可空 structured_content。沒有本機 path、Verified、TestsPassed、GitSnapshot 或 checkpoint 欄位；不轉寄 annotations、_meta、原始標頭及錯誤訊息。外部 URL 若出現在文字／JSON 中仍為惰性參考，不宣稱已驗證來源內容。 |
| MCP-056 | 送出／讀取前後檢查名稱、Schema、所有 JSON key／字串、固定 metadata、實際傳輸內容及組合投影；合成短 Key、跨欄拼接及指示碰撞均整份拒絕，不傳遮蔽後的半份結果。原始錯誤本文只可於有界記憶體做拒絕判斷，不出現在模型、CLI、日誌或存檔。 |
| MCP-057 | outcome 區分 NotSent、SentUnconfirmed、ResponseReceived，另記 SafeResultAccepted。進入不可保證未送出的 HTTP 送出階段即保守標 SentUnconfirmed；只有完整且配對的回應才 ResponseReceived。安全投影失敗或取消後收到結果不能標安全成功；success 只代表這次安全協定結果，不證明遠端內容、唯讀實作或業務狀態。 |
| MCP-058 | 每一般回合最多 2 次 tools/call 啟動機會，各 30 秒累計主動網路／解析時間，含前後目錄查核及工具往返、排除人類確認等待；進入 CallAsync 前保守扣次，失敗或未能確認送出不退款。connect／reload 每次整體 20 秒；DNS／連線個別最多 10 秒且受剩餘總額度約束，progress 不重設計時。 |
| MCP-059 | metadata 查核不扣 tools/call 次數，但每次查核最多 4 頁且失敗不重試；connect／reload 是一次 discovery 加兩次完整目錄觀察，每個工具流程是預覽前與 yes 後各一次完整目錄觀察。沒有其他隱藏輪詢、生成或無界工作。 |
| MCP-060 | 網路標頭每回應最多 64 個欄位、總 16384 bytes；每個回應 body／完整 SSE 串流最多 262144 bytes，每份跨頁目錄合計亦最多 262144 bytes。以有界串流讀到上限加 1 判超量，不能先 ReadAsStringAsync 無界配置；停用自動解壓縮且只接受 identity Content-Encoding，缺 Content-Length 亦逐次計數。 |
| MCP-061 | 每項准入 Schema compact JSON 最多 8192 bytes、遠端描述最多 2048 UTF-16 單位；完整兩項模型定義及來源狀態投影各最多 32768 UTF-8 JSON bytes。參數 compact JSON 最多 8192 bytes，成功 data 最多 32768 bytes、完整工具 Response 最多 65536 bytes、完整 CLI 區塊最多 65536 UTF-8 bytes；所有 envelope、跳脫、Unicode 及固定欄位皆計入，各自獨立拒絕，不截斷或借額度。 |
| MCP-062 | 使用 checked 計數；新 MCP bytes／次數／時間統計與生成 token／程序預算分列，不猜服務費用。模型請求估算仍含全部定義／結果／附錄，實際 SDK body 維持 262144 bytes；不加生成、隱藏 client、額度退款或以移除必要附錄補送超限請求。 |

```json
{
  "status": "success",
  "error_code": "none",
  "data": {
    "source": "external_mcp",
    "server_id": "learn",
    "remote_tool": "microsoft_docs_search",
    "catalog_revision": 1,
    "content": [{"type": "text", "text": "Synthetic documentation result."}],
    "structured_content": null
  },
  "truncated": false,
  "safety_filtered": false
}
```

失敗沿既有 status／error_code 固定 envelope，不帶 data、原始參數、遠端錯誤片段或認證挑戰。傳輸狀態另為本機安全 outcome，不塞進 SDK 原始協定或冒充任務證據。

## 8. 版本、重驗、原子發布與生命週期

| 編號 | 狀態／生命週期要求 |
|---|---|
| MCP-063 | 本機狀態固定 Disconnected（無可用目錄）、Ready（已接受安全快照）、Stale（已知失效或不能安全重驗）。Stale 阻擋一般／摘要／保存生成與 /execute 確認，直到明確 reload 成功或 disconnect；Review 固定工具且零 MCP I/O，但仍不得採用已過時的共同候選。 |
| MCP-064 | CatalogRevision 初始 0；每次首次接受、失效後重新接受、內容／適用身分改變或由活動狀態斷線加 1，Ready 完全相同 reload 為 no_change。McpGeneration 撤銷在途候選時增加，SessionGeneration 區分認證／清除生命週期；同一 ChatApplication 不重用計數，溢位安全停止，不以新 Coordinator 回繞。 |
| MCP-065 | connect／reload 候選在有界記憶體中完成 discovery、雙重目錄、Schema、敏感、模型定義及 CLI 容量查核，再於單一決策一次接受 client／快照／版本及確認失效。任何 await 後再核對完整戳記；不半套用兩工具、部分 Schema、Scope 或核准。 |
| MCP-066 | 初次普通連線失敗保持 Disconnected、無外部工具，內建能力可用；活動狀態下 reload／呼叫重驗失敗標 Stale，不能保留「仍可呼叫」的舊目錄。Stale 即使遠端恢復原描述，仍須新接受版本；連線／重載不自動重放未確認的工具。 |
| MCP-067 | MCP 戳記納入一般模型請求／回應採用、純文字顯示／成功歷史提交、工具批次、Plan 確認、檔案／程序／復原核准、Context 量測、摘要與保存候選。這些共同戳記檢查是本機比較，不使內建工具、Review 或摘要偷偷連網；只有本份明定 MCP 流程做網路重驗。 |
| MCP-068 | 首次 connect、非 no_change reload／disconnect、Stale 均撤銷舊執行確認及操作候選：Plan 身分／Revision、任務及歷史事實保留，ApprovalState 回 Draft、執行代次增加、清 read_file 修改快照、使 Context／保存候選過時；模式保持。無計畫時不新增一套 no-plan 確認，後續仍須新指示及逐次外送確認。 |
| MCP-069 | 確認失效沿既有 Settle 順序：已取得發布權的本機寫入／程序／復原先完成查核與入帳，再撤確認及換代次；在途任務若需中止則安全收尾為 Blocked／mcp_changed。外部安全回應可保留「已接收但未採用」觀察，不能先撤代次而丟掉已發生事實，也不能把遲到回應續送模型。 |
| MCP-070 | /clear、共同安全重設、接受新 Key、退出先拒新工作、撤 Permit／候選、取消並等所有自有工作關閉，再丟目錄／結果暫存／client／政策；保留原模式、程序生成預算及用量，不退 MCP 已扣機會。新認證不自動重連；一般對話安全歷史依原 clear／Key 契約處理。 |
| MCP-071 | Ctrl+C／逾時以關閉該次 HTTP 回應串流及取消 token 收尾，禁止另發舊協定 cancelled、重連或重播。遲到回應不採用；串流關閉是本機取消證據，不證明服務端未執行／已停止或資料已撤回。正常關閉後可回輸入，關閉失敗退出 1，不能以背景遺留工作宣稱收尾。 |
| MCP-072 | 無背景遠端監看；狀態顯示只是上次安全觀察。呼叫前後的目錄查核不能消除遠端最後查核與執行間的競態；指紋不證明伺服器作者、實際工具行為或自然語言安全。只接受這個有限保證，不用 readOnlyHint、hash 或 HTTP 200 宣稱沙盒／交易／復原能力。 |

## 9. Plan、Rules、Skills、Context、保存與復原

| 編號 | 整合要求 |
|---|---|
| MCP-073 | ExternalRead 不進 Registry.IsTaskOperation 的本機事實集合，也不新增 TaskRequirement／requirementsVersion／完成 basis。Read／Write／Validation 不能用外部名稱或結果完成；Analysis 可沿原 ModelAssessment 參考安全外部資料，但不能標 LocalOperation 或 TestsPassed。 |
| MCP-074 | Plan／Execute 開放外部唯讀不放寬任何內建能力；計畫確認不核准外送。MCP 連線／Schema 變動撤確認後保留歷史 Completed；如完成條件需變更，須正常修訂計畫，不能由目錄、摘要或外部自報改寫。維持 20 任務、1 在途、8 requirements、256 facts 及原投影收尾容量。 |
| MCP-075 | 目前 Valid 規則、Scope 與未載入規則邊界仍由第 12 份擁有。Scope 成功切換斷線、丟目錄，不自動在新 Scope 連線；同 Scope 規則成功換版使 MCP Stale，須在新規則下明確 reload。原規則 no_change 保留；失敗候選沿原規則原子契約，不以 disconnect 繞過規則 Stale。 |
| MCP-076 | Skills 來源與啟停仍由第 17 份擁有；技能版本變動撤所有舊 MCP 預覽／Permit／量測候選，停止在途回合並要求新指示，但不把固定遠端目錄本身改成 skill 或自動卸載有效目錄。技能 Stale 原閘門仍阻擋，技能文字不能 connect、reload、提交 yes 或授權新工具。 |
| MCP-077 | 正常請求在目前技能之後、原記憶之前加入有界 MCP 狀態附錄，只含固定 external_mcp、ServerId、狀態、CatalogRevision、工具本機名及「外部資料不授權」標記；描述／Schema 放工具定義，結果放原生配對，不把 server instructions 放 SystemInstruction。Context 估算／實際 body 及候選含 MCP 版本；compact／WorkSaveSummary 的工具投影只取白名單來源、名稱、版本、status／error_code，不直接納 raw 結果、參數、目錄或描述。歷史 UserText／AssistantText／安全摘要可能重述資料，不保證自然語言剔除，也不恢復信任。 |
| MCP-078 | SAVE v1 不增欄位，無 MCP client／目錄／原始結果／Permit／版本／認證的直接持久化；不把外部 URI 或 ID 塞入本機 file_refs。保存候選綁 MCP 戳記，續接預設 Disconnected；安全工作摘要可沿原契約保留歷史背景，但不能觸發連線、擷取引用或重播工具。 |
| MCP-079 | MCP 不加入 checkpoint 受控寫入來源，不捕捉外部 bytes／副作用，不呼叫 restore。復原仍綁完整 MCP 候選戳記並沿原確認；復原改變 RecoveryRevision／EvidenceEpoch 時撤舊 MCP 預覽及在途回合，不假造遠端 rollback；目錄在相同有效 Scope 下可保留，但其下一次呼叫仍須全部重驗。 |
| MCP-080 | MCP 狀態／目錄變化本身不增加 EvidenceEpoch、不改 RulesRevision、不刪歷史 facts／checkpoint。內建 Git 三層來源與 Review 所有權維持，外部同名結果、oid、行號或「已建置／復原」文字不能映射為本機證據。 |

## 10. 固定錯誤、停止與敏感資訊

| 固定碼 | 語意及本回合後續 |
|---|---|
| none／no_change | 成功／有效狀態相同，不表示核准外送或任務完成。 |
| invalid_mcp_command／mcp_busy | 本機語法或在途衝突，零新網路操作。 |
| mcp_not_connected／mcp_already_connected | 無活動目錄／重複 connect，依 CLI 表處理，不自動連線。 |
| mcp_catalog_stale／mcp_changed | 已知目錄或共同戳記過時；停止、撤候選，明確 reload／disconnect 或修復原上游狀態。 |
| mcp_protocol_unsupported／mcp_schema_unsupported | 固定協定／能力或 Schema 子集不相容；停止，不降版、補標頭重送或忽略限制。 |
| mcp_catalog_invalid | 發現／目錄／分頁／兩項必要工具不合法；整次拒絕，活動狀態轉 Stale。 |
| mcp_auth_unsupported／mcp_destination_rejected | 需要認證、代理、重新導向、錯誤目的地或 TLS 不合；停止，不取得憑證或另訪網址。 |
| mcp_limit／mcp_timed_out | 固定容量／次數／時間耗盡；停止，不截斷後續送、不退款或當場重試。 |
| mcp_transport_failed／mcp_unconfirmed | 傳輸失敗，分別依 outcome 說明未送或可能已送但無完整結果；停止，不推斷遠端未執行。 |
| mcp_remote_error／mcp_result_unsupported／mcp_invalid_response | 遠端業務錯誤、結果種類不支援或協定／配對非法；停止，不顯示 raw error 或改成成功。 |
| approval_declined／mode_restricted／invalid_arguments | 沿原固定語意；拒絕確認停止，模式／參數拒絕零外送，不以 MCP 另開寬鬆入口。 |
| sensitive_arguments／sensitive_content／cancelled | 沿原敏感重設或取消收尾；取消的工具 Response 沿既有 cancelled／none，不新增 cancelled 錯誤碼。 |

| 編號 | 錯誤／安全要求 |
|---|---|
| MCP-081 | MCP 新碼須正式接入 ToolErrorCodes、Runner／CLI 停止映射及最後結果，不挪用 LlmErrorCode、假造模型回答或 .NET 程序退出碼。受控遠端故障不是本機工作區失效，不能把它錯映為 workspace_unavailable 或解除既有致命收尾。 |
| MCP-082 | 所有異常只發布固定安全分類及必要數值；無法安全解析／投影時停止，不輸出 SDK exception、HTTP body、認證挑戰、完整物件或堆疊。敏感命中走原較嚴格共同重設，活動目錄不以自動停用降級續跑。 |
| MCP-083 | 低層 SDK／HTTP body、headers、參數、結果及原始 metadata logging／tracing 關閉；不寫 transcript、cache、診斷檔或 telemetry payload。必要診斷僅固定碼、階段、byte／次數／時間與傳輸狀態；通過日誌設定也不能啟用 raw 輸出。 |
| MCP-084 | 文件、測試及真實驗收不得使用／保存真實 Key、Token 或完整指示。離線注入合成短 Key 與合成受保護指示，驗所有欄位及拼接邊界；文字檢查不宣稱理解所有提示注入意圖，硬政策與逐次外送確認仍必須成立。 |

## 11. 受影響契約與後續產品同步清單

| 原擁有者 | 後續實作須同步的增量及保留 |
|---|---|
| SPEC／第 03、05 份 | 五個命令、新確認狀態、ExternalRead、版本化 RegistrySnapshot、完整配對及停止映射；公開 LLM 簽章與既有工具格式不變。 |
| 第 04／06／08、12 份 | 共用租約、Rules／Scope／敏感閘門及候選戳記；不新增一般讀取權、修改快照或放寬來源政策。 |
| 第 09、10 份 | Plan 確認撤銷、事實先入帳與容量；外部結果不進 requirement／fact，.NET recipe／原生隔離／2 次與 300 秒／test 不支援不變。 |
| 第 11、15 份 | MCP 狀態附錄、定義／結果容量、量測與摘要／保存戳記、安全 metadata 投影；單一 Runner／用量／生成預算及 SAVE v1 不變。 |
| 第 13、14 份 | 內建 Git 命名及 Review 三工具保持；新增隱藏外送確認沿原單一讀取者／busy／預輸入排空／命令歷史契約。 |
| 第 16、17 份 | 復原與技能版本加入 MCP 候選重驗；原 anchor、EvidenceEpoch、來源、載入及逐次確認所有權保留。 |

| 編號 | 相容性要求 |
|---|---|
| MCP-085 | 後續產品實作須同一變更接入正式組裝、全部生成／直接入口、CLI help／--help、README、SPEC 及受影響舊子規格的接續說明；本次不提前回寫能力完成，也不以只有未使用的 Coordinator 類別算完成。 |
| MCP-086 | 原 ToolRegistry 建構時固定十三項／同份 Definitions 契約只按本份明示改為版本化快照與兩項追加，既有順序／配對／權限保留。目錄切換及無 MCP 時不得破壞歷史原生訊息；SDK Schema 與真正 Gemini 相容性必須分列驗證，不能以假 HTTP 成功宣稱真服務通過。 |
| MCP-087 | Plan 實際／保守投影、完整顯示及必然收尾須容納 MCP 版本／停止原因，仍守 32768／65536 bytes。接近上限及 255→256 facts 的已開始本機操作仍可入帳再撤確認；不可在失效時因新增欄位留下可執行舊確認。 |
| MCP-088 | 固定套件的相依樹、版本、授權與產物敏感掃描須於後續開發重新檢查；新增依賴不能取得檔案／認證／生成入口或掃描豁免。保留原 01～17、敏感掃描與 PTY 目的，只按本份增量更新工具集合與狀態預期。 |

## 12. FUT 耦合矩陣與固定交接 memo

必要整合為目前的工具循環、Rules／Scope、安全輸出、逐次確認與單一輸入讀取者；已存在的 FUT-03／04／10 必須承接其版本／Context 閘門，不要求先完成所有延伸功能。FUT-02／06／08／09 不是連上公開 MCP 的必要前置，但目前產品既有能力仍須相容。沒有必須合併開發的 FUT。

<a id="upstream-handoffs"></a>

### 12.1 六筆上游必讀交接逐項承接

| 原交接／擁有者 | 本份承接位置／需求 | 後續驗收 |
|---|---|---|
| RULE-HO-009／第 12 份 | 第 2～4、6、9 節；MCP-005、014、018～022、032、045、050～051、075：區分外部來源、Scope／規則與硬政策，不向服務提供 Key。 | MCP-AC-003～004、011、019、024 |
| GIT-HO-009／第 13 份 | 第 2、4、6、9 節；MCP-009、013、026、032～034、049、073、080：命名空間及型別分離，外部 Git／oid 無本機資格。 | MCP-AC-005、012、018、023～024 |
| TERM-HO-009／第 14 份 | 第 5～6、8 節；MCP-035～044、047、070～071：單行命令、單一讀取者及新確認；外部文字不是按鍵／yes。 | MCP-AC-009～011、024～025、029～030 |
| SAVE-HO-010／第 15 份 | 第 5、8～9 節；MCP-039、067、070、077～078：無直接外部資料持久化／認證／連線恢復，SAVE v1 保持。 | MCP-AC-017、021～022、028、034 |
| CKPT-HO-010／第 16 份 | 第 2、6、8～9 節；MCP-013、045、051、069、073、079～080：外部非來源集合，不建 Verified／checkpoint，不承諾遠端復原。 | MCP-AC-012、018、023～025、034 |
| SKILL-HO-010／第 17 份 | 第 2～6、9 節；MCP-004～005、009、014、021～022、032、044、050、076：MCP 獨立命名／准入，技能不啟用、不授權或取得供應商 Key。 | MCP-AC-004、011、020、024、034 |

<a id="future-handoffs"></a>

### 12.2 對其他 FUT 的前向交接

| 編號／對象 | 關係與本份已定邊界 | 後續責任及驗收 |
|---|---|---|
| MCP-HO-001／FUT-01 | 相容性、無搜尋格式依賴；外部結果不建 read_file 快照（MCP-013、050、073）。 | 索引／搜尋不得自動外送命中內容或把遠端片段當修改基準；仍守 Scope 與新規則邊界。 |
| MCP-HO-002／FUT-02 | 無程序能力依賴；MCP HTTP client 不是 .NET 執行器（MCP-004、049、058、073）。 | 新 recipe／修正循環不得借 MCP 取得 shell、安裝、網路或 test 權限，也不得用遠端通過文字完成 Validation。 |
| MCP-HO-003／FUT-03 | 直接共用確認／版本，沒有外部完成 requirement（MCP-068～074、087）。 | 若新增外部任務證據，另訂版本、可信觀察、完成語意及容量；不從 MCP success 直接產生 LocalOperation。 |
| MCP-HO-004／FUT-04 | 直接共用量測、候選與唯一生成額度（MCP-061～062、067、077）。 | 新壓縮／預算策略保留來源與版本，MCP 呼叫成本另列，不把 bytes／時間當模型 token 或退款。 |
| MCP-HO-005／FUT-05 | 必要目前 Scope／Valid 規則閘門（MCP-045、051、075）。 | 多 Scope／規則來源改版須明定外部目錄適用及切換；MCP instructions 不能成為 project_rule 或設定來源。 |
| MCP-HO-006／FUT-06 | 無必要依賴；Review 固定三 Git 工具（MCP-032～034、049、080）。 | 若日後審查連網補資料，須另定明確觸發、來源與覆蓋；外部行號／oid 不冒充本機來源。 |
| MCP-HO-007／FUT-07 | 直接共用 CLI、確認及取消（MCP-035～047、071）。 | 輸入／進度改版保持唯一讀取者、完整外送預覽、命令不入歷史，遠端文字不得回放成按鍵。 |
| MCP-HO-008／FUT-08 | 共用保存候選戳記、無持久連線格式依賴（MCP-067、070、077～078）。 | 若要保存外部參照，另訂資料最小化、過時、版本及重新同意，不直接增加 SAVE v1 或恢復 Permit。 |
| MCP-HO-009／FUT-09 | 無外部復原來源；原 anchor／epoch 仍獨立（MCP-013、069、079～080）。 | 新外部寫入須獨立定義可驗證來源、衝突、敏感與補償，不能由遠端成功宣稱本機 Verified 或 rollback。 |
| MCP-HO-010／FUT-10 | 無載入格式依賴；技能只指導已授權能力（MCP-021～022、044、076）。 | MCP Skills 擴充、遠端技能安裝／啟用另定；工具描述／結果不能冒充本機 SKILL 來源或改技能。 |
| MCP-HO-011／FUT-12 | 採 MCP 路線才條件依賴；本份只轉接兩個搜尋工具，fetch 排除（MCP-004、026、054～055）。 | 擷取、搜尋詞／URL 目的地、重新導向、引用／版本／來源充分性由 FUT-12 定案；專用網路轉接器可獨立開發。 |
| MCP-HO-012／FUT-13 | 無必要依賴；公開 LLM 契約與單 client／Key 政策保持（MCP-008～009、062、070）。 | 新模型工具 Schema 能力及切換歷史須重驗；不得跨供應商傳認證、沿舊 Permit 或失敗後隱式換模型重送。 |
| MCP-HO-013／FUT-14 | 無委派能力；目前單一操作者／在途呼叫／總預算（MCP-011、044～048、058～062）。 | 子 Agent 的工具子集、共用 MCP 額度、父子取消與確認歸屬另定；子任務不能直接讀鍵、取得 Permit 或冒充人類同意。 |

| 編號 | 交接要求 |
|---|---|
| MCP-089 | 上游六筆逐項回填本份章節、需求及案例，保留原穩定錨點；第 05／06／09／10／11 份仍是工具、核准、Plan、程序、Context 的原擁有者，不能以摘要取代其原文。 |
| MCP-090 | 本節是 FUT-11 memo 唯一來源，不另抄交接文件。後續相關 FUT 須核對適用 MCP-HO-001～013 與當時產品；改來源、權限、版本、容量、保存或證據時明列擁有者與遷移。 |
| MCP-091 | 沒有必須合併開發項目；必要整合、條件依賴與無必要依賴均如表。前向交接不是其他 FUT 的完成證據、提前授權或共享格式，不為 FUT-12～14／外部寫入／認證新增空殼介面或欄位。 |
| MCP-092 | ROADMAP 的 FUT-11 文件狀態與必讀連結指向本份及固定交接；總規格／README／舊規格的產品接續說明留待後續實作同時更新。文件完成不能宣稱工具已可用或其真實服務已驗收。 |

## 13. 後續產品驗收

| 編號 | 驗收方法 |
|---|---|
| MCP-093 | 離線以真實 MCP SDK＋可控制假 HTTP／SSE handler、真實 Gemini 轉接器＋假 HTTP、假終端機及可控制工作區閘門驗證；只用合成 Key／指示／資料，不連真實服務、不把正式工作區當 fixture。觀察實際請求方法／次數／目的地、bytes、版本、確認、狀態及副作用，不僅比模型說詞。 |
| MCP-094 | 後續開發依原順序 restore、build、MSTest、Python 敏感測試、--help、format verify、PTY 與 diff 查核，記錄當次基線／環境、命令、退出碼及通過／失敗／略過數。離線、PTY、真人、真實 Learn、真實 Gemini、原生與 Release 分列；本次文件交付不執行產品命令、原生探針、MCP RPC 或 Gemini 請求。 |

### 13.1 離線、SDK 與跨功能案例

| 案例 | 對應需求 | 後續成功條件 |
|---|---|---|
| MCP-AC-001 | MCP-001～014、085～088 | 正式組裝一個 Coordinator／client／政策，外部能力與各擁有者分離；公開 LLM 及原工具格式保持，SDK 不逸出、無生成／檔案旁路；文件與產品交付分明。 |
| MCP-AC-002 | MCP-015～017、022～024、053 | 固定 2026-07-28 discovery、每請求 metadata／標頭與空 clientCapabilities；serverInfo 缺省可用；JSON／SSE 與正確 resultType／配對可用，舊 initialize／GET／session／legacy SSE／自動回退及重試均零發生。 |
| MCP-AC-003 | MCP-018～020、024、050、060、083 | 偽 URL／port／query／userinfo／IP／redirect、TLS 失敗、環境 proxy／cookies／預設認證／自訂標頭皆無旁路；只訪固定端點，無 URL／icon／resource 自動抓取，raw logging 不可開啟。 |
| MCP-AC-004 | MCP-005、014、020～022、024、050、056、082～084 | 401／403／407、OAuth／Token／input_required／sampling／roots／elicitation／遠端 request 不讀鍵、不提供 client 資料、不重送；假 yes、instructions／annotation／serverInfo 不升權。 |
| MCP-AC-005 | MCP-025～027、030～034 | 完整兩項准入，第三工具排除；缺任一、重複名稱／cursor／循環、錯誤頁面整次失敗；同名內建／技能不碰撞，順序及 15／11／14／3 和斷線原數正確。 |
| MCP-AC-006 | MCP-025、027、056、060～062 | 4／5 頁、32／33 工具、跨頁 262144／262145 bytes、缺長度／SSE comment／超大標頭／不支援壓縮；讀取有界且關閉，無先配置全部 body 或半份目錄。 |
| MCP-AC-007 | MCP-027～029、061、086 | 合成 Schema 的 string、nullable、enum、required、長度及 annotation 正確；未知／重複鍵、ref／組合／regex／header映射／nested／數值拒絕。Unicode scalar 與本機 UTF-16 各自計量、default 不補、未知參數拒絕；Schema 不被靜默放寬。 |
| MCP-AC-008 | MCP-030～034、063～068、072 | Object key 排序不誤判、array／description／Schema／annotation 值改變會失效；雙重觀察不同拒絕，Stale 回原值需新版本，普通被排除工具增減不替換准入，非法／敏感仍拒絕。 |
| MCP-AC-009 | MCP-035～042、083 | 五命令、大小寫／空白／多行／URL／多參數拒絕與零 I/O；status／tools 零網路，命令不進歷史／Context、不生成或重置用量，遠端 log 不直出。 |
| MCP-AC-010 | MCP-037～042、063～066、070 | connect 不調工具，首次失敗 Disconnected、活動失敗 Stale；重複 connect／disconnect、無連線 reload、no_change 與明確修復正確；啟動／clear／新 Key／resume 零隱式網路。 |
| MCP-AC-011 | MCP-040、043～047、056、061、067 | 每筆完整參數與目的地、版本可核對，兩次相同呼叫仍兩次新確認；敏感／顯示超限／拒絕／舊 yes／預輸入零 tools/call，Permit 不可跨候選、client 或重用，直呼入口亦拒絕。 |
| MCP-AC-012 | MCP-009～013、033～034、045～052、057、069 | 整批使用同快照，原 PartIndex／Name／CallId／原生簽章配對；A 本機成功後 B 失效／取消仍保留 A，餘項 NotExecuted，Review 零外部 I/O、外部結果零本機 facts／快照。 |
| MCP-AC-013 | MCP-053～057、061、081～084 | complete／缺省 complete、isError false／缺省、text／structuredContent 成功；isError true、未知類型、空結果、超深／重複 JSON、多 final／錯 ID、圖片／resource 混合整份拒絕，raw error 不外洩。 |
| MCP-AC-014 | MCP-028～029、040、056、060～062 | Schema 8192／8193、描述 2048／2049 UTF-16、參數 8192／8193、字串 1024／1025、定義／附錄／data 32768／32769、Response／CLI 65536／65537；含再跳脫／Unicode／固定欄位，獨立拒絕無截斷。 |
| MCP-AC-015 | MCP-024、052、057～059、066、071、081～083 | 網路、協定、目錄、結果、容量及遠端錯誤映射正確；拒絕確認／MCP 流程失敗停止剩餘工具與生成，沒有自動重試／重連／HeaderMismatch 重送／隱式降版。 |
| MCP-AC-016 | MCP-030～031、045、063～069、072 | 預覽前／yes 後／送出前／模型回應採用及成功歷史提交前換代次，舊候選／Permit／最終文字拒絕；目錄原子接受、取消前後完整狀態、版本溢位安全停止。 |
| MCP-AC-017 | MCP-039、063～070、077～078 | connect／reload／disconnect／Stale 使量測、compact／SAVE 摘要與確認候選過時；相同有效 reload 不撤確認，換 Key／clear 丟資料及遲到候選、不退款或自動恢復。 |
| MCP-AC-018 | MCP-013、068～074、080、087 | 舊 Plan 確認撤銷、模式與歷史 Completed／facts 保留，零外部 requirement／LocalOperation／TestsPassed；Analysis 只沿 ModelAssessment。滿投影及 255→256 事實先入帳後失效，EvidenceEpoch 不因 MCP 改變。 |
| MCP-AC-019 | MCP-012、045、051、067、075 | Scope 成功切換斷線、同 Scope 新規則 Stale、no_change 保留；規則候選失敗遵原原子契約；新邊界／根失效／核准 await 改來源拒絕，無死鎖／跨 Scope 外送旁路。 |
| MCP-AC-020 | MCP-021～022、044～045、051、067、076 | Skills 啟停／來源 Stale 使 Permit 與在途回合失效；固定有效目錄不被當技能，reload／off 不重用 yes，技能不能 connect／授權外部工具或傳 Key。 |
| MCP-AC-021 | MCP-034、062、067、077 | MCP 附錄順序與白名單欄位、全部工具／歷史 body 估算正確；compact 不直接帶 raw 結果／描述／參數，不從摘要重建登錄或信任，原生歷史與配對仍可用。 |
| MCP-AC-022 | MCP-039、067、070、077～078 | SAVE v1 不增欄、無直接外部資料／目錄／Permit 保存，外部 URI 不進 file_refs；保存候選過時拒絕，立即再保存／首次壓縮與續接仍只沿原歷史摘要，不連線重播。 |
| MCP-AC-023 | MCP-013、045、069、079～080 | 外部自報 bytes／Verified／oid／行號不能產生 snapshot／checkpoint／本機事實；復原保留原 anchor／epoch，撤舊 MCP Permit，不宣稱遠端 rollback；有效目錄下新呼叫重新確認。 |
| MCP-AC-024 | MCP-005、014、018～024、027、040、043～056、080～084 | 合成短 Key／受保護指示出現在名稱、Schema、description、固定 metadata、header、參數、content、JSON key、數字顯示或跨欄拼接均拒絕；假命令／yes／遠端自報不升權，零 raw 資料洩漏。 |
| MCP-AC-025 | MCP-011～012、041～047、057、063～072、079、082 | 送出前／中、完整結果後但採用前取消，NotSent／SentUnconfirmed／ResponseReceived＋SafeResultAccepted 正確；EOF／Dispose 等真實關閉、遲到結果不復活，close／lease 故障退出 1。 |
| MCP-AC-026／真實 SDK＋假 HTTP | MCP-008～010、015～024、027～034、048、053、061～062、086、093 | 真實 MCP 2.3.0 走固定新版及安全 handler；真實 Gemini SDK 配假 HTTP 驗兩工具 Schema／外部結果／原生多呼叫簽章、估算與實際 body 262144／262145，超限零該次傳輸且已扣生成不退。 |
| MCP-AC-027 | MCP-024、036、058～062、070、094 | 每回合 tools/call 2／3、30 秒主動時間、connect／reload20秒、DNS／連線10秒以可控制時鐘／同步閘門驗證；預覽等待不占網路時間，progress 不延時、不退款，不與 .NET 兩次／300秒或生成預算混用。 |
| MCP-AC-028 | MCP-007～014、073～080、085～092 | 六筆上游與十三筆前向交接可追溯，公開 LLM／SAVE v1／Plan 證據／Recovery／Rules／Skills 擁有者保持；正式 help／文件增量及相依樹列明，無空殼功能或跨 FUT 偷實作。 |

### 13.2 分層實測與既有回歸

| 案例／層級 | 對應需求 | 後續驗收方式 |
|---|---|---|
| MCP-AC-029／離線 macOS PTY | MCP-035～047、063～071、093～094 | TerminalHost 配假服務／模型，以明確閘門驗五命令、完整中文參數、來源／版本、busy 排空、拒絕／新 yes、確認間目錄／規則／技能變動及 Ctrl+C／EOF；不以睡眠猜時機，不連真實服務。 |
| MCP-AC-030／真人終端機 | MCP-035～047、056、071、093～094 | 記 OS／架構／終端機／日期，人工核對中文／Unicode／IME、隱藏確認、完整外送內容、取消與失效後新預覽，進出回顯正常；PTY／GUI 自動化不抵真人。 |
| MCP-AC-031／真實 Microsoft Learn | MCP-003、006、015～034、043～062、071～072、093～094 | 明確發起 connect 及合成公開查詢，各次新確認；驗固定協定、兩工具實際 Schema／結果與目的地，不自動降版。若版本／Schema／結果不相容，記安全分類與環境、標受阻並修訂規格，不冒稱通過。驗證服務可用不抵 Gemini。 |
| MCP-AC-032／真實 Gemini | MCP-003～006、008～009、033～034、043～062、073～080、093～094 | 使用者自行隱藏輸入 Key，以公開合成問題觀察兩工具選擇／配對、逐次外送、來源說明及失效停止；不記真實 Key／指示／完整轉錄，不以模型遵循取代程式權限保證。 |
| MCP-AC-033／既有離線回歸 | MCP-001～014、024、033、049、062、068～088、093～094 | 原 01～17、敏感掃描與適用 PTY 目的保留，只按明示增量改集合／狀態；無 MCP／斷線正常內建行為、Plan 禁本機副作用、Review 三工具、逐次核准、預算／body 與原生邊界不倒退。 |
| MCP-AC-034／交接 | MCP-073～080、085～092 | 六筆上游與十三筆前向逐項核對章節／需求／案例；FUT-12 條件依賴及其他 FUT 分別驗收明確，沒有重複 memo／來源擁有者／外部事實偽裝。 |
| MCP-AC-035／發布及未驗層級 | MCP-001～006、008、015、072、088、093～094 | 固定依賴／產物掃描與既有 BUD-S003 不豁免；test 不支援、未結原生、其他 OS／架構、真人／Learn／Gemini／人工分發分列。規格完成不抵任一產品門檻。 |

## 14. 本次文件查核與完成條件

| 編號 | 文件要求 |
|---|---|
| MCP-095 | MCP-001～100、MCP-AC-001～036、MCP-HO-001～013 唯一連續，所有需求有案例／交接可追溯；本版固定版本、兩工具、命令、Schema、上限、失敗及生命週期不留待實作者選方案的 TBD。 |
| MCP-096 | 以不落地腳本解析三個 JSON 範例，核對 Schema 子集／參數／結果一致、兩個本機名稱長度、全部 Markdown 圍欄／表格、相對連結與穩定錨點；範例明標合成，不以未呼叫服務的資料宣稱目前 Schema。 |
| MCP-097 | 逐項核對目錄／請求／批次／Permit 版本、失效先記事實、有限遠端競態、各項容量與外部來源標記；本份與六筆原交接及第 05／06／09／10／11 份無權限、證據或生命週期矛盾。 |
| MCP-098 | 完整讀回新檔與 ROADMAP 差異，執行一般及新檔 no-index 空白診斷、文件敏感外形與指示／認證洩漏查核；no-index 的差異存在與空白錯誤分開判定，不能因未追蹤新檔未列一般 diff 就當已驗。 |
| MCP-099 | 確認僅新增本檔、最小修改 ROADMAP，HEAD 及索引不變；不產生額外 memo／計畫／驗收紀錄、產品程式、測試、套件或設定，不執行清理、提交、推送或發布。 |
| MCP-100 | 完成回報只列本次實際文件查核及耦合結論；產品離線／PTY／真人／真實 Learn／真實 Gemini／原生／Release 一律依實際狀態標待執行或既有未結。後續開發重新核對本份、固定交接與當時產品，文件完成不自動開始實作。 |

| 案例 | 對應需求 | 本次文件成功條件 |
|---|---|---|
| MCP-AC-036／文件 | MCP-001～006、085～100 | 版本／待實作／乾淨基線正確，100 個需求／36 個案例／13 個前向及6筆上游交接可追溯；三範例／名稱／連結／錨點／Markdown／敏感外形／空白通過。兩個文件變更、索引／HEAD 不變，無產品實作／驗收。 |

```sh
git diff --check
git diff --no-index --check -- /dev/null specs/18-mcp-external-tools.md
git diff -- ROADMAP.md
git status --short
git diff --name-only
git diff --cached --name-only
```

## 15. 官方參考與後續閱讀

官方資料於 2026-10-28 核對；以下是協定／服務參考，不是本專案或真服務相容性完成證據。本份其餘准入、固定數值與拒絕策略是本機產品決策，不宣稱為 MCP 的通用要求。

- [MCP 2026-07-28](https://modelcontextprotocol.io/specification/2026-07-28)、[Base protocol](https://modelcontextprotocol.io/specification/2026-07-28/basic)：固定版本、每請求 metadata 及結果型別。
- [Discovery](https://modelcontextprotocol.io/specification/2026-07-28/server/discover)、[Tools](https://modelcontextprotocol.io/specification/2026-07-28/server/tools)：服務發現、工具描述與呼叫；自報資訊不作身分授權。
- [Streamable HTTP](https://modelcontextprotocol.io/specification/2026-07-28/basic/transports/streamable-http)、[Cancellation](https://modelcontextprotocol.io/specification/2026-07-28/basic/patterns/cancellation)：JSON／SSE、必要標頭及取消；不得混入舊版 session／GET／重連假設。
- [官方 C# SDK](https://github.com/modelcontextprotocol/csharp-sdk)、[2.3.0 發布紀錄](https://github.com/modelcontextprotocol/csharp-sdk/releases/tag/v2.3.0)、[Core 套件](https://www.nuget.org/packages/ModelContextProtocol.Core/2.3.0)、[v2 transports](https://csharp.sdk.modelcontextprotocol.io/v2/concepts/transports/transports.html)：用戶端套件選擇、nullable serverInfo 與明確傳輸設定。
- [Microsoft Learn 服務說明](https://learn.microsoft.com/en-us/training/support/mcp)、[開發者參考](https://learn.microsoft.com/en-us/training/support/mcp-developer-reference)：公開免認證端點、三個工具及介面可能變動；不保證本份固定版本／子集目前可用。
- [總規格](../SPEC.md)、[README](../README.md)、[上游承接](#upstream-handoffs)、[前向 memo](#future-handoffs)：原契約、目前能力及後續開發的固定入口。
