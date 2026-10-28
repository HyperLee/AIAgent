# 04：工作區與唯讀工具子規格

## 第 19 份產品實作接續（2026-10-28）

文件擷取只使用 ExternalRead；URL 准入先於該筆工作區來源查核，外部正文與來源資料不建立讀取／修改快照，也不擴大 Scope 或本機檔案權限。 詳見[第 19 份](19-web-search-and-document-fetch.md)及其固定交接；當次分層證據與未驗項目見 [README](../README.md#web-validation)。

## 第 18 份產品實作接續（2026-10-28）

外部工具共用排他操作租約與目前 Rules／Scope／Skills 閘門；工作區操作候選綁 MCP 戳記。MCP 不呼叫一般 ReadFileAsync、不取得檔案權限或建立修改快照；已開始發布的本機結果先查核與入帳再撤確認。 詳見[第 18 份](18-mcp-external-tools.md)及其固定交接；實際驗證與未驗層級見 [README](../README.md#mcp-validation)。

## 第 17 份產品實作接續（2026-10-27～28）

新增 ISkillsWorkspace 專用安全來源讀取，共用原安全核心、規則邊界及排他租約；不建立 read_file 快照／Plan 事實。技能啟停／失效清除修改基準；原敏感、種類、連結與深度限制不放寬。詳見[第 17 份](17-reusable-workflows-and-skills.md)及[當次分層驗證](../README.md#skills-validation)。歷史文件交付敘述保留，未結原生／真人／真實 Gemini／Release 門檻不變。

## 第 16 份產品實作接續（2026-10-27）

完整 read_file 基準與 checkpoint 的 before／after 分開持有；list／show／forget 不讀目標。復原成功、Unconfirmed 或已觀察衝突清目標修改快照，不把復原 bytes 發布成新 read_file 基準；根換代使全部歷史只能人工查核。一般讀取路徑、敏感與範圍限制沒有擴張。 詳見 [第16份](16-change-checkpoints-and-recovery.md) 與 [當次分層證據](../README.md#checkpoint-validation)。下列原階段敘述保留歷史範圍。

## 第 15 份產品實作接續（2026-10-26）

[工作保存與續接](15-work-save-and-resume.md)的限定增量：`.aiagent`、實際私人根、交易鎖及工作暫存加入一般路徑拒絕；工作區選家目錄、祖先或私人根也不能經工具取得內容。檔案參照查核是共用租約下的用途限定唯讀入口，不呼叫公開 read_file、不建立快照或成功操作事實。 本節保留舊階段歷史，實際分層驗證見 [README](../README.md#work-save-validation)。

## 第 13 份產品實作接續（2026-10-26）

[第 13 份 Git 差異與程式碼審查](13-git-diff-and-code-review.md) 新增專用 GitRead 能力與三項唯讀工具，依 Plan／Execute 提供九／十二項；`/review` 為共用 Runner 與生成預算的新入口，該回合只允許三項 Git 工具。原十項工具的順序、輸入與成功資料保持，公開 LLM 契約與 SDK 訊息所有權保持。

Git 來源、覆蓋與報告不建立修改快照、Plan Read requirements 或 Verified／Validation 事實；一般 Git 探索與正式審查分開。僅審目前 Scope 的安全子集；忽略來源的祖先 metadata 例外不放寬一般 `.git`、跨 Scope 讀取或搜尋遇新規則邊界整次拒絕的契約。規則失效、共同重設及已發生操作事實仍依原契約收尾。

實作、當次分層證據與未驗證項目見 [README](../README.md#git-review-validation)。真人、真實 Gemini、第 10 份 test 不支援／未結原生驗收，以及第 07 份 Release／分發門檻分開保留。


## 第 12 份產品實作接續（2026-10-25）

新增專用規則讀取核心，共用安全路徑、普通檔案、UTF-8 與前後重驗；規則原始檔上限 16384 bytes。自動載入及重驗不建立修改快照或 Read／LocalOperation 事實。所有正式工作區入口另檢查目前 Scope 與新巢狀規則邊界，既有檔案／搜尋上限保留。

實作、當次離線／PTY 證據與未驗證部分見 [README](../README.md#project-rules-validation)。真人 RULE-AC-033、真實 Gemini 034、第 10 份未結原生驗收與第 07 份 Release／分發門檻分開保留。

> 文件版本：0.1｜建立日期：2026-10-21｜狀態：待實作規格
>
> 上位文件：[入門 AI Agent 開發總規格書](../SPEC.md)，主要依據第 6、11、13、15～16 章，並承接第 8～10、14 章的清除、工具及資料量約定。前置規格：[01：專案啟動與 API Key](01-bootstrap-and-key.md)、[02：Gemini 用戶端](02-gemini-client.md)、[03：CLI 與 Context](03-cli-and-context.md)。01～03 已有實作；目前能力以 [README](../README.md) 與現有程式為準，舊規格的「待實作」描述其撰寫當時的狀態。本文件完成不表示檔案工具或第二階段「可以讀檔」已完成。

## 1. 目的、交付邊界與完成標準

讓後續工具只能透過同一個工作區入口，按需列出第一層項目及完整讀取小型 UTF-8 文字檔。入口負責路徑、敏感資訊、符號連結、大小、檔案種類、取消與讀取快照；不能把模型要求當作存取授權。

本次交付只新增本檔，不修改 README、總規格、01～03 規格、程式、設定、測試、專案或套件；不建立其他規格或空實作，不安裝 hooks，不提交、推送或改寫歷史。以下契約、資料與命令均是後續開發要求，不是本次已實作或已通過的結果。

需求使用 `WS-xxx`，驗收使用 `WS-AC-xxx`；後續新增時接續編號，不重排既有編號。引用上游需求時保留 BOOT、GEM、CLI 前綴。

| 編號 | 具體要求 |
|---|---|
| WS-001 | 沿用既有 .NET 10 Console、Google.GenAI 1.26.1、MSTest 與 Microsoft.Testing.Platform；保留可空值分析、警告視為錯誤、既有啟動參數及非機密設定。不升級 SDK、不增加產品套件或設定欄位，先驗收 macOS。 |
| WS-002 | 本份只開發工作區入口、型別化列檔／讀檔操作、有限讀取快照及相關清除串接。正式聊天的 `LlmRequest.Tools` 仍為空，每個文字聊天回合仍至多一次生成；不為測試增加產品參數或本機讀檔指令。 |
| WS-003 | 不實作工具參數 Schema、ToolRegistry、ITool、AgentRunner、呼叫配對或模型工具循環；不建立、修改、刪除、重新命名檔案，不執行 shell，不讀 Office／PDF，不搜尋、遞迴、分頁、分段讀取、索引或自動載入整個工作區。 |
| WS-004 | 本份產品的獨立完成點為：真實暫存檔案與可控制檔案系統的離線驗收、快照生命週期及完整既有回歸通過，留下當次結果。自然語言列檔／讀檔與真實 Gemini 工具往返須等第 05 份串接，不計為本份已完成能力。 |

## 2. 既有基線、模組責任與跨階段交接

本次以原始碼確認下列基線，未重新執行既有 build、MSTest、PTY 或真人驗收，不沿用歷史測試數當作本次證據。

| 既有能力 | 本份接續方式 |
|---|---|
| `ConfirmedWorkspace(string RootPath)`、啟動專用 `IWorkspaceResolver` | 使用已確認的實體完整根路徑，不更換工作區、不改程序目前目錄。啟動允許解析根路徑的連結，不代表工具可跟隨工作區內連結。 |
| `BootstrapSession`、當前 `ISensitiveDataPolicy` | 工作區服務與快照綁定當前 session／政策；不另持有 Key 原值，不增加 Key 來源。 |
| `ChatApplication` 的 `/clear`、強制清除、換 Key 與退出 | 在相同清除路徑丟棄快照。第 03 份只有文字回合的保存規則保持不變。 |
| `LlmToolResult.Response` 與不透明模型訊息 | 本份只提供可安全轉為 Response 的資料投影；第 05 份才附加呼叫識別並回填模型。不重建、顯示或修改原始模型紀錄。 |
| Gemini 的 262144 bytes 實際請求上限 | 本份工具結果上限不能取代它；歷史、工具定義、簽章與多份結果仍可能使完整請求超限。 |

| 元件或階段 | 責任與界線 |
|---|---|
| WorkspaceFiles | 每次操作統一驗證、列舉／讀取、結果限量與快照發布；不使用 Console、不呼叫 Gemini。 |
| 工作區路徑及敏感規則元件 | 純路徑規則與檔案系統檢查分工；工具不得各自複製較寬鬆的規則。 |
| 工作區檔案系統轉接器 | 提供可替換的項目檢查、第一層列舉、普通檔案唯讀開啟及狀態重驗；與啟動用途的既有檔案系統介面分開，不把後者直接當成工具權限入口。 |
| 快照儲存與 session 擁有者 | 只存記憶體、限制容量、處理清除與 session 隔離；不能憑快照宣稱磁碟內容仍相同。 |
| 第 05 份 | 定案兩個工具的參數 Schema、登錄、參數解析、派送、呼叫與結果配對、完整工具回合、5 次生成上限與本機結果顯示。 |
| 第 06 份 | 沿用同一入口的限制，讀取有效快照、建立候選變更、預覽確認、重新讀取比對及寫入。本份不先加入寫入方法。 |
| 第 07 份 | 整體成本、真人服務流程、提交及發布檢查；不取代本份當下的資料量與敏感資訊檢查。 |

後續實作主要新增於產品的 `Workspace/` 與對應測試；只為快照生命週期調整必要的核心組裝及清除路徑。`Program.cs` 仍只組裝與啟動，既有 Key、模型與設定行為不可放寬。

## 3. 核心介面、結果與資源所有權

### 3.1 最小契約

以下是待實作的 C# 宣告形狀，不是本次加入的產品程式。所有新增契約預設為 `internal`，不擴大現有公開 API。

```csharp
internal interface IWorkspaceFiles : IAsyncDisposable
{
    IReadSnapshotStore Snapshots { get; }
    Task<WorkspaceListResult> ListFilesAsync(string path, CancellationToken cancellationToken);
    Task<WorkspaceReadResult> ReadFileAsync(string path, CancellationToken cancellationToken);
}

internal interface IWorkspaceFilesFactory
{
    IWorkspaceFiles Create(BootstrapSession session);
}

internal interface IReadSnapshotStore
{
    int Count { get; }
    long TotalBytes { get; }
    bool TryGet(string relativePath, out WorkspaceReadSnapshot? snapshot);
    void Clear();
}

internal enum WorkspaceStatus { Success, Rejected, Cancelled, Failed }
internal enum WorkspaceEntryKind { File, Directory }
internal enum WorkspaceErrorCode
{
    None, InvalidPath, OutsideWorkspace, SensitivePath, SymbolicLink,
    NotFound, WrongEntryType, AccessDenied, FileTooLarge, InvalidUtf8,
    BinaryContent, ResultTooLarge, SensitiveContent, PathChanged,
    WorkspaceUnavailable, IoFailure, Unavailable
}
```

| 編號 | 契約要求 |
|---|---|
| WS-005 | 工具入口只接受已確認工作區及當前政策，契約不公開 SDK、HTTP、Key、原始例外或工作區外的路徑。工廠不得讀檔案內容、列舉項目、生成或重新提示認證；建立服務不等於根目錄之後仍可存取。 |
| WS-006 | `WorkspaceListResult`、`WorkspaceReadResult` 各有唯讀 Status、ErrorCode 及可空 Data；只有 Success／None 可有完整 Data。Cancelled 的 ErrorCode 為 None；其他結果不得攜帶部分項目、文字、快照或原始失敗資料。 |
| WS-007 | `WorkspaceListData` 具有 Path、Entries、Truncated、SafetyFiltered；Entry 具有 Path、Kind。`WorkspaceReadData` 具有 Path、Content、ByteCount、HasUtf8Bom。Path 都是根目錄相對路徑；ByteCount 為含 BOM 的原始 bytes，不是 UTF-16 長度或 token 數。 |
| WS-008 | 含路徑、文字、位元組或結果的型別採密封類別、唯讀屬性及防禦性複製，`ToString()` 只回傳固定型別標記；清單不暴露可變陣列。快照原始 bytes 只透過回傳新副本的 `CopyOriginalBytes()` 取用，不提供可修改內部陣列的出口。 |
| WS-009 | session 擁有者經工廠建立並持有一份工作區服務；所有操作依序執行。第二個並行操作回傳 Failed／Unavailable，不排隊、不開始檔案 I/O。DisposeAsync 停止新操作、取消並等待進行中操作、清空快照並丟棄政策參考；重複釋放安全，釋放後操作為 Failed／Unavailable。 |

| 資料型別 | 必要唯讀欄位或操作 |
|---|---|
| WorkspaceEntry | `string Path`、`WorkspaceEntryKind Kind`。 |
| WorkspaceListData | `string Path`、`IReadOnlyList<WorkspaceEntry> Entries`、`bool Truncated`、`bool SafetyFiltered`。 |
| WorkspaceReadData | `string Path`、`string Content`、`long ByteCount`、`bool HasUtf8Bom`。 |
| WorkspaceReadSnapshot | `string RelativePath`、`long OriginalByteCount`、`bool HasUtf8Bom`、`string Sha256Hex`，以及 `byte[] CopyOriginalBytes()`；Sha256Hex 為原始 bytes 的 64 字元小寫十六進位 SHA-256。 |

結果對模型的投影格式由第 7～8 節固定，與第 05 份的輸入 Schema 不同。只允許明確欄位的 JSON writer 產生投影，不以一般物件序列化意外帶出快照、內部完整路徑或政策；不提供整份 session、服務或快照的 JSON 匯出。

`IReadSnapshotStore` 的 TryGet 僅供本機後續修改流程；它不做 I/O、不重新讀檔、不更新淘汰順序，也不接受跨工作區的完整路徑。不合法或未正規化的查詢回傳 false，不自動修正索引。快照只由 WorkspaceFiles 的成功發布流程新增或更新，外部沒有任意寫入快照的方法；第 06 份須從活動服務的儲存取得基準，不接受外部自行提交的快照作為目前 session 的授權基準。

## 4. 有限資源與結果大小

| 編號 | 具體要求 |
|---|---|
| WS-010 | 以下值為固定具名常數，初版不加入設定覆寫、環境變數或工具參數來放寬。KiB 為 1024 bytes，MiB 為 1048576 bytes；各限制獨立生效。 |
| WS-011 | 原始檔案大小按 bytes 計算，包含 BOM；最多實際讀取 MaxFileBytes 加 1 個 byte 以偵測增長。不可只信開啟前 Length，也不可先把無限內容讀完才驗證。 |
| WS-012 | 工具結果按第 7～8 節的完整 Response object 實際 UTF-8 JSON bytes 計算，包含狀態、欄位、路徑及 JSON 跳脫；196608 可接受，196609 即超限。不用可見字數、字串長度或原始檔案大小代替。 |
| WS-013 | 每次操作的記憶體與工作量須受路徑、列舉、讀取及結果上限約束。結果以有界記憶體序列化或邊寫邊計量，超限即停止；不建立磁碟暫存、日誌或無上限快取。取消訊號傳入可取消的 I/O，並於逐項列舉及資料發布前檢查。 |

| 常數 | 值 | 適用方式 |
|---|---:|---|
| MaxRelativePathCodeUnits | 4096 | 原始相對 path 的 UTF-16 code units 上限，正規化前檢查。 |
| MaxFileBytes | 131072 | 128 KiB，包含 UTF-8 BOM。 |
| MaxListEntries | 200 | 回傳的完整項目數，不是實際目錄總數。 |
| MaxScannedEntries | 4096 | 本次最多從列舉器取得的直接子項目，包含後續會隱藏的項目；不繼續取第 4097 筆。 |
| MaxToolResultBytes | 196608 | 192 KiB 的完整工具 Response object，不含外層呼叫 ID、工具名、SDK 與歷史封裝。 |
| MaxSnapshotCount | 20 | 一份服務內最多 20 個不同正規化相對路徑的快照，空檔也占一份。 |
| MaxSnapshotBytes | 1048576 | 快照原始 bytes 合計最多 1 MiB，不把它當作程序總記憶體保證。 |

資料投影採非縮排 JSON、固定欄位順序、標準 JSON 字串跳脫，非 ASCII 文字可直接以 UTF-8 編碼；不把 JSON 放進 HTML。具體 writer 使用 `JavaScriptEncoder.UnsafeRelaxedJsonEscaping`，以避免一般中文全部變成 `\uXXXX`，但引號、反斜線與必要控制字元仍須合法跳脫。此選擇只用於資料 JSON，不改變終端機安全顯示規則。

檔案未超過 128 KiB，仍可能因 JSON 跳脫超過 192 KiB 而拒絕。單份結果合格，也不保證加上其他訊息後能通過既有 Gemini 262144 bytes 請求上限；第 05 份沿用模型層的 InputTooLarge 收尾，不偷偷裁切內容、淘汰更多歷史、摘要或重試。

## 5. 路徑、工作區邊界與符號連結

### 5.1 純路徑規則

| 編號 | 具體要求 |
|---|---|
| WS-014 | path 必須是非 null、非空、合法 Unicode 的相對路徑；不 Trim、不 Unicode 正規化、不 URL decode，不展開 `~` 或環境變數。允許一般空白及純空白檔名，按原文字面尋找；根目錄必須使用 `.`，不以空字串代替。 |
| WS-015 | 拒絕 `/` 起始、Windows 磁碟機形式、UNC、反斜線、NUL、C0／C1 控制字元、U+2028／U+2029、未配對代理字元及超過 4096 UTF-16 單位的 path；錯誤為 InvalidPath。`C:notes.txt` 亦拒絕，避免日後跨平台解讀改變。 |
| WS-016 | `/` 是唯一分隔符；可移除中間的 `.` 及重複分隔符。任何 `..` 區段一律以 InvalidPath 拒絕，即使折疊後仍在根目錄內也不接受；不先消去可能藏有連結或敏感目錄的父目錄操作。尾端 `/` 表示目錄要求：列檔可用，read_file 拒絕，不把 `file.txt/` 默默改為檔案。 |
| WS-017 | 以固定根路徑建立完整候選，再以路徑區段確認它為根或其子路徑；不能只用 StartsWith(root)。若完整候選仍越界，回傳 OutsideWorkspace。成功結果統一輸出 `/` 分隔的正規化相對 Path，根為 `.`；不回傳候選完整路徑。 |

例如 `docs//./notes.md` 可轉為 `docs/notes.md`；`docs/../notes.md`、`../workspace-other/a.txt` 均拒絕。`~`、`$HOME`、`%HOME%` 可作為工作區內的字面名稱，但不會存取家目錄或展開任何變數。` notes.md ` 與 `notes.md` 是不同名稱，回傳及快照均不得去除其空白。

列檔回傳的每一個 Entry.Path 必須符合相同相對路徑規則，以便後續讀取；不合法、超長或含控制字元的項目名稱不回傳，不以轉義後的另一個檔名取代。正常檔案系統的大小寫及 Unicode 名稱解析由作業系統決定；敏感名稱另採第 6 節的保守比對。

### 5.2 實際檔案系統檢查

| 編號 | 具體要求 |
|---|---|
| WS-018 | 每次操作重新檢查固定根路徑與工作區內沿途目錄、目標的實際種類及連結狀態，不沿用上次 FileSystemInfo 的快取。目標、沿途任一連結或 reparse point 均拒絕，包括指向工作區內、外、壞目標或循環的連結；不解析後繼續存取。 |
| WS-019 | 先做純路徑及敏感規則，再做存在性、種類及存取檢查。中間區段須為目錄；list_files 目標須為目錄，read_file 目標須為普通檔案。FIFO、socket、字元／區塊裝置等特殊項目不得作為一般檔案開啟或試讀，列檔亦隱藏它們。 |
| WS-020 | 列舉／開啟前及結果發布前重驗根、沿途目錄與目標；讀取另比較開啟前後、讀完後可取得的種類、長度及修改時間。偵測消失、種類改變、連結替換或狀態不一致時，丟棄全部候選與該路徑快照，回傳 PathChanged 或根失效的 WorkspaceUnavailable，不重新解析到新位置或自動重讀。 |

工作區檔案系統轉接器須能區分普通檔案、目錄、連結與其他項目，不得用「存在且不是目錄」推論為普通檔案，也不得為確認 FIFO 種類先對它做可能阻塞的讀取。若平台無法可靠辨識，拒絕該種類；不得退回較寬鬆的任意讀取。

本份採應用層的前後檢查，不導入以目錄控制代碼逐段開啟的 macOS 專用競態強化。靜態或已偵測的受限路徑不得讀取內容；偵測到讀取期間變動時不得發布資料或快照。但檢查與開啟之間仍有時間差，不能保證阻擋惡意外部程序精確交換再還原路徑、偽造相同狀態，或保證競態期間從未暫讀另一個目標。這不是作業系統沙盒，也不保證識別硬連結的全部其他名稱或抵抗掛載變動；不得以重驗測試通過宣稱具備這些保證。

## 6. 敏感路徑、名稱與內容規則

| 編號 | 具體要求 |
|---|---|
| WS-021 | 下表為工具自己的固定拒絕清單，以每個完整路徑區段、OrdinalIgnoreCase 比對，不採子字串猜測。檢查工作區根路徑的區段及工具相對路徑的所有區段，與根／巢狀位置無關；命中即 SensitivePath，不因使用者選了敏感工作區便開放讀取。 |
| WS-022 | 敏感名稱不因實際種類、檔案存在與否、是否被 Git 追蹤或 `.gitignore` 例外而放行。直接存取受限路徑一律受控拒絕；列檔則隱藏受限項目，不顯示其名稱、連結目標、隱藏原因或數量。 |
| WS-023 | 操作 path、列舉取得的名稱、完整相對結果路徑及讀取文字均依既有政策檢查當前 Key；路徑／名稱使用 ToolArguments 或 ToolResult 的相應邊界，文字使用 ToolResult。任一命中時整次 SensitiveContent，不回傳遮蔽後的成功資料，不建立快照。 |
| WS-024 | 檔案內容只是資料，不執行其中指令、不自動讀取其提到的檔案、不切換工作區。敏感名稱清單與當前 Key 檢查不是通用秘密掃描，不宣稱能找出其他 Key、所有私鑰、編碼或變形的秘密；模型輸入層仍須再次檢查實際資料。 |

| 比對類型 | 完整規則 |
|---|---|
| 精確區段名稱，不分檔案／目錄 | `.git`、`.agents`、`.codex`、`.aws`、`.ssh`、`.secrets`、`secrets`。 |
| 環境檔名稱 | `.env`，以及以 `.env.` 起始的名稱；包含 `.env.example`，工具沒有例外。 |
| 精確檔名，亦不允許同名目錄作為繞過 | `gemini-api-key.txt`、`api-key.txt`、`appsettings.Local.json`、`credentials`、`credentials.json`、`id_rsa`、`id_dsa`、`id_ecdsa`、`id_ed25519`、`identity`。 |
| 完整名稱後綴 | `.secrets.json`、`.pem`、`.key`、`.p12`、`.pfx`、`.ppk`、`.keystore`、`.jks`；即相應的 `*.secrets.json` 等規則。 |

後綴採保守拒絕，可能包含只裝公開憑證的 `.pem`，本份不嘗試讀內容再判斷。`mysecrets`、`secrets-notes.md`、`.gitignore`、`appsettings.json` 不因名稱相似而自動拒絕，仍需通過其他檢查；允許讀一般 JSON 不代表允許把它當成 Key 或應用設定來源。

Git 排除與工具權限彼此獨立：沿用[現有 .gitignore](../.gitignore)，不解析它來決定可否讀檔，也不把所有被忽略的 bin、obj、logs 自動當作敏感名稱。它們仍受普通檔案、文字、大小及 Key 檢查。提交與發布掃描留給第 07 份，不在本份安裝 hook 或擴大 Git 排除規則。

## 7. list_files：第一層列檔

### 7.1 流程與截斷

1. 檢查取消、服務狀態、原始 path、當前 Key、工作區邊界及敏感規則。
2. 檢查根、沿途區段及目標，不跟隨連結；確認目標為可列舉目錄。
3. 建立第一層的惰性列舉器，最多取得 4096 個直接子項目；每一項先檢查取消及名稱中的當前 Key，再進行安全過濾與不跟隨連結的種類檢查。
4. 將安全項目轉為根目錄相對 Path，以 `StringComparer.Ordinal` 對完整 Path 升冪排序，最多保留 200 筆；不依種類分組或使用目前文化排序。
5. 建立完整 JSON 投影並檢查大小；必要時從排序後尾端移除完整項目，再以 Truncated 為 true 重算，不切斷名稱或 JSON。
6. 重驗目標與沿途狀態、檢查完整結果的敏感資訊及取消，再發布成功結果；不建立讀取快照。

| 編號 | 具體要求 |
|---|---|
| WS-025 | 只列第一層，Entry.Kind 僅 File／Directory。子目錄只回傳自己的相對 Path，不開啟其列舉器、不讀任何檔案內容，不跟隨符號連結。 |
| WS-026 | 敏感項目、連結、特殊種類及無法形成合法結果路徑的名稱均隱藏。SafetyFiltered 固定為 true，表示所有結果都套用安全過濾，不透露本次有無或多少受限項目。正常隱藏不算截斷。 |
| WS-027 | 因安全候選超過 200 筆、掃描取得第 4096 筆，或 JSON 容量而省略項目時，Truncated 為 true。掃描達 4096 即保守標示，無須取得第 4097 筆證明；不回傳總項目數或實際掃描／隱藏數量。 |
| WS-028 | 掃描未達上限時，輸出是整個安全集合排序後的前綴；達掃描上限時，只保證已觀察安全集合的排序前綴，不保證是整個目錄全域最前 200 筆，也不保證不同列舉順序下子集相同。不為排序先無限收集整個目錄。 |
| WS-029 | 列舉器、允許項目的種類檢查或重驗失敗時整次失敗，不回傳已收集的部分資料。空安全集合可成功；只有 Truncated 為 false 才表示這次已完整列出安全項目，仍不表示目錄沒有被隱藏的項目。 |

無法存取項目的屬性屬於受控失敗，不一律當作敏感項目悄悄略過；已知敏感名稱可在讀其目標屬性前隱藏。結果量測始終包含空清單與必要 envelope，不改寫 path 或漏掉必要欄位。在本份 4096 單位路徑與 192 KiB 結果限制下，合法路徑的空清單 envelope 可容納，不另要求建構此條件下不可達的超限案例。

### 7.2 結果資料投影

以下是後續預期資料，不是本次工具執行結果。固定欄位順序為 status、error_code、data；data 依序為 path、entries、truncated、safety_filtered；每筆 entry 為 path、type。type 為 `file` 或 `directory`。

```json
{"status":"success","error_code":"none","data":{"path":"docs","entries":[{"path":"docs/guide.md","type":"file"},{"path":"docs/samples","type":"directory"}],"truncated":false,"safety_filtered":true}}
```

回傳 `docs/guide.md` 而不是只回傳 `guide.md`，讓後續 read_file 可直接沿用。根目錄回傳 path `.`，其直接子項目不加 `./` 前綴。Truncated 是資料不完整的標記，第 05 份必須讓模型及本機狀態保留此事實，不能宣稱已列出整個目錄。

## 8. read_file：完整 UTF-8 文字讀取

### 8.1 流程與編碼

1. 檢查取消、服務狀態、原始 path、當前 Key、工作區邊界及敏感規則。取得合法正規化相對路徑後，先使該路徑的既有快照失效。
2. 檢查根、沿途與目標；目標須為普通檔案且唯讀可開啟。取得大小等操作前狀態，超過 131072 bytes 即拒絕。
3. 只以讀取權限開啟；確認可用狀態與前一步一致，最多讀到 131073 bytes。若觀察到超限則 FileTooLarge，不繼續讀完整檔案。
4. 檢查 BOM 與嚴格 UTF-8；只在開頭辨識並去除一份 UTF-8 BOM 作為顯示文字，保留原 bytes 供快照使用。
5. 檢查解碼文字的二進位指標及當前 Key；產生完整結果投影，驗證 JSON 大小及所有實際字串欄位。
6. 重驗路徑及檔案狀態、檢查取消與快照世代；全部通過後，在同一發布點更新快照並回傳完整 Success。

| 編號 | 具體要求 |
|---|---|
| WS-030 | 接受空檔、合法 UTF-8、中文與補充平面字元，支援開頭 bytes EF BB BF；ByteCount 包含 BOM，Content 不含該開頭標記。BOM-only 檔案成功且 Content 為空。檔案中間的 U+FEFF 按原文保留，不全域刪除。 |
| WS-031 | 使用 `UTF8Encoding(false, true)` 嚴格解碼；不以 U+FFFD 代換非法序列，不自動偵測並改讀 UTF-16／32、Big5 或其他編碼。開頭 UTF-16 BOM FF FE／FE FF、UTF-32 BOM FF FE 00 00／00 00 FE FF，以及過長編碼、截斷多位元組及無效 scalar 均為 InvalidUtf8；不可依副檔名跳過。 |
| WS-032 | 解碼後含 U+0000、除 Tab／LF／CR 以外的 C0 控制字元、DEL 或 C1 控制字元時，以 BinaryContent 拒絕。明訂此為保守的文字判準，不宣稱能識別所有二進位格式；不因內容可解碼就直接接受。 |
| WS-033 | Content 保留原始 CRLF、LF、單獨 CR、混合換行及檔尾有無換行，不 Trim、不正規化、不修改來源。工具不預先把原文字元轉成終端機顯示轉義，顯示仍由第 03 份安全純文字投影負責。 |
| WS-034 | FileTooLarge、InvalidUtf8、BinaryContent、SensitiveContent、ResultTooLarge、取消、I/O 或路徑變動均無部分成功；不得回傳前幾行、前 128 KiB、遮蔽後文字或新快照。不因失敗重讀、換編碼或自動拆分。 |
| WS-035 | 成功必須對應本次實際取得的完整 bytes，結果文字與快照由同一份已驗證 bytes 產生。只回報資料取得，不把歷史快照當作本次讀取，也不把讀檔成功描述為內容正確、已修改或已成功傳給 Gemini。 |

判斷先後為：原始大小、編碼、二進位判準、敏感內容、結果大小、最終狀態／取消。任何階段不得把原始 bytes、解碼失敗片段或例外原文帶出；後續階段已發現敏感內容時仍以固定安全結果處理，不回傳前面建立的候選。

### 8.2 結果資料投影

成功 data 固定依序為 path、content、byte_count、has_utf8_bom；不包含完整路徑、檔案時間、快照內容、快照容量或本機 SHA-256。

```json
{"status":"success","error_code":"none","data":{"path":"notes.md","content":"Hello\n","byte_count":6,"has_utf8_bom":false}}
```

這是預期資料範例；原始檔案必須實際是 6 bytes 的 `Hello` 加 LF 才符合它，不以範例代替驗收。寫入衝突需要的資料保存在本機快照，不在每份工具結果重複回傳原始 bytes。

## 9. 讀取快照、容量與清除

| 編號 | 具體要求 |
|---|---|
| WS-036 | 只有通過全部讀取、敏感檢查、結果限量、重驗及取消提交點的 read_file 才可發布快照。快照包含正規化 RelativePath、OriginalByteCount、HasUtf8Bom、原始 bytes 與其 SHA-256；指紋包含 BOM 及全部換行 bytes，只用於檔案資料，不使用 Key 指紋。 |
| WS-037 | 快照索引採正規化相對路徑的 Ordinal 比對；同路徑成功重讀取代舊快照並移至最新端。TryGet 不刷新順序，list_files 不新增或刷新快照。大小寫或 Unicode 不同的路徑不由本機快取猜測為同一名稱；即使磁碟指向同一檔案，也可占不同索引。 |
| WS-038 | 新增／更新前扣除同路徑舊份，再從最早成功讀取端淘汰，直到份數不超過 20 且原始 bytes 合計不超過 1048576。一次讀取最大 131072 bytes，可單獨放入容量；不能因快取滿而省略目前成功結果或把快照落地。 |
| WS-039 | 合法路徑的新讀取一旦開始，先移除其舊快照；之後失敗不得恢復它。無效參數及受限路徑不新增快照；根失效時清空整份儲存。失效、淘汰或 Clear 後的 TryGet 回傳 false，不能從對話文字或舊工具結果補回基準。 |
| WS-040 | `/clear`、任何模型 Context 強制清除、接受替換 Key 建立新 session 前、工作區服務釋放與程序退出，都須串接快照 Clear；換 Key 建立新服務，不更換舊服務的政策後繼續用快照。保留既有 Key／工作區／模型／用量規則，Clear 不刪檔、不取消已完成的磁碟變更。 |

快照是讀取時取得的基準，不是持續同步的磁碟事實。外部修改之後若尚未再次讀取，舊快照可能仍在；第 06 份每次修改仍須重新檢查路徑，將當下 bytes 與基準比對，並於確認後再次比對。不能僅比較長度、時間或相信模型聲稱「剛讀過」。快照缺失時先重新讀取，不以歷史回答取代。

清除與發布須有明確的世代／提交點：Clear 使先前世代的操作無法在稍後補回快照或發布成功；觀察到世代改變者按 Cancelled 收尾。正常 CLI 只在等待輸入時執行 `/clear`，仍以可控制操作測試這項約束，避免未來接入工具後出現遲到發布。

session 擁有者需將既有 Context 清除分支收斂到共同的對話狀態清除流程，包含 `/clear`、RequiresContextReset／非法結果、換 Key 及 finally。舊政策有效時先完成必要狀態處理；工作區服務取消／釋放完成後才釋放其 session，模型 client 亦須先於 session 釋放。不增加啟動掃描或檔案生成，`/help` 仍如實說明正式聊天尚無檔案工具。

Clear、淘汰及 Dispose 只清除自有參考，不承諾覆寫 runtime、字串、SDK 或已由本機呼叫者取出的所有複本。快照不寫入暫存、日誌、對話存檔或測試報告，也不跨 session 恢復。

## 10. 取消、失敗分類與唯讀保證

| 編號 | 具體要求 |
|---|---|
| WS-041 | 呼叫前取消不開始檔案 I/O，也不移除既有快照；合法路徑讀取流程開始後仍依 WS-039 先使舊份失效。列舉、讀取、解碼前後及發布前觀察到取消時，關閉列舉器／stream，回傳 Cancelled／None，無部分 Data 或新快照。發布點之前取消優先於遲到成功；發布後取消不回溯既有成功，亦不自動重做操作。 |
| WS-042 | 外部取消、服務釋放與操作完成透過單一生命週期提交點協調；不得讓 DisposeAsync 返回後還有活動 I/O、快照寫入或政策使用。預期檔案錯誤映射下表，不將原始例外交給 CLI 或模型；未預期本機錯誤按 Failed／IoFailure 安全停止該次操作。 |
| WS-043 | 只有列舉及唯讀開啟，不建立工作區目錄、暫存檔、鎖定檔、日誌、備份或檔案內容；不修改來源 bytes、權限或時間戳。作業系統可能因讀取更新存取時間，唯讀驗收比較內容及未新增／刪除項目，不承諾所有檔案系統 metadata 絕對不變。 |
| WS-044 | 失敗投影只含固定 status 與 error_code，不包含 data、原始 path、隱藏名稱、完整路徑、例外、stack trace、解碼片段或 SDK 資料。錯誤碼以固定 snake_case 對應 enum，不能使用作業系統例外文字作為代碼或訊息。 |
| WS-045 | 本份工作區狀態不是程序退出碼，也不自行要求換 Key。第 05 份依分類形成工具結果或停止鏈；正常聊天原有取消、認證與退出規則保持不變，不因工作區失敗額外生成或重試。 |

| 條件 | Status／ErrorCode | 受控意義 |
|---|---|---|
| 非法、過長、非相對語法或含 `..` 的 path | Rejected／InvalidPath | 路徑格式不接受；純路徑 API 的格式例外及作業系統的 PathTooLongException 亦映射此碼，不回顯例外。 |
| 完整候選不在固定根的區段邊界內 | Rejected／OutsideWorkspace | 目標越界，未讀取內容。 |
| 敏感區段、檔名或副檔名 | Rejected／SensitivePath | 目標受限，不回報其存在性。 |
| 目標或沿途符號連結 | Rejected／SymbolicLink | 不跟隨目標，不回傳連結內容。 |
| 初次檢查時不存在 | Rejected／NotFound | 工作區內目標不存在。 |
| 檔案／目錄種類不符或特殊項目 | Rejected／WrongEntryType | 只支援指定種類。 |
| 權限不足 | Failed／AccessDenied | 沒有取得完整結果。 |
| 原始檔案超過上限，包含讀取時增長 | Rejected／FileTooLarge | 未回傳部分檔案。 |
| 不符合嚴格 UTF-8，包括第 8 節列出的 UTF-16／32 BOM | Rejected／InvalidUtf8 | 不換編碼或代換字元。 |
| 命中保守二進位文字判準 | Rejected／BinaryContent | 不作為文字回傳。 |
| read_file 完整投影超限 | Rejected／ResultTooLarge | 未裁切檔案文字；列檔容量截斷另依第 7 節。 |
| path、名稱、內容或投影字串含當前 Key | Rejected／SensitiveContent | 整次拒絕，不帶原值或位置。 |
| 檢查後消失、被換成連結或可觀察狀態改變 | Rejected／PathChanged | 丟棄候選及該路徑基準，不自動重試。 |
| 固定根消失、不可用或已變成連結 | Failed／WorkspaceUnavailable | 清空快照，不重新選根或跟隨新位置。 |
| 其他預期或未預期本機 I/O 問題 | Failed／IoFailure | 只有固定安全狀態，無原始例外。 |
| 服務已結束或第二個並行操作 | Failed／Unavailable | 未開始新的 I/O。 |
| 發布前取消或快照世代已改變 | Cancelled／None | 無部分結果，不增加快照。 |

例如敏感路徑拒絕只投影如下；其他非成功結果採相同兩欄結構，status 為 `rejected`、`cancelled` 或 `failed`，error_code 為 enum 的固定 snake_case。

```json
{"status":"rejected","error_code":"sensitive_path"}
```

優先次序：已結束服務／並行拒絕不開始 I/O；執行中的外部取消於發布前優先。正常驗證順序先 Key 與語法／敏感規則，才問存在性與種類；根失效優先於工作區內 NotFound。完成初次檢查後目標才消失，不再假裝成初始 NotFound。

## 11. 工具資料離開邊界前的保護

| 編號 | 具體要求 |
|---|---|
| WS-046 | 除 path、名稱及完整文字外，發布前檢查完整 JSON 投影的實際欄位名稱與字串值，包含 entry path 及固定欄位；使用 ToolResult 邊界。不只搜尋跳脫後 JSON 原文，以免引號／反斜線讓精確比對失效。任何命中均整次拒絕，快照亦不得先發布。 |
| WS-047 | 後續本機顯示只接收受控狀態及通過檢查的資料，沿用 CLI-038 的完整區塊控制字元處理與 RedactForDisplay；模型送出仍沿用 GEM-021 的邏輯及實際 body 檢查。工作區層的成功不能豁免後面任一檢查。 |
| WS-048 | Key 比對繼續是當前值的 Ordinal 完整子字串，不新增公開認證存取、診斷傾印、原始檔案報告或提示匯出。文件、例子及測試不得包含完整應用指示、真實 Key 或憑證；只使用無認證功能的假值。 |

短 Key 可能與固定 JSON 欄位或狀態字面重疊。不得因此停用政策、改名繞過或把敏感投影送出；若固定失敗投影也無法通過送出邊界，第 05 份須停止鏈並顯示安全本機狀態，不無限嘗試產生另一份工具結果。錯誤顯示仍須做最後遮蔽，不能假設固定文字永遠不含當前 Key。

## 12. 需求對照與後續產品驗收

### 12.1 方法與證據

| 編號 | 具體要求 |
|---|---|
| WS-049 | 沿用 MSTest，以 acceptance-linked 名稱如 `WSAC006_RejectsNestedLinksWithoutReadingTargets` 建立測試。真實 UTF-8／BOM／連結／內容不變案例使用隔離暫存工作區；權限、取消、讀取增長、種類與時序競爭使用可控制檔案系統，不能只依測試程序的作業系統權限。 |
| WS-050 | 全部自動驗收離線，只用假 Key、假互動、假模型或既有假 HTTP handler；不讀本機真實憑證，不為工具測試連 Gemini。斷言只報案例 ID、狀態、數值及布林，不傾印內容、快照、原始終端機或例外；暫存測試資料保留，不使用遞迴或批次刪除。 |
| WS-051 | 分開記錄本份離線驗收、既有回歸、PTY、真人終端機及後續真實服務驗收。列出實際命令、退出碼、測試數、結果與未測項目；不能把本次文件檢查、案例表或 README 歷史紀錄當成本次產品通過證據。 |

### 12.2 第 04 份獨立離線驗收

| 案例 | 對應需求 | 情境與預期結果 |
|---|---|---|
| WS-AC-001 | WS-001～009、045、048 | 版本、專案及設定限制保留；工廠不掃描／讀內容／生成。契約不公開 SDK、秘密或原始例外；正式 Tools 為空、無產品讀檔捷徑或後續工具。 |
| WS-AC-002 | WS-006～008、036、046 | 成功／拒絕／取消／失敗資料形狀與 ToString 安全；改動原清單、byte 陣列或取出的副本不影響保留結果與快照，非成功狀態不帶 Data。明確 writer 不序列化快照或完整路徑。 |
| WS-AC-003 | WS-010、014～017 | null、空字串、4096／4097 UTF-16 單位、非法代理、控制字元、行分隔符、反斜線、絕對／UNC／磁碟機及 drive-relative 路徑均按規則處理；不得先觸碰內容。 |
| WS-AC-004 | WS-014～017、037 | `.`、中間 `.`、重複 `/`、尾端 `/`、空白及純空白名稱、中文、補充平面字元與字面 `~`／變數名稱：保留名稱、不展開、不 Trim，輸出可再輸入；檔案尾斜線不誤讀。所有 `..` 拒絕。 |
| WS-AC-005 | WS-017、020～022 | `../`、相似前綴 workspace-other、候選越界的內部檢查、敏感根與祖先區段：不讀外部內容、不暴露完整路徑、不更換根；不只做字串前綴比對。 |
| WS-AC-006 | WS-018～020、025 | 真實與假的目標連結、巢狀目錄連結、指向內／外、壞連結、循環及 reparse 類型：直接存取拒絕，列檔隱藏，不讀其目標或解析後繼續。啟動已解析的合法根仍可使用。 |
| WS-AC-007 | WS-019～020、029、042 | 不存在、種類相反、FIFO／socket 等特殊項目及不可存取目錄／檔案：分類正確；特殊檔案不為辨識而開啟，不產生阻塞試讀。以替身驗證權限，不依賴真人憑證或高權限程序。 |
| WS-AC-008 | WS-021～024 | 拒絕清單逐項覆蓋根／巢狀、大小寫變化、同名檔案／目錄及 `.env.example`；近似但未命中的一般名稱可讀。Git 追蹤／忽略狀態不改變權限，已知受限名稱先拒絕再問存在性。 |
| WS-AC-009 | WS-025～026、029、043 | 空目錄、只有隱藏項目、混合檔案／目錄：只回傳安全第一層，固定 SafetyFiltered；不提供隱藏數量、名稱或連結目標。替身記錄零檔案內容讀取、零子目錄列舉。 |
| WS-AC-010 | WS-025～028 | 199／200／201 筆安全項目與不同原始列舉順序：未達掃描上限時固定 Ordinal 排序、200 筆上限及 Truncated 正確；中文、大小寫與空白排序不受目前文化影響。 |
| WS-AC-011 | WS-010、013、026～028 | 4095／4096／超過 4096 筆，包含大量隱藏項目：最多取得 4096 筆、不取第 4097 筆，達限即 Truncated；只保證觀察集合排序，不宣稱全域完整或洩漏掃描數。 |
| WS-AC-012 | WS-012～013、027～029 | 長路徑／跳脫字元使列檔 JSON 超限：只刪排序尾端完整項目、保留合法 envelope、重新標示截斷，大小包含全部必要欄位；列舉或項目檢查中途失敗時無部分結果。不以超過 path 上限的輸入製造不可達的最小 envelope 案例。 |
| WS-AC-013 | WS-030、033、035～036 | 真實空檔、BOM-only、ASCII、中文、補充平面字元、內部 U+FEFF、CRLF／LF／CR 混合及無檔尾換行：Content、ByteCount、BOM、原 bytes 與指紋正確，來源完全不變。 |
| WS-AC-014 | WS-031～034 | UTF-16／32 BOM、Big5 非 UTF-8、過長編碼、代理編碼、截斷多位元組及損壞序列：InvalidUtf8，無替換字元、降級或部分內容，不因 `.txt`／`.json` 放行。 |
| WS-AC-015 | WS-019、032～034 | 含 NUL、ESC、DEL、C1 與其他禁止 C0 的合法 UTF-8 為 BinaryContent；Tab／LF／CR 可保留。使用安全假資料驗證，不宣稱所有二進位皆能辨識。 |
| WS-AC-016 | WS-010～012、030、034 | 131071／131072／131073 bytes，含 BOM 邊界及讀取中增長：合格且結果未超限者完整成功；超限不讀超過偵測所需 bytes、不截斷回傳。開啟前 Length 合格不能繞過實際上限。 |
| WS-AC-017 | WS-011～013、034～036 | 引號／反斜線造成投影擴大，建立實際 JSON 196608／196609 bytes 的邊界：前者可完整發布，後者 ResultTooLarge、無新快照。大小計算包括 envelope 與 path，不只是文字。 |
| WS-AC-018 | WS-020、029、034、039、042 | 可控制地在檢查後、開啟前、讀取中或發布前移除／替換目標、改為連結、改長度／時間：PathChanged，無候選發布或舊快照復活。根消失／換成連結為 WorkspaceUnavailable 且清空快照。 |
| WS-AC-019 | WS-023、034、046～048 | 假當前 Key 分別在 path、名稱、相對組合路徑、一般檔案文字與 JSON 跳脫字串：實際入口整次拒絕、無原文、無快照。不是只測政策函式；read_file 絕不回傳遮蔽後成功文字。 |
| WS-AC-020 | WS-023、046～048 | 列檔先前已有安全項目，稍後遇含 Key 名稱仍整次失敗；固定 JSON 欄位與短 Key 重疊亦拒絕。成功投影不能豁免模型邊界，失敗狀態與例外測試輸出均無原值。 |
| WS-AC-021 | WS-036～039 | 同路徑成功重讀更新 bytes／BOM／指紋並移至最新端；TryGet 及列檔不刷新順序。不同大小寫／Unicode 名稱依 Ordinal 索引，無磁碟讀取或跨路徑猜測。 |
| WS-AC-022 | WS-010、036～039 | 第 20／21 份、恰好 1 MiB 及超出容量、更新同路徑與空檔：先扣舊份再按最早讀取順序淘汰，Count／TotalBytes 正確，當前成功檔仍有快照，不落地。 |
| WS-AC-023 | WS-034、039、041 | 已有快照後，同路徑已進入合法讀取流程，再遇不存在、權限、編碼、大小、敏感、結果超限或取消：舊份失效且不恢復；呼叫前已取消則不移除既有快照。無效或受限參數不新增資料；淘汰／Clear 後 TryGet 為 false，不能由歷史補回。 |
| WS-AC-024 | WS-036、039～041 | 可控發布競爭：Clear 或取消已發生，讀取才遲到完成，不發布 Success 或快照；已成功提交後的取消不回溯，之後正常讀取可成功，無自動重讀。 |
| WS-AC-025 | WS-005、009、040、045 | 真正 ChatApplication 的 `/clear`、重複 clear、清除旗標及非法模型結果清除同步丟棄快照；Key／工作區／模型／用量保留，無檔案寫入或新增生成。仍拒絕模型工具呼叫、Tools 為空。 |
| WS-AC-026 | WS-005、009、040、042 | 換 Key 前清空舊 Context／快照、新服務綁新政策；舊服務不再接受操作。新 session／client／工作區服務建構或切換失敗均清理，取消／EOF／finally 退出亦釋放，不回退舊認證或保存舊內容。 |
| WS-AC-027 | WS-009、013、041～042 | 呼叫前取消為零 I/O；列舉／讀取／發布前取消關閉資源。並行第二操作與釋放後呼叫無新 I/O，進行中 DisposeAsync 取消並等待，重複釋放安全，政策不在 session 釋放後使用。 |
| WS-AC-028 | WS-042～044、047～050 | 存取及解碼例外帶假敏感內容時，只回固定分類；結果、stdout、stderr、ToString、斷言及明確 JSON 投影不含原文、完整路徑或提示內容。安全顯示仍沿用既有控制字元與遮蔽規則。 |
| WS-AC-029 | WS-003、024、043、049～050 | 真實暫存工作區於成功、拒絕、取消及錯誤前後逐一比較 bytes 與項目清單：無修改、建立或刪除。檔案內的切換目錄、讀憑證或執行命令等假指令只作資料，沒有額外操作。只允許測試自行準備／變更假資料，不把測試競爭造成的改動算作工具寫入。 |
| WS-AC-030 | WS-001～004、040、045、049～051 | 完整既有與新增離線測試回歸；必要時更新內部組裝替身，保留原 Key、模型、Context、取消及防洩漏案例。既有 Gemini body 超限仍受控，無外網、額外生成、產品捷徑或寫入能力。 |

WS-AC-018、024、027 的可控競爭只驗證指定時點與已觀察變動的收尾，不證明能阻擋任意外部競態。真假檔案系統驗收互補；不得只用假的完整結果代替實際路徑、解碼、結果 writer 與快照邊界測試。

### 12.3 PTY、真人與後續整合責任

| 案例 | 對應需求 | 完成界線 |
|---|---|---|
| WS-AC-031 | WS-002、040、045、050～051 | 後續第 04 份實作時執行既有 macOS PTY 回歸：聊天、clear、隱藏換 Key、取消及退出正常，終端機恢復；不新增檔案指令，也不因 PTY 通過宣稱已完成自然語言讀檔。 |
| WS-AC-032 | WS-002、040、051 | 真人終端機回歸另記：既有聊天、clear、換 Key／退出的互動提示仍正確，沒有假工具能力。測試專用 host 或假的終端機不能當真人證據；未做時明列未驗證。 |
| WS-AC-033 | WS-004、012、024、027、035、040、045～048 | 第 05 份才驗收模型要求真實列檔／讀檔、Schema 與參數、結果配對、多步／5 次上限、取消、截斷提示及後續 API 失敗時的本機結果。以使用者自行提供 Key 的真實服務操作另驗收，不由本份自動測試代辦。 |
| WS-AC-034 | WS-003、020、036～040、043、048、051 | 第 06～07 份才驗收快照缺失／過期後重讀、確認前後內容比對、BOM／換行保留、拒絕不寫、部分完成及整體成本／提交／發布檢查；本份快照不是寫入授權或提交掃描。 |

上游第 01 份 AC-019 的「真實讀取結果含 Key」由 WS-AC-019～020 承接；其中派送參數、寫入候選仍交第 05～06 份。第 02 份 GEM-AC-028 及第 03 份 CLI-025 的工具／快照交接，分別由本份讀取結果檢查與清除串接承接；未知工具、配對、完整工具回合及 5 次生成仍是第 05 份責任，不能宣稱整個上游案例已在本份完成。

### 12.4 後續開發驗收命令

以下在產品實作完成後，從儲存庫根目錄依序執行；build 與 test／run 不並行。本次文件交付未執行這些產品命令。

```sh
dotnet restore tests/AIAgent.Cli.Tests/AIAgent.Cli.Tests.csproj
dotnet build tests/AIAgent.Cli.Tests/AIAgent.Cli.Tests.csproj --no-restore
dotnet test --project tests/AIAgent.Cli.Tests/AIAgent.Cli.Tests.csproj --no-build
dotnet run --project src/AIAgent.Cli/AIAgent.Cli.csproj --no-build -- --help
python3 tests/terminal_smoke.py
dotnet format src/AIAgent.Cli/AIAgent.Cli.csproj --verify-no-changes
git diff --check
```

真人互動回歸另執行：

```sh
dotnet run --project src/AIAgent.Cli/AIAgent.Cli.csproj --no-build -- --workspace "."
```

不把 Key 放入命令、環境或檔案。第 04 份的離線產品完成條件為 WS-AC-001～030、完整既有回歸與格式檢查；PTY 另列 WS-AC-031，真人另列 WS-AC-032。WS-AC-033～034 是後續責任，不納入本份已測數。既有真人聊天／Gemini 驗收若仍未執行，保持該未驗證狀態，不由本份文件補作完成宣稱。

## 13. 本次文件檢查、完成條件與下一步

| 編號 | 具體要求 |
|---|---|
| WS-052 | 本次只檢查文件目的、範圍、流程、責任、介面、限制、失敗及驗收齊全，需求／案例可追溯、數值與契約一致、Markdown 連結／區塊／表格／空白正確，以及只新增本檔的變更範圍；不以文件檢查取代產品編譯或行為驗收。 |

本次文件案例獨立為 WS-AC-035，對應 WS-051～052，不計入產品驗收通過數：

| 案例 | 本次檢查與成功條件 |
|---|---|
| WS-AC-035 | WS-001～052 與 WS-AC-001～035 可追溯；上位規格及 01～03 的依賴／交接一致；相對連結存在、程式碼區塊閉合、無未定項或矛盾、限制值正確，既有檔案保持原內容，只新增本檔。 |

本次文件完成需逐項確認：

- 單檔 131072 bytes、列檔 200 筆／掃描 4096 筆、結果 196608 bytes、快照 20 份／1048576 bytes 及既有 262144 bytes 模型請求上限各自獨立，沒有用字元或 token 代替。
- 隱藏受限項目與 Truncated／SafetyFiltered 的意義一致；完整讀檔、JSON 超限拒絕、快照提交／失效及應用層競態限制前後一致。
- 正式聊天 Tools 為空；未越入第 05 份 Schema／派送／循環，或第 06 份寫入／確認，未新增 hooks、設定或其他檔案。
- 檢查 `git diff --check`；新檔未追蹤時另執行 `git diff --no-index --check -- /dev/null specs/04-workspace-and-read-tools.md` 並檢視實際內容。no-index 若因存在新增差異退出 1，需確認無空白錯誤，不能只憑退出碼判定失敗或通過。
- 記錄實際文件檢查結果；本次不執行 restore、build、MSTest、formatter、PTY、真人終端機或 Gemini 驗證，不宣稱已建立執行期防護。

下一步先依本份開發並驗收 WorkspaceFiles、讀取快照及清除串接，記錄當次結果與操作能力，再展開第 05 份工具循環規格。第二階段「可以讀檔」須等 04～05 完成及實際整合驗收；原生競態強化、硬連結隔離、可設定允許清單、搜尋、大檔分段、分頁、多工作區與其他平台都需另訂規格，不自動納入本份。

## 參考資料

以下官方文件於 2026-10-21 查閱；只用來確認相關 .NET API 的行為，不代表本次已編譯介面或完成平台安全驗收。

- [UTF8Encoding 建構式：啟用 throwOnInvalidBytes 及 BOM 行為](https://learn.microsoft.com/dotnet/api/system.text.utf8encoding.-ctor)
- [Encoding.UTF8：預設替換 fallback 與嚴格編碼選擇](https://learn.microsoft.com/en-us/dotnet/api/system.text.encoding.utf8?view=net-10.0)
- [FileSystemInfo.LinkTarget：連結目標與一般項目](https://learn.microsoft.com/en-us/dotnet/api/system.io.filesysteminfo.linktarget?view=net-10.0)
- [File.OpenHandle：讀取權限、分享與檔案選項](https://learn.microsoft.com/en-us/dotnet/api/system.io.file.openhandle?view=net-10.0)
