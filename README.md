# AIAgent

以 .NET 10 Console 分階段開發的 Gemini Agent。目前已實作子規格 01～06、08～09 的產品功能，以及第 07 份的整合測試、提交／產物掃描器與選用 hook；第 10 份已加入逐次核准的受控 Debug 建置與 Validation v2，MTP 測試不支援，完整驗收仍待完成。具備啟動與隱藏 Key、Gemini 用戶端、最近 5 個完整回合、用量累計、受限檔案工具、遞迴檔名／文字搜尋、指定行範圍讀取、單檔建立／精確替換、逐次核准，以及 Plan／Execute、版本確認與任務證據；第 11 份提供程序內摘要、原文備註與可調生成額度，預設回合5次／程序100次；第 12 份新增根 AGENTS.md 自動載入、明確目錄 Scope、完整來源呈現與規則失效閘門；第 13 份加入唯讀 Git 三層快照與專用程式碼審查；第 14 份加入隱藏多行編輯、安全預覽、明確貼上、程序內輸入歷史及階段進度；第 15 份提供私人目錄的工作摘要保存、單檔管理與重啟續接；第 16 份新增程序內修改檢查點與逐次確認的單檔復原。

離線自動測試、macOS PTY 與 VS Code F5 偵錯已通過；第 07 份本機 Release 掃描仍有 BUD-S003 阻擋，真人終端機、真實 Gemini 與人工分發檢查尚未驗證，第三階段整體驗收尚未完成。日期、基線與證據範圍見[最新驗證摘要](#latest-validation)。

開發依據為 [總規格](SPEC.md)、[01：啟動與 API Key](specs/01-bootstrap-and-key.md)、[02：Gemini 用戶端](specs/02-gemini-client.md)、[03：CLI 與 Context](specs/03-cli-and-context.md)、[04：工作區與唯讀工具](specs/04-workspace-and-read-tools.md)、[05：Agent 工具循環](specs/05-agent-loop.md)、[06：檔案編輯與核准](specs/06-file-edit-and-approval.md)、[07：預算與整體驗收](specs/07-budget-and-acceptance.md)、[08：程式碼搜尋與局部讀檔](specs/08-code-search-and-partial-read.md)、[09：Plan 模式與任務清單](specs/09-plan-mode-and-task-list.md) 與 [10：受控建置與測試](specs/10-controlled-build-and-test.md)。[11：Context 與預算管理](specs/11-context-and-budget-management.md) 接續相關Context與生成限制；[12：專案規則載入](specs/12-project-rules-loading.md) 接續來源、Scope 與操作重驗。[13：Git審查](specs/13-git-diff-and-code-review.md)與[14：終端機操作改善](specs/14-terminal-interaction.md)接續來源審查及輸入／讀取交接。[15：工作保存與續接](specs/15-work-save-and-resume.md)定義私人存取、歷史資料、確認與跨次來源。[16：修改檢查點與復原](specs/16-change-checkpoints-and-recovery.md)定義受控寫入捕捉、Git 衝突、單檔復原及證據失效。規格中的「本次只新增文件」描述撰寫當時的交付；目前已依開發要求實作，後續擴充以各規格的接續契約與目前能力補充為準。

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
| 09：Plan 模式與任務清單 | 模式限制、版本確認、任務狀態、本機完成證據與有界投影。 | 已實作並接入正式聊天。 | 證據見第 09 份驗證紀錄。 | PLAN-AC-034 真人、035 真實 Gemini；第 07 份門檻保留。 |
| 10：受控建置與測試 | 固定 Debug 任務、逐次程序核准、原生限制與 Validation v2。 | 建置已接入；測試不支援。 | 離線回歸、真實建置 fixtures 與 PTY 通過，範圍見下方紀錄。 | MTP 與部分原生邊界待驗，第 10 階段未完整驗收；BUILD-AC-036 真人、037 真實 Gemini 及第 07 份門檻另計。 |
| 11：Context 與預算管理 | 摘要、原文備註、容量與可調生成額度。 | 已實作並接入正式聊天。 | 當次離線／PTY證據見下方紀錄。 | CTX-AC-034真人、035真實Gemini；其他階段未結門檻保留。 |
| 12：專案規則載入 | 根目錄自動載入、明確 Scope、來源顯示、重驗與失效收尾。 | 已實作並接入正式聊天／工具／程序。 | 當次證據見第 12 份驗證紀錄。 | RULE-AC-033 真人、034 真實 Gemini；既有原生與發布門檻保留。 |
| 13：Git 差異與程式碼審查 | 原始 bytes 三層快照、專用 GitRead、唯讀審查回合及本機報告驗證。 | 已實作並接入正式聊天。 | 分層證據見第 13 份驗證紀錄。 | GIT-AC-021 真人、022 真實 Gemini；既有原生／發布門檻保留。 |
| 14：終端機操作改善 | 隱藏多行編輯、安全預覽、明確貼上、程序內歷史及階段進度。 | 已實作並接入正式聊天。 | 當次分層證據見第 14 份紀錄。 | TERM-AC-030 真人、031 真實 Gemini；原生／發布未結門檻保持。 |
| 15：工作保存與續接 | 私人摘要／pins／歷史計畫、四個工作命令、啟動來源重驗及原子匯入。 | 已接入正式聊天。 | 當次結果見下方紀錄。 | 真人 SAVE-AC-030、真實 Gemini 031、其他 OS／架構及下列原生未驗範圍；既有發布門檻保持。 |

子規格 01～03 對應總規格第一階段「可以聊天」，04～05 對應第二階段「可以讀檔」，06～07 對應第三階段「可以修改」。正式登錄七項檔案工具（`list_files`、`read_file`、`create_file`、`replace_text`、`search_files`、`search_text`、`read_file_range`），再接 `submit_plan`、`update_task`、`run_dotnet_task`，再追加 `git_status`、`git_diff`、`read_git_file_range`，依目前模式提供九／十二項；每筆寫入及程序須預覽後明確核准，工作區工具不提供刪除；`/work delete ID` 僅是私人單一存檔的本機限定入口。第 07 份不新增模型工具或產品參數；上表的自動驗證不等於各階段所有人工／服務／發布門檻均已通過。

<a id="latest-validation"></a>
<a id="checkpoint-validation"></a>

## 第 16 份修改檢查點與復原

每次受控 `create_file`／`replace_text` 在預覽前預留容量，發布後保存完整 before／expected after bytes 及來源事實。資料只在目前程序記憶體；`/clear`、安全重設、換 Key、退出會丟失，重新啟動或 `--resume` 不會帶回。讀檔快照、Git HEAD／index、Review 與工作摘要均不能代替復原來源。

| 本機入口 | 行為 |
|---|---|
| `/checkpoint list` | 依 ID 降冪列出程序內歷史、狀態與容量；不讀目標檔、不代表目前仍可復原。 |
| `/checkpoint show c1` | 完整顯示 before→after 原始差異；不是目前檔案或復原核准。 |
| `/checkpoint restore c1` | 僅 Execute／目前有效規則；重驗完整 bytes、inode、父目錄、mode 與目標 Git metadata，顯示目前→before 差異，再取得新隱藏確認。每次只恢復一檔。 |
| `/checkpoint forget c1` | 新確認後只移除指定記憶體紀錄；不讀寫或刪除工作檔，可能使較早鏈節只能人工處理。 |

命令大小寫敏感、參數間單一 ASCII 空格，ID 必須是 `c1` 至 `c9223372036854775807` 的正規十進位寫法；不接受 path、latest、多 ID 或多行。命令與確認不進輸入歷史，零模型生成。每次新預覽後重新輸入 `y`／`yes` 才同意，拒絕或取消不發布；預輸入會排空，不能辨識提示後所有延遲貼上的來源。

容量固定 32 筆、before＋after 共 8388608 bytes、每側 131072 bytes（含 BOM）；在途預留、人工及已復原紀錄都占容量。滿額停止新寫入及當前工具鏈，等待使用者 forget，不自動淘汰或略過保護。單次完整顯示 65536 UTF-8 bytes，超限整次拒絕。

只有已核對 Replace 可自動復原。新建空檔／非空檔都是 `created_file` 人工紀錄，請自行核對指定單檔是否刪除；產品沒有工作區刪除入口。來源未確認、Git 不可驗證、不支持布局／條目及已觀察衝突亦不強制覆寫。根無 `.git` 時只確認其仍缺席，不向父目錄查 Git；根 repository 僅用固定唯讀 recipes 比較 HEAD／目標 index／ITA，不取 blob、不改 index、HEAD 或 refs。

同檔必須由新到舊逐筆復原；Verified 才將 predecessor 綁到新 inode，不建立 redo。忘記中間鏈節、外部改寫或 Unconfirmed 會斷鏈，不能靠相同 bytes 自動接回。復原只涵蓋完整 before bytes／BOM 與普通 mode，不保證 owner／group、ACL、xattr、resource fork、時間戳、硬連結、所有 metadata、程序產物或外部副作用；原非 CAS rename／TOCTOU／斷電持久性限制保留。

復原一旦開始發布，先核對並保留 Verified／Unconfirmed／CleanupFailed 事實，再增加 RecoveryRevision／EvidenceEpoch、撤執行確認並進 Plan。較早 Read／Write／Validation 事實保留歷史，不能用於新完成判定；舊 Completed 明示未重新驗證。一般、Review、compaction 與保存摘要請求均必帶有界目前復原附錄（32768 bytes），量測／保存候選隨版本失效，SAVE v1 格式不變。復原 AGENTS.md 使用目前 Valid 規則，先記事實再 Stale，須明確 `/rules reload`、重新確認及新指示。

### 第 16 份驗證紀錄（2026-10-27）

實作起點乾淨 `main@7a9925f`。macOS arm64／.NET SDK 10.0.100；測試離線，使用合成憑證及隔離工作區。原受限環境基線 838 項中 13 項原生沙箱案例因 `sandbox_apply: Operation not permitted` 失敗；此環境限制與最終可用環境驗證分列。最終自動驗證使用相同產品程式；全套 944 項通過後另補 3 項來源／衝突回歸及 1 項真實 Git 復原整合，合計 948 個不同 MSTest 執行案例通過。沒有以受限環境失敗冒充產品通過。

| 實際命令／查核 | 結果 |
|---|---|
| `dotnet restore tests/AIAgent.Cli.Tests/AIAgent.Cli.Tests.csproj` | 0；固定相依套件，沒有新增套件。 |
| `dotnet build tests/AIAgent.Cli.Tests/AIAgent.Cli.Tests.csproj --no-restore` | 0；CLI／ProcessHost／TerminalHost／測試建置完成，0 警告、0 錯誤。 |
| `dotnet test --project tests/AIAgent.Cli.Tests/AIAgent.Cli.Tests.csproj --no-build` | 0；944／944，0 略過，3 分 03.119 秒；在允許 nested sandbox 的執行環境完成，產品原生政策未放寬。 |
| 補充 `--filter`：Unconfirmed 原寫入、核准後根 `.git` 出現（原寫入／復原） | 0；3／3，核對人工紀錄容量、零非法發布及斷鏈。 |
| 補充 `--filter FullyQualifiedName~CKPTAC013_NativeCheckpointRestoresDirtyBeforeBytesAndPreservesIndexAndHead` | 0；1／1，1 分 00.508 秒；隔離根 repository 經真實受控寫入及復原，恢復含 BOM／CRLF／中文的 dirty before，index／HEAD bytes 不變。 |
| `dotnet run --project src/AIAgent.Cli/AIAgent.Cli.csproj --no-build -- --help` | 0；新增 checkpoint 操作／保留／人工限制說明。 |
| `python3 -B -m unittest discover -s tests -p 'test_sensitive_content.py'` | 0；36／36。 |
| `python3 -B tests/terminal_smoke.py` | 0；122 組，含新增 10 組 checkpoint；原有 112 組回歸保留。 |
| `dotnet format src/AIAgent.Cli/AIAgent.Cli.csproj --no-restore --verify-no-changes`；變更測試／TerminalHost 格式 | 0；產品完整 verify，6 份變更測試／Host 另做指定格式檢查。 |
| 差異／來源敏感掃描 | `git diff --check` 通過；307 份來源複製到隔離 Git fixture 後以 `scripts/check_sensitive_content.py staged` 掃描，0 findings；原工作區索引未 stage。此項不是 Release 產物掃描。 |

離線證據分工：`CheckpointWorkspaceTests` 驗來源／容量／單檔發布、反向鏈、衝突與故障／清除；`CheckpointGitTests` 驗有限目標 metadata、變更偵測、原生 SHA1／SHA256／ITA 及 index 不變；`CheckpointStateTests` 驗舊事實 epoch、Completed 歷史、必備附錄與候選失效；`CheckpointChatTests` 驗嚴格本機入口與新確認；`CheckpointSdkCapacityTests` 的 9 項案例驗實際 SDK body 262144 可接受、262145 在生成／HTTP 前拒絕，以及單一操作、取消、釋放與敏感邊界。容量預檢沿相同 SDK 序列化，但固定在傳輸處理常式內停止，不呼叫 HTTP handler、不記 API 用量或生成；保守估算仍與實際 body 分列。

獨立程式碼審查的容量阻塞原因、根失效資格及非法／過大來源重新接鏈問題均已用失敗測試重現，再修正通過。C# CRLF／檔尾、Markdown 圍欄與本機連結另外查核；沒有把 PTY 視為真人或真實服務證據。

真人 CKPT-AC-031、真實 Gemini CKPT-AC-032、其他 OS／架構未驗；沒有讀取真實 Key 或呼叫 Gemini。第 10 份產品 MTP `test` 不支持、第 07 份 BUD-S003 Release／人工分發及既有原生驗收缺口保留，不以此次結果解除。變更未提交、推送或發布。

<a id="work-save-validation"></a>

## 第 15 份工作保存與續接

保存的是可檢視且可能過時的工作背景。檔案為 OS 家目錄 `.aiagent/work/ID.json` 的明文，應用目錄須 0700，檔案／鎖須 0600 且無 extended ACL；不安全的既有權限不會自動修復。同 UID 程序、管理員、備份或實體磁碟仍可能取得內容；SHA-256 只比較變動，不是簽章、加密或防竄改認證。

| 入口 | 行為 |
|---|---|
| `/work save TITLE` | 保留標題原文，1～80 UTF-16 單位。必要時以同 client 生成一次摘要，包含最新成功回合；完整預覽後另輸入 y／yes 才建立新 ID。未新增回合時重用有效摘要，僅 pins／plan 也可零生成保存。 |
| `/work list` | 列出 ID、標題、UTC、工作區及 Scope；零生成，壞檔或不安全項使整次拒絕。 |
| `/work show ID` | 完整顯示未驗證歷史；不載入、不核准、不查目前檔案，零生成。 |
| `/work delete ID` | 預覽後新確認，再比對 bytes／身分，只刪指定單一存檔；不清除目前工作。 |
| `--resume ID` | 新程序確認工作區、隱藏輸入新 Key 後續接；可與 `--workspace DIR` 任意順序各一次。ID 必須精確為 32 個小寫十六進位字元。 |

摘要可能有損，必留原文請另 pin。保存候選完整顯示明文意義、所有欄位與生成／可得用量；拒絕確認也不退已開始生成次數。沒有自動保存、latest、任意路徑、聊天內 load、同步、加密或檔案復原。100 份存檔滿時不自動輪替或清理；每次只接受一個 ID。

續接先精確匹配 root／Device／Identity，再讀目前根規則；Scope hint 另取明確確認。規則與檔案差異完整列出，所有歷史狀態仍未驗證；通過最終重驗後摘要、原文 pins 與計畫一次匯入。只有非 Completed 定義重建新 ID／Revision 1／Draft／Pending，沒有舊證據或核准；全完成也進 Plan，`/execute` 另確認後仍等新一般訊息。只有一份有效摘要，立即再保存及首次 compaction 保留歷史來源，本程序回合從零計。

`/clear`／安全重設／換 Key 清除載入背景、候選、查核與原有記憶體，不刪存檔、不退款；單檔 delete 不清記憶體。發布後取消仍須核對結果；PublishedUnconfirmed／DeleteUnconfirmed／自有暫存或關閉收尾不明會停用本程序儲存操作，clear／換 Key 不能解除，亦不補寫或補刪。一般工具、搜尋、規則、Git 與受控 .NET 都拒絕私人資料，即使工作區直接指向私人根。

### 第 15 份驗證紀錄（2026-10-26）

實作起點 `606d971`。工作期間另有程序建立 `57db2dc`、`825a905`，本次保留它們並繼續未提交變更，沒有由本次工具提交、推送或發布。環境 macOS arm64／.NET SDK 10.0.100；全部自動測試離線，使用合成資料與真實 SDK 配假 HTTP。

| 實際命令／查核 | 結果 |
|---|---|
| `dotnet restore tests/AIAgent.Cli.Tests/AIAgent.Cli.Tests.csproj` | 0；固定依賴還原完成，沒有新增套件。 |
| `dotnet build tests/AIAgent.Cli.Tests/AIAgent.Cli.Tests.csproj --no-restore` | 0；CLI／ProcessHost／TerminalHost／測試建置完成，0 警告、0 錯誤。 |
| `dotnet test --project tests/AIAgent.Cli.Tests/AIAgent.Cli.Tests.csproj --no-build` | 0；838 通過、0 失敗、0 略過，含既有 767 與新增 71 個執行案例；最終 1 分 21.232 秒。 |
| `python3 -B -m unittest discover -s tests -p 'test_sensitive_content.py'` | 0；36 通過，含原 33 與新增 3 個私人路徑／忽略案例。 |
| `python3 -B tests/terminal_smoke.py` | 0；112 組通過，含原 99 與新增 13 組 SAVE-AC-029；使用離線記憶體 store，原生儲存證據另列。 |
| `dotnet run --project src/AIAgent.Cli/AIAgent.Cli.csproj --no-build -- --help` | 0；新增工作命令及啟動選項說明，零 Key／client／存檔 I/O 的回歸保留。 |
| `dotnet format src/AIAgent.Cli/AIAgent.Cli.csproj --no-restore --verify-no-changes`、變更測試／TerminalHost 格式 verify | 0；產品及選定變更檔格式通過，不擴大改寫舊測試。 |
| CRLF／C# 檔尾、Markdown 連結／圍欄、JSON、差異與敏感掃描 | 44 份變更 C#、17 份 Markdown／349 個本機連結及 6 個 JSON 區塊通過；290 檔來源複製至隔離 Git fixture 後，`scripts/check_sensitive_content.py staged` 為 0 findings。原工作區索引沒有由本次工具改動；此項不是 Release 產物掃描。 |

變更前受限外層環境的基線為 755／767，12 個既有原生案例因 `sandbox_apply: Operation not permitted` 失敗。最終允許外層測試程序初始化 macOS 原生 sandbox，產品子程序維持原隔離，838 項完整回歸通過。真實原生案例涵蓋 UID／mode／ACL、無跟隨與連結／特殊檔拒絕、額度／列舉、跨程序鎖、排他發布與精確讀回、單檔刪除，以及 Git／.NET 子程序私人內容拒絕。內部測試注入另驗證 flush 前失敗、發布後讀回／關閉失敗、自有暫存清理失敗、發布前後取消及刪除後同名重現，核對已知結果與 StoreStopped；注入不等同實際磁碟／核心故障。

獨立唯讀程式碼審閱指出啟動忙碌時 EOF 被升級為退出 130，以及零生成的敏感目前來源未要求共同重設；兩項皆先以回歸重現，再修正並納入最終完整測試。真實原生探針另發現 sandbox regex 的 `{32}` 不符合預期比對，改為明確展開並分拆長 regex；私人檔案讀寫拒絕及公開對照實測通過。後續自查亦以失敗回歸修正缺值 API 用量呈現與 unavailable 檔案的身分重驗；沒有另行獨立複審。

SAVE-AC-030 真人 macOS、031 真實 Gemini 均未執行；其他 OS／x64、非本機檔案系統、真實不同帳戶 UID 的敵對競爭、硬體斷電與核心 close／flush 故障尚未實驗。不得把本次測試數視為上述條件全部驗收。既有第 10 份 MTP／原生未結、第 07 份 BUD-S003 Release 與人工分發門檻保持；沒有使用真實 Key、呼叫 Gemini、推送或發布。

<a id="terminal-interaction-validation"></a>

## 第 14 份終端機操作改善

依 [第 14 份規格](specs/14-terminal-interaction.md) 提供一般訊息的隱藏編輯與完整安全預覽。Key、換 Key 選單、計畫／檔案／程序核准仍使用原隱藏單行輸入；核准須在完整預覽後另行輸入，不採用草稿或歷史中的 yes。

| 按鍵 | 一般輸入行為 |
|---|---|
| Enter | 提交整份草稿；CRLF只算一次。 |
| F2 | 顯示完整安全草稿快照、行號及邏輯游標；不送出。 |
| F3 | 進入／結束貼上收集；收集中換行只作內容。 |
| F4、Tab | 插入 LF／字面 Tab，保留縮排及首尾空白。 |
| Left／Right、Backspace／Delete | 依完整文字元素移動或前後刪除，含組合字與 emoji。 |
| Home／End、Up／Down | 在邏輯行定位；上下移動按實際抵達欄夾界，不估算螢幕顯示寬度。 |
| Ctrl+P／N | 回取上一／下一筆安全歷史；越過最新項恢復原草稿與游標。 |
| Ctrl+U | 清空草稿並結束歷史瀏覽；不解除該次無效旗標。 |
| Ctrl+C／SIGINT、Ctrl+D／EOF | 等待一般輸入時分別退出130／0，丟棄草稿；活動回合及核准沿原取消語意。 |

多行貼上操作：**F3 → 等待收集提示 → 使用終端機貼上 → 等待貼完 → F3 → 等待結束提示 → 可選F2 → 另按Enter送出**。未先F3的換行仍可能提交；不自動辨識貼上、不讀剪貼簿，也不以按鍵速度猜測來源。提示／預覽輸出期間已緩衝的普通輸入會丟棄，取消與EOF仍處理。ConsoleKeyInfo不能驗證實體按鍵來源或延遲尾段。macOS可能需要Fn＋功能鍵；真人IME與鍵盤組合須另驗。

草稿最大16384個UTF-16單位，包含Tab／LF；非法控制、代理配對或超限使該次輸入永久無效。預覽最大65536個嚴格UTF-8 bytes，含全部標籤、跳脫與提示；整段超限或顯示拼接敏感碰撞時拒絕預覽並保留隱藏稿。原稿含當前Key時清除草稿及歷史瀏覽還原副本，留在同一次輸入重新開始，不顯示原文。

歷史只收錄通過敏感與目前規則准入的一般訊息，最多20筆／65536個UTF-16單位；相鄰精確去重、整筆逐舊淘汰。普通生成失敗後仍可回取；命令、拒絕輸入、Key及核准答案不收錄。clear、安全重設、認證選單前、政策切換及退出清除；模式／Scope切換或規則重載保留安全歷史，送出時重新准入。它不加入Context、摘要、pins、計畫或Git帳本，沒有磁碟保存、匯出或回放授權。

本機指令只接受單行；含LF且去除開頭空白後以`/`起頭的整筆拒絕，包含結尾LF。一般文字後續行的`/exit`或yes保持普通內容。單行命令的精確匹配、字面Scope／pin餘文及短Key例外保持。

進度使用現有狀態與事件，區分模型生成、摘要維護、工具開始、等待核准、同意後重驗、取消收尾及本機結果。Review保留原來源側別／行號／過時／未覆蓋資訊；工具開始或零finding都不建立Verified／TestsPassed。沒有新增工具、套件、設定、生成或權限。

### 第 14 份驗證紀錄（2026-10-26）

開發基線為乾淨`main@91bb6e6`，macOS26.7.1 arm64、.NET SDK10.0.100。全部自動驗證只用合成Key、假模型／終端機或真實SDK配假HTTP，沒有真實Gemini；變更未提交、推送或發布。

| 命令／檢查 | 本次實際結果 |
|---|---|
| `dotnet restore tests/AIAgent.Cli.Tests/AIAgent.Cli.Tests.csproj` | 0；既有依賴完成還原，無新增套件。 |
| `dotnet build tests/AIAgent.Cli.Tests/AIAgent.Cli.Tests.csproj --no-restore` | 0；CLI、ProcessHost、TerminalHost與測試成功，0警告／0錯誤。 |
| `dotnet test --project tests/AIAgent.Cli.Tests/AIAgent.Cli.Tests.csproj --no-build` | 0；767通過、0失敗、0略過，包含基線714與新增53個執行案例；最終耗時1分22.261秒。 |
| `python3 -B -m unittest discover -s tests -p 'test_sensitive_content.py'` | 0；33通過。 |
| `python3 -B tests/terminal_smoke.py` | 0；99組通過，包含原86組及新增13組TERM-AC-029；不連Gemini。 |
| `dotnet run --project src/AIAgent.Cli/AIAgent.Cli.csproj --no-build -- --help` | 0；包含F2／F3／F4、歷史與單行指令說明，無互動或Key讀取。 |
| `dotnet format`產品、變更測試與TerminalHost的`--no-restore --verify-no-changes` | 皆為0；只針對變更測試檔檢查，不擴大改寫既有測試格式。 |
| 敏感掃描、文件連結／圍欄、CRLF／檔尾及差異檢查 | 0；23份變更C#的CRLF／無檔尾換行、13份Markdown／258個本機連結／圍欄、Python語法、`git diff --check`及8份新檔no-index空白檢查通過。全263份來源複製至獨立保留fixture，僅於fixture建立index；`python3 -B scripts/check_sensitive_content.py staged`為263檔／0 findings，原工作區index不變；此項非Release產物掃描。 |

基線在受限外層sandbox為702通過／12個既有原生能力失敗；允許外層測試程序初始化macOS原生sandbox後714／714通過，產品子程序仍保留原隔離。首次整合回歸760／761，唯一失敗是舊核准替身只辨認獨立`你> `提示；已改為完整提示後的啟用閘門，保留EOF／取消與零寫入斷言。新功能測試先觀察舊行為失敗再實作，另重現並修正清除途中按鍵／政策候選回填與認證提示控制訊號。

TERM-AC-001～028以新增TerminalEditor／Boundary／Core／Lifecycle測試及原有01～13回歸對照；PTY TERM-AC-029使用既有host與明確pipe／讀取啟用標記，覆蓋F2／F3／F4、文字元素、拆段CRLF、歷史、敏感預覽、指令閘門、預輸入與取消／EOF／SIGINT恢復。啟動用產品PTY案例以EOF收尾；命令操作由具啟用閘門的離線host驗證，不以提示已印出當作讀取已啟用。

TERM-AC-030真人macOS（中文IME、Fn功能鍵與實際終端機組合）、031真實Gemini均未執行；其他OS／x64未驗。第10份MTP／原生未結、第07份BUD-S003 Release掃描與人工分發門檻保持，不併入離線通過數。獨立只讀程式碼審查提出換Key提交結尾停止訊號及帶文字控制鍵NUL分類兩項；各以回歸測試重現失敗後修正，4個案例轉通過並納入767個完整回歸，未作第二輪獨立審查。規格第12節TERM-HO-001～011仍為單一交接來源，不表示其他FUT已交付。

<a id="git-review-validation"></a>

## 第 13 份 Git 差異與程式碼審查

依 [第 13 份規格](specs/13-git-diff-and-code-review.md) 提供提交前唯讀審查；工作區根本身須為普通 `.git` 目錄的非 bare repository，僅限已准入的 macOS／Git／runtime 布局。一般工作區工具仍禁止 `.git`。不提供 Git 寫入、任意分支比較、shell、復原或遠端 PR 操作。

| 指令／工具 | 行為 |
|---|---|
| `/review`、`/review all` | 捕捉目前 Scope，分別審查 staged、unstaged、untracked 的安全文字差異。 |
| `/review staged`、`/review unstaged`、`/review untracked` | 僅向模型提供指定層；同一路徑可同時存在 staged／unstaged 變更。 |
| `/review show` | 顯示最近成功報告，作有界來源重驗並標示過時；零新增生成。 |
| `git_status` | 一般回合捕捉新快照；審查回合只提供綁定快照的所選 manifest。 |
| `git_diff` | 取得指定 path／layer 的完整本機單一 hunk 與原始來源 metadata。 |
| `read_git_file_range` | 讀取該快照 old／new 的至多 200 行，包含 staged index／HEAD 來源，並非一律目前工作檔。 |

差異採**原始 bytes 視角**，不執行 Git filters、textconv、EOL 轉換或 rename 猜測；線性完整 hunk 可能包含中間相同行，不是可套用 patch。BOM、mode、空檔、衝突、intent-to-add、非文字／不支援來源分別呈現，不假造行號或乾淨工作區。忽略規則僅適用 untracked，來源限根至候選祖先 `.gitignore` 與 `.git/info/exclude`，不讀全域 excludes；新巢狀規則與 Scope 外正文不讀取，隱藏路徑及數量不顯示。

Review 共用原 Runner、client、生成額度及 Context 整理；只允許三項 GitRead 工具，混入寫入、建置、檔案或計畫工具時整批拒絕。來源只有確實隨安全請求派送後才列為提供；局部原文不抵完整差異。模型最終 JSON 經欄位、位置、敏感、容量與來源重驗，再共同發布報告及成功歷史；失敗不自動修 JSON 或重捕捉。零 finding 不表示無 bug／安全／測試通過，報告固定明示本回合未執行建置或測試。

最多保存一份 raw 快照及最近成功報告，均只在記憶體；Git 觀察不建立修改基準或 Plan 完成證據。新候選、HEAD／index／工作檔 bytes／mode、忽略來源、規則或世代改變即停止；show 的歷史報告另持無 raw bytes 的驗證 manifest。clear、換 Key、切 Scope、安全重設及釋放依各自契約丟棄或標過時；id 不重用，生成不退款。

原生執行使用固定 recipe、最小環境及每次探針，拒絕持久檔案寫入、網路、helper 與工作區外使用者資料。使用者明確批准 **僅 `/dev/null` 的 `file-write-data` 例外**，供 Git 啟動所需 O_RDWR null-sink；不開放建立、刪除、重新命名或 .NET 產物權限。`check-ignore` 的 NUL stdin 加固定 `./` 前綴以保留冒號／glob 字面檔名語意，該 recipe 不帶不相容的 literal-pathspecs 旗標，其餘 recipe 保持。能力不足拒絕啟動，無裸跑 fallback。

上限為單側 131072 raw bytes、保留來源含忽略資料 8388608 bytes、8192 觀察、200 不同變更路徑、根起深度32；工具／報告／顯示65536、審查附錄32768 bytes。生成預算與完整 SDK body 限制不提高。單命令30秒、捕捉／重驗60秒均含收尾，預留停止時間；未確認程序或收尾失敗仍嚴格停止並退出1。

### 第 13 份驗證紀錄（2026-10-26）

開發基線為乾淨 `main@9f1c9d9`；目前產品、測試與文件增量未提交。環境 macOS 26.7.1 arm64、.NET SDK 10.0.100；全部自動測試使用合成 Key、替身或真實 SDK 配假 HTTP，沒有真實 Gemini 呼叫。真實 Git fixtures 的準備提交與修改不計為產品執行；產品呼叫前後的工作檔及 Git metadata bytes 另行比較，fixtures 保留。

| 命令／檢查 | 本次實際結果 |
|---|---|
| `dotnet restore tests/AIAgent.Cli.Tests/AIAgent.Cli.Tests.csproj` | 0；固定依賴還原完成，沒有新增套件。 |
| `dotnet build tests/AIAgent.Cli.Tests/AIAgent.Cli.Tests.csproj --no-restore` | 0；CLI、ProcessHost、TerminalHost 及測試成功，0警告／0錯誤；格式修正後另行重建通過。 |
| `dotnet test --project tests/AIAgent.Cli.Tests/AIAgent.Cli.Tests.csproj --no-build` | 0；714通過、0失敗、0略過，包含既有627及新增87個執行案例；最終完整回歸耗時1分19.863秒。 |
| `python3 -B -m unittest discover -s tests -p 'test_sensitive_content.py'` | 0；33通過。 |
| `python3 -B tests/terminal_smoke.py` | 0；86組通過，包含新增8組 GIT-AC-020：有效／無文字審查，以及捕捉、模型等待、工具執行各自的Ctrl+C／EOF、預輸入丟棄與終端機恢復。 |
| `dotnet run --project src/AIAgent.Cli/AIAgent.Cli.csproj --no-build -- --help` | 0；包含 `/review` 說明，help 零 Key／client／來源 I/O 回歸保持。 |
| `dotnet format src/AIAgent.Cli/AIAgent.Cli.csproj --no-restore --verify-no-changes`、變更測試／ProcessHost whitespace verify | 0；產品與變更檔格式通過。完整測試專案格式檢查另發現既有未修改的LF檔案，未擴大改寫。 |
| CRLF／C#無檔尾換行、文件／JSON／連結與差異查核 | 0；54份變更C#、14份Markdown、234個本機連結、45個JSON區塊、程式碼圍欄、`git diff --check` 及新檔 no-index 空白查核通過。 |
| `python3 -B scripts/check_sensitive_content.py staged` | 0；目前原工作區index為227檔／0 findings；另將本次所有254份來源檔複製至獨立保留fixture，僅在fixture建立index，掃描254檔／0 findings。前者不代表未暫存增量已受掃描，後者涵蓋本次增量；兩者均非Release產物掃描。 |

離線需求回歸涵蓋三層互抵、unborn／detached HEAD、ITA、SHA-256、Scope／新規則邊界、忽略否定與協定錯誤、完整候選集合／raw指紋變動、非文字來源、精確工具輸入及禁止工具整批零執行。容量案例包含單側128KiB、保留來源8MiB、8192觀察、200路徑、深度32、200行、32KiB附錄、64KiB結果／顯示及實際SDK body；各自測試上限及超限。真實SDK配假HTTP另核對三工具往返、原生訊息／thought signature配對、實際派送帳本與finding發布，沒有真實服務請求。

真實Git／原生部分在本機arm64執行，GitAdapterTests／GitNativeTests的19個案例已納入上述714個完整回歸；其中8個真實原生案例覆蓋能力探針、repository前後bytes不變、binary／輸出超限、外部config include拒絕、三層、ITA／忽略否定、SHA-256及活躍Git取消／逾時。後兩項使用保留的loose-object FIFO使真實Git阻塞，核對自有程序身分及退出；逾時包含收尾少於30秒，取消收尾少於5秒。外層測試程序須允許初始化macOS sandbox，產品Git子程序仍使用專用限制；假程序／時鐘不抵上述原生證據。

獨立程式碼審查提出三項P2：直接Runner入口限制、最終敏感來源重設、未變blob的記憶體保留；均先重現失敗再修正並納入完整回歸，沒有宣稱修正後已作第二次獨立審查。後續亦補上raw blob／非文字指紋、捕捉總期限分類、忽略回覆解析，以及原生啟動前工作區／固定參數／環境值的當前Key碰撞檢查。

尚未強制重現真實子程序的描述管線故障或OS無法確認收尾；其時間預算由阻塞stream及可控時鐘回歸驗證。其他OS／x64布局、真人 GIT-AC-021、真實 Gemini GIT-AC-022未驗證；第10份MTP／原生待辦、第07份BUD-S003 Release與人工分發門檻保持。沒有提交、推送或發布，沒有實作FUT-09。

<a id="project-rules-validation"></a>

## 第 12 份專案規則載入

確認工作區及隱藏輸入 Key 後，先在本機載入根 `AGENTS.md`，初始 Scope 為 `.`；沒有規則是正式的空來源快照，載入失敗則拒絕一般生成、整理及 Execute 確認。規則可能傳送至 Gemini。只讀目前工作區根至明確選定目錄的祖先鏈，不探查家目錄或工作區外祖先，不展開引用、連結、Skills 或程式碼。

| 本機指令 | 行為 |
|---|---|
| `/rules` | 顯示 Scope、狀態、RulesRevision、來源相對路徑／範圍、原始 bytes、SHA-256 與完整投影 bytes。 |
| `/rules show` | 顯示來源資訊及每份完整安全文字；過時快照明標不適用，不能安全完整顯示時整段拒絕。 |
| `/rules scope DIR` | 原子載入根到 DIR 的完整鏈；DIR 為字面相對目錄，保留前後空白，不解析引號、變數或 `~`；`.` 返回根。 |
| `/rules reload` | 明確重載目前 Scope；Valid 且來源完全相同為 `no_change`，失效後恢復相同 bytes 仍發布新版本。 |

四種指令大小寫敏感，零模型生成與額度重置，完成後等待新指示。檔案與直接 `.csproj` 目標必須在 Scope 內；途中有未載入的 `AGENTS.md` 即拒絕，須先明確切換。遞迴搜尋遇新邊界整次拒絕，第一層列檔不探查子目錄內部；原忽略及工作量上限保持。建置仍以工作區根為 cwd，不承諾解析間接輸入的規則。

來源變動會停止目前鏈、使修改快照與核准失效；重載後須重新確認計畫、給新指示及取得新的操作核准。原歷史、摘要、pins、PlanId／Revision、任務與已知事實保留，完成依據記錄原 RulesRevision。已開始的寫入／程序先完成核對及事實入帳，再撤確認；合法修改規則檔也遵守此順序。取消不是復原，不自動續跑、重試或補送。

規則單檔最多 16384 原始 bytes（含 BOM）、8 份存在來源、根起深度32；完整 JSON 投影32768、CLI完整區塊65536 bytes。UTF-8、敏感路徑／內容、身分與缺席位置均重驗，原文不壓縮或截斷。規則與工具共用排他租約，載入不建立修改快照或任務證據。一般模型請求重建完整規則附錄，摘要不直接加入規則全文；規則版本／世代使過時候選與量測失效。既有196608估算／262144實際body、計畫、證據、生成與程序限制不放寬。

程式保證來源、排序、Scope 與操作閘門；自然語言慣例不增加權限，也不保證模型理解全部矛盾。`.agents`／`.codex` 等敏感位置仍拒絕。沒有規則持久化、背景監看、技能引擎或新增模型工具。

### 第 12 份驗證紀錄（2026-10-25）

基線為乾淨 `main@11b7100`，本次產品／測試／文件變更尚未提交。環境為 macOS 26.7.1 arm64、.NET SDK 10.0.100、Python 3.9.6；使用合成 Key、替身、真實暫存檔與真實 SDK 配假 HTTP，全程沒有呼叫真實 Gemini。

| 命令／檢查 | 實際結果 |
|---|---|
| `dotnet restore tests/AIAgent.Cli.Tests/AIAgent.Cli.Tests.csproj` | 0；固定依賴還原完成。 |
| `dotnet build tests/AIAgent.Cli.Tests/AIAgent.Cli.Tests.csproj --no-restore` | 0；CLI、ProcessHost、TerminalHost 及測試成功，0警告／0錯誤。 |
| `dotnet test --project tests/AIAgent.Cli.Tests/AIAgent.Cli.Tests.csproj --no-build` | 0；627通過、0失敗、0略過，含原558及新增69個執行案例。 |
| `python3 -B -m unittest discover -s tests -p 'test_sensitive_content.py'` | 0；33通過。 |
| `python3 -B tests/terminal_smoke.py` | 0；78組通過，含原74及新增4組規則指令、Scope／重載、Unavailable恢復、busy取消／EOF與終端機恢復。PTY不呼叫Gemini或執行專案建置。 |
| `dotnet run --project src/AIAgent.Cli/AIAgent.Cli.csproj --no-build -- --help` | 0；用途及規則說明通過，既有 help 零 Key／client／規則 I/O 回歸保留。 |
| `dotnet format src/AIAgent.Cli/AIAgent.Cli.csproj --no-restore --verify-no-changes`、變更測試 whitespace verify | 0；產品與變更測試格式通過，未改寫無關舊測試檔。 |
| CRLF／C#無檔尾換行、文件／JSON／連結與差異查核 | 0；變更 C# 格式、158個本機連結、36個JSON區塊、程式碼圍欄、`git diff --check` 與新檔 no-index 空白查核通過。 |
| 公開 LLM 契約、套件、設定與 global.json 差異 | 無變更。 |

基線及最終完整 MSTest 在允許測試初始化自身原生 sandbox 的外層環境執行。初始受限環境有三項既有原生案例因 `sandbox_apply: Operation not permitted` 失敗；未修改產品隔離政策或刪除案例。新增真實離線建置案例僅驗「程序事實已確認後，規則 I/O 故障不得抹去該結果」，不取代第10份完整原生驗收。

| 規則案例群 | 實際驗證範圍 |
|---|---|
| RULE-AC-001～009 | 正式組裝及既有契約回歸、來源鏈／缺席／BOM／指紋、嚴格編碼、敏感／非普通來源、8／9來源、32／33深度、16384／16385、32768／32769、65536／65537邊界與租約互斥／取消收尾。 |
| RULE-AC-010～018 | 直接入口Scope、新建目標父鏈、搜尋整次拒絕及發布前新邊界、程序准入前拒絕、完整附錄、Context量測過時、保留計畫／事實、no_change、失敗候選保留舊鏈、同長改寫／身分／時間／缺席變動與無關目錄時間戳。 |
| RULE-AC-019～027 | 純文字與SDK await期間變動、已發布新版本拒絕舊生成、摘要不直接含規則、整批取消配對、核准世代失效、合法規則修改、檔案系統解析名稱、已知來源變更後取消、最終提交閘門取消、Dispose等待、clear／共同重設／換Key及255→256事實收尾。 |
| RULE-AC-028～031 | 既有工具Schema／成功資料及SDK所有權回歸、四種新錯誤與data=null、無額外FUT能力、真實SDK多呼叫／簽章及262144／262145實際body；額度與嚴格故障優先順序回歸。 |
| RULE-AC-032 | 離線macOS PTY：字面中文／空白目錄、完整來源、Stale與Unavailable、手動恢復、預輸入丟棄、Ctrl+C／Ctrl+D等待讀取收尾。 |

獨立唯讀審查指出：實際計畫修訂保留 Completed 任務時遺失 CompletionRulesRevision，以及第二次來源重驗已觀察變更後取消可能保留 Valid。兩項皆先補失敗回歸，再修正來源版本複製與兩次觀察的失效記錄；新增3個執行案例，修正後完整627項通過。沒有另行獨立複審。

RULE-AC-033真人終端機、034真實Gemini、第10份未結原生案例及第07份Release掃描／人工分發仍待獨立驗收。未執行Release發佈、真實服務、提交或推送，未宣告整體初版驗收完成。RULE-AC-035保留原文件撰寫階段的交付案例；本次改以產品增量與實際分層證據記錄。

<a id="context-budget-validation"></a>

## 第 11 份 Context 與預算管理

原始歷史最多五個完整成功回合。新回合開始前，已有五回合則摘要最舊三回合；三至四回合且本機估算達196608 JSON bytes時整理至剩兩回合。每回合最多一次自動摘要，成功後只保留一份摘要；摘要可能遺失未釘選資訊，必要自由文字請使用原文備註。PlanStore仍管理目前目標、版本、任務與完成依據；摘要不能授權操作、建立讀檔快照或恢復核准。

| 指令 | 行為 |
|---|---|
| /context、/context show | 查看容量、摘要及原文備註；過時計量明示過時。 |
| /context pin TEXT、/context unpin n1 | 加入完整單行原文，或移除一筆現存備註；保留文字前後空白。最多16筆、每筆1024 UTF-16單位、pins JSON最多8192 bytes。 |
| /compact | 至少三個原始成功回合時執行一次摘要維護，保留最新兩回合；消耗生成及API用量。 |
| /budget | 查看回合／程序額度、已消耗、剩餘及最近一般／維護統計。 |
| /budget set turn N、/budget set program N | 設定後續回合1～20／程序1～1000次生成；預設5／100。 |
| /budget reset | 恢復5／100上限，既有消耗保留；clear、換Key、切模式及修訂計畫也不退款。 |

自動摘要與一般生成共用當次回合額度，開始前回合及程序各須至少剩兩次；手動摘要另計單次維護並納入程序總計，不覆蓋最近一般結果。最後一次生成仍可完成文字回答，工具則整批不執行。調高額度、整理、reset或切Execute後都等待新指示；先前已核對寫入及完成事實保留。

本機估算含原生訊息、指示、工具與附錄，與真正SDK body長度分列；196608 bytes是整理軟門檻，實際body仍以262144 bytes為硬上限。來源131072、摘要原始／canonical各16384、記憶附錄32768、完整CLI區塊65536 bytes，皆含封裝與跳脫。bytes、生成次數、API五欄用量與程序兩次／300秒分別計量，模型token容量不可得，不推算帳單。沒有額外端點、client、套件、設定、持久化或背景摘要。

### 第 11 份驗證紀錄（2026-10-25）

基線為a43f798加本次未提交變更；環境為macOS 26.7.1 arm64、.NET SDK 10.0.100、Python 3.9.6。全程使用合成Key、離線替身與真實SDK配假HTTP handler，沒有呼叫真實Gemini。

| 命令／檢查 | 實際結果 |
|---|---|
| dotnet restore tests/AIAgent.Cli.Tests/AIAgent.Cli.Tests.csproj | 0；固定依賴還原完成。 |
| dotnet build tests/AIAgent.Cli.Tests/AIAgent.Cli.Tests.csproj --no-restore | 0；CLI、ProcessHost、TerminalHost及測試建置成功，0警告／0錯誤。 |
| dotnet test --project tests/AIAgent.Cli.Tests/AIAgent.Cli.Tests.csproj --no-build | 0；558通過、0失敗、0略過（12.040秒），包含原456與本次新增／擴充102個執行案例。 |
| python3 -B -m unittest discover -s tests -p 'test_sensitive_content.py' | 0；33通過。 |
| dotnet run --project src/AIAgent.Cli/AIAgent.Cli.csproj --no-build -- --help | 0；新能力與用途說明通過，不讀Key或建立client。 |
| dotnet format src/AIAgent.Cli/AIAgent.Cli.csproj --no-restore --verify-no-changes | 0；產品格式檢查通過。變更測試檔另以whitespace／--include／verify-no-changes檢查通過。 |
| python3 -B tests/terminal_smoke.py | 0；74組通過，含原67組與新增7組CTX-AC-033指令／手動及自動摘要／取消與EOF收尾。 |
| git diff --check；變更C#行尾、Markdown連結與區塊 | 0；CRLF且無檔尾換行；本機連結及程式碼區塊通過。 |
| 公開LLM契約、套件、設定與global.json差異 | 無變更。 |

初始受限外層執行環境有3項既有原生sandbox測試失敗；允許產品自身sandbox初始化後，11項原生組及最終558項完整測試皆通過，產品隔離政策未放寬。額外的「整個測試專案whitespace驗證」退出2，指出17個未修改的舊測試檔已有LF行尾問題；本次變更檔零錯誤，未擴大改寫舊檔。

| 離線案例群 | 當次回歸範圍 |
|---|---|
| CTX-AC-001～006 | 正式組裝、精確指令、原文／Unicode備註、16筆／8192 bytes、完整投影／顯示、短Key欄名／識別／數字／跨欄組合與原子拒絕。 |
| CTX-AC-007～014 | 最舊完整前綴替換、涵蓋數、最新PlanStore／同版本狀態失效、快照保留、真實SDK多呼叫／簽章、來源白名單與131072 bytes、嚴格Schema及raw／canonical各16384 bytes。 |
| CTX-AC-015～023 | 196608估算觸發、一次自動摘要、5次含摘要、實際262144／262145 bytes、失敗不fallback、取消與遲到候選、換Key不補送、敏感／非法協定共同重設、診斷不保留已摘要原生前綴。 |
| CTX-AC-024～029 | 原Plan容量與收尾回歸、維護不開始程序回合、程序1000次耗盡與80%提醒、1／5／20最後生成整批零工具、開始／未傳輸／throw唯一記帳、clear／換Key消耗保留及一般／維護統計分列。 |
| CTX-AC-030～032 | 活動計畫A已Verified／Completed後，B因額度或容量受阻；新指示及新核准只續B。20次生成不解除程序兩次／300秒、失敗即停、Unconfirmed鎖定及test不可用；既有01～10與Python適用回歸保留。 |

獨立唯讀審查指出敏感內容被Schema錯誤提前遮蔽、遙測前置檢查誤計生成，以及部分驗收覆蓋不足；已加入失敗回歸並修正。實作者另以WeakReference／GC重現容量診斷保留舊原生回合，改為僅保留計量版本及弱計畫身分；來源metadata再檢查受控相對路徑。修正後完整558項與PTY通過，沒有另行獨立複審。

CTX-AC-034真人、035真實Gemini、第10份未結原生驗收及第07份Release掃描／分發門檻保留；未執行Release發佈、真人終端機、真實Gemini、提交或推送，未宣告整體初版驗收完成。

<a id="controlled-build-validation"></a>

## 第 10 份受控建置與測試

依 [第 10 份規格](specs/10-controlled-build-and-test.md) 實作受控建置；**第 10 階段尚未完整驗收**。2026-10-24 已依使用者決定保留私人 IPC 隔離邊界，將 SDK 10.0.100 的 `test` 明示為 `capability_unavailable`，不啟動目標、探針或核准，不放寬到全域 `/tmp` socket。

SDK 10.0.100 的 MTP 使用 `Path.Combine("/tmp", name)`，不採用私人 TMPDIR，與本規格的單次私人 IPC 邊界衝突。[固定版本原始碼](https://github.com/dotnet/sdk/blob/v10.0.100/src/Cli/dotnet/Commands/Test/MTP/IPC/NamedPipeServer.cs#L214-L223) 因此 parser／假執行器的 TestsPassed 測試只驗證契約，不能作為產品 MTP 可用的證據。未來須另行驗證相容 SDK 與支援契約，才能解除此限制。

新增 `run_dotnet_task`，參數只接受 `task`（build／test）及 `project_path`（單一安全 `.csproj`），固定 Debug 與工作區根 cwd。正式工具順序在原九項後追加它，Plan 仍六項、Execute 九項。測試 recipe／MTP parser 保留規格契約，但目前產品 `test` 不支援；未來支援時才可執行目前來源建置及停用 launchSettings 環境／參數。不提供自由命令、restore、額外參數、TRX 安裝或部署。

無計畫 Execute 可逐次核准。有計畫時須確認目前版本，並開始含精確 project／build 條件的 Validation v2；包含 test 的任務目前轉為 capability_unavailable，原 v1 空條件保持受阻。計畫確認不授權程序，每次仍完整預覽並使用新的隱藏 y／yes。build 成功只證明該次目標 Debug 建置。MTP 未來支援時，還須完整有效摘要、實際執行大於零、零失敗及收尾確認，略過數如實顯示。

程序與檔案共用一項工作區操作租約，程序前後失效全部修改快照。每回合兩次啟動／300 秒，build 120 秒、test 180 秒；輸出總量 1 MiB，保留有界節錄。一般失敗／拒絕停止剩餘工具與生成，下一則明確指示須重新核准；Unconfirmed／收尾失敗退出 1，需使用者人工處理，不能靠 clear／換 Key 繼續。

### 獨立產品位置

先從來源建置，再將產品發佈到工作區以外的新目錄；以下是維護者操作，並非模型額外允許的 recipe：

```sh
dotnet publish src/AIAgent.Cli/AIAgent.Cli.csproj --configuration Debug --no-restore --output /private/tmp/aia-stage10-product
/private/tmp/aia-stage10-product/AIAgent.Cli --workspace /absolute/path/to/trusted-workspace
```

發佈內容須包括 `ProcessHost/` 下的 apphost、DLL、deps、runtimeconfig 與必要相依檔。產品與監督器位置保持唯讀；與任何允許輸出 bin／obj 相交時拒絕操作，不能直接從待建置來源的產物位置覆寫自身。依賴須由維護者事先還原，產品不安裝或下載依賴。

建置只以實際通過的 macOS 26.7.1 arm64／SDK 10.0.100 組合驗收；其他平台及 MTP 不宣稱可用。首版只接受受信任安裝根內單一 10.0.x SDK；global.json 如指定 SDK，版本須相同，rollForward 僅接受 disable／latestPatch，allowPrerelease 僅接受 false，SDK paths 等其他覆寫拒絕。核准後重新檢查 SDK 選擇，避免預覽版本與實際執行不同。原生政策不可用、別名／輸出布局不安全或依賴不足即拒絕。禁止外部與 loopback 網路，只允許該次私人 tmp 的 Unix IPC；產物與 `/private/tmp/aia-…` 預設保留，不執行批次或遞迴清理。監督只涵蓋支援群組與已觀測後代，不保證偵測所有刻意 setsid／double-fork／重新掛接行為。

真實正向建置 fixture 為預先還原、`UseAppHost=false` 的小型專案。SDK 預設 `UseAppHost=true` 的 apphost 工作可能嘗試受限的群組／目錄服務，因而受控失敗；目前不宣稱任意 .NET 專案皆可成功建置，也不為此增加權限。

### 第 10 份驗證紀錄（2026-10-24）

基線 `4f5169c` 加本次未提交變更；初始 389 項 MSTest 通過。環境為 macOS 26.7.1 arm64、.NET SDK 10.0.100、Python 3.9.6；全程使用合成資料、離線 fixture 及真實 SDK 配假 HTTP handler，沒有呼叫真實 Gemini。下列是最終審查修正後的實際執行紀錄。

| 命令／檢查 | 退出碼與實際結果 |
|---|---|
| `dotnet restore tests/AIAgent.Cli.Tests/AIAgent.Cli.Tests.csproj` | 0；依賴還原成功。 |
| `dotnet build tests/AIAgent.Cli.Tests/AIAgent.Cli.Tests.csproj --no-restore` | 0；CLI、ProcessHost、TerminalHost 與測試建置成功，0 警告、0 錯誤。 |
| `dotnet test --project tests/AIAgent.Cli.Tests/AIAgent.Cli.Tests.csproj --no-build` | 0；456 通過、0 失敗、0 略過（10.952 秒）；含既有 389 項與本階段新增 67 項。 |
| `python3 -B -m unittest discover -s tests -p 'test_sensitive_content.py'` | 0；33 通過，0 失敗。 |
| `dotnet run --project src/AIAgent.Cli/AIAgent.Cli.csproj --no-build -- --help` | 0；只顯示用法與測試不支援的提示。 |
| `dotnet format src/AIAgent.Cli/AIAgent.Cli.csproj --no-restore --verify-no-changes`；另查 `src/AIAgent.ProcessHost/AIAgent.ProcessHost.csproj` | 皆 0；CLI 與監督器格式檢查通過。 |
| `python3 -B tests/terminal_smoke.py` | 0；67 組 PTY 通過，含 57 組既有與 10 組程序互動案例。程序 UI 使用離線模擬器，與下列真實原生驗證分列。 |
| `dotnet publish src/AIAgent.Cli/AIAgent.Cli.csproj --configuration Debug --no-build --no-restore --output /private/tmp/aia-stage10-product-final-ibp77n7x` | 0；本機獨立目錄產物，CLI `--help` 退出 0，ProcessHost 無參數退出 64；四個必要檔案齊全，apphost 為 Mach-O arm64／0755，deps 不含 CLI／Google 依賴。未對外發布。 |
| `git diff --check` | 0；已追蹤差異通過；新 C# 檔另查 CRLF、檔尾及空白。 |

真實原生 fixtures 實際驗證離線 Debug 建置、原始碼未變、正常根程序退出後停止已觀測子程序、取消建置與已觀測子程序，以及核准後專案 bytes 變更即拒絕啟動。每次核准後的內建探針驗證來源寫入／rename、敏感檔、新敏感名稱、hard link／symlink、IPv4／IPv6 loopback 拒絕及私人 Unix IPC 允許。外層執行工具的 sandbox 會阻止巢狀 sandbox 初始化，因此這些測試在已核准的外層執行權限下運行，產品自身的 deny-default 政策仍啟用。

離線測試另涵蓋嚴格 Schema、Plan／直接入口零 I/O 拒絕、v2 遷移與逐項證據、單次核准、工作區共用租約、byte 邊界、敏感文字跨區塊／節錄檢查、程序摘要保留、第五次生成、失敗停止及 Unconfirmed 鎖定。MTP parser 的合成摘要案例並非真實測試執行證據。

獨立審查指出 SDK 目錄選擇與實際 muxer 選擇不一致、排序前無界列舉，以及控制回執失敗、父程序過早結束監督器、觀測遺失與收尾期間逾時的問題，均已修正並限縮重審。原生模組的 24 項 focused 測試通過；其中使用合成 sleep 目標與注入觀測失敗，驗證 Started／收尾回執丟例外及觀測喪失仍嘗試停止自有程序，保留 Unconfirmed。這些直接監督器測試與上述產品 sandbox 建置 fixture 分列，尚未實際切斷父程序控制管道。

**未驗證／未完成：** 真實 MTP 正向案例 BUILD-AC-016～018 目前不支援；原生完整 120 秒逾時、忽略 TERM、已觀測後代改群組／setsid／double-fork、256 個身分上限、敵對祖先交換與大小寫／Unicode 別名，以及監督器崩潰後不確定收尾，尚未全部以真實 fixture 驗證。故不能以本次測試全綠宣告 BUILD-AC-001～034 整體通過；PTY 的 67 組亦不等於第 035 項所有條件均完成。真人 BUILD-AC-036、真實 Gemini 037 與第 07 份發布／分發門檻繼續待驗。

本次沿用指定 checkout，保留未提交變更；產品採獨立目錄部署，與允許寫入的 bin／obj 相交時拒絕；對不支援的 SDK／布局採關閉能力，不改動使用者 SDK 或 global.json。這些界線可能使部分合法專案無法啟動，須另行驗證相容布局後擴充。所有私有暫存與 fixture 預設保留，未執行遞迴清理。

## 第 09 份 Plan 模式與任務清單

第 09 份實作 FUT-03。啟動預設 Execute，沒有活動計畫時沿用既有聊天與逐筆檔案核准。模式、任務與證據由本機程式管理；計畫及檔案文字不能授權切換模式或建立操作事實。

| 操作 | 行為 |
|---|---|
| `/plan` | 切換唯讀探索與規劃；提供五項唯讀工具及 `submit_plan`。保留同版確認，但在途任務轉為受阻。 |
| `/tasks` | 本機顯示目標、版本、確認、全部任務、必要操作、摘要、完成依據、受阻原因及舊事實數；不呼叫模型。 |
| `/execute` | Draft 計畫完整預覽後，只有新輸入的隱藏 y／yes 可確認該版本。確認後只切 Execute，等待下一則一般訊息。沒有計畫或同版已確認時不重問。 |
| Execute 的 `update_task` | 開始、完成或封鎖任務；同時最多一項在途。模型不能提交自造 evidence、approved 或退出碼。 |

典型流程是 `/plan` → 一般訊息要求探索及提出計畫 → `/tasks` → `/execute` → 隱藏輸入 yes → 下一則一般訊息要求執行。計畫確認只確認方向；每筆寫入仍須另行預覽、核准及重驗。Plan 的工具定義、批次預檢、Registry 與實際工作區入口共同禁止寫入，不能靠偽造呼叫繞過。

Read／Write 任務須逐項符合必要操作。完成證據取同一任務最近的相符本機結果；截斷、後來失敗、未套用、Unconfirmed 或清理失敗皆不能完成，Write 每項都須 Verified。範圍讀取只證明指定請求的實際片段，不建立修改快照。Analysis 只標示「完成：模型分析判斷」。第 09 份原有 Validation v1 仍為「受阻：驗證能力尚未提供」；第 10 份新增的 v2 才能使用程序證據，且目前僅支援 build。

計畫跨五回合歷史淘汰保留；每回合結束仍在途時轉為受阻，正常回答使用「等待下一則指示；任務尚未完成」。部分寫入與已接受完成獨立於模型後續成敗，不自動重試或復原。清除、強制安全重設及換 Key 丟棄計畫、確認、證據與預留，保留模式及程序用量；舊世代遲到結果不能復活狀態。

最多 20 任務、每項 8 個必要操作、256 筆事實（含舊計畫／退役任務）。文字上限依原始 UTF-16 長度檢查；實際與保守模型投影各最多 32768 UTF-8 JSON bytes，完整新結果／確認／tasks 顯示各最多 65536 bytes。先保留取消與自動受阻收尾空間，再開始操作；不截斷計畫取得核准，不增加五次生成或 262144 bytes 實際模型請求上限。

### 第 09 份驗證紀錄（2026-10-24）

基線為 HEAD `79140cc` 加上本次未提交的工作區變更；只使用合成 Key、可控制檔案系統、真實 SDK 配假 HTTP handler 及離線 PTY。下表為當次實際命令、退出碼及數量；既有 F5 紀錄仍屬先前驗證。

| 命令 | 退出碼與當次結果 |
|---|---|
| `dotnet restore tests/AIAgent.Cli.Tests/AIAgent.Cli.Tests.csproj` | 0；固定依賴還原成功。 |
| `dotnet build tests/AIAgent.Cli.Tests/AIAgent.Cli.Tests.csproj --no-restore` | 0；產品、TerminalHost 及測試建置成功；0 警告、0 錯誤。 |
| `dotnet test --project tests/AIAgent.Cli.Tests/AIAgent.Cli.Tests.csproj --no-build` | 0；389 通過、0 失敗、0 略過；含原有 330 項與新增 59 項。 |
| `python3 -B -m unittest discover -s tests -p 'test_sensitive_content.py'` | 0；33 通過，0 失敗。 |
| `dotnet run --project src/AIAgent.Cli/AIAgent.Cli.csproj --no-build -- --help` | 0；只顯示用法，沒有認證或生成。 |
| `dotnet format src/AIAgent.Cli/AIAgent.Cli.csproj --no-restore --verify-no-changes` | 0；格式檢查通過，未改寫檔案。 |
| `python3 -B tests/terminal_smoke.py` | 0；57 組 PTY 通過；含既有 50 組與新增 7 組，不是真人操作。 |
| `git diff --check` | 0；已追蹤差異空白檢查通過；未追蹤 C# 另逐檔查核。 |

當次環境為 macOS 26.7.1 arm64、.NET SDK 10.0.100、Python 3.9.6。精確 byte 邊界以實際序列化資料校準，不把 UTF-16 長度或估算當作 SDK body 大小。

獨立唯讀審查在額度中斷前確認一項重要問題：過期 AsyncLocal 世代被新回合重新綁定，可能重開模型請求。已先以測試重現，再改為沿用原執行世代並禁止過期狀態投影。其後由實作者自行完成剩餘檢查，另修正強制重設後重建最近結果，以及分析完成與在途操作共用識別的溢位／短 Key 邊界；相關測試先失敗後通過，最終整套 389 項全綠。這不代表已取得完整獨立審查結論。

| 案例 | 自動驗證內容 |
|---|---|
| PLAN-AC-001～007、021、023、029、032 | 本機指令與零生成、模式工具及嚴格 Schema、整批敏感／模式預檢、Unicode／長度／路徑與安全顯示、原生簽章及多結果配對、唯一記帳與既有回歸。 |
| PLAN-AC-008～016、019、025、031 | 本機身分與版本、精確任務保留／退役、狀態轉移、逐項必要證據、最新失敗、範圍摘要、分析與未提供的驗證能力、256 筆容量。 |
| PLAN-AC-017～020、026～030 | 部分寫入、取消決策點、Unconfirmed／CleanupFailed 優先、正常暫停、世代失效、清除／換 Key、五回合淘汰及完整請求上限。 |
| PLAN-AC-024、026 | 投影 32767／32768／32769、結果與實際／保守預覽 65535／65536／65537 bytes，包含跳脫、保留容量及尚未操作即中止的收尾。結果 writer 防線另以超長合成內部識別測試，產品本機識別不會達此長度。 |
| PLAN-AC-033 | macOS PTY 的同意、拒絕、空白、Ctrl+C、EOF、預貼 yes 及 busy 輸入七組，含獨立檔案核准與終端機復原。 |

PLAN-AC-034 真人終端機與 PLAN-AC-035 真實 Gemini 尚未驗證；離線及 PTY 不代替這兩層。第 07 份 Release BUD-S003、x64 與人工分發門檻繼續保留。本次沒有新增套件、設定、認證、程序／shell、Git 工具、摘要、保存或多 Agent 能力，也沒有提交、推送或發布。

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

`preLaunchTask` 與任務 label 一致；**Cmd+Shift+B** 可單獨執行同一個預設建置任務。偵錯設定關閉例外及模組載入訊息記錄；本機操作與 Key 輸入仍由產品終端機流程管理。2026-10-23 已實際驗證的 F5 範圍見[第 07 份既有驗證摘要](#stage07-validation)。

## 聊天操作

一般輸入提示前顯示 `[Plan]`／`[Execute]`，提示仍為 `你> `。**輸入不回顯；Enter送出，F2安全預覽，F3貼上收集，F4換行**。不顯示星號或輸入長度；已知指令只顯示操作結果。一般訊息含當前 Key 時整筆阻擋，原文不顯示或送出。

| 操作 | 行為 |
|---|---|
| 一般文字 | 保留原始首尾空白，啟動最多五次生成的回合；生成及工具處理期間不接受下一則訊息。 |
| 空白 | 不送出，重新提示。 |
| `/help` | 顯示聊天指令與目前能力；啟動參數 `--help` 只顯示啟動用法。 |
| `/plan`、`/execute`、`/tasks` | 切模式、確認版本或檢視本機任務；詳見第 09 份操作。 |
| `/clear` | 清除對話、快照、計畫、確認、證據及最近結果，保留模式、Key、工作區、模型及程序累計；不復原已寫檔案。 |
| `/exit`、EOF、Ctrl+D | 正常退出；部分輸入會丟棄。 |
| 等待輸入／認證選擇／Key 輸入時 Ctrl+C | 退出 130。 |
| 計畫確認中 Ctrl+C | 取消該次確認並回到 Plan；不退出 130。 |
| 生成／工具處理／檔案核准中 Ctrl+C | 取消並等待本回合收尾，再回到輸入；不取消後續聊天。 |
| 生成／工具處理／核准中 Ctrl+D | 取消並等待收尾，正常退出。 |

指令在 Trim 後區分大小寫且完整匹配；`/CLEAR`、`/help extra`、`/tmp/a` 皆為未知指令，不送模型。生成、工具處理、取消及收尾期間的普通按鍵（包含 `/exit`）會丟棄，不能排隊；外部 SIGINT 依相同聊天狀態路由。

一般草稿最多16384個UTF-16單位，支援多行、Tab、完整文字元素編輯及程序內安全歷史；詳見[第14份操作](#terminal-interaction-validation)。Key與核准仍保持原隱藏單行限制。

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
