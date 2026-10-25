# 08：程式碼搜尋與局部讀檔開發規格書

## 第 13 份產品實作接續（2026-10-26）

[第 13 份 Git 差異與程式碼審查](13-git-diff-and-code-review.md) 新增專用 GitRead 能力與三項唯讀工具，依 Plan／Execute 提供九／十二項；`/review` 為共用 Runner 與生成預算的新入口，該回合只允許三項 Git 工具。原十項工具的順序、輸入與成功資料保持，公開 LLM 契約與 SDK 訊息所有權保持。

Git 來源、覆蓋與報告不建立修改快照、Plan Read requirements 或 Verified／Validation 事實；一般 Git 探索與正式審查分開。僅審目前 Scope 的安全子集；忽略來源的祖先 metadata 例外不放寬一般 `.git`、跨 Scope 讀取或搜尋遇新規則邊界整次拒絕的契約。規則失效、共同重設及已發生操作事實仍依原契約收尾。

實作、當次分層證據與未驗證項目見 [README](../README.md#git-review-validation)。真人、真實 Gemini、第 10 份 test 不支援／未結原生驗收，以及第 07 份 Release／分發門檻分開保留。


## 第 12 份產品實作接續（2026-10-25）

明示擴充 SEARCH-005、010 的專案規則邊界。搜尋進入安全、未忽略的目錄前檢查 AGENTS.md 候選，遇未載入邊界整次拒絕且不回傳部分成功；結果發布前重驗缺席觀察。第一層 list 不探查子目錄規則，原搜尋忽略、深度及工作量限制保持。

實作、當次離線／PTY 證據與未驗證部分見 [README](../README.md#project-rules-validation)。真人 RULE-AC-033、真實 Gemini 034、第 10 份未結原生驗收與第 07 份 Release／分發門檻分開保留。

> 文件版本：0.1｜建立日期：2026-10-23｜狀態：待實作規格
>
> 選題依據：[未來開發大綱 FUT-01](../ROADMAP.md)。上位文件：[開發總規格](../SPEC.md)第 5、9～15 章；承接 [04：工作區與唯讀工具](04-workspace-and-read-tools.md)、[05：Agent 工具循環](05-agent-loop.md)、[06：檔案編輯與核准](06-file-edit-and-approval.md)及 [07：預算與整體驗收](07-budget-and-acceptance.md)。目前能力以 [README](../README.md)及原始碼為準，既有文件的「待實作」描述其撰寫當時狀態。本份完成不表示 FUT-01 功能、產品測試或第 07 份未結驗收已完成。

## 1. 目的、範圍與交付界線

讓使用者描述功能、錯誤或符號名稱後，Agent 能以檔名／路徑樣式定位檔案，跨檔搜尋文字，取得命中行號與附近內容，再讀取指定行範圍，減少逐層列檔及回傳無關全文的成本。局部輸出減少模型資料量，不放寬本機完整檔案檢查。

本次交付僅新增本檔；不修改程式、測試、設定、專案、套件、README、ROADMAP、總規格或既有子規格，不建立額外計畫、空實作、工作樹或 hook，不變更 Git 索引／歷史，不提交或推送。下述宣告、工具、輸出、案例及產品命令均屬後續開發要求。

需求使用 `SEARCH-xxx`，驗收使用 `SEARCH-AC-xxx`；新增時接續編號。引用既有需求保留 WS、LOOP、EDIT、BUD 前綴。FUT-01 是 ROADMAP 項目代號，本份的 08 是新增子規格編號，兩者不代表其餘 FUT 項目必須依號碼開發。

| 編號 | 具體要求 |
|---|---|
| SEARCH-001 | 首版只新增 `search_files`、`search_text`、`read_file_range` 三項唯讀工具，涵蓋有限 glob、跨檔單行字面搜尋、命中行號／附近內容及指定行範圍。搜尋起點是目錄，範圍讀取目標是單一普通檔案。 |
| SEARCH-002 | 本次只交付指定規格書；文件完成與產品開發完成分開計算，不以未執行的測試、範例或模型回答宣稱功能完成。 |
| SEARCH-003 | 沿用 .NET 10 Console、`Google.GenAI 1.26.1`、既有模型與 LLM 契約、MSTest／Microsoft.Testing.Platform；保留 nullable、warnings-as-errors、編碼及格式慣例。先驗收既有 macOS 平台，不新增套件、產品參數、設定欄位或認證來源。 |
| SEARCH-004 | 搜尋、分行及匹配在既有受控程式內執行，沿用 macOS 檔案系統轉接器的 `lstat`／`fstat`、唯讀 handle 及身分查核；不啟動 `rg`、Git、shell 或其他子程序，不建立索引、暫存、快取檔或背景掃描。 |
| SEARCH-005 | 不開發大型檔案串流／分段安全協定、正規表示式、語意／向量搜尋、語言伺服器、分頁／續接游標、自動專案規則、Plan 模式、建置測試執行、Git 工具、多 Agent 或其他 FUT 能力。 |

## 2. 現況、模組責任與既有契約的擴充

撰寫時已核對 README、工作區服務、工具 Registry、AgentRunner 及 CLI 本機結果顯示；未重新執行產品驗收。目前正式工具依序為 `list_files`、`read_file`、`create_file`、`replace_text`，已具備五次生成、五個完整歷史回合、完整讀取快照與逐次寫入核准。README 記錄第 07 份 Release 掃描受阻，真人、真實 Gemini 及人工分發檢查尚未完成；本份不接手解除這些發布門檻。

| 接續位置 | 後續開發責任 |
|---|---|
| `Workspace/` 的 WorkspaceFiles／路徑規則／檔案系統轉接器 | 統一路徑、安全列舉、完整安全讀取、匹配、分行、資源計量及最終發布；重用既有檢查，不另設繞過入口。 |
| `Tools/` 的工具、參數解析及明確 JSON writer | 新工具 Schema、嚴格參數、固定投影、受控錯誤及安全摘要。 |
| ChatApplication／Registry／AgentRunner | 同一 session 登錄七工具、循序派送、原生呼叫配對、有限生成及本機事實；搜尋不需要寫入核准。 |
| ToolExecutionResult／LocalToolOutcome／ChatDisplay | 新增可辨識的搜尋／範圍摘要，不把搜尋當列檔，也不把回傳片段 bytes 當作完整讀取量。 |
| ConversationContext／UsageAccumulator／Gemini 傳輸邊界 | 保留完整成功回合、逐次記帳、敏感檢查與實際請求 body 上限，不因搜尋而額外生成或摘要。 |

| 編號 | 擴充及相容性要求 |
|---|---|
| SEARCH-006 | 本份只擴充總規格第 11.6 節及 WS-003、EDIT-004 等先前排除的搜尋／局部讀取能力；既有 `list_files` 仍只列第一層，`read_file` 仍完整讀取並建立快照。兩者輸入、輸出、限制及寫入工具行為不變。 |
| SEARCH-007 | 正式工具順序固定為 `list_files`、`read_file`、`create_file`、`replace_text`、`search_files`、`search_text`、`read_file_range`；擴充 LOOP-010／011、EDIT-005 與 BUD-005 的工具集合限制。前四項的名稱／順序／Schema 保留，七項共同受每回合最多五次生成限制。 |
| SEARCH-008 | WS-010～013／BUD-014 的既有列檔限制不套作遞迴搜尋總預算；新工具適用第 6 節獨立上限。既有 196608 bytes 工具結果及 262144 bytes 實際模型請求限制仍有效，新工具另採更小的 65536 bytes 結果上限。 |
| SEARCH-009 | FUT-01 可獨立於其餘 FUT 項目開發；不要求先完成第 07 份全部真人／服務／發布待辦，也不代表解除其門檻。後續產品交付須按本份驗收，並保留第 07 份未結責任及既有 01～07 回歸。 |
| SEARCH-010 | 後續產品實作時同步更新能力說明、README、ROADMAP 與總規格的適用範圍／索引；01～07 保留歷史時態並由本節明示擴充。本次依文件交付界線只新增本檔，不提前修改其他文件。 |

## 3. 內部工作區契約與資料所有權

### 3.1 入口及結果

以下為後續加入既有 `IWorkspaceFiles` 的方法；不是新建第二份工作區服務，也不擴大公開 LLM API。

```csharp
Task<WorkspaceFileSearchResult> SearchFilesAsync(
    string path, string pattern, CancellationToken cancellationToken);
Task<WorkspaceTextSearchResult> SearchTextAsync(
    string path, string query, string filePattern, bool caseSensitive,
    int contextLines, CancellationToken cancellationToken);
Task<WorkspaceRangeReadResult> ReadFileRangeAsync(
    string path, int startLine, int endLine, CancellationToken cancellationToken);
```

| 型別 | 必要唯讀屬性 |
|---|---|
| WorkspaceFileSearchResult／WorkspaceTextSearchResult／WorkspaceRangeReadResult | `WorkspaceStatus Status`、`WorkspaceErrorCode ErrorCode`、相應可空 Data；結果規則同既有 WorkspaceReadResult。 |
| WorkspaceFileSearchData | `string Path`、`IReadOnlyList<WorkspaceFileMatch> Matches`、`bool Truncated`、`bool SafetyFiltered`。 |
| WorkspaceFileMatch | `string Path`；只表示普通檔案，不攜帶內容、大小或目錄項目。 |
| WorkspaceTextSearchData | `string Path`、`IReadOnlyList<WorkspaceTextMatch> Matches`、`bool Truncated`、`bool SafetyFiltered`。 |
| WorkspaceTextMatch | `string Path`、`int LineNumber`、`string Content`、`IReadOnlyList<WorkspaceContextLine> BeforeLines`、`IReadOnlyList<WorkspaceContextLine> AfterLines`。 |
| WorkspaceContextLine | `int LineNumber`、`string Content`；Content 不含該行換行分隔。 |
| WorkspaceRangeReadData | `string Path`、`int? StartLine`、`int? EndLine`、`int TotalLines`、`string Content`、`long FileByteCount`、`long ContentByteCount`、`bool HasUtf8Bom`、`bool HasMore`。 |

| 編號 | 契約要求 |
|---|---|
| SEARCH-011 | 新增契約及內容屬性維持 internal；由原 IWorkspaceFilesFactory 及同一 session／政策建立與持有。工廠不搜尋、讀內容、生成或索取 Key；所有新操作共用既有單一進行中操作限制，不開另一條並行讀取通道。 |
| SEARCH-012 | 只有 Success／None 可攜帶完整 Data；Cancelled／None 及其他非成功結果的 Data 為 null。成功零命中允許空 Matches，不能用 NotFound 表示零命中。Path 是正規化工作區相對路徑，不是相對搜尋起點或本機完整路徑。 |
| SEARCH-013 | 含路徑、內容或集合的型別使用密封類別、唯讀屬性、防禦性複製及固定型別標記 ToString；不使用會傾印內容的 record，不公開原始 bytes、身分、完整路徑、例外、政策或 SDK 紀錄。 |
| SEARCH-014 | 新結果由 WorkspaceResponseWriter 的相應 overload 明確投影，第 7～9 節的 JSON 為唯一模型資料形狀；不使用一般物件序列化，也不把本機摘要或快照當結果 JSON 的替代。 |
| SEARCH-015 | WorkspaceErrorCode 新增 `InvalidArguments`、`InvalidPattern`、`InvalidQuery`、`InvalidLineRange`；ToolErrorCodes 接受相應 snake_case 固定碼，其中 invalid_arguments 已存在。結構／型別與 context_lines 選項錯誤為 invalid_arguments，其餘依第 11 節；不把本機錯誤加入公開 LlmErrorCode。 |
| SEARCH-016 | ToolExecutionResult 與 LocalToolOutcome 新增可空 `WorkspaceDiscoverySummary DiscoverySummary`；其唯讀欄位為 `DiscoveryKind Kind`（FileSearch／TextSearch／LineRange）、`int ReturnedCount`、`int? StartLine`、`int? EndLine`、`int? TotalLines`、`long? ContentByteCount`。搜尋後四欄為 null；範圍使用實際值，空檔起訖為 null。只在新工具完整成功且投影可安全回填時保留；沿用 RelativePath／Truncated，舊 ByteCount／ReturnedEntries 不填入新工具統計。非成功或 WithoutResponse 不保留此摘要，既有寫入事實仍依第 06 份保留。 |

### 3.2 生命週期

新方法共用既有 `RunAsync`、活動操作世代及 DisposeAsync 協調機制。服務不擁有 Console、模型 client 或 session 的釋放責任；工具傳入同一回合 token。任何第二個並行操作或釋放後操作仍為 Failed／Unavailable，不排隊、不開始 I/O；取消與快照規則見第 10 節。

## 4. 工具 Schema、預設值與參數解析

### 4.1 search_files

用途描述固定為「遞迴搜尋指定工作區相對目錄中的安全檔名或路徑，使用有限 glob 樣式」。

```json
{"type":"object","properties":{"path":{"type":"string"},"pattern":{"type":"string"}},"required":["path","pattern"],"additionalProperties":false}
```

```json
{"path":"src","pattern":"**/*Service.cs"}
```

### 4.2 search_text

用途描述固定為「在小型 UTF-8 文字檔中搜尋單行字面文字，回傳行號及附近內容」。

```json
{"type":"object","properties":{"path":{"type":"string"},"query":{"type":"string"},"file_pattern":{"type":"string"},"case_sensitive":{"type":"boolean"},"context_lines":{"type":"integer"}},"required":["path","query"],"additionalProperties":false}
```

```json
{"path":"src","query":"ReadFileAsync","file_pattern":"*.cs","case_sensitive":true,"context_lines":2}
```

### 4.3 read_file_range

用途描述固定為「取得小型 UTF-8 文字檔的指定行範圍，不建立修改快照」。

```json
{"type":"object","properties":{"path":{"type":"string"},"start_line":{"type":"integer"},"end_line":{"type":"integer"}},"required":["path","start_line","end_line"],"additionalProperties":false}
```

```json
{"path":"src/Example.cs","start_line":10,"end_line":30}
```

| 編號 | 參數要求 |
|---|---|
| SEARCH-017 | Schema 透過既有 ParametersJsonSchema 傳遞；本機仍列舉全部 properties，精確、區分大小寫地檢查名稱、重複、必填、型別與額外欄位。不用 dictionary／最後值覆蓋接受重複欄位，沿用原生回應在 SDK 轉換前的重複欄位檢查。 |
| SEARCH-018 | 三工具都必填 path，不省略或以空字串代入根；根用 `.`。兩個 search 的 path 必須是目錄，read_file_range 必須是普通檔案且不接受檔案尾端 `/`。路徑字串不 Trim、正規化 Unicode、展開變數或 URL decode；沿用既有純路徑正規化及 4096 UTF-16 上限。 |
| SEARCH-019 | search_text 省略 file_pattern、case_sensitive、context_lines 時，分別使用 `**/*`、true、2；只有省略才取預設，null 不代表省略。context_lines 必須是 0～3 的 Int32，boolean 不轉數值。 |
| SEARCH-020 | 數值必須是 JSON 整數 token 且可由 TryGetInt32 取得；字串數字、小數形式如 1.0、指數形式如 1e0、超出 Int32 或 null 都為 invalid_arguments。start_line／end_line 的數值關係由工作區層按 invalid_line_range 驗證，計算行跨度時避免整數溢位。 |
| SEARCH-021 | query 為 1～1024 UTF-16 單位、合法 Unicode 的單行字串；拒絕 CR、LF、U+2028／U+2029、未配對代理及除 Tab 外的控制字元。純空白或 Tab 查詢合法，按字面搜尋；不 Trim、不做 Unicode 正規化、不支援跨行或正規式選項。 |
| SEARCH-022 | 參數結構／型別、重複欄位及 context_lines 範圍錯誤在工具層回傳 Rejected／invalid_arguments，零工作區呼叫；正確型別的空／非法 path、pattern、query 或行範圍交由工作區層受控拒絕，零檔案內容讀取。內部方法亦驗證自身參數，不把工具驗證當作唯一防線。 |
| SEARCH-023 | 沿用 Registry 對工具名、CallId、所有實際解碼參數的敏感檢查，包含未知及額外欄位；新工具不得記錄或回顯 query／pattern。敏感參數優先依既有 sensitive_arguments 停止與清除，不藉結構失敗回填含秘密的模型紀錄。 |

Schema 只固定欄位與基本型別；UTF-16 長度、有限 glob 及行範圍由程式驗證，不以 JSON Schema 的字元長度或模型是否遵守提示取代。

## 5. 搜尋樣式與固定忽略規則

### 5.1 有限 glob

| 編號 | 樣式要求 |
|---|---|
| SEARCH-024 | pattern／file_pattern 長度為 1～1024 UTF-16 單位；只接受合法 Unicode、`/` 分隔的相對樣式。拒絕絕對／磁碟機形式、反斜線、控制字元、U+2028／U+2029、空區段、`.`／`..` 區段及尾端 `/`，不自動修正；固定為 InvalidPattern。樣式不是實際 path，不用既有路徑正規化去消除其區段。 |
| SEARCH-025 | `*` 匹配同區段零個以上 Unicode scalars，`?` 匹配同區段恰一個 scalar；兩者不跨 `/`。`**` 只能獨占完整區段，匹配零個以上完整區段；`a**b`、`***` 拒絕。其他合法字元按字面處理，沒有跳脫、字元集合、brace、否定或 extglob 展開。 |
| SEARCH-026 | 無 `/` 的樣式與任何深度檔案的完整 basename 比對；有 `/` 則與相對搜尋起點的完整路徑比對。匹配採 Ordinal、區分大小寫，不使用目前文化或作業系統的大小寫假設；省略 file_pattern 等同 `**/*`。 |
| SEARCH-027 | glob 實作不得採無界或指數回溯，不以正規式執行任意輸入；解析及匹配受樣式／path 長度限制，長迴圈檢查取消。無索引或樣式磁碟快取，不為擷取全部路徑先無限載入工作區。 |

以 `path="src"` 為例：`*.cs` 可命中 `src/A.cs` 與 `src/Core/A.cs`；`Core/*.cs` 只命中該層；`**/*.cs` 包含根層 `src/A.cs`；`?.cs` 可以命中單一 emoji 檔名，但不把其代理字元對算成兩個字元。`[AB].cs` 只匹配字面檔名 `[AB].cs`，不等同 A.cs／B.cs。

### 5.2 固定忽略及安全過濾

| 編號 | 具體要求 |
|---|---|
| SEARCH-028 | 搜尋過程略過名稱完整匹配 `bin`、`obj`、`node_modules`、`.vs`、`.idea`、`dist`、`coverage`、`TestResults`、`logs`、`.cache` 的子目錄及其內容，採 OrdinalIgnoreCase。僅對目錄生效，同名普通檔案仍可匹配；不解析任何 .gitignore、不詢問 Git 追蹤狀態，也不略過所有點開頭名稱。 |
| SEARCH-029 | 明確傳入安全的上述目錄作為 path 時，允許搜尋該起點；其下符合清單的子目錄仍略過。例如 path=obj 可搜尋其直接文字檔，path=. 不走入 obj。此例外只適用產物忽略，不適用敏感路徑；不提供 include_ignored 或修改忽略清單的選項。既有 list_files／read_file／寫入不採這份產物忽略清單。 |
| SEARCH-030 | 沿用 WS-014～024 全部路徑／敏感規則。列舉取得名稱及組成的相對路徑先做當前 Key 檢查，再依合法路徑、敏感名稱、種類、忽略及 glob 過濾；敏感路徑、連結、特殊種類、不合法結果名稱不回傳，亦不讀其目標。SafetyFiltered 固定 true，不提供隱藏名稱、原因、數量或連結目標。 |

敏感名稱仍包括 `.git`、`.agents`、`.codex`、`.aws`、`.ssh`、`.env`／`.env.*`、私鑰及既有完整清單；`.env.example` 沒有工具存取例外。未被列舉或已在安全過濾處停止的子樹不會讀內容，不能宣稱已檢查其中的所有秘密。

## 6. 資源、工作量與截斷

| 常數／既有擁有者 | 固定值 | 計量及邊界 |
|---|---:|---|
| WorkspaceLimits.MaxFileBytes | 131072 bytes | 含 BOM 的原始單檔上限；讀取最多加 1 byte 偵測增長。search_files 不讀內容，故不按大小排除檔名結果。 |
| MaxSearchScannedEntries | 8192 | 一次搜尋跨全部已開啟目錄取得的直接子項目合計，包含之後被隱藏、忽略或不匹配者；不取得第 8193 筆。 |
| MaxSearchCandidateFiles | 512 | search_text 符合 file_pattern 的安全普通檔案，包含其後因大小／編碼／二進位而略過者；不處理第 513 個。 |
| MaxSearchReadBytes | 8388608 bytes | search_text 各候選的實際 ReadAsync 返回 bytes 合計；含 BOM、增長探測及最後判為不支援的檔案。 |
| MaxSearchDepth | 32 | 起點目錄深度為 0；深度 32 的直接檔案可檢查，深度 33 的目錄不進入。 |
| MaxSearchResults | 200 | search_files 的檔案筆數，或 search_text 的命中行筆數，非出現次數／附近行總數。 |
| MaxSearchPatternCodeUnits／MaxSearchQueryCodeUnits | 各 1024 | 原始樣式／查詢 UTF-16 單位，補充平面字元計 2。 |
| DefaultSearchContextLines／MaxSearchContextLines | 2／3 | 每筆命中前、後各自的附近行數。 |
| MaxRangeLines | 200 | 請求 end_line-start_line+1；先檢查請求跨度，再做 EOF 夾限。 |
| MaxDiscoveryResultBytes | 65536 bytes | 三個新工具完整 Response object 的實際 UTF-8 JSON bytes，包含 envelope、欄名、路徑及跳脫。 |
| 既有工具／模型上限 | 196608／262144 bytes | 前者為共同結果上限，後者為實際 SDK 完整請求；新工具 65536 不取代其餘邊界。 |

| 編號 | 上限及發布要求 |
|---|---|
| SEARCH-031 | 上述限制為具名固定常數，無設定、環境變數或工具參數可放寬。bytes、UTF-16 單位、行數、項目數與 token 分開計量，不互換成精確成本。 |
| SEARCH-032 | 全樹列舉達 8192、文字候選達 512 或實際讀取達 8388608 時停止擴大掃描，保守設定 Truncated=true，不為證明完整再取下一筆／開下一檔。深度界限遇到原本可進入的安全、未忽略子目錄時，略過該子樹並設 Truncated=true；正常安全／產物過濾不算截斷。 |
| SEARCH-033 | 每次讀取均計入總 bytes，實際 ReadAsync 要求長度不得大於剩餘預算；預算耗盡而尚未完成整檔驗證時丟棄當前檔案、不匹配、不保留其結果，停止並以此前完整候選形成截斷成功。剛好用盡仍保守標記截斷；零 bytes 的 EOF 探測不增加 bytes 計量，不以零長度讀取假裝證明 EOF。 |
| SEARCH-034 | 搜尋先對已完成的匹配結果依 Path Ordinal、再依 LineNumber 數值升冪排列；最多保留前 200 筆。觀察到第 201 筆匹配才因結果筆數設 Truncated=true；不因剛好 200 筆而捏造省略。搜尋仍在工作量限制內處理其他候選，不能因回傳已滿便略過其必要安全檢查。 |
| SEARCH-035 | 搜尋 JSON 超過 65536 bytes 時，從排序尾端移除完整 match 並設 Truncated=true，連同 envelope 重算；不得截斷 path、命中行、附近行或 JSON。可回傳空 Matches／Truncated=true，例如唯一一筆完整命中無法容納；此結果不可描述為無命中。read_file_range 超限則 ResultTooLarge，無部分 Data。 |
| SEARCH-036 | 未達工作量上限時，結果是完整受支援搜尋集合排序後的有限前綴；掃描截斷時只保證已觀察候選的排序，不保證全工作區最前 200 筆。不同列舉順序在完整掃描下須得到相同結果，工作量截斷時允許子集不同；不回傳總檔案數、掃描數、略過數或完整性百分比。 |
| SEARCH-037 | 目錄與候選收集、逐行匹配、附近內容及 JSON 計量均使用有界記憶體；最多保留受限目錄項目、目前一檔的完整內容及有界候選結果，不先保留全部檔案 bytes／所有命中。序列化沿用明確、非縮排 JSON writer 及既有安全跳脫，完整邏輯字串亦須敏感檢查。 |

符合本機 64 KiB 結果上限，仍可能因七工具定義、歷史、簽章與同批其他結果超過 256 KiB 實際請求上限；依既有 InputTooLarge 收尾，不另裁歷史、丟棄結果、摘要、改簽章、重送或重放工具。

## 7. search_files：遞迴定位檔案

### 7.1 列舉及一致性

1. 驗證服務／取消、參數及當前政策，確認工作區根、沿途區段與起點目錄。
2. 從起點深度 0 開始，以可取消的逐項檢查及有界列舉探索安全目錄；不跟隨連結，不讀任何檔案內容。
3. 每個普通檔案以相對搜尋起點的名稱／路徑匹配 glob；結果 Path 一律轉為工作區相對路徑。
4. 在工作量限制內保留排序前綴及結果大小，追蹤必要路徑觀察，完成安全重驗後發布。

| 編號 | 具體要求 |
|---|---|
| SEARCH-038 | 每個目錄進入前、列舉後及最終發布前重驗根與已走訪目錄身分／種類；普通檔案結果也重驗其已觀察狀態。初次觀察即為連結／特殊種類者隱藏；觀察後消失、交換或變成連結者為 PathChanged／相應安全失敗，不能悄悄略過。 |
| SEARCH-039 | 起點不存在為 NotFound，類型不符為 WrongEntryType，起點／沿途連結為 SymbolicLink。可存取子樹中的屬性或列舉權限／I/O 錯誤使整次操作失敗，不把未讀取子樹誤稱已完整搜尋。 |
| SEARCH-040 | search_files 只回傳普通檔案名稱，不開啟內容、不要求 UTF-8 或 128 KiB 大小條件，不新增／刷新普通檔案快照。固定敏感名稱仍先過濾，不能用檔名搜尋取得被禁止的路徑。 |
| SEARCH-041 | 一般安全目錄是否遞迴不由目前 glob 有無命中決定；首版不藉樣式推論略過其他安全子樹，以保持工作量計數及安全檢查一致。可以採深度優先及每層有界收集後排序；不得以任何走訪策略宣稱工作量截斷結果具有全域完整性。 |
| SEARCH-042 | 結果只描述本次可觀察集合，不是整個工作區的原子快照。相同位置後續讀取須重新驗證；保留既有應用層前後檢查對 TOCTOU、硬連結別名及掛載變動的限制，不宣稱提供作業系統沙盒或消除競態。 |
| SEARCH-043 | 成功 data 欄位順序固定 path、matches、truncated、safety_filtered；match 只含 path。空 Matches／Truncated=false 表示本次在允許範圍完成且沒有符合樣式的安全普通檔案，不代表未搜尋子樹不存在。 |

### 7.2 結果範例

以下均是預期格式，不是本次執行結果。

```json
{"status":"success","error_code":"none","data":{"path":"src","matches":[{"path":"src/Core/ExampleService.cs"},{"path":"src/OtherService.cs"}],"truncated":false,"safety_filtered":true}}
```

```json
{"status":"success","error_code":"none","data":{"path":"src","matches":[],"truncated":true,"safety_filtered":true}}
```

## 8. search_text：完整驗證後取得命中行

### 8.1 完整讀取、匹配及附近內容

| 編號 | 具體要求 |
|---|---|
| SEARCH-044 | 沿用第 7 節安全走訪，對符合 file_pattern 的安全普通檔案依候選及讀取預算處理。每個候選先經既有大小、唯讀開啟、身分／mode、完整 bytes、嚴格 UTF-8、二進位判準及完整內容 Key 檢查，再分行匹配；不可只檢查命中片段。 |
| SEARCH-045 | 已確認過大、InvalidUtf8 或 BinaryContent 的普通候選可略過，無命中資料、名稱或略過數；不換編碼、不讀前 128 KiB 當全文、不自動分段或重試。這三類是搜尋能力範圍排除，不單獨設定 Truncated；搜尋內容安全保證僅涵蓋成功解碼且通過文字判準的完整候選。 |
| SEARCH-046 | 任一已取得名稱／完整相對路徑、通過文字判準的完整解碼內容或實際 JSON 字串命中當前 Key，即整次 SensitiveContent、無 Data；包括不含 query、未回傳附近行、之後會因排序或容量移除的內容。AccessDenied、IoFailure、PathChanged 及根失效亦不得保留先前候選作部分成功。 |
| SEARCH-047 | 在每行不含行尾分隔的 Content 上，以 StringComparison.Ordinal 或 OrdinalIgnoreCase 執行精確子字串匹配；不自行轉小寫、不採文化比較、不做 Unicode 正規化或模糊匹配。每個命中行一筆，即使同一行出現多次；空行可出現在附近內容，但不能命中合法非空 query。 |
| SEARCH-048 | CRLF 視為一個分隔，LF、單獨 CR 亦各為一個；行號從 1 起，末端分隔不額外產生一行。空檔及 BOM-only 為 0 行；`a\n` 為 1 行、`\n` 為 1 個空行、`a\n\n` 為 2 行。移除開頭一份 UTF-8 BOM 後才分行，內部 U+FEFF 保留。 |
| SEARCH-049 | BeforeLines／AfterLines 分別取命中前／後最多 context_lines 個實際存在的行，按行號升冪，不含命中行及行尾分隔。相鄰命中的附近內容可重複，不合併成會改變筆數的區塊；不加虛構省略號或切短長行，附近行同受結果大小限制。 |
| SEARCH-050 | 成功 data 順序為 path、matches、truncated、safety_filtered；每個 match 順序 path、line_number、content、before_lines、after_lines；附近行物件為 line_number、content。最終發布前重驗所有已完整讀取候選的狀態及其目錄，包含無命中或結果已移除者；格式／大小排除檔案亦須完成可觀察路徑重驗才可略過。 |

單檔重驗比較沿用既有種類、Device／Identity、長度、修改時間及可取得的 mode；不另讀第二份全文來增加總量或假裝鎖定檔案。後續檔案變動仍由下一次操作重新驗證，不能將搜尋當時的行號套作永遠有效的修改位置。

### 8.2 結果範例

假設檔案第 9～11 行依序是下列 Content，且 query 為 `ReadFileAsync`、context_lines=1，預期結果為：

```json
{"status":"success","error_code":"none","data":{"path":"src","matches":[{"path":"src/Example.cs","line_number":10,"content":"await ReadFileAsync(path);","before_lines":[{"line_number":9,"content":"// 讀取資料"}],"after_lines":[{"line_number":11,"content":"return;"}]}],"truncated":false,"safety_filtered":true}}
```

零命中、未截斷只表示在本次固定忽略及受支援 UTF-8 小檔範圍內沒有觀察到匹配，不可宣稱整個專案不存在該字串。

## 9. read_file_range：指定行範圍

| 編號 | 具體要求 |
|---|---|
| SEARCH-051 | start_line 與 end_line 均為正 Int32，start_line<=end_line，請求跨度最多 200；不合法為 InvalidLineRange，內容 I/O 前拒絕，不先以 EOF 夾限挽救過大請求。 |
| SEARCH-052 | 目標沿用既有完整安全讀取，最多 131072 原始 bytes；不 seek 到指定範圍、不只驗證回傳行。過大／非法編碼／二進位／敏感內容是受控拒絕，不適用 search_text 的略過規則；只保留完整驗證後的切片，最終發布前重驗。 |
| SEARCH-053 | 分行同 SEARCH-048。非空檔 start_line 大於 TotalLines 時 InvalidLineRange；end_line 大於 EOF 時夾限。空檔／BOM-only 只接受 start_line=1 的合法請求，回傳 StartLine=null、EndLine=null、TotalLines=0、Content=""、HasMore=false；start_line>1 為 InvalidLineRange。 |
| SEARCH-054 | Content 是原解碼文字從實際起行開始，到實際末行行尾分隔結束（若存在）的連續切片；保留 CRLF／LF／CR、空白與檔尾狀態，不加行號、標頭或換行，不包含開頭 BOM。HasMore 只表示實際 EndLine<TotalLines，空檔為 false；EOF 夾限不是容量截斷。 |
| SEARCH-055 | FileByteCount 是完整原始檔 bytes，含 BOM；ContentByteCount 是 Content 的嚴格 UTF-8 bytes，不含 BOM 或 JSON 跳脫。三者與 JSON bytes 分別計量；新工具本機摘要顯示回傳 ContentByteCount，不把它說成實際 I/O bytes 或完整檔大小。 |
| SEARCH-056 | data 順序固定 path、start_line、end_line、total_lines、content、file_byte_count、content_byte_count、has_utf8_bom、has_more。start_line／end_line 是實際起訖；非空為正整數，空檔兩欄為 JSON null。完整投影超過 65536 時 ResultTooLarge，無 Data，不截行、不減少請求行數後假成功。 |

假設原始檔無 BOM，完整內容為 `one\r\n二\nlast`，共 13 bytes、3 行；請求第 2～20 行，預期為：

```json
{"status":"success","error_code":"none","data":{"path":"notes.txt","start_line":2,"end_line":3,"total_lines":3,"content":"二\nlast","file_byte_count":13,"content_byte_count":8,"has_utf8_bom":false,"has_more":false}}
```

BOM-only 檔案為 3 bytes，請求第 1～1 行，預期為：

```json
{"status":"success","error_code":"none","data":{"path":"empty.txt","start_line":null,"end_line":null,"total_lines":0,"content":"","file_byte_count":3,"content_byte_count":0,"has_utf8_bom":true,"has_more":false}}
```

read_file_range 不提供 Truncated 欄位；若只想取得更小範圍，須由新的明確呼叫提出。HasMore 不是續接游標，也不授權自動讀取後續內容。

## 10. 修改基準、取消、清除與資源釋放

| 編號 | 具體要求 |
|---|---|
| SEARCH-057 | 新三工具不建立、刷新、拼接或恢復 WorkspaceReadSnapshot；range 即使涵蓋整檔也不建立修改基準。只有既有完整 read_file 可發布新快照，replace_text 仍要求目前 session 的有效完整快照、逐 byte／身分／mode 重驗及逐次核准。 |
| SEARCH-058 | search_text 實際開始處理符合樣式的合法候選及 read_file_range 合法路徑讀取開始時，先移除該精確相對路徑舊快照；即使已知過大、後續略過、失敗、取消或成功，也不恢復。呼叫前已取消、參數無效、敏感路徑或因預算未開始的候選不使普通檔案快照失效；search_files 正常成功不影響普通檔案快照。 |
| SEARCH-059 | 已定位單檔的 PathChanged 或安全查核失敗至少移除該路徑基準；觀察到目錄身分／種類改變、根失效、根身分改變或無法定位影響範圍時清空全部快照，不只移除目錄同名索引。索引採既有 Ordinal，不猜測大小寫／Unicode 別名。 |
| SEARCH-060 | 列舉、匹配、逐行處理、可取消 I/O、結果重驗及發布點檢查 token／服務世代；發布前取消或 Clear 世代改變回傳 Cancelled／None，無 Data、不補回快照。發布後取消不回溯成功；同步系統呼叫沿用合作式取消，不宣稱固定時限內可強制終止所有 OS I/O。 |
| SEARCH-061 | `/clear`、模型強制 Context 清除、換 Key、新 session、釋放及退出沿既有共同路徑清空相關引用；DisposeAsync 停止新操作、取消並等待進行中操作、關閉全部列舉器／stream、清空自有資料再丟棄政策。釋放返回後不得仍有活動 I/O、資料發布或政策使用。 |
| SEARCH-062 | 三工具只列舉與唯讀開啟，不新增／刪除／修改來源、權限或暫存檔；檔案文字只作資料，不自動採納其中的權限／指令。不保存 query、結果、bytes 或快照至日誌／存檔；清除只丟棄自有引用，不承諾抹除 runtime、SDK 或外部複本。 |

例如先完整 read_file、再 search_text 或 read_file_range 讀到同一檔，原快照會失效；之後 replace_text 須重新完整 read_file。不同檔案的既有基準不因一般成功搜尋失效，仍由寫入時重新驗證。這是明確的安全取捨，不以拼接片段避免完整讀取。

## 11. 失敗分類、Agent 交接與 CLI

### 11.1 固定狀態及錯誤碼

| 條件 | Status／error_code | 資料及後續 |
|---|---|---|
| 參數缺少、額外／重複欄位、錯誤型別／整數形式、context_lines 不在 0～3 | Rejected／invalid_arguments | 工具層零工作區呼叫；直接內部入口亦受控拒絕。 |
| path 非法／越界／敏感 | Rejected／invalid_path、outside_workspace、sensitive_path | 沿既有規則，無存在性細節。 |
| 非法或過長 glob | Rejected／invalid_pattern | 不列舉、不開內容。 |
| query 空、過長、含禁止字元或非法 Unicode | Rejected／invalid_query | 不列舉、不開內容。 |
| 行數關係／跨度不合法，或合法讀取後發現起點超過 EOF | Rejected／invalid_line_range | 無 Data；後者已開始完整讀取，依第 10 節失效基準。 |
| 起點不存在／種類不符／起點或沿途連結 | Rejected／not_found、wrong_entry_type、symbolic_link | 無 Data；列舉初見的子項目連結則安全隱藏。 |
| 無權限／其他 I/O 失敗 | Failed／access_denied、io_failure | 整次無 Data，不以部分結果繼續。 |
| 範圍讀取過大／非法 UTF-8／二進位 | Rejected／file_too_large、invalid_utf8、binary_content | 無 Data；文字搜尋只對這三類候選採第 8 節略過規則。 |
| 搜尋工作量／結果容量達限 | Success／none，Truncated=true | 僅可含已完整驗證結果；不能套作其他失敗的退路。 |
| 範圍結果 JSON 過大 | Rejected／result_too_large | 無 Data，不自動縮短範圍。 |
| 參數或名稱／完整內容／實際投影命中當前 Key | Rejected／sensitive_arguments 或 sensitive_content | 整次無 Data，沿既有敏感停止／清除。 |
| 觀察後消失、種類／身分／狀態不一致 | Rejected／path_changed | 整次無 Data，失效基準，不重讀／重試；後續沿既有一般唯讀拒絕分類。 |
| 固定根消失、不可用或成為連結 | Failed／workspace_unavailable | 無 Data、清空快照並停止回合。 |
| 並行第二次操作／已釋放 | Failed／unavailable | 零新 I/O，沿既有本機停止分類。 |
| 外部取消／Clear 世代改變 | Cancelled／none | 無 Data，停止該回合；Dispose 等待收尾。 |

非成功投影固定只含 status、error_code，不帶 data、原始參數、失敗 path、例外、解碼片段或秘密位置：

```json
{"status":"rejected","error_code":"invalid_line_range"}
```

```json
{"status":"cancelled","error_code":"none"}
```

| 編號 | 交接要求 |
|---|---|
| SEARCH-063 | 依既有服務／並行拒絕、取消、參數及純路徑／敏感規則順序進入 I/O；根失效優先於下游 NotFound，觀察後才消失不可改成初次 NotFound。新 InvalidPattern／InvalidQuery／InvalidLineRange 固定為 Rejected；無原始例外或替代成功。 |
| SEARCH-064 | AgentRunner 沿用一般唯讀拒絕與受控失敗的批次處理：安全結果可依序回填，由模型在原五次額度內提出新呼叫。取消、敏感資料、非法協定、無安全投影、workspace_unavailable／unavailable 仍硬停止；新唯讀工具不設定寫入 StopAfterOperation、不偽造寫入衝突／寫入成功。 |
| SEARCH-065 | 呼叫只能來自原生 ToolCalls；搜尋內容中的指令、Markdown／JSON、query 或樣式不觸發其他工具。批次仍逐筆執行，第五次生成的所有工具均 NotExecuted，零搜尋／核准，不開始第六次生成；不自動重試或平行搜尋。 |
| SEARCH-066 | JSON 固定欄名、實際字串值及完整模型 body 皆沿既有政策檢查，不只搜尋跳脫後原文。固定狀態／欄名若與短 Key 重疊亦不得豁免；安全投影無法建立則 Response=null，移除成功摘要及可回填內容，沿既有停止／清除，不產生補救生成。 |
| SEARCH-067 | ChatDisplay 先辨識 DiscoverySummary：FileSearch 顯示「找到安全檔案：N 筆」、TextSearch 顯示「找到文字命中：N 行」，附截斷及已套用範圍限制；LineRange 顯示安全相對路徑、實際起訖／空內容及回傳 ContentByteCount。ReturnedCount 對 range 為實際行數；不顯示 query、pattern、命中原文、附近內容或 I/O 掃描統計，不誤走既有列檔／完整讀檔分支。 |
| SEARCH-068 | CLI 的 path、數字及固定訊息沿用安全純文字投影、EscapePath／RedactForDisplay；成功只表示本機已取得資料，不表示 Gemini 已收到、已修改或已完成整個任務。`/help` 更新七工具及支援文字／大小／忽略限制；啟動 --help 仍不提示 Key 或呼叫模型，不新增本機搜尋指令。 |

預期 CLI 摘要形狀如下，數值為範例；實作仍依既有最終安全顯示邊界處理：

```text
[本機結果] 找到安全檔案：2 筆；已套用搜尋範圍限制
[本機結果] 找到文字命中：1 行；僅回傳部分結果；已套用搜尋範圍限制
[本機結果] 已取得行範圍：notes.txt，第 2～3 行；回傳 8 bytes
```

## 12. 需求對照與後續產品驗收

### 12.1 方法及證據

| 編號 | 測試要求 |
|---|---|
| SEARCH-069 | 全部自動產品驗收保持離線，使用合成假 Key、假模型、真實 SDK 配假 HTTP handler、可控制檔案系統及隔離暫存工作區。一般 UTF-8／BOM／連結／來源不變使用真實檔案；取消、權限、增長、變動及精確停止點用替身。 |
| SEARCH-070 | MSTest 使用如 `SEARCHAC007_GlobMatchesUnicodeScalarsAndZeroDirectories` 的驗收連結名稱；斷言只報案例 ID、狀態、數值及布林，不傾印 query、文字、模型紀錄、快照、終端機或原始例外。測試清理不遞迴／批次刪除，保留測試資料或僅處理明確單一路徑檔案。 |
| SEARCH-071 | 離線、自動 PTY、真人終端機、真實 Gemini 與發布／分發證據分層記錄，不以 README 歷史測試數或本次文件檢查替代。新增功能回歸涵蓋 01～07，既有掃描器／hook 不為新規格放寬，Release 阻擋及人工待辦保留。 |

### 12.2 離線產品案例

以下為未來必須執行的案例表，不是已通過結果。每個案例可由多個 TestMethod 實現；不要求依表列筆數聲稱測試數。

| 案例 | 對應需求 | 情境與預期結果 |
|---|---|---|
| SEARCH-AC-001 | SEARCH-001～016 | 僅三項新能力、七工具固定順序、四舊工具 Schema／投影不變；既有版本／設定／LLM 公開 API 不變，工廠零 I/O／生成，無外部程序、套件或額外 FUT 工具。 |
| SEARCH-AC-002 | SEARCH-011～016、062 | 三結果狀態／Data、只讀集合、防禦性複製、ToString、摘要 Kind／欄位一致；失敗及 WithoutResponse 不帶新摘要，既有寫入事實不丟失，無完整路徑／政策／bytes 意外序列化。 |
| SEARCH-AC-003 | SEARCH-017～020、022～023 | 三 Schema、必填／省略預設、null／額外／重複／大小寫欄位、錯誤型別、1.0／1e0／Int32 溢位、context_lines=-1／4：受控拒絕，工具層零工作區呼叫；原生 args 重複在 SDK 轉換前攔截。 |
| SEARCH-AC-004 | SEARCH-018、030、063 | 絕對／UNC／drive-relative、反斜線、`..`、4096／4097 UTF-16 path、非法代理、控制字元、相似根前綴越界：正確拒絕、零內容開啟；純空白／中文／emoji／字面變數名稱可按字面使用。 |
| SEARCH-AC-005 | SEARCH-018、030、038～039、042 | 敏感根、巢狀敏感名稱及副檔名、`.env.example`、近似合法名稱；起點／沿途／巢狀連結、壞連結、循環、FIFO／socket：不讀受限目標，隱藏不透露數量。 |
| SEARCH-AC-006 | SEARCH-024～027 | 空／1024／1025 單位樣式，絕對、空區段、`.`／`..`、尾斜線、反斜線、非法 Unicode、`a**b`／`***` 拒絕；空白、`[AB]`、brace 等其餘合法字元按字面，不展開。 |
| SEARCH-AC-007 | SEARCH-025～027、036 | `*.cs` 跨深度 basename、`Core/*.cs` 錨定、`**/*.cs` 零層、整段 `**`、`?` emoji 與組合字元；大小寫及不同文化相同，完整名稱匹配，不採指數回溯。 |
| SEARCH-AC-008 | SEARCH-028～030、040～041 | 固定清單逐項含大小寫／巢狀目錄；明確 path 例外、同名普通檔案、一般點檔及 .gitignore 否定均按規則；忽略目錄不被列舉、不讀內容，既有 list/read 不採此忽略清單。 |
| SEARCH-AC-009 | SEARCH-012、034、036、038～043 | 空目錄、零命中、混合檔案／目錄、重複 basename、多種列舉順序：只回傳普通檔案、工作區相對路徑及 Ordinal 排序；search_files 對過大／非 UTF-8 檔案仍可回名稱且零內容 I/O。 |
| SEARCH-AC-010 | SEARCH-031～032、036～037 | 跨目錄合計 8191／8192／超限項目，包含大量安全隱藏／忽略者；不取得第 8193 筆，達限標截斷，子集不宣稱全域前綴。 |
| SEARCH-AC-011 | SEARCH-031～032、044～045 | 符合 file_pattern 的 511／512／超限候選，含格式／大小排除者；不處理第 513 個，非匹配普通檔案不讀內容；達限標截斷。 |
| SEARCH-AC-012 | SEARCH-031～033、044～046 | 8388607／8388608 bytes 邊界、BOM、探測增長及非法格式已讀 bytes 均計入；不超讀總額，不把部分候選算命中／未命中；預算耗盡時只保留之前完整候選且截斷。 |
| SEARCH-AC-013 | SEARCH-031～032、038 | 深度 0／31／32／33：32 的直接檔案可用，33 目錄不進入且截斷；敏感／產物忽略子樹不因深度另洩漏狀態。 |
| SEARCH-AC-014 | SEARCH-034～037、043、049 | 199／200／201 個匹配、同檔多行、排序尾端淘汰：只有實際省略結果才因筆數截斷；有限回傳後仍於工作量內檢查其他候選，無無界結果保留。 |
| SEARCH-AC-015 | SEARCH-008、035、037、043、050、056 | 完整新 Response JSON 65535／65536／65537 bytes，中文及引號／反斜線跳脫皆計入；搜尋移除完整尾端 match、單筆過長可空截斷，範圍超限拒絕；JSON 合法，不切行或文字。 |
| SEARCH-AC-016 | SEARCH-019、021～023、047 | query 空／1／1024／1025 UTF-16，空白／Tab、CR／LF／控制字元、非法代理；預設區分大小寫與顯式 false 的 OrdinalIgnoreCase、文化及 Unicode 正規化差異，正規式字元只按字面。 |
| SEARCH-AC-017 | SEARCH-044～048、052 | 131071／131072／131073 bytes、讀取增長、嚴格 UTF-8、UTF-16／32 BOM、二進位；搜尋只略過指定三類、範圍受控拒絕，不換編碼／拆檔／重試，來源 bytes 不變。 |
| SEARCH-AC-018 | SEARCH-030、044、046、050、066 | 假 Key 在名稱、路徑、未命中檔案、未回傳附近行、排序尾端或超大單筆中：整次無 Data，無後續生成；不以只回片段或最後移除 match 規避完整內容檢查。 |
| SEARCH-AC-019 | SEARCH-047～050 | 一行多次只一筆、前後 0／1／2／3 行、檔首尾、相鄰命中附近內容重複及空行；命中／附近行號正確，不帶原行尾、虛構行或省略號。 |
| SEARCH-AC-020 | SEARCH-048、053～055 | 空檔、BOM-only、ASCII／中文／emoji、內部 U+FEFF、CRLF／LF／CR 混合、末尾無換行、`a\n`／`\n`／`a\n\n`：行數、內容、BOM 及兩種 byte 數正確。 |
| SEARCH-AC-021 | SEARCH-020、051～056 | start=0／負值／start>end、200／201 行跨度、Int32 極端值、起點超 EOF、終點夾限、空檔 start=1／2；空檔 null 起訖、HasMore、原始行尾切片及範例 13／8 bytes 正確。 |
| SEARCH-AC-022 | SEARCH-038～039、044、046、050、052、059 | 權限、列舉失敗、開啟前後 inode／mode／內容狀態變動、普通檔案消失／換連結、零命中或已淘汰候選在發布前改變：整次失敗無 Data，不悄悄略過／重讀。 |
| SEARCH-AC-023 | SEARCH-038、042、059、063 | 父目錄／根身分交換、根消失／成連結及根失效與 NotFound 同時發生：停止或拒絕分類正確，目錄／根變動清空全部快照，不跟隨新位置。 |
| SEARCH-AC-024 | SEARCH-057～059 | 完整 read_file 後對同檔 search_text／range：舊基準移除且不重建，範圍涵蓋全檔亦同；replace_text 回 snapshot_required，重新完整讀後才可走正常核准。不同檔案快照不因一般成功失效。 |
| SEARCH-AC-025 | SEARCH-057～060 | 呼叫前取消、非法參數／敏感路徑／未開始候選不失效普通快照；合法候選開始後成功／格式排除／失敗／取消不恢復；search_files 正常不碰檔案快照，根／目錄失效仍全清。 |
| SEARCH-AC-026 | SEARCH-011、060～062 | 開始前、列舉、glob、完整讀取、逐行匹配、JSON、發布點取消及 Clear 競爭；零遲到 Data／快照，列舉器／stream 關閉，不刪／改來源。 |
| SEARCH-AC-027 | SEARCH-011、061～062 | 任一新／舊工作區操作並行互斥、重複釋放、釋放中取消及之後呼叫；第二次零 I/O，Dispose 返回後無活動 I/O／政策使用，新 session 不重用舊結果。 |
| SEARCH-AC-028 | SEARCH-007～008、017、064～066 | 真實 ChatApplication／Registry／SDK 配假 handler，七工具定義與原生多工具配對；混合搜尋／讀取／寫入按原序，安全拒絕可回填，硬停止標剩餘 NotExecuted，不重建簽章或假造回答。 |
| SEARCH-AC-029 | SEARCH-007～008、064～066 | 第五次生成含新工具整批零 I/O、第六次不發送；七工具／多結果／歷史使實際 body 262144／262145 bytes 的邊界正確，不另裁結果／歷史、重試或重放既有寫入。 |
| SEARCH-AC-030 | SEARCH-013～016、023、066～068 | 短假 Key 與固定欄名／狀態重疊、JSON 跳脫及安全投影失敗：Response=null 後停止並移除新摘要；無內容／參數／原始例外傾印，既有已核對寫入事實保留。 |
| SEARCH-AC-031 | SEARCH-016、055、067～068 | 三種 CLI 摘要、空範圍、零命中／空截斷、安全路徑及片段 bytes：不誤報列檔／完整讀取／已送 Gemini；/help 七工具與限制正確，--help 零認證／生成，舊 CLI 行為回歸。 |
| SEARCH-AC-032 | SEARCH-003～010、062、069～073 | 既有 01～07 產品與掃描器回歸離線通過；工具唯讀來源不變、無子程序／新增持久化／套件設定；測試只用合成資料、證據分層且保留第 07 份待辦。 |

### 12.3 PTY、真人及真實服務

| 案例 | 對應需求 | 後續驗收方式 |
|---|---|---|
| SEARCH-AC-033 | SEARCH-060～062、064～068、071 | 自動 macOS PTY 配離線 host：長搜尋期間 Ctrl+C 取消後可繼續聊天，Ctrl+D 等待收尾後正常退出；busy 普通輸入不排隊，七工具 help／摘要正確，回顯及終端機狀態恢復。 |
| SEARCH-AC-034 | SEARCH-060～062、067～068、071 | 真人終端機檢查中文／emoji 查詢、合法空白名稱、長摘要與取消體驗；依實際結果記錄，不以 PTY 或 GUI 自動操作替代真人證據。 |
| SEARCH-AC-035 | SEARCH-007～008、057、064～068、071 | 使用者另行授權後才以真實 Gemini 測試檔名定位→文字搜尋→範圍讀取→以真實結果回答；修改時仍先完整 read_file 再預覽核准。記錄模型／平台／實際工具與停止狀態，不保存 Key、原始模型紀錄或完整內容。 |

## 13. 後續產品命令、完成標準及證據模板

| 編號 | 具體要求 |
|---|---|
| SEARCH-072 | 後續實作完成後，依序執行下列離線命令及完整回歸；記錄當次退出碼、實際測試數與未測項目。產品 restore 只還原固定依賴；測試本身不得連 Gemini。本次規格交付不執行這些產品命令。 |
| SEARCH-073 | 本份可分別宣稱「文件完成」「產品離線驗收完成」「PTY 完成」「真人／服務完成」，不得跨層推論。產品離線完成至少需 SEARCH-AC-001～032 及既有回歸全部通過；PTY 另需 033，真人／服務另需 034～035。發布還需原第 07 份門檻，不以本份完成取代。 |

```sh
dotnet restore tests/AIAgent.Cli.Tests/AIAgent.Cli.Tests.csproj
dotnet build tests/AIAgent.Cli.Tests/AIAgent.Cli.Tests.csproj --no-restore
dotnet test --project tests/AIAgent.Cli.Tests/AIAgent.Cli.Tests.csproj --no-build
python3 -B -m unittest discover -s tests -p 'test_sensitive_content.py'
dotnet run --project src/AIAgent.Cli/AIAgent.Cli.csproj --no-build -- --help
dotnet format src/AIAgent.Cli/AIAgent.Cli.csproj --no-restore --verify-no-changes
python3 -B tests/terminal_smoke.py
git diff --check
```

驗收紀錄最少包含：日期、commit／工作區狀態、作業系統／架構、SDK／Python 版本、命令、退出碼、實際通過／失敗／略過數、案例覆蓋、未執行項目與第 07 份未結門檻。只保存受控摘要，不附原始秘密或工具文字；本節範本不是完成證據。

## 14. 本次文件檢查與交接

| 編號 | 文件要求 |
|---|---|
| SEARCH-074 | 本次以已確認的 128 KiB 上限、固定忽略、字面搜尋及三項獨立唯讀工具為基準，核對所有需求／案例、Schema／JSON、欄位順序、固定碼、byte 範例、上限、快照與生命週期；不得留下影響實作的待定項目或互相矛盾的成功條件。 |
| SEARCH-075 | 解析全部 JSON 區塊、核對 Schema properties／required／additionalProperties、Markdown 表格／區塊、本機連結與需求覆蓋；執行空白檢查，確認既有追蹤內容及索引不變，只新增本檔。未追蹤文件亦須單獨納入差異查核。 |
| SEARCH-076 | 本次只回報實際文件檢查；不執行 restore、build、MSTest、formatter、PTY、真人或 Gemini，不將文件審查算作產品驗收。後續才依本份另行開發，不因規格書完成自動啟動產品工作。 |

| 案例 | 對應需求 | 本次文件成功條件 |
|---|---|---|
| SEARCH-AC-036 | SEARCH-002、069～071、074～076 | 需求及案例編號唯一連續、全部需求有驗收對照；三工具／七工具交接、JSON／Schema／預設、上限單位、錯誤／截斷、完整檢查、快照及清除一致。JSON、byte 範例、本機連結、Markdown 及空白查核通過，只新增指定規格且既有內容／索引不變；產品及真人／服務驗收未執行。 |

本次應執行的文件查核命令：

```sh
git diff --check
git diff --no-index --check -- /dev/null specs/08-code-search-and-partial-read.md
git status --short
git diff --name-only
git diff --cached --name-only
```

新增檔案的 no-index 差異可能以退出 1 表示存在新增差異；須讀取空白診斷，不能單憑 1 判定失敗，也不能因 git diff 未包含未追蹤檔案便宣稱已檢查全文。JSON／連結／編號的查核可使用只讀腳本，不將檢查腳本另存為本次交付物。

## 參考資料

下列官方資料於 2026-10-23 核對，僅用於字串、Unicode scalar 及嚴格 UTF-8 語意；本規格的 glob、忽略、上限及工具格式是本專案契約，並非宣稱相容 Git、shell 或其他 Agent 的搜尋語法。模型／SDK 維持既有固定版本，沒有升級或執行真實服務驗證。

- [.NET 字串比較建議：Ordinal／OrdinalIgnoreCase](https://learn.microsoft.com/en-us/dotnet/standard/base-types/best-practices-strings)
- [.NET 10 Rune：Unicode scalar](https://learn.microsoft.com/en-us/dotnet/api/system.text.rune?view=net-10.0)
- [.NET 10 UTF8Encoding 建構式：throwOnInvalidBytes](https://learn.microsoft.com/en-us/dotnet/api/system.text.utf8encoding.-ctor?view=net-10.0)
