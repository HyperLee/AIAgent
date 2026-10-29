# 21：唯讀多 Agent 分工開發規格書

> 文件版本：0.1｜建立日期：2026-10-29｜文件狀態：待實作
>
> 選題依據：[ROADMAP：FUT-14](../ROADMAP.md#fut-14多-agent-分工)。上位文件：[總規格](../SPEC.md)第 5～10、13～15 章。FUT-14 是項目代號，21 是子規格編號。本次僅交付本檔；以下工具、指令、內部介面及產品驗收均為後續開發要求。

## 1. 目的、方案與交付邊界

將可獨立探索或審查的工作交給至多兩個唯讀子 Agent，讓父 Agent 取得有來源、有狀態及有限成本的結果，再於主對話彙整。子 Agent 是本機程式管理的獨立模型工具循環，沒有直接存取終端機、認證、任務帳本或任意資源的能力。

使用者已選定「同模型唯讀並行」「明確啟用後自動分派」及「用途限定認證轉接」。依序分派雖較容易沿用單一 client，但不提供並行收益；子任務各自選模則擴大認證、目的地及生命週期，首版不採用。共享寫入、平行實作與分散式執行仍依 ROADMAP 排除，不因這是最後一個 FUT 而併入。

| 編號 | 要求 |
|---|---|
| MULTI-001 | 本次只新增本檔，不修改產品、測試、套件、設定、README、ROADMAP、SPEC、既有子規格、Git 索引或歷史；不另建 memo、實作計畫、工作樹、hook 或驗收轉錄檔，不提交、推送或發布。 |
| MULTI-002 | 文件查核基線為乾淨 `main@7bad214ac1ce32c69d5572d21ae617582321b678`。目前 FUT-01～13 的產品增量已接入；第 20 份 A、B 已有離線紀錄，不能沿用其「撰寫當時尚無多模型」歷史敘述重新設計現況，也不能把歷史通過數當本次證據。 |
| MULTI-003 | 首版每個父一般回合最多接受一批，每批一至兩個子任務，委派深度恰為一；同一批子模型請求可並行，父模型等待整批收尾後才彙整。不存在常駐子 Agent、跨回合續聊、巢狀委派或自動重派。 |
| MULTI-004 | 全批固定使用分派時父目前的同一 Provider／Model。三個既有 profile 均須支援，不新增模型、目的地、NuGet、啟動參數、設定欄位或認證來源；維持 .NET 10、既有 SDK／MCP 版本、nullable、warnings-as-errors 及 macOS 首驗平台。 |
| MULTI-005 | 排除子寫檔、一般 shell、build／test、套件安裝、MCP／網頁、保存／載入工作、checkpoint、Plan 更新、模型切換、讀鍵、核准及其他 Agent 間直接通訊。Git 唯讀原生 recipe 仍由原用途限定執行器管理，並非一般程序權限。 |
| MULTI-006 | 第 07 份 BUD-S003 Release／人工分發、第 10 份 MTP test 不支援與原生待驗，以及各份真人／真實服務驗收保持分列。規格補齊不代表 FUT-14 已實作、其他 FUT 全部驗收或整體產品可發布。 |

## 2. 現況、必讀來源與責任

目前 AgentRunner 以自身在途旗標防止重入；每次 RunAsync 呼叫 GenerationBudget.BeginConversation。兩個模型轉接器各自只接受一個在途請求，原生 owner 綁 client instance。UsageAccumulator 的集合與累計並非並行記帳介面。WorkspaceOperationCoordinator 採單一排他租約，GitSnapshotCoordinator 同時持有來源及可變的 pending／delivered 覆蓋。因此不能直接對現有 Runner 呼叫兩次 Task.WhenAll、共用審查協調器，或為每子建立一份獨立總預算。

開發前依序閱讀：

1. [ROADMAP](../ROADMAP.md)、[README 目前模型基線](../README.md#model-validation)、[總規格](../SPEC.md)，核對當時分支、工作區及分層驗收狀態。
2. 第 [01](01-bootstrap-and-key.md)、[02](02-gemini-client.md)、[03](03-cli-and-context.md)、[05](05-agent-loop.md)、[07](07-budget-and-acceptance.md) 份的認證、原生所有權、工具配對、取消與唯一記帳；第 [04](04-workspace-and-read-tools.md)、[06](06-file-edit-and-approval.md)、[08](08-code-search-and-partial-read.md)、[10](10-controlled-build-and-test.md) 份的工作區、快照及原生隔離。
3. 第 [09](09-plan-mode-and-task-list.md) 份第 3、7～12 節，尤其 [FUT04-HO-001～008](09-plan-mode-and-task-list.md#fut-04-handoff)；第 [11](11-context-and-budget-management.md) 份 CTX-007、011～016、050～080。既有任務與證據不能由子結果重建。
4. 第 12～20 份的九筆原始 FUT-14 交接，見[上游承接表](#upstream-handoffs)。本份摘要不能取代原條文。
5. 當時 ChatApplication／ChatRuntimeFactory、BootstrapSession／SessionSecrets、AgentRunner／AgentContext、GenerationBudget／UsageAccumulator、PlanStore／AgentExecutionPolicy、WorkspaceOperationCoordinator、GitSnapshotCoordinator／CodeReviewCoordinator 與兩個模型轉接器。只讀必要來源，不把完整應用指示、Key、原生紀錄或例外原文附入文件及報告。

| 擁有者 | 本份限定責任 |
|---|---|
| ChatApplication／DelegationCoordinator | 啟停、父回合／批次身分、批次准入、事件排序、狀態與取消；終端機仍由父唯一互動器管理。 |
| AgentRunner／共同生成閘門 | 父子共用開始決策、原生配對、額度／用途、工具預檢、結果與用量記帳；不是第二套平行聊天入口。 |
| Llm／Security 的 DelegationRuntimeFactory | 借用目前認證建立目的限定子 client／inspector；每子獨立 owner，子循環不取得 BootstrapSession 或 SessionSecrets。 |
| DelegationReadSession／工作區及 Git 原擁有者 | 子工具子集、Scope 收窄、共用排他租約、公平排隊、獨立觀察／覆蓋及結果重驗。 |
| PlanStore／Context／Rules／Skills／MCP／Work／Checkpoint | 繼續擁有原狀態、來源、核准、證據、保存及復原；只提供必要不可變投影或有效性檢查，不讓子任務修改。 |

| 編號 | 要求 |
|---|---|
| MULTI-007 | FUT-03 任務狀態與 FUT-04 共用總預算是必要能力；目前 Rules／CLI／模型生命週期亦須整合。FUT-06 僅在 review 角色使用時需要可用 GitRead；MCP／網頁、保存與復原不是子任務的必要能力，不要求合併開發或補完所有前項真人驗收才開始。 |
| MULTI-008 | 不以現有 Runner／client 的重入限制已存在，推論其可支援多個子循環。必須抽出或重用共同生成與工具安全路徑，讓子執行附屬於同一父回合，不呼叫會重置父回合的頂層 RunAsync 入口。 |
| MULTI-009 | 公開 ILlmClient、LlmRequest、LlmResult、LlmMessage、LlmUsage 簽章及序列化禁令不變；新增協調型別均為 internal，SDK／原生 JSON 封裝留在 Llm/。SAVE v1、既有工具參數及成功結果形狀保持。 |
| MULTI-010 | 新資料使用密封、唯讀、防禦性複製及固定 ToString 標記；透過明確 writer 投影。不一般序列化 client、認證 lease、政策、原生內容、來源 bytes、核准或整個協調器。 |

## 3. 最小內部契約與身分

以下為後續實作契約，並非本次新增產品 API；方法所在型別與主要輸入輸出固定，既有型別的低階實作依原擁有者調整。

```csharp
internal enum DelegationMode { Disabled, Enabled }
internal enum DelegationRole { Explore, Review }
internal enum DelegationTaskState { Pending, Running, Completed, Failed, Cancelled, TimedOut }
internal enum DelegationBatchStatus { Completed, Partial, Failed, Cancelled, TimedOut }

// DelegationCoordinator
Task<DelegationBatchResult> RunBatchAsync(
    DelegationBatchRequest request, CancellationToken cancellationToken);
// DelegationRuntimeFactory；只返回能力，不移交認證物件。
Task<DelegationRuntime> CreateAsync(
    DelegationChildSpec spec, CancellationToken cancellationToken);
// 同一 GenerationBudget，預留不代表已開始生成。
bool TryReserveDelegation(
    long parentTurnId, int childCount, CancellationToken cancellationToken,
    out DelegationBudgetLease? lease);
```

| 型別 | 必要資料／語意 |
|---|---|
| DelegationBatchRequest／DelegationChildSpec | 本機 ParentTurnId、依序子定義；每子 Role、Objective、ScopePath、Acceptance。批次、子身分及配額由本機接受時加入，不接受模型指定。 |
| DelegationStamp | ParentTurnId、BatchId、AgentId、DelegationRevision、ModelSessionGeneration，以及平坦的 Context／pins／Rules／Skills／MCP／Recovery 版本、Mode、PlanId／Revision／execution generation／EvidenceEpoch、實體根身分、父／子 Scope。只含安全唯讀欄位；真正父 ContextOperationStamp 由 trusted coordinator 保留，不把其 ContextSnapshot／原生 Messages 交給子。 |
| DelegationRuntime | 每子專用 ILlmClient、同 client inspector、唯讀工具 facade、子訊息及結果候選；實作 IAsyncDisposable，不擁有父 session／Key／共同工作區。 |
| DelegationBudgetLease | 同一父回合之每子配額與彙整預留、已開始次數及剩餘；只透過共同閘門開始，不公開可轉用的認證或工具權限。 |
| DelegationSourceLedger | 每子已安全交給模型的工作區來源及 Git delivery 範圍；與父修改快照、Plan facts、另一子及父 Review ledger 分開。 |
| DelegationChildResult／DelegationBatchResult | 本機身分、角色、狀態／固定原因、模型分析、來源／覆蓋、安全用量、開始次數及是否部分完成；不可帶可續傳原生訊息。 |
| DelegationEvent | 本機遞增 Sequence、ParentTurnId／BatchId／AgentId、Queued／Started／ToolCompleted／Ended 與安全數值；不含任務或工具原文。 |

| 編號 | 要求 |
|---|---|
| MULTI-011 | ParentTurnId 使用同一 ChatApplication 的正 long 回合身分；新批次與子身分分別為 d1、a1 等本機正 long 遞增值，同程序 clear／換 Key／換模型不重用。溢位安全停止，不回繞。AgentId 與 Plan TaskId 不是同一身分。 |
| MULTI-012 | 只有批次完成純本機准入並接受時配置身分及消耗該父回合唯一批次名額；參數、Scope、模式或額度拒絕不配置身分／消耗名額，但仍按 MULTI-030 結束該父工具階段，不能以未消耗名額為由同回合重派。接受後即使 client 建立失敗、取消或零傳輸也不能同回合再派。 |
| MULTI-013 | 委派修訂與原有 model／Context／Plan／Rules／Skills／MCP／Recovery 戳記共同檢查，不能設第二個不同步的安全世代。子結果、量測、配額與事件均綁父回合及子身分；未知、終結或過時身分不能發布。 |
| MULTI-014 | Pending 只能進 Running 或安全終結；Running 只能進一個終態。狀態終結後不重開。Completed 表示子模型合法結束且結果／來源已核對，或 MULTI-064 的本機零差異分支已核對；後者零子生成，不得說已經模型審查。兩者皆不表示父任務完成、語意正確或測試通過。 |
| MULTI-015 | 所有原子決策、預算及帳本更新共用父協調鎖或同一序列化決策入口；持鎖時不等待模型、終端機、檔案或程序 I/O。並行工作回報後仍重驗，不因先檢查過就跳過發布閘門。 |
| MULTI-016 | 僅保留最近一批的有界安全狀態／報告供 /agents 查詢；下一批接受時替換，失效時清除正文，只保留可安全呈現的停止原因。子原生歷史及來源 bytes 在批次收尾後釋放，不形成跨回合子 Context。 |

## 4. CLI、啟用與輸出

| 本機指令 | 行為 |
|---|---|
| `/agents` | 顯示 Enabled／Disabled、目前 profile／Scope、最多兩子、共同剩餘額度，以及最近一批各子終態／原因／來源覆蓋與用量；沒有批次時明示無資料。 |
| `/agents on` | 在目前可用 session／Rules／Skills 狀態明確啟用；完整顯示同模型目的地、唯讀工具、認證轉接、共用成本、清除時停用及 Ctrl+C 取消整批。命令本身即為啟用意圖，不追加 y／yes、不生成或自動派任務。 |
| `/agents off` | 停用後續分派，增加 DelegationRevision，保留安全最近結果作歷史；不變更父目前模式、模型、Plan 核准、檔案或已消耗額度。 |

| 編號 | 要求 |
|---|---|
| MULTI-017 | 僅 AwaitingInput 接受上述整行 Trim 後 Ordinal 精確指令；/agents on extra、大小寫變體及多行斜線輸入拒絕，不傳模型。指令不進一般輸入歷史，不回顯非法原文；既有單行與敏感閘門先執行。 |
| MULTI-018 | 啟動、--resume 匯入後及新認證／profile 預設 Disabled。/clear、共同安全重設、Scope 改變、Rules／Skills 來源失效、MCP 版本失效或復原改變來源時也停用；單純成功的一般回合或 compaction 不停用。停用不退款、不自動再次啟用。 |
| MULTI-019 | on 的完整安全說明無法顯示、來源無效或 runtime 能力不可用時不啟用；同值 on／off 不增加修訂。on 不更改 /budget；必須提示預設五次通常只容一子，兩子批次在分派時至少需要五次剩餘額度。 |
| MULTI-020 | 整個批次沿父 busy 觀察器，普通輸入消耗後丟棄；/agents off 等字串在 busy 期間不另外解析。Ctrl+C 取消父回合及全批，EOF 終止後收尾退出；不提供單子互動取消命令或第二個讀取者。 |
| MULTI-021 | 父互動器集中投影事件，以本機 Sequence 排序、標示 BatchId／AgentId／角色／階段；不同子真實完成次序可不同，但最終結果按 tasks 原順序。子不能直接 Show、寫 stdout／stderr、讀鍵或打斷既有核准流程。 |
| MULTI-022 | 新顯示完整區塊最多 65536 UTF-8 bytes，使用既有純文字／路徑跳脫及目前敏感政策。進度不印 query、檔案正文、任務全文、認證、模型原文或 raw Git；完整報告只在安全結果區塊呈現。/help 與 --help 後續同步說明，--help 仍零設定／工作區／Key／網路讀取。 |

## 5. 父工具與子工具子集

### 5.1 delegate_readonly

新增能力 `ToolCapability.DelegationRead`，只有已啟用的父一般 Plan／Execute 回合可使用；不是 WorkspaceRead／GitRead／ExternalRead，也不是 Plan task operation。正式 Review、Compaction、WorkSaveSummary 與全部子 runtime 不登錄、不接受直接呼叫。

工具參數 Schema：

```json
{"type":"object","additionalProperties":false,"required":["tasks"],"properties":{"tasks":{"type":"array","minItems":1,"maxItems":2,"items":{"type":"object","additionalProperties":false,"required":["role","objective","scope_path","acceptance"],"properties":{"role":{"type":"string","enum":["explore","review"]},"objective":{"type":"string"},"scope_path":{"type":"string"},"acceptance":{"type":"string"}}}}}}
```

合成參數範例；假設父 Scope 是工作區根，並不代表實際檔案存在或此次已呼叫：

```json
{"tasks":[{"role":"explore","objective":"定位取消流程及相關測試。","scope_path":"src","acceptance":"列出已觀察來源與仍未確認的問題。"},{"role":"review","objective":"檢查目前差異的取消與收尾問題。","scope_path":"tests","acceptance":"只報告可對應已提供差異行的問題。"}]}
```

| 編號 | 要求 |
|---|---|
| MULTI-023 | 根恰含 tasks；一至兩項，每項恰含上列四字串，重複／額外欄位、null、未知角色及型別錯誤整批 invalid_arguments。objective／acceptance 各為 1～2048 UTF-16 單位、單行合法 Unicode、非全空白，原文不 Trim／正規化／截斷，控制字元與非法代理拒絕；Scope 上限沿既有 4096 單位。 |
| MULTI-024 | 完整 tasks 參數 UTF-8 JSON 最多 16384 bytes，採既有明確 JSON escaping 計量；Schema 無法表達的字元／bytes 條件由本機補驗。整批敏感預檢先於讀取、身分配置、排程及 client 建立；任一子含敏感內容拒絕全部。 |
| MULTI-025 | scope_path 是工作區相對目錄，根以 `.` 表示；不接受絕對路徑、越界、連結、敏感名稱、私人存檔或未載入 AGENTS 邊界。可沿原路徑正規化規則接受合法字面路徑，接受後固定 canonical 值；子工具所有 path 再以此子根限縮，不能只在提案時檢查一次。 |
| MULTI-026 | 子根必須等於或位於父目前 Rules Scope 內；子規則沿用目前已驗證祖先鏈，子根有新 AGENTS 時拒絕，而非自動載入／變更 Scope。目錄身分在排隊後、每次使用及發布時重驗；一個子不能借另一子 Scope 讀取。 |
| MULTI-027 | delegate_readonly 必須是該 native tool batch 唯一呼叫。混合其他父工具或同批重複時，整批開始前拒絕，委派回 delegation_batch_invalid、其他呼叫 NotExecuted／turn_stopped，零該批工具／子 client／子請求；仍維持全部原生呼叫與結果配對，不先執行排在前面的合法寫入。 |
| MULTI-028 | 同父回合已接受過批次時回 delegation_limit，不重新派送。所有委派准入拒絕只可回安全工具結果並依 MULTI-030 結束；不能改寫參數、改派較少子、換另一工具批次或安全重設在同回合重取機會。安全重設直接結束原回合。 |
| MULTI-029 | 啟用只授權首版受限能力，無計畫時仍可唯讀分派，不強迫建 Plan；有計畫時不改其核准／狀態。父既有 InProgress 至回合結束仍按原規則保留／受阻，子成功不自動滿足 Read、Write、Validation 或 Analysis 完成條件。 |
| MULTI-030 | 父提出 delegate_readonly 後，不論整個 native batch 預檢拒絕、純本機准入拒絕或接受後完成，該父工具階段均結束；另記此階段已關閉，不與「已接受批次名額」混為同一旗標。安全結果完整回填且仍有生成額度時，最多一次 tools=[] 的說明／彙整；沒有餘額則只本機說明停止原因。取消、敏感、非法原生、來源失效或致命故障依第 10 節直接停止，不生成結尾。彙整照常計費，不再執行同回合其他工具／第二批，之後需新使用者訊息。 |

### 5.2 子工具與登錄數

| 角色／模式 | 恰可使用的工具 |
|---|---|
| Explore 子 | list_files、read_file、search_files、search_text、read_file_range；各自原 Schema／結果及忽略規則保持。 |
| Review 子 | git_status、git_diff、read_git_file_range；固定 selection=all，只審子 Scope 安全子集，來源側別及報告依第 13 份。 |
| 父 Review | 原三個 Git 工具，沒有 delegate_readonly，即使 Enabled 也不增加。 |

| 父登錄／模式 | Disabled：MCP 斷線／Ready | Enabled：MCP 斷線／Ready |
|---|---|---|
| 全登錄 | 13／16 | 14／17 |
| Plan | 9／12 | 10／13 |
| Execute | 12／15 | 13／16 |
| Review | 3／3 | 3／3 |

| 編號 | 要求 |
|---|---|
| MULTI-031 | 新工具只追加在原登錄末端，不改既有相對順序；當次 definitions 綁 Enabled／DelegationRevision。舊快照、停用後直接入口、子內偽造工具名、隱藏 Schema 或角色自報皆不能繞過 capability 與實際 facade。 |
| MULTI-032 | Explore 的 read_file 可安全完整讀取，但不寫入父 IReadSnapshotStore、PlanStore 或 checkpoint；子搜尋／範圍操作也不失效或刷新父基準。不得直接借用帶父快照／證據副作用的工具實例；抽出共用安全讀取後各自使用獨立觀察空間。 |
| MULTI-033 | Review 不借用父或另一子的可變 GitSnapshotCoordinator／CodeReviewCoordinator。共享來源只准許不可變 bytes／manifest；delivery、選取、待發布報告與原生訊息均每子獨立，子 A 已傳來源不能抵子 B 的 finding 來源。子工具限制使用局部 capability overlay，不呼叫父 EnterReview／SetMode 或改寫父 ReviewTurnActive。 |
| MULTI-034 | 子角色的所有實際讀取、Git 入口、模型呼叫及結果發布仍檢查當前父世代、根／Scope、Rules／Skills 及敏感政策；沒有 UI 限制即可省略程式防線的分支。被禁止能力回 mode_restricted／unknown_tool 並停止該子，不啟動任何核准或替代能力。 |

## 6. 認證、原生協定與移交 Context

| 編號 | 要求 |
|---|---|
| MULTI-035 | 明示擴充第 20 份單一活動 client 限制：父 client 保留但等待，至多兩個短生命週期子 client 同時執行，全部固定同一 profile／官方目的地。每個 client 仍最多一個在途請求，不將同 client 設成可重入，也不自動換模型或備援。 |
| MULTI-036 | 新用途限定認證轉接器留在 Llm／Security，借用目前 session 的認證與 model lease，建立已綁 profile／owner 的子能力；Core 子執行器只取得 ILlmClient／inspector，不取得 BootstrapSession、SessionSecrets、Key span／字串或可轉送憑證的 factory。 |
| MULTI-037 | 不把父 Key 複製進子任務、Context、工具、事件、結果、環境、參數、存檔或日誌。供應商 SDK／HTTP 認證所需副本只留原認證轉接／傳輸層，不另建應用級 Key 字串、子 SessionSecrets 或認證來源；子 disposal 不 Dispose 父認證。安全說明不宣稱可覆寫所有 SDK／runtime 複本。 |
| MULTI-038 | 每子建立新原生 owner、新訊息鏈及同 client inspector；不複用父／兄弟原生 LlmMessage、簽章、call id 或 results pairing。僅子自己的完整工具配對可在該子續傳；結果到父時轉為安全分析 JSON，不移交原生回合。 |
| MULTI-039 | 子移交包含明確 objective／acceptance、由程式加入的本回合一般使用者訊息完整原文、全部原文 pins、完整目前規則、唯讀 Plan 投影、必備復原附錄及來源有效的 active skill 快照。不能只由父模型挑選／摘要必要約束。父更早完整對話、原生歷史、工具正文、Review raw、舊摘要與存檔原文不自動複製；必要額外背景只能由父寫入該次受限 objective／acceptance，不增加自由原生匯入。 |
| MULTI-040 | 子固定指示由程式重建，不從父請求擷取或回報；文件與終端機不傾印完整指示。規則／技能維持來源與優先順序，技能只以該次不可變快照指導子唯讀工作，沒有獨立啟停或自動引用資源；不能反向更改父技能。 |
| MULTI-041 | 子請求不直接引用父 Coordinator 的可變歷史。不可省的 pins／Rules／Plan／Recovery／Skills 與包裝皆納入 inspector、實際 body 及敏感檢查；放不下即 context_capacity，不能裁必要約束換取分派。子不自動或手動 compact，不建立摘要鏈。 |
| MULTI-042 | 所有子 runtime 必須在父 session／政策／Key 釋放前取消、等待及釋放。建立第二子失敗時先收尾已建資源；能確認收尾才回安全失敗。認證失敗沿父原流程處理，舊批次不在新 Key 下自動重送。 |

## 7. 排程、唯讀來源與容量

批次准入順序為：整個 native batch 協定與敏感預檢 → 取消／原安全狀態 → 父最後生成與程序餘額 → 唯一委派批次／角色／Scope／來源與投影准入 → 預留配額、配置身分、接受批次 → 必要 Git 擷取及零差異／不可用分支 → 建立需要模型工作的子能力 → 並行生成與串行本機讀取 → 各子候選重驗並終結 → 全子終結或整批停止 → 共同十秒安全重驗及全部必要收尾 → 安全回填 → 父最後彙整。純本機准入不建立子 client、不讀新認證或呼叫模型；必要的目前來源讀取仍受租約及敏感閘門。父彙整前必須已完成子 join／disposal。

| 編號 | 要求 |
|---|---|
| MULTI-043 | 並行上限為兩個子生成請求，父此時沒有生成；不得有第三子、預先暖身請求或背景維護。先完成必要 Git 准入，把零差異及普通 Git 不可用者本機終結，再建立其餘需要模型工作的 runtime，全部政策／工具能力驗證通過才釋出開始閘門。普通建立失敗使尚待執行者 failed／delegation_runtime_failed，零子生成，已合法本機終結者保留；敏感或安全驗證失敗仍按整批安全停止。 |
| MULTI-044 | 所有子檔案、Rules／Skills 磁碟重驗及 Git 原生操作經父共用 FIFO 本機佇列，沿同一 WorkspaceOperationCoordinator 排他租約執行；不另建一個可與父寫入並行的租約擁有者。取消者從佇列移除，取得租約後再驗有效性；等待不能以 busy 失敗代替公平排程。 |
| MULTI-045 | 排程只包住實際本機操作，不持租約等模型或父終端機。每子同時最多一個本機操作，原生多呼叫批次仍原序執行；多個子的操作可交錯，不能交叉套用彼此結果。既有 Git 原生 sandbox／收尾政策不放寬。 |
| MULTI-046 | 批次含 Review 時 trusted Core 在批次接受後、子生成前擷取一份目前父 Scope 的不可變 GitSnapshot，受原 8 MiB／200 路徑／60 秒與全部原生限制；它計入批次期限。每子只取得自己 Scope 的來源 view，其他 Scope 的正文／路徑不外送；父過往 Review 身分、報告及 ledger 不改寫。 |
| MULTI-047 | 共享 GitSnapshot 不能變成父子全工作區原子快照的保證；各子結果及批次發布前重驗原 manifest、Scope 與來源版本。既有排除原因、非文字／缺席側與列舉完整性保留，安全子集不能宣稱完整專案已審查。 |
| MULTI-048 | Explore ledger 區分存在性與正文來源：列檔／檔名搜尋只記 canonical path、種類、實體身分及該次已觀察 metadata，零內容 I/O，不要求內容 hash；只有實際安全讀過正文者另記完整 bytes 的 SHA-256／身分與已提供行範圍，不保留額外正文複本。發布前按來源種類重驗：存在性不宣稱內容相同，正文比對完整安全重讀的 bytes／身分，不只 mtime／長度。列檔／搜尋只標當次觀察、截斷與篩選，不承諾整個目錄完全不變。 |
| MULTI-049 | 每子可引用最多 16 個已提供檔案／範圍來源，並最多執行 16 次實際工具呼叫；每筆開始前預留來源／結果的收尾容量。多呼叫會超過剩餘名額時整個該子 native batch 零工具，回 delegation_limit；來源或報告容量不足不能默默省略後宣稱完整。原搜尋單次掃描／候選／bytes／深度及單檔上限保持。 |
| MULTI-050 | 子交接資料（含本份移交包裝、不含另有上限的原規則／Plan／技能／復原附錄）最多 32768 UTF-8 JSON bytes；子完整安全結果最多 8192 bytes，批次 data 最多 32768 bytes，delegate 完整 response 最多 65536 bytes，CLI 完整區塊最多 65536 bytes，各自含欄名／識別／跳脫。 |
| MULTI-051 | 子一般檔案工具保持原結果上限：完整讀檔／列檔依第 04 份，搜尋／範圍讀取與 Git 結果各 65536 bytes；不把本份 8192-byte 最終報告上限錯套所有工具。完整模型估算 196608-byte 門檻及實際 262144-byte 硬上限、單次 8192 output tokens／120 秒不調高。 |
| MULTI-052 | 子工作的共同 120 秒 wall-clock deadline 從接受批次起算，包含建 client、Git 擷取、佇列、全部子生成／工具與各子候選重驗；每子 deadline 不得晚於共同期限，單請求再取原 120 秒與批次剩餘的較小者。全批發布前的 trusted Core 安全重驗與必要收尾另共用 MULTI-073 的十秒，不增加子工作時間。父最後彙整依原請求期限另計，子沒有等待人類輸入的入口。 |

## 8. 共用生成額度與唯一用量

設分派時回合與程序實際剩餘較小值為 R、子數為 N。只有 R ≥ 2N+1 才接受；先保留每子兩次、父彙整一次，再依 tasks 順序以尚可分配額度把每子加至最多三次。父彙整預留只允許父最後生成消耗，子不能挪用。剩餘未預留額度不開新子、不增加每子三次上限。

例：預設回合五次，父已生成一次提出委派，R=4：一子取得三次、父一；兩子需要五次，故整批拒絕。若父回合上限六次且此前只生成一次，R=5：兩子各兩次、父一。這是生成機會，不保證模型必定在配額內提出足夠工具或產出有效報告。

| 編號 | 要求 |
|---|---|
| MULTI-053 | 共用唯一 GenerationBudget，父有效一般輸入開始時只 BeginConversation 一次；子建立、等待、失敗、切角色與批次回填均不重置。原回合預設 5／範圍 1～20、程序 100／1～1000、clear／reset／換 Key 不退款保持。 |
| MULTI-054 | 配額預留、可用剩餘與所有父子生成開始決策在同一原子閘門處理，同時檢查取消／世代及回合／程序上限；已有預留不能再承諾給另一子或其他用途。預留不增加已開始次數，只有緊接實際 GenerateAsync 前扣一次；開始後即使未傳輸、throw 或取消也不退款。 |
| MULTI-055 | 每子至少兩次、至多三次及父一次依上述公式決定，原子拒絕不足額度；拒絕前不建子 client 或呼叫模型。不以各子一份完整回合上限、維護用途、補送結尾或另一 UsageAccumulator 取得額外機會。 |
| MULTI-056 | 子判定「最後一次」使用自己的 lease 剩餘與共同有效性，而非把其他子／父的預留當可用機會。子最後一次請求強制 tools=[]；所有子工具開始前仍須保有該子至少一次後續生成。已用最後一次回工具者以原協定錯誤停止，零該批實際工具。 |
| MULTI-057 | 子終結後釋回尚未開始的預留；這不是退回已消耗額度，不增加每子上限或再派名額。父最後彙整最多一次，tools=[]，其開始扣父回合／程序次數；已開始但無有效回答不再補送。取消／安全失效時不用完預留也不生成結尾。 |
| MULTI-058 | 新增 internal GenerationPurpose.DelegatedRead，並以 ParentTurnId／BatchId／AgentId／Role 與開始時 profile 歸屬；父彙整仍為 Conversation。每個子使用對應 inspector／數值 telemetry，不覆蓋其他子量測；/agents 分列子容量，父 /context 最近請求保持父來源。 |
| MULTI-059 | GenerationBudget、programUsage.BeginGeneration／RecordAttributed／Snapshot 與父回合用量更新經共同序列化決策入口；現有非執行緒安全累計器不能由子直接並行寫入。每個返回 LlmResult 只記一次程序及一次所屬父回合，個子統計為同筆事件的投影，不把子總數再次加進父總計。 |
| MULTI-060 | RequestDispatched、五欄已知／缺值／溢位語意保持：未傳輸不補零，Total 不合成，Thought／Cached 不重加。開始但無結果只記已開始次數，不捏造傳輸與用量。遲到結果即使不得發布正文，仍在收尾確認時唯一記帳；已計事件依本機請求身分去重。 |
| MULTI-061 | 額度不足不是完成；拒絕委派保留此前父操作與安全事實，按原生成／資源原因收尾。on、off、提高額度或查詢不自動重跑；提示使用 /budget，但不得聲稱 clear 可以恢復額度。原程序兩次／300 秒及 test 不支援不變。 |

## 9. 結果、來源與父彙整

### 9.1 子模型輸出與本機外框

Explore 子最後回答使用以下資料格式；不是移交指示或核准。summary 為 1～2048 UTF-16 單位單行非空白文字；observations 最多 8 項，每項恰含 path、start_line、end_line、note，note 為同規則 1～1024 單位；limitations 為最多 8 個同規則字串。行號皆為正整數且 start≤end、最多 200 行；純檔案存在性觀察兩個行號同為 null，不得由此宣稱看過正文。所有來源須在本子 delivery ledger 中且位於子 Scope。整份模型報告先受 8192-byte 最終子外框的較嚴格限制，不能藉內層字串上限忽略外框成本。

```json
{"version":1,"summary":"已定位取消入口；仍須核對其他呼叫者。","observations":[{"path":"src/example.cs","start_line":10,"end_line":12,"note":"此區段包含取消訊號處理。"}],"limitations":["未執行建置或測試。"]}
```

Review 子最後輸出沿第 13 份 review v1：version、snapshot_id、selection、summary、findings；finding 欄位、嚴重度、最多 30 筆／每筆 10 行與已提供變更行條件保持，再受本份 8192-byte 外框較嚴格限制。不另設可繞過 CodeReviewCoordinator 的自由文字 findings。合成 Explore 範例只示格式，不能當真實來源驗證案例。

本機 child 外框恰含 agent_id、role、scope_path、state、error_code、report、coverage。report 為上述合法模型報告或 null；coverage 由本機產生，恰含 basis（workspace_observation／git_snapshot）、complete、source_count、delivered_count、truncated、limitations。Explore 的 source_count／delivered_count 分別為本子安全來源集合／確實外送之不同檔案數，complete 固定 false；Review 的兩個 count 及 complete 沿第 13 份依子 Scope、eligible／delivered change、列舉及排除計算，不把兩個角色的數字混稱相同尺度。coverage.limitations 是 observation_only、truncated、safety_filtered、no_source、no_changes、review_incomplete 及本份准入的既有固定失敗碼之去重陣列；來源不可得時 count=0、complete=false，不能回填猜測值。

子外框合法終態如下；Pending／Running 僅用於本機進度，不出現在最終 children：

| state | error_code／report |
|---|---|
| completed | none；report 是合法模型報告，只有 MULTI-064 的本機零差異分支可為 null，coverage 明示 no_changes 或 review_incomplete。 |
| failed | 普通模型失敗／拒答／截斷用 delegation_model_failed，Explore 無效分析報告用 delegation_report_invalid，普通 runtime 建立失敗用 delegation_runtime_failed；其他沿原工作區／Git／容量固定碼。不得用 none、turn_cancelled 或 delegation_timed_out；report=null。 |
| cancelled | turn_cancelled，report=null；整批取消時只供本機狀態，不發布 children 正文。 |
| timed_out | delegation_timed_out，report=null。 |

| 編號 | 要求 |
|---|---|
| MULTI-062 | 子模型只能提出分析內容；本機決定身分／角色／Scope／狀態／原因／覆蓋。Explore 報告的額外／重複欄位、無效型別、越界行號、未提供來源或非法 Unicode 全部拒絕，不能只以有非空白回答標 Completed。 |
| MULTI-063 | Explore 的 null 行號只可引用本子成功外送的列檔／檔名搜尋項目，發布前按存在性路徑／種類／身分及已觀察 metadata 重驗，不讀正文；有行號只能引用本子已外送的完整／範圍／文字命中區間並重驗內容 bytes／身分。相同行範圍可合併本子多次提供，不用兄弟 ledger 或父歷史；變更回 delegation_source_changed。大檔／非 UTF-8 檔仍可有合法存在性觀察，不宣稱內容相同或文字推理已機械證明。 |
| MULTI-064 | Review 使用原 finding／changed-line／側別／層別／原生 owner 與本子已傳輸 ledger，未傳輸來源不算已看過。無 Git／不支援布局／原生不可用為該子普通 failed，沿原固定碼。子 Scope 的 EligibleChangeCount=0 時沿 GIT-022 本機零子生成／零子 client：state=completed、error_code=none、report=null；只有子 view 沒有任何變更、沒有排除且列舉完整時 coverage.complete=true、limitations=[no_changes]，仍有 metadata 等不可審變更、排除或不完整時 complete=false、limitations=[review_incomplete]。釋回該子未用預留，父彙整預留保持；兩分支均不宣稱經模型審查，零 finding 不等於測試通過。 |
| MULTI-065 | 子失敗／取消／逾時／容量拒絕時 report=null，保留可安全表達的狀態及數值；不能把未完成的串流片段、截斷回答或未驗報告當完成成果。單子完整結果 8193 bytes 整份改為 failed／result_too_large，不刪 observations 或 findings 以保留成功外觀。 |
| MULTI-066 | 批次 data 恰含 version=1、batch_id、state、children、merged_findings。children 按 tasks 順序，merged_findings 僅彙整合法 Review findings，每項恰含 finding 與 agent_ids。保守去重鍵為同一共享來源 manifest／path／layer／side／start／end／severity／title／body，字串 Ordinal 精確相等才合併，作者全留；不同版本、範圍或結論分列，不做語意合併。 |
| MULTI-067 | N 為一或二且所有子皆 Completed 時為 completed；至少一子 Completed 且另有非成功為 partial；全無成功時，任一子 TimedOut 則 timed_out，否則 failed。Completed 包含已明示的本機零差異分支。整批取消／安全失效依第 10 節優先，不因一子先完成仍發布 partial。 |
| MULTI-068 | 已接受並正常收尾的 completed／partial／failed／timed_out 批次，delegate response 使用 status=success、error_code=none、data=上列批次資料，表示協調完成而非子成功；父須讀 data.state。接受前拒絕為 rejected／固定碼／data=null，取消為 cancelled／turn_cancelled／data=null，安全失效／不可確認為 failed／固定碼／data=null，沿停止或重設。批次 data 超 32768 或完整 response 超 65536 時不發布正文，回 failed／result_too_large／data=null 並停止當回合。 |
| MULTI-069 | 父工具配對只接安全外框，不插入假使用者／假模型授權訊息；子內原生訊息不加入 ConversationContext。父最後回答明示分析來源、部分完成、本機零生成分支與未驗限制；來源以批次最後重驗時的觀察為準，不承諾彙整生成期間磁碟凍結。不改父 Plan facts／核准、修改快照、checkpoint／EvidenceEpoch 或正式 /review show 報告。 |
| MULTI-070 | 父成功回合可按原完整工具配對保留 delegate 安全結果；後續 Compaction／WorkSaveSummary 的白名單投影只新增工具名稱及批次／子狀態／固定原因／角色，不直接納子 report、參數、path、finding 或原文來源。SAVE v1 不加欄位、不保存子 runtime／啟用／資格；模型生成的一般摘要仍只是既有有損歷史背景。 |

## 10. 取消、失效、錯誤與收尾

新增固定原因碼：invalid_agents_command（僅本機解析）、delegation_disabled、delegation_batch_invalid、delegation_limit、delegation_budget_insufficient、delegation_source_changed、delegation_timed_out、delegation_cleanup_failed、delegation_model_failed、delegation_report_invalid、delegation_runtime_failed。前述 delegation 碼加入委派 writer／工具固定碼准入；子 context_capacity／既有安全容量分類亦須明確准入，不把任意 SDK／exception 字串當原因。未新增的工作區、Rules／Skills、Git及敏感錯誤沿原擁有者，公開模型錯誤仍不擴充。新增 AgentStopReason.DelegationStopped、DelegationUnconfirmed，後者是不可恢復退出 1。

| 條件 | 固定處理 |
|---|---|
| 未啟用／不合角色能力 | delegation_disabled 或原 mode_restricted，零子能力／請求。 |
| native batch 混合／重複 | delegation_batch_invalid，整批預檢拒絕，其他父工具零執行。 |
| 子數／同回合批次／工具次數超限 | delegation_limit；不得假稱已排程或改用背景分派。 |
| R 不足 | delegation_budget_insufficient，零子 client／請求；不提高預算或改派較少子。 |
| 普通單子 API／Git／工具／無效分析報告失敗 | 該子 failed，原因依第 9 節精確映射，另一子可完成；全批收尾後按狀態彙整，不重試。 |
| 120 秒期限 | 未終結子 timed_out／delegation_timed_out；禁止新子生成／工具／排隊讀取，進入共同十秒收尾；已有 Completed 候選只有經用途限定最終重驗與收尾成功才可構成 partial。 |
| Ctrl+C | 取消父及全批、阻止正文發布，等待收尾後回輸入；無致命錯誤時不退出 130。 |
| EOF／互動故障／不可恢復本機故障 | 取消、等待、退出；正常 EOF 0，I/O／收尾失敗 1。 |
| 認證失敗 | 停全批，照實記帳後走父換 Key 流程；批次與啟用失效，不自動補送。 |
| 敏感資料／非法原生協定／無安全工具配對 | 停全批、丟候選正文，沿共同重設並停用，先保留可安全記帳的事實。 |
| Rules／Skills／MCP／model／Plan／Recovery／Scope 或已引用來源失效 | 停全批，回原 source 錯誤或 delegation_source_changed；不背景 reload、不沿用舊結果，不自動重新啟用。 |
| 必要收尾十秒內不能確認 | delegation_cleanup_failed／DelegationUnconfirmed，停止 session 並退出 1，不靠 clear／換 Key／調額度解除。 |

| 編號 | 要求 |
|---|---|
| MULTI-071 | 取消及終態發布決策在共同閘門原子排序；取消先接受則所有未發布正文作廢，即使模型稍後回成功。普通單子失敗不自動取消健康兄弟；認證、敏感、非法原生或來源失效屬整批失敗，不回含舊正文的 partial。 |
| MULTI-072 | deadline 只將尚未終結的子標逾時，不把已 Completed 改寫逾時；禁止新子生成／工具／排隊讀取並釋回未用子預留。普通 timeout 且仍有 Completed 候選時，trusted Core 可在共同十秒收尾窗口內以獨立用途及收尾 token 取得同一排他租約，只重驗既有候選 manifest／引用來源，不探索新來源、不回填子 Context；父 Ctrl+C 隨時取消此步。Ctrl+C／EOF／敏感／認證／來源失效等不發布正文的停止路徑不額外作發布重驗。安全重驗失敗或窗口不足不得發布 partial；能確認所有活動與釋放已停止則回安全 delegation_source_changed 並停止，無法確認活動／清理則沿 MULTI-074 退出 1。 |
| MULTI-073 | 第一子正常完成不開始全批十秒倒數，不縮短健康兄弟原批次期限。所有子皆正常終結，或首次要求整批停止時，才開始唯一共同十秒收尾窗口，包含 join、遲到用量、必要發布重驗及 disposal，不能各自再加十秒。先阻止子准入、取消在途及排隊讀取、等待已持租約操作；可發布路徑再作 trusted Core 最終重驗，依子 client／來源 bytes／配額順序釋放，確認完成後才安全回填及父彙整，父 session 仍存活。不只是取消等待就宣稱已停止。 |
| MULTI-074 | 子 disposal 不釋放父 Key、共同 lease coordinator、父 Context／Plan 或另一子資源。十秒到期無法確認、子事件投影／父輸出失敗、原生 Git Unconfirmed／cleanup failure 按最嚴重故障退出 1；不能被同時 Ctrl+C、deadline 或普通資源不足覆蓋。 |
| MULTI-075 | Rules／Skills／MCP 等原來源失效先保留先前已發生父寫入／程序／本機安全操作及用量，再撤回確認並依原契約收尾；子不能自行 Revoke、Block 或提交父事實。父先前已寫 A、後來委派失效時，A 的事實與檔案不能被說成全部未變或自動回復。 |
| MULTI-076 | 普通成功及失敗均不自動重試、重派、改模型、加生成、換命令、重放工具或復原檔案。普通失敗後下一回合須由新的使用者訊息開始；安全停用後還須使用者明確 on，不能由模型、技能、報告、存檔或外部 yes 代替。 |
| MULTI-077 | 新停止原因映射到父現有 generation／resource／model／source 對應受阻原因；DelegationStopped 沒有更精確原因時採 operation_failed，DelegationUnconfirmed 採 local_failure 並退出。子任務狀態不新增父 PlanTaskState 或完成 basis，不擴充公開 LlmErrorCode。 |
| MULTI-078 | 任何路徑不寫子報告、認證、來源 ledger 或原生歷史至磁碟；/agents off 只是記憶體狀態，clear／退出丟棄。沒有刪除工具或批次清理；需要清除既有產物時仍交使用者人工處理。 |

## 11. 原契約的明示增量與實作同步清單

下表只列後續產品實作時須同步的接續與回歸，本次不改來源文件。它不是全面廢除原階段限制；未列增量維持原擁有者。

| 原契約 | 限定增量與保持責任 |
|---|---|
| SPEC 第 5～10、13～15 章；第 03／05 份 | 新 CLI 啟停、父委派能力、子循環、單批工具預檢及有來源進度；仍一個使用者父回合與唯一鍵盤讀取者，公開 LLM 不變。 |
| 第 04／06／08 份 | 抽出不建立父快照／證據的子唯讀 facade、子 Scope 及觀察 ledger；共用排他租約與敏感／路徑／搜尋限制不放寬，父後續寫入仍須完整重讀及逐筆核准。 |
| 第 09 份 PLAN-011、014～015、037～043、050～055 與 FUT04-HO-001～008 | 額外子狀態不算父任務在途，不增加 20 任務／1 在途／256 事實上限；子工具不提交父證據，Plan 確認與逐次核准不變。 |
| 第 10 份 BUILD；第 11 份 CTX-011、014～016、050～064、071～080 | 子同 client inspector、共同父回合與配額預留、DelegatedRead 與序列化記帳；唯一程序預算不變，父 BeginConversation 一次；原程序預算／隔離與 test 不支援不變。 |
| 第 12 份 RULE-013～019、028、034～036、048～064 | 子根為目前 Scope 子集，不啟用新規則鏈；重驗／失效傳播與目前來源仍由 Rules 擁有，子結果不能重新核准。 |
| 第 13 份 GIT-032～040、056～069、073～077 | 新共享不可變來源 view 與每子 delivery／候選；原來源、側別、finding 驗證及原生隔離不變，正式 /review 無委派、報告不覆寫。 |
| 第 14 份 TERM-007、049～065 | 新狀態／來源身分與集中進度，仍單一讀取者、busy 丟棄、無子核准／讀鍵，預輸入不能成為啟用或同意。 |
| 第 15～17 份 SAVE v1、CKPT、SKILL | 不持久化子 runtime／啟用／資格、不新增 checkpoint；子借用有效技能快照，父狀態與候選仍由原擁有者管理；compact／save 白名單只增委派安全 metadata。 |
| 第 18～19 份 MCP／WEB | 子不登錄 ExternalRead，不借 Permit、不消耗或重設 MCP 的每父回合額度；父 MCP 版本仍為候選有效性來源，原 per-send 新確認／URL／來源界線不變。 |
| 第 20 份 MODEL-004～006、015～018、050～054、MODEL-HO-013、MODEL-073 | 僅本份啟用期間允許父等待＋至多兩個同 profile 子 client；認證由新用途限定轉接處理，原生 owner 不跨 client，切換仍取消／釋放／清除後重新輸入 Key，沒有跨模型混用。 |

| 編號 | 要求 |
|---|---|
| MULTI-079 | 後續實作在同一變更同步上表受影響介面、組裝、來源戳記、錯誤、CLI／help、README／ROADMAP／SPEC 及子規格產品接續註記；保留歷史階段描述並明示本份限定覆蓋，不以內部重構名義放寬單 client、預算或證據。 |
| MULTI-080 | 不新增父完成資格、任務格式或 SAVE 版本；原 Plan／必備附錄與安全停止原因的實際／保守投影及必然收尾仍須符合 32768／65536 bytes。子額外狀態使用獨立有界投影，不侵占原 facts 容量。 |
| MULTI-081 | 既有 01～20、Python 敏感掃描與適用 PTY 回歸只按本份明示增量修改預期；Disabled 與正式 Review 保持原工具集合，三 profile 均需正式工廠／Runner 整合證據，不能只以假 coordinator 宣稱接入。 |

<a id="upstream-handoffs"></a>

## 12. FUT 耦合矩陣與固定交接 memo

本節是 FUT-14 交接的唯一 memo，不另建重複筆記。第 09／11 份是任務與共同預算的直接來源，第 12～20 份的原交接仍由各自規格擁有；本份只記錄承接、增量與驗收責任。這是目前最後一個 ROADMAP 項目，因此對既有十三個 FUT 記錄整合回交，不創造 FUT-15 或提前定義共享寫入功能。

### 12.1 九筆上游交接逐項承接

| 原交接／擁有者 | 本份承接與明示決策 | 後續驗收 |
|---|---|---|
| [RULE-HO-012](12-project-rules-loading.md#future-handoffs)／FUT-05 | MULTI-013、025～026、039～041、071、075：子 Scope 子集、目前規則鏈／版本及失效傳播，子結果無授權，不帶舊快照續跑。 | MULTI-AC-009、016、026、030 |
| [GIT-HO-012](13-git-diff-and-code-review.md#future-handoffs)／FUT-06 | MULTI-033、046～047、064、066：不可變來源共享、每子 delivery、取消、共用額度及精確去重，正式 Review 保持原流程。 | MULTI-AC-011、020、023、025 |
| [TERM-HO-011](14-terminal-interaction.md#future-handoffs)／FUT-07 | MULTI-017、020～022、071～074：事件身分／排序、唯一讀取者與整批取消，子不能讀鍵／核准。 | MULTI-AC-004、027～029、036 |
| [SAVE-HO-012](15-work-save-and-resume.md#future-handoffs)／FUT-08 | MULTI-018、038～039、069～070、078：不共享工作資格或存檔，子分析不能匯入完成事實，續接停用、SAVE v1 不變。 | MULTI-AC-012、032～033 |
| [CKPT-HO-013](16-change-checkpoints-and-recovery.md#future-handoffs)／FUT-09 | MULTI-032、044、069、075：單一租約、子不寫入／復原或共享 anchor，保留父已發生事實與原 epoch。 | MULTI-AC-010、017、030、032 |
| [SKILL-HO-013](17-reusable-workflows-and-skills.md#future-handoffs)／FUT-10 | MULTI-018、034、039～041：有效技能快照只在本批借用、同來源版本，不獨立啟用、不改父技能或提升能力。 | MULTI-AC-016、026、031 |
| [MCP-HO-013](18-mcp-external-tools.md#future-handoffs)／FUT-11 | MULTI-005、031、034、058、075：首版子工具不含 MCP，沒有子 Permit／讀鍵，父既有額度不重置，MCP 失效取消舊候選。 | MULTI-AC-008、019、026、031 |
| [WEB-HO-013](19-web-search-and-document-fetch.md#future-handoffs)／FUT-12 | MULTI-005、024、034、069：子不連網查文件、不借外送核准；文件、引文與子結論不是本機事實或鍵盤同意。 | MULTI-AC-008、015、031 |
| [MODEL-HO-013](20-multi-model-and-provider-support.md#future-handoffs)／FUT-13 | MULTI-035～038、042、053～060、073：同 profile、每子 owner／inspector、新用途限定認證轉接、共同額度及父子取消；不把父 Key／原生歷史交子任務。 | MULTI-AC-012～014、018～022、028、035 |

### 12.2 第 09／11 份直接契約

| 原契約 | 本份承接 | 後續驗收 |
|---|---|---|
| FUT04-HO-001～002；PLAN-011～015、037～043 | MULTI-011～016、029、069、075、077：原 PlanStore／事實來源、身分與歷史 basis 不由子改寫。 | MULTI-AC-010、030、032 |
| FUT04-HO-003～004；CTX-007、014、050～055 | MULTI-038～041、047～051、063～064：owner／配對、完整必要約束及獨立容量，來源不成父修改基準。 | MULTI-AC-010、012、016、020～024 |
| FUT04-HO-005～006；CTX-011、056～070、080 | MULTI-053～061：父回合一次初始化、總預留與唯一記帳，不退款、不假完成或重播。 | MULTI-AC-018～022、030 |
| FUT04-HO-007～008；CTX-015～016、071～080 | MULTI-013～016、071～080：候選原子發布、失效／清除與安全收尾，20／1／256 不放寬。 | MULTI-AC-026～034 |

<a id="future-handoffs"></a>

### 12.3 對 FUT-01～13 的整合回交

| 編號／對象 | 原擁有者、關係與本份邊界 | 後續同步與驗收責任 |
|---|---|---|
| MULTI-HO-001／FUT-01 | 第 08 份擁有搜尋／範圍讀取；子沿原有界讀取，獨立來源 ledger（MULTI-032、048～051）。 | 不用子搜尋刷新父快照，不跨 Scope 或私人存檔；AC-009～010、017、024。 |
| MULTI-HO-002／FUT-02 | 第 10 份擁有程序／Validation；子無此能力（MULTI-005、034、061）。 | 保留 build 隔離、兩次／300 秒及 test 不支援，子「已測試」文字不能成完成證據；AC-008、030、034。 |
| MULTI-HO-003／FUT-03 | 第 09 份擁有任務／核准／facts；子狀態獨立（MULTI-029、069、077、080）。 | 原 20／1／256 與版本收尾保持，子成功不滿足父 requirement；AC-010、030、032、034。 |
| MULTI-HO-004／FUT-04 | 第 11 份擁有 Context／共同總預算；本份限定增加 lease 與用途（MULTI-041、053～061、070）。 | 同父回合／程序唯一記帳、body 與必要資料保持，不由子重設或背景摘要；AC-016、018～022、024、033。 |
| MULTI-HO-005／FUT-05 | 第 12 份擁有 Scope／規則來源；子只收窄（MULTI-025～026、039、071）。 | 新來源／Scope 改版重驗並取消舊批次，不自動載入規則或復活確認；AC-009、016、026。 |
| MULTI-HO-006／FUT-06 | 第 13 份擁有 Git 原始來源與 finding；本份只新增不可變 view／獨立 delivery（MULTI-033、046～047、064、066）。 | 原生隔離、來源側別與正式 Review 保持，精確去重保留作者／衝突；AC-011、020、023、025。 |
| MULTI-HO-007／FUT-07 | 第 14 份擁有終端機生命週期；父集中事件（MULTI-017～022、071～074）。 | 新事件保持唯一讀取者、busy 丟棄及取消／EOF 優先，不把外部文字回放按鍵；AC-004、027～029、036。 |
| MULTI-HO-008／FUT-08 | 第 15 份擁有 SAVE v1／續接；無子格式依賴（MULTI-018、069～070、078）。 | 不持久化子身分／啟用／原生資料／事實，維護摘要只投安全 metadata；AC-012、032～033。 |
| MULTI-HO-009／FUT-09 | 第 16 份擁有 checkpoint／RecoveryRevision／EvidenceEpoch；子無資格（MULTI-013、032、069、075）。 | 恢復或來源失效取消舊子候選，父先前寫入事實保留，無共享寫入／rollback；AC-010、026、030、032。 |
| MULTI-HO-010／FUT-10 | 第 17 份擁有技能來源／啟用；子僅借有效快照（MULTI-039～041）。 | 不由子切技能、遞迴載入或執行步驟；改版傳播與停用清楚；AC-016、026、031。 |
| MULTI-HO-011／FUT-11 | 第 18 份擁有 MCP 連線／Permit／額度；子不提供 ExternalRead（MULTI-005、031、034）。 | 不替子取 Permit、不重置父額度、不轉認證或遠端結果資格；AC-008、019、026、031。 |
| MULTI-HO-012／FUT-12 | 第 19 份擁有 Learn URL／引用／外送；子無網頁能力（MULTI-005、024、069）。 | 官方來源／子分析不升權，不將引文當本機證據或外送同意；AC-008、015、031。 |
| MULTI-HO-013／FUT-13 | 第 20 份擁有目錄／認證／model lease；本份明示同 profile 多 client 例外（MULTI-035～038、042）。 | 認證只留轉接層，各 owner 隔離，切換先 join／dispose 再清 Key，仍須新 Key／Plan／新指示；AC-012～014、028、035。 |

| 編號 | 要求 |
|---|---|
| MULTI-082 | 九筆上游與第 09／11 份直接契約逐項映射本份需求及驗收，保留原穩定錨點；原規格、當時程式與本份增量一起讀，不以 memo 作既有能力或授權證明。 |
| MULTI-083 | 本節是唯一交接紀錄；十三筆回交各列原擁有者、限定變更與回歸，不再建立重複 memo、共享格式或通用外掛引擎。未來若增共享寫入、異模型、子網路或持久子任務，須先另訂明示契約與驗收，不能援引本份取得能力。 |
| MULTI-084 | 無必須合併開發項目；本份功能完成與其他未結人工／服務／發布門檻分開記錄。本次不提前更新索引或產品完成狀態，後續實作才依第 11 節同步。 |

<a id="acceptance"></a>

## 13. 後續產品驗收

以下均是後續實作的可執行驗收要求，不是本次已通過的產品測試。自動案例使用假終端機、合成資料／憑證、fake clock、可控排程器及假 HTTP handler；不呼叫真實模型或 Learn。併發測試以 barrier／可控 completion 排程，不靠不穩定 sleep 推論結果。

| 編號 | 需求 | 驗收情境與必要斷言 |
|---|---|---|
| MULTI-AC-001 | MULTI-001～010、079～084、085～090 | 文件／能力基線：只新增本檔，現況與歷史分開；無新套件、模型、設定、認證來源或產品完成宣稱，所有需求／交接可追溯。 |
| MULTI-AC-002 | MULTI-003～004、011～016、043 | 正式父回合接受兩子，觀察兩個子 HTTP barrier 同時進入、父無同時請求；至多兩子／一批／深度一，不用序列完成冒充並行。 |
| MULTI-AC-003 | MULTI-017～019、022、031 | 啟動／續接 Disabled，on 只顯示完整說明並啟用、零生成／不調額度；off 不退款，同值不增修訂；登錄表的 Disabled／Enabled、MCP 斷線／Ready 及 Review 各數量、順序精確符合。 |
| MULTI-AC-004 | MULTI-017、020～022 | 單行精確解析、大小寫／額外參數／多行拒絕、敏感命令原子拒絕、不入輸入歷史；busy 的 /agents off 被丟棄，預輸入／模型 yes 不啟用或核准。 |
| MULTI-AC-005 | MULTI-012、023～024、028 | 空陣列、三子、重複／額外欄位、非法 Unicode、空白、2048／2049、16384／16385-byte 邊界；拒絕零子client／生成且不配身分／名額，但父工具階段關閉、最多一次tools=[]說明，不回一般工具循環重派；接受後失敗同樣不得再派。 |
| MULTI-AC-006 | MULTI-027、030、034 | 同 native batch 依序擺合法寫入＋delegate、delegate＋update_task、兩個 delegate：整批零讀寫／任務更新／子建立，所有 call 有安全結果配對，原生簽章保持。 |
| MULTI-AC-007 | MULTI-028～030、053～057 | 同父回合第二批、子假委派、父彙整仍回工具皆拒絕；參數／混合native批次／額度拒絕後均最多一次tools=[]說明，沒有餘額只本機停止，下一使用者回合才可新批。 |
| MULTI-AC-008 | MULTI-005、007、031、034、061、081 | 兩子角色工具精確五／三項；偽造寫檔、build/test、MCP、web、保存、復原、submit_plan／update_task、換模型與讀鍵直接入口全部零副作用／核准；正式 /review 保持三工具。 |
| MULTI-AC-009 | MULTI-025～026、034、044、047 | 子根等父／嚴格子集成功；跨兄弟 Scope、..、絕對、連結、敏感／私人目錄、新 AGENTS、排隊中換目錄身分拒絕，不自動 reload／切 Scope，兩子互不借路徑。 |
| MULTI-AC-010 | MULTI-029、032、069、075、077、080 | 子 read_file／搜尋／Git／成功分析均不新增或刷新父快照、fact、checkpoint、basis／epoch；後續父 replace 仍 SnapshotRequired，已有父在途任務與 255／256 facts 收尾依原契約。 |
| MULTI-AC-011 | MULTI-033、046～047、064、066 | 父／兩子共用不可變 Git bytes但 ledger 各自獨立；A已傳來源、B未傳時B finding必拒；scope view不外送另一子正文；不改父EnterReview／Mode／ReviewTurnActive或正式 /review show。乾淨子Scope、僅metadata變更及僅排除項目皆零子client／生成、report=null、Completed；只有真正無變更為no_changes，其餘review_incomplete，未用預留釋回。 |
| MULTI-AC-012 | MULTI-009～010、035～039、070、078 | 新client owner各異；父原生訊息給子、A給B、子給父均在傳輸前拒絕；子stamp只含平坦版本，無父ContextSnapshot／native messages參照。子結果只經安全JSON，SDK／Secrets／lease一般序列化拒絕，磁碟無子資料。 |
| MULTI-AC-013 | MULTI-004、035～038、042 | 正式用途限定 factory 建兩子，各自同 profile／官方目的地；只在對應認證 header／SDK 注入合成 Key，子 spec／工具／事件／結果／Context／環境零 Key 或 Secrets，無新讀鍵。 |
| MULTI-AC-014 | MULTI-013、036、042～043、073～074 | 第一子建立成功、第二失敗／取消／政策驗證失敗：零子生成、已建 client 有且只有一次釋放；子 disposal 不 Dispose 父認證，失效 lease 不能送 HTTP。 |
| MULTI-AC-015 | MULTI-010、024、037、040、062、069 | 合成短 Key／憑證外形、檔案／技能／子報告假指令、假 yes、提示洩漏要求、控制字元與非法 raw結果：原邊界拒絕、無正文／Key／指示洩漏、零新增核准。 |
| MULTI-AC-016 | MULTI-013、039～041、050～051 | 本回合使用者原文、全部pins／當前規則／只讀Plan／技能／復原警示由程式完整移交，不能只用父模型挑出的約束；父更早原生歷史／舊摘要不自動傳。必要附錄放不下零該次HTTP，不裁約束、不觸發子compact。 |
| MULTI-AC-017 | MULTI-032、044～045、048～049 | 同時兩子本機讀取 barrier：最多一個租約、FIFO且不持租約等模型；取消排隊子零後續取得；第17工具／超來源容量整批子工具拒絕，父快照不受影響。 |
| MULTI-AC-018 | MULTI-053～057、061 | 預設父已用1次，兩子R=4拒絕、一子配3＋父1；上限6時兩子2＋2＋父1，R=5成功。程序剩餘更小時以較小者拒絕；不改子數、額度或重新 BeginConversation。 |
| MULTI-AC-019 | MULTI-054～061 | 同時兩子搶最後額度、未傳輸／throw／取消、早完成釋回：總開始不超回合／程序、每次只扣一次，父預留不被搶；無子最後工具、無退款／重派、MCP額度與原程序預算不重設。 |
| MULTI-AC-020 | MULTI-038、056、058～060、064 | 真 Gemini SDK／假 handler 驗兩profile各自多呼叫簽章／配對、數值telemetry與來源MarkDispatched只屬該子；OpenAI原生Responses假handler驗相同流程，不改公開結果。 |
| MULTI-AC-021 | MULTI-050～051、058 | 量測與實際 body 涵蓋全部附錄／原生配對；估算不可用、196608門檻與262144／262145實際 bytes沿原策略；子量測不覆寫父 /context，子結果8192／8193邊界。 |
| MULTI-AC-022 | MULTI-058～060 | 亂序／遲到結果、RequestDispatched false／true、五欄缺值、long溢位與同一事件重送；程序／父回合只記一次，個子／profile／用途可核對且總數不二次加總，未知非零。 |
| MULTI-AC-023 | MULTI-046～048、062～064 | Git同長改寫／index／HEAD變動／未提供側別行號拒絕；Explore正文同長改寫、身分替換、未傳輸行、未讀正文卻給行號拒絕。僅存在性觀察的大檔／非UTF8檔可合法引用且零內容I/O，按種類／身分／已觀察metadata重驗，不假稱內容相同或全樹目前狀態。 |
| MULTI-AC-024 | MULTI-023～024、049～051、062、065、068、080 | Unicode／跳脫／欄名使內層合法但外框超限：交接32768、子8192、batch data32768、response／display65536各測邊界；不刪條件、finding或來源換成功，原工具上限不誤降。 |
| MULTI-AC-025 | MULTI-064～069 | 一子成功、兩子成功（含本機零差異）皆completed；一成功一失敗partial，全失敗有逾時為timed_out、否則failed。兩子finding完全相同才合併保留作者，title相同但body／severity／側別／版本不同均分列；invalid Explore／普通模型／runtime失敗碼及state組合精確，不將review_incomplete合成完整。 |
| MULTI-AC-026 | MULTI-013、018、026、047～048、071、075 | 在生成、佇列、來源提供後、子完成後／父彙整前分別使Rules／Skills／MCP／Plan／Recovery／model／Scope失效：取消全批、停用、遲到正文不發布、不reload／重放；已知安全用量保留。 |
| MULTI-AC-027 | MULTI-014、020～021、052、071～073 | fake clock測120秒共同期限不被各子重開；A於10秒完成、B至120秒才逾時，不能在20秒被正常cleanup誤停。期限後零新子生成／工具；trusted Core僅在共同十秒內重驗既有來源且收尾成功才回partial，重驗失敗無partial，均逾時為timed_out；事件Seq單調且最終tasks原序。 |
| MULTI-AC-028 | MULTI-020、042、071～074、076 | 父Ctrl+C／EOF與子完成／認證失敗競態：先取消則零正文，全部join／dispose後才返回或讀新Key，無新Key下自動補送；EOF退出0，正常busy取消可回輸入。 |
| MULTI-AC-029 | MULTI-021～022、071～074、077 | disposal／租約／Git收尾永不完成、共同10秒界線、事件／終端機I/O故障與同時取消：不可確認退出1，原Git Unconfirmed優先。正常／逾時兩路皆須先全子join／重驗／dispose才回填及父彙整；清理已確認但重驗失敗只安全停止、不虛報partial，父Key最後釋放。 |
| MULTI-AC-030 | MULTI-029、061、067～069、075、077 | 父此前寫A已Verified、單子普通失敗另一成功：保留A與父facts，partial不完成父任務；全普通失敗仍回可彙整data，安全失效不回partial，無自動重播／rollback。 |
| MULTI-AC-031 | MULTI-005、018、034、039～040、069、076 | 技能／網頁／MCP／存檔／子報告偽造啟用、Scope、Permit、yes、完成／復原事件皆無效；子有效技能快照不反向改父啟用，無子ExternalRead。 |
| MULTI-AC-032 | MULTI-016、018、069～070、078、080 | clear／換Key／換profile／續接／復原後停用、舊ID不重用、最近正文清除、子資源全釋放；SAVE v1不增欄位／事實，checkpoint與父既有檔案不被子清除或復原。 |
| MULTI-AC-033 | MULTI-016、018、041、053、070 | 一般成功／compaction後Enabled保留；compact／WorkSaveSummary只投委派白名單metadata，不直接納report／參數／來源正文；保存再續接Disabled且沒有子runtime／原生資格。 |
| MULTI-AC-034 | MULTI-006、009、061、078～084 | 既有01～20／Python／適用PTY回歸，僅按明示增量更新預期；原20／1／256、程序限制、SAVE v1、MTP不支援及未結發布門檻各自保留，無產品批次刪除。 |
| MULTI-AC-035 | MULTI-004、008、035～043、058～060、081 | 三profile皆透過正式ChatRuntimeFactory／父Runner／子factory接入，兩子生成與最後彙整使用同profile、各owner及正確用途／用量；全程假HTTP、無認證探測或真服務。 |
| MULTI-AC-036 | MULTI-017～022、071～074 | macOS PTY：精確啟停、預輸入排空、busy普通輸入丟棄、來源標籤、Ctrl+C／EOF／失效收尾與終端機還原；另驗--help零互動／網路。PTY不能假稱驗證真並行API。 |
| MULTI-AC-037 | MULTI-006、017～022、027、071～074 | 真人macOS：中文輸入、長結果、兩子交錯進度、取消／EOF及原核准互動；獨立記錄終端機／架構與觀察，不用PTY替代。 |
| MULTI-AC-038 | MULTI-004、006、035～038、052、058～060、064、067～069 | 人工明確授權真Gemini兩profile／OpenAI三組分列實際並行、工具往返、額度與彙整；不記Key／原生body／指示。服務授權或quota不足如實未通過，不換模型、重試或算離線通過。子不使用Learn，真Learn既有待驗另列。 |
| MULTI-AC-039 | MULTI-001～002、079、082～090 | 本次文件查核：需求／案例／九上游／十三回交完整連號與映射、JSON／表格／fence／本機連結／錨點／空白通過，HEAD／索引／既有檔案不變，只有新增本檔。 |

後續產品實作依倉庫指引循序 restore → build → MTP tests → help，再執行適用 Python／PTY／format／空白檢查；原生隔離測試受外層環境限制時照實分列。產品本身受控 test 不支援與開發者可執行測試專案是不同入口，不能混為通過或解除限制。真人、真實模型服務、其他 OS／架構、Release 及人工分發另記，不以離線案例總數代替。

## 14. 本次文件查核與完成條件

| 編號 | 要求 |
|---|---|
| MULTI-085 | 需求 MULTI-001～090、驗收 MULTI-AC-001～039、回交 MULTI-HO-001～013 各自唯一連續；每項需求有對應案例，九上游及第 09／11 份直接責任可追溯，首版行為、數值、權限與失敗不得留下實作者自行決定的高影響空白。 |
| MULTI-086 | 查核 JSON 範例／Schema、Markdown表格欄數／fence、本機連結／固定錨點、限制值及狀態轉換一致性；對未追蹤完整檔做 no-index 空白檢查，再做 git diff --check，不能只檢查未涵蓋新檔的 tracked diff。 |
| MULTI-087 | 對整份文件做獨立唯讀可實作性檢查，特別核對混合native批次、認證轉接、owner／來源ledger、預留／記帳、取消／收尾與九項交接；實質缺口直接修入本檔，不延伸為產品開發或第二份memo。 |
| MULTI-088 | 本次不執行restore／build／產品測試／PTY／真人／真實服務／Release作為規格完成證據；只回報實際文件檢查與範圍結果。所有第13節產品驗收仍待實作，歷史數字不算本次通過。 |
| MULTI-089 | 落檔後確認只有本檔新增，既有工作保留、HEAD／Git index不變；不附機密指示、Key／認證、raw SDK／HTTP、真實例外或其他敏感資料。使用合成範例，不因文件檢查建立持久產品資料或批次清理。 |
| MULTI-090 | 完成點為一份可獨立開發、追溯耦合及分層驗收的FUT-14規格；後續收到另次實作指示才開發產品，開始前再讀本份與原交接、核對當時程式並按第11節同步。最後FUT規格完成不抹除既有人工／服務／原生／發布待辦。 |

### 本次文件查核紀錄（2026-10-29）

文件檢查確認 90 項需求、39 項案例、九筆上游及十三筆整合回交具完整編號與映射；三個 JSON 區塊、表格、fence、27 個本機連結／錨點及新檔空白檢查通過。預留公式以剩餘額度 0～1000、子數一／二逐一核對，符合共同上限及文中範例。

獨立唯讀審閱後已補齊逾時發布重驗、收尾順序、存在性／正文來源區分、零可審差異、拒絕後工具階段、單子成功及固定錯誤分類；一併明確隔離父原生 Context 參照與本回合必要約束。Git 範圍檢查確認僅新增本檔，既有檔案／索引／HEAD 不變。本次沒有產品實作或產品驗收，第 13 節全部仍待後續執行。
