# 15：工作保存與續接開發規格書

## 第 20 份產品實作接續（2026-10-29）

承接 SAVE-HO-011：SAVE v1 不增加 provider／model／Key／原生歷史或預算。重啟續接由當前設定選模，預覽目前 Provider／Model／目的地並取得新確認；舊存檔依當前政策整份重驗，拒絕不改寫或刪除。 詳見[第 20 份規格](20-multi-model-and-provider-support.md)；A、B 分層證據見 [README](../README.md#model-validation)，真實服務／真人與既有 Release 未結門檻另列。

## 第 19 份產品實作接續（2026-10-28）

承接 SAVE-HO-010：SAVE v1 不增欄位，fetch 來源、URL／時間／hash／view 不直接進工具摘要或 file_refs；自然語言仍可能轉述。續接預設 Disconnected，連結只作歷史，不恢復連線、來源資格、Permit 或外送同意。 詳見[第 19 份](19-web-search-and-document-fetch.md)及其固定交接；當次分層證據與未驗項目見 [README](../README.md#web-validation)。

## 第 18 份產品實作接續（2026-10-28）

SAVE v1 保持，不直接持久化 MCP 連線、目錄、原始結果、版本或 Permit；外部 URI 不進 file_refs。保存與摘要候選綁 MCP 戳記，Stale 受控拒絕而不誤判 store 故障；續接不連線、擷取引用或重播。 詳見[第 18 份](18-mcp-external-tools.md)及其固定交接；實際驗證與未驗層級見 [README](../README.md#mcp-validation)。

## 第 17 份產品實作接續（2026-10-27～28）

承接 SAVE-HO-009：保存摘要／預覽／確認／發布綁技能版本；不直接以技能正文作摘要來源，SAVE v1 無新增欄位。啟動續接 Disabled，歷史／file_refs 只作原安全檔案觀察，不解析或啟用技能。詳見[第 17 份](17-reusable-workflows-and-skills.md)及[當次分層驗證](../README.md#skills-validation)。歷史文件交付敘述保留，未結原生／真人／真實 Gemini／Release 門檻不變。

## 第 16 份產品實作接續（2026-10-27）

工作保存與檔案復原繼續獨立。SAVE v1 不增加 checkpoint bytes、anchor、ID 資格或核准欄位，--resume 無檢查點；RecoveryRevision 使保存候選／量測過時，必要保存摘要含目前復原附錄。清除或換 Key 丟棄程序內復原資料但不刪存檔、不改工作檔、不退生成。 詳見 [第16份](16-change-checkpoints-and-recovery.md) 與 [當次分層證據](../README.md#checkpoint-validation)。下列原階段敘述保留歷史範圍。

## 產品實作接續（2026-10-26）

本份已依使用者後續要求接入產品。SAVE-001～002、077～080 與 SAVE-AC-032 保留文件撰寫當時的交付邊界；本次開發依 SAVE-003～076 及第 10～12 節實作、同步來源與分層驗證，不以原文件查核替代產品證據。當次結果及未驗範圍見 [README](../README.md#work-save-validation)；不提交、推送或發布。

> 文件版本：0.1｜建立日期：2026-10-26｜狀態：待實作規格
>
> 選題依據：[ROADMAP：FUT-08](../ROADMAP.md#fut-08工作保存與續接)。上位文件：[總規格](../SPEC.md)第 5、6、8～15 章。FUT-08 是項目代號，15 是子規格編號，與第 08 份搜尋規格不同。本次僅交付文件，以下介面、存檔及驗收均為後續產品開發要求。

## 1. 目的、選定方案與交付範圍

讓使用者結束工作後，能在下次啟動時接續目標、決策、必要備註與待辦。保存的是可檢視、可能過時的工作脈絡；目前工作區、專案規則、檔案、工具能力及操作授權，仍須由新程序重新取得。

| 編號 | 要求 |
|---|---|
| SAVE-001 | 本次只新增本檔，並最小更新 ROADMAP 的入口、必讀交接及狀態；不修改產品、測試、設定、套件、README、總規格、既有子規格或 Git 索引／歷史，不建立額外 memo、計畫、工作樹、hook 或驗收轉錄檔，不提交、推送或發布。 |
| SAVE-002 | 核對基線為乾淨的 main@1e17c0828a5c91d69dfc214a5cd64ed59ce9c460。第 08～14 份能力已接入產品；以目前 README 與程式為準，舊規格撰寫時的「尚未實作」不是目前狀態。既有測試數不是本次驗收證據。 |
| SAVE-003 | 使用者選定「私人目錄」「摘要加未確認計畫」「保存時產生摘要後確認」「重新啟動時續接」。不採工作區內存檔、任意路徑匯入／匯出、整份 SDK 對話序列化或聊天中替換工作。 |
| SAVE-004 | 首版為單一使用者、本機 macOS；沿用 .NET 10、既有 Google.GenAI、MSTest／MTP、Runner、敏感政策及隱藏輸入，不新增 NuGet、認證來源、API 端點或設定欄位。其他 OS／架構須另有實際驗證，不能回退較弱的檔案操作。 |
| SAVE-005 | 不提供自動保存、當機恢復、自動續跑、背景整理、存檔同步、加密金鑰管理、完整稽核日誌、任意 shell、檔案復原或其他未開發 FUT 能力。工作保存不以 FUT-09 為前置。 |
| SAVE-006 | Key 僅經隱藏輸入取得並留在記憶體。不得保存或顯示固定應用指示、摘要指示、thought、簽章、認證、原始 SDK／HTTP／例外、原始工具資料或終端機轉錄；不得為檢查而傾印這些內容。 |

### 1.1 開發者必讀順序

1. [ROADMAP](../ROADMAP.md)、[README 的目前能力與分層證據](../README.md#latest-validation)、[總規格](../SPEC.md)。
2. [03：CLI 與 Context](03-cli-and-context.md)、[05：工具循環](05-agent-loop.md)、[06：檔案編輯與核准](06-file-edit-and-approval.md)、[07：預算與驗收](07-budget-and-acceptance.md)、[08：搜尋與局部讀檔](08-code-search-and-partial-read.md)。
3. [09：Plan 與任務](09-plan-mode-and-task-list.md)，尤其 [FUT04-HO-001～008](09-plan-mode-and-task-list.md#fut-04-handoff)；[10：受控建置](10-controlled-build-and-test.md)第 12 節；[11：Context 與預算](11-context-and-budget-management.md)第 3、6、9、11、12 節及 CTX-080。
4. [12：專案規則的 RULE-HO-007](12-project-rules-loading.md#future-handoffs)、[13：Git 審查的 GIT-HO-007](13-git-diff-and-code-review.md#future-handoffs)、[14：終端機的 TERM-HO-007](14-terminal-interaction.md#future-handoffs)，再讀本份[承接對照](#upstream-handoffs)。
5. 核對當時 ChatApplication／Bootstrapper／ArgumentParser、ContextCoordinator／ConversationContext／ContextDocuments、PlanStore／PlanArguments、GenerationBudget／AgentRunner、規則協調器、工作區安全讀取、原生程序限制與敏感掃描器；不讀取或附上完整應用指示。

## 2. 模組責任與最小內部契約

| 擁有者／新增內部元件 | 責任 |
|---|---|
| Bootstrapper／啟動協調 | 解析續接 ID，先依既有流程確認使用者選定的工作區及隱藏輸入 Key；將非認證的續接選項交給聊天啟動流程。 |
| ChatApplication／WorkSessionCoordinator | 序列化保存、檢視、刪除及啟動續接；管理 busy、唯一讀取者、確認、候選世代與原子記憶體發布。 |
| WorkSaveDocument／WorkSaveCodec | 不可變白名單資料、嚴格有界解析與明確 JSON writer；不持有 session、client、原生訊息或核准物件。 |
| IWorkSaveStore／macOS 轉接器 | 僅處理固定私人目錄及單一 ID；負責所有權、ACL、種類、穩定讀取、排他發布、精確讀回及單檔刪除，不授予工作區工具新權限。 |
| WorkSaveCandidate／WorkOperationStamp | 綁定 session、Context／pins／Plan／rules 版本、模式、來源與候選 bytes、顯示、檔案參照及當次操作；確認後不可換內容套用。 |
| ContextCoordinator／PlanStore | 提供目前不可變快照及受控匯入候選。載入摘要另有歷史來源標記；任務重建仍沿原欄位／投影限制，不注入操作事實。 |
| AgentRunner／GenerationBudget／UsageAccumulator | 新增內部用途 WorkSaveSummary；同一 client 進行至多一次無工具摘要，沿唯一生成、傳輸及用量記帳。 |
| WorkOperationResult | Status、固定 ErrorCode、可空 WorkId、PublicationState、CleanupFailed／StoreStopped 及可得用途用量；與 LastTurnOutcome、Plan 事實帳本分開。 |

| 編號 | 契約要求 |
|---|---|
| SAVE-007 | 公開 ILlmClient／LlmRequest／LlmResult／LlmMessage 與工具 Schema 不變。新增資料與協調介面均為 internal，SDK 型別留在 Llm/；不得以一般物件序列化匯出 ContextSnapshot、PlanSnapshot、BootstrapSession 或服務。 |
| SAVE-008 | 含文字／路徑／集合的物件使用密封類別、唯讀屬性、防禦性複製及固定 ToString 標記。文件格式版本、摘要版本、requirements_version、目前 rules／Plan 版本是不同契約，不互相代用。 |
| SAVE-009 | 新工作操作共用活動 session、決策閘門及取消／停止路徑；不持決策鎖等待模型、終端機、檔案 I/O 或舊讀取者。工作區參照查核共用原 WorkspaceOperationCoordinator 租約，不巢狀取鎖；私人存檔另有短時間跨程序儲存交易鎖。 |
| SAVE-010 | 僅使用者本機指令及啟動續接流程可呼叫保存服務。Plan 模式可由使用者明確保存／刪除私人存檔；這是本機命令的限定副作用，不是模型 WorkspaceWrite／程序能力，不得開放 Plan 工具寫入。 |
| SAVE-011 | 保存／續接不得呼叫公開 read_file 來補基準、提交假使用者回合，或向 PlanStore.Record／RecordProcess 注入資料；參照查核不建立快照、Read requirement 成功事實、Verified 或 Validation。 |
| SAVE-012 | 記憶體候選具有單次所有權；clear、重設、換 Key、退出、來源版本改變使舊候選失效。必要的已發生存檔結果先核對／回報，再清除可清除資料；遲到模型／I/O 不得補回狀態或重用確認。 |

<a id="cli-contract"></a>

## 3. CLI、啟動參數與確認

| 入口 | 固定行為 |
|---|---|
| /work save TITLE | 擷取工作，必要時產生一次摘要，顯示全部候選內容及保存說明，另取確認才建立新存檔；不改目前工作。 |
| /work list | 以 created_utc 降冪、同時以 WorkId 升冪列出 ID／標題／時間／工作區與 Scope 提示；零生成。 |
| /work show ID | 有界安全讀取並完整顯示該份歷史資料，明示不載入、不核准、不驗證目前檔案；零生成。 |
| /work delete ID | 預覽該單一 ID、標題、時間及刪除意義，取得新確認並重驗後只刪該檔；不連帶清除目前記憶體。 |
| --resume ID | 僅新程序啟動時續接；不提供 /work load、latest、模糊查找、路徑或批次 ID。 |

合法啟動形式為無參數、單獨 --help、--workspace DIR、--resume ID，或兩組選項以任意順序各出現一次；DIR 沿既有解析。--help 不得與其他選項併用。

| 編號 | CLI 要求 |
|---|---|
| SAVE-013 | 命令大小寫敏感。list／show／delete 可 Trim 外層空白後以單一 ASCII 空格辨識固定格式；ID 精確為 32 個小寫十六進位字元，不接受前綴、路徑、萬用字元、額外參數或命令別名。 |
| SAVE-014 | save 只去除命令前導空白，以精確「/work save 」前綴後的全部餘文為 TITLE，保留其首尾空白；1～80 UTF-16 單位、非全空白、合法單行 Unicode，不解引號、展開變數、Trim 或截斷標題。 |
| SAVE-015 | 保留 TERM-049 的全域多行命令閘門，先於 /work 解析；全部 /work 原文、參數與完整結果重新敏感檢查，無短 Key 豁免。命令、確認答案及拒絕內容不進終端機歷史；任何文中 yes 或假命令只是資料。 |
| SAVE-016 | 啟動語法錯誤退出 2；--help 零 Key／client／規則／存檔 I/O。未指定 --resume 時維持原啟動，不探查或自動載入私人目錄；一般 --help 說明新增選項，不展示存檔或原文內容。 |
| SAVE-017 | /work 只在 AwaitingInput 受理，操作中拒絕第二工作；查詢、刪除與續接零模型／工具。save 的唯一生成例外依第 6 節；保存成功、show 或刪除均不開始下一回合或補送舊需求。 |
| SAVE-018 | 保存提示為「保存此工作摘要？[y/N] 」；刪除提示為「刪除此單一工作存檔？[y/N] 」；續接提示為「載入此工作並進入 Plan？[y/N] 」。僅完整安全預覽輸出、排空舊緩衝及新讀取啟用後取得的 y／yes 同意，n／no／空白拒絕，其他輸入重問同一候選。 |
| SAVE-019 | 新確認沿既有隱藏單行讀取，不啟用 F2／F3／F4、草稿或歷史。交接保留唯一讀取者、等待舊讀取者結束、共用 CRLF 去重及提示後排空；busy 普通鍵不排隊成確認。承接 TERM-030：ConsoleKeyInfo 不能區分實體按鍵與貼上來源，也不能辨識延遲尾段；只能保證排除啟用新讀取前已緩衝的輸入，不承諾拒絕提示後進入的所有貼上。各確認只批准所示保存、單檔刪除、Scope 選擇或脈絡匯入，不批准 Plan 或工具。 |
| SAVE-020 | /work 操作及其確認的 Ctrl+C 取消當次並回一般輸入；Ctrl+D／EOF 正常退出 0，終端機／輸出故障退出 1。啟動續接任何確認拒絕／EOF 退出 0，Ctrl+C 退出 130；不悄悄改成空白工作繼續。發布開始後的取消仍須完成第 7 節結果查核。 |

<a id="private-store"></a>

## 4. 私人儲存、存取控制與保留

固定根以作業系統的使用者家目錄 API 取得，再接 .aiagent/work；不從環境變數讀取認證，也不接受模型／規則／存檔改寫位置。每份正式存檔為 ID.json，ID 由本機隨機產生；標題不進檔名。

| 名稱 | 上限／政策 |
|---|---|
| WorkStoreDirectory／WorkFileMode | .aiagent 與 work 各為 0700；正式檔、交易鎖及自有暫存為 0600。 |
| MaxWorkSaveFiles | 100 個符合正式檔名的目錄項；損壞、連結或無法讀取的同名項也占額度。 |
| MaxWorkStoreEntries | 每次列舉最多 1024 個直接子項；第 1025 個即拒絕，不遞迴、不部分列出。 |
| MaxWorkDocumentBytes／MaxWorkDisplayBytes | 各 65536 個嚴格 UTF-8 bytes；完整外層、標籤、跳脫及提示都計入相應上限。 |
| MaxWorkJsonDepth | JSON 深度 16；數值、字串與集合另依第 5 節限制。 |
| MaxWorkFileRefs／MaxReferenceFileBytes | 160 個唯一 requirement 路徑；單一普通檔最多 131072 原始 bytes，至多讀上限加一偵測增長。 |
| 暫存／鎖 | .aiagent-work- 加本機 32 位小寫十六進位值再加 .tmp；固定 .store.lock 僅用於儲存交易，不含工作內容。 |

| 編號 | 儲存要求 |
|---|---|
| SAVE-021 | 第一次明確 save 才可建立缺少的 .aiagent、work 及鎖檔；list 在根缺席時顯示空集合，show／delete／resume 為 work_not_found，不建立目錄。只建立上述已知層級，不搜尋其他帳號、舊路徑或備份。 |
| SAVE-022 | 從已驗證的檔案系統根目錄 handle 無跟隨逐層走到家目錄與儲存根。家目錄以上的系統祖先可由 UID=0 或目前 UID 所有；家目錄及其下 .aiagent／work 必須由目前 UID 所有。各層均為普通非連結目錄；系統祖先及家目錄的 mode／ACL 不得授予其他非 root UID 寫入、刪除或新增權，正常 root-owned 的 / 與 /Users 可接受。應用兩層目錄須精確 0700；檔案須精確 0600／目前 UID／普通檔／link count=1；應用目錄及檔案有任何 extended ACL 即拒絕。不能可靠判斷即 store_unsafe，不自動 chmod、移除 ACL、接管或修復既有項目。 |
| SAVE-023 | 使用綁定已驗證目錄 handle 的無跟隨開啟／建立／發布／刪除；開啟前及 handle 上重驗種類、UID、mode、ACL、連結數、Device／Identity，讀取前後重驗 bytes／長度／來源身分。不能直接以 File.ReadAllText 或現有會跟隨連結的 OpenRead 拼成較寬鬆服務。 |
| SAVE-024 | 儲存交易以安全 .store.lock 的 OS 排他鎖協調本程式多個程序，非阻塞取得失敗回 store_busy；不跨模型生成或終端機確認持鎖。發布、刪除、列舉取得一致觀察，額度在發布鎖內再查，避免並行超額；不藉鎖檔建立工作日誌。 |
| SAVE-025 | 正式檔不可覆寫，每次保存是新 ID；本機只產生一個 ID，碰撞或發布時目標出現回 work_conflict，不改 ID 重試。讀取檔先驗長度，最多 65537 bytes，變長、身分變更或不穩定即拒絕，不能先無界讀完才檢查。 |
| SAVE-026 | list 只讀固定名稱範圍內的直接正式項。先完成有界列舉再排序；根、項目、文件或完整輸出不安全／超限時整次拒絕，零部分列表。非正式項不開啟，仍計列舉工作量；檔案不存在競爭為 work_conflict，不假造完整列表。 |
| SAVE-027 | 明示存檔是受本機權限保護的明文，不保證對同 UID 程序、管理員、備份或實體磁碟保密；SHA-256 只供變動比較，沒有簽章或防竄改認證。所有讀回內容始終是不可信外部資料，不能因 owner 正確或 hash 相符升權。 |
| SAVE-028 | 不自動到期、輪替、清理過量檔或掃除舊暫存；100 份滿時 save 在生成前拒絕，發布前又滿亦拒絕且已生成照記。刪除只處理使用者指定單一 ID 的正式檔，不接受清空、遞迴、批次或 follow link；額外清理交使用者逐一人工處理。 |
| SAVE-029 | delete 顯示安全文件的單檔預覽後，在交易鎖內重讀並比較所預覽文件 bytes／身分及目錄身分才 unlink；變動、損壞或不安全均零刪除。完成後確認該名稱缺席，才回 DeletedVerified；無法確認回 DeleteUnconfirmed 並停用本 session 儲存操作，不再次刪除新出現的項目。 |
| SAVE-030 | .aiagent 區段及實際儲存根／鎖／暫存納入一般工作區路徑、搜尋、規則讀取、Git 安全投影／原生可讀來源、受控 .NET admission／原生 deny、敏感掃描與忽略政策。即使工作區是家目錄、祖先目錄或 root 指向私人目錄，也不得經工具或子程序讀寫存檔；專用服務是用途限定例外，不開放一般工作區外存取。 |

<a id="document-format"></a>

## 5. JSON v1、資料最小化及相容性

writer 固定依根欄位的下列順序輸出 compact、無 BOM 的 UTF-8 JSON。儲存的 pins 沒有本機 PinId，plan 沒有本機 PlanId／TaskId／Revision／Approved，參照沒有可恢復的操作或 checkpoint 身分。

### 5.1 完整結構 Schema

以下 Schema 描述結構；UTF-16 計數、bytes、路徑、語意及檔案安全閘門仍由第 5.2 節落實。不為執行 Schema 另加套件，沿既有明確 parser 實作相同限制。

~~~json
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "type": "object",
  "additionalProperties": false,
  "required": ["format_version", "work_id", "title", "created_utc", "workspace", "scope_hint", "rules_refs", "summary", "pins", "plan", "file_refs"],
  "properties": {
    "format_version": {"const": 1},
    "work_id": {"type": "string", "pattern": "^[0-9a-f]{32}$"},
    "title": {"type": "string", "minLength": 1, "maxLength": 80},
    "created_utc": {"type": "string", "pattern": "^[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2}Z$"},
    "workspace": {
      "type": "object", "additionalProperties": false,
      "required": ["root", "device", "identity"],
      "properties": {
        "root": {"$ref": "#/$defs/path"},
        "device": {"type": "string", "pattern": "^(0|[1-9][0-9]{0,19})$"},
        "identity": {"type": "string", "pattern": "^[1-9][0-9]{0,19}$"}
      }
    },
    "scope_hint": {"$ref": "#/$defs/path"},
    "rules_refs": {
      "type": "array", "maxItems": 8,
      "items": {
        "type": "object", "additionalProperties": false,
        "required": ["path", "sha256"],
        "properties": {"path": {"$ref": "#/$defs/path"}, "sha256": {"$ref": "#/$defs/hash"}}
      }
    },
    "summary": {"oneOf": [{"type": "null"}, {"$ref": "#/$defs/summary"}]},
    "pins": {"type": "array", "maxItems": 16, "items": {"$ref": "#/$defs/shortText"}},
    "plan": {"oneOf": [{"type": "null"}, {"$ref": "#/$defs/plan"}]},
    "file_refs": {
      "type": "array", "maxItems": 160,
      "items": {
        "type": "object", "additionalProperties": false,
        "required": ["path", "observation", "byte_count", "sha256"],
        "properties": {
          "path": {"$ref": "#/$defs/path"},
          "observation": {"enum": ["missing", "file", "directory", "unavailable", "outside_scope"]},
          "byte_count": {"type": ["integer", "null"], "minimum": 0, "maximum": 131072},
          "sha256": {"oneOf": [{"type": "null"}, {"$ref": "#/$defs/hash"}]}
        }
      }
    }
  },
  "$defs": {
    "path": {"type": "string", "minLength": 1, "maxLength": 4096},
    "hash": {"type": "string", "pattern": "^[0-9a-f]{64}$"},
    "shortText": {"type": "string", "minLength": 1, "maxLength": 1024},
    "texts": {"type": "array", "maxItems": 16, "items": {"$ref": "#/$defs/shortText"}},
    "summary": {
      "type": "object", "additionalProperties": false,
      "required": ["version", "goals", "decisions", "open_questions", "background"],
      "properties": {
        "version": {"const": 1},
        "goals": {"$ref": "#/$defs/texts"},
        "decisions": {"$ref": "#/$defs/texts"},
        "open_questions": {"$ref": "#/$defs/texts"},
        "background": {"$ref": "#/$defs/texts"}
      },
      "anyOf": [
        {"properties": {"goals": {"minItems": 1}}},
        {"properties": {"decisions": {"minItems": 1}}},
        {"properties": {"open_questions": {"minItems": 1}}},
        {"properties": {"background": {"minItems": 1}}}
      ]
    },
    "requirement": {
      "oneOf": [
        {
          "type": "object", "additionalProperties": false, "required": ["tool", "path"],
          "properties": {
            "tool": {"enum": ["list_files", "read_file", "create_file", "replace_text", "search_files", "search_text"]},
            "path": {"$ref": "#/$defs/path"}
          }
        },
        {
          "type": "object", "additionalProperties": false, "required": ["tool", "path", "start_line", "end_line"],
          "properties": {
            "tool": {"const": "read_file_range"}, "path": {"$ref": "#/$defs/path"},
            "start_line": {"type": "integer", "minimum": 1, "maximum": 2147483647},
            "end_line": {"type": "integer", "minimum": 1, "maximum": 2147483647}
          }
        },
        {
          "type": "object", "additionalProperties": false, "required": ["tool", "path", "task"],
          "properties": {
            "tool": {"const": "run_dotnet_task"}, "path": {"$ref": "#/$defs/path"},
            "task": {"enum": ["build", "test"]}
          }
        }
      ]
    },
    "plan": {
      "type": "object", "additionalProperties": false, "required": ["objective", "tasks"],
      "properties": {
        "objective": {"type": "string", "minLength": 1, "maxLength": 2048},
        "tasks": {
          "type": "array", "minItems": 1, "maxItems": 20,
          "items": {
            "type": "object", "additionalProperties": false,
            "required": ["kind", "title", "acceptance", "requirements_version", "requirements", "reported_state", "reported_basis", "summary", "block_reason"],
            "properties": {
              "kind": {"enum": ["analysis", "read", "write", "validation"]},
              "title": {"type": "string", "minLength": 1, "maxLength": 256},
              "acceptance": {"$ref": "#/$defs/shortText"},
              "requirements_version": {"enum": [1, 2]},
              "requirements": {"type": "array", "maxItems": 8, "items": {"$ref": "#/$defs/requirement"}},
              "reported_state": {"enum": ["pending", "in_progress", "completed", "blocked"]},
              "reported_basis": {"enum": ["none", "local_operation", "model_assessment"]},
              "summary": {"type": ["string", "null"], "minLength": 1, "maxLength": 2048},
              "block_reason": {"type": ["string", "null"], "minLength": 1, "maxLength": 64}
            }
          }
        }
      }
    }
  }
}
~~~

### 5.2 語意、來源及容量

| 編號 | 格式要求 |
|---|---|
| SAVE-031 | 每層物件欄位必須完全符合 Schema；拒絕重複／未知欄位、註解、尾逗號、BOM、非法 UTF-8／代理字元、Markdown fence、額外內容及深度超限。整數只接受原始十進位非負整數拼字，不接受 1.0、指數、字串或負零；format_version／summary.version 為原始 1，未知版 work_version_unsupported，不自動降版／遷移／重寫。 |
| SAVE-032 | 字串長度再以 UTF-16 單位檢查，禁止控制字元與 U+2028／U+2029、全空白；標題／pins／摘要保留原文，不正規化或截短。Plan 定義須已符合目前 PlanArguments 的正規化文字與路徑；載入不默默 Trim 改寫。created_utc 以 UTC 秒精度嚴格解析且 round-trip 相同；device／identity 必須可解析 UInt64，identity 非零；work_id 等於檔名 ID。 |
| SAVE-033 | workspace.root 是已確認工作區的實際正規絕對路徑及目錄身分，只作匹配不作解析來源。其餘 path／scope_hint 依現有工作區正規相對路徑及敏感規則；rules_refs 只列目前有效鏈的 AGENTS.md 路徑／原始 bytes SHA-256、按根到葉排序、唯一且在 hint 的祖先鏈，最多 8 筆，不保存全文、RulesRevision 或缺席快取。 |
| SAVE-034 | summary 為 null 或第 11 份四類集合的合法非空摘要，content 原始輸出／canonical 各守 16384 bytes。pins 最多 16 個原文，各 1024 UTF-16，儲存的 pins envelope 及重新配 ID 後的既有 ContextDocuments.Pins 均須不超過 8192 bytes；重複 pins 保留。整份文件 65536、完整預覽 65536、載入後記憶附錄 32768、Plan 實際／保守投影 32768 及完整顯示 65536 皆獨立驗證，任一超限整份拒絕，不裁資料。 |
| SAVE-035 | plan.tasks 按原順序存最新一份計畫，沒有本機 ID／核准／事實。kind 與 requirements_version／requirements 的組合完全沿目前 PlanArguments：analysis、validation v1 必須空；read／write 及 validation v2 為 1～8 項，各自工具集合、range 大小與 .csproj 精確限制不變，重複 requirement 拒絕。reported_state／basis／summary／block_reason 只代表歷史轉述；非 completed 的 basis=none，completed 的 analysis 為 model_assessment、其他為 local_operation；只有 blocked 有受控 block_reason，其餘為 null。 |
| SAVE-036 | file_refs 精確對應 plan requirements 去重後的全部 path，按 Ordinal 排序；無 plan 則空集合，不容許額外路徑。file 觀察才有原始長度與 SHA-256，其餘兩欄為 null。以目前 Scope 及安全內部讀取查核：普通小檔計 hash、既有目錄只查種類、目標缺席為 missing；超限／連結／種類或讀取不合規為 unavailable，Scope 外為 outside_scope 且零目標 metadata／內容 I/O。觀察永不抵執行時的完整讀檔或規則閘門。 |
| SAVE-037 | 不保存操作 ID、completionFactIds、Verified／Validation 結果、程序輸出、快照、Git oid／finding／ledger、預算／費用／用量、模型認證或舊模式。原文歷史可能提到這些字詞，仍只是資料；parser 不從文字提取授權。檔案指紋及 reported_basis 不代表目前已驗證、修改者或復原基準。 |

### 5.3 合成範例

下例僅示意格式；工作區身分是虛構值，不是目前機器狀態或可用續接工作。刻意不加入任何可命中憑證外形的假資料。

~~~json
{
  "format_version": 1,
  "work_id": "0123456789abcdef0123456789abcdef",
  "title": "補齊匯入功能",
  "created_utc": "2026-10-26T08:00:00Z",
  "workspace": {"root": "/Users/example/Project", "device": "1", "identity": "12345"},
  "scope_hint": ".",
  "rules_refs": [],
  "summary": {
    "version": 1,
    "goals": ["完成輸入格式的設計。"],
    "decisions": ["先釐清格式，再實作。"],
    "open_questions": ["是否需要支援空白欄位？"],
    "background": ["過去分析不代表目前檔案已通過驗證。"]
  },
  "pins": ["保留原有公開介面。"],
  "plan": {
    "objective": "確認匯入格式與驗收條件。",
    "tasks": [
      {
        "kind": "analysis",
        "title": "整理格式選項",
        "acceptance": "列出必要欄位與失敗案例。",
        "requirements_version": 1,
        "requirements": [],
        "reported_state": "blocked",
        "reported_basis": "none",
        "summary": "等待確認空白欄位的處理。",
        "block_reason": "model_reported"
      }
    ]
  },
  "file_refs": []
}
~~~

<a id="summary-generation"></a>

## 6. 保存摘要、來源與唯一記帳

一般 /compact 只整理舊前綴並保留最新兩回合，不能直接拿它的結果宣稱涵蓋最新工作。本份另取保存候選，不呼叫 /compact，也不把保存候選發布成目前 Context。

| 編號 | 摘要要求 |
|---|---|
| SAVE-038 | 在目前規則 Valid、無活動回合／任務預留時擷取不可變 Context／pins／Plan／rules 與 session stamp。有任何保留成功回合時，以同 client 的 ILlmContextInspector.ProjectCompletedTurn 投影全部回合，並加入有效既有摘要；包含最新兩回合。只取第 11 份白名單文字與安全工具觀察，另排除 structured Git snapshot_id／來源識別。失敗鏈、編輯器歷史、草稿、未提交回答、arguments、原始工具內容、規則正文與固定指示不得直接成為來源。 |
| SAVE-039 | 有載入摘要時，它是有效摘要來源及非空判定的一部分；不能只查 ConversationContext.Summary／CompletedTurns。無新成功回合時，直接重用完整有效摘要、原文 pins 及目前計畫，零生成；只有 pins／plan 也可零生成保存，summary=null。摘要、pins、plan 與歷史續接計畫皆不存在才 nothing_to_save，零生成／寫入。 |
| SAVE-040 | 需要摘要時，來源 envelope 沿第 11 份的 version／previous_summary／turns，最多 131072 UTF-8 bytes；單一新 FromUserText、Tools=[]、專用最小摘要指示，由同一 Runner／client 發一次請求，不把原生歷史續傳。輸出沿第 11 份四集合 Schema 及 16384 bytes，只有 Completed、零工具、無 RequiresContextReset 且安全合法才建立候選。 |
| SAVE-041 | 生成前先檢查當前規則、儲存安全／份數、來源／候選容量及程序剩餘額度；使用 WorkSaveSummary 用途且當次上限固定為一次，不提高既有可設定回合／程序上限。沿原 120 秒、8192 output tokens、1 candidate、196608 估算門檻及 262144 實際 SDK body 上限。只有真的進入生成閘門才消耗次數；失敗、取消、拒絕保存及最後容量／額度衝突仍不退款。 |
| SAVE-042 | 同一 UsageAccumulator 記錄生成、傳輸及 API 可得用量；不另建 client、token API、補請求、隱藏 retry 或第二次模型校驗。WorkOperationResult 區分零生成與已產生摘要；不覆寫 LastTurnOutcome／LastMaintenanceOutcome，也不新增成功回合、Plan 事實或工具輸出；API 缺值／溢位及程序累計沿原契約。 |
| SAVE-043 | pins、Plan 定義／狀態、rules_refs、workspace 身分及 file_refs 都由本機候選擷取，不讓模型指定、刪改或由摘要解析回填。模型摘要有損，不能承諾保留所有決策；必留原文只能靠獨立 pins／本機狀態。完整顯示摘要及所有保存欄位，使用者可拒絕並另以現有 pin／一般對話更正後重新保存。 |
| SAVE-044 | 來源、回覆、解碼字串、canonical JSON、完整記憶／Plan 附錄及顯示均驗敏感與容量。普通 JSON／Schema 不合回 work_summary_failed 並保留安全目前工作；原敏感、非法協定、配對、認證或本機致命失敗沿原更嚴格處理。禁止修 JSON、丟回合、截短或重送以湊出存檔。 |

<a id="save-publication"></a>

## 7. 保存候選、確認與發布

固定順序為：前檢 → 擷取 stamp／來源 → 必要的一次摘要 → 重新驗證 rules／stamp → 安全取得檔案參照 → 形成完整文件與預覽 → 新確認 → 最後重驗 → 私人儲存交易 → 排他發布 → 精確讀回 → 安全結果。

| 編號 | 發布要求 |
|---|---|
| SAVE-045 | stamp 包含 session generation、Context／pins revision、Plan generation／snapshot／模式、rules revision／generation／Scope 及已確認工作區身分；不把程序用量的正常增加當來源衝突。摘要與確認返回後逐一比對，並依原規則來源閘門重驗；改變即 work_revision_conflict／原 rules 錯誤，零新暫存／發布，不把 yes 移到新版。 |
| SAVE-046 | file_refs 使用同工作區安全讀取核心的專用唯讀路徑，最多 160 個、各 131072 bytes 加一；不開啟公開工具或增加事實。保存確認後再查同一參照集合；觀察或 bytes／身分變動拒絕，不在已確認候選中換新 hash。沒有 plan 時不額外搜尋檔案；範圍外只記 outside_scope。目錄缺席與父鏈失效分開，工作區根或規則來源失效仍整次停止。 |
| SAVE-047 | 預覽顯示完整標題、ID、UTC 時間、工作區匹配資訊、Scope 提示、規則／檔案參照、摘要、pins、最新歷史計畫及其不可信狀態，並說明明文保存、可能送出摘要來源、生成用量、尚未保存與不恢復授權。顯示整塊上限 65536，含標籤及提示；過大、敏感碰撞或輸出失敗不得省略後接受核准。 |
| SAVE-048 | 取得確認及最終重驗後，在短交易鎖內再查目錄／容量／目標缺席，才 CreateNew 自有 0600 單檔暫存，寫入已確認 bytes、flush 至磁碟、驗 handle／長度，再用原子且不可覆寫的發布。任一步不支援即拒絕，不回退直接覆寫／先刪目標／非原子複製，不修改目前工作區檔案。 |
| SAVE-049 | 發布前取消或失敗為 NotPublished，僅可逐一刪除這次確定擁有的暫存。取得發布決策權後即使取消，仍完成必要收尾，以安全有界讀取核對正式檔 bytes、身分、權限及來源根；完全相同才 PublishedVerified。已呼叫發布而結果不可判定或讀回不符為 PublishedUnconfirmed，不能回報「未保存」或再試一次。 |
| SAVE-050 | 正式檔發布不明或自有暫存刪除／關閉無法確認時，保留 PublicationState 並標 CleanupFailed 或 StoreStopped；本 session 不再開始儲存讀寫／刪除，安全聊天資料仍保留。僅顯示可安全形成的單檔 ID／暫存名稱供人工處理，不自動刪正式檔、掃其他暫存或回復工作區。終端機、原模型／程序致命故障仍按原優先序退出 1。 |
| SAVE-051 | 正常 save 成功或單純儲存拒絕不切模式、不改有效摘要／pins／Plan、現有操作事實或工作區快照；規則失效與原敏感／協定安全重設仍依其既有契約撤銷／清除，不能因保存而略過。預算／可得用量照實增加。新存檔在該次程序內精確讀回不等於抗斷電、備份完成或永久保存保證；不額外建立日誌、備份副本或恢復標記。 |

<a id="resume-lifecycle"></a>

## 8. 啟動續接、工作重驗與原子匯入

固定順序為：解析參數 → 確認使用者選定的工作區 → 隱藏輸入新 Key → 固定存檔安全讀取 → 工作區匹配 → 根規則初始化 → 必要的明確 Scope 選擇 → 目前來源及檔案參照查核 → 完整續接預覽／確認 → 一次記憶體發布 → Plan 等待輸入。

| 編號 | 續接要求 |
|---|---|
| SAVE-052 | 存檔內容 I/O 僅在工作區確認及 Key 政策就緒後開始；不從存檔取得 Key、client、模型設定或工作區。workspace.root 必須與目前正規根精確相等，Device／Identity 也須相符；不同、搬移、重建或別名工作區回 work_workspace_mismatch，零參照目標 I/O／匯入，不猜測 Git remote 或提供 v1 映射。 |
| SAVE-053 | 啟動初始 Scope 仍為「.」，根規則依目前來源讀取；hint 不同時，先顯示安全 hint 與「選擇此 Scope？[y/N] 」，以第 3 節的新輸入規則取得使用者選擇，才呼叫原 TryChangeScopeAsync 讀該鏈。這是啟動時的明確使用者選擇增量，不是存檔觸發自動切換；不允許選擇其他未預覽 Scope，也不繞過 /rules 的路徑與來源閘門。 |
| SAVE-054 | 當前規則必須 Valid；rules_refs 與目前來源清單／hash 不同，於續接預覽明示「規則已變動，舊記錄不代表符合目前規則」，不把舊全文或 hash 當授權。新規則敏感／超量／不可讀、hint 不存在或不安全時退出 1，零匯入；無有效規則不能用空集合降級。 |
| SAVE-055 | 重新比較 file_refs 的範圍、存在、種類與安全小檔 hash；依目前來源標 unchanged／changed／missing／unverified，unavailable 或 outside_scope 不得判 unchanged。僅讀目前 Scope 允許的參照，不從摘要／工作區外補資料、不全專案掃描；差異或缺檔可繼續匯入背景，但完整列出且全部任務仍需新證據。原生根／來源安全故障則整次停止。 |
| SAVE-056 | 預覽顯示保存時間、完整摘要／pins、全部歷史任務及舊 basis 只是未驗證轉述、預計重建的待辦、目前工作區／Scope／規則狀態、全部參照差異及未核對範圍，並明示不執行、不恢復檔案、無有效核准。此頁不顯示目前規則正文或 raw file；使用者可在載入後 /rules show 看目前完整規則。全部預覽受 65536 bytes 及新政策檢查，不能截短後匯入。 |
| SAVE-057 | 確認後再次安全重讀同 ID bytes／檔案身分，並核對工作區、規則、file_refs 觀察及候選 stamp；期間任何變化回 work_revision_conflict，零工作記憶體匯入，不用剛讀的新內容替換已確認候選。新的 Context／pins／Plan／歷史來源全數通過各自容量後，在同一決策點一次發布；失敗不能留下半份 pins 或半個計畫。 |
| SAVE-058 | 不匯入原始成功回合。摘要以來源 saved_work_summary、WorkId 及不可信歷史標記納入既有記憶附錄的單一有效摘要位置；不與另一份完整摘要重複占容量，不填舊 ThroughTurnId／CoveredTurnCount。pins 配新的本機 PinId，文字精確保留但標為已確認匯入的歷史備註，不能假造新的使用者指示或規則來源。 |
| SAVE-059 | 僅歷史 state 非 completed 的任務重建為新 PlanId／TaskId、Revision=1、Draft、Pending、basis=None、空證據／summary／block reason；保留原 kind／title／acceptance／requirements_version／requirements。completed 只在有界歷史檢視與摘要背景中展示，不當目前 Completed，也不自動重做；沒有未完成任務就不建立活動計畫。全程進入 Plan，/execute 仍須獨立確認，之後等新一般訊息。 |
| SAVE-060 | 匯入不改 requirements_version、不把 test 或 Validation v1 提升成可用能力。原計畫內容若不符合目前參數／投影／敏感限制，整份 work_plan_invalid，不只載入一部分任務。現有允許但執行能力不可用的 test 條件可以保留為 Pending，後續執行仍依既有 capability_unavailable；歷史 basis 不建立完成事實。 |
| SAVE-061 | 保存來源、非空判定與第一次／後續 compaction 都必須取得「單一有效摘要」，包括尚未壓縮的載入摘要；新成功回合數從零起，只計本程序。第一次壓縮以載入摘要作 previous_summary，合併後取代同一摘要位置，保留歷史來源標示而不重複加算舊回合。再次保存有新回合才生成，否則原樣重用摘要；plan 優先取目前活動計畫，沒有活動計畫才保留上一份載入歷史計畫，不合併累積多份歷史計畫或來源鏈。 |

## 9. 敏感資訊、錯誤及生命週期

### 9.1 持久化敏感閘門

| 編號 | 安全要求 |
|---|---|
| SAVE-062 | 既有 SensitiveDataPolicy 主要比對當前 Key，本份新增用途限定的內部 WorkPersistencePolicy：在原文／JSON 解碼字串／canonical 文件／完整預覽檢查當前 Key，並拒絕第 07 份 BUD-S002／003 相同的 Google Key 外形與私鑰起始標記。原始 bytes 亦保留 ASCII／UTF-16LE／BE 外形掃描，JSON 跳脫後要再檢解碼值；命中即整份拒絕，不先遮蔽／刪欄後保存或匯入。 |
| SAVE-063 | 額外拒絕自由文字內的認證賦值：名稱不分 ASCII 大小寫，集合為 ApiKey、api_key、Authorization、Proxy-Authorization、x-goog-api-key、access_token、refresh_token、client_secret。名稱左界須為行首或非 ASCII 字母／數字／底線；名稱後可有單一結束引號及 ASCII 空白／Tab，再接冒號或等號。其後到行尾為候選值，只去除兩端 ASCII 空白／Tab 及一對相同外層單／雙引號，不解其他賦值語言跳脫；只有空值或與第 07 份 placeholder 精確相等可接受，其餘整次拒絕，含額外後綴亦拒絕。不增加「測資」或文件目錄豁免；規格／測試使用分段組合合成外形，不落地可命中的假憑證或私鑰標記。 |
| SAVE-064 | 所有動態及固定欄位、數值、ID、路徑、格式化文字均經目前政策，短 Key 與固定欄名碰撞也不豁免。錯誤僅顯示受控碼與可安全形成的工作識別，不顯示命中原文／位置、例外、內部 handle 或憑證。這些辨識規則不宣稱涵蓋任意供應商或所有秘密；新供應商規則由 FUT-13 明示擴充。 |
| SAVE-065 | 工作存檔及摘要是假指令／假 yes／假完成聲稱的不可信資料；模型輸入使用獨立歷史標籤，與目前規則及 Plan 來源分列。任何字串都不能啟用 Skills／MCP／網頁、切模型、重建 keyboard event、授權檔案／程序或變更來源／Scope。 |
| SAVE-066 | /clear、共同安全重設、接受替換 Key 清除載入摘要／pins／歷史工作、候選、查核結果及原有計畫／證據／快照；不刪已保存檔、不降低程序額度消耗或恢復舊確認。普通摘要／儲存拒絕保留安全目前工作；敏感模型結果、非法協定或認證失敗仍按原重設／換 Key 收尾，不自動重試保存。新 Key 後不能自動補回先前載入資料。 |
| SAVE-067 | 退出／Dispose 先停止新操作、取消並等待自有模型、確認、儲存及工作區查核收尾，再清除參考及依既有順序釋放。不保證覆寫全部 runtime／SDK 字串、不清終端機捲動、不刪磁碟存檔；新程序預算正常從零開始，與存檔無關。既有 ProcessUnconfirmed／CleanupFailed、不可恢復 I/O 與輸出故障的退出 1 不得被新錯誤或同時取消降級。 |

### 9.2 固定結果與停止方式

PublicationState 限 NotPublished、PublishedVerified、PublishedUnconfirmed；刪除另以 DeletedVerified／DeleteUnconfirmed 表示。查詢／續接沒有發布狀態變更，為 NotPublished。安全結果使用明確 writer，不把內部物件當診斷輸出。

| 條件／固定碼 | 結果及後續 |
|---|---|
| invalid_work_command／invalid_work_title／invalid_work_id | 本機輸入拒絕，零生成／存檔 I/O；啟動語法錯誤用既有 InvalidArguments／2。 |
| nothing_to_save | 零生成、零檔案建立；保留目前工作。 |
| work_not_found／work_version_unsupported／work_format_invalid／work_plan_invalid | /work 回安全拒絕，啟動續接退出 1；不遷移或修正檔案。 |
| store_unsafe／store_unavailable／store_busy／store_limit | 零新發布；save 能前檢時零生成；本機回輸入，啟動續接退出 1。 |
| work_capacity／work_summary_failed／work_revision_conflict／work_conflict | 整份拒絕，不部分發布／匯入，已開始生成照記；本機回輸入，啟動續接退出 1。 |
| work_workspace_mismatch | 啟動續接退出 1，不探查參照目標、不自動換工作區。 |
| generation_limit／program_generation_limit／context_measure_unavailable／原模型錯誤 | 依原用途與預算分類停止保存，顯示可得用量；不退款、不提高額度或補送。 |
| rules_changed／rules_unavailable／rules_scope_mismatch／rules_limit | 沿既有來源撤確認／快照與收尾；不自動 reload／切 Scope／續跑；啟動續接失敗退出 1。 |
| work_sensitive_content | 讀入存檔不發布／匯入任何內容；save 的模型／目前 Context 敏感故障依原更嚴格重設。磁碟候選拒絕不以刪檔「修復」。 |
| approval_declined／work_cancelled | NotPublished；若發布決策已取得，改按實際發布及查核結果回報，不假稱撤回。 |
| work_publish_failed | 明確未發布才為 NotPublished；只清理自己該單一暫存。 |
| work_publish_unconfirmed／work_cleanup_failed／work_delete_unconfirmed | 保留已知結果並停用本 session 儲存操作；不重試、覆寫、補刪或清理其他檔案。 |
| 本機終端機／原不可恢復流程故障 | 沿原退出 1；保留可安全回報的已發生結果及用量，完成必要收尾。 |

<a id="compatibility"></a>

## 10. 既有契約的明示增量與後續同步

以下是後續產品開發必須在同一變更同步的來源。本次不回寫舊規格的歷史敘述，不把本份待實作能力提前當成既有功能。

| 來源契約／擁有者 | 本份限定增量與保持界線 |
|---|---|
| 第 01／03 份、BootstrapOptions／ArgumentParser／啟動流程 | 增加 --resume ID 與四個 /work 命令；續接選項以 internal 資料交接，不承載 Key。原工作區確認、隱藏 Key、--help 無 I/O、退出碼與無參數啟動保持。 |
| PLAN-004、050、053～061、070 及第 09 份第 12.2 節 | 增加不可信歷史計畫投影及一次性 Draft 匯入；PlanStore 仍擁有目前任務與證據。跨次未完成定義可重建，舊確認／事實不可重建；20／8／1／256／投影限制不提高。 |
| CTX-005、007～016、028～041、056～064、072～080 | 「僅程序內、無存檔」由本份明示擴充；程序內 /compact 本身不寫檔。增加單一摘要的載入來源、保存摘要用途及相同記帳，原 pins／容量／唯一 client／失敗重設保持。 |
| RULE-011、020、023、026、028～034、044～048、061～062；RULE-HO-007 | 增加啟動續接時的使用者明確 Scope 確認，以及私人目錄用途限定服務；不自動切 Scope、不從 hash 還原規則、不讓歷史滿足目前來源閘門。 |
| GIT-018～020、026～031、065、071、074～075；GIT-HO-007 | 私人目錄加入安全子集及原生拒絕；存檔無 finding／Git 原始來源，載入不建立 Git 觀察、不開放一般 .git，/review 仍重新取得來源與生成。 |
| TERM-046～050、054～059、065、069；TERM-HO-007 | 新確認共用隱藏單行／唯一讀取者；命令與存檔不進草稿歷史，不從存檔復原按鍵、yes 或舊輸入。共同清除涵蓋新增工作資料。 |
| 第 04／06／08 份安全路徑、完整讀檔與寫入；BUILD admission／原生 deny；BUD-S001／004 | .aiagent 與專用暫存名稱納入對應路徑／掃描規則及 .gitignore；不放寬一般工作區外存取。參照查核不能建立 read_file 快照；已有單檔寫入、程序隔離與逐次核准照舊。 |
| README／SPEC／ROADMAP／CLI help | 後續實作才同步產品入口、能力、存檔明文／摘要有損、刪除／clear 差異與實際驗收狀態。本次 ROADMAP 只加入文件入口及交接。 |

| 編號 | 相容性要求 |
|---|---|
| SAVE-068 | 不增加／更名模型工具、不改現有工具 Schema／成功 JSON，不以保存規格放寬一般 Plan／Execute／Review 能力；Plan 九項、Execute 十二項與 Review 三項仍以目前登錄為準。 |
| SAVE-069 | 工作區外私人服務不共用可由工具任意指定 path 的入口。私人目錄的忽略及掃描是多層保護，不能單靠 .gitignore；原 staged／artifacts 固定檢查不減弱，不以新增目錄規則解除第 07 份 BUD-S003 Release 阻擋。 |
| SAVE-070 | 上表來源在後續產品實作同一變更明示接續，並更新原驗收的必要預期；保留「模型文字不能建立授權／事實」「局部／歷史來源不抵修改基準」「單一讀取者／唯一記帳」等原測試目的，不因匯入而刪除安全回歸。 |
| SAVE-071 | 既有 test 不支援、原生未結、真人／真實 Gemini、第 07 份 Release／人工分發門檻分列；新存檔、恢復成功或本份文件查核均不能解除它們。首版無搬移／匯出／加密／同步，新增版本或能力必須另行修訂格式、相容性與驗收，不接受實作者自行補選項。 |

<a id="upstream-handoffs"></a>

## 11. FUT 耦合矩陣與固定交接 memo

本節是 FUT-08 的交接紀錄單一來源；沒有必要另外建立相同內容的 memo。第 09／11／12／13／14 份仍擁有各自的目前任務、Context、規則、Git 與終端機契約；本份只擁有存檔格式、私人存取、保存及續接。後續引用固定錨點，不複製另一套權限或狀態定義。

### 11.1 上游必讀契約逐項承接

| 來源交接 | 本份承接位置／需求 | 後續驗收 |
|---|---|---|
| FUT04-HO-001 | 第 2、8 節；SAVE-007～012、057～060：PlanStore 仍是目前來源，舊任務另標歷史、全新身分。 | SAVE-AC-001、018～020 |
| FUT04-HO-002 | 第 5～8 節；SAVE-034～043、056～061：目標／決策、原文 pins、未完成／受阻資訊與歷史 basis 分開保留。 | SAVE-AC-010～013、018～021 |
| FUT04-HO-003 | 第 2、6、8 節；SAVE-007、011、037～040、055～060：不保存或重建 SDK／快照，舊字串不抵目前 bytes。 | SAVE-AC-011、017～020、026 |
| FUT04-HO-004 | 第 4～8 節；SAVE-025、031～036、040～041、047、056～057：各層 bytes 與投影獨立限制，不能裁必要條件。 | SAVE-AC-007～010、014、020 |
| FUT04-HO-005 | 第 6、9 節；SAVE-041～044、066～067：共用 Runner／預算／唯一用量，拒絕確認也不退款。 | SAVE-AC-012～014、022 |
| FUT04-HO-006 | 第 7～9 節；SAVE-045～051、059～060、066～067：不恢復或重放已做操作，安全目前事實與未確認結果分清。 | SAVE-AC-015～016、019、022～023 |
| FUT04-HO-007 | 第 2、7～9 節；SAVE-012、044～050、057、066～067：候選全有或全無、清除撤銷、不由載入解除 Plan。 | SAVE-AC-015～016、020、022～023 |
| FUT04-HO-008 | 第 1、5、8、10 節；SAVE-005、034～037、059～061、068～071：保持原上限，跨次能力只由本份擴充，無多 Agent。 | SAVE-AC-009～010、019～021、024～025 |
| CTX-080 | 第 5、6、8～9 節；SAVE-034、038～044、058～067：接摘要／pins／來源與清除，不復原核准；明定再次保存與首次壓縮。 | SAVE-AC-010～014、021～022 |
| RULE-HO-007 | 第 3、5、8～10 節；SAVE-018～019、033、045、052～060、065～070：工作區／認證重走、來源重讀、明確 Scope 與歷史來源降權。 | SAVE-AC-017～020、024、029 |
| GIT-HO-007 | 第 4～6、8、10 節；SAVE-030、037～038、055、065、068～070：首版不存 finding／raw blob／快照，Git 查核另作新探索。 | SAVE-AC-011、017、024～026 |
| TERM-HO-007 | 第 3、6、8～10 節；SAVE-015、018～020、037～038、058、065～070：排除草稿／歷史、共同清除與新輸入確認。 | SAVE-AC-002～003、011、022、029～030 |

<a id="fut-09-handoff"></a>

### 11.2 FUT-09 固定邊界

FUT-08 保存目標、決策及待辦；FUT-09 決定修改前後 bytes、可復原基準、變更歸屬、衝突及逐次核准。兩者互相獨立、分開驗收，不要求合併或固定先後。本份 file_refs 的 hash、missing 與目錄身分只是歷史觀察；不能證明修改者、檔案可復原或所有副作用都在範圍內。

FUT-09 規格仍須先讀第 13 份 [FUT09-HO-001～010](13-git-diff-and-code-review.md#fut-09-handoff)與第 12 份 [RULE-HO-008](12-project-rules-loading.md#future-handoffs)，再承接下表 SAVE-HO-008。本份不取代其原始交接，也不預留可直接當作 checkpoint 的欄位。

<a id="future-handoffs"></a>

### 11.3 其他 FUT 的前向交接

| 交接編號／對象 | 本份已定邊界 | 後續責任與驗收 |
|---|---|---|
| SAVE-HO-001／FUT-01 | 私人目錄排除；檔案參照查核不建立快照或搜尋結果（SAVE-011、030、036、046）。 | 擴充索引／搜尋時繼續排除存檔，不將舊 path／hash 當目前內容；維持搜尋遇新規則邊界整次拒絕。 |
| SAVE-HO-002／FUT-02 | 存檔與程序證據獨立，私人根須原生 deny（SAVE-030、037、060、067、071）。 | 新 recipe／輸出／修正循環須重驗私人儲存不可達；不從歷史 basis 恢復 TestsPassed，不解除 test 不支援。 |
| SAVE-HO-003／FUT-03 | 全新 Draft／Pending、歷史狀態另列，無事實匯入（SAVE-035、043、058～061）。 | 任務格式或條件改版時明訂存檔相容性；不復用舊身分、完成事實與核准。 |
| SAVE-HO-004／FUT-04 | 單一有效摘要、原文 pins、保存用途及唯一記帳（SAVE-034、038～044、058、061、066）。 | 後續壓縮策略須保留載入背景及容量標示；測立即再次保存與第一次壓縮，不累積無界摘要鏈、不退款。 |
| SAVE-HO-005／FUT-05 | 保存參照不是規則／Scope 授權；重讀目前來源（SAVE-033、045、053～054、057、065）。 | 多 Scope 或規則來源改版明示版本遷移；不能讓歷史全文／hash 重建 Valid 或復活確認。 |
| SAVE-HO-006／FUT-06 | 不保存 Git 報告／來源／快照，沒有 Git 身分恢復（SAVE-030、037～038、055、068）。 | 日後若新增歷史 finding，另定來源、過時及格式版本，仍須重新取得目前 Git；不把舊報告轉成證據。 |
| SAVE-HO-007／FUT-07 | 草稿／歷史及按鍵不落地，新確認隱藏單行（SAVE-015、018～020、037、066）。 | 新輸入器／進度顯示保留唯一讀取者與預輸入排空，不從文件回放 yes 或命令。 |
| SAVE-HO-008／FUT-09 | 脈絡與復原分開，無 bytes／backup／checkpoint ID（SAVE-005、011、036～037、049～051、059）。 | 復原基準、敏感保存、衝突／部分失敗與逐次核准另定；不因成功載入工作而復原檔案，也不批次清理。 |
| SAVE-HO-009／FUT-10 | 存檔是歷史資料，不啟用技能或執行資源（SAVE-005、037、065）。 | 技能來源／版本／Scope 必須重新核對；本份不預先加入技能狀態或假定共享格式。 |
| SAVE-HO-010／FUT-11／12 | 無外部連線、遠端引用讀取或外部認證（SAVE-004～006、037、065）。 | 外部工具／網頁引用若需持久化，另訂資料最小化、認證、過時及格式版本，文字不得授權網路或工具。 |
| SAVE-HO-011／FUT-13 | provider-independent 摘要無原生協定／Key；當前認證與固定憑證規則（SAVE-007、037、052、062～066）。 | 新供應商需擴充敏感外形與認證隔離／相容性；不能續傳別 client 原生資料或失敗後隱式換模型重送。 |
| SAVE-HO-012／FUT-14 | 單一工作、單一讀取者、沒有父子狀態或跨 Agent 預算（SAVE-005、009、017～019、041～042、065）。 | 委派、共享工作及總預算另定；子 Agent 結果不是人類授權，不可自行保存／載入父工作或匯入完成事實。 |

| 編號 | 交接要求 |
|---|---|
| SAVE-072 | 上游 12 列逐一回填本份章節、需求及驗收，保留原固定錨點；修改共用來源、狀態、記帳或權限須列出受影響契約與遷移，不只寫「沿用」。 |
| SAVE-073 | 本節為交接 memo 唯一來源；相關後續 FUT 須逐項承接適用 SAVE-HO-001～012，並核對當時產品。第 09／11／12／13／14 份原契約仍有各自擁有者，不因本份重述而被取代。 |
| SAVE-074 | 前向交接只約定邊界與後續責任，不能當作能力完成證據、格式共享或合併開發要求；不為未開發 FUT 新增空殼介面、存檔欄位或工具。 |

<a id="acceptance"></a>

## 12. 後續產品驗收

### 12.1 方法與層級

| 編號 | 要求 |
|---|---|
| SAVE-075 | 離線用合成 Key、可控檔案／權限／終端機／時鐘／ID／取消替身、同一 Runner 與真實 SDK 配假 HTTP；全部離線，不連 Gemini。macOS 原生案例實際測 UID／mode／ACL／連結／排他發布及子程序 deny，能力不可用明記拒絕與未驗範圍，不以替身宣稱原生通過。 |
| SAVE-076 | 文件、離線、原生、PTY、真人、真實 Gemini、發布分別記錄實際命令、結果及未驗範圍；PTY 使用現有 TerminalHost 與讀取啟用／同步閘門，不靠 sleep 猜測。真人用合成 Key／隔離檔；真實服務另取明確授權並由使用者隱藏輸入，不存 Key、完整 request、原始終端機錄製或敏感轉錄。 |

### 12.2 離線及 macOS 原生案例

以下皆為待開發驗收；名稱可採 SAVEAC001_PublicBoundariesStayIndependent，拆分案例時仍保留需求對照。

| 案例 | 對應需求 | 接受條件 |
|---|---|---|
| SAVE-AC-001 | SAVE-001～012、068～071 | 正式組裝與服務只在指定入口啟用；公開 LLM／工具不變，保存結果獨立；無 SDK／服務序列化、無新事實，既有未結門檻保持。 |
| SAVE-AC-002 | SAVE-013～017 | --resume 與 workspace 兩種順序、重複／缺值／多參數／大小寫／ID 非法；title 80／81 UTF-16、首尾空白／emoji／控制字元；多行命令整筆拒絕；--help 零內容 I/O／Key／client。 |
| SAVE-AC-003 | SAVE-018～020 | 完整預覽後才啟用新讀取；提示前／busy 期間已緩衝的 yes／貼上及程式持有的模型文字不批准，CRLF 不產生第二決定；提示後來源不可辨識的限制正確標示。Ctrl+C／EOF／顯示故障、單一讀取者及所有退出碼正確。 |
| SAVE-AC-004 | SAVE-021～023、027 | 私人根缺席與首次明確建立；正常 root-owned 的 /、/Users 加目前 UID 家目錄可接受，其他 UID／可寫祖先、非 0700／0600、額外 ACL、symlink／hardlink／FIFO／socket／種類變化拒絕；實際 macOS handle 與前後重驗。 |
| SAVE-AC-005 | SAVE-024～026、028 | 100／101 份、1024／1025 項、壞檔也占額度；並行程序短鎖、ID 碰撞、發布前額度改變、列舉期間替換；零部分列表／覆寫／額外生成或自動清理。 |
| SAVE-AC-006 | SAVE-028～029、050 | 單 ID 刪除新確認、候選替換／link 競爭零刪除；成功缺席才 Verified，取消／unlink／查核失敗不補刪、StoreStopped 不被 clear 解除。 |
| SAVE-AC-007 | SAVE-025、031～032 | 65536／65537 bytes、depth 16／17、非法 UTF-8／BOM／Unicode、重複／額外欄位、非整數拼字、ID 不一致、UTC 無效與 UInt64 溢位；未知版本不改寫／降版。 |
| SAVE-AC-008 | SAVE-033、036～037 | 正規絕對 root 與安全相對 Scope／path 分別驗證；越界／敏感／含引號字面路徑；規則順序／重複／多餘 refs；file_refs 必須精確對應 requirements，hash 不是身分／授權。 |
| SAVE-AC-009 | SAVE-034～035、060 | 摘要 16384／16385、pins 16／17 與 8192／8193、計畫 20／21、requirements 8／9；range／v1／v2／kind／basis／block reason 不合法拒絕，不裁任務或提高舊上限。 |
| SAVE-AC-010 | SAVE-034、040～041、047、056～057 | 文件、顯示、記憶及 Plan 的 UTF-8 跳脫膨脹各測上限與超一；最大有效匯入保留後續收尾餘裕，超限零部分狀態／確認／傳輸，不把 bytes 當 token。 |
| SAVE-AC-011 | SAVE-006～008、011、037～040、043、065 | 實際保存來源涵蓋最新兩回合，排除失敗鏈／草稿／輸入歷史／工具原文／Git 識別；真實 SDK 所有權合法，零工具／快照／事實；文中假 yes／命令不生權限。 |
| SAVE-AC-012 | SAVE-038～043、058、061 | 普通 save 正好一次同 Runner 摘要；只有既有／載入摘要、pins、plan 或全空的零生成分支；摘要有損與原文 pins 分清，沒有修改目前 Context／Plan。 |
| SAVE-AC-013 | SAVE-040～044、062～064 | 模型拒答／空白／截斷／無效 JSON／工具呼叫／敏感／認證故障；普通候選失敗保留安全工作，嚴重錯誤走原重設，沒有修復 JSON／第二請求／重試。 |
| SAVE-AC-014 | SAVE-040～045、051 | 來源 131072／131073、196608 估算與 262144 實際 body；程序額度零／最後一次、取消／未傳輸／拒絕確認、API 缺值／溢位；唯一記帳、不退款、無額外傳輸。 |
| SAVE-AC-015 | SAVE-009、012、045～048 | generation／preview／yes 前後受控改變 Context／pins／Plan／rules／Scope／根／檔案；候選拒絕而非套新版，取消前零暫存，租約不自鎖、不持核心鎖等待 I/O。 |
| SAVE-AC-016 | SAVE-024、048～051、067 | 真實排他發布及可控 flush／rename／讀回／取消競爭；NotPublished／PublishedVerified／PublishedUnconfirmed 精確，僅清自有單檔暫存，故障不重寫、不清旁檔、不誤報未保存。 |
| SAVE-AC-017 | SAVE-011、030、036～037、046、052～055 | 工作區先確認／隱藏新 Key 後才讀存檔；原根／Device／Identity 不符、搬移／同路徑重建拒絕；檔案變動／缺席／過大／目錄／範圍外正確分類，零快照／事實。 |
| SAVE-AC-018 | SAVE-033、052～058 | Scope 不由存檔自動切換；明確選擇後才讀深層規則。規則同 hash 不恢復身分，變動明示；Stale／敏感／缺失 Scope 拒絕；完整續接預覽與確認後來源重驗。 |
| SAVE-AC-019 | SAVE-035、037、043、059～060 | Pending／Blocked／InProgress 歷史定義全部新 ID／Draft／Pending，清舊 summary／basis／證據；Completed 僅歷史，不自動重做。全完成／無計畫仍 Plan，/execute 獨立確認後等新訊息，test 不支援保持。 |
| SAVE-AC-020 | SAVE-012、034、054、056～060 | 原子匯入取消／容量／原生讀取／rule／plan 故障零部分 pins／計畫；不匯入成功回合、認證、舊批准／快照／completed 事實。候選之後任何來源變動不補回資料。 |
| SAVE-AC-021 | SAVE-039、058、061 | 載入後立即再保存不丟摘要或錯報空工作；新回合後保存涵蓋雙方背景；第一次／第二次 compaction 不丟來源、重複占容量或灌入舊回合數。最新計畫取代歷史計畫，無無界合併。 |
| SAVE-AC-022 | SAVE-012、019、041～044、050～051、066～067 | clear／安全重設／新 Key／退出撤候選與載入背景、清 CLI 歷史，但保留磁碟存檔與累計；普通儲存拒絕保留目前工作，StoreStopped 黏著至程序結束、不可清除後續寫；不自動補送。 |
| SAVE-AC-023 | SAVE-020、049～050、067 | 取消／EOF／發布／釋放與既有程序致命故障競爭；必要查核／收尾完成後才釋放政策，已知結果保留，退出 1 不被取消降級。 |
| SAVE-AC-024 | SAVE-030、065、068～074 | 工作區選家目錄／祖先／私人根時，read/search/rules/Git／原生 .NET 都不能讀寫私人內容；掃描器拒絕路徑／暫存，.gitignore 不當唯一防線；交接不開其他 FUT。 |
| SAVE-AC-025 | SAVE-068～076 | 適用 01～14／Python 回歸及格式檢查，明示增量才改預期；不降低核准、快照、來源、程序／API 限制，README／SPEC 接續與分層證據正確。 |
| SAVE-AC-026 | SAVE-006、027、037、062～065 | 不同當前 Key／舊 Key 外形、JSON Unicode 跳脫、ASCII／UTF-16 bytes、私鑰外形／認證賦值／短 Key 碰撞整份拒絕；不輸出原文或以「fake」豁免。竄改 hash／state／yes 永不成授權。 |
| SAVE-AC-027 | SAVE-013、017、026～027、047、056、064 | list 排序／show 歷史標示／完整預覽的安全與容量；單一損壞或不安全項使列表整次拒絕，查詢不生成／切模式／改 Context／核對目前檔案，不自動修復權限或檔案。 |
| SAVE-AC-028 | SAVE-004、009、021～030、048～050、075 | macOS 真實 UID／mode／ACL、無跟隨開啟、link 競爭、跨程序額度鎖及不可覆寫發布；真實子程序私人根 deny。其他 OS／架構與外層 sandbox 限制如實分列。 |

### 12.3 PTY、真人與真實 Gemini

| 案例 | 對應需求 | 接受條件 |
|---|---|---|
| SAVE-AC-029 | SAVE-013～020、041～042、045、052～060、066、075～076 | 離線 macOS PTY：save／list／show／單檔 delete、--resume 啟動順序、隱藏 Key、Scope／存檔／續接新確認、預輸入 yes／分段 CRLF／多行拒絕、busy／Ctrl+C／EOF，依明確啟用閘門判讀，無敏感轉錄。 |
| SAVE-AC-030 | SAVE-018～020、027、043、047、052～061、076 | 真人 macOS 用合成 Key 在隔離工作區操作，確認可理解明文保存、歷史／目前狀態、差異／未核對範圍、clear／delete 差異及取消；不以 PTY 代替真人。 |
| SAVE-AC-031 | SAVE-038～044、051～061、066、071、076 | 另經授權由使用者隱藏輸入真 Key：一次保存摘要與用量、重啟續接後檔案／規則變動、新計畫確認及新指示；核對沒有自動工具重播與原生歷史續傳。只記安全結果，不連帶宣稱發布門檻完成。 |

<a id="document-acceptance"></a>

## 13. 本次文件查核與完成條件

| 編號 | 文件要求 |
|---|---|
| SAVE-077 | 需求 SAVE-001～080、案例 SAVE-AC-001～032、SAVE-HO-001～012 唯一連續，需求皆有案例／固定交接可追溯。第 5 節兩個 JSON 區塊可解析，Schema 及範例結構、引用、語意與限制一致；不留下本份需實作者選方案的 TBD。 |
| SAVE-078 | 以不落地的只讀腳本檢查 JSON／Schema 參照／範例、Markdown 圍欄與表格、相對連結／固定錨點、需求／案例範圍及敏感外形；完整檢查未追蹤新檔，搭配 git diff --check 及新檔 no-index 空白查核，不用空的 tracked diff 冒充新檔已檢查。 |
| SAVE-079 | 檢查最終狀態只有本檔新增及 ROADMAP 最小更新；Git index 原始指紋及 staged entries 保持，不更動其他未提交工作。文件審閱如發現實質矛盾先修文件，再重跑受影響查核；沒有產品程式可由本次文件查核宣稱通過。 |
| SAVE-080 | 本次完成回報限文件與實際檢查；離線／原生／PTY／真人／服務案例均標後續待執行。後續開發先讀本份與第 11 節上游來源，核對當時產品並同步第 10 節接續契約，再實作本階段，不提前展開 FUT-09 或其他 FUT。 |

| 案例 | 對應需求 | 接受條件 |
|---|---|---|
| SAVE-AC-032 | SAVE-001～080 | JSON／Schema／範例、編號與需求覆蓋、Markdown／連結／錨點／敏感外形／空白查核通過；保存來源、再次保存、首次壓縮、任務身分、Scope、權限、發布／刪除／取消、上游交接一致。只兩份文件改動、索引不變，零產品實作／產品驗收／提交／推送。 |

可使用下列只讀命令；no-index 有差異的非零碼須與真正空白診斷分開判斷：

~~~sh
git diff --check
git diff --check --no-index /dev/null specs/15-work-save-and-resume.md
git diff --stat
git status --short
~~~

## 14. 參考與後續閱讀

- [ROADMAP](../ROADMAP.md)：FUT-08 定位、FUT-03／04 協調與 FUT-09 獨立關係。
- [README](../README.md#latest-validation)：目前產品與實際證據；歷史通過數不當作本次查核。
- [09 的 FUT-04 固定交接](09-plan-mode-and-task-list.md#fut-04-handoff)、[11 的 Context／預算](11-context-and-budget-management.md)：任務來源、摘要、原文 pins、額度與清除。
- [12 的固定交接](12-project-rules-loading.md#future-handoffs)、[13 的固定交接](13-git-diff-and-code-review.md#future-handoffs)、[14 的固定交接](14-terminal-interaction.md#future-handoffs)：來源重驗、Git 歷史與終端機生命週期。
- [本份上游對照](#upstream-handoffs)、[FUT-09 邊界](#fut-09-handoff)、[前向交接](#future-handoffs)：後續規格引用的單一 memo。
