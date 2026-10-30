# 10：受控建置與測試開發規格書

## 第 22 份產品實作接續（2026-10-30）

[第 22 份](22-release-publish-and-processhost.md)修正產品發佈時的 ProcessHost apphost 缺失：僅 ProcessHost 專案以 `TreatAsLocalProperty="UseAppHost"` 保留無條件 true，外部指定 false 亦須生成；CLI 可繼續以 DLL 發布。直接／參照建置與乾淨／增量發布由真 SDK 離線回歸驗證，必要四件套位置、權限及內容雜湊保持。

不改受控建置或唯讀 Git 的執行路徑、控制管道／固定協定、身分檢查及 runtime 唯讀／原生隔離，不提供 DLL 回退或缺檔略過。產物完整性不證明原生執行已驗收；MTP test 不支援與第 10 份其餘未結門檻保持。完整掃描非零時禁止啟動同一 Release 探針；分層結果見 [README](../README.md#release-publish-validation)。

## 第 21 份產品接續（2026-10-30）

[唯讀多 Agent 分工](21-multi-agent-delegation.md)明示接續本份：子不取得 build／test 或一般程序能力；原程序兩次／300 秒與 MTP test 不支援保留。Git 唯讀 recipe 仍走原用途限定原生執行器及收尾政策。 本段為產品增量；下列撰寫當時的階段邊界與歷史驗證保留，不能視為全部人工／服務／發布驗收完成。

## 第 20 份產品實作接續（2026-10-29）

不同模型沿用同一受控程序准入與逐次核准，每回合兩次／300 秒及 test 不支援保持。已開始發布／查核的操作先安全收尾，切模型不能解除 Unconfirmed 或 cleanup 故障。 詳見[第 20 份規格](20-multi-model-and-provider-support.md)；A、B 分層證據見 [README](../README.md#model-validation)，真實服務／真人與既有 Release 未結門檻另列。

## 第 19 份產品實作接續（2026-10-28）

Learn 搜尋與 fetch 共用原 MCP 兩次／30 秒額度，與 .NET 兩次／300 秒分列；外部範例不執行、不授予程序或網路 recipe，也不完成 Validation。原 MTP test 不支援及原生門檻不變。 詳見[第 19 份](19-web-search-and-document-fetch.md)及其固定交接；當次分層證據與未驗項目見 [README](../README.md#web-validation)。

## 第 18 份產品實作接續（2026-10-28）

程序候選納入 MCP 戳記，在核准後及最終同步啟動權限點重驗；先記事實再 Settle，外部結果不抵 Validation。MCP HTTP client 不取得程序能力，其兩次／30 秒主動網路額度與本份兩次／300 秒預算分列；test 不支援與原生門檻不變。 詳見[第 18 份](18-mcp-external-tools.md)及其固定交接；實際驗證與未驗層級見 [README](../README.md#mcp-validation)。

## 第 17 份產品實作接續（2026-10-27～28）

受控程序直接入口、確認後及啟動前重驗技能，借用既有排他租約；實際程序結果先入帳再撤確認。recipe、cwd、隔離、預算及 test 不支援保持。詳見[第 17 份](17-reusable-workflows-and-skills.md)及[當次分層驗證](../README.md#skills-validation)。歷史文件交付敘述保留，未結原生／真人／真實 Gemini／Release 門檻不變。

## 第 16 份產品實作接續（2026-10-27）

checkpoint 不捕捉 build 產物或程序副作用；本機復原後全部舊 Validation 事實失去新完成資格，須新 epoch 的實際驗證。原受控 recipe、原生隔離、生成／時間預算與產品 MTP test 不支持邊界維持，復原不是 rollback build／test。 詳見 [第16份](16-change-checkpoints-and-recovery.md) 與 [當次分層證據](../README.md#checkpoint-validation)。下列原階段敘述保留歷史範圍。

## 第 15 份產品實作接續（2026-10-26）

[工作保存與續接](15-work-save-and-resume.md)的限定增量：私人存檔位置加入 .NET admission 與原生 read／write deny，即使它位於其他允許讀取／輸出根內仍不可達。保存及續接不建立程序證據，也不提升 Validation／test 能力；SDK 10.0.100 的私人 IPC 邊界、test 不支援及既有未結原生驗收保持。 本節保留舊階段歷史，實際分層驗證見 [README](../README.md#work-save-validation)。

## 第 14 份產品實作接續（2026-10-26）

[第14份終端機操作改善](14-terminal-interaction.md)只擴充一般ReadInputAsync的隱藏多行／文字元素編輯、F2安全預覽、F3明確貼上、F4換行及程序內安全歷史；Key／認證選單／三種核准仍走原隱藏單行。多行斜線指令在所有解析器之前整筆拒絕，單行精確匹配與Scope／pin字面餘文保持。

CLI輸入狀態加入共同安全清除、認證選單前清除及政策／退出生命週期；不加入Context／摘要／計畫／Git來源或完成證據，沒有新增生成、工具與權限。RULE-HO-006／GIT-HO-006的承接細節見第14份第7、8、9、11、12節與TERM-AC-016～026。原階段歷史保留，當次證據及真人／服務／原生／發布待辦見[README](../README.md#terminal-interaction-validation)。


## 第 13 份產品實作接續（2026-10-26）

[第 13 份 Git 差異與程式碼審查](13-git-diff-and-code-review.md) 新增專用 GitRead 能力與三項唯讀工具，依 Plan／Execute 提供九／十二項；`/review` 為共用 Runner 與生成預算的新入口，該回合只允許三項 Git 工具。原十項工具的順序、輸入與成功資料保持，公開 LLM 契約與 SDK 訊息所有權保持。

Git 來源、覆蓋與報告不建立修改快照、Plan Read requirements 或 Verified／Validation 事實；一般 Git 探索與正式審查分開。僅審目前 Scope 的安全子集；忽略來源的祖先 metadata 例外不放寬一般 `.git`、跨 Scope 讀取或搜尋遇新規則邊界整次拒絕的契約。規則失效、共同重設及已發生操作事實仍依原契約收尾。

實作、當次分層證據與未驗證項目見 [README](../README.md#git-review-validation)。真人、真實 Gemini、第 10 份 test 不支援／未結原生驗收，以及第 07 份 Release／分發門檻分開保留。


## 第 12 份產品實作接續（2026-10-25）

直接 project_path 新增目前 Scope／祖先規則檢查，核准預覽綁來源版本並在啟動前重驗。已確認的程序結果先記錄，規則收尾失敗不得改造成程序未確認；原生未確認與清理失敗仍維持較嚴格退出。固定 recipe、工作區根 cwd、私人 IPC、每回合兩次／300 秒、test 不支援及間接輸入限制均保留。

實作、當次離線／PTY 證據與未驗證部分見 [README](../README.md#project-rules-validation)。真人 RULE-AC-033、真實 Gemini 034、第 10 份未結原生驗收與第 07 份 Release／分發門檻分開保留。

## 第 11 份實作接續（2026-10-25）

本份歷史階段限制由 [第 11 份 Context 與預算管理](11-context-and-budget-management.md) 明示擴充：成功歷史以摘要替換最舊完整前綴並保留最新兩回合；生成預設仍每回合五次，新增程序總額度100次，回合／程序可調為1～20／1～1000。自動摘要共用一般回合額度，手動 /compact 另有單次維護統計且計入程序總額度；只有有效 /compact 是新增會生成的本機指令例外。clear、reset、換 Key、模式及計畫變更不退還程序消耗。

PlanStore、原始工具配對與簽章、讀檔快照及逐次核准仍由原邊界管理。20任務／1在途／256事實與計畫容量不放寬；程序仍兩次／300秒、一般失敗即停止，test仍不支援。實際功能、當次證據及未驗證部分見 [README](../README.md#context-budget-validation)，不以本增量解除其他階段門檻。

> 文件版本：0.1｜建立日期：2026-10-24｜狀態：待實作規格
>
> 選題依據：[未來開發大綱 FUT-02](../ROADMAP.md)。上位文件：[開發總規格](../SPEC.md)第 5、8～15 章；承接 [05：Agent 工具循環](05-agent-loop.md)、[06：檔案編輯與核准](06-file-edit-and-approval.md)、[07：預算與整體驗收](07-budget-and-acceptance.md)、[08：程式碼搜尋與局部讀檔](08-code-search-and-partial-read.md)及 [09：Plan 模式與任務清單](09-plan-mode-and-task-list.md)，尤其第 09 份的 [FUT-02 交接專節](09-plan-mode-and-task-list.md#fut-02-handoff)與 [FUT-04 交接專節](09-plan-mode-and-task-list.md#fut-04-handoff)。目前能力以 [README](../README.md)及程式為準；既有規格的「待實作」是其撰寫當時狀態。

## 實作接續與驗收限制（2026-10-24）

本檔原文的「本次只交付文件」是 0.1 撰寫階段的歷史界線。後續已依使用者要求開發，實作與實際證據見 [README](../README.md#controlled-build-validation)，原需求／案例編號及完整完成門檻保留。

真實程序驗證確認：SDK 10.0.100 的 MTP `NamedPipeServer.GetPipeName` 在 Unix 固定回傳全域 `/tmp` 路徑，不採私人 TMPDIR，與 BUILD-035～036／第 6 節的私人 IPC 限制衝突。[固定版本來源](https://github.com/dotnet/sdk/blob/v10.0.100/src/Cli/dotnet/Commands/Test/MTP/IPC/NamedPipeServer.cs#L214-L223) 使用者於 2026-10-24 明確選擇保留隔離邊界、完成可用建置並明示測試不支援；因此目前 `test` 應在目標／探針／核准前回報 capability_unavailable，不修改 SDK、不增加全域 IPC 或網路權限。既有 MTP recipe、parser、v2 型別與測試是待支援的契約，不等於產品能執行 MTP。

這是目前產品交付範圍的明示限制，不把 BUILD-AC-016～018 的真實 MTP 正向驗收改成成功或略過便算完成；第 10 階段仍未完整驗收。將來需另行確認 SDK 相容修正、維持原限制並通過真實案例後，才能重新宣告測試支援。第 07 份發布門檻與真人／真實服務亦繼續分列。

## 1. 目的、範圍與本次交付

Agent 修改程式後，能在使用者逐次核准、離線且有作業系統存取限制的環境執行 .NET 建置／測試，取得本機程序事實，分別回報已驗證與未驗證部分。確認計畫不等於核准程序；命令正常結束也不一律等於測試實際通過。

本次只新增本檔，不修改程式、測試、設定、套件、其他文件或 Git 索引／歷史，不建立實作、額外計畫、工作樹、hook 或執行紀錄，不提交、推送或發布。以下型別、工具、命令及產品案例均是後續開發要求，不是本次已提供的功能或已執行的驗收。

需求使用 BUILD-xxx，驗收使用 BUILD-AC-xxx。FUT-02 是 ROADMAP 項目代號，10 是本份子規格編號；不能將 FUT-02 誤認為既有第 02 份 Gemini 用戶端規格。

| 編號 | 具體要求 |
|---|---|
| BUILD-001 | 首版僅支援使用者確認信任的 macOS 本機 .NET 專案；固定 Debug、明確單一 .csproj 目標及 build／test 兩種任務。測試限 .NET 10 的 Microsoft.Testing.Platform（MTP）模式，成功摘要辨識限單一 module／目標框架，不自動改用 VSTest。 |
| BUILD-002 | 本次只交付本規格書；文件完成、功能實作、假執行器驗收、真實隔離、PTY、真人／真實 Gemini 及發布門檻分別記錄。 |
| BUILD-003 | 沿用 .NET 10、Google.GenAI 1.26.1、公開 LLM 契約、MSTest／Microsoft.Testing.Platform、nullable、warnings-as-errors 及既有格式；不新增 NuGet 套件、產品 CLI 參數、可調預算或認證來源。允許後續新增隨產品建置的內部程序監督專案，不提供一般使用者入口。 |
| BUILD-004 | 不提供任意 shell／命令字串、自由參數、response file、使用者指定環境、restore／套件安裝、clean／rebuild、publish／部署、背景服務、解決方案選取、其他語言工具、Git 工具或其他 FUT 功能。專案內部原有的建置／測試程式可能啟動子程序，仍受同一隔離政策限制。 |
| BUILD-005 | 採原生受限執行，不安裝容器或要求管理員權限；隔離不可用、無法證明必要限制或布局不支援時拒絕啟動，禁止改用未隔離程序、sudo、臨時放寬政策或連網補齊依賴。 |
| BUILD-006 | 不承諾完整封鎖惡意專案、原生程式攻擊、刻意背景化或所有逃離追蹤的後代程序；也不宣稱 VM 等級隔離或未落實的整棵程序 CPU／RAM 硬上限。不得據此放寬已定義的檔案、網路、認證、核准或失敗停止要求。 |

## 2. 現況、責任與相容性

本份以 HEAD 87258e7 為撰寫基線，已核對 ROADMAP、README、第 09 份交接與 PlanStore、PlanArguments、PlanContracts、AgentRunner、ToolRegistry、工作區快照及核准流程；本次不重新執行產品驗收，也不讀取或附上完整應用指示。

目前正式登錄七項檔案工具與 submit_plan、update_task，預設 Execute，已具備計畫版本確認、任務與有限本機證據。Validation 只接受空 requirements，開始時受阻、不能完成；沒有程序執行能力。測試專案設定 TestingExtensionsProfile=None，不能假定已註冊 TRX reporter。README 的第 07 份 Release 掃描、真人／服務及人工分發待辦繼續保留。

| 接續位置 | 後續實作責任 |
|---|---|
| ChatApplication／CLI | 持有 session 的程序服務、核准橋接、回合執行預算及停止鎖定；保持單一終端機讀取者，顯示受控進度及實際結果。 |
| PlanStore／共用執行政策 | Validation v2 定義、版本／任務權限、證據預留與完成選取；沿用既有身分及清除世代。 |
| Tools／Registry／AgentRunner | 新工具嚴格解析、能力過濾、整批預檢、循序派送、程序結果投影、失敗停止及唯一用量記帳。 |
| 工作區服務 | 與檔案工具共用排他操作租約，純路徑檢查、metadata 重驗、允許輸出位置檢查與程序前後快照失效。 |
| 後續 Processes 模組／內部 ProcessHost | 固定 recipe、核准候選、原生 sandbox 及程序群組、可靠啟動回執、輸出讀取、期限、停止與收尾。不可直接取得 BootstrapSession、Key、LlmRequest 或對話歷史。 |

| 編號 | 擴充及相容性要求 |
|---|---|
| BUILD-007 | 七項檔案工具的 Schema／JSON、Analysis／Read／Write 語意與公開 ILlmClient、LlmRequest、LlmResult、LlmMessage 不變；程序不是檔案寫入，不套用或偽造 FileWriteState.Verified。 |
| BUILD-008 | 正式工具登錄順序保留原九項，再追加 run_dotnet_task。Plan 仍提供原六項；Execute 在原八項後追加此工具。新增明確 DotnetTask 能力，未知能力預設禁止。 |
| BUILD-009 | 本份是第 09 份預留交接的後續擴充。第 12 節列出的 PLAN 契約須在後續產品實作的同一變更中同步修訂；本次只新增本檔，不改寫既有歷史時態，也不讓尚未實作的契約提前生效。 |
| BUILD-010 | 無計畫 Execute 可逐次核准獨立執行；有活動計畫時，不論其為 Draft 或 Approved，均不得繞開其版本與 Validation 任務要求。 |
| BUILD-011 | 後續實作同步更新最小必要能力說明、help、README、ROADMAP、總規格索引及第 09 份受影響敘述；不傾印固定應用指示。第 07 份未結門檻不因本份完成而解除。 |

## 3. 內部契約與資料所有權

所有新增型別為 internal。含路徑、文字及集合者使用密封類別、唯讀屬性、防禦性複製及固定型別標記 ToString，不提供一般物件／SDK 序列化出口。

~~~csharp
internal enum DotnetTaskKind { Build, Test }
internal enum ProcessStartState { NotStarted, Started, StartFailed }
internal enum ProcessEndState { NotStarted, Exited, Cancelled, TimedOut, OutputLimit, Failed, Unconfirmed }
internal enum ValidationOutcome { NotVerified, BuildSucceeded, TestsPassed }
internal enum ProcessApprovalDecision { Approved, Declined, Cancelled, EndOfInput, Unavailable }

internal interface IControlledDotnetTasks : IAsyncDisposable
{
    Task<DotnetTaskResult> RunAsync(
        DotnetTaskKind task, string projectPath, CancellationToken cancellationToken);
}

// 新增到既有 IChatInteraction；保留原檔案與計畫確認方法。
ValueTask<ProcessApprovalDecision> ConfirmProcessAsync(
    ProcessApprovalPreview preview, CancellationToken cancellationToken);
~~~

| 資料 | 必要欄位／語意 |
|---|---|
| DotnetTaskRequest | 任務種類、正規化工作區相對 projectPath；不含命令字串、認證或自由 argv。 |
| ProcessApprovalPreview | 完整 DisplayBlock、session 身分、清除世代、不可變 TaskExecutionContext（無計畫時 null）、root／project／runtime／輸出目錄身分、recipe／sandbox 政策版本及單次核准識別。 |
| DotnetTaskResult | ToolStatus／固定錯誤碼、安全程序摘要、可空工具 Response、是否必須停止；程序已發生的事實不依賴 Response 能否形成。 |
| ProcessExecutionSummary | Task、ProjectPath、StartState、EndState、可空 ExitCode、ElapsedMilliseconds、CleanupConfirmed、CleanupFailed、ValidationOutcome、可空 TestCounts、各 stream 原始 byte 計數及截斷／輸出狀態。 |
| TestCounts | Total、Passed、Failed、Skipped，皆為非負 long；Executed=Passed+Failed，檢查相加溢位及 Total=Executed+Skipped。 |
| TaskOperationFact 的程序擴充 | 沿用本機 OperationId／TaskExecutionContext，追加受控 recipe、project、啟動／終結狀態、退出碼、收尾、判準、可空測試計數及截斷旗標。不得保存 stdout／stderr、絕對路徑、argv、控制通道、原始例外或模型報告。 |

| 編號 | 契約要求 |
|---|---|
| BUILD-012 | 每個 session 共用同一工作區、政策與 PlanStore；ProcessHost 僅收一次性、明確欄位的本機工作描述，不擁有 Key、生成能力、任務儲存或一般命令解析器。 |
| BUILD-013 | 新增隨產品建置的內部 .NET ProcessHost，由固定絕對路徑的 sandbox-exec 啟動；它在同一 sandbox 中建立獨立程序群組並啟動 dotnet。使用專用匿名管道傳送固定控制訊息；目標及其後代不得繼承控制管道。stdin 關閉，stdout／stderr 僅作不可信文字，不能偽造控制回執。 |
| BUILD-014 | Started 只表示受控監督器已確認目標 dotnet 啟動，不能以 sandbox-exec／監督器的 Process.Start 成功代替。監督器以原生啟動結果及 exec 錯誤回傳確認目標；控制通道中斷或無法區分 wrapper／目標退出時記 Unconfirmed，不猜退出碼。 |
| BUILD-015 | ToolExecutionResult、WithoutResponse、LocalToolOutcome 及安全 CLI 事件須保留程序摘要；新增 ProcessCleanupFailed 與既有寫入 CleanupFailed 分開，既有寫入建構不變式保留。無法安全形成結果時仍先保留及回報可安全使用的本機事實，再依第 11 節停止。 |
| BUILD-016 | 假執行器只在測試依賴注入入口提供；產品證據只能由實際受控服務提交。模型不能指定 Started、exit_code、approved、證據識別或完成結果；舊 session／世代回執不得發布。 |

## 4. 工具 Schema、固定 recipe 與結果

### 4.1 run_dotnet_task

僅接受原生 function call，兩個必填欄位，不接受額外、重複、大小寫變體、null 或其他型別。任務採 Ordinal 精確比對；path 不 Trim 或進行 Unicode 正規化，沿既有 4096 UTF-16 單位、工作區及敏感路徑規則，且須指向普通 .csproj 檔。

~~~json
{
  "type": "object",
  "properties": {
    "task": { "type": "string", "enum": ["build", "test"] },
    "project_path": { "type": "string", "minLength": 1, "maxLength": 4096 }
  },
  "required": ["task", "project_path"],
  "additionalProperties": false
}
~~~

目標必須明確，不能是目錄、.sln／.slnx、DLL、檔案式 .cs、glob、符號連結或敏感位置；不從檔名猜測測試能力。模型可先使用既有唯讀工具探索，但先前讀取不取代啟動前重驗。

### 4.2 固定命令

工作目錄一律是已確認工作區根。下表的 dotnet 為經重驗的受信任安裝絕對路徑，project 為由程式形成的單一完整路徑參數；表格僅為 argv 展示，不使用 shell 解析或字串串接。所有參數與實際環境均列入核准。

| 任務 | 固定 argv |
|---|---|
| build | build PROJECT --configuration Debug --no-restore --nologo --disable-build-servers --tl:off --verbosity minimal -maxcpucount:1 -nodeReuse:false -property:UseSharedCompilation=false |
| test | test --project PROJECT --configuration Debug --no-restore --no-launch-profile --no-ansi --no-progress --output Normal --results-directory PRIVATE_RESULTS -maxcpucount:1 -nodeReuse:false -property:UseSharedCompilation=false -- --minimum-expected-tests 1 |

PRIVATE_RESULTS 是該次私有暫存下的固定 results 子目錄，不由模型提供。test 不帶 --no-build，會建置當前來源；固定 --no-launch-profile，停用 launchSettings.json 的環境及 CommandLineArgs，不能只停用 profile arguments。其餘不帶 filter、list-tests、ignore-exit-code、report-trx、coverage 或診斷日誌。尾端 minimum-expected-tests 轉交 MTP test module，防止零執行及全略過；本機仍另驗證摘要，不把此參數或退出碼單獨當成完成證據。

| 編號 | recipe 要求 |
|---|---|
| BUILD-017 | 工具先做原始參數及正規化後的敏感檢查，再做 Schema／路徑與能力檢查；Plan 在任何專案 metadata／內容 I/O、暫存、核准或程序啟動前拒絕。不得靠工具描述、檔案文字或 dry-run 宣稱豁免。 |
| BUILD-018 | 固定使用已安裝且符合 global.json 的 .NET SDK 10.0.x；不跟隨工作區 dotnet shim、PATH 中的專案可執行檔或模型指定路徑。受信任安裝根限 /usr/local/share/dotnet 或 /opt/homebrew/share/dotnet 的實際安裝；解析系統安裝連結後驗證真實根及身分。其他布局首版回報 capability_unavailable。 |
| BUILD-019 | 不在核准前執行 MSBuild、測試發現、dotnet --info 或專案程式。允許以有界安全讀取檢查 global.json／目標專案與 restore metadata；SDK／MTP 不符、依賴不足或執行時不相容均受控失敗，不安裝、restore、切 SDK 或換命令重試。 |
| BUILD-020 | 固定參數與受控環境停用可重用建置／編譯伺服器、telemetry、診斷、workload 自動更新及測試 launch profile；不能從 profile 載入未核准環境或額外 argv。不可預先連接既有 host build server。仍嘗試建立不支援服務或輸出位置的專案失敗停止，不增加權限。 |

### 4.3 固定結果投影

完整工具結果依序為 status、error_code、data。Schema／模式／路徑等事前拒絕不帶 data；完成預覽後的拒絕，以及已取得啟動權的操作可帶安全 data，藉 start_state／end_state 區分未啟動與已發生操作。data 的欄位順序固定如下範例，識別／計數／列舉由本機產生。

~~~json
{
  "status": "success",
  "error_code": "none",
  "data": {
    "task": "test",
    "project_path": "tests/Example.Tests/Example.Tests.csproj",
    "start_state": "started",
    "end_state": "exited",
    "exit_code": 0,
    "elapsed_ms": 1250,
    "cleanup_confirmed": true,
    "validation": "tests_passed",
    "tests": { "total": 4, "passed": 3, "failed": 0, "skipped": 1 },
    "stdout": "",
    "stderr": "",
    "stdout_bytes": 180,
    "stderr_bytes": 0,
    "stdout_truncated": true,
    "stderr_truncated": false,
    "output_state": "safe"
  }
}
~~~

範例只展示線上資料形狀，不是本次輸出；stdout 節錄可被結果容量縮減，但 tests 必須另由完整安全摘要形成。build 的 tests 為 null；非正常 Exited 的 exit_code 為 null，validation 為 not_verified。start_state 使用 not_started／started／start_failed；end_state 使用 not_started／exited／cancelled／timed_out／output_limit／failed／unconfirmed。output_state 使用 safe／invalid_utf8／sensitive／unavailable；無法安全發布文字時兩個節錄為空字串。

~~~json
{"status":"rejected","error_code":"invalid_arguments"}
~~~

| 編號 | 結果要求 |
|---|---|
| BUILD-021 | 程序結果使用明確 writer，保留原始程序事實與輸出截斷的差異；所有欄名、固定值、數字及完整 JSON 均受目前敏感政策檢查。不能安全回填時 Response=null，不把遮蔽後內容當成原完整報告繼續使用。 |
| BUILD-022 | ToolStatus.Success 僅用於符合第 9 節的 BuildSucceeded／TestsPassed，且必要收尾已確認。正常退出非零為 Failed／process_failed；退出 0 但測試證據不足為 Failed／validation_inconclusive，仍保留真實 Exited／0。 |

## 5. Validation requirements v2 與任務完成

僅擴充 submit_plan 的任務欄位及 requirement 變體；update_task 的輸入保持原樣。新增可選的 requirements_version，wire 只接受十進位整數 1 或 2，省略視為 1。Analysis／Read／Write 必須省略或為 1，使用原定義；Validation v1 仍為空陣列，v2 必須 1～8 項。

~~~json
{
  "kind": "validation",
  "title": "建置並執行測試",
  "acceptance": "建置成功，且實際執行測試並通過；略過項目另列",
  "requirements_version": 2,
  "requirements": [
    { "tool": "run_dotnet_task", "path": "tests/Example.Tests/Example.Tests.csproj", "task": "build" },
    { "tool": "run_dotnet_task", "path": "tests/Example.Tests/Example.Tests.csproj", "task": "test" }
  ]
}
~~~

上例為 submit_plan.tasks 的一個任務項目。v2 requirement 只接受 tool、path、task，三者必填，additionalProperties=false；tool 必須 run_dotnet_task，task 必須 build 或 test，不能帶行範圍。path 與工具 project_path 使用同一正規化及 .csproj 規則。檔案 requirement 不得帶 task。相同工具／path／task 的重複條件拒絕；同專案的 build 與 test 是兩個不同條件。

| 編號 | 版本及證據要求 |
|---|---|
| BUILD-023 | 在 TaskRequirement 中加入明確程序變體與 Task 種類，在 PlanTaskSnapshot 加 RequirementsVersion；只有版本與全部定義精確相同才保留 TaskId／證據。v1 空 Validation 改 v2 必須增加計畫版本、建立新任務身分並失效舊確認，不靜默補條件。 |
| BUILD-024 | 活動計畫內啟動程序須為目前 Approved 版本、同清除世代，且唯一 InProgress 任務是 v2 Validation，精確包含該 tool／正規化 path／task。不以 Write／Analysis、任務標題、一般文字或相似路徑替代。 |
| BUILD-025 | v1 Validation 的開始／完成保持 capability_unavailable；v2 在程序能力可用時可由 Pending／Blocked→InProgress。程序能力不可用時轉 Blocked／capability_unavailable；其他狀態轉移、最多一項在途及回合正常結束的 turn_paused 沿第 09 份。 |
| BUILD-026 | 啟動前擷取不可變 TaskExecutionContext、預留一筆事實及完成投影容量；執行後至多提交一筆本機事實。拒絕、取消、失敗及 Unconfirmed 不是成功證據；尚未開始者釋放預留。無計畫獨立任務僅進本回合操作紀錄，不建立隱含計畫或消耗任務帳本。 |
| BUILD-027 | completed 時由本機對每個 requirement 選同 TaskId 最近相符終結事實，且 OperationId 各不相同；每項均須正確核准、Started、Exited／0、CleanupConfirmed、無收尾失敗且結果安全。build 需 BuildSucceeded，test 需 TestsPassed。模型不能提供或挑選事實；較早成功不能掩蓋後來失敗。 |
| BUILD-028 | 純輸出節錄截斷不撤銷正常程序事實。Validation 與 Read／Write 的截斷規則分開：build 可依已確認 Exited／0 完成；test 另須完整可辨識測試摘要，即使節錄被縮短亦可保留其已驗證計數。必要摘要缺失／截斷時不能完成。原 Read／Write 的 Truncated 拒絕規則不變。 |
| BUILD-029 | 保留原事實歸屬，不因修訂、回合失敗或模型宣稱成功重綁；只有精確未變且保留 TaskId 的任務可沿用舊版本證據。完成只證明該次核准範圍與當時結果，不保證後來檔案未變、所有測試皆未略過或整體產品正確。 |

## 6. 原生隔離、檔案權限與環境

### 6.1 適用範圍及 admission gate

首版產品驗收平台為 macOS 26.7.1 arm64、.NET SDK 10.0.100 與現有測試套件；同系列修補版本仍須通過真實隔離與 recipe 相容驗收。x64、其他 macOS 版本及未通過平台組合回報 capability_unavailable，不從既有檔案轉接器的支援範圍推定程序支援。

使用固定 /usr/bin/sandbox-exec 載入產品內建、版本化的 deny-default 政策，隨後執行受保護的 ProcessHost。該工具及 sandbox API 在本機 man page 已標示 DEPRECATED；不是未來 macOS 的相容保證。啟動前以產品內建、無專案程式碼的探針驗證必要拒絕／允許案例，探針也只能在 Execute 並經該次核准後執行；探針與真正任務合計一個啟動嘗試及期限。失敗不嘗試真實任務。

原生監督限同一工作群組及已觀測到的後代。刻意 setsid／double-fork／重新掛接父程序、惡意並行檔案系統變動及超越此信任範圍的程式不屬首版承諾。若觀測到這些行為，或不能完成必要檢查，不能照常標示成功；依 Unconfirmed／不支援處理。此界線必須在核准中顯示，不能只放在開發文件。

### 6.2 允許與禁止的資源

| 資源 | 固定政策 |
|---|---|
| 工作區來源與設定 | 允許安全讀取，禁止建立、覆寫、刪除或 rename。沿既有敏感路徑集合拒絕任何讀寫；不額外允許 .git、.env、Keychain、家目錄設定或專案規則中的授權要求。 |
| 建置輸出 | 只允許工作區安全專案目錄的標準 bin／obj 子樹；名稱以精確 bin／obj 比對。其他輸出布局拒絕，不能採工作區全部可寫。 |
| SDK／runtime／套件 | 受信任 .NET 安裝、必要唯讀系統程式／函式庫及現有 NuGet 全域套件根唯讀。NuGet 僅允許目前使用者家目錄的 .nuget/packages；不讀 NuGet.Config、credentials、使用者 home 其他內容或憑證儲存區。其他 package root 首版不支援。 |
| 產品 runtime | ProcessHost、目前 Agent 及其相依組件所在目錄保持唯讀；與 bin／obj 允許寫入集合相交時拒絕。不能建置覆寫目前正在執行的 Agent／監督器，須從獨立受保護的產品位置啟動後再驗證其來源。 |
| 私人暫存 | 每次操作在固定 /private/tmp 建立 aia- 加 16 個本機隨機十六進位字元的 0700 自有目錄，含 home、tmp、results；只允許該次目錄寫入。使用短路徑避免 macOS sockaddr_un.sun_path 的 104-byte 含結尾 NUL 限制；不沿用較長的使用者 /var/folders 暫存根。HOME、DOTNET_CLI_HOME、TMPDIR 指向對應私有位置。 |
| 網路及 IPC | 禁止外部 IPv4／IPv6 TCP、UDP、DNS、代理及通往本機服務的 loopback TCP。只允許位於該次私人 tmp 的必要 AF_UNIX／named-pipe IPC；若 MTP／SDK 需要額外網路或 host 服務，該組合不支援。 |
| 程序及控制 | 子孫繼承同一 sandbox；禁止存取父 Agent 記憶體、task port、debug／認證服務或重新取得工作區外權限。不提供 shell 工具，受信任專案內必要子程序仍只能在相同資源邊界內執行。 |

| 編號 | 隔離及路徑要求 |
|---|---|
| BUILD-030 | 能力預檢與 sandbox 探針不得因專案文字、模型回覆或工具名稱而放寬。政策不能編譯、系統拒絕套用、探針不符、缺監督器或不支援平台皆 capability_unavailable，零目標專案執行；真正任務只能在已套用限制後開始。 |
| BUILD-031 | 沿既有根、每段路徑、普通檔案及敏感規則檢查；SDK／產品安裝根另外記錄受信任實體身分。專案文字讀取最多 131072 原始 bytes、只接受完整安全 UTF-8；不在預檢解譯或執行 MSBuild imports／targets。 |
| BUILD-032 | 建立允許輸出集合時有界列舉工作區，最多 8192 項、深度 32；不跟隨連結，敏感位置不列為輸出。既有 bin／obj 及其沿途／內容須通過 metadata 檢查；符號連結、非普通檔案、普通檔案 link count≠1、身分變動或超限均拒絕。缺失輸出目錄可列為候選，建立權限仍只在核准後授予。 |
| BUILD-033 | sandbox 須限制 link／rename／替換等操作，不能將來源、敏感檔案或外部 inode 變成可寫別名；不能只檢查字串 bin／obj。既有別名、來源到輸出的 link、輸出祖先交換及執行期間 rename 必須有真實 fixtures。無法在支援平台落實時回報 capability_unavailable，不宣稱來源不可寫。 |
| BUILD-034 | 核准後再次核對 root、目標專案身分／完整 bytes、global.json、runtime／監督器、政策指紋及允許輸出集合；有變更則停止並要求下一回合重新提出，不沿用舊核准。原生政策同時限制實際開啟時的存取，不能把一次預檢當作執行期間的隔離。 |
| BUILD-035 | 不繼承環境。固定 PATH=/usr/bin:/bin 與受信任 dotnet 目錄、DOTNET_ROOT／DOTNET_HOST_PATH、NUGET_PACKAGES；HOME／TMPDIR／DOTNET_CLI_HOME 為私人目錄。固定 LANG／LC_ALL=en_US.UTF-8、DOTNET_CLI_UI_LANGUAGE=en-US、DOTNET_CLI_TELEMETRY_OPTOUT=1、TESTINGPLATFORM_TELEMETRY_OPTOUT=1、DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE=1、DOTNET_CLI_USE_MSBUILD_SERVER=0、MSBUILDDISABLENODEREUSE=1、DOTNET_EnableDiagnostics=0、DOTNET_NOLOGO=1；TZ=UTC。其他變數、代理、認證、動態載入及診斷設定不傳入。 |
| BUILD-036 | 僅繼承必要的 stdout／stderr 及監督器控制 handle；dotnet／專案子程序不繼承控制 handle、終端機、工作區寫入 handle 或 Agent 自有資源。私人目錄只在核准後以排他 mkdir 建立並核對身分／owner／0700；碰撞或既有連結即失敗，不刪除、不改用未預覽路徑。Unix socket 完整路徑須在支援平台限制內，不能失敗後改用 TCP。程序不接受互動輸入；不能為要求認證／網路／互動的專案開新讀取者。 |
| BUILD-037 | 建置／測試可能在已核准產物與私人暫存內產生、修改或移除工具管理的檔案；核准須完整說明。產品不提供來源刪除、批次／遞迴清理或工作區復原；不呼叫目錄遞迴刪除。產物及私人目錄預設保留，需整批清理時交使用者人工處理；不得以清理名義掃描後逐檔批次刪除。 |

## 7. 單次核准與啟動權

核准完整區塊須列出：使用者信任專案的適用界線、任務、專案／工作目錄、SDK／執行檔、完整 argv、政策版本、允許讀取／寫入集合與私人位置、環境鍵及實際受控值、禁止外部網路／僅私人 IPC、期限／輸出上限、程序可能執行專案程式碼，以及產物保留及原生後代追蹤限制。有計畫時另列 PlanId／Revision／TaskId。

提示固定為「確認此單次受控 .NET 任務？[y/N] 」。完整區塊與首次提示共用 65536 UTF-8 bytes 上限，不省略權限或輸出目錄後批准；超限拒絕並停止。

| 編號 | 核准要求 |
|---|---|
| BUILD-038 | 新增 AwaitingProcessApproval，與 AwaitingPlanApproval／檔案 AwaitingApproval 分別處理。使用既有隱藏輸入與單一讀取者，只有完整安全預覽後丟棄舊緩衝、啟用該次讀取所得的 y／yes 可同意；大小寫與首尾空白可接受。模型 yes、預貼 yes、計畫確認或 InProgress 均不能授權。 |
| BUILD-039 | n／no／空行拒絕；其他輸入重問同一候選，不當作聊天或斜線指令。Ctrl+C 取消目前回合並收尾後回輸入；Ctrl+D／EOF 等待收尾後正常退出 0。這不是計畫確認 Ctrl+C 的特例。互動／輸出失敗退出 1；零目標程序執行。 |
| BUILD-040 | 核准只綁單次候選、session／世代、目前計畫 Context、recipe／sandbox 指紋及預覽身分。回傳後重新核對全部權限、預算、取消及 BUILD-034 的觀察；核准不可快取、重用、移交另一操作或套到新版本。 |
| BUILD-041 | 在同一排他租約中完成最終重驗、證據／資源預留與快照失效後，才於原子決策點消耗一次核准並授予啟動權。此前取消零啟動；此後取消必須處理可能已開始的監督器／目標，不得只取消等待。 |

## 8. 執行、取消、程序收尾與快照

固定流程：模式／Schema／任務預檢 → 共用工作區排他租約 → 有界檢查及候選／容量預留 → 完整核准 → 再重驗 → 全快照失效 → 取得啟動權及開始計時 → 隔離探針／監督器 → 目標啟動 → 有界輸出與進度 → 正常結束或要求停止 → 群組／已知後代及管線收尾 → 再次全快照失效 → 提交安全事實／結果 → 釋放租約。

| 編號 | 執行及收尾要求 |
|---|---|
| BUILD-042 | 程序與既有檔案操作共用工作區排他租約，不開第二條讀寫通道；Registry、Runner 與服務入口共用目前政策。整批仍依 PartIndex／CallId 順序，不能平行建置／測試或在同一回合啟動背景工作。 |
| BUILD-043 | 取得啟動權前及所有啟動嘗試收尾後使全部讀取快照失效，即使探針／目標啟動失敗或任務已取消亦同。使用工作區租約內的專用失效入口，不呼叫會取消自身操作的 public Clear，也不清除 PlanStore。原子更新自己的快照租約世代並保留原 Plan 執行世代；程序輸出不能建立或恢復快照。 |
| BUILD-044 | 使用單調時鐘；啟動權授予時開始、開始必要收尾時停止累計執行時間，涵蓋探針、監督器啟動、目標與等待正常結果，核准等待不計。各任務實際期限為 recipe 上限與本回合剩餘 300 秒的較小值；剩餘為零不得授予啟動權。 |
| BUILD-045 | 一次啟動嘗試以消耗核准／授予啟動權計數；之後探針或啟動失敗亦占一次，不以內部 helper 子程序數計量。事前拒絕、使用者拒絕或取消不消耗啟動次數。每個一般使用者回合重設兩次／300 秒預算；計畫修訂與工具不能重設同回合預算。 |
| BUILD-046 | 取消、逾時、輸出上限、I/O／監督器失敗時停止新工作，對該次程序群組及已知後代發出 SIGTERM，最多等 2 秒，再對仍存活的自有程序發 SIGKILL 並最多等 3 秒；記錄 PID 與開始身分，避免 PID 重用誤殺。不殺 host 既有 build server 或其他使用者程序。 |
| BUILD-047 | 正常根程序退出也要確認工作群組與已知後代終止、控制通道完成，以及 stdout／stderr EOF／讀取器／handle 釋放；管線最長再等 1 秒。監督器失去觀測、已知後代換群組、收尾逾時或不能確認停止皆 Unconfirmed／ProcessCleanupFailed，不以 WaitForExit／HasExited 或 Kill(entireProcessTree:true) 單獨證明全部停止。 |
| BUILD-048 | CleanupConfirmed 只表示本次受支援群組、已知後代及自有管線／handle 的必要收尾完成，不表示已偵測所有刻意逃離的程序。觀測到不支援的背景化／脫離行為時拒絕成功；Unconfirmed 優先於同時取消／逾時，鎖定 session 並退出 1，不再生成、讀寫、clear 後續跑或換 Key 重開工作。 |
| BUILD-049 | 正常 Exited 才可公布真實目標退出碼；取消、逾時、輸出超限、被信號終止、StartFailed 或 Unconfirmed 一律 exit_code=null。同時事件依最終決策點判斷：正常結果與必要收尾已提交後的遲到取消不回溯事實；尚未提交則保留取消及實際已知副作用，不能稱成功完成。 |
| BUILD-050 | 非成功程序或核准結果停止剩餘工具與生成，剩餘呼叫各有 NotExecuted／turn_stopped（取消沿既有 turn_cancelled），不重試、換命令、自動修改、復原或啟動修正循環。先前 Verified 寫入及已接受完成保留；安全的一般失敗可在下一則使用者明確指示後重新啟動任務及取得新核准。 |

## 9. 輸出、安全與測試成功判準

### 9.1 輸出讀取及保留

stdout／stderr 同時以固定 4096 bytes 區塊讀取，各自保持嚴格增量 UTF-8 decoder，跨區塊字元不得拆壞；EOF 時未完成字元為 invalid_utf8。兩個 stream 的相對交錯不作可靠的總排序，不拼接成一份可還原的原始轉錄。

每個 stream 原始保留窗最多 16384 bytes：前 4096 bytes 與最近 12288 bytes；超過時標該 stream truncated。先完整增量解碼與敏感檢查，再決定保留；邊界以完整 Unicode scalar 對齊，保留窗的實際 bytes 可較少。每個 stream 另有最多 8192 UTF-8 bytes 的當前邏輯行／MTP 摘要候選緩衝；超長行使該候選無效，不無界累積。只有最終合格摘要可形成計數；不可從任意中間成功字樣完成任務。

| 編號 | 輸出要求 |
|---|---|
| BUILD-051 | stdout／stderr 每個實際讀入 byte 均計入合計上限 1048576；讀到第 1048577 byte 時觸發 OutputLimit 並停止，不把輸出單純丟棄後無限排空。停止期間仍在固定緩衝內排空／計數至收尾期限，不再累積內容；byte 計數溢位視為本機失敗。 |
| BUILD-052 | 敏感掃描涵蓋保留與即將丟棄的文字；以每個 stream 的連續原文、移除控制／ANSI 的可見文字及固定順序的候選節錄組合分別檢查，保留當前 Key 所需的有界跨區塊比對狀態，不假定區塊／行等於敏感邊界。完整最終 JSON／CLI 區塊再次檢查。不得將 Key、原始認證、應用指示、SDK／HTTP／例外傾印輸出。 |
| BUILD-053 | 不直接串流原始輸出至終端機。正常進度只用固定階段、任務及每 5 秒一次的 elapsed 訊息；最終 CLI 顯示安全相對 path、啟動／結果／退出碼、計數、截斷與收尾限制，不自動顯示 stdout／stderr 全文。模型僅取得通過檢查的有界純文字節錄，不能由其內容授權任何操作。 |
| BUILD-054 | 保留文字中的 ESC／ANSI／OSC、退格、CR 與其他控制字元以可見跳脫表示，LF／Tab 可保留；非法 UTF-8 不以替代字元假裝成功。敏感、解碼或輸出讀取失敗時先停止並完成必要收尾，整份節錄不得發布，validation=not_verified。 |
| BUILD-055 | 完整 JSON 結果含跳脫、欄名及 envelope 最多 65536 bytes。writer 先保留必要程序摘要及截斷標記，再按 stdout 尾端、stderr 尾端、stdout 前端、stderr 前端順序縮減節錄至合格，以完整 scalar 移除；不可裁計數或結果旗標。固定部分已超限或敏感時 Response=null，沿既有重設；不影響可安全保留的本機程序事實。 |

### 9.2 MTP 摘要解析與完成判準

固定使用英文／無 ANSI／Normal 的 SDK 10.0.x MTP 輸出。只接受 stdout 最後一個完整的整體摘要區塊，區塊後僅可有空白；區塊必須以完整一行「Test run summary: Passed!」開始，後續包含 total、failed、succeeded、skipped 各一次的十進位整數行。每行可有 ASCII 空格／Tab 縮排，冒號兩側可有 ASCII 空白；不得帶千分位、負號、小數、指數或其他值。鍵採 Ordinal；succeeded 對應 Passed。duration 行可有一次，僅作安全顯示資料、不作計數或完成依據；不接受其他未知行、重複鍵、缺欄或溢位。

整體摘要最多 8192 UTF-8 bytes，必須完整解碼、通過敏感及語法檢查；前面模組結果或 build 摘要不能代替它。出現多個整體摘要候選、互相矛盾的計數、Failed 摘要或無法辨認的 SDK／runner 格式，一律不形成 TestsPassed。不同修補版本若輸出不符，須明確新增並驗收 parser 版本，不在執行時猜語言或採寬鬆正規式放行。

| 編號 | 成功要求 |
|---|---|
| BUILD-056 | BuildSucceeded 需實際核准並 Started、正常 Exited／0、CleanupConfirmed、輸出檢查無故障及結果安全；只能稱「此目標 Debug 建置成功」，不能稱測試通過或全新重建。增量建置允許，但不接受舊回合命令文字當證據。 |
| BUILD-057 | TestsPassed 除同樣的程序／安全條件外，需完整有效 MTP 整體摘要，Passed>0、Failed=0、Total=Passed+Failed+Skipped，且尾端完整。零測試、全略過、只有 discovery／list、無摘要、摘要過長／損壞、計數矛盾或只看見成功字樣均 validation_inconclusive，即使退出碼為 0。 |
| BUILD-058 | Skipped 不算實際執行；CLI／模型回填如實列出，不能說「全部測試均執行」。若專案或其設定忽略失敗退出碼，仍須通過本機摘要條件；偽造摘要的惡意專案不在首版信任承諾內，這些數值僅為 runner 回報，不是對專案誠實性的證明。 |
| BUILD-059 | 不要求或自動安裝 TRX reporter，不解析任意既有報告檔，也不啟用 coverage／dump／原始日誌。未支援 runner／摘要格式，以及多 module／多目標框架額外 assembly 行的摘要，保留實際命令結果但不能完成 Validation；不悄悄改用 VSTest、讀舊檔或回退為 exit_code=0 即通過。 |

## 10. 固定資源限制與用量

| 資源 | 固定值／計量 |
|---|---|
| 每回合程序啟動／執行時間 | 最多 2 次／累計 300 秒；依 BUILD-044～045，build 上限 120 秒，test 上限 180 秒。 |
| 停止／強制確認／管線 | 2／3／1 秒，按階段各只用一次；正常退出後發現殘留程序亦使用同一套收尾，不重置期限。 |
| 並行工作／已觀測程序 | 共用工作區最多 1 項；監督器最多記錄 256 個目標／後代身分，每 100 ms 檢查一次群組及已知身分，另監看根程序結束。達身分上限即停止，不能丟棄身分後宣稱收尾完成。此觀測不構成完整惡意後代追蹤保證。 |
| 預檢 | 目標文字各 131072 原始 bytes；列舉 8192 項、深度 32、相對 path 4096 UTF-16 單位；每次 metadata 重驗最多按同一上限重走一次，超限拒絕。 |
| 原始輸出／保留／摘要候選 | 合計 1048576 bytes／每 stream 16384 bytes／每 stream 8192 bytes；固定 4096-byte 讀取緩衝。 |
| 工具結果／程序核准 | 各完整區塊最多 65536 UTF-8 bytes，含跳脫／提示；核准不截斷。 |
| 任務／事實／模型計畫投影 | 原 20 項、最多 1 InProgress、256 筆事實及 32768 UTF-8 JSON bytes；v2 requirements 仍最多 8。 |
| 歷史／生成／模型 body | 原五個完整成功回合、每回合最多五次 GenerateAsync、262144 UTF-8 JSON bytes；程序時間與每次生成 120 秒分開。 |

| 編號 | 預算要求 |
|---|---|
| BUILD-060 | 所有新限制由原擁有模組執行，不新增可調大設定。程序期限／啟動預算／證據不足時，在該次啟動權前拒絕，停止剩餘流程；不調高生成限制或偷偷送第六次請求。 |
| BUILD-061 | PlanProjection 的實際及保守容量須涵蓋 requirements_version、task 變體及 v2 每項完成 fact ID；舊 Validation 預留零個完成 ID 的算法不能套用 v2。使用全部允許 block reason／固定值的實際最大 bytes 計量，保留取消、確認及自動受阻餘裕；不得寫入後才發現無法保存事實。 |
| BUILD-062 | 未開始的預留可釋放；退役任務／舊版本仍計 256 筆，換計畫不能重置。普通程序成功／失敗不清除安全既有證據，/clear／安全重設／換 Key 沿原語意處理。Unconfirmed 的 session 停止鎖定不得被 clear 或換 Key 解除。 |
| BUILD-063 | 程序工具、核准、進度與 parser 不自行呼叫模型或記 token。後續安全成功結果可沿原額度交回模型；每個返回 LlmResult 仍只由 Runner 記帳一次。失敗停止不另生成修復方案；長修正循環、摘要、總金額及可調資源另屬 FUT-04。 |

## 11. 錯誤、停止與生命週期

事前錯誤沿既有 invalid_arguments、invalid_path、outside_workspace、sensitive_path、symbolic_link、wrong_entry_type、path_changed、workspace_unavailable、mode_restricted、plan_not_approved、task_required、task_scope_mismatch、evidence_limit、plan_too_large、sensitive_arguments／sensitive_content。新增下列固定碼，不加入公開 LlmErrorCode：

| error_code | ToolStatus／結果及後續 |
|---|---|
| capability_unavailable | Rejected；不支援平台／recipe／隔離或無法成立必要限制，零目標執行，停止本回合。探針已開始時仍依真實啟動階段保留安全事實。 |
| unsupported_project_layout | Rejected；非標準輸出、套件根或所需外部資源，零目標執行。 |
| unsafe_output_path／protected_runtime_path | Rejected；別名／連結／metadata 不安全，或寫入集合與產品 runtime 相交，零啟動。 |
| process_limit | Rejected；本回合兩次啟動或時間額度耗盡，零該次啟動。 |
| process_start_failed | Failed；目標未成功啟動，StartFailed，退出碼 null；必要收尾完成才可回輸入。 |
| process_failed | Failed；正常 Exited／非零，或已確認的被信號終止等非成功；後者 EndState=Failed、退出碼 null。 |
| process_timed_out | Failed；TimedOut、退出碼 null；先完成停止／收尾，再回輸入。 |
| process_output_limit | Failed；OutputLimit、退出碼 null；同樣先停止／收尾。已觀測程序身分超限亦以此資源停止類別處理，CLI 明示為程序數限制。 |
| process_io_failure／process_output_invalid | Failed；輸出 I/O／控制通道格式錯誤或非法 UTF-8，無成功驗證。控制通道無法確認目標狀態時優先轉 Unconfirmed。 |
| validation_inconclusive | Failed；正常 Exited／0 仍缺有效測試證據，保留實際結果但不完成任務。 |
| process_unconfirmed／process_cleanup_failed | Failed；Unconfirmed，退出碼 null、validation=not_verified。後者另有 ProcessCleanupFailed；session 鎖定並退出 1，固定提示需使用者人工處理，不能宣稱沒有殘留程序。 |
| approval_declined／turn_cancelled | Rejected 或 Cancelled；分別沿既有核准拒絕及取消語意，停止目前流程；未啟動不偽造 StartFailed。 |

| 編號 | 收尾及映射要求 |
|---|---|
| BUILD-064 | Runner 新增 ProcessFailed、ProcessUnconfirmed、ProcessResourceLimit 停止原因。程序能力／布局／路徑／核准／資源事前拒絕為 Stopped；實際非零、啟動／輸出失敗、逾時與證據不足為 Failed；使用者取消為 Cancelled。Unconfirmed／收尾失敗與既有不可恢復本機失敗優先，不能被同時取消或較輕原因蓋過。 |
| BUILD-065 | 任務受阻新增 process_start_failed、process_failed、process_timed_out、process_output_limit、validation_inconclusive、process_unconfirmed；核准拒絕／取消／資源／模式與能力不足使用原對應原因。CleanupFailed 以 process_unconfirmed 封鎖；實際程序摘要保留詳細受控分類，不塞入 summary。 |
| BUILD-066 | 新增程序工具不屬 IsWorkspaceTool 的既有檔案分類；Runner 改用明確「可關聯任務操作」能力判斷及程序 StopAfterOperation，確保無計畫程序失敗也停止。整批敏感／協定、取消、第五次生成、模式預檢的既有優先順序保持；第五次不得啟動探針／核准／程序。 |
| BUILD-067 | clear、換 Key、退出／Dispose 必須先取消並等待自有程序必要收尾，再安全回報結果、清除引用與原有資源；遲到事件只能丟棄，不能復活計畫或核准。敏感故障走共同重設並保留目前模式；Unconfirmed／收尾失敗先鎖定退出，不開新 Key 讀取。 |
| BUILD-068 | 原始輸出不進任務帳本或產品日誌；通過檢查的工具節錄僅可進既有工具鏈／成功回合，沿五回合及 clear 規則釋放。所有錯誤與顯示使用固定文字，不傾印 argv／環境／例外／Key；短 Key 命中固定欄名或列舉亦不得豁免。 |

## 12. FUT-03／FUT-04 交接對照

### 12.1 FUT02-HO-001～010 承接

本表承接第 09 份固定交接，不另建任務狀態、核准或證據來源。兩份規格同時適用時，第 09 份仍是原契約來源，本份只對下表明示擴充生效；後續產品實作須同步更新來源敘述及驗收。

| 交接編號 | 本份章節／需求 | 後續驗收 |
|---|---|---|
| FUT02-HO-001 | 第 2、4、8、11 節；BUILD-008、012、017、042、066 | BUILD-AC-002、003、027 |
| FUT02-HO-002 | 第 3、7 節；BUILD-013～014、034、038～041 | BUILD-AC-006～009、022 |
| FUT02-HO-003 | 第 4～5 節；BUILD-010、017～019、023～027 | BUILD-AC-001、004、005、010～013 |
| FUT02-HO-004 | 第 3、8、11 節；BUILD-014～016、022、046～049、064～065 | BUILD-AC-014、015、020～023 |
| FUT02-HO-005 | 第 5、9 節；BUILD-027～029、056～059 | BUILD-AC-011～018 |
| FUT02-HO-006 | 第 3、9～10 節；BUILD-015、021、035～036、051～055、068 | BUILD-AC-016～019、024～026、032 |
| FUT02-HO-007 | 第 6、8 節；BUILD-030～037、042～043 | BUILD-AC-020～022、028、029 |
| FUT02-HO-008 | 第 8、11 節；BUILD-046～050、064～067 | BUILD-AC-020～023、030、031 |
| FUT02-HO-009 | 第 8、10 節；BUILD-044～045、060～063、066 | BUILD-AC-023、027、032～034 |
| FUT02-HO-010 | 第 3、13 節；BUILD-012～016、026～027、069～071 | BUILD-AC-002、010～015、020～035 |

### 12.2 第 09 份受影響契約與遷移

| 既有需求／契約 | 本份明示擴充與後續同步修訂 |
|---|---|
| PLAN-003～004、007～008、044～045、067 | 增加 macOS 程序能力、內部監督器與 Execute 第九項可用工具；Plan 六項與原七檔案工具保持。程序排除敘述改為第 09 份歷史界線並連至本份，不刪除原安全目的。 |
| PLAN-014、024～025、028、030～035 | 新增 requirements_version 及 v2 程序 requirement；保留嚴格解析、版本／身分精確比對。舊空 Validation 不自動啟用；定義改變須新 TaskId／Pending、重新確認。 |
| PLAN-036～041、043、046～048 | v2 Validation 可進行並以程序事實完成；工具關聯及最新相符結果擴充程序變體。原檔案證據及 Analysis 判斷不變，輸出節錄截斷與必要測試摘要分開處理。 |
| PLAN-038、050～052 | 程序操作前預留事實／投影容量；保守投影涵蓋 v2 完成識別、變體欄位與最長原因，沿用 256／32768／65536，不另建無界帳本。 |
| PLAN-049、056～060、062、066 | 程序啟動權、快照失效、取消／收尾與新增受阻／停止原因；Unconfirmed 鎖定 session，無法以 clear／換 Key 恢復。既有寫入 Unconfirmed 與 CleanupFailed 優先語意不降級。 |
| PLAN-068～069、PLAN-AC-016、031～035 | 保留固定交接錨點及對照；產品實作時更新「Validation 尚無能力」及工具集合預期，新增本份正向程序／隔離驗收。不得刪除假造結果不能注入證據的回歸；真人／服務與發布證據仍分層。 |

計畫、核准及證據資料仍僅存本程序記憶體；建置產物與私人暫存依第 6 節保留，不是工作脈絡存檔。沒有任務存檔格式遷移、跨次核准恢復或 SDK 歷史序列化。requirements_version 是程序內／模型資料契約版本，不是 FUT-08 保存格式。

### 12.3 FUT-04 邊界

沿用第 09 份 FUT04-HO-001～008：PlanStore 仍是本機事實來源，歷史摘要不能替代證據或核准；保留完整工具配對、簽章、實際 body 檢查、唯一記帳、耗盡受阻及安全清除。本份只增加固定程序時間／輸出／次數上限，不開發摘要、可調總預算、計費、跨次保存或多 Agent。日後擴大修正循環前須另訂 FUT-04 規格，不以調高本份數字取代設計。

## 13. 後續產品驗收

### 13.1 方法與證據層級

| 編號 | 驗收要求 |
|---|---|
| BUILD-069 | 使用合成 Key、假終端機、可控制時鐘／程序／檔案系統及真實 SDK 配假 HTTP handler；核對實際核准次數、argv／環境、零啟動、狀態／事實、byte 邊界及原生配對，不連 Gemini。真實程序 fixtures 使用隔離可信小專案與私人暫存，不讀真實認證。 |
| BUILD-070 | 假執行器不構成原生 sandbox／程序收尾證據；必須在宣告支援的平台執行真實允許／拒絕、別名及後代 fixtures，記錄平台、當次基線、命令、退出碼及未支援範圍。自動測試不得使用遞迴清理；資料保留。 |
| BUILD-071 | 離線產品完成至少需 BUILD-AC-001～034 及既有回歸通過；PTY 另需 035、真人另需 036、真實 Gemini 另需 037。第 07 份發布／分發另計。測試方法採 acceptance-linked 名稱，例如 BUILDAC002_PlanRejectsProcessAtEveryBoundary。 |

### 13.2 離線產品案例

| 案例 | 對應需求 | 必須驗證的結果 |
|---|---|---|
| BUILD-AC-001 | BUILD-001～012、017～020 | 無計畫 Execute 可核准兩種固定任務，明確 .csproj／Debug／cwd，保留原工具順序、help 與聊天；無自由 shell、參數、設定或新認證。 |
| BUILD-AC-002 | BUILD-008、012、017、030、042、066 | Plan 的 Definitions、偽造整批、Registry／服務直接入口均拒絕程序，零專案 metadata／內容 I/O、暫存、預覽、核准、探針與啟動。未指定能力也禁止。 |
| BUILD-AC-003 | BUILD-008、017、066、068 | 批次後段含合成 Key 時前段零 I/O／狀態更新；混合合法與模式禁止工具整批不執行，逐呼叫結果及優先順序正確。 |
| BUILD-AC-004 | BUILD-017～019、023 | 嚴格 Schema、額外／重複／大小寫／null／錯型別／非法 Unicode、4096／4097 path、目錄／solution／DLL／glob、越界／敏感／連結均原子拒絕，不執行專案。 |
| BUILD-AC-005 | BUILD-017～020、030～036、059 | argv／受控環境完全符合固定 recipe；以 launchSettings.json 含額外環境、filter／診斷參數的 fixture 確認兩者都未套用，不能只測停用 profile arguments。核准前不評估 MSBuild／發現測試。SDK／global.json／MTP 不符、restore state 缺失、非標準套件根、網路／互動依賴均受控失敗且不補安裝或切 runner。 |
| BUILD-AC-006 | BUILD-034、038～041 | 完整安全預覽含真實權限、輸出與原生限制；新輸入 y／yes 才授權，計畫確認／模型 yes／預貼 yes 不授權；完整預覽上限及加一拒絕，不裁切核准。 |
| BUILD-AC-007 | BUILD-038～041 | n／空白／無效選擇、Ctrl+C 取消回合、EOF 正常退出、I/O 退出 1，單一隱藏讀取者及 busy 緩衝丟棄正確；拒絕零目標啟動。 |
| BUILD-AC-008 | BUILD-016、034、040～041、067 | 過期 session／PlanId／Revision／TaskId／清除世代、遲到核准及替換 runtime／專案 bytes／輸出身分不啟動；新候選不能沿用舊同意。 |
| BUILD-AC-009 | BUILD-013～014、038～041 | 專用控制通道與目標 stdout 分離；假造 Started／退出碼文字不能回執。wrapper 成功但目標啟動失敗不記 Started；控制通道斷線不猜成功。 |
| BUILD-AC-010 | BUILD-023～026、061 | v1 空條件仍受阻；v2 為 1～8 項，空／9項／重複／帶行號／錯工具／非法版本拒絕。原三種任務保持；v1→v2 新 TaskId、版本／Draft、重新確認，投影帶正確版本。 |
| BUILD-AC-011 | BUILD-010、024～027、042 | 有計畫但未確認、無在途 Validation、錯 task／project、Write／Analysis 或舊版本均零啟動；正確 InProgress 才可預留及執行，無計畫仍逐次核准。 |
| BUILD-AC-012 | BUILD-026～029、056～059 | build＋test 缺任一項不完成；全部不同且最新相符安全事實才完成，舊／他任務／模型證據不能拼湊，定義不變的保留身分才可沿用舊事實。 |
| BUILD-AC-013 | BUILD-016、026～029、050 | 後來失敗不能被較早成功掩蓋；Started／Exited／0 但收尾未確認或驗證不足不得完成；重複 completed／完成重開仍按第 09 份拒絕。 |
| BUILD-AC-014 | BUILD-014～016、021～022、046～049 | NotStarted、StartFailed、Started→Exited／0或非零、信號終止、Cancelled、TimedOut、OutputLimit、Failed、Unconfirmed 映射及退出碼 null 規則；控制回執由本機來源產生。 |
| BUILD-AC-015 | BUILD-021～022、027～029、056 | 真實小專案建置成功／語法錯誤與必要收尾；成功只描述 Debug 目標建置，不能宣稱測試。只提議命令、舊輸出或模型成功文字不能建立事實。 |
| BUILD-AC-016 | BUILD-028、051～059 | 真實 MTP 通過、失敗、零測試、全略過與部分略過；只有完整安全摘要＋正常 0＋實際執行>0＋零失敗可通過，Skipped 明示。不得使用不存在的 TRX extension。 |
| BUILD-AC-017 | BUILD-051、056～059 | 摘要缺欄／重複／矛盾／溢位／錯語系／未知行／多摘要／過長／截斷、只有模組或 discovery 文字，以及多 module／多目標框架摘要，即使退出 0 也不完成；支援版本真實 fixture 必須符合指定 parser。 |
| BUILD-AC-018 | BUILD-021～022、028、055～059 | 一般節錄截斷但合格完整摘要已取得可保留測試計數；必要摘要未取得則 inconclusive。增量建置、非零、忽略退出碼或偽成功文字不能越過判準；不假造完整報告。 |
| BUILD-AC-019 | BUILD-035～036、051～055、068 | 合成 Key 跨 read 區塊／保留窗／截斷尾端／控制字元／欄位拼接，以及非法 UTF-8、ANSI／OSC／退格／CR；先停止及收尾再安全回報，無原文發布或落地。 |
| BUILD-AC-020 | BUILD-030～036、069～070 | 真實 sandbox 允許正常 SDK／套件讀取、bin／obj／私人暫存及必要 Unix IPC；核對短暫存根、排他建立／身分／mode／碰撞與 Unix socket 路徑邊界，超長不改用 TCP。拒絕來源覆寫／刪除、工作區外與敏感讀寫、Keychain／父程序存取、IPv4／IPv6／DNS／loopback TCP。探針失敗零目標執行，無裸跑。 |
| BUILD-AC-021 | BUILD-031～034、037、070 | 真實既有／新建 symlink、hard-link、來源 link 至輸出、bin／obj／祖先 rename／交換、大小寫／Unicode 路徑、runtime 相交及掃描上限。不得藉可寫別名修改來源／敏感 inode；失敗即拒絕能力，不擴大權限。 |
| BUILD-AC-022 | BUILD-013～014、034、036、046～048、070 | 真實一般子程序／孫程序、根先退出但後代存活、持有 pipe、忽略 SIGTERM、已知程序換群組、監督器失敗／身分追蹤超限；期限內收尾或 Unconfirmed 退出 1。另列 setsid／double-fork 的不支援界線，不能以未觀測到宣稱完整惡意隔離。 |
| BUILD-AC-023 | BUILD-041、044～050、060、064～067 | 可控制時鐘及必要真實逾時：120／180／累計300秒、兩次／第三次、啟動失敗占次數、核准等待不計、剩餘時間縮短、2／3／1秒分段且不重置；取消各決策點／遲到取消與 Unconfirmed 優先正確。 |
| BUILD-AC-024 | BUILD-051～055 | 合計輸出 1048575／1048576／1048577 bytes、兩 stream 同時大量輸出無死鎖、16383／16384／16385 保留窗、UTF-8 跨塊及8192摘要行邊界；超限真正停止，無無界排空。 |
| BUILD-AC-025 | BUILD-021、051～055、068 | JSON 65535／65536／65537 bytes 含控制跳脫／Unicode／metadata；按固定順序縮減節錄且保留事實，固定部超限或敏感時 Response=null，不把失敗變成功。 |
| BUILD-AC-026 | BUILD-015～016、021、026、049、055、068 | 程序已完成後 Response／顯示失敗，WithoutResponse 與本機紀錄仍保留安全實際事實；不重跑，輸出原文不入帳本／日誌，短 Key 命中固定欄名亦阻擋。 |
| BUILD-AC-027 | BUILD-008、042、060～063、066 | 第五次要求程序或與 update_task 混批，整批 generation_limit、零更新／核准／探針／啟動、無第六次生成；成功流程可於原額度內順序續接，無背景／平行程序。 |
| BUILD-AC-028 | BUILD-023～029、042～043 | 程序前完整 read_file 快照全部失效；開始失敗／取消／結束／收尾後亦失效，且不自我取消、不重設 Plan；後續修改須重新完整讀檔，程序輸出／局部讀取不建快照。 |
| BUILD-AC-029 | BUILD-030～037、042～043、067 | 共用排他租約、根及輸出身分變動、直接入口、Clear／Dispose 競態、遲到世代與 handle 關閉；零第二通道／讀取者，不重開已清除操作，不執行批次清理。 |
| BUILD-AC-030 | BUILD-026～029、048～050、064～067 | 先 A Verified 再程序拒絕／失敗／逾時：A 保留，Validation 受阻、剩餘未執行，零修復／重試／復原生成。一般失敗下一則明確指示需重新開始與核准；Unconfirmed 不能繼續。 |
| BUILD-AC-031 | BUILD-015、049～050、062、064～068 | 模型後續失敗、取消／截斷、清除、換 Key、退出及清理故障的優先順序；先回報再清除，已接受完成不撤銷，舊回執不復活，停止鎖定不能靠 clear／換 Key 解除。 |
| BUILD-AC-032 | BUILD-023～029、060～063、068 | 255→256筆及下一筆拒絕；20任務、8條件、v2多完成ID，實際／保守投影32767／32768／32769、核准／tasks65535／65536／65537；尚未啟動即取消仍可安全受阻，不丟必要事實。 |
| BUILD-AC-033 | BUILD-007～008、015、021、060～063、066 | 真實 SDK 假 handler：Execute九項／Plan六項、工具 Schema、原生 CallId／PartIndex／簽章、多結果配對及含程序結果／歷史／投影的實際 body262144／262145；超限不傳輸、不裁資料，程序事實保留。 |
| BUILD-AC-034 | BUILD-002～011、037、063、068～071 | 既有01～09／Python回歸通過，只按明示契約更新預期；沒有新產品參數／設定／NuGet／認證／其他FUT或批次刪除。用量缺值／溢位／唯一記帳保持，程序時間不冒充模型或計費預算。 |

### 13.3 PTY、真人與真實 Gemini

| 案例 | 對應需求 | 驗收方式 |
|---|---|---|
| BUILD-AC-035 | BUILD-038～050、053、064～071 | 離線 macOS PTY：完整程序預覽、隱藏同意／拒絕／空白／無效／預貼 yes、Ctrl+C／EOF、busy 丟棄、5秒進度、取消收尾、Unconfirmed 退出及回顯恢復；計畫確認後仍另核准程序。 |
| BUILD-AC-036 | BUILD-001、006、038～050、053、071 | 真人用合成資料確認中文／長路徑、信任及原生限制可辨識、完整權限、拒絕與取消、結果／略過／截斷、來源未改、產物保留與退出；不以 PTY 或 GUI 自動操作代替。 |
| BUILD-AC-037 | BUILD-010、023～029、038～050、056～063、071 | 使用者隱藏輸入真實 Key，以小型可信已還原工作區完成 Plan探索→v2 Validation→計畫確認→下一訊息Execute→逐次核准建置／測試→本機完成依據；另驗無計畫、失敗停止及下一回合明確繼續。只記安全結果，不讀取或保存真實 Key。 |

後續產品實作才依序執行原離線命令，另加入本份真實 sandbox／程序 fixtures；每步記錄當次環境、基線、命令、退出碼、實際通過／失敗／略過數及未驗證範圍。以下命令是維護者驗收產品的方法，不是 run_dotnet_task 額外允許的 recipe：

~~~sh
dotnet restore tests/AIAgent.Cli.Tests/AIAgent.Cli.Tests.csproj
dotnet build tests/AIAgent.Cli.Tests/AIAgent.Cli.Tests.csproj --no-restore
dotnet test --project tests/AIAgent.Cli.Tests/AIAgent.Cli.Tests.csproj --no-build
python3 -B -m unittest discover -s tests -p 'test_sensitive_content.py'
dotnet run --project src/AIAgent.Cli/AIAgent.Cli.csproj --no-build -- --help
dotnet format src/AIAgent.Cli/AIAgent.Cli.csproj --no-restore --verify-no-changes
python3 -B tests/terminal_smoke.py
git diff --check
~~~

本次撰寫規格不執行上述產品命令，不啟動 sandbox 探針、建置／測試程序、PTY 或真實 Gemini，也不新增驗收執行器／空殼測試。

## 14. 本次文件檢查與完成條件

| 編號 | 文件要求 |
|---|---|
| BUILD-072 | 需求／案例唯一連續，每項需求均有驗收對照；十項 FUT02-HO 逐項回填本份章節／需求／驗收，既有 PLAN 受影響契約、版本遷移及 FUT-04 責任明示，不以另一套任務或核准來源取代。 |
| BUILD-073 | 查核 JSON 範例可解析、Schema／required／additionalProperties／跨欄位及 v2 變體一致；宣告／欄位／列舉／recipe／錯誤／限制／完成判準相符，Markdown 區塊／表格、相對連結及交接錨點有效。 |
| BUILD-074 | 確認本次只新增本檔、其他檔案與索引不變，完整讀取未追蹤新檔及檢查空白。只回報實際文件查核；未執行的產品驗收明示未執行，不把文件、範例或假執行器當成功證據。 |

| 案例 | 對應需求 | 本次文件成功條件 |
|---|---|---|
| BUILD-AC-038 | BUILD-002、009、072～074 | 版本0.1／待實作、交付邊界、已選 .NET／macOS原生／可信專案、FUT02十項對照、v2遷移、介面／JSON／錯誤／時間／輸出／容量／判準及分層驗收一致；編號、連結、空白及檔案／索引邊界查核通過，零產品開發或驗收。 |

文件查核使用只讀、不中途落地的腳本解析編號、JSON、連結與區塊；Git 查核如下。未追蹤檔案須另逐檔讀取與執行 no-index 空白檢查，不能因一般 diff 未列出便宣稱已檢查：

~~~sh
git diff --check
git diff --no-index --check -- /dev/null specs/10-controlled-build-and-test.md
git status --short
git diff --name-only
git diff --cached --name-only
~~~

no-index 的退出碼須區分差異存在與空白錯誤，不單憑非零判為文件失敗；以實際診斷及內容檢查結果判定。後續開發從本份與第 09 份固定交接開始，先同步共用契約，再實作／驗收當前階段，不提前展開 FUT-04／08 或宣稱整體發布已完成。

## 參考資料

- [ROADMAP：未來功能大綱](../ROADMAP.md)：FUT-02 的目標、程序隔離要求及與 FUT-03／04 的分工。
- [第 09 份 FUT-02 固定交接](09-plan-mode-and-task-list.md#fut-02-handoff)：共用模式、核准、身分、證據、快照及停止契約。
- [第 09 份 FUT-04 固定交接](09-plan-mode-and-task-list.md#fut-04-handoff)：任務事實、Context、容量、用量與長任務擴充責任。
- [Microsoft：dotnet build](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-build)：固定建置參數、隱含 restore、建置伺服器與輸出；本份只採 .NET 10 支援部分。
- [Microsoft：dotnet test 與 MTP](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-test-mtp)：MTP 選取、project、no-restore、輸出及版本差異；不可將文件中的 .NET 11／12 選項直接套到首版。
- [Microsoft：MTP CLI options](https://learn.microsoft.com/en-us/dotnet/core/testing/microsoft-testing-platform-cli-options)：minimum-expected-tests、結果目錄、extension 與退出碼覆寫；本份仍另驗證實際摘要。
- [dotnet/sdk v10.0.100：MTP TestApplication](https://github.com/dotnet/sdk/blob/v10.0.100/src/Cli/dotnet/Commands/Test/MTP/TestApplication.cs)：核對該版預設 launch profile 的環境及 CommandLineArgs 套用，固定以 --no-launch-profile 停用。
- [Microsoft：Process.Kill](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.process.kill?view=net-10.0)：Kill 非同步，WaitForExit／HasExited 不表示所有後代已終止。
- 原生平台資料：撰寫時核對本機 sandbox-exec(1)、sandbox_init(3) man page 及 macOS SDK sys/event.h／sys/un.h；sandbox-exec／sandbox_init 已棄用，NOTE_TRACK／NOTE_TRACKERR／NOTE_CHILD 自 macOS 10.5 起不支援，不能用它們宣稱完整 fork 自動追蹤；sockaddr_un.sun_path 為 104 bytes。以上均為技術選型依據，真實隔離能力仍須後續按本份驗收。
