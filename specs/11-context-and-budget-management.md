# 11：Context 與預算管理開發規格書

## 第 17 份產品實作接續（2026-10-27～28）

一般與 Review 在規則後加入完整目前技能附錄；Disabled 明示 active=null。技能版本納入 Context／量測／摘要及保存候選，摘要不直接帶技能正文，原 196608／262144 bytes 與唯一預算／用量保持。詳見[第 17 份](17-reusable-workflows-and-skills.md)及[當次分層驗證](../README.md#skills-validation)。歷史文件交付敘述保留，未結原生／真人／真實 Gemini／Release 門檻不變。

## 第 16 份產品實作接續（2026-10-27）

最近已開始發布的復原結果有獨立 RecoveryRevision；所有一般／Review／摘要／保存摘要及估算重建 mandatory 附錄，含安全 ID／相對 path／寫入事實與需重讀來源警示，含包裝最多32768 bytes。復原版本加入 ContextOperationStamp／MetricStamp／compaction／save 候選，過時者拒絕；不改 SDK 原生歷史、不送 raw bytes／anchor、不增加生成或262144-byte實際body上限。 詳見 [第16份](16-change-checkpoints-and-recovery.md) 與 [當次分層證據](../README.md#checkpoint-validation)。下列原階段敘述保留歷史範圍。

## 第 15 份產品實作接續（2026-10-26）

[工作保存與續接](15-work-save-and-resume.md)的限定增量：跨次保存由第 15 份明示擴充。載入摘要使用 saved_work_summary／WorkId／歷史標記，仍只有單一有效摘要；本程序回合從零計。首次 compaction 使用載入背景作 previous_summary 並保留來源，立即再保存無新回合時零生成。原文 pins、新身分、記憶／投影容量與唯一記帳保持；/compact 本身不寫存檔。 本節保留舊階段歷史，實際分層驗證見 [README](../README.md#work-save-validation)。

## 第 14 份產品實作接續（2026-10-26）

[第14份終端機操作改善](14-terminal-interaction.md)只擴充一般ReadInputAsync的隱藏多行／文字元素編輯、F2安全預覽、F3明確貼上、F4換行及程序內安全歷史；Key／認證選單／三種核准仍走原隱藏單行。多行斜線指令在所有解析器之前整筆拒絕，單行精確匹配與Scope／pin字面餘文保持。

CLI輸入狀態加入共同安全清除、認證選單前清除及政策／退出生命週期；不加入Context／摘要／計畫／Git來源或完成證據，沒有新增生成、工具與權限。RULE-HO-006／GIT-HO-006的承接細節見第14份第7、8、9、11、12節與TERM-AC-016～026。原階段歷史保留，當次證據及真人／服務／原生／發布待辦見[README](../README.md#terminal-interaction-validation)。


## 第 13 份產品實作接續（2026-10-26）

[第 13 份 Git 差異與程式碼審查](13-git-diff-and-code-review.md) 新增專用 GitRead 能力與三項唯讀工具，依 Plan／Execute 提供九／十二項；`/review` 為共用 Runner 與生成預算的新入口，該回合只允許三項 Git 工具。原十項工具的順序、輸入與成功資料保持，公開 LLM 契約與 SDK 訊息所有權保持。

Git 來源、覆蓋與報告不建立修改快照、Plan Read requirements 或 Verified／Validation 事實；一般 Git 探索與正式審查分開。僅審目前 Scope 的安全子集；忽略來源的祖先 metadata 例外不放寬一般 `.git`、跨 Scope 讀取或搜尋遇新規則邊界整次拒絕的契約。規則失效、共同重設及已發生操作事實仍依原契約收尾。

實作、當次分層證據與未驗證項目見 [README](../README.md#git-review-validation)。真人、真實 Gemini、第 10 份 test 不支援／未結原生驗收，以及第 07 份 Release／分發門檻分開保留。


## 第 12 份產品實作接續（2026-10-25）

一般請求新增完整當前規則附錄，位於既有應用指示與記憶／計畫附錄之間；摘要來源與請求不直接加入規則全文。規則版本及世代納入操作、候選與量測戳記；生成結果、摘要及成功歷史發布前重驗。規則指令不生成、不重置／退還額度；既有 196608 估算門檻及 262144 bytes 實際 body 上限保持。

實作、當次離線／PTY 證據與未驗證部分見 [README](../README.md#project-rules-validation)。真人 RULE-AC-033、真實 Gemini 034、第 10 份未結原生驗收與第 07 份 Release／分發門檻分開保留。

> 文件版本：0.1｜建立日期：2026-10-24｜狀態：待實作規格
>
> 選題依據：[未來開發大綱 FUT-04](../ROADMAP.md)。上位文件：[開發總規格](../SPEC.md)第 5、7～10、13～15 章；承接 [03：CLI 與 Context](03-cli-and-context.md)、[05：Agent 工具循環](05-agent-loop.md)、[06：檔案編輯與核准](06-file-edit-and-approval.md)、[07：預算與整體驗收](07-budget-and-acceptance.md)、[08：程式碼搜尋與局部讀檔](08-code-search-and-partial-read.md)、[09：Plan 模式與任務清單](09-plan-mode-and-task-list.md)及 [10：受控建置與測試](10-controlled-build-and-test.md)。第 09 份 [FUT-04 固定交接](09-plan-mode-and-task-list.md#fut-04-handoff)及第 10 份第 10、12.3 節是必要交接來源。

## 產品實作接續（2026-10-25）

已依本份接續產品開發；下文「本次只新增本檔」與文件查核案例 CTX-AC-036 保留規格撰寫時的交付紀錄。產品實作與當次驗證見 [README](../README.md#context-budget-validation)。真人 CTX-AC-034、真實 Gemini 035、第 10 份未結原生驗收及第 07 份發布門檻分別列示。

## 1. 目的、交付與能力邊界

讓較長的開發工作在本程序內保留目標、決策與必要約束，能辨識 Context 容量及生成額度，並在資料或預算不足時安全停止。摘要協助延續對話；目前任務、模式、核准及本機操作事實仍由各自的程式來源管理。

本次只新增本檔，不修改程式、測試、設定、套件、其他文件或 Git 索引／歷史；不建立額外計畫、實作、工作樹、hook 或驗收執行器，不提交、推送或發布。以下型別、指令及產品案例均為後續開發要求。需求使用 CTX-xxx，驗收使用 CTX-AC-xxx；FUT-04 是 ROADMAP 項目代號，11 是子規格編號，不能與既有第 04 份工作區規格混用。

| 編號 | 具體要求 |
|---|---|
| CTX-001 | 首版提供程序內摘要、自動及手動整理、原文必留備註、容量資訊、可調回合及程序總生成額度；正常摘要不等於清除對話或重建工作。 |
| CTX-002 | 本次只交付規格書；文件、產品實作、離線、PTY、真人終端機、真實 Gemini 及發布驗收分別記錄，不以範例或文件完成宣稱產品已提供本份能力。 |
| CTX-003 | 沿用 .NET 10、Google.GenAI 1.26.1、MSTest／Microsoft.Testing.Platform、nullable、warnings-as-errors、既有格式與 macOS 首驗平台；不新增 NuGet、產品啟動參數、設定檔欄位、認證來源或 API 端點。 |
| CTX-004 | 不提供建置失敗後自動修正／重試、任意 shell、背景摘要、額外模型／client、token 計數 API、模型容量查詢、快取服務、正式費用或帳單預算。較多生成機會不改變既有失敗即停止規則。 |
| CTX-005 | 不提供 FUT-08 跨次保存／載入、FUT-09 復原、FUT-14 多 Agent 或其他 FUT 能力；沒有摘要檔、備註檔、對話日誌、自動記憶寫入或 SDK 歷史序列化。 |
| CTX-006 | API Key 仍只由隱藏輸入取得並留在記憶體；新增資料、摘要請求、估算、顯示及錯誤均經目前敏感政策。不向文件或終端機傾印完整應用指示、thought、簽章、認證、SDK／HTTP 或例外原文。 |

## 2. 目前基線與模組責任

本份以 HEAD `180bc59` 的乾淨工作區為文件撰寫基線，已讀 ROADMAP、總規格、前置規格、README 及直接相關 Context、Runner、用量、PlanStore、程序預算與 Gemini 邊界。未重新執行產品驗收；README 的既有測試數及日期不是本次測試證據。

目前 ConversationContext 只保存五個完整成功回合，提交後直接淘汰最舊者；AgentRunner 固定每回合五次生成，尚無摘要或程序總生成上限。GeminiTransport 檢查實際完整 SDK body；UsageAccumulator 已區分傳輸、缺值與溢位。PlanStore 獨立保留計畫與證據。第 10 份已有受控 Debug 建置及 Validation v2，但 SDK 10.0.100 的 MTP 與私人 IPC 邊界不相容，產品 test 仍不支援，第 10 階段亦未完整驗收，詳見 [README](../README.md#controlled-build-validation)。

| 擁有者 | 後續實作責任 |
|---|---|
| ChatApplication／CLI | 指令解析、單一讀取者、busy／取消、一般回合與維護操作生命週期；擁有 Context、備註及程序總預算。 |
| ConversationContext／ContextCoordinator | 原始完整回合、摘要及備註快照、候選與原子發布、版本／世代；不呼叫模型或工作區工具。 |
| AgentRunner／GenerationBudget | 正常及摘要生成的共用入口、額度消耗、逐次結果記帳、工具開始前檢查、停止及本機事實回報。 |
| Gemini 轉接層／ILlmContextInspector | 對同一 client 擁有的原生紀錄作本機容量估算及安全摘要來源投影，保持 SDK 所有權、配對與傳輸檢查。 |
| PlanStore／工作區／Processes | 維持原計畫、核准、證據、快照及程序限制；不由摘要重建或替代來源。 |

| 編號 | 相容性要求 |
|---|---|
| CTX-007 | 公開 ILlmClient、LlmRequest、LlmResult、LlmMessage 簽章與序列化禁令不變。新增整合介面均為 internal，SDK 型別只留在 Llm/，不由 Core 解讀或重建原生 Content。 |
| CTX-008 | 七項檔案工具、submit_plan、update_task、run_dotnet_task 的 Schema／JSON 與 Plan 六項／Execute 九項 Definitions 保持；不新增模型可呼叫的摘要、備註或預算工具。 |
| CTX-009 | 預設 Execute、手動 Plan／模式切換、版本確認後等下一則訊息、逐次檔案及程序核准保持。摘要、釘選、增加額度、計畫確認及任務 InProgress 都不能授予操作權限。 |
| CTX-010 | 第 07 份 Release 掃描阻擋及真人／服務／分發待辦、第 10 份 test 不支援及未完成的真實原生案例繼續分列；不放寬私人 IPC 或其他原生限制。 |

## 3. 內部契約與資料所有權

以下是後續實作的最小契約，並非本次新增產品 API。含文字／路徑／集合者使用密封類別、唯讀屬性、防禦性複製及固定型別標記 ToString；使用明確 writer，不提供一般物件序列化出口。

```csharp
internal enum GenerationPurpose { Conversation, Compaction }
internal enum CompactionTrigger { Automatic, Manual }
internal enum ContextMeasureStatus { Measured, Unavailable }

internal interface ILlmContextInspector
{
    ContextRequestMeasure Measure(LlmRequest request);
    ContextTurnProjection ProjectCompletedTurn(
        long turnId, IReadOnlyList<LlmMessage> messages);
}
```

| 型別／資料 | 必要欄位或操作 |
|---|---|
| ContextSnapshot | Revision、PinsRevision、可空 Summary、依序 CompletedTurns／Pins；每個完整回合有本機 TurnId 及原始唯讀 Messages。 |
| ContextSummary | Version=1、ThroughTurnId、CoveredTurnCount、四類字串集合；來源固定標為 model_summary，沒有任務／核准／完成狀態。 |
| ContextPin | 本機 PinId、完整 Text；先加入者先呈現，不按模型相關性排序。 |
| ContextOperationStamp | SessionGeneration、ContextRevision、PinsRevision、PlanGeneration、Mode、可空 PlanId／PlanRevision，以及捕捉時的不可變 PlanSnapshot；供發布核對，不是授權物件。 |
| ContextRequestMeasure | Status、EstimatedJsonBytes；數值是第 8 節計量物件大小，不是實際傳輸長度或 token。失敗沒有猜測值。 |
| ContextTurnProjection | TurnId、UserText、AssistantText、白名單 Tools；僅是已保留完整回合的安全文字投影，不含可續傳協定物件。 |
| ContextCoordinator | Capture、BuildMemoryAppendix、PrepareCompaction、TryPublishCompaction、AddPin、RemovePin、Clear；建立不可變候選或原子決策，不直接 GenerateAsync。 |
| CompactionCandidate | Stamp、被替代回合前綴及其最後 TurnId、候選 Summary、預先驗證的完整記憶投影；不持有核准或可執行操作。 |
| GenerationBudget | TurnLimit、ProgramLimit、ProgramGenerationsStarted、BudgetRevision；BeginConversation、TryBeginGeneration、Snapshot、SetTurnLimit、SetProgramLimit、RestoreDefaults。 |
| GenerationBudgetSnapshot | 設定值、程序已消耗及剩餘、當前一般回合已消耗及剩餘、摘要消耗次數；剩餘下限為零。 |
| LastMaintenanceOutcome | 最近一次手動摘要的安全狀態、生成／傳輸計數及用量，不含來源／模型原文，也不覆蓋一般 LastTurnOutcome。 |

| 編號 | 契約要求 |
|---|---|
| CTX-011 | ChatApplication 擁有單一 ContextCoordinator 及 GenerationBudget；換 client 時預算物件不更換。同一 Runner 的一般／摘要路徑使用同一 client、政策與程序 UsageAccumulator，禁止第二個生成或記帳入口。 |
| CTX-012 | TurnId、PinId 分別以正 long 及 n1、n2 等本機遞增識別產生，僅成功發布才消耗識別；同一 ChatApplication 在 clear／換 Key 後不重用。任何識別、版本或覆蓋回合計數溢位均安全停止，不回繞。 |
| CTX-013 | Context Revision／PinsRevision 初始為 0；成功提交回合或摘要增加 Revision，新增／移除備註同時增加兩者，清除使舊 stamp 失效。失敗／no_change 不增加版本。Plan 的身分／版本／世代繼續由 PlanStore 產生。 |
| CTX-014 | Inspector 只檢視同一 client 所有的合法原生訊息；安全驗證與原 SDK 轉換共用規則。工廠提供目前 client 對應的 inspector，不以建立另一個 client、GenerateAsync 或假傳輸來估算；無 inspector 時回報不可用並停止需估算的流程。 |
| CTX-015 | 模型不能指定 TurnId、PinId、Stamp、摘要涵蓋範圍、預算、模式或本機狀態。候選／用量／容量事件只暴露安全數值及受控欄位，不把原始請求或原生紀錄交給 CLI。 |
| CTX-016 | Context 發布、備註與生命週期變更由同一協調決策路徑序列化；不得在持鎖時等待模型、終端機或工作區 I/O。清除先取消並等待在途自有工作，遲到候選仍須再次核對 stamp。 |

## 4. 本機指令與顯示

| 指令 | 固定行為 |
|---|---|
| `/context` | 顯示原始完整回合數、摘要存在／版本／涵蓋數、備註筆數及 bytes、最近請求容量估算與最近 SDK body 計量；未取得為不可得，版本不同標過時。 |
| `/context show` | 顯示完整摘要四類內容與全部必留備註，明示模型摘要可能遺失資訊、備註原文保留、本機任務請用 /tasks。 |
| `/context pin TEXT` | 新增一筆原文備註，成功才配 PinId；文字相同仍是獨立備註，不語意去重。 |
| `/context unpin ID` | 只移除目前存在的單一 PinId；未知或已移除識別拒絕，不影響其他資料。 |
| `/compact` | 僅整理已有歷史；至少三個原始完整回合才嘗試一次摘要，保留最新兩個。不足時零生成、零版本變更並提示。 |
| `/budget` | 顯示回合／程序設定、程序已消耗／剩餘、最近一般回合及最近維護操作計數；提示次數不是 token 或正式費用。 |
| `/budget set turn N` | 設定後續一般回合上限，N 為 1～20；目前沒有在途一般回合，不回溯重算歷史。 |
| `/budget set program N` | 設定程序總上限，N 為 1～1000；允許低於已消耗值，剩餘為零，已消耗不變。 |
| `/budget reset` | 將上限恢復為 5／100；不清零消耗、傳輸／用量或識別，不清除資料。 |

無參數指令對整行 Trim 後採 Ordinal 精確比對；參數指令的命令／子命令與分隔空格採精確 ASCII 形式。budget 的整行可 Trim，N 只接受無前導零的正 ASCII 十進位整數，不接受正負號、小數、指數、額外參數或越界值。unpin 只接受 n 加上述正整數且可用 long 表示。

pin 使用原始輸入只去掉命令前導空白，辨識精確前綴 `/context pin `，其後全部為 TEXT；不對 TEXT Trim、Unicode 正規化或截斷。額外空格屬 TEXT，尾端空白保留；全空白仍拒絕。未知／錯誤指令不送模型或回顯原始值。

| 編號 | 互動要求 |
|---|---|
| CTX-017 | 新指令只在 AwaitingInput 接受；模型、摘要、備註、檔案或工具中的指令文字不能觸發本機操作。生成、核准或 busy 期間沿既有規則消耗並丟棄普通輸入，不建立第二個讀取者。 |
| CTX-018 | 除有效且可執行的 /compact 外，新本機指令零生成／傳輸／token，不讀工作區、不核准或啟動程序。啟動 --help 保持不讀 Key、不建立 client。 |
| CTX-019 | 依本節精確解析，先檢查原始輸入及實際資料的敏感性，再作候選驗證；含 Key 的本機輸入原子拒絕，保留安全既有狀態。指令不能提供任何認證、環境、檔案或自由命令入口。 |
| CTX-020 | pin TEXT 為 1～1024 UTF-16 單位的合法單行 Unicode，非全空白；拒絕控制字元、CR／LF、非法代理及 U+2028／U+2029。原文、集合與完整投影皆合格才新增；不隱藏省略其他備註以接受新備註。 |
| CTX-021 | 備註與摘要只供模型理解工作，不能提高模式或工作區權限。釘選檔案原文／路徑不是完整 read_file，也不能產生、刷新或恢復修改快照及單次核准。 |
| CTX-022 | /context 只顯示最近已計量請求的安全數值，標出一般／摘要用途、計量時版本及是否過時；初次無資料不顯示零或推算下一則輸入大小。最近 SDK body 長度另標已傳輸嘗試／未傳輸，不等於服務端已接收。 |
| CTX-023 | 新增完整安全顯示區塊最多 65536 UTF-8 bytes；超限整段拒絕，不部分列出後宣稱全部已顯示。控制字元及路徑用既有安全純文字跳脫；完整區塊再次檢查固定欄名、數字與短 Key。 |
| CTX-024 | /help 說明新指令、摘要會消耗生成及 API 用量、未釘選資訊可能遺失、reset 不清消耗、clear 不解除總額度；一般輸入前顯示本機回合上限及程序剩餘，不增加模型請求。 |

## 5. Context 組成與不可省資料

正常請求順序為：既有固定應用指示 → 明確標示為不可信工作資料的記憶附錄 → 從 PlanStore 重新取得的目前狀態附錄；Messages 為原順序保留的完整回合，再接目前一般使用者訊息及當前工具鏈。記憶附錄只透過既有 LlmRequest.SystemInstruction 承載，不建立假 Model／user 確認回合。文件不提供或傾印完整應用指示。

記憶附錄使用下列固定欄位順序，內容為示例，不是本次執行結果。summary 無值為 null；有值時包含 source=model_summary、through_turn_id、covered_turn_count、content（第 6 節格式）。pins 使用加入順序。

```json
{"version":1,"summary":null,"pins":[{"id":"n1","text":"本階段只撰寫規格書。"}]}
```

必留備註的容量另以完整 `{"pins":[...]}` 物件計量。記憶附錄整體最多 32768 UTF-8 JSON bytes，與原有最多 32768 bytes 的計畫投影分別計量，不共用或取代其額度；在 SystemInstruction 內的再跳脫仍納入 SDK body。

| 資料類別 | 來源及保留方式 |
|---|---|
| 安全、工作區與操作限制 | 原程式政策及固定指示，不交由模型摘要決定是否保留或生效。 |
| 目前目標、計畫版本、任務、待辦、受阻原因、完成依據 | PlanStore 最新不可變投影；與歷史摘要重複或矛盾時以本機來源為準，不發布帳本原文。 |
| 必要的使用者自由文字約束 | 使用者透過 pin 明確指定的原文；沒有計畫或尚未結構化的必要目標／決策也應釘選。 |
| 其他目標、決策、未決問題與背景 | 模型摘要加最新兩個以上原始完整回合；標示為有損整理，不保證語意完整或逐字保留。 |
| 過去檔案或程序觀察 | 只代表來源回合當時的觀察，不是目前磁碟、有效快照、核准或新完成事實。 |

| 編號 | 保留與投影要求 |
|---|---|
| CTX-025 | 同時最多一份摘要、五個原始完整成功回合及目前工具鏈；釘選另最多 16 筆。回合與摘要只在程序記憶體，失敗或取消的當前鏈不提交為成功歷史。 |
| CTX-026 | 本份取代第六次提交時直接 Dequeue 的歷史策略；需整理時在新回合首次生成前完成摘要，否則不能進入可能提交第六個原始回合的流程。Commit 仍防禦性拒絕第六份，不靜默淘汰未摘要資料。 |
| CTX-027 | 摘要只替換完整成功回合的最舊前綴，保留回合及其工具配對／簽章原樣續傳；不修改、切開或重建任何仍保留的原生訊息。摘要的 ModelMessage 不提交正常歷史。 |
| CTX-028 | PlanStore 仍是唯一任務事實來源，每次一般生成重新取得投影；模型摘要不能調整 PlanId／Revision／TaskId、狀態、證據種類／識別或核准，也不能復活退役任務。 |
| CTX-029 | 必留保證明確限定為程式政策、PlanStore 的結構化資料及已釘選原文；自由對話由模型有損整理，不宣稱自動辨識全部必要約束。摘要前後均提示這個限制；已釘選資料不得以摘要同義句代替。 |
| CTX-030 | 記憶附錄、備註投影、摘要及計畫投影分別通過容量與完整敏感檢查後才可用；任何一項不能安全保留時停止，不刪必要資料換取容量。模型語句不得作授權或本機證據。 |
| CTX-031 | 摘要後仍有效的既有完整讀檔快照由原擁有者及重驗規則管理；摘要本身不建立、更新或恢復快照。替換仍需原完整讀檔與衝突檢查，過期快照不得用摘要補足。 |
| CTX-032 | 摘要中「已修改／已測試」等語句只作模型敘述；CLI 不把它顯示為 local_operation，Analysis 的 model_assessment 不轉成 Verified 或程序成功。/tasks、已知副作用及未驗證部分仍由本機回報。 |

## 6. 摘要來源、輸出格式與候選驗證

### 6.1 來源投影

來源只包含既有摘要內容及本次被替代的最舊完整回合，按 TurnId 遞增。保留的最新兩個回合及當前使用者訊息不進摘要來源；必留備註與當前計畫另由本機保留，不要求模型重述。來源 envelope 順序固定為 version、previous_summary、turns；previous_summary 為 null 或既有摘要 content。

```json
{"version":1,"previous_summary":null,"turns":[{"turn_id":1,"user_text":"先釐清修改範圍。","assistant_text":"已提出可供核對的分析。","tools":[]}]}
```

UserText 保留來源完整使用者文字；AssistantText 只取該完整回合最終模型訊息中 Thought!=true 的 Text，沿既有可見文字規則及原序串接。中間模型訊息與 arguments 不匯出。一般回答若曾引用程式碼，仍只是歷史文字，不能據此宣稱不存在檔案片段或保證其正確。

tools 依來源工具結果順序，以已登錄工具的明確 writer 取 name、status、error_code；若其原契約可取得，再列 path、byte_count、returned_entries、truncated，以及程序 task、start_state、end_state、exit_code、cleanup_confirmed、validation。可選欄位沒有值即省略，其他欄位不匯出；路徑只能是原受控相對路徑。submit_plan／update_task 只取前三項，不複製其計畫／任務資料。這些資料仍標為過去觀察，不注入 PlanStore。

### 6.2 模型輸出

只接受一個完整 JSON object，允許 JSON 外圍空白但計入原始 bytes；不接受 Markdown fence、前後說明、註解、尾逗號、重複／額外／大小寫變體欄位或 null。欄位順序可不同，發布 writer 固定為 version、goals、decisions、open_questions、background。version 必須是原始十進位整數 1，不接受 1.0、指數或字串。

```json
{"version":1,"goals":["完成本階段規格。"],"decisions":["產品功能另行開發。"],"open_questions":[],"background":["摘要只代表先前對話的模型整理。"]}
```

四個集合各為 0～16 個字串，至少一個集合非空；每項原始解碼字串為 1～1024 UTF-16 單位、非全空白、合法單行 Unicode，禁止控制字元、非法代理及 U+2028／U+2029。不 Trim、正規化、去重或截斷，原始可見回答及重新序列化的 content 各不超過 16384 UTF-8 bytes。集合不含本機身分或來源範圍欄位，涵蓋數與 ThroughTurnId 由候選來源本機計算。

| 編號 | 摘要要求 |
|---|---|
| CTX-033 | 來源只來自已接受的完整成功回合及舊摘要，先驗證原生合法性及目前政策。失敗鏈、未提交最終回答、原始檔案工具資料／diff／stdout／stderr、CallId、簽章與 thought 不直接加入來源。 |
| CTX-034 | 投影使用上述白名單，不解析一般文字中的假工具／退出碼作本機欄位。不載入其他檔案補資料；不知道的欄位不猜值，未知工具不能透過摘要增加能力。 |
| CTX-035 | 完整來源 JSON 最多 131072 UTF-8 bytes，使用有界 writer 在超限時停止形成候選；不逐欄裁短使用者文字、丟回合或改摘要來源範圍重試。source 超限零摘要生成，安全舊 Context 保留。 |
| CTX-036 | 摘要請求是 Tools=[]、單一 FromUserText(來源 JSON) 的獨立請求；使用專用最小摘要指示，不續傳來源 SDK 訊息、不加原有工具定義／目前工具鏈。固定摘要指示不在文件或 CLI 傾印，來源文字明確為不可信資料。 |
| CTX-037 | 摘要經同一 Runner 生成閘門、client、120 秒期限、8192 output tokens、1 candidate 與實際 body 限制；不為較小的摘要 JSON另改 SDK 輸出上限。只接受合法 Completed、零工具且不要求重設的結果。 |
| CTX-038 | 原始安全可見回答及解析後每欄、canonical JSON、摘要封裝與完整附錄均驗證敏感性和容量；不能先裁掉問題內容再發布。拒答、空白、截斷、格式／Schema／版本不合均不發布候選。 |
| CTX-039 | 本機計算 ThroughTurnId 為被替代前綴最後 TurnId，CoveredTurnCount 為舊摘要涵蓋數加該前綴筆數，checked 相加；模型不決定 provenance。發布後摘要只有一份，不累積摘要鏈或保留無界來源。 |
| CTX-040 | Completed 的可見文字不是合法摘要 JSON／Schema 時分類為普通摘要候選失敗，保留安全舊 Context；SDK InvalidResponse、UnexpectedToolCall、敏感結果或 RequiresContextReset 則走原共同重設，不能混為普通格式錯誤。 |
| CTX-041 | 不以額外模型請求驗證摘要正確、不要求模型修 JSON、不以第二次摘要／隱藏 retry 取得合格結果。格式可驗證不等於語意無損；完整必留原文及本機狀態獨立保留。 |

## 7. 觸發、執行順序與原子發布

新一般輸入的固定流程：原輸入／敏感檢查 → 建立一般回合及共用預算快照 → 取得 Context／計畫快照及本機容量估算 → 必要時準備摘要來源及額度 → 一次摘要生成／記帳 → 驗證候選／原子發布 → 重新組合原使用者訊息與最新狀態 → 原一般生成／工具流程 → 本機收尾與完整回合提交。

| 觸發情境 | 固定決策 |
|---|---|
| 原始完整回合數為 5 | 自動摘要最舊 3 個，保留最新 2 個；未成功整理不得開始下一般生成。 |
| 回合數為 3～4，估算達 196608 bytes | 自動摘要最舊前綴至只剩 2 個；來源範圍不由相關性或模型決定。 |
| 回合數為 0～2 | 不自動摘要；即使估算達門檻也只按一般預檢及實際傳輸硬限制處理。/compact 提示無可整理前綴。 |
| 成功自動摘要後估算仍達 196608 bytes | 不再摘要；軟門檻不直接禁止一般請求，其他預檢合格才可嘗試，由實際 SDK body 邊界最後決定。 |
| 一般回合進行中容量不足 | 停止目前鏈，保留本機事實，不中途裁歷史、摘要、補生成或重放；下一則明確指示才開始新回合。 |

| 編號 | 流程要求 |
|---|---|
| CTX-042 | 自動觸發使用大於或等於 196608 的估算，或已有 5 個原始完整回合；只在新一般回合首次生成前檢查並最多執行一次。新使用者訊息原文維持當前輸入，不被摘要或替換成舊指示。 |
| CTX-043 | 自動摘要開始前須同時具備至少 2 次本回合及 2 次程序剩餘機會；預檢不足零摘要生成、零工具並停止。摘要成功消耗一次，後續一般生成仍受同一回合及程序餘額限制，不額外補足原五次。 |
| CTX-044 | /compact 是單次維護操作，至多生成一次，只消耗程序總額度並有自己的單次用量累計；不建立普通使用者回合、不觸發 ProcessTurnBudget.BeginTurn、不重置或覆蓋最近一般結果／已知副作用。 |
| CTX-045 | 發布前在共同決策路徑核對取消、session／清除世代、Context／PinsRevision、目前 Mode、PlanId／Revision 及捕捉時 PlanSnapshot 是否仍為當前快照；任一變更丟棄候選，不改綁新版或重跑。 |
| CTX-046 | 候選先通過摘要、備註、完整記憶附錄、保留回合合法性及容量檢查，才原子替換舊摘要及指定回合前綴。PlanStore、確認、事實、快照與磁碟不在該發布交易中修改；讀者只看到完整舊狀態或完整新狀態。 |
| CTX-047 | 取消／清除先於發布決策時不發布；發布已接受後的遲到取消不回溯摘要，仍停止尚未開始的一般生成。後續一般生成失敗也不撤銷已接受摘要；失敗的當前鏈不提交。 |
| CTX-048 | 自動摘要普通失敗／取消時停止當次一般流程，保留安全舊歷史、備註、計畫及證據，顯示尚未執行原需求；不自動退回未整理請求。手動普通失敗回輸入，使用者日後明確 /compact 或新訊息才可再次嘗試。 |
| CTX-049 | 摘要使用 Generating／Cancelling 及既有 busy 觀察，顯示固定「正在整理對話」用途；不開額外讀取者或核准提示。Ctrl+C 取消該一般回合／維護操作並收尾後回輸入；Ctrl+D／EOF 等待收尾正常退出，輸出／清理失敗退出 1。 |

## 8. 容量計量、顯示與固定上限

ILlmContextInspector.Measure 先共用原 GeminiProtocol 的合法轉換及 Config 組裝，再在 Llm/ 內以目前既有 System.Text.Json 預設選項、compact JSON 序列化本機計量物件 `{"contents":CONTENTS,"config":CONFIG}`，計算 UTF-8 bytes。CONTENTS 包含 SDK 所需的所有原生部分，CONFIG 包含指示、附錄、工具及生成設定；輸出只回傳長度，計量物件不發布、落地或作為實際傳輸 body。

此為固定且可重現的**本機容量估算**，與 SDK 真正的 wire 格式不同，可能高估或低估；196608／262144 只能表達「相對本機請求上限的估算水位」，不得稱模型 Context 已用百分比。首版不使用字數或 bytes 換算 token，也不依模型名稱猜模型容量；模型 token 上限及剩餘容量顯示不可得。

| 資源／擁有者 | 固定值與單位 |
|---|---|
| ConversationContext | 1 份摘要、最多 5 個原始完整成功回合、另加目前工具鏈；摘要保留最新 2 個完整回合。 |
| ContextCoordinator | 自動摘要門檻 196608 估算 UTF-8 JSON bytes；每一般回合最多 1 次自動摘要。 |
| 摘要來源／content | 完整來源最多 131072 bytes；原始可見回答及 canonical content 各最多 16384 UTF-8 bytes。 |
| 摘要字串 | 四類各最多 16 項、每項 1024 UTF-16 單位；至少一項非空。 |
| 必留備註 | 最多 16 筆、各 1024 UTF-16 單位；完整 pins 投影最多 8192 UTF-8 JSON bytes。 |
| 記憶附錄／完整 CLI 區塊 | 各最多 32768 UTF-8 JSON bytes／65536 UTF-8 顯示 bytes。 |
| 原 PlanStore | 20 任務、最多 1 在途、每任務最多 8 requirements、256 筆事實；實際／保守計畫投影各 32768 bytes。 |
| 原模型傳輸 | 實際完整 SDK body 最多 262144 bytes、8192 output tokens、1 candidate、每次 120 秒。 |
| 原受控程序 | 每一般回合 2 次啟動／300 秒；build 120 秒，test 契約 180 秒但目前不支援；收尾與輸出另沿第 10 份。 |

| 編號 | 容量要求 |
|---|---|
| CTX-050 | 本機估算包括完整目前請求，不只算對話文字；SDK 原生部分／簽章、工具定義、所有指示、最新計畫、備註、摘要與當前結果皆納入。摘要請求亦先估算並標用途，估算零生成／傳輸／token。 |
| CTX-051 | 估算採上述固定計量物件與既有序列化選項，不標為實際 wire bytes 或精確 token；估算失敗為不可用，停止該次流程，不傳一份未檢查請求或以 0 代替。無安全投影或原生協定非法時沿共同重設。 |
| CTX-052 | GeminiTransport 的實際完整 SDK body 仍是最終 262144 bytes 硬邊界。262145 為 InputTooLarge、零該次傳輸；若已開始 GenerateAsync 仍消耗機會，不再摘要或補送。最近實際長度與 RequestDispatched 透過內部數值事件記錄，不改公開 LlmResult 或保留 body。 |
| CTX-053 | 摘要、pins、記憶附錄、計畫及 CLI 容量包含 envelope、欄名、識別、數字、跳脫及 Unicode；每個尺度獨立檢查，內層合格不保證附錄／實際 body 合格。所有計數 checked，超限原子拒絕。 |
| CTX-054 | 保留第 09／10 份實際及保守計畫投影、完成識別、最長固定原因／顯示及收尾餘裕；摘要不移除條件或事實，256 筆證據滿時仍 evidence_limit，20 任務／1 在途不因歷史整理而變動。 |
| CTX-055 | 不將生成上限、token、bytes、程序啟動／時間、API quota 或帳單混為同一預算；API 五欄採真實結果原值。未知用量／模型容量明示不可得，不承諾免費額度或精確剩餘費用。 |

## 9. 生成額度、唯一記帳與預警

| 範圍 | 初始值／可調範圍 | 消耗與重設 |
|---|---|---|
| 每一般回合 | 5／1～20 次 GenerateAsync | 自動摘要及一般生成共用；只在接受新的有效一般訊息、建立回合時開始新的回合計數。 |
| ChatApplication 程序生命週期 | 100／1～1000 次 GenerateAsync | 包含一般／自動摘要／手動摘要；clear、安全重設、換 Key、模式／計畫變更及指令皆不清消耗。新 ChatApplication 才重新起算。 |
| 手動摘要維護操作 | 最多 1 次 GenerateAsync | 不使用已結束一般回合的剩餘，也不建立新普通回合；只有自己的單次統計，仍受程序總上限。 |
| 模型單次設定 | 8192 output tokens／120 秒 | 不可透過上述指令修改；程序時間及核准等待不計成生成次數。 |

TryBeginGeneration 必須在同一原子決策內檢查取消／目前生命週期及所屬用途額度，緊接呼叫 GenerateAsync 前消耗。此前的純本機來源、候選、參數、估算或額度拒絕不消耗；已開始方法後本機拒絕／throw／未傳輸亦消耗，不退款。傳輸嘗試仍只由結果 RequestDispatched 判定。

| 編號 | 預算及用量要求 |
|---|---|
| CTX-056 | 回合預設 5、範圍 1～20；程序預設 100、範圍 1～1000。初始值與每次設定完整驗證後原子套用，同值不增加 BudgetRevision；設定變更不修改任何已消耗計數。 |
| CTX-057 | 程序上限可低於已消耗值，remaining=max(0,limit-used)；提高上限只增加未來可用機會。reset 恢復上限，不退款或清零；消耗已超過 100 時 reset 後仍耗盡，達 1000 後本次程序不能再生成。 |
| CTX-058 | 每次 GenerateAsync 都經共用開始閘門且只消耗一次；自動摘要屬同一一般回合。預設 5 次含 1 次摘要時最多再開始 4 次一般生成，不存在第 6 次整理／結尾請求。手動摘要只消耗程序及維護單次額度。 |
| CTX-059 | 一般回應含工具時，先沿原協定／整批敏感及取消預檢，再檢查回合最後額度與程序最後額度，之後才作模式預檢／派送。任一額度已用完則整批零工具／更新／核准／探針／啟動；各呼叫 NotExecuted，回合額度使用 generation_limit，只有程序額度耗盡使用 program_generation_limit；兩者同時以回合原因為先。最後一次合法文字回答仍可完成及提交。 |
| CTX-060 | Runner 在每筆工具開始前再次確認仍有至少 1 次一般後續生成機會、目前世代／政策有效；不預留或保證後續傳輸成功。不足則停止尚未開始呼叫，已開始操作按原規則收尾，已接受本機結果不重播。 |
| CTX-061 | 每個返回 LlmResult 只由 Runner 對程序及當前一般／維護 UsageAccumulator 各 Record 一次；自動摘要用量算入同一回合，手動摘要另顯示維護統計，皆列入程序總計。開始但未返回結果者只保留已消耗次數，不捏造傳輸或缺值。 |
| CTX-062 | RequestDispatched=false 不更新五欄／缺值；true 時成功、失敗、拒答、截斷、敏感及取消均依原 Prompt／Output／Thought／Cached／Total 的非負值、缺值及溢位記帳。Total 不補算，Thought／Cached 不重複加總，不額外請求統計。 |
| CTX-063 | 一般生成剩最後 1 次前顯示固定本機提示；程序 consumed×5≥limit×4 時顯示接近上限，耗盡時顯示需檢視 /budget。每個 BudgetRevision 的接近提醒最多一次，耗盡可於每次被阻擋時說明；不另呼叫模型產生警告。 |
| CTX-064 | 不新增計畫層額度或以 PlanId 綁定消耗；修訂／更換／移除計畫、反覆 /compact、切模式、clear、重設或換 Key 均不能重置程序已消耗。原 ProcessTurnBudget 只在新一般回合開始一次，摘要不重新開始它，也不以增加模型額度增加其兩次／300 秒上限。 |

## 10. 錯誤、停止與完成事實

下列是新增的內部分類／安全顯示碼，並非新模型工具 JSON 或公開 LlmErrorCode。既有工具錯誤、模型錯誤與程序結果保持其擁有者；缺少工具 Response 的原重設規則不變。

| 條件／內部分類 | 固定後續 |
|---|---|
| 指令、整數、備註、識別非法／invalid_context_command、invalid_budget、pin_not_found | 原子拒絕，零生成與狀態變更，保留安全資料。 |
| 備註容量／pins_limit | 拒絕新增，既有原文完整保留；可由使用者逐筆 unpin。 |
| 無可整理回合／nothing_to_compact | /compact 零生成、零版本變更，不是模型錯誤。 |
| 回合／程序額度不足／generation_limit、program_generation_limit | 零下一次生成或工具；一般回合 Stopped，任務按下表受阻。設定／清除不自動續跑。 |
| 原生檢視不可用／context_measure_unavailable | 停止需估算的流程，安全舊狀態保留；原生協定或敏感故障依更嚴格分類。 |
| 來源／記憶容量不足／context_capacity | 零當次摘要生成或發布；一般回合 Stopped，舊 Context 保留，不裁必要資料。 |
| 普通摘要候選不合格／compaction_failed | 有生成照實記帳；一般回合 Stopped 或維護未完成，保留安全舊 Context，不補生成。 |
| 過期候選／context_revision_conflict | 丟棄候選，停止當次流程，不對新版重放；已開始的生成照記。 |
| 取消／turn_cancelled | 取消後等待收尾，舊狀態或已接受新摘要依發布決策點保留。 |
| 真實 SDK body 超限／原 InputTooLarge | 保留已知操作，零該次傳輸，不在鏈內重新摘要或重送；按原模型失敗結果收尾。 |
| 敏感、原生非法、無安全結果／原敏感及協定分類 | 先安全回報事實／用量，再共同重設，保持目前模式；不以一般候選失敗代替。 |
| ProcessUnconfirmed／ProcessCleanupFailed／不可恢復本機故障 | 沿第 10 份鎖定／退出 1，不能用摘要、調額度、clear 或換 Key 繼續。 |

| 編號 | 停止要求 |
|---|---|
| CTX-065 | 新增 internal AgentStopReason.ProgramGenerationLimit、ContextCapacity、ContextCompactionFailed；回合額度仍 GenerationLimit，檢視不可用與候選過期分別歸 ContextCapacity、ContextCompactionFailed。保留原模型 LlmStatus／LlmErrorCode，不假造為成功或擴充公開錯誤。 |
| CTX-066 | GenerationLimit 使尚在途任務 Blocked／generation_limit；程序總額度、ContextCapacity 或普通摘要失敗使用既有 Blocked／resource_limit。取消沿 turn_cancelled；敏感、非法協定及原不可恢復錯誤保持更嚴格處理。沒有計畫時只記本機回合原因，不建立隱含計畫。 |
| CTX-067 | resource_limit 的 CLI 說明改為「資源或生成額度不足；請檢視 /context、/budget 或 /tasks。任務／證據容量不足時需 /clear 後重新規劃。」不能提示 clear 可以重置程序總生成額度；同步重算原保守顯示上界，容量數值不放寬。 |
| CTX-068 | 預算／容量耗盡不是完成。A 已 Verified、後續生成或 B 操作尚未開始時，保留 A 及已接受 Completed，封鎖仍在途任務並顯示剩餘未執行；不說整項完成或全部未變。 |
| CTX-069 | 等下一則明確指示才可繼續；受阻任務重新 update_task in_progress，符合目前版本／模式、重新完整讀檔及逐次核准。調高額度、摘要完成、reset 或切 Execute 都不直接開始模型／工具，不重播先前成功操作。 |
| CTX-070 | 程序普通非成功／拒絕仍依 BUILD-050 停止剩餘工具及生成，即使本回合上限調為 20 也不啟動修正循環。test 及含 test 的 Validation v2 仍 capability_unavailable，不用摘要或預算擴充解除限制。 |
| CTX-071 | 工具整批敏感／協定、取消、額度、模式及實際收尾優先順序保持；Unconfirmed、收尾／輸出本機失敗不被一般額度或摘要原因蓋過。必要安全結果先回報，再按原重設或退出流程處理。 |

## 11. 清除、認證、取消與釋放

| 事件 | 資料及預算處理 |
|---|---|
| 正常摘要發布 | 替換一份摘要及指定最舊前綴；保留備註、計畫／核准／證據／快照與設定／消耗；不改模式。 |
| 普通摘要失敗／發布前取消 | 安全舊 Context／備註／本機資料保持；已消耗生成及可得用量不退款，未開始一般需求不重送。 |
| `/clear`／共同安全重設 | 清除原始歷史、摘要、備註、容量快照、維護結果及原本計畫／核准／證據／讀取快照；保留模式、工作區、Key、模型、設定與程序生成／用量累計。 |
| 接受替換 Key | 先以舊政策回報、完成取消／釋放，再清除上述工作資料；新 client／inspector 不接舊協定或摘要，保留目前模式及程序總設定／消耗／用量。 |
| 切模式／修訂計畫 | 原既有行為持續；Context 不清除，但在途候選 stamp 不符即拒絕。下一般請求用最新狀態附錄。 |
| `/budget reset` | 只恢復上限及更新設定版本，不清除 Context、不減消耗、不重新執行工作。 |
| 退出／Dispose | 先停止新操作並取消、等待所有自有模型／程序／顯示收尾，清除參考，再依原順序釋放工作區、client／inspector、session；結束該 ChatApplication 的預算生命週期。 |

| 編號 | 生命週期要求 |
|---|---|
| CTX-072 | 普通壓縮絕不呼叫共同 ClearConversationState、Workspace.Clear 或 PlanStore.Clear；不能藉此取消自身、丟事實或使先前單次核准重新有效。 |
| CTX-073 | clear、強制重設、換 Key、Dispose 全部撤銷舊摘要候選及 inspector 借用；舊結果即使成功也不能補回摘要／備註／計畫。所有自有在途工作完成必要收尾後才釋放其政策與 client。 |
| CTX-074 | 手動摘要維護結果不覆蓋 LastTurnOutcome；失敗、取消與認證拒絕仍顯示其用途及用量。AuthenticationFailed 沿現有換 Key 流程，舊資料先安全處理，新 session 等待新指示，不重送 /compact 或待執行一般輸入。 |
| CTX-075 | AwaitingInput 的 Ctrl+C／EOF 與既有退出碼保持；摘要 busy 的 Ctrl+C 只取消目前一般／維護操作，正常後可回輸入。I/O、顯示或釋放故障退出 1，不被同時取消降級。 |
| CTX-076 | 已發布新摘要後仍可保留原回合的新輸入至此次一般流程結束；只清理自有暫存，不建立跨次待送訊息。取消／失敗後不背景重試或在換 Key 後自動補送。 |
| CTX-077 | 清除只丟棄可清除的自有參考，不保證覆寫全部 runtime／SDK 複本或終端機捲動內容；沒有檔案刪除或清理。批次／遞迴刪除仍禁止，需要時由使用者人工處理。 |

## 12. 固定交接與相容性遷移

### 12.1 FUT04-HO-001～008 承接

| 交接編號 | 本份章節／需求 | 後續驗收 |
|---|---|---|
| FUT04-HO-001 | 第 3、5、7、11 節；CTX-013～016、028、045～047、073 | CTX-AC-009、010、021 |
| FUT04-HO-002 | 第 4～6 節；CTX-020～021、025、028～032、038～041 | CTX-AC-005～010、014 |
| FUT04-HO-003 | 第 3、5～6、8 節；CTX-007、014、027、031、033～037、050～052 | CTX-AC-011～013、019、020 |
| FUT04-HO-004 | 第 5、7～8 節；CTX-030、042～046、050～055 | CTX-AC-006、015～020、024 |
| FUT04-HO-005 | 第 7、9、11 節；CTX-011、043～044、056～064、073～074 | CTX-AC-016、025～029 |
| FUT04-HO-006 | 第 9～10 節；CTX-059～060、065～071 | CTX-AC-027、030～031 |
| FUT04-HO-007 | 第 7、10～11 節；CTX-040、045～049、071～076 | CTX-AC-021～023、029、033 |
| FUT04-HO-008 | 第 1、8、10、12 節；CTX-004～005、054、064、070、078～080 | CTX-AC-024、031～032 |

### 12.2 受影響契約與後續同步修訂

以下擴充只在後續產品實作生效，本次不修改來源文件或刪除其歷史階段敘述。

| 來源契約 | 本份明示變更及保留責任 |
|---|---|
| SPEC 第 9、10、14 章；CLI-002、008～010 及原 Context 契約 | 加入有界摘要、備註與帶參數的本機指令；完整回合由直接淘汰改為已成功摘要的前綴替換。共同清除仍清全部工作資料。 |
| LOOP-004 及每回合五次／第五次停止契約；BUD-003、010～011、016 | 摘要及可調生成上限屬明示增量；預設仍五次，最後一次停工具改按當前回合／程序上限判定。實際 body、失敗鏈不提交、不自動重試及不在工具鏈中壓縮維持。 |
| BUD-018～025；PLAN-055；BUILD-063 | 摘要開始也計生成次數，唯一 Runner 記帳涵蓋一般與維護操作；API 五欄、缺值／溢位及程序用量跨 clear／換 Key 保留不變。手動 /compact 是新增會生成的本機指令例外。 |
| PLAN-004、050、053～055、070；第 09 份第 12.2 節 | 增加程序內摘要與可調生成預算，保留 PlanStore 事實來源、當次投影、無核准恢復／跨次保存；附錄增加記憶資料，計畫原 Schema 不變。 |
| PLAN-045、047、050、056、062 及相關驗收 | 固定第五次改依設定與程序剩餘判定，增加本份停止原因；resource_limit 說明更新且重算保守 CLI 上界。20／1／256／32768／65536 不放寬。 |
| BUILD-044～045、050、060～063、066、068；第 10 份第 12.3 節 | 本份只擴充模型生成／Context；程序兩次／300 秒及單次期限不調、普通失敗即停、test 不支援與 Unconfirmed 鎖定維持。普通成功後生成沿新額度；程序節錄原文不進摘要來源。 |
| README／ROADMAP／總規格索引與能力說明 | 後續實作同步最小必要指令、設定、摘要風險、限制及真實驗收狀態，不附完整應用指示；本次文件交付不提前修改索引或標示已實作。 |

| 編號 | 交接要求 |
|---|---|
| CTX-078 | 逐項承接上述八項固定交接，保留第 09 份錨點及來源；必要約束的保證範圍依 CTX-029 明示，不以「模型會保留」作無損承諾。 |
| CTX-079 | 後續實作同一變更同步受影響指令／說明、Context 及生成限制預期與來源文件接續註記；保留原安全測試目的，僅依本份明示增量修改預期，不默默放寬第 09／10 份。 |
| CTX-080 | 對 FUT-08 只交接有界摘要、必留原文、來源版本及清除語意；不定存檔格式或恢復核准。對 FUT-14 只保留未來需共用總預算的責任，單程序 GenerationBudget 不宣稱已有多 Agent 額度協調。 |

## 13. 後續產品驗收

### 13.1 方法與層級

| 編號 | 驗收要求 |
|---|---|
| CTX-081 | 後續離線使用合成 Key、假終端機／可控制 client、真實 SDK 配假 HTTP handler、現有工作區及可控制取消／發布點；核對真實方法／傳輸次數、實際 bytes、SDK 配對及本機事實，不連 Gemini。 |
| CTX-082 | MSTest 採 acceptance-linked 名稱，如 CTXAC016_AutoCompactionSharesConversationBudget、CTXAC030_VerifiedWriteSurvivesBudgetStop；保留既有 01～10／Python 回歸，僅更新明示相容性預期，不為本次規格新增空測試或驗收執行器。 |
| CTX-083 | 離線產品完成需 CTX-AC-001～032 及適用既有回歸；PTY 另為 033、真人／真實 Gemini 分別為 034／035。不把本份通過算作第 10 份全部原生驗收或第 07 份發布完成。 |

### 13.2 離線案例

| 案例 | 對應需求 | 必須驗證的結果 |
|---|---|---|
| CTX-AC-001 | CTX-001～011、017～019、024 | 正式組裝預設 Execute／5／100，既有工具／啟動與設定不變，新指令適用兩模式；只有有效 /compact 例外生成，無新端點、client 或認證。 |
| CTX-AC-002 | CTX-017～019、056～057 | 指令大小寫、缺欄／額外參數、1／20／21、1／1000／1001、0／負號／前導零／小數／指數；拒絕零生成與部分變更，reset 不清消耗。 |
| CTX-AC-003 | CTX-012～013、019～021 | pin 原文的前後空白、1023／1024／1025 UTF-16、全空白／控制／代理／Unicode；加入／移除及版本原子，識別跨 clear 不重用，未知／退役 unpin 不影響其他筆。 |
| CTX-AC-004 | CTX-006、019～023、030、038 | 合成短 Key 命中原文、欄名、數字／組合；本機輸入拒絕、模型污染共同重設、完整 CLI 不部分發布；無指示／thought／簽章／SDK 原文傾印。 |
| CTX-AC-005 | CTX-020～021、025、029 | 15→16／第17筆、pins 8191／8192／8193 bytes、中文／跳脫／長識別；原文不裁切，摘要改寫／假 yes 不影響備註、模式或核准。 |
| CTX-AC-006 | CTX-022～023、030、046、053 | 記憶附錄32767／32768／32769、CLI65535／65536／65537 bytes；內層合格但外層超限仍原子拒絕；初次不可得、改版過時、估算與 SDK 實際長度分列。 |
| CTX-AC-007 | CTX-025～027、039、042、046 | 0～2回合不整理、3～4依門檻、5必整理最舊3留2；第6原始提交不可靜默 Dequeue，反覆整理仍一份摘要且涵蓋數正確，失敗回合不列來源。 |
| CTX-AC-008 | CTX-028～030、032、078 | 摘要前後原文 pins、目前目標／版本／待辦／受阻原因／完成依據可追溯；未釘選對話有損提示明確，沒有全約束無損宣稱。 |
| CTX-AC-009 | CTX-009、015、021、028、032、034 | 假模式、假核准、假完成／退出碼／TaskId 都只是資料；ModelAssessment 不變 LocalOperation，任務與證據只從當前 PlanStore。 |
| CTX-AC-010 | CTX-013、028、045～046、073 | 修訂 PlanId／Revision、同版本任務狀態變更、模式／世代及退役任務；舊候選不改綁、不發布或復活事實，下次一般請求用最新投影。 |
| CTX-AC-011 | CTX-007、014、027、033、036 | 真實 SDK 假 handler：只移除完整舊回合，保留多工具 PartIndex／CallId／簽章／原序；摘要以獨立 User source、Tools=[]，沒有假 Model 或孤立結果。 |
| CTX-AC-012 | CTX-021、031、072 | 摘要／pin 舊檔案文字不建立 SnapshotRequired；仍有效快照不因正常摘要清除，搜尋／程序的既有失效與替換前重驗照常，零自我取消。 |
| CTX-AC-013 | CTX-014、033～035、050 | 來源只含指定前綴及可見最終文字／白名單 metadata，排除 thought／簽章／arguments／完整工具內容／stdout；131071／131072／131073 bytes，超限零生成、不切欄重試。 |
| CTX-AC-014 | CTX-029、037～041 | 四欄格式、缺／額外／重複／大小寫／null／fence／非法version、16／17項、1024／1025單位、全空集合、16383／16384／16385原始及canonical bytes；普通候選錯誤與SDK非法／工具呼叫／敏感重設區分。 |
| CTX-AC-015 | CTX-025～026、035、042、048、050 | 本機估算196607／196608／196609、原始回合2／3／4／5；一回合至多一次自動摘要，來源無法保留時不生成、不默默淘汰。 |
| CTX-AC-016 | CTX-037、043、056、058、061、064 | 預設5含摘要後最多4一般生成；回合／程序剩1時零自動摘要、剩2時可摘要後單次一般文字；共用開始閘門及唯一用量，ProcessTurnBudget 不被摘要重設。 |
| CTX-AC-017 | CTX-042、046～048、051～052 | 成功一次摘要後估算仍≥196608不重試；軟門檻不作硬拒絕，合法實際 body 可送，超硬界零傳輸；摘要已發布則後續失敗不回滾。 |
| CTX-AC-018 | CTX-026～027、047～048、052、068 | 進行中工具鏈超限時不整理、丟工具結果或補生成；不提交失敗鏈，先前操作事實保留，下一般回合仍需明確指示。 |
| CTX-AC-019 | CTX-014、050～051、055 | Inspector 完整 contents／config 及附錄估算、零 HTTP／Generate；不透明所有權與非法來源拒絕，沒有模型 token 容量時不可得，不把 bytes 視為 token 或帳單。 |
| CTX-AC-020 | CTX-007、037、050～053、062 | 真實 SDK body262144／262145含工具／計畫／pins／摘要／簽章／目前結果；摘要請求同樣受界線，未傳輸仍占方法機會但不增加 token 缺值，無隱藏第二傳輸。 |
| CTX-AC-021 | CTX-016、039、045～047、073 | 來源／發布各決策點取消、Context／Pin／Plan變更、clear／換Key與遲到結果；完整舊或完整新狀態擇一，計數溢位不發布／不重用身分。 |
| CTX-AC-022 | CTX-040～041、048、065～066、074 | 摘要Empty／Refused／Truncated／Timeout／普通無效JSON／版本錯／一般網路失敗保持安全舊狀態且停止、不fallback／補請求；認證失敗走原換Key且不重送。 |
| CTX-AC-023 | CTX-006、038、040、071、073～075 | 摘要敏感、InvalidResponse／UnexpectedToolCall／RequiresContextReset走共同清除；Plan限制與程序消耗保留，無半份發布，較嚴格故障不被取消或預算蓋過。 |
| CTX-AC-024 | CTX-010、030、054、067、078～080 | 20任務／1在途／256事實及32768計畫投影不放寬，255→256後拒絕新證據；resource_limit新說明的保守顯示含最長原因，接近上限仍可收尾。 |
| CTX-AC-025 | CTX-018、043～044、056～058、064 | /compact不足3回合零生成，有來源最多1維護生成／程序計數；不建立正常回合或重置程序啟動預算，/context／/budget／pin零生成。 |
| CTX-AC-026 | CTX-011、056～058、063～064 | 程序99→100／下一次拒絕、可調至1000及耗盡；調低低於used、提高／reset、計畫／模式／clear／換Key均不退款；80%提示及每BudgetRevision最多一次。 |
| CTX-AC-027 | CTX-058～060、065～066、071 | 可調1／5／20的最後生成、程序最後生成、兩者同時；文字可成功，工具整批零更新／I/O／核准／探針，完整NotExecuted及原因優先，無額外結尾。 |
| CTX-AC-028 | CTX-037、058、061～062 | 摘要成功／失敗／取消／未傳輸／開始後throw，GenerationsStarted與傳輸分開、只記一次；五欄0／缺值／負值／溢位保持，Total不補算、無結果不捏造傳輸。 |
| CTX-AC-029 | CTX-044、061～064、073～074 | 自動摘要用量屬同一一般回合，手動獨立維護／程序小計；LastTurnOutcome不被維護覆蓋，clear／換Key保留設定與程序生成／用量，舊模型紀錄不承接。 |
| CTX-AC-030 | CTX-009、059～060、066～069、071 | A Verified後額度／容量耗盡：A及已接受完成保留，剩餘未執行、任務受阻，調額度／摘要／reset不續跑；新指示重驗及逐次核准，不重寫A。 |
| CTX-AC-031 | CTX-010、031、064、070～072、077 | 20次模型額度也不能越過程序2次／300秒、build失敗即停、test不可用與Unconfirmed鎖定；保持程序前後快照失效及原生限制，無修正循環或批次清理。 |
| CTX-AC-032 | CTX-001～010、015～016、076～083 | 既有01～10／Python適用回歸僅按明示擴充更新；無新設定／套件／端點／保存／多Agent／指示洩漏；新資料釋放及原未結驗收分列。 |

### 13.3 PTY、真人與真實 Gemini

| 案例 | 對應需求 | 驗收方式 |
|---|---|---|
| CTX-AC-033 | CTX-017～024、049、063、071、073～075、081～083 | 離線macOS PTY：新指令、含空白／中文pin、完整安全顯示、用途／用量、額度耗盡提示；手動／自動摘要 busy丟棄、Ctrl+C回輸入、Ctrl+D等待退出、單一讀取者與回顯恢復。 |
| CTX-AC-034 | CTX-020～024、029、049、055、063、075、083 | 真人以合成資料確認中文／Unicode／尾空白備註、模型摘要風險可辨識、估算與實際／API數值分列、調額度不續跑、取消／退出；不以PTY或GUI自動化替代。 |
| CTX-AC-035 | CTX-009、028～032、036～049、058～064、070、074、081～083 | 使用者自行隱藏輸入真實Key，驗手動及五回合／容量觸發的自動摘要，核對原文pin與最新任務、來源配對、同回合生成／用量及耗盡；逐次核准仍需另做，test不支援。只記安全結果，不讀取或保存Key。 |

未來實作才依序執行既有 restore、build、MSTest、Python 敏感掃描測試、--help、format --verify-no-changes、PTY 及 git diff --check，另記本份案例；使用當次環境、基線、實際退出碼／通過／失敗／略過數及未驗證部分。真實程序與第 10 份未完成案例、真人、真實 Gemini、Release 掃描及分發分別驗收。本次不執行這些產品命令、不連 Gemini、不啟動原生探針或程序 fixtures。

## 14. 本次文件查核與完成條件

| 編號 | 文件要求 |
|---|---|
| CTX-084 | 需求及案例唯一連續、每項需求有驗收對照；八項FUT04交接逐列回填章節／需求／驗收，受影響的PLAN／BUILD／BUD及歷史契約、必要約束保證範圍、摘要發布及額度生命週期一致。 |
| CTX-085 | 以不落地只讀腳本解析全部JSON、編號／引用、Markdown區塊／表格、相對連結及來源錨點；完整檢查未追蹤新文件及no-index空白，確認本次只新增本檔，其他檔案與Git索引不變。 |
| CTX-086 | 只回報實際文件查核；版本0.1／待實作與交付邊界正確，未執行產品驗收明示未執行，不新增空殼測試／執行紀錄或自動啟動產品開發。 |

| 案例 | 對應需求 | 本次文件成功條件 |
|---|---|---|
| CTX-AC-036 | CTX-002、078～080、084～086 | 本文件需求／案例、JSON、來源與附錄、觸發／限值／記帳、停止／發布／清除、交接及相容性一致；全部本機連結與空白查核通過，只有本檔新增，Git索引未變，零產品實作／驗收。 |

文件查核使用不寫入新腳本或快取的只讀解析及以下 Git 命令；未追蹤文件不能只靠一般 diff 判斷：

```sh
git diff --check
git diff --no-index --check -- /dev/null specs/11-context-and-budget-management.md
git status --short
git diff --name-only
git diff --cached --name-only
```

no-index 退出碼須區分差異存在與空白診斷，不單憑非零宣告文件失敗。完成後交接後續開發者先讀本份、兩個前置交接來源及當時原始碼，再依本份明示擴充實作；不把本規格當成已交付產品或解除其他階段門檻的證據。

## 參考資料

- [ROADMAP：FUT-04與相依關係](../ROADMAP.md)：Context／預算、程序內管理、與長任務及FUT-08／14的分工。
- [第09份：FUT-04固定交接](09-plan-mode-and-task-list.md#fut-04-handoff)：本機來源、必留資訊、協定、容量、記帳、耗盡、清除及後續責任。
- [第10份：受控建置與測試](10-controlled-build-and-test.md)：固定程序預算、停止、test不支援及未完成驗收。
- [Google：Function calling](https://ai.google.dev/gemini-api/docs/generate-content/function-calling)：原生function call／response配對及簽章須保留；本份不改用其他API。
- [Google：Counting tokens](https://ai.google.dev/api/tokens)：token計數是另一種模型計量；本份不呼叫該端點，也不將本機JSON bytes或API用量冒稱模型剩餘Context／正式帳單。
