# 06：檔案編輯與核准子規格

## 第 18 份產品實作接續（2026-10-28）

檔案候選及確認後來源重驗納入 MCP 戳記；外送確認與檔案確認獨立，不能互相沿用。MCP 失效先保留已發布寫入與 checkpoint／事實，再沿共同 Settle 撤確認與清修改基準，不復原已寫檔案。 詳見[第 18 份](18-mcp-external-tools.md)及其固定交接；實際驗證與未驗層級見 [README](../README.md#mcp-validation)。

## 第 17 份產品實作接續（2026-10-27～28）

操作與預覽綁技能戳記，核准後及發布前重驗；合法修改目前 SKILL 使用舊戳記完成發布／查核，先保留寫入與 checkpoint 事實，再 Stale 並停止剩餘工具。詳見[第 17 份](17-reusable-workflows-and-skills.md)及[當次分層驗證](../README.md#skills-validation)。歷史文件交付敘述保留，未結原生／真人／真實 Gemini／Release 門檻不變。

## 第 16 份產品實作接續（2026-10-27）

每次合法非 NoChange 候選先原子預留 checkpoint，再取得有限目標 Git guard、原始預覽與核准；發布後完成來源歷史。滿額停止、不淘汰。ID 限定復原另建目前→before 預覽與新核准，復用同目錄原生暫存、完整 mode／bytes 核對與精確清理；新建檔僅人工，沒有一般刪除入口。原 EDIT-044～063 安全閘門及 metadata／TOCTOU 限制保持。 詳見 [第16份](16-change-checkpoints-and-recovery.md) 與 [當次分層證據](../README.md#checkpoint-validation)。下列原階段敘述保留歷史範圍。

## 第 15 份產品實作接續（2026-10-26）

[工作保存與續接](15-work-save-and-resume.md)的限定增量：工作存檔另有私人目錄用途限定的排他發布與單檔刪除；不增加模型刪除能力，不改一般寫入核准或修改快照。保存／刪除均需完整預覽、新輸入及來源重驗；發布不明不能誤報未保存或自動重試。 本節保留舊階段歷史，實際分層驗證見 [README](../README.md#work-save-validation)。

## 第 14 份產品實作接續（2026-10-26）

[第14份終端機操作改善](14-terminal-interaction.md)只擴充一般ReadInputAsync的隱藏多行／文字元素編輯、F2安全預覽、F3明確貼上、F4換行及程序內安全歷史；Key／認證選單／三種核准仍走原隱藏單行。多行斜線指令在所有解析器之前整筆拒絕，單行精確匹配與Scope／pin字面餘文保持。

CLI輸入狀態加入共同安全清除、認證選單前清除及政策／退出生命週期；不加入Context／摘要／計畫／Git來源或完成證據，沒有新增生成、工具與權限。RULE-HO-006／GIT-HO-006的承接細節見第14份第7、8、9、11、12節與TERM-AC-016～026。原階段歷史保留，當次證據及真人／服務／原生／發布待辦見[README](../README.md#terminal-interaction-validation)。


## 第 13 份產品實作接續（2026-10-26）

[第 13 份 Git 差異與程式碼審查](13-git-diff-and-code-review.md) 新增專用 GitRead 能力與三項唯讀工具，依 Plan／Execute 提供九／十二項；`/review` 為共用 Runner 與生成預算的新入口，該回合只允許三項 Git 工具。原十項工具的順序、輸入與成功資料保持，公開 LLM 契約與 SDK 訊息所有權保持。

Git 來源、覆蓋與報告不建立修改快照、Plan Read requirements 或 Verified／Validation 事實；一般 Git 探索與正式審查分開。僅審目前 Scope 的安全子集；忽略來源的祖先 metadata 例外不放寬一般 `.git`、跨 Scope 讀取或搜尋遇新規則邊界整次拒絕的契約。規則失效、共同重設及已發生操作事實仍依原契約收尾。

實作、當次分層證據與未驗證項目見 [README](../README.md#git-review-validation)。真人、真實 Gemini、第 10 份 test 不支援／未結原生驗收，以及第 07 份 Release／分發門檻分開保留。


## 第 12 份產品實作接續（2026-10-25）

檔案操作與逐次核准另綁目前規則版本／世代；核准後、發布前重驗來源及目標規則邊界。合法修改規則檔先完成原子發布、精確讀回與事實入帳，再標 Stale、撤銷計畫確認及停止餘項；規則載入不提供修改基準，失效不覆蓋已發生結果或解除 Unconfirmed／清理故障。

實作、當次離線／PTY 證據與未驗證部分見 [README](../README.md#project-rules-validation)。真人 RULE-AC-033、真實 Gemini 034、第 10 份未結原生驗收與第 07 份 Release／分發門檻分開保留。

> 文件版本：0.1｜建立日期：2026-10-22｜狀態：待實作規格
>
> 上位文件：[入門 AI Agent 開發總規格書](../SPEC.md)，主要依據第 16 節及第 6、8、11～13、15 章，並承接第 5、7、9～10、14 章的模組、協定及用量約定。前置規格：[01：專案啟動與 API Key](01-bootstrap-and-key.md)、[02：Gemini 用戶端](02-gemini-client.md)、[03：CLI 與 Context](03-cli-and-context.md)、[04：工作區與唯讀工具](04-workspace-and-read-tools.md)、[05：Agent 工具循環](05-agent-loop.md)。01～05 已有實作，目前能力以 [README](../README.md) 與原始碼為準；舊文件的「待實作」描述其撰寫當時狀態。本文件完成不表示寫入工具或第三階段整體驗收已完成。

## 1. 目的、交付邊界與完成標準

將模型提出的單檔建立與精確替換，接到本機產生的差異預覽及使用者逐次核准。讓「提出變更」「同意候選」「發布到目標」「讀回核對」成為可分辨的步驟，並在取消、衝突及 I/O 失敗時保留可證明的檔案事實。

本次交付只新增本檔；不修改總規格、01～05 規格、README、程式、測試、設定、專案、套件或 Git 索引／歷史，不建立第 07 份、額外計畫或空實作，不安裝 hooks，不提交或推送。以下宣告、流程、畫面、限制、案例及產品命令都是後續開發要求，不是目前新增的產品能力或已執行的產品驗收。

需求使用 `EDIT-xxx`，驗收使用 `EDIT-AC-xxx`，新增時接續編號；引用上游時保留 BOOT、GEM、CLI、WS、LOOP 前綴。文件完成與後續產品完成分開計算。

| 編號 | 具體要求 |
|---|---|
| EDIT-001 | 沿用 .NET 10 Console、Google.GenAI 1.26.1、既有模型、v1beta Generate Content、MSTest／Microsoft.Testing.Platform，先驗收 macOS arm64／x64。保留可空值分析、警告視為錯誤及 .editorconfig，不新增套件、設定欄位、認證來源或公開 LLM API。 |
| EDIT-002 | 本次只新增指定規格書，依第 14 節進行文件驗收；本文件內的產品開發、測試及操作均屬後續要求，不因規格寫完宣稱功能已完成。 |
| EDIT-003 | 後續產品完成點包含真實 ChatApplication、四工具 Registry、工作區服務、CLI 核准、真實 SDK 配假 handler 及隔離暫存工作區的離線整合與既有回歸。PTY、真人終端機及真實 Gemini 分列證據，不以模型聲稱成功判定寫入完成。 |
| EDIT-004 | 只增加 create_file、replace_text 的單次單檔操作；同批多筆仍逐一核准、逐一執行，沒有多檔交易或整批核准。不加入 shell、刪除／重新命名使用者檔案、建目錄、直接覆寫工具、搜尋、模糊替換、正規表示式、replace-all、patch、備份／復原、語法驗證、平行工具、背景工作或其他平台承諾。內部暫存檔發布不是向模型開放重新命名能力。 |
| EDIT-005 | 五次生成、五個完整歷史回合、完整模型紀錄與呼叫配對、現有路徑／敏感規則及所有有限資源持續有效；核准等待不生成、不計 token，也不放寬模型或工具限制。第 07 份承接整體成本、提交及發布掃描，不能替代本份當下的安全與回歸驗收。 |

## 2. 既有基線、責任與內部契約

### 2.1 接續位置

本次已閱讀總規格、前置規格及直接相關原始碼，未重新執行既有 build、MSTest、PTY 或真實服務。README 的歷史通過數不是本次證據。

| 既有能力或限制 | 本份後續實作接續方式 |
|---|---|
| Registry 正式只登錄 list_files、read_file，派送前共用 TryPath | 擴充為四工具，各工具自行嚴格解析其參數；保留整批與 Registry 自身入口的敏感檢查，不能讓共用單一 path 規則拒絕所有寫入呼叫。 |
| IWorkspaceFiles、讀取快照、WorkspaceResponseWriter | 擴充同一工作區入口的寫入流程及明確投影，不在 Tools 另寫直接磁碟存取。 |
| IWorkspaceReadFileSystem、MacWorkspaceFileSystem | 保留讀取用途，另提供第 10 節可控制的寫入能力與原生發布轉接；不能把啟動用途的檔案系統介面當工具權限入口。 |
| IChatInteraction、ObserveBusyInput | 核准介面由 CLI 實作，明定 busy 與確認讀取權交接；不讓兩個讀取者爭搶 y／yes。 |
| ToolExecutionResult、LocalToolOutcome、AgentRunner | 增加獨立寫入事實與停止原因；即使工具 Response 無法安全建立，也不抹除已完成或未確認的寫入。 |
| ChatApplication、ClearConversationState、session 生命週期 | 共同清除候選／核准及快照；等待寫入收尾後才釋放政策與 session，不因回合取消假裝檔案已回復。 |

本份產品實作承接 LOOP-003～004、010～012、021、037、046、049、057 中「兩工具、唯讀、單一 path、只有讀取結果、沒有核准」的階段限制，以及 WS-043 的唯讀限制；這是增量交接，不改寫舊文件或放寬既有讀取、防洩漏與取消要求。其他既有行為維持。

### 2.2 最小契約形狀

以下為後續 C# 宣告形狀；新增型別均為 `internal`，不公開 SDK、HTTP、Key、原始例外或檔案控制代碼。

```csharp
internal interface IWorkspaceFiles : IAsyncDisposable
{
    IReadSnapshotStore Snapshots { get; }
    Task<WorkspaceListResult> ListFilesAsync(string path, CancellationToken cancellationToken);
    Task<WorkspaceReadResult> ReadFileAsync(string path, CancellationToken cancellationToken);
    Task<WorkspaceWriteResult> CreateFileAsync(
        string path, string content, CancellationToken cancellationToken);
    Task<WorkspaceWriteResult> ReplaceTextAsync(
        string path, string oldText, string newText, CancellationToken cancellationToken);
}

internal interface IFileChangeApproval
{
    ValueTask<FileApprovalDecision> ConfirmAsync(
        FileChangePreview preview, CancellationToken cancellationToken);
}

internal enum FileChangeKind { Create, Replace }
internal enum FileApprovalDecision { Approved, Declined, Cancelled, EndOfInput, Unavailable }
internal enum FileWriteState { NotApplied, Verified, Unconfirmed }
```

| 型別或擴充 | 必要唯讀欄位與語意 |
|---|---|
| FileChangePreview | Kind、RelativePath、long OriginalByteCount、long CandidateByteCount、bool OriginalHasUtf8Bom、bool CandidateHasUtf8Bom、string DisplayBlock、int DisplayByteCount。新檔原始 bytes 為 0、原始 BOM 為 false，Kind 與 /dev/null 標頭區分「不存在」與「空檔」。DisplayBlock 是第 7 節完成安全投影後的整份預覽，包含首次確認提示。 |
| WorkspaceReadSnapshot 增量 | 在原有欄位之外新增 internal ulong OriginalDevice、ulong OriginalIdentity、UnixFileMode OriginalMode，保存成功 read_file 同次安全讀取的檔案身分與完整 mode。IWorkspaceReadFile 新增 UnixFileMode GetMode()，從同一開啟 handle 取得 mode；讀完及發布快照前重驗路徑身分／mode 一致。這些欄位不進 read_file JSON、不改原始 bytes 容量計算，外部仍無發布快照的入口。 |
| 工作區內部候選 | 不可變的 Kind、正規化相對路徑、原始 bytes／基準、候選 bytes、BOM、預覽、服務與 session 所有權、快照世代及操作識別。原始與候選 bytes 防禦性複製，不向模型、一般事件或核准服務公開。只在該次受理中的操作內建立與消費，不提供公開 Prepare／Apply 或可任意組裝候選的入口。 |
| WorkspaceWriteData | Path、ByteCount、HasUtf8Bom，全部描述已發布且核對的完整目標；不含全文、指紋、暫存名稱或核准識別。 |
| WorkspaceWriteResult | Status、ErrorCode、FileWriteState WriteState、可空 WorkspaceWriteData Data、可空 string VerifiedPath、可空 long VerifiedByteCount、可空 bool VerifiedHasUtf8Bom、bool CleanupFailed。只有 Success／None／Verified 有 Data；Verified 的三個本機欄位保留已核對事實，WriteState 不為 Verified 時該三欄為 null。CleanupFailed 獨立表達內部暫存清理失敗。 |
| ToolExecutionResult／LocalToolOutcome | 新增可空 FileWriteState WriteState、VerifiedPath、VerifiedByteCount、VerifiedHasUtf8Bom 及 CleanupFailed。唯讀工具的 WriteState 為 null；寫入即使 Failed、Cancelled 或 Response 為 null，也保留這些安全本機事實。既有 RelativePath／ByteCount 等成功欄位規則不變。 |
| Chat／Agent 擴充 | ChatState 新增 AwaitingApproval；AgentStopReason 新增 ApprovalDeclined、FileConflict、FileWriteFailed、WriteUnconfirmed。CleanupFailed 與核准 I/O 失效使用既有 LocalFailure。不把本機停止塞進 LlmErrorCode 或假造供應商回應。 |

VerifiedPath 等欄位只可來自已通過政策的正規化成功核對資料；Unconfirmed 不帶成功路徑或 bytes，不對未知狀態輸出「未變更」。錯誤／停止訊息不回顯未驗證的請求 path，必要時使用工具名稱與生成／PartIndex 位置識別。

| 編號 | 具體要求 |
|---|---|
| EDIT-006 | 使用上述 internal 契約及明確欄位；含文字／路徑／bytes 的新增型別採密封類別、唯讀屬性、防禦性複製及固定型別標記 ToString，不提供一般序列化或原始內容傾印。原有公開 LLM 契約不變。 |
| EDIT-007 | IWorkspaceFilesFactory.Create(session) 的既有簽章保留；工廠建構時注入同一 ChatApplication 的核准橋接器，建立服務時綁定當前政策。橋接器只在當前活動回合及正確 session 綁定下轉接 IFileChangeApproval，未綁定時 Unavailable、零寫入。WorkspaceFiles 統一驗證、建立候選、呼叫核准、重驗、寫入與核對；Tools 只解析參數及投影結果，核心／工作區不直接依賴 Console。不得提供可略過核准的寫入模式、可選自動同意或外部候選入口。 |
| EDIT-008 | 核准服務只取得不可變預覽，回傳該次 await 的決定；工作區將決定綁定原候選、服務、session、世代及單次操作，結束即消費並釋放。未定義的 enum 值、非取消例外或不合法橋接狀態一律 Unavailable；與當前 token 對應的取消才按 Cancelled。不能把 Approved 保存成可套用其他路徑、內容或下一個呼叫的授權；不使用可由模型提供的 approval_id 或 confirmed 參數。 |
| EDIT-009 | WriteState 描述目標寫入事實，與 Status、回合取消及投影安全性分開。NotApplied 表示本次未開始發布，或僅有 EDIT-050 明確保證未套用的發布失敗，不表示外部程序沒有改檔；Verified 表示發布成功且完成精確讀回；其餘已嘗試發布但無法證明完整核對者為 Unconfirmed。非成功不攜帶候選或部分檔案資料。 |
| EDIT-010 | 工作區同時只受理一個操作，受理範圍包含核准等待及全部收尾；並行第二次 Failed／Unavailable，零 I/O。等待核准不持有會阻擋 Clear／取消的 monitor lock。DisposeAsync 停止新受理、取消核准／發布前工作並等待所有寫入查核及清理完成，最後丟棄候選、快照及政策；重複釋放安全。 |
| EDIT-011 | 原生寫入轉接器提供可注入的建立暫存、寫入／flush／close、狀態及權限檢查、發布與單一路徑清理；具體最小介面與語意見第 10 節。所有操作仍由活動工作區入口驗證，不向 Tools 暴露任意完整路徑寫入服務。 |
| EDIT-012 | Registry 在敏感投影失敗時只移除 Response／不安全的投影資料，不丟失已安全取得的獨立寫入事實；Runner 先記錄本機結果再決定停止，ChatApplication 及 LastTurnOutcome 保留這些欄位。不得將寫入成功顯示為「已讀取」，或將已核對寫入改為未執行／未套用。 |

## 3. 正式工具、Schema 與參數

正式 Definitions 固定依序為 `list_files`、`read_file`、`create_file`、`replace_text`。前兩個定義與 Schema 沿用第 05 份；後兩個描述分別說明「核准後建立工作區內不存在的 UTF-8 檔案，父目錄須存在」與「核准後替換已讀檔案中唯一的精確文字，衝突須重新讀取」。不包含完整路徑、Key、檔案內容或完整應用指示。

### 3.1 create_file

```json
{"type":"object","properties":{"path":{"type":"string"},"content":{"type":"string"}},"required":["path","content"],"additionalProperties":false}
```

```json
{"path":"hello.md","content":"Hello\n"}
```

### 3.2 replace_text

```json
{"type":"object","properties":{"path":{"type":"string"},"old_text":{"type":"string"},"new_text":{"type":"string"}},"required":["path","old_text","new_text"],"additionalProperties":false}
```

```json
{"path":"notes.md","old_text":"草稿","new_text":"正式版"}
```

範例均為預期輸入，不是本次工具執行紀錄。沿用 SDK ParametersJsonSchema，不同時增加 parameters 或自動 function invocation。

| 編號 | 具體要求 |
|---|---|
| EDIT-013 | 每次生成提供同一份不可變四工具 Definitions，固定登錄順序及精確名稱。只有已實作且可用時才更新正式能力提示、/help 及必要的固定應用指示；建構、啟動、help、clear、換 Key 與退出不因此掃描、讀內容、寫檔或額外生成。 |
| EDIT-014 | create_file 恰好接受 path、content 兩個 string 欄位；content 為空合法，缺省不代入空字串。不接受 overwrite、encoding、bom、mkdir、confirmed、approval_id 或其他額外參數。 |
| EDIT-015 | replace_text 恰好接受 path、old_text、new_text 三個 string 欄位；空 new_text 合法，空 old_text 由工作區依第 6 節拒絕。不接受 regex、count、all、expected_hash、外部快照或核准旗標。 |
| EDIT-016 | 各工具列舉全部 JsonElement properties，以 Ordinal 比對名稱及檢查欄位恰好出現一次；拒絕非 object、缺欄、額外欄、null、錯型別、重複及大小寫變體，為 rejected／invalid_arguments，零工作區呼叫。Registry 移除對全部工具一律 TryPath 的假設，仍保留名稱／完整參數敏感檢查；唯讀參數規則不變。 |
| EDIT-017 | 字串傳入工作區時保持原值，不 Trim、Unicode 正規化、URL decode、展開變數或換行轉換。沿用真實 wire 重複欄位在 SDK dictionary 轉換前拒絕、整批結構與敏感預檢、PartIndex／Name／CallId 配對及原始簽章保存；自由文字、預覽及檔案內容均不得解析成操作或核准。 |

## 4. 有限資源與文字判準

| 常數或既有限制 | 值與單位 | 適用方式 |
|---|---:|---|
| MaxFileBytes | 131072 bytes | 原始檔案與完整候選，包含 BOM；核對最多讀取此值加 1 byte。 |
| MaxEditArgumentCodeUnits | 131072 UTF-16 code units | content、old_text、new_text 各自的原始字串上限，先檢查以約束本機處理。 |
| MaxEditArgumentBytes | 131072 UTF-8 bytes | 各編輯文字參數嚴格 UTF-8 編碼後的上限，不把 UTF-16 長度當 bytes。 |
| MaxPreviewBytes | 65536 UTF-8 bytes | 經第 7 節安全投影的完整預覽及首次確認提示；65536 可接受，65537 拒絕。 |
| PreviewContextLines | 3 行 | 替換區段前後各最多三行脈絡。 |
| 暫存檔數 | 1 個／受理中寫入 | 只在核准後建立，不預先產生檔案或落地原始快照。 |
| 路徑／工具結果 | 4096 UTF-16 單位／196608 UTF-8 JSON bytes | 沿用原始相對 path 與完整 Response object 限制。 |
| 快照／完整模型請求 | 20 份、1048576 原始 bytes／262144 UTF-8 JSON bytes | 沿用第 04～05 份；完整 body 包含四份工具定義及歷史。 |
| 回合／模型期限 | 5 次生成、5 個完整歷史回合／每次生成 120 秒 | 核准沒有自動同意或固定逾時；120 秒不是使用者確認期限。 |

| 編號 | 具體要求 |
|---|---|
| EDIT-018 | 上述限制為固定具名常數，不加入設定、環境變數或工具參數放寬。參數 UTF-16、UTF-8、完整候選、預覽及 JSON 上限獨立生效，全部使用實際單位；不得只量測替換片段而漏掉完整檔案。 |
| EDIT-019 | 編輯字串須為合法 Unicode，以 UTF8Encoding(false, true) 嚴格編碼；未配對代理字元為 InvalidUtf8，不用替換字元。沿用 WS 的保守文字判準，拒絕除 Tab／LF／CR 外的 C0、DEL 及 C1 控制字元；原始與候選皆須通過，不依副檔名豁免。U+2028／U+2029 及格式字元可作文字，但預覽必須可辨識地跳脫。 |
| EDIT-020 | 有界組合候選、逐位元組讀回及預覽；預覽超限即停止，不建立無上限 diff 矩陣，不先產生無限字串再檢查。預覽只保存在記憶體，不寫日誌、剪貼簿、額外檔案或操作歷史。 |
| EDIT-021 | 四工具定義及寫入結果照常受實際 SDK body 上限檢查，超限停止，不裁切原始紀錄、工具結果、簽章或更多歷史，不摘要、不補送結尾、不自動重放已寫入工具。核准本身不增加生成或傳輸計數。 |

超過編輯參數 UTF-16／UTF-8 上限為 EditTooLarge，完整候選超過檔案上限為 FileTooLarge；兩者在預覽及核准前拒絕。讀取原始檔案仍遵守第 04 份嚴格 UTF-8、UTF-16／32 BOM 拒絕及完整結果容量規則。

## 5. create_file：建立不存在的檔案

1. 驗證服務、取消、參數、相對 path、敏感規則、固定根及沿途目錄；目標要求為普通檔案位置，不可是 `.` 或尾斜線的目錄要求。
2. 確認父目錄已存在且不是連結，目標項目不存在；不以 File.Exists 為 false 推論不存在，必須分辨目錄、連結、特殊項目及不可存取狀態。
3. 嚴格編碼 content，驗證完整候選、實際 BOM 狀態及敏感資訊，在記憶體建立新增差異。
4. 通過完整預覽及核准後，再重驗路徑與目標不存在性，依第 10 節建立暫存並發布。

| 編號 | 具體要求 |
|---|---|
| EDIT-022 | 父目錄不存在為 NotFound，不建立父目錄；目標已是普通檔案為 AlreadyExists，目錄／特殊種類為 WrongEntryType，連結按 SymbolicLink 拒絕。敏感規則與根失效仍優先，不為分類受限名稱讀內容或透露存在性。 |
| EDIT-023 | 空 content 可建立 0-byte 檔案；不附加換行、不 Trim、不自動排版。編碼器不額外加 BOM；若 content 本身以 U+FEFF 開頭，該字元仍編碼為 EF BB BF，候選 BOM 標記按實際 bytes 為 true，預覽明示而不默默移除。中間 U+FEFF 保留。 |
| EDIT-024 | 目標不存在須在初次、核准後及發布前檢查，發布採不可覆蓋原生操作；若他人在最後檢查後建立目標，仍不得覆蓋。發布目標已存在時為 Conflict、NotApplied，保留他人 bytes，停止本回合，不改用另一個檔名或覆寫 API。 |
| EDIT-025 | 預覽成功不代表已建檔；核准前零磁碟寫入、零暫存檔，拒絕／取消／EOF 亦同。每次建立都要自己的預覽與明確核准，不因同回合曾同意其他操作而跳過。 |

## 6. replace_text：依讀取基準精確替換

1. 驗證全部參數、路徑、政策與文字限制，從目前服務的 Snapshots 取得正規化 path 的基準。
2. 使用基準原始 bytes 嚴格解碼，保留 BOM 資訊；重新安全讀取目前目標，確認完整 bytes 與基準相同。這次內部查核不等於模型的 read_file，不發布新快照或讀取結果。
3. 在去除一份開頭 UTF-8 BOM 的文字內，按 Ordinal 計算 old_text 的全部起始位置，找到第二處即可拒絕；匹配後下一次從起始位置加一尋找，以涵蓋重疊。
4. 唯一匹配時，以原文前段、new_text、原文後段組合候選；保留原來是否有 BOM，不改寫其他 bytes，通過完整候選與預覽限制後才核准。
5. 核准後及發布前重新安全讀取並比較基準，依第 10 節寫入與核對；不以歷史文字、時間或模型參數聲稱「已讀過」取代本機基準。

| 編號 | 具體要求 |
|---|---|
| EDIT-026 | 必須從活動 IWorkspaceFiles.Snapshots.TryGet 取得當前正規化 path 的基準；缺失、淘汰、clear 或跨 session 均為 SnapshotRequired、NotApplied，零預覽／核准／寫入。不得自動替模型補一次 read_file、從歷史重建快照或接受模型提供的指紋。模型須明確提出新的 read_file，再提出替換。 |
| EDIT-027 | 基準及目前檔案都須通過原本的路徑、普通檔案、UTF-8、二進位、大小、敏感與重驗規則；基準內容亦重新檢查當前政策。本份擴充成功 read_file 快照的 OriginalDevice／OriginalIdentity／OriginalMode，從同一 handle 及讀完／發布前重驗取得，不從預覽當下補造；無法完整取得時不發布新快照或讀取成功。讀取不得先把受限內容拿來比對、預覽或顯示；任何失敗無部分結果。 |
| EDIT-028 | old_text 非空且精確匹配恰好一處；空值為 EmptyOldText，零處為 TextNotFound，多處包含重疊為 AmbiguousMatch。例如 aaa 中 aa 有兩處，拒絕。採 Ordinal、區分大小寫、不做 Unicode 或換行正規化，亦不使用 culture、Regex、模糊比對或替換全部。 |
| EDIT-029 | new_text 可為空，只刪除匹配文字，不刪除檔案；即使得到空內容仍保留原檔的 BOM 狀態。舊文字不涵蓋被辨識為 BOM 的開頭標記；中間 U+FEFF 是一般內容。完整候選與原始 bytes 相同則 NoChange，零核准／寫入，不假造一次成功修改。 |
| EDIT-030 | 原始檔案可為空或 BOM-only，但非空 old_text 必然 TextNotFound。唯一匹配可跨行、增減行數、改變該匹配區段的換行或檔尾；未修改部分的 CRLF／LF／CR、空白、Unicode 及有無檔尾換行須逐 byte 保留，不順便排版 JSON／C# 或補換行。 |
| EDIT-031 | 精確匹配、候選大小及 NoChange 都在預覽前判斷；不符合者不提示核准。成功寫入或發布結果未確認後使目標基準失效，不將候選直接當新的 read_file 快照；同批第二次替換同路徑亦須先有新的成功 read_file。 |

嚴格 UTF-8 重新編碼合法 scalar 不改變其編碼 bytes；既有 BOM 單獨保留。若 new_text 在無 BOM 檔案的內容起點引入 U+FEFF，使原本無 BOM 變成實際 EF BB BF 開頭，則以 BomChangeNotAllowed 拒絕，避免違反「既有 BOM 狀態不變」；其他位置的 U+FEFF 可保留。此規則只約束替換的開頭狀態，create_file 的明確 content 仍依 EDIT-023。

## 7. 本機差異預覽與安全格式

### 7.1 差異演算法與完整性

預覽為純文字的 unified diff 風格，只供核對，不是可直接交給 patch 工具執行的檔案。對原始與候選文字分行：CRLF 為一個行尾，其餘 CR、LF 各為行尾；檔尾未換行記為 EOF，空文字為零行，不為末尾換行虛構額外空行。每個行 token 同時包含文字及行尾種類。

替換只產生一個連續 hunk：線性比較前後相同行 token，取得不重疊的最長共同前綴與後綴，剩餘區段全部列為刪除／新增，再保留前後各最多三行共同脈絡。不計算最短 diff 或全檔 LCS；此選擇可能多列幾行，但不能漏掉任何改變。新增檔案全數列為新增行，空新檔以 metadata 及固定「建立空文字檔」標記表示，仍須核准。

標頭使用 `--- 相對路徑（原始）`、`+++ 相對路徑（預計）`；建立檔案的原始標頭為 `--- /dev/null`，這只是固定顯示文字，不是允許讀取工作區外路徑。hunk 使用 `@@ -原始起行,行數 +候選起行,行數 @@`；非空範圍從 1 起算，插入／刪除的空邊以其前方已消耗的行數為起行，因此空檔為 0。

### 7.2 可辨識的字元與大小

每行用一個固定前綴空白／`-`／`+` 表示脈絡／刪除／新增，後接已跳脫內容及固定行尾標記 ` [CRLF]`、` [LF]`、` [CR]`、` [EOF]`。內容中的反斜線顯示為 `\\`、Tab 為 `\t`；方括號分別顯示為 `\u{005B}`／`\u{005D}`，Unicode Format、LineSeparator、ParagraphSeparator 類別的 scalar 以大寫十六進位 `\u{XXXX}` 顯示，補充平面使用足夠位數。其他合法 Unicode 原樣顯示。這可區分原文的字面跳脫、偽行尾標記、BOM／雙向格式字元與真正行尾；不靠色彩表達差異。

路徑標頭以同樣跳脫保護反斜線、方括號與格式字元，並以固定雙引號包住，路徑中的雙引號顯示為 `\"`。實際操作使用原始正規化 path，不使用顯示字串尋找檔案。

| 編號 | 具體要求 |
|---|---|
| EDIT-032 | 差異由本機已驗證的原始與候選 bytes 產生；不能採用模型提供的 diff、直接列印 old_text／new_text 假裝涵蓋全變更，或用模型附帶的完成文字代替核對。列出操作種類、相對目標、原始／候選 bytes 與 BOM，缺省內容或空新檔也須有可辨識預覽。 |
| EDIT-033 | 使用第 7.1 節線性行 token 比較、單一完整 hunk 及三行脈絡；所有刪除／新增行必須完整顯示，保留精確行尾資訊。不隱藏長行、不用省略號裁切修改、不因終端機寬度改變內容；正常終端機自動折行不改變待寫 bytes。 |
| EDIT-034 | 使用第 7.2 節固定、可辨識跳脫；原文不得注入新的狀態行、ANSI、游標操作或假核准提示。整份 DisplayBlock 包含 metadata、標頭、hunk、標記、LF 分隔及首次提示套用此變更？[y/N] 加一個尾端空白，以嚴格 UTF-8 實際計量。此字元投影僅供顯示，不改變候選 bytes。 |
| EDIT-035 | MaxPreviewBytes 包含跳脫膨脹，65536 可接受、65537 為 PreviewTooLarge，NotApplied，零核准／暫存／寫入；要求縮小變更，不提供「略過預覽仍套用」。無法完整顯示或輸出中途失敗為核准 Unavailable，不能沿用部分預覽繼續取得同意。 |
| EDIT-036 | 原始／候選文字、路徑與完整已格式化 DisplayBlock 都須通過當前 Key 檢查才可顯示。若需遮蔽才能顯示，整次拒絕而不提供核准；最後顯示邊界仍沿用安全遮蔽。CLI 不二次改寫已完成的預覽為另一份看似合規內容，不顯示原始 JSON、簽章、內部指紋或完整應用指示。 |

以下為預期操作示意，非本次執行輸出；原文為「草稿」加 CRLF（8 bytes），候選為「正式版」加 CRLF（11 bytes）：

```text
[預覽] 精確替換；目標："notes.md"
原始：8 bytes；UTF-8 BOM：無
候選：11 bytes；UTF-8 BOM：無
--- "notes.md"（原始）
+++ "notes.md"（預計）
@@ -1,1 +1,1 @@
-草稿 [CRLF]
+正式版 [CRLF]
套用此變更？[y/N]
```

實際提示有一個尾端空白供輸入，範例為避免 Markdown 尾端空白而省略。確認文字本身不回顯；CLI 另以固定訊息回報接受或拒絕。行尾標記不是寫入文字。

## 8. 核准輸入、CLI 狀態與停止

### 8.1 輸入權交接

`IFileChangeApproval` 由 ChatApplication 的回合橋接器與 CLI 共同提供。橋接器在同一停止協調下切換 AwaitingApproval、停止並等待目前 busy 讀取者、丟棄其緩衝，然後呼叫互動層確認；既有 IChatInteraction 可新增相同的 ConfirmAsync 方法，由橋接器轉接。工作區不持有 Console，也不擁有互動層。

CLI 輸出完整預覽及首次提示，確認輸出成功後再次清除預覽顯示期間累積的輸入，以共用 CRLF 去重狀態處理殘留換行，再啟用確認讀取者；不得盲目重設標記而讓上一模式的 LF 成為新的空白選擇。這個啟用點之後的按鍵才可形成該次決定；過早輸入可被丟棄，不能排隊成為同意。完成、拒絕或取消後，在仍需回合收尾且程序尚未停止時恢復 busy 觀察；回合結束才恢復一般輸入。每次交接都等待舊讀取者實際結束。

| 編號 | 具體要求 |
|---|---|
| EDIT-037 | 任何時刻只有一個終端機讀取者；核准前後必須依上述流程移交，不與 ObserveBusyInput 並行呼叫既有 Read。普通輸入、先前 CRLF 的 LF、預覽前／顯示期間的 y、上一個核准的殘餘按鍵都不能成為新核准。核准後的額外按鍵亦不得排隊成下一個訊息或下一次同意。 |
| EDIT-038 | 固定提示為套用此變更？[y/N]；單行輸入沿用 16384 UTF-16 上限、Unicode、刪除、CRLF 及隱藏輸入規則。Trim 後 OrdinalIgnoreCase 等於 y／yes 才 Approved；n／no／空白為 Declined。其他值或 Invalid 只顯示固定「請輸入 y／yes 或 n／no。」再提示，不回顯原文、不重新生成或改變候選。每次重問前丟棄殘餘輸入，重新取得該次選擇。 |
| EDIT-039 | Declined 回傳 Rejected／ApprovalDeclined、NotApplied；Runner 立即 Stopped／ApprovalDeclined，本批其餘呼叫記 NotExecuted／turn_stopped，零後續工具與生成。顯示「未套用此變更」，不宣稱其他已完成或外部變更不存在；若本回合完全沒有已發布寫入，可說「本次未寫入」。當前鏈不提交、不續跑；既有成功歷史保留，不要求模型換種寫法再次核准。 |
| EDIT-040 | AwaitingApproval 的 Ctrl+C／SIGINT 與 ExecutingTools 一樣取消本回合，不當作等待一般輸入的程序退出。核准端的 Cancelled 轉為工作區 Cancelled／None、NotApplied；EndOfInput／Ctrl+D 由橋接器先向 ChatApplication 提交正常停止訊號，再以相同工作區取消結果等待收尾，退出 0。程序取消退出 130，輸入／輸出或清理失敗退出 1；核准 Unavailable 映射本機失效，不當作 Declined 或 Approved。 |
| EDIT-041 | 從生成到核准及收尾都不接受第二個回合。確認期間的 /clear、/exit、JSON、檔案文字或「已核准」敘述只是不合法選擇，不執行本機指令或傳模型；支援的中止方式為 Ctrl+C 或 EOF。拒絕與取消不重送提示或自動改提案，無超時自動同意。 |
| EDIT-042 | Approved 只是允許對該候選進入最後重驗，不表示已寫入。返回核准後，在原停止／世代決策點再次檢查取消、程序停止、候選一致及 session；若取消已成立，即使讀到 yes 也不得建立暫存或發布。任何候選改變、衝突或操作結束都消費舊決定，重新提案必須重新預覽。 |
| EDIT-043 | 橋接器於 finally 恢復或結束讀取權及正確 ChatState，不可在工具尚未完成時切回 AwaitingInput。核准 I/O 失敗、恢復 busy 失敗、狀態回呼失敗與寫入期間同時取消，均保留不可恢復本機失效的退出 1 優先權，並保留全部安全寫入事實。 |

### 8.2 核准生命週期

正常路徑為 Generating → ExecutingTools → AwaitingApproval → ExecutingTools → Generating／回合收尾。取消可從任一活動狀態進入 Cancelling；EOF／程序停止進入 Stopping。不把 AwaitingApproval 當可執行 /clear 的一般輸入狀態，也不讓核心事件回呼直接另開模型或工具。

## 9. 基準、衝突與快照世代

| 編號 | 具體要求 |
|---|---|
| EDIT-044 | replace_text 在預覽前、核准後及發布前各安全讀取當下完整 bytes，與同一基準逐 byte 比較，涵蓋 BOM、全部換行及檔尾，同時與快照保存的 Device／Identity／完整 mode 比較。長度、mtime、SHA-256 或歷史文字可供快速排除，不能單獨證明相等；不能只比對 old_text 那一段。查核不發布／刷新快照順序。 |
| EDIT-045 | 沿用路徑檢查，並在各關卡重驗固定根、沿途目錄與普通檔案身分。預覽前已存在的快照過期、預覽後外部修改、消失、替換成另一 inode、權限或目標種類改變，均使候選及該路徑基準失效；對可安全觀察的內容／目標變動為 Conflict，連結／敏感／根失效仍保留其安全分類，停止本回合，不接受舊核准。 |
| EDIT-046 | create_file 的基準是「目標不存在」，不要求 read_file；核准後他人建立目標亦為 Conflict。目標為原本已存在時仍依 AlreadyExists，不把初始一般業務拒絕與候選過期混為一談。內部暫存建立後會改變父目錄長度／mtime，重驗父目錄改採種類、Device、Identity 及連結狀態；不得把自身變動當衝突，也不得因此跳過身分與邊界檢查。 |
| EDIT-047 | /clear、強制 Context 清除、接受新 Key、服務釋放及退出使候選與核准世代失效；發布前活動操作按 Cancelled 收尾，不能遲到補回快照或套用。發布已開始後仍完成第 10 節查核，保留目標事實，不因 Clear 世代改變變成 NotApplied。新 session 建立新服務及核准綁定，不保存跨 session 決定。 |
| EDIT-048 | 成功、Unconfirmed、Conflict、PathChanged 或安全查核失敗均移除目標快照；根失效清空全部。單純拒絕、預覽過大、匹配／參數失敗或發布前取消可保留仍有效的基準，但不刷新／新增。之後每次寫入仍重新驗證，不能因保留快照而沿用核准；大小寫／Unicode 別名不猜測合併，其他索引即使仍在也須通過完整 bytes 重驗。 |

這些前後檢查沿用第 04 份的應用層限制。最後一次比較與 rename 不是原子比較並交換，無法保證抵抗惡意程序在極短時間交換再還原路徑、硬連結別名或掛載變動；不能以有限測試宣稱消除全部 TOCTOU。對已觀察到的衝突必須停止，不以「仍有競態限制」作為忽略可檢測變動的理由。

## 10. 同目錄暫存、發布、讀回與清理

### 10.1 寫入轉接器與順序

保留既有讀取介面，新增下列 internal 能力；以下是轉接器形狀，只有工作區實作可以使用：

```csharp
internal interface IWorkspaceWriteFileSystem : IWorkspaceReadFileSystem
{
    UnixFileMode GetMode(string fullPath);
    IWorkspaceStagedFile CreateStagedFile(string fullPath);
    void PublishNew(string stagedPath, string targetPath);
    void PublishReplacement(string stagedPath, string targetPath);
    void DeleteOwnedStagedFile(string fullPath);
}

internal interface IWorkspaceStagedFile : IAsyncDisposable
{
    Stream Stream { get; }
    WorkspaceNodeState GetState();
    void SetMode(UnixFileMode mode);
    void FlushToDisk();
}
```

GetMode 用於既有目標權限的讀取及重驗；CreateStagedFile 必須是 CreateNew 語意、0600、普通檔案、不可跟隨既有連結，碰撞即受控失敗，不覆寫或自動重試。StagedFile 的 SetMode 在其持有 handle 上操作，FlushToDisk 對應 FileStream.Flush(true)。發布兩方法使用第 10.2 節原生語意，失敗回傳受控例外／分類，不自行重試或 copy fallback。DeleteOwnedStagedFile 只用於本次精確持有的暫存路徑，實際所有權與沿途重驗仍由工作區負責。

一次操作的固定順序：

1. 完成核准後重驗及取消檢查；原始內容與候選仍在記憶體。
2. 產生一個同父目錄 `.aiagent-edit-` 加 Guid 的 32 位小寫 N 格式加 `.tmp` 名稱，驗證路徑／政策並以 0600 排他建立。記住 Device／Identity；不從模型取得名稱，不列舉搜尋可用名稱，不重試碰撞。
3. 以有界候選 bytes 寫入，期間維持 0600；觀察取消時停止，關閉並清理該暫存，不發布。完整寫完並 flush(true) 後進入最後發布準備；新檔保留 0600，替換此時才在自有 handle 上設定已核對的原始目標普通讀寫執行權限位元，再 flush 並關閉。任一階段失敗都不進發布。
4. 經同一安全讀取程序讀回暫存，確認普通檔案、相同 Device／Identity、完整 bytes、BOM、長度及 mode 與候選相符。原文不出現在工具結果，不發布快照。
5. 再次重驗根／沿途／暫存身分、目前目標的完整原始 bytes 或不存在性、目標權限、政策、候選與世代；在同一生命週期鎖的決策點檢查取消並取得發布開始權。
6. 執行一個原生發布操作。發布成功後，以安全有界讀取重新開啟目標，確認它是暫存的預期 Device／Identity、權限及全部候選 bytes；stream 完整關閉後才認定 Verified。
7. 使基準失效，完成必要單一路徑清理並建立本機結果與安全 Response。只有全部成功才為 Success；不得在 rename 前或讀回前回報成功。

### 10.2 macOS 發布及權限

| 編號 | 具體要求 |
|---|---|
| EDIT-049 | 採同目錄暫存後發布，不直接截斷／改寫原檔，不先刪除目標，不跨目錄／跨磁碟暫存，不建立備份、鎖檔或復原指令。核准前驗證拒絕零暫存；核准後驗證失敗則不再建立，若已有暫存就依單一路徑規則清理。核准後至發布前目標不由本工具修改。 |
| EDIT-050 | macOS PublishNew 使用 /usr/lib/libSystem.B.dylib 的 renamex_np，旗標 RENAME_EXCL = 0x00000004；EEXIST 映射 Conflict／NotApplied，即使目標剛出現也不覆蓋。不支援排他 rename、平台／檔案系統不支援或跨裝置為 PublishUnsupported／NotApplied，停止本回合，不退回普通 rename、File.Move 的檢查後覆寫、link/copy 或直接寫入。 |
| EDIT-051 | PublishReplacement 使用同檔案系統原生 rename(staged, target)，不走會跨裝置複製的封裝 fallback。先依第 9 節完成完整重驗；原生錯誤以第 11 節分類。rename 是目標名稱發布步驟，不等於內容 CAS、完整備份、檔案系統交易或斷電持久性保證。 |
| EDIT-052 | 暫存建立及候選寫入期間的普通權限為 0600，建立時即限制，不能先用寬鬆權限再縮小；umask 不得使結果更寬，無法設定／核對時失敗。新檔發布後仍為 0600；替換只在完整寫入與 flush 後的最後發布準備階段，才將自有暫存 handle 設為原 mode 的 0777 讀寫執行位元，隨後關閉、核對並發布，不再寫候選內容。原目標完整 mode 在各重驗點須與快照一致；原檔帶 setuid／setgid／sticky 位元時以 UnsupportedMetadata 拒絕。不自行 chmod 原目標。 |
| EDIT-053 | 暫存可能改變目錄 metadata，重驗依 EDIT-046；核對暫存／目標本身仍須確認種類、身分、長度、mode 及 bytes。置換會改變 inode 與時間，初版不承諾保留原 owner／group、ACL、xattr、resource fork、建立時間或硬連結關係；新 inode 受目前程序與目錄規則影響。文件及 /help 說明此限制，需要完整 metadata 保留的用途不列入本份支援保證。 |
| EDIT-054 | 每個可取消 I/O 及解碼／比較關卡觀察同回合 token；發布開始權與取消／Clear／Dispose 有互斥決策點，取消先成立就不呼叫發布。發布開始後不以已取消 token 跳過讀回與清理，必須使用本機收尾路徑完成有界查核，DisposeAsync 等待它結束。取消不回復檔案，也不把發布後結果改成 NotApplied。 |
| EDIT-055 | 原生發布回報成功，且目標身分、mode、完整 bytes 與候選相同、讀取及關閉成功，才 Verified。發布一旦已嘗試，除 EDIT-050 明確未套用分類外，發布錯誤、讀回不符、路徑改變、核對權限／讀取／close 失敗均為 Unconfirmed，不能用 mtime、部分 bytes 或模型回答補成功。 |
| EDIT-056 | Unconfirmed 立即停止後續工具及生成、使目標基準失效；尚未讀回者做一次安全有界讀取，已失敗的完整讀回不再自動重讀。可取得的內容僅供本機查核，不能推論是哪個程序造成或轉為本次成功，不發布快照／全文。下一個使用者回合須明確 read_file 後才可提出新替換；禁止自動重試發布、重放工具或反向還原。 |
| EDIT-057 | 清理只關閉 handle 並刪除本次持有且重驗身分／沿途安全的單一暫存路徑；發布成功已移走暫存則無須刪除。路徑已被換成不同項目、沿途不安全或刪除失敗時，不刪其他項目，標記 CleanupFailed、LocalFailure／退出 1 並停止；WriteState 與已核對欄位保留。只顯示「內部暫存清理未完成；需人工處理」等安全分類，不輸出暫存內容／名稱，不掃描、批次清理或在下次啟動自動刪除殘留檔。 |

flush(true) 用於清除檔案資料緩衝，不能宣稱已同步父目錄或保證斷電後仍存在。本份沒有 crash recovery；程序被強制終止時可能留下單一暫存檔。單一路徑清理是內部資源收尾，不提供刪除使用者檔案的工具。確認後仍可能因父目錄無寫入權限或檔案系統不支援而失敗；不在預覽前建立試寫檔探測權限。

## 11. 工具結果、錯誤與 Agent 收尾

### 11.1 明確 JSON 投影

成功投影的固定欄位順序為 status、error_code、data；data 為 path、byte_count、has_utf8_bom，與 WorkspaceWriteData 一致。兩個寫入工具共用此格式；工具名稱／呼叫識別仍由原有 functionResponse 外層保存，不重複加入 data。

```json
{"status":"success","error_code":"none","data":{"path":"hello.md","byte_count":6,"has_utf8_bom":false}}
```

此例要求實際目標是 `Hello` 加 LF 的 6 bytes。失敗、拒絕及取消只投影兩欄，不帶 data 或原始請求 path：

```json
{"status":"rejected","error_code":"approval_declined"}
```

```json
{"status":"failed","error_code":"write_unconfirmed"}
```

```json
{"status":"cancelled","error_code":"none"}
```

WriteState、VerifiedPath、CleanupFailed 等本機事實不加入模型 Response。兩欄安全拒絕投影仍可能因短 Key 無法建立；此時 Response 為 null，按既有政策停止／重設，而不是換個 payload、漏掉欄位或把空 object 當成功。

### 11.2 固定碼、狀態與後續行為

WorkspaceErrorCode 新增下表 PascalCase 名稱，ToolErrorCodes 納入對應 snake_case；既有碼不改名。WorkspaceStatus 仍只有 Success、Rejected、Cancelled、Failed。表內「停止」表示不再執行剩餘呼叫、不再生成，當前鏈不提交；一般停止回到輸入，不自行取得新 Key。

| 情境／新增 enum | Status／error_code | WriteState 與後續 |
|---|---|---|
| 完整發布並核對 | Success／none | Verified；可繼續依序工具及模型。 |
| 初次目標已是普通檔案／AlreadyExists | Rejected／already_exists | NotApplied；一般業務拒絕，可在原額度內由模型提出合法新呼叫。 |
| 無活動快照／SnapshotRequired | Rejected／snapshot_required | NotApplied；可由模型明確 read_file 後重新提出，不自行補讀。 |
| 空舊文字／EmptyOldText | Rejected／empty_old_text | NotApplied；一般業務拒絕。 |
| 零處匹配／TextNotFound | Rejected／text_not_found | NotApplied；一般業務拒絕。 |
| 多處含重疊／AmbiguousMatch | Rejected／ambiguous_match | NotApplied；一般業務拒絕。 |
| bytes 完全不變／NoChange | Rejected／no_change | NotApplied；一般業務拒絕，不提示核准。 |
| 編輯參數超限／EditTooLarge | Rejected／edit_too_large | NotApplied；一般業務拒絕。 |
| 完整候選超限 | Rejected／file_too_large | NotApplied；沿用既有碼，一般業務拒絕。 |
| 預覽超限／PreviewTooLarge | Rejected／preview_too_large | NotApplied；一般業務拒絕，不能裁切後同意。 |
| 替換會改變既有 BOM 狀態／BomChangeNotAllowed | Rejected／bom_change_not_allowed | NotApplied；一般業務拒絕。 |
| 原目標有特殊 mode 位元／UnsupportedMetadata | Rejected／unsupported_metadata | NotApplied；一般業務拒絕，不升權或默默丟棄特殊位元。 |
| 使用者拒絕／ApprovalDeclined | Rejected／approval_declined | NotApplied；Stopped／ApprovalDeclined。 |
| 基準／目標變動／Conflict | Rejected／conflict | NotApplied；Stopped／FileConflict，失效基準。 |
| 原生發布不支援／PublishUnsupported | Rejected／publish_unsupported | NotApplied；Stopped／FileWriteFailed，不降級策略。 |
| 發布前權限或 I/O 失敗 | Failed／access_denied 或 io_failure | NotApplied；Failed／FileWriteFailed，停止但不將普通檔案失敗當程序失效。 |
| 已嘗試發布且無法核對／WriteUnconfirmed | Failed／write_unconfirmed | Unconfirmed；Stopped／WriteUnconfirmed，停止並依 EDIT-056 查核。 |
| 暫存清理失敗／CleanupFailure | Failed／cleanup_failure | 保留 NotApplied 或 Unconfirmed；若已 Verified 亦保留核對欄位、Data 為 null。CleanupFailed=true，Failed／LocalFailure，退出 1。 |
| 發布前取消／世代失效 | Cancelled／none | NotApplied，已建立暫存則先清理；回合取消，剩餘未執行。 |
| 核准介面 Unavailable／服務失效 | Failed／unavailable | NotApplied；Failed／LocalFailure，退出 1。若已核對後狀態輸出失敗，本機事實仍保留。 |
| 根失效 | 發布前 Failed／workspace_unavailable；發布後 Failed／write_unconfirmed | 發布前 NotApplied、Stopped／WorkspaceUnavailable；發布後 Unconfirmed、Stopped／WriteUnconfirmed；兩者都清空快照，保留已知事實。 |

未列的路徑、連結、敏感、編碼、二進位及大小失敗沿用既有碼；發布前為 NotApplied。合法候選開始後遇到 PathChanged 或路徑變成連結／受限、根失效時停止鏈，不能沿用舊核准改路徑；純初始參數／業務拒絕則可維持原第 05 份的受控失敗回填。任何發布後同類失敗一律保留 Unconfirmed，不把它轉成未套用。

| 編號 | 具體要求 |
|---|---|
| EDIT-058 | WorkspaceResponseWriter 新增明確寫入結果投影，不使用一般物件序列化。成功只能來自 Verified 且無清理失敗；失敗兩欄，無原文、指紋、暫存資訊、核准或秘密。沿用 196608-byte 實際 JSON 及全部欄位／字串敏感檢查；Data 與本機核對欄位來源一致。 |
| EDIT-059 | enum、固定 snake_case、Status／WriteState 及後續處理依第 11.2 節，不輸出 OS errno／例外原文。型別須驗證合法組合：Success 必為 None＋Verified；Cancelled 必為 None＋NotApplied；CleanupFailed 必為 Failed／CleanupFailure、Data 為 null；Unconfirmed 不得冒充 Success 或攜帶核對資料。新增 AgentStopReason FileWriteFailed 以區分受控寫入失敗與程序失效。 |
| EDIT-060 | 保留整批預檢、依序處理及每個呼叫恰好一筆本機結果。Declined、Conflict、候選後路徑失效、PublishUnsupported、寫入 I/O 失敗、Unconfirmed 或 CleanupFailed 依表停止，本批剩餘記 NotExecuted／turn_stopped；取消則 turn_cancelled。第五次生成仍有工具時，整批 generation_limit，零核准／暫存／寫入，亦不額外生成說明。 |
| EDIT-061 | 即時本機結果先於模型回答／錯誤，Verified 顯示「已寫入並核對」與安全相對路徑及 bytes；Unconfirmed 顯示「寫入結果未確認；停止後續操作」，不說未變更。取消、EOF、模型失敗、最終提交失敗、敏感 Response 或顯示失效均不抹除既有 WriteState。LocalFailure／CleanupFailed 的程序失效優先；Unconfirmed 停止分類不被同時取消蓋成單純取消，其餘回合取消照舊優先，操作事實獨立保留。 |
| EDIT-062 | 成功模型最終回答才提交完整工具回合；拒絕／衝突／未確認／取消不提交當前鏈，也不在下一回合續跑。一般停止保留既有安全歷史；敏感／非法配對等仍共同清除 Context、快照、候選與核准。只保留最近安全 LastTurnOutcome，不加入全文或額外磁碟紀錄；用量仍由 Runner 對每次已返回 LlmResult 各記一次，寫入與核准不自行累加。 |
| EDIT-063 | 後續開發更新最小必要能力提示、/help 及 README，說明四工具、單檔逐次核准、拒絕停止回合、metadata 限制、取消不復原、clear 只清記憶體。此為未來產品交付要求，本次不修改這些檔案或附完整應用指示；模型聲稱成功不作為本機事實。 |

### 11.3 故障點的判定

| 故障點 | 目標寫入事實 | 必要收尾 |
|---|---|---|
| 候選、預覽、核准或最後重驗 | NotApplied | 無暫存或僅清理已建立的一份；拒絕不能顯示成功。 |
| 暫存建立／write／flush／SetMode／close／暫存讀回 | NotApplied | 不開始發布，嘗試安全清理單一路徑；清理失敗另記。 |
| PublishNew 明確 EEXIST／不支援，或發布前已取消 | NotApplied | 不覆蓋、不 fallback，清理單一暫存，保留他人檔案。 |
| 已開始發布後的其他錯誤 | Unconfirmed | 一次安全有界查核，沒有發布重試／復原；不可假定 errno 表示目標完全未變。 |
| 發布成功但目標讀回／核對／close 失敗 | Unconfirmed | 不再重讀、不提交成功，基準失效；需下一回合 read_file。 |
| 發布及讀回成功，後續投影／模型／顯示／清理失敗 | Verified | 保留已核對事實；安全或本機失效仍停止，不重寫、不回復。 |

例如同一回合先核准寫入 A，再拒絕 B，A 的 Verified 紀錄保留；只能說 B 未套用，不能說整個回合檔案皆未變。寫入成功後 Gemini 認證失敗，先在舊政策有效時顯示本機事實與用量，再沿既有流程換 Key；不重送原訊息或工具。

## 12. 敏感資訊與生命週期邊界

| 編號 | 具體要求 |
|---|---|
| EDIT-064 | 沿用當前 Key 的 Ordinal 完整子字串政策；Runner／Registry 在任何工具 I/O 前檢查整批 Name、CallId 及所有 JSON 欄位名稱／字串值。工作區再次檢查 path、old_text、new_text、content、基準與完整候選，寫入文字使用 WriteCandidate，參數使用 ToolArguments；不能只檢查新增片段。 |
| EDIT-065 | 完整候選的前後片段相接、原始 bytes 解碼文字、預覽原始與安全投影區塊、內部暫存名稱及發布前全部實際字串均檢查政策。拼接後才形成 Key 亦阻擋，不將遮蔽後候選寫入或沿用核准；核准後檢查失敗仍零發布，必要暫存按單一路徑清理。 |
| EDIT-066 | 內容中的假指令、假 diff、假確認、路徑或「使用者已同意」都只是資料，不能改變工具登錄、工作區、核准來源或敏感規則。只有目前互動讀取者於啟用點後取得的 y／yes 能同意，模型／測試以外的任意文字來源不能自動回傳 Approved。 |
| EDIT-067 | 工具 Response 與事件輸出維持逐欄及完整區塊檢查，錯誤不帶原文、原始參數、失敗 path、暫存名稱、Key、簽章、SDK／HTTP 傾印或完整應用指示。短 Key 與固定標籤／碼重疊時停止安全回填、必要時重設；不改欄名或繞過政策，但數值用量與獨立寫入事實須保留。 |
| EDIT-068 | Key 仍只由隱藏輸入取得及官方 HTTPS 認證使用，不加入參數／環境／檔案來源。原始基準與候選只在記憶體；唯一額外落地內容是核准後的一份候選暫存及正式目標，不建立備份、日誌或快照匯出。政策只辨識當前 Key，不宣稱掃出所有其他或變形秘密；清除只丟棄自有參考，不承諾覆寫全部 runtime 複本。 |

既有敏感區段、`.env.example` 仍受限、連結／特殊檔案拒絕、4096 單位路徑及不展開變數等規則全部適用於建立與替換。內部暫存只可由本機流程生成，不增加模型可用的 tmp-path 或 cleanup 工具；程式不能把自有原始快照落地成暫存。

## 13. 需求對照與後續產品驗收

### 13.1 方法與上游交接

| 編號 | 具體要求 |
|---|---|
| EDIT-069 | 自動驗收全程離線，使用無認證功能的假 Key、假模型／handler、可控制核准與檔案系統、隔離暫存工作區。真實普通檔案驗證 UTF-8／BOM／mode／發布；權限、I/O 故障、取消與時序使用可控制轉接器，不依真實磁碟故障或小檔案速度碰運氣。測試資料保留，不使用遞迴或批次刪除。 |
| EDIT-070 | 分別驗證 Workspace 寫入核心、Tools／Registry、Runner、真實 SDK 配假 handler、真實 ChatApplication／CLI／工作區整合；斷言核准次數、真實 bytes、mode、生成／傳輸次數、原生發布次數、逐呼叫結果與快照。不以只有假成功 ToolExecutionResult 的測試替代真正寫入入口；報告只列案例 ID、狀態、數值與布林，不傾印秘密或原文。 |
| EDIT-071 | MSTest 採 acceptance-linked 名稱，如 EDITAC007_RejectsOverlappingMatches、EDITAC029_CancellationAfterPublishKeepsVerifiedWrite。既有兩工具／唯讀／單一 path 預期只按本份增量更新，保留其他安全、配對、取消、用量與 .editorconfig 回歸目的，不刪測試或放寬規則取得通過。 |
| EDIT-072 | 後續產品當次分別記錄離線驗收、完整既有回歸、PTY、真人終端機、真實 Gemini 的實際命令、退出碼、測試數與未測範圍。文件檢查、案例表、README 歷史數字與假 handler 成功都不能替代真人／真實服務驗證。 |

| 上游責任 | 本份承接內容與保留界線 |
|---|---|
| 總規格第 12、13、15、16 節 | 建立／精確替換、差異、逐次核准、衝突、故障及資料保護；第三階段完整完成仍須第 07 份。 |
| BOOT-022～026、AC-019 的寫入候選部分 | 工具參數、候選、預覽及寫入前 Key 檢查，假資料安全輸出；不新增認證來源。 |
| GEM-AC-029、WS-AC-034 | 寫入成功後 API 失敗的本機事實、快照缺失／過期、核准與衝突；提交／發布掃描仍交第 07 份。 |
| CLI-038、LOOP-036～040 | 安全顯示、AwaitingApproval、busy 讀取權交接、取消／發布競爭與釋放；保留舊認證交接停止規則。 |
| LOOP-AC-040 | 本份實際驗收寫入、預覽、核准、基準及故障；最終提交／發布檢查不提前宣稱完成。 |

### 13.2 離線產品驗收

以下是後續必須實作及執行的案例，不是本次文件檢查的通過紀錄。每個編號可展開多個測試方法，驗收案例數不等於 MSTest 執行數。

| 案例 | 對應需求 | 情境與預期結果 |
|---|---|---|
| EDIT-AC-001 | EDIT-001～007、013、063、068～072 | 保留技術、設定、公開 LLM 契約；四工具登錄順序與能力一致。建構／啟動／help／clear／換 Key／退出零額外生成、掃描或寫入；未提供後續工具與設定。 |
| EDIT-AC-002 | EDIT-006～012、058～059、067 | internal 契約、防禦性 bytes／集合、固定 ToString；來源改動不影響候選或預覽，結果組合拒絕不合法狀態。原始資料不被一般序列化；Verified 在非成功或 Response=null 時仍保留安全本機事實，唯讀結果無寫入標記。 |
| EDIT-AC-003 | EDIT-013～017、021、070 | 真實 SDK 假 handler 擷取四份 functionDeclarations，名稱、順序、ParametersJsonSchema 與本文件一致；無重複 parameters、自動執行或後續工具。純聊天仍可一次生成、零讀寫。 |
| EDIT-AC-004 | EDIT-014～017、064 | 兩個寫入工具及唯讀工具的缺欄、額外／重複／大小寫欄位、null、數字、陣列、巢狀 object、非 object：invalid_arguments，零工作區呼叫；wire 重複欄位在 SDK 轉換前 InvalidResponse、清除、零 I/O。合法新參數不被 Registry 單一 path 驗證誤拒。 |
| EDIT-AC-005 | EDIT-017～019、022、027、064 | 真實及可控入口驗證空／空白／首尾空白 path、中文、補充平面、字面變數、4096／4097、絕對／反斜線／..／相似前綴／尾斜線／敏感區段；不 Trim 或展開。越界及敏感檔案不讀、不預覽、不寫。 |
| EDIT-AC-006 | EDIT-022～025、032、049～055、058 | 真實建立 ASCII／中文／補充平面／空檔／BOM-only／內部 U+FEFF／無檔尾換行；核准前零暫存，核准後 bytes、BOM、0600、讀回與成功 JSON 正確，沒有附加換行或目錄。 |
| EDIT-AC-007 | EDIT-026～030、018～019 | 無快照、空 old_text、零處、多處、aaa 中 aa 重疊、大小寫／Unicode 不同及 NoChange：碼正確，零預覽／核准／發布；合法唯一匹配才建立候選，空 new_text 合法。 |
| EDIT-AC-008 | EDIT-019、023、027～030、032～034、052～055 | 真實 UTF-8 BOM／無 BOM、混合 CRLF／LF／CR、跨行、增減行數、空結果與有無檔尾換行；未改部分逐 byte 相同，保留 BOM。無 BOM 替換引入開頭 U+FEFF 為 bom_change_not_allowed，原檔不變；中間 U+FEFF 保留。 |
| EDIT-AC-009 | EDIT-018～021、035 | 131072／131073 UTF-16 參數、各參數 UTF-8 bytes 邊界、候選 bytes 含 BOM 的 131072／131073，中文／補充平面按正確單位；超限不進核准，未先無界配置或把參數大小誤當候選大小。 |
| EDIT-AC-010 | EDIT-018～020、033～036 | 安全預覽完整區塊精確 65536／65537 UTF-8 bytes，包含標頭、跳脫、行尾與首次提示；前者可核准、後者拒絕，零裁切或暫存。超長單行、換行膨脹與大量跳脫均有界，不使用平方 diff。 |
| EDIT-AC-011 | EDIT-019、027、064～065 | 非法代理、UTF-16／32 BOM、損壞 UTF-8 原檔、NUL／ESC／DEL／C1、合法 Tab／CR／LF：嚴格判準、無替換字元／降級，錯誤不攜帶片段。原始與完整候選兩側都覆蓋。 |
| EDIT-AC-012 | EDIT-032～036 | 對單一替換與新檔驗證精確 hunk、三行脈絡、行數、空檔、EOF／CR／LF／CRLF、Tab、反斜線、方括號及 Unicode 格式字元的可辨識投影；所有增刪完整，標記不進目標 bytes，無 ANSI／假提示注入。 |
| EDIT-AC-013 | EDIT-037～038、042～043 | 真實互動層配可控 terminal，busy 在核准前停止且實際等待完成；預覽前／顯示期間的 y、舊 CRLF、連續兩個核准與多餘 yes 只用於各自啟用點後輸入，峰值讀取者一個，無排隊核准或讀取者競爭。 |
| EDIT-AC-014 | EDIT-025、037～039、041～042 | y、yes、Y、YES、首尾空白可同意；n、no、大小寫及空白拒絕。其他、超限／非法輸入不回顯、重問同候選、不生成；/clear／/exit 不是核准期間本機指令。只有該次有效決定能進最後重驗。 |
| EDIT-AC-015 | EDIT-008、025、039、060～062 | 第一筆拒絕，剩餘讀／寫全部 NotExecuted、零續接、不提交當前鏈；既有歷史保留。另先成功寫 A 再拒絕 B，A Verified 保留、B NotApplied，不顯示全部檔案未變，也不要求模型換寫法核准。 |
| EDIT-AC-016 | EDIT-007～008、010、037、042、047 | 不同候選、路徑、操作、世代／session 的舊 Approved 不可重用；未綁活動回合／session、非法決定 enum 或核准例外為 Unavailable、零寫入。核准返回 yes 前後可控制 Clear／取消時序，取消先成立即零暫存／發布；外部 bytes 改動不影響已顯示內容。 |
| EDIT-AC-017 | EDIT-026～027、031、044、048 | 真實 clear、快照淘汰、失敗重讀、換 Key、強制重設及釋放後 SnapshotRequired；歷史全文或模型指紋無效。查核不刷新快照；成功寫入後同批第二次替換須新 read_file，不能自行發布候選基準。 |
| EDIT-AC-018 | EDIT-024、027、044～048、060 | 分別在成功 read_file→預覽、預覽→發布兩個區間注入同 bytes 但 inode／完整 mode 變動，快照 metadata 能辨識衝突；另覆蓋同長度／同 mtime 但 bytes 不同、BOM／換行及核准期間外部改動。Conflict、NotApplied，基準失效，剩餘未執行、零續接，外部 bytes 不被覆蓋；快照新增欄位不進模型 JSON。 |
| EDIT-AC-019 | EDIT-022、024、045～046、050 | 初次檔案已存在、父目錄不存在、目標目錄／特殊項目分類正確；最後重驗後他人建檔，真實 PublishNew 排他拒絕，不覆蓋。目標連結與壞連結不因 File.Exists=false 放行，無替代檔名或 mkdir。 |
| EDIT-AC-020 | EDIT-027、044～048、053、064 | 真實與可控根／沿途／目標連結、FIFO／socket、根失效、目標消失或權限／種類改變；靜態受限目標無內容 I/O，候選後變動停止、無舊核准套用；根失效清空快照。保留競態測試的實際邊界。 |
| EDIT-AC-021 | EDIT-011、046、049、052～053 | 暫存確在同一父目錄且只在核准後建立一份；建立／寫候選期間排他 0600，新檔發布後仍 0600，替換只在完整寫入與 flush 後切換為原 0644／0755 再核對發布。自有暫存改變父目錄 mtime 不誤判，父目錄 inode／連結變動仍拒絕；特殊 mode 拒絕，未改原目標 mode 或產生備份。 |
| EDIT-AC-022 | EDIT-011、049～051、055、059 | 真實 macOS 新檔排他發布、替換原生 rename、原生錯誤及不支援／跨裝置替身；零 copy/link／普通覆寫 fallback、零原目標先刪除、一次發布。模式不支援受控停止，不冒稱跨平台或 crash durability。 |
| EDIT-AC-023 | EDIT-010～011、049、052、055～057、059 | 可控制暫存 CreateNew 碰撞、部分 write、flush、SetMode、close、暫存核對失敗：NotApplied，原目標 bytes 不由本工具改動，沒有發布或自動重試；只嘗試清理本次精確暫存。 |
| EDIT-AC-024 | EDIT-044～046、050～051、055～056、059 | 可控制發布錯誤、EEXIST、發布後目標不符／消失／連結／inode 改變、讀取或 close 失敗：明確未套用與 Unconfirmed 分類正確，最多一次安全查核、不補成功、基準失效、零後續工具／生成。 |
| EDIT-AC-025 | EDIT-009～012、055～059、061、067 | 已發布且核對，後續 Response 因短 Key 無法建立或 Registry 再檢查失敗：Response=null、停止／必要重設，Verified、核對欄位與本機結果仍在；不能丟成只有 Status 的結果或顯示「已讀取」。 |
| EDIT-AC-026 | EDIT-010～012、054～057、059、061 | 暫存清理失敗、路徑換成他人項目或沿途不安全：不刪別人項目，CleanupFailed／LocalFailure／退出 1，保留 NotApplied／Unconfirmed／Verified 各自事實。取消同時到達不蓋掉本機失效，無殘留掃描或批次清理。 |
| EDIT-AC-027 | EDIT-037、040～043、049、054、060 | 候選前、預覽顯示、核准等待、yes 返回及建立暫存前取消；零目標發布、必要清理，剩餘未執行，等讀取者結束再回一般輸入。下一回合新 token 可正常核准，不把 Ctrl+C 當等待輸入時退出。 |
| EDIT-AC-028 | EDIT-010、042、047、054、057 | 暫存 write／flush／close、暫存核對及發布開始權前取消或 Dispose／Clear；未取得發布權即零發布，Close／清理完成後才返回／釋放政策，無並行重新開啟或遲到快照。 |
| EDIT-AC-029 | EDIT-009～012、047、054～056、061～062 | 發布已開始後 Ctrl+C、EOF、Clear／Dispose：仍完成有界讀回與清理，Verified 不改成 NotApplied；核對失敗保留 Unconfirmed 的停止分類，不被單純取消掩蓋。回合不因此提交遲到回答，已寫 bytes 不回復。 |
| EDIT-AC-030 | EDIT-037、040、043、054、061 | 核准期間 Ctrl+D／EOF 正常停止 0、程序取消 130、terminal read/write 及 busy 恢復失敗退出 1；部分預覽零核准／發布，停止優先於回一般輸入或換 Key，輸入模式及資源完成收尾。 |
| EDIT-AC-031 | EDIT-005、017、060 | 真實 Runner 第五次回應仍含 create／replace／read，先做敏感與結構驗證，整批 generation_limit，零該批核准／暫存／發布，無第六次生成；先前已寫結果保留。 |
| EDIT-AC-032 | EDIT-003、005、013～017、021、025、058、062、070 | 真實 ChatApplication＋SDK 假 handler＋工作區，create→核准→本機核對→模型回答，以及 read→replace→核准→模型回答；真實 bytes、簽章／parts／ID、functionResponse 順序、完整回合提交、生成／傳輸與用量各記一次。 |
| EDIT-AC-033 | EDIT-005、008、017、026、031、039、060～062 | 同批多檔各自核准、read→replace、成功後再要求同路徑寫入、無 ID／同名不同 PartIndex，全部按序且峰值操作一；不得整批核准、去重或自動重放。第二筆拒絕／衝突保留前筆事實，剩餘未執行。 |
| EDIT-AC-034 | EDIT-009、012、021、054、058、061～062 | Verified 後模型網路／逾時／認證／InputTooLarge 或最終提交取消，仍保留已核對本機結果，不重寫、不提交失敗回合。認證失敗才換 Key，舊政策有效時先顯示結果／用量；新 session 不承接候選、快照、核准。 |
| EDIT-AC-035 | EDIT-005、010、031、047～048、062 | 混合寫入工具回合超過五回合，仍按完整回合裁切；/clear、強制重設、換 Key、退出清除相應記憶體與候選，已寫檔案仍在，程序累計用量保留，不把歷史當最新磁碟。 |
| EDIT-AC-036 | EDIT-017、036、064～065、067～068 | 假當前 Key 位於工具名稱／CallId／未知／額外／重複欄名或值、path／old／new／content、基準、完整候選與跨片段拼接，整批／工作區正確阻擋；敏感值不先進預覽或暫存，不以遮蔽候選沿用核准。 |
| EDIT-AC-037 | EDIT-034～038、064～067 | 短 Key 命中固定預覽文字、確認提示、錯誤碼、結果欄名或安全投影；不能藉改名、漏欄、忽略政策或顯示遮蔽預覽取得核准。固定安全分類仍做最後遮蔽，未知或失敗 path 不回顯。 |
| EDIT-AC-038 | EDIT-017、032～036、041、066～068 | 檔案／模型混入假上級指令、shell、假 diff、假 y／yes／核准 ID、路徑及 ANSI／格式控制字元，不觸發額外讀寫、切根或核准，畫面可辨識，原始簽章及 metadata 不輸出。 |
| EDIT-AC-039 | EDIT-018～021、058、062、070 | 寫入成功／拒絕 Response 的明確欄位、順序、bytes 與敏感檢查，無內部欄位或全文；完整 SDK body 的 262144／262145 邊界包含四工具及真實結果，超限零當次傳輸、已寫事實保留，無裁切或重試。 |
| EDIT-AC-040 | EDIT-006～012、047、054、057、062、068 | 可控制服務釋放、並行第二操作、重複 Dispose、清除與遲到完成：同一工作區僅一操作，停止後無新 I/O／核准；全部收尾後才釋放 session／政策，沒有對話／預覽／快照／備份落地。 |
| EDIT-AC-041 | EDIT-001～005、013、016～017、021、063、069～072 | 完整 01～05 回歸、help、格式及空白檢查通過；只更新本份預告的能力與參數增量預期，不放寬防洩漏、工作區、取消、配對、用量或樣式要求。後續 README 記錄實際結果，不借用歷史數字。 |

### 13.3 PTY、真人與後續交接

| 案例 | 對應需求 | 驗收與證據邊界 |
|---|---|---|
| EDIT-AC-042 | EDIT-003、025、034～043、049、054、061、069～072 | macOS PTY 使用測試 host 注入假模型／可控制寫入與核准，測試 y／yes、拒絕、空白、無效選擇、busy 轉核准、預輸入丟棄、CRLF、Ctrl+C／SIGINT／EOF、發布前後取消及終端機恢復。明確同步故障與時間點，不靠睡眠或檔案速度碰運氣；保留既有隱藏輸入／防洩漏組。 |
| EDIT-AC-043 | EDIT-003、032～043、052～054、061、063、072 | 真人 macOS 終端機檢查差異、行尾標記、長行折行、提示、輸入不回顯、拒絕、取消及下一回合、clear、退出復原；檔案 bytes 為實際核對依據。PTY 或測試 host 不當真人證據，未操作明列未驗證。 |
| EDIT-AC-044 | EDIT-003、005、013、025～026、039、044、058、061～063、068、072 | 使用者自行隱藏輸入 Key 的真實 Gemini 另驗收：自然語言建檔、讀檔後唯一替換、同意／拒絕、BOM／換行保留、外部衝突、取消及寫入後模型狀態。本機 bytes、核准及操作紀錄為依據；模型未觸發的分支只保留離線證據，不以服務成功替代故障注入，不保存真實 Key 或請求。 |
| EDIT-AC-045 | EDIT-002～005、063、068、072 | 第 07 份承接整體限制／成本、完整人工清單、暫存內容及發布產物的憑證掃描與阻擋範圍。本份不安裝 hooks、不修改 Git 排除／索引／歷史，也不宣稱第三階段整體完成。 |

### 13.4 後續產品開發完成後執行的命令

以下命令留待產品實作完成後，從儲存庫根目錄依序執行；build 與 test／run 不並行，本次文件交付不執行：

```sh
dotnet restore tests/AIAgent.Cli.Tests/AIAgent.Cli.Tests.csproj
dotnet build tests/AIAgent.Cli.Tests/AIAgent.Cli.Tests.csproj --no-restore
dotnet test --project tests/AIAgent.Cli.Tests/AIAgent.Cli.Tests.csproj --no-build
dotnet run --project src/AIAgent.Cli/AIAgent.Cli.csproj --no-build -- --help
python3 tests/terminal_smoke.py
dotnet format src/AIAgent.Cli/AIAgent.Cli.csproj --verify-no-changes
git diff --check
```

真人與真實服務另沿用既有啟動方式，不將 Key 放入命令、環境或檔案；驗收寫入應選自行建立的隔離工作區及假內容：

```sh
dotnet run --project src/AIAgent.Cli/AIAgent.Cli.csproj --no-build -- --workspace "/example/isolated-workspace"
```

本份離線產品完成條件為 EDIT-AC-001～041、完整既有回歸與格式檢查；PTY 為 EDIT-AC-042，真人為 EDIT-AC-043，真實服務為 EDIT-AC-044，第 07 份交接為 EDIT-AC-045。各類記錄實際結果及未測範圍，不把未執行的真人或後續驗收加進通過數。

## 14. 本次文件檢查、完成條件與下一步

| 編號 | 具體要求 |
|---|---|
| EDIT-073 | 本次只驗證文件的目的、範圍、流程、責任、宣告、限制、失敗及驗收完整；全部需求與案例可追溯，Schema／JSON、固定碼、狀態組合、取消／發布／清理、核准輸入權及上游交接一致，無待定項目或互相矛盾的成功條件。 |
| EDIT-074 | JSON 範例可解析、Schema 欄位正確、本機連結存在、Markdown 區塊／表格／空白正確，既有追蹤檔案內容及索引不變，只新增指定規格。不執行 restore、build、MSTest、formatter、PTY、真人或 Gemini；將本次實際文件檢查結果與後續產品驗收要求分開回報。 |

本次文件驗收獨立編為 EDIT-AC-046，不計入未來產品通過數：

| 案例 | 對應需求 | 本次文件成功條件 |
|---|---|---|
| EDIT-AC-046 | EDIT-002、069～074 | 需求／案例編號連續且每項需求有對照；四工具、Schema、參數、限制單位、固定碼、獨立寫入事實、拒絕停止及核准／衝突／取消／發布／清理交接一致。JSON、連結、Markdown 及空白檢查通過，沒有待定項目，只新增本檔，既有檔案與索引不變；產品與真人／服務驗收未執行。 |

本次檢查程序：

- 核對總規格第 16 節與 01～05 的責任；確認只展開本份，四工具依序及逐次核准不擴成批次交易，第 07 份保留最終掃描與完整驗收。
- 核對 internal 方法、欄位、狀態／固定碼、JSON 與生命週期：一般拒絕、明確拒絕停止、衝突、未確認寫入及本機失效分開，取消與安全投影不抹除已寫事實。
- 核對需求與案例的完整對照、限制及範例 byte 數；解析全部 JSON 區塊，檢查 required 與 properties、additionalProperties=false、Markdown 連結與區塊配對。
- 執行 git diff --check；本檔尚未追蹤時另執行下列命令。no-index 若因新增差異退出 1，確認無空白診斷，不單靠退出碼判定。

```sh
git diff --no-index --check -- /dev/null specs/06-file-edit-and-approval.md
```

- 核對 git status 只有新增本檔；以 git diff 及 git diff --cached 確認既有追蹤內容與索引沒有新增變更。不執行產品命令，不在文件中寫入未曾執行的通過紀錄。

下一步才依本份開發並記錄當次產品驗收。本份文件不自動啟動開發；第三階段「可以修改」仍需 06～07 的實際整合與驗收證據。多檔交易、復原、完整 metadata 保留、原生競態強化、其他平台、搜尋與其他編輯方式均另訂規格。

## 參考資料

以下官方資料於 2026-10-22 核對，用於本份後續開發的發布與緩衝語意，不代表本次已執行原生寫入或產品測試。模型／SDK／API 維持第 02 份基線，未升級或切換。

- [Apple XNU stdio.h：renamex_np 與 RENAME_EXCL 定義](https://github.com/apple-oss-distributions/xnu/blob/main/bsd/sys/stdio.h)
- [Apple Foundation：檔案系統的排他 rename 支援能力](https://developer.apple.com/documentation/foundation/urlresourcekey/volumesupportsexclusiverenamingkey)
- [Apple rename(2)：同檔案系統名稱置換與錯誤](https://developer.apple.com/library/archive/documentation/System/Conceptual/ManPages_iPhoneOS/man2/rename.2.html)
- [.NET 10 Unix 檔案實作：rename、跨裝置 fallback 與非原子的前置檢查](https://github.com/dotnet/runtime/blob/v10.0.0/src/libraries/System.Private.CoreLib/src/System/IO/FileSystem.Unix.cs)
- [FileStream.Flush(Boolean)：清除檔案資料緩衝](https://learn.microsoft.com/en-us/dotnet/api/system.io.filestream.flush?view=net-10.0)
- [FileStreamOptions.UnixCreateMode：建立時的 Unix 權限](https://learn.microsoft.com/en-us/dotnet/api/system.io.filestreamoptions.unixcreatemode?view=net-10.0)
