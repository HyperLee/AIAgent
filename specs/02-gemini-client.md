# 02：Gemini 用戶端子規格

## 第 20 份產品實作接續（2026-10-29）

Gemini 固定支援 Flash-Lite 與 3.6 Flash，沿原 SDK／Generate Content、原生 owner、parts／簽章與請求限制。PublicSettings 增加唯讀 Provider 與雙參數建構入口，公開 LLM 契約保持；B 已以內建 HttpClient／System.Text.Json 加入獨立 OpenAiClient，固定 Responses／Bearer；不重用 Gemini 原生紀錄或 SDK 認證，零新 NuGet。 詳見[第 20 份規格](20-multi-model-and-provider-support.md)；A、B 分層證據見 [README](../README.md#model-validation)，真實服務／真人與既有 Release 未結門檻另列。

> 文件版本：0.1｜建立日期：2026-10-20｜狀態：待實作規格
>
> 上位文件：[入門 AI Agent 開發總規格書](../SPEC.md)，主要依據第 5～7、14～15 章，並遵循第 9～10、16 章的協定與階段約定。前置規格：[01：專案啟動與 API Key](01-bootstrap-and-key.md)。第 01 份已有產品及測試程式；本文件定義下一步的 Gemini 模組，不表示 Gemini 呼叫或第一階段「可以聊天」已完成。

## 1. 目的與交付邊界

建立單一、可替換及可模擬的模型呼叫邊界，將既有記憶體 session 接到官方 Gemini SDK。讓後續 CLI、Context 與 AgentRunner 使用自己的核心契約，不必直接操作 SDK、HTTP 認證或原始例外。

本子規格的獨立完成點為「以已確認的 session 建立 GeminiClient，經官方 SDK 送出一次受控生成請求，回傳可辨識的文字、工具呼叫或失敗狀態，並能以假傳輸驗證認證、次數、協定及敏感資訊邊界」。獨立自動驗收由測試呼叫模型介面，不要求先實作第 03 份聊天輸入迴圈。

| 本文件負責定案 | 後續文件負責 |
|---|---|
| 固定 SDK 版本、預設模型、單一 API 模式、傳輸端點與有限請求 | 第 03 份：CLI 指令、顯示、完整回合儲存、最近 5 回合裁切及用量顯示。 |
| 模型核心契約、文字／工具回應解析、協定紀錄保存、取消與逾時 | 第 04 份：工作區內每次檔案操作的限制、列檔及讀檔。 |
| 認證用途限制、送出前內容檢查、輸出及錯誤的安全交接 | 第 05 份：實際工具 Schema、登錄、派送、工具鏈與每回合 5 次生成請求上限。 |
| 單次用量擷取、錯誤分類、SDK 假傳輸驗收及 session 更換契約 | 第 06～07 份：寫入確認、衝突處理、整體成本控制及提交／發布檢查。 |

本次文件交付只新增本檔；不修改程式、第一份規格、總規格、設定、專案檔或套件。不建立後續子規格空檔，不實作聊天或工具，不安裝 hooks，不提交、推送或改寫歷史。下述程式、設定範例與命令是後續開發要求，不能視為本次已執行結果。

## 2. 既有依賴與模組責任

### 2.1 開發起點

目前儲存庫已有下列直接依賴；本次只閱讀原始碼確認介面，未重新執行第 01 份的建置、測試或人工終端機驗收。

| 既有能力 | 本份如何接續 |
|---|---|
| `net10.0` Console、可空值分析及警告視為錯誤 | 沿用單一產品專案，不新增 Web、App、Agent 框架或類別庫專案。 |
| 獨立 `MSTest.Sdk/4.5.1` 測試專案、Microsoft.Testing.Platform | 加入本份測試，沿用既有測試方式及產品專案參考。 |
| `Bootstrapper`、`BootstrapSession`、`PublicSettings(string? Model)` | 從已取得的 session 組裝模型轉接器；模型必填的變更責任見第 3 節。 |
| `SessionSecrets` 的內部 `AuthenticationKey`、安全 `ToString()` 及釋放 | 僅在 SDK 認證組裝範圍讀取，不新增公開 Key 屬性。 |
| `ISensitiveDataPolicy`、`ContentBoundary` | 沿用目前 Key 的精確比對、整次拒絕與顯示遮蔽，不另建較寬鬆的政策。 |
| 應用程式目錄的嚴格 `appsettings.json` | 目前只有 `Gemini.Model`，值為 `null`，尚未引用 `Google.GenAI`。 |

### 2.2 責任分工

| 元件 | 責任與限制 |
|---|---|
| 核心模型契約 | 定義請求、結果、工具資料與不透明訊息；不公開 `Google.GenAI.Types`。 |
| GeminiClient | 一次呼叫對應至多一次生成傳輸；處理轉接、內容檢查、結果分類及用量，不使用 Console、不讀寫工作區。 |
| SDK 建立與傳輸轉接器 | 固定 Developer API、官方端點、認證標頭、逾時及無重試；提供可注入的 HTTP 測試邊界。 |
| 協定紀錄包裝 | 在程序記憶體保存完整 SDK Content，不讓顯示層序列化、列印或重建它。 |
| 既有啟動模組 | 在互動及 Key 輸入前要求實際模型；其餘工作區與隱藏輸入規則沿用第 01 份。 |
| 第 03 份 CLI／Context | 選取及保存完整回合、顯示結果、提供 Key 重新輸入、管理跨請求用量累計。 |
| 第 05 份 AgentRunner／ToolRegistry | 驗證工具授權與參數、依序執行、回填結果及控制每回合 5 次上限。 |

未來產品程式主要新增於 `Core/` 的模型契約及 `Llm/` 的 Gemini 轉接；沿用既有 Security 元件。`Program.cs` 只組裝與交接，不放入 SDK 解析或 Key 判斷邏輯。

## 3. 技術基線、設定與限制

需求編號使用 `GEM-xxx`，驗收使用 `GEM-AC-xxx`；新增時接續編號，不重排既有編號。第 01 份的 `BOOT-xxx` 與 `AC-xxx` 只以明確前綴引用，不挪用其編號。

### 3.1 固定選擇

| 編號 | 具體要求 |
|---|---|
| GEM-001 | 後續實作在既有產品專案明訂 `Google.GenAI` 版本 `1.26.1`，不使用浮動、預覽套件或另一個 Gemini 套件替代。保留現有 .NET 與 MSTest 基線。 |
| GEM-002 | 本份預設模型定為 `gemini-3.5-flash-lite`；模型來源仍僅為既有非機密設定，不加入 `--model`、環境變數、執行中切換或自動換模型。其他模型名稱不代表已經本專案驗收。 |
| GEM-003 | 只採 Gemini Developer API 的 `v1beta` Generate Content，使用 `client.Models.GenerateContentAsync` 的完整 Content 清單多載及取消參數。所有範例採 `contents`、`parts`、`functionCall`、`functionResponse`；不混入 Interactions、OpenAI 相容格式、Vertex AI、串流或 SDK 自動聊天歷史。 |
| GEM-004 | 明確設定 Developer API 模式、`https://generativelanguage.googleapis.com` 與 `v1beta`。生成目標為 `/v1beta/models/{model}:generateContent`；模型名稱不得帶協定、主機、路徑分隔符、查詢或片段。環境變數及 SDK 全域 Base URL 不得改變有效認證或端點；不提供公開自訂端點設定。 |
| GEM-005 | 後續將應用程式設定中的模型改為本份預設值。`null`、空白、控制字元或不合法的模型識別名稱均於啟動設定檢查時失敗，映射既有 `InvalidSettings`／退出碼 2，且不確認工作區、不讀 Key、不送 HTTP。`--help` 仍不讀設定。 |

本份模型識別名稱限定 ASCII 小寫英文字母、數字、`.`、`-`、`_`，第一個字元須為英文字母或數字，長度 1～128；這是本機允許的簡單模型 ID，不接受 `models/` 前綴。沿用先去除首尾空白、再驗證非空的設定規則，不把本機檢查稱為模型可用性驗證。

後續開發應使用的設定仍為原有唯一結構：

```json
{
  "Gemini": {
    "Model": "gemini-3.5-flash-lite"
  }
}
```

`PublicSettings.Model` 可保留既有可空型別以維持介面，但模型模組不接受空值；更新既有「設定允許 null」的測試預期並保留其他嚴格 JSON 案例。這是第 01 份 BOOT-007 預告的收緊，不新增設定檔或 Key 來源。

### 3.2 有限請求

| 編號 | 具體要求 |
|---|---|
| GEM-006 | 使用具名常數 `RequestTimeoutSeconds = 120`、`MaxOutputTokens = 8192`、`CandidateCount = 1`、`MaxRequestBodyBytes = 262144`；初版不擴充 JSON 設定欄位。輸出 token 上限不是保證可見文字長度，亦不等於字元數。 |
| GEM-007 | 每次生成採外部取消與 120 秒期限連結的 token，傳入 SDK、傳輸與回應讀取。SDK `HttpOptions.Timeout` 設為 `120000` 毫秒；連結期限負責限制整次操作，不能依 SDK 的預設逾時。逾時／取消後不發布遲到的成功結果。 |
| GEM-008 | 明確設定 `HttpRetryOptions.Attempts = 1` 及 `GenerateContentConfig.AutomaticContinuation = false`；不包裹另一層重試，不使用自動 function invocation。一次 `GenerateAsync` 不送認證測試、模型列舉、token 計數、摘要或結尾生成。 |
| GEM-009 | 在 SDK 完成轉換、最內層傳輸尚未開始前，以實際 UTF-8 JSON body 位元組計算大小；262,144 位元組可送，262,145 即拒絕。包含應用指示、全部選定訊息、工具定義、工具結果、簽章及生成設定，不含 HTTP 標頭；不能只把可見字串長度加總。讀取及檢查過程只使用記憶體，不修改原文或偷偷裁切歷史。 |
| GEM-010 | 產品 HTTP handler 關閉自動重新導向；送出前核對 HTTPS、官方主機、API 路徑及無 query／fragment。不因重新導向、自訂端點或錯誤處理將認證送往其他主機。測試以注入假 handler 攔截相同官方 URI，不以產品可設定端點繞過限制。 |

`GenerateContentConfig` 固定上述輸出與續傳選擇，僅傳送當次提供的應用指示、內容及工具。溫度、進階思考調校、快取、搜尋、程式執行及其他內建工具不在本份啟用；保留模型原有預設，不額外要求思考摘要。

## 4. 核心契約與協定紀錄

### 4.1 最小公開形狀

以下是待實作契約，不是本次新增的 C# 原始碼。資料內容型別使用一般密封類別、唯讀屬性及防禦性複製；不得用自動列印內容的 record 承載請求、工具參數或原始協定資料。只有純數值 `LlmUsage` 可使用 record。

```csharp
public interface ILlmClient : IAsyncDisposable
{
    Task<LlmResult> GenerateAsync(
        LlmRequest request, CancellationToken cancellationToken);
}

public enum LlmStatus
{
    Completed, ToolCalls, Empty, Refused, Truncated, Cancelled, Failed
}

public enum LlmErrorCode
{
    None, InvalidRequest, InputTooLarge, SensitiveInput, SensitiveOutput,
    AuthenticationFailed, ModelUnavailable, PermissionDenied,
    QuotaOrRateLimited, NetworkFailure, Timeout, ServiceUnavailable,
    InvalidResponse, UnexpectedToolCall, ClientUnavailable
}

public sealed record LlmUsage(
    long? PromptTokens, long? OutputTokens, long? ThoughtTokens,
    long? CachedTokens, long? TotalTokens);
```

| 型別 | 必要建構資料或唯讀屬性 |
|---|---|
| `LlmRequest` | `string SystemInstruction`、`IReadOnlyList<LlmMessage> Messages`、`IReadOnlyList<LlmToolDefinition> Tools`；不含 Key 或每次覆寫模型／端點的選項。 |
| `LlmMessage` | 代表一份 user、model 或工具回覆 Content；公開 `FromUserText(string text)` 及 `FromToolResults(IReadOnlyList<LlmToolResult> results)` 靜態建立方法。模型訊息只由轉接層建立，內部封裝原始 SDK Content。 |
| `LlmToolDefinition` | `string Name`、`string Description`、`JsonElement Parameters`，其中 Parameters 為獨立擁有的 JSON object Schema；不帶可執行委派。 |
| `LlmToolCall` | `int PartIndex`、`string? CallId`、`string Name`、`JsonElement Arguments`；PartIndex 是原始 parts 中從 0 起算的位置，Arguments 必須為 object 並複製其所有權。 |
| `LlmToolResult` | `int PartIndex`、`string? CallId`、`string Name`、`JsonElement Response`；Response 為 object，之後的工具模組在其中表達成功、失敗、拒絕或未執行。PartIndex 只用於本機配對，不另加到 API functionResponse。 |
| `LlmResult` | `LlmStatus Status`、`LlmErrorCode ErrorCode`、`string SafeText`、`IReadOnlyList<LlmToolCall> ToolCalls`、`LlmMessage? ModelMessage`、`LlmUsage? Usage`、`bool RequestDispatched`、`bool RequiresContextReset`。 |

| 編號 | 契約要求 |
|---|---|
| GEM-011 | 請求為呼叫時的不可變快照；Messages 依呼叫者提供的順序，包含已選歷史及當前鏈。不得自行選取 5 回合、追加隱藏對話、讀取工作區或改寫應用指示。訊息清單不可為空；null、空白的當前文字與不合法資料在傳輸前回傳 `InvalidRequest`。 |
| GEM-012 | 核心與 CLI 不公開 SDK 型別、Key、HTTP request／response 或原始例外。內容型別的 `ToString()` 回傳固定型別標記，不傾印屬性；不透明協定紀錄不提供公開序列化、原始 JSON 或可變 SDK 物件出口。 |
| GEM-013 | 一次只允許一個進行中的 GenerateAsync；第二個並行呼叫直接回傳 `Failed/InvalidRequest`，不排隊、不送 HTTP。預期 API／傳輸／內容錯誤以 LlmResult 表達，不把 SDK 例外帶給顯示層。 |
| GEM-014 | `RequestDispatched` 表示曾將請求交給最內層傳輸開始嘗試；送出前拒絕為 false，開始傳輸後即使網路錯誤／取消仍為 true。它不證明 Google 已收到或計費。`ModelMessage` 僅可在 Completed 或合法 ToolCalls 時提供；其他狀態不提供可續傳的本次模型紀錄。 |

`SafeText` 是可供顯示的文字，不是判斷操作成功的依據；`ToolCalls` 狀態的附帶文字不是最終回答。失敗、Empty 與 Refused 的固定狀態說明由錯誤碼或狀態對應，不附 SDK 原文。非 Failed／Cancelled 狀態的 ErrorCode 為 None；截斷的安全部分文字可保留，必須由呼叫者標示「未完整產生」。

### 4.2 保存與工具回填

| 編號 | 協定要求 |
|---|---|
| GEM-015 | 保存單一候選的完整 Content、role、各 part 的內容／順序、functionCall ID 及 SDK 支援的必要中繼資料，包含 thought signatures。不得把相鄰 parts 合併、只保存文字，或用投影出的工具參數重建帶簽章的 model Content。顯示用文字投影與協定保存分開。 |
| GEM-016 | 文字顯示依序取非 thought 的 text parts，直接串接後再做顯示遮蔽；不顯示 thought parts、簽章或原始中繼資料。這不代表刪除協定紀錄中的必要 parts。 |
| GEM-017 | 工具呼叫按原始 PartIndex 回傳；若 API 有 CallId 則原樣保存，沒有則保持 null，不偽造供應商 ID。工具結果以 CallId（有值時）、Name 及本機 PartIndex 配對，同名呼叫也不得互相覆蓋。參數／Schema／結果 JSON 必須在原 JsonDocument 釋放後仍可使用。 |
| GEM-018 | 回填時先附上完整原始 model Content，再把全部對應 functionResponse 依原呼叫順序放入同一份 user Content。不得交錯為「呼叫 1、結果 1、呼叫 2、結果 2」，不得漏掉呼叫或修改簽章。缺少／重複／不匹配的結果以 InvalidRequest 阻擋送出；真正執行與循環由第 05 份負責。 |
| GEM-019 | 第一階段正式請求的 Tools 為空，不預先公布四個檔案工具或任何內建工具。若模型仍回傳 functionCall，回傳 `Failed/UnexpectedToolCall`，不執行、不製造工具成功結果。自動驗收可提供純描述的假工具以驗證原生協定，但不接入檔案或 shell。 |

不透明訊息限目前模型 client／認證 session 使用，不支援跨 session 續用、存檔或反序列化恢復；內部識別只用於所有權檢查，不使用 Key 指紋。失效或其他 session 的紀錄於送出前拒絕。假的 ILlmClient 可以提供測試專用訊息，不需要真的建 SDK client；SDK 轉接測試則透過真實 SDK 與假 handler 驗證傳輸。

## 5. 單次請求主要流程

1. 確認 client 未釋放且沒有並行生成，建立取消與期限範圍；若已取消，不開始傳輸。
2. 檢查模型及請求形狀、訊息所有權與工具配對。呼叫者已選好歷史，本層不裁切完整回合。
3. 依第 6 節檢查應用指示、訊息、工具定義、參數與結果；若含當前 Key，拒絕整次請求。
4. 轉為單一模式的 SDK Content 與 GenerateContentConfig；設定非串流、單一候選、輸出上限及關閉續傳。
5. 由受控傳輸邊界檢查 SDK 實際 JSON body、大小、官方 URI 及認證位置；通過後才將本次請求交給最內層 handler，標記 RequestDispatched。
6. 接收回應，擷取可用的數值用量；在建立公開文字、工具呼叫或可續傳 ModelMessage 前檢查敏感內容及協定。
7. 依第 7 節分類；只有合法完整文字或工具請求可帶 ModelMessage。回傳結構化結果，釋放本次傳輸／期限資源。
8. 本方法到此結束；是否顯示、保存回合、執行工具或再呼叫模型，由後續模組決定。

不以建構 client、格式檢查或 HTTP 200 宣稱「Key 認證成功且回答正確」。第一個由正常使用者需求產生的生成請求才反映當時的服務端結果；成功取得文字不表示檔案已讀取或修改。

## 6. 認證與敏感資訊邊界

### 6.1 請求與回應

| 編號 | 具體要求 |
|---|---|
| GEM-020 | 從 BootstrapSession 的內部 SessionSecrets 取得 Key，只用於 SDK 認證，實際傳輸僅在 `x-goog-api-key` 標頭。不得從環境／檔案補值，不把 Key 置於 URL、query、模型內容、一般 body、工具 Schema 或工具參數。認證標頭不參與禁止模型內容含 Key 的判斷。 |
| GEM-021 | 邏輯內容送出前使用既有 ISensitiveDataPolicy 檢查每個實際字串欄位及 JSON 欄位名稱／值，包含應用指示、選定歷史、當前文字、工具描述、參數及工具結果；對應 ModelInput、ToolArguments、ToolResult 邊界。SDK 完成轉換後，再核對實際 body 未因轉接意外加入 Key；結構化 JSON 的字串需依其值檢查，不能只搜尋跳脫後的 JSON 原文。任一命中均整次拒絕，回傳 `Failed/SensitiveInput`，不以遮蔽後內容繼續送出。 |
| GEM-022 | 回應的文字、工具名稱／ID／參數及將保留的協定字串在交給核心前檢查；顯示文字組合後亦再檢查，避免分段顯示重新形成敏感原值。若任何內容含當前 Key，回傳 `Failed/SensitiveOutput`、固定安全提示、空 ToolCalls、null ModelMessage，並設定 RequiresContextReset 為 true；保留已取得的純數值用量，不保留或回傳原始回應作為附件。 |
| GEM-023 | 所有可顯示文字使用 RedactForDisplay，結構化診斷欄位沿用 BOOT-024 的完整欄位遮蔽；遮蔽是輸出保護，不是繼續使用敏感紀錄的授權。不得改寫含 Key 的帶簽章 Content 再聲稱可安全續傳。 |
| GEM-024 | 不輸出、記錄或快照 SDK 例外 Message／ToString、原始請求／回應、標頭、完整 URL 或 SDK 物件；不以例外的 InnerException 保存原文到核心結果。SDK 除錯、HTTP 記錄與自動遙測不啟用；不呼叫會自行向 stderr 傾印解析失敗內容的 JSON 輔助方法。 |

精確比對繼續採 BOOT-021 的 Ordinal 完整子字串規則，不新增變形秘密偵測承諾。正常 JSON 解析是為了檢查實際欄位值，並非對任意編碼秘密進行猜測；不解密 thought signatures，也不宣稱找得到經切割、編碼或轉換的其他憑證。

### 6.2 session 與釋放

| 編號 | 生命週期要求 |
|---|---|
| GEM-025 | 一個 GeminiClient 綁定一份 BootstrapSession 的認證與政策。SDK 若需要 string Key，只在建立認證物件時產生必要複本，不增加公開屬性或跨 session 快取；釋放 SDK client 後丟棄自身參考，不承諾 SDK 或受控記憶體的所有複本立即歸零。 |
| GEM-026 | DisposeAsync 停止接受新生成，取消並等待本 client 進行中的請求完成收尾，再釋放 SDK／HTTP 資源及持有參考；重複釋放安全。後續 GenerateAsync 回傳 `Failed/ClientUnavailable`，不送 HTTP。CLI 擁有 BootstrapSession，須先釋放 client 再釋放 session，不由 GeminiClient 擅自清除他人仍在使用的 session。 |
| GEM-027 | AuthenticationFailed 只回傳安全分類，不在 GeminiClient 內提示輸入或重送。第 03 份先在舊政策有效時完成舊錯誤顯示、停用及釋放舊 client，沿用隱藏輸入；取得新 Key 後清除全部舊 Context／協定紀錄，建立新 session／政策／client，再釋放舊 session。工作區與已知累計用量保留，不保存舊 Key 清單，也不自動重送原訊息；取消或 EOF 則退出，不回用舊 Key。 |

新的輸入也不得進入終端機回顯或診斷。第 03 份實作互動選單及所有權切換；本份以假 session／模型呼叫驗證原始例外不外洩、client 可釋放及舊協定紀錄不可跨 session 使用，不把尚未存在的聊天 Context 宣稱已清除。

## 7. 回應、錯誤與取消

### 7.1 模型回應分類

先做 GEM-022 的敏感檢查，再判斷拒絕／結束狀態及協定完整性，最後才發布文字或工具投影。即使部分文字可讀，也不得跳過不正常的 finish reason。

| API／回應條件 | 核心結果 | 可續傳與處理 |
|---|---|---|
| 單一候選、`STOP`、有非空白可見文字、無 functionCall | `Completed/None` | SafeText 為依序投影並遮蔽的文字；可帶完整 ModelMessage。 |
| 單一候選、`STOP`、有合法 functionCall，且有提供工具定義 | `ToolCalls/None` | 回傳全部呼叫及完整 ModelMessage；附帶文字不當作最終回答，不在本層執行工具。 |
| 沒有候選且無 block reason，或 `STOP` 僅空白／沒有可見文字及呼叫 | `Empty/None` | 不當作成功；無 ModelMessage。僅 thought 內容也不是可見回答。 |
| PromptFeedback 明確阻擋，或 finish reason 為 `SAFETY`、`RECITATION`、`BLOCKLIST`、`PROHIBITED_CONTENT`、`SPII` | `Refused/None` | 只回報固定拒絕狀態，不顯示被阻擋的部分輸出或回傳工具；無 ModelMessage。 |
| `MAX_TOKENS` 或 `CONTINUATION` | `Truncated/None` | 可帶安全的部分可見文字，但無可執行呼叫及 ModelMessage，不使用 continuation token 補送請求。 |
| `MALFORMED_FUNCTION_CALL`、`UNEXPECTED_TOOL_CALL`、`TOO_MANY_TOOL_CALLS`，或未提供工具卻收到呼叫 | `Failed/UnexpectedToolCall` | 不執行；無 ModelMessage，RequiresContextReset 為 true。 |
| 無法解析 JSON、候選超過 1、缺少正常結束原因、未知結束原因、非預期資料種類或不合法呼叫結構 | `Failed/InvalidResponse` | 不猜測成功、不部分執行；無 ModelMessage，RequiresContextReset 為 true。 |
| 回應含當前 Key | `Failed/SensitiveOutput` | 依 GEM-022 優先阻擋，不能保留原始敏感模型紀錄。 |

工具呼叫的 Name 必須非空，Arguments 必須為 object；不從自由文字、Markdown JSON、XML 或 shell 片段推斷呼叫。結構合法但工具名稱未知／業務參數不合法的呼叫，留給第 05 份 ToolRegistry 形成受控失敗結果；不在本層引入工具授權或路徑檢查。第一階段 Tools 為空時，仍依 GEM-019 一律拒絕呼叫。

若截斷、Empty、Refused 或傳輸失敗發生於已有待回填呼叫的工具鏈，第 03／05 份仍須判斷是否無法形成合法完整回合並清除 Context。`RequiresContextReset = false` 只代表本轉接器沒有額外要求清除，不保證上層未完成的鏈可繼續。

### 7.2 錯誤映射

| 編號 | 具體要求 |
|---|---|
| GEM-028 | 依第 7.1 節回傳明確模型狀態；文字與工具同時出現時優先回傳工具狀態。截斷／拒絕／不合法結構中即使帶工具資料，也不回傳可執行呼叫；未知 SDK enum 值採 InvalidResponse，不預設為 STOP。 |
| GEM-029 | 錯誤依下表固定分類及安全訊息。判斷使用 HTTP 狀態、SDK 的受控狀態欄位及必要的結構化 ErrorInfo，不以原始例外文字是否含關鍵字推斷認證；不能把每個 400 或 403 都判為 Key 錯誤。 |
| GEM-030 | SDK 例外若未公開 ErrorInfo，於注入的傳輸層只在記憶體以最多 65,536 位元組解析錯誤 body 的 status 與 `google.rpc.ErrorInfo` 的 domain／reason；只把白名單分類碼交給 GeminiClient，其他 message、metadata、原始 JSON 及例外不跨邊界。過大、格式錯誤或未知 reason 退回 HTTP 一般分類，仍不輸出 body。 |
| GEM-031 | 外部 token 已取消時回傳 `Cancelled/None`；否則連結期限或本次 SDK 請求逾時回傳 `Failed/Timeout`。兩者均停止本次流程、不重試；外部取消與期限同時成立時以 Cancelled 為準。傳輸已開始不代表服務端不會計費，沒有回應用量時維持不可得。 |

| 條件 | ErrorCode／Status | 固定安全訊息 |
|---|---|---|
| HTTP 401 或可信認證 ErrorInfo | `Failed/AuthenticationFailed` | 「Gemini 認證失敗；請重新輸入 Key 或退出。」 |
| HTTP 404 | `Failed/ModelUnavailable` | 「模型或 API 路徑不可用；請檢查模型設定與授權。」 |
| HTTP 403 且無可信認證 reason | `Failed/PermissionDenied` | 「Gemini 存取遭拒；請檢查服務與模型權限。」 |
| HTTP 429 | `Failed/QuotaOrRateLimited` | 「Gemini 配額不足或請求受限；本次未自動重試。」 |
| HTTP 400／422，且無可信認證 reason | `Failed/InvalidRequest` | 「Gemini 請求不被接受；請檢查模型設定或縮小輸入。」 |
| HTTP 408／504，或本次期限觸發 | `Failed/Timeout` | 「Gemini 請求逾時；本次未自動重試。」 |
| 其他 HTTP 5xx | `Failed/ServiceUnavailable` | 「Gemini 服務暫時無法完成請求；本次未自動重試。」 |
| DNS、TLS 或一般連線失敗 | `Failed/NetworkFailure` | 「無法連線至 Gemini；請檢查網路。」 |
| 其他非成功 HTTP 狀態或未知 SDK 失敗 | `Failed/InvalidResponse` | 「Gemini 回應無法處理；本次流程已停止。」 |
| 呼叫前資料／配對不合法 | `Failed/InvalidRequest` | 「模型請求資料不完整或不合法；未送出請求。」 |
| 最終請求 body 超限 | `Failed/InputTooLarge` | 「模型輸入超出本機上限；請縮小內容或清除對話。」 |
| 輸入含當前 Key | `Failed/SensitiveInput` | 「模型輸入含當前 Key；已阻擋本次傳送。」 |
| 輸出含當前 Key | `Failed/SensitiveOutput` | 「模型回應含敏感資訊；已阻擋後續使用，需清除對話。」 |
| client 已釋放 | `Failed/ClientUnavailable` | 「模型用戶端已結束；未送出請求。」 |
| 使用者取消 | `Cancelled/None` | 「已取消本次請求。」 |

可信認證 reason 僅接受 400／401／403 中 `domain = googleapis.com` 的 `API_KEY_INVALID`、`API_KEY_EXPIRED`；其他 reason 保留一般分類，不回顯未知值。HTTP 401 本身即為認證失敗。錯誤分類不是保證可確認所有服務端細節，亦不自動修正模型、調降安全設定或增加另一個請求。

## 8. 用量與請求計數

| 編號 | 具體要求 |
|---|---|
| GEM-032 | 對應 UsageMetadata 的 `promptTokenCount`、`candidatesTokenCount`、`thoughtsTokenCount`、`cachedContentTokenCount`、`totalTokenCount` 至 LlmUsage 的 PromptTokens、OutputTokens、ThoughtTokens、CachedTokens、TotalTokens。未提供欄位為 null；整份缺失則 Usage 為 null。零值若由 API 明確提供則保留 0。負值視為該欄無效並標記不可得。 |
| GEM-033 | TotalTokens 直接採 API 數值，不用輸入加輸出補算；ThoughtTokens／CachedTokens 是不同含義的欄位，不能再加到 TotalTokens，亦不把 CachedTokens 當成額外輸入。Refused、Truncated、Empty 或 SensitiveOutput 若已有合法數值用量仍回傳，不能因回答未成功就丟棄。 |
| GEM-034 | 第 03 份負責跨請求累計：每個欄位分別保存已知 long 合計與缺值的已送出請求數。曾缺值時顯示「已知小計／不完整」；從未取得值顯示「不可得」，不顯示完整零用量。未送出的本機拒絕不增加缺值數。累計溢位時標記該欄不可完整累計，不回繞；不為統計失敗重送請求。 |
| GEM-035 | 請求次數與 token 用量分開：依 RequestDispatched 記錄已嘗試傳輸，不以 Usage 是否存在推斷是否呼叫。第 05 份限制每使用者回合最多 5 次生成請求；GeminiClient 本身不執行循環，也不擅自補送第 6 次。本機統計不是 Google 正式帳單，不固定價格或宣稱免費額度。 |

第 02 份以單次結果及合成的結果序列驗證用量交接規則；第 03 份實作累計保存與顯示。`/clear`、更換 Key 保留已知累計；退出程序後不持久化。SDK 有提供其他用量欄位時，不為了顯示它們擴大本份公開契約；未來需求再另外修訂。

## 9. 驗收條件與測試案例

### 9.1 測試方法與證據

| 編號 | 驗收要求 |
|---|---|
| GEM-036 | 沿用 MSTest，透過 `ClientOptions.HttpClientFactory` 注入自製假 HttpMessageHandler；每個 SDK 轉接案例走真實序列化及回應解析，不只用假的 ILlmClient 代替全部驗收。最內層假 handler 記錄次數及必要布林判斷，絕不連至網路。 |
| GEM-037 | 所有 Key 使用 `FAKE_KEY_FOR_TESTS_ONLY` 等無認證功能值；假回應與錯誤僅存記憶體。斷言只報案例 ID、狀態、數值或布林，不傾印 request、response、模型訊息或秘密；不用真實 Key 的環境變數、檔案或傳輸錄製。 |
| GEM-038 | 於相關程式完成後記錄命令、退出碼、測試數及未測項目，區分本份自動驗收、後續串接與真實 API 人工驗收。第 01 份既有測試保持通過，只有已預告的模型必填預期需更新；不以舊測試紀錄代替當次結果。 |

### 9.2 第 02 份獨立自動驗收

| 案例 | 需求 | 情境與預期結果 |
|---|---|---|
| GEM-AC-001 | GEM-001～003 | 專案固定 SDK 版本、保留 net10.0／MSTest；假 handler 收到 Generate Content 請求，契約未公開 SDK 型別；不採 Interactions、串流或其他模型框架。 |
| GEM-AC-002 | GEM-002、004～005 | 預設模型正確；模型 null、空白、控制字元、過長、路徑／URL／query／fragment 及非允許字元均在讀 Key 前失敗，退出 2；合法名稱首尾空白去除。`--help` 仍不讀設定。 |
| GEM-AC-003 | GEM-004、010、020 | 假 handler 驗證官方 HTTPS 主機、v1beta 路徑、無 query／fragment，Key 只在 x-goog-api-key；URL／一般 body 不含 Key。故意提供非官方端點或 302 時拒絕或受控失敗，不跟隨重新導向。 |
| GEM-AC-004 | GEM-004、020 | 使用互相衝突的假環境值及 SDK 全域 Base URL，實際仍採顯式 Developer API、官方端點與 session Key；不讀取工作區檔案。測試需隔離程序或序列化並完整還原測試自行設定的程序狀態。 |
| GEM-AC-005 | GEM-006～007 | 假 handler 驗證 CandidateCount 1、MaxOutputTokens 8192，及生效的 120 秒期限／120000 毫秒設定。以可控制期限或內部時間注入測試逾時，不要求測試真的等待 120 秒，不暴露產品設定捷徑。 |
| GEM-AC-006 | GEM-009 | 以實際序列化 body 建立 262,144／262,145 位元組邊界；前者傳輸 1 次，後者 InputTooLarge 且 0 次。涵蓋中文 UTF-8、多位元組字元、JSON 跳脫、工具 Schema 與簽章，證明並非字元加總。 |
| GEM-AC-007 | GEM-008、035 | 模擬 429、500、503、連線失敗及 CONTINUATION：每次 GenerateAsync 最多傳輸 1 次。建構 client、輸入拒絕與釋放不額外呼叫；無認證測試、摘要、模型列舉或 token 計數。 |
| GEM-AC-008 | GEM-011～014 | 空訊息、不合法請求、並行第二次呼叫在送出前拒絕；合法訊息順序不變。請求完成後改動原清單或原 JSON 文件不影響保留的快照，ToString／序列化不能暴露不透明協定資料。 |
| GEM-AC-009 | GEM-015～016、028 | STOP 純文字、多個 text parts、文字間 thought parts：依序顯示可見文字；不顯示 thought 或簽章，原始 model Content 保持完整，可供下一次合法請求使用。 |
| GEM-AC-010 | GEM-015～019、028 | 假工具回應含單次、多次、同名不同參數及文字混合呼叫；取得全部 PartIndex、名稱、JSON 及有值／缺省 CallId，狀態為 ToolCalls，不執行工具、不把附帶文字當完成。 |
| GEM-AC-011 | GEM-015、017～018 | 模擬「呼叫 A → 假結果 → 呼叫 B → 假結果 → 文字」；每一步由測試顯式再呼叫一次 client，核對完整 Content、簽章原值與位置、ID、順序均保存。原 JsonDocument 釋放後參數及結果仍有效。 |
| GEM-AC-012 | GEM-017～018 | 同一回應兩個同名、無 ID 的呼叫依 PartIndex 配對；結果依原順序合併在完整 model Content 之後。遺漏、重複、錯名、錯 ID 或錯位置的結果不送出，不覆蓋其他呼叫。 |
| GEM-AC-013 | GEM-019、028 | 正式空 Tools 收到任何呼叫時 UnexpectedToolCall；假工具下空工具名、非 object 參數或非正常結束的工具資料均不執行，不解析自由文字成工具。 |
| GEM-AC-014 | GEM-014、028 | 無候選、僅空白及僅 thought 為 Empty；block reason 與各拒絕 finish reason 為 Refused；均無可續傳 ModelMessage。 |
| GEM-AC-015 | GEM-008、028 | MAX_TOKENS／CONTINUATION 帶文字及工具資料：Truncated、最多顯示安全部分文字、無可執行呼叫或 ModelMessage、無自動續傳。 |
| GEM-AC-016 | GEM-014、028 | 損壞 JSON、多候選、缺少／未知 finish reason、非預期資料或非法呼叫結構：InvalidResponse 或指定工具錯誤，要求清除時旗標為 true，不把部分資料當成功。 |
| GEM-AC-017 | GEM-020～021 | 假 Key 分別混入應用指示、當前文字、歷史、工具描述／Schema 欄位名稱／值、參數與結果，均 SensitiveInput 且最內層 handler 0 次。含 JSON 跳脫的假 Key 依實際值檢出；必要認證標頭仍可設定。 |
| GEM-AC-018 | GEM-015、022～024 | 假回應的文字、跨 text parts 組合文字、工具名稱／ID／巢狀參數及保留的協定字串含 Key：SensitiveOutput、固定安全提示、空呼叫、null ModelMessage、要求清除；用量仍保留，不以修改簽章後續傳。 |
| GEM-AC-019 | GEM-023～024、029～030、037 | 假 SDK 例外、ErrorInfo metadata、HTTP body、已知憑證欄位及無效 JSON 含假 Key：stdout、stderr、核心結果及斷言訊息均無原值或原始傾印。已知欄位含巢狀／大小寫變化時沿用既有遮蔽。 |
| GEM-AC-020 | GEM-029～030 | 分別模擬 401、可信 API_KEY_INVALID／API_KEY_EXPIRED、一般 400、一般 403、404、429、408／504 及 5xx；固定分類正確。錯誤 reason 來自未知 domain、不在白名單、body 過大或無效時不誤判認證、不回顯原文。 |
| GEM-AC-021 | GEM-007、014、031 | 呼叫前取消為 Cancelled 且 0 次；傳輸中取消不再送出、RequestDispatched 為 true；期限為 Timeout，外部取消同時成立時以 Cancelled 為準；遲到回應不發布成功。 |
| GEM-AC-022 | GEM-025～027 | client 建立及釋放不產生 API；進行中釋放會取消並收尾，重複釋放安全，釋放後 ClientUnavailable；舊 session 紀錄不能由新 client 使用，不修改原工作區或持久化 Key。 |
| GEM-AC-023 | GEM-032～033 | 完整用量、明確 0、整份缺失、部分缺失、負數及 Refused／Truncated／Empty／SensitiveOutput 帶用量，逐欄符合可空及保留規則；Total 不以其他欄位補算，不重複加入 thought／cached。 |
| GEM-AC-024 | GEM-014、034～035 | 合成已送出／未送出、部分缺值及超過 int 範圍的結果序列，核對交接的 long 數值、缺值旗標與請求計數；列出累計溢位預期。真正累計狀態與 CLI 顯示依 GEM-AC-026 驗收。 |
| GEM-AC-025 | GEM-005、020、036～038 | 更新模型 null 的預期後執行完整既有測試；工作區確認、Key 隱藏輸入、JSON 嚴格解析、精確比對及排除規則不被放寬；測試均不使用真實 Gemini 或憑證。 |

### 9.3 後續整合責任

| 案例 | 需求 | 負責階段與成功條件 |
|---|---|---|
| GEM-AC-026 | GEM-011、014、022、027、031～035 | 第 03 份：正常訊息才送出第一個生成請求；安全顯示、完整回合與 5 回合裁切；取消後回到輸入。實測逐欄已知累計、缺值提示、溢位、clear／換 Key 保留用量及敏感回應後清除 Context。 |
| GEM-AC-027 | GEM-020～027、029 | 第 03 份，接續第 01 份 AC-018：認證失敗提供隱藏重新輸入或退出；舊錯誤先整理，新 session 及政策一次切換，舊 client／Context 不再使用；取消退出且不自動重送。 |
| GEM-AC-028 | GEM-015～019、021～022、028、035 | 第 04～05 份：真實工具結果與參數的敏感檢查、未知工具受控失敗、多呼叫依序執行、合法配對；第 5 次仍要求工具時不執行新操作，也不送第 6 次生成。 |
| GEM-AC-029 | GEM-020～024、033、037～038 | 第 06～07 份：寫入預覽、工具完成後 API 失敗的本機結果、整體用量與產物檢查；不洩漏 Key、不因後續模型失敗重做工具。 |

第 02 份獨立完成需通過 GEM-AC-001～GEM-AC-025；GEM-AC-026～GEM-AC-029 是明確的後續交接，不計為已測。第 01 份 AC-017 的模型必填與認證傳輸由本份落實，實際聊天的首次請求由第 03 份串接，不提前增加測試生成。

### 9.4 後續建置與驗收命令

以下從儲存庫根目錄依序執行，採目前已有的 Microsoft.Testing.Platform 模式；本次文件工作未執行這些命令：

```sh
dotnet restore tests/AIAgent.Cli.Tests/AIAgent.Cli.Tests.csproj
dotnet build tests/AIAgent.Cli.Tests/AIAgent.Cli.Tests.csproj --no-restore
dotnet test --project tests/AIAgent.Cli.Tests/AIAgent.Cli.Tests.csproj --no-build
dotnet run --project src/AIAgent.Cli/AIAgent.Cli.csproj --no-build -- --help
git diff --check
```

產品與測試專案的既有警告視為錯誤設定須保留。未接上第 03 份之前，`dotnet run` 的正常啟動不能拿來聲稱聊天已可用；以獨立模型測試驗收本份。不可為了測試新增真實 Key 參數或硬編碼生成訊息。

### 9.5 真實 Gemini 人工驗收

第 03 份接上正常互動後，由使用者在本機終端機自行提供 Key，送出一個實際需求，確認可見回答、用量或不可得提示，以及取消／退出。需驗證認證失敗時，以使用者再次輸入正常需求才開始新請求；不在報告貼 Key、原始傳輸或可含 Key 的錯誤。

此項是獨立的服務可用性驗收，不由本次文件工作或自動測試代辦。未執行時明確標示「真實 Gemini API 未驗證」；官方模型頁列出能力不保證特定 Key、地區、配額或執行時服務可用，也不保證回答內容正確。讀檔與寫入的真實驗收仍等待對應工具階段。

## 10. 本次文件檢查與下一步

本次文件核對：目的、範圍、流程、模組責任、驗收與後續延伸完整；GEM 需求及驗收案例可追溯；Markdown 相對連結與程式碼區塊完整；與總規格第 16 章及第 01 份交接一致。變更範圍只能新增本檔，既有來源與設定不得改動。

新檔未追蹤時，`git diff --check` 本身不涵蓋它；另以新檔內容的空白／區塊／連結檢查及 `git diff --no-index --check -- /dev/null specs/02-gemini-client.md` 核對。no-index 比較新增內容時可因存在差異而退出 1，須同時確認沒有空白錯誤輸出，不能只看退出碼判定。這些是文件檢查，不是建置、SDK 型別編譯、MSTest、終端機操作或真實 API 驗證。

後續先依本份開發 GeminiClient、必要的模型設定收緊及假傳輸測試，完成獨立驗收並記錄結果，再展開第 03 份 CLI 與 Context 子規格。串流、自動重試、自動續傳、進階思考調校、模型切換、精確 token 計數、快取、模型備援與對話持久化均需另訂規格，不自動納入本份。

## 參考資料

以下為 2026-10-20 查核的官方來源；本份固定選擇不因線上最新範例改用其他模型、API 模式或套件版本。引用範例中的環境變數與 Key 輸出方式不取代第 01 份的隱藏輸入及記憶體限制。

- [Google.GenAI 1.26.1：固定套件版本與框架相容性](https://www.nuget.org/packages/Google.GenAI/1.26.1)
- [Gemini 3.5 Flash-Lite：穩定模型及 function calling 能力](https://ai.google.dev/gemini-api/docs/models/gemini-3.5-flash-lite)
- [Google GenAI .NET SDK：Models.GenerateContentAsync](https://googleapis.github.io/dotnet-genai/api/Google.GenAI.Models.html)
- [HttpOptions：端點、API 版本及毫秒逾時](https://googleapis.github.io/dotnet-genai/api/Google.GenAI.Types.HttpOptions.html)
- [HttpRetryOptions：Attempts 包含首次嘗試](https://googleapis.github.io/dotnet-genai/api/Google.GenAI.Types.HttpRetryOptions.html)
- [GenerateContentConfig：輸出限制及 AutomaticContinuation](https://googleapis.github.io/dotnet-genai/api/Google.GenAI.Types.GenerateContentConfig.html)
- [ClientOptions：HttpClientFactory 測試傳輸入口](https://googleapis.github.io/dotnet-genai/api/Google.GenAI.Types.ClientOptions.html)
- [Thought signatures：Generate Content 的完整紀錄與順序](https://ai.google.dev/gemini-api/docs/generate-content/thought-signatures)
- [Generate Content API：回應、finish reason 及 UsageMetadata](https://ai.google.dev/api/generate-content)
- [Google AIP-193：HTTP／RPC 錯誤與結構化 ErrorInfo](https://google.aip.dev/193)
- [Using Gemini API keys：認證與 Key 保護](https://ai.google.dev/gemini-api/docs/api-key)
- [官方 .NET SDK ApiClient 原始碼：明確覆寫環境來源及傳輸設定](https://github.com/googleapis/dotnet-genai/blob/main/Google.GenAI/ApiClient.cs)
- [官方 .NET SDK HttpApiClient 原始碼：認證標頭及例外邊界](https://github.com/googleapis/dotnet-genai/blob/main/Google.GenAI/HttpApiClient.cs)
