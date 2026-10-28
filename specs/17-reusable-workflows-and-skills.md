# 17：可重用工作流程（Skills）開發規格書

## 第 18 份產品實作接續（2026-10-28）

MCP 與 Skills 獨立准入及命名；技能不能 connect／reload、提供 yes 或取得供應商 Key。技能版本改變使舊外送與共同候選過時，不自動卸載有效 MCP 目錄；MCP 描述或結果不成為本機技能來源。 詳見[第 18 份](18-mcp-external-tools.md)及其固定交接；實際驗證與未驗層級見 [README](../README.md#mcp-validation)。

## 產品實作接續（2026-10-27～28）

本份的產品契約已依後續開發指示接入正式組裝；原第 1／14 節與版本 0.1／待實作標記描述規格撰寫時的文件交付，保留其歷史。來源安全核心、單一 Coordinator、六命令、版本重驗、Context、Plan、寫入／程序、保存及復原整合以本份 SKILL 契約為準；證據與尚未驗收範圍集中於[README](../README.md#skills-validation)。不將文件完成或技能載入當作流程、真人／服務、原生或 Release 驗收完成。

macOS 大小寫不敏感檔案系統另以有界、無跟隨的 basename metadata 核對技能根／ID／固定來源名稱；不列舉 ID 內資源、不擴大一般讀檔權限。跨操作目錄身分與單次嚴格前後查核維持，不宣稱消除原 TOCTOU／硬連結／掛載限制。

> 文件版本：0.1｜建立日期：2026-10-27｜狀態：待實作
>
> 選題依據：[ROADMAP：FUT-10](../ROADMAP.md#fut-10可重用工作流程skills)。上位文件：[開發總規格](../SPEC.md)第 5、6、8～15 章。FUT-10 是項目代號，17 是子規格編號；與第 10 份受控建置規格不同。本次交付為文件，以下產品介面、行為及產品驗收均待後續開發。

## 1. 目的、選定方案與交付範圍

將撰寫測試、檢查修改及維護文件等重複工作，整理成來源可核對、由使用者按需選用的純文字流程。技能指導目前已有的能力；啟用不代表開始執行、核准操作或證明流程已完成。

| 編號 | 要求 |
|---|---|
| SKILL-001 | 本次只新增本檔，最小更新 ROADMAP 的文件入口與待實作狀態；不修改產品、測試、設定、套件、README、總規格、既有子規格、Git 索引或歷史，不另建 memo、計畫、工作樹、hook 或驗收轉錄檔，不提交、推送或發布。 |
| SKILL-002 | 文件核對基線為乾淨 main@0366607765b0fb5ab03f3efb197060f63153e489。已閱讀 ROADMAP、總規格、README 及適用子規格，核對安全讀取、規則／Scope、Runner、Context、Plan、工作保存與復原的目前程式。第 15／16 份已接入產品；舊規格的待實作敘述及 README 測試數不能當成本次驗收證據。 |
| SKILL-003 | 使用者選定目前明確 Rules Scope 下的 `skills/<id>/SKILL.md`、只由使用者明確選用，以及同時一個技能跨回合啟用。初始不啟用、不發現；無祖先技能繼承或模型自動選用。 |
| SKILL-004 | 不提供腳本執行、遠端安裝、市場、技能建立／編輯／刪除工具、全域技能、動態工具登錄、MCP、網頁擷取、多模型、多 Agent 或完整 Agent Skills／YAML 相容性。資源引用、技能相依與遞迴選用不自動展開。 |
| SKILL-005 | Key 只經隱藏輸入取得並留在記憶體；技能不能成為認證、固定應用指示或硬政策來源，不能增加工作區、工具、程序、網路、刪除或核准權限。Skills 正文與外部資料不得冒充使用者確認。 |
| SKILL-006 | 既有第 07 份 BUD-S003 Release／分發門檻、第 10 份 test 不支援及未結原生驗收、各階段真人／真實 Gemini 待辦保持分列。規格完成或技能載入成功均不解除這些門檻。 |

方案取捨：普通 skills/ 目錄可沿用安全讀取，避免新增 .agents／.codex 用途例外；代價是與這些目錄布局不直接相容。只採手動單一選用，避免模型載入工具、隱式觸發與多技能排序；日後擴充須另訂契約。本版技能不需要 FUT-02 才能載入；流程若要求建置，只能使用已交付的受控能力，不能藉技能補足 test 或 shell。

### 1.1 開發者必讀順序

1. [ROADMAP](../ROADMAP.md)、[README 的目前能力及分層證據](../README.md#latest-validation)、[總規格](../SPEC.md)。
2. [12：專案規則](12-project-rules-loading.md)第 3、5～9 節及 [FUT10-HO-001～008](12-project-rules-loading.md#fut-10-handoff)；同讀 [RULE-HO 表](12-project-rules-loading.md#future-handoffs)，確認規則與按需流程的不同所有權。
3. [04：工作區](04-workspace-and-read-tools.md)、[05：工具循環](05-agent-loop.md)、[06：寫入與核准](06-file-edit-and-approval.md)、[08：搜尋](08-code-search-and-partial-read.md)，再讀 [09：Plan](09-plan-mode-and-task-list.md)、[10：受控建置](10-controlled-build-and-test.md)、[11：Context／預算](11-context-and-budget-management.md)的准入、收尾及容量契約。
4. [13：GIT-HO-008](13-git-diff-and-code-review.md#future-handoffs)、[14：TERM-HO-009](14-terminal-interaction.md#future-handoffs)、[15：SAVE-HO-009](15-work-save-and-resume.md#future-handoffs)、[16：CKPT-HO-009](16-change-checkpoints-and-recovery.md#future-handoffs)，再核對本份[逐項承接](#upstream-handoffs)。
5. 核對當時 WorkspaceSafeRead／WorkspaceProjectRules／WorkspaceRulesGates、WorkspaceOperationCoordinator、ProjectRulesCoordinator、AgentRunner／AgentContext、ContextCoordinator、ChatApplication／ChatPlanInteraction、PlanStore、WorkSessionCoordinator 及 CheckpointRecoveryState。不要附上完整應用指示、認證、SDK 原生歷史或真實轉錄。

## 2. 責任、最小內部契約與所有權

以下型別為後續最小 internal 契約，不建立通用外掛引擎或第二套工作區政策。

| 元件／型別 | 責任及必要資料 |
|---|---|
| SkillsCoordinator | 由 ChatApplication／AgentExecutionPolicy 共用同一實例；負責 Capture、ListAsync、ShowAsync、TryUseAsync、TryReloadAsync、Disable、ValidateAsync、BuildAppendix、MarkStale、Settle、BindPolicy 及 DisposeAsync。啟用決策只接受活動 CLI 使用者命令。 |
| ISkillsWorkspace | 由 WorkspaceFiles 提供 ReadSkillCatalogAsync、ReadSkillAsync、ValidateSkillSourceAsync；接受目前 Scope、固定 ID、既有租約及取消，不接受任意絕對路徑或外部來源候選。不建立公開讀檔結果。 |
| SkillDescriptor／SkillCatalog | 描述含固定 kind=skill、id、description、工作區相對 path、來源 Scope、RawByteCount、Sha256；目錄含目前 Scope、排序後的描述及排除數量，不保留正文。 |
| SkillSource／ActiveSkillSnapshot | 來源另保留完整安全檔案文字、解析後 Body、檔案／祖先身分與規則邊界觀察；啟用快照含 SkillsRevision、來源及完整投影，另由 Coordinator 管理有效狀態。 |
| SkillsOperationStamp | SessionGeneration、SkillsGeneration、SkillsRevision、Status、快照身分、RulesGeneration、RulesRevision 及 Scope；Context／Plan／核准／保存候選再沿原責任綁定各自既有版本。 |
| SkillsOperationResult | 固定結果碼及可空安全描述／完整顯示；失敗不發布部分正文或目錄。只回傳有界資料，不保存 client、session、Key 或核准答案。 |

| 編號 | 契約要求 |
|---|---|
| SKILL-007 | ChatApplication 負責本機指令與輸入生命週期；Workspace 負責實際安全 I/O；Coordinator 負責來源、版本與原子決策；Runner 負責生成與結果採用；PlanStore、Context、工作保存及檢查點保留各自所有權。 |
| SKILL-008 | 新型別均為 internal；含文字／bytes／集合的資料採密封型別、唯讀屬性、防禦性複製及固定 ToString 標記，不一般序列化整份服務或觀察。公開 ILlmClient／LlmRequest／LlmResult／LlmMessage 簽章不變，SDK 型別仍限 Llm/。 |
| SKILL-009 | 不新增模型工具或改工具輸入／成功資料；正式十三項定義的順序保持，依模式提供 Plan 九項、Execute 十二項、Review 三項。新增內部狀態、拒絕碼及版本欄位須接到正式組裝，不只建立未使用的類別。 |
| SKILL-010 | 以 WorkspaceSafeRead 的有界嚴格安全核心提供專用技能讀取，沿用路徑、敏感名稱、無連結、普通檔案、編碼及前後重驗；不得使用裸 File.ReadAllText 或另複製較寬鬆的政策。所有實際來源讀取／重驗共用 WorkspaceOperationCoordinator 排他租約。 |
| SKILL-011 | 已持租約的檔案／程序／復原操作借用同一租約重驗，不巢狀取得而自鎖；不持決策鎖等待 I/O、模型、輸入或輸出。取消／Dispose 等待自有工作及讀取關閉後才釋出租約。 |
| SKILL-012 | 發現、show、啟用與重驗不呼叫公開 ReadFileAsync，不新增、刷新或占用 read_file 修改快照，不建立 Plan Read／LocalOperation／Validation 事實，也不產生 checkpoint。後續編輯來源檔仍須原完整 read_file、差異及新核准。 |

## 3. 載入位置、發現與安全來源

| 編號 | 來源要求 |
|---|---|
| SKILL-013 | Scope=. 的固定根為 skills/；Scope=src/App 時為 src/App/skills/。只讀該根直下一層 ID 目錄的固定 SKILL.md，不合併根或祖先技能。來源身分是 Scope＋ID＋相對 path，切 Scope 後相同 ID 仍是另一來源。 |
| SKILL-014 | list 僅列舉技能根的直接項目；每個名稱合格且未被敏感政策排除的普通目錄只探查固定 SKILL.md，不列舉其內資源或其他子樹。非合格 ID／非目錄項目不讀取，敏感名稱先排除且僅計安全排除數；合格名稱的連結或特殊節點不能當普通目錄略過，整次拒絕。 |
| SKILL-015 | 技能根、ID 目錄、來源檔均受目前 Valid 規則與 Scope／祖先鏈檢查。存在未載入的 skills/AGENTS.md 或 ID/AGENTS.md 時，回原 rules_scope_mismatch，不讀其正文、不自動載入或切 Scope。切 Scope 會改變固定技能根；不得保證單純切進技能目錄即可繼續使用原來源，也不另開跨 Scope 例外。 |
| SKILL-016 | ID 以 Ordinal 比較及排序，不做大小寫、Unicode、別名或其他檔名搜尋。缺 SKILL.md 的候選目錄不成為來源；記錄有限缺席觀察供當次目錄發布前查核，不用舊缺席快取決定下次結果。敏感路徑依原政策拒絕，不為 name=secrets 等名稱設例外。 |
| SKILL-017 | list 在同一操作內先完成有界列舉、候選讀取、格式／敏感／容量驗證，再重驗技能根成員集合、祖先、存在／缺席來源及來源指紋，一次發布完整安全目錄。合格候選任一錯誤或列舉超限即整次失敗，不發布部分成功或上次目錄冒充目前結果。 |
| SKILL-018 | 安全確認技能根缺席或為空，是正式空目錄結果；拒絕存取、錯誤種類、無法重驗或來源損壞不是空集合。缺席只檢既有安全祖先與固定候選，不建立目錄。啟動及未使用任何 skills 指令時，技能載入器零來源 I/O；既有一般工具或 SAVE file_refs 重驗仍可能讀取同一路徑，只作原用途的檔案觀察，不解析／啟用技能。 |
| SKILL-019 | SHA-256 對含 BOM 的全部原始 bytes 計算；來源相等另比較 Scope、ID、路徑、檔案種類／身分／mode、原始長度、指紋、解析內容及規則邊界觀察。祖先目錄跨操作只比較原有種類／身分等必要屬性，不因無關檔案造成的目錄 mtime 變動誤報技能失效；單次安全讀取的前後查核不放寬。 |
| SKILL-020 | 一般工作只重驗目前啟用來源、其安全路徑及規則邊界，不掃描所有未啟用技能；新增或修改無關技能不自行啟用，也不因目錄 mtime 改變停止目前流程。啟用來源的替換、刪除、內容或適用邊界變動則依第 8 節處理。 |
| SKILL-021 | 目錄結果只供當次 CLI 發現，不進模型 Context 或成為啟用依據。use／show 直接安全讀取指定 ID，不必先有目錄快取；32 份限制是單次目錄結果上限，不是以完整掃描授予單檔讀取權。查詢不自動改啟用狀態；若查詢確已觀察目前啟用來源變動，仍須標 Stale，不能裝作不知。 |
| SKILL-022 | Markdown 連結、include 字樣、路徑、腳本、其他技能名稱及程式碼區塊均為惰性文字，不解析／抓取／遞迴載入，故無自動引用鏈或循環展開。若模型另用既有工具讀資源，須明確指定正常工作區相對路徑並接受原 Scope、敏感及快照規則；技能目錄不是新的相對 cwd，越界引用不取得讀取權。 |

## 4. SKILL.md 格式與合成範例

| 編號 | 格式要求 |
|---|---|
| SKILL-023 | 去除合法 UTF-8 BOM 後，第一邏輯行必須精確為 ---，其後精確兩行欄位、第四行精確為 ---；欄位為 name: VALUE 與 description: VALUE，各一行、順序可互換。冒號後恰一個 ASCII 空格作分隔，VALUE 不可空白開頭或結尾；不接受空白行、重複／未知鍵、額外欄位或未閉合標記。 |
| SKILL-024 | ID／name 長度 1～64 個 ASCII 字元，符合 `\A[a-z][a-z0-9]*(?:-[a-z0-9]+)*\z`；以小寫字母起頭，不允許連續／首尾連字號。name 與目錄 ID 必須 Ordinal 完全相同；CLI 只接受此 ID，不接受 path、空白、萬用字元或 shell 語法。 |
| SKILL-025 | description 是非空單行普通文字，最多 512 個 UTF-16 單位，不含 Tab。VALUE 不解碼 YAML 引號、跳脫、註解、anchor、tag 或 block scalar；引號及 # 如位於普通文字內只保留字面，不產生其他語法。首字為引號、\|、>、&、*、!、{ 或 [ 的值拒絕，避免把完整 YAML 誤認成支援格式。 |
| SKILL-026 | 檔案使用嚴格 UTF-8，可選 BOM；以 LF 或 CRLF 分行，保留原始換行與空白，不接受孤立 CR 或原安全核心禁止的控制字元。結束 frontmatter 後的 Body 必須含至少一個非空白字元；只去除第四行的行分隔，不 Trim、摘要或重寫正文，可有或沒有結尾換行。 |
| SKILL-027 | RawByteCount／Sha256 涵蓋完整檔案，包含 frontmatter、BOM 與結尾換行；Body 則只含第四行之後的原文。目錄可為驗證描述而有界讀完整來源，完成後丟正文；「按需」保證未選正文不保留到目錄或模型，不宣稱發現期間未讀正文 bytes。 |
| SKILL-028 | 以下是合成格式範例，不是產品已載入來源。範例採 LF、無 BOM、最後句點後無結尾換行，完整來源為 102 bytes；若實際存檔加入結尾換行，長度及指紋須重算。任何範例步驟皆不能直接建立操作或完成事實。 |

```markdown
---
name: review-change
description: Review current changes.
---
Report findings from current changes.
```

## 5. CLI、檢視與明確選用

| 指令 | 固定行為 |
|---|---|
| /skills | 顯示 Disabled／Valid／Stale、目前 Scope、SkillsRevision、可安全顯示的目前來源 ID／path／bytes／指紋；不讀磁碟，不保證當下來源尚未變動。 |
| /skills list | 在目前 Scope 下按需發現並顯示名稱、描述、完整來源 metadata、排除數量與適用範圍；沒有自動選用或模型請求。 |
| /skills show ID | 安全重讀指定完整來源並顯示，標明「目前磁碟檢視，不會啟用或更新啟用快照」；對目前 ID 另顯示是否與啟用快照相同。 |
| /skills use ID | 重讀並驗證指定來源，原子啟用或替換；成功後顯示來源版本與需要重新確認／等待新指示。 |
| /skills reload | 明確重載目前啟用 ID；Disabled 時回 skill_not_active，不猜測最近技能。 |
| /skills off | 丟棄目前技能正文與選用狀態；不寫入或刪除來源，不開始下一回合。 |

| 編號 | CLI 要求 |
|---|---|
| SKILL-029 | 遵守全域多行命令閘門後才解析；名稱大小寫敏感、Trim 外層空白、參數間單一 ASCII 空格，ID 精確符合第 4 節。多餘參數／錯誤大小寫／多 ID／path 均回 invalid_skills_command，零來源 I/O、零啟用變更，不把未知指令轉為模型訊息。 |
| SKILL-030 | 六種命令只在 AwaitingInput 受理；讀取操作沿 busy 單一輸入讀取者、預輸入排空及取消／EOF 收尾，不佇列後續命令。命令零 GenerateAsync、零新增 API 用量、不重置回合或程序預算，不進安全輸入歷史。 |
| SKILL-031 | CLI 只展示專案技能來源及受控狀態，不匯出固定應用指示、完整內部物件、絕對候選路徑、例外或協定資料。所有動態／固定標籤、欄位、指紋及組合區塊均經當前政策和原安全純文字跳脫；舊快照查詢須明標過時。 |
| SKILL-032 | show 是一次新讀取，不替代 use／reload，不自動更新目前正文。對目前來源已知不同時先標 Stale；無法安全顯示則整段拒絕，不顯示半份內容。檢視非目前技能失敗，不能單靠失敗就丟棄仍安全有效的目前技能。 |
| SKILL-033 | use 本身是明確選用，不另問 y／yes，也不視為執行確認。候選安全完整發布後才更改目前技能；有效相同來源依 SKILL-056 回 no_change。即使流程文字要求立即測試／審查，仍等待下一則一般訊息或獨立本機命令。 |
| SKILL-034 | reload 只重讀目前 ID，不替換為其他名稱、路徑或空來源；來源不見回 skill_not_found 並保留 Stale。啟用／重載候選失敗時依第 8 節重驗舊來源，不能把錯誤當作無技能成功或自動續跑。 |
| SKILL-035 | off 是純記憶體停用，在技能 Stale 或規則非 Valid 時仍可使用；它不能修復規則。Disabled 時 no_change，否則發布新停用版本、撤舊執行確認並等待新指示；無磁碟 I/O、無核准沿用或檔案清理。 |
| SKILL-036 | /help 說明手動單一選用、Scope 位置、來源可能送至 Gemini、檢視／啟用差異、過時後重載或停用、clear 損失及逐次核准。技能中的 /skills、/review、/checkpoint、yes 或按鍵序列不能餵回解析器／輸入器；F2 預覽、F3 結束及歷史回取不構成新的選用或操作核准。 |

## 6. 容量、工作量與限額

| 常數／擁有者 | 固定值與計算 |
|---|---|
| MaxSkillCatalogSources／Workspace | 每次 list 最多 32 份存在且合格的 SKILL.md；超過拒絕完整目錄，不只取前 32 份。 |
| MaxSkillDirectoryEntries／Workspace | 技能根直接列舉最多 256 項，包含非候選與排除項；讀取第 257 項即拒絕，不無界列舉後才計數。 |
| MaxSkillFileBytes／Workspace | 每份完整來源 16384 原始 bytes，包含 BOM／frontmatter。 |
| MaxSkillCatalogProjectionBytes／Coordinator | 完整目錄 JSON 最多 32768 UTF-8 bytes，包含欄名、描述、路徑、數字、指紋與跳脫；只供 CLI，不直接送模型。 |
| MaxActiveSkillsProjectionBytes／Coordinator | 第 7 節完整啟用／停用 JSON 物件連同外部來源標籤最多 32768 UTF-8 bytes，包含所有跳脫。 |
| MaxSkillsDisplayBytes／CLI | 每次完整顯示區塊最多 65536 UTF-8 bytes，包含標籤、狀態、內容及終端機跳脫。 |
| 已有 Context／Plan／模型邊界 | 規則、記憶、Plan 與復原附錄仍守各自限制；估算軟門檻 196608、實際 SDK 完整 body 硬上限 262144 bytes，不混同 token。 |

| 編號 | 限額要求 |
|---|---|
| SKILL-037 | 上表為獨立具名常數；上限可接受、超一拒絕。同時啟用數固定為 1，不建立列表疊加、最後勝出或多技能分額規則。 |
| SKILL-038 | 單檔、目錄、啟用附錄及顯示分別檢查；單檔合格不保證 JSON 跳脫後可容納。超限不截斷、刪描述、丟原文、摘要技能，亦不借用規則／記憶／Plan 的容量或提高全請求上限。 |
| SKILL-039 | 完整目錄或來源顯示超限整次拒絕，不分頁後冒稱完整、不以顯示不足的內容啟用另一份來源。use 不依賴 show 成功，但必須有完整安全啟用投影及完整受控選用結果；顯示可用性在候選發布前驗證。 |
| SKILL-040 | 一般對話與 Review 的正常請求均納入目前完整技能附錄；不得為騰空間靜默移除技能、規則、目前復原警示、必要計畫資訊或工具配對。沿原 Context 策略處理歷史；仍超硬界則零當次傳輸並停止，不重試或切模型。 |
| SKILL-041 | 來源開啟前先檢長度，每次最多讀 16385 bytes 偵測增長；候選與發布前重讀各一輪，不背景輪詢或重試。list 受 32 份與 256 項限制，每輪合法來源內容合計至多 524288 bytes；首個增長超限的來源另容許 1 byte 探測，該輪實收至多 524289 bytes，隨即拒絕。EOF 零 bytes 探測不能因內容配額剛好滿而被拒絕。候選來源最多兩輪；若失敗後依 SKILL-055 重驗舊啟用來源，另容許至多一輪 16385 bytes 的有界讀取，不算第三輪目錄或候選重試。額外祖先／規則重驗仍各受原深度及來源上限；已超限立即停止並關閉資源。 |
| SKILL-042 | 不新增 appsettings、環境變數、套件、任意路徑參數或模型欄位調整限制。沿用 .NET 10 及既有安全與 JSON writer；受限格式直接依第 4 節解析，不引入一般 YAML 引擎。 |

## 7. 投影、Context 與權限

目錄投影固定順序為 version、scope、excluded_count、skills；skills 依 ID Ordinal 排序，每筆為 kind、id、description、path、scope、byte_count、sha256。空集合範例：

```json
{"version":1,"scope":".","excluded_count":0,"skills":[]}
```

目前技能投影固定順序為 version、skills_revision、scope、active。Disabled 仍提供小型目前狀態，避免歷史轉述被誤認為仍在啟用；不用讀技能檔即可建立。

```json
{"version":1,"skills_revision":0,"scope":".","active":null}
```

Valid 的 active 欄位順序如下；此合成來源與第 4 節一致，指紋是完整 102 bytes 的 SHA-256。Body 不包含 frontmatter。

```json
{"version":1,"skills_revision":1,"scope":".","active":{"kind":"skill","id":"review-change","description":"Review current changes.","path":"skills/review-change/SKILL.md","scope":".","byte_count":102,"sha256":"2656293af027bea3b084c39cfb343f37d446cd5c475bb412443fe92cfd2d3889","body":"Report findings from current changes."}}
```

| 編號 | 投影與權限要求 |
|---|---|
| SKILL-043 | 依上述欄位與順序使用明確 JSON writer；版本固定 1，指紋為 64 位小寫十六進位，數值來自本機。Stale 不建可用附錄。完整原文、metadata 與組合附錄經當前政策，底層觀察／絕對路徑不進投影，來源只能由本機專用讀取產生。 |
| SKILL-044 | kind=skill 與 project_rule、使用者、固定應用政策明確分開。程式硬政策最高；在既有授權內目前明確使用者要求、適用專案規則優先於技能。技能可要求較保守做法，但自然語言不會自動成為可證明的程式權限限制，description 也不是權限宣告。 |
| SKILL-045 | 正常請求順序明示增量為固定應用指示 → 目前規則 → 目前技能狀態／原文 → 原記憶 → 最新計畫 → 既有 Review 來源 → 目前復原狀態。技能標明外部流程、不能授權；沿 LlmRequest.SystemInstruction 承載，不新增假使用者訊息或工具回合。一般回合與 /review 都重建目前附錄；Review 固定唯讀工具、輸出格式與本機來源查核優先於技能文字。 |
| SKILL-046 | compact 與 WorkSaveSummary 不直接加入技能正文／目錄作為摘要來源或摘要指示；SkillsRevision／Generation／Scope 納入 ContextOperationStamp、MetricStamp、摘要與保存候選的過時判斷。歷史 UserText／AssistantText 或安全摘要可能重述技能，不保證自然語言剔除；轉述不恢復啟用或授權。 |
| SKILL-047 | 完整附錄及 SDK 再跳脫納入估算／實際 body；原 Messages、完整工具配對、SDK thought／簽章及不透明協定所有權不變。不另開 client、隱藏生成或記帳入口；技能命令不增減用量，一般、Review、摘要與保存生成仍走同一 Runner／預算。 |
| SKILL-048 | 單一技能不涉及技能間自動排序；技能內部或與使用者／規則有無法相容的語意要求時，模型應回報來源請使用者釐清，不聲稱載入器已理解並解決所有衝突。技能不能委派、自動切模式／Scope、確認計畫、啟動 /review、復原、腳本或網路；使用者另發命令及既有工具准入仍各自成立。 |

## 8. 版本、重驗、候選與生命週期

| 編號 | 狀態與重驗要求 |
|---|---|
| SKILL-049 | 狀態為 Disabled（無啟用來源）、Valid（目前安全啟用快照）、Stale（已知啟用來源或適用版本失效）。Disabled 是正常可工作狀態；Stale 阻擋新一般／Review／摘要／保存生成及 /execute 確認，直到使用者明確 reload／use 成功或 off。初始化失敗的服務不能偽裝 Disabled。 |
| SKILL-050 | 同一 ChatApplication 的 SkillsRevision 初始 0；每次成功初啟用、替換、失效後重新啟用或由有技能轉停用加 1。SkillsGeneration 撤銷在途候選／結果時遞增；SessionGeneration 區分生命週期。clear／換 Key 不重用計數，long 溢位安全停止，不回繞或以新 Coordinator 重設序號。 |
| SKILL-051 | 候選捕捉技能戳記、目前 RulesRevision／Generation／Scope、取消及原有操作版本，發布／採用時全部一致才可使用。Context 量測、Plan 確認預覽、檔案／程序／復原核准與工作保存候選都須綁目前技能戳記；技能版本不能取代任何既有版本。 |
| SKILL-052 | 重驗點至少包括：一般／Review／摘要／保存生成前、每次模型結果採用前、最終文字顯示與成功歷史提交前、工具整批預檢與每項開始前、檔案／程序／復原核准後且發布／啟動前、計畫確認及摘要／保存候選發布前。純文字回答也不能略過。直接工作區／程序／復原入口同樣執行，不能只在 Runner 或提示中檢查。 |
| SKILL-053 | 啟用來源新增邊界、改寫、刪除、身分／mode 改變或無法安全重驗時標 Stale、拒絕新工作及遲到成功結果，停止剩餘工具／生成。只讀／hash／規則閘門的有界重驗是必要本機 I/O，不算模型要求的工具已執行；不能用舊正文或自動停用繼續。 |
| SKILL-054 | use／reload 先完整驗證來源、重讀一致性、投影、顯示及全部戳記，再在原決策鎖一次發布快照／狀態／版本與確認失效決策。候選只在有界記憶體；任何取消、容量或來源失敗不得半套用新正文、Scope 或確認。 |
| SKILL-055 | 新候選失敗時，原狀態若 Valid，須重驗舊來源仍安全且適用才可保持；舊來源已變則 Stale。原本 Stale 不因失敗恢復 Valid；原本 Disabled 且普通失敗仍 Disabled，可繼續無技能工作。敏感、無法安全收尾或共同重設條件仍走原較嚴格路徑。 |
| SKILL-056 | 只有目前 Valid、完整來源／規則戳記與投影相同且政策通過，use／reload 才回 no_change，不增版或撤確認。已 Stale 即使還原原 bytes，成功 reload 仍是新版本；舊核准永不復活。 |
| SKILL-057 | 同 Scope 的不同 RulesRevision 成功發布時，目前技能標 Stale，須在新規則下明確 reload／use；有效相同規則 no_change 保留技能。Scope 成功改變時直接停用、丟舊正文／目錄及候選，不掃新技能根；Scope 候選失敗則依規則原子契約保留或拒絕舊狀態。規則本身非 Valid 時原規則閘門仍阻擋，不以 off 放寬。 |
| SKILL-058 | /clear、共同安全重設、接受替換 Key 及退出丟啟用正文、目錄與候選；保留原模式、工作區及程序預算／用量，技能計數不重用。新 Key 不重載／啟用舊技能。--resume 預設 Disabled，SAVE v1 不加技能狀態／正文／戳記，安全歷史摘要或 file_refs 只作歷史，不能啟用或重建來源信任。 |
| SKILL-059 | 讀取中 Ctrl+C 取消當次命令回輸入；EOF／Ctrl+D 等待收尾後正常退出 0，無法恢復的 I/O／輸出／收尾故障退出 1。發布前取消保留完整舊狀態，發布後取消不回滾已接受版本；已知 Stale 不因取消消失。Dispose 先拒新工作、撤候選、取消並等待自有工作，再丟來源／政策；遲到結果不得復活資料。 |
| SKILL-060 | 延續第 04／12 份應用層安全讀取保證，不宣稱抵抗精確交換後還原、所有硬連結別名、掛載變動或新增 OS 沙盒。hash／版本用於變動查核，不證明作者、簽章信任、自然語言安全或模型必然遵守。沒有背景監看；/skills 狀態是上次安全觀察。 |

## 9. Plan、寫入事實與復原整合

| 編號 | 整合要求 |
|---|---|
| SKILL-061 | 初啟用、不同技能／來源替換、非 no_change 停用或 Stale 均使舊執行確認及操作預覽失效：PlanId／Revision、任務與歷史事實保留，ApprovalState 回 Draft，執行代次增加，在途任務安全收尾後 Blocked／skills_changed，清空 read_file 修改快照與失效 Context／保存候選。模式保持；若無計畫，不新增一套 no-plan 確認，既有續接／復原確認仍有效執行。 |
| SKILL-062 | 像規則失效一樣，已取得發布權的操作先在原預留／代次下完成查核、記錄 Verified／其他實際結果及安全回報，再完成確認撤銷與代次變更。不得先改 PlanGeneration 使 A 的事實入帳失敗；A 已發生、B 未開始時分別回報，停止 B／後續生成，不重播或復原 A。 |
| SKILL-063 | 計畫確認、檔案差異、程序及復原預覽綁技能戳記。等待確認期間來源變動，yes 後仍須重驗並拒絕該次發布／啟動；原 yes 不轉給新候選。reload 或 off 只修復技能狀態，仍須原計畫重新確認、新訊息及新的逐次操作預覽／輸入。 |
| SKILL-064 | 技能切換不增加 EvidenceEpoch、不假造 RulesRevision、不新增流程完成條件或證據種類。既有事實仍依原 rules／epoch／計畫／任務條件選取；它們只證明當時實際操作，不證明符合新技能的全部步驟。已接受 Completed 保留歷史；需要不同完成條件時須明確修訂計畫，不能由技能或摘要改寫。 |
| SKILL-065 | 經既有完整讀檔及新核准修改目前 SKILL.md 時，使用原技能戳記完成該筆預覽、發布及結果查核；不得把預期自身寫入誤報為發布前外部衝突。先記寫入／checkpoint 事實，再標技能 Stale 並停止其餘工作。復原目前技能也先記復原結果／RecoveryRevision／EvidenceEpoch，再失效技能；回到舊 bytes 不恢復舊版本或 yes。 |
| SKILL-066 | 檢查點的原始 bytes、來源事件、鏈與復原資格仍由第 16 份擁有。技能啟停不刪 checkpoint 或改 EvidenceEpoch；復原其他檔案仍維持原 recovery 失效與重新確認，技能需重驗但不因此自動卸載。技能文字、MCP、網頁或子 Agent 自報不得建立紀錄、呼叫本機 restore 或匯入外部資格。 |

## 10. 敏感資訊、固定結果與失敗處理

| 結果碼 | 固定語意 |
|---|---|
| none／no_change | 操作成功／目前有效狀態完全相同；不表示執行流程或核准操作。 |
| invalid_skills_command | 命令或 ID 語法不符，零來源 I/O。 |
| invalid_skill_format | 合格來源的 frontmatter、名稱、描述、正文或換行格式不符。 |
| skill_not_found／skill_not_active | 指定來源缺席／reload 時無啟用 ID；與合法空目錄不同。 |
| skills_limit | 來源數、列舉工作量、描述／投影／顯示上限不足；一般單檔過大可沿用 file_too_large。 |
| skills_changed | 啟用來源／技能版本已失效，或本回合捕捉版本與目前不同。 |
| skills_unavailable | 目前需使用的技能服務或安全快照不可得；不得用 Disabled 掩蓋。 |
| 原工作區／規則錯誤 | invalid_path、sensitive_path、symbolic_link、invalid_utf8、binary_content、sensitive_content、path_changed、access_denied、io_failure、rules_unavailable、rules_changed、rules_scope_mismatch、rules_limit 等維持原意，不另造較寬鬆的技能版本。 |

| 編號 | 安全與失敗要求 |
|---|---|
| SKILL-067 | 原始／正規化路徑、ID、metadata、全部來源文字、JSON 實際欄名與字串值、指紋／數值及組合附錄／CLI 區塊均經當前敏感政策。含當前 Key 整份拒絕，不遮蔽後當成功技能；短 Key 命中固定欄位或組合接縫也無例外，不冒稱通用秘密掃描。 |
| SKILL-068 | show 只顯示來源技能，不輸出固定應用／摘要／審查指示、認證、thought、簽章、SDK／HTTP 或例外原文。讀取候選敏感先拒絕，無不安全內容發布；若目前啟用來源或已進模型的資料觸發原 RequiresContextReset 條件，沿共同安全重設，不能僅把錯誤列為一般格式失敗。 |
| SKILL-069 | 命令依上表回固定結果；來源安全錯誤沿原分類，未知例外使用受控診斷。Workspace／ToolErrorCodes／Runner 新增 skills_unavailable、skills_changed、skills_limit 及對應停止理由；Plan 增加 skills_changed 受阻原因，不新增公開 LlmErrorCode。其他技能命令結果只用本機入口，不新增工具 Schema。 |
| SKILL-070 | 工具整批預檢發現 Stale 時，零模型要求的內容讀寫、任務更新、核准或程序探針，依原協定回完整 NotExecuted 配對；失敗 data=null。必要的本機撤確認及受阻收尾仍執行，不將查核失敗映成工具成功。回合為 Stopped，純文字遲到回答不顯示為成功或提交成功歷史。 |
| SKILL-071 | 敏感／非法協定／Unconfirmed／CleanupFailed／無法確認程序停止等更嚴格重設或退出優先，不被 skills_changed／取消蓋過。已消耗生成與可得用量照記、不退款；不自動重試、停用技能、換路徑／模型、壓縮後補送、重放操作、復原或批次清理。 |
| SKILL-072 | Stale 時仍允許安全本機狀態、list／show、use／reload／off、規則處理及原本允許的本機管理命令；每項仍受自己的模式／有效規則／確認限制。尤其 restore 不因可用於本機管理而繞過技能有效性，/execute、compact、work save 也不能避開生成／候選閘門。使用者可 off 後明確重新規劃。 |

## 11. 既有契約的明示增量與後續同步

| 原契約擁有者 | 未來產品實作須同步的增量及保留 |
|---|---|
| SPEC 第 5、6、8～15 章；第 03／05 份 | 六種本機命令、目前技能附錄與技能停止理由；不增模型工具、認證、SDK 公開介面或假使用者授權。 |
| 第 04／06／08 份 | 專用安全來源讀取與技能閘門、啟停失效 read_file 基準、目前來源自修改後停止；原 Scope／敏感／搜尋／單檔差異與核准不放寬，讀取本身不建證據。 |
| 第 09 份 PLAN-037～043、050～054 | 新技能戳記、確認撤銷與 skills_changed 保守收尾；事實判定、256 筆容量與 EvidenceEpoch 保留，不發明流程完成 basis。 |
| 第 10 份 BUILD | 程序直接入口及核准後技能重驗；固定 recipe、cwd、原生隔離、每回合 2 次／300 秒及 test 不支援保持。 |
| 第 11 份 CTX-050～080 | 新附錄容量、Context／量測／候選技能戳記及共同清除；不直接摘要技能全文、不加生成或放寬 body／預算。 |
| 第 12 份 RULE-012～019、027～064、FUT10-HO-001～008 | 共用最小安全讀取、原 Scope／規則鏈；規則來源仍由原 Coordinator 擁有。技能版本與規則版本分開，換 Scope 停用／同 Scope 規則改版待重載明示。 |
| 第 13／14 份 GIT-HO-008、TERM-HO-009 | Review 使用目前流程但只允許三個 Git 工具；新命令沿單行／敏感／busy／歷史契約，不把外部文字回送輸入器。 |
| 第 15 份 SAVE-037、065、SAVE-HO-009 | 保存候選含技能過時判斷；SAVE v1 無新欄位，續接 Disabled、歷史不啟用，摘要不直接納技能來源。 |
| 第 16 份 CKPT-043～048、057～069、CKPT-HO-009 | 復原候選綁技能戳記、目前技能來源復原先記事實後 Stale；原 anchor、RecoveryRevision／EvidenceEpoch 及逐筆核准保留。 |

| 編號 | 相容性要求 |
|---|---|
| SKILL-073 | 後續產品實作須同一變更同步上表的正式組裝、狀態／錯誤映射、全部准入點、/help、README、SPEC 與相關子規格接續說明；本次不提前回寫產品完成或修改原契約正文。 |
| SKILL-074 | Plan 實際／保守投影及完整顯示須容納技能版本／停止原因／確認說明及必然收尾餘裕，仍守 32768／65536 bytes。舊合法計畫及在途 255→256 事實情況下仍能先記事實再撤確認，不能到失效時才因容量留下可執行舊確認。 |
| SKILL-075 | 既有 01～16、Python 敏感掃描及適用 PTY 目的保留，只按明示增量更新工具集合／CLI／投影預期；不以技能文字略過安全測試、放寬掃描、宣稱一般 test 可用或把舊驗證數算成本次通過。 |

## 12. FUT 耦合矩陣與固定交接 memo

必要能力是受限工作區安全讀取、目前規則／Scope、單一 CLI 讀取者及現有狀態／核准閘門。FUT-03／04 在目前產品已存在，須整合確認／Context，但不要求先完成它們所有延伸功能。FUT-02、06、08、09 不是純文字技能載入的必要前置；引用其能力時各自的原契約仍有效。

<a id="upstream-handoffs"></a>

### 12.1 上游必讀交接逐項承接

| 原交接／擁有者 | 本份承接章節與需求 | 後續驗收 |
|---|---|---|
| FUT10-HO-001／第 12 份 | 第 2～4、7 節；SKILL-007～009、013、019、023～028、043～044：區分描述、原文、skill／project_rule 及來源版本，只重用最小讀取。 | SKILL-AC-001、004、011 |
| FUT10-HO-002／第 12 份 | 第 3、6、10 節；SKILL-010、013～019、037～042、067～068：普通 Scope 內位置、無敏感例外，路徑／種類／內容及一般工具旁路仍拒絕。 | SKILL-AC-002～008、014 |
| FUT10-HO-003／第 12 份 | 第 3、5、8 節；SKILL-013～018、021、029～036、057：明確單一選用、Scope＋ID、不繼承，選用不切 Scope 或核准。 | SKILL-AC-002～003、008～010、023 |
| FUT10-HO-004／第 12 份 | 第 5、7、9 節；SKILL-033、036、044、048、061～064：使用者／規則優先及語意衝突回報，技能無升權或完成資格。 | SKILL-AC-010～011、018、026～027 |
| FUT10-HO-005／第 12 份 | 第 6～7、11 節；SKILL-037～047、074：原始／描述／正文／顯示各自限量，完整 body 及必然收尾不放寬。 | SKILL-AC-005、012～013、018、028 |
| FUT10-HO-006／第 12 份 | 第 8～9 節；SKILL-049～057、061～065：共同戳記、原子候選、結果閘門及先記事實後失效。 | SKILL-AC-015～023、025 |
| FUT10-HO-007／第 12 份 | 第 2～3、7、9 節；SKILL-005、009～012、022、047～048、064～066：引用惰性、零快照／完成證據、只組合既有能力。 | SKILL-AC-007、011、019、026～027 |
| FUT10-HO-008／第 12 份 | 第 7～8、12 節；SKILL-046、050～051、057～059、077～078：啟用生命週期、續接／新認證不恢復，未來模型／子 Agent 另定。 | SKILL-AC-017、021～025、032 |
| GIT-HO-008／第 13 份 | 第 5、7 節；SKILL-009、033、036、044～048：已啟用技能可指導使用者明確發起的 Review，但不改固定唯讀能力或報告來源。 | SKILL-AC-011、026～027 |
| TERM-HO-009／第 14 份 | 第 5、10 節；SKILL-029～036、059、067～072：外部文字不是按鍵、yes 或本機命令，沿單一讀取者及新輸入。 | SKILL-AC-009～010、025、027、029～030 |
| SAVE-HO-009／第 15 份 | 第 7～8、11 節；SKILL-046、051、058、073：SAVE v1 無技能狀態，歷史／續接不啟用，保存候選綁目前版本。 | SKILL-AC-021～024、032 |
| CKPT-HO-009／第 16 份 | 第 8～9 節；SKILL-048、051～052、062～066：文字不能呼叫復原或建立資格，來源復原先記結果後 Stale。 | SKILL-AC-018～020、027、032 |

<a id="future-handoffs"></a>

### 12.2 對其他 FUT 的前向交接

| 交接編號／對象 | 關係與本份已定邊界 | 後續責任與驗收 |
|---|---|---|
| SKILL-HO-001／FUT-01 | 相容性整合，無新搜尋格式依賴；來源讀取不建搜尋結果／修改快照（SKILL-010～012、015、022）。 | 搜尋／索引改版仍守目前 Scope、新規則邊界與敏感排除；不能把片段或技能 Body 當修改基準。 |
| SKILL-HO-002／FUT-02 | 條件式能力組合，純文字技能不依賴程序；固定能力及新核准由原執行器擁有（SKILL-005～006、048、051～052、063、071）。 | 新 recipe／修正循環另訂隔離／預算／來源戳記，技能不授權 shell、安裝、網路、test 或自動重試。 |
| SKILL-HO-003／FUT-03 | 直接共用確認／收尾；無流程完成證據（SKILL-061～064、074）。 | 計畫或證據改版保留各版本與容量，不由技能改任務／完成條件、復活確認或假稱已遵循新流程。 |
| SKILL-HO-004／FUT-04 | 直接共用 Context／預算／候選；原文附錄獨立、摘要不直接納正文（SKILL-040、045～047、050～052）。 | 新壓縮／量測策略保留完整目前技能與失效判斷，不加隱藏生成、退款或提高 body。 |
| SKILL-HO-005／FUT-05 | 必要 Scope／規則能力；兩來源分別擁有（SKILL-013～020、044、057）。 | 規則多 Scope／來源改版明示技能根與戳記遷移；不能把技能當 project_rule 或全面開放敏感目錄。 |
| SKILL-HO-006／FUT-06 | 無必要格式依賴；Review 仍固定受控唯讀（SKILL-009、045、048）。 | 新審查組合另訂觸發與來源，技能不能自行啟動 /review、解除唯讀或將零 finding 改成 TestsPassed。 |
| SKILL-HO-007／FUT-07 | CLI 直接整合，無新按鍵格式（SKILL-029～036、059）。 | 新輸入／進度保持單行、命令不入歷史、唯一讀取者及外部文字不回放；不讓草稿或貼上代替新核准。 |
| SKILL-HO-008／FUT-08 | 保存候選共用版本；無持久技能格式依賴（SKILL-046、051、058）。 | 如需保存技能參照，須另訂版本／資料最小化／來源重讀與明確選用；不能直接增加 SAVE v1 欄位或從摘要恢復啟用。 |
| SKILL-HO-009／FUT-09 | 復原候選直接共用戳記；來源 bytes／anchor 仍屬檢查點（SKILL-051～052、062～066）。 | 新寫入來源／持久復原另訂資格與衝突；技能步驟不是來源事件、外部 bytes 或 restore 核准。 |
| SKILL-HO-010／FUT-11 | 無必要依賴；本版沒有外部工具／動態工具登錄（SKILL-004、009、044、048、066）。 | MCP 命名、認證及工具能力另定，同名 skill／工具結果不能冒充本機來源、核准或傳出供應商 Key。 |
| SKILL-HO-011／FUT-12 | 無必要依賴；連結惰性、無遠端載入（SKILL-004、022、048）。 | 網頁／文件引用、目的地與容量另定；遠端描述不自動安裝／啟用，也不成為目前規則或本機證據。 |
| SKILL-HO-012／FUT-13 | 前向政策／client 切換契約；本版換 Key 丟啟用（SKILL-008、047、050～051、058、067）。 | 模型／供應商切換須另訂重新驗證與明確啟用，不沿用別 client 原生歷史、認證或失敗後隱式重送。 |
| SKILL-HO-013／FUT-14 | 無必要依賴；單一啟用者／讀取者，沒有父子啟用狀態（SKILL-003～004、030、048、051）。 | 委派另訂 Scope 子集、來源版本、取消、共用預算與啟用傳遞；子 Agent 結論不能改父技能、讀鍵、核准或匯入完成事實。 |

| 編號 | 交接要求 |
|---|---|
| SKILL-076 | 上游十二列逐一回填本份章節／需求／案例，保留原穩定錨點；不得只寫「與 FUT-05 相同」或略過後續第 13～16 份。改共用來源、版本、權限、容量或完成語意時，明列原擁有者、受影響需求與遷移。 |
| SKILL-077 | 本節是本份交接 memo 唯一來源，不另建重複筆記。後續 FUT 須承接適用 SKILL-HO-001～013 並核對當時產品；原規則、Plan、Context、Review、保存與復原契約仍由原規格擁有，前向列不是提前定義完整後續功能。 |
| SKILL-078 | 各 FUT 分別開發及驗收，沒有必須合併的項目；無必要依賴不等於忽略目前已存在的整合閘門。memo 不代表能力、格式共享、授權或完成證據，不為交接新增空殼服務或實作 FUT-11～14。 |

## 13. 後續產品驗收

| 編號 | 驗收方法 |
|---|---|
| SKILL-079 | 離線案例使用合成 Key、可控制模型／終端機／檔案系統及真實暫存檔；SDK 使用真實轉接器配假 HTTP handler。檢查實際 I/O／呼叫數、版本、狀態、bytes、確認與事實，不連真實 Gemini、不把正式工作區當 fixture，也不傾印不安全輸入。 |
| SKILL-080 | 產品開發後才依既有 restore、build、MSTest、Python 敏感測試、--help、format verify、PTY 及 diff 查核流程執行，記錄當次基線／環境、命令、退出碼與通過／失敗／略過數；離線、PTY、真人、服務與原生未驗事項分列。本次不執行產品命令、原生探針或 Gemini 請求。 |

### 13.1 離線、整合與 SDK 案例

| 案例 | 對應需求 | 後續成功條件 |
|---|---|---|
| SKILL-AC-001 | SKILL-001～012、028 | 正式組裝只有一份技能 Coordinator，責任／來源可分，公開 LLM／工具集合保持；文件與產品交付分清，無 SDK／Console／檔案旁路。 |
| SKILL-AC-002 | SKILL-013～019、024 | 根／子 Scope 使用各自固定技能根；同 ID 不繼承、Unicode／大小寫別名／..／絕對或敏感路徑、連結／FIFO／socket／種類交換拒絕；無跨 Scope 讀取。 |
| SKILL-AC-003 | SKILL-014、016～018、021、037、041 | 缺根／空根／缺 SKILL、非候選及敏感排除；目錄排序／排除數明確，32／33 來源、256／257 項、發布前新增／刪除／替換成員均無部分成功；use 不靠舊目錄授權。 |
| SKILL-AC-004 | SKILL-019、023～028、042 | name／description 交換順序可用；缺欄／重複／未知、錯名、空正文、非法 YAML 形式拒絕；ID 1／64／65、連字號邊界、描述 512／513 UTF-16、前後空白與 Tab 正確。範例 hash／bytes 可重算。 |
| SKILL-AC-005 | SKILL-025、037～042、043 | raw bytes 16384／16385、目錄與附錄 32768／32769、顯示 65536／65537，含跳脫膨脹及完整固定欄位；各上限獨立、無截斷／借額度。32 份各 16384 bytes 可完成 EOF 查核；31 份滿額加最後一份增長時可讀到第 524289 byte 後拒絕，超量讀取有界且關閉完成。 |
| SKILL-AC-006 | SKILL-010、019、026～027、041、060 | UTF-8 無／有 BOM、LF／CRLF、中文／emoji、結尾換行、非法 UTF-8、孤立 CR／控制字元；raw 指紋與 Body 正確，無關父目錄 mtime 不誤報，精確競態／硬連結保證範圍如實。 |
| SKILL-AC-007 | SKILL-010～012、022、027、066 | 發現／show／use／重驗零 read_file 快照／Plan 事實／checkpoint；正文不保留到目錄，不遞迴展開 links／include／腳本，資源正常工具不能越界或取代完整修改基準。 |
| SKILL-AC-008 | SKILL-015、017、020～021、052～053 | skills／ID 途中 AGENTS 存在或在結果發布前新建，零來源正文／部分目錄、不自動切 Scope；目前技能邊界新建阻擋，無關未啟用來源變動不阻擋目前工作。 |
| SKILL-AC-009 | SKILL-024、029～030、036、069～070 | 六種精確命令、外層空白、大小寫／多參數／path／多行拒絕，無效命令零來源 I/O；命令零生成、不入歷史、不重置額度，失敗沿受控結果。 |
| SKILL-AC-010 | SKILL-030～036、049、055、059、072 | list／show 不啟用，use 後跨回合單一保留，show 不更新啟用原文；無 active reload、重複 off、Stale／規則失效時 off、busy／取消均不自動開始回合或接受預輸入。 |
| SKILL-AC-011 | SKILL-008～009、043～048 | skill／project_rule／使用者及來源 metadata 可區分，請求附錄順序與 full Body 正確；技能與規則衝突不升權，不建立假訊息或解析模型／技能提交來源。 |
| SKILL-AC-012 | SKILL-018、037～038、040、043、049、058 | 初始／停用狀態明確 active=null，零技能 I/O；已停用的歷史轉述不復活技能。不同 Scope 狀態、固定字串及數字同樣受容量／敏感檢查。 |
| SKILL-AC-013 | SKILL-037～047、071 | 規則、技能、記憶、Plan、Review、復原及工具合計的估算／實際 body 正確，196608 軟門檻仍依原整理；不刪技能騰空間、不加生成／用量／重試或改既有預算。 |
| SKILL-AC-014 | SKILL-005、019、031～032、043、067～068 | 合成短 Key 命中 ID／path／描述／正文／hash／固定欄名／數字／拼接附錄均整份拒絕；無原始錯誤／內部指示／認證洩漏，需共同重設的目前來源走原路徑。 |
| SKILL-AC-015 | SKILL-019～021、049、051～053、060、070 | 同長改寫、刪除、身分／mode／規則邊界改變，在生成前、await 後、純文字顯示與成功歷史提交前均停止；遲到結果不採用，開始生成仍記額度／可得用量。 |
| SKILL-AC-016 | SKILL-033～034、039、041、054～055、067～071 | 候選格式／讀取／投影／顯示失敗、兩次重讀不一致、取消時完整舊或完整新狀態擇一；安全舊來源可保留，舊來源已變維持 Stale，初次普通失敗保持 Disabled，無隱式降級續跑。第二輪候選失敗後仍可另作一次有界舊來源重驗，不重新掃目錄或重試候選。 |
| SKILL-AC-017 | SKILL-049～051、054、056、058～059 | Valid 相同 use／reload no_change，Stale 回原 bytes 再載仍新版本；停用／清除／換 Key 不重用序號，世代不符的候選拒絕，計數溢位停止。 |
| SKILL-AC-018 | SKILL-051、061～064、069～071、074 | 技能初啟用／替換／停用／失效撤確認，計畫身分／歷史 Completed／facts 保留；無 Plan 不新增確認。接近投影上限與 255→256 預留時 A 已完成仍入帳、B 未執行；EvidenceEpoch／RulesRevision 不冒用。 |
| SKILL-AC-019 | SKILL-012、019、052、062、065～066 | 已核准寫入或復原 active SKILL，先記真實寫入／checkpoint／recovery 事實再 Stale；預期自身發布不誤拒，舊 bytes 不復活核准；其他檔復原保留原 recovery 規則，不自動卸載有效技能。 |
| SKILL-AC-020 | SKILL-011、051～052、061～063、071～072 | Plan／檔案／程序／復原確認 await 期間改來源，yes 後零該次發布／啟動；新 reload／off 不重用 yes，直接入口也拒絕；同租約重驗無死鎖、收尾故障不降級。 |
| SKILL-AC-021 | SKILL-040、046～047、051～055、058、071 | 手動／自動 compact、摘要候選發布及量測 await 間技能改變，遲到結果／量測可辨識且不發布；摘要請求不直接帶技能正文，歷史轉述仍不授權，唯一用量不退款。 |
| SKILL-AC-022 | SKILL-046、051～052、058、069、073 | work save 摘要／預覽／確認／發布間技能變動使候選失效，零舊候選保存；SAVE v1 不加欄，--resume 即使歷史提技能或 file_refs 也 Disabled、不發現／啟用技能；原 file_refs 安全重驗可讀同一路徑，但不解析 frontmatter、建立技能來源或啟用資格。 |
| SKILL-AC-023 | SKILL-013、049～051、056～057、061 | 規則有效 no_change 保留 active；同 Scope 新規則使 Stale，明確 reload 才恢復；Scope 變更清舊正文且不掃新技能，失敗 Scope 候選遵守原子保留／失效，不跨邊界偷載。 |
| SKILL-AC-024 | SKILL-030、046、050～051、058、061、071 | clear／共同重設／換 Key／退出清正文／目錄／候選與讀取基準，新政策不能用舊檢查結果或自動啟用；模式／程序預算／用量依原契約保留，遲到結果不復活。 |
| SKILL-AC-025 | SKILL-010～011、030、051、054～055、059、071～072 | 來源讀取前／中／發布前後取消、EOF、Dispose 競態；完整原子狀態、排他租約與單一讀取者，等待真實關閉；已接受版本不回滾，I/O／輸出／收尾故障退出 1。 |
| SKILL-AC-026 | SKILL-004～006、009、036、040、044～048、052 | 明確 /review 帶目前技能，但固定三工具與報告 Schema／來源查核保持；技能要求 build／寫入／額外工具／格式破壞仍拒絕，零 finding 不建立測試或任務事實。 |
| SKILL-AC-027 | SKILL-004～005、012、022、029、033、036、044、048、063～066 | 技能／規則／存檔／網頁／MCP／其他 Agent 的 yes、指令、相依／循環或自報不能啟用、切 Scope、升權、建立修改／復原資格、核准操作或假造流程完成。 |
| SKILL-AC-028／真實 SDK＋假 HTTP | SKILL-008～009、038、040、043～047、051～053、067、070、074 | 完整附錄、工具、多結果／簽章、Plan 及 recovery 合計 body 262144／262145 bytes；超限零當次傳輸、原生配對不改。含 JSON 再跳脫、短 Key、來源 await 變動及保守投影收尾，無第二 client／隱藏生成。 |

### 13.2 PTY、真人、真實服務與回歸

| 案例／層級 | 對應需求 | 後續驗收方式 |
|---|---|---|
| SKILL-AC-029／離線 macOS PTY | SKILL-029～036、049、052、057～059、063、067、072、079～080 | 現有 TerminalHost／假模型以明確同步閘門驗六命令、描述／完整來源、Stale 重載／停用、busy 排空、Ctrl+C／Ctrl+D、確認間來源變動及回顯恢復；多行技能命令拒絕，命令不入歷史。不用睡眠猜來源變動時機。 |
| SKILL-AC-030／真人終端機 | SKILL-025～026、029～036、048、057～059、063、079～080 | 使用合成技能核對中文／Unicode 描述與正文、macOS IME／功能鍵、來源／Scope 可辨識、檢視與啟用區分、失效後新確認及新指示；不以 PTY／GUI 自動化代替。 |
| SKILL-AC-031／真實 Gemini | SKILL-003～006、022、033、040、043～048、052～053、061～068、071、079～080 | 使用者自行隱藏輸入 Key，觀察明確選用對一般任務與 /review 的指導、規則優先、變動後停止、重載／停用及逐次核准；只記安全結果，不保存 Key 或完整應用指示。模型遵循是觀察，不是載入器保證。 |
| SKILL-AC-032／交接與相容性 | SKILL-007～012、046～047、051、058、064～066、069、073～080 | 十二列上游逐項可追溯、十三列前向邊界明確；SAVE v1、Plan 證據／epoch、recovery、規則及工具所有權保持，沒有以技能提前實作其他 FUT 或解除未結驗收。 |
| SKILL-AC-033／既有回歸 | SKILL-001～006、009～012、037～042、047、058～066、069～075、079～080 | 原 01～16、敏感掃描與適用 PTY 目的保留；Plan 禁副作用、逐次核准、實際 body、一般失敗即停、test 不可用、預算／原生／Release 門檻保持。記當次實際數，不引用舊結果當通過。 |

## 14. 本次文件查核與完成條件

| 編號 | 文件要求 |
|---|---|
| SKILL-081 | SKILL-001～084、SKILL-AC-001～034、SKILL-HO-001～013 唯一連續，每項需求皆有案例／固定交接對照；上游十二列逐項承接，不留下本版需實作者選方案的 TBD。 |
| SKILL-082 | 以不落地腳本解析全部 JSON 範例、核對完整來源 102 bytes／SHA-256、格式與欄位／值、Markdown 圍欄／表格、全部相對連結及穩定錨點。檢查 Scope、來源、失效、先記事實後撤確認、容量與各項所有權一致。 |
| SKILL-083 | 完整讀回新檔與 ROADMAP 差異；新檔另用 no-index 空白檢查，不因一般 git diff 未列未追蹤檔就當成已驗。確認只新增本檔並最小修改 ROADMAP，Git 索引未變，沒有額外文件、產品實作或清理。 |
| SKILL-084 | 只回報本次實際文件查核及未執行的產品驗收；保留版本 0.1／待實作與既有未結門檻。後續開發者先依必讀順序及固定交接核對當時程式，再另行實作；文件完成不自動啟動開發。 |

| 案例 | 對應需求 | 本次文件成功條件 |
|---|---|---|
| SKILL-AC-034／文件 | SKILL-001～006、028、073、076～078、081～084 | 需求／案例／交接編號及承接表完整，JSON／來源範例／Markdown／本機連結／錨點／空白查核通過；目前技能、規則、Plan、保存與復原責任一致，只有指定兩份文件變更、索引不變，零產品實作／驗收。 |

文件查核包括以下命令；no-index 的差異存在與空白診斷須分開判讀，不能只依非零退出碼判失敗。

```sh
git diff --check
git diff --no-index --check -- /dev/null specs/17-reusable-workflows-and-skills.md
git diff -- ROADMAP.md
git status --short
git diff --name-only
git diff --cached --name-only
```

## 15. 參考與後續閱讀

- [ROADMAP](../ROADMAP.md)：FUT-10 選題、相依關係及其他項目方向。
- [總規格](../SPEC.md)、[README](../README.md)：目前能力、認證／工作區共同約定與當次分層證據。
- [第 12 份 FUT-10 固定交接](12-project-rules-loading.md#fut-10-handoff)：八項上游契約的原始擁有者。
- [上游承接對照](#upstream-handoffs)、[前向交接 memo](#future-handoffs)：後續開發及相關 FUT 的固定入口，不另抄一份交接筆記。
