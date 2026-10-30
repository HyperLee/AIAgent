# 01：專案啟動與 API Key 子規格

## 第 21 份產品接續（2026-10-30）

[唯讀多 Agent 分工](21-multi-agent-delegation.md)明示接續本份：認證仍只來自隱藏輸入；用途限定轉接器可借用目前 session，建立至多兩個同 profile 子 client。子循環不取得 session、Secrets 或認證來源；全部子能力先收尾，父認證最後釋放。 本段為產品增量；下列撰寫當時的階段邊界與歷史驗證保留，不能視為全部人工／服務／發布驗收完成。

## 第 20 份產品實作接續（2026-10-29）

新增非機密 Llm.Provider／Model，並相容原 Gemini.Model；固定目錄准入在讀 Key 前完成。啟動確認列 Provider、Model 與官方目的地，認證仍只由隱藏終端機輸入取得。--help 不讀設定或工作區。 詳見[第 20 份規格](20-multi-model-and-provider-support.md)；A、B 分層證據見 [README](../README.md#model-validation)，真實服務／真人與既有 Release 未結門檻另列。

## 第 15 份產品實作接續（2026-10-26）

[工作保存與續接](15-work-save-and-resume.md)的限定增量：新增 `--resume ID`，可與 `--workspace DIR` 任意順序各出現一次；選項只經 internal 資料交接。工作區確認與隱藏新 Key 完成後才讀存檔，不從存檔取得認證或選定工作區。`--help` 保持零 Key／client／規則／存檔 I/O，語法錯誤退出 2。 本節保留舊階段歷史，實際分層驗證見 [README](../README.md#work-save-validation)。

> 文件版本：0.1｜建立日期：2026-10-19｜狀態：待實作規格
>
> 上位文件：[入門 AI Agent 開發總規格書](../SPEC.md)，以第 3～6、8、13、15～16 章為依據。本文件只定義後續開發與驗收要求；目前尚無產品程式、測試專案、執行期防護或 Git hooks。文件完成不代表第一階段「可以聊天」已完成。

## 1. 目的與交付邊界

讓具備基本 C# 知識的開發者可以先建立可測試的 Console 啟動流程：解析非機密設定、確認工作區、隱藏輸入 API Key，再將啟動結果交給後續模組。

本子規格的獨立完成點為「工作區已確認、有效的非空 Key 已取得並只存在記憶體、啟動結果可交接」。此處的「有效」只代表通過本機輸入檢查，不代表 Google 已接受認證。

| 本文件負責定案 | 後續文件負責 |
|---|---|
| 專案骨架、啟動參數、JSON 設定、工作區根路徑選定 | 第 02 份：SDK 固定版本、實際模型、API 模式、模型核心契約、認證錯誤分類。 |
| Key 隱藏輸入、記憶體生命週期、共用阻擋與遮蔽約定 | 第 03 份：對話指令、完整回合、歷史、處理中取消與故障收尾。 |
| 啟動取消、排除規則、提交前人工檢查、基礎驗收 | 第 04～06 份：工具路徑限制、讀檔、工具循環、差異預覽與確認寫入。 |
| 各模組應沿用的敏感資訊契約 | 第 07 份：整體驗收、提交與發布自動掃描及阻擋的具體範圍。 |

本次文件交付只新增本檔；保留總規格與現有 `.gitignore`。以下專案、介面、命令與畫面均為未來實作要求，不是目前已存在的成果。不建立其他子規格空檔，不安裝套件、掃描工具或 hooks，不進行提交、推送或歷史改寫。

## 2. 專案骨架與模組責任

後續實作採 .NET 10、C#、單一 Console 產品專案，先在 macOS 終端機驗收；其他平台須實測後才宣稱支援。測試採獨立 MSTest 專案，不以真實 Gemini 服務為依賴。

```text
AIAgent/
├── SPEC.md
├── .gitignore
├── specs/01-bootstrap-and-key.md
├── src/AIAgent.Cli/
│   ├── AIAgent.Cli.csproj
│   ├── appsettings.json
│   ├── Cli/                 # 參數、終端機輸入輸出、工作區確認
│   ├── Configuration/       # 非機密設定載入及驗證
│   ├── Core/                # 啟動流程、結果與最小契約
│   ├── Security/            # Key 生命週期、共用敏感資訊政策
│   └── Program.cs           # 組裝、呼叫啟動流程、映射退出碼
└── tests/AIAgent.Cli.Tests/
    └── AIAgent.Cli.Tests.csproj
```

不為未開發的 Llm、Tools 或 WorkspaceFiles 建立空類別。根路徑解析可以由本次的啟動專用元件處理，第 04 份再接到統一檔案入口；根路徑確認不能取代未來每次工具操作的安全檢查。

| 元件 | 責任與限制 |
|---|---|
| 參數解析器 | 純解析，回傳結構化結果；不讀 Key、不呼叫模型。 |
| 設定載入器 | 只讀指定位置的非機密 JSON；不建立、更新或儲存設定。 |
| 根路徑解析器 | 驗證目錄、解析符號連結並固定根路徑；不掃描工作區內容。 |
| CLI 轉接器 | 處理隱藏輸入、明文確認、EOF 與 Ctrl+C；所有顯示通過敏感資訊政策。 |
| Bootstrapper | 協調啟動次序與取消；透過介面取得互動結果，不直接使用 Console。 |
| SessionSecrets | 保存當前 Key，供認證與精確比對；不提供會列印原值的字串表示。 |
| SensitiveDataPolicy | 共用精確比對、內容拒絕與輸出遮蔽；不宣稱可找出所有秘密。 |

## 3. 需求與啟動設定

需求編號 `BOOT-xxx` 固定使用；新增需求另增編號，不重新排序既有編號。驗收案例使用 `AC-xxx`，並標示本份或後續整合責任。

### 3.1 參數與非機密設定

| 編號 | 具體要求 |
|---|---|
| BOOT-001 | 依第 2 節建立骨架；產品使用 `net10.0`，測試可替換互動、路徑與設定讀取，不要求真實終端機或 Key。 |
| BOOT-002 | 僅接受 `--workspace <path>` 與 `--help`，名稱區分大小寫。`--help` 單獨使用時顯示用法並成功退出，不讀設定、不檢查工作區、不提示 Key；和其他參數混用視為參數錯誤。 |
| BOOT-003 | 未知參數、重複參數、缺值、空白路徑及額外位置參數均失敗；不支援短參數、`--workspace=value`、`--model` 或任何 Key 參數。錯誤只顯示固定原因與用法，不回顯參數原值或整條命令。 |
| BOOT-004 | 工作區唯一來源為 `--workspace`，未指定時使用程序啟動當下的目前目錄；相對路徑也以該目錄解析。解析後不改變程序目前目錄，不展開 `~` 或環境變數；shell 在傳入前完成的展開不屬於程式行為。 |
| BOOT-005 | 只從 `AppContext.BaseDirectory` 下的 `appsettings.json` 載入非機密設定，建置與發布時複製該檔。不得從工作區、環境變數、`.env`、User Secrets 或其他 Local 設定疊加讀取。 |
| BOOT-006 | JSON 採第 3.2 節的唯一結構；缺檔、無權限、無效 JSON、重複或未知欄位、錯誤型別均失敗。錯誤不輸出原始 JSON 或例外內容。 |
| BOOT-007 | 模型名稱為 `null` 或去除首尾空白後的非空字串，拒絕空字串、純空白及控制字元。本份允許 `null` 以獨立驗收骨架；第 02 份整合後必須在 Key 輸入前要求實際非空模型，定案並更新非機密設定。 |

`--workspace` 的下一個值若以 `--` 開頭，視為缺值；這類目錄須改用 `./--name` 或完整路徑。含空白的路徑由 shell 引號保留為單一參數。

### 3.2 JSON 結構與預期命令

本份後續實作所用的設定範本如下；`null` 明確表示模型尚未由第 02 份定案，不得送往 API，也不是可用的模型名稱。

```json
{
  "Gemini": {
    "Model": null
  }
}
```

根物件必須且只能有 `Gemini` 物件，該物件必須且只能有 `Model` 欄位；欄位名稱區分大小寫。不接受註解或尾隨逗號，不提供 Key 欄位；遇到 `ApiKey` 等未知欄位直接拒絕，亦不回顯其值。未來擴充設定須在對應子規格明訂。

以下命令只示範非機密參數，須等產品專案建立後才可執行：

```sh
dotnet run --project src/AIAgent.Cli/AIAgent.Cli.csproj -- --help
dotnet run --project src/AIAgent.Cli/AIAgent.Cli.csproj
dotnet run --project src/AIAgent.Cli/AIAgent.Cli.csproj -- --workspace "/example/my workspace"
```

### 3.3 工作區選定

| 編號 | 具體要求 |
|---|---|
| BOOT-008 | 工作區必須是既有、可存取的目錄；檔案、壞連結、不存在、無權限或解析循環均回傳受控啟動錯誤。不自動建立目錄。 |
| BOOT-009 | 先正規化完整路徑，再逐段解析根路徑及祖先的符號連結，取得實際根目錄並固定至本次程序結束。不得只做字串前綴比對或只解析最後一段連結。 |
| BOOT-010 | 驗證目錄可開啟列舉，但不收集或顯示其項目，也不讀取任何工作區檔案。確認後、Key 輸入前再驗證固定根目錄可存取；若已消失或不可存取則停止。後續工具仍須另做每次操作檢查。 |
| BOOT-011 | 顯示解析後完整工作區、模型名稱或「尚未設定模型」，以及「本機聊天與選取的檔案內容可能傳送至 Gemini；本階段尚未提供檔案工具」提示，再詢問 `使用此工作區？[y/N]`。 |
| BOOT-012 | 確認輸入去除首尾空白後，只有不分大小寫的 `y` 或 `yes` 同意；其他輸入、空白、EOF 或 Ctrl+C 均結束啟動，不提示 Key、不改檔、不發送 API 請求。切換工作區須重新啟動。 |

這項根目錄固定不是作業系統沙盒，也不保證目錄之後不會被外部程序更動。工具目標、沿途符號連結、敏感檔案及工作區邊界的完整檢查由第 04 份負責。

## 4. Key 輸入與生命週期

### 4.1 隱藏輸入規則

| 編號 | 具體要求 |
|---|---|
| BOOT-013 | Key 僅由互動終端機讀取，字元及遮罩星號均不回顯；不加入 shell、應用輸入歷史或剪貼簿。不得主動讀取剪貼簿。 |
| BOOT-014 | 輸入支援 Backspace 與 Delete 刪除最後一個已輸入字元，空緩衝區刪除無動作；Enter 完成。拒絕空白結果並重新提示，不使用 Key 前綴或固定格式推斷授權。 |
| BOOT-015 | 接受可列印 ASCII 字元，去除完成輸入的首尾 ASCII 空白後檢查非空；最大 512 個字元。超限或收到不允許的控制／非 ASCII 字元時，將本次輸入標記無效並持續隱藏消耗至 Enter，再整筆丟棄重試，不能把截斷內容當成 Key。方向鍵等導覽鍵不修改內容。 |
| BOOT-016 | Key 輸入時 Ctrl+C 取消啟動；在 macOS 將 Ctrl+D 視為輸入結束，即使已有部分輸入也丟棄。底層讀取遇到 EOF、終端機中斷或 I/O 失敗時不得採用部分 Key。 |
| BOOT-017 | 標準輸入或輸出被重新導向，或終端機不支援無回顯輸入時，於工作區互動與 Key 提示前停止，回報需互動終端機；不得退回 `ReadLine` 明文讀取。單獨 `--help` 可重新導向。 |
| BOOT-018 | 成功只顯示固定訊息，不顯示 Key、片段、長度或指紋。建立 session 後清除輸入緩衝區及不必要參考；結束或失敗時釋放 session 參考。不得宣稱受控記憶體的每份複本立即歸零。 |
| BOOT-019 | 禁止將 Key 寫入設定、環境、暫存、日誌、歷史、測試快照、傾印或匯出檔；禁止由參數、檔案、安全儲存或環境變數取得 Key。只允許後續 Gemini 轉接器將原值用於官方 HTTPS 認證。 |
| BOOT-020 | 本份不做網路認證或測試生成。第 02 份以第一個正常生成請求檢查認證；認證失敗時提供重新輸入或退出，不自動重送原訊息。 |

512 是本地輸入資源上限，不是 Google Key 格式宣告。一般終端機貼上可列印字元可接受；含換行的貼上可能等同按 Enter，不提供多行 Key 或密碼管理器整合。不得為此開啟按鍵記錄。

### 4.2 重新輸入與交接

第 01 份提供可再次呼叫的 Key 輸入元件，但不實作 API 認證錯誤選單；該流程由第 02～03 份整合。重新輸入前，先將錯誤轉為固定分類並完成必要遮蔽，停止當前請求；重新輸入期間舊 Key 仍由原 session 的遮蔽政策保護，新的輸入不得進入輸出。

新 Key 成功取得後，以新 session 一次替換認證資料與比對政策，清除對話 Context 並丟棄舊 session 的 Key 參考及可能含舊值的原始 SDK 資料。若取消重新輸入則退出，不能以舊 Key 繼續送出。工作區與既有用量累計保留；不保存舊 Key 清單，不把舊錯誤物件留到政策切換後才顯示。

## 5. 最小介面與共用敏感資訊約定

### 5.1 核心契約

以下是未來 C# 契約的最小形狀，不是已建立的程式碼。實作可增加建構驗證與內部輔助型別，但不可改變此處的行為或將 Key 放進可自動列印屬性的 record。

```csharp
public sealed record BootstrapOptions(string StartupDirectory, string? WorkspaceArgument);
public sealed record PublicSettings(string? Model);
public sealed record ConfirmedWorkspace(string RootPath);

public enum BootstrapStatus { Ready, Help, Declined, EndOfInput, Cancelled, Failed }
public enum KeyInputStatus { Accepted, EndOfInput, Cancelled, Unavailable }
public enum ContentBoundary { ModelInput, ToolArguments, ToolResult, WriteCandidate }

public interface IBootstrapInteraction
{
    ValueTask<bool> ConfirmWorkspaceAsync(
        ConfirmedWorkspace workspace, PublicSettings settings,
        CancellationToken cancellationToken);
    ValueTask<KeyInputResult> ReadKeyAsync(CancellationToken cancellationToken);
    void ShowStatus(string safeMessage);
}

public interface ISensitiveDataPolicy
{
    bool ContainsCurrentKey(string content);
    bool CanSendOrUse(string content, ContentBoundary boundary);
    string RedactForDisplay(string content);
}
```

`KeyInputResult`、`SessionSecrets`、`BootstrapSession`、`BootstrapResult` 使用一般密封類別：

- `KeyInputResult` 具有 `Status`；只有 `Accepted` 能在內部轉移 Key 所有權，其他狀態不得帶部分內容。覆寫 `ToString()` 為固定安全文字。
- `SessionSecrets` 不公開 Key 屬性，不支援序列化；只提供內部認證存取與政策比對，`ToString()` 固定回傳 `[REDACTED]`。釋放後存取認證應失敗；清除自有緩衝區並丟棄參考，不承諾清除 SDK 或 runtime 複本。
- `BootstrapSession` 包含 `Workspace`、`Settings` 及內部 `SessionSecrets`；不提供全物件傾印，負責釋放 Key。`BootstrapResult` 只有 `Ready` 可帶 session，失敗只帶固定錯誤碼，不帶原始例外或輸入。
- `Bootstrapper.StartAsync(string[] args, CancellationToken cancellationToken)` 回傳 `ValueTask<BootstrapResult>`；相依服務透過建構式注入。CLI 擁有 session，後續 Gemini 轉接器只在認證範圍內取得必要存取。

終端機轉接器在輸入期間同步處理按鍵亦可，不為非同步介面額外啟動長駐背景工作。取消必須解除終端機事件處理並恢復原輸入狀態。

### 5.2 共用敏感資訊規則

| 編號 | 規則與責任 |
|---|---|
| BOOT-021 | 當前 Key 以 `StringComparison.Ordinal` 進行完整子字串精確比對，包含重複及嵌入其他文字的出現；空 Key 不得建立政策。不做大小寫摺疊、解碼或變形猜測。 |
| BOOT-022 | 模型輸入、工具參數、工具結果與候選寫入含當前 Key 時整次拒絕，不以遮蔽後內容繼續原操作。錯誤只回傳固定原因。第 01 份測試政策；第 02～06 份在實際資料邊界呼叫。 |
| BOOT-023 | 顯示內容中所有當前 Key 出現均替換為 `[REDACTED]`，包括模型輸出、狀態、工具失敗、預覽及錯誤。模型回應含 Key 時，後續模組須先阻止原文進入可續傳 Context，再中止當前鏈並依總規格第 9 章收尾。 |
| BOOT-024 | 結構化診斷欄位採名稱不分大小寫比對：`Authorization`、`Proxy-Authorization`、`x-goog-api-key`、`ApiKey`、`api_key`、`key`、`access_token`、`refresh_token`、`client_secret`；整個值替換為 `[REDACTED]`，含巢狀物件及陣列。文字輸出仍須再做當前 Key 比對；不以正規表示式聲稱能完整解析任意錯誤文字。 |
| BOOT-025 | 不直接輸出例外 `Message`、`ToString()`、原始 SDK 回應、HTTP 標頭、完整 URL 或設定傾印；錯誤先映射固定碼與受控訊息。禁止將原文放到日誌、快照或測試斷言失敗訊息。 |
| BOOT-026 | Key 只限認證，不放入 URL、模型訊息、工具參數或一般請求 body。內容檢查不得阻擋必要的 HTTPS 認證欄位。第 02 份另驗證 SDK 實際傳輸。 |

已知欄位遮蔽以結構化欄位名稱為依據，實作可使用政策的內部輔助方法；`RedactForDisplay` 只保證當前 Key 的文字替換，不保證識別任意文字中的其他憑證。前述精確比對也不涵蓋經切割、編碼或變形的秘密。範例與測試只用 `YOUR_GEMINI_API_KEY` 或明確無認證功能的假資料。

在取得 Key 之前，啟動錯誤不輸出原始參數、JSON、例外與環境資料。尚未知的 Key 無法依當前值遮蔽，因此更不能傾印使用者輸入。

## 6. 啟動流程與退出結果

1. 捕捉啟動目錄，解析參數；單獨 `--help` 直接顯示用法退出。
2. 載入並驗證應用程式設定，檢查互動終端機能力。
3. 解析、驗證並固定工作區根路徑。
4. 顯示工作區、模型與資料傳送提示；取得明確確認。
5. 重新檢查工作區可存取，隱藏輸入 Key；無效輸入留在該步重試。
6. 建立 session 與敏感資訊政策，回傳 `Ready`。
7. 第 01 份獨立執行時只顯示「啟動檢查完成；聊天功能尚未接入」，立即釋放 session 後正常退出；第 02～03 份整合後改由後續模組接手。不得顯示「認證成功」。

| 結果 | 退出碼 | 要求 |
|---|---|---|
| `Ready`（本份獨立執行）、`Help`、`Declined`、`EndOfInput` | 0 | 正常完成或使用者結束，不發送 API 請求。 |
| `Cancelled` | 130 | Ctrl+C 或取消訊號；丟棄部分 Key、釋放 session。 |
| `Failed`：參數或設定錯誤 | 2 | 顯示固定原因，不繼續互動。 |
| `Failed`：工作區、終端機或 I/O 問題 | 1 | 顯示受控錯誤，不降級為不安全的輸入方式。 |

介面中的 `ConfirmWorkspaceAsync` 回傳 `false` 表示不同意；EOF 由轉接器拋出不帶原始輸入的內部 `BootstrapEndOfInputException`，由 Bootstrapper 轉為 `EndOfInput`，不可混成 I/O 故障。取消一致由 `OperationCanceledException` 映射 `Cancelled`。`ReadKeyAsync` 的 `Unavailable` 映射終端機／I/O 失敗。

## 7. 版本控制、提交與分享檢查

| 編號 | 要求 |
|---|---|
| BOOT-027 | 沿用[現有排除設定](../.gitignore)：`.env`、`.env.*`（排除例外為 `.env.example`）、Key 檔名、`*.secrets.json`、`appsettings.Local.json`、敏感目錄、日誌、傾印及 .NET 建置產物。不把忽略目錄當成允許 Key 落地的位置。 |
| BOOT-028 | 提交前人工檢查未追蹤、已追蹤修改及實際暫存內容；分享前檢查文件、截圖與附件；發布前檢查產物。一般原始碼與文件也可能含秘密，不只檢查敏感檔名。 |
| BOOT-029 | 清楚區分 Git 忽略與工具權限：`.env.example` 可追蹤，但只能放佔位值，也不代表工具可讀取。`.gitignore` 不會解除追蹤、清除歷史或阻止強制加入；自動掃描／提交阻擋交給第 07 份。 |

人工檢查命令供後續開發者在本機使用，輸出可能含敏感內容，不得直接複製到聊天、Issue、PR 或報告：

```sh
git status --short
git ls-files --others --exclude-standard
git diff --name-only
git diff --cached --name-only
git diff
git diff --cached
git diff --check
```

前兩種 diff 不涵蓋未追蹤檔案內容；須在本機逐一檢視這些檔案。使用名稱清單先縮小檢查範圍，確認安全後才分享結果。不得將真實 Key 寫進搜尋命令列來驗證是否洩漏。

若發現疑似洩漏，停止分享，先撤銷或更換 Key 並檢查用量；版本歷史與其他副本另行確認處理範圍。不因加入忽略規則便宣稱修復完成。不執行歷史改寫或批次刪除；需清理測試檔時只針對明確單一路徑，若需批次清理則交由使用者手動處理。

## 8. 驗收條件與測試案例

### 8.1 待實作後執行的自動驗收

測試以假的互動服務、假的設定／路徑服務與隔離暫存目錄驗證；使用 `FAKE_KEY_FOR_TESTS_ONLY` 等無認證功能的值，不存取真實憑證或呼叫 Gemini。失敗斷言只報案例識別碼、布林與固定錯誤碼，不傾印待測秘密內容。

| 案例 | 需求 | 情境與預期結果 |
|---|---|---|
| AC-001 | BOOT-001 | 產品與獨立 MSTest 專案可建置；以替代互動完成啟動，不要求 Console。 |
| AC-002 | BOOT-002～003 | `--help` 不讀設定或 Key；未知、重複、缺值、空值、額外參數及與 help 混用均退出 2；錯誤不回顯值。 |
| AC-003 | BOOT-004 | 未指定採啟動目錄，指定相對路徑依啟動目錄解析，含空白路徑可用；不改變目前目錄或自行展開變數。 |
| AC-004 | BOOT-005～007 | 只讀應用程式目錄的設定；工作區同名檔不生效；驗證正常 JSON、缺檔、權限、格式、重複／未知欄位與錯誤模型型別。`null` 只在本份骨架允許。 |
| AC-005 | BOOT-008～010 | 不存在、檔案、壞連結、循環及不可存取目錄失敗；以模擬驗證權限錯誤，不依賴測試程序是否具高權限。正常路徑、祖先與最後一段連結解析正確，不讀檔案內容。 |
| AC-006 | BOOT-011～012 | 完整顯示根路徑與資料提示；僅 y／yes 確認。空白、n、其他文字、EOF、Ctrl+C 不讀 Key；確認後目錄消失則停止。 |
| AC-007 | BOOT-013～016 | 用按鍵序列驗證 Enter、Backspace、Delete、空緩衝區、空白重試、前後空白去除；輸出不含字元、星號、長度或片段。 |
| AC-008 | BOOT-015 | 512 字元可接受，513 字元拒絕整筆；控制／非 ASCII 字元使整筆無效；消耗至 Enter 後重試，不採用截斷值。 |
| AC-009 | BOOT-016～018 | 部分輸入後 EOF、Ctrl+D、Ctrl+C、讀取失敗均不產生有效 session；重新導向／不支援隱藏輸入不降級為明文讀取，退出碼符合第 6 節。 |
| AC-010 | BOOT-018～020 | 成功只交接記憶體 session；退出與失敗均釋放參考，不新增設定、日誌或 Key 檔，不送 API；不把釋放測試當成證明所有記憶體歸零。 |
| AC-011 | BOOT-021～023 | 假當前 Key 嵌入、重複、單獨出現時均檢出；四種內容邊界均拒絕；顯示替換全部出現。不同大小寫不視為精確匹配，空 Key 無法建立政策。 |
| AC-012 | BOOT-024～026 | 已知憑證欄位的大小寫變化、巢狀及陣列值均完整遮蔽；固定錯誤不帶原始例外、JSON 或參數；認證用途與模型內容檢查分開。 |
| AC-013 | BOOT-027～029 | 暫存 Git 儲存庫中使用假資料驗證根目錄與巢狀忽略、`.env.example` 及一般文件可追蹤、已追蹤檔案不因忽略自動解除追蹤；不改本專案索引或歷史。 |

### 8.2 macOS 終端機人工驗收

| 案例 | 需求 | 操作與預期結果 |
|---|---|---|
| AC-014 | BOOT-011～018 | 在真實終端機確認路徑、輸入假 Key、刪除與重新輸入；畫面與終端機輸出均無 Key 或星號。按 Enter 後只顯示啟動完成，不宣稱認證。 |
| AC-015 | BOOT-015～017 | 分別測試長輸入、無效字元、貼上、Ctrl+C、Ctrl+D 與輸入／輸出重新導向；符合整筆拒絕、取消及不降級規則；退出後終端機正常回顯。 |
| AC-016 | BOOT-027～029 | 在本機檢查變更、暫存區、未追蹤檔及發布檔案清單；範例與報告僅有假值，無秘密或原始診斷。未做發布時標示「發布檢查未執行」。 |

### 8.3 後續整合驗收責任

| 案例 | 需求 | 負責階段與成功條件 |
|---|---|---|
| AC-017 | BOOT-007、020、026 | 第 02 份：缺實際模型時在讀 Key 前停止；第一個正常請求才驗證認證，無額外測試生成，Key 只出現在 HTTPS 認證。 |
| AC-018 | BOOT-018～025 | 第 02～03 份：模擬認證失敗、重新輸入成功／取消、SDK 錯誤含 Key、輸入／歷史／模型輸出含 Key；舊錯誤先安全處理，新 session 清除舊 Context，不自動重送。 |
| AC-019 | BOOT-021～026 | 第 04～06 份：真實工具邊界阻擋含 Key 的結果、參數及候選內容，預覽前已阻擋，不寫檔、不傳模型；不以政策單元測試取代實際串接。 |
| AC-020 | BOOT-027～029 | 第 07 份：整合提交與發布檢查，另定自動掃描與阻擋範圍；不把人工清單當作已安裝的 hook。 |

### 8.4 未來建置與執行命令

以下均須在產品與測試專案建立後，從儲存庫根目錄依序執行，目前未執行：

```sh
dotnet restore tests/AIAgent.Cli.Tests/AIAgent.Cli.Tests.csproj
dotnet build tests/AIAgent.Cli.Tests/AIAgent.Cli.Tests.csproj --no-restore
dotnet test --project tests/AIAgent.Cli.Tests/AIAgent.Cli.Tests.csproj --no-build
dotnet run --project src/AIAgent.Cli/AIAgent.Cli.csproj --no-build -- --help
dotnet run --project src/AIAgent.Cli/AIAgent.Cli.csproj --no-build
```

測試專案須參考產品專案，讓上述 build 同時建置產品。採 .NET 10 MSTest 範本的 Microsoft.Testing.Platform 模式，實作時固定使用的套件版本並記錄於專案檔；若 SDK／範本造成命令差異，先更新本節再驗收，不直接宣稱原命令通過。

本份獨立完成需通過 AC-001～AC-016；AC-017～AC-020 以交接責任列管，不計為本份已測。紀錄實際命令、退出碼、結果與未測項目；自動測試不代表真實終端機、Google 授權、模型可用性或其他平台已驗證。

## 9. 本次文件檢查與後續延伸

本次只核對文件的目的、範圍、流程、責任、驗收與延伸齊全，Markdown 連結及程式碼區塊閉合、需求與案例對應、與總規格第 16 章的階段依賴一致，以及 `git diff --check` 與變更範圍。新檔尚未追蹤時需另檢查內容及空白，不能只靠一般 `git diff`。

未執行產品建置、MSTest、終端機隱藏輸入、執行期防護測試或真實 Gemini API 驗證。第 8 節列的是待執行驗收，不是測試報告。

下一份應展開第 02 份 Gemini 子規格，定案模型、SDK、單一 API 模式並串接本文件的 session 與敏感資訊契約。環境變數讀 Key、作業系統安全儲存、記住 Key、多模型、GUI 與背景任務皆需另訂規格，不自動納入本份。
