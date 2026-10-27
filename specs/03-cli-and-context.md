# 03：CLI 與 Context 子規格

## 第 17 份產品實作接續（2026-10-27～28）

新增六種 /skills 本機命令與 LoadingSkills 單一讀取者生命週期；命令不入歷史、零生成、啟用後仍等新訊息，clear／換 Key／退出丟啟用內容。詳見[第 17 份](17-reusable-workflows-and-skills.md)及[當次分層驗證](../README.md#skills-validation)。歷史文件交付敘述保留，未結原生／真人／真實 Gemini／Release 門檻不變。

## 第 16 份產品實作接續（2026-10-27）

四個 `/checkpoint` 命令為 AwaitingInput 本機入口，零模型回合；命令與核准不入歷史。復原必須目前 Execute／Valid 規則、完整新預覽及單檔新確認；發布後先顯示事實，再處理取消、EOF、模式及規則收尾。共同清除與換 Key 丟棄檢查點及最近復原結果，ID／復原代次不重用。 詳見 [第16份](16-change-checkpoints-and-recovery.md) 與 [當次分層證據](../README.md#checkpoint-validation)。下列原階段敘述保留歷史範圍。

## 第 15 份產品實作接續（2026-10-26）

[工作保存與續接](15-work-save-and-resume.md)的限定增量：新增 `/work save TITLE`、`list`、`show ID`、`delete ID` 與啟動續接。只有 save 必要時生成一次摘要；命令及新確認不進草稿歷史。續接後進入 Plan 並等待新訊息，`/execute` 仍另取確認；clear 只清記憶體，不刪磁碟存檔。 本節保留舊階段歷史，實際分層驗證見 [README](../README.md#work-save-validation)。

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

> 文件版本：0.1｜建立日期：2026-10-20｜狀態：待實作規格
>
> 上位文件：[入門 AI Agent 開發總規格書](../SPEC.md)，主要依據第 6、8～9、14～16 章。前置規格：[01：專案啟動與 API Key](01-bootstrap-and-key.md)、[02：Gemini 用戶端](02-gemini-client.md)。目前已完成的能力以 [README](../README.md) 與現有程式介面為準；前兩份文件的「待實作」及「本次只新增文件」描述其撰寫當時的交付，不代表目前仍未完成。

## 1. 目的、交付邊界與完成標準

將既有啟動流程與 GeminiClient 接成可持續操作的純文字 CLI。使用者可聊天、清除歷史、取消生成、退出，並在認證失敗後安全重新輸入 Key；Context 只保留有限完整回合，用量則跨回合保留於程序記憶體。

本次交付只新增本檔，不修改 README、總規格、前兩份規格、產品、設定、測試、專案或套件；不建立其他規格或空工具，不安裝 hooks，不提交、推送或改寫歷史。以下契約、畫面及命令都是後續開發要求，不是本次已實作或已通過的結果。

| 編號 | 具體要求 |
|---|---|
| CLI-001 | 後續實作沿用既有 .NET 10 Console、MSTest 與分階段架構，先驗收 macOS 終端機。保留可空值分析、警告視為錯誤、原有啟動參數及設定限制，不升級 SDK 或增加產品套件。 |
| CLI-002 | 本份只接入文字對話；正式 `LlmRequest.Tools` 必須為空，每個使用者回合至多呼叫一次 `GenerateAsync`。不執行工具、重試、續傳、摘要、測試生成、模型列舉或 token 計數。 |
| CLI-003 | 對話、當前回合、本機結果、Key 與用量只存在記憶體；不讀取工作區檔案內容，不建立日誌、輸入歷史、對話存檔或遙測。不加入串流、多行編輯、模型切換、費用估算、GUI 或背景回合。 |

本份開發的獨立完成點為：通過第 11 節離線及 PTY 驗收，實測 macOS 終端機操作，並留下當次結果。第一階段「可以聊天」的真實服務能力另須通過正常 Gemini 聊天人工驗收；未執行時必須標示「真實 Gemini API 未驗證」。第 04～07 份的讀檔、寫入與整體交付不是本份完成條件。

## 2. 既有能力、依賴與跨階段責任

### 2.1 已存在的開發基線

以下以本次讀取的原始碼及 README 確認；本次未重新執行其建置、測試或終端機驗收，也不將既有測試數列為本次證據。

| 既有能力 | 本份接續方式 |
|---|---|
| `net10.0`、.NET SDK 10.0.100 相容修補版、`Google.GenAI 1.26.1`、`MSTest.Sdk/4.5.1`、Microsoft.Testing.Platform | 保持版本與測試模式，新增 CLI／Context 的相關實作與驗收。 |
| `Bootstrapper.StartAsync`、`BootstrapResult`、`BootstrapSession` | 成功啟動後移交 session，再進入聊天；失敗、拒絕與 help 仍依既有結果退出。 |
| 固定實體工作區、嚴格設定、隱藏 Key 輸入 | 不重新讀設定、不重新確認或切換工作區；重新輸入沿用原 Key 規則。 |
| `ILlmClient.GenerateAsync(LlmRequest, CancellationToken)` | 由聊天協調器選好歷史後呼叫；模型層仍不管理 CLI 或回合裁切。 |
| 不透明 `LlmMessage` 與 session／client 所有權檢查 | 原樣保留模型訊息，不反序列化、不以安全顯示文字重建。 |
| `LlmResult` 的狀態、錯誤、用量、傳輸旗標及清除旗標 | 分別決定顯示、歷史提交、統計與故障收尾，不能只判斷 SafeText。 |
| `ISensitiveDataPolicy` 與受控傳輸 | 延伸至一般輸入、顯示及歷史選取；模型層的實際 JSON 檢查繼續生效。 |

現有 `Program.cs` 在啟動成功後只建立並釋放 client，尚未聊天；現有全域 Ctrl+C 直接取消程序的方式須在後續開發時調整為第 7 節的狀態式取消。現有 PTY 腳本預期輸入 Key 後退出，也須在本份開發時更新為進入聊天後明確退出，保留原有防洩漏案例。

### 2.2 責任歸屬

| 元件或後續階段 | 責任與界線 |
|---|---|
| 本份聊天協調器 | 輸入迴圈、狀態、單次生成、回合提交、取消、重新認證及用量記帳。 |
| ConversationContext | 完整回合快照、最近 5 回合、請求訊息順序及清除。 |
| CLI 轉接器 | 按鍵、隱藏輸入、安全純文字顯示、控制訊號與終端機復原。 |
| 既有 GeminiClient | 原有傳輸、認證、協定保存、120 秒期限、8192 輸出 tokens、262144 位元組 body 上限及結果分類，不由 CLI 重複實作。 |
| 第 04 份 | 實際列檔、讀檔、工作區操作邊界及讀取快照。 |
| 第 05 份 | AgentRunner、工具登錄與派送、完整工具鏈、每回合最多 5 次生成。第 03 份的一次生成是文字聊天限制，並未改寫總規格的工具回合上限。 |
| 第 06～07 份 | 寫入確認、衝突、操作結果、整體成本、提交及發布檢查。 |

## 3. 模組分工、核心契約與資源所有權

### 3.1 最小責任單位

| 編號 | 具體要求 |
|---|---|
| CLI-004 | `Program.cs` 只組裝、執行啟動、移交 session 及回傳退出碼。`ChatApplication` 協調聊天，透過互動及 runtime 工廠介面運作；核心、Context 與 CLI 契約不得公開 SDK、HTTP、Key 或原始例外型別。 |
| CLI-005 | 成功移交後 ChatApplication 擁有當前 session 及 client，始終先停止／等待並釋放 client，再釋放其 session。更換及最終退出都要處理部分建構失敗，移交與釋放不可重複取用秘密。 |
| CLI-006 | 含輸入、顯示文字、訊息或 session 的型別採密封類別、唯讀屬性及必要的防禦性複製；`ToString()` 為固定型別標記，不提供內容傾印或公開序列化。純數值統計可用 record；不透明訊息繼續遵守既有序列化禁令。 |

### 3.2 後續實作的契約形狀

下列是規格中的宣告形狀，尚未加入產品；所有新聊天契約預設為 `internal`，不擴大現有公開 API。介面中的各結果型別定義於下表。

```csharp
internal interface IChatInteraction
{
    ValueTask<ChatInputResult> ReadInputAsync(CancellationToken cancellationToken);
    ValueTask<KeyReplacementChoice> ChooseKeyReplacementAsync(CancellationToken cancellationToken);
    ValueTask<KeyInputResult> ReadKeyAsync(CancellationToken cancellationToken);
    IDisposable ObserveBusyInput(Action<ChatControlSignal> onSignal);
    void SetPolicy(ISensitiveDataPolicy policy);
    void Show(ChatEvent message);
}

internal interface IChatRuntimeFactory
{
    BootstrapSession CreateReplacementSession(
        ConfirmedWorkspace workspace, PublicSettings settings, KeyInputResult acceptedInput);
    ILlmClient CreateClient(BootstrapSession session);
}
```

| 型別或操作 | 必要形狀與語意 |
|---|---|
| `ChatApplication` | 建構時注入 `IChatInteraction`、`IChatRuntimeFactory`、`ConversationContext`、`UsageAccumulator` 與固定應用指示；`Task<int> RunAsync(BootstrapSession initialSession, CancellationToken applicationCancellation)` 取得 session 所有權，單次執行並回傳退出碼。 |
| `ChatState` | `AwaitingInput`、`Generating`、`Cancelling`、`AwaitingKeyChoice`、`ReadingReplacementKey`、`Stopping`；不設尚未支援的工具／寫入狀態。 |
| `ChatInputStatus`／`ChatInputResult` | 狀態為 `Submitted`、`Invalid`、`EndOfInput`、`Cancelled`、`Unavailable`；只有 Submitted 可有 `string Text`，其他狀態不帶部分原文。Submitted 尚須經協調器的 Key 檢查。 |
| `KeyReplacementChoice` | `Reenter`、`Exit`、`EndOfInput`、`Cancelled`、`Unavailable`；無效選擇由轉接器顯示固定提示後重問，不把原文交給核心。 |
| `ChatControlSignal` | `CancelTurn`、`EndOfInput`、`Unavailable`；只由 busy 觀察期間回報，不承載任何按鍵文字。 |
| `ChatEventKind`／`ChatEvent` | 種類為 `Ready`、`InputRejected`、`UserMessage`、`Generating`、`Answer`、`TurnEnded`、`ContextCleared`、`Usage`、`Stopping`。事件包含 Kind，以及按種類使用的可空 Text、Status、ErrorCode、TurnUsage、SessionUsage 和布林 RequestDispatched；不帶原始請求、模型訊息或例外。 |
| `ConversationContext` | `int CompletedTurnCount`、`IReadOnlyList<LlmMessage> BuildMessages(LlmMessage currentUserMessage)`、`void CommitCompletedTurn(IReadOnlyList<LlmMessage> messages)`、`void Clear()`。BuildMessages 回傳有序唯讀快照；本份提交只接受 user／model 兩則完整文字回合。 |
| `UsageAccumulator` | `void Record(LlmResult result)`、`UsageSnapshot Snapshot()`；單一協調器依每次生成結果呼叫一次，不保存結果物件清單來去重。 |
| `UsageFieldSnapshot` | 純數值資料：`long KnownTotal`、`bool HasKnownValue`、`long MissingRequestCount`、`bool Overflowed`。 |
| `UsageSnapshot` | `long DispatchedRequests`、`bool CounterOverflowed` 及 Prompt、Output、Thought、Cached、Total 五份 UsageFieldSnapshot。 |
| `LastTurnOutcome` | 只保存最近一次已開始回合的 `LlmStatus Status`、`LlmErrorCode ErrorCode`、`bool RequestDispatched`、`bool ContextWasReset`；不保存輸入或模型內容。 |

ChatEvent 中的 Text 只供 UserMessage、Answer 及受控狀態文字使用；Usage 使用 `LlmUsage? TurnUsage` 與 `UsageSnapshot? SessionUsage`。非該事件使用的欄位保持 null／false，不將傳輸結果整包交給顯示層。轉接器依結構化種類決定標籤，組合區塊後仍依第 9 節處理。

ObserveBusyInput 在生成、取消及回合收尾期間維持無回顯控制輸入；只回報控制訊號，消耗其他按鍵而不形成訊息。它須與其他讀取互斥，釋放時等待停止讀取並丟棄已緩衝按鍵，再允許下一個提示。回呼只提交取消／停止旗標，不直接變更 Context、顯示或釋放資源；協調器依序收尾。

初始 session 移交新增內部 `BootstrapResult.TakeSession()`：只允許 Ready 且尚未移交時取出，成功後將 Session 設為 null，失敗只拋固定內部錯誤。如此外層釋放 BootstrapResult 不會先釋放正在使用的 session。替換 session 工廠只從 Accepted 的 KeyInputResult 一次轉移內部秘密；client 工廠的產品實作建立既有 GeminiClient，測試可注入假 client。工廠建構失敗由當前擁有者清理，KeyInputResult 不得留下第二份所有權。

## 4. CLI 狀態、指令解析及單行輸入

### 4.1 狀態與本機指令

| 編號 | 具體要求 |
|---|---|
| CLI-007 | 啟動成功並建立 client 後進入 AwaitingInput；輸入有效一般訊息才進入 Generating。取消先進 Cancelling 並等待本次工作；認證失敗先完成回合收尾再進 AwaitingKeyChoice。Stopping 後不再開始生成或讀取。狀態轉換由單一協調器負責。 |
| CLI-008 | 對整行採 `.Trim()` 只作空白及指令判斷；`/help`、`/clear`、`/exit` 以 Ordinal 完整匹配，區分大小寫。Trim 後以 `/` 開頭的其他內容皆為未知指令，不送模型、不回顯原值；不加入參數、別名或斜線跳脫規則。一般文字保留原始首尾空白。 |
| CLI-009 | `/help` 顯示三個指令、空白／EOF／Ctrl+C 行為、單行上限、輸入期間不回顯，以及目前只有聊天、沒有檔案工具的能力。啟動後顯示一次簡短就緒提示；`--help` 繼續只處理啟動用法，不讀設定或 Key、不建立 client。 |
| CLI-010 | `/clear` 立即清除歷史、當前暫存及 LastTurnOutcome，顯示固定完成提示，保留 Key、工作區、模型與累計用量；不送模型、不刪除檔案。空歷史可重複清除。 |
| CLI-011 | AwaitingInput 的 `/exit`、EOF 或 Ctrl+D 丟棄部分輸入後正常退出；Ctrl+C 丟棄部分輸入後以 130 退出。不以退出時的 API 錯誤狀態代替退出原因。 |

指令實際為三個；`--help` 是啟動參數，與 `/help` 不同。` /clear ` 可辨識，`/CLEAR`、`/help extra`、`/` 與 `/tmp/a` 皆為未知指令。一般訊息即使看起來像 JSON、XML 或 shell，也只作文字輸入，不轉為本機操作。

### 4.2 輸入、顯示與資源上限

| 編號 | 具體要求 |
|---|---|
| CLI-012 | 一般聊天及指令輸入期間均不回顯字元、星號、長度或片段。Enter 後先解析本機指令；已知指令只顯示操作結果，未知指令只顯示固定錯誤。一般訊息經當前 Key 檢查通過後，才顯示「使用者：」及安全文字並開始回合。 |
| CLI-013 | `MaxInputCodeUnits = 16384`，計算整行未 Trim 的 UTF-16 code units；16384 可接受、16385 整筆拒絕。允許中文及合法 Unicode，代理配對的補充平面字元計 2；不正規化或默默替換非法代理字元。不允許一般文字含控制字元或 Unicode 行／段分隔字元 U+2028、U+2029。 |
| CLI-014 | Enter 提交，Backspace／Delete 刪除最後一個 Unicode scalar，空緩衝區刪除無動作；導覽鍵不移動游標或改變內容。超限或收到非法文字後，該行永久標記無效，保持有界緩衝並隱藏消耗至 Enter，再整筆拒絕；刪除不會恢復成有效行。Ctrl+C／Ctrl+D 優先執行取消／EOF，不等待 Enter。 |
| CLI-015 | 生成、取消及收尾期間不排隊、不回顯普通按鍵，不保留給下一個訊息或 Key 提示。只接受取消、EOF 與 I/O 失敗等控制訊號；新提示開啟前丟棄前狀態已緩衝的按鍵。任一時刻只允許一個終端機讀取者。 |

提示固定包含「輸入期間不回顯；Enter 後顯示安全訊息」，輸入提示使用 `你> `。無效行提示為「輸入無效或超過 16384 個 UTF-16 單位；本次未送出。」未知指令提示為「未知指令；請輸入 /help 查看操作。」這些固定文字仍需經顯示政策。

Enter 的 CR／LF 是提交事件，不存入文字；CRLF 視為同一次 Enter。空白輸入無操作。單行模式不提供多行貼上編輯或合併；換行可能等同 Enter，忙碌期間的後續貼上內容會丟棄，不自動形成下一個回合。不主動讀取剪貼簿，也不建立按鍵紀錄。

已知本機指令不屬於模型內容，即使字面上含當前 Key 的子字串仍可執行，且不回顯輸入原文；其結果提示照常遮蔽。這避免合法但很短的測試 Key 意外封鎖 `/clear` 或 `/exit`。

## 5. 一般聊天回合與模型結果處理

### 5.1 正常流程

1. 取得 Submitted 行，依第 4 節處理空白及本機指令。
2. 檢查一般訊息未含當前 Key；未通過時拒絕且不建立回合。
3. 建立 `LlmMessage.FromUserText`；以 Context 的 BuildMessages 選取歷史並附上當前訊息。
4. 建立 `LlmRequest`，包含固定應用指示、有序訊息及空工具清單；進入 Generating，啟動 busy 輸入觀察與本回合取消範圍，顯示處理中狀態。
5. 至多呼叫一次 GenerateAsync，等待完成；依第 7 節解決取消與完成競爭。
6. 對該結果記帳一次，檢查清除旗標、敏感顯示內容與結果合法性，再依下表顯示及決定是否提交。
7. 更新唯一的 LastTurnOutcome，顯示單次及累計用量；清除當前暫存，關閉 busy 觀察後回到輸入，或依認證／退出原因轉入其他狀態。

| 編號 | 具體要求 |
|---|---|
| CLI-016 | client 建構、就緒、help、空白、clear、重新輸入及退出均不生成。只有正常使用者訊息觸發首次生成；每次請求重新選取歷史，不由 CLI 呼叫原始 SDK、HTTP 或另一個認證測試。 |
| CLI-017 | 只有 Completed、ErrorCode 為 None、非空白安全可見文字、非 null ModelMessage、空 ToolCalls、未取消且無清除要求，才視為成功並提交完整回合。缺少必要模型紀錄或互相矛盾的結果視為 InvalidResponse，清空 Context，不捏造模型回答。 |
| CLI-018 | Empty、Refused、Truncated、Cancelled、Failed 均不提交當前回合；一般失敗保留原先成功歷史。截斷可顯示安全部分文字，但前置固定「回答未完整產生」，不續傳、不當作成功歷史。未知／不合法結果採受控失敗。 |
| CLI-019 | `RequiresContextReset = true` 優先於成功判斷：丟棄當前回合及全部歷史，自動顯示「模型 Context 已清除；請開始新的對話。」並保留 Key、工作區及用量，不等待 /clear。敏感內容一律不顯示或保存；合法數值用量仍記帳。 |
| CLI-020 | 只保留最近一份 LastTurnOutcome，失敗不另建無限成長的錯誤歷史；本機狀態、提示與用量不加入模型訊息。沒有實際工具時不顯示讀檔／寫入成功或假工具結果。 |

### 5.2 結果與下一狀態

| 結果 | 顯示及歷史 | 下一步 |
|---|---|---|
| 合法 Completed | 顯示安全最終回答，提交 user 與原始 ModelMessage | AwaitingInput。 |
| Empty／Refused | 固定狀態，無本回合歷史 | AwaitingInput。 |
| Truncated | 明確未完成標示及安全部分文字，無本回合歷史 | AwaitingInput，不補送。 |
| Cancelled | 固定取消提示，無本回合歷史 | 收尾後 AwaitingInput；另有程序停止原因時退出。 |
| Failed／AuthenticationFailed | 舊政策下顯示固定認證錯誤，無本回合歷史 | 完成用量收尾後，依第 8 節。 |
| Failed／InputTooLarge | 提示縮小訊息或 /clear，不私自裁切更多歷史 | AwaitingInput，原成功歷史保留。 |
| Failed／SensitiveInput | 固定阻擋提示，不回顯內容 | 無強制清除旗標時保留原成功歷史；旗標為 true 時依 CLI-019。 |
| Failed／SensitiveOutput、InvalidResponse、UnexpectedToolCall，或任何清除旗標 | 固定安全錯誤及已清除通知，不顯示可疑原文 | 清空 Context 後 AwaitingInput。 |
| 模型不可用、權限、配額、網路、逾時或一般 InvalidRequest | 沿用第 02 份安全分類，不將它們一律視為 Key 錯誤 | AwaitingInput，無自動重試。 |
| ToolCalls 或非空工具資料 | 本份未授權工具，視為 UnexpectedToolCall，無派送、無工具結果 | 清空 Context 後 AwaitingInput。 |
| ClientUnavailable 或未預期的本機例外 | 固定內部失敗，不自動重建 client 或顯示例外 | Stopping，退出 1。 |

清除旗標不會取消應有的認證選單或程序停止原因；例如帶清除要求的認證失敗，仍先清除並完成用量，再進選單。終端機已失效時不強求顯示成功，不因畫面輸出失敗重送生成。

## 6. 完整回合儲存、歷史選取與裁切

| 編號 | 具體要求 |
|---|---|
| CLI-021 | 本階段完整回合固定為「一份使用者訊息＋該次合法 Completed 的完整 ModelMessage」。提交資料作唯讀清單快照；只保留必要的訊息參考，不另複製顯示文字、SDK JSON 或完整 LlmResult。 |
| CLI-022 | `MaxCompletedTurns = 5`。BuildMessages 依時間順序展開所有保留的完整回合，再加當前使用者訊息；當前回合不計入這 5 個歷史回合。成功提交後，才從最舊端移除超額完整回合，不按訊息數裁切。 |
| CLI-023 | 保存原始不透明 ModelMessage，包括轉接層內必要 parts、順序、thought 與簽章；不檢視、列印、修改或用 SafeText 代替。BuildMessages 不更改原訊息內容，且只在當前 client／session 內使用。 |
| CLI-024 | 失敗、取消、拒答、空回應或截斷不得留下孤立使用者訊息；提交前才取消也不得提交。超出完整 body 上限時整次停止，不額外淘汰歷史重試、不摘要。 |
| CLI-025 | Clear 清除全部回合及本地當前暫存，不清除固定應用指示、認證、設定或用量。第 04 份加入快照後須串接同一清除流程；第 05 份加入工具鏈後須擴充完整回合驗證與保存，不在本份先實作工具配對。 |

用 `Ui` 表示第 i 回合使用者訊息，`Mi` 表示對應完整模型訊息：

| 時點 | 本次 Messages | 成功後保留歷史 |
|---|---|---|
| 第 1 回合 | U1 | [U1, M1]。 |
| 第 5 回合 | U1, M1, …, U4, M4, U5 | 第 1～5 回合。 |
| 第 6 回合 | U1, M1, …, U5, M5, U6，共 11 份 Content | 第 2～6 回合。 |
| 第 7 回合 | U2, M2, …, U6, M6, U7，共 11 份 Content | 第 3～7 回合。 |
| 第 6 回合失敗後重問 | 第 1～5 回合，加新的使用者訊息；不含失敗的 U6 | 新回合成功後才淘汰第 1 回合。 |
| /clear 或強制清除後 | 只有新的使用者訊息 | 從新的成功回合開始。 |

固定應用指示使用 LlmRequest.SystemInstruction 傳遞，不偽裝為歷史 user 訊息，也不受 5 回合裁切影響。Context 只釋放被淘汰參考，不承諾能使 runtime 或 SDK 的全部複本立即歸零。

## 7. 取消、故障收尾及退出碼

### 7.1 程序與回合生命週期

| 編號 | 具體要求 |
|---|---|
| CLI-026 | 分開管理程序停止與每回合取消；每回合建立新的取消來源，連結程序 token 後交給 GenerateAsync。取消某一回合不得取消供後續讀取或回合使用的程序 token。既有 120 秒模型期限仍由 GeminiClient 管理。 |
| CLI-027 | AwaitingInput、AwaitingKeyChoice、ReadingReplacementKey 收到 Ctrl+C 時退出 130；Generating 收到時只取消當前回合，Cancelling 中的重複 Ctrl+C 不開啟新回合、不重送、不改成成功。外部 SIGINT 與按鍵 Ctrl+C 應依相同狀態路由，避免同一訊號處理兩次。 |
| CLI-028 | 回合結束以「檢查取消／程序停止並決定是否提交」為提交點。在此之前觀察到取消，就算假 client 回傳遲到 Completed，亦不得顯示成功或提交；仍對實際返回結果記帳一次。提交點之後才發生的取消不回溯已提交回合，接續狀態處理。 |
| CLI-029 | 生成或取消期間遇 EOF／Ctrl+D，取消並等待目前請求、記帳及必要安全收尾後正常退出 0；I/O 失敗則以 1 收尾。這些停止原因優先於回到輸入或認證選單，不把 Ctrl+D 當一般文字。 |
| CLI-030 | 所有退出路徑解除終端機事件、關閉輸入觀察、恢復輸入狀態，先釋放 client 再釋放 session，丟棄 Context 及用量。退出碼依下表；API 回合失敗本身不強迫程序退出。 |
| CLI-031 | 預期模型錯誤使用 LlmResult；互動、工廠或未預期本機例外映射固定失敗並安全退出，不輸出 Message／ToString／InnerException。清理失敗改為退出 1，不嘗試以新生成修補。 |

忙碌期間普通文字及 `/exit` 字元都會丟棄；取消用 Ctrl+C，結束輸入用 Ctrl+D。取消後必須等待原請求真正完成收尾才顯示下一個輸入提示，不在原請求仍存活時開始第二次呼叫。busy 觀察與事件解除也須在 finally 路徑完成。

### 7.2 退出碼

| 條件 | 退出碼 |
|---|---|
| --help、不同意工作區、啟動 EOF | 維持第 01 份規則，0。 |
| /exit、聊天 EOF／Ctrl+D、認證選擇退出或重新輸入 EOF | 0。 |
| 等待聊天、認證選擇或隱藏輸入時 Ctrl+C；程序 token 取消 | 130。 |
| 生成期間 Ctrl+C | 不退出；回合取消後繼續等待輸入。之後正常退出為 0。 |
| 原有啟動參數或設定錯誤 | 2。 |
| 工作區／終端機／I/O、client 建構或釋放失敗、不可恢復本機失敗 | 1。 |

程序取消與 EOF 同時成立時，以明確程序取消的 130 為準；清理失敗的 1 再優先於正常／取消退出。模型層同時發生回合取消與逾時時，仍沿用外部取消優先的第 02 份契約。

## 8. 認證失敗、隱藏重新輸入與 session 切換

| 編號 | 具體要求 |
|---|---|
| CLI-032 | 只有 `Failed/AuthenticationFailed` 啟動重新輸入流程；模型不可用、PermissionDenied、配額、網路或逾時維持各自錯誤。不得用訊息文字猜測認證、不提供執行中換模型或自動換 Key。 |
| CLI-033 | 選單固定為「重新輸入 Key？[y/N]」。隱藏讀取並 Trim 後，y／yes 不分大小寫選 Reenter，空白、n／no 選 Exit；其他文字只重問，不回顯、不送模型。EOF 正常退出、Ctrl+C 退出 130、I/O 失敗退出 1。 |
| CLI-034 | 先在舊 policy 有效時顯示固定認證錯誤、完成當前用量及本機狀態，再停止並等待釋放舊 client。清除所有 busy 按鍵後才開啟選單；舊 session 暫時保留供遮蔽，不再使用舊 client 或開始生成。 |
| CLI-035 | Reenter 沿用第 01 份 HiddenKeyReader：不回顯、不顯示星號、同樣的 512 ASCII 上限及整筆拒絕。Accepted 後先清除全部舊 Context／協定參考，建立同工作區及設定的新 session／policy／client；成功後一次切換活動擁有者與顯示政策，最後釋放舊 session。 |
| CLI-036 | 新 session、client 或切換失敗時，釋放已建立資源並退出 1，不回退舊 Key。重新輸入取消或 EOF 退出，不採用部分 Key。成功後顯示「認證資料已更新；對話已清除，請重新輸入訊息。」保留累計，不自動重放失敗訊息或送出驗證生成；再次輸入原值也按新 session 處理。 |

所有權順序固定如下：

```text
舊請求完成 → 舊政策下顯示與記帳 → 釋放舊 client
→ 隱藏選擇與新 Key 輸入（仍使用舊顯示政策）
→ 清空舊 Context → 建立新 session → 建立新 client
→ 切換活動 session／client／顯示政策 → 釋放舊 session
→ 等待使用者的新訊息
```

選擇退出及任何失敗都要釋放仍持有的 session。新政策生效後不可再輸出舊錯誤物件或延後的原始內容；不保留舊 Key 清單或指紋。累計用量是本程序各次傳輸的統計，不依 Key 分帳，也不是任一帳戶的正式帳單。

## 9. 顯示、歷史及輸入的敏感資訊防護

| 編號 | 具體要求 |
|---|---|
| CLI-037 | 在一般訊息回顯、建立當前回合及模型送出前，以既有政策檢查當前 Key；命中則整筆拒絕，不先遮蔽後送出。模型層繼續檢查選定歷史、固定指示與實際 body。CLI 亦再檢查待顯示的 SafeText，若不可信替身意外帶 Key，按 SensitiveOutput 丟棄及清空 Context。 |
| CLI-038 | 所有顯示先組合完整區塊，經終端機控制字元處理及 RedactForDisplay 後才寫出，包含標籤、提示、使用者文字、回答與用量。純文字不執行 ANSI／OSC 或其他控制序列；不將 thought、簽章或不透明訊息交給顯示層。 |
| CLI-039 | 沿用當前 Key 的 Ordinal 完整子字串規則，拒絕所有嵌入及重複出現；不承諾偵測任意編碼、變形或其他憑證。Key 只由既有隱藏輸入取得，不新增參數、環境、檔案、安全儲存或公開認證屬性。 |
| CLI-040 | 固定錯誤、SDK 診斷與已知憑證欄位遮蔽延續前兩份約定；不輸出或保存原始 HTTP／SDK／JSON、例外、輸入傾印、完整 URL 或回應附件。測試斷言與失敗報告也不能附秘密；舊政策下的錯誤必須在換 Key 前處理完。 |
| CLI-041 | 固定應用指示只定義產品角色、文字聊天能力、真實能力限制及資料保護責任；本文件不附完整提示內容。CLI 不提供提示內容、內部設定或協定資料的顯示／匯出指令；提示本身不能代替程式權限或 Key 檢查。 |

輸入含 Key 的固定提示為「模型輸入含當前 Key；已阻擋本次傳送。」不顯示遮蔽後的使用者原文、長度或位置。模型回應含 Key 時只顯示受控狀態與清除通知，不將遮蔽版本作為合法模型歷史。

顯示轉接器將 CRLF 正規化為換行，保留換行與 Tab；其餘 C0／C1 控制字元及單獨 CR 使用可見的 `\uXXXX` 文字表示，ESC 因而不能執行終端機指令。轉換後再對完整區塊遮蔽，避免標籤拼接或顯示轉換形成未檢查內容。此處只改顯示投影，不能改寫待續傳的 ModelMessage 或原使用者訊息。既有精確遮蔽不代表任意輸出編碼都可被辨識。

`/clear` 及 Context 強制清除只釋放記憶體參考，不清除終端機既有捲動紀錄、不刪檔、不保證 runtime 複本覆寫。無回顯與輸出處理須由實際轉接器落實，不能只測政策函式就宣稱聊天邊界已驗收。

## 10. 單次用量、程序累計與請求次數

### 10.1 計數及逐欄資料

| 編號 | 具體要求 |
|---|---|
| CLI-042 | 每次 GenerateAsync 返回後，協調器恰好 Record 一次。僅 RequestDispatched 為 true 才增加 DispatchedRequests 及各欄用量／缺值；false 的本機拒絕不增加任一欄缺值。取消與失敗亦遵守，不以成功、文字、HTTP 200 或 Usage 有無推斷已送出。 |
| CLI-043 | 分別累計 PromptTokens、OutputTokens、ThoughtTokens、CachedTokens、TotalTokens 的非負 long；null、整份缺失及負值視為該欄不可得。明確 0 是已知值。各欄不互相補算，Total 直接採 API 原值，thought／cached 不再加進 Total。 |
| CLI-044 | 每欄保存已知小計、是否曾有已知值、已送出但缺值的請求數及溢位旗標；從未取得值顯示「不可得」，有缺值且有已知值顯示「已知小計 N／不完整（M 次缺值）」，完整則顯示 N。無傳輸時次數可為 0，但 token 欄不得宣稱完整零用量。 |
| CLI-045 | 累計加法採 checked；token 小計溢位時保留最後可表示小計並永久標記「不可完整累計（溢位）」，不回繞、不飽和成看似精確的 long.MaxValue。後續仍處理其他欄與請求，不為統計重送。計數器溢位亦保留最後值並標記不完整。 |
| CLI-046 | 每個已開始且取得結果的回合收尾，顯示本次五欄用量、是否已嘗試傳輸，以及程序五欄累計與傳輸嘗試數，包含失敗、截斷、敏感輸出及取消。沒有模型結果的本機指令或輸入拒絕不產生虛構單次用量；不可用的欄位明示不可得。 |
| CLI-047 | /clear、強制清除及換 Key 不重置累計或溢位旗標；只有程序退出丟棄。不寫入價格、不估計費用或保證免費額度；明示傳輸嘗試不證明 Google 收到或計費，本機累計不是正式帳單。 |

UsageSnapshot.CounterOverflowed 表示 DispatchedRequests 計數溢位；各欄 Overflowed 表示該欄 KnownTotal 或 MissingRequestCount 溢位。token 小計溢位後不再增加該欄 KnownTotal，但仍更新 HasKnownValue、可表示的缺值計數及其他欄位；缺值計數溢位不阻止仍可表示的小計更新。輸出採十進位整數，不截為 int 或浮點數。

### 10.2 具體案例與顯示

以下只示範 Total 欄；其他四欄各自套用同一規則。

| 結果序列 | 傳輸次數 | Total 累計顯示 |
|---|---|---|
| 尚未送出，或本機拒絕且 RequestDispatched = false | 0 | 不可得。 |
| 已送出，Total = 0 | 1 | 0。 |
| 已送出，Total = null | 1 | 不可得（1 次缺值）。 |
| Total = 10，之後已送出但缺值 | 2 | 已知小計 10／不完整（1 次缺值）。 |
| 上述序列後，Total = 7 | 3 | 已知小計 17／不完整（1 次缺值）。 |
| Total = 3000000000 | 1 | 3000000000，不溢位為 int。 |
| Total = long.MaxValue，之後 Total = 1 | 2 | 已知小計 9223372036854775807／不可完整累計（溢位）。 |
| Failed／SensitiveOutput 但 Total = 17 且已送出 | 1 | 17；不因內容遭丟棄而丟失用量。 |

每回合使用固定順序「輸入／輸出／思考／快取／總計」，至少兩行顯示「本次用量」及「程序累計」，另顯示傳輸嘗試資訊。本次缺值不以累計值代填，累計缺值不因後來收到完整值而變成完整。RequestDispatched 為 false 時本次標示「未嘗試傳輸」，不採用不合契約的替身所附用量。

## 11. 需求對照、離線測試、PTY 與人工驗收

### 11.1 測試策略與限制

| 編號 | 具體要求 |
|---|---|
| CLI-048 | 自動測試只用無認證功能的假 Key、假互動、可控完成／取消的 ILlmClient 及假 handler，離線執行。Context 與 CLI 測試不得全由政策測試取代；SDK 串接案例走既有真實 SDK 轉換及最內層假 handler，絕不連至 Gemini。 |
| CLI-049 | 分開記錄離線自動測試、PTY、真人終端機及真實 Gemini。本次只做文件檢查；後續才執行本節案例與命令，留下實際退出碼、測試數、結果及未測項目，不沿用 README 的舊結果當新證據。 |
| CLI-050 | 後續開發保持第 01～02 份既有測試與安全限制通過，新增 acceptance-linked MSTest 名稱，例如 `CLIAC007_SixthTurnKeepsFivePriorTurnsBeforeCommit`。更新既有 PTY 的啟動完成預期，不刪除原 Key、取消、設定或敏感資訊回歸測試來消除失敗。 |
| CLI-051 | 文件完成檢查需涵蓋需求／案例對照、契約與數值一致、Markdown 連結及區塊、空白、新檔 no-index 檢查，以及只新增本檔的範圍；文件檢查不能取代產品編譯或行為驗收。 |

假模型結果可利用現有測試組件的內部可見權限建立測試專用不透明訊息，無須公開模型原始資料。真正協定保存另以 SDK／假 handler 驗證。PTY 的忙碌與認證分支若需測試專用 host，必須位於測試範圍、呼叫真正的互動轉接器與 ChatApplication，僅在測試組裝注入替身；不得新增產品假模型參數、環境 Key 或自訂端點。

### 11.2 本份離線自動驗收

| 案例 | 對應需求 | 情境與預期結果 |
|---|---|---|
| CLI-AC-001 | CLI-001～006、016、041 | 組裝後只進入聊天、無啟動生成；契約不公開 SDK／秘密，新內容型別不可傾印；內部 session 移交僅一次，外層結果釋放不破壞活動 session；版本與原設定不變。 |
| CLI-AC-002 | CLI-008～011、016 | 空白、三指令、重複 clear、大小寫、指令附參數、未知斜線與 /tmp/a：零模型呼叫，固定提示無原文。--help 在重新導向下仍不讀設定或 Key；聊天 help 正確描述目前能力。 |
| CLI-AC-003 | CLI-012～014、037、039 | 逐鍵與貼上假 Key：Enter 前零回顯，Enter 後固定拒絕，無模型／Context／原文輸出。安全中文文字完成後才顯示；已知本機指令與短 Key 字面重疊仍可執行。 |
| CLI-AC-004 | CLI-013～014 | 16384／16385 單位、含首尾空白、補充平面字元、合法代理對、未配對代理、Tab／控制字元及行分隔字元：邊界正確。超限後刪除仍拒絕，消耗至 Enter，下一行可正常使用。 |
| CLI-AC-005 | CLI-008、012～015 | Backspace／Delete、空刪除、中文與代理對刪除、導覽鍵、CRLF、空白及部分輸入 EOF／Ctrl+C：不破壞 Unicode、不洩漏或誤提交；一般文字保留原始首尾空白，無輸入歷史。 |
| CLI-AC-006 | CLI-002、007、015～017 | 首次正常訊息產生一次 GenerateAsync；Tools 為空，訊息與固定指示位置正確。阻塞假 client 期間輸入其他文字／指令再釋放：無並行、無第二回合、無殘留按鍵帶入下一提示。 |
| CLI-AC-007 | CLI-021～024 | 連續 7 個成功回合：第 6／7 次請求各有 5 個先前完整回合及當前 user；第 6 次送出前不提前淘汰，提交後只保留最新 5 回合，順序與原始訊息身分不變。 |
| CLI-AC-008 | CLI-006、021～023 | Commit 的來源清單及 BuildMessages 回傳快照不可被外部修改影響；只提交兩則 user／model。本份不接受工具鏈或任意原文重建的模型訊息，不為未來工具建立空實作。 |
| CLI-AC-009 | CLI-017～020、024 | Empty、Refused、Truncated、Cancelled、一般 Failed：無孤立 user／假回答，原成功歷史保留；截斷有未完成標示，不續傳。下一次提問不含失敗輸入；本機只留最近狀態。 |
| CLI-AC-010 | CLI-010、019、025、047 | /clear、重複 clear 及清除旗標：下一請求無舊歷史，固定指示仍在，Key／工作區／模型／用量保留；clear 無檔案操作。強制清除立即通知且可直接開始新對話。 |
| CLI-AC-011 | CLI-017、019、023、037～038 | 真實 SDK／假 handler 跨至少兩次聊天驗證完整模型 Content、parts 與簽章續傳，顯示不含 thought／簽章；不以顯示文字替代協定。敏感回應後再送新訊息不含任何舊 Context。 |
| CLI-AC-012 | CLI-017～019、031 | 假 Completed 缺模型訊息、帶工具、空白或錯誤碼，ToolCalls，以及不合法 enum：不執行工具、不部分成功；按 InvalidResponse 或 UnexpectedToolCall 清空 Context。ClientUnavailable 則安全退出 1。 |
| CLI-AC-013 | CLI-018、024、032 | 262144／262145 實際 body 邊界沿用 SDK 假傳輸：超限不增加傳輸、不改原歷史、不裁切重試；配額、模型、權限、網路及逾時分開顯示，均不錯誤進入認證流程。 |
| CLI-AC-014 | CLI-026～028、042 | 生成前與進行中取消，含按鍵及外部取消路由：取消 token 正確傳到 client；回合收尾後第二次正常訊息能成功，未取消程序 token，未重送，使用量只記一次。 |
| CLI-AC-015 | CLI-027～028、043、046 | 可控競爭驗證「取消已觀察、假 client 遲到 Completed」不顯示成功或提交，但保留已取得數值用量；提交後的訊號不回溯歷史。外部取消與模型期限同時成立仍為取消。 |
| CLI-AC-016 | CLI-011、027、029～031 | 等待輸入、生成中、取消中及認證各狀態的 EOF、Ctrl+D、Ctrl+C、重複取消、I/O 失敗與程序 token 取消，退出碼及優先順序正確，無第二次模型呼叫。 |
| CLI-AC-017 | CLI-005、015、030～031 | 正常退出、輸出失敗、工廠建構失敗及釋放失敗：解除事件／觀察器、釋放順序為 client 後 session，無活躍請求或失效政策再使用；固定錯誤不帶例外內容。 |
| CLI-AC-018 | CLI-032～034、040、042 | AuthenticationFailed 先以舊政策顯示安全錯誤與用量，再釋放 client，才開啟隱藏選單。真假認證分類不靠例外文字；忙碌時鍵入的 y 或 Key 不能成為選擇或新 Key。 |
| CLI-AC-019 | CLI-033、035～036、047 | yes／y 重新輸入、空白／no 退出、非法選擇重問；Accepted 後清除 Context，新 session／policy／client 一次切換，舊 session 最後釋放。工作區／模型／累計保留，零驗證生成，原訊息不自動重送。 |
| CLI-AC-020 | CLI-005、030、033～036 | 重新輸入空白、非法字元、512／513 邊界、取消、EOF、新 session／client 失敗與再次輸入相同 Key：沿用隱藏規則，退出／清理正確，不用舊 client、不回退舊 Key、不保存部分輸入。 |
| CLI-AC-021 | CLI-023、035～036、039～040 | 換 Key 前舊錯誤含假舊 Key，換後新回應含假新 Key：兩個時點皆不外洩；新 client 不接收任何舊 ModelMessage。不保留舊 Key 清單、指紋或舊錯誤內容供延後顯示。 |
| CLI-AC-022 | CLI-019、037～040 | SDK 敏感輸出及不可信假 client 的 SafeText 含 Key：無回顯、無提交、無續用；Context 自動清空，合法用量保留，stdout／stderr／斷言只有安全狀態。涵蓋重複、嵌入與跨 text parts。 |
| CLI-AC-023 | CLI-012、038、040～041 | 完整顯示區塊的標籤／文字拼接、CRLF、單獨 CR、ESC／OSC、Tab、固定提示與控制字元轉換：不執行控制序列，最後遮蔽仍生效；無 thought／簽章／原始例外／提示內容傾印。 |
| CLI-AC-024 | CLI-042～044、046 | 完整、部分、整份缺失、負值及明確零用量逐欄核對；Total 採原值，不加 thought／cached。本次與累計各自顯示，不以缺值補零或使用其他欄位補算。 |
| CLI-AC-025 | CLI-042～044、046 | 已送出／未送出、成功／取消／拒答／截斷／失敗／SensitiveOutput 的合成序列：傳輸次數及逐欄缺值正確，每次只記一次；未送出卻帶用量的不合法替身不能污染累計。 |
| CLI-AC-026 | CLI-043～045 | 3000000000、long.MaxValue 再加 1，及測試內部注入接近上限的計數器：不 int 截斷、不回繞，各欄獨立；溢位標記保留最後有效小計且不因稍後的零值消失。 |
| CLI-AC-027 | CLI-010、019、036、044～047 | 缺值→已知值→缺值→已知值，以及 /clear、強制清除、換 Key：已知小計、缺值數、傳輸次數及溢位旗標不重置，不完整不變完整；退出不持久化。 |
| CLI-AC-028 | CLI-001～003、048～050 | 執行完整既有與新增 MSTest；假 handler 證明沒有額外生成或外網連線，無工作區內容讀取或寫入、無新 Key 來源／產品測試捷徑。保留原回歸測試，失敗報告不傾印敏感內容。 |

### 11.3 macOS PTY 自動驗收

PTY 是自動模擬終端機，不等於真人操作。所有 Key 皆為假值；截取內容僅在記憶體斷言，不寫終端機錄製檔或在失敗時傾印原始資料。

| 案例 | 對應需求 | 情境與預期結果 |
|---|---|---|
| CLI-AC-029 | CLI-009～014、030、048～050 | 更新既有 terminal_smoke：Key 後進入聊天，先驗證一般輸入不回顯，再以本機指令或 /exit 結束；保留既有隱藏 Key、512／513、無效輸入、重新導向及 --help 案例，全程離線。 |
| CLI-AC-030 | CLI-007、015、026～030、048 | 測試 host 的假模型保持忙碌，輸入普通文字、Ctrl+C 與外部 SIGINT；無回顯／排隊，回合取消後可再次輸入，等待提示時 Ctrl+C 則退出 130，終端機 ECHO／ICANON／ISIG 等原始狀態恢復。 |
| CLI-AC-031 | CLI-015、029～030、032～040、048 | 假認證失敗後隱藏選單及重新輸入：新舊 Key 均不出現；忙碌按鍵不流入選單，新 session 不自動送出；EOF、Ctrl+C 及錯誤退出均恢復終端機。 |
| CLI-AC-032 | CLI-012～014、038、048～049 | 真正轉接器處理中文、補充平面字元、刪除、CRLF、無效／超長輸入與安全輸出控制字元；畫面及應用輸出無當前 Key、星號或原始控制序列，退出碼正確。 |

### 11.4 真人終端機與真實服務驗收

| 案例 | 對應需求 | 操作與預期結果 |
|---|---|---|
| CLI-AC-033 | CLI-007～015、027～030、038、049 | 使用者在 macOS 真實終端機，以假 Key 檢查啟動、中文／輸入法、貼上、刪除、三指令、等待時取消與 EOF，以及退出後回顯正常。不得因 PTY 通過就宣稱本項完成。 |
| CLI-AC-034 | CLI-016～019、022～024、026～028、042～049 | 使用者自行隱藏輸入真實 Key，送出正常需求，核對回答、接續對話、超過 5 回合、/clear、生成中取消後再聊天、用量或不可得提示及退出。沒有額外認證生成；只記錄安全結果，不貼 Key 或原始傳輸。 |
| CLI-AC-035 | CLI-032～040、047、049 | 以無認證功能的假值觸發實際服務認證失敗，再由使用者隱藏輸入可用 Key；確認 Context 清空、累計保留且等待新的使用者訊息才生成。服務回傳不同分類時記錄實際安全分類，不假稱通過認證選單；不更改正式 Key 來製造失敗。 |

自動測試與測試 host 不取得真實 Key。CLI-AC-034～035 由使用者日後獨立執行；未執行或服務無法提供必要情境時，明確保留未驗證項目，不因此讓開發測試連線至 Gemini。模型回應成功不表示內容正確，也不表示已讀取或修改任何檔案。

### 11.5 上游承接與後續交接

| 既有或後續責任 | 本份對照與完成界線 |
|---|---|
| 第 02 份 GEM-AC-026 | CLI-AC-003、006～017、022～027、029～034：首次正常請求、安全顯示、5 回合、取消、Context 清除及累計用量。 |
| 第 02 份 GEM-AC-027；第 01 份 AC-018 | CLI-AC-018～022、031、035：認證選單、隱藏重新輸入、舊錯誤處理、新 session、清除與不自動重送。 |
| 第 01 份 AC-017 的首次正常請求 | CLI-AC-001、006、034：不額外測試生成；模型必填與認證位置仍由第 02 份既有測試保證。 |
| 第 04～05 份；GEM-AC-028 | 真實檔案與結果檢查、工具配對、完整工具回合、快照清除、多步及每回合 5 次生成另驗收。本份只保證空 Tools 不會執行操作。 |
| 第 06～07 份；GEM-AC-029 | 差異預覽、已完成操作與後續 API 失敗、寫入安全及產物檢查另驗收；不將文字聊天案例當作檔案工具證據。 |

### 11.6 後續開發完成後執行的命令

以下從儲存庫根目錄依序執行；本次文件工作未執行產品命令。測試專案 build 同時建置產品，build 與 run／test 不並行。

```sh
dotnet restore tests/AIAgent.Cli.Tests/AIAgent.Cli.Tests.csproj
dotnet build tests/AIAgent.Cli.Tests/AIAgent.Cli.Tests.csproj --no-restore
dotnet test --project tests/AIAgent.Cli.Tests/AIAgent.Cli.Tests.csproj --no-build
dotnet run --project src/AIAgent.Cli/AIAgent.Cli.csproj --no-build -- --help
python3 tests/terminal_smoke.py
dotnet format src/AIAgent.Cli/AIAgent.Cli.csproj --verify-no-changes
git diff --check
```

人工操作另外執行下列命令，不在指令、環境或檔案放 Key：

```sh
dotnet run --project src/AIAgent.Cli/AIAgent.Cli.csproj --no-build -- --workspace "."
```

後續開發的離線完成條件為 CLI-AC-001～032 通過、完整既有測試回歸及格式檢查通過；macOS 操作另需 CLI-AC-033，真實服務另列 CLI-AC-034～035。有未驗證項目時精確說明完成範圍，不能只以「第一階段全部通過」概括。

## 12. 文件檢查、完成條件及後續交接

本次文件驗收獨立列為 CLI-AC-036，不計入第 11 節的產品開發通過數：

| 案例 | 對應需求 | 情境與預期結果 |
|---|---|---|
| CLI-AC-036 | CLI-049、051 | 核對本節文件清單、需求／案例對照、連結、區塊、空白及單檔新增範圍；只記錄實際文件檢查，不宣稱產品、PTY 或服務驗證完成。 |

本次文件完成只驗證以下項目：

- 目的、範圍、流程、模組責任、驗收與後續延伸齊全，且與總規格第 16 章一致。
- CLI-001～051 與 CLI-AC-001～036 可追溯；5 回合、16384 單位、512 Key 上限及模型層既有上限沒有互相取代。
- C# 契約名稱、結果欄位、生命週期、取消及換 Key 順序前後一致；無未定項目、空章節或未授權的後續工具。
- 相對連結存在、程式碼區塊閉合、Markdown 表格與空白正確；只新增本檔，既有檔案保持原內容。
- 新檔尚未追蹤時，除 `git diff --check` 外，另執行 `git diff --no-index --check -- /dev/null specs/03-cli-and-context.md` 並核對實際內容。no-index 若因新增差異退出 1，須確認無空白錯誤；不能只依退出碼推斷通過。

本次不執行 restore、build、MSTest、PTY、真實終端機、執行期敏感資訊測試或 Gemini 認證。第 11 節是待執行的驗收規格，不是本次測試報告；規格完成也不表示聊天已接入。

下一步先依本份開發並驗收 CLI／Context，更新開發當次的操作說明及驗證結果，再展開第 04 份工作區與唯讀工具規格。工具鏈、讀取快照、寫入確認及最終提交／發布檢查保持明確交接；長期記憶、對話保存、摘要、串流、輸入歷史、自動重試及其他平台支援皆另訂規格，不自動納入本份。
