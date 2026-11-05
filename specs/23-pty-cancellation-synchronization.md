# 第 23 份：PTY 取消同步與分層驗收開發規格書

## 實作接續（2026-11-05）

下方版本 0.1 的「待實作／本次只新增本檔」及 PTYC-001／PTYC-AC-009 文件限定條款描述原規格撰寫階段，保留為歷史；本次依第 3～6 節後續契約實作測試，不沿用文件限定範圍宣稱未修改測試。

已加入 busy 首次模型呼叫的唯一開始標記與 flush、三種取消共用案例，以及 `--group cancellation`／`--repeat N`。Generating 事件回呼驗證零呼叫／零生成，呼叫中及延遲 Completed 以受控同步點驗證先收尾、無取消歷史及一次用量。產品組件、政策及公開 API 未改。

當次四項聚焦 MSTest、14 項新增 Python 回歸、30 輪共 90 新 host 及完整 145 項 macOS PTY 通過。完整 MSTest 在沙箱外為 1329 通過／2 失敗／11 略過，仍有原生環境限制；另三項 Release SDK 測試依本次邊界明列未執行，REVIEW-02 暫不解除。命令、來源版本與驗收層級見 [README](../README.md#pty-cancellation-validation)。真人／真實服務、其他平台及其他 REVIEW 保留。

---

> 文件版本：0.1｜建立日期：2026-11-05（Asia/Taipei）｜文件狀態：待實作
>
> 選題依據：[ROADMAP 開發完成度檢查紀錄](../ROADMAP_REVIEW_NOTES.md)的 REVIEW-02。23 是子規格編號，REVIEW-02 是問題追蹤編號。本次只交付規格；以下測試修改與產品驗收均待後續開發，不表示已修正或已通過。

## 1. 目的、選定方案與交付邊界

修正 macOS PTY 取消案例把「處理中」誤當成假模型已開始執行的同步條件，避免第一次取消發生過早，導致下一回合才進入假模型的無限等待分支。另以確定性單元測試驗證產品在模型呼叫前及呼叫中取消的既有責任，將測試可靠性與產品行為分開驗收。

使用者已選定「同步標記與分層驗收」：PTY 透過測試組件的固定模型開始標記定位呼叫中取消；模型呼叫前取消使用既有事件回呼與可控制替身驗證。完整雙向 PTY 閘門會增加 host 協定與資源管理，本次不採用；只補 PTY 標記則不足以直接驗證原競態涉及的零生成分支，因此不採用。

本次僅新增本檔，不修改產品、測試、README、ROADMAP、問題摘要、SPEC 或既有子規格，不新增套件、工作樹、hook 或另一份實作計畫，不更動 Git index／HEAD，不提交、推送或發布。不執行 restore、build、產品測試、PTY、publish 或真實服務呼叫；文件檢查不作產品驗收證據。

本規格不擴充取消能力或重新設計產品生命週期。REVIEW-01 的發布生成修正、REVIEW-03 的產品內 test 不支援，以及 REVIEW-04 的 Release 掃描與原生／真人／服務／分發待辦各自保留。

## 2. 基準、來源與問題證據

### 2.1 基準與必讀來源

- 原始查核基準：`1724e36`（`Add bounded read-only agent delegation`），查核日期 2026-10-30；實際重現紀錄以 [REVIEW-02](../ROADMAP_REVIEW_NOTES.md#review-02pty-取消案例競態)為準。
- 文件撰寫基準：`6219f4851d18d684ed588fdabfb1c12858bcf8e2`，開始撰寫時工作目錄乾淨。本次重新核對下列來源，但未重跑競態或產品驗收。
- 上位契約：[總規格](../SPEC.md)第 8～10、15 章；[第 03 份](03-cli-and-context.md) CLI-007、015、026～030、CLI-AC-014～016、030、033～035；[第 05 份](05-agent-loop.md)的生成准入、取消與 LOOP-AC-016、037～038；[第 14 份](14-terminal-interaction.md) TERM-AC-021～024、029～031。
- 測試入口：[PTY 腳本](../tests/terminal_smoke.py)的 `chat_cases`、[離線 TerminalHost](../tests/AIAgent.Cli.TerminalHost/Program.cs)的 `OfflineClient.GenerateAsync` 與 `OfflineTerminal`。
- 產品流程：[AgentContext](../src/AIAgent.Cli/Core/AgentContext.cs)、[GenerationGateway](../src/AIAgent.Cli/Core/GenerationGateway.cs)及 [AgentRunner](../src/AIAgent.Cli/Core/AgentRunner.cs)。
- 既有單元測試：[AgentRunnerTests](../tests/AIAgent.Cli.Tests/AgentRunnerTests.cs)及 [ChatApplicationTests](../tests/AIAgent.Cli.Tests/ChatApplicationTests.cs)。

後續開發前重新確認來源版本、未提交變更、SDK、Python 與平台。本文的來源觀察及歷史總數不能當成未來版本的實測結果；不讀取、附錄或匯出完整應用指示、認證或原始終端機轉錄。

### 2.2 缺陷因果鏈

1. PTY 送出第一則訊息，等待顯示「處理中」。產品的 `AgentEventKind.Generating` 已發出，但模型尚未必被呼叫。
2. `AgentContext.GenerateAsync` 在該事件後仍執行規則與 Review 來源重驗。`GenerationGateway` 也會檢查取消，再經既有生成准入開始計數，最後才呼叫 client。
3. 若 PTY 此時送出外部 SIGINT，回合可在模型呼叫前正常取消；該回合的 `GenerationsStarted` 為 0，假模型 `_calls` 仍為 0。這不是產品應強迫開始生成的理由。
4. 測試收到第二次 `OFFLINE_INPUT_READY` 後送出 `next`。假模型這時才第一次進入 `GenerateAsync`，將 `_calls` 增為 1。
5. 現行 `busy` 分支在 `_calls == 1` 時以取消 token 等待無限延遲。測試卻等待第三次輸入提示，未再送取消；因此子程序仍存活而測試逾時。

busy 的 typed Ctrl+C 與 EOF 案例也使用相同「處理中」等待條件，後續一併改用精確標記。EOF 案例未必呈現同一個下一回合卡住症狀，但仍須證明實際涵蓋模型呼叫中的退出，而非只測到呼叫前停止。

### 2.3 證據分層

| 來源 | 已知事實 | 證據限制 |
|---|---|---|
| 2026-10-30 原查核 | 完整 PTY 首次失敗於第三次輸入提示；原案例單獨三次及完整重跑曾成功。 | 重跑成功不能排除競態，也不是本次實測。 |
| 原查核的重複觀察 | 第 13 次觀察到第一回合生成開始為 0；下一回合未返回提示，程序存活，出現兩次進度事件。 | 證實同步不足與假模型分支相符，不證明真實服務有相同卡住問題。 |
| 本次程式碼核對 | 進度早於最終重驗及模型呼叫；busy 首次呼叫才無限等待；PTY 仍以進度同步。 | 靜態來源觀察，未執行修正或重現。 |
| 既有測試來源 | 已有預取消、busy 啟動前取消、呼叫中等待收尾與延遲完成案例。 | 須檢查斷言並補足精確事件邊界，不能直接沿用歷史通過宣稱本項完成。 |

## 3. 開發需求與介面契約

| 編號 | 要求 |
|---|---|
| PTYC-001 | 本次只新增規格；所有後續修改與測試明示待執行，文件完成不解除 REVIEW-02。 |
| PTYC-002 | 保留原查核與目前來源觀察的區別；進度事件不等於模型開始，不以重跑、延長逾時或固定 sleep 代替同步。 |
| PTYC-003 | 後續僅在 TerminalHost 的 `scenario == "busy" && _calls == 1` 分支輸出模型開始標記；必須在進入 `GenerateAsync` 並完成 `_calls++` 後、等待取消前輸出及 flush。 |
| PTYC-004 | 標記固定為獨立一行 `OFFLINE_BUSY_MODEL_STARTED`，每個 busy host 程序恰一次；無動態內容，不送模型、不進 Context／工作保存，不加入產品組件或顯示協定。 |
| PTYC-005 | PTY 的 busy Ctrl+C、busy SIGINT、busy EOF 在送出忙碌預輸入或控制訊號前，均先確認進度並等待開始標記。進度只驗證顯示，不授予送取消的時序資格。 |
| PTYC-006 | Ctrl+C／SIGINT 後須完成取消、返回第二次輸入提示；`next` 必須產生安全正常回答並返回第三次提示，沒有殘留忙碌文字或額外訊息；等待輸入時再取消退出 130。 |
| PTYC-007 | busy EOF 必須在模型開始後觸發，完成取消與收尾後退出 0；failure 情境維持原本失敗退出 1，不等待只適用 busy 的標記。 |
| PTYC-008 | 以既有 Generating 事件回呼精確取消模型呼叫前的回合，斷言假模型零呼叫、生成開始零次、沒有新工具操作／成功提交，再驗證下一回合正常生成；不依排程速度或外部訊號碰運氣。 |
| PTYC-009 | 呼叫中取消以可控制假模型明確回報已進入及已觀察 token 取消，測試主動釋放收尾；釋放前不得開啟下一個輸入讀取者，取消結果或延遲 Completed 均不得提交該回合為成功。 |
| PTYC-010 | 重複取消不變成下一回合取消、不重複提交或記帳；區分生成開始、假模型呼叫及 `RequestDispatched` 用量，零呼叫分支不消耗生成機會，已開始分支不因取消退款。 |
| PTYC-011 | 沿用有界等待、程序退出與終端機恢復檢查。標記缺失、意外退出或逾時明確非零；不自動重試、不把略過算通過。快退出但已輸出的標記仍按既有緩衝處理。 |
| PTYC-012 | 測試全程離線且只用合成認證；輸出只含案例／輪次／狀態及固定安全分類，不落地或傾印原始 PTY 資料、請求、認證或例外正文。 |
| PTYC-013 | 不改公開 API、工具 Schema、設定、產品 CLI 參數、進度位置、取消政策、預算或套件；既有單一讀取者、清緩衝與資源所有權保持。若新測試揭露產品缺陷，先獨立記錄，不能以放寬斷言或測試旁路掩蓋。 |
| PTYC-014 | 完整離線回歸與 macOS PTY 之外，三個受影響取消情境各執行 30 次；每輪使用新 host，任一失敗即整體失敗。分層記錄完成狀態，不解除其他 REVIEW 或真人／真實服務門檻。 |

### 3.1 TerminalHost 與 PTY 內部約定

後續 busy 分支的固定順序為：`_calls++` → 判斷 busy 首次呼叫 → `Console.WriteLine("OFFLINE_BUSY_MODEL_STARTED")` → `Console.Out.Flush()` → 使用原 cancellation token 等待無限延遲。沿用原取消例外的受控結果處理，不新增一般重試或第二個取消來源。標記輸出／flush 失敗時測試須失敗，不吞掉錯誤假裝同步成功。

PTY 以固定 ASCII bytes 等待標記，沿用每次等待 8 秒的 monotonic deadline 與既有程序存活檢查；不要求固定的 PTY CRLF 位元組布局。每個 host 只預期一筆開始標記，成功案例結束前核對次數。下一回合不會再進入首次呼叫等待，應取得「離線回答」，而不只是看到任意輸入提示。

保留 `OFFLINE_INPUT_READY` 作為輸入已啟動的 gate，不改用產品文字提示取代它。Ctrl+C 保留既有重複控制字元情境，SIGINT 保留對 host PID 的外部訊號；不可互換兩者而失去路徑覆蓋。EOF 使用原 Ctrl+D 位元組。

標記只證明假 client 進入點；不證明 HTTP 已送出、供應商已接受請求或模型開始運算。`GenerationsStarted`、假 client 呼叫計數及 `RequestDispatched` 分別斷言，不由進度或標記反推網路用量。

### 3.2 確定性單元測試責任

在既有 `AgentRunnerTests` fixture 的 `OnEvent` 於第一次 `AgentEventKind.Generating` 同步取消 token，不新增產品 hook。第一回合須得到 `Cancelled`／`TurnCancelled`、`GenerationsStarted == 0`、client Requests 為空、CompletedTurn 為空、零本機操作及零新增 dispatched 用量；同一 runner 使用新 token 執行下一回合，須 Completed、一次模型呼叫及一次生成開始。此案例與只在 `RunAsync` 前就取消 token 的既有案例分開。

`ChatApplicationTests` 承接並補強 `CLIAC014_CancelBeforeGenerationDoesNotCancelApplication`、`CLIAC014_InterruptRoutesByStateAndWaitsForCancelledGeneration` 及延遲完成案例：確認取消回合不提交歷史、下一請求不帶被取消訊息、下一 token 未取消，且程式仍可正常退出。

呼叫中案例以 `TaskCompletionSource` 搭配 `RunContinuationsAsynchronously` 建立「已進入／已觀察取消／允許結束」同步點，等待上限 5 秒；不得用 sleep 推定已進入。受控假 client 可在觀察取消後仍等待測試釋放，以證明產品沒有提前回到輸入。重複控制與延遲 Completed 分支均須核對單次結果處理；fixture 的釋放放在 finally，失敗時也不留下未結 task。

用量斷言依測試指定的 `RequestDispatched`：零呼叫取消為 0；已進入且假結果明示 dispatched 的取消或延遲完成為一次，不得重複記帳。下一成功回合另加一次；不把合成數值當真實服務成本證據。

## 4. 後續實作順序與驗證命令

本節全部待後續開發執行，本次不建立測試或執行器。

1. 核對基準與既有案例，新增精確「Generating 事件後、client 前」單元案例及必要收尾斷言；既有產品正確時，新增測試直接通過是有效結果，不捏造紅燈。
2. 在 busy 假模型加入開始標記與 flush，修改三個 PTY 情境的等待條件；先同步再傳忙碌文字與控制訊號，保留原本失敗情境。
3. 將這三個情境抽成腳本內共用取消案例函式，完整 `chat_cases` 與定向執行共用同一份動作及斷言，避免複製出兩套測試。
4. 僅在測試腳本新增 `--group cancellation` 與 `--repeat N`。`N` 為正整數，預設 1；明確傳入 `--repeat` 僅允許 cancellation group，其他組合或非正整數由 parser 非零拒絕。該 group 執行前仍須檢查 macOS，輸出每輪／每案例安全結果。這不是產品 CLI 參數。
5. 完整模式仍只執行每案例一次；定向 30 輪共 90 個新 host，每輪依序跑 Ctrl+C、SIGINT、EOF。首個失敗即非零停止，記錄失敗輪次，其餘標未執行，不自動續跑或重試。

後續從儲存庫根目錄依序執行，各命令單獨判斷退出碼；前置建置失敗不得使用舊 DLL 宣稱已驗證修改：

```sh
dotnet restore tests/AIAgent.Cli.Tests/AIAgent.Cli.Tests.csproj
dotnet build tests/AIAgent.Cli.Tests/AIAgent.Cli.Tests.csproj --no-restore
dotnet test --project tests/AIAgent.Cli.Tests/AIAgent.Cli.Tests.csproj --no-build
python3 -B -m unittest discover -s tests -p 'test_*.py'
python3 -B tests/terminal_smoke.py --group cancellation --repeat 30
python3 -B tests/terminal_smoke.py
dotnet format src/AIAgent.Cli/AIAgent.Cli.csproj --no-restore --verify-no-changes
git diff --check
```

`cancellation` 與 `repeat` 是後續新增的測試入口，目前尚不存在。MSTest 專案參照會建置 TerminalHost；執行前須確認實際 Debug host 為當次輸出。命令須全程離線，既有依賴未備妥或環境不允許必要終端機／原生能力時記前置條件不足，不靜默略過或放寬產品隔離。

不為本項重跑 Release 發布或掃描，也不啟動受阻產物。驗證若因其他已知限制受阻，記錄具體案例與安全分類；無證據時不歸因為既知競態，不以成功重跑覆蓋失敗。

## 5. 後續驗收案例與需求對照

| 案例 | 對應需求 | 方法與成功條件 |
|---|---|---|
| PTYC-AC-001 | PTYC-002、008、010、013 | 在 Generating 事件回呼取消，直接檢查零呼叫、零生成、零 dispatched、零操作及取消結果；同 runner 新 token 的下一回合正常。另保留原有預取消案例。 |
| PTYC-AC-002 | PTYC-009～010、013 | 假模型以同步點證明已進入及收到取消；釋放前保持收尾且沒有新輸入讀取者，釋放後回到輸入；取消回合不提交，下一回合正常。 |
| PTYC-AC-003 | PTYC-003～006、011～012 | macOS PTY typed Ctrl+C 等待進度及唯一開始標記後才送忙碌文字與重複取消；第二提示後 next 得到正常回答及第三提示，等待輸入再 Ctrl+C 退出 130。 |
| PTYC-AC-004 | PTYC-003～006、011～012 | 同前例但使用外部 SIGINT；不能換成 typed 控制字元，完成下一回合並在輸入時 SIGINT 退出 130。 |
| PTYC-AC-005 | PTYC-003～005、007、011～012 | busy EOF 等待開始標記後送 Ctrl+D，退出 0；failure 案例不等待此標記，仍退出 1。 |
| PTYC-AC-006 | PTYC-006、009～013 | 重複取消／延遲 Completed 不提交取消回合、不影響下一 token、不重複記帳；PTY 普通忙碌文字無回顯／無排隊，各退出後 ECHO／ICANON／ISIG 與原狀態一致。 |
| PTYC-AC-007 | PTYC-002、011～012 | 使用只輸出固定安全標記的可控制測試子程序，驗證缺少標記但存活時有界逾時、提早退出而無標記時失敗、已輸出標記後快退出可讀取緩衝；非預期退出仍由 finish 判失敗。失敗輸出不含原始轉錄。 |
| PTYC-AC-008 | PTYC-005～007、011～014 | 定向 30 輪、共 90 個新 host 全通過，逐輪可追溯；首失敗即停止且不自動重試。非法 repeat／group 組合非零拒絕；完整模式仍涵蓋相同取消案例，並完成完整離線回歸與 macOS PTY。 |
| PTYC-AC-009 | PTYC-001～014 | 文件狀態、基準、方案、連結、需求／案例對照與空白檢查完成；只新增本檔，未修改產品或執行產品驗收，其他 REVIEW 與真人／服務門檻分列。 |

PTYC-AC-007 的測試子程序與 Python 驗證於後續開發才新增，使用標準函式庫及既有 `TerminalProcess`，不得為驗證失敗路徑修改產品或加入真認證。永久存活的測試子程序由 finally 終止及 wait；沿用逐一關閉 PTY fd 的生命週期，不遞迴或批次刪除暫存工作區。

30 輪只提供穩定性證據，不證明所有排程都不會失敗；精確事件邊界由 PTYC-AC-001～002 證明，PTY 證明真實控制字元／訊號與終端機交接。

## 6. 完成判準與交接

| 層級 | 可宣告條件 | 保留事項 |
|---|---|---|
| 本次規格完成 | PTYC-AC-009 的文件部分完成；只有本檔新增。 | 產品仍待實作，REVIEW-02 未解除；不將規格範例記為實測。 |
| REVIEW-02 修正已驗證 | 開始標記及三個等待條件落地，PTYC-AC-001～008 有當次證據，完整回歸無本項新回歸。 | 只解除本項測試同步缺陷；真實服務取消或真人操作未因此驗收。 |
| 真人／真實服務驗收完成 | 依第 03、05、14 份各自實際執行與記錄適用案例。 | PTY、假模型及 30 輪均不替代真人 IME、服務往返或其他平台。 |

後續實作時才更新 README 的本次驗證紀錄、SPEC 子規格索引與 REVIEW-02 狀態，保留原查核失敗與重現歷史。若產品取消出現可重現缺陷，獨立界定原因與修正，不在本項默默擴張政策或宣稱真實供應商同樣失敗。

驗收紀錄至少包括來源版本、OS／架構、實際 SDK／Python、各案例與輪次、命令、退出碼、實際通過／失敗／略過數、未執行原因及證據層級。只記固定安全分類，不保存完整應用指示、Key、請求、原始 PTY 轉錄或例外正文。

## 7. 本次文件檢查

本次只執行以下文件檢查，不執行第 4、5 節產品命令：

1. 完整讀回本檔，核對版本 0.1、待實作、日期、兩個基準、已選方案與文件限定範圍。
2. 核對 PTYC-001～014、PTYC-AC-001～009 唯一連續，全部需求有案例對照；取消前／中、EOF、失敗、記帳、下一回合與完成層級一致。
3. 核對相對檔案連結、錨點、Markdown 表格／fences 與命令；測試腳本新增參數不得寫成現已可用。
4. 檢查新增檔空白與 Git 狀態；一般 git diff 不含未追蹤檔，須另外讀回及檢查本檔。
5. 比對原追蹤檔內容、index 與 HEAD，確認沒有其他變更；檢查不寫入 index、不提交或推送。

```sh
git diff --check
git diff --no-index --check -- /dev/null specs/23-pty-cancellation-synchronization.md
git status --short
git diff --name-only
git diff --cached --name-only
```

`--no-index` 的結果須區分新增檔差異與空白診斷，不因檔案不同於空檔就判為空白失敗。文件檢查結果僅代表規格交付品質，產品驗收保持未執行。
