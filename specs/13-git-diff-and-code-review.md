# 13：Git 差異與程式碼審查開發規格書

## 第 20 份產品實作接續（2026-10-29）

承接 GIT-HO-011：各 profile 仍以相同三項 Git 工具及原報告驗證進行 Review；切換清除來源、候選與報告，不能失敗後換模型重送。Git 觀察與模型判斷仍不建立本機完成事實。 詳見[第 20 份規格](20-multi-model-and-provider-support.md)；A、B 分層證據見 [README](../README.md#model-validation)，真實服務／真人與既有 Release 未結門檻另列。

## 第 19 份產品實作接續（2026-10-28）

承接 GIT-HO-010：Review 維持三個 Git 工具、零 MCP 網路；文件 URL、hash、時間及自報內容不進 GitSnapshot、位置驗證或本機測試證據。 詳見[第 19 份](19-web-search-and-document-fetch.md)及其固定交接；當次分層證據與未驗項目見 [README](../README.md#web-validation)。

## 第 18 份產品實作接續（2026-10-28）

Review 仍只提供三項本機 GitRead、零 MCP I/O；新 Review 可在 MCP Stale 下按原來源閘門執行，但不能採用舊共同候選。外部同名、oid、行號與自報測試／復原不建立 GitSnapshot、本機證據或 checkpoint。 詳見[第 18 份](18-mcp-external-tools.md)及其固定交接；實際驗證與未驗層級見 [README](../README.md#mcp-validation)。

## 第 17 份產品實作接續（2026-10-27～28）

承接 GIT-HO-008：明確 /review 可帶目前技能，但固定三個 GitRead 工具、報告 Schema 與本機來源驗證優先；Git 直接入口與審查結果也重驗技能，流程文字不能啟動審查或升權。詳見[第 17 份](17-reusable-workflows-and-skills.md)及[當次分層驗證](../README.md#skills-validation)。歷史文件交付敘述保留，未結原生／真人／真實 Gemini／Release 門檻不變。

## 第 16 份產品實作接續（2026-10-27）

新增借用既有 workspace lease 的目標 metadata guard：兩次有限觀察、literal target 的 HEAD entry／index stage/tag/mode/oid／ITA，加根／gitdir／executable／HEAD／format。沒有 blob、全域 index digest、額外 mutation recipe、持久寫入或新原生例外；唯一 /dev/null 例外保持。Git guard 只拒絕衝突，原始 before bytes 仍由受控寫入持有。 詳見 [第16份](16-change-checkpoints-and-recovery.md) 與 [當次分層證據](../README.md#checkpoint-validation)。下列原階段敘述保留歷史範圍。

## 第 15 份產品實作接續（2026-10-26）

[工作保存與續接](15-work-save-and-resume.md)的限定增量：私人存檔加入安全投影與原生 Git 拒絕，不能藉 ignore／原生可讀來源洩露。保存摘要另排除 structured Git snapshot_id 與來源識別；不保存 finding、raw blob、ledger 或舊核准，續接不建立 Git 觀察，/review 仍取目前來源。 本節保留舊階段歷史，實際分層驗證見 [README](../README.md#work-save-validation)。

## 第 14 份產品實作接續（2026-10-26）

[第14份終端機操作改善](14-terminal-interaction.md)只擴充一般ReadInputAsync的隱藏多行／文字元素編輯、F2安全預覽、F3明確貼上、F4換行及程序內安全歷史；Key／認證選單／三種核准仍走原隱藏單行。多行斜線指令在所有解析器之前整筆拒絕，單行精確匹配與Scope／pin字面餘文保持。

CLI輸入狀態加入共同安全清除、認證選單前清除及政策／退出生命週期；不加入Context／摘要／計畫／Git來源或完成證據，沒有新增生成、工具與權限。RULE-HO-006／GIT-HO-006的承接細節見第14份第7、8、9、11、12節與TERM-AC-016～026。原階段歷史保留，當次證據及真人／服務／原生／發布待辦見[README](../README.md#terminal-interaction-validation)。


## 產品實作接續與准入修訂（2026-10-26）

本檔 GIT-001／090 與 GIT-AC-023 的「本次只交付文件」是原 0.1 撰寫階段的歷史界線。使用者後續明確要求依本規格實作產品；本次增量、當次證據與未驗證項目見 [README](../README.md#git-review-validation)，原需求、案例及固定交接編號保留。

實際 Git 2.54 啟動會以 O_RDWR 開啟 `/dev/null`。使用者已明確選擇：GIT-026／031 的拒寫政策僅新增 `/dev/null` 的 `file-write-data` OS null-sink 例外；所有持久檔案寫入、建立、刪除、重新命名及其他權限仍禁止，不建立暫存或報告檔，不沿用 .NET 可寫權限。原生 fixture 與既有發布門檻仍須獨立驗證。

`check-ignore --stdin` 不支援通用 `--literal-pathspecs`，且內部仍解析前綴 magic。GIT-024 的此項 recipe 改為不帶該旗標；每個已准入候選在 NUL stdin 前加固定 `./`，回傳 pathname 僅去除同一個固定前綴後精確比對候選集合。其餘 recipe 保留 literal pathspecs；模型仍不能提供 argv、oid 或未准入路徑，冒號及 glob 字元按字面檔名處理。


> 文件版本：0.1｜建立日期：2026-10-25｜狀態：待實作
>
> 選題依據：[ROADMAP：FUT-06](../ROADMAP.md#fut-06git-差異與程式碼審查)。上位文件：[總規格](../SPEC.md)第 5、8～15 章；承接 [04：工作區與唯讀工具](04-workspace-and-read-tools.md)、[05：Agent 工具循環](05-agent-loop.md)、[06：檔案編輯與核准](06-file-edit-and-approval.md)、[07：預算與整體驗收](07-budget-and-acceptance.md)、[08：搜尋與局部讀檔](08-code-search-and-partial-read.md)、[09：Plan 與任務](09-plan-mode-and-task-list.md)、[10：受控建置](10-controlled-build-and-test.md)、[11：Context 與預算](11-context-and-budget-management.md)及 [12：專案規則](12-project-rules-loading.md)。尤其須承接第 12 份 [RULE-HO-005／008](12-project-rules-loading.md#future-handoffs)。FUT-06 是大綱代號，不是第 06 份寫入規格。

## 1. 目的、選定方案與文件交付

使用者在自行提交前，能核對本次工作區的實際變更，取得有來源位置的模型審查，並知道哪些內容未檢查。Git 觀察、模型問題判斷、建置測試與修改復原各自判定，不能互相代替。

| 編號 | 要求 |
|---|---|
| GIT-001 | 本次只新增本規格及最小 ROADMAP 入口／狀態更新，不修改其他文件、產品、測試、套件、設定、索引或歷史，不建立額外計畫、紀錄檔、工作樹或 hook，不提交、推送或發布。以下產品介面及產品驗收均待後續實作。 |
| GIT-002 | 文件核對基線為乾淨 `main@d1215d7801b8564011838bf7c856dd7f5b59ed03`；已閱讀 ROADMAP、總規格、相關子規格、README，以及工具登錄、Runner、PlanStore、Context、規則／工作區租約、程序輸出與開發掃描器的相關結構。既有驗證紀錄不是本次驗收結果。 |
| GIT-003 | 使用者選定提交前審查、專用 `/review` 及目前 Scope 的安全子集。第一版不比較任意分支、commit 或 merge-base。另一方案把分支比較一起交付會增加基準與參照變動語意；沿用一般聊天則不足以固定唯讀審查回合及可驗證報告，因此不採用。 |
| GIT-004 | 不提供 add、commit、push、pull、fetch、merge、checkout、switch、reset、restore、stash、apply、clean、刪除、遠端 PR 或檢查點功能。不得藉內部 Git 能力增加一般 shell、自由 argv 或新的寫入入口。 |
| GIT-005 | Key 仍只經隱藏輸入取得並留在記憶體；不得交給 Git、程序環境／stdin、Git 設定、快照、報告、Context 工具資料或持久化。一般工作區工具仍禁止 `.git`；專用例外僅限本份列明的唯讀用途。 |
| GIT-006 | 第 07 份 BUD-S003 Release 阻擋、真人／真實服務／分發待辦，以及第 10 份 test 不支援和原生完整驗收待辦均保留。文件完成或未來審查成功不解除上述門檻，也不宣稱完成安全掃描或所有錯誤檢查。 |

## 2. 目前基線、責任與相容性

目前沒有產品 Git 工具或 `/review`；開發用途的 Python 暫存內容掃描器不是產品服務，不能直接當成已交付的 Git 審查轉接器。現有產品已有七項檔案工具、submit_plan、update_task、run_dotnet_task，依 Plan／Execute 提供六／九項。規則 Scope、排他工作區租約、敏感政策、寫入核准、計畫證據及生成額度須共同承接。

| 接續元件 | 後續責任 |
|---|---|
| ChatApplication／CLI | 指令與單一輸入讀取者、審查回合入口、報告顯示與生命週期；不自行執行 Git 或產生另一個模型 client。 |
| GitReadAdapter／GitSnapshotCoordinator | 受控 Git recipe、用途限定的 metadata 讀取、不可變快照、來源及變動重驗；不持有 Key、SDK 紀錄、核准或 PlanStore 寫入權。 |
| CodeReviewCoordinator／Runner／Tools | 同一 Runner 的審查覆蓋政策、固定快照、來源提供帳本、最終報告驗證、用量與結果發布；不由模型決定覆蓋完整性。 |
| Workspace／ProjectRules | 重用安全路徑、種類、無連結、穩定讀取及規則閘門，與檔案／程序共用同一租約；專用讀取不建立寫入快照。 |
| PlanStore／Context | 保留原事實所有權與清除語意；Git 探索不是既有 Read requirement、Write／Validation 完成證據，模型分析亦非本機驗證。 |

| 編號 | 相容性要求 |
|---|---|
| GIT-007 | 公開 ILlmClient、LlmRequest、LlmResult、LlmMessage 與 SDK 不透明協定所有權保持；新增服務及型別為 internal，SDK 型別留在 Llm/，不增加供應商或 NuGet 套件。 |
| GIT-008 | 含路徑、文字、指紋或集合的型別使用密封類別、唯讀屬性、防禦性複製及固定型別標記 ToString；只用明確 writer 投影，不序列化整個服務、session、原生紀錄或底層程序描述。 |
| GIT-009 | 原十項工具的登錄順序、輸入與成功資料保持；依序追加 git_status、git_diff、read_git_file_range，新增明確 GitRead 能力，Plan／Execute 均可使用，未指定能力仍預設禁止。一般模式 Definitions 成為九／十二項；審查覆蓋僅提供這三項。 |
| GIT-010 | 一般回合可用 Git 工具探索，結果記在安全 LocalToolOutcome；不擴充 Plan Read requirements 或替其補造成功事實。Analysis 任務完成仍只標 ModelAssessment。Git 工具失敗若發生於有在途任務的一般回合，沿既有任務受阻／停止規則處理。 |
| GIT-011 | `/review` 不以 update_task 改既有任務，不取得／撤銷執行確認，也不刷新寫入基準；規則失效、共同安全重設等原有全域事件仍可依原契約撤確認。不得為了維持審查回合隔離而忽略這些事件。 |

## 3. 基準、Scope 與安全選取

| 層／selection | 舊側 old | 新側 new | 呈現語意 |
|---|---|---|---|
| staged | 捕捉的 HEAD tree blob；unborn 為缺席 | 捕捉的 stage 0 index blob | 自行提交時準備送出的這一層。 |
| unstaged | 捕捉的 stage 0 index blob | 安全讀取的工作檔，或缺席 | 相對暫存區尚未暫存的這一層。 |
| untracked | 缺席 | 安全且未被允許來源忽略的工作檔 | 不是 HEAD／index 內容，不以零 oid 偽裝成已追蹤。 |
| all | 上述三層各自的舊側 | 上述三層各自的新側 | 不把三層折成單一 HEAD→工作目錄差異。 |

| 編號 | 選取要求 |
|---|---|
| GIT-012 | 層別採上表；同一路徑可同時有 staged／unstaged。即使 HEAD=A、index=B、工作檔=A，也須保留兩個變更。不得用 Git 總差異為空推定沒有準備提交的變更。 |
| GIT-013 | HEAD 只准解析目前 HEAD，隨即固定成完整 oid；支援 detached HEAD 及尚無第一筆提交的正常 repository。unborn 使用「不存在的舊樹」語意，不寫入 empty-tree object，也不以解析失敗一律假裝 unborn。 |
| GIT-014 | 第一版只支援確認工作區根本身為非 bare 工作樹，根 `.git` 是普通真實目錄且無外部配置。工作區只是 repository 子目錄、`.git` 檔案／連結、linked worktree、commondir、objects alternates、外部 object/index、sparse checkout／sparse index 均回 unsupported_git_layout。不得向工作區外祖先尋找 repository，或讀外部 gitdir 來補齊。 |
| GIT-015 | 無 `.git` 為 not_git_repository；Git 缺失／功能不符為 git_unavailable；原生限制不可用為 capability_unavailable。只關閉 Git 請求，不自動初始化、安裝、改設定或連網；未發生不可恢復故障時，普通聊天與既有能力保持可用。 |
| GIT-016 | 狀態及差異依原始 blob 與工作檔 bytes／存在性／mode 比較，不套用 clean、smudge、textconv、working-tree-encoding、ident 或 EOL 轉換。顯示固定「原始 bytes 視角」；與一般 git status／diff 的正規化結果可能不同，這不是可直接套用的 patch。 |
| GIT-017 | HEAD、index 及刪除舊內容同樣先通過目前工作區相對路徑、敏感名稱、無連結、普通檔案／blob、128 KiB、嚴格 UTF-8、控制字元與當前 Key 政策。歷史來源不能成為讀取目前禁止路徑或秘密的旁路。 |
| GIT-018 | 只選目前 Scope 中通過規則鏈的變更；未載入的更深 AGENTS.md 邊界不進入、不讀來源正文、不自動重載。跨 Scope／受限子樹只給固定未覆蓋說明，不露出範圍外或敏感路徑及隱藏數量。安全子集可繼續，但不能稱全 repository 已審；既有 search_files／search_text 遇新邊界整次拒絕的規則不變。 |
| GIT-019 | untracked 先由安全工作區遍歷取得候選，再按第 4 節的 check-ignore 判斷。允許用途限定地讀取工作區根到候選父目錄的 `.gitignore` 及 `.git/info/exclude`，含 Scope 外祖先；不讀工作區外祖先、使用者全域 excludes 或引用內容。來源須為安全普通檔、單份最多 131072 bytes、深度最多 32，原始 bytes 計入總來源額度；記錄存在／缺席及指紋，內容不投影給模型。不能安全讀取／套用時，整個 untracked 層標 ignore_unavailable，staged／unstaged 仍可觀察。 |
| GIT-020 | 忽略只處理 untracked；已追蹤檔不能因新增 `.gitignore` 被當作不存在。安全政策先於忽略與 Git 狀態，敏感名稱仍隱藏；允許來源忽略、敏感過濾與 Scope 篩選分別呈現為固定範圍說明，不宣稱等同使用者含全域設定的 git status。 |
| GIT-021 | 不做 rename／copy 相似度猜測，呈現舊路徑刪除及新路徑新增；只支援普通檔 mode 100644／100755 的文字內容。非 stage 0、intent-to-add 的占位內容、gitlink／submodule、symlink／特殊檔、非法編碼、二進位、大檔及僅 metadata 的變更，各以安全原因列入未做內容審查；不跟隨、不解衝突、不進子模組或假造文字。 |
| GIT-022 | `no_changes` 只能在選定層候選集合完整、所有可判定項目無變更、且沒有會影響該結論的未知項時使用。只有被排除／不支援項目時回「沒有可審查的文字差異；仍有未檢查範圍」，不是乾淨工作區。零可審查差異由本機零生成回報。 |

## 4. 專用 Git 與外部程序邊界

以下是允許 recipe 的完整種類，不是一般 shell 白名單。參數值只來自已確認根、已驗證的本機 oid／候選及固定常數；模型不能提供命令或 oid。

| Recipe | 固定命令主體／用途 |
|---|---|
| 能力／HEAD | `git --version`；`rev-parse --is-inside-work-tree`、`--is-bare-repository`、`--show-object-format`；`symbolic-ref -q HEAD`、`rev-parse --verify HEAD^{commit}` 與 `show-ref --verify` 僅供目前 HEAD／unborn 判定。不得輸出 branch 名稱、commit message 或作者。 |
| HEAD manifest | `ls-tree -r -z --full-tree <captured-head-oid> -- <literal-scope>`；unborn 直接為空集合。 |
| index manifest／布局 | `ls-files --full-name --stage --sparse -v -z -- <literal-scope>`，解析 tag、mode、oid、stage、path；S／s 或 040000 sparse directory 拒絕該布局。`config --local --null --get <key>` 只准依序查固定四項 core.sparseCheckout、index.sparse、core.worktree、core.bare 的准入值，不輸出原文；啟用 sparse、bare 或外部 core.worktree 即拒絕，未設定的退出碼 1 是合法缺席。 |
| intent-to-add metadata | 對同一 HEAD 各取一次 `diff --cached --raw -z --no-renames --no-ext-diff --no-textconv --no-color --no-abbrev --ignore-submodules=all -O/dev/null --ita-visible-in-index <captured-head-oid> -- <literal-scope>` 與只將旗標換成 `--ita-invisible-in-index` 的 manifest。unborn 省略 HEAD 參數並前後確認仍 unborn；比較兩份 raw manifest 的同路徑差異以辨識 intent_to_add，不用空 blob oid 推測。這兩項實驗旗標列入能力檢查，缺任一即 git_unavailable；僅 cached metadata，不讀工作檔正文、不採用其 patch。 |
| blob | `cat-file --batch-check` 先查 oid／blob／大小，再以 `cat-file --batch` 取已准入的 raw blob；stdin 只有完整 oid，不用 rev:path、不加 --filters／--textconv／--follow-symlinks。 |
| untracked 忽略 | `check-ignore --stdin -z --verbose --non-matching --no-index`；stdin 只有安全遍歷所得、非 index 路徑，以 NUL 分隔。source／line／pattern 只作內部判斷，不顯示其輸出原文；0／1 依官方忽略結果語意解析，其他退出碼拒絕該層。 |

| 編號 | 程序要求 |
|---|---|
| GIT-023 | 產品組裝從固定候選 `/opt/homebrew/bin/git`、`/usr/local/bin/git`、`/usr/bin/git` 依序解析可信本機安裝，固定真實絕對執行檔及身分；不從工作區或任意 PATH 解析。啟動前檢身分及必要 flags／raw 格式能力；不因版本字串看似較新就推定可用。無相容候選即拒絕，不安裝或修改主機 Git。 |
| GIT-024 | 使用 shell=false 與 ArgumentList，固定 `--git-dir`／`--work-tree`／cwd 及 `--literal-pathspecs`，每個 pathspec 前使用 `--`；Oid 只接受目前 object format 的完整本機值，支援 SHA-1／SHA-256，不硬編 40 位。只允許上表命令及固定控制選項，不執行 git status、對工作檔的 git diff、ls-files --others、difftool 或 aliases。 |
| GIT-025 | 子程序環境從最小允許集合重建，只保留已准入 runtime 必要值並固定 LANG／LC_ALL=C；HOME、XDG_CONFIG_HOME、TMPDIR 固定 `/var/empty`，該位置不存在／不安全時拒絕。移除任意 GIT_*、trace、認證及 loader 注入值，再固定 GIT_CONFIG_NOSYSTEM=1、GIT_CONFIG_GLOBAL=/dev/null、GIT_OPTIONAL_LOCKS=0、GIT_TERMINAL_PROMPT=0、GIT_NO_LAZY_FETCH=1、GIT_NO_REPLACE_OBJECTS=1、GIT_ATTR_NOSYSTEM=1。每次固定通用 `--no-pager --no-optional-locks --no-replace-objects --no-lazy-fetch`，以及 `-c core.fsmonitor=false -c credential.helper= -c core.excludesFile=/dev/null -c core.attributesFile=/dev/null -c maintenance.auto=false -c gc.auto=0 -c diff.relative=false`；不繼承 .NET 任務的較寬權限。 |
| GIT-026 | 以每次受控呼叫的 macOS 原生限制拒絕全部寫入、網路、任意子程序／helper、程序探查與工作區外使用者資料讀取；只開放已准入 Git／OS runtime、必要 Git metadata、已核准用途的忽略來源。Git 不直接讀工作檔正文，正文由 Workspace 安全核心提供。原生能力先以固定離線探針確認；限制、可執行檔／runtime 布局或探針不符即 capability_unavailable，不裸跑或使用 sudo。 |
| GIT-027 | `.git` 例外只涵蓋目前 HEAD／refs、index、必要 config／格式、objects／pack 及上表忽略來源的 metadata 讀取；不得把 config、remote URL、reflog、作者或其他 metadata 原文傳模型／CLI。repo config 中的 include 或物件轉向若要求越界／額外執行即拒絕，不跟隨補權限。一般 WorkspaceFiles 的 `.git` 敏感規則完全保留。 |
| GIT-028 | stdout 為嚴格、有界的協定 bytes，不直接套用會刪控制碼的程序文字收集器；NUL 路徑分隔及 batch 長度由明確 parser 驗證。先查 blob 大小，至多讀上限加一以偵測增長；stderr 僅受限排空後回固定原因，不列印底層訊息。過量立即停止，不無界 buffering。 |
| GIT-029 | 單次 Git 期限 30 秒、一次快照擷取／重驗總期限 60 秒，包含能力及收尾，不在模型 await 期間累計擷取時間。固定 stdin 用完即關閉；取消、超限或逾時停止自有 Git 並確認退出、管道收尾後才釋出租約。無法確認或收尾失敗沿 process_unconfirmed／process_cleanup_failed 的嚴格停止與退出 1，不把它當成普通審查失敗。 |
| GIT-030 | Git 能力可共用既有原生啟動／監督原語，但須獨立 GitRead recipe 與限制描述，不讓 run_dotnet_task 接受 Git 或自由 argv，不承接可寫 bin／obj、私人建置輸出或 test 能力。唯讀探針不需要逐次建置核准，不新增可被重用的程序核准。 |
| GIT-031 | 不改 HEAD、index（含 stat cache）、refs、config、object database 或工作檔，不建立暫存 diff、報告檔、lock、日誌或 on-disk cache。唯讀限制是後續真實 fixture 的驗收項，不能只用 shell=false、命令名或假執行器聲稱已證明；也不承諾抵抗所有惡意 Git 物件／OS 攻擊。 |

## 5. 內部契約、快照與資料所有權

以下是後續 internal 最小契約；方法可在不改語意及擁有者的前提下拆為小型元件，不建立空殼通用外掛引擎。

| 型別／入口 | 必要資料／責任 |
|---|---|
| GitSelection／GitLayer／GitSide | Selection=All／Staged／Unstaged／Untracked；Layer 不含 All；Side=Old／New。 |
| IGitReadWorkspace | `CaptureAsync(CancellationToken)`、`ValidateAsync(GitSnapshot, CancellationToken)`、`DisposeAsync()`；重用規則／租約，不持有 Session 或生成能力。 |
| GitSnapshotCoordinator | 一般工具 CaptureAsync 原子取代目前快照；審查 Bind(snapshot, selection) 固定該份，不讓模型重捕捉。持有最多一份 raw 快照與失效狀態。 |
| GitSnapshot／GitSource | SnapshotId、根／gitdir 身分、object format、HEAD/unborn、Scope／規則／session 戳記、候選集合及觀察，index 另含本機判定 intent_to_add 與 tag；各來源有 path、layer、side、存在性、mode、byte_count、BOM、raw SHA-256、可空 blob oid 及受保護 bytes。 |
| GitChange／GitCoverage | 每個 path/layer 的狀態、兩側來源、完整本機行 token／變更範圍；本機選取、排除原因、完整性及實際提供來源帳本。不得由模型提交覆蓋旗標。 |
| CodeReviewCoordinator／CodeReviewReport | 單一在途審查、選定快照、世代、當回合覆蓋、報告驗證與一次發布；報告保存安全 summary/findings、版本／覆蓋 metadata 及不含 raw bytes 的驗證 manifest（候選集合、各來源指紋／觀察、忽略及規則戳記），供 show 有界重驗，不保存 raw diff。 |

| 編號 | 快照要求 |
|---|---|
| GIT-032 | SnapshotId 由同一 ChatApplication 本機配發 g1、g2 等，正 long 單調增加，不因 clear／換 Key 重用；溢位安全停止。SnapshotId、oid、指紋及規則版本都不是授權或修改來源證明，模型只能引用目前存在的快照。 |
| GIT-033 | 操作開始前擷取工作區／gitdir／執行檔身分、HEAD、index 路徑／身分及所選 index entries（mode、oid、stage、tag、intent_to_add）、Scope／RulesRevision／RulesGeneration／session generation；重驗包含這些旗標，不以相同空 blob oid 視為狀態相同。工作檔保留存在／缺席、種類、穩定身分／mode／長度及 raw SHA-256；不只看 mtime 或解碼文字。 |
| GIT-034 | 快照前後比對同一 Scope 的安全候選路徑集合、追蹤狀態、來源、忽略檔存在／缺席與規則邊界。重驗必須重新探索安全集合，涵蓋新增、刪除、staged↔untracked 等轉換，不能只檢已選檔案；觀察不一致整份候選丟棄、回 git_snapshot_stale，禁止自動重捕捉。 |
| GIT-035 | 每次 git_diff／read_git_file_range、審查生成前、採用模型結果前、最終顯示／報告／成功歷史提交前重驗規則與綁定來源集合；來源變更標 Stale、停止剩餘工具及生成，不發表為目前結果。不同 Scope 的未選來源改變不擴張為其內容審查；HEAD 變更仍使整份基準過時。 |
| GIT-036 | 捕捉／重驗／工具讀取與寫入、建置、規則操作共用 WorkspaceOperationCoordinator 排他租約；規則重驗沿同一租約執行，不能巢狀取租約。不持租約或決策鎖跨模型／使用者 await；外部程式仍可能改檔，所以結果發布必須再次重驗。 |
| GIT-037 | 所有 raw 來源先在有界記憶體完整安全驗證，再原子發布快照；只存實際變更路徑的兩側／三個不同版本，不額外捕捉未變更依賴供審查。新快照失敗不冒稱舊快照為新結果；舊報告可保留為歷史結果，但不能引用已被取代的 raw 快照繼續工具呼叫。 |
| GIT-038 | Git／忽略來源／範圍讀取走 Workspace 安全內部核心，絕不呼叫會建立修改快照的公開 ReadFileAsync；不新增、刷新或消耗 IReadSnapshotStore，也不生成 PlanStore 的 Read／Verified／Validation 事實。修改前仍須新的完整 read_file、預覽及逐次核准。 |
| GIT-039 | metadata／原始來源、完整工具 JSON、模型最終 JSON、CLI 區塊及動態數字／指紋均經目前敏感政策，沿用既有精確 Key 檢查，不冒稱通用秘密掃描。已准入來源命中敏感內容時整次拒絕，不以遮蔽成功或跳過內容後稱已審查；不能安全形成固定錯誤時沿原安全停止流程。 |
| GIT-040 | 此快照只是有界前後觀察，不是 OS 原子整 repository transaction；無法保證抵抗精確交換後還原、所有 hard-link／大小寫／Unicode 別名或掛載變動。所有完整／目前狀態均限定最後重驗時點，不承諾顯示後檔案不再變更。 |

## 6. CLI、工具輸入與固定結果

### 6.1 本機指令

| 指令 | 行為 |
|---|---|
| `/review`、`/review all` | 捕捉目前 Scope，選取三層，啟動一次唯讀模型審查。 |
| `/review staged`、`/review unstaged`、`/review untracked` | 同一完整快照只選指定層；其他層不提供正文，仍明示未選。 |
| `/review show` | 查詢最近成功報告及其範圍、時點／過時狀態；零 GenerateAsync，不重跑審查。 |

| 編號 | 指令／工具要求 |
|---|---|
| GIT-041 | 指令大小寫敏感，去除整行外層空白後按上表精確比對，其他參數拒絕且不改狀態。只在 AwaitingInput 接受；help 說明差異可能送 Gemini、Scope／raw bytes 視角、額度、排除及審查不執行驗證。`--help` 仍零 Key／client／規則或 Git I/O。 |
| GIT-042 | `/review` 新增為會生成的本機指令例外，與 `/compact` 分別辨識。准入／捕捉成功且至少一個安全文字內容差異後，經既有 Context 前置整理再由同一 Runner 開始回合；三項 Git 工具綁定快照。若前置整理後重驗失敗，停止且不送審查生成；已花的整理額度不退款。 |
| GIT-043 | 每回合覆蓋只能縮小能力：只允許 GitRead，封鎖 create_file／replace_text／run_dotnet_task／submit_plan／update_task 及其他工具。Definitions、Runner 整批預檢、Registry 及直接服務入口均檢查；混入禁止工具整批零工具執行，沿配對回 mode_restricted／not_executed 並停止。不切持久模式、不另建計畫或模型 client。 |
| GIT-044 | 正常 busy 單一讀取者、預輸入丟棄、Ctrl+C／EOF 沿既有輸入及收尾契約；審查不開核准選單，不讓貼上文字成為確認、scope 切換或新一輪生成。`show` 可做有界本機重驗以判過時，但不建立新快照或重播工具。 |
| GIT-045 | 工具輸入只接受下表全部必填欄位，不接受額外、重複、大小寫變體、null、非指定型別或隱含預設。path 使用精確正規化工作區相對路徑，最多 4096 UTF-16 單位，不 Trim、不解析引號／glob／shell；先敏感預檢再查目前快照，不讓假 id 或錯參數觸發新擷取。 |
| GIT-046 | 一般回合 git_status 每次捕捉目前 Scope 的三層並配發新 id；審查回合則只回綁定 id／已選層 manifest，不能重捕捉或擴選。git_diff／read_git_file_range 只讀目前快照及其所選、可審查的 path/layer；舊 id、缺席側或未捕捉的未變更檔案拒絕。 |
| GIT-047 | 三工具結果固定為 status、error_code、data；成功 status=success/error_code=none，失敗 data=null。錯誤與結果仍經既有 ToolExecutionResult／敏感／配對流程；不得更動原工具 envelope 來套用新格式。成功但有安全排除的 manifest 是有限資料，不是全部內容已取得。 |
| GIT-048 | read_git_file_range 起訖為正整數，start≤end、跨度最多 200，沿第 08 份 CRLF／LF／CR、空檔／EOF 夾限語意；完整 raw bytes 已安全驗過，再切片。存在但空的側回 total_lines=0、實際 start_line/end_line=null、content 空字串；缺席側回 git_source_unavailable，兩者不可混同。 |
| GIT-049 | 正式三工具追加 GitSummary 至安全 LocalToolOutcome／必要伴隨欄位；WithoutResponse 等複製路徑保留安全的觀察狀態／錯誤，但不把 raw blob、diff、argv、stderr 或 SDK 紀錄放入本機事實帳本。一般回合不因此取得任務完成權。 |
| GIT-050 | 解析及實際工具結果 JSON 的 UTF-8 bytes 均守第 9 節上限，含 envelope、路徑及 JSON 跳脫；無法完整容納則 result_too_large，data=null，不回半個 JSON 或無標記省略。審查仍可保留已知未覆蓋資訊，不把拒絕的來源列為已提供。 |

### 6.2 工具欄位與結果資料

| 工具 | 精確輸入欄位 | 成功 data 欄位（順序固定） |
|---|---|---|
| git_status | 空物件 `{}` | snapshot_id、scope、selection、head_oid（unborn 為 null）、object_format、view（固定 raw_bytes）、changes、coverage。changes 按 path Ordinal 再 staged／unstaged／untracked 排序，每筆為 path、layer、change_kind、old、new、content_available、exclusion_reason；change_kind 為 added／modified／deleted／metadata／unsupported。 |
| git_diff | snapshot_id:string、layer:staged/unstaged/untracked、path:string | snapshot_id、path、layer、old、new、diff、hunks、coverage。hunks 為 old_start、old_count、new_start、new_count；0 行側依已消耗行數標示。 |
| read_git_file_range | snapshot_id:string、layer、path、side:old/new、start_line:integer、end_line:integer | snapshot_id、path、layer、side、start_line、end_line、total_lines、content、file_byte_count、content_byte_count、has_utf8_bom、has_more。 |

old／new 固定為 exists、mode、byte_count、has_utf8_bom、sha256、blob_oid；缺席時 exists=false，其餘為 null。mode 為固定六位八進位字串，byte_count 為非負整數；sha256 為原始 bytes 的 64 位小寫十六進位，不是 Git oid。unsupported 來源未知的欄位為 null，不假造已驗證數值。

coverage 固定欄位為 scope_limited=true、safety_filtered=true、enumeration_complete:boolean、eligible_change_count:integer、delivered_change_count:integer、exclusions:array；計數指安全且可揭露的 path/layer 對。exclusions 每筆為 path（不能安全揭露時 null）、layer（適用全部時 null）、reason；不得輸出隱藏子樹數量或被忽略檔名。固定 Scope／安全過濾說明不等於發現內容錯誤；只要有選定層未檢查項目，完整性仍須揭露。

以下皆為合成輸入及失敗結果，不是目前產品功能：

```json
{"snapshot_id":"g1","layer":"staged","path":"src/Sample.cs"}
```

```json
{"snapshot_id":"g1","layer":"staged","path":"src/Sample.cs","side":"new","start_line":1,"end_line":20}
```

```json
{"status":"rejected","error_code":"git_snapshot_stale","data":null}
```

## 7. 差異呈現與覆蓋判定

| 編號 | 差異與覆蓋要求 |
|---|---|
| GIT-051 | 本機以已驗證原始來源產生差異，重用第 06 份行 token、最長相同前後綴及單一完整 hunk、前後三行脈絡的線性方式，不採用模型 diff 或全檔無界 LCS。這可能列出中間仍相同的行，須說明不是最短 diff；新增／刪除列完整對應側，不能漏掉 bytes 差異。 |
| GIT-052 | 差異標頭及每一側明標 snapshot／layer／old-new，行尾使用第 06 份可辨識 CRLF／LF／CR／EOF 與安全跳脫；BOM、mode、存在性另列 metadata，不假造正文行號。機器 hunks 與來源行號依原始行 token，不能從已跳脫的顯示文字反算。 |
| GIT-053 | 同一快照的來源讀取只提供所選變更檔案的原始 old／new 正文；不以工作檔取代 index／HEAD 的行號。只改 BOM／mode、空檔建立／刪除等沒有合法文字變更錨點時，呈現 metadata，content_available=false，不接受該項模型 finding；報告明示未做內容審查。 |
| GIT-054 | 本機為每個 path/layer/side 記錄確實送進審查請求的差異、行範圍及 metadata；工具已成功但未能形成／送出下一次安全請求，不算已提供模型。使用者輸入、摘要、先前聊天或模型自稱讀過，不能補上來源帳本。 |
| GIT-055 | delivered_change_count 只在該 path/layer 的完整差異已送入審查請求時增加；僅查部分原文可支持該片段位置，但不能替代完整差異提供。eligible_change_count 為選定層可審查的對數；達成全部仍只表示資料完整提供，不保證模型逐行理解或發現所有缺陷。 |
| GIT-056 | coverage 由本機組合 selection、來源提供與 exclusions；初版不額外分析未變更依賴、其他 Scope、未選層、被忽略或不支援內容。每次報告固定揭露這些邊界；partial 不得呈現為全面審查通過。 |
| GIT-057 | 候選超過 8192 項、200 個變更路徑、來源總量或 manifest 容量時拒絕整份新快照，不發布排序前綴冒充完整集合。已知個別不支援項目可在完整枚舉後發布有限 manifest；任何大小／顯示／額度不足的內容只計未覆蓋，不悄悄截斷修改。 |

## 8. 審查回合、報告結構與位置驗證

模型最終文字必須為以下物件，不加 Markdown fence、前言或後記。固定 schema version=1，selection 與 snapshot_id 必須等於綁定值。此示例只示範零 finding 的結構，是否完整由本機判定。

```json
{"version":1,"snapshot_id":"g1","selection":"all","summary":"已檢查本次提供的差異。","findings":[]}
```

findings 每筆只接受 severity、title、body、path、layer、side、start_line、end_line，全部必填。severity 精確為 P0／P1／P2／P3；其餘路徑及層／側沿工具契約，行號為正整數。summary 最多 2048 UTF-16 單位，title 為 1～256、body 為 1～2048，均不可為全空白；最多 30 筆。完整 JSON 限 65536 UTF-8 bytes，字串另受既有 Unicode／控制字元／敏感政策。

| 編號 | 報告要求 |
|---|---|
| GIT-058 | 最終 JSON 只接受上述精確欄位及型別，不允許重複、額外欄位、大小寫變體、浮點行號、未知版本或 null；不接受模型指定 coverage、approved、verified、tests_passed、snapshot bytes 或本機事實。解析失敗為 review_invalid，不再生成修復 JSON，也不把原文當普通成功回答顯示。 |
| GIT-059 | 一項 finding 位置最多 10 行，範圍全部屬於同一 path/layer/side 且已實際提供的原文行範圍，並至少重疊一行本機差異中的刪除／新增行。staged new 是 index 行號，unstaged new 才是工作檔；刪除可用 old。越界、未提供、缺席／非文字側或過時來源拒絕整份報告，不夾限、不移到另一側或改成 line 1。 |
| GIT-060 | finding 聚焦變更引入或暴露且能說明觸發條件／影響的具體問題；未知依賴、推測風格或無根據「可能有錯」不強迫列項。P0 表示可直接成立的最高急迫阻斷、P1 為高優先、P2 為一般修正、P3 為低優先；都只是模型判斷，不由位置驗證器保證語意、嚴重度或真實可重現性。 |
| GIT-061 | 同一路徑／層／側／範圍／title 的精確重複 finding 拒絕；其他合法 finding 保留模型順序，同嚴重度不擅自合併。固定 writer 顯示安全 title/body 與可核對位置；summary 是模型摘要，不能用其中的自然語言結論覆蓋本機完整性或驗證狀態。 |
| GIT-062 | 成功顯示區塊包含快照／最後重驗時點、Scope、selection、raw bytes 視角、三層變更摘要、來源提供比例、未覆蓋原因、問題及固定「本回合未執行建置或測試」。只有在所選可審查內容完整提供且無影響完整性的未知項時標完整；其他合法報告標部分。零 finding 只說「在已提供範圍未提出可確認問題」，不說安全、無 bug 或測試通過。 |
| GIT-063 | 一般聊天自然語言可整理 Git 資料，但不是正式 CodeReviewReport，不能更新 `/review show`。正式審查最終結果先經 schema／位置／敏感／容量／來源／世代驗證，再由同一決策發布報告、固定顯示及成功回合；保留 SDK 原生訊息／工具配對，不以重寫 ModelMessage 偽造模型回答。 |
| GIT-064 | Runner 在採用最終文字前增加 internal 審查結果驗證／完成閘門；公開 LLM 介面不改。只接受合法終結的模型回合，未完成工具鏈、API 失敗、取消、額度耗盡或非法報告不提交成功歷史／新成功報告，不在結束後另開一次私有生成補答案。 |
| GIT-065 | 最多保留一份最近成功報告及一份目前 raw 快照；新審查成功才取代報告，失敗／取消不覆寫舊成功報告。嘗試開始前立即撤銷舊 raw 快照的工具引用；舊報告固定標示原 selection／版本、是否已過時及最近嘗試失敗的受控原因，不偽裝成這次結果。報告不成為 Context pin、計畫事實或持久化檔。 |
| GIT-066 | 攻擊性檔案、歷史 blob、忽略規則、模型報告與工具文字均是外部資料；不能要求改 Scope、洩漏固定應用指示、讀 Key、執行命令或取消審查覆蓋。即使來源聲稱已核准或已完成測試，程式能力、來源及驗證判準仍由本機契約決定。 |

## 9. 固定上限、Context 與生成預算

| 名稱／擁有者 | 固定上限與計量 |
|---|---|
| MaxGitSourceBytes／Workspace | 每個 raw blob／工作檔側及每份忽略來源 131072 bytes，含 BOM。 |
| MaxGitSnapshotSourceBytes／Coordinator | 8388608 原始 bytes，包含所有保留來源及忽略來源；同 path/layer/side 的重複參照可共用同一不可變版本，不能藉共用省略驗證。 |
| MaxGitScannedEntries／Workspace | 8192 項，HEAD/index manifest 與安全工作區遍歷每個觀察項均計；重驗另開同樣有界計量，不把輸出先無界收完再排序。 |
| MaxGitChangedPaths／Coordinator | 200 個不同安全 path，跨層同 path 計一次；path/layer 對另計 eligible／delivered。 |
| MaxGitDepth／Workspace | 工作區根起 32 層，含規則／忽略祖先及安全遍歷。 |
| MaxGitToolResultBytes／Tools | 65536 UTF-8 bytes，完整 envelope 與跳脫。 |
| MaxGitDisplayBytes／CLI | 65536 UTF-8 bytes，完整報告／狀態區塊及安全跳脫。 |
| MaxGitMetadataStdoutBytes／Adapter | 每命令 1048576 原始 bytes；blob stdout 另限 131072 加固定 256-byte header／分隔容量。 |
| MaxGitDiagnosticBytes／Adapter | 每命令 stderr 8192 原始 bytes，只作排空／錯誤分類，無直接輸出。 |
| GitCommandTimeout／GitCaptureTimeout | 單次 30 秒；一次擷取／重驗含收尾 60 秒。 |
| MaxGitRangeLines／Tools | 每次 200 行；不修改既有 read_file_range 的契約。 |
| MaxReviewFindings／Report | 30 項；title 256、body／summary 2048 UTF-16 單位，位置跨度 10 行；最終完整 JSON 65536 bytes。 |
| 既有共用界線 | 回合／程序生成預設 5／100，可調 1～20／1～1000；估算軟門檻 196608、實際完整 SDK body 262144 bytes；Plan／記憶投影各 32768、顯示 65536；20 任務、1 在途、256 事實均不變。 |

| 編號 | 容量／記帳要求 |
|---|---|
| GIT-067 | 上表為獨立具名常數，不新增放寬設定／環境來源。所有邊界接受恰好上限、超一拒絕；raw bytes、JSON bytes、UTF-16 單位及行數分開計量，不稱為剩餘 token 或費用。字串／數值相加及 id 計數使用 checked，溢位安全失敗。 |
| GIT-068 | 後續 `/review` 的審查附錄包含 selection、當前快照安全 manifest／覆蓋與結果格式，不直接插入全部 raw bytes。只透過三項工具按需取得正文；Git 附錄含完整 JSON 跳脫限 32768 bytes，超過即拒絕開始生成，不省略變更後聲稱完整。既有規則、記憶、計畫及附錄合併仍守完整 SDK body，不能挪用其他額度。 |
| GIT-069 | `/review` 的每次一般生成及必要自動整理均走現有 Runner 唯一記帳；生成預設／可調值與程序總消耗保持，失敗／取消亦照記可得用量，不退款。不增加新的模型呼叫預算，Git 子程序不計為 Gemini 生成，也不扣 .NET 任務每回合兩次／300 秒的額度。 |
| GIT-070 | `/review show`、Git 捕捉／工具本身、零內容差異與本機准入失敗均零新增 GenerateAsync；純 Git 工具出現在一般回合時，只有包圍它的正常模型請求照既有規則記帳。最後可用生成仍不得派送工具，資料不夠時停止／部分，不提高上限補報告。 |
| GIT-071 | 當前 Git 快照／報告屬本機來源，不由摘要恢復；歷史中的 diff／舊結論只能視為過去觀察。Context 整理沿既有 UserText／AssistantText 與協定所有權，不把 raw blob／Git stderr 當新增摘要來源；每次正式審查重新取得目前資料，不以舊摘要補足已提供行範圍。 |

## 10. 失敗、清除、取消與資源釋放

| 編號 | 生命週期要求 |
|---|---|
| GIT-072 | 新增受控錯誤只限 not_git_repository、git_unavailable、unsupported_git_layout、git_snapshot_required、git_snapshot_stale、git_source_unavailable、git_limit、git_failed、git_timed_out、ignore_unavailable、review_invalid、review_incomplete；沿用規則、敏感、路徑、容量、取消及嚴格程序收尾碼。分類詳見下表，不回例外／Git 原文。 |
| GIT-073 | 來源變動導致 Stale 時停止工具及生成，不自動 reload、重捕捉、重試 Git、修報告、換模型、復原或重播工具；使用者明確新 `/review` 或新一般指示才可再嘗試。規則來源失效另沿 RULE-048～063，不能被較輕的 git_snapshot_stale 蓋掉。 |
| GIT-074 | `/clear`、共同安全重設、換 Key 與 Dispose 丟棄全部 raw 快照、報告、提供帳本及在途候選，保持 id 單調計數與既有程序用量；不重置模式、工作區或退款，不修改檔案。換 Key 先用舊政策收尾，未來新觀察全部重新驗證，不能沿用舊敏感檢查。 |
| GIT-075 | Scope 成功切換或規則版本變更即撤銷 raw 快照與在途審查；切 Scope 丟棄舊報告，避免 show 帶出新 Scope 外的內容。同 Scope 規則失效可保留安全報告為 Stale，show 先按當前政策重驗可顯示欄位並明標歷史；無法安全展示整段即拒絕，不能當目前結果。 |
| GIT-076 | Ctrl+C 取消目前捕捉／模型／工具，先等待自有程序及 I/O 收尾，回輸入；Ctrl+D／EOF 等收尾退出，AwaitingInput 的退出碼沿原規則。Dispose 先拒新工作、撤代次、取消並等待，再丟政策／來源參考；遲到結果不能補回成功報告或快照。 |
| GIT-077 | 正常取消／失敗不改既有已知寫入或建置事實；規則事件需要全域撤確認時，先沿原代次收尾並保留已發生事實。敏感／非法 SDK 協定／顯示 I/O 故障、Unconfirmed 或收尾失敗仍走原較嚴格清除／退出，不以「只是審查」降級。 |
| GIT-078 | 沒有自動磁碟保存、背景監看、連續重審、額外核准或清理任務；成功／拒絕／取消均不刪檔。後續真實 fixture 保留，僅可刪明確單一路徑檔；需要批次清理由使用者人工處理。 |

| 條件 | 固定分類及結果 |
|---|---|
| 沒有 `.git`／不支援布局／Git 不可用／原生限制不可用 | Rejected，分別對應 not_git_repository／unsupported_git_layout／git_unavailable／capability_unavailable；不生成或啟動目標 Git 工作。必要有限准入探查與產品資料查閱分列。 |
| 舊 id／沒有快照／缺席或不可審查來源 | Rejected，git_snapshot_stale／git_snapshot_required／git_source_unavailable；data=null。 |
| 捕捉／結果重驗觀察變動 | Rejected，git_snapshot_stale；在途報告不發布，停止本回合。 |
| 掃描／來源總量／路徑數等超限 | Rejected，git_limit；單側超限可作安全 exclusion，manifest 總量超限不發布。投影過大沿 result_too_large，附錄沿 Context 容量停止。 |
| 合法但未完整提供資料的最終報告 | 成功發布「部分」模型審查，固定 review_incomplete 提示；工具 envelope 本身仍依各次實際結果，不把非成功工具改成 success。 |
| 最終 JSON／finding 位置不合法 | Failed，review_invalid，零新報告／成功歷史；不顯示未驗證正文或自動重試。 |
| 忽略來源不能安全判定 | untracked 層 exclusion=ignore_unavailable；若其原因是敏感、根失效或規則失效，原較嚴格停止優先，不降級為有限成功。 |
| Git 非預期退出／協定非法／一般 I/O／逾時 | Failed，git_failed／git_timed_out；相關層不能作成功來源，保留安全觀察分類，禁止自動再跑。 |
| 取消／無法確認收尾 | Cancelled 沿 none／turn_cancelled；Unconfirmed／cleanup failure 沿既有嚴格停止並退出 1，兩者不可混同。 |

## 11. 受影響契約與後續遷移

本次不改寫既有來源文件。後續產品實作須同一變更同步能力說明及適用需求，保留歷史敘述與未完成驗收。

| 編號 | 遷移要求 |
|---|---|
| GIT-079 | 以下表為共用契約增量清單；新增 GitRead、Review 回合覆蓋、工具結果／停止映射、最終報告閘門與當次投影，不能只改模型指示。既有規格的嚴格安全及證據規則只按明示增量擴充，不全面放寬。 |
| GIT-080 | 後續實作同步 help、README、ROADMAP、總規格索引與受影響子規格接續註記；區分已實作、假程序測試、真實 Git／原生限制、PTY、真人與真實 Gemini。Git 觀察不改第 07 份掃描器／hook 規則或解除發布阻擋。 |
| GIT-081 | 承接 RULE-HO-005：規則正文與一般內容仍守原 Scope；本份 GIT-019 只為忽略判斷新增祖先 metadata 的用途限定例外，GIT-018 對 Git 安全子集的呈現也不改搜尋整次拒絕。後續更新 RULE-010、016～017、029～031 的接續說明及測試，不讓「Git 唯讀」自動成為一般跨 Scope 權限。 |
| GIT-082 | 原 PLAN 任務狀態與 requirement schema 不增 Git 類型；新一般工具觀察與正式 Review 回合分開。後續若要把 Git 操作加入 Read requirement 或自動更新 Analysis 任務，必須另訂版本、證據與容量遷移，不能從本份推得已支援。 |

| 來源契約 | 本份增量／保留 |
|---|---|
| SPEC 第 8、10～15 章；第 03／05 份指令與循環 | `/review` 為新增生成入口，三工具與 internal final validator；SDK 原生訊息／配對、取消、失敗歷史及唯一用量保持。 |
| WS／EDIT／SEARCH 安全讀取及快照 | 共用內部讀取、租約、行 token 與跳脫；Git／忽略來源不建立可修改快照，不降低原子寫入、預覽或衝突檢查。 |
| PLAN-007、014～015、037～048 及能力集合 | 新 GitRead 可探索但不抵 requirement；Review 覆蓋禁任務更新／寫入；一般任務受阻及既有事實仍保持。 |
| BUILD 程序／收尾及能力 | 可重用監督原語，Git recipe 權限獨立；不擴大 .NET 任務、test、輸出或核准，也不宣稱已驗證全部後代收尾。 |
| CTX-020～024、044～049、056～071 | 新生成入口共用額度，附錄／來源帳本屬當次資料；不提高 body，不從摘要重建快照／覆蓋或退還生成。 |
| RULE-010、016～019、029～034、048～064；RULE-HO-005／008 | 承接 Scope、來源／世代、規則失效及先記事實後撤確認；忽略 metadata 例外與 Git 子集明示，未來復原規則檔另見固定交接。 |

## 12. 固定交接紀錄

<a id="fut-09-handoff"></a>

### 12.1 FUT-09：修改檢查點與復原

以下左欄由 FUT-06 擁有；右欄是 FUT-09 必須獨立定案的事項，不是本次復原規格、預先批准或實作。撰寫 FUT-09 時，須同讀第 06 份安全寫入及第 12 份 RULE-HO-008。

| 交接編號 | 已定契約／來源 | FUT-09 後續決策 | 衝突風險與驗收責任 |
|---|---|---|---|
| FUT09-HO-001 | GitSnapshot 是 GIT-032～040 的有界唯讀觀察，不是 checkpoint。 | 選 Git、獨立快照或其他明確復原基準，定義捕捉時點及涵蓋來源。 | HEAD 不一定是 Agent 修改前 bytes；不得把 g1／oid 當作足夠復原資料或承諾可逆。 |
| FUT09-HO-002 | staged、unstaged、untracked 分層且可互抵（GIT-012～013、020）。 | 是否及如何保留 index、工作檔、未追蹤／忽略內容；明示不支援項目。 | 恢復工作檔不能偷偷覆蓋使用者暫存區；Git 或一次備份不能代表全工作區。 |
| FUT09-HO-003 | 指紋、身分、mode、存在性及完整候選集合可供衝突辨識；來源仍有應用層限制（GIT-033～040）。 | checkpoint 的穩定性／版本、保留與復原前比較策略。 | 不只比較 mtime／目前 HEAD；外部新增、刪除、同長改寫與追蹤狀態變動須能拒絕或交人工。 |
| FUT09-HO-004 | Git 差異不證明作者；Review 不寫 PlanStore Verified（GIT-010～011、038）。 | 以受控寫入事件記錄 Agent 修改、與人工修改區分的責任及不可判定情況。 | 不把使用者原有 dirty 檔納入「Agent 全部修改」，也不以模型聲稱復原完成。 |
| FUT09-HO-005 | GitRead 不提供任何 mutation／核准（GIT-004、024、030～031）。 | 若採 Git 寫入，另訂精確目的、recipe、隔離、每次預覽／確認及 index 保護；獨立快照亦須安全發布。 | 不沿用本份能力執行 reset／restore／checkout／stash／apply，不藉原計畫或審查結果重用核准。 |
| FUT09-HO-006 | Review raw 來源僅記憶體、8 MiB、有敏感檢查，clear／換 Key 丟棄（GIT-037、039、074）。 | 復原備份的保存位置、容量、期限、敏感防護、存取權及單檔清除。 | raw 審查來源會消失，不能當持久備份；不自動沿用 FUT-08 存檔格式或保留認證。 |
| FUT09-HO-007 | 新增／刪除／缺席及非文字側明確區分，未提供刪除能力（GIT-021、048、053、078）。 | Agent 新建檔、缺備份、不能安全刪除或不支援 metadata 的人工復原流程。 | 不掃描清理目錄、不批次刪除；未知基準或檔案被他人接手時不能猜測安全復原。 |
| FUT09-HO-008 | 承接 RULE-HO-008、RULE-040、053～055：規則寫入先完成該筆查核／記事實，再 Stale。 | 復原 AGENTS 的差異、版本、確認、部分失敗及重載流程。 | 回到舊 bytes 也不能復活舊 RulesRevision、計畫確認或單次 yes；須明確 reload、重新確認及新指示。 |
| FUT09-HO-009 | 租約、世代及已發生事實不能被取消／失敗抹除（GIT-036、073～077）。 | 多檔 checkpoint 的逐檔處理、發布順序及部分失敗回報。 | 已復原 A、B 衝突時分開回報，不稱全部成功或全部未變，不自動再套用 A 或回滾人工修改。 |
| FUT09-HO-010 | 審查報告及恢復工作脈絡與檔案復原各自獨立（GIT-065、071）。 | 與 FUT-08 之間僅可交換哪些安全參照／歷史資訊，版本遷移另定。 | 載入摘要／報告不恢復 checkpoint、寫入快照或核准；兩項不強制合併，也沒有必然先後。 |

<a id="future-handoffs"></a>

### 12.2 其他 FUT 的衝突與承接

| 交接編號／對象 | 已定契約 | 後續決策 | 衝突風險與驗收責任 |
|---|---|---|---|
| GIT-HO-001／FUT-01 | raw 快照來源與一般搜尋／局部讀檔分開，Git 安全子集不改 search 的整次拒絕（GIT-018、037～038、053～055）。 | 日後如允許審查未變更依賴，定義同一版本的來源取得與覆蓋。 | 不以目前工作檔的搜尋結果冒充舊 index 來源，不把局部來源當完整修改基準。 |
| GIT-HO-002／FUT-02 | GitRead 與 .NET recipe、核准／預算及真實 Validation 分開（GIT-026～031、062、069）。 | 未來如組合 review 與 build/test，分別定義入口與對應來源版本。 | 審查零問題不能成 TestsPassed；Git 原生限制成功也不解除 test 不支援或建置原生待驗。 |
| GIT-HO-003／FUT-03 | Review 不變更任務；一般 Git 工具只探索，Analysis 仍 ModelAssessment（GIT-010～011、043、082）。 | Git Read requirement／多 Scope 計畫若擴充，另訂版本與事實。 | 不偷新增完成條件或挪用 256 筆事實，既有 Completed 與核准不由報告重建。 |
| GIT-HO-004／FUT-04 | 同 Runner、同總額度、當次有界附錄／來源帳本，摘要不復活快照（GIT-067～071）。 | 後續 Context 策略變更時的來源保留與投影遷移。 | 不提高 body／生成上限補完審查，不將 bytes 當 token、不把舊摘要當目前差異。 |
| GIT-HO-005／FUT-05 | 明示承接 RULE-HO-005；只審目前 Scope，忽略祖先 metadata 為用途限定例外（GIT-018～020、035、075、081）。 | 未來多 Scope 審查的來源集合／切換／顯示必須另定。 | 不能自動讀跨 Scope 正文、開放一般 `.git`、自動 reload 或讓歷史 finding 符合新規則。 |
| GIT-HO-006／FUT-07 | 指令精確解析、busy 單一讀取者與取消／EOF 收尾（GIT-041～044、076）。 | 多行／歷史／進度 UI 如何表達 Review 及來源側別。 | 貼上／預輸入不觸發二次審查／核准；舊 staged 行號不能被 UI 當磁碟行號導航。 |
| GIT-HO-007／FUT-08 | 本份不保存，show 的舊報告只為歷史（GIT-065、071、074～075）。 | 存檔是否保留安全 finding／來源參照、格式及資料最小化。 | 不保存 raw blob／Key，續接需重新確認工作區、認證、規則與目前 Git；不能恢復舊 snapshot id／核准。 |
| GIT-HO-008／FUT-10 | Review 是固定受控能力，技能文字不能增加工具／模式權限（GIT-043、066）。 | 技能能否組合審查流程及其明確觸發／來源。 | 無直接格式耦合；不能由 SKILL 的 yes／任務步驟解除唯讀、執行腳本或假造使用者指示。 |
| GIT-HO-009／FUT-11 | 內建 GitRead 命名及本機來源由本份擁有（GIT-007～009、024、066）。 | MCP 命名空間、認證與外部 Git 結果型別另定。 | 無必要依賴；外部 git_status 同名或回傳 oid 不能冒充本機快照／使用者授權，供應商 Key 不外送。 |
| GIT-HO-010／FUT-12 | 第一版只審本機提供來源，報告不連網補證據（GIT-004、026、056、066）。 | 日後官方文件／引用整合與版本、網路及額度邊界另定。 | 無必要依賴；網頁引用不是本機行號／測試證據，也不能放寬 Scope 或執行遠端內容。 |
| GIT-HO-011／FUT-13 | 公開 LLM 契約保持，報告 schema 與來源由 Core 驗證；換 Key 丟資料（GIT-007、058～064、074）。 | 模型切換能力、報告格式相容性及新政策重驗。 | 不跨 client 續用原生協定或傳認證，不在失敗後隱式換模型重送來源／工具。 |
| GIT-HO-012／FUT-14 | 目前單一在途 Review、唯讀快照與總額度，不具多 Agent 協調（GIT-032～040、065、069、076）。 | 父子 Scope 子集、快照傳遞、共用預算、取消及結果去重另定。 | 無必要依賴；子 Agent 結論不是本機事實／核准，不能用舊來源續跑或擴大 Git 能力。 |

| 編號 | 交接要求 |
|---|---|
| GIT-083 | FUT-09 規格逐項回填 FUT09-HO-001～010 的承接章節／需求／驗收；若改本份 GitRead、基準、失效或容量，列出受影響 GIT、EDIT、RULE 及遷移，不只寫「沿用 FUT-06」。 |
| GIT-084 | 其他 FUT 規格須核對上表適用列及當時產品，而非把 memo 當能力證明；無直接格式耦合或必要依賴已明示，不為交接提前開發存檔、技能、網路、多模型或多 Agent。 |
| GIT-085 | 本份是 FUT-06 唯讀審查契約擁有者；FUT-09 的復原保存／寫入、FUT-08 的續接及其他 FUT 右欄刻意留給其獨立規格。本份自身的行為、限制及驗收不留待實作者選方案。 |
| GIT-086 | ROADMAP 的 FUT-06 入口及 FUT-09 必讀交接連結須指向本份穩定錨點；交接放本份單一來源，不另建重複 memo 或暗示兩項需合併開發。 |

## 13. 後續產品驗收

### 13.1 方法與證據層級

| 編號 | 驗收要求 |
|---|---|
| GIT-087 | 後續產品開發保留既有離線 MSTest、敏感掃描及適用 PTY 回歸；新增測試使用合成 Key、假模型／HTTP、可控制檔案系統及假 Git 程序，另以保留的真實本機 repository fixtures 驗受控 Git。準備 fixture 可由測試建立提交／修改，產品執行階段唯讀保證另行比較，不能混算。 |
| GIT-088 | 離線、真實受控 Git／原生限制、PTY、真人及真實 Gemini 各自記日期、平台、基線、命令／退出碼與未驗證項；假程序或 PTY 不代替原生、真人或真實服務。真實服務只由使用者自行隱藏輸入 Key，不保存轉錄／raw SDK／認證。 |

### 13.2 離線及真實受控 Git 案例

以下是待產品實作後執行的案例，不是本次已通過的測試。

| 案例 | 對應需求 | 後續成功條件 |
|---|---|---|
| GIT-AC-001 | GIT-001～011、079～082 | 正式組裝／工具次序、一般 Plan／Execute 與 Review 定義、公開 LLM／SDK 所有權及原工具 schema 保持；沒有產品／證據旁路。 |
| GIT-AC-002 | GIT-012～013、016 | HEAD=A/index=B/worktree=A 仍兩層，部分暫存／新增後再改／暫存刪除後重建皆正確；unborn／detached、SHA-1／SHA-256 及損壞 HEAD 不混同。 |
| GIT-AC-003 | GIT-014～015、023～024 | 無 repo、子目錄、bare、gitdir 檔／連結、linked worktree、commondir、alternates、外部 index、sparse 與 Git 缺旗標拒絕；零工作區外探索／安裝／連網，不影響普通聊天。 |
| GIT-AC-004 | GIT-017～018、020、081 | Scope 外／敏感路徑與新 AGENTS 子樹不讀正文、不洩名稱／隱藏數量；安全子集明示未覆蓋，搜尋原拒絕語意保留，已追蹤檔不因 ignore 消失。 |
| GIT-AC-005 | GIT-019～020、025、027、033～035 | 根／祖先／巢狀 .gitignore、否定規則與 info/exclude 可判定；全域 excludes 不讀。缺席後新建、修改、連結／過大來源使重驗過時或 untracked 未檢查；敏感／規則故障仍嚴格停止。 |
| GIT-AC-006 | GIT-021～022、048、053、056 | rename/copy 為刪增；衝突／intent-to-add／gitlink／link／特殊／binary／非法 UTF-8／大檔／mode／BOM／空檔皆有正確邊界。相同 path／mode／oid／stage 的真正空檔與 intent-to-add 必須區分，包含 unborn；缺 ITA 旗標能力拒絕。不捏造文字行號、未檢查也不稱 no_changes。 |
| GIT-AC-007 | GIT-016、023～028、031 | 假 Git 及真實 repo 配置具惡意 pager／fsmonitor／clean／smudge／textconv／credential／trace／alias／include／replace／promisor；無 helper、檔案寫入或網路，raw CRLF／BOM 不經轉換，診斷不輸出原文。 |
| GIT-AC-008 | GIT-026～031、036、077 | 真實原生 fixture 驗拒絕工作檔／index／refs／objects／config 寫入、工作區外使用者資料、IPv4／IPv6、helper；能力不足零裸跑。取消／逾時／stdout-stderr 同時排空／控制失聯／收尾失敗保持嚴格退出，原有 .NET 能力不擴權。 |
| GIT-AC-009 | GIT-028～029、050、057、067 | 30 秒／60 秒、stderr 8192／8193、metadata 1048576／1048577、每側 131072／131073、來源總 8388608／8388609、8192／8193 掃描、200／201 路徑、32／33 深度及 65536／65537 JSON 邊界；不發布部分協定冒充成功。 |
| GIT-AC-010 | GIT-032～037、040 | 捕捉／模型 await／工具／最終發布前改 HEAD、index、同長 bytes、身分／mode、存在性、追蹤轉換、新候選檔或規則邊界均 stale；僅 intent_to_add／tag 旗標改變也不能沿用，不只驗已選檔。取消後遲到候選不發布，取租約不自鎖，不聲稱防所有交換還原。 |
| GIT-AC-011 | GIT-038～039、049、066、071 | 讀 diff／ignore／range 不建立修改快照或 Verified／Validation；舊摘要／假完成文字不補證據。當前 Key 出現在歷史 blob／刪除文字／路徑／實際欄名／指紋／合成完整投影時整次拒絕，ToString／錯誤無原文。 |
| GIT-AC-012 | GIT-041～047、070 | 指令精確／無參數與錯參數、show／help 零生成；Review 僅三工具，混入寫入／建置／計畫工具整批零執行；持久模式／計畫進度／確認不由審查修改，Git 工具不能重捕捉擴選。 |
| GIT-AC-013 | GIT-045～050 | 多餘／重複／null／大小寫欄位、假 snapshot、任意 ref／argv、路徑越界與 4096／4097 拒絕；range 的 200／201 行、EOF／空側／缺席側及精確 UTF-8 bytes 正確。 |
| GIT-AC-014 | GIT-051～057 | 三行脈絡、CRLF／LF／CR／EOF、安全跳脫與 BOM／mode metadata 可辨識；staged 與工作檔行號不同仍正確。工具成功但下一請求未送不增加 delivered；部分原文不冒充完整 diff。 |
| GIT-AC-015 | GIT-058～061、063～064 | 最終 JSON 缺／重複／多欄、未知版本、浮點／越界／超 10 行、錯層側、未提供行、未重疊變更、缺席側、重複 finding、31 項及字串上限拒絕；不自動修 JSON，不提交非法成功歷史／報告。 |
| GIT-AC-016 | GIT-054～056、062～065、072 | 合法完整／部分／零 finding 及無可審文字情況，本機準確顯示覆蓋與未驗證；不把模型 summary 的通過／測試聲明當事實。普通聊天不更新 show；新嘗試失敗保留舊報告但不當成新結果。 |
| GIT-AC-017 | GIT-067～071 | 32768／32769 附錄、65536／65537 顯示／報告、262144／262145 實際 SDK body、預設及可調生成邊界、唯一用量、最後生成禁工具、自動整理後來源變動與零生成入口；不加額度或退款。 |
| GIT-AC-018 | GIT-011、036、065、072～078 | clear／換 Key／切 Scope／規則重載／來源 Stale／Dispose 及 Ctrl+C／EOF 競爭；raw 與報告正確丟棄／標過時，id 不重用、資料不復活。原已完成寫入／建置事實先收尾保留，嚴格故障不降級，無磁碟清理。 |
| GIT-AC-019 | GIT-079～088 | 全部固定交接可追溯；同步受影響契約且原安全／任務／預算測試保留。未實作 FUT-09／其他 FUT，未以假 Git／PTY／本份規格解除真人／服務／Release／test 不支援門檻。 |

### 13.3 PTY、真人與真實 Gemini

| 案例 | 對應需求 | 獨立驗證責任 |
|---|---|---|
| GIT-AC-020 | GIT-041～044、062、065、070、074～078、088 | 離線 macOS PTY：中文／空白路徑、層及側別顯示、help／show／無變更零生成、busy 預輸入丟棄、捕捉／模型／工具 Ctrl+C、Ctrl+D 等收尾及終端機恢復；假 Git／假模型，不當真人或原生證據。 |
| GIT-AC-021 | GIT-041～044、052、059、062、075～076、088 | 真人 macOS 終端機檢查中文輸入法、長路徑／折行、raw 行尾標記、staged 與工作檔位置辨識、取消／EOF 及部分報告可理解；未做即未驗證。 |
| GIT-AC-022 | GIT-009、042～043、054～064、067～071、088 | 使用者自行隱藏輸入真實 Key，驗真正 Gemini 三工具配對、同快照來源／行號、合法報告、錯誤 JSON／不足覆蓋／額度及來源變動；只記受控分類與數值，未觸發分支不算通過。 |

## 14. 本次文件查核與完成條件

| 編號 | 文件要求 |
|---|---|
| GIT-089 | GIT-001～090、GIT-AC-001～023、FUT09-HO-001～010、GIT-HO-001～012 唯一連續且可追溯；每項需求有案例對照，全部 JSON／Markdown 表格／圍欄、相對連結與穩定錨點正確。限制、raw bytes／ignore 例外、覆蓋、世代、報告、核准及交接不能互相矛盾。 |
| GIT-090 | 只用不落地文件檢查腳本，核對新規格全文與 ROADMAP 最小差異、空白及 Git 索引前後不變；未追蹤新檔另用 no-index --check。只回報實際文件檢查，不跑 restore／build／產品測試／PTY／原生探針／真實 Gemini，不把本規格寫成產品已實作或已驗收。 |

| 案例 | 對應需求 | 本次文件成功條件 |
|---|---|---|
| GIT-AC-023 | GIT-001～090 | 需求、案例、交接與限制可追溯，JSON／表格／圍欄／連結／錨點／空白查核通過；只有本檔新增與 ROADMAP 最小更新，index 原始指紋及 staged entries 不變，零產品實作／驗收／提交／推送。 |

文件查核命令包括下列項目；`--no-optional-locks` 避免查詢刷新 index，no-index 的非零退出碼須區分差異存在與空白診斷。

```sh
git --no-optional-locks diff --check
git --no-optional-locks diff --no-index --check -- /dev/null specs/13-git-diff-and-code-review.md
git --no-optional-locks diff -- ROADMAP.md
git --no-optional-locks status --short
git --no-optional-locks diff --name-only
git --no-optional-locks diff --cached --name-only
```

## 參考與後續閱讀

- [ROADMAP](../ROADMAP.md)、[總規格](../SPEC.md)、[README](../README.md#project-rules-validation)：目前定位、已交付能力與既有未結門檻；舊文件的待實作時態按接續註記閱讀。
- [第 06 份寫入與核准](06-file-edit-and-approval.md)、[第 09 份任務證據](09-plan-mode-and-task-list.md)、[第 11 份 Context／預算](11-context-and-budget-management.md)、[第 12 份固定交接](12-project-rules-loading.md#future-handoffs)：後續實作與 FUT-09 必讀來源。
- [Git status：Background refresh](https://git-scm.com/docs/git-status#_background_refresh)、[Git 通用選項／環境](https://git-scm.com/docs/git)：可選 index 更新、literal pathspec、禁止 lazy fetch／replace 等技術依據；本份仍另要求原生限制，不能只靠 flags 宣稱隔離。
- [Git cat-file](https://git-scm.com/docs/git-cat-file)、[ls-files](https://git-scm.com/docs/git-ls-files)、[ls-tree](https://git-scm.com/docs/git-ls-tree)、[check-ignore](https://git-scm.com/docs/git-check-ignore)：原始物件、NUL manifest 及忽略判定協定；後續須核對實際 Git 能力。
- [Git attributes](https://git-scm.com/docs/gitattributes)、[Git diff](https://git-scm.com/docs/git-diff)：工作檔轉換、外部 diff／textconv、cached raw 及 ITA visible／invisible 旗標的技術依據，不表示本份允許執行 filters 或對工作檔跑 Git diff。
- [FUT-09 固定交接](#fut-09-handoff)、[其他 FUT 交接](#future-handoffs)：下次撰寫對應規格時逐項回填，不以 memo 代替功能驗收。
