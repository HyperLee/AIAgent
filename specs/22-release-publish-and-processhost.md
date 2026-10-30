# 22：Release 發布與 ProcessHost apphost 開發規格書

> 文件版本：0.1｜建立日期：2026-10-30｜文件狀態：已實作；完整 Release 驗收未通過
>
> 選題依據：[ROADMAP 開發完成度檢查紀錄](../ROADMAP_REVIEW_NOTES.md)的 REVIEW-01。22 是子規格編號，REVIEW-01 是缺陷追蹤編號。本次僅新增本檔；以下專案修改、測試與發布驗收均為後續開發要求，尚未因本文件交付而執行。

## 產品實作接續（2026-10-30）

本節記錄後續依使用者要求執行的產品開發；下方第 1～8 節及前述「本次僅新增本檔」保留規格撰寫當時的交付與歷史證據，不能用作本次產品通過紀錄。本次來源為乾淨的 `main@50c9157326568113349e01090fcbd871c1e97c3c`，macOS 26.7.1 arm64、SDK 10.0.100、Python 3.9.6；變更留在原工作目錄，沒有另建工作樹、提交、推送或對外分發。

ProcessHost 根 `Project` 已加入 `TreatAsLocalProperty="UseAppHost"`，保留原無條件 true；CLI 專案及其參照／複製邏輯沒有修改。新增 [Python 發布回歸](../tests/test_release_publish.py)，以真 SDK、空本機 NuGet 來源與既有快取在新來源副本驗證。產品執行器、公開契約、套件及設定未變。

| 案例 | 本次產品證據與邊界 |
|---|---|
| PUB-AC-001 | 修正前屬性測試退出 1，Debug／Release 明確 false 各失敗；修正後兩個 Configuration × 三個 ProcessHost 輸入及兩筆 CLI false 查詢符合要求。直接／參照 × Debug／Release × 未指定／true／false，共 12 組乾淨來源真 SDK 建置產生 apphost。 |
| PUB-AC-002～004 | 修正前乾淨發布退出 1／MSB3030；修正後獨立新來源離線 restore 後直接發布，再從同來源增量發布至另一新目錄，兩次均退出 0。八個必要檔的種類、非空、非連結與 apphost 執行權限通過；ProcessHost 四件套 SHA-256 與當次來源 Release 建置輸出一致，CLI 根無自己的 apphost。 |
| PUB-AC-005 | 原儲存庫 Debug 與各回歸實際命令、退出碼及平台分列於 [README 當次驗證紀錄](../README.md#release-publish-validation)，不沿用歷史數量。REVIEW-02 另行追蹤，沒有修改 PTY 或產品取消行為。 |
| PUB-AC-006 | 兩次新發布的完整產物掃描各退出 1／blocked，25 檔、1 項 BUD-S003；同產物 help 均未執行。可控案例另驗證發布／結構／掃描失敗即停止，以及掃描 0 後才以同一輸出 help；停止流程符合要求不等於實際 Release 通過。 |

上述發布來源保留於 `/private/tmp/aiagent-pub-qr2p78r_/source`，兩個輸出為同根的 `first publish with spaces` 與 `second-publish`。來源起始沒有 bin／obj；直接發布前沒有預建置 Release apphost。測試以引數陣列呼叫 README 原發布命令，掃描器使用原儲存庫絕對路徑；沒有清理、略過相依 DLL、變更掃描器或啟動受阻產物。

REVIEW-01 的發布生成證據與掃描阻擋分列；BUD-AC-043、REVIEW-04 及完整 Release 仍未通過。REVIEW-02～03、真人、真實模型／Learn、其他 OS／架構及原生／分發待辦保持原狀。當次完整命令及未驗證項目統一見 [README](../README.md#release-publish-validation)，不保存命中正文、應用指示或原始診斷。

## 1. 目的與本次交付邊界

修正 Release 發布命令將全域 `UseAppHost=false` 傳入 ProcessHost，導致必要執行檔未產生、發布失敗的問題。修正後須維持 CLI 的 DLL 發布形式，同時保證內部 ProcessHost apphost 可由目前受控建置及唯讀 Git 執行器使用。

本次只交付本規格書，不修改程式、測試、套件、設定、README、ROADMAP、ROADMAP_REVIEW_NOTES、SPEC 或既有子規格，不另建實作計畫、工作樹、hook 或驗收轉錄檔；不更動 Git index／HEAD，不提交、推送或發布。本次不執行 restore、build、產品測試、PTY、publish、產物啟動或真實服務呼叫。

本規格使用 PUB-001～008 定義開發需求，PUB-AC-001～006 定義後續產品驗收。文件完成只表示需求與案例已獨立成文，不代表 REVIEW-01 已修正或完整 Release 已通過。

## 2. 基準、必讀來源與問題證據

### 2.1 基準與來源

- 原始缺陷查核基準：`1724e36`（`Add bounded read-only agent delegation`），查核日期 2026-10-30；詳細命令及結果以 [ROADMAP_REVIEW_NOTES](../ROADMAP_REVIEW_NOTES.md) 為準。
- 本規格文件基準：`31493b170266090289839cd58806b51b6c967e13`（`Document roadmap review findings`），開始撰寫時工作目錄乾淨。
- 上位約束：[總規格](../SPEC.md)的發布檢查及第 10 份適用範圍補充；[第 07 份](07-budget-and-acceptance.md) BUD-051～056、069～071、BUD-AC-043；[第 10 份](10-controlled-build-and-test.md)的 ProcessHost、產品 runtime 與原生隔離要求；[第 13 份](13-git-diff-and-code-review.md)的唯讀 Git 執行邊界。
- 現行發布入口：[README 發布與人工驗收](../README.md#發布與人工驗收)、[CLI 專案](../src/AIAgent.Cli/AIAgent.Cli.csproj)及 [ProcessHost 專案](../src/AIAgent.ProcessHost/AIAgent.ProcessHost.csproj)。
- 執行檔使用者：[DotnetProjectAdmission](../src/AIAgent.Cli/Processes/DotnetProjectAdmission.cs)及 [GitProcessRunner](../src/AIAgent.Cli/Git/GitProcessRunner.cs)。兩者依賴產品目錄內的 `ProcessHost/AIAgent.ProcessHost`，不可只以 CLI DLL 存在推論產品完整。

後續實作開始前須重新核對分支、未提交變更、SDK 與相關專案；本文件基準與歷史測試數不是未來版本的驗收證據。不讀取或附錄完整應用指示、認證或未整理的診斷內容。

### 2.2 缺陷鏈與已取得證據

現有 ProcessHost 專案雖設定 `UseAppHost=true`，命令列的全域 `UseAppHost=false` 仍會覆蓋它。CLI 專案一方面參照 ProcessHost，另一方面明列其 apphost、DLL、deps 與 runtimeconfig 為輸出及發布內容；另有 Build／Publish 後的萬用字元複製目標。停用 apphost 生成沒有同步取消執行期需求，因此在沒有舊檔時發生複製失敗。

| 證據層級 | 已觀察結果 | 能證明的範圍 |
|---|---|---|
| REVIEW-01 原始查核 | 在沒有 Release ProcessHost apphost 的情況下，README 原命令退出 1，發生 MSB3030；DLL、deps、runtimeconfig 與 PDB 已存在。 | 發布失敗與必要 apphost 缺失。 |
| REVIEW-01 對照查核 | 僅改為 `UseAppHost=true`，改用另一全新輸出目錄，發布退出 0。 | apphost 選項與缺陷相關；不是本規格選定方案的實作證據。 |
| 本規格同日規劃階段唯讀查詢 | SDK 為 10.0.100；CLI 與 ProcessHost 在 Release 且明確指定 `UseAppHost=false` 時，實際查詢均為 `false`，`SelfContained=false`、`RuntimeIdentifier` 為空。 | 現有屬性求值仍符合缺陷原因；沒有執行修正或冷發布。 |
| 同日規劃階段檔案觀察 | 原工作目錄已有 Release ProcessHost apphost。 | 直接使用該目錄重新發布可能受到舊產物影響，不能取代乾淨來源案例。 |
| REVIEW-04 對照產物掃描 | 發布成功的對照產物掃描退出 1／blocked，命中一項 BUD-S003；未繼續執行該 Release 的 `--help`。 | 既有掃描門檻未結；未確認存在真實私鑰，也不表示本規格可以放寬掃描。 |

上述實測為原查核或同日規劃階段的既有證據，本次文件落地沒有重跑發布或產品驗收。後續不得使用已有舊 apphost 的成功結果，取代沒有舊產物的驗收。

## 3. 已選方案與最小修改

使用者已選定：**CLI 保留 DLL 發布，ProcessHost 固定產生 apphost**。

後續僅在 ProcessHost 專案的根 `Project` 加入下列屬性；原有 PropertyGroup 的無條件 `UseAppHost=true` 與其他設定、參照及來源保持：

```xml
<Project Sdk="Microsoft.NET.Sdk" TreatAsLocalProperty="UseAppHost">
```

`TreatAsLocalProperty` 允許專案內設定覆蓋同名命令列全域屬性。將例外限定在 ProcessHost，讓它在直接建置與各條專案參照路徑都維持必要 apphost，而 CLI 仍可使用全域 `false`。這是後續選定的修正機制，實際 SDK 行為須由第 6 節驗收，不把文件範例當成已驗證實作。依據為 [Microsoft：MSBuild local properties](https://learn.microsoft.com/en-us/visualstudio/msbuild/msbuild-properties?view=vs-2022#local-properties)；`UseAppHost` 與原生執行檔生成的關係見 [Microsoft：UseAppHost](https://learn.microsoft.com/en-us/dotnet/core/project-sdk/msbuild-props#useapphost)。

比較過的另一方案是改用全域 `UseAppHost=true`，讓 CLI 與 ProcessHost 都產生 apphost。它已在原查核作為對照，但會改變現有 CLI 發布約定，使用者未選用；不得在實作時以此取代本方案。

不改所有專案的全域預設，不另外建立發布包裝器，也不以 `dotnet AIAgent.ProcessHost.dll` 回退取代既有執行檔。既有複製流程不作無關重構；不得新增「檔案不存在就略過」條件，讓缺少必要 ProcessHost 的產物被視為發布成功。

## 4. 開發需求、介面與相容性

| 編號 | 要求 |
|---|---|
| PUB-001 | README 現有 `UseAppHost=false` 發布命令，在沒有舊建置產物的來源副本中，完成離線 restore 後須直接成功；不得要求預先獨立建置 ProcessHost、手動補檔或先以 `true` 發布。 |
| PUB-002 | CLI 維持 framework-dependent、非單檔發布，以 `dotnet AIAgent.Cli.dll` 啟動；明確 `UseAppHost=false` 的全新發布目錄不產生 CLI 自己的 apphost。ProcessHost 的 apphost 為必要內部產物，不增加一般使用者入口。 |
| PUB-003 | ProcessHost 在 Debug／Release，以及直接或經專案參照建置時，均維持 `UseAppHost=true`；未指定、明確指定 `true` 或 `false` 都相同。外部對 ProcessHost 指定 `false` 不再停用 apphost，是本規格刻意限定的內部建置契約。 |
| PUB-004 | 發布根保有第 4.1 節的必要 CLI 檔案；ProcessHost 子目錄保有可執行 apphost、DLL、deps 與 runtimeconfig。必要檔缺失須失敗，不得只因 CLI 主檔存在便宣稱產物完整。 |
| PUB-005 | 維持受控建置及唯讀 Git 使用的 ProcessHost 路徑、啟動參數、匿名管道／固定協定、身分檢查、runtime 唯讀及既有隔離政策；不增加 DLL 回退、自由程序入口或放寬能力判斷。 |
| PUB-006 | 不新增套件、公開 API、產品 CLI 參數、設定或認證來源；維持現有目標框架及 SDK 選取政策。既有複製流程不作無關重構，不擴充 RID、自包含、單檔、交叉發布或其他平台支援。 |
| PUB-007 | 發布成功後須掃描完整產物；只有掃描退出 0 才能執行同一產物的 `--help`。發布或掃描非零即停止該次後續發布驗收並保留產物。不得忽略廠商相依檔、修改掃描規則或刪除 DLL 取得通過。 |
| PUB-008 | REVIEW-01、敏感掃描阻擋及其他未結驗收分別記錄，不解除 REVIEW-02～04；不得以發布命令成功、失敗停止案例通過或既有 Debug 測試全綠，宣稱完整 Release／人工分發完成。 |

### 4.1 必要產物契約

以下路徑相對於當次全新發布目錄。所有必要檔須為非空普通檔，不得是符號連結；apphost 另須具本機可執行權限。

| 位置 | 必要檔案 |
|---|---|
| 發布根 | `AIAgent.Cli.dll`、`AIAgent.Cli.deps.json`、`AIAgent.Cli.runtimeconfig.json`、非機密 `appsettings.json`。 |
| `ProcessHost/` | `AIAgent.ProcessHost`、`AIAgent.ProcessHost.dll`、`AIAgent.ProcessHost.deps.json`、`AIAgent.ProcessHost.runtimeconfig.json`。 |

以內容雜湊比較 ProcessHost 四件套與當次來源副本對應 Configuration 的建置輸出，確認複製內容一致，不只比檔名或時間。來源副本起始不含舊建置產物，才可排除舊檔掩蓋。此檢查不證明所有依賴可啟動或原生隔離已驗收。

這是最低必要集合，不固定總檔數，也不刪除 PDB、既有合法副本或相依組件。掃描仍涵蓋整個發布目錄，不能只掃表內檔案。

### 4.2 介面與平台邊界

公開 LLM 契約、工具 Schema、工作保存格式、設定結構、認證輸入及 runtime 執行協定均不變；唯一指定的建置政策例外是 ProcessHost 對 `UseAppHost` 的本機控制。必要產物位置沿用現況，沒有資料遷移。

首輪產品驗收基線為本機 macOS arm64、.NET SDK 10.0.100；依 `global.json` 選到相容修補版本時須記錄實際版本及各案例結果。這不擴充第 10 份的原生平台准入條件；其他 OS／架構與未實測組合保留「未驗證」，不得從發布成功推定跨平台支援。

## 5. 後續離線回歸與發布流程

### 5.1 回歸位置與乾淨來源副本

後續新增 `tests/test_release_publish.py`，以 Python 標準函式庫 `unittest`／`subprocess` 呼叫真實 SDK。測試須驗證屬性求值及實際發布結果，不只比對 XML 字串；本次不建立該測試或空殼執行器。

每個獨立案例在 `/private/tmp` 建立全新、自有的隔離根目錄；增量發布案例僅在自己的來源副本中連續執行，兩次輸出仍使用不同的新目錄。測試保留來源副本與產物，不遞迴或批次清理，不修改原工作目錄的 `bin/obj`、index、HEAD 或 hook。

來源副本僅複製 CLI、ProcessHost 的必要普通來源／專案／非機密設定與根 `global.json`，維持兩專案相對布局及 ProcessHost 連結來源。不得複製 `bin/obj`、Git metadata、敏感位置、認證檔案或符號連結。發布輸出置於來源副本之外，避免輸出被納入來源；輸出路徑包含空白的案例也須使用正確引數傳遞。

先確認副本沒有舊 `bin/obj`，再以空的本機 NuGet 來源與既有套件快取 restore。限定測試 restore 的 `NuGetAudit=false`，避免稽核網路要求；不更動產品 NuGet 設定或版本。以下路徑均為需替換的佔位，命令在隔離來源副本根目錄執行：

```sh
dotnet restore src/AIAgent.Cli/AIAgent.Cli.csproj --source "/example/empty-local-source" -p:NuGetAudit=false
```

所需 SDK、框架／apphost pack 與既有套件須預先備妥。缺少依賴時明確回報前置條件不足，必要案例不能記為通過；測試命令須非零，不得以靜默略過、下載新依賴或回退連網取得綠燈。restore 可建立新的 `obj`，但直接發布前仍不得有先行建置的 Release apphost。

實作時先確認未修正基線的屬性及乾淨發布案例能抓到原缺陷，再套用最小修正重跑；不刪除原工作目錄的舊 apphost 來製造案例。

### 5.2 當次發布命令與停止條件

restore 成功後，在來源副本根目錄直接執行與 README 相同的發布命令；只替換輸出路徑：

```sh
dotnet publish src/AIAgent.Cli/AIAgent.Cli.csproj -c Release --no-restore --no-self-contained -p:UseAppHost=false -o "/example/new publish directory"
```

發布退出 0 後先做第 4.1 節的唯讀結構／權限／內容檢查，再以原儲存庫掃描器的絕對路徑檢查該輸出目錄。下列 `/example/original-repository` 須換成原儲存庫實際根目錄；隔離來源副本不含 `scripts`，本步驟不依賴目前工作目錄：

```sh
python3 -B "/example/original-repository/scripts/check_sensitive_content.py" artifacts --path "/example/new publish directory"
```

只有掃描退出 0，才執行相同產物：

```sh
dotnet "/example/new publish directory/AIAgent.Cli.dll" --help
```

上述步驟須分別檢查退出碼，不能貼成無條件連續執行的腳本。發布、結構檢查或掃描失敗時，後續步驟標為「未執行」並保留產物；不得啟動未通過掃描的 Release CLI 或 ProcessHost 作為替代探針。`--help` 預期退出 0、無互動且無模型請求。

如仍出現既有 BUD-S003，記錄規則代號、掃描狀態、數量與退出碼，不傾印命中內容，也不直接斷言是真實私鑰。掃描後任何產物變更都須重新驗收。這些門檻只限制該次 Release 流程，既有開發者 Debug 回歸另行記錄；本規格不新增封裝、簽章、上傳或分發。

## 6. 後續產品驗收案例

所有案例均留待產品實作後執行。本次沒有新增測試、執行驗收或產生預期成功紀錄。

| 案例 | 對應需求 | 方法與成功條件 |
|---|---|---|
| PUB-AC-001 | PUB-003、006 | 以真 SDK 查詢 Debug／Release 的 `UseAppHost`；ProcessHost 未指定、明確 `true`、明確 `false` 均得到 `true`，CLI 明確指定 `false` 仍得到 `false`。另在乾淨副本直接建置 ProcessHost，以及從 CLI 專案參照建置，確認實際生成 apphost；不得只有查詢或 XML 測試。 |
| PUB-AC-002 | PUB-001～004 | 使用獨立全新來源副本，先確認無 `bin/obj`，完成第 5.1 節離線 restore 後直接執行原 Release 命令，退出 0。沒有 ProcessHost 預建置、補檔或 `true` 對照先行步驟；必要產物齊全，CLI 根沒有自己的 apphost。修正前同一案例須能暴露原缺陷。 |
| PUB-AC-003 | PUB-004～005 | 針對 PUB-AC-002 的輸出驗證第 4.1 節普通檔、非空、非連結及 apphost 執行權限；四件套雜湊與當次來源建置輸出一致。保留既有固定路徑，沒有 DLL 回退或缺檔略過。產物內容檢查不是原生執行證據。 |
| PUB-AC-004 | PUB-001～004 | 在 PUB-AC-002 的同一來源副本保留該次建置輸出，第二次發布至另一全新目錄，仍退出 0 且通過相同必要檔及內容檢查；不重用舊發布目錄、不將增量成功替代首次乾淨成功。兩次至少一個輸出路徑包含空白。 |
| PUB-AC-005 | PUB-005～006 | 從原儲存庫依序執行既有 Debug build、MSTest、Python 回歸與 macOS PTY，另執行新增發布回歸、格式及空白檢查。實際命令、退出碼、通過／失敗／略過與平台分列，沒有本修正引入的新回歸；REVIEW-02 的既知競態須依具體證據另列，不能藉本項修改它、以重跑成功抹除失敗或無證據歸因。 |
| PUB-AC-006 | PUB-007～008 | 發布失敗即停止；成功後掃描完整目錄，掃描非零時保留安全結果並確認同產物 `--help` 未執行。掃描為 0 時才以同目錄 DLL 執行 `--help`，須退出 0。分別記錄停止流程符合需求及產物實際 blocked／未結狀態，不能用失敗停止覆蓋通過宣稱完整 Release 已通過。 |

PUB-AC-001 的屬性查詢範例如下；後續須展開兩種 Configuration 與三種 ProcessHost 輸入，不把這一筆當成完整矩陣：

```sh
dotnet msbuild src/AIAgent.ProcessHost/AIAgent.ProcessHost.csproj -getProperty:UseAppHost -p:Configuration=Release -p:UseAppHost=false
```

後續預期輸出為 `true`；目前基準的既有查詢為 `false`。不可將預期值記成此次實測值。

## 7. 完成判準與跨項目交接

| 層級 | 可宣告條件 | 保留事項 |
|---|---|---|
| 本次規格完成 | 本檔內容、需求／案例對照、連結與空白檢查完成，只有本檔新增，既有追蹤檔、index／HEAD 不變。 | 產品狀態仍為待實作，REVIEW-01 原問題尚未解除。 |
| 後續 REVIEW-01 修正已驗證 | 最小專案修正落地；PUB-AC-001～004 有當次實測證據，PUB-AC-005 無本修正新回歸，PUB-AC-006 的停止邊界符合要求。 | 掃描仍 blocked 時可獨立記錄本缺陷的修正證據，但須將同一 Release 掃描及未執行的 help 保留為未結，不能宣告完整 Release 通過。 |
| 完整 Release／分發驗收 | 同一當次產物完成生成、結構檢查、完整掃描與 `--help`，並補齊適用的原生、真人、真實服務、平台及人工分發門檻。 | 完成依原上游規格及 REVIEW-04 分別判定；本規格不授權對外分發。 |

後續產品實作才同步更新 README 的發布說明、SPEC 索引、必要的第 07／10 份適用補充與 REVIEW-01 追蹤狀態；以新增當次說明保留歷史命令及失敗結果，不把原紀錄改寫為成功。本次不更新這些檔案，也不將 REVIEW-02 的 PTY 同步、REVIEW-03 的產品內 MTP test 或 REVIEW-04 的掃描／人工／服務待辦併入修正。

產品驗收結果至少記錄來源版本、SDK、OS／架構、各案例、實際命令、退出碼、產物結構與內容檢查、掃描安全分類，以及 help 執行／未執行原因。未做的項目填「未執行／未驗證」，不沿用歷史總數；不保存完整應用指示、Key、原始命中內容、請求或終端機轉錄。

## 8. 本次文件檢查與交付條件

文件交付時依序完成以下唯讀檢查，不啟動第 5、6 節的產品命令：

1. 核對標題、版本 0.1、待實作、來源與兩個基準，及使用者選定方案一致。
2. 核對 PUB-001～008、PUB-AC-001～006 唯一連續，每項需求有案例對照；乾淨／增量、直接／參照、產物完整性與掃描停止界線無矛盾。
3. 檢查本機相對連結、Markdown fences／表格、範例命令與路徑；外部官方連結為選型依據，不是此次產品通過證據。
4. 完整讀取新增檔案並檢查空白；未追蹤檔案不能只依一般 `git diff` 查核。
5. 比對既有追蹤檔案、index 與 HEAD，確認僅新增本檔，沒有產品、其他文件或 Git 歷史變更。

```sh
git diff --check
git diff --no-index --check -- /dev/null specs/22-release-publish-and-processhost.md
git status --short
git diff --name-only
git diff --cached --name-only
```

`--no-index` 的退出碼須區分檔案差異與空白診斷，不能只因新檔與空檔不同便判為檢查失敗。本次交付回報僅涵蓋實際文件檢查；不將文件範例、唯讀屬性查詢或歷史成功數當成此次產品驗收。
