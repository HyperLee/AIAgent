# 09：Plan 模式與任務清單開發規格書

> 文件版本：0.2｜建立日期：2026-10-23｜修訂日期：2026-10-23｜狀態：待實作規格
>
> 選題依據：[未來開發大綱 FUT-03](../ROADMAP.md)。上位文件：[開發總規格](../SPEC.md)第 5、8～15 章；承接 [03：CLI 與 Context](03-cli-and-context.md)、[04：工作區與唯讀工具](04-workspace-and-read-tools.md)、[05：Agent 工具循環](05-agent-loop.md)、[06：檔案編輯與核准](06-file-edit-and-approval.md)、[07：預算與整體驗收](07-budget-and-acceptance.md)及 [08：程式碼搜尋與局部讀檔](08-code-search-and-partial-read.md)。目前能力以 [README](../README.md)與原始碼為準，前置規格的「待實作」是其撰寫當時狀態。本份文件完成不表示 FUT-03 已實作或產品驗收通過。

## 1. 目的、範圍與交付界線

使用者面對跨檔修改或多步工作時，可以先進入只允許探索與規劃的 Plan 模式，檢視可確認的任務清單，再明確切換 Execute。程式追蹤實際進度、未完成原因及完成依據；確認計畫與核准單次操作分開處理。

0.1 初稿於 2026-10-23 僅新增本檔；0.2 本次僅修訂此檔，補充 FUT-04 交接與相關導讀／文件驗收。兩次文件交付均不修改程式、測試、設定、專案、套件、README、ROADMAP、總規格或其他子規格，不建立額外計畫、空實作、工作樹、hook 或執行紀錄，不變更 Git 索引／歷史，不提交或推送。下述宣告、工具、輸出及產品命令全部是後續開發要求。

需求使用 `PLAN-xxx`，驗收使用 `PLAN-AC-xxx`。`FUT-03` 是 ROADMAP 項目代號，`09` 是本份子規格編號。**FUT-02「受控建置與測試」不是既有第 02 份 Gemini 用戶端規格。** 下次撰寫 FUT-02 或 FUT-04 時，須分別回讀本檔的 [FUT-02 交接專節](#fut-02-handoff)或 [FUT-04 交接專節](#fut-04-handoff)，不另行定義相互矛盾的任務、核准、摘要與預算契約。

| 編號 | 具體要求 |
|---|---|
| PLAN-001 | 首版提供 Plan／Execute、單一活動計畫、版本確認、四種任務狀態與可追溯的完成依據；Plan 可先配合現有檔案工具獨立開發，不依賴 FUT-02。 |
| PLAN-002 | 本次只交付本規格書；文件檢查、後續功能實作、離線驗收、PTY、真人／真實服務及發布門檻分別記錄。 |
| PLAN-003 | 沿用 .NET 10 Console、Google.GenAI 1.26.1、MSTest／Microsoft.Testing.Platform、nullable、warnings-as-errors 及既有格式；先驗收 macOS，不新增套件、產品參數、設定欄位或認證來源。 |
| PLAN-004 | 不實作建置／測試程序、任意 shell、套件安裝、部署、Git、背景排程、多 Agent、專案規則載入、跨次保存、摘要、復原或其他 FUT 功能；單次檔案寫入仍依第 06 份核准。 |
| PLAN-005 | 計畫及證據僅存程序記憶體，不自動輸出計畫檔、日誌或存檔；取消、切模式、修訂及清除均不復原已寫檔案。 |

## 2. 現況、模組責任與相容性

0.1 初稿核對 HEAD `546020f` 的 ROADMAP、README、既有子規格、ChatApplication、AgentRunner、ToolRegistry、ConversationContext、核准橋接器及工作區寫入入口；未重新執行產品驗收。當時已有七項檔案工具、五個完整成功回合、每回合五次生成、用量累計、完整讀檔快照與逐次寫入核准，尚無 Plan 模式或跨回合任務儲存。LastTurnOutcome 只保留最近一次回合，不能直接當作任務證據帳本。0.2 補充在 HEAD `b098c77` 核對 ROADMAP 與本規格的 FUT-03／04 交接；未重新執行產品驗收，不以文件修訂更新產品完成狀態。

| 接續位置 | 後續責任 |
|---|---|
| ChatApplication／CLI 互動與顯示 | 持有模式、計畫服務及清除世代；本機指令、版本確認、單一讀取者、取消、換 Key 與安全任務顯示。 |
| 後續 PlanStore／AgentExecutionPolicy | 管理計畫與任務身分、版本、確認、狀態、有限證據、原子更新及模式／任務權限；不直接生成、存檔或執行程序。 |
| AgentRunner／ToolRegistry／工作區 | 建立當次狀態投影、提供模式允許的工具、整批預檢、逐次派送、任務關聯、證據保留及實際寫入入口防線。 |
| ConversationContext／Gemini／UsageAccumulator | 保留原生協定、完整成功回合、實際請求限制及唯一用量記帳入口；不由任務服務重播、摘要或補請求。 |

| 編號 | 擴充及相容性要求 |
|---|---|
| PLAN-006 | 預設 Execute；沒有活動計畫時，既有聊天、讀寫、逐筆核准及一般工具錯誤處理保持原行為，不強制每則訊息先建立計畫。 |
| PLAN-007 | 只擴充先前排除的 Plan／任務能力與 CLI 本機指令集合；既有七工具的參數 Schema、結果 JSON、工作區邊界、完整讀檔快照及發布查核不變。新增任務資料使用獨立安全投影。 |
| PLAN-008 | 後續實作時同步更新最小必要能力說明、/help、README、ROADMAP 與總規格適用範圍／索引；本次不提前修改這些檔案，也不附完整應用指示。 |
| PLAN-009 | 第 07 份 Release 掃描阻擋、真人終端機、真實 Gemini 及人工分發等未結責任繼續保留；本份不將歷史驗證紀錄當作當次證據。 |

## 3. 內部契約、狀態與資料所有權

以下形狀供後續實作，全部為 internal；公開 ILlmClient、LlmRequest、LlmResult、LlmMessage 及既有方法簽章不變。含文字／路徑／集合的型別採密封類別、唯讀屬性、防禦性複製及固定型別標記 ToString，不用會傾印內容的 record，不提供一般序列化出口。

```csharp
internal enum AgentMode { Execute, Plan }
internal enum PlanApprovalState { Draft, Approved }
internal enum PlanTaskKind { Analysis, Read, Write, Validation }
internal enum PlanTaskState { Pending, InProgress, Completed, Blocked }
internal enum TaskCompletionBasis { None, LocalOperation, ModelAssessment }
internal enum PlanApprovalDecision { Approved, Declined, Cancelled, EndOfInput, Unavailable }

// 新增到既有 IChatInteraction；不是既有檔案確認方法的替代。
ValueTask<PlanApprovalDecision> ConfirmPlanAsync(
    PlanApprovalPreview preview, CancellationToken cancellationToken);
```

| 型別／資料 | 必要欄位或語意 |
|---|---|
| PlanSnapshot | PlanId、Revision、Objective、ApprovalState、依序 Tasks；單一活動版本。 |
| PlanTaskSnapshot | TaskId、Kind、Title、Acceptance、Requirements、State、Summary、BlockReason、CompletionBasis、CompletionFactIds。 |
| TaskRequirement | ToolName、正規化工作區相對 Path；read_file_range 另含 StartLine／EndLine，其餘工具不得帶行範圍。 |
| TaskExecutionContext | 不可變 PlanId、PlanRevision、TaskId；由本機在操作開始時擷取，供 FUT-02 重用。 |
| TaskOperationFact | OperationId、Context、Kind、ToolName、Status、ErrorCode、受控相對路徑、必要數值摘要、Truncated、WriteState、CleanupFailed；範圍讀取另記已驗證的請求起訖及實際範圍摘要。Kind 僅 LocalOperation／ModelAssessment；分析事實可有受控 Summary。 |
| PlanApprovalPreview | PlanId、Revision、清除世代及完整安全 DisplayBlock；不能持有可變 PlanStore 或可執行操作。 |
| PlanStore | 建立／修訂、取得不可變快照、確認版本、更新任務、預留／提交證據、封鎖在途任務及清除；操作為同步原子決策，不自行呼叫外部資源。 |
| AgentExecutionPolicy | 提供目前模式及同一 PlanStore 的權限檢查；Registry、Runner、工作區寫入入口共用同一實例，未知能力預設禁止。 |

資料投影與顯示使用明確 writer，不將整個型別或 SDK 紀錄序列化。TaskOperationFact 不保存參數 JSON、檔案原文、diff、query、pattern、old_text、new_text、content、CallId、簽章、Key、原始例外或程序輸出。

| 編號 | 契約要求 |
|---|---|
| PLAN-010 | AgentMode 與 ChatState 為獨立狀態軸；新增 AwaitingPlanApproval，不以 Plan 取代 Generating／ExecutingTools／Cancelling 等互動狀態。模型不得寫入這兩個狀態軸。 |
| PLAN-011 | ChatApplication 擁有 PlanStore 與政策的生命週期；Runner／Registry／工作區借用同一實例。既有 client／工作區／session 的釋放順序與唯一終端機讀取者不變。 |
| PLAN-012 | PlanId、TaskId、OperationId 分別由本機遞增產生 p1、t1、op1 等識別，採 Ordinal 比對；同一 ChatApplication 不因 clear 或換 Key 重用識別，計數溢位安全停止，不回繞。模型只能引用已存在的身分。 |
| PLAN-013 | PlanRevision 從 1 開始，使用正 long；所有變更核對 expected_revision、目前身分及清除世代。舊確認、遲到更新、證據預留與事件在失效後不能發布。 |
| PLAN-014 | 新增結構化任務摘要事件；既有 LocalToolOutcome／工具結果可在本機伴隨關聯，但不得改寫七工具結果 JSON 或將計畫確認偽造成檔案 Verified。 |
| PLAN-015 | 計畫確認、任務狀態、實際操作、回合完成及程序退出各自判定；AgentTurnStatus.Completed 只表示模型回合合法結束，不自動將任務或整個計畫標成完成。 |

## 4. CLI 指令、版本確認與呈現

| 指令／情境 | 固定行為 |
|---|---|
| `/plan` | 切為 Plan；保留目前計畫及其版本確認，將原 InProgress 任務改為 Blocked／mode_changed。重複切換不新增版本、生成或讀檔。 |
| `/execute`，無計畫 | 切為 Execute，回到輸入；不生成、不建立空計畫。 |
| `/execute`，Draft | 顯示完整最新計畫及版本，進入 AwaitingPlanApproval；確認該版本後改 Approved 並切 Execute，等待下一則一般訊息。 |
| `/execute`，Approved | 同版本確認仍有效時直接切 Execute 並顯示安全摘要，等待下一則訊息；不再次核准計畫、不自動恢復受阻任務。 |
| `/tasks` | 顯示模式、PlanId／版本／確認狀態、目標、全部任務、必要操作、狀態、完成依據、未完成原因，以及保留的舊版本事實數；無計畫時顯示固定提示。 |
| `/clear` | 沿既有共同清除流程移除對話、快照、計畫、確認、任務、證據與最近結果，保留 Key／工作區／模型／程序用量及目前模式。 |

上述三項新增指令不帶參數，與既有指令一樣對 Trim 後整行採區分大小寫的 Ordinal 完整比對。`/plan extra`、`/PLAN`、`/execute now` 仍為未知指令；自然語言「切到執行」只作模型輸入，不能改變本機模式。

計畫確認顯示完整 Objective、版本、全部任務／必要操作／狀態與完成條件，結尾固定說明「確認計畫只確認方向；寫入與程序操作仍須逐次核准」，提示為 `確認此版本並切換 Execute？[y/N] `。不附原始證據全文、應用指示或檔案內容。

| 編號 | 互動要求 |
|---|---|
| PLAN-016 | 新指令只在 AwaitingInput 接受；整個本機操作零生成、零工具執行、零新增用量。保留 --help 不讀 Key、不建立 client 及原有斜線指令規則。 |
| PLAN-017 | 只有確認 DisplayBlock 完整安全輸出、丟棄舊緩衝並啟用該次讀取後取得的 y／yes 可同意，大小寫及首尾空白可接受；n／no／空行為拒絕，其他輸入重問同一版本。不得解析模型文字、計畫內容或預先貼上的 yes 為核准。 |
| PLAN-018 | 確認綁定 PlanId／Revision／世代，返回後再核對；不符時保留 Plan 及 Draft，不自動換成新版批准。拒絕不刪計畫、不生成替代方案，仍為 Plan；整段確認不授權任何寫入。 |
| PLAN-019 | 確認期間使用獨立取消來源及單一讀取者；Ctrl+C 取消確認並回到 Plan 輸入，不退出 130、不取消已結束回合；Ctrl+D／EOF 正常退出 0，互動不可用退出 1。普通 /clear／/exit 字串只是無效確認選擇。 |
| PLAN-020 | 每次一般輸入提示前顯示安全模式標籤 `[Plan]`／`[Execute]`；/tasks 由本機快照產生，不請模型重述。區分「完成：本機操作已核對」「完成：模型分析判斷」「受阻：驗證能力尚未提供」。 |
| PLAN-021 | /help 說明新指令、版本確認不直接執行、Plan 唯讀、逐次核准、證據限制及清除不復原。原聊天、Key、檔案核准與 busy 輸入行為分別回歸。 |
| PLAN-022 | 切模式不清除合法成功歷史，也不把歷史內容當作當前權限；即使保留舊寫入工具訊息，下一請求及實際入口仍依目前模式檢查。 |
| PLAN-023 | 完整核准預覽或 /tasks 區塊無法安全形成時整段拒絕，不省略任務後核准、不以截斷版本接受同意；計畫保持未確認。一般顯示仍由既有安全純文字、路徑跳脫與完整區塊處理。 |

## 5. 結構化工具 Schema 與結果

### 5.1 submit_plan

僅 Plan 可用。首次 expected_revision 為 0；已有計畫則必須等於目前 Revision。task_id 省略代表新任務；提供時只能引用目前計畫中的任務。任務狀態、模式、確認、證據及本機識別不得由此工具指定。

```json
{
  "type": "object",
  "properties": {
    "expected_revision": { "type": "integer", "minimum": 0 },
    "objective": { "type": "string", "minLength": 1, "maxLength": 2048 },
    "tasks": {
      "type": "array", "minItems": 1, "maxItems": 20,
      "items": {
        "type": "object",
        "properties": {
          "task_id": { "type": "string", "minLength": 1, "maxLength": 32 },
          "kind": { "type": "string", "enum": ["analysis", "read", "write", "validation"] },
          "title": { "type": "string", "minLength": 1, "maxLength": 256 },
          "acceptance": { "type": "string", "minLength": 1, "maxLength": 1024 },
          "requirements": {
            "type": "array", "maxItems": 8,
            "items": {
              "type": "object",
              "properties": {
                "tool": { "type": "string", "enum": ["list_files", "read_file", "create_file", "replace_text", "search_files", "search_text", "read_file_range"] },
                "path": { "type": "string", "minLength": 1, "maxLength": 4096 },
                "start_line": { "type": "integer", "minimum": 1 },
                "end_line": { "type": "integer", "minimum": 1 }
              },
              "required": ["tool", "path"],
              "additionalProperties": false
            }
          }
        },
        "required": ["kind", "title", "acceptance", "requirements"],
        "additionalProperties": false
      }
    }
  },
  "required": ["expected_revision", "objective", "tasks"],
  "additionalProperties": false
}
```

Schema 以外仍由本機執行第 6 節跨欄位規則、UTF-16／byte 限制及嚴格整數解析。這份 Schema 不表示產品已支援建置測試或任意工具名稱。

### 5.2 update_task

僅 Execute 可用，更新目前計畫的一項任務；不修改定義、模式或確認。summary 是進度／完成判斷文字，不是外部工具結果。完成所需證據由本機選取，模型沒有 evidence、exit_code、approved 等輸入欄位。

```json
{
  "type": "object",
  "properties": {
    "plan_id": { "type": "string", "minLength": 1, "maxLength": 32 },
    "expected_revision": { "type": "integer", "minimum": 1 },
    "task_id": { "type": "string", "minLength": 1, "maxLength": 32 },
    "status": { "type": "string", "enum": ["in_progress", "completed", "blocked"] },
    "summary": { "type": "string", "minLength": 1, "maxLength": 2048 }
  },
  "required": ["plan_id", "expected_revision", "task_id", "status", "summary"],
  "additionalProperties": false
}
```

### 5.3 固定結果投影

以下是情境範例，不是本次實際工具輸出。成功欄位順序為 status、error_code、data；submit_plan 的 data 為 plan_id、revision、approval、tasks，任務項目只含 task_id、state，順序對應提交陣列。

```json
{"status":"success","error_code":"none","data":{"plan_id":"p1","revision":1,"approval":"draft","tasks":[{"task_id":"t1","state":"pending"}]}}
```

update_task 的 data 順序固定為 plan_id、revision、task_id、state、completion_basis、completion_fact_ids、block_reason。完成依據使用 none／local_operation／model_assessment；無受阻原因時為 null，非完成狀態的完成識別集合為空。

```json
{"status":"success","error_code":"none","data":{"plan_id":"p1","revision":1,"task_id":"t1","state":"completed","completion_basis":"local_operation","completion_fact_ids":["op1"],"block_reason":null}}
```

```json
{"status":"rejected","error_code":"plan_revision_conflict"}
```

| 編號 | 工具要求 |
|---|---|
| PLAN-024 | 只接受原生工具呼叫；Name、CallId、所有 JSON 欄名／字串值先沿既有整批敏感預檢。工具再次驗證所有層級為正確型別、Ordinal 欄名、無額外／重複欄位，不從一般回答抽取 JSON 執行。 |
| PLAN-025 | 整數使用原始 JSON 十進位整數形式，不接受小數、指數、字串、負零或超過 long；範圍行號另限 int。字串與跨欄位檢查由內部入口重做，不能只依賴 Schema。wire 重複欄位仍沿既有 SDK 邊界拒絕。 |
| PLAN-026 | 所有文字先檢查原始長度與敏感資訊，再將 objective／title／acceptance／summary 的首尾空白 Trim 後保存，不能為了通過上限先裁切。非空欄位 Trim 後不得為空；不做 Unicode 正規化或語意等價判斷。 |
| PLAN-027 | submit_plan 完整驗證並形成候選快照、模型投影與核准預覽後才原子發布；失敗零部分更新、零工作區 I/O，不預先建立身分／狀態後再嘗試修補。 |
| PLAN-028 | update_task 核對計畫已確認及版本，僅依第 7 節轉移；錯誤不套用模型要求的部分狀態。新工具不觸發檔案核准、不啟動程序、不自行生成。 |
| PLAN-029 | 明確 writer 逐欄及完整區塊檢查結果；失敗只含 status／error_code。無法安全回填則 Response=null，停止並共同重設；已發生的本機寫入事實仍依第 06 份先安全回報。 |

## 6. 任務定義、版本與修訂

| 任務種類 | requirements 與完成語意 |
|---|---|
| Analysis | 空陣列；完成只表示模型已提供可檢視的分析摘要，標示 ModelAssessment，不表示功能、檔案或測試已核對。 |
| Read | 1～8 項，工具限五項唯讀工具。全部必要操作成功且未截斷才具備完成所需本機事實。搜尋只證明按該工具契約取得資料，不證明已找到全部語意相關程式。 |
| Write | 1～8 項，工具限 create_file／replace_text。每個指定目標都有相符的 Verified 寫入事實才可完成；程式品質、功能行為及測試另拆 Analysis／Validation。 |
| Validation | 空陣列；表示待驗證工作。FUT-02 尚未提供程序能力時不能執行或完成，選為進行中時改為 Blocked／capability_unavailable。 |

requirements 的 path 沿既有路徑及敏感政策做純文字正規化，不讀檔或檢查存在性；根目錄使用 `.`，比較採既有 Ordinal 索引，不猜測大小寫／Unicode 別名。read_file_range 必須同時提供正整數 start_line／end_line，起點不大於終點且跨度最多 200；其他工具不得帶行號。相同工具／正規化路徑／範圍的重複 requirement 拒絕。搜尋 query、pattern、替換文字及檔案內容不進計畫契約。

| 編號 | 版本及定義要求 |
|---|---|
| PLAN-030 | 首次建立 PlanId／Revision=1，全部任務 Pending；沒有任務的計畫不合法。Validation 可以列入待辦，但不得因列入計畫就當作能力已存在。 |
| PLAN-031 | 同 Objective 的任何有效結構修訂增加 Revision，ApprovalState 改 Draft；純任務排序也須重新確認。完全相同提交為 Rejected／no_change，不消耗版本、不失效仍有效的確認。 |
| PLAN-032 | 只在 task_id、Kind、Trim 後 Title／Acceptance 及 requirements 全部精確相同時保留 TaskId、狀態與證據；requirements 比較採工具／path／行範圍排序後的集合。純任務排序、requirement 排序及首尾空白整理可保留身分，其他改字保守視為新任務，不請模型判斷語意相同。 |
| PLAN-033 | task_id 有效但定義改變時，由本機配發新 TaskId 並從 Pending 開始；不存在、已退役或重複引用的 task_id 直接拒絕。省略 task_id 一律新任務，不能靠相同標題繼承證據。 |
| PLAN-034 | Objective 改變視為新計畫，配發新 PlanId／Revision=1，所有任務重新配身分；該次不得引用舊 task_id。先前操作事實仍保留原計畫／版本／任務歸屬，不重新綁定、不撤銷已寫 bytes。 |
| PLAN-035 | 同時只有一份活動計畫；移除任務使其退役，證據仍在有界帳本中，不可復活身分或轉作其他任務證據。修訂失效的是執行方向確認，單次檔案核准本來就不可重用；讀取快照仍沿原有世代／重驗規則處理，不因批准計畫刷新。 |

## 7. 任務轉移與本機完成證據

| 原狀態 | 允許的請求／事件 | 結果 |
|---|---|---|
| Pending／Blocked | in_progress，Analysis／Read／Write，且沒有其他 InProgress | InProgress，清除舊受阻原因及完成依據；既有事實仍保留。 |
| Pending／Blocked | in_progress，Validation | Blocked／capability_unavailable；以成功狀態更新回傳，不假裝啟動了程序。 |
| InProgress | completed，符合本節證據條件 | Completed，附完成依據與本機選取的 fact IDs。 |
| InProgress | blocked | Blocked／model_reported，保存受控 summary。 |
| InProgress | 非成功工具、回合失敗／停止／取消或切 Plan | Blocked，記錄程式確定的原因及既有操作事實。 |
| InProgress | 模型合法成功回合結束，但未送出合格 completed／blocked | Blocked／turn_paused，保留 summary 與事實；顯示等待下一則指示。該成功回合仍按原規則提交歷史。 |
| 其他轉移，包含 Pending→Completed、Completed→InProgress | 任意請求 | Rejected／invalid_task_transition，原狀態不變；重新做已完成工作須建立新任務。 |

model_reported 只表示模型回報遇阻，不把 summary 當成本機錯誤證據。completed／blocked 只接受目前 InProgress 任務；同一任務重複 in_progress、重複 completed 等請求也拒絕，不新增事實。任何狀態更新都不改計畫結構版本。

| 編號 | 證據及轉移要求 |
|---|---|
| PLAN-036 | 同時最多一個 InProgress；啟動任務須為目前已確認版本。沒有活動計畫的 update_task 為 plan_required，不建立隱含清單。 |
| PLAN-037 | 有活動計畫時，每次檔案寫入必須對應一個 InProgress 的 Write 任務，且工具與正規化 path 必須列於 requirements；讀取可探索其他路徑，但只將相符結果當成 requirement 證據。沒有計畫時仍沿原本逐次寫入核准。 |
| PLAN-038 | 操作開始前本機擷取 TaskExecutionContext 及預留證據，完成後由實際工具結果產生唯一 OperationId。只取通過政策的結果欄位；未執行、被拒絕、Cancelled、Failed、NotApplied、Unconfirmed 或 CleanupFailed 均不能當作成功證據。 |
| PLAN-039 | Read／Write 完成需每個 requirement 各有相符且不同的本機成功事實，全部歸屬同一有效 TaskId；Write 另須 Verified 且無清理失敗。read_file_range 比對已驗證的請求行範圍，成功空檔／EOF 夾限依第 08 份記錄實際結果；不將成功片段描述成完整讀檔。Truncated=true 的結果不符合完成條件。 |
| PLAN-040 | update_task completed 不接受模型提供的操作紀錄；本機對每個 requirement 選取該任務最近一次相符終結事實，必須為合格成功。後來相符失敗不能被較早成功掩蓋；缺任一項則 evidence_required，任務保持 InProgress。 |
| PLAN-041 | Analysis completed 將該次安全 summary 作為 ModelAssessment 事實並顯示，不能標為本機驗證。Validation completed 在本階段一律 capability_unavailable，不因模型聲稱命令成功、文字像測試報告或計畫已確認而完成。 |
| PLAN-042 | 任務已接受的進度及操作事實獨立於回合歷史提交；最終模型生成失敗不抹除先前已接受的完成／Verified。只封鎖仍為 InProgress 的任務；取消不將 Completed 改成未套用。強制清除另依第 10 節先回報再清除。 |
| PLAN-043 | 證據只證明該次操作及當時結果，不保證目前磁碟未再變更或整體功能正確。修訂後只有定義未變且保留 TaskId 的任務可沿用舊版本事實，原 Context 不改寫；其他任務不能重綁、複製或拼湊證據完成。 |

例如 Write 任務要求修改 A 與 B：A Verified、B 拒絕時，任務為受阻，顯示 A 已核對、B 未套用；不能稱「整項完成」或「全部未變」。下一個使用者回合明確要求繼續後，可將同一受阻任務改 InProgress，依原快照／預覽規則處理 B；不自動重寫 A。即使兩檔皆 Verified，也只完成修改步驟；獨立的建置／測試任務仍未驗證。

## 8. 工具能力、整批預檢與實際入口

正式 Registry 內建九項工具，七項檔案工具保持原順序，再接 submit_plan、update_task。對模型提供的 Definitions 依當次模式過濾：

| 模式 | 當次 Definitions 順序 |
|---|---|
| Plan | list_files、read_file、search_files、search_text、read_file_range、submit_plan。 |
| Execute | list_files、read_file、create_file、replace_text、search_files、search_text、read_file_range、update_task。 |

所有工具以明確能力登錄，不由名稱前綴、描述、模型或檔案文字推斷權限。未來新增工具必須另行指定能力及模式政策；未指定時不對模型提供，也不允許派送。

| 編號 | 執行限制 |
|---|---|
| PLAN-044 | 工具定義過濾之外，Runner 整批預檢已登錄工具的模式權限，Registry 派送及實際寫入入口再次檢查共用政策；Plan 呼叫寫入在目標 metadata／內容 I/O、預覽、核准、暫存及發布前拒絕。直接繞過 Registry 呼叫工作區亦同。 |
| PLAN-045 | 先沿既有協定及整批敏感檢查，再檢查取消與第五次生成上限，最後進行模式預檢。有模式禁止工具時整批零工具執行；禁止呼叫各記 Rejected／mode_restricted，其餘各記 NotExecuted／turn_stopped，停止本回合，不補生成。未知工具仍為既有 unknown_tool，不假造已登錄能力。 |
| PLAN-046 | 同批各呼叫仍依 PartIndex／CallId 配對並順序處理；任務條件及證據容量在每次開始前檢查，不能只檢查批次最初狀態。update_task in_progress→讀寫→completed 可在同批依序成功，證據從實際結果取得。 |
| PLAN-047 | 活動計畫內的任務關聯、容量或寫入入口政策拒絕，停止本回合；其餘呼叫 NotExecuted／turn_stopped，不替換工具重試。第五次生成包含 submit_plan／update_task 也整批 generation_limit，零狀態更新。 |
| PLAN-048 | 已綁定任務的工作區工具返回非成功時，保留結果並將在途任務 Blocked，停止剩餘工具與生成。這是有活動任務時的增量行為；沒有任務關聯的安全唯讀失敗仍沿既有分類。已成功寫入事實與敏感／Unconfirmed／CleanupFailed 的優先順序不降低。 |
| PLAN-049 | 切模式只發生在回合完成收尾後，確認也不與工具並行；世代／政策失效時，遲到事件、預留或狀態更新不得重新啟動執行。操作一旦開始後的取消與寫入查核仍依第 06 份完成收尾。 |

## 9. 資源限制、模型投影與用量

| 資源 | 固定值／單位 | 超限處理 |
|---|---|---|
| 活動計畫／任務／InProgress | 1 份／1～20 項／最多 1 項 | 原子拒絕，不裁切任務。 |
| objective／title／acceptance／summary | 各 2048／256／1024／2048 UTF-16 code units | 檢查原始輸入後 Trim，過長整筆拒絕。 |
| 每任務 requirements | 最多 8 項 | 不自動拆任務或省略條件。 |
| 證據帳本 | 最多 256 筆，包含退役任務／舊計畫事實及分析判斷 | 不淘汰必要紀錄；開始需要新證據的操作前拒絕，提示 /clear 後重新規劃。 |
| 模型計畫投影及自動轉移容量 | 實際 object 與保守容量投影各最多 32768 UTF-8 JSON bytes | 拒絕相應變更或操作，不截斷後當成同一計畫。 |
| 新工具結果／核准預覽／tasks 顯示 | 各完整區塊最多 65536 UTF-8 bytes | 各自檢查欄名、跳脫、標籤與提示；不得略去任務後繼續。 |
| 既有回合／生成／實際請求 | 5 個完整成功回合／每回合 5 次 GenerateAsync／262144 UTF-8 JSON bytes | 沿既有停止，不加裁歷史、摘要、重試或第六次生成。 |

text 欄位為單行合法 Unicode，可含一般空白；不接受控制字元、CR／LF、非法代理字元或 U+2028／U+2029。路徑、行號、工具讀寫、預覽及快照另沿既有尺度；每欄合格不代表整份投影或最終模型 body 合格。

模型投影固定欄位為 mode、plan。plan 為 null 或含 plan_id、revision、approval、objective、tasks；task 依序為 task_id、kind、title、acceptance、requirements、state、summary、block_reason、completion_basis、completion_fact_ids。requirement 順序為 tool、path 及適用的 start_line、end_line。summary／block_reason 無值為 null，識別集合無值為空陣列。已退役事實保留在本機帳本，投影只包含活動任務的完成事實識別，不附帳本全文。

每次候選計畫／進度更新另形成一份**保守容量投影**，只用於計量，不保存或送模型：保持候選使用者文字與 requirements；mode 使用 execute、approval 使用 approved、revision 使用 long.MaxValue 的十進位數字。所有 plan_id／task_id 以 32 個 ASCII 字元計量；每個 task 的 state、block_reason、completion_basis 分別使用最長固定值 in_progress、capability_unavailable、model_assessment；completion_fact_ids 預留該任務 requirements 筆數（Analysis 為 1、Validation 為 0）個各 32 ASCII 字元的識別。summary 保留候選原值，所有自動轉移只改固定原因而不新增或擴寫 summary。此不一定代表合法狀態的計量物件只作安全上界，不能發布為本機事實。

實際投影及此上界均須不超過 32768 bytes；相應完整核准／tasks 顯示亦須以此上界估算並各不超過 65536 bytes，舊事實計數按最大 256 計量。這項餘裕在計畫發布後持續保留，涵蓋尚未執行任何工具就取消、模型結束、切模式與確認字串增長，不能等到工具開始才預留。因計量餘裕拒絕的候選即使目前實際 bytes 尚未達上限也不得接受。

```json
{"mode":"plan","plan":null}
```

| 編號 | 預算及投影要求 |
|---|---|
| PLAN-050 | 上表為原擁有者執行的固定限制，不新增可調寬設定。256 筆上限不因換計畫／修訂重置；/clear、強制重設、換 Key 或退出才丟棄帳本，識別計數仍依 PLAN-012。 |
| PLAN-051 | 任意計畫／進度候選均先符合本節保守容量投影，進入 InProgress 時已持有必然收尾的容量餘裕。有 InProgress 任務時，每個開始的工作區工具與 Analysis 完成另先預留一筆事實，再開始操作；不足為 evidence_limit，零該次 I/O 或分析完成。預留不算成功，完成／失敗／取消各發布至多一筆，未開始者釋放預留。 |
| PLAN-052 | 提交或更新核對實際與保守投影、核准及顯示容量，全部通過才發布；plan_too_large 不半套用。模式／確認／回合收尾只在已保留的上界內改固定欄位，容量拒絕也能安全將在途任務轉受阻，不能在寫入後才丟事實或取消時無法收尾。不得把檔案內容或任意參數當成預留資料。 |
| PLAN-053 | 每次 GenerateAsync 前由 Runner 從當前模式／PlanStore 重新建立受控狀態附錄，透過既有 LlmRequest.SystemInstruction 承載；計畫文字明確作為資料，不成為可變權限來源。既有固定應用指示不向 CLI／文件傾印。 |
| PLAN-054 | 狀態附錄不加入 ConversationContext、不偽造使用者斜線指令、不修改 SDK 原始模型紀錄或簽章。任務狀態跨五回合淘汰仍可接續，但不恢復失敗工具鏈、讀取快照或舊核准。完整 SDK body 仍含附錄、工具及歷史接受原邊界檢查。 |
| PLAN-055 | 本機指令、計畫確認、任務更新與檔案工具不自行記帳；工具循環內為取得它們而發生的生成仍由 Runner 對每個返回 LlmResult 記一次。程序累計跨 clear／換 Key 保留，沒有額外認證、規劃、摘要或統計生成。 |

## 10. 取消、清除、失敗與釋放

| 事件 | 計畫、任務及模式處理 |
|---|---|
| 使用者取消回合／達生成上限／一般模型失敗、拒答或截斷 | 已接受的事實及已完成任務保留；仍在途任務 Blocked，附受控原因；模式及版本確認保留，等待下一個使用者回合。 |
| 正常成功回合結束，任務仍 InProgress | 轉 Blocked／turn_paused，保留 summary／證據；成功回合仍可提交歷史，但任務未完成，也沒有背景執行。 |
| 拒絕單次寫入／衝突／寫入失敗／Unconfirmed | 先回報已知檔案事實，再封鎖任務並停止；不自動重試、換候選或復原。 |
| `/plan` | 停止將任務視為在途，改 Blocked／mode_changed；同版本確認可保留，但 Plan 權限仍禁止副作用。 |
| `/clear`／RequiresContextReset | 沿共同路徑清除 Context、快照、計畫、確認、任務、預留與證據；保留目前模式、工作區、Key、模型及程序累計。 |
| 接受替換 Key | 舊政策有效時先安全顯示事實／用量，再共同清除並釋放舊資源；新 session 不承接計畫、核准或證據，目前為 Plan 時維持 Plan。 |
| 退出／Dispose | 停止新操作，取消並等待確認／工具／Runner 收尾，清除引用，再依原順序釋放工作區、client 及 session；新的 ChatApplication 預設 Execute。 |

| 編號 | 生命週期要求 |
|---|---|
| PLAN-056 | 中止不是完成；受控原因限 turn_cancelled、generation_limit、model_outcome、operation_failed、approval_declined、file_conflict、write_unconfirmed、mode_changed、capability_unavailable、model_reported、resource_limit、workspace_unavailable、local_failure、turn_paused。CLI 顯示對應固定文字，turn_paused 為「等待下一則指示；任務尚未完成」，不附原始例外。 |
| PLAN-057 | 任一回合結束的最終決策點不得留下 InProgress；合法成功回合仍在途時以 turn_paused 封鎖，保留摘要及成功歷史提交資格，不假造模型失敗。已接受的 Completed 不因後續回合取消撤銷。下一則一般訊息可要求繼續，但須新的 update_task in_progress 與新操作核准，不自動續跑。 |
| PLAN-058 | 清除世代使所有舊快照、確認預覽、證據預留及借用狀態失效；未完成工作的遲到回呼只能被丟棄，不能在 clear／換 Key 後補回計畫。先完成必要的已發布寫入查核與安全回報，再清除可清除的自有參考。 |
| PLAN-059 | 普通錯誤不清除安全的先前計畫／證據，敏感資訊、非法協定、配對失敗或無安全結果則沿共同重設。強制清除時仍保留 Plan 模式限制，不能悄悄改成 Execute。 |
| PLAN-060 | 等待一般輸入 Ctrl+C、EOF、生成／檔案核准期間的控制鍵及退出碼維持既有規則；新增計畫確認的 Ctrl+C 特例僅適用 AwaitingPlanApproval。本機容量或計畫拒絕可回輸入，不偽裝成模型錯誤或一律終止程序。 |
| PLAN-061 | 清除只丟棄自有記憶體參考，不清除終端機捲動內容，不保證覆寫全部 runtime／SDK 複本；沒有檔案刪除、保存、背景恢復或自動重播。 |

## 11. 錯誤分類與敏感資訊

| 條件 | ToolStatus／固定 error_code | 後續 |
|---|---|---|
| Schema、型別、重複／額外欄位、跨欄位或字串／行號限制不合法 | Rejected／invalid_arguments | 新工具零狀態變更、零工作區 I/O；可在原額度內由模型明確更正。 |
| requirement 路徑非法、越界或敏感 | Rejected／invalid_path、outside_workspace、sensitive_path | 沿既有純路徑政策分類，零存在性／metadata／內容 I/O。 |
| 已登錄工具為目前模式禁止／未指定能力 | Rejected／mode_restricted | 整批預檢或入口拒絕，停止回合。 |
| 缺少活動計畫／尚未確認 | Rejected／plan_required、plan_not_approved | 零該次操作，停止回合。 |
| PlanId／expected_revision 不符／task_id 不存在或已退役 | Rejected／plan_revision_conflict、task_not_found | 零該次變更；可依目前投影明確重新提出，不自動套新版。 |
| 無變更／不合法狀態／另一任務在途 | Rejected／no_change、invalid_task_transition、task_in_progress | 保留原狀態，不自動切掉另一任務。 |
| 寫入無合規任務／工具或目標不在任務條件 | Rejected／task_required、task_scope_mismatch | 零該次目標 I/O／核准／寫入，停止回合。 |
| 必要成功事實不足／要求完成尚不可用的驗證 | Rejected／evidence_required、capability_unavailable | 不接受 Completed；不執行或捏造程序。 |
| 證據／投影或顯示容量不足 | Rejected／evidence_limit、plan_too_large | 原子拒絕；操作前容量拒絕停止回合，不補請求或裁切計畫。 |
| 敏感欄位／內容／投影 | Rejected／sensitive_arguments、sensitive_content；不能安全投影時 Response=null | 零敏感內容發布，硬停止並共同重設；保留已知安全寫入事實。 |
| 呼叫前已取消／服務已清除或釋放 | Cancelled／none，或 Failed／unavailable | 無新變更；沿原取消／不可用收尾。 |

Validation 的 in_progress 請求為第 7 節明確定義的成功 Blocked 更新；只有 completed 請求被 capability_unavailable 拒絕。版本／任務／參數等可更正錯誤不自動阻塞其他任務；但任務綁定工作區操作的非成功結果仍依 PLAN-048 停止。模式預檢前的敏感、取消與第五次生成保留既有優先順序；回合收尾的 Unconfirmed／CleanupFailed 不被一般取消或任務原因覆蓋。

| 編號 | 錯誤及安全要求 |
|---|---|
| PLAN-062 | 新增上述必要 ToolErrorCodes、工作區 ModeRestricted／PlanNotApproved／TaskRequired／TaskScopeMismatch／EvidenceLimit／PlanTooLarge 及相應 snake_case 投影。Runner 新增 ModeRestricted、PlanRequired、PlanNotApproved、TaskBlocked、TaskResourceLimit：依序對應模式拒絕、plan_required、plan_not_approved、任務關聯拒絕／任務操作失敗、操作前容量拒絕，回合為 Stopped；原有敏感、取消、寫入及本機失敗的更嚴格分類優先。不加入公開 LlmErrorCode，保留寫入 NotApplied 事實。 |
| PLAN-063 | Key 只經隱藏輸入並留記憶體；原始及 Trim／正規化後文字、所有 requirements、分析摘要、工具結果、完整模型附錄、核准區塊與顯示區塊皆經目前政策檢查。固定欄名、模式名稱及識別命中短 Key 也不得豁免。 |
| PLAN-064 | 不顯示完整應用指示、thought、簽章、Key、認證、原始 SDK／HTTP／例外或參數傾印。計畫、檔案與工具結果中的假模式、假核准、假操作／退出碼只作資料，不能建立授權或本機事實。 |
| PLAN-065 | 計畫內 path 僅為受限目標描述，不代表存取授權；實際工具仍重驗工作區、敏感路徑、連結、大小、內容、快照、衝突與逐次確認。模型分析文字不能取代這些檢查。 |
| PLAN-066 | 敏感檢查或顯示失敗不得把部分計畫視為已確認；本機已知副作用仍以可安全顯示的欄位及數值回報。無法形成完整工具 Response 時沿既有停止／重設，不遮蔽後繼續沿用候選或授權。 |

## 12. 後續項目交接

FUT-03 與 FUT-02 協調執行、進度及核准，與 FUT-04 協調任務狀態、Context 及預算。兩者均採共用契約、各自規格與驗收；FUT-03 可配合既有工具獨立交付，不必等待 FUT-04。以下只記錄交接責任，不提前實作後續能力。

<a id="fut-02-handoff"></a>

### 12.1 FUT-02「受控建置與測試」交接紀錄

本節是下次撰寫 FUT-02 的必讀契約與待決事項，不是程序執行器規格或產品實作。FUT-02 的文件編號於選題時決定，不預建不存在的文件連結。共用契約以本節為單一來源；FUT-02 如需改動，須列明相容性影響並同步修訂，不另外複製一套任務狀態或自動核准語意。

| 交接編號 | 本份已定案／FUT-02 必須承接 | FUT-02 必須定案／驗收 |
|---|---|---|
| FUT02-HO-001 | 重用 AgentMode、TaskExecutionContext、TaskId 與程序產生的操作識別；Plan 一律禁止啟動建置／測試，即使工具宣稱安全、唯讀或 dry-run。 | 程序工具能力登錄、入口檢查與假造呼叫回歸；未知程序能力預設禁止。 |
| FUT02-HO-002 | 計畫確認與每次程序操作核准分離；計畫文字、模型 yes 或任務 InProgress 都不能核准啟動。 | 明確可核對的任務、可執行檔／參數、工作目錄、環境／網路權限與副作用預覽；核准來源、版本綁定及啟動前重驗。 |
| FUT02-HO-003 | 有計畫時關聯目前已確認版本及 InProgress 的 Validation 任務；無計畫的 Execute 仍可提供逐次核准的獨立開發任務，不強迫先有計畫。 | Validation requirements 的版本化擴充、允許任務識別及與執行結果的精確比對；不得用任意字串命令替代受控任務。 |
| FUT02-HO-004 | 程序結果需分清 Started／StartFailed、Exited、Cancelled、TimedOut、Unconfirmed；退出碼只在實際正常結束可得時填值。模型文字不能建立此結果。 | 定義程序結果型別、啟動權決策點、退出碼、收尾完成與結果不可得的明確語意，擴充本機事實而非公開 LlmErrorCode。 |
| FUT02-HO-005 | 驗證完成至少須：正確且已核准的任務確實執行、正常結束、退出碼 0、必要收尾已確認且結果可安全使用；非零、未啟動、取消、逾時或無法確認不得完成。 | 各受控建置／測試任務的成功判準；區分「命令正常結束」「建置成功」「測試實際執行且通過」與尚未驗證範圍。 |
| FUT02-HO-006 | 輸出截斷與程序結果分開；截斷不能捏造成完整測試報告。原始 stdout／stderr 不進任務證據帳本，認證不能進子程序參數、環境、stdin、輸出或日誌。 | stdout／stderr byte 上限、解碼、控制字元、敏感檢查、截斷標示、模型回填與證據保留；訂明輸出不足時哪些結論不能成立。 |
| FUT02-HO-007 | 建置測試會執行專案程式碼並可能修改工作區；不能只靠命令白名單宣稱隔離。程序輸出不能建立完整讀檔修改快照。 | 隔離方式及真實能力邊界、工作目錄、環境及網路政策；取得程序啟動權前清空讀取快照，執行／收尾後再次清空，不能恢復舊快照。 |
| FUT02-HO-008 | 取消、逾時、拒絕或執行失敗須停止目前執行流程，保留先前檔案 Verified 事實；不自動重試、換命令、復原或啟動修正循環。 | 程序樹終止、寬限與強制終止、管線／輸出讀取收尾、孤兒程序及無法確認停止的處理；不得僅取消等待就宣稱程序已停止。 |
| FUT02-HO-009 | 沿用五次生成、五回合、256 筆事實及實際請求 body 上限；程序時間不等同模型 120 秒期限，不把提高生成上限當作修正循環設計。 | 執行逾時、輸出／資源上限及進度事件；擴大長任務前須回讀 [FUT-04 交接專節](#fut-04-handoff)的容量、記帳及耗盡處理，不在本份訂假定數值。 |
| FUT02-HO-010 | 程式執行結果僅由受控本機轉接器產生；舊計畫確認、跨 session 身分或模型造出的結果不得注入帳本。 | 以假程序／可控制執行器及隔離測試，驗證 Plan 拒執行、逐次核准、結果映射、快照失效、取消／逾時／輸出與容量邊界；真人與真實服務另記。 |

<a id="fut-04-handoff"></a>

### 12.2 FUT-04「Context 與預算管理」交接紀錄

ROADMAP 第 2.2 節要求 FUT-03／04／08 協調任務狀態、摘要與保存資料。FUT-03 已定義獨立於五回合歷史的任務來源、安全投影、證據上限與清除流程；FUT-04 日後若管理歷史、摘要或執行預算，必須接續這些契約，避免摘要遺失約束、替代事實或繞過核准。FUT-04 是 ROADMAP 項目代號，不是既有第 04 份工作區規格。

本節每列的「本份已定案契約」是後續不得默默放寬的整合界線；「必須決定的事項」留給 FUT-04 獨立規格，不在本次選定摘要策略、觸發門檻、可調預算或新增 API。後續驗收由 FUT-04 實作時執行；本次僅以 PLAN-AC-036 查核交接文件完整性。

| 交接編號 | 本份已定案契約 | FUT-04 必須決定的事項 | FUT-04 後續驗收 |
|---|---|---|---|
| FUT04-HO-001 | PlanStore 是計畫、版本、任務狀態及完成證據的本機來源；歷史淘汰或模型摘要不能改寫這些事實。模型請求使用當前不可變投影，摘要不能自行改身分、任務狀態或核准。 | 定義 Context 管理取得任務快照的介面、更新時機及 PlanId／Revision／清除世代核對；如需調整既有契約，明示相容性影響。 | 歷史壓縮前後本機計畫與證據不被改寫；舊版本／世代的摘要或遲到發布不能覆蓋目前狀態、復活退役任務或建立完成事實。 |
| FUT04-HO-002 | 接續資訊須保留目標、必要約束、已做決策、目前計畫版本、待辦、受阻原因及完成依據；本機狀態仍從 PlanStore 取得。分析判斷與已核對操作分列，摘要文字不能取得權限、代替計畫確認或單次核准。 | 定義摘要格式、來源、必留／可省內容、選取與壓縮策略；必要資訊無法保留時的停止及提示方式。 | 摘要前後必要約束及未完成事項可追溯；假指令／假 yes／模型聲稱完成不產生授權，也不將 ModelAssessment 改成 LocalOperation。 |
| FUT04-HO-003 | 完整工具呼叫／結果配對與不透明 SDK 紀錄／簽章的合法性仍由原邊界驗證；不能直接裁改原生片段後續傳。舊檔案內容或摘要不是目前磁碟狀態，也不能建立修改快照。 | 定義摘要插入／替代的訊息位置、完整回合保留單位、協定與簽章相容性，以及無法合法接續時的處理。 | 真實 SDK 配假 handler 驗證無孤立呼叫／結果、無簽章改寫；舊檔案摘要不能滿足 SnapshotRequired，修改仍須依原完整讀檔及重驗。 |
| FUT04-HO-004 | 容量須涵蓋計畫投影、工具定義、歷史、當前工具結果及協定資料，維持第 9 節實際／保守投影與自動收尾餘裕。UTF-8 JSON bytes、估算 token、API 回報用量及模型可用容量不得混為精確同一數值。 | 定義容量估算／顯示、模型容量來源、摘要觸發門檻、預留與超限策略；如需改變既有限制，列明擁有模組、相容性與驗收，不只調高常數。 | 含計畫附錄、工具、歷史與結果的實際 body 邊界仍正確；接近上限時取消／切模式／確認可收尾，不能靠省略計畫條件或必要證據取得空間。 |
| FUT04-HO-005 | 摘要生成也須納入生成次數、傳輸嘗試及用量累計；保留唯一記帳入口、缺值／溢位語意及程序累計。新規格明示修訂前仍守每回合五次生成，不以輔助 client／背景請求繞過 Runner 或重複記帳。 | 定義預算的回合／計畫／程序範圍、可調設定及優先順序、摘要成本與額度分配，以及修訂／clear／換 Key 對預算的影響；估算與 API 實際用量分列。 | 摘要成功、失敗、取消及未傳輸分別正確記帳，不補請求取得統計；clear／換 Key 不抹除既有程序用量，計畫修訂不成為重置預算的隱含旁路。 |
| FUT04-HO-006 | 預算耗盡或容量不足不等於任務完成；保留已接受的完成依據、Verified／Unconfirmed 等實際操作事實及未完成原因。不能自動重試、重放寫入、還原檔案或沿用單次核准。 | 定義耗盡前提示、操作開始前的預算檢查、停止原因與任務 Blocked 的對應，以及後續明確繼續時需重驗的狀態；與 FUT-02 分清程序時間及模型預算。 | 先寫入 A 後耗盡時，A 的事實保留，其餘未執行；任務受阻而非整項完成，後續不重播 A，進行新操作仍須符合當前模式／版本及逐次核准。 |
| FUT04-HO-007 | 正常歷史壓縮不等於 /clear 或 RequiresContextReset；真正安全重設、換 Key 及退出仍清除計畫／證據／確認。摘要失敗或取消不能發布半份 Context，遲到結果不能復活已清除資料；目前為 Plan 時不因此放寬模式。 | 定義摘要候選的原子發布與取消決策點、一般摘要失敗時保留安全舊 Context 的條件，以及敏感／非法協定必須走共同重設的處理。 | 可控制摘要失敗／取消／遲到完成及 clear／換 Key 的競態；安全舊狀態或完整新狀態擇一，不混用世代；敏感失敗不能以普通壓縮避開清除，Plan 限制保持有效。 |
| FUT04-HO-008 | 本份 20 任務／1 在途／256 筆事實等限制仍由原擁有者執行；歷史摘要不構成調高限制、淘汰必要證據或跨次保存的授權。跨程序／跨次保存留給 FUT-08，多 Agent 的總預算及協調另屬 FUT-14。 | 定義與任務／證據容量拒絕的銜接；若變更上限或資料保留，明示型別／資料相容性與新驗收。摘要策略、觸發門檻及失敗處理由 FUT-04 定案，不合併 FUT-08／14 開發。 | 任務與證據容量不足時不靜默省略條件、丟棄事實或假造完成；沒有新存檔、核准恢復、背景排程或多 Agent 能力，跨項目待辦仍可追溯。 |

### 12.3 共用交接要求

| 編號 | 交接要求 |
|---|---|
| PLAN-067 | FUT-03 只實作本份任務及既有工具整合，不新增程序工具、可執行命令集合、程序轉接器、環境設定或空殼執行器；可用假結果做契約驗收，不能以假結果宣稱真實程序能力完成。 |
| PLAN-068 | FUT-02 撰寫時逐項回填 FUT02-HO-001～010 的承接章節／需求／驗收；若調整共用型別或規則，先列明本份受影響需求及遷移方式，不能默默放寬 Plan 或核准。 |
| PLAN-069 | FUT-02 的未決事項刻意留給該份規格：程序啟動 API、允許任務、隔離、環境／網路權限、核准內容、逾時／輸出數值、程序樹收尾及結果判準。本份 FUT-03 本身的行為、限制與驗收不得使用 TBD。 |
| PLAN-070 | FUT-04 規格須逐項承接 [FUT04-HO-001～008](#fut-04-handoff)，回填章節／需求／驗收，改動共用契約時明示相容性及本份受影響需求。本份只記錄交接，不自動摘要、加入可調預算、跨次保存或恢復核准／快照；保存另屬 FUT-08，FUT-14 不由本份取得多 Agent 權限。 |

## 13. 需求對照與後續產品驗收

### 13.1 方法及證據層級

| 編號 | 驗收要求 |
|---|---|
| PLAN-071 | 後續離線測試使用合成 Key、假終端機、可控制檔案系統、實際工作區入口與真實 SDK 配假 HTTP handler；驗證呼叫數、模式／狀態、真實 bytes、零 I/O、核准次數、證據與原生配對，不連 Gemini 或啟動建置／測試程序。 |
| PLAN-072 | MSTest 使用 acceptance-linked 名稱，如 PLANAC002_PlanRejectsWritesAtEveryBoundary、PLANAC015_AllRequiredTargetsNeedVerifiedFacts；既有 01～08 測試目的保留，僅更新本份明示擴充的工具集合／CLI 預期，不刪安全測試取得通過。 |
| PLAN-073 | 產品離線完成至少需 PLAN-AC-001～032 及既有回歸通過；PTY 另需 033，真人另需 034，真實 Gemini 另需 035。第 07 份發布門檻另計，不能以文件／假程序／PTY 取代。 |

### 13.2 離線產品案例

| 案例 | 對應需求 | 必須驗證的結果 |
|---|---|---|
| PLAN-AC-001 | PLAN-001～011、015～016、021 | 真實 ChatApplication 預設 Execute；無計畫一般聊天／七檔案工具行為、help／--help／未知指令保持相容，新指令零生成／記帳；僅加入本份能力。 |
| PLAN-AC-002 | PLAN-007、010～011、037、044～045、062、065 | Plan 五唯讀及 submit_plan 定義順序；偽造寫入、Registry 直接派送、工作區直接寫入均 mode_restricted，零 metadata／內容 I/O、預覽、核准、暫存及發布。 |
| PLAN-AC-003 | PLAN-016～019、023、049、060 | Draft 完整版本預覽與隱藏 y／yes，空白／n拒絕、無效重問；確認只切 Execute，下一則一般訊息前零生成／工具。已確認同版切回不新增授權。 |
| PLAN-AC-004 | PLAN-012～013、017～019、058 | 預先貼上 yes、過期版本／世代、清除後遲到確認不能批准；Ctrl+C 回 Plan、Ctrl+D／EOF 退出 0、I/O 退出 1，確認期間只有一個讀取者。 |
| PLAN-AC-005 | PLAN-014、020～023、029、052、063～066 | /tasks 的本機模式、完整任務、必要操作、完成依據與受阻原因；安全純文字、控制字元／路徑跳脫、短 Key 及完整區塊失敗不部分確認，不顯示秘密或完整應用指示。 |
| PLAN-AC-006 | PLAN-024～027、030、062 | 兩工具所有層級的缺欄、額外／重複／大小寫、null、錯型別、非 object、數字格式及 wire 重複欄位；invalid_arguments／原協定分類，零部分狀態與工作區 I/O。 |
| PLAN-AC-007 | PLAN-025～027、030、050 | 原始 UTF-16 長度各上限及加一、Trim 後空白、中文／補充平面／非法代理／控制字元；20／21 任務、8／9 requirements、範圍 200／201 行，正確原子接受或拒絕。 |
| PLAN-AC-008 | PLAN-012～013、027、030～031 | 初始 expected_revision=0→本機識別／版本1；過期版本拒絕；完全相同提交 no_change；排序修訂增加版本並失效確認，溢位不重用識別。 |
| PLAN-AC-009 | PLAN-030～035、043 | task_id 精確相同定義／排序／首尾空白可保留證據；改目標、kind、完成條件或文字配新 TaskId／Pending；未知、重複、退役身分拒絕。 |
| PLAN-AC-010 | PLAN-032～035、043、050 | 更換 Objective 得新 PlanId、全新任務；舊事實保留原歸屬與容量，不重綁、不恢復退役身分；舊確認無效，檔案 bytes 不回復。 |
| PLAN-AC-011 | PLAN-028、036、046 | Pending／Blocked→InProgress、最多一項在途；重複開始、Pending→Completed、重複完成及重開 Completed 拒絕，進度不改結構版本。 |
| PLAN-AC-012 | PLAN-028、036～037、044、047、062 | 有活動計畫時無在途 Write、未確認、工具／path 不符均零該次 I/O／核准／寫入；無計畫 Execute 仍逐筆核准正常使用。 |
| PLAN-AC-013 | PLAN-038～040、043、046 | 同批 update_task開始→真實工作區讀寫→完成，OperationId／Context 由程式產生；本機選取證據，既有七工具 Schema／JSON 不被改寫。 |
| PLAN-AC-014 | PLAN-038～040、063～065 | 任意模型 evidence／exit_code／approved 欄位、假工具報告、不同任務／計畫事實均不能完成；失敗、未執行、截斷及後來相符失敗不能由舊成功掩蓋。 |
| PLAN-AC-015 | PLAN-037～040、043 | 跨檔 Read／Write 缺任一必要結果不完成；全部相符且不同的成功事實才完成，Write 每項須 Verified；範圍／空檔／EOF摘要不冒充完整讀取。 |
| PLAN-AC-016 | PLAN-015、038～043、067 | Analysis 完成需可檢視的安全摘要並標 ModelAssessment；Validation 開始→Blocked／capability_unavailable，要求完成被拒絕，零程序啟動、不捏造測試。 |
| PLAN-AC-017 | PLAN-042、048、056～057、066 | A Verified、B拒絕／衝突／失敗：A保留，任務受阻，其餘未執行，零後續生成；Unconfirmed／CleanupFailed 不被取消或任務狀態降級。 |
| PLAN-AC-018 | PLAN-022、042、049、056～060 | 在模型／工具／完成提交各決策點取消、晚到成功、生成失敗或截斷：沒有殘留 InProgress，已接受完成／檔案事實保留，失敗鏈不提交、不重放。另測正常開始任務後直接最終回答：Blocked／turn_paused、摘要保留、成功歷史照常提交，任務不算完成。 |
| PLAN-AC-019 | PLAN-013、022、035、043、049、056～061 | 切 Plan 封鎖在途任務；同版確認保留也不能寫入；再切 Execute 不自動生成／恢復任務；修訂不刷新快照或沿用單次操作核准。 |
| PLAN-AC-020 | PLAN-005、012～013、050、054、058～061 | 真實 /clear、強制重設、換 Key、退出／Dispose 清除所有計畫／證據／預留；Plan模式限制及程序用量依規則保留，遲到事件不復活，磁碟不回復。 |
| PLAN-AC-021 | PLAN-024、044～048、063～066 | 批次後段含 Key 時前段也零 I/O／狀態更新；混合合法讀取／模式禁止工具整批不執行且逐呼叫結果完整；偽造模式／假 yes 不授權。 |
| PLAN-AC-022 | PLAN-015、045～047、054～055 | 第五次含 submit_plan／update_task／讀寫全部 generation_limit，零狀態／證據／操作且無第六次；最終文字成功也不自動完成任務。 |
| PLAN-AC-023 | PLAN-024～029、044～046、053～054 | 真實 SDK 假 handler 擷取 Plan六工具／Execute八工具、嚴格 Schema、原生 PartIndex／CallId／簽章及混合工具原序配對；歷史含舊工具不放寬目前模式。 |
| PLAN-AC-024 | PLAN-027、029、050～054 | 實際／保守投影各自 32767／32768／32769 bytes、新結果及實際／保守預覽 65535／65536／65537 bytes，包含欄名／跳脫／Unicode；即使實際合格而上界超限亦拒絕。模式／確認變動不突破保留容量，無截斷批准。 |
| PLAN-AC-025 | PLAN-038、041、050～052、056 | 255→256筆成功，下一筆操作或分析完成開始前 evidence_limit、零該次 I/O／更新；退役／新計畫仍計容量，未開始預留可釋放，取消／失敗不重複計錄。 |
| PLAN-AC-026 | PLAN-027、029、042、051～052、056～057、066 | update_task 進行中後尚未開始工具即取消、模型失敗／正常結束或操作容量拒絕，在最接近上限的合法保守投影下均能轉 Blocked 且不超限；工具失敗／取消／Response不可用保留安全事實。更新超限不半套用，不在寫入後才丟事實。 |
| PLAN-AC-027 | PLAN-007、022、035、043、053～054 | 超過五成功回合後活動任務仍在投影；失敗回合不提交，模式附錄不污染歷史；搜尋失效快照、完整讀檔與寫入重驗回歸，不以任務證據取代基準。 |
| PLAN-AC-028 | PLAN-050、053～055、063 | 真實 SDK body 262144／262145 bytes 含狀態附錄、模式工具、歷史、簽章與多結果；超限零當次傳輸、不裁歷史、不摘要，既有寫入事實保留。 |
| PLAN-AC-029 | PLAN-016、021、055、059～060 | 新本機指令／確認零生成，工具回合每次模型結果記一次；缺值／溢位、清除／換 Key 程序累計與模型錯誤分類仍正確。 |
| PLAN-AC-030 | PLAN-010～014、049、058～061 | 並行入口、重複釋放、清除世代與遲到確認／證據預留，零第二讀取者、零重開 I/O；等待自有收尾後才釋放政策，無計畫／原文落地。 |
| PLAN-AC-031 | PLAN-062、067～070 | 僅驗證既有共用身分／世代、Plan禁止副作用及未登錄程序能力、計畫確認不授權執行、Validation無能力受阻；模型或假工具帶入成功／非零／未啟動／取消／逾時／未確認報告均不能注入證據或完成驗證。不建立可接受程序結果的產品入口；正向結果映射、程序前後快照與收尾留給 FUT02-HO-003～010 的後續驗收。 |
| PLAN-AC-032 | PLAN-001～009、065、070～073 | 全部既有 01～08 回歸通過，只按明示擴充修改預期；沒有額外設定、套件、認證、Git／程序／保存／多Agent能力，第07未結門檻分列。 |

### 13.3 PTY、真人與真實 Gemini

| 案例 | 對應需求 | 驗收方式 |
|---|---|---|
| PLAN-AC-033 | PLAN-016～023、049、056～061、071～073 | 離線 macOS PTY：模式標籤、新指令、完整版本確認、不回顯與緩衝防偽、拒絕、Ctrl+C 回 Plan、Ctrl+D、busy 丟棄、執行後仍逐次檔案核准及回顯恢復；不宣稱真人操作。 |
| PLAN-AC-034 | PLAN-016～023、056～061、073 | 真人終端機使用合成資料確認中文／Unicode、長清單、模式可辨識、拒絕與取消、確認後須下一訊息執行，逐筆寫入核准與退出；只記安全結果。 |
| PLAN-AC-035 | PLAN-015、024～029、036～055、067、073 | 使用者自行隱藏輸入真實 Key，以小型工作區完成 Plan探索→submit_plan→修訂→本機確認→下一訊息Execute→真實讀寫與update_task；確認分析／本機修改／尚未建置測試分列。不讀取或記錄真實Key，不以假handler替代此層。 |

後續產品實作完成後，依序執行既有離線命令，記錄當次環境、commit／工作區狀態、命令、退出碼、實際通過／失敗／略過數與未驗證項目：

```sh
dotnet restore tests/AIAgent.Cli.Tests/AIAgent.Cli.Tests.csproj
dotnet build tests/AIAgent.Cli.Tests/AIAgent.Cli.Tests.csproj --no-restore
dotnet test --project tests/AIAgent.Cli.Tests/AIAgent.Cli.Tests.csproj --no-build
python3 -B -m unittest discover -s tests -p 'test_sensitive_content.py'
dotnet run --project src/AIAgent.Cli/AIAgent.Cli.csproj --no-build -- --help
dotnet format src/AIAgent.Cli/AIAgent.Cli.csproj --no-restore --verify-no-changes
python3 -B tests/terminal_smoke.py
git diff --check
```

以上是未來產品驗收命令；本次只寫文件，不執行 restore、build、MSTest、formatter、PTY 或真實 Gemini，也不啟動 FUT-02 程序。

## 14. 本次文件檢查、完成條件與交接

| 編號 | 文件要求 |
|---|---|
| PLAN-074 | 核對已確認的預設 Execute、手動 Plan、確認後等待下一訊息、證據判定、20任務／1在途／256事實／32768投影及 FUT-02／04 分工；需求／驗收唯一連續、每項需求有驗收對照，宣告／Schema／投影／狀態／錯誤與上限一致，既有編號保留。 |
| PLAN-075 | 解析全部 JSON、核對 Schema 欄位／required／additionalProperties 與跨欄位規則、Markdown 區塊／表格、相對連結及兩項固定交接錨點；檢查本檔完整差異與空白，確認本次只有本檔修訂、其他檔案及 Git 索引未變，保留 0.1 初稿交付歷史。 |
| PLAN-076 | 只回報本次實際文件檢查及未執行的產品驗收，不把規格、範例、假程序結果或文件審閱當成產品完成；後續另行授權才依對應規格實作，不因文件完成自動啟動 FUT-03／02／04 開發。 |

| 案例 | 對應需求 | 本次文件成功條件 |
|---|---|---|
| PLAN-AC-036 | PLAN-002、067～076 | 全部需求／案例、JSON／Schema、模式工具順序、版本／任務／完成依據、資源尺度、清除與安全停止一致；保留 FUT02-HO-001～010，新增 FUT04-HO-001～008 並逐列區分已定案契約、FUT-04 待決事項及後續驗收。任務來源、必要摘要、協定／時效、容量／記帳、耗盡、清除與後續責任皆有對照；兩個固定錨點、交叉引用、本機連結及空白檢查通過，版本 0.2／待實作、既有需求及案例編號保留，僅修訂本檔，未執行產品驗收。 |

本次文件查核命令如下；編號／JSON／連結另使用不落地的只讀腳本檢查，不新增檢查腳本作為交付物：

```sh
git diff --check
git diff -- specs/09-plan-mode-and-task-list.md
git status --short
git diff --name-only
git diff --cached --name-only
```

本次是既有追蹤檔修訂，以 git diff 核對實際變更；若後續另有未追蹤文件，仍須單獨檢查，不能以一般 diff 沒有列出便宣稱已查核。文件完成後，FUT-02／04 規格作者分別先讀本檔 [12.1](#fut-02-handoff)／[12.2](#fut-04-handoff)，再核對當時程式、README 與尚未完成的能力；不得將交接待辦、摘要或假結果當成產品完成證據。

## 參考資料

- [專案未來開發大綱](../ROADMAP.md)：FUT-03 先於 FUT-02 的建議排序、FUT-03／04／08 的任務／摘要協調，以及共用介面、各自交付的界線。
- [Claude Code：Plan before editing](https://code.claude.com/docs/en/common-workflows#plan-before-editing)：2026-10-23 核對，僅參考先探索、提出計畫再確認的流程；本專案的指令、記憶體資料、工具限制、版本及逐次核准均依本規格，不承諾相容其產品行為。
