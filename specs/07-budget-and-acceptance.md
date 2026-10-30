# 07：預算控制與整體驗收子規格

## 第 21 份產品接續（2026-10-30）

[唯讀多 Agent 分工](21-multi-agent-delegation.md)明示接續本份：父子生成共用唯一程序／回合預算與用量事件；預留不等於開始，已開始不退款。離線、PTY、真人、三組真實模型服務及 Release／人工分發分列；BUD-S003 門檻保留。 本段為產品增量；下列撰寫當時的階段邊界與歷史驗證保留，不能視為全部人工／服務／發布驗收完成。

## 第 20 份產品實作接續（2026-10-29）

多 profile 共用程序生成上限與消耗，clear／換 Key／切模型均不退款。程序共同顯示生成及傳輸次數，token 分模型／用途列出已知值、缺值與溢位，不換算跨模型費用；B 已加入 BUD-S006：小寫 sk 加連字號後連續至少 16 個 ASCII 字母／數字／底線／連字號，不要求尾界。與原規則同掃 ASCII／UTF-16LE／BE、雙位移及跨塊，無目錄豁免；placeholder 精確追加 YOUR_OPENAI_API_KEY。 詳見[第 20 份規格](20-multi-model-and-provider-support.md)；A、B 分層證據見 [README](../README.md#model-validation)，真實服務／真人與既有 Release 未結門檻另列。

## 第 15 份產品實作接續（2026-10-26）

[工作保存與續接](15-work-save-and-resume.md)的限定增量：保存摘要也消耗共用程序生成額度與 API 可得用量；拒絕確認、取消或未傳輸不退已開始次數。掃描及忽略新增 `.aiagent`／工作暫存／鎖的路徑保護；保留原 BUD-S003 Release 阻擋，離線／原生／PTY／真人／服務／發布證據分列。 本節保留舊階段歷史，實際分層驗證見 [README](../README.md#work-save-validation)。

## 第 14 份產品實作接續（2026-10-26）

[第14份終端機操作改善](14-terminal-interaction.md)只擴充一般ReadInputAsync的隱藏多行／文字元素編輯、F2安全預覽、F3明確貼上、F4換行及程序內安全歷史；Key／認證選單／三種核准仍走原隱藏單行。多行斜線指令在所有解析器之前整筆拒絕，單行精確匹配與Scope／pin字面餘文保持。

CLI輸入狀態加入共同安全清除、認證選單前清除及政策／退出生命週期；不加入Context／摘要／計畫／Git來源或完成證據，沒有新增生成、工具與權限。RULE-HO-006／GIT-HO-006的承接細節見第14份第7、8、9、11、12節與TERM-AC-016～026。原階段歷史保留，當次證據及真人／服務／原生／發布待辦見[README](../README.md#terminal-interaction-validation)。


## 第 13 份產品實作接續（2026-10-26）

[第 13 份 Git 差異與程式碼審查](13-git-diff-and-code-review.md) 新增專用 GitRead 能力與三項唯讀工具，依 Plan／Execute 提供九／十二項；`/review` 為共用 Runner 與生成預算的新入口，該回合只允許三項 Git 工具。原十項工具的順序、輸入與成功資料保持，公開 LLM 契約與 SDK 訊息所有權保持。

Git 來源、覆蓋與報告不建立修改快照、Plan Read requirements 或 Verified／Validation 事實；一般 Git 探索與正式審查分開。僅審目前 Scope 的安全子集；忽略來源的祖先 metadata 例外不放寬一般 `.git`、跨 Scope 讀取或搜尋遇新規則邊界整次拒絕的契約。規則失效、共同重設及已發生操作事實仍依原契約收尾。

實作、當次分層證據與未驗證項目見 [README](../README.md#git-review-validation)。真人、真實 Gemini、第 10 份 test 不支援／未結原生驗收，以及第 07 份 Release／分發門檻分開保留。


## 第 11 份實作接續（2026-10-25）

本份歷史階段限制由 [第 11 份 Context 與預算管理](11-context-and-budget-management.md) 明示擴充：成功歷史以摘要替換最舊完整前綴並保留最新兩回合；生成預設仍每回合五次，新增程序總額度100次，回合／程序可調為1～20／1～1000。自動摘要共用一般回合額度，手動 /compact 另有單次維護統計且計入程序總額度；只有有效 /compact 是新增會生成的本機指令例外。clear、reset、換 Key、模式及計畫變更不退還程序消耗。

PlanStore、原始工具配對與簽章、讀檔快照及逐次核准仍由原邊界管理。20任務／1在途／256事實與計畫容量不放寬；程序仍兩次／300秒、一般失敗即停止，test仍不支援。實際功能、當次證據及未驗證部分見 [README](../README.md#context-budget-validation)，不以本增量解除其他階段門檻。

> 文件版本：0.1｜建立日期：2026-10-22｜狀態：待實作規格
>
> 上位文件：[入門 AI Agent 開發總規格書](../SPEC.md)，主要依據第 16 節及第 6、13～15 章，並承接第 7～12 章的模型、對話、工具與寫入約定。前置規格：[01：專案啟動與 API Key](01-bootstrap-and-key.md)、[02：Gemini 用戶端](02-gemini-client.md)、[03：CLI 與 Context](03-cli-and-context.md)、[04：工作區與唯讀工具](04-workspace-and-read-tools.md)、[05：Agent 工具循環](05-agent-loop.md)、[06：檔案編輯與核准](06-file-edit-and-approval.md)。01～06 已有實作，目前能力以 [README](../README.md) 與原始碼為準；前置文件的「待實作」描述各自撰寫當時狀態。本文件不代表第 07 份產品開發、真人終端機、真實 Gemini 或第三階段整體驗收已完成。

## 1. 目的、交付邊界與完成標準

將各模組已實作的有限資源、用量累計、寫入事實與停止規則，整合成可重現的初版完成門檻；補上開發流程中的 Git 暫存內容與發布產物檢查。此處「預算」指既有請求次數、資料量及輸出限制，不是新增金額扣抵或精確輸入 token 配額。

本次交付只新增本檔；不修改總規格、01～06、README、程式、測試、設定、套件、排除規則或 Git 索引／歷史，不建立額外計畫、掃描腳本、hook 或空實作，不提交、推送或發布。下列契約、命令、測試及畫面都是後續開發要求，不能當成本次執行結果。

需求使用連續的 `BUD-xxx`，驗收使用 `BUD-AC-xxx`；引用上游時保留 BOOT、GEM、CLI、WS、LOOP、EDIT 與既有驗收前綴。文件完成、離線產品完成及真人／服務／發布完成分別判定。

| 編號 | 具體要求 |
|---|---|
| BUD-001 | 沿用 .NET 10、global.json 的 SDK 選擇、Google.GenAI 1.26.1、既有設定模型、v1beta Generate Content、MSTest.Sdk 4.5.1／Microsoft.Testing.Platform。保留 nullable、warnings-as-errors、.editorconfig 與 macOS 驗收邊界；本份不升級 SDK、模型或套件。 |
| BUD-002 | 本次只新增本規格，依第 14 節做文件檢查；不得以規格、歷史測試數或靜態閱讀宣稱產品功能已驗證。 |
| BUD-003 | 不新增精確 token 計數／計數 API、金額預算、價格／免費額度估算、摘要、快取服務、重試、模型切換、串流、搜尋、平行工具、多工作區、批次交易、復原、shell、刪除工具、App 或其他平台承諾。 |
| BUD-004 | 後續離線完成需限制／用量與四工具整合、完整 01～06 回歸、掃描與 hook 的隔離驗收通過。PTY、真人、真實 Gemini 與實際發布檢查另留當次證據；缺任何必要項目時，只報已完成層級，不宣稱第三階段整體完成。 |
| BUD-005 | 公開 LLM 契約、四工具名稱／順序／Schema、產品啟動參數及非機密設定維持既有定義。掃描是開發者命令，不登錄成模型工具、不放進產品啟動或聊天流程，也不把 Git 讀取權加入 WorkspaceFiles。 |

## 2. 既有基線、模組責任與上游交接

### 2.1 實作接續位置

目前原始碼已有 AgentRunner、ConversationContext、UsageAccumulator、四工具 Registry、WorkspaceFiles、逐次核准與安全寫入；README 記錄 01～06 已實作，真人及真實 Gemini 尚未驗證。本次未重新執行產品命令，這些資訊只作接續位置，不作本次測試通過證據。

| 元件 | 本份後續工作 |
|---|---|
| ConversationContext／AgentRunner | 維持完整回合、五次生成與安全停止；整合測試檢查沒有旁路生成、漏配對或失敗重放。 |
| GeminiClient／GeminiTransport | 維持實際 SDK body、輸出 token、期限及用量轉接；使用真實 SDK 配假 handler 驗收。 |
| UsageAccumulator／UsageSnapshot／ChatDisplay | 沿用逐欄小計、缺值、溢位與安全顯示；不另建計費狀態或第二個記帳入口。 |
| Tools／WorkspaceFiles／核准橋接器 | 維持路徑、大小、快照、預覽與發布邊界；確認失敗或取消不抹除已知寫入事實。 |
| 後續 scripts/check_sensitive_content.py | Python 3.9 以上的獨立離線檢查命令，掃描 index 或指定發布目錄；不呼叫產品或模型。 |
| 後續 .githooks/pre-commit | 明確啟用後呼叫 staged 檢查；不負責自動安裝、修改 index、提交或推送。 |
| 後續測試與維護文件 | 補強既有 MSTest、PTY，新增 Python 標準函式庫掃描測試；README 記錄實際能力、命令與未測範圍。本次不建立或修改這些檔案。 |

| 編號 | 具體要求 |
|---|---|
| BUD-006 | 以現有程式與前置子規格核對基線；不把總規格最初「尚無產品」或前置階段的「兩工具／唯讀」當作目前能力。文件中的歷史時態保留，不回頭改寫 01～06。 |
| BUD-007 | 各限制仍由原擁有模組執行，Runner 協調、CLI 顯示，掃描器只管理開發輸入與產物。提示文字、掃描通過或 .gitignore 均不能取代執行期權限／Key／核准檢查。 |
| BUD-008 | 第 3 節整合表引用既有具名常數及契約；不另建立可獨立調寬的 Budget 設定、命令列／環境覆寫或與原常數不同的第二份執行期限制。若實作有缺口，只在原邊界補正並加回歸。 |
| BUD-009 | 07 的增量是整合驗收、開發掃描、選用 hook 與必要維護文件；不得刪除既有安全測試、降低限制、將未測項改為通過，或為掃描新增真實認證來源。跨模組修正須標示受影響需求及既有案例。 |

### 2.2 上游未結責任

| 上游來源 | 本份承接內容 | 本份驗收 |
|---|---|---|
| 總規格第 6、13～16 節；BOOT-027～029 | 憑證不進版本控制／分享產物、排除限制、提交與發布檢查及三階段完成條件。 | BUD-AC-021～044、047～049。 |
| GEM-032～035、GEM-AC-029 | 五欄用量、缺值／溢位、寫入後模型失敗及產物檢查。 | BUD-AC-003～007、016～017、037～040。 |
| CLI-042～047；CLI-AC-033～035 | 程序累計、清除／換 Key、真人終端機與服務證據。 | BUD-AC-003～008、018、045～047。 |
| WS-010～013、036～040；WS-AC-034 | 讀取／結果／快照上限、基準生命週期及整體成本／掃描交接。 | BUD-AC-009～012、015、018～019、021～040。 |
| LOOP-051～056；LOOP-AC-040 | 多次生成逐次記帳、完整請求上限、本機結果與最終驗收。 | BUD-AC-002～007、012、016～018、047～049。 |
| EDIT-005、061～072；EDIT-AC-045 | 四工具、核准、寫入事實、完整限制、真人清單及提交／發布檢查。 | BUD-AC-010～020、021～049。 |

上表是責任映射，不代表所引用上游案例已由本次文件重新執行。後續驗收應保留原案例，新增跨模組證據，不將引用次數當測試數。

## 3. 執行期限制與超限處理

| 限制／既有擁有者 | 固定值與單位 | 邊界及超限結果 | 本份整合案例 |
|---|---|---|---|
| MaxGenerationRequestsPerTurn／AgentRunner | 5 次 GenerateAsync／使用者回合 | 第五次有工具時整批不執行、不核准；停止 GenerationLimit，不送第六次。 | BUD-AC-002。 |
| MaxCompletedTurns／ConversationContext | 最近 5 個完整成功回合，另加當前鏈 | 新成功回合提交後整回合淘汰最舊者；不能中途裁切當前工具鏈。 | BUD-AC-008。 |
| MaxInputCodeUnits／ChatInputReader | 16384 UTF-16 code units | 一般訊息與核准讀取沿用既有無效輸入處理；不截斷後送出或同意。 | BUD-AC-009。 |
| MaxRelativePathCodeUnits／WorkspaceLimits | 原始 path 4096 UTF-16 code units | 正規化前檢查；超限 InvalidPath，不以展開或裁切修正。 | BUD-AC-009。 |
| MaxFileBytes／WorkspaceLimits | 131072 原始 bytes，包含 BOM | 讀檔／完整候選 FileTooLarge；偵測讀取增長最多讀此值加 1 byte，不回傳部分成功。 | BUD-AC-010。 |
| MaxEditArgumentCodeUnits／MaxEditArgumentBytes | content、old_text、new_text 各 131072 UTF-16 單位及各 131072 嚴格 UTF-8 bytes | 任一參數任一尺度超限 EditTooLarge；候選仍另驗證完整檔案大小。 | BUD-AC-010。 |
| MaxListEntries／MaxScannedEntries | 回傳 200 筆／取得 4096 個直接子項目 | 安全項目排序後有限回傳，達掃描上限不取第 4097 筆，Truncated=true；隱藏項目不揭露名稱／數量。 | BUD-AC-011。 |
| MaxToolResultBytes | 完整 Response object 196608 UTF-8 JSON bytes | 包括欄名、path、跳脫及 envelope；超限 ResultTooLarge，不截斷讀檔內容，無新快照。 | BUD-AC-011。 |
| MaxSnapshotCount／MaxSnapshotBytes | 20 份／1048576 原始 bytes | 沿成功讀取順序淘汰；空檔也占一份，查詢不刷新順序，不落地。 | BUD-AC-011、018。 |
| MaxPreviewBytes／PreviewContextLines | 完整預覽與首次提示 65536 UTF-8 bytes／前後各最多 3 行脈絡 | 含跳脫、metadata、行尾標記及提示；超限 PreviewTooLarge，零核准／暫存／發布。 | BUD-AC-010。 |
| 受理中候選暫存／工作區 | 每次寫入最多 1 份同目錄候選暫存 | 只在核准及重驗後建立；不做試寫、備份或碰撞重試。 | BUD-AC-015、019。 |
| MaxRequestBodyBytes／GeminiLimits | 實際 SDK body 262144 UTF-8 JSON bytes | 包含應用指示、四工具、歷史、當前鏈、結果及簽章；262145 為 InputTooLarge、零該次傳輸。 | BUD-AC-012。 |
| MaxOutputTokens／CandidateCount | 8192 output tokens／1 candidate | 由生成設定限制；截斷按 Truncated 處理，不當完整回答提交。 | BUD-AC-001、007。 |
| RequestTimeoutSeconds | 每次生成 120 秒；SDK timeout 120000 ms | 與外部取消串接；不是整回合期限，也不是核准期限，不自動重試。 | BUD-AC-001、007、015。 |
| MaxErrorBodyBytes／GeminiLimits | 65536 bytes 的錯誤 body 分類上限 | 只作既有受控錯誤分類；不得傾印，或為統計額外讀取／重送。 | BUD-AC-001、020。 |

| 編號 | 具體要求 |
|---|---|
| BUD-010 | 五次生成按 GenerateAsync 呼叫計，不是五個工具、五次成功或五次已傳輸；未傳輸的本機 InputTooLarge 亦消耗該次生成機會。第五次最終文字可正常完成，第五次含工具則全部未執行。 |
| BUD-011 | 只保存完整成功回合；第六個回合開始時可帶前五個，提交成功後再淘汰。失敗、拒絕、衝突、未確認及取消的當前鏈不提交，也不藉歷史裁切取得額外生成額度。 |
| BUD-012 | 輸入與 path 依原始 UTF-16 code units 計量，補充平面字元計 2；維持控制字元、代理字元、CRLF 及路徑檢查，不以字數／UTF-8 bytes 代替。 |
| BUD-013 | 原文、各編輯參數及完整候選分別驗證；BOM、中文、多位元組字元與拼接內容皆納入實際尺度。參數合格不保證候選、預覽或模型請求合格。 |
| BUD-014 | 列檔筆數／掃描數與完整工具 JSON 各自限量；read_file 超限直接拒絕，不截斷假成功。SafetyFiltered 與 Truncated 保留原語意，不能洩漏隱藏名稱、目標或數量。 |
| BUD-015 | 快照容量、清除、候選暫存數及完整預覽上限沿既有流程生效；核准不消耗生成或 token，也沒有逾時自動同意。預覽超限不得提供略過選項。 |
| BUD-016 | 每次生成皆由真實 SDK 序列化 body 執行大小及敏感檢查，保留 8192／1／120 秒與錯誤 body 上限。請求超限保留既有本機操作事實；不丟結果、加裁歷史、改簽章、摘要、補生成或重放工具。 |
| BUD-017 | UTF-16 單位、原始 bytes、UTF-8 JSON／顯示 bytes、筆數、回合、秒與 token 均明列；不得相互換算成精確 token 或正式費用。第 6 節掃描器限制只管理開發掃描，不改動本表。 |

## 4. 用量記帳、缺值與顯示契約

### 4.1 責任與生命週期

| 編號 | 具體要求 |
|---|---|
| BUD-018 | 分開保存 GenerationsStarted 與 RequestDispatched 所形成的傳輸嘗試數；不以 HTTP 成功、工具次數、Usage 有無或回答文字推算。啟動、help、clear、換 Key、核准與掃描不產生驗證生成。 |
| BUD-019 | Runner 在每個 GenerateAsync 返回後，對程序與回合 UsageAccumulator 各 Record 一次；ChatApplication、工具、核准或顯示不可再次記同一結果。不保存整份結果列表去重，也不從程序快照相減推算回合用量。 |
| BUD-020 | 僅 RequestDispatched=true 才更新五欄及缺值數。Prompt、Output、Thought、Cached、Total 各自接受非負 long；明確 0 是已知值，null、整份 Usage 缺失或負值是該欄缺值。未傳輸結果即使替身附 Usage 也不記帳、不增加缺值。 |
| BUD-021 | Total 直接採 API 數值，不用 Prompt+Output 補算，不將 Thought／Cached 再加進 Total，也不從其他欄反推缺值。含工具定義或歷史的重複輸入仍依每次實際 API 回報累計，不自行扣除。 |
| BUD-022 | 沿用 UsageFieldSnapshot 的 KnownTotal、HasKnownValue、MissingRequestCount、Overflowed。從未取得值顯示「不可得」；有已知值但缺值時顯示「已知小計 N／不完整（M 次缺值）」；後來取得值不消除先前缺值。 |
| BUD-023 | long 加法及計數溢位保留最後可表示值、永久標記不完整，不回繞、不偽裝成精確飽和值。各欄與傳輸計數獨立，某欄溢位不妨礙其他欄；缺值計數溢位仍可累加可表示小計。 |
| BUD-024 | 每回合重建回合累計；/clear、強制 Context 重設及接受新 Key 都保留程序累計與溢位旗標，退出才丟棄。不將累計落地、跨程序恢復或混稱同一 Google 帳戶／Key 的正式統計。 |
| BUD-025 | 有模型結果的成功、失敗、拒答、截斷、敏感輸出及取消均回報可得用量；依固定順序「輸入／輸出／思考／快取／總計」顯示本次與程序累計，另列生成／傳輸次數。輸出先形成完整安全區塊再遮蔽；本機指令不虛構用量，統計失敗不補請求。 |

RequestDispatched 表示傳輸開始嘗試，不證明服務端收到、成功處理或計費；本機統計不是 Google 正式帳單。即使核准等待本身沒有新用量，前面已送出的模型請求仍照計。

### 4.2 可核對的範例

下表只列 Total；其他四欄獨立計算。這是後續測試資料，不是本次執行輸出。

| 同一回合依序返回的結果 | 生成／傳輸次數 | Total 回合顯示 |
|---|---|---|
| 已傳輸 Total=10；已傳輸 Total=null；未傳輸 InputTooLarge | 3／2 | 已知小計 10／不完整（1 次缺值）。 |
| 已傳輸 Total=0 | 1／1 | 0。 |
| 未傳輸，替身卻附 Total=99 | 1／0 | 不可得，不增加缺值。 |
| 已傳輸 Total=-1；已傳輸 Total=7 | 2／2 | 已知小計 7／不完整（1 次缺值）。 |
| 已傳輸 Total=long.MaxValue；已傳輸 Total=1 | 2／2 | 已知小計 9223372036854775807／不可完整累計（溢位）。 |
| 已傳輸 SensitiveOutput，但 Total=17 | 1／1 | 17，內容被丟棄不等於用量消失。 |

例如第一列在新程序發生時，本機應呈現三次生成、兩次傳輸嘗試及 Total 已知小計 10；不能顯示完整總量 10、三次缺值、三次傳輸或為補統計再請求。

## 5. 四工具整合、本機事實與停止流程

| 編號 | 具體要求 |
|---|---|
| BUD-026 | 正式 Definitions 依序為 list_files、read_file、create_file、replace_text。純文字不強迫工具；列檔／讀檔按需取得資料，建檔直接檢查不存在，替換須先有活動 read_file 快照。不啟動掃描整個工作區、不自動補讀、不解析自由文字為工具或核准。 |
| BUD-027 | 工具結果先於模型最終回答／錯誤顯示；本機 WriteState 與 Status 分離。NotApplied 只描述本次未套用，Verified 須已發布且精確讀回，Unconfirmed 不得聲稱未變更。Response 無法安全建立亦保留安全的 Verified 欄位／CleanupFailed。 |
| BUD-028 | 每筆寫入完整本機預覽後才接受該次 y／yes；同批逐筆核准。明確拒絕立即停止本回合，剩餘未執行，無後續生成或替代提案；已先寫入的其他檔案仍保留 Verified，不把整批說成未變更。 |
| BUD-029 | 候選後衝突、路徑失效、不支援發布、寫入失敗、Unconfirmed 或 CleanupFailed 沿 EDIT 分類停止，不重試或自動換策略。下一回合重新讀檔與預覽，不沿用舊核准／快照；保留已知單檔事實。 |
| BUD-030 | 發布前取消無發布並完成自有暫存收尾；發布已開始後仍做有界讀回與必要清理。Verified 不因取消變 NotApplied，Unconfirmed 不被單純取消蓋掉，CleanupFailed 保留退出 1。停止期間等原操作結束，不平行重開回合。 |
| BUD-031 | /clear、敏感／非法協定重設、換 Key、釋放及退出沿共同清除流程管理 Context、快照、候選、核准與最近安全結果；依原規則保留程序用量及必要的新失敗摘要。認證失敗先在舊政策有效時顯示事實／用量，等待清理才切換 session；不重送原需求。 |
| BUD-032 | 當前 Key 的完整 Ordinal 子字串檢查、結構化敏感欄遮蔽、整批參數預檢、完整候選及顯示邊界仍獨立驗收。模型／檔案的假指令、假差異或假 yes 不改權限；不顯示完整應用指示、thought、簽章、認證、原始例外或請求傾印。 |

正常路徑為一般訊息 → 有限生成 → 按序工具 → 必要時核准與最後重驗 → 本機結果 → 有限生成／收尾 → 完整回合提交。工具配對、PartIndex、可空 CallId 及原始 SDK 紀錄保持 02／05 的契約。

若已核准寫入 A，再拒絕 B，應先回報 A 已寫入並核對、B 未套用，再停止；若寫入 A 後模型網路或認證失敗，A 仍為本機成功，模型狀態另列。取消、clear 或換 Key 均不回復已寫 bytes。

## 6. 開發掃描命令、輸出與資源契約

### 6.1 入口

後續唯一命令入口為 scripts/check_sensitive_content.py，只有下列模式；下列檔案尚未由本次建立：

```sh
python3 scripts/check_sensitive_content.py staged
python3 scripts/check_sensitive_content.py artifacts --path "/example/new-publish-directory"
```

staged 以目前 Git 工作目錄定位儲存庫並檢查有效 index；artifacts 的 path 可為目前目錄相對或絕對的既有發布目錄。此 path 是開發命令參數，與產品工具禁止絕對路徑的規則屬不同介面，不得拿來放寬產品工具。

| 編號 | 具體要求 |
|---|---|
| BUD-033 | 嚴格解析 staged、artifacts --path 及單獨 --help；未知／重複／缺少參數退出 2。--help 固定說明後退出 0，不讀 index、產物或憑證。沒有 key、token、allow、ignore、force 或修復參數，沒有自動刪除／遮蔽檔案模式。 |
| BUD-034 | 掃描使用 Python 3.9 以上及標準函式庫，Git 需支援本份使用的唯讀選項；不自動安裝依賴、使用第三方秘密服務、呼叫網路或產品／模型，也不取得當前執行期 Key。與產品 SDK 及測試框架分離。 |
| BUD-035 | 正常掃描 stdout 只用下表固定 ASCII 格式；不輸出 path、檔名、OID、命中值、片段、行內容、環境、traceback 或子程序 stderr。item 是本次穩定排序的六位序號，非內容指紋；同一項目同一規則最多報一次。所有錯誤先映射固定碼，退出碼依下表，不能以吞例外或略過檔案取得 0。 |
| BUD-036 | 使用第 6.2 節具名上限、有界列舉／管線及分塊比對；達限或無法完整處理為 incomplete／2，不裁切後當通過。完整讀完且穩定才算 scanned_files；停止時不宣稱未觀察部分安全，取消／輸出失敗亦非 0。 |
| BUD-037 | staged 通過只覆蓋當時 index；artifacts 通過只覆蓋當時指定目錄。未暫存修改、未追蹤檔案、截圖／附件及後續改動仍須各自人工檢查；不自動加入、提交、清理或發布。 |

| 輸出種類 | 固定格式／退出碼 |
|---|---|
| 規則命中行 | `[finding] item=000001 rule=BUD-S001`；item 依排序編號，rule 使用第 7 節固定碼。 |
| 無命中且完整 | `[scan] mode=staged status=passed scanned_files=12 findings=0`；mode 亦可為 artifacts，數字依實際結果，退出 0。 |
| 有命中且完整 | 相同摘要，status=blocked、findings 為不同 item／rule 組合數，退出 1。 |
| 未完整 | `[error] code=scan_changed`，之後 status=incomplete 的摘要，退出 2；即使已有命中仍為 2，不冒稱完整掃描。 |

固定錯誤碼為 invalid_arguments、dependency_unavailable、git_unavailable、index_invalid、unsupported_entry、artifact_invalid、scan_limit、scan_io、scan_changed、scan_cancelled、output_unavailable、internal_failure。允許在 error 行增加安全 item 序號，不增加自由文字。正常診斷統一寫 stdout，stderr 不轉送原始資料；若 stdout 已失效，只退出非零，不為補印錯誤再次洩漏內容。Python 未能啟動等外層失敗可能無此摘要，但必須非零。

輸出範例均為格式示意，不是本次掃描結果。使用者可在本機依同一排序查找項目；不得為方便定位而把含秘密的檔名／Git 輸出貼到報告。序號在下一次內容清單改變後不保證仍指同一項目。

### 6.2 掃描器專用限制

| 名稱 | 固定值 | 計量方式 |
|---|---:|---|
| MaxScanEntries | 10000 個 | staged 的 index entries；artifacts 的所有子目錄與項目，根本身不算。必要時最多再觀察一筆以判超限，不繼續收集。 |
| MaxScanFileBytes | 67108864 bytes | 每個 blob／普通檔案 64 MiB；超限不略過，至多讀上限加 1 byte 偵測增長。 |
| MaxScanTotalBytes | 1073741824 bytes | 每個路徑內容計入一次，總計最多 1 GiB；同 OID／硬連結不同名稱亦各計，不用去重繞過。 |
| MaxScanPathBytes／MaxScanDepth | 4096 bytes／64 層 | 原始儲存庫／發布目錄相對名稱的 OS bytes，斜線分段計層；不是產品 path 的 UTF-16 限制。 |
| MaxExampleFileBytes／MaxExampleLineBytes／MaxExampleAssignments | 131072 bytes／4096 bytes／200 筆 | 只限 staged 的 .env.example 特例；檔案含 BOM、實體行含行尾，賦值筆數含後續會判無效者。超限 incomplete／2，不為語法判斷收集無界整行。 |
| ScanChunkBytes／ScanOverlapBytes | 65536／256 bytes | 比對 ASCII、UTF-16LE／BE 固定特徵；保留跨塊尾端，含第二種對齊，不能漏跨塊特徵。 |
| GitCommandTimeoutSeconds／MaxGitDiagnosticBytes | 30 秒／65536 bytes | 每次 Git 子程序的期限及可捕捉診斷上限；持續排空並丟棄不安全診斷，不能因 stderr 管線滿而卡住。 |

路徑清單及 item／rule 結果亦受 MaxScanEntries 約束；比對只保存所需區塊、有限名稱／狀態與命中布林，不累積全文。總量先驗證已知長度，再逐塊核對實際 bytes；錯誤、管線中止或宣告長度不符均不可 0。不得把掃描限制套到產品讀檔，亦不得把產品 128 KiB 上限拿來跳過 DLL。

## 7. 固定偵測規則、佔位值與限制

### 7.1 路徑與內容規則

下表是固定的保守檢查，不是 Google Key 有效性驗證或通用秘密分類器。路徑以原始相對名稱的完整區段／檔名比對，ASCII A～Z 轉小寫後比較固定清單；不做 Unicode 正規化、URL decode、環境展開或子字串猜測。staged 處理非 UTF-8 名稱時保留原 bytes，ASCII 規則仍生效，診斷不解碼回顯。

| 規則碼 | 精確偵測範圍 | 例外／處理 |
|---|---|---|
| BUD-S001 | 任一區段等於 .git、.agents、.codex、.aws、.ssh、.secrets、secrets、logs；檔名為 .env 或以 .env. 起始；gemini-api-key.txt、api-key.txt、appsettings.Local.json、credentials、credentials.json、id_rsa、id_dsa、id_ecdsa、id_ed25519、identity；檔名後綴 .secrets.json、.pem、.key、.p12、.pfx、.ppk、.keystore、.jks、.log、.dmp、.dump。staged 另拒絕 bin、obj 區段。 | 只對完全小寫拼字 .env.example 依第 7.2 節作例外；其他受限區段／後綴仍優先。artifacts 的根是產物範圍，不因根的外部祖先名為 bin 而拒絕；其內敏感區段仍檢查。 |
| BUD-S002 | ASCII bytes 正規表示式 `AIza[A-Za-z0-9_-]{35}`；不要求字串邊界，嵌在更長文字亦命中。 | 不連服務驗證，不因附註「fake」或位於 tests／docs 而放行；只代表此種可辨識外形，不涵蓋所有新式或其他供應商 Key。 |
| BUD-S003 | ASCII bytes 正規表示式 `-{5}BEGIN (?:RSA \|DSA \|EC \|ENCRYPTED \|OPENSSH )?PRIVATE KEY-{5}`。 | 涵蓋列出的 PEM／OpenSSH 私鑰起始標記；不是完整金鑰解析，不因內容自稱測資而免檢。公開憑證可能仍被敏感副檔名保守阻擋。 |
| BUD-S004 | 任一檔名符合 `.aiagent-edit-` 加 32 個十六進位字元再加 `.tmp`，依上述 ASCII 大小寫規則。 | 即使空檔或是使用者同名檔也阻擋；只提示可能殘留，不掃描刪除、不判定所有權。 |
| BUD-S005 | .env.example 不符合第 7.2 節限定的文字／賦值格式或值清單。 | staged 的範例特例專用；artifacts 不應攜帶 .env.example，仍以 BUD-S001 阻擋。 |

| 編號 | 具體要求 |
|---|---|
| BUD-038 | 固定路徑拒絕依 BUD-S001／004；Git 忽略、檔案是否空白、已追蹤或強制加入不構成例外。只檢查掃描範圍內的相對名稱，不將 Git 自身 metadata 或產物根外部祖先列為待掃檔案。 |
| BUD-039 | BUD-S002 精確依表內外形掃描，不宣稱 Google 已確認真偽，不查詢網路、不計算熵或任意新增其他服務 Key 規則。誤判須檢視內容／改寫測資，不能用整個目錄忽略解決。 |
| BUD-040 | BUD-S003 依固定私鑰起始標記比對；正常程式碼、規格與測試內也適用。說明偵測器時使用正規表示式或分段敘述，不在版本控制中存放完整可命中的假私鑰標記。 |
| BUD-041 | BUD-S002／003 對普通檔案與二進位原始 bytes 均檢查 ASCII 及每個 ASCII 字元間帶零的 UTF-16LE／BE 形式；支援兩種位移、BOM 存在／不存在及跨塊，不因 UTF-8 解碼失敗或 NUL 就跳過。只匹配原始連續形式，不解壓、反混淆、拼接不同檔案或任意解碼。 |
| BUD-042 | .env.example 只在 staged 容許第 7.2 節格式；仍做其他路徑及全部內容規則，不藉大小寫、外層敏感目錄或字元編碼繞過。此例外不改變 WorkspaceFiles 對 .env.example 的 SensitivePath。 |
| BUD-043 | 全專案沒有 tests／docs／vendor 一般豁免、可配置 allowlist 或行內忽略註解。非敏感一般檔案可用明確佔位文字；若測試要命中外形／標記，於隔離暫存資料中分段組合，不將完整合成命中值寫進來源／報告。佔位值不能消除同檔其他命中。 |

### 7.2 .env.example 的唯一提交特例

允許嚴格 UTF-8，可有一份檔頭 UTF-8 BOM；拒絕非法編碼、NUL、非 Tab／CR／LF 的控制字元及 Unicode 行／段分隔字元。逐行容許空白、首個非空白字元為 `#` 的註解，或 `NAME=VALUE`；NAME 僅接受 `[A-Za-z_][A-Za-z0-9_]*`，不支援 export、插值、跳脫、續行、多行值或行尾註解。

以有界增量 UTF-8 解碼及行緩衝解析，實體行尾為 CRLF、CR 或 LF，末行可無行尾；遵守 MaxExampleFileBytes／MaxExampleLineBytes／MaxExampleAssignments，超限依 scan_limit／2，與格式不符的 BUD-S005／1 分開。表內正規表示式的直線是 alternation；Markdown 的跳脫不屬於實際比對式。

NAME 與 VALUE 兩端只去除 ASCII 空白與 Tab；VALUE 可去除一對相同單／雙引號，之後必須完全等於空字串、YOUR_GEMINI_API_KEY、YOUR_API_KEY、YOUR_ACCESS_TOKEN、YOUR_CLIENT_SECRET、EXAMPLE_ONLY_NOT_A_SECRET 或 [REDACTED]，採大小寫敏感比對。重複 NAME 拒絕。任何其他值即 BUD-S005，不猜測網址、數字或看似假的值是否安全。

以下為可提交的格式示意，不是產品認證來源，也不由本次建立此檔：

```text
# 僅供文件示範，產品不讀環境變數
GEMINI_API_KEY=YOUR_GEMINI_API_KEY
```

### 7.3 明確不保證的範圍

固定規則無法識別所有任意短 Key、其他憑證、經編碼／加密／分割的秘密、圖片中的文字或壓縮內容；也無法判斷普通業務文字是否機密。執行期當前 Key 精確比對及已知欄位遮蔽仍由原模組負責，不能因開發掃描更保守便擴張其承諾。系統／應用指示不得放到報告，掃描器也不靠重印它來證明安全。

## 8. staged：完整 Git index 的實際內容

### 8.1 讀取流程

1. 從原呼叫工作目錄以唯讀 Git 命令定位工作樹根，並由 `git rev-parse --git-path index` 取得有效 index 位置。先依原工作目錄固定成絕對 index 路徑，再以該工作樹根作後續子程序的工作目錄，取得 `git ls-files --full-name --stage -z`；GIT_INDEX_FILE 必須仍指同一有效 index，包含 hook 的暫時 index，不硬編碼 .git/index。不可直接從巢狀呼叫目錄列檔而只掃其子樹。
2. 以 NUL 切記錄，只解析 metadata 的固定分隔，不按路徑內空白、Tab 或換行切檔名。保存每筆 mode、完整 OID、stage 與原始 path bytes，按 path bytes 的序列排序建立 item。
3. 只接受 stage=0、mode=100644／100755 的普通 blob；檢查名稱與資源上限。未合併 stage、符號連結 mode=120000、gitlink=160000 或其他不支援項目為 incomplete，不跟隨或掃描外部目標。
4. 按 OID 讀 `git cat-file` 的原始 blob 型別、大小及 bytes；可使用有界 batch 管線，但必須依宣告長度讀取並驗證分隔／結束。先驗長度再讀取，不使用 textconv、filters、外部 diff 或 path 展開。
5. 完整掃描各 blob 的規則後，再取一次相同 index entries，逐筆比較 mode／OID／stage／path。不同則 scan_changed，不自動重掃或取得新快照補成通過。
6. 所有內容及前後清單一致後，才輸出 passed／blocked；關閉並等待所有子程序。空 index 可完整通過 scanned_files=0，不能因此聲稱工作目錄／歷史安全。

| 編號 | 具體要求 |
|---|---|
| BUD-044 | 掃描完整有效 index，而非僅 staged diff、新增行、變動檔案或呼叫目錄子樹；涵蓋普通已追蹤檔案及強制加入。使用上述根目錄／有效 index／NUL／metadata／OID 流程，尚無 HEAD 及 linked worktree 亦可檢查。 |
| BUD-045 | 內容來自 index 指向的原始 blob，不能用工作樹同名檔、HEAD 或套過 filter 的內容替代。部分暫存時檢查準備提交的版本，工作樹乾淨版不能掩蓋已暫存命中。 |
| BUD-046 | 刪除後不在 index 的檔案不重讀舊內容，重新命名依新 path／blob 檢查；同 OID 不同 path 各驗路徑。非 UTF-8 名稱、空白、Tab、換行、前導 -／: 及路徑中類似 shell 文字不能成為參數注入或漏檔。 |
| BUD-047 | Git 子程序用 argument array、shell=false、受控工作目錄；不寫 index／config／history，不啟動 filter、hook、pager、credential helper 或 lazy fetch。子程序環境關閉 GIT_TRACE*，設 GIT_NO_LAZY_FETCH=1、GIT_NO_REPLACE_OBJECTS=1、GIT_OPTIONAL_LOCKS=0；不列印環境，保留必要 Git index 定位。缺 blob、命令失敗／不支援、逾時、診斷超限均 incomplete，不下載或重試。 |
| BUD-048 | 前後 entries 完全相同才可通過；掃描不鎖住使用者後續編輯／提交，也不保證偵測中途變更再還原。檢查只對本次觀察到的 index snapshot 有效，之後 add／改 index 須重新掃描。 |
| BUD-049 | 未合併、連結、submodule、非 blob、不完整／重複／無法解析的 index 記錄與超限均受控 incomplete／2，不跳過後報 passed；已知命中仍可列安全規則碼，但不改為「完整阻擋」摘要。 |
| BUD-050 | Git 自身排除不參與 staged 的准入；不用 .gitignore 跳過已追蹤檔案。未暫存修改與未追蹤檔案不自動讀取，仍依 BOOT-028 人工檢查；staged 不掃 Git 歷史或自動解除追蹤。 |

部分暫存的反向案例亦須成立：若 index 安全、工作樹有合成命中，staged 可通過該 index，但不能宣稱工作樹安全。此結果用來證明掃描對象精確，不是允許將可疑工作樹直接分享。

## 9. artifacts：發布目錄及分發門檻

本節「發布產物」指 dotnet publish 產生的本機目錄；「對外發布」指分享、封裝或上傳。第 06 份的檔案名稱發布／rename 是另一個概念。產生本機 publish 輸出尚不表示允許對外發布。

| 編號 | 具體要求 |
|---|---|
| BUD-051 | artifacts 接受既有、可列舉的目錄；根、指定路徑沿途及子項目不跟隨符號連結。固定所選根，僅在其內列舉／讀取，拒絕特殊檔案，不因 .gitignore 或隱藏屬性略過。路徑錯誤／不可讀／不支援為 artifact_invalid 或 scan_io／2，不輸出原 path。 |
| BUD-052 | 按原始相對 path bytes 排序編號，檢查所有普通檔案的名稱及實際 bytes，包含 DLL、PDB、設定、相依套件與非 UTF-8 檔案。不因副檔名或供應商跳過內容；受限目錄先記規則命中再停止進入，其內容未掃完整時結果為 incomplete，不能當完整 passed。 |
| BUD-053 | 目錄列舉前後記錄／核對根與子目錄身分及項目集合；每個檔案檢查開啟前、handle、讀完及結束清單的 device／identity、普通種類、size、mtime_ns、ctime_ns。變動／連結／消失或讀取長度不符為 scan_changed，不重讀補成功；這是應用層檢查，不能宣稱抵抗所有惡意競態。 |
| BUD-054 | 發布驗收以 framework-dependent、非單檔的既有 .NET 10 CLI 目錄為基準；根至少具有普通檔案 AIAgent.Cli.dll、AIAgent.Cli.deps.json、AIAgent.Cli.runtimeconfig.json、appsettings.json。缺漏或空目錄為 artifact_invalid／2；相依項全部照掃，必要檔存在不保證完整依賴或功能正確。 |
| BUD-055 | 初版不解壓或 OCR；產物內檔名後綴 .zip、.tar、.gz、.tgz、.7z、.rar、.dmg、.pkg 為 unsupported_entry／2，不當成已檢查其內部。其他二進位只保證第 7 節連續特徵；重新命名的容器、嵌入壓縮、圖片及混淆不在保證內，封裝後不得沿用目錄通過宣稱已掃包內一切。 |
| BUD-056 | 每次產物生成使用新的隔離輸出目錄，掃描通過且人工發布檢查完成前不得對外分享。產物有任何改動即重新驗收；失敗只停止分發、不修改或刪除內容。掃描器不包裝、簽章、上傳、產生清除命令或替使用者按發布。 |

已知受限目錄、連結、壓縮檔或其他無法完整掃描的產物均不能發布；不以「已發現一個問題」掩蓋其餘未檢查內容。純敏感檔名的普通檔案仍在資源許可下掃完內容，供完整 blocked 結果；敏感目錄不進入以免擴大收集其內容，摘要為 incomplete。這個差異須在測試中固定。

## 10. 選用 pre-commit hook 與非零阻擋

| 編號 | 具體要求 |
|---|---|
| BUD-057 | 後續提供具可執行 mode 的 .githooks/pre-commit，以 POSIX shell 包裝獨立 staged 命令；正確引用儲存庫根與程式路徑，不 eval、不傳 path 清單、不重設 GIT_INDEX_FILE。只做掃描，不改 index、格式、Git 設定或產品檔案。 |
| BUD-058 | 採使用者明確啟用，產品啟動、build、restore、測試或掃描均不自動安裝。啟用前檢查有效 core.hooksPath 及既有 pre-commit；已有設定／hook 時保留，說明人工整合既有 hook 呼叫，不覆蓋或自動改設。只有確認無既有自訂 hook 時，才由使用者執行下列本機設定。 |
| BUD-059 | hook 只有掃描完整 passed／0 才回傳 0；blocked／1、incomplete／2、Python／腳本缺失或執行失敗皆非零，使該次正常 git commit 中止。hook 不回顯原始 stderr；依賴無法啟動時使用固定安全分類。 |
| BUD-060 | 說明 hook 可被 --no-verify、變更設定、未安裝或其他 Git 路徑繞過，並非遠端／不可繞過的防護。未啟用者仍須直接跑命令及人工檢查；本份不加 CI、遠端阻擋、pre-push、自動 commit／push 或自動修改他人 hook。 |

以下是後續人工選用方式，本次不執行；必須先確認沒有上述衝突：

```sh
git config --local core.hooksPath .githooks
```

停用時只有在本機設定確由這次選用建立且仍為 .githooks，才移除該設定；若已有其他設定須由使用者還原原值，不提供盲目 unset／覆寫程序。hook 與獨立命令都只對本次有效 index 作檢查，無法補救已提交／已分享的內容。

## 11. 憑證、輸出、分享與失敗處置

| 編號 | 具體要求 |
|---|---|
| BUD-061 | Key 仍只由產品隱藏輸入取得及用於官方 HTTPS 認證。掃描不要求 Key、不將檔案中的值當認證、不讀 OS 憑證或發送掃描內容；結果／測試斷言只用案例 ID、受控碼、數值與布林。產品輸出與掃描診斷均不公開秘密、完整應用指示或原始資料。 |
| BUD-062 | 發現可疑內容先停止提交／分享／分發，由使用者在本機檢視；若疑似真實憑證洩漏，先撤銷／更換並檢查用量，再另行確認歷史與副本處置。掃描器不自動修改、解除追蹤、改寫歷史或宣稱已清除所有副本。 |
| BUD-063 | 人工檢查涵蓋未追蹤／未暫存內容、範例、README、Issue／PR、截圖、錄影、報告、附件及最終分發物；自動通過只表示固定規則在指定快照未命中，不證明沒有其他秘密、模型回答正確、真實 API 可用或作業系統沙盒安全。 |
| BUD-064 | 掃描完全唯讀，不建立內容暫存、日誌、快照匯出、備份或清理資料。產品第 06 份自有候選暫存仍按其單一路徑規則收尾；掃到殘留也不刪除。測試資料保留，若需批次清理交由使用者手動處理，不使用遞迴／批次刪除。 |

## 12. 需求對照與後續產品驗收

### 12.1 方法與證據

| 編號 | 具體要求 |
|---|---|
| BUD-065 | 自動驗收全程離線，以假 Key、假 HTTP handler、可控制核准／檔案系統及隔離工作區執行。Git 案例另用暫存儲存庫、合成身分與本機設定，不改本專案 index／history／hooks，不讀真實憑證、不使用實際網路或真實磁碟故障。 |
| BUD-066 | 分別記錄離線 MSTest、Python 掃描測試、PTY、真人終端機、真實 Gemini、發布輸出檢查的實際命令、退出碼、案例／通過／失敗／略過數與平台。macOS arm64／x64 依實測分列，不用其中一種或交叉建置冒稱兩者實際通過。 |
| BUD-067 | C# 以 BUDAC 前綴 acceptance-linked MSTest 補強跨模組回歸；Python 使用 unittest 及 subprocess 驗證實際 CLI／Git／hook。上限、故障與時序可用可控制 stream／process／filesystem 測試，另有真實小型 Git／發布目錄流程；不只 mock 掃描成功，也不為測 1 GiB 而必須寫滿磁碟。 |
| BUD-068 | 所有 BUD 需求至少有一項下列案例對照；引用上游案例只承接責任，不把既有案例、子情境或文件檢查重複算成新通過數。故障測試以呼叫數、bytes、狀態及副作用判斷，不以模型含「成功」或固定摘要文字單獨判定。 |

### 12.2 離線產品與掃描驗收

下列 BUD-AC-001～044 為後續離線門檻，每列可有多個具名測試；報告列實際測試數，不能直接把表格列數稱為通過數。

| 案例 | 對應需求 | 操作與成功條件 |
|---|---|---|
| BUD-AC-001 | BUD-001、005～009、016～018、065、067 | 核對版本／設定／四工具與既有常數；真實 SDK 假 handler 的請求帶 8192、1 candidate 及既有期限，無自動 function invocation／重試。啟動、help、clear、換 Key 與掃描沒有模型呼叫；既有錯誤 body 邊界受控。 |
| BUD-AC-002 | BUD-010、018、026、028、068 | 前四次含工具、第五次最終回答可提交；另一流程第五次仍要求 read／create／replace，整批零 I/O／核准／暫存，無第六次。先前已完成事實保留，不把五次生成當五次工具。 |
| BUD-AC-003 | BUD-019～022、025 | 三次生成序列 Total=10、缺值、未傳輸 InputTooLarge：生成 3／傳輸 2、已知小計 10／一次缺值；程序與回合各只記一次，其他四欄各自核對。 |
| BUD-AC-004 | BUD-020～022、025 | 明確 0、null、整份缺失、負值及未傳輸但附假用量；0 保留、缺值逐欄累計、未傳輸不計，無請求不得顯示完整零 token。 |
| BUD-AC-005 | BUD-021～023 | Prompt／Output／Thought／Cached／Total 故意不相加，仍各採 API 值；大於 int.MaxValue 與 long 溢位保持精確可表示小計及旗標，缺值計數／傳輸溢位不妨礙其他欄。 |
| BUD-AC-006 | BUD-019、024～025、031 | 混合成功／缺值／溢位後 clear、強制重設及換 Key：程序累計保留，新回合獨立；不以程序差值計回合、不把舊用量送模型、不落地。 |
| BUD-AC-007 | BUD-016、018～025 | 失敗、拒答、空回應、Truncated、SensitiveOutput、Timeout、Cancelled 各有可得或缺失用量；已傳輸照記、未知不猜，固定安全顯示，無補生成或自動重試。 |
| BUD-AC-008 | BUD-011、024、026、031 | 純文字及多步／寫入工具回合超過五回合，第六次帶完整前五回合、成功後整回合淘汰；失敗鏈不提交。clear／取消不復原已寫 bytes。 |
| BUD-AC-009 | BUD-012、017 | 一般輸入 16383／16384／16385 UTF-16 單位，path 4095／4096／4097，含補充平面及控制字元；依原邊界接受／拒絕，零超限傳輸或 I/O。 |
| BUD-AC-010 | BUD-013、015、017、028 | 原始／候選 131071／131072／131073 bytes、各編輯參數兩種尺度、BOM／中文、完整預覽 65536／65537 bytes 與跳脫膨脹；合格不代表其他尺度合格，超限零核准／暫存／發布，保留完整差異及三行脈絡。 |
| BUD-AC-011 | BUD-014～015、017 | 回傳 199／200／201、安全與隱藏項目混合、掃描 4095／4096／更多，結果 JSON 196608／196609；快照 20／21 份及 1048576 bytes 邊界：截斷、拒絕、淘汰及無新快照正確，無隱藏數量洩漏。 |
| BUD-AC-012 | BUD-008、016～017、027 | 真實 SDK＋假 handler＋真實工作區結果，完整 body 精確 262144／262145 bytes，含四工具、中文／跳脫、歷史及簽章；前者送出，後者零當次傳輸且保留既有 Verified，不裁切／補送。 |
| BUD-AC-013 | BUD-005、026、032、065、068 | 真實 ChatApplication＋SDK 假 handler：純聊天一次生成、零工作區 I/O；列檔→讀檔→回答按真實結果／順序完成，無額外掃描或 shell，自由文字不能觸發操作。 |
| BUD-AC-014 | BUD-026～028、032、065、068 | 真實 create→核准→讀回→回答，及 read→唯一 replace→核准→回答；檢查 bytes、BOM／行尾、mode、配對／簽章、生成／傳輸與用量各計一次，結果來自磁碟而非假成功 ToolExecutionResult。 |
| BUD-AC-015 | BUD-015、028～030 | 同批逐次核准，第二筆 n／no／空白後立即停止；無效選擇不生成，預輸入／殘餘 CRLF 不成同意。發布前 Ctrl+C／EOF 零發布、必要清理；沒有整批同意或核准逾時自動同意。 |
| BUD-AC-016 | BUD-027～030、032 | 已寫 A 後拒絕 B、外部改檔、根失效、發布失敗／讀回不符／CleanupFailed；分別保持 NotApplied／Verified／Unconfirmed，剩餘未執行，無自動重放／復原，清理失敗退出 1。 |
| BUD-AC-017 | BUD-016、019、025、027、029～031 | Verified 後下一次網路／配額／逾時／認證／InputTooLarge 或最終提交取消：本機已寫事實仍在、模型錯誤分列、無重寫，當前鏈不提交、已返回用量只計一次。 |
| BUD-AC-018 | BUD-015、024、030～031 | clear、強制重設、快照淘汰、換 Key 及釋放使相應快照／候選／核准失效；發布後取消仍完成查核。舊服務收尾後才釋放政策，新 session 不續用舊資料，程序累計保留。 |
| BUD-AC-019 | BUD-007、015、029～030、064 | 真實／可控制工作區驗證暫存只在核准後一份、精確持有及單一路徑清理；殘留／清理失敗不批次刪除、不啟動時掃除。掃描器前後項目及 bytes 不變。 |
| BUD-AC-020 | BUD-007、009、016、025、032、061、063 | 假當前 Key 混入輸入、歷史、SDK 回應、工具欄名／值、完整候選拼接及顯示；敏感原值不出邊界、不先進預覽／暫存，短 Key 固定標籤碰撞安全停止；假指令／ANSI／假 yes 不改權限，數值用量及安全寫入事實保留。 |
| BUD-AC-021 | BUD-033～035、037、065、067 | 掃描 CLI 兩模式／help；未知、缺值、重複參數、非儲存庫及依賴不可用：退出及固定摘要正確，無 Key／網路／產品啟動、零 index／工作樹變更。 |
| BUD-AC-022 | BUD-036、038、044、050 | 隔離 Git 中根／巢狀敏感名稱、一般原始碼／文件／測試、強制加入與已追蹤命中；空 index 可通過，已追蹤檔不因忽略而跳過，完整 index 皆掃。 |
| BUD-AC-023 | BUD-035、044～045、048 | index 含合成命中而工作樹已移除，仍 blocked；反向 index 安全而工作樹有合成命中，只對 index passed。另驗部分暫存、未變動但已追蹤檔及無 HEAD。 |
| BUD-AC-024 | BUD-038、044～046 | staged 刪除、重新命名、同 OID 不同 path，刪除不重讀舊內容，新名稱受限仍擋；可執行 mode 普通檔也掃，不靠 rename detection 或 HEAD 推算。 |
| BUD-AC-025 | BUD-035、044、046、049 | 空白、Tab、換行、前導 -／:、非 UTF-8 名稱與類似命令替換文字：NUL 解析正確、argument array 不執行文字、診斷無原始名稱。 |
| BUD-AC-026 | BUD-035、044～045、047、049 | 未合併、symlink、gitlink、不完整 metadata、非 blob、缺失物件、Git 非零／逾時／過量 stderr：incomplete／2，無外部目標讀取、filter、網路或原始錯誤洩漏。 |
| BUD-AC-027 | BUD-036、044、047～048 | 可控地在兩次 entries 間換 OID／mode／path：scan_changed／2、不重掃；從巢狀目錄及 linked worktree 呼叫仍涵蓋整個有效 index，相對／絕對 GIT_INDEX_FILE（含 hook 暫時 index）保持同一檔案。trace 關閉、replace refs 不替換實際 blob、缺 promisor 物件不 fetch。 |
| BUD-AC-028 | BUD-038、042～043、050 | .gitignore 根／巢狀排除、.env.example 可追蹤例外、已追蹤及強制加入限制於暫存 Git 驗證；工具仍拒絕 .env.example，scanner 不把可追蹤誤認為工具授權。 |
| BUD-AC-029 | BUD-038～041、043 | 合成 Key 外形及列出的每種私鑰標記，置於原始碼、文件、二進位及較長文字；ASCII／UTF-16LE／BE、兩種位移、BOM、跨 65536-byte 區塊皆命中。非 UTF-8／NUL 不跳過，近似但不符規則可不命中且不宣稱通用偵測。 |
| BUD-AC-030 | BUD-038、042～043 | .env.example 的空白／註解、BOM、引號、空值與每個精確佔位值可接受；其他值、大小寫變體、export、重複 NAME、非法編碼／控制、多行或外層敏感目錄拒絕。任一其他規則命中仍擋，tests／docs 註解不豁免。 |
| BUD-AC-031 | BUD-035、038、043、061、064 | 空或非空 .aiagent-edit-32位hex.tmp、一般相似名稱及大小寫；精確規則命中、相似不誤當刪除目標，零刪除／清理。來源只放分段生成器，測試輸出不帶合成命中原值。 |
| BUD-AC-032 | BUD-035～036、042、067 | 掃描各容量 L-1／L／L+1、深度／path bytes、總量、.env.example 檔案／行／賦值上限及同內容多路徑；合法邊界完整，超限 incomplete，不無限緩衝、不截斷假通過。大值用可控 stream，另驗真實小型 CLI 資料；單一 item／rule 不因跨塊重複報告。 |
| BUD-AC-033 | BUD-035～036、061、064 | 讀取／列舉失敗、KeyboardInterrupt、stdout 寫失敗及未預期例外；固定錯誤與非零退出，無 traceback／path／OID／命中片段／stderr／內容落地，資源關閉，原檔 bytes 不變。 |
| BUD-AC-034 | BUD-034～038、051～052、054、056 | 真實隔離的最小發布目錄加一般 DLL／PDB／非 UTF-8 合成檔，所有普通檔照掃，不因 .gitignore／隱藏檔或相依套件略過。空目錄／必要檔缺失 incomplete，不把必要檔存在當功能通過。 |
| BUD-AC-035 | BUD-036、038～041、051～052、055 | 產物根內敏感名稱、私鑰／Key 合成特徵、內部暫存、連結／特殊項目及壓縮後綴：普通敏感檔完整 blocked，不進入敏感目錄／連結／解壓，未完整部分為 2，絕不 passed。 |
| BUD-AC-036 | BUD-036、048、051、053、063 | 可控制根／父路徑／子目錄／檔案換身分、長度／時間／集合變動、連結替換與讀取中增長：scan_changed／2，未讀範圍不發布為安全；不重試，不宣稱全部 TOCTOU 已消除。 |
| BUD-AC-037 | BUD-037、050、054～056、063 | 本機產物掃描 passed 後改檔或另封裝，舊結果失效；人工發布清單仍待完成。掃描器不封裝／簽章／上傳，未追蹤檔案／歷史／附件未被誤計為已掃。 |
| BUD-AC-038 | BUD-005、033～034、057～058、060 | hook 未啟用時產品／build／測試／掃描不自動安裝；已有 core.hooksPath 或 pre-commit 保留不覆蓋，說明人工整合。hook 已提供可執行 mode，僅在暫存 Git 測試明確啟用。 |
| BUD-AC-039 | BUD-044～048、057、059～060、065、067 | 隔離 Git 中實際 git commit：安全 index 建立提交；命中／掃描未完成／缺 Python 或腳本時非零且不建立提交。命令保留有效 index、正確引用路徑，不擅自修改其他設定／內容。 |
| BUD-AC-040 | BUD-004、037、056、060、063 | 文件清楚區分獨立命令、選用 hook 與發布門檻；繞過 hook 不等於掃描通過。無 CI／pre-push／遠端保證；不能拿 staged 結果覆蓋產物或包裝後內容。 |
| BUD-AC-041 | BUD-061～064、065、068 | 假資料故障與可疑內容處置清單不收集真實 Key、不自動撤銷／改寫歷史、不輸出秘密或完整應用指示、不批次刪除。報告只有受控分類與數值，不以掃描成功取代人工分享檢查。 |
| BUD-AC-042 | BUD-001、004、009、065～068、069 | 完整 01～06 既有 MSTest 與新增整合、Python unittest、help、格式／空白檢查通過；新增前綴案例與實際測試數可追溯，不放寬舊要求換通過。 |
| BUD-AC-043 | BUD-004、056、065～066、070～071 | Release 產物採全新輸出目錄，先生成後掃描，再由該目錄執行 --help；使用當次輸出與退出碼，不使用 Debug 執行或歷史產物替代。沒有上傳，未測架構明列未驗證。 |
| BUD-AC-044 | BUD-004、006、009、066、068、071 | 完成證據表能指出各上游交接、當次命令／結果與未測項；文件完成、離線、PTY、真人／服務、實際發布分列，不把案例表或 README 舊通過數當當次產品證據。 |

### 12.3 PTY、真人與真實 Gemini

| 案例 | 對應需求 | 後續操作與證據邊界 |
|---|---|---|
| BUD-AC-045 | BUD-004、012、015、025、028、030～032、066、069 | macOS PTY 使用現有測試 host 注入假模型／核准／可控寫入，覆蓋 busy↔核准讀取權、預輸入丟棄、Unicode／CRLF、y／yes、拒絕／空白、無效選擇、Ctrl+C／SIGINT／EOF、發布前後取消及退出回顯恢復。時序用明確同步點，保留原隱藏輸入／防洩漏組；不當真人證據。 |
| BUD-AC-046 | BUD-004、025、028～031、061、063、066、071 | 真人 macOS 終端機以假 Key／隔離假檔案確認中文輸入法、貼上／刪除、長行折行、完整差異與行尾、輸入不回顯、拒絕／取消後下一回合、clear 及退出恢復。記錄實際平台與結果，不錄製 Key 或貼原始畫面；測試 host 不算真人。 |
| BUD-AC-047 | BUD-004、018、024～032、061、063、066、071 | 使用者自行隱藏輸入真實 Key，依下表驗收自然語言聊天、讀寫及取消；以真實操作紀錄／檔案 bytes 為依據，服務未觸發分支明列未驗證。只記安全數值及分類，不保存真實 Key、SDK 請求或含檔案全文的報告。 |
| BUD-AC-048 | BUD-004、037、050、056、060～063、066、071 | 提交／分享／對外發布前人工核對實際 index、未暫存／未追蹤內容、文件與附件、發布目錄及最終包裝；固定規則通過不能替代人工檢查，任何改動重做相應門檻。沒有實際發布可記「產物檢查完成、未對外發布」，不能寫已發布。 |
| BUD-AC-049 | BUD-004、066、068、071 | 第三階段完成僅在離線、PTY、目標平台真人、必要真實 Gemini 與提交／產物檢查均有當次通過證據後宣告；不需要自動上傳或發行版本，但未測服務／平台時不得宣稱完整支援。 |

真實 Gemini 人工清單如下；不為製造網路／I/O 故障讓自動測試接實際服務，故障分支仍以離線注入證明。

| 步驟 | 操作 | 核對事實 |
|---|---|---|
| 1 | 確認隔離工作區與資料傳送提示，隱藏輸入 Key，送出正常聊天需求。 | 無額外認證生成；可見回答與實際可得用量分開，不能從有回答推論檔案權限。 |
| 2 | 接續聊天超過五回合，再執行 clear。 | 操作狀態、歷史／快照清除與累計保留；真實服務文字不取代離線歷史結構斷言。 |
| 3 | 自然語言要求列檔，再讀取一份自行建立的 UTF-8 假文件。 | 真實工具名稱／順序、相對路徑、實際資料與列檔安全過濾；不讀憑證檔。 |
| 4 | 要求建一份不存在檔案，核對完整預覽後同意。 | 一次明確核准，實際 bytes／BOM 與本機 Verified；沒有自動建目錄或覆寫。 |
| 5 | 讀取 BOM／既有行尾文件，要求唯一精確替換並同意。 | 未改部分 bytes／BOM／行尾保留，以本機檔案核對，不能只看模型說已修改。 |
| 6 | 再提單檔變更並拒絕／空白確認。 | 該次未套用、立即停止，無同回合替代提案；前次已寫內容仍在。 |
| 7 | 預覽期間由使用者在另一個本機編輯器修改同一假檔，再同意舊候選。 | Conflict、不覆蓋外部內容；新回合重新讀檔／預覽。 |
| 8 | 生成、工具或核准期間取消，再送新訊息；最後正常退出。 | 等待收尾、用量仍可得／不可得、已寫事實保留、終端機模式恢復。未能觸發的狀態記未驗證。 |
| 9 | 另以明確無認證功能的假值觀察服務認證拒絕，再由使用者重新輸入可用 Key。 | 只有實際 AuthenticationFailed 才算認證切換案例；累計保留、舊 Context 清除，等新訊息才生成。服務若回其他分類，記實際分類與未驗證。 |

## 13. 後續開發命令、發布清單與結果模板

| 編號 | 具體要求 |
|---|---|
| BUD-069 | 後續從根目錄依序執行既有 restore／build／test／help、Python 掃描測試、PTY、formatter verify 及 diff check；build 與依賴它的 test／run 不並行。不以執行文件檢查替代這些產品門檻。 |
| BUD-070 | Release 以全新目錄生成 framework-dependent、非單檔 DLL，掃描該目錄，再用相同輸出的 DLL --help；記錄實際命令與結果。這只是本機產物驗收，不含上傳、封裝、簽章或新增平台支援。 |
| BUD-071 | 完成報告使用下表層級，逐項記實際結果／未驗證及上游交接；文件交付只完成文件層。離線／PTY／真人／真實服務／產物門檻未齊時不宣稱整個初版完成，外部發布亦不得由本份規格自動啟動。 |

### 13.1 離線產品命令

以下全部留待後續實作完成後執行；scripts 與掃描測試目前尚未因本次規格建立：

```sh
dotnet restore tests/AIAgent.Cli.Tests/AIAgent.Cli.Tests.csproj
dotnet build tests/AIAgent.Cli.Tests/AIAgent.Cli.Tests.csproj --no-restore
dotnet test --project tests/AIAgent.Cli.Tests/AIAgent.Cli.Tests.csproj --no-build
dotnet run --project src/AIAgent.Cli/AIAgent.Cli.csproj --no-build -- --help
python3 -m unittest discover -s tests -p 'test_sensitive_content.py'
python3 tests/terminal_smoke.py
dotnet format src/AIAgent.Cli/AIAgent.Cli.csproj --verify-no-changes
git diff --check
python3 scripts/check_sensitive_content.py staged
```

未啟用 hook 時最後一行仍須執行。掃描 Python 回歸未來集中於 tests/test_sensitive_content.py，只使用標準函式庫、暫存 Git 及合成資料；本次不建立該檔。Git 測試必須隔離作者／簽章／hook 設定，避免繼承真人憑證、全域簽章或外部工具。

### 13.2 本機 Release 產物命令

下列 /example 路徑是需換成實際新目錄的佔位；不對既有輸出做批次清理，也不使用舊通過紀錄：

```sh
dotnet publish src/AIAgent.Cli/AIAgent.Cli.csproj -c Release --no-restore --no-self-contained -p:UseAppHost=false -o "/example/new-publish-directory"
python3 scripts/check_sensitive_content.py artifacts --path "/example/new-publish-directory"
dotnet "/example/new-publish-directory/AIAgent.Cli.dll" --help
```

每一步成功才進下一步；任何一步非零都停止該次發布流程。掃描目錄根外的 bin／obj 不屬於本次產物範圍，目錄內檔案則全部按第 9 節處理。真的產品互動另在該產物與隔離假檔工作區執行，Key 仍由隱藏輸入，不透過命令、環境或檔案提供。

### 13.3 人工提交／分享／發布清單

- 確認實際準備提交的完整 index 已掃；在本機另外檢視未追蹤、未暫存及強制加入內容，不將原始 diff／憑證貼進報告。
- 檢查文件、PR／Issue 文字、截圖、錄影、附件及測試輸出只有佔位或可分享資料，沒有 Key、原始認證／例外、完整應用指示與檔案全文傾印。
- 確認本機 Release 的來源版本、平台、全新輸出與當次掃描結果，必要產品檔存在、help 可執行、設定非機密；對尚未實測架構保持未驗證。
- 確認產物未再改動，未混入測試憑證、日誌、dump 或候選暫存；若另封裝／簽章，重新人工檢查最終分發物，不把目錄通過當成包內掃描證據。
- 可疑或未完整結果停止分發；不自動上傳、不批次清理，疑似洩漏依第 11 節處理。

### 13.4 實際結果記錄模板

| 證據層級 | 必填內容 | 可宣稱的範圍 |
|---|---|---|
| 文件 | 版本／日期、需求／案例映射、連結／範例／Markdown／空白及變更範圍檢查。 | 規格完整，不代表功能已開發。 |
| 離線產品 | commit 或本機變更基線、OS／架構／工具版本、MSTest 與 Python 的實際命令、退出碼、通過／失敗／略過數、固定案例 ID。 | 假 handler／可控服務與真實隔離檔案的當次結果。 |
| PTY | 實際 host、腳本、組數、退出碼與同步情境。 | 自動終端機行為，不是人為操作或真實 Gemini。 |
| 真人終端機 | 日期、OS／架構／終端機、人工清單逐項通過／失敗／未驗證。 | 該平台實際互動，不擴大到其他平台。 |
| 真實 Gemini | 日期、設定模型、使用者隱藏輸入、實際觸發工具／分支、安全狀態及可得用量。 | 當次服務往返，不保證未來可用或正式費用。 |
| 提交／產物 | index／產物範圍、實際命令、掃描碼／數量、非零失敗處置、人工分享／分發檢查與改動後重查。 | 指定當次範圍的固定規則結果，不是歷史／通用秘密保證。 |

未執行欄填「未驗證」，不得填預期通過。除非確實執行，不能寫「已提交」「已發布」或「真人／真實 API 通過」。不保存 Key、原始終端機轉錄、完整請求、命中內容或未整理例外作為證據。

## 14. 本次文件驗收、完成條件與下一步

| 編號 | 具體要求 |
|---|---|
| BUD-072 | 本次檢查目的、邊界、責任、流程、既有介面、限制／單位、記帳、固定掃描規則／退出碼、Git／產物／hook 與驗收完整；所有需求有案例、編號連續，無待定項、內部矛盾或未落地宣稱。 |
| BUD-073 | 本機連結存在、範例格式／數值可核對、Markdown fences／表格與空白正確；對既有追蹤檔案做內容比對，確認 index／HEAD 不變且只新增本檔。不執行 restore、build、MSTest、Python 產品掃描測試、formatter、PTY、publish、真人或 Gemini。 |
| BUD-074 | 下一步才依本份開發、補強回歸與掃描／hook，再分層記錄實際結果；本次不自動啟動開發或安裝。本規格完成後仍不能宣告第三階段全部完成；延伸能力與平台另訂規格。 |

本次文件檢查獨立編為 BUD-AC-050，不計入後續產品通過數：

| 案例 | 對應需求 | 文件成功條件 |
|---|---|---|
| BUD-AC-050 | BUD-002～003、005～009、017、033～043、068、071～074 | BUD／BUD-AC 連續、需求皆有對照；上游／限制／單位／用量／寫入狀態一致，掃描命令、規則、範例、退出碼、hook 與發布保證不矛盾；連結／Markdown／空白檢查通過，僅新增本檔且既有追蹤內容、index／HEAD 不變，無產品或真人／服務驗收執行宣稱。 |

本次檢查程序：

1. 核對總規格第 16 節及前置交接，確認不是重新實作 01～06，也沒有增加產品工具／公開 API／認證來源。
2. 核對每項需求與案例、所有常數／單位／超限處理、五欄範例及停止／寫入事實；掃描器的開發資源與產品限制分開。
3. 核對固定規則與佔位特例、實際 index blob、NUL 解析、完整產物／二進位、未完整優先退出 2、選用 hook 及可繞過限制。
4. 檢查本機連結、fences、表格欄數、範例與尾端空白，執行 git diff --check；新檔另用下列命令。no-index 若只因新增差異退出 1，須確認沒有空白診斷，不能只憑退出碼判失敗。

```sh
git diff --no-index --check -- /dev/null specs/07-budget-and-acceptance.md
```

5. 比對工作開始時的追蹤檔內容與 index／HEAD，確認 git status 只有本檔新增；不為了驗收規格而新增測試腳本、Git 暫存、hook 或產品輸出。最終回報本次實際文件檢查及未執行的產品範圍。

## 參考資料

下列官方文件於 2026-10-22 核對，用於後續 Git 讀取、hook 及本機發布語意；不是本次已執行掃描或發布的證據。第 7 節外形規則是本專案明定的有限啟發式，不是官方保證的完整秘密格式清單。

- [Git ls-files：index mode、OID、stage 與 NUL 分隔](https://git-scm.com/docs/git-ls-files)
- [Git cat-file：原始物件及 batch 長度格式](https://git-scm.com/docs/git-cat-file)
- [Git 全域選項：停用 lazy fetch、replace objects 與 optional locks](https://git-scm.com/docs/git)
- [Git hooks：pre-commit 非零中止及可繞過限制](https://git-scm.com/docs/githooks)
- [Git ignore：已追蹤檔案不受忽略保護](https://git-scm.com/docs/gitignore)
- [.NET publish：本機輸出目錄與 framework-dependent 發布](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-publish)
- [Google Cloud API Key 管理：建立、保護及撤銷](https://docs.cloud.google.com/docs/authentication/api-keys)
- [GitHub：敏感資料洩漏與歷史／副本處理](https://docs.github.com/en/authentication/keeping-your-account-and-data-secure/removing-sensitive-data-from-a-repository)
