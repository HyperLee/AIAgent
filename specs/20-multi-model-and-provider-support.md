# 20：多模型與供應商支援開發規格書

> 文件版本：0.1｜建立日期：2026-10-29｜文件狀態：待實作
>
> 選題依據：[ROADMAP：FUT-13](../ROADMAP.md#fut-13多模型與供應商支援)。上位文件：[總規格](../SPEC.md)第 5～10、14～15 章。FUT-13 是項目代號，20 是子規格編號。本次交付規格書與本檔內的固定交接 memo；產品、測試與服務驗收另行執行。

## 1. 目的、方案與交付邊界

讓使用者依工作需求明確選擇模型，並在相同的工作區、工具、逐次核准與預算政策下使用 Gemini 或 OpenAI。選擇模型是本機使用者行為，不交給模型自動路由；更換模型不代表增加工具或放寬完成條件。

| 編號 | 要求 |
|---|---|
| MODEL-001 | 本次只新增本檔，不修改產品、測試、套件、設定、README、ROADMAP、總規格、既有子規格、Git 索引或歷史；不另建 memo、實作計畫、工作樹、hook 或驗收轉錄檔，不提交、推送或發布。下列流程、內部介面與驗收案例均是後續開發要求。 |
| MODEL-002 | 文件核對基線為乾淨 `main@bef53e2a84e16ae2bdb710a30deaa750210fa84f`。FUT-01～12 的對應產品已接入，FUT-02 僅受控建置、test 不支援且完整驗收未完成；README 的歷史測試數不是本次證據。ROADMAP 第 1 節「其餘項目仍為未開發方向」與後續逐項狀態不一致，應以逐項接續、README 及目前程式核對，不能據該句重新設計已交付能力。 |
| MODEL-003 | 使用者已選定「Gemini＋OpenAI 分階段規格」。A 階段提供 Gemini 明確選模及共同切換生命週期；B 階段加入 OpenAI 官方 Responses API。A 可獨立交付，但不能宣稱多供應商完成；FUT-13 功能完成需 A、B 各自取得證據。 |
| MODEL-004 | 排除自動路由、失敗備援、跨模型重送、在途切換、模型比較／競賽、角色自動選模、多 Agent、第三供應商、自架／代理／Azure／OpenAI 相容端點、OAuth、Key 儲存、環境認證、串流、多模態、供應商託管工具、背景生成及正式金額預算。 |
| MODEL-005 | 保留單一活動 client、單一在途回合、同一 Runner／GenerationBudget、逐筆寫入／程序／MCP 核准、原生隔離與單一路徑刪除限制。第 07 份 BUD-S003 Release／分發、各階段真人／真實服務及第 10 份原生／MTP 未結門檻保持分列，不因新增供應商解除。 |
| MODEL-006 | FUT-13 必要依賴現有模型契約、認證生命週期及 FUT-04 的 Context／共用預算；對 FUT-05～12 是既有能力相容責任，不是新功能的必要前置清單。不必與任何 FUT 合併開發，也不要求先完成其他 FUT 的全部延伸或真人驗收才撰寫本規格。 |

方案取捨：採兩個供應商各自的原生轉接器，共用 SDK 無關的 Core 與 Runner。統一成 OpenAI 相容協定會遺失 Gemini 原生 parts／簽章與錯誤語意，因此不採用；通用外掛供應商引擎超出首版需求。模型清單、端點及能力由本機固定目錄管理，便於驗證實際送出點。

以下為本次設計決策，並非使用者另外指定的產品細節：初始模型目錄採第 3 節三個 profile；同供應商換模型也重新輸入 Key；任何實際切換都清除工作階段資料，不自動搬運摘要或 pins。這犧牲即時續聊便利，換取明確的認證、原生協定與核准邊界。需要保留工作時，使用者可在切換前自行 `/work save`，之後按第 15 份以重啟續接與新確認匯入背景。

## 2. 現況與開發前必讀

目前 `PublicSettings` 僅有 Model；設定只接受 `Gemini.Model`。`ChatRuntimeFactory` 固定建立 GeminiClient，原生紀錄綁 client owner；GeminiContextInspector 擁有估算與安全摘要來源投影。ChatApplication 的換 Key 路徑會清除 Context／Plan／快照／檢查點／Skills／MCP 狀態，重建工作區服務並重載規則；程序生成與用量不退款。不能只把設定中的模型名稱改掉，就認為完成多模型切換。

1. 先讀 [ROADMAP](../ROADMAP.md)、[README 最新驗證](../README.md#latest-validation)及 [總規格](../SPEC.md)，核對當時分支、產品能力與未結門檻。
2. 讀 [01：啟動與 Key](01-bootstrap-and-key.md)、[02：Gemini](02-gemini-client.md)、[03：CLI／Context](03-cli-and-context.md)、[05：Runner](05-agent-loop.md)、[07：預算與掃描](07-budget-and-acceptance.md)，釐清認證、原生歷史、錯誤與唯一記帳。
3. 讀 [09：Plan](09-plan-mode-and-task-list.md)、[11：Context／預算](11-context-and-budget-management.md)，尤其 CTX-007、014、033、050～064、071～075；多模型不重定義摘要白名單、核准或完成事實。檔案／程序行為另讀 [04](04-workspace-and-read-tools.md)、[06](06-file-edit-and-approval.md)、[08](08-code-search-and-partial-read.md)、[10](10-controlled-build-and-test.md)。
4. 逐筆讀第 12～19 份的八筆 FUT-13 固定交接：[RULE-HO-011](12-project-rules-loading.md#future-handoffs)、[GIT-HO-011](13-git-diff-and-code-review.md#future-handoffs)、[TERM-HO-010](14-terminal-interaction.md#future-handoffs)、[SAVE-HO-011](15-work-save-and-resume.md#future-handoffs)、[CKPT-HO-012](16-change-checkpoints-and-recovery.md#future-handoffs)、[SKILL-HO-012](17-reusable-workflows-and-skills.md#future-handoffs)、[MCP-HO-012](18-mcp-external-tools.md#future-handoffs)、[WEB-HO-012](19-web-search-and-document-fetch.md#future-handoffs)。本檔[上游承接表](#upstream-handoffs)不能取代原條文。
5. 核對當時 SettingsLoader／PublicSettings、BootstrapSession／SessionSecrets／SensitiveDataPolicy、ChatRuntimeFactory／ChatApplication、LlmMessage／ToolCallPairing、GeminiProtocol／Transport／Inspectors、AgentRunner／ContextCoordinator／UsageAccumulator、WorkPersistencePolicy 與敏感掃描器。只讀必要來源，不把真實指示、Key 或原生紀錄附入文件／報告。

## 3. 分階段模型目錄、能力與設定

### 3.1 固定 profile

| 階段 | Provider／Model（Ordinal 精確值） | API／端點 | 首版能力 |
|---|---|---|---|
| A | `gemini`／`gemini-3.5-flash-lite` | 現有 Developer API `v1beta` Generate Content | 文字、多輪、function calling、既有 Context inspector。保留預設。 |
| A | `gemini`／`gemini-3.6-flash` | 同上 | 同一工具與 Context 契約，保留其原生 parts／簽章；須獨立驗收。 |
| B | `openai`／`gpt-6.1-terra` | `POST https://api.openai.com/v1/responses` | 文字、多輪、自訂 function calling、Context inspector；明確 `reasoning.effort="none"`。 |

Google 官方列出上述兩個 Gemini 模型與工具能力；這裡新增 3.6 是為驗證第二個 Gemini profile，沒有改用最新模型的要求。[Gemini 3.5 Flash-Lite](https://ai.google.dev/gemini-api/docs/models/gemini-3.5-flash-lite)、[Gemini 3.6 Flash](https://ai.google.dev/gemini-api/docs/models/gemini-3.6-flash)。OpenAI 官方目前列出 Terra 的 `none` reasoning 與 Responses 工具支援；選用它是本次設計提案，不保證使用者帳戶可用。[GPT-6.1 Terra](https://developers.openai.com/api/docs/models/gpt-6.1-terra)。

| 編號 | 要求 |
|---|---|
| MODEL-007 | A 目錄恰含兩個 Gemini profile，B 追加一個 OpenAI profile；不任意接受語法合法但未登錄的模型，不做遠端模型列舉或認證探測。加入其他模型須明示目錄／能力／驗收增量，不自動追隨服務端新模型。 |
| MODEL-008 | 目錄與能力只代表本產品准入，不能宣稱服務授權、配額、延遲、價格或品質已確認。官方模型／API 變更或服務停用時回固定錯誤，先修訂本規格並補驗，不靜默換模型、端點、SDK 或協定。 |
| MODEL-009 | 每個 profile 的不可變能力描述包含 Provider、Model、ProtocolKind、文字／function calling／非串流／安全歷史投影／容量量測支援與 RequestBodyLimit。三個首版 profile 均須滿足整套現有能力；不能只支援聊天而默默隱藏工具或停用摘要。當前模式完整工具集合無法表示時，整次本機拒絕，不刪減 Schema 或降權續跑。 |
| MODEL-010 | Provider 只接受上述小寫列舉；Model 沿現有 1～128 個小寫 ASCII 字母、數字、點、連字號或底線及首字元限制，再作目錄比對。禁止 URL、路徑、query、fragment、控制字元、Unicode 混淆、未知大小寫與額外字串。 |
| MODEL-011 | 新設定根恰含 `Llm`，內層恰含 `Provider` 與 `Model` 兩個非 null 字串，順序不限、重複／額外欄位拒絕。值先拒絕控制字元，再沿既有設定習慣 Trim，之後作精確准入。發布預設為 Gemini Flash-Lite，不把切換結果寫回設定檔。 |
| MODEL-012 | 相容原根恰含 `Gemini`、內層恰含 `Model` 的格式，解讀為 Provider=gemini；仍須符合目錄。新舊根同時出現、任一值非法或 A 階段設定 OpenAI 都是 InvalidSettings／退出 2，零工作區確認／讀 Key／HTTP。舊格式中原本僅語法合法的未登錄模型，現在明確拒絕；錯誤提示只能列安全支援清單，不回顯非法值。 |
| MODEL-013 | `--help` 保持不讀設定、Key、工作區、存檔或連網；不新增 `--model`／`--provider`／`--key`、認證環境變數或執行中設定重載。啟動工作區確認顯示 Provider、Model、固定官方目的地及資料外送說明，再隱藏輸入該供應商 Key；第一個正常請求才驗證服務端授權。 |
| MODEL-014 | 端點不屬可編輯設定。Gemini 保持 `https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent`；OpenAI 僅上述 POST，無 query／fragment／userinfo／自訂 port。測試以假 handler 攔截同一官方 URI，不引入產品測試端點。 |

新格式範例只含非機密值；A 階段的 Gemini 設定如下，B 可改為上表 OpenAI tuple。

```json
{"Llm":{"Provider":"gemini","Model":"gemini-3.5-flash-lite"}}
```

## 4. 元件責任與所有權

| 擁有者／最小內部契約 | 責任 |
|---|---|
| ModelCatalog／ModelProfile | 固定目錄、能力、官方目的地與本機准入；不呼叫服務、不持有 Key。 |
| ChatApplication／ModelSessionCoordinator | `/model`、完整切換預覽、新確認、停止准入、清除／釋放／建立新 session 及失敗收尾；仍只有一個活動 runtime。 |
| BootstrapSession／SessionSecrets／Security | 一份 profile 的認證與政策所有權；只有對應認證轉接器取得必要 Key。 |
| IChatRuntimeFactory | 依已驗證 profile 建立 GeminiClient 或 OpenAiClient，及同 client 的 inspector；不能依失敗類型挑另一供應商。 |
| GeminiClient／OpenAiClient（`Llm/`） | 各自的傳輸、不可透明化的原生紀錄、解析、錯誤與量測；不執行工具或管理 Plan。OpenAI 首版採 .NET 內建 HttpClient／System.Text.Json 直接呼叫官方 REST，不新增 NuGet 或相容代理層。 |
| AgentRunner／GenerationBudget／UsageAccumulator | 唯一生成准入、工具派送、世代核對、用途及用量記帳；供應商分組只是既有記帳的投影。 |
| Rules／Skills／MCP／Work／Checkpoint／Git／Processes | 各原規格繼續擁有來源、版本、Scope、核准、證據、保存與副作用；切換協調器不能重建其資格。 |

| 編號 | 要求 |
|---|---|
| MODEL-015 | 沿現有 RuntimeFactory／ChatApplication 延伸，避免平行建立另一套聊天或生成入口。所有新增整合型別為 internal；Google 型別及 OpenAI 原生 JSON 封裝留在 Llm/，Core 不解讀原生協定。保留 .NET 10、Google.GenAI 1.26.1、MCP 2.3.0／既有協定與測試基線。 |
| MODEL-016 | 公開 ILlmClient、LlmRequest、LlmResult、LlmMessage、LlmUsage 與工具契約保持簽章及序列化禁令；profile／用途／世代由 Runner 的本機呼叫上下文綁定。PublicSettings 明示增加唯讀 Provider 與 provider/model 建構入口，保留原單參數 Model 建構方式並預設 gemini；不在公開設定／結果放 Key、SDK 或原始 HTTP。 |
| MODEL-017 | profile、切換候選及原生封裝採密封、唯讀、防禦性複製、固定 ToString 標記與明確 writer。Key、政策、client、Permit 與原生紀錄均不可序列化。原生 owner 是 client instance 的不可偽造身分；同 provider／model 的另一 client 也不能沿用。 |
| MODEL-018 | 每次真實切換遞增本機 ModelSessionGeneration，不回捲或重用。模型結果、量測、Plan／Context／save／review／寫入／程序／checkpoint／MCP 確認候選均以該代次與既有戳記共同驗證。盡量承接既有 SessionGeneration 的統一來源，不能另設不同步的授權代次；溢位安全停止，不重用身分。 |
| MODEL-019 | 模型清單與切換只有本機 CLI 入口，零新模型工具。模型、規則、技能、MCP／網頁、存檔或模型錯誤文字中的 `/model`／yes 只是資料，不觸發解析或核准。 |

## 5. CLI 與切換狀態機

| 指令 | 固定行為 |
|---|---|
| `/model` | 顯示目前 Provider／Model／固定目的地、能力、目前代次與「Key 僅本機輸入、服務授權尚須正常請求確認」。不顯示 Key 指紋、長度、尾碼或原生身分。 |
| `/model list` | 顯示本階段固定目錄與能力，明示是產品支援清單；零網路、零生成。 |
| `/model use PROVIDER MODEL` | 驗證目錄；同 tuple 為 no-op。不同 tuple 則完整預覽及新隱藏確認，按下述交易切換。 |

| 編號 | 要求 |
|---|---|
| MODEL-020 | 先走第 14 份單行閘門，含換行整筆拒絕。整行 Trim 後精確接受上表形式；use 的 token 間只允許一個 ASCII 空格，不接受引號、Tab、額外參數、別名或大小寫變體。未知／非法指令回 invalid_model_command 或 model_not_supported，零生成／變更，不回顯原輸入。 |
| MODEL-021 | list、查詢、非法值、同 tuple 及完整切換本身均零模型／認證探測／token 計數／模型列舉 HTTP；不能在切換時用一次隱藏生成「測試新 Key」。它們也不消耗一般生成預算。 |
| MODEL-022 | 所有固定／動態欄位與完整區塊經當前敏感政策及終端機純文字處理，最大 65536 UTF-8 bytes，超限整份拒絕，不截短關鍵風險或模型資訊。確認與 Key 輸入保持唯一讀取者、進出排空、CRLF 去重、無回顯及回顯恢復。 |
| MODEL-023 | 相同 Provider＋Model 直接回 already_selected，保留 client／Key／Context／Scope／核准／檢查點／MCP，零確認及代次變更。需要換同 profile 的失效 Key 時，仍走既有認證失敗重輸路徑，不藉 no-op 重建 client。 |
| MODEL-024 | 不同 tuple 預覽必列舊／新 Provider、Model、官方目的地，及將清除的完整資料類別：對話／摘要／pins、Plan／完成證據／核准、讀取與 Git 快照／報告、檢查點及復原歷史、技能啟用、MCP 連線／Permit、未發布工作候選、草稿／輸入歷史。說明不復原已寫檔案、不刪私人存檔、檢查點丟棄後無法由本程序復原、程序消耗保留；需要保存／復原可拒絕後自行處理。完整預覽後新輸入 y／yes 才同意；空白／其他文字拒絕，不接受預輸入。 |
| MODEL-025 | 只在 AwaitingInput 且無任何在途／未確認副作用時受理。busy 期間普通輸入依原政策丟棄，不排隊執行切換；Unconfirmed、程序收尾失敗或已鎖定的 store／workspace 致命狀態不能藉切換解除。確認候選綁舊代次、原 profile 及既有來源／版本；確認期間變動即 model_revision_conflict，零切換，不把候選改綁新版。 |
| MODEL-026 | 新確認後進 SwitchingModel，先停止新生成／工具准入並遞增代次，清除所有候選與第 024 列的記憶體狀態，斷 MCP、停舊讀取者；等待自有操作收尾及舊 client DisposeAsync，再解除舊政策／服務綁定並 Dispose 舊 SessionSecrets。完成後才建立新認證輸入器與新 client；不持決策鎖等待 I/O 或人類輸入。任何安全收尾失敗退出 1，不繼續換 Key 或回退舊 runtime。 |
| MODEL-027 | 不同 tuple 一律要求新的隱藏 Key，包括同供應商換模型；不保留 Key ring、復用舊字元陣列或把舊 Key 當另一供應商輸入。新輸入依原 1～512 可列印 ASCII／不得首尾空格規則接受，不用前綴推定授權。建新政策、client／inspector／工作區服務後，重驗原已確認工作區的實體根身分並重讀根規則，根更換／不可用即失敗，不重新猜路徑。初始 Scope="."，進 Plan，零舊回合重送並等待新一般訊息。 |
| MODEL-028 | 預覽拒絕／確認前 Ctrl+C 保留安全舊狀態並回輸入；EOF 按原正常退出。確認後已清除／釋放，不承諾可取消回舊 session：新 Key 輸入空／格式錯可在同一新認證階段重輸，Ctrl+C 等收尾後退出 130、EOF 退出 0、輸入或建立／根規則失敗退出 1。無活動政策的認證空窗只顯示事先經舊政策核可的固定隱藏輸入提示／固定錯誤，不顯示動態資料或保留舊 Key 供遮蔽；新政策建立後再驗固定提示。中止時清除新 Key；不得發布半個 runtime 或保留新 Key 於舊 session。 |
| MODEL-029 | 新模型首個正常請求如 AuthenticationFailed，仍只提供當前 profile 重輸 Key 或退出；重輸清除並重驗目前政策、不換模型且不重播失敗訊息。其他模型／配額／網路錯誤不能自動進認證或切換。未做切換的啟動／clear／認證失敗路徑，除本份明示增量外沿原模式語意；只有明確 `/model use` 成功切換強制 Plan。 |

狀態順序：`AwaitingInput → AwaitingModelApproval → SwitchingModel → ReadingModelKey → LoadingRules → AwaitingInput(Plan)`。同 tuple 不進狀態機。換 Key 與切換共用清除／重建原語，但觸發、profile 與取消語意須分別驗收。

## 6. 認證、敏感資訊與釋放

| 編號 | 要求 |
|---|---|
| MODEL-030 | Key 只由第 14 份隱藏輸入取得並在活動 session 記憶體持有。Gemini Key 只進 `x-goog-api-key`；OpenAI Key 只進該 client 的 `Authorization: Bearer` 認證欄位。禁止互傳、同送兩類認證、寫入 URL／body／工具／Context／規則／存檔／日誌／子程序，亦不得從環境、檔案、參數、SDK 預設認證或模型結果補值。 |
| MODEL-031 | 兩供應商都保留當前 Key 精確比對、JSON 解碼與組合後再檢查。B 額外引入明確的保守 OpenAI 外形規則：任意位置小寫前綴 `sk` 加連字號，緊接至少 16 個 ASCII 字母／數字／底線／連字號即拒絕；不要求尾界、不解碼任意混淆。這是本產品啟發式，不是官方 Key 格式、真實性或全面秘密偵測保證，可能拒絕非憑證文字。文件與測試以分段組合建立合成外形，不落地可命中的完整假憑證。 |
| MODEL-032 | B 的外形規則在 Gemini／OpenAI 兩條路徑一致生效，涵蓋一般輸入、模型文字／原生欄位、工具參數／結果、規則／技能投影、MCP 外送／回應、寫入與 checkpoint bytes、save／resume／預覽。沿既有政策的失敗／共同重設語意，整份拒絕敏感候選，不遮蔽後繼續使用帶簽章或不透明紀錄。原 Google／私鑰／認證賦值規則不移除。 |
| MODEL-033 | B 同步擴充 WorkPersistencePolicy 與第 07 份 staged／artifacts 掃描器，以新 BUD-S006 報 OpenAI 外形；ASCII／UTF-16LE／BE、兩種位移／跨區塊及持久 JSON 解碼檢查沿原覆蓋。佔位值集合明確追加 `YOUR_OPENAI_API_KEY`，其他規則與目錄無豁免；舊存檔讀取也過新增政策，命中即拒絕且不改寫／自動刪除。這是 SAVE-062～064 與 BUD 固定規則的明示增量。 |
| MODEL-034 | 系統／應用指示仍只在既有用途限定的模型請求欄位內使用；不能顯示、附入摘要／工具參數／MCP／檔案／存檔／診斷或以錯誤附件洩漏。新增供應商不得增加擷取指示的公開出口；測試使用合成標記。所有供應商原始 body／標頭／例外／協定／reasoning 內容不得輸出或寫日誌。 |
| MODEL-035 | 先停新請求、取消並等待本 client 在途請求，再釋放 HTTP／SDK 與原生紀錄參考，最後釋放 SessionSecrets。DisposeAsync 重入安全；已釋放 client 回 ClientUnavailable，零網路。認證必要 string 複本只留在對應認證物件，不承諾受控記憶體／SDK 的每份複本立即覆寫。 |
| MODEL-036 | 新 Key 產生新政策；完整重驗新 profile 的固定文字／工具 Schema、目前根規則及所有新投影，短 Key 與固定欄名碰撞也不豁免。不可安全建立即拒絕／安全退出，不借舊政策顯示、不跳過敏感欄位。舊 session 的 Context 與候選已清除，不跨政策移交；私人存檔只有日後明確續接才重驗。 |

## 7. 原生協定、工具與傳輸

### 7.1 Gemini 的限定增量

| 編號 | 要求 |
|---|---|
| MODEL-037 | 兩個 Gemini profile 都沿 Google.GenAI 1.26.1／GenerateContentAsync、CandidateCount=1、MaxOutputTokens=8192、AutomaticContinuation=false、Retry Attempts=1，以及原 GeminiProtocol／Transport／Results。完整 Content／parts／CallId／thought signatures 與原序保持，不以顯示文字或 Core 投影重建；同供應商換模型也不能移交原 owner。新增模型若固定 SDK 不相容，受控停止並修訂，不切 Interactions、Vertex 或 OpenAI 相容模式。 |

Google 官方要求原樣傳回必要 thought signatures；本規格選擇清除切換歷史，並未授權使用假簽章跳過驗證。[Gemini thought signatures](https://ai.google.dev/gemini-api/docs/generate-content/thought-signatures)。

### 7.2 OpenAI 請求與原生歷史

| 編號 | 要求 |
|---|---|
| MODEL-038 | B 使用官方 Responses REST，固定 body 的 model 取活動 profile、instructions 取本次 LlmRequest、input 取下列原生轉換、max_output_tokens=8192、stream=false、store=false、background=false、reasoning.effort="none"、include=["reasoning.encrypted_content"]。工具存在時 tool_choice="auto"、parallel_tool_calls=false；無工具時 tools=[]、tool_choice="none"。不送 previous_response_id、conversation、metadata、檔案／圖片／音訊、供應商 hosted tool、遠端 MCP、server-side compaction 或背景工作參數。 |
| MODEL-039 | 每個 LlmToolDefinition 映射為頂層 type="function"、name、description、parameters 及明確 strict=false；保留現有 JSON Schema 的 optional／null 語意，不轉成全 required 或改工具輸入格式。Core 仍執行完整本機參數驗證；strict=false 不構成繞過。名稱須符合官方 function 名稱限制且完整可表示，遇不支援的目錄／Schema 在送出前 InvalidRequest，不能省略工具或換 Schema 重送。 |
| MODEL-040 | 一般 User 以 role=user／input_text 表示；Model 只能來自同一 OpenAiClient owner 的不可變原生 output items，依原序回填。每一組工具先放完整原 model output，再依 LlmToolResult 原序加入 type=function_call_output、原 call_id 與完整安全 Response JSON 字串。每次請求重送本次 instructions 與 Core 選定的完整歷史／當前鏈；不額外維持一份 SDK／服務端對話。 |
| MODEL-041 | PartIndex 對應原 output 陣列的實際索引，不壓縮成呼叫序號；CallId 必須是原 function_call 的非空 call_id，不拿 item.id 替代。以 PartIndex／CallId／Name 精確配對；同名不同呼叫可共存，缺漏／重複 ID、缺結果、錯序、跨 owner、投影不一致或孤立結果在送出前 InvalidRequest。禁止合成供應商 ID、按名字合併或從可見文字解析工具。 |
| MODEL-042 | 完整解析與敏感檢查整份回應後才返回工具候選。只接受可支援的 message、function_call、reasoning output item；function_call.arguments 必須是單一完整 JSON object，採嚴格解析、拒絕重複欄位，clone 後交既有 Runner。文字只取 message 中 output_text 原序串接後檢查；refusal 獨立辨識，reasoning／簽章／ID／annotations 不顯示。不執行 hosted／未知 item、不得將正文中類似工具的文字當呼叫。 |
| MODEL-043 | response.status=completed 且有合法呼叫時為 ToolCalls，沒有呼叫且有非空安全文字為 Completed，兩者均無為 Empty。明確 refusal 為 Refused；refusal 混工具或不完整／非法呼叫為 InvalidResponse，整批零派送。incomplete 且 max_output_tokens 為 Truncated、content_filter 為 Refused；兩者都不返回可執行呼叫或本次 ModelMessage。其他 failed／未知狀態按固定受控錯誤，不發布部分成功。 |
| MODEL-044 | 同 client 中原樣保留可重放的原生 item，包括合法 reasoning 的 encrypted_content／必要 metadata；不解密、不顯示、不摘入摘要或持久保存。需續傳的 reasoning item 缺少所需加密資料、未知 item 或結構不完整時 InvalidResponse／RequiresContextReset，不改造成 user 文字續傳。所有保留字串同樣經當前敏感檢查；不透明不代表跳過安全檢查。 |

Responses 使用 call_id 對應 function_call_output；明確 strict=false 可維持既有 optional Schema 語意，工具執行與驗證仍由本產品負責。[OpenAI function calling](https://developers.openai.com/api/docs/guides/function-calling)。本產品採手動歷史與 store=false，並保留必要的原生 items；這是本機狀態策略，不宣稱供應商完全零留存。[Conversation state](https://developers.openai.com/api/docs/guides/conversation-state)、[Responses 遷移與狀態選擇](https://developers.openai.com/api/docs/guides/migrate-to-responses)、[資料控制](https://developers.openai.com/api/docs/guides/your-data)。

### 7.3 送出與回應安全閘門

| 編號 | 要求 |
|---|---|
| MODEL-045 | 每次 GenerateAsync 一個 120 秒整體期限，連結外部取消與 client lifetime；涵蓋序列化、傳輸、完整回應讀取及解析。實際最終 UTF-8 body 上限沿 262144 bytes，包含所有 instructions、工具、設定與不透明資料；262145 即 InputTooLarge／RequestDispatched=false。不裁切、偷換模型或額外摘要來重送。 |
| MODEL-046 | OpenAI handler 關閉 redirect、cookies、系統／環境 proxy 與重試；核對最終方法、完整固定 HTTPS URI 及僅正確供應商認證。最內層送出前再驗代次／取消／原生 owner／完整解碼字串與實際 body。一次 GenerateAsync 最多一次 POST，不做 token API、認證 ping、模型發現、polling、遙測或隱藏 retry；3xx 不跟隨，不向 Location 傳認證。 |
| MODEL-047 | OpenAI 成功回應以有界記憶體讀取最多 1048576 bytes、JSON 深度最多 64；超界／無效 Unicode／重複結構欄位／非法 JSON 整份 InvalidResponse，不派送前綴工具。錯誤 body 最多 65536 bytes，只取白名單分類與可安全解析的純數值用量；超界不顯示原文，仍可依 HTTP status 分類。Gemini 現有回應處理不藉本份另改上限。 |
| MODEL-048 | OpenAI HTTP 401→AuthenticationFailed、403→PermissionDenied、404→ModelUnavailable、408→Timeout、429→QuotaOrRateLimited、409／5xx→ServiceUnavailable；400 中白名單 context_length_exceeded→InputTooLarge，其他→InvalidRequest；3xx／其他非預期狀態→InvalidResponse。網路／期限／外部取消依既有分類，禁止依任意錯誤訊息猜 Key 或模型。訊息只用固定安全文字，不顯示遠端 error.message／headers／request id。 |
| MODEL-049 | RequestDispatched 僅表示交給最內層傳輸開始嘗試，與服務收到／計費分開；LlmResult 僅 Completed／ToolCalls 可攜本次 ModelMessage。SensitiveOutput、InvalidResponse、UnexpectedToolCall／原生協定故障依原規則 RequiresContextReset=true，清空呼叫／原生候選，先結算可得用量及已發生本機事實，再共同清除。取消或代次過時不得採用遲到成功。 |

## 8. Context、共用預算與供應商用量

| 編號 | 要求 |
|---|---|
| MODEL-050 | Core 仍擁有摘要、pins、完整成功回合與當前鏈選取；轉接器不自行挑歷史、補 prompt 或代做工具。第 11 份最多五個原始完整回合、保留最新兩個、摘要門檻及必要附錄不變；工具呼叫與全部結果不可拆開壓縮。切換清除所有摘要／pins／原始回合，不自動生成「交接摘要」。 |
| MODEL-051 | 每個 client 必須實作 ILlmContextInspector、ILlmRequestCapacityInspector 與既有數值 telemetry。本機量測使用相同原生轉換／序列化與敏感檢查，零 HTTP／生成；SDK bytes 與本機估算分列、不視為 token 容量。新 session 量測初始不可得，舊代次資料標過時且不得作准入依據，無 inspector 時拒絕需要它的流程。 |
| MODEL-052 | OpenAI 的 ProjectCompletedTurn 只輸出第 11／15／18／19 份既定白名單：完整 user text、最終可見 assistant text、允許的工具狀態 metadata；不輸出 reasoning、簽章、CallId、arguments、raw file／diff／stdout／MCP 正文或新的來源物件。一般回答自然語言可能轉述內容，保留原有損及不可信標記。壓縮／工作摘要仍沿同一模型、Runner、無工具生成與原 JSON 驗證，不因模型差異改摘要格式。 |
| MODEL-053 | 三個 profile 共用同一程序 GenerationBudget：預設回合5／程序100，可調1～20／1～1000；開始方法即扣一次、未傳輸也不退款。切換、換 Key、clear、/budget reset 不重置程序消耗／設定；不另建供應商預算。摘要、一般、Review、work-save 都經原共用入口；程序2次／300秒與 MCP 每回合2次機會不因選模增加。 |
| MODEL-054 | 每次開始生成捕獲不可變 profile／代次／用途，結果只在既有唯一記帳點記一次；用量歸屬請求開始時 profile，不能按收尾時選模推定。clear／切換保留程序總計及各 profile 小計；同 profile 重訪累加同組，不以每個 client 建立無界統計。開始但無 LlmResult 只保留生成次數，不捏造傳輸或 token 缺值。 |
| MODEL-055 | OpenAI input_tokens→PromptTokens、output_tokens→OutputTokens、output_tokens_details.reasoning_tokens→ThoughtTokens、input_tokens_details.cached_tokens→CachedTokens、total_tokens→TotalTokens。零值是已知零；缺漏、負值、格式錯或超出 long 為該欄不可得，沿原缺值／溢位記帳。Thought／Cached 為子集合，不再加到總數，Total 不補算；HTTP 失敗也不靠輸入字數推估費用。Gemini 原對應保持。 |
| MODEL-056 | 用量顯示新增 Provider／Model／用途歸屬；程序共同顯示生成／傳輸次數，各 profile 顯示五欄已知值、缺值與溢位。跨模型 token 不能當同價或同能力單位，不以合計 token 推算金額；既有內部五欄機械累加可保留，但 UI 不把跨供應商總數當可比較帳單。目錄最多三組，所有數字／固定標籤仍經當前政策；累計不含 Key 或原生請求身分。 |
| MODEL-057 | 額度、429、模型不可用或服務失敗皆停止當次流程，依原語意保存已發生操作、拒絕未開始工具；不換另一 profile 試一次、不回退舊模型、不增加結尾生成。使用者日後可明確選模並給新指示，仍須重新取得來源與各次核准，不自動執行舊計畫。 |

## 9. 跨功能狀態與生命週期

| 編號 | 要求 |
|---|---|
| MODEL-058 | Rules 的來源／Scope／Valid 由第 12 份擁有。切換先撤舊投影與所有確認，按新政策重新安全讀根規則，不保留舊 Valid；先前 Scope 只在切換預覽作清除提示，成功後為根 Scope，使用者須以 /rules scope 明確再選。新規則敏感／不可讀時不以空規則降級。 |
| MODEL-059 | 切換清除活動 Plan、任務狀態、證據、Review 來源／報告及未發布結果，不把 model assessment 改成 local operation；成功進 Plan 等新指示。寫入／build／Git 事實若已發生須先按原流程回報；清除不復原檔案、index、產物或其他副作用，不宣稱舊完成仍可證明目前狀態。 |
| MODEL-060 | checkpoint／RecoveryRevision／EvidenceEpoch 由第 16 份擁有；本次切換採換 Key 的丟資料語意，丟棄全部程序內 bytes、anchor、候選與復原歷史，不把它們移交新政策。代次仍保持不可重用；已開始發布的操作先安全收尾，不能以切換中斷查核或復原檔案。 |
| MODEL-061 | Skills 在切換後為 Disabled；不從歷史、設定或目錄恢復啟用。後續重新 list／show／use 須讀目前 Scope、目前規則／政策並依原新啟用流程；新模型看過技能內容不代表取得工具或核准。 |
| MODEL-062 | MCP 切換時斷線並丟目錄、Permit 與候選，新 session 為 Disconnected；不自動 connect／reload。重新連線後完整目錄、三項 Schema、URL 准入、外送預覽與新確認依第 18／19 份；所有 Permit 綁新代次／client 一次使用，任何供應商 Key 不能進 MCP。Review 保持三項 Git 工具、零 MCP 網路。 |
| MODEL-063 | SAVE v1 不增 provider／model／Key／原生歷史／預算或「切換摘要」欄位；存檔不是模型選擇來源。`--resume ID` 先按目前啟動設定選 profile、隱藏輸入其新 Key、重讀根規則，再按原預覽／確認與原子匯入重建 Draft／Pending。可在另一供應商續接安全的 provider-independent 背景，但必須明示預覽將使用的 Provider／Model／目的地，沒有原生歷史或舊核准移交。 |
| MODEL-064 | 本版沒有執行中載入存檔或 `/model use --keep-context`。既存私人存檔不因切換刪除；新政策不合格的存檔整份拒絕，保留原檔供使用者處理，不改版本、刪欄或繞過新增敏感檢查後載入。 |
| MODEL-065 | `/clear` 沿既有清除但保留目前 profile／認證／模式與程序累計；退出清除活動認證及所有程序內來源，釋放全部自有資源，不批次清理檔案。草稿與歷史按第 14 份清除，任何外部文字／子 Agent 都不能直接讀 Key、鍵盤或提供核准。 |

### 9.1 固定停止與錯誤

| 條件／固定碼 | 行為 |
|---|---|
| invalid_model_command／model_not_supported | 零狀態變更／確認／HTTP，保留舊 session。 |
| already_selected | 同 tuple no-op，不讀 Key、不清除。 |
| model_revision_conflict | 丟棄確認候選，保留安全當前狀態；無隱式重試。 |
| model_capability_unavailable | profile／工具／inspector 缺必要能力，本機停止，零當次傳輸；不是自動切純聊天。 |
| 切換確認拒絕／確認前取消 | 安全舊 session 保留；不執行清除。 |
| 確認後輸入／建立／根規則故障 | 依 MODEL-028 退出，清新舊認證參考，無回退或半發布。 |
| 敏感／原生協定故障 | 沿 MODEL-049 共同清除及當前政策固定錯誤，已知用量／本機事實先收尾。 |
| 取消與較嚴重本機收尾故障競爭 | 故障優先退出 1，不能以正常取消、切換成功或認證選單覆蓋。 |

| 編號 | 要求 |
|---|---|
| MODEL-066 | 上表新增碼只屬本機模型選擇／能力結果，不新增模型工具 envelope 或更改公開 LlmErrorCode；adapter 失敗映射既有 enum。能力不足於 Runner／runtime 准入前受控拒絕，直接呼叫 adapter 也不能繞過 owner、端點、認證、body 與取消閘門。 |
| MODEL-067 | 實際清除、認證重建與後續 Ready 是不同決策點；新 client／inspector／工作區／根規則全部可用後才一次發布 Ready。任何遲到候選不得復活舊 session；清除或釋放失敗有明確 fatal 狀態與退出碼，不只靠顯示「已切換」宣稱原子性。 |

## 10. 受影響契約與後續開發同步

本節限定未來實作時需要調整的擁有者；本次不修改這些文件或程式。未列的工具輸入／成功格式、檔案／程序權限、Plan／SAVE v1、MCP 協定與來源分類保持。

| 原契約／擁有者 | 本份明示增量與遷移責任 |
|---|---|
| SPEC 的單 Gemini 範圍；01 的固定設定；GEM-002、005 | 兩階段固定 profile、Llm 設定／舊 Gemini 設定相容、目錄外拒絕、啟動／切換呈現。更新 help、啟動與 InvalidSettings 測試；不增認證來源。 |
| GEM-003～004、020、025～027；Core／Llm | Gemini 原路線保持，B 加獨立 OpenAI 原生 REST／Bearer 認證及 owner；PUBLIC LLM 不變，PublicSettings 明示小幅延伸。 |
| CLI 的認證重輸／clear；TERM-044、047、056、065 | 新三個 /model 形式、完整預覽、確認／切換／新 Key 狀態、確認前後不同取消語意；原指令保持。 |
| CTX-003～004、007、050～064；SAVE-042 | 原排除額外 client 的階段界線由本份限定擴充；同時仍一個 client，兩種 inspector、單一預算、有限分組用量與維護歸屬。 |
| SAVE-062～064；BUD-S001～005 | B 額外的外形／佔位值、BUD-S006 掃描與跨 JSON／bytes 驗收；舊存檔不改版但重新驗證可能拒絕，不自動遷移或放行舊不合格資料。 |
| RULE／GIT／CKPT／SKILL／MCP／WEB 的戳記與生命週期 | 統一接入模型工作階段代次；換模型的狀態丟棄與重新明確選用。原契約仍為來源／證據／核准單一擁有者。 |
| README／ROADMAP／SPEC／01～19 適用接續與測試 | 實作 A、B 各自同步能力、設定範例、尚未實作階段、分層證據與八筆交接完成情形，不把兩階段一起標完成。 |

| 編號 | 要求 |
|---|---|
| MODEL-068 | 後續實作逐列更新上表受影響契約、正式 Bootstrap／Chat／Runner／Inspector 與 TerminalHost 測試路徑；不只新增直接 adapter 測試。保留公共 LLM 與既有工具回歸，對原設定任意模型准入、單模型用量顯示等變更明示測試期待增量。 |
| MODEL-069 | A 只交付兩 Gemini profile、共同生命週期／設定／歸屬，不註冊 OpenAI client 空殼、假裝可用或提前標 B 完成；B 才加入真實 OpenAI adapter、第三 profile 與新增敏感掃描政策。兩階段均保持可建置，新增狀態型別與工廠重構不要求順帶實作 FUT-14。 |
| MODEL-070 | 開發前重新核對官方模型／端點與本地固定版本；模型名稱是明確准入值，別名不保證永遠不變，帳戶可用性／回應相容性須另測。任何必要版本／API／能力改變先修訂文件及驗收，不由實作者暗中擴張。本次只查閱公開文件，未向 Gemini、OpenAI 或 Learn 送出產品請求。 |

<a id="upstream-handoffs"></a>

## 11. FUT 耦合矩陣與固定交接 memo

### 11.1 八筆上游必讀交接逐項承接

| 原交接／契約擁有者 | 本份承接／邊界決定 | 後續驗收 |
|---|---|---|
| RULE-HO-011／第 12 份 | MODEL-016～018、026～027、036、058：Core 安全原文由原來源持有；新政策重讀根規則、重新投影，不移交別 client 協定或認證。 | MODEL-AC-008～012、017、027 |
| GIT-HO-011／第 13 份 | MODEL-009、037～044、057、059、062：各 profile 須能表達三 Git 工具／報告，來源及報告驗證仍 Core；切換丟舊報告，不失敗換模重送來源。 | MODEL-AC-013～018、028、032 |
| TERM-HO-010／第 14 份 | MODEL-020～029、035～036、065：新政策先清除、唯一讀取者、完整預覽與新隱藏 Key；舊輸入歷史不是認證來源。 | MODEL-AC-005～012、034～035 |
| SAVE-HO-011／第 15 份 | MODEL-031～033、052、063～064：新增憑證特徵與舊存檔重驗，SAVE v1 保持；跨供應商只可由重啟續接明確匯入安全背景。 | MODEL-AC-018～020、023、030 |
| CKPT-HO-012／第 16 份 | MODEL-024～028、060、067：明示切換等同新政策資料丟棄，先提示檢查點喪失，不移交 bytes、核准或資格，不復原已寫檔案。 | MODEL-AC-007～012、029、033 |
| SKILL-HO-012／第 17 份 | MODEL-027、036、061：切換 Disabled，重新讀目前 Scope 與政策後才可明確啟用，無自動復活／升權或失敗後重送。 | MODEL-AC-008、011、019、027、031 |
| MCP-HO-012／第 18 份 | MODEL-009、018、030、039～041、053、062：重驗工具能力，斷線／舊 Permit 失效、不得跨 client，供應商認證與 MCP 分開。 | MODEL-AC-009、013～017、019、026、032 |
| WEB-HO-012／第 19 份 | MODEL-018、030～032、052、062：同三工具能力、原 URL／來源／逐筆確認；切換不重送 fetch、不把文件當本機事實。 | MODEL-AC-016～019、026、030、032 |

第 09／11 份沒有專屬 FUT-13 HO 編號；第 2 節列出的 Plan／Context 原契約仍須直接承接，不能因上表無列而跳過。FUT-13 的主要直接耦合是共同預算、認證與各候選的失效／清除；沒有新增共用持久資料格式，也沒有需要先另寫第二份 memo 才能開發的事項。

<a id="future-handoffs"></a>

### 11.2 對其他十三個 FUT 的固定交接

| 交接編號／對象 | 關係與本份已定邊界 | 後續責任／回歸 |
|---|---|---|
| MODEL-HO-001／FUT-01 | 無新搜尋格式依賴；選模／摘要不建 read_file 快照（MODEL-050、059）。 | 未來索引不跨模型沿用修改基準，不外送私人存檔；AC-028、030。 |
| MODEL-HO-002／FUT-02 | 共用同一 Runner，模型增加不改程序核准／2次300秒／test 不支援（MODEL-005、053、057）。 | 修正循環另訂，失敗後不換模重跑或解除原生鎖定；AC-026、028、033。 |
| MODEL-HO-003／FUT-03 | 計畫與核准原擁有者不變；切換清除並進 Plan（MODEL-027、059）。 | 新跨模型計畫若保留須另訂遷移及新確認，不能把歷史完成復活；AC-008、028。 |
| MODEL-HO-004／FUT-04 | 直接共用 inspector／GenerationBudget／用途／唯一記帳（MODEL-050～056）。 | 新容量／摘要／金額策略須逐模型定義可比性、來源與缺值，不能建立第二預算；AC-021～026。 |
| MODEL-HO-005／FUT-05 | 目前新政策重讀根、Scope 回根（MODEL-036、058）。 | 如未來保留 Scope／多 Scope，須另訂新政策讀取及新使用者選擇；AC-011、017、027。 |
| MODEL-HO-006／FUT-06 | Git raw／finding／本機位置仍由第 13 份驗證，Review 零網路（MODEL-059、062）。 | 新審查模型／報告格式逐 profile 補驗，不自動用另一供應商重試；AC-016、028、032。 |
| MODEL-HO-007／FUT-07 | 共用完整預覽、新單行確認、唯一讀取者與清除（MODEL-022、024～029、065）。 | UI 改版保留確認前後取消區別，不把外部 yes 當鍵盤；AC-005～012、034～035。 |
| MODEL-HO-008／FUT-08 | SAVE v1 與歷史背景不綁原生模型，續接目的地明示（MODEL-033、063～064）。 | 若日後保存 profile hint／切換摘要須新版本與同意，不能由存檔選供應商或恢復認證；AC-020、023、030。 |
| MODEL-HO-009／FUT-09 | 換 profile 丟 checkpoint，新舊已寫檔案仍在（MODEL-024、060）。 | 若未來保留檢查點，需另訂新政策 bytes 重驗、來源／證據代次及核准，不可直接解除本版丟棄規則；AC-007、029、033。 |
| MODEL-HO-010／FUT-10 | 技能不能選模型，切換 Disabled（MODEL-019、061）。 | 新技能 routing／profile 建議只能是文字，啟用、Scope 與切換仍須本機使用者動作；AC-019、027、031。 |
| MODEL-HO-011／FUT-11 | MCP 與供應商認證分離、Permit 不跨 client（MODEL-030、053、062）。 | 新 MCP／Schema 擴充須兩供應商同能力檢查，外部認證另訂，不借模型 Bearer；AC-013、019、026、032。 |
| MODEL-HO-012／FUT-12 | 無必要順序依賴；引用／來源不是供應商可信核准（MODEL-052、062）。 | 新網站／外送資料須保持 per-send 新確認、來源與共同 MCP 額度，不能用 hosted web 旁路；AC-018、026、032。 |
| MODEL-HO-013／FUT-14 | 無多 Agent 能力，FUT-14 不強制依賴本份；仍單一活動模型／在途（MODEL-004～006、015、053～054）。 | 多 Agent 另訂每個子 client 的 profile／owner、Scope 子集、共用總預算、並行上限、用途／用量歸屬、父子取消及彙整；不得複製父 Key／原生歷史給子任務、給子 Agent 讀鍵或核准。需要額外認證轉接器必須新用途限定設計；AC-019、025～026、038。 |

| 編號 | 交接要求 |
|---|---|
| MODEL-071 | 上表八筆上游逐項可追溯到原擁有者、本份需求與驗收；更改任何原邊界，列出明示增量及遷移，不以「沿用」概括原生歷史／認證／候選清除。 |
| MODEL-072 | 本節是 FUT-13 耦合與交接 memo 的單一來源，不另建內容重複的筆記。後續 FUT 開發前讀適用 MODEL-HO 列、原規格與當時程式；尤其 FUT-14 先讀 MODEL-HO-013 及第 09／11 份，不把本份當委派能力或授權。 |
| MODEL-073 | 未來如要「同供應商保留 Key」「跨模型保留 Context／pins／Plan／checkpoint」「自動路由／備援」「多個活動 client」，都須明示修訂本份清除、認證、owner、預算及八份來源契約，補相應回歸；不能以內部重構之名靜默放寬。 |

## 12. 後續產品驗收

| 編號 | 要求 |
|---|---|
| MODEL-074 | 離線以真實 Google SDK＋假 HTTP、實際 OpenAI REST adapter＋假 HTTP／TimeProvider、假終端機與隔離工作區驗正式 Bootstrap／Chat／Runner；全用合成憑證／指示，不連真實 Gemini、OpenAI 或 Learn。驗實際目的地、完整 body／認證、次數、狀態、取消與副作用，不只驗字串提示。 |
| MODEL-075 | A、B 分別執行與記錄離線、macOS PTY、真人終端機、真實服務及既有 Release／原生驗收。相同框架測三個 profile 的一般聊天／工具／Context／Plan／Review／save 行為；B 必須兩供應商交叉測切換與無隱藏第二傳輸，不能以 Gemini 通過代替 OpenAI。 |

### 12.1 離線與跨功能案例

| 驗收編號 | 對應需求 | 必須觀察的結果 |
|---|---|---|
| MODEL-AC-001 | MODEL-002～010、066、069～070 | A 只有兩 Gemini、B 恰三 profile；未知／僅語法合法／非文字／能力缺項不被接受，固定碼不改公開LLM／工具enum，直接adapter入口不可旁路；無模型發現或降級聊天，清單不是帳戶可用承諾。 |
| MODEL-AC-002 | MODEL-010～014、016 | 新設定／舊 Gemini 相容、兩根並存、欄位順序、重複／額外欄、null、控制字元、Trim、大小寫、URL／路徑／未知模型；InvalidSettings 退出2且零確認／讀Key／網路；--help 不讀設定。 |
| MODEL-AC-003 | MODEL-013～016、021、030、036 | 兩供應商啟動前完整目的地提示、隱藏新Key，無環境／檔案／SDK預設來源；首個正常請求才傳輸，RuntimeFactory／inspector與 profile 一致。 |
| MODEL-AC-004 | MODEL-019～023 | 三個 CLI 形式、單行／ASCII token／多行／Unicode／未知輸入，非法值不回顯／不送模型；list／查詢零生成；同 tuple 保留所有狀態及代次。 |
| MODEL-AC-005 | MODEL-020、022、024～025 | 預覽列清 Provider／Model／目的地及全部喪失資料、檢查點與程序消耗風險；65536／65537 bytes 全或零；新yes、預輸入／殘LF／外部yes不能授權，唯一讀取者。 |
| MODEL-AC-006 | MODEL-024～025、028、067 | 拒絕／空白／確認前取消保留完整舊狀態；EOF 正常退出；確認期間代次／規則／來源改變拒絕，不採更新後候選；busy輸入不排隊切換。 |
| MODEL-AC-007 | MODEL-024～028、035、060 | 同供應商與跨供應商都在新確認後停准入、增代次、丟指定資料、斷MCP、先釋放舊client再清舊Key；不雙活動、不回退；已寫檔案／私人存檔仍原樣。 |
| MODEL-AC-008 | MODEL-018、026～029、058～061、067 | 三種 tuple 切換方向均輸入新Key、重建政策／inspector／服務、根Scope、Skills Disabled／MCP Disconnected、空Plan／Context／checkpoint，成功Plan等新訊息；零舊訊息重送。 |
| MODEL-AC-009 | MODEL-018、025～026、035、062、067 | 模型／量測／所有確認候選與舊Permit的遲到結果在新代次零發布／零操作；代次溢位停止，直接入口／另一相同模型client不能復用。 |
| MODEL-AC-010 | MODEL-026～028、035～036、067 | 新Key空／格式錯重輸、確認後Ctrl+C=130／EOF=0、建client／inspector／實體根重驗／根規則失敗=1；釋放故障=1且不再讀新Key，認證空窗僅已核可固定提示，無半個Ready，原始例外不外洩。 |
| MODEL-AC-011 | MODEL-027、029、036、058、065 | 新Key命中固定欄名／來源時整份拒絕；根規則重讀／新政策，舊Scope不復活。認證失敗只重輸同profile且不重送；clear保留目前profile／模式／認證。 |
| MODEL-AC-012 | MODEL-025、028～029、035、057、067 | 在途／致命程序／storage狀態不可切換解鎖；取消與清理故障競爭故障優先，Dispose重入／在途收尾／釋放後呼叫安全，零認證／原生參考洩漏。 |
| MODEL-AC-013 | MODEL-009、037～041、045～046 | Gemini官方URI／v1beta／header／SDK設定保持；OpenAI固定POST／Bearer／body與三模式工具全集正確，strict=false不改Schema；無hosted tools／conversation／previous_response_id／proxy／redirect／重試。 |
| MODEL-AC-014 | MODEL-017、037、040～044 | 同owner完整原序／parts／簽章；OpenAI output索引、call_id與item.id不同、多個同名呼叫、完整結果配對；跨provider／同模型不同client／缺漏／重複／錯序零傳輸，clone離開JsonDocument仍有效。 |
| MODEL-AC-015 | MODEL-042～049 | completed文字／工具／Empty、refusal／混工具、incomplete兩原因、未知／hosted item、非法JSON／重複欄／arguments非object／缺必要reasoning加密資料，全批解析與敏感預檢後才派送。 |
| MODEL-AC-016 | MODEL-038～049、059、062 | 多筆工具及兩供應商／三profile的Plan／Execute／Review集合：斷線登錄13、Ready16，Plan9／12、Execute12／15、Review3；不改Schema／成功data，不以模型能力增加網路或完成資格。 |
| MODEL-AC-017 | MODEL-018、036、045～051、058 | 實際body262144可送／262145拒絕；instructions／工具／原生欄／所有附錄均計入；120秒取消及遲到結果／代次競爭，inspect零網路，舊量測不可作新准入。 |
| MODEL-AC-018 | MODEL-042～044、047、050～052、063 | OpenAI成功回應1048576／1048577 bytes、深度64／65、錯誤65536／65537、非法Unicode；raw／reasoning不顯示／摘要／保存，完整工具鏈不裁切或重建。 |
| MODEL-AC-019 | MODEL-019、030～036、046、061～062、065 | 當前Key混入任一字串／JSON欄名值／分段重組、認證目的地互換、MCP／外部假命令／yes、規則／技能／save偽造授權，阻擋且零敏感輸出／工具；只用合成指示驗無洩漏。 |
| MODEL-AC-020 | MODEL-031～033、063～064 | 新外形15／16字元門檻、長度／底線／連字號／前後夾字、非ASCII／大小寫、JSON跳脫、ASCII／UTF16雙端／兩位移／跨塊；BUD-S006與舊規則同效，新增placeholder精確接受，舊存檔拒絕不改寫。 |
| MODEL-AC-021 | MODEL-037、044、050～052 | 三profile五回合／最舊三整理留二、同owner合法鏈、本機量測及完整來源白名單、無工具摘要／嚴格JSON／原文pins；錯誤不靠改模型、裁鏈或多生成修復。 |
| MODEL-AC-022 | MODEL-045、049、053～054、057 | 程序99→100、回合最後生成、摘要／Review／save共享開始閘門；切換／重輸／clear／reset不退款；最後工具整批零執行、無結尾補請求，既有停止原因優先。 |
| MODEL-AC-023 | MODEL-044、050～056、063 | 無新歷史的工作保存不補生成，有來源的save／compact只用目前profile一次既有生成；維護／一般／Review歸屬與LastOutcome保持，匯入單一有效摘要不重複或含原生欄。 |
| MODEL-AC-024 | MODEL-048～049、054～056 | OpenAI五欄映射、已知零／缺值／負值／非整數／long溢位、reasoning／cached子集合與total不補算，false dispatch不記缺值；取消／失敗／截斷照記，只記一次，跨模型不用費用合計。 |
| MODEL-AC-025 | MODEL-018、053～056、065、069 | 反覆三profile切換與重訪仍最多三組統計，程序已耗／上限保留、用途與開始時profile一致；舊結果晚到不歸新模型，無結果不捏造傳輸。A不假設B統計或client已存在。 |
| MODEL-AC-026 | MODEL-005、046、048、053、057、062 | HTTP錯誤各類、網路／逾時／429、服務／模型不可用都最多一次POST，不探測／切model／provider／protocol重送；build2次300秒與MCP2次上限獨立保持。 |
| MODEL-AC-027 | MODEL-018、036、058、061 | 規則變動／新Scope與技能版本維持原失效；新模型不沿舊Valid或自動啟用，重新明確選Scope及技能後才投影，原檔來源與權限不重定義。 |
| MODEL-AC-028 | MODEL-005、050、057、059、068 | 工具A已寫且核對、B失敗／模型失敗／取消，先回報事實；切換不復原A、不把舊Git／摘要成快照／TestsPassed；新模型仍須完整read_file與新核准，test保持不支援。 |
| MODEL-AC-029 | MODEL-024～028、059～060 | 切換前完整提示且拒絕可自行復原／保存；確認後丟checkpoint／anchor／復原候選，不能在新client恢復資格；檔案與Git index不因丟資料改動。 |
| MODEL-AC-030 | MODEL-033、052、063～064 | 兩供應商交叉的SAVE v1重啟續接，當前設定選模、完整目的地／背景／新確認、Draft／Pending／無事實；存檔不選模型，不增欄／執行中load或刪除舊不合格存檔。 |
| MODEL-AC-031 | MODEL-019、036、061、073 | 規則／SKILL指名供應商、模型回覆命令／自動fallback建議都只為資料；Skill重新明確啟用不批准選模／Key／工具，無多client或保留Context後門。 |
| MODEL-AC-032 | MODEL-009、018、030、039～041、052、062 | 新模型重新MCP連線後三工具／fetch URL與來源格式、外送完整新確認／單次Permit／舊Permit拒絕、compaction白名單，Review零MCP；無OpenAI hosted web／remote MCP旁路。 |
| MODEL-AC-033 | MODEL-005、025～026、035、060、065、067 | build／寫入／存檔發布已開始須收尾，不以切換中斷安全查核；不可確認／close／cleanup故障退出1，零批次刪除／副作用rollback或新session續跑。 |

### 12.2 分層實測與交付標準

| 驗收編號 | 對應需求 | 範圍 |
|---|---|---|
| MODEL-AC-034 | MODEL-020～029、035、065、074～075 | macOS 離線 PTY：三CLI形式、中文／完整預覽、拒絕／新yes／預輸入、確認前後Ctrl+C／D／EOF、busy丟棄、錯誤Key重輸、雙供應商切換與認證故障，唯一讀取者及ECHO／ICANON／ISIG恢復；零真API。 |
| MODEL-AC-035 | MODEL-022～029、056、075 | 真人 macOS：中文IME／貼上、供應商及目的地可辨識、清除與檢查點風險可理解、新Key無回顯、Plan等新指示、用途與分組用量可讀，取消／退出無終端機殘留。PTY不代替真人。 |
| MODEL-AC-036 | MODEL-003、007～009、037～057、063、070、075 | 另次明確授權後，由使用者自行隱藏輸入真Key，逐profile驗一般文字、同owner工具往返／摘要／Review／save、Gemini選模及雙供應商切換不重播。Learn另需服務授權／新外送確認；只記安全分類、模型、日期、數值與通過／失敗／未觸發，不保存Key／完整指示／raw SDK或HTTP。 |
| MODEL-AC-037 | MODEL-005、015～016、033、068～070、074～075 | A、B各跑正式restore→build→MTP測試、help、format、Python掃描回歸、適用PTY及git diff --check；固定套件／C#格式、公開LLM、既有工具與SAVE v1保持。Release仍獨立掃描，BUD-S003受阻／原生／MTP／未驗平台及人工分發不得標已過。 |
| MODEL-AC-038 | MODEL-001～006、068～073、076～080 | 本次文件查核：需求／案例／八筆上游／十三筆前向交接可追溯、JSON／Markdown／連結／錨點／空白通過；只有本檔新增、無產品／測試／設定／索引／HEAD變更。明示A／B待實作及產品驗收未執行，FUT-14不因memo取得任何能力。 |

後續正式驗證命令沿 [AGENTS.md](../AGENTS.md)，必須順序執行 restore、build、test；不得把測試命令中的 MTP 與產品 `run_dotnet_task` 的 test 不支援混為同一能力。真服務模型／帳戶相容性未實測時，應填未驗證，不以公開文件或假HTTP宣稱成功。

## 13. 本次文件查核與完成條件

| 編號 | 要求 |
|---|---|
| MODEL-076 | 需求 MODEL-001～080、驗收 MODEL-AC-001～038 與交接 MODEL-HO-001～013 唯一連續；每個需求有對應案例。設定、profile、切換前後清除、取消／退出、認證、owner、實際body、用途與用量生命週期一致，不把資料丟棄誤寫成副作用復原。 |
| MODEL-077 | 核對本檔全部 JSON、Markdown fence／表格欄數、相對路徑與穩定錨點、需求／案例／交接引用；對完整未追蹤檔作 no-index 空白檢查，再做 git diff --check 與範圍查核。檢查只用讀取或私人暫存，不寫產品或索引。 |
| MODEL-078 | 依設計文件的 builder check 進行獨立唯讀檢查：找出會讓實作者對認證、切換、原生重放、預算、SAVE／MCP／checkpoint或階段邊界作錯誤猜測的缺口，補回本檔；不將檢查延伸為實作。 |
| MODEL-079 | 本次只回報實際文件檢查結果；不執行restore／build／產品測試／PTY／真人／真實服務／Release作為規格完成證據。所有產品案例仍待實作，公開資料查核與產品驗收分開，不以歷史通過數填本次結果。 |
| MODEL-080 | 完成點為可獨立開發及分層驗收的規格書、明確耦合結論及單一來源交接紀錄。後續依使用者另次實作指示才進產品開發；開始前讀本份及原交接、核對當時程式，實作時同步第10節文件與相應測試。 |

## 14. 官方參考與後續閱讀

以下為 2026-10-29 公開文件查核入口，供後續實作重新核對；不保證當時帳戶授權、服務可達性或固定版本相容。本產品的固定選擇與限制以本份為準，不把官方範例中的環境Key、預設儲存／重試或hosted工具直接搬入。

- [Gemini 模型目錄](https://ai.google.dev/gemini-api/docs/models)、[3.5 Flash-Lite](https://ai.google.dev/gemini-api/docs/models/gemini-3.5-flash-lite)、[3.6 Flash](https://ai.google.dev/gemini-api/docs/models/gemini-3.6-flash)：本份兩個 Gemini profile 的公開能力依據。
- [Gemini 原生 thought signatures](https://ai.google.dev/gemini-api/docs/generate-content/thought-signatures)：同owner原生歷史／配對及不合成簽章的依據。
- [OpenAI GPT-6.1 Terra](https://developers.openai.com/api/docs/models/gpt-6.1-terra)、[API 認證](https://developers.openai.com/api/reference/overview)：首個 OpenAI profile、Responses／reasoning選項與Bearer認證；沒有價格／品質承諾。
- [OpenAI function calling](https://developers.openai.com/api/docs/guides/function-calling)、[手動管理 Context](https://developers.openai.com/api/docs/guides/conversation-state)、[Responses 狀態與轉換](https://developers.openai.com/api/docs/guides/migrate-to-responses)、[reasoning 原生資料](https://developers.openai.com/api/docs/guides/reasoning)：原生工具配對與本機歷史策略。
- [OpenAI 錯誤分類](https://developers.openai.com/api/docs/guides/error-codes)、[資料控制與保留](https://developers.openai.com/api/docs/guides/your-data)：錯誤／store=false的保證範圍；不能據此宣稱所有服務端資料零留存。
- [八筆上游承接](#upstream-handoffs)、[十三筆前向交接](#future-handoffs)：後續規格與實作的固定閱讀入口，不另建重複memo。
