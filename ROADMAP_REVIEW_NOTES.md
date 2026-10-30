# ROADMAP 開發完成度檢查紀錄

- 查核日期：2026-10-30（Asia/Taipei）。
- 查核基準：`1724e36`（`Add bounded read-only agent delegation`）。
- 環境：macOS arm64、.NET SDK 10.0.100。
- 依據：[ROADMAP](ROADMAP.md)、[總規格](SPEC.md)、[子規格](specs/)及實際程式與測試。
- 用途：保存本次發現與驗證證據，供後續分別撰寫開發規格書；本檔不是修正規格或完成驗收證明。

本次已找到 FUT-01～14 的對應實作與測試，但仍有發布流程缺陷、測試競態、既有功能限制及未結驗收，不能宣告全部開發完成且無誤。以下不預先決定修正方案，也不將既有明示限制誤列為新發現的產品回歸。

## 項目總覽

| 編號 | 分類 | 項目 | 狀態 |
|---|---|---|---|
| REVIEW-01 | 新發現缺陷 | Release 發布命令連帶停用 ProcessHost apphost，造成發布失敗。 | 待另立規格處理 |
| REVIEW-02 | 新發現缺陷 | PTY 取消案例缺少假模型開始執行的同步條件，可能卡住後續回合。 | 待另立規格處理 |
| REVIEW-03 | 既有限制 | FUT-02 產品內受控 `test` 不支援，第 10 階段未完整驗收。 | 待另立規格處理 |
| REVIEW-04 | 未結驗收 | Release 掃描阻擋，真人、真實服務及部分原生／平台驗收未完成。 | 待另立規格處理 |

## REVIEW-01：Release 發布失敗

**狀態：待另立規格處理。**

來源：[README 發布與人工驗收](README.md#發布與人工驗收)、[CLI 專案](src/AIAgent.Cli/AIAgent.Cli.csproj)及 [ProcessHost 專案](src/AIAgent.ProcessHost/AIAgent.ProcessHost.csproj)。查核基準的相關位置為 README 第 1132 行、CLI 專案第 10／13 行及 ProcessHost 專案第 9 行。

在尚無 Release ProcessHost apphost 的情況下，從根目錄依 README 執行下列命令，輸出至當次新建的隔離目錄：

```sh
dotnet publish src/AIAgent.Cli/AIAgent.Cli.csproj -c Release --no-restore --no-self-contained -p:UseAppHost=false -o "/private/tmp/aiagent-roadmap-review-release-qLu2Ms"
```

實際退出碼為 `1`，錯誤為 `MSB3030`：找不到要複製的 `src/AIAgent.ProcessHost/bin/Release/net10.0/AIAgent.ProcessHost`。當時該目錄有 DLL、deps、runtimeconfig 與 PDB，但沒有 apphost 執行檔。

原因證據：命令列的全域 `UseAppHost=false` 會傳至參照專案，覆蓋 ProcessHost 專案內的 `UseAppHost=true`；CLI 專案卻仍要求複製該執行檔。以下唯讀屬性查詢實際輸出 `false`：

```sh
dotnet msbuild src/AIAgent.ProcessHost/AIAgent.ProcessHost.csproj -getProperty:UseAppHost -p:UseAppHost=false
```

對照驗證僅將命令列改為 `UseAppHost=true`，並改用另一個全新隔離輸出目錄，發布退出 `0`：

```sh
dotnet publish src/AIAgent.Cli/AIAgent.Cli.csproj -c Release --no-restore --no-self-contained -p:UseAppHost=true -o "/private/tmp/aiagent-roadmap-review-apphost-dI4jqB"
```

此對照只確認失敗與 apphost 選項的關係，不代表已選定修正方案或原發布流程已修正。已有舊 Release apphost 的工作目錄可能掩蓋問題；後續驗收須涵蓋沒有該舊產物的情境。原命令失敗後未繼續驗收其輸出，對照產物的掃描結果另見 REVIEW-04。

## REVIEW-02：PTY 取消案例競態

**狀態：待另立規格處理。**

來源：[PTY 測試](tests/terminal_smoke.py)第 275～294 行、[生成流程](src/AIAgent.Cli/Core/AgentContext.cs)第 76～91 行，以及 [TerminalHost 假模型](tests/AIAgent.Cli.TerminalHost/Program.cs)第 63／181 行；行號均以查核基準為準。

`CLI-AC-030-busy-SIGINT` 等待「處理中」後便發送外部 SIGINT。產品在實際模型呼叫前已發出該進度，之後還會重驗來源與取消狀態，因此看到進度不等於假模型已進入 `GenerateAsync`。

若取消先於假模型開始，第一回合會正常取消且生成開始次數為 `0`，假模型的 `_calls` 也仍為 `0`。測試接著送出下一則訊息，此時才進入假模型的第一次呼叫；`busy` 情境會在 `_calls == 1` 時持續等待取消，造成測試等不到第三次輸入提示。

本次觀察與重現結果：

- 首次完整 PTY 執行退出 `1`，停止於 `PTY-prompt-OFFLINE_INPUT_READY-3-exit-None`。
- 原情境單獨重跑三次均通過；完整 PTY 再跑一次亦退出 `0`，故不能只憑重跑成功排除競態。
- 另以既有 TerminalHost 的 `busy` 情境重複相同步驟，第 13 次確認第一回合生成開始次數為 `0`。
- 該次下一回合未返回輸入提示，程序仍存活，已觀察兩次「處理中」事件，與上述假模型分支相符。

這項證據確認測試同步條件不足，不代表已證明真實模型服務或產品的一般取消功能存在相同卡住問題。後續規格應分別界定測試可靠性與產品取消行為的驗收責任。

## REVIEW-03：FUT-02 產品內測試不支援

**狀態：待另立規格處理。**

來源：[ROADMAP 的 FUT-02](ROADMAP.md#fut-02受控建置與測試)、[第 10 份實作接續與驗收限制](specs/10-controlled-build-and-test.md#實作接續與驗收限制2026-10-24)及[受控程序實作](src/AIAgent.Cli/Processes/ControlledDotnetTasks.cs)第 26／58 行。

產品目前支援受控 Debug build；`DotnetTaskKind.Test` 仍直接回報 `capability_unavailable`，不執行目標測試。這是已記錄的交付限制：SDK 10.0.100 的 MTP IPC 與私人 IPC 邊界衝突，既有決策保留隔離限制，沒有因此放寬權限。

開發者從終端機執行測試專案，與產品透過 `run_dotnet_task` 執行受控測試，是不同入口。本次開發者測試 `1341／1341` 通過，不能當成產品已支援 `test` 的證據，也不能將 BUILD-AC-016～018 的真實 MTP 正向驗收標為完成。

ROADMAP 原方向中的建置失敗後自動修正或重試循環亦未提供，現有文件已明示另行定義；不得僅以 FUT-02 已有建置入口推論該延伸能力已交付。

## REVIEW-04：Release 與人工／真實服務驗收未結

**狀態：待另立規格處理。**

來源：[README 第 21 份驗證紀錄](README.md#第-21-份驗證紀錄2026-10-30)、[第 10 份驗證紀錄](README.md#第-10-份驗證紀錄2026-10-24)及[發布與人工驗收](README.md#發布與人工驗收)。

### 本次 Release 掃描證據

對 REVIEW-01 成功發布的對照產物執行：

```sh
python3 -B scripts/check_sensitive_content.py artifacts --path "/private/tmp/aiagent-roadmap-review-apphost-dI4jqB"
```

實際退出碼為 `1`，安全分類輸出為：

```text
[finding] item=000009 rule=BUD-S003
[scan] mode=artifacts status=blocked scanned_files=26 findings=1
```

這是目前產物在既定規則下受阻的證據，不代表已確認存在真實私鑰。掃描非零後未執行該 Release 產物的 `--help`，亦未分發、放寬掃描規則或刪除相依檔案。

### 尚未完成的驗收範圍

- 真人終端機操作，包括中文輸入法、長結果、進度、核准、取消及退出還原；PTY 不替代真人驗收。
- 真實 Gemini 兩個 profile、OpenAI 及 Microsoft Learn 的相容性與服務往返；本次自動模型驗證使用假 HTTP 與合成認證。
- 第 10 份列出的部分原生邊界，包括完整逾時、忽略 TERM、後代程序變更群組／setsid／double-fork、身分上限、敵對路徑交換及監督器崩潰後收尾；真實 fixture 證據尚未齊全。
- 其他 OS／架構及人工提交、分享、最終包裝與分發檢查。

各份規格的真人／真實服務案例仍須分別記錄；例如 MULTI-AC-037／038、MODEL-AC-035／036 及 BUILD-AC-036／037。離線測試全綠不能解除這些門檻。

## 本次查核的自動驗證結果

以下是 2026-10-30 查核當時實際執行的結果；新增本紀錄檔時未重新執行全套產品測試，不將歷史通過數視為後續修改的新證據。

| 命令／範圍 | 當次結果與限制 |
|---|---|
| `dotnet restore tests/AIAgent.Cli.Tests/AIAgent.Cli.Tests.csproj` | 退出 `0`。 |
| `dotnet build tests/AIAgent.Cli.Tests/AIAgent.Cli.Tests.csproj --no-restore` | 退出 `0`，零警告／錯誤。 |
| `dotnet test --project tests/AIAgent.Cli.Tests/AIAgent.Cli.Tests.csproj --no-build` | 退出 `0`，1341／1341 通過、零失敗／略過；4 分 17.824 秒。測試在外層沙箱之外執行以允許初始化產品原生隔離，產品隔離保持啟用。 |
| `python3 -B -m unittest discover -s tests -p 'test_*.py'` | 退出 `0`，38／38 通過。 |
| `python3 -B tests/terminal_smoke.py` | 首次退出 `1`；完整重跑退出 `0`。已另行重現 REVIEW-02，不將重跑通過解讀為不存在競態。 |
| `dotnet run --project src/AIAgent.Cli/AIAgent.Cli.csproj --no-build -- --help` | 退出 `0`，無互動。此項為 Debug 入口，不是受阻 Release 產物的驗收。 |
| `dotnet format src/AIAgent.Cli/AIAgent.Cli.csproj --no-restore --verify-no-changes` | 退出 `0`。 |
| `git diff --check` | 通過；查核當時追蹤工作檔無變更。 |
| `python3 -B scripts/check_sensitive_content.py staged` | 退出 `0`，401 檔、零命中；只涵蓋當次 Git index，不含本次新增但未暫存的紀錄。 |
| 原 README Release 發布命令 | 退出 `1`，`MSB3030`，見 REVIEW-01。 |
| `UseAppHost=true` 對照發布／產物掃描 | 發布退出 `0`；掃描退出 `1`／`blocked`，26 檔、1 項 `BUD-S003`，見 REVIEW-04。 |

## 後續使用與紀錄範圍

後續以 REVIEW-01～04 分別追蹤，於個別開發規格書中定義修正範圍、權限邊界與驗收條件。修正或補驗完成前，各項均維持「待另立規格處理」，不先選定實作方案或宣告問題解除。

本次文件交付只新增此根目錄紀錄，不修改程式、README、ROADMAP、SPEC 或既有子規格，不提交、推送或發布。查核使用的隔離產物與暫存目錄保留，未執行批次或遞迴刪除。
