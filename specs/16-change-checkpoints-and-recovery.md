# 16：修改檢查點與復原開發規格書

## 第 19 份產品實作接續（2026-10-28）

承接 CKPT-HO-011：文件正文與來源資料不成為 before、anchor、路徑映射或復原資格；fetch 候選沿 RecoveryRevision 失效，既有本機發布與事實先入帳，不聲稱遠端 rollback。 詳見[第 19 份](19-web-search-and-document-fetch.md)及其固定交接；當次分層證據與未驗項目見 [README](../README.md#web-validation)。

## 第 18 份產品實作接續（2026-10-28）

復原候選綁 MCP 戳記；復原變動撤舊外送候選，目錄在相同有效 Scope 下可保留並於下次呼叫完整重驗。MCP 不進受控寫入來源、不增加 EvidenceEpoch、不捕捉外部 bytes 或提供遠端 rollback。 詳見[第 18 份](18-mcp-external-tools.md)及其固定交接；實際驗證與未驗層級見 [README](../README.md#mcp-validation)。

## 第 17 份產品實作接續（2026-10-27～28）

承接 CKPT-HO-009：復原候選與確認綁技能戳記，Stale 不允許 restore。復原目前 SKILL 先保留結果、RecoveryRevision／EvidenceEpoch，再使技能 Stale；其他檔復原重驗技能但不自動卸載。原 bytes／anchor／Git／逐筆核准所有權保持。詳見[第 17 份](17-reusable-workflows-and-skills.md)及[當次分層驗證](../README.md#skills-validation)。歷史文件交付敘述保留，未結原生／真人／真實 Gemini／Release 門檻不變。

## 產品實作接續（2026-10-27）

依使用者本次開發指示實作 FUT-09；原 CKPT-001／002、077～080 與 AC-034 記錄規格撰寫當時的文件交付，保留其歷史，不限制本次已授權產品開發。產品入口、容量／安全／相容性邊界依下列規格，實際分層驗證及未驗項目以 [README](../README.md#checkpoint-validation) 為準；規格中的歷史「待實作」不代表已驗收全部真人／服務／發布條件。

> 文件版本：0.1｜建立日期：2026-10-26｜狀態：待實作
>
> 選題依據：[ROADMAP：FUT-09](../ROADMAP.md#fut-09修改檢查點與復原)。上位文件：[總規格](../SPEC.md)第 5、8～15 章。FUT-09 是項目代號，16 是子規格編號；與第 09 份 Plan 規格不同。本次交付為文件，以下介面、操作與產品案例皆為後續開發要求。

## 1. 目的、選定方案與交付範圍

讓使用者在 Agent 的受控文字修改不符預期時，能確認原始修改、目前復原差異與衝突，再逐筆恢復修改前內容。復原只證明指定工作檔的結果，不代表整個任務、Git 狀態、程序副作用或外部系統都已回復。

| 編號 | 要求 |
|---|---|
| CKPT-001 | 本次只新增本檔，最小更新 ROADMAP 的入口及文件狀態；不修改產品、測試、設定、套件、README、總規格、既有子規格、Git 索引或歷史，不另建 memo、計畫、工作樹、hook 或驗收轉錄檔，不提交、推送或發布。 |
| CKPT-002 | 文件核對基線為乾淨 main@e6f29170455487edd71d91cfea21e0be4fd97161。已讀 ROADMAP、README、總規格及適用子規格，核對安全寫入、快照、租約、Plan 證據、Context、規則、Git 與工作保存的目前原始碼。既有測試數及舊規格的「待實作」不是本次產品驗收證據。 |
| CKPT-003 | 使用者選定獨立快照、僅當前程序記憶體、每次 create_file／replace_text 自動建立單檔紀錄，以及新建檔案只提供人工刪除指引。受控 replace_text 的完整 before bytes 是復原來源；Git HEAD、index、Review、摘要與讀檔快照皆不能代用。 |
| CKPT-004 | 首版沿用 .NET 10、現有 Google.GenAI、MSTest／MTP、macOS 原生發布及目前敏感政策；不新增套件、設定欄位、認證來源或模型生成。其他平台／架構須另有實測，不能回退較弱的寫入方式。 |
| CKPT-005 | 不提供持久備份、跨次載入、當機恢復、redo、具名多檔檢查點、批次復原、多檔交易、任意 path／bytes 復原、強制覆蓋、三向合併、Git mutation、工作區檔案刪除、shell 或任意程序副作用復原。 |
| CKPT-006 | 第 07 份 Release／分發門檻、第 10 份 test 不支援與未結原生驗收，以及各階段真人／真實 Gemini 待辦保持分列。文件完成及未來復原 Verified 均不能解除這些門檻。 |

選定方案的取捨：Git 作為備份會增加 index／分支、寫入 recipe 及原生隔離契約，而且 HEAD 未必等於 Agent 修改前的 dirty 內容；跨次獨立備份會新增原始檔案內容的持久化與清除責任。因此本版採程序內獨立 bytes，只借用用途限定的 Git 唯讀觀察作衝突閘門。容量滿時停止新寫入，代價是需要使用者明確釋放紀錄。

### 1.1 開發者必讀順序

1. [ROADMAP](../ROADMAP.md)、[README 的目前能力與證據](../README.md#latest-validation)、[總規格](../SPEC.md)。
2. [04：工作區與唯讀工具](04-workspace-and-read-tools.md)、[06：安全寫入](06-file-edit-and-approval.md)第 4、7～12 節、[08：搜尋與局部讀檔](08-code-search-and-partial-read.md)。完整 read_file 修改基準與本份復原基準是不同資料。
3. [09：Plan 與任務](09-plan-mode-and-task-list.md)第 7～10 節、[10：受控建置](10-controlled-build-and-test.md)、[11：Context 與預算](11-context-and-budget-management.md)第 3、9～12 節。
4. [12：RULE-HO-008](12-project-rules-loading.md#future-handoffs)、[13：FUT09-HO-001～010](13-git-diff-and-code-review.md#fut-09-handoff)、[14：TERM-HO-008](14-terminal-interaction.md#future-handoffs)、[15：FUT-09 固定邊界](15-work-save-and-resume.md#fut-09-handoff)與 [SAVE-HO-008](15-work-save-and-resume.md#future-handoffs)，再讀本份[承接對照](#upstream-handoffs)。
5. 核對當時 WorkspaceFileWrites／WorkspaceSafeRead／ReadSnapshotStore、WorkspaceOperationCoordinator、FileChangeApprovalBridge、PlanStore、AgentContext／ContextCoordinator、ProjectRulesCoordinator、GitReadAdapter／GitProcessRecipe、ChatApplication 及 WorkSessionCoordinator；不讀取或附上完整應用指示、認證或 SDK 原生歷史。

## 2. 責任、最小內部契約與所有權

| 擁有者／內部元件 | 責任及必要資料 |
|---|---|
| WorkspaceFiles／CheckpointStore | 在原受控寫入內預留與完成紀錄；持有完整 before／expected after bytes、來源操作識別及已核對觀察。模型、Tools 與核准介面不能自行提交紀錄。 |
| CheckpointRecord | 不可變 CaptureVersion=1、CheckpointId、Create／Replace、正規化 path、根／父目錄身分、原始 before／after、BOM、mode、捕捉 RulesRevision／Scope、原發布後觀察、可空 PredecessorId；沒有 Key、session、client 或核准值。 |
| CheckpointRecoveryAnchor | 與不可變歷史分開的目前復原資格：目標預期身分／完整 after bytes／mode、父目錄身分及 Git guard。只有已核對的受控反向鏈轉移可重綁。 |
| ICheckpointWorkspace | 由活動工作區提供 List、Show、RestoreAsync(ID, approval, token)、ForgetAsync(ID, approval, token) 的用途限定入口；不接受外部候選或原始內容，共用原工作區租約。 |
| CheckpointCoordinator／ChatApplication | 本機指令、單一輸入讀取者、預覽、確認、取消／EOF、最後復原結果、目前狀態投影及跨元件發布後收尾。 |
| CheckpointGitGuard | 借用既有租約及受控 Git 轉接器，取得／比較第 6 節的有限 metadata；不保存 blob，不執行 Git mutation，不建立 GitSnapshot／Review 覆蓋。 |
| CheckpointRestoreCandidate | 綁定同一服務／session、檢查點、anchor／store 版本、目前規則／Plan 執行代次、復原代次、目標與候選 bytes、預覽及操作所有權；單次 await 的同意只能消費一次。 |
| CheckpointOperationResult／LastCheckpointOutcome | 前者保留指令固定錯誤碼、可空安全 ID／path、FileWriteState、Verified bytes／BOM 與 CleanupFailed；後者只保留最近已開始復原發布的安全結果及 RecoveryRevision，與 LastTurnOutcome／Plan facts 分開。 |

| 編號 | 契約要求 |
|---|---|
| CKPT-007 | 上述新增型別及介面均為 internal；含文字／bytes／集合者使用密封型別、唯讀屬性、防禦性複製及固定 ToString。公開 ILlmClient／LlmRequest／LlmResult／LlmMessage、現有模型工具定義、輸入及成功資料格式不變；SDK 型別仍限 Llm/。 |
| CKPT-008 | 紀錄來源只能是目前工作區 create_file／replace_text 的本機受控寫入事件，沒有計畫時也有獨立來源操作識別。Git 差異、模型聲稱、搜尋／局部讀檔、輸入歷史及存檔參照不得建立紀錄或歸屬證明。 |
| CKPT-009 | CheckpointState 為 Available、ManualRequired、Restored；另以固定原因碼說明人工原因。Available 只表示當時具備資料，list／show 不查目前磁碟、不保證現在可復原。內部預留不是可列出的成功紀錄。 |
| CKPT-010 | 全程使用原 WorkspaceOperationCoordinator 單一排他租約，涵蓋核准等待、Git guard、發布、讀回及收尾；第二操作 unavailable，不巢狀呼叫自行取租約的 CaptureGitAsync／公開讀檔。不要持決策鎖等待終端機、程序或 I/O。 |
| CKPT-011 | 捕捉歷史、可變 anchor、StoreRevision、CheckpointLifetimeGeneration、RecoveryRevision、EvidenceEpoch 與 RulesRevision 是不同契約。紀錄集合／資格／anchor 的每次變動均增加 StoreRevision。read_file 快照被淘汰、程序清除讀取基準或規則重載，不等於丟棄檢查點；舊候選與核准仍必須失效。 |
| CKPT-012 | 只由活動 ChatApplication 的本機使用者指令呼叫復原入口；不新增模型復原工具、approval_id、confirmed 參數、自動同意或可繞過入口檢查的 Apply(bytes)。核准橋接須綁定當前 session／操作，無綁定及非法 enum 一律 unavailable。 |

<a id="cli-contract"></a>

## 3. CLI、預覽與新確認

| 命令 | 固定行為 |
|---|---|
| /checkpoint list | 依 ID 數值降冪列全部紀錄的 ID、Create／Replace、安全相對 path、捕捉版本／Scope、bytes、狀態／原因與容量；標示為程序內歷史。 |
| /checkpoint show ID | 顯示指定紀錄的安全 metadata 與 before→after 原始修改差異；明示不是目前檔案、不是復原核准。差異超限整次拒絕。 |
| /checkpoint restore ID | Execute 下重驗目前來源，顯示目前→before 的完整反向差異、ID、範圍／限制及目前規則版本，新確認後依第 7 節處理單檔。 |
| /checkpoint forget ID | 顯示指定紀錄的 metadata、失去復原資料及可能斷鏈的影響，新確認後只丟棄該筆記憶體資料；不讀寫或刪除工作檔。 |

| 編號 | CLI 要求 |
|---|---|
| CKPT-013 | 命令大小寫敏感，Trim 外層空白後精確匹配上表；參數間為單一 ASCII 空格。ID 為 c 加 1～long.MaxValue 的十進位正整數，無前導零／正負號；不接受 latest、別名、path、萬用字元、多 ID 或多餘參數。已分配 ID 程序內不重用，clear 也不重設序號；溢位回 checkpoint_unavailable。 |
| CKPT-014 | 遵守 TERM 的全域多行命令閘門與 Unicode／敏感檢查，先於解析。命令及核准答案不進安全輸入歷史，不偽造一般使用者訊息或工具回合；所有命令零模型生成、零 token／生成額度新增。 |
| CKPT-015 | 僅 AwaitingInput 受理；list／show／forget 可在 Plan／Execute 使用，restore 必須 Execute 且目前規則 Valid。busy 時不佇列命令，查詢與核准均保留唯一讀取者，不由貼上結束、歷史回取或進度事件觸發操作。 |
| CKPT-016 | 復原提示為「復原此單一檔案變更？[y/N] 」；忘記提示為「忘記此單一檢查點？[y/N] 」。僅完整安全預覽成功輸出、舊讀取者結束、緩衝排空及新讀取啟用後的 y／yes 同意；n／no／空白拒絕，其他輸入重問同一候選。新隱藏單行確認不啟用一般編輯／歷史按鍵。 |
| CKPT-017 | 沿 TERM-030 的可證明輸入邊界：只能排除啟用新讀取前已緩衝的輸入，不能宣稱辨識實體鍵與貼上來源或拒絕提示後所有延遲貼上。文件內 yes、模型或其他 Agent 的回答均不是確認。 |
| CKPT-018 | Ctrl+C 取消當次指令並回輸入；EOF／Ctrl+D 正常退出 0；不可恢復輸入／輸出／收尾故障退出 1。復原發布開始後須先完成事實查核，不能用取消蓋掉 Verified、Unconfirmed 或 CleanupFailed。 |
| CKPT-019 | show 使用原第 06 份安全差異顯示與跳脫規則；restore 另產生新差異，不能沿用 show 畫面或舊 yes。完整預覽／metadata／提示各次合計不得超過 65536 UTF-8 bytes，不截短後核准。list 超限也整次拒絕，不隱藏部分紀錄假稱完整。 |
| CKPT-020 | 未知／已忘記 ID 回 checkpoint_not_found；Create／ManualRequired 回 checkpoint_manual_required，列可安全顯示的單一路徑與原因，不呼叫刪除／發布。Restored 回 checkpoint_already_restored，零寫入。指令結果不自動開始下一回合、切 Scope 或重送舊需求。 |

## 4. 記憶體容量、預留與釋放

| 常數 | 固定值與計算 |
|---|---|
| MaxCheckpointCount | 32 筆，包含 Available、ManualRequired、Restored 及在途預留。 |
| MaxCheckpointBytes | 8388608 原始 bytes；每筆 before＋完整 expected after 各計一次，含 BOM，在途預留也計入。 |
| MaxCheckpointSideBytes | 131072 bytes／側；Create 的 before 為明確 Absent，0 bytes，與既有空檔不同。 |
| MaxCheckpointDisplayBytes | 65536 UTF-8 bytes；完整 list／show／restore／forget 預覽各自限制。 |
| MaxCheckpointAppendixBytes | 32768 UTF-8 bytes；第 9 節必備目前復原狀態投影及包裝。 |
| Git guard 與路徑 | 沿用 Git MetadataLimit=1048576 bytes／回應、診斷 8192 bytes、單 recipe 30 秒及 path 4096 UTF-16 單位。 |

| 編號 | 容量要求 |
|---|---|
| CKPT-021 | 上限使用固定具名常數，不新增設定／環境變數或借用 read snapshots、Git raw／Plan facts 容量。bytes、UTF-16、JSON／預覽各自計量；上限可接受、超一拒絕。原始 bytes 預算不宣稱是含全部 runtime 複本的記憶體精確值。 |
| CKPT-022 | 原始候選合法且非 NoChange 後、顯示寫入預覽／等待核准前，原子預留一筆及完整 before＋after 容量。數量／bytes 不足回 checkpoint_limit，NotApplied、零核准／暫存／發布，停止當前工具鏈並將在途任務以 resource_limit 受阻，等待使用者處理。 |
| CKPT-023 | 不靜默略過紀錄、不自動淘汰、不以「繼續但無法復原」降級。原讀檔快照既有 LRU 不變；checkpoint 獨立保留，不靠將原 snapshot 留在 LRU 實現。 |
| CKPT-024 | NotApplied 釋放該次預留，已分配 ID 留空不重用；Verified／Unconfirmed 完成為一筆紀錄。若已嘗試發布而紀錄完成失敗，保留已知寫入事實，停止為本機故障，不能宣稱已具備復原保護或重試寫入。 |
| CKPT-025 | forget 在確認後重驗同一 ID、StoreRevision、候選及 session；相符才原子移除該筆、釋放其 bytes／數量並增 StoreRevision。若紀錄變動或已消失回 checkpoint_revision_conflict，不套用另一筆；不清 LastCheckpointOutcome、RecoveryRevision 或 EvidenceEpoch。 |
| CKPT-026 | 每筆 metadata／祖先觀察受既有 path／深度上限約束；不額外保存無界操作日誌、差異字串鏈或被忘記 ID 的 tombstone 清單。原始 bytes 只有所屬紀錄及有界在途候選可持有；釋放只丟棄自有參考，不保證清零全部 GC／SDK 複本。 |

## 5. 自動捕捉、歸屬與寫入事實

固定流程為：原安全寫入驗證／建立候選 → 預留 checkpoint → 取得穩定 Git guard → 原始差異與核准 → 重驗目標、規則及 guard → 同目錄暫存／發布 → 精確讀回 → 完成紀錄及原操作事實 → 規則失效／取消等收尾。Git metadata 無法取得的一般不可用情形依 CKPT-039 記為人工，不取消原合法寫入；已觀察衝突則依 CKPT-040 停止。

| 編號 | 捕捉要求 |
|---|---|
| CKPT-027 | Replace 的 before 只能取同次原寫入已通過 EDIT-044 完整重驗的 read_file 基準；after 是實際完整候選，不是 new_text 片段。保存原始 bytes、BOM、普通 mode 及原始／發布後身分，涵蓋原有人工 dirty 內容；不以 HEAD 還原掉使用者修改。 |
| CKPT-028 | Create 保存 Absent→完整 after，發布精確 Verified 後仍標 ManualRequired／created_file；不建立一般工作區刪除入口，不把空內容當不存在。人工指引只說需自行核對指定檔案，不提供批次清理命令或宣稱其他檔案也由 Agent 建立。 |
| CKPT-029 | 原寫入 Verified 且生命週期世代仍有效時，完成 CaptureVersion、來源識別、原始資料及 after anchor；Available 另要求 Replace、穩定可用 guard、未觀察衝突及可支持的來源。model／Response／最終回答之後失敗不撤掉已確定紀錄，除非既有安全重設明確清除。 |
| CKPT-030 | 原寫入 Unconfirmed 保存 before、expected after、固定來源及 source_unconfirmed 原因為 ManualRequired；expected after 不能冒稱已發布後內容，不建立可復原 anchor。保留原嚴格停止及一次有界查核，不能因讀到相似內容升級成功。 |
| CKPT-031 | 不捕捉 build 產物、任意外部修改、Git 操作、MCP 或其他 Agent 自報結果；首版來源集合固定兩項受控工具。歸屬僅證明應用記錄的操作，不宣稱能辨識所有作者、修改後再還原的行為或程序全部副作用。 |
| CKPT-032 | 原路徑、敏感、UTF-8／二進位、BOM、候選大小、預覽與核准限制全部保留；checkpoint 不增加可讀／可寫路徑。忽略／未追蹤檔僅在原受控寫入已准入時收錄該檔，不為建立 checkpoint 額外掃描、讀取或納入其他檔案。 |
| CKPT-033 | 預留與來源操作具有不可偽造的內部所有權。先完成已發生寫入的紀錄／事實，再處理 RULE 的延後撤確認；不能先遞增 PlanGeneration 使剛完成的 Verified 事實遺失，也不能以讀檔結果或模型資料補建。 |
| CKPT-034 | clear／換 Key／退出撤銷 CheckpointLifetimeGeneration 後，遲到寫入仍完成查核與必要安全回報，但不能補回已清除的 checkpoint。這是使用者明確清除造成的保留例外，不以「每次自動捕捉」保證清除後仍有復原資料。 |

<a id="git-guard"></a>

## 6. Git 觀察與分層狀態邊界

獨立快照不等於可以省略 Git 衝突檢查。例：工作檔 after bytes 完全相同，但使用者已 git add 或改為未追蹤，復原工作檔可能違反其最新安排；這些情形須拒絕或交人工。index、HEAD 與 refs 本身永遠不是本份的復原目標。

| 編號 | Git guard 要求 |
|---|---|
| CKPT-035 | 先安全觀察固定根的 .git。缺席記為 NotRootRepository，捕捉／復原各關卡重驗其仍缺席；不向工作區外搜尋 Git、讀父 repository 或宣稱已檢查外部 index。存在時只支持第 13 份已准入的根普通 .git 目錄／非 bare 布局；gitfile、linked worktree、submodule、連結或其他不支援布局標人工。 |
| CKPT-036 | RootRepository guard 保存根／gitdir 身分、已准入 executable 身分、object format、symbolic HEAD／resolved HEAD 或明確 unborn，以及目標精確 path 的 HEAD entry、index 全部 stage／tag／mode／oid 及 intent-to-add 差異。缺席是明確值，不能以未讀取表示未變；不只比較 mtime／長度。 |
| CKPT-037 | 重用 Version、InsideWorkTree、BareRepository、ObjectFormat、SymbolicHead、VerifyHead／VerifyHeadRef、四項既有 config 准入、HeadManifest、IndexManifest、IntentToAddVisible／Invisible 的固定唯讀 recipes。目標 manifest／diff 使用已驗證的相對檔案 path 作 literal pathspec，取得後只保留該精確路徑；拒絕不合法／不支持的目標條目。unborn 不呼叫 HeadManifest。不使用 Batch／BatchCheck／CheckIgnore、blob、全區正文掃描、任意 argv 或額外 Git recipe。 |
| CKPT-038 | guard 借用原 workspace lease，由工作區提供根／規則／metadata 安全觀察；不能呼叫會巢狀取租約的公開 Git capture。每次 guard 取得含兩次完整有限觀察，tuple 相等才穩定，整次最長沿用 Git 60 秒期限；原生禁止持久寫入、網路及 helper，唯一 /dev/null 資料寫入例外不擴張。metadata／診斷／stdin 仍經政策，不送 Key。 |
| CKPT-039 | 只在最初寫入預覽前，捕捉遇一般 Git 不可用、超限／逾時、不支援布局或不能安全證明狀態時，原合法寫入可繼續但紀錄標 ManualRequired／git_unverified，預覽先明示無自動復原資格。不靜默改成 NotRootRepository。已有穩定 guard 後的處理依 CKPT-040，不能在核准後默默降級。程序收尾不確定或敏感故障依既有嚴格停止／重設，不作一般降級。 |
| CKPT-040 | 已取得穩定 guard 後，原寫入核准後及發布前須重驗相等；已觀察根 .git 出現／消失、HEAD／目標追蹤或 index 狀態變動時回 checkpoint_git_conflict；一般無法安全重驗時回 checkpoint_manual_required。兩者皆 NotApplied、清目標讀取基準並停止，不繼續發布。原寫入已 Verified 後的 guard 變動／一般不可驗證，不抹除 Verified，紀錄改人工。 |
| CKPT-041 | restore 在預覽前、核准後、最後發布前比較捕捉／已證明 anchor 的 guard；變動回 checkpoint_git_conflict，無法安全重驗回 checkpoint_manual_required，零發布。只核對受支援根 repository 的目標 metadata，不要求全域 index digest，也不宣稱覆蓋工作區外／其他路徑的 Git 狀態。 |
| CKPT-042 | 原 index 可以與 before／after 不同，staged／unstaged 可互抵；只要 guard 相等，復原工作檔仍恢復 before，index 保持原樣。目標 unmerged stage、skip-worktree／assume-unchanged、gitlink 或非普通文字 mode 不提供自動復原，交人工；不執行 reset／restore／checkout／stash／apply／clean，也不從 oid 取備份。 |

<a id="restore-contract"></a>

## 7. 單檔復原、衝突與發布

固定流程為：確認 ID 與最新資格 → 目前 Execute／Valid 規則與路徑准入 → 完整目標與 guard 比較 → 新反向預覽 → 新確認 → 再比較 → 同目錄暫存及最後比較 → 原生發布 → 精確讀回及清理 → 結果入帳／反向鏈轉移 → 證據代次／模式／規則收尾。每次僅一檔，沒有整批核准。

| 編號 | 復原要求 |
|---|---|
| CKPT-043 | 僅 Available 的 Replace、仍在相同工作區／session 且第 8 節允許的紀錄能建立候選。現在有效的規則可與捕捉版本不同；顯示兩版本，依目前 Scope／來源重新准入，不要求回到舊規則或恢復舊確認。Stale／Unavailable 先要求明確 reload，不能為復原繞過規則。 |
| CKPT-044 | 預覽前、核准後及發布前各安全讀取完整目標，逐 byte 比較 anchor 的 after，並核對根／父目錄種類與 Device／Identity、目標普通檔案身分及完整 mode。涵蓋 BOM、換行、檔尾、同長改寫、消失／新增、不同 inode、權限／種類變換；hash／mtime 只能快速排除，不能單獨證明相等。 |
| CKPT-045 | 目標 bytes／身分衝突回 checkpoint_conflict；連結、敏感路徑、新巢狀規則界線、越界與根失效仍保留原安全分類。衝突使該路徑鏈轉人工，清目標讀取基準，不強制覆寫、不靠人工將 bytes 改回就自動重接鏈。 |
| CKPT-046 | 反向預覽是本次已核對目前 after→記錄 before 的精確差異，含 ID、path、bytes／BOM／mode、目前規則、只恢復該檔及 metadata 限制。原第 06 份安全顯示、控制字元跳脫及全部敏感檢查保持，超限不裁切。 |
| CKPT-047 | 核准後復用 EDIT-049～057 的同目錄 0600 排他暫存、完整寫入／flush、原普通 mode、暫存讀回、最後重驗、原生 replacement 及目標精確讀回。不得透過 replace_text 假造 old_text、建立 read snapshot 或沿用一般工具候選以略過復原入口；不得先刪目標或 copy fallback。 |
| CKPT-048 | 結果維持 NotApplied／Verified／Unconfirmed 與 CleanupFailed 分離。發布前拒絕／取消零目標發布；已開始發布的其他失敗為 Unconfirmed，停止、自有單次有界查核並交人工。Verified 後顯示／模型／清理失敗仍保留復原事實；CleanupFailed／不可恢復 I/O 退出 1，不被取消或規則變動降級。 |
| CKPT-049 | 普通拒絕、預覽過大或發布前取消可保留未被證明衝突的 anchor；任何再次嘗試仍完整重驗、新預覽與新確認。成功、Unconfirmed、目標衝突或安全失效均清目標讀檔快照；根失效清全部讀取基準並使所有 checkpoint 只可人工查核，不猜測新根映射。 |
| CKPT-050 | 只恢復完整 before bytes／BOM 及記錄的普通 mode，特殊 setuid／setgid／sticky 位元拒絕；不承諾 owner／group、ACL、xattr、resource fork、時間戳、硬連結或全部 metadata 還原。沿 EDIT 的非 CAS rename、硬連結／掛載與 TOCTOU 限制，flush 不等於父目錄同步或斷電持久性。已觀察衝突仍必須拒絕。 |

## 8. 同檔反向鏈與部分完成

| 編號 | 反向鏈要求 |
|---|---|
| CKPT-051 | 以精確正規化 path 分鏈；只有新受控修改的 before bytes、mode、身分、父目錄、Git guard 與該路徑目前合格 anchor 相等時，才能記錄唯一 PredecessorId。原紀錄歷史不改寫；大小寫／Unicode／硬連結別名不猜測合併。無法證明連續性時，舊鏈轉人工，新修改可依自身新基準獨立建立。 |
| CKPT-052 | 同路徑有較晚尚未復原的受控修改時，較早筆回 checkpoint_order，零核准／寫入。不可跳過新筆直接恢復早期 before；新的 Create／Unconfirmed／人工節點亦不能當作可穿越的鏈節點。 |
| CKPT-053 | restore 不建立新的可復原 checkpoint。Verified 才將指定筆標 Restored、消費資格；僅當其 before 與已記錄 predecessor 的 after／guard／mode 全部相符，才把 predecessor 的 anchor 綁到這次復原精確讀回的新 inode／父目錄觀察。原發布身分仍保留於不可變歷史。 |
| CKPT-054 | Unconfirmed 不標 Restored、不重綁 predecessor，整條該路徑鏈轉人工；不能由下一次讀到相同 bytes 補判先前成功。Verified 且 CleanupFailed 仍保留已復原事實，但程序必須依原契約停止退出，不能利用 anchor 繼續工作。 |
| CKPT-055 | 忘記尚未消費的鏈節，會使依賴它證明連續性的較早紀錄轉人工，不自動跨越缺口重接；較新且本身基準完整的紀錄仍可依自身狀態判斷，但其復原後不能越過已斷 predecessor。忘記已 Restored 紀錄只移除該筆歷史與 bytes，不撤銷先前已完成、由 predecessor 自身 anchor 保存的 Verified 重綁。人工／外部改寫或來源不可證明亦不自動重接；只有新的受控寫入可建立新的來源鏈。 |
| CKPT-056 | 跨檔沒有全有全無交易：使用者先完成 A、再請求 B 而衝突時，分別回報 A Verified 與 B NotApplied；不自動重套 A、回滾 A 或稱全部成功／全部未變。每次成功後進 Plan，下一次復原仍須由使用者明確切 Execute 再指定 ID。 |

<a id="state-integration"></a>

## 9. Plan 證據、Context 與規則整合

| 編號 | 整合要求 |
|---|---|
| CKPT-057 | restore 是使用者明確指定的本機復原入口；必須 Execute／目前 Valid 規則與新單次核准，但不借用舊計畫核准、不要求已 Completed 原 Write 任務可反向執行，也不將 restore 冒充 create_file／replace_text requirement。一般模型寫入仍嚴守 PLAN-037，不因本機入口放寬。 |
| CKPT-058 | 復原發布已開始後，先完成核對、清理及安全結果入帳，再增加 RecoveryRevision／EvidenceEpoch、撤銷執行確認、將在途任務 Blocked／workspace_restored、清修改候選並進 Plan；Verified 與 Unconfirmed 都適用。發布前 NotApplied 不增加這兩個代次、不任意改計畫；原更嚴格故障優先。代次溢位須在發布前拒絕。 |
| CKPT-059 | PlanStore 預留及 TaskOperationFact 新增 internal EvidenceEpoch；初始為 0，每次復原發布收尾後遞增。後續 completed 證據選取只接受目前 epoch 的合格事實，所有舊 facts 仍以原 OperationId／TaskId／規則版本保存為歷史。已接受 Completed 保持歷史狀態並明示復原後未重新驗證，不能把舊 facts 重綁到新操作、未完成任務或新 epoch。 |
| CKPT-060 | 不改 requirements_version、任務種類或 RulesRevision 來實現失效；不注入 restore 的 Read／Write／TestsPassed 成功 fact。原在途任務重啟後須重新取得全部 requirement 證據；Completed 需重做時依既有規則建立新任務。全部較早證據失去新完成資格，包含 Validation，避免猜測建置間接輸入圖。 |
| CKPT-061 | 每次已開始復原發布，LastCheckpointOutcome 保存最新安全 ID／相對 path、WriteState、可得 Verified bytes／BOM、CleanupFailed、RecoveryRevision，及「較早對話／摘要／證據是歷史，需重讀目前來源」固定標示。僅保留最新一筆及累計代次，不建立無界結果日誌；forget 不清此結果。 |
| CKPT-062 | LastCheckpointOutcome 非空時，所有一般／Review／摘要／工作保存摘要的模型請求及容量量測必須重建有界目前復原狀態附錄，含 CKPT-061 欄位及固定警示。上限 32768 bytes 含包裝，全部欄位通過目前政策；不能省略、裁切或只在 CLI 顯示。附錄敏感失敗沿既有共同安全重設；超量／無法完整容納沿 ContextCapacity 零該次生成，保留安全本機結果，不省略後補送。clear／換 Key 已清除結果時不產生此附錄、不捏造舊 ID／path；單調 RecoveryRevision 仍參與版本戳。附錄納入完整估算／實際 SDK 262144-byte 閘門，不增加 body／生成額度。 |
| CKPT-063 | RecoveryRevision 納入 ContextOperationStamp／MetricStamp、compaction、工作保存及其他會使用目前工作狀態的候選有效性。復原後舊量測／候選過時，不能發布或重送；不改寫既有 SDK 原生訊息與成功回合，不將 before／after、anchor 或舊核准送模型。工作摘要可記錄安全歷史結果，但 SAVE v1 不加 checkpoint 欄位或資格。 |
| CKPT-064 | 合法復原 AGENTS.md 使用目前 Valid 規則完成預覽、發布及結果，先記事實及復原代次，再標 Stale／撤確認／進 Plan；須明確 /rules reload、重新確認及新一般指示。若原規則已 Stale／Unavailable 或無法安全 reload，只能人工處理。回到舊 bytes 不能復活 RulesRevision、舊計畫確認、read snapshot 或任何 yes。 |

## 10. 敏感資訊、取消與生命週期

| 編號 | 安全及生命週期要求 |
|---|---|
| CKPT-065 | API Key 僅隱藏輸入留在 session 記憶體，不能進入 checkpoint、Git 請求／輸入、差異、結果、模型、日誌或存檔。所有捕捉／復原文字及固定／動態投影均經目前政策；命中即拒絕，不遮蔽後繼續核准，不傾印完整應用指示、SDK／HTTP／原始例外。 |
| CKPT-066 | checkpoint raw bytes、歷史差異、候選與 anchor 只保留在程序內，不存入 FUT-08、額外備份檔、暫存原始副本、對話、剪貼簿或稽核日誌。只有已核准的復原候選可依 EDIT 寫入一份自有同目錄暫存；這不構成持久備份。 |
| CKPT-067 | /clear、共同安全重設、接受替換 Key 與退出清除全部紀錄／預留／候選／LastCheckpointOutcome，撤銷 lifetime generation；不復原已寫工作檔、不刪工作存檔、不退生成或用量。ID 序號不重用；RecoveryRevision／EvidenceEpoch 不回退造成舊 stamp 相等，新程序才有新生命週期。 |
| CKPT-068 | 普通取消保留安全已確定紀錄及原操作事實；發布前取消釋放該次預留／候選，發布後用不受已取消 token 略過的本機收尾查核。clear／換 Key／退出先讓已發生結果安全回報，再清可清資料；遲到結果不得補回已清除狀態。 |
| CKPT-069 | 規則重載／Scope 切換／普通 compaction 保留 checkpoint 歷史，但撤銷不符版本候選；復原仍依目前來源、Scope、政策新核准。不從摘要、saved work、歷史或別 client 重建資格；新程序／--resume 沒有舊 checkpoint。 |
| CKPT-070 | 退出／Dispose 停新受理、取消並等待自有核准、Git／工作區操作、發布查核及精確暫存清理後才釋放政策。Unconfirmed、程序收尾不確定、CleanupFailed 及輸出故障的嚴格停止／退出優先；不自動重試、重新載入備份或掃描殘留。 |

### 10.1 固定結果與錯誤

下表是 CKPT-020、022、024、040～049、058、065～070 的固定分類。指令錯誤碼由本份擁有；原安全／I/O 分類保留，不能變更公開 LlmErrorCode。捕捉失敗影響原寫入時，新增相應 internal WorkspaceErrorCode／ToolErrorCodes，原工具成功資料形狀不變。

| 情境 | 固定碼／寫入事實 | 後續 |
|---|---|---|
| 不合法命令／ID | invalid_checkpoint_command／invalid_checkpoint_id，NotApplied | 回輸入，零發布。 |
| 未知或已忘記 | checkpoint_not_found，NotApplied | 不掃描或猜測其他 ID。 |
| 容量不足 | checkpoint_limit，NotApplied | 原寫入鏈停止，resource_limit。 |
| 非 Execute | mode_restricted，NotApplied | 不藉復原切模式。 |
| 新建檔／資料不明／不支持 | checkpoint_manual_required，NotApplied | 明示原因與安全單一路徑，交人工。 |
| 已復原／順序不符 | checkpoint_already_restored／checkpoint_order，NotApplied | 不重新套用或跳過新筆。 |
| 目標／Git 衝突 | checkpoint_conflict／checkpoint_git_conflict，NotApplied | 清基準、該路徑鏈轉人工，無 force。 |
| 候選／紀錄版本變動 | checkpoint_revision_conflict，NotApplied | 丟候選，不套用新內容。 |
| 預覽過大／拒絕 | preview_too_large／approval_declined，NotApplied | 保留仍安全資料，下一次重新預覽。 |
| 發布前取消／EOF | turn_cancelled／end_of_input，NotApplied | 分別回輸入／正常退出；已發布按下兩列。 |
| 發布並精確讀回 | none，Verified | 保留事實、更新鏈及代次、進 Plan。 |
| 已發布嘗試但無法核對 | write_unconfirmed，Unconfirmed | 停止該路徑鏈並交人工，更新代次／進 Plan。 |
| 自有暫存清理失敗 | cleanup_failure，保留原 WriteState | 退出 1，不能改稱未變更。 |
| 規則／路徑／編碼／I/O | 原 rules_*、sensitive_*、symbolic_link、outside_workspace、workspace_unavailable、unsupported_metadata、publish_unsupported、access_denied、io_failure 等 | 依原安全與發布前後分類；不輸出原始例外、errno、命中內容或暫存名。 |
| 服務／核准失效或內部狀態不合法 | checkpoint_unavailable，保留已知事實 | 本機不可恢復者退出 1；不偽造成功。 |

## 11. 既有契約的明示增量與後續同步

| 契約擁有者 | 本份後續產品增量／保持界線 |
|---|---|
| SPEC 第 8、12、13、15 章；第 03／05 份 | 增加四個本機命令、內部狀態／錯誤與生命週期；復原不走模型工具，不改 SDK 配對／原生歷史或核准的信任來源。 |
| 第 06 份 EDIT-004、007～012、018、044～063 | 增加 checkpoint 預留與來源記錄、限定 ID 的復原入口；調整「無備份／復原」階段限制。原候選、byte 驗證、模式／規則、發布與清理不放寬，沒有工作區刪除。 |
| 第 09 份 PLAN-037～043、050～054 | 增加本機復原的執行確認撤銷及 EvidenceEpoch；PLAN-043 的歷史事實語意保留，較早 facts 不再供復原後未完成任務選取。一般寫入任務限制不改。 |
| 第 10 份 BUILD；第 11 份 CTX-050～080 | 既有原生程序隔離、預算及 test 不支援保持；新增必備復原附錄、容量與候選代次。不將 build 產物視為可復原，不退款。 |
| 第 12 份 RULE-016、040、048～064、RULE-HO-008 | 復原套用目前 Scope／規則；來源 Stale 收尾先記事實後失效。checkpoint 歷史獨立保留不代表舊確認仍有效。 |
| 第 13 份 GIT-004、014～015、024～040、FUT09-HO-001～010 | 新增用途限定 metadata guard 與借用租約的內部路徑；原 GitRead recipe／原生政策保持，GitSnapshot 不升格成 checkpoint，沒有 Git mutation／index 復原。 |
| 第 14 份 TERM-049～059、065～074 | 新本機命令遵守單行閘門／不入歷史、busy 唯一讀取者與隱藏新確認；不從貼上或歷史推定授權。 |
| 第 15 份 SAVE-005、011、036～037、059、066、SAVE-HO-008 | 無格式遷移、原始 bytes／checkpoint 資格或核准匯入；新增復原版本對保存候選有效性的影響。工作摘要與檔案復原分別驗收。 |

| 編號 | 相容性要求 |
|---|---|
| CKPT-071 | 後續實作須依上表同步相關契約的產品接續說明及回歸，保留原階段歷史，不只寫「沿用」。新增 internal API／enum／錯誤、事實 epoch、投影與版本戳須接到真實 Chat／Workspace／Runner，而非空殼服務。 |
| CKPT-072 | 後續才同步 /help、README、SPEC 與相關測試，明示程序內保留／clear 損失、容量滿停止、新建檔人工、每次單檔新確認、metadata 限制及分層證據。本次只更新 ROADMAP 的文件入口／狀態；不提前宣稱 FUT-09 已接入產品。 |

<a id="upstream-handoffs"></a>

## 12. FUT 耦合矩陣與固定交接 memo

本節為 FUT-09 交接的單一來源，不另建立內容重複的 memo。原第 06／09／11／12／13／14／15 份仍分別擁有安全寫入、任務證據、Context、規則、Git、輸入與工作保存契約；本份只擁有檢查點捕捉／保留、復原與明示的相容性增量。沒有必須合併開發的 FUT。

### 12.1 上游必讀交接逐項承接

| 來源交接 | 本份承接位置／需求 | 後續驗收 |
|---|---|---|
| FUT09-HO-001 | 第 1、5 節；CKPT-003、027～030：獨立受控 before／after，不用 HEAD／GitSnapshot。 | CKPT-AC-001、007 |
| FUT09-HO-002 | 第 5～7 節；CKPT-028、032、035～042：只復原准入工作檔，不保存／改寫 index。 | CKPT-AC-007～010、019 |
| FUT09-HO-003 | 第 6～8 節；CKPT-035～045、049～055：完整 bytes／身分／mode、Git tracking、版本與反向鏈。 | CKPT-AC-008～014、017～018 |
| FUT09-HO-004 | 第 2、5、9 節；CKPT-008、027、031、059～060：來源只取受控事件，facts 保留歷史。 | CKPT-AC-001、007、020～021 |
| FUT09-HO-005 | 第 3、6、7 節；CKPT-012、015～019、037～038、043～048：本機 ID、新核准、安全發布；無 Git mutation。 | CKPT-AC-002～004、009、015～016 |
| FUT09-HO-006 | 第 4、10 節；CKPT-021～026、065～070：有界記憶體、敏感、防洩漏及清除，無持久備份。 | CKPT-AC-005～006、025～028 |
| FUT09-HO-007 | 第 1、3、5、7 節；CKPT-005、020、028、030、050：Create／缺資料／不支持 metadata 人工處理。 | CKPT-AC-011、019、025 |
| FUT09-HO-008 | 第 7、9 節；CKPT-043、058、064：目前 Valid 規則核准，先結果後 Stale，不復活授權。 | CKPT-AC-022～023 |
| FUT09-HO-009 | 第 7～10 節；CKPT-048、053～061、068～070：發布後事實、逐檔部分結果、代次及取消。 | CKPT-AC-015～018、024、026～028 |
| FUT09-HO-010 | 第 9～11 節；CKPT-063、066、069、071：SAVE 不帶 bytes／資格／核准，兩者獨立。 | CKPT-AC-023、027 |
| RULE-HO-008 | 第 7、9 節；CKPT-043、048、058、064：承接 RULE-040、053～055，復原 AGENTS 不能回復舊規則版本。 | CKPT-AC-022～023 |
| TERM-HO-008 | 第 3、7、10 節；CKPT-014～019、046、065～070：輸入歷史不是備份，新預覽／新確認。 | CKPT-AC-002～004、025、029～031 |
| 第 15 份 11.2／SAVE-HO-008 | 第 1、9～11 節；CKPT-003、005、063、066、069、071：工作脈絡與檔案復原獨立，沒有跨次 checkpoint。 | CKPT-AC-023、027 |

<a id="future-handoffs"></a>

### 12.2 對其他 FUT 的前向交接

| 交接編號／對象 | 本份已定邊界 | 後續責任及驗收 |
|---|---|---|
| CKPT-HO-001／FUT-01 | 搜尋／局部讀取不建立 checkpoint；復原使 read_file 基準失效（CKPT-008、032、049）。 | 搜尋或索引 UI 不把歷史 before／after 當目前正文／修改快照；新寫入仍完整讀取。驗收 AC-001、014、033。 |
| CKPT-HO-002／FUT-02 | 不收錄 build 產物／程序副作用；復原後舊 Validation 證據不再可選（CKPT-031、058～060）。 | 未來 recipe／修正循環須另外定義可追蹤副作用；不宣稱 rollback build／test，不解除原生或 test 門檻。驗收 AC-021、024、033。 |
| CKPT-HO-003／FUT-03 | 目前狀態仍由 PlanStore 擁有；獨立 EvidenceEpoch、舊 Completed 歷史與本機復原入口（CKPT-057～060）。 | 任務／證據改版須保留 epoch 選取與容量；不能拿已撤回 A 的 fact 完成 A／B。驗收 AC-020～021。 |
| CKPT-HO-004／FUT-04 | 必備目前復原附錄與 RecoveryRevision，有界且零額外生成（CKPT-061～063、067）。 | 新壓縮／量測策略保留版本失效與警示，不從摘要重建 anchor，不提高 body／預算。驗收 AC-023～024、027。 |
| CKPT-HO-005／FUT-05 | 目前 Scope／Valid 規則、新確認及 AGENTS 先記事實後失效（CKPT-043～045、064、069）。 | 多 Scope／來源改版須明示目前規則准入與歷史保留，不能復活原 yes。驗收 AC-022～023。 |
| CKPT-HO-006／FUT-06 | Git 唯讀觀察只作衝突閘門，不是復原資料／作者證明（CKPT-035～042）。 | 擴充 Git 布局或 recipe 時重驗目標 metadata、借用租約與原生政策；不偷加入 index／Git 寫入。驗收 AC-007～010。 |
| CKPT-HO-007／FUT-07 | 單行本機命令與新隱藏確認；歷史／貼上不授權（CKPT-013～019）。 | 輸入／進度改版保留唯一讀取者、預輸入排空及發布後結果，不以草稿預覽取代復原差異。驗收 AC-029～031。 |
| CKPT-HO-008／FUT-08 | 沒有 checkpoint 持久化或資格交換，SAVE v1 不增欄位（CKPT-063、066、069）。 | 若未來另訂跨次備份，須新版本／敏感保存／存取權／清除／重驗契約，不能直接重用工作存檔。驗收 AC-023、027。 |
| CKPT-HO-009／FUT-10 | 技能文字不是寫入事件、操作權限或新確認（CKPT-008、012、031）。 | 流程若引用復原，只能指示使用者或組合已授權能力；不從步驟／yes 直接呼叫本機入口。驗收 AC-025、033。 |
| CKPT-HO-010／FUT-11 | 外部工具／同名結果不進來源集合（CKPT-008、031、065）。 | 新寫入來源須獨立定案可驗證事件、衝突、敏感與授權，不將外部自報視作本機 Verified。驗收 AC-025、033。 |
| CKPT-HO-011／FUT-12 | 網頁／文件內容沒有本機復原資格（CKPT-008、012、065）。 | 引用與內容不得成為 before、路徑映射或 yes；網路副作用不納入本版。驗收 AC-025、033。 |
| CKPT-HO-012／FUT-13 | checkpoint／政策綁當前 session，換 Key 丟資料（CKPT-007、012、065～069）。 | 新模型／供應商切換須明示政策與資料重驗，不能跨 client 恢復核准、原生歷史或舊資格。驗收 AC-025、027。 |
| CKPT-HO-013／FUT-14 | 單一租約、來源事件與核准者；無跨 Agent 共享儲存（CKPT-010～012、031）。 | 平行寫入、父子證據／預算／取消及衝突須另定；子 Agent 結論不授權復原，不從共享 path 猜來源。驗收 AC-025、028、033。 |

| 編號 | 交接要求 |
|---|---|
| CKPT-073 | 上游 13 列逐項回填本份章節／需求／案例並保留原錨點；尤其不得略過 FUT09-HO-003 的 Git 追蹤狀態、FUT09-HO-008 的規則復原或 SAVE-HO-008 的獨立邊界。 |
| CKPT-074 | 本節為本份交接 memo 唯一來源。適用後續 FUT 須核對 CKPT-HO-001～013 與當時產品；改共用介面、來源、證據、版本、容量或權限須列明受影響契約及遷移，不複製另一套擁有權。 |
| CKPT-075 | 必要能力是受控寫入、目前規則／工作區閘門、核准及目前狀態整合；Git 工作區另需可用 guard 才能自動復原，非根 Git 缺席分支依 CKPT-035。FUT-08 無必要前置，FUT-10～14 無必要格式依賴，不要求合併開發。 |
| CKPT-076 | 前向交接不是其他 FUT 的完成證據、預先授權或共享格式；不新增未開發 FUT 的空殼介面、存檔欄位、模型工具或權限。ROADMAP 只標本規格已定義／待實作，並指向本節。 |

## 13. 後續產品驗收

本節所有案例均為後續待執行。本次未執行產品 build、MSTest、Python 掃描測試、PTY、真人或真實 Gemini；文件查核另見第 14 節。離線使用假終端機、檔案系統、程序／HTTP handler 與合成資料，不讀真實 Key；原生案例只在隔離的明確測試工作區，從測試框架管理自有單檔，不批次刪除。

### 13.1 離線、整合與原生案例

| 案例 | 對應需求 | 成功條件 |
|---|---|---|
| CKPT-AC-001 | CKPT-003、007～011、027～034 | 真實 create／replace 入口才捕捉；before／after 完整、原 dirty／空檔／Absent／BOM／CRLF／LF／無尾換行區分正確；read／search／Git／模型聲稱均不能建紀錄，無計畫也有來源 ID。 |
| CKPT-AC-002 | CKPT-012～015、020 | 四命令大小寫、空格、ID 1／long.MaxValue／溢位／前導零／path／多 ID／多行嚴格拒絕；Plan 的 restore 零發布，查詢／forget 可用；零生成／工具回合。 |
| CKPT-AC-003 | CKPT-012、016～019、046 | 每次完整新預覽／新確認；拒絕、空白、Ctrl+C、EOF、預輸入及非法 enum 都不能套用；原 yes／show／模型 yes 無效。預覽 65536 可接受、65537 零核准。 |
| CKPT-AC-004 | CKPT-010、015～018、020、025 | list／show 零目標 I/O；forget 只丟指定記憶體、新確認與版本重驗，拒絕／版本變動不丟其他筆；busy 第二操作 unavailable，無雙讀取者／巢狀租約。 |
| CKPT-AC-005 | CKPT-021～024、026 | 32／33 筆與 8388608／8388609 bytes、131072／131073 單側邊界；含預留、BOM、Restored／Manual 筆數。滿額停止新寫入，零核准／暫存、不淘汰；forget 後可重新預留，序號不重用。 |
| CKPT-AC-006 | CKPT-022～026、029～030、034 | NotApplied 釋放預留；Verified／Unconfirmed 消費一筆；發布後紀錄故障保留寫入事實並停本機，clear 競態不補回。原 snapshot LRU 不淘汰 checkpoint。 |
| CKPT-AC-007 | CKPT-003、027、031～032、035、042 | 隔離無根 .git、tracked dirty、untracked／ignored 已准入工作檔；恢復 before 而不是 HEAD／index；不掃額外內容，根外 Git 不讀／不宣稱已核對。 |
| CKPT-AC-008 | CKPT-035～041 | 捕捉後 .git 新增／消失、HEAD 切換、git add／rm --cached／ITA 改變時，工作檔 bytes／inode 未變也拒絕；最初預覽前非支持布局／Git 不可用／超限／逾時可標人工，不假裝缺席。已有穩定 guard 後查核失敗則 NotApplied，不沿用原 yes 降級發布。 |
| CKPT-AC-009 | CKPT-010、035～042 | 假轉接器驗固定 recipe／literal target、兩次穩定觀察、規則及租約；真實受控 Git fixture 核對 metadata，不啟動 shell／blob／helpers／網路，不改 index、HEAD、refs 或原生政策。 |
| CKPT-AC-010 | CKPT-036～042 | staged／unstaged 互抵、unborn、metadata 同長變更、stage／tag／ITA／mode；不支持條目人工。只有目標 guard 作比較，無全域 index digest；成功或拒絕後 index bytes 均不被產品改寫。 |
| CKPT-AC-011 | CKPT-005、020、028、030、042、050 | 新建 0-byte／有內容檔、缺 before、source_unconfirmed、特殊 mode／非文字／人工紀錄均不發布、不刪除，顯示安全原因；普通 mode 可精確恢復。 |
| CKPT-AC-012 | CKPT-043～045、049～050 | 預覽前／後外部同長改寫、不同 inode、刪檔、權限／種類改變、父目錄替換、連結／根換代；完整比較拒絕，無 force／三向合併，已觀察衝突不自動重接。 |
| CKPT-AC-013 | CKPT-043～047、050 | 真實隔離 macOS 檔案驗反向 diff、BOM／各種換行／Unicode、普通 mode、同目錄原子替換與讀回；不經 replace_text／read_file 補基準，metadata 與 TOCTOU 保證範圍有明文。 |
| CKPT-AC-014 | CKPT-011、045、049、051～053 | 讀取基準與 checkpoint 分離；成功／衝突／Unconfirmed 清 read snapshot，反向鏈重綁不建立 replace_text 基準，下一次一般修改需完整讀檔。 |
| CKPT-AC-015 | CKPT-024、047～049、058、068～070 | 故障注入暫存建立／write／flush／mode／close／暫存核對／發布／讀回；NotApplied／Verified／Unconfirmed 正確，單一路徑精確清理，無 retry／copy／原檔截斷。 |
| CKPT-AC-016 | CKPT-018、033～034、048、054、058、068～070 | 發布決策前／後取消、clear／換 Key／Dispose、讀回／顯示／清理故障；已發生事實不被抹除，CleanupFailed 退出 1，不因規則／取消降級，遲到結果不復活資料。 |
| CKPT-AC-017 | CKPT-051～054、056 | A→B→C 只可先退 C→B，Verified 後舊筆 anchor 綁新 inode 再退 B→A；原歷史不變，restore 無新 checkpoint／redo；原筆重複 restore 拒絕。 |
| CKPT-AC-018 | CKPT-025、045、049、051～056 | forget 尚未消費的中間／最新筆、外部改寫、別名、Unconfirmed 造成斷鏈；forget 已 Restored 筆不撤銷已完成的獨立 anchor 重綁。不跨缺口、不以相同 bytes 補判成功，新受控寫入可建新鏈；A 成功 B 衝突分別回報。 |
| CKPT-AC-019 | CKPT-005、020、028、042、050、056 | 新檔人工核對／刪除指引、index 不復原、多檔各自新確認；沒有模型刪除／批次清理或工作區全回復承諾。 |
| CKPT-AC-020 | CKPT-008、057～060、071 | A Verified、B 失敗→復原 A→重新確認原任務完成 B：原 A fact 不可用，須新 epoch A 證據；舊 OperationId／Completed 保留歷史，原 task／requirement schema 不變，無 restore 成功 fact 注入。 |
| CKPT-AC-021 | CKPT-057～060、064 | Verified 及 Unconfirmed 均先收尾後增加代次、撤確認進 Plan；舊 Read／Write／Validation facts 全不可再選。NotApplied 不增代次；不虛增 RulesRevision，Completed 重做需新任務，普通寫入仍要原任務限制。 |
| CKPT-AC-022 | CKPT-043～045、058、064、069 | AGENTS 復原先以目前 Valid 規則核准／發布／記事實，再 Stale；必須 reload／重新確認／新指示。原本 Stale／Unavailable、規則敏感或不可重載時人工；舊 bytes 不復活授權。 |
| CKPT-AC-023 | CKPT-011、043、061～064、069、071 | Scope／規則版本變動保留歷史、撤舊候選；有界 mandatory 附錄進一般／Review／摘要／保存摘要與估算。RecoveryRevision 使 measure／compaction／save 候選過時；原生歷史不改寫、SAVE 不增欄位。 |
| CKPT-AC-024 | CKPT-014、056、058、061～063、071 | 連續 A／B 復原後最後結果與累計代次正確；forget 不抹提示。32768 附錄及實際 262144-body 獨立限制，超限走 ContextCapacity、不省略／補送；附錄敏感沿共同安全重設。零額外生成／退款，不將舊建置證據當目前驗證。 |
| CKPT-AC-025 | CKPT-007～008、012、017、026、031、065～066 | 合成短 Key／固定欄名／Unicode／預覽／Git metadata 敏感碰撞拒絕；假 yes、Skills／MCP／網頁／子 Agent 自報不能建資格或升權；無 raw bytes／Key／應用指示落地、模型或錯誤輸出。 |
| CKPT-AC-026 | CKPT-009～011、018、024、048、067～070 | 普通取消與安全 reset、發布後查核及退出優先序；無不受限結果日誌，所有 owned operation 收尾後才釋放，單一租約與讀取者維持。 |
| CKPT-AC-027 | CKPT-025～026、034、061～063、066～070 | clear／安全重設／新 Key／退出丟紀錄與結果但不回復檔案／退款、不重用 ID 或 stamp；clear 後 revision 單調但無結果附錄，不殘留／捏造舊 path。普通 compaction／reload 保留歷史；--resume 不帶 checkpoint，forget 不抹復原提示。 |
| CKPT-AC-028 | CKPT-010～012、024、033～034、048、068～070 | 第二操作、遲到核准／Git／I/O、服務釋放、候選偽造／世代變動，零非法發布；已發布的必要事實與代次收尾不被中途失效吃掉。 |

### 13.2 PTY、真人、真實服務與回歸

| 案例 | 對應需求 | 方法與成功條件 |
|---|---|---|
| CKPT-AC-029 | CKPT-013～019、046、048、056、072 | macOS PTY：合法／拒絕／空白／Ctrl+C／EOF／預貼 yes、show 與 restore 差異、Plan 閘門、連續復原與 busy；每次新提示及唯一讀取者，發布後安全事實先顯示。 |
| CKPT-AC-030 | CKPT-014～019、065、067～070、072 | macOS PTY：命令不進歷史、多行拒絕、F2／F3／F4 不進確認、短 Key 零敏感回顯、清除／換 Key／輸出故障／EOF 收尾與終端機恢復；不宣稱已證明所有貼上來源或真人行為。 |
| CKPT-AC-031 | CKPT-004、006、015～019、046、050、056、064、072 | 真人終端機另記 OS／架構／終端機／日期：中文 Unicode、差異可讀性、新確認、取消、人工新檔指引、AGENTS reload 及模式交接；PTY 不抵真人驗收。 |
| CKPT-AC-032 | CKPT-006～008、012、029、057～065、072 | 明確授權後才跑真實 Gemini：修改後本機復原，再給新指示，模型收到目前附錄且重讀來源；模型失敗保留本機事實，無自動續跑／資格重建。此案例不替代離線／原生證據。 |
| CKPT-AC-033 | CKPT-001～080 | 後續實作才依 AGENTS 順序 restore／build／MSTest／help、Python 掃描器測試、PTY、format verify 與 diff check，保留各階段來源／核准／預算／保存回歸；舊 Release／MTP／真人／服務待辦不得被整體「通過」掩蓋。 |

## 14. 本次文件查核與完成條件

| 編號 | 文件要求 |
|---|---|
| CKPT-077 | CKPT-001～080、CKPT-AC-001～034、CKPT-HO-001～013 唯一連續，需求皆有案例／固定交接可追溯。工具來源、預留、上限、guard、鏈、事實代次、附錄、規則與生命週期一致，不留待實作者選方案的 TBD。 |
| CKPT-078 | 檢查完整新檔、Markdown 表格／圍欄、相對連結、穩定錨點、需求／案例引用及敏感外形；不能因新檔尚未追蹤而只檢查普通 git diff。本份不附完整應用指示、憑證、SDK／HTTP 原文或可命中的假秘密。 |
| CKPT-079 | 檢查 git diff --check 及新檔 no-index 空白診斷；no-index 的差異存在與空白錯誤須分開判定。核對僅本檔新增及 ROADMAP 最小修改、原索引／HEAD 不變，沒有產品程式、測試、套件、其他 memo／產物或提交／推送。 |
| CKPT-080 | 完成回報只列實際文件查核與耦合結論，產品離線／原生／PTY／真人／服務均標待執行。後續開發先讀本份、上游固定交接與當時產品，再同步第 11 節；規格完成不等於 FUT-09 產品已完成。 |

| 案例 | 對應需求 | 文件成功條件 |
|---|---|---|
| CKPT-AC-034 | CKPT-001～006、071～080 | 版本／待實作／基線與範圍正確；編號、需求覆蓋、13 列上游與13列前向交接、Markdown／連結／錨點／敏感外形／空白通過。獨立 bytes、Git 衝突、反向鏈、epoch、必備附錄、AGENTS 與 SAVE 邊界一致；僅兩份文件改動，索引及 HEAD 不變，零產品實作／產品驗收。 |

## 15. 參考與後續閱讀

- [ROADMAP](../ROADMAP.md)：FUT-09 選題、獨立交付與相依關係。
- [06：安全寫入](06-file-edit-and-approval.md)：原始候選、逐 byte 基準、預覽、單次核准、原生發布、結果與清理。
- [09：Plan](09-plan-mode-and-task-list.md)、[11：Context](11-context-and-budget-management.md)：事實擁有者、完成選取、摘要、容量及生命週期。
- [12：規則](12-project-rules-loading.md#future-handoffs)、[13：FUT-09 交接](13-git-diff-and-code-review.md#fut-09-handoff)、[14：終端機](14-terminal-interaction.md#future-handoffs)、[15：工作保存](15-work-save-and-resume.md#fut-09-handoff)：原始固定契約，不能以本份摘要取代。
- [上游承接](#upstream-handoffs)、[前向 memo](#future-handoffs)、[復原流程](#restore-contract)、[狀態整合](#state-integration)：後續開發／交接的本份單一來源。
