# 19：網頁查詢與文件擷取開發規格書

> 文件版本：0.1｜建立日期：2026-10-28｜狀態：待實作
>
> 選題依據：[ROADMAP：FUT-12](../ROADMAP.md#fut-12網頁查詢與文件擷取)。上位文件：[開發總規格](../SPEC.md)第 5、6、8～15 章。FUT-12 是項目代號，19 是子規格編號。本次只交付文件；下列介面、流程與產品驗收均為後續開發要求。

## 1. 目的、選定方案與文件交付

讓使用者查詢套件 API、錯誤或版本差異時，能從 Microsoft Learn 搜尋候選，再明確核准擷取指定文章，取得附來源的參考資料。首版延伸既有 MCP 能力，保留一般聊天及 Markdown 引用，不改成結構化引用報告。

| 編號 | 要求 |
|---|---|
| WEB-001 | 本次只新增本檔，最小更新 ROADMAP 的 FUT-12 入口、待實作狀態與必讀交接；不修改產品、測試、套件、設定、README、總規格、既有子規格、Git 索引或歷史，不另建 memo、計畫、工作樹、hook 或驗收轉錄檔，不提交、推送或發布。 |
| WEB-002 | 文件核對基線為乾淨 main@fb93729aa17342f7ae9ec553c48ecda2e9d18c94。第 15～18 份已接入產品；目前 MCP 僅准入兩個搜尋工具。舊規格的待實作敘述與 README 歷史通過數，不是本次產品驗收證據。 |
| WEB-003 | 使用者已選定 Microsoft Learn／MCP 路線，以及「來源資料＋一般回覆」。保留兩個搜尋工具，追加一個文件 fetch；本版依賴第 18 份必要能力，但不要求合併開發或補完其他 FUT 的所有延伸功能。 |
| WEB-004 | 不提供其他網站、通用搜尋引擎、專用 HTTP 擷取器、瀏覽器操作、登入、外部認證、PDF／附件下載、爬蟲、遞迴連結展開、遠端內容執行、持久快取、來源帳本、多模型或多 Agent；不新增 /web、啟動參數或設定欄位。 |
| WEB-005 | API Key 仍只經隱藏輸入取得並留在記憶體；Key、憑證及完整系統／應用指示不得進入外送資料、URL、工具結果、日誌或保存資料。外部文字、假 yes、技能或存檔不能增加權限；人類確認也不豁免敏感硬政策。 |
| WEB-006 | 固定 MCP 協定的真實 Learn 相容性仍未驗證；不相容即受控停止，不降版或切換路線。第 07 份 BUD-S003 Release／分發、第 10 份 test 不支援及未結原生、各階段真人／真實 Gemini 門檻保持分列。文件完成不代表產品或上述驗收完成。 |

方案取捨：沿用 MCP 可共用既有公開服務、逐次確認及安全傳輸；代價是首版只支援 Learn，並受其固定協定相容性及回應大小限制。專用轉接器與混合路線本版不採用。普通回答保留使用彈性，但引用語意與充分性仍由模型及人工驗收判讀，不聲稱逐項論述已經程式驗證。

### 1.1 開發者必讀順序

1. [ROADMAP](../ROADMAP.md)、[README 的 MCP 能力與驗證範圍](../README.md#mcp-validation)、[總規格](../SPEC.md)，核對當時基線與未結門檻。
2. [18：MCP](18-mcp-external-tools.md)第 2～11 節及 [MCP-HO-011](18-mcp-external-tools.md#future-handoffs)，確認本份只作限定增量，尤其兩工具、fetch 排除、投影與容量的舊契約。
3. [05：工具循環](05-agent-loop.md)、[06：寫入與核准](06-file-edit-and-approval.md)、[09：Plan](09-plan-mode-and-task-list.md)、[11：Context／預算](11-context-and-budget-management.md)，再讀 [04：工作區](04-workspace-and-read-tools.md)、[08：搜尋](08-code-search-and-partial-read.md)及 [10：受控建置](10-controlled-build-and-test.md)的來源與證據邊界。
4. 第 12～17 份的 [RULE-HO-010](12-project-rules-loading.md#future-handoffs)、[GIT-HO-010](13-git-diff-and-code-review.md#future-handoffs)、[TERM-HO-009](14-terminal-interaction.md#future-handoffs)、[SAVE-HO-010](15-work-save-and-resume.md#future-handoffs)、[CKPT-HO-011](16-change-checkpoints-and-recovery.md#future-handoffs)、[SKILL-HO-011](17-reusable-workflows-and-skills.md#future-handoffs)，逐項核對[本份承接](#upstream-handoffs)。
5. 核對當時 McpProfile／McpCoordinator／McpCallPermit、McpReadClient／McpWireGuard、McpSchema／McpResult／McpJson、ToolRegistry／AgentExecutionPolicy、AgentRunner、Context／GeminiContextTools、ChatMcpInteraction 及跨功能候選戳記。只讀必要程式，不將真實指示、Key 或原生協定紀錄附入文件。

## 2. 所有權與最小內部介面

以下是後續實作的 internal 契約，不是本次加入的程式碼；不建立第二套網路服務或通用文件框架。

| 元件／型別 | 責任與必要資料 |
|---|---|
| 既有 McpProfile／McpSchema／McpReadClient | 三工具映射及 fetch Schema 准入；SDK、HTTP、目錄觀察、原始回應與傳輸狀態仍由第 18 份擁有。 |
| WebDocumentUrlPolicy | 純本機判斷單一 url 字串；回固定成功／web_url_rejected，成功另提供原值及可空 RequestedView。不得修正、正規化後換值、I/O 或取得認證。 |
| WebDocumentSource | 不可變來源資料：Observation、RequestedUrl、RetrievedAtUtc、SafePayloadSha256、RequestedView；只描述本次安全 provider 回應，無來源 ID、持久註冊或授權狀態。 |
| 既有 McpResult 的 fetch 分支 | 沿既有安全結果解析，從原 Permit 與本機 TimeProvider 建來源資料，再明確寫出第 6 節投影；兩個搜尋成功 data 保持原格式。 |
| 既有 McpCoordinator／ToolRegistry／Runner | URL 准入入口、完整預覽、新確認、版本與來源重驗、配對及結果採用；不新增可獨立消費裸 URL 的公開 client 方法。 |

| 編號 | 契約要求 |
|---|---|
| WEB-007 | FUT-11 繼續擁有連線、目錄、Permit、傳輸、預算、取消及生命週期；FUT-12 只擁有文件 URL 准入、fetch 來源投影與引用行為。Plan、Rules、Skills、Git、Context、SAVE 及 checkpoint 保留原擁有者。 |
| WEB-008 | 公開 ILlmClient、LlmRequest、LlmResult、LlmMessage、原十三個工具與兩個搜尋工具的輸入／成功資料契約保持；新 fetch 使用既有 ExternalRead，不取得 WorkspaceRead、GitRead、DotnetTask 或任務完成能力。 |
| WEB-009 | WebDocumentSource 採密封型別、唯讀屬性、固定 ToString 標記；新 JSON 資料克隆所有權、只用明確 writer。不得序列化 Permit、服務、快照、政策、原始 SDK 物件或診斷內容。 |
| WEB-010 | URL 政策及來源投影只取得用途限定的敏感檢查能力，不取得 Key、完整指示、聊天 Context、工作區原文或 BootstrapSession。保護指示的值留在原 Core／Security 邊界，測試只用合成標記。 |
| WEB-011 | 沿用單一 ChatApplication／McpCoordinator／IMcpReadClient，SDK 型別留在 Mcp/、Google SDK 型別留在 Llm/；保持 ModelContextProtocol.Core 2.3.0、MCP 2026-07-28、原 .NET／Google 套件版本，不新增套件或生成入口。 |
| WEB-012 | 來源資料附於該次工具結果，不另建跨回合來源集合、來源代次、必備附錄或背景重驗。仍只有一項 MCP 操作在途，沿原排他租約、共同戳記及決策鎖；不持鎖等待網路或人類輸入。 |

## 3. 三工具目錄與相容性

| 順序 | 遠端名稱 | 本機名稱 | 本份增量 |
|---|---|---|---|
| 1 | microsoft_docs_search | mcp__learn__microsoft_docs_search | 保留原搜尋及成功 data。 |
| 2 | microsoft_code_sample_search | mcp__learn__microsoft_code_sample_search | 保留原搜尋及成功 data。 |
| 3 | microsoft_docs_fetch | mcp__learn__microsoft_docs_fetch | 唯一新工具；url 准入及 document_source。 |

| 編號 | 准入要求 |
|---|---|
| WEB-013 | 固定 learn profile 由兩項擴成上表三項；保留內建十三項相對順序及兩搜尋順序，fetch 最後追加。名稱 Ordinal 精確比對，不接受同名別字、別名或覆蓋內建工具。 |
| WEB-014 | connect／reload 仍 discovery 加兩次完整有界 tools/list 一致後原子接受；三項均須各一份，缺少、重複或不一致即 mcp_catalog_invalid，未准入工具繼續排除，不只接受可用的兩搜尋。 |
| WEB-015 | fetch 原始 inputSchema 仍走 MCP-027～030 的完整安全／指紋及 Schema 子集准入，再收緊為唯一 properties.url、型別精確 string、required 精確包含 url 一項；不接受 nullable、額外屬性或別名。既有合法 annotation／enum／長度仍適用，default 不自動補值；不硬套或虛構真服務目前 Schema。 |
| WEB-016 | Schema 關鍵字／型別不相容回 mcp_schema_unsupported；不刪除 format／ref／其他限制後繼續，不安裝相依或改用較寬的直接 HTTP。實際 tools/list 不符合本版時明示受阻，須修訂規格才能變更策略。 |
| WEB-017 | fetch 描述、Schema、annotation 等原始定義亦進既有指紋及雙重觀察；任何准入定義變化沿原 Stale／版本契約，不以文件 URL、payload hash 或服務自報替代目錄身分。 |
| WEB-018 | Ready 的全登錄／Plan／Execute／Review 為 16／12／15／3；Disconnected 為 13／9／12／3。Stale 不提供可用外部定義並沿原閘門阻擋；兩搜尋與 fetch 在 Plan／Execute 均須逐次確認，Review 始終只有三個 Git 工具。 |
| WEB-019 | 同一模型請求、回應與整批派送綁同一 RegistrySnapshot；IsExternalTool、SafeName、模式、停止分類與安全摘要白名單須辨識 fetch，不讓它落入 unknown_tool、內建身分或本機事實集合。 |
| WEB-020 | /mcp tools 與狀態附錄依已接受目錄呈現三工具；connect 只授權原 metadata 發現，不擷取文件。啟動、--help、續接、一般本機查詢與 Review 不隱式連網；既有五個 /mcp 命令及語法保持。 |

以下為合成 Schema 與參數範例，不是服務轉錄或真服務相容性證據：

```json
{
  "type": "object",
  "properties": {
    "url": {"type": "string", "minLength": 1, "maxLength": 1024}
  },
  "required": ["url"],
  "additionalProperties": false
}
```

```json
{
  "name": "mcp__learn__microsoft_docs_fetch",
  "arguments": {"url": "https://learn.microsoft.com/en-us/dotnet/api/system.string?view=net-10.0&preserve-view=true"}
}
```

<a id="url-admission"></a>

## 4. 文件 URL 准入與網路保證範圍

本節判斷的是 tools/call 中的文件參數，不是把 MCP 端點改成該 URL。政策刻意只接受 ASCII 文章網址；拒絕並不代表網址有害或 Learn 沒有該頁，只代表本版無法接受。符合格式也不證明頁面存在、作者身分或資料正確。

| 編號 | 固定規則及順序 |
|---|---|
| WEB-021 | 解碼 JSON 取得單一非空 url string 後，在任何工作區查核、目錄 RPC 或預覽之前先做完整參數敏感與純本機 URL 准入。長度最多 1024 UTF-16 單位，另須符合原 Schema 及 8192-byte 完整參數限制；不 trim、不自動補 scheme／locale 或換語系。 |
| WEB-022 | 先拒絕非 ASCII、空白、控制字元、反斜線、任何百分比符號及 #。不先 URL decode、消除 dot-segment 或呼叫 Uri 正規化來挽救原字串；JSON 跳脫先按原協定解碼，再檢值及完整參數。 |
| WEB-023 | 原字串必須以小寫 https://learn.microsoft.com/ 精確開始；拒絕顯式 :443／其他 port、userinfo、尾點、子網域、IDN、IP、其他 scheme／host、相對與 scheme-relative URL。語法查核後可用 absolute Uri 交叉核對 HTTPS／主機／無 userinfo，但不以 Uri 產出的新字串送出。 |
| WEB-024 | path 精確為 /{locale}/{area}/{article...}：locale 符合 [a-z]{2}-[a-z]{2}，後面至少兩段非空 segment；每段符合 [A-Za-z0-9][A-Za-z0-9._~-]*。不接受連續或結尾斜線、單獨 .／.. 段、空文章路徑；不合併或移除任何段。 |
| WEB-025 | area 是 locale 後第一段，不分 ASCII 大小寫拒絕 api、answers、users、search、profile、account 六個值；不是對任意子字串比對，因此 /en-us/dotnet/api/system.string 可接受。這是固定路徑政策，不宣稱完整分類 Learn 所有頁面。 |
| WEB-026 | 無 query 可接受；有 ? 時須至少一個非空 key=value 配對，以 & 分隔，鍵和值大小寫精確比對，不允許分號、空配對、額外等號或重複鍵。只允許 view 與 preserve-view，順序任意但保持原輸入次序。 |
| WEB-027 | view 值符合 [A-Za-z0-9._-]{1,64}；preserve-view 若出現，值精確 true 且必須同時有 view。拒絕未知／不同大小寫鍵、空值、false、編碼值或第二個同名鍵；view 只作版本提示，不驗證該版本存在或服務有採用。 |
| WEB-028 | 原 URL、解析後各字串、整個參數 JSON 及預覽／外送組合均經原敏感檢查；ASCII／禁止編碼的政策不能替代敏感政策。含 Key／受保護指示沿原敏感拒絕與重設，不以遮蔽值、刪 query 或使用者 yes 繼續。 |
| WEB-029 | 非敏感的文件 URL 不合本節時回 web_url_rejected、NotSent，零目錄 RPC／tools/call／確認／呼叫機會扣款，不改 Ready 為 Stale；停止當前回合其餘工具與生成，等新指示。非字串、缺欄或額外欄仍用原 invalid_arguments；敏感與共同致命故障優先。 |
| WEB-030 | 准入在該筆 InvokeAsync 入口、新確認後及 client 實際送出前各驗一次，均對同一完整參數值；不能直呼低層入口略過。若整批敏感／結構預檢已失敗，仍依原整批零派送；普通 URL 拒絕則不抹除本批先前已完成的安全操作。 |
| WEB-031 | 模型參數＝完整預覽＝Permit.Arguments＝實際 tools/call 的 URL 字串。拒絕 fragment 而非靜默移除；不從搜尋、文章內連結、canonical、登入頁或存檔自動發第二次擷取。使用者直接提供或模型提出的合法 URL 均可候選，不以出現於搜尋結果當授權。 |
| WEB-032 | 本機仍只連固定 https://learn.microsoft.com/api/mcp，沿 MCP-018～024 拒絕其 HTTP redirect／認證／代理與重送。服務端內部 fetch 的 DNS、重新導向、最終 URL、原站狀態與權限不可由本 client 觀察或強制；不得宣稱本機限制已證明遠端擷取路徑或沙盒。 |

### 4.1 准入案例

下表只判 URL 格式；成功仍需有效目錄、目前來源、敏感檢查、新確認及剩餘額度。

| URL／形狀 | 結果與理由 |
|---|---|
| https://learn.microsoft.com/en-us/dotnet/api/system.string | 可候選；文件區為 dotnet，內層 api 不在排除位置。 |
| https://learn.microsoft.com/zh-tw/aspnet/core/fundamentals/?view=aspnetcore-10.0 | 拒絕；path 有結尾斜線，不自行改寫。 |
| https://learn.microsoft.com/zh-tw/aspnet/core/fundamentals?preserve-view=true&view=aspnetcore-10.0 | 可候選；保留 query 次序。 |
| https://learn.microsoft.com/en-us/Answers/example/page | 拒絕；area 不分大小寫命中排除集合。 |
| https://learn.microsoft.com/api/mcp | 拒絕；不是 locale／文件區／文章路徑。 |
| https://learn.microsoft.com/en-us/dotnet/api/system.string#methods | 拒絕；有 fragment。 |
| https://learn.microsoft.com/en-us/dotnet/../api/example | 拒絕；dot-segment。 |
| https://learn.microsoft.com/en-us/dotnet/api/system.string?view=net-10.0&view=net-9.0 | 拒絕；重複鍵。 |
| https://learn.microsoft.com/en-us/dotnet/api/system.string?preserve-view=true | 拒絕；缺少 view。 |
| 大寫 scheme／host、顯式 port、userinfo、其他 host、百分比編碼、連續斜線、空 query | 一律拒絕，不先正規化後接受。 |

## 5. CLI、確認與派送流程

```text
明確 /mcp connect learn → 原 discovery／雙重目錄，接受三工具
一般訊息 → 同一 Runner／RegistrySnapshot → 模型搜尋或提出 fetch
完整批次預檢 → 該筆 URL 准入 → 原來源／目錄查核 → 完整外送預覽
新隱藏 yes → 原來源／目錄／戳記與 URL 再驗 → 單次 Permit → 一次 tools/call
有界安全回應＋本機來源資料 → 原生工具配對 → 一般回答及來源連結
```

| 編號 | 行為要求 |
|---|---|
| WEB-033 | 保留 /mcp、connect learn、tools、reload、disconnect 五個命令及零網路查詢行為；help／tools 後續明示搜尋與文件擷取、三工具、URL 限制及資料可能過時。使用者不必先搜尋才能提出合法文章 URL。 |
| WEB-034 | fetch 預覽完整固定 MCP 端點、external_mcp／learn、兩個工具名、目錄版本／指紋及全部參數，並明示「將 URL 交給 Learn 服務擷取，服務可能記錄，原站狀態與版次未由本機驗證」。不把外送 URL 稱為本機直接連線目的地。 |
| WEB-035 | 隱藏新 y／yes、唯一讀取者、預輸入排空、拒絕、Ctrl+C／EOF 及輸出跳脫完全沿原契約。connect、Plan 確認、其他操作／前次 yes、歷史、網頁與模型文字不能代替本筆；無安全完整預覽即零 tools/call。 |
| WEB-036 | 候選及 Permit 綁完整工具／參數、MCP／Registry／Session、Rules／Scope、Skills、Plan／模式／執行代次、RecoveryRevision 及取消代次；新 yes 後仍須原目錄與來源重驗。Permit 只由同一 Coordinator 簽發、由綁定 client 消費一次，版本不是同意。 |
| WEB-037 | 呼叫只送已確認參數與原最小協定 metadata；不附加聊天、規則、技能、pins、工作區、檔案或受保護指示。fetch 的工具描述／內容／URL 不授權工具、Scope 或任何本機寫入。 |
| WEB-038 | 同批工具依原順序執行、每筆外部操作各取新確認；不因同參數去重，也不因搜尋成功自動串接 fetch。A 已完成、B URL 拒絕／取消／過時時保留 A 的事實／觀察，其餘按原 PartIndex／Name／CallId 及簽章配 NotExecuted。 |
| WEB-039 | Plan／Execute 的 fetch 為 ExternalRead，仍受原執行准入；Review、compact 與工作保存摘要不提供 fetch 工具。搜尋和 fetch 同用當前回合外部額度，任一 MCP 流程失敗沿原停止及安全結果配對，不讓模型當場重試或換途徑。 |

<a id="document-source"></a>

## 6. 安全回應、來源資料與成功語意

| 編號 | 結果要求 |
|---|---|
| WEB-040 | fetch 僅沿 MCP-053～057 接受安全 complete、text content 與可空 object structuredContent；不解析回應中的 URL、HTML、命令、JSON 字串或 canonical 來觸發動作。圖片／resource／混合不支援內容仍整份拒絕，不部分保留。 |
| WEB-041 | 原始回應、解碼字串／JSON 欄名與值、來源資料、hash／時間／固定標籤、完整 data／Response／CLI 組合全部經原敏感檢查。短 Key 命中固定欄位或指紋也不豁免；遠端欄位不得覆蓋本機 document_source。 |
| WEB-042 | 只有新 fetch 的成功 data 追加 document_source；原 external_mcp、server_id、remote_tool、catalog_revision、content、structured_content 與頂層 status／error_code／truncated／safety_filtered 沿原格式。兩搜尋 data 不增欄；沒有來源合成失敗卻當普通 fetch 成功的分支。 |
| WEB-043 | document_source 恰含 observation="provider_response"、requested_url、retrieved_at_utc、safe_payload_sha256、requested_view。requested_url 精確取原 Permit.Arguments.url；requested_view 取本機通過准入的 view 或 null。資料不可由模型、遠端 structuredContent 或回應中的相似欄位供給。 |
| WEB-044 | retrieved_at_utc 取注入的本機 TimeProvider，於完整安全回應接收／解析完成後擷取 UTC，Invariant 格式 yyyy-MM-ddTHH:mm:ss.fffffffZ。這是本機觀察時間，不是原站發布／更新時間；後續候選失效時不得單獨發布該 metadata。 |
| WEB-045 | safe_payload_sha256 是明確物件 {"content":完整安全文字項目陣列,"structured_content":原安全物件或null} 的 SHA-256 小寫64位hex。沿既有 McpJson 正規化：object key 遞迴 Ordinal 排序、array 原序、文字不正規化，以現有 writer 的 compact UTF-8／無 BOM bytes 計算；不含來源 metadata、時間、URL 或 envelope。 |
| WEB-046 | 指紋只辨識本次接受的安全 payload；不是原始 HTML bytes、origin ETag、文件版本或數位簽章。metadata 不建立 actual_version、canonical_url 或 last_updated 推測值；服務／正文的自報仍只是外部文字，requested_view 不能當 served version。 |
| WEB-047 | success／complete／isError=false 只表示本機接受安全 provider 回應，不保證原站 HTTP 成功、全文完整或題目已獲回答。服務即使以普通文字回「無法擷取」，也不能因 envelope 成功而宣稱已讀到正文；模型須依內容回報限制，不用猜測性的 Markdown 標題／長度判定完整文件。 |
| WEB-048 | 沿 NotSent／SentUnconfirmed／ResponseReceived 與 SafeResultAccepted；已送但缺完整回應不可報未外送，回應收到但投影／戳記失敗不可報已採用。URL 預檢失敗沒有來源 metadata，遲到回應不提交成功歷史。 |
| WEB-049 | 安全 content、來源 metadata、整體容量及所有戳記須全數通過後，作為同一工具 Response 採用；不先發布 hash／時間再補正文。沒有獨立來源 ID、補查、來源快取或日後自動刷新；相同 payload 不代表遠端一直未變。 |

以下是合成 fetch 結果；範例 hash 為本節規則對範例 payload 計算，不代表任何真實頁面：

```json
{
  "status": "success",
  "error_code": "none",
  "data": {
    "source": "external_mcp",
    "server_id": "learn",
    "remote_tool": "microsoft_docs_fetch",
    "catalog_revision": 1,
    "content": [{"type": "text", "text": "Synthetic documentation response."}],
    "structured_content": null,
    "document_source": {
      "observation": "provider_response",
      "requested_url": "https://learn.microsoft.com/en-us/dotnet/api/system.string?view=net-10.0&preserve-view=true",
      "retrieved_at_utc": "2026-10-28T00:00:00.0000000Z",
      "safe_payload_sha256": "a5c4366bba26eb05156f5915f5563fe9f9fd7e772cf1b409492490d0f3f01e7d",
      "requested_view": "net-10.0"
    }
  },
  "truncated": false,
  "safety_filtered": false
}
```

## 7. 容量、記帳與固定失敗

本表引用第 18 份的單一限制，不新增可獨立調寬的設定或第二份預算。

| 既有限制／擁有者 | 本份適用方式 |
|---|---|
| MCP 每一般回合 2 次 tools/call 啟動機會 | 搜尋與 fetch 合計 2 次；搜尋＋擷取已用盡，不能再抓第三頁。 |
| 每筆 30 秒主動網路／解析；DNS／連線最多10秒 | 仍包含前後目錄查核，排除人類確認等待；進度不重設期限。 |
| connect／reload 整體20秒；目錄4頁／32項 | 三工具雙重完整觀察仍用原額度，不增頁數或隱藏輪詢。 |
| 每回應／SSE及跨頁目錄262144 bytes；標頭64欄／16384 bytes | 原有界串流及上限加1偵測；不因文章較長先無界配置。 |
| Schema／完整參數8192 bytes；描述2048 UTF-16；每字串1024 UTF-16 | fetch URL另受第4節格式；三項完整模型定義合計仍最多32768 bytes。 |
| 狀態附錄／成功data32768 UTF-8 JSON bytes | 三工具名稱及新 document_source 都計入原本完整物件限制。 |
| 完整Response／CLI區塊各65536 UTF-8 bytes | 來源、envelope、控制字元跳脫、Unicode與固定欄位一併計量。 |
| Context估算軟門檻196608；實際SDK body262144 bytes | 定義、結果、所有既有附錄與再跳脫照計，不移除必要來源來補送。 |

| 編號 | 限制／失敗要求 |
|---|---|
| WEB-050 | 全數沿上表與 MCP-058～062 的獨立限制；data 含 metadata 後超32768亦整份 mcp_limit，不截短正文、拿掉來源、壓縮偽裝全文或改由另一 client 擷取。 |
| WEB-051 | 只有原第18份的呼叫扣款點扣機會；通過新確認及最後准入、進入 client CallAsync 前保守扣次，失敗不退款。URL 預檢拒絕不扣次，既有 metadata 查核不扣 tools/call 次數但仍計原主動時間／bytes。 |
| WEB-052 | MCP 次數／bytes／時間與模型 token／生成、.NET 兩次／300秒分列；不估服務費、不退款、不增加生成上限或額外 client。必要摘要由一般既有 Runner 在剩餘額度內回答，不啟動背景或專用摘要請求。 |
| WEB-053 | 除 web_url_rejected 外沿原 mcp_*、invalid_arguments、mode_restricted、approval_declined、敏感與取消固定碼；HTTP／遠端錯誤不顯示原文、標頭、例外或認證挑戰，不拿 web 錯誤取代更嚴格的 unavailable／清理故障。 |
| WEB-054 | web_url_rejected 必須加入 ToolErrorCodes、工具安全結果與停止映射；停止原因使用既有 MCP 停止類別，保留先前操作及原配對。訊息只顯固定分類，不回顯被拒 URL／敏感片段；不把普通參數拒絕錯判為目錄或工作區失效。 |
| WEB-055 | isError=true、超限、不支援結果、傳輸／配對錯誤與無法產生來源投影沿原停止流程，不重送、不以直接HTTP／瀏覽器／換模型回退。已收到的普通「來源不足」文字由一般回覆說明，不能以 API success 自動補查或放寬限額。 |

## 8. Context、跨功能與生命週期

| 編號 | 整合要求 |
|---|---|
| WEB-056 | 三工具共享原 McpOperationStamp／CatalogRevision／Generation。fetch 定義變動仍使共同候選過時；文件 payload／URL 改變只是不同查詢觀察，不單獨使目錄 Stale，也不新增來源版本或背景監看。 |
| WEB-057 | 原 MCP 戳記繼續涵蓋模型請求／最終文字與成功歷史採用、Plan確認、檔案／程序／復原核准、量測、compact及保存候選。目錄變動先完成已發布本機操作的查核／入帳，再撤確認／清修改快照；新 metadata 不改這個順序。 |
| WEB-058 | 目前 Valid Rules／Scope與Skills仍須重驗；Scope切換斷線，同Scope規則換版使MCP Stale，技能／復原換版撤舊候選，沿 MCP-075～079。網頁／文件／引用不成為 project_rule、技能來源或新同意，也不增加任何工作區存取。 |
| WEB-059 | fetch安全結果與metadata只留在既有完整工具配對／回合記憶體，保持原生簽章與順序。正常請求只擴充既有MCP狀態附錄的工具名稱，不另附整份文件或新必備附錄；全部仍計入Context估算及實際body。 |
| WEB-060 | fetch不進IsTaskOperation、本機Read／Write／Validation requirements、Verified／TestsPassed、GitSnapshot或checkpoint來源；Analysis只可沿原ModelAssessment參考外部資料。沒有新增requirementsVersion、完成basis或EvidenceEpoch變動；歷史Completed仍依原規則。 |
| WEB-061 | compact／WorkSaveSummary的工具投影僅沿MCP-077白名單來源、名稱、目錄版本、status／error_code，追加准許fetch名稱；不直接加入URL、時間、hash、view、raw正文、參數或document_source。自然語言UserText／AssistantText可能已轉述資料，不能宣稱已語意剔除。 |
| WEB-062 | SAVE v1不增欄位，URI不放file_refs；不直接保存原始文件、來源物件、Permit或client，也不藉另建檔案繞過。續接的自然語言連結只作過時背景，預設MCP Disconnected、不抓引用、不恢復目錄／信任／同意或來源資格。 |
| WEB-063 | clear／共同安全重設／換Key／退出先撤候選與Permit、取消並等自有工作關閉，再丟client／目錄及原契約要求清除的歷史／結果；計數不回繞、不退款、不改磁碟或自動重連。普通斷線只沿原MCP狀態契約，已提交的安全回合仍為歷史；取消不證明服務端停止或撤回資料。 |

## 9. 一般回答、引用與資料充分性

| 編號 | 回覆行為與保證 |
|---|---|
| WEB-064 | 回答維持一般文字與Markdown連結，不要求固定final JSON或新增報告validator；本機保證metadata來源、配對、容量與硬政策，不能宣稱已驗證每個論述受引用支持。這些回覆行為由離線提示組裝測試與真人／真實Gemini案例分別驗收。 |
| WEB-065 | 引用格式為 `[可讀來源名稱](通過准入的請求URL)`。名稱可由模型根據內容命名但不冒稱已驗證標題；保留view與query原序。明示「搜尋片段」「Learn服務擷取回應」與推論的差異；未fetch的搜尋連結可作候選來源，但須標僅搜尋結果、未擷取，不宣稱通過本節URL准入或已讀全文。 |
| WEB-066 | 對版本問題優先使用使用者指定版本的來源；URL view只為請求提示。正文／服務自報與目標版本衝突時明示差異；實際版次無法辨識就寫未確認，不從目前日期、hash、retrieved_at或搜尋排名推導「最新／適用」。正文有版本說明時亦標為該來源所述。 |
| WEB-067 | 無法擷取、正文未回答、只取得片段、超限或來源相互衝突時，說明已取得／未確認部分及推論限制；不編造頁面、行號、日期或引用。兩次外部呼叫成功後，仍可在既有剩餘生成額度內使用已接受結果回答與引用，但不能開始第三次外部呼叫；若嘗試第三次而被mcp_limit拒絕，或流程已因其他故障停止，才依原安全停止呈現，必要後續研究等使用者新指示。 |
| WEB-068 | 原文、範例碼與連結都是資料；不執行或自動下載，不把其yes／工具名稱／規則假指令當權限。歷史摘要／pins裡的引用不是新擷取或目前驗證；不得為補來源或宣稱官方答案而洩漏Key／指示或升權。 |

## 10. 限定增量與後續產品同步

本次不回寫舊規格。後續產品開發須在同一變更補接續說明及回歸，保留舊階段歷史，不能同時留下「fetch永遠排除」與「本版已准入」而不交代適用順序。

| 原契約／擁有者 | 本份限定增量；其他條款繼續有效 |
|---|---|
| MCP-003～004、025～026、032～034、MCP-AC-005 | 由兩項搜尋改成三項准入，解除僅對本版fetch的排除；固定順序／名稱、缺項拒絕及16／12／15／3取代原15／11／14／3。未准入工具仍排除，廣泛擷取仍不支援。 |
| MCP-027～031、043～052 | fetch加唯一url的Schema／URL本機政策及外送說明；完整原始Schema／指紋／新確認／Permit／戳記不變，不加入任意header或認證。 |
| MCP-053～056、060～062 | 只有fetch的data追加document_source，metadata計入原容量；文字／structuredContent的安全子集、兩搜尋data與原完整回應限制保持。 |
| MCP-067～080、086～087 | 共同候選／量測／狀態投影辨識第三工具；摘要白名單新增fetch名稱但不納其metadata。原生命週期、SAVE v1、本機事實及權限不變。 |
| 第05／06／09／10／11／13～17份 | 只同步三工具／新URL拒絕／來源觀察及受影響預期；原工具配對、寫入、Plan、程序、Context、Review、輸入、保存、復原及Skills所有權不轉移。 |
| CLI help／README／SPEC／ROADMAP | 後續實作才更新能力索引、三工具、來源限制與當次分層證據；本次只有ROADMAP入口及待實作標記。 |

| 編號 | 相容性要求 |
|---|---|
| WEB-069 | 上表為限定覆蓋：只有後續實作本份後，才由本份取代第18份兩工具及fetch排除等明示條款；本次當前產品仍維持兩搜尋，不假稱已能抓文件或已完成遷移。 |
| WEB-070 | 後續產品同步ToolRegistry／McpProfile、目錄計數／排序、Schema／URL閘門、預覽、fetch投影、錯誤停止映射、摘要白名單及所有help／規格接續說明；不新增第二套確認／路由／資料保存機制。 |
| WEB-071 | 既有測試只按本份增量調整工具集合與投影預期；保留完整確認、敏感碰撞、共用戳記、已發生事實、取消、原生限制及歷史配對目的。不能藉測試更新放寬原來源或刪除失敗分支。 |
| WEB-072 | 新增功能與套件升級、協定降版、真服務相容性修復分開；固定版本若不可用須回報受阻，明示修訂並補驗，不自動安裝CLI、改endpoint、加maxTokenBudget或用其他供應商。 |

## 11. FUT 耦合矩陣與固定交接 memo

本節為 FUT-12 的交接紀錄單一來源，不另抄一份memo。七份上游仍各自擁有原契約；以下對照只記錄本份的承接、限定增量與驗收責任。本版採MCP，因此對FUT-11有必要能力依賴；這不把所有FUT或其未結驗收合併成單一交付。

<a id="upstream-handoffs"></a>

### 11.1 七筆上游必讀交接逐項承接

| 原交接／擁有者 | 本份承接位置／需求 | 後續驗收 |
|---|---|---|
| RULE-HO-010／第12份 | 第2、4～5、8～9節；WEB-005、007、010、028、036～037、058、068：來源／Scope／規則與授權分離，不從規則連結抓頁。 | WEB-AC-007、008、018、024、031 |
| GIT-HO-010／第13份 | 第2～3、5、8節；WEB-008、018～020、039、060：Review三Git工具零網路，引用非本機位置／測試證據。 | WEB-AC-002、009、020、025、031 |
| TERM-HO-009／第14份 | 第5、8～9節；WEB-033～038、063、068：完整安全預覽、新隱藏確認、單一讀取者；外部文字不回放按鍵。 | WEB-AC-008、022、024、026～027、031 |
| SAVE-HO-010／第15份 | 第2、8～9節；WEB-004、012、059、061～063、068：不直接保存外部來源、擴充SAVE v1或恢復連線與核准。 | WEB-AC-017、021～022、024、031 |
| CKPT-HO-011／第16份 | 第2、5、8節；WEB-008、036～038、057、060、063：外部內容非before／路徑映射／復原資格，不建本機事實或遠端rollback。 | WEB-AC-009、016、019～020、024、031 |
| SKILL-HO-011／第17份 | 第1～2、4～5、8～9節；WEB-004～005、007、031、037、058、068：技能引用惰性，遠端描述不安裝、啟用或升權。 | WEB-AC-018、024～025、031 |
| MCP-HO-011／第18份 | 第2～10節；WEB-007、011、013～020、021～032、040～055、064～072：選MCP路線、URL／來源／版本／充分性定案，明列兩→三工具及投影增量。 | WEB-AC-002～017、023、028～031 |

<a id="future-handoffs"></a>

### 11.2 對其他13個FUT的前向交接

| 交接編號／對象 | 關係與本份已定邊界 | 後續責任及驗收 |
|---|---|---|
| WEB-HO-001／FUT-01 | 無搜尋格式依賴；文件不是本機read_file快照（WEB-008、037、060）。 | 索引／搜尋不能自動外送檔案或把文件片段當修改基準；驗收AC-019、024～025。 |
| WEB-HO-002／FUT-02 | 無程序前置依賴；fetch不執行範例或取得網路recipe（WEB-004、039、052、060）。 | 修正循環仍需獨立程序核准／隔離，外部文字不完成Validation；驗收AC-019、024～025、030。 |
| WEB-HO-003／FUT-03 | 共用目前Plan與候選戳記，沒有外部requirement或basis（WEB-036、057、060）。 | 新外部證據須另定版本與容量，不從引用／success完成本機任務；驗收AC-016、019、031。 |
| WEB-HO-004／FUT-04 | 直接共用生成／容量／摘要白名單（WEB-050～052、059、061）。 | 新策略保留來源過時語意、完整配對與唯一記帳，不還原正文資格；驗收AC-010、014、017、023。 |
| WEB-HO-005／FUT-05 | 必要目前Valid／Scope閘門；遠端不是規則（WEB-028、036～037、058、068）。 | 規則來源／多Scope改版重驗准入，不用官方標記升權；驗收AC-007、016、018、024。 |
| WEB-HO-006／FUT-06 | 無必要依賴；Review保持本機三工具（WEB-018、039、060）。 | 若日後審查連網，另訂觸發、來源與網路額度，不能直接沿本版放行；驗收AC-009、020、025。 |
| WEB-HO-007／FUT-07 | 共用預覽／確認／取消；外部文字不是鍵盤（WEB-033～036、063、068）。 | 輸入或進度改版保留完整參數、唯一讀取者與新確認；驗收AC-008、022、026～027。 |
| WEB-HO-008／FUT-08 | 共用保存候選，無來源持久格式依賴（WEB-061～063、068）。 | 日後保存引用須另定版本／最小化／過時／重新同意，不能塞SAVE v1 file_refs；驗收AC-017、021～022。 |
| WEB-HO-009／FUT-09 | 文件無復原資格；RecoveryRevision仍撤舊候選（WEB-036、057～058、060）。 | 新外部寫入或備份需獨立定義來源與衝突，不從provider回應匯入anchor；驗收AC-016、020、024。 |
| WEB-HO-010／FUT-10 | 無載入格式依賴，技能／連結惰性（WEB-004、031、037、058、068）。 | 遠端技能取得／安裝／啟用另定，不能由文章指示授權；驗收AC-018、024～025。 |
| WEB-HO-011／FUT-11 | 本版必要MCP能力依賴；三工具為明示增量（WEB-007、011、013～020、069～072）。 | 擴充端點／認證／Schema時保留本份URL與來源政策，版本遷移不得靜默降級；驗收AC-002～003、016、028、031。 |
| WEB-HO-012／FUT-13 | 無多模型前置依賴；公開LLM與當前Key政策保持（WEB-005、008、011、052、063、072）。 | 新client重驗工具能力、原生歷史及認證隔離，不跨供應商沿舊Permit或隱式重送；驗收AC-007、022～024。 |
| WEB-HO-013／FUT-14 | 無委派能力；單一在途、操作者與共用額度（WEB-004、012、035～039、051～052）。 | 子Agent另訂工具子集、父子取消、確認歸屬及總額度，子任務不能讀鍵／核准；驗收AC-008～010、022、024。 |

| 編號 | 交接要求 |
|---|---|
| WEB-073 | 七筆上游逐項列本份章節／需求／案例，保留原穩定錨點；不以「沿用」摘要取代原文，尤其MCP的fetch排除／配對／容量與SAVE的歷史背景邊界。 |
| WEB-074 | 本節為FUT-12 memo唯一來源，相關後續FUT須核對適用WEB-HO-001～013與當時產品；改來源、權限、版本、容量、保存或證據須明列原擁有者、增量與遷移。 |
| WEB-075 | 沒有必須合併開發項目；本版對FUT-11的必要依賴不要求補完其真實服務／其他未結驗收後才寫規格，但後續產品驗收必須證明實際相容。其他FUT的既有能力須相容，不新增未開發能力空殼。 |
| WEB-076 | ROADMAP只記本份已定義／待實作並指向兩個固定交接錨點；前向memo不是其他FUT的完成證據、提前授權或共享格式，不另建來源存檔／通用網路或多Agent介面。 |

## 12. 後續產品驗收

| 編號 | 方法與分層要求 |
|---|---|
| WEB-077 | 離線使用真實MCP SDK＋可控制假HTTP／SSE、真實Gemini轉接器＋假HTTP、假終端機／TimeProvider與合成資料，驗實際目的地、參數、次數、預覽、版本、metadata、bytes、停止及副作用。不得連真服務或使用真Key／指示，也不只比較模型說詞。 |
| WEB-078 | 後續產品依原順序restore、build、MSTest、敏感Python、--help、format verify、PTY及diff查核，記當次環境、命令、退出碼及通過／失敗／略過數；離線、PTY、真人、真Learn、真Gemini、原生、Release分列。本次不執行這些產品命令、RPC或原生探針。 |

### 12.1 離線、SDK與跨功能案例

| 案例 | 對應需求 | 可觀察的成功條件 |
|---|---|---|
| WEB-AC-001 | WEB-001～012、069～072 | 固定版本／依賴／公開介面與文件基線正確；無新設定、client、認證、一般HTTP或其他FUT空殼，型別／JSON所有權安全。 |
| WEB-AC-002 | WEB-013～014、017～020、033、056、069～071 | 三項缺一、重複、第四項排除、雙目錄差異及固定排序；Ready16／12／15／3、斷線13／9／12／3、Stale無可用外部工具，舊snapshot不可派送。 |
| WEB-AC-003 | WEB-013～017、021、070、072 | 真實SDK假目錄接受單一必填url string及原合法限制；nullable、別名、額外property、未知keyword／format／ref、重複JSON拒絕；無刪約束、補default或兩搜尋降級。 |
| WEB-AC-004 | WEB-021～027、030～031 | 第4.1節正例、無query、兩種query順序、大小寫文章段、內層api及非空多段path可候選；原參數逐字等於預覽／Permit／實際外送。 |
| WEB-AC-005 | WEB-021～025、028～032 | host大小寫／尾點／後綴／userinfo／IP／port／scheme、非ASCII／空白／反斜線／百分比／fragment、locale／area排除、空段／結尾斜線／dot-segment拒絕；零metadata RPC、確認與呼叫扣款，Ready保留。 |
| WEB-AC-006 | WEB-021、026～029、031 | view長度0／1／64／65、未知鍵／重複鍵／大小寫／空配對／分號／多等號／空query、preserve-view缺view／false拒絕；格式合法但未知版本只標提示，不猜served版次。 |
| WEB-AC-007 | WEB-005、010、021～022、028～030、037、041、053～054 | 合成短Key／受保護指示命中URL、JSON解碼、固定欄、時間、hash及組合預覽／data均拒絕；無敏感外送／原文錯誤／降級成功，普通URL拒絕不掩蓋敏感重設。 |
| WEB-AC-008 | WEB-020、033～038、051、063 | 完整MCP端點／文件URL／指紋／參數與說明；相同兩筆各新確認，舊yes／預輸入／connect／Plan確認無效；拒絕／取消／EOF或顯示超限零tools/call，Permit跨client／重用／直呼不可旁路。 |
| WEB-AC-009 | WEB-008、012、018～020、030、036～039、060 | 正確ExternalRead及模式／快照／整批配對；A本機成功後B拒絕保留A，其餘NotExecuted；Plan可候選但不越過原准入，Review零網路，無文件本機facts。 |
| WEB-AC-010 | WEB-012、038～039、050～052、055、067 | 搜尋＋fetch兩次成功後，仍可在原剩餘生成額度內用已接受結果回答與引用；若要求第三筆才mcp_limit並停止，不直接因剩餘外部次數為零而攔截純回答。兩fetch同額度，重複不去重；20／30／10秒及確認等待排除不重置主動時間，失敗不退款，無額外生成或輪詢。 |
| WEB-AC-011 | WEB-040～043、047～049、053、055 | text／structuredContent與缺省complete按原契約；isError、空／混合resource、不合法JSON／深度／配對皆拒絕；安全普通失敗文字不冒稱已讀正文，無HTML／連結執行。 |
| WEB-AC-012 | WEB-041～044、046～049、064～067 | metadata精確來自Permit及假UTC時鐘，無view為null；遠端偽document_source不能覆蓋；時間非更新日、view非served版、無origin／canonical／完整性推測，失效零半套metadata。 |
| WEB-AC-013 | WEB-041、045～046、049 | 範例hash正確；object重排序同hash、array／文字／Unicode或structured值改變換hash；URL／時間變動不影響hash。指紋不作目錄、版本或可信身分。 |
| WEB-AC-014 | WEB-015、021、040～042、049～055、059 | 原8192／32768／65536／262144及上限+1、描述／字串邊界均覆蓋，三工具定義合計不增額；metadata使data超限亦整份拒絕，含JSON再跳脫，無截短假完整／先無界讀取。 |
| WEB-AC-015 | WEB-029、035、038～039、048、051、053～055 | web_url_rejected與invalid_arguments區分，MCP停止映射／較嚴故障優先；NotSent／可能已送／已收到未採用照實，拒絕／超限／超時／遠端錯誤不當場重試或換route。 |
| WEB-AC-016 | WEB-017、019、030、036、048～049、056～058、069～072 | 預覽前、yes後、送出前、工具結果／最終歷史採用前變目錄／Scope／規則／技能／Plan／Recovery／取消代次，舊候選不採用；先記事實再失效，無半套三工具／來源發布。 |
| WEB-AC-017 | WEB-009、012、050、052、056～059、061～062、068 | 原生完整工具配對含來源資料與容量；正常附錄只有既有狀態／三名稱。compact／保存工具摘要只見原白名單，無URL／正文／document_source直接來源，無新增來源帳本或摘要生成。 |
| WEB-AC-018 | WEB-005、007、028、031、036～037、058、068 | Scope／規則／Skills所有權及Stale閘門保持；規則／SKILL連結、外部假指令不能自動fetch／reload／啟用技能／升權，換版保留原先記事實順序。 |
| WEB-AC-019 | WEB-008、030、037～039、057、060、071 | 外部success／自報read／build／test／Verified不能滿足Read／Write／Validation或生成本機fact；Analysis仍ModelAssessment。20任務／8requirements／256facts／1在途及255→256收尾不退步。 |
| WEB-AC-020 | WEB-008、018、039、057～058、060 | Review三工具與原Git來源／行號查核保持；文件／URL／hash不進GitSnapshot、before、anchor或restore。復原撤舊fetch候選，無遠端rollback聲稱或EvidenceEpoch偽變動。 |
| WEB-AC-021 | WEB-004、009、012、057、061～063、068 | SAVE v1無新增欄／URI file_refs／原始來源，保存候選仍綁MCP；續接連結只是歷史、Disconnected，零隱式fetch／目錄／Permit恢復，立即再保存與首次compact沿原契約。 |
| WEB-AC-022 | WEB-012、035、048～049、051～053、057、063、071 | Ctrl+C／逾時／clear／安全reset／換Key／退出及輸出／關閉故障競態，所有owned工作關閉後才釋放；遲到不採用，斷線保留歷史語意，計數不回繞、無重播／退款。 |
| WEB-AC-023 | WEB-008、011、019、036、040～052、057、059、061、070、077 | 真MCP與Gemini SDK配假HTTP驗完整配對／metadata／新增工具，完整body262144／262145與Unicode再跳脫、hash／時間固定欄敏感；零第二client、raw協定log或超限傳輸。 |
| WEB-AC-024 | WEB-004～005、010、028、031～032、035、037、040～041、058、060～062、068 | 網頁／MCP／技能／存檔／其他Agent的假yes／按鍵／系統指令／完成聲稱皆無權威；禁止登入、遠端執行、下載、跨站或資料外洩，測試不保存真指示／憑證。 |
| WEB-AC-025 | WEB-004、008、011、018～020、031～032、037、039、055、060、072 | 只觀察固定MCP網路，無直接文件GET／redirect／proxy／cookies／認證／瀏覽器／安裝／跨站或Review RPC；服務端內部目的地不可觀察的限制如實呈現，不用測試宣稱控制遠端。 |

### 12.2 分層實測、回歸與交接

| 案例 | 對應需求 | 後續證據；本次未執行 |
|---|---|---|
| WEB-AC-026／PTY | WEB-020、033～036、041、050、063、070、078 | 保留原131組目的，補fetch完整預覽、URL拒絕零RPC、新確認、確認後過時、在途取消及EOF；合成資料，原輸入／核准回顯不放寬。組數以當次結果為準。 |
| WEB-AC-027／真人 | WEB-006、033～035、063～068、078 | 記OS／架構／終端機／日期；中文、長URL、完整參數可讀性、新確認／取消／EOF、來源與版本限制說明，不以PTY替代。 |
| WEB-AC-028／真Learn | WEB-006、011、013～018、021～032、034、040～055、069、072、075、078 | 經另次明確授權在固定協定／SDK下實測三工具／Schema與小型文章；不相容照實受阻，不回退。只記安全分類／必要數值及來源觀察，不保存raw協定轉錄或宣稱遠端內部路徑已驗。 |
| WEB-AC-029／真Gemini | WEB-006、019、033～039、047、052、064～068、077～078 | 經另次明確授權測搜尋→fetch→引用、指定版本／衝突／來源不足、惡意文件與額度耗盡；檢查回答語意，不以fake模型通過宣稱真模型必然遵循。 |
| WEB-AC-030／回歸 | WEB-001～006、011、050～063、069～072、077～078 | 記實際restore／build／MSTest／敏感Python／help／format／PTY結果與未驗；原01～18安全、原生、test不支援及Release門檻保留，不借本增量宣稱整體驗收完成。 |
| WEB-AC-031／交接 | WEB-007、069～076 | 七筆上游與13筆前向逐項核對章節／需求／案例；FUT-11必要能力與其他FUT獨立交付、原擁有者、限定覆蓋及不另建memo完整。 |

## 13. 本次文件查核與完成條件

| 編號 | 文件要求 |
|---|---|
| WEB-079 | WEB-001～080、WEB-AC-001～032、WEB-HO-001～013唯一連續；所有需求至少一項案例可追溯。核對三JSON範例可解析／Schema及hash一致、URL表、三工具名稱／數量、Markdown圍欄／表格、相對連結／錨點、七上游與13前向交接及敏感外形。 |
| WEB-080 | 完整讀回新檔與ROADMAP差異，執行一般與新檔no-index空白查核；確認只有這兩份文件變動，HEAD／索引不變，無額外memo／產物、清理／提交／推送。本次回報只列實際文件查核；產品離線／PTY／真人／真Learn／真Gemini／原生／Release依實際狀態標待執行或既有未結。 |

| 案例 | 對應需求 | 本次文件成功條件 |
|---|---|---|
| WEB-AC-032／文件 | WEB-001～006、069～080 | 版本／日期／待實作／基線及範圍正確；80需求／32案例／13前向及七上游完整，三範例／hash／URL／Markdown／連結／錨點／敏感外形／空白通過。僅新規格及ROADMAP變動、HEAD／索引不變，沒有產品實作或產品驗收。 |

```sh
git diff --check
git diff --no-index --check -- /dev/null specs/19-web-search-and-document-fetch.md
git diff -- ROADMAP.md
git status --short
git diff --name-only
git diff --cached --name-only
```

no-index查核須區分差異存在與空白錯誤；一般diff不涵蓋未追蹤新檔，不能據此省略新檔診斷。本份完成後，後續開發仍先讀本份／原交接及當時產品，不自動開始實作。

## 14. 官方參考與後續閱讀

官方頁面於2026-10-28核對；只證明所述工具與動態介面方向，不是本版固定協定／Schema／真服務相容性驗收。URL語法、三項必要工具、數值、metadata與拒絕策略為本機產品決策，不宣稱是Microsoft或MCP通用要求。

- [Microsoft Learn MCP開發者參考](https://learn.microsoft.com/en-us/training/support/mcp-developer-reference)：列出三項工具，並要求透過目錄取得當時定義，不保證固定request／response格式。
- [MicrosoftDocs官方服務倉庫](https://github.com/microsoftdocs/mcp)：fetch用途為取得文件頁Markdown，url為文件網址；本機仍以實際安全准入為準。
- [Microsoft Learn MCP說明](https://learn.microsoft.com/en-us/training/support/mcp)、[使用建議](https://learn.microsoft.com/en-us/training/support/mcp-best-practices)：公開文件服務與使用情境，不能當內容正確／最新或本機完成證據。
- [18：MCP原契約](18-mcp-external-tools.md)、[本份URL准入](#url-admission)、[來源投影](#document-source)、[上游承接](#upstream-handoffs)、[前向memo](#future-handoffs)：後續實作及其他FUT的固定入口。
