# 第 24 份：受控 MTP 測試支援開發規格書

> 文件版本：0.1｜建立日期：2026-11-05（Asia/Taipei）｜文件狀態：待實作／相容性待驗證
>
> 選題依據：[問題摘要 REVIEW-03](../ROADMAP_REVIEW_NOTES.md#review-03fut-02-產品內測試不支援)。24 是子規格編號，REVIEW-03 是問題追蹤編號，FUT-02 是功能方向。本次只新增本檔；文件完成不代表產品支援 test，不解除 REVIEW-03，也不代表第 10 階段完整驗收。

## 1. 目的與交付邊界

讓使用者能在逐次核准、離線且維持原生隔離的條件下，透過產品的 `run_dotnet_task` 執行單一專案的 Debug MTP 測試，取得可靠的程序結果及測試計數，並供 Validation v2 判斷完成。

本份承接[第 10 份](10-controlled-build-and-test.md)的既有程序契約，補齊正式 SDK 相容性、私人 IPC、能力准入及真實 MTP 驗收。已選定「正式 SDK 相容性驗證後條件式啟用」：允許評估後續正式 SDK，包含跨主要版本，但不以升級本身當成支援證據。

本次僅交付文件，不修改產品、測試、SDK、套件、設定、README、ROADMAP、問題摘要、SPEC 或既有子規格；不另建實作計畫、工作樹、hook 或原始驗收轉錄，不變動 Git index／HEAD，不提交、推送或發布。不執行 restore、build、產品 test、PTY、publish 或真實服務呼叫。文件檢查不構成產品驗收。

後續產品開發不包含失敗後自動修正／重試循環、自由命令、VSTest、直接執行 test module、容器、其他語言、跨平台移植或 Release 掃描規則修訂。REVIEW-01、02、04 仍分別追蹤；本份不承諾補齊第 10 份全部歷史原生待辦。

## 2. 基準、來源與現況

### 2.1 基準與必讀來源

- 規劃及文件撰寫來源基準：`da4e86edcf25653126dc01977ab39bd7af0421c9`；開始撰寫時工作目錄乾淨。
- 規劃階段實際查詢：SDK `10.0.100`、macOS `26.7.2`。現行 `DotnetInstallation.PlatformSupported` 限 macOS `26.7.1` arm64；本機 OS 不符合產品准入，不能用修改版本字串取代隔離驗收。
- 上位契約：[SPEC](../SPEC.md)第 5、8～15 章；[第 10 份](10-controlled-build-and-test.md) BUILD-017～058、069～071 與 BUILD-AC-016～023；[ROADMAP FUT-02](../ROADMAP.md#fut-02受控建置與測試)。
- 直接實作：[ControlledDotnetTasks](../src/AIAgent.Cli/Processes/ControlledDotnetTasks.cs)、[DotnetProjectAdmission](../src/AIAgent.Cli/Processes/DotnetProjectAdmission.cs)、[DotnetRecipe](../src/AIAgent.Cli/Processes/DotnetRecipe.cs)、[MtpSummaryParser](../src/AIAgent.Cli/Processes/MtpSummaryParser.cs)、[ProcessHost](../src/AIAgent.ProcessHost/Program.cs)及[SandboxProbe](../src/AIAgent.ProcessHost/SandboxProbe.cs)。
- 既有回歸：[BuildNativeTests](../tests/AIAgent.Cli.Tests/BuildNativeTests.cs)、[BuildProcessTests](../tests/AIAgent.Cli.Tests/BuildProcessTests.cs)、[BuildOutputTests](../tests/AIAgent.Cli.Tests/BuildOutputTests.cs)、[BuildPlanTests](../tests/AIAgent.Cli.Tests/BuildPlanTests.cs)及 [README 第 10 份紀錄](../README.md#controlled-build-validation)。

後續開發前重新確認來源、未提交變更、SDK／runtime／MTP 及平台，不把本文基準或歷史通過數視為新版本實測。

### 2.2 問題因果與證據限制

| 來源 | 已知事實 | 不代表的結論 |
|---|---|---|
| 現行工廠 | `Supports` 只對 Build 與可用安裝回傳 true。 | 工具列舉含 test 不等於支援 test。 |
| 現行直接服務 | 純參數、敏感及 Plan 檢查後，Test 直接回傳 `capability_unavailable`，早於目標專案 I/O、核准及探針。 | 不能只刪除此拒絕便啟用。 |
| 既有契約 | 已有 test argv、摘要 parser、`TestCounts`、結果 writer 及 Validation v2。 | 假結果測試不證明真實 MTP／隔離可運行。 |
| 第 10 份歷史決策 | SDK 10.0.100 的 MTP Unix IPC 固定全域 `/tmp`，使用者選擇保留私人 IPC 限制。 | 不授權修改 SDK 或放寬全域 IPC。 |
| REVIEW-03 歷史測試 | 開發者從終端機執行的測試曾全部通過。 | 不是經產品受控入口的 BUILD-AC-016～018 證據。 |

### 2.3 官方來源查核

規劃階段查閱官方固定 tag 中的 `NamedPipeServer.GetPipeName`：`v10.0.100`、`v10.0.101`、`v10.0.200`、`v10.0.201`、`v10.0.300`、`v10.0.400` 均見 `Path.Combine("/tmp", name)`。這些是已查閱版本，不能推論所有未查閱修補版本都相同。

- [SDK v10.0.100 固定來源](https://github.com/dotnet/sdk/blob/v10.0.100/src/Cli/dotnet/Commands/Test/MTP/IPC/NamedPipeServer.cs)
- [SDK v10.0.400 固定來源](https://github.com/dotnet/sdk/blob/v10.0.400/src/Cli/dotnet/Commands/Test/MTP/IPC/NamedPipeServer.cs)
- [官方變更 2b2639d：Harden MTP dotnet test orchestration](https://github.com/dotnet/sdk/commit/2b2639d95c5b6a442fa51b66626a30da39fe5eed)
- [Microsoft：MTP 模式的 dotnet test](https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-with-dotnet-test)

查閱時的 SDK 主分支已有 `TESTINGPLATFORM_PIPE_DIRECTORY` 及 `ResolvePipeDirectory` 機制，屬候選來源線索。尚未確認包含此變更的合格正式 SDK，亦未安裝或實測候選；不能將主分支、PR 合併、官方文件的通用支援或 MTP 最低版本要求等同本產品支援組合。

## 3. 選定方案與相容性啟用流程

### 3.1 方案比較

| 方案 | 評估與決定 |
|---|---|
| 正式 SDK 原生提供私人 IPC，驗證後條件式啟用 | 已選定。保留 `dotnet test --project` 與程序證據架構；代價是合格發行版本及原生證據未備齊時仍受阻。 |
| 固定 10.0.100，放行全域 `/tmp` 或修改 SDK | 不採用。與已確認隔離邊界或受信任 SDK 來源不符。 |
| 建置後直接執行 module／轉用 VSTest | 不採用。會改變入口、核准、module 選擇與摘要責任，超出本份。 |

### 3.2 開發時資格流程

以下是後續開發驗證流程，不是產品每次執行時的自動研究或回退：

1. 以官方發行資訊與該版本固定 tag／commit 證明候選為正式 SDK，核對其 IPC 路徑形成、runner 啟動與參數傳遞。記錄來源及確切 SDK／runtime／MTP 版本，不採 preview、nightly 或自行修改的組件。
2. 維護者在隔離測試環境預先準備可信安裝、離線套件及已 restore fixture。產品不負責安裝、下載或 restore；不為測試改動使用者原 SDK 安裝。
3. 在精確 OS／架構組合下，以與產品一致的 sandbox、環境、ProcessHost 及 recipe 執行可信 fixture，證明真正 MTP IPC 位於該次私人 tmp；同時證明全域 IPC 與網路仍拒絕。僅設定 TMPDIR 或看到目錄存在不足以通過。
4. 驗證實際 argv、launch profile 停用、正常／失敗輸出、摘要格式、取消、期限及收尾；連同 build、Git、Plan 及跨功能回歸形成當次證據。
5. 全部必要案例通過後，才把確切組合加入產品內建相容性描述，並同步更新產品能力與文件。不使用範圍版本自動放行未驗證 patch。

跨主要版本只允許使用正式 SDK 建置及測試既有 `net10.0` 目標，必須驗證目標 runtime、資產解析、命令選項及結果相容。若需要提高目標框架、增加新 reporter／套件、不同執行入口或新增非私人權限，本份不得默默採用，須另行修訂規格。

若找不到合格正式候選，停止產品啟用，記錄「候選相容性受阻」，保留 `capability_unavailable`。不得交付假的支援條目、僅移除拒絕、只讓假測試通過，或將研究受阻寫成 REVIEW-03 已修正。

## 4. 開發需求與介面契約

### 4.1 能力、版本與來源身分

| 編號 | 要求 |
|---|---|
| MTP-001 | 本次只新增規格。後續 test 支援須經第 3 節與驗收矩陣，不把文件完成當成功能完成。 |
| MTP-002 | 產品內建不可變的 internal 相容性描述，至少綁定確切 OS 產品版本、架構、SDK 版本、所需 runtime／MTP 版本、recipe／parser／sandbox 版本及私人 IPC 機制；不接受模型、工作區文字或使用者設定擴充允許集合。 |
| MTP-003 | 工廠 Supports、Plan 的 test 能力與直接 RunAsync 使用同一能力來源；無支援組合時 Test 早於專案 I/O、私人目錄、核准、探針及目標執行拒絕。模式、純參數及敏感檢查既有優先順序保持。 |
| MTP-004 | 工廠能力表示環境有已驗證組合，不保證任意專案可執行。目標的 global.json、restore metadata、MTP／框架與輸出布局仍須在有界 admission 驗證；不相容時拒絕，不猜測或嘗試不同 runner。 |
| MTP-005 | 保留受信任實體安裝根、無 shim／PATH 選取及現行單一已安裝 SDK 限制。允許正式新版本須由已驗證描述明示；多 SDK、未知 patch、預覽版或 global.json 不能精確對應時拒絕，不自動挑選其他版本。 |
| MTP-006 | 跨主要版本維持產品及目標 net10.0，明列所需 runtime；global.json SDK 必須符合實際安裝及支援組合。沿用 disable／latestPatch 與禁止 preview 的有界設定規則，但實際解析結果仍須是已驗證版本；不新增 latestMajor／任意 paths。 |
| MTP-007 | 版本相容性納入預覽與核准後快照重驗。除既有 runtime bytes 外，新增用來授權相容性的 SDK／runner 描述與本機檔案也須有界擷取實體身分及內容雜湊；不以相同版本字串或 metadata 代替 bytes 一致。 |
| MTP-008 | macOS 26.7.2 與每個新 OS／架構須各有原生證據，不以版本大小比較放行。保持現有 build／Git 能力政策，不能因 test 支援而自動擴大其他任務平台。 |

相容性描述只保存固定版本、機制與本機驗證所需資料，不保存命令輸出、認證、對話或專案內容。所有新增型別為 internal，集合防禦性複製；含路徑資料的 ToString 使用固定型別標記。

### 4.2 固定 recipe、環境與核准

| 編號 | 要求 |
|---|---|
| MTP-009 | 沿用 run_dotnet_task 的 task／project_path Schema、DotnetTaskKind、結果 JSON、TestCounts、Validation v2 與公開 LLM 契約；不新增 CLI 參數、自由 argv、環境輸入或認證來源。 |
| MTP-010 | 固定工作區根 cwd、單一 .csproj、Debug、--no-restore、不帶 --no-build、停用 launch profile／ANSI／進度，以及 minimum-expected-tests 1；不提供 filter、discovery、忽略退出碼、coverage 或 reporter 安裝。 |
| MTP-011 | 優先沿用第 10 份完整 argv。候選正式 SDK 必須實測接受；不支援時該候選不啟用，不在執行期改命令。僅因正式輸出格式差異新增明確 parser 版本，不採猜測式解析。 |
| MTP-012 | 環境仍不繼承父程序變數。候選若正式支援 TESTINGPLATFORM_PIPE_DIRECTORY，固定設為該次 PRIVATE_ROOT/tmp 的絕對路徑；不得使用全域 /tmp 或模型值，亦不得只依賴 TMPDIR 推定有效。其他 IPC 機制須先修訂本規格，不自行猜測變數名。 |
| MTP-013 | DotnetRecipe 的預覽／環境與 ProcessHost 實際 argv／環境逐項一致；新增私人 IPC 變數及相容性版本列入 65536-byte 完整核准。任何改變需新 recipe／描述身分，不能沿用舊核准。 |
| MTP-014 | 沿既有單次隱藏輸入與單一讀取者；模式／能力檢查前後均不得預執行 MSBuild、發現測試或載入目標組件。只有完整安全預覽後的新輸入可核准，計畫確認不代替程序核准。 |
| MTP-015 | 核准後在原排他租約內重驗 SDK、runner、專案 bytes、global.json、restore metadata、輸出、相容性身分及 Rules／Skills／MCP 戳記；變動即停止並要求下一回合新候選，不自動重問並沿用舊權限。 |

候選正式 SDK 的最低 MTP 版本要求只作相容性前提；不能把「MTP ≥ 某版本」當成全部後續版本可用。實際 SDK、runner 與摘要格式均須固定到已驗證組合。

### 4.3 私人 IPC 與原生隔離

| 編號 | 要求 |
|---|---|
| MTP-016 | 保持 deny-default、來源／runtime 唯讀、標準 bin／obj 與當次私人目錄可寫，敏感路徑及工作保存位置不可達。不新增全域 tmp IPC、TCP／UDP／DNS／代理／loopback 或 host 服務權限。 |
| MTP-017 | 私人根沿用 /private/tmp/aia- 加 16 個隨機十六進位字元、排他建立、owner／0700 及身分檢查，含 home／tmp／results；只有核准後建立，碰撞拒絕，不刪除或改用未預覽位置。 |
| MTP-018 | 真實 SDK 與 MTP 的所有必要 socket 必須落在該次 tmp，完整路徑符合 macOS 104-byte 含 NUL 限制。過長、回退全域路徑或要求網路均失敗停止，不縮短安全識別、改 TCP 或放寬政策。 |
| MTP-019 | 每次核准後內建探針保持必要允許／拒絕檢查，補足私人 IPC 可通訊與私人根外 IPC 拒絕。探針使用產品控制的合成端點；不得連線既有 host socket。探針失敗零目標執行。 |
| MTP-020 | 開發時另以實際 MTP runner 驗證端點位置與行為，不能只用通用 socket 探針推論 MTP 支援。證據限端點所在根、是否越界、版本及結果，不輸出不受控原始轉錄或使用者資料。 |
| MTP-021 | 控制通道仍只屬 Agent／ProcessHost，目標不繼承控制 handle、認證、終端機或 Agent 資源。保留 link／rename／輸出祖先交換及 runtime 相交拒絕；不得為新版 SDK 放寬來源可寫。 |

## 5. 結果、預算、收尾與跨功能責任

### 5.1 成功與錯誤結果

| 編號 | 要求 |
|---|---|
| MTP-022 | TestsPassed 必須同時具備有效單次核准、Started、正常 Exited／0、CleanupConfirmed、無 cleanup 故障、輸出安全及完整有效的整體摘要；Passed>0、Failed=0、Total=Passed+Failed+Skipped，檢查 long 溢位。 |
| MTP-023 | 沿用第 10 份英文摘要契約：單一完整終端整體摘要，total／failed／succeeded／skipped 各一次，後面僅空白；duration 不作完成依據。多摘要、未知格式、語系不符、多 module／多目標框架、零測試或全略過不得完成。新增正式格式須綁明確 parser 版本及真實樣本回歸。 |
| MTP-024 | 兩 stream 完整增量 UTF-8 與敏感檢查後才形成證據；節錄截斷可與有效計數並存，必要摘要缺失／截斷則不通過。CLI 列出部分略過，不說全部均執行；數值是可信 runner 回報，不保證惡意專案不能偽造 stdout。 |
| MTP-025 | 沿用 ToolStatus、error_code 及 ProcessExecutionSummary，不新增僅為研究狀態的線上錯誤。沒有合格能力為 capability_unavailable；布局不符為 unsupported_project_layout；核准後來源身分變動沿 path_changed／既有較具體原因；已退出 0 但證據不足為 validation_inconclusive。 |
| MTP-026 | 非零正常退出保留 Exited／實際退出碼並回 process_failed；取消、逾時、輸出上限及未確認結果沿既有終態／空退出碼規則。錯誤優先級保持 cleanup／Unconfirmed 比一般失敗或同時取消嚴格，不把 wrapper 退出當成目標完成。 |

### 5.2 額度與生命週期

| 編號 | 要求 |
|---|---|
| MTP-027 | build／test 共用每回合兩次啟動及 300 秒；build 上限120秒、test上限180秒，取回合剩餘較小值。核准等待不計；探針／啟動失敗在已授予啟動權後仍占一次，不因 test 新增獨立額度。 |
| MTP-028 | 合計輸出上限1048576 bytes、每 stream 16384-byte 保留窗、8192-byte 摘要緩衝及65536-byte結果上限保持。超限真正停止，無無界累積或無限排空。 |
| MTP-029 | 取消、逾時、輸出／監督故障沿 TERM 2秒、KILL 3秒、管線1秒收尾與身分追蹤；正常根退出也收尾已觀測後代及管線。未確認則鎖定 session 並退出1，不宣稱整棵惡意程序樹已完全清除。 |
| MTP-030 | 取得啟動權前及啟動嘗試收尾後失效全部讀取快照；安全程序事實先記帳再做跨功能 Settle。模型後續失敗不抹除已知程序結果、不重跑；原已確認寫入保留。 |
| MTP-031 | 程序非成功停止剩餘工具與生成，不自動修正、重試、換命令或復原。一般失敗需下一則使用者明確指示及新核准；Unconfirmed 不可由 clear、切模型、換 Key 或重載技能解除。 |

### 5.3 計畫與其他功能

| 編號 | 要求 |
|---|---|
| MTP-032 | Plan 模式不取得程序能力。有活動計畫時仍需目前 Approved／唯一 InProgress 的 Validation v2 精確匹配 task／project；v1 保持受阻。能力變為可用不恢復任何舊核准或自動完成任務。 |
| MTP-033 | 每項 requirement 使用同 TaskId 最新相符且不同 OperationId 的合格本機事實；build 不抵 test，較早成功不掩蓋較晚失敗。模型文字、摘要、外部文件及開發者手動測試不得注入證據。 |
| MTP-034 | 保持共用工作區排他租約、Rules／Scope／Skills／MCP 世代與核准重驗。模型切換共用預算與停止狀態；工作保存不恢復程序證據／核准；checkpoint 復原後須使用新 epoch 的驗證。 |
| MTP-035 | 多 Agent 子不取得 build／test、程序核准或額外額度。私人目錄及產物保留，不提供遞迴／批次清理，不掃描後逐檔批次刪除；需整批清理由使用者人工處理。 |

## 6. 後續驗收矩陣

### 6.1 測試方法與證據

所有自動案例離線，使用合成認證、可信小專案、假終端機與可控制同步／時鐘。準備 fixture 的離線 restore 是維護者測試設定，與產品零 restore 分開記錄。不讀取真實 Key 或輸出完整應用指示，不留下不受控 stdout／stderr 快照。

真實原生案例須從來源輸出樹之外、受保護且完整的產品位置啟動，避免建置覆寫正在執行的 Agent／ProcessHost。外層環境不允許巢狀隔離時，記為環境受阻；不得停用產品 sandbox 取得成功。產品受控測試與開發者直接 dotnet test 兩個入口分開驗收。

### 6.2 案例

| 編號 | 需求／既有驗收 | 必須驗證的結果與層級 |
|---|---|---|
| MTP-AC-001 | MTP-001～008 | 文件基準與固定官方來源可追溯；無合格正式版本時明示阻擋，無虛構支援版本，REVIEW-03 保留。文件／來源查核。 |
| MTP-AC-002 | MTP-002～006、008 | 支援組合、未知 patch／OS／架構、preview、多 SDK、global.json 不符及缺 runtime 分別准入／拒絕。工廠、Plan、直接入口一致；已知不支援時零專案 I/O／核准／暫存／探針／目標啟動。離線。 |
| MTP-AC-003 | MTP-003～004、014、032；BUILD-AC-002～004 | Plan、未知能力、非法／敏感參數、批次預檢及直接入口保持既有拒絕優先級；不以工具描述繞過。離線。 |
| MTP-AC-004 | MTP-004～007、010～011；BUILD-AC-005 | 正式 SDK 來源、runtime／MTP 精確版本與實際解析一致；缺 restore metadata、不符框架／runner／輸出布局受控失敗，無 restore、安裝或回退。跨主要版本實際建置／測試 net10.0。離線加真實 SDK。 |
| MTP-AC-005 | MTP-009～014；BUILD-AC-005～007 | Schema／JSON 相容；預覽與兩處實際 argv／環境完全一致，不繼承父環境；launchSettings 含環境及參數均不生效；預貼 yes／模型 yes／計畫確認不授權。離線加真實 fixture。 |
| MTP-AC-006 | MTP-007、013、015；BUILD-AC-008 | 核准後 SDK／runner bytes、版本描述、專案、global.json、資產或輸出身分變動均不啟動；相同 metadata 不掩蓋內容置換，舊核准不可重用。離線及必要原生 fixture。 |
| MTP-AC-007 | MTP-016～020；BUILD-AC-020 | 真正 MTP 使用當次私人 tmp IPC，通訊成功；未知／不相容機制不啟用。private-root 外的合成 IPC、全域 tmp、IPv4／IPv6／loopback／DNS 仍拒絕；不能只靠通用探針。真實原生。 |
| MTP-AC-008 | MTP-017～019；BUILD-AC-020 | 0700／owner／排他建立、碰撞／連結及 Unix socket 長度邊界；失敗不換路徑或 TCP。探針失敗零目標啟動。離線及真實原生。 |
| MTP-AC-009 | MTP-016、021；BUILD-AC-021 | 來源／敏感／runtime 讀寫邊界、symlink／hard-link、輸出祖先交換及 runtime 相交保持拒絕；目標無控制 handle／父程序認證。真實原生。 |
| MTP-AC-010 | MTP-022～024；BUILD-AC-016 | 經產品真實受控入口執行通過、失敗、零測試、全略過、部分略過 fixture；只有正常0、實際執行>0、零失敗、完整安全摘要與收尾確認成功，Skipped 如實列出。真實 MTP。 |
| MTP-AC-011 | MTP-023～024；BUILD-AC-017 | 缺欄、重複、負值／溢位／矛盾、錯語系、未知行、多摘要、過長／截斷、僅 discovery／模組字樣、多 module／多目標框架均不完成；正式格式樣本與指定 parser 一致。離線及真實 MTP。 |
| MTP-AC-012 | MTP-022～025、028；BUILD-AC-018 | 有效摘要與一般節錄截斷並存仍可計數；必要摘要未取得則 validation_inconclusive。舊輸出、偽成功字樣、忽略非零退出碼不建立成功。離線及真實 MTP。 |
| MTP-AC-013 | MTP-024～026、028；BUILD-AC-019、024～026 | 合成 Key 跨塊／保留窗／控制字元、非法UTF-8、輸出上限及結果容量邊界；不發布敏感節錄，Response 缺失不重跑、不抹除可安全保留的已知事實。離線。 |
| MTP-AC-014 | MTP-026～029；BUILD-AC-009、014、022～023 | 真實成功、失敗、取消、逾時、忽略TERM、根先退出／後代持有管線及控制通道中斷；期限內收尾或 Unconfirmed 退出1，目標與wrapper回執不混淆。離線同步案例加真實原生。 |
| MTP-AC-015 | MTP-027～028；BUILD-AC-023～024 | build＋test 共用兩次／300秒；第三次拒絕、剩餘期限縮短、啟動失敗占額度、核准等待不計；輸出與摘要 byte 邊界維持。可控制時鐘及必要真實期限。 |
| MTP-AC-016 | MTP-030～034；BUILD-AC-010～013、028～031 | 正確 Validation v2 才執行；build／test 任一缺證據不完成，最新失敗不能被舊成功取代，模型不能注入事實；程序前後快照失效。離線整合。 |
| MTP-AC-017 | MTP-029～035 | Rules／Skills／MCP 改變、Clear／Dispose、切模型、checkpoint／work生命週期與已完成事實收尾正確；子不取得程序能力；未確認鎖定不可清除。離線整合。 |
| MTP-AC-018 | MTP-030～031；BUILD-AC-030～031 | 先寫入再test失敗保留寫入；剩餘工具／生成未執行，零自動修正／重試／復原；下一則明確指示才可新核准，遲到取消不回溯已提交成功。離線整合。 |
| MTP-AC-019 | MTP-013～014、024、029；BUILD-AC-035 | 完整核准、拒絕、Ctrl+C、SIGINT、EOF、忙碌輸入丟棄及下一回合／退出；等待精確執行同步點，不用固定sleep推定已開始。PTY；假host與真實MTP結果分列。 |
| MTP-AC-020 | MTP-013～014、024、029；BUILD-AC-036 | 真人驗證長預覽、中文／IME、核准、取消、部分略過呈現及終端機還原，不以PTY替代。真人。 |
| MTP-AC-021 | MTP-009、024、030～034；BUILD-AC-037 | 真實模型提出test、使用者核准、取得安全結果及後續模型失敗仍顯示本機事實；真實服務驗收另列provider／profile，不把假HTTP視為通過。真人／真實服務。 |
| MTP-AC-022 | MTP-005～011、016、029～035 | 新SDK組合下既有build、唯讀Git、Plan、工作區／敏感、Rules／Skills／MCP、模型／delegation等受影響回歸通過；原不支援組合仍拒絕。離線及對應原生回歸。 |
| MTP-AC-023 | MTP-001、008、035 | 分別記錄規格、候選查核、產品離線、PTY／真人／服務、Release與原生待辦；不因新增test解除其他REVIEW或第10份其餘門檻，不遞迴清理。證據審核。 |

新測試以 `MTPACxxx_行為描述` 命名，保留既有 BUILD-AC 編號的歷史意義。原 `BUILDAC005_KnownSdkMtpIpcLayoutRejectsBeforeProjectIoOrApproval` 等拒絕測試須針對已知不支援組合保留，不改成「所有 Test 都拒絕」或刪除其零副作用斷言。

## 7. 後續實作順序與文件交接

1. **完成候選查證及原生可行性證據：**固定正式版本與私人 IPC 機制。無候選則保留受阻，停止啟用；不先移除 Test 拒絕。
2. **建立一致的准入與重驗：**集中相容性判定，串接工廠、Plan與直接入口，完善安裝／目標 metadata及核准後身分檢查，保持build／Git既有政策。
3. **接續受控執行與證據：**同步recipe／ProcessHost環境、私人IPC探針與真實runner驗證，必要時新增固定parser版本；接通原結果／Plan證據及生命週期。
4. **完成分層回歸與文件：**依矩陣記錄當次命令、來源、平台／版本、退出碼、通過／失敗／略過與原因，才更新能力說明及問題狀態。

後續產品實作須在同一交付中同步最小必要文件：README的受控測試用法及當次證據、ROADMAP FUT-02狀態、SPEC索引、第10份明示增量與REVIEW-03。保留原始不支援決策與失敗紀錄，用新段落接續，不把歷史改寫成成功。

與[第 09 份](09-plan-mode-and-task-list.md)Validation交接、[第 11 份](11-context-and-budget-management.md)預算、[第 12 份](12-project-rules-loading.md)規則、[第 13 份](13-git-diff-and-code-review.md)Git、[第 14 份](14-terminal-interaction.md)終端機、[第 15 份](15-work-save-and-resume.md)保存、[第 16 份](16-change-checkpoints-and-recovery.md)checkpoint、[第 17 份](17-reusable-workflows-and-skills.md)技能、[第 18 份](18-mcp-external-tools.md)MCP、[第 20 份](20-multi-model-and-provider-support.md)模型及[第 21 份](21-multi-agent-delegation.md)子Agent的既有權限邊界保持。受影響的「test一律不支援」敘述只在實作與證據齊備後增補為條件式支援。

[第 22 份](22-release-publish-and-processhost.md)必要ProcessHost產物契約與[第 23 份](23-pty-cancellation-synchronization.md)測試同步保持。Release產物須先通過既定結構及敏感掃描；掃描非零不得啟動同產物驗收。研究／Debug原生fixture不替代Release門檻，亦不得藉SDK升級放寬掃描。

## 8. 完成定義與紀錄模板

| 狀態 | 必要條件 | 仍不可宣稱 |
|---|---|---|
| 規格完成 | 本檔需求、連結、矩陣、邊界及文件差異檢查通過。 | 產品支援test、REVIEW-03解除。 |
| 候選版本已查證 | 正式版本來源、精確組合與真實私人IPC／固定recipe可行性證據齊備。 | 產品整合／完整原生與回歸完成。 |
| 產品離線支援完成 | MTP-AC-002～018、022相關離線／真實MTP／必要原生案例全部通過；AC-001、023紀錄正確，能力僅對已驗證組合開啟。 | 真人／服務／Release及第10份全部歷史門檻完成。 |
| 其他分層驗收完成 | MTP-AC-019～021及發布／其餘原生門檻各自有當次證據。 | 未測OS／架構／版本或所有測試皆未略過。 |

沒有合格正式SDK、必要原生證據未齊或必須案例受環境阻擋時，不解除REVIEW-03。產品離線支援完成後可將其狀態改為「已提供指定組合的受控test；其他分層驗收另列」，不能僅據此將第10階段標成完整驗收。REVIEW-04及其他問題依各自證據判定。

後續驗收每批至少記錄：來源commit與未提交範圍、日期／時區、OS／架構、SDK／runtime／MTP精確版本、recipe／parser／sandbox身分、產品入口或開發者入口、fixture準備與離線前提、實際命令與退出碼、案例結果及必要收尾、阻擋原因與未執行層級。只記安全分類與計數，不附認證、完整應用指示或原始敏感輸出。

## 9. 本次文件交付檢查

本節限定本次撰寫工作，不是第6節產品驗收：

- 確認只有本檔新增，未修改產品、測試、其他文件、index或HEAD。
- 檢查需求編號唯一、MTP-001～035均有驗收對應，BUILD-AC-016～018明確承接。
- 檢查Markdown表格／程式碼圍欄、相對檔案連結與使用的本機章節錨點，確認未連到不存在的實作或測試空殼。
- 執行 `git diff --check`；本檔未追蹤時另執行 `git diff --no-index --check -- /dev/null specs/24-controlled-mtp-test-support.md`，核對無空白錯誤。
- 不執行產品build／test／PTY／publish；不以歷史通過數、本次文件檢查或官方來源查閱替代產品相容性及驗收。
