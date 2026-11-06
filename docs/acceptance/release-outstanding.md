# 第 25 份實作與分層驗收紀錄

依據[第 25 份規格](../../specs/25-release-and-outstanding-acceptance.md)。本文件逐項保留當次證據；測試框架成功不代表完整 Release、真人、真實服務或 MTP 已完成。REVIEW-04 保持未結。

## 執行基準與紀錄規則

- 日期／時區：2026-11-06～07／Asia/Taipei；來源基準 `e3c4692`，本輪修改未提交，index／HEAD 保持。
- 平台：macOS 26.7.2 arm64；SDK 10.0.403、runtime 10.0.14；原生政策 `macos-26.7.1-26.7.2-arm64-v2`。
- 自動案例僅用合成資料／認證並離線；官方 NuGet 套件下載屬獨立開發準備。
- 工作紀錄及保留 fixture：`/private/tmp/aiagent-rva-5mvWs0`。未執行清理或對外發布。
- 狀態沿用規格 §10：待執行、通過、失敗、受阻、未觸發、未執行、部分完成、不適用。Skipped／Inconclusive 另記原因，不計通過。
- 相同操作可支持多個斷言；每個斷言仍須有入口、觸發 gate、結果及證據 ID。重跑使用新 ID，保留失敗及修正原因。
- 產品 CleanupConfirmed、Supervisor 直接結果與獨立 watchdog 代清分列。guard 代清不能將產品結果改成成功。
- 本地檔案系統拒絕非 UTF-8 檔名（EILSEQ）；Unicode 名稱的原始 bytes 排序已測，任意 byte 檔名的實機能力未驗證。

## 證據模板

每批填寫：證據 ID／日期時區；來源 commit／未提交範圍；候選精確版本／固定官方來源；層級／入口；OS／架構／FS 語意／Terminal／IME／鍵盤；SDK／runtime／MTP／Git 與相依；recipe／parser／sandbox／外層條件；產物新目錄／Configuration／八檔與完整清單內容及 mode 身分；profile／目的地／授權／生成與外送上限；fixture／自有 PID＋開始身分／watchdog 責任；實際命令及退出碼；RVA／N／H／S／上游案例與每項斷言；框架總數／通過／失敗／略過；真實 scan 的狀態／規則／計數；來源 bytes／實際時間／終態／收尾／用量；受阻／未觸發／未執行原因；產物是否變更／失效範圍／是否發布。

僅保存安全分類、數值、合成 canary 雜湊一致性及非機密身分。不得附 Key、完整應用指示、原始 HTTP／SDK／MCP／PTY／模型轉錄、命中正文或原始 stderr。

## Release 產物身分

`AIAgent-artifact-identity-v1`：逐項以原始相對名稱 bytes 排序；每筆為四位元組大端名稱長度、名稱、D／F 類型、四位元組大端 permission bits，以及普通檔的 SHA-256。納入根與所有目錄 mode、所有普通檔（含隱藏與相依）；不跟隨連結。兩次 descriptor-based 觀察的內容及 metadata 必須一致，另綁定根 device／inode。不是 scanner、簽章或不可變保證；檢查後再變更仍須重驗。

publish → 八檔／權限／四件套比對 → 身分 → 完整 scan → 身分比對 → help 前身分比對 → 同目錄 help → 身分比對。任一失敗停止後續啟動。blocked、incomplete、非法摘要及產物變動各自記錄。

## 官方候選查核

官方來源為 NuGet v3 flat-container 的精確版本 nupkg 與其 nuspec repository commit；完整 package／DLL 雜湊、相依與固定來源 metadata 保留於本地 `candidates/metadata.json`。以未修改的 ContentMatcher 做單組件觀察，這不是完整發布掃描。

| 套件／精確版本 | 官方固定 commit | 單組件結果及資格 |
|---|---|---|
| Google.GenAI 1.27.0 | [固定來源](https://github.com/googleapis/dotnet-genai/tree/2a9d172558cf4461391bce9fd1a3e6668e51d4c6) `2a9d172558cf4461391bce9fd1a3e6668e51d4c6` | 主 DLL 無命中；Auth 仍為 >= 1.69.0，完整資格未完成 |
| Google.GenAI 1.28.0 | [固定來源](https://github.com/googleapis/dotnet-genai/tree/e1e8110f8d1d2df0f9bff00146bfe03545d5edaa) `e1e8110f8d1d2df0f9bff00146bfe03545d5edaa` | 主 DLL 無命中；Auth 仍為 >= 1.69.0，完整資格未完成 |
| Google.GenAI 2.0.0 | [固定來源](https://github.com/googleapis/dotnet-genai/tree/f72b7e6a218363abc383a58dc7b97e60235ca04b) `f72b7e6a218363abc383a58dc7b97e60235ca04b` | 主 DLL 無命中；Auth 仍為 >= 1.69.0，完整資格未完成 |
| Google.Apis.Auth 1.70.0 | [固定來源](https://github.com/googleapis/google-api-dotnet-client/tree/9a4236184dde96e33770d89b7827e4ee757bfe69) `9a4236184dde96e33770d89b7827e4ee757bfe69` | BUD-S003；此候選元件仍受阻 |
| Google.Apis.Auth 1.71.0 | [固定來源](https://github.com/googleapis/google-api-dotnet-client/tree/2f9d63a4eaaa7d190d3dfb2cf886a19092b881e2) `2f9d63a4eaaa7d190d3dfb2cf886a19092b881e2` | BUD-S003；此候選元件仍受阻 |
| Google.Apis.Auth 1.72.0 | [固定來源](https://github.com/googleapis/google-api-dotnet-client/tree/b0198894ac065b2af51b87444206b9d82aec2c69) `b0198894ac065b2af51b87444206b9d82aec2c69` | BUD-S003；此候選元件仍受阻 |
| Google.Apis.Auth 1.73.0 | [固定來源](https://github.com/googleapis/google-api-dotnet-client/tree/6386ed813d1de3556ac5a4f211160b3765ba1c10) `6386ed813d1de3556ac5a4f211160b3765ba1c10` | BUD-S003；此候選元件仍受阻 |
| Google.Apis.Auth 1.74.0 | [固定來源](https://github.com/googleapis/google-api-dotnet-client/tree/4510b35f16f13b38c9f4505df818e132c8b9ceef) `4510b35f16f13b38c9f4505df818e132c8b9ceef` | BUD-S003；此候選元件仍受阻 |
| Google.Apis.Auth 1.75.0 | [固定來源](https://github.com/googleapis/google-api-dotnet-client/tree/98e41d392e31e664d9e2bbfe4348717d12dda7e7) `98e41d392e31e664d9e2bbfe4348717d12dda7e7` | BUD-S003；此候選元件仍受阻 |
| Google.Apis.Auth 1.76.0 | [固定來源](https://github.com/googleapis/google-api-dotnet-client/tree/dd7f8ffe5a9aa3b9b07174dc03faf0f3289e7e24) `dd7f8ffe5a9aa3b9b07174dc03faf0f3289e7e24` | BUD-S003；此候選元件仍受阻 |
| Google.Apis.Auth 1.77.0 | [固定來源](https://github.com/googleapis/google-api-dotnet-client/tree/a7d25ddf34d6e8db8a106e93dfd7baf0347d4f61) `a7d25ddf34d6e8db8a106e93dfd7baf0347d4f61` | BUD-S003；此候選元件仍受阻 |
| Google.Apis.Auth 1.78.0 | [固定來源](https://github.com/googleapis/google-api-dotnet-client/tree/e14fc8a0671f76b040b726b29b4a77a0e13760af) `e14fc8a0671f76b040b726b29b4a77a0e13760af` | BUD-S003；此候選元件仍受阻 |

Auth 1.70.0～1.78.0 的所有套件內目標 DLL 均仍命中既有 BUD-S003；未修改或重新封裝 DLL。GenAI 1.27.0、1.28.0、2.0.0 主 DLL 無命中不能替代相依與完整 Release。先按同 major 的最低版本評估；下一 major 只在既有相容性契約允許時研究。GenAI 2.0.0 的官方變更另包含預設 API 版本調整，不能暗中改產品固定端點。

正式相依保留 Google.GenAI 1.26.1／Auth 1.69.0，MCP 2.3.0 不變。沒有完整來源／解析／API／離線／原生／完整產物與兩 Gemini 真服務證據的候選，一律不採用；真服務目前未授權、未執行。

## 當次執行證據

| ID | 入口／命令 | 實際結果 | 判定與限制 |
|---|---|---|---|
| R01-red | 新增 ReleaseAcceptanceTests，針對修正前執行 | 8 個 test methods；9 個失敗子項、12 個 missing-identity 錯誤子項 | 抓到摘要一致性與身分檢查缺口，未啟動真實 Release |
| R01-green | `PYTHONPATH=tests python3 -B -m unittest test_release_acceptance test_release_publish.ReleaseBoundaryTests` | 17 通過，退出 0 | 停止邊界／真檔案身分；不代表真實 Release scan 通過 |
| N00-red | 新原生測試建置 | 13 個 fixture 缺少錯誤，退出 1；後續抓到缺少兩個 internal 觀測介面 | 測試準備的紅燈，非產品原生驗收 |
| N00-compiler | 原生第一輪；乾淨環境直接呼叫 clang | 2 個失敗；缺少系統 headers | 修正測試準備為 `xcrun clang`，不繼承使用者環境 |
| ENV01 | 外層受限環境 `sandbox-exec` 最小只讀探針 | 退出 71，sandbox_apply 被外層環境拒絕 | 此環境的產品原生正向受阻；不得停用產品 sandbox |
| SDK01 | SDK wrapper 的 `dotnet test ... -- --help` | 退出 134；SDK help／IPC 分派錯誤 | 僅查詢失敗，不是產品 test 能力；直接測試 DLL help 退出 0，後續使用實際 filter |

## RVA 逐項追蹤

| 案例 | 必要證據與子項 | 狀態／證據 ID |
|---|---|---|
| RVA-AC-002 | 官方候選來源／版本／解析／API／框架／固定產物可追溯；不存在合格候選時明確受阻，未捏造版本或改 DLL。來源查核及候選真實建置。 | 部分完成／受阻；C01～C04。已查候選未取得完整資格，正式相依未採用。 |
| RVA-AC-003 | 六規則、編碼／跨塊／二進位／限額／異動／診斷與退出碼既有回歸保留；候選不引入 allowlist 或另一套掃描器。離線 Python／變更檢查。 | 離線通過；P02。既有六規則／編碼／跨塊回歸及 scanner 未修改查核。 |
| RVA-AC-004 | 真 SDK 乾淨／增量兩個新目錄各驗八檔、mode、四件套雜湊、全目錄 scan 與同產物 help；真實 blocked 與停止測試分列。 | 部分完成；R03。生成／結構／身分通過，兩次真實 scan 受阻、help 未執行。 |
| RVA-AC-005 | 注入 publish／結構／scan 1／scan 2／非法摘要／help 失敗，確認後續未呼叫；通過後改檔／mode／相依使舊身分失效。離線停止邊界。 | 離線通過；R01-green、P02。19 項定向發布／身分／離線環境回歸，涵蓋非法摘要與失效。 |
| RVA-AC-006 | N01、N02：真正 sandbox 正常／失敗 build 及完整 120 秒；產品結果、來源 bytes、實際時間與收尾證據齊全。 | Debug 指定入口通過；M03／N01～N12。Release 產物受阻，不能將 Debug 證據升格。 |
| RVA-AC-007 | N03、N04：TERM 被忽略後 KILL、根先退出及 pipe 持有／輸出，按 gate 真實觸發、在原預算收尾；guard 代清不算產品成功。 | Debug 指定入口通過；M03／N01～N12。Release 產物受阻，不能將 Debug 證據升格。 |
| RVA-AC-008 | N05、N06：已觀測變群組及 255／256／257 身分，驗真實身分、上限、不確定結果與應用鎖定，不只使用合成快照。 | Debug 指定入口通過；M03／N01～N12。Release 產物受阻，不能將 Debug 證據升格。 |
| RVA-AC-009 | N07、N08：真管道兩方向中斷、wrapper 崩潰與父控制端消失分列；無假回執／假清除，應用與 guard 結果分開。 | Debug 指定入口通過；M03／N01～N12。Release 產物受阻，不能將 Debug 證據升格。 |
| RVA-AC-010 | N09、N10：核准後身分交換零目標啟動；執行中祖先／link／別名競爭的來源／敏感 canary 不變，記真 FS 語意與拒絕／停止。 | Debug 指定入口通過；M03／N01～N12。Release 產物受阻，不能將 Debug 證據升格。 |
| RVA-AC-011 | N11：真正 probe／產品 sandbox 的讀寫、短根、權限、碰撞、socket 邊界及網路／IPC 拒絕，不為測試外送或停用政策。 | Debug 指定入口通過；M03／N01～N12。Release 產物受阻，不能將 Debug 證據升格。 |
| RVA-AC-012 | N12：快速脫離的觀測限制、guard 及產品結果如實記錄，沒有「全部程序已清除」或新平台保證。 | Debug 指定入口通過；M03／N01～N12。Release 產物受阻，不能將 Debug 證據升格。 |
| RVA-AC-013 | H01～H03：真人 IME、輸入、長結果、完整差異、核准及取消／EOF／還原；逐列原案例斷言與 bytes 證據。 | 未執行；真人待操作，且 Release 門檻受阻；相關 test 子項另待第 24 份。 |
| RVA-AC-014 | H04～H08：Plan／程序／Context／規則／Git 真人操作；test 依賴部分保留，不將部分完成算整列成功。 | 未執行；真人待操作，且 Release 門檻受阻；相關 test 子項另待第 24 份。 |
| RVA-AC-015 | H09～H12：Fn／多行／歷史／保存續接／單檔復原／技能，正式 CLI 與真人組合，無批次清理。 | 未執行；真人待操作，且 Release 門檻受阻；相關 test 子項另待第 24 份。 |
| RVA-AC-016 | H13～H15：外送完整新確認、三 profile 選模及兩子交錯長結果，逐項觸發與終端機恢復。 | 未執行；真人待操作，且 Release 門檻受阻；相關 test 子項另待第 24 份。 |
| RVA-AC-017 | S01～S03：真 Gemini 聊天／認證、唯讀／搜尋與寫入；實際分類、工具、本機 bytes、未觸發及新核准分列。 | 未執行；未另次授權真服務，且 Release／相關 MTP 依賴未結。 |
| RVA-AC-018 | S04～S07：Plan／build、摘要／額度、規則／技能、Review 的真服務斷言；MTP 未資格時 test 正向保留受阻。 | 未執行；未另次授權真服務，且 Release／相關 MTP 依賴未結。 |
| RVA-AC-019 | S08～S10：多行送出、真服務保存／重啟與 checkpoint 交接，確認不重播／重建資格，原生與模型結果分列。 | 未執行；未另次授權真服務，且 Release／相關 MTP 依賴未結。 |
| RVA-AC-020 | S11：三個精確 profile 分列同 owner 往返及兩類切換；失敗／quota／不可用不回退，認證與 Context 不跨 owner。 | 未執行；未另次授權真服務，且 Release／相關 MTP 依賴未結。 |
| RVA-AC-021 | S12：真 Learn 固定協定／三工具與真 Gemini 搜尋→fetch→引用分別有證據；不相容受阻，不降版。 | 未執行；未另次授權真服務，且 Release／相關 MTP 依賴未結。 |
| RVA-AC-022 | S13：三 profile 各有真實兩子並行、唯讀工具、額度、彙整與收尾證據；無法觀測並行就未驗證。 | 未執行；未另次授權真服務，且 Release／相關 MTP 依賴未結。 |
| RVA-AC-023 | 能力矩陣區分 CLI／build／test／Git，未知平台負向與未實機範圍正確；開發者 test／parser 不抵 MTP 正向，無新增啟用。 | 部分完成；M03、P02。既有能力／負向回歸通過；其他 OS／架構未實測，MTP 正向受阻。 |
| RVA-AC-024 | 真實候選產物與實際 index／文件／附件／最終形式人工檢查分列；scan blocked 或包裝未驗不分發，沒有自動對外動作。 | 部分完成；IDX01、R03。index 掃描通過；未暫存／未追蹤另查，Release／包裝／分發受阻或未執行。 |
| RVA-AC-025 | 第 10、11 節證據表可逐列重建結論；所有需求都有案例，部分／未觸發／受阻不算通過，MTP 或其他必要依賴未齊不得解除 REVIEW-04。 | 分層紀錄接續；依本表及 N／H／S 子項判定；必要條件未齊，不解除 REVIEW-04。 |
| RVA-AC-026 | 官方相依／最小修正後，既有 MSTest、Python、PTY、help、格式／空白及受影響原生回歸按實際結果記錄；無新增網路自動測試，保留既有格式問題，不宣稱限定檢查等於全專案通過。 | 部分完成；M03、P02、T01、T02、F01。自動回歸通過；全專案格式仍有三份歷史差異，真人／服務等未結。 |

## 原生 N01～N12 操作與斷言

每個 fixture 先有 watchdog-ready，再 armed 後啟動操作；ready、真正已觀測與故障 gate 分開。完整逾時外部 150 秒、身分上限 60 秒、其他 30 秒；收尾保留產品 2／3／1 秒與最多 2 秒觀察容差。C helper 僅為測試來源，不進入產品發布。

### N01 正常／失敗；BUILD-AC-014、015、020

操作順序／gate：產品入口各執行可信成功、語法失敗專案；保存來源 canary 的前後雜湊，核對 target Started 與最終回執。

- A01：正常 0 且收尾確認才 BuildSucceeded。結果：Debug 指定入口通過；證據 ID：M03。Release 分支未執行。
- A02：非零保留實際結果但不成功。結果：Debug 指定入口通過；證據 ID：M03。Release 分支未執行。
- A03：來源不變、產物僅在允許範圍。結果：Debug 指定入口通過；證據 ID：M03。Release 分支未執行。

### N02 完整逾時；BUILD-AC-023

操作順序／gate：新回合、完整 120 秒剩餘額度；BeforeTargets helper 維持執行。記產品取得啟動權、Started、目標存活、開始收尾及最終結果的 monotonic 時間；不送取消或製造先到的輸出故障。

- A01：依實際預算起點與傳入剩餘期限核對沒有提前逾時或重置。結果：Debug 指定入口通過；證據 ID：M03。Release 分支未執行。
- A02：不以短 timeout 取代。結果：Debug 指定入口通過；證據 ID：M03。Release 分支未執行。
- A03：得到 TimedOut、空退出碼、未完成 Validation，必要收尾確認。結果：Debug 指定入口通過；證據 ID：M03。Release 分支未執行。
- A04：若不確定則較嚴格 Unconfirmed／退出 1。結果：Debug 指定入口通過；證據 ID：M03。Release 分支未執行。

### N03 忽略 TERM；BUILD-AC-022、023

操作順序／gate：helper 完成 TERM handler 安裝並回報 ready，後代已被實際觀測後才取消；helper 回報曾收到 TERM 且繼續存活。

- A01：先 TERM 再必要 KILL，在既有 2／3／1 秒階段及共用收尾預算內完成。結果：Debug 指定入口通過；證據 ID：M03。Release 分支未執行。
- A02：不得因 wrapper 退出就放行存活後代。結果：Debug 指定入口通過；證據 ID：M03。Release 分支未執行。
- A03：無確認則 Unconfirmed。結果：Debug 指定入口通過；證據 ID：M03。Release 分支未執行。

### N04 根先退出／持有 pipe；BUILD-AC-022、024

操作順序／gate：helper 建立已觀測後代並保持 stdout／stderr 開啟，接到測試釋放才讓根正常退出；另測後代持續輸出。

- A01：根 0 不立即等於操作成功。結果：Debug 指定入口通過；證據 ID：M03。Release 分支未執行。
- A02：停止已觀測後代並完成有界讀管線。結果：Debug 指定入口通過；證據 ID：M03。Release 分支未執行。
- A03：未確認或讀取未結依原故障政策停止，不無限排空。結果：Debug 指定入口通過；證據 ID：M03。Release 分支未執行。

### N05 已知後代改群組；BUILD-AC-022

操作順序／gate：Supervisor 真觀測 gate 後才允許 helper setpgid／setsid；再次以原生身分確認是同一後代、群組已改。

- A01：驗證按身分嘗試收尾。結果：Debug 指定入口通過；證據 ID：M03。Release 分支未執行。
- A02：現行監督器將改群組列為不確定，須 Unconfirmed、鎖定及退出 1，即使 harness 代清成功也不能改成產品成功。結果：Debug 指定入口通過；證據 ID：M03。Release 分支未執行。

### N06 身分上限；BUILD-AC-022

操作順序／gate：使用受限資源的可信 helper 與 barrier，分別取得 255／256／257 個同時有效身分。現行 known 集合含根，256 包含根及已記錄後代；先批次建立總數 255，取得全數真實觀測 gate 後才增加至 256，再取得觀測 gate 才加入第 257 個。使用第 5.1 節獨立 60 秒 watchdog，不以每 100ms 逐筆建立方式消耗整段期限。

- A01：256 為集合上限。結果：Debug 指定入口通過；證據 ID：M03。Release 分支未執行。
- A02：第 257 個不可無界納入，超限不得成功，須不確定收尾及停止。結果：Debug 指定入口通過；證據 ID：M03。Release 分支未執行。
- A03：未納入身分由獨立 guard 負責，不宣稱產品已清除全部。結果：Debug 指定入口通過；證據 ID：M03。Release 分支未執行。
- A04：資源不足則環境受阻，不降低數量冒稱實測。結果：Debug 指定入口通過；證據 ID：M03。Release 分支未執行。

### N07 控制管道中斷；BUILD-AC-009、022、031

操作順序／gate：測試擁有的真正 ProcessHost／管道：Started 及後代觀測後，分別斷父→host 控制與 host→父結果管道。直接 host 與產品整合觀察分列。

- A01：本機不能靠 stdout 或 wrapper 退出補造回執。結果：Debug 指定入口通過；證據 ID：M03。Release 分支未執行。
- A02：host 可執行時仍嘗試收尾，應用無確認則 Unconfirmed／退出 1，零下一回合程序。結果：Debug 指定入口通過；證據 ID：M03。Release 分支未執行。

### N08 監督器崩潰；BUILD-AC-022、031

操作順序／gate：確認當次 wrapper、root、後代身分後，只終止該自有 ProcessHost；另測父控制端消失，禁止把兩者混成同案例。

- A01：wrapper 崩潰不能回報已清除。結果：Debug 指定入口通過；證據 ID：M03。Release 分支未執行。
- A02：產品受控失敗／鎖定，殘留由 guard 處理且另記。結果：Debug 指定入口通過；證據 ID：M03。Release 分支未執行。
- A03：父端消失時 host 可確認的收尾另列，不擴張成抗所有父／OS 崩潰保證。結果：Debug 指定入口通過；證據 ID：M03。Release 分支未執行。

### N09 核准與輸出祖先交換；BUILD-AC-008、021、029

操作順序／gate：在完整預覽等待新同意的 gate 交換專案 bytes／runtime／bin／obj 或其祖先；執行中競爭另用 helper phase 與受限合成樹。

- A01：核准後身分不符零目標啟動。結果：Debug 指定入口通過；證據 ID：M03。Release 分支未執行。
- A02：執行中來源／敏感 canary 不被更動，拒絕或非成功停止。結果：Debug 指定入口通過；證據 ID：M03。Release 分支未執行。
- A03：不能重用舊核准或擴大可寫根。結果：Debug 指定入口通過；證據 ID：M03。Release 分支未執行。

### N10 link、大小寫／Unicode；BUILD-AC-020、021

操作順序／gate：既有／新建 symlink、hard-link、來源 inode 的輸出別名，以及大小寫／正規化名稱；明列實際檔案系統的比較／正規化語意。

- A01：不以可寫別名更動來源／合成敏感 inode。結果：Debug 指定入口通過；證據 ID：M03。Release 分支未執行。
- A02：必要拒絕與 canary 不變都須成立。結果：Debug 指定入口通過；證據 ID：M03。Release 分支未執行。
- A03：不能只證明路徑字串不同。結果：Debug 指定入口通過；證據 ID：M03。Release 分支未執行。
- A04：測不到的 FS 組合標未驗證。結果：Debug 指定入口通過；證據 ID：M03。Release 分支未執行。

### N11 私人狀態／網路／IPC；BUILD-AC-020

操作順序／gate：真正 sandbox 探針及受控專案讀 SDK／套件、寫 bin／obj／私人 tmp，驗短根、owner／mode、碰撞與 socket 長度；對合成目標測 IPv4／IPv6、loopback、DNS、非私人 IPC。

- A01：允許／拒絕符合原政策。結果：Debug 指定入口通過；證據 ID：M03。Release 分支未執行。
- A02：不建立外部服務連線來測拒絕，探針失敗零目標執行，不回退 TCP／全域 tmp。結果：Debug 指定入口通過；證據 ID：M03。Release 分支未執行。
- A03：通用 IPC 探針成功仍不抵真實 MTP。結果：Debug 指定入口通過；證據 ID：M03。Release 分支未執行。

### N12 快速脫離限制；BUILD-AC-022

操作順序／gate：自有、會自行到期的 helper 在觀測前執行 setsid／double-fork／重新掛接；用獨立 guard 追蹤全部自有身分及最終存活狀況。

- A01：記錄可觀測範圍、實際產品結果與限制。結果：Debug 指定入口通過；證據 ID：M03。Release 分支未執行。
- A02：即使根 0 或產品未知後代，也不能宣稱整棵已清除。結果：Debug 指定入口通過；證據 ID：M03。Release 分支未執行。
- A03：只驗證限制已明示、無虛假保證，不將其提升為已支援的惡意逃離情境。結果：Debug 指定入口通過；證據 ID：M03。Release 分支未執行。


## 真人 H01～H15 交接

目前所有真人項目未執行。先記正式 Release 身分、Terminal 版本／欄寬／Fn 鍵與系統繁體中文 IME 組合。CLI 合成本機操作不使用真認證；需生成的步驟等另次服務授權。逐筆操作測試存檔，不批次刪除。

### H01 啟動與基本操作；[第 03 份](../../specs/03-cli-and-context.md) CLI-AC-033、[第 04 份](../../specs/04-workspace-and-read-tools.md) WS-AC-032

操作順序／gate：啟動、確認工作區、隱藏合成 Key；中文組字／選字、貼上／刪除，本機 help／clear／退出，等待輸入時 Ctrl+C 與 EOF 分開操作。

- A01：Key／確認輸入無回顯，文字與命令邊界正確。結果：未執行（真人／Release 前置未齊）；證據 ID：無。
- A02：對應退出狀態及 ECHO／ICANON／ISIG 恢復，能正常使用終端機。結果：未執行（真人／Release 前置未齊）；證據 ID：無。

真人觀察／底層 bytes／終端機還原：未執行。

### H02 工具與取消；[第 05 份](../../specs/05-agent-loop.md) LOOP-AC-038、[第 08 份](../../specs/08-code-search-and-partial-read.md) SEARCH-AC-034

操作順序／gate：以中文／emoji／空白名稱搜尋；觀察生成／長工具結果，分別在確實在途時 Ctrl+C、外部 SIGINT；等待收尾後送下一訊息，另開一輪測 EOF。

- A01：進度對應實際階段。結果：未執行（真人／Release 前置未齊）；證據 ID：無。
- A02：busy 預輸入不變成下一回合，結果截斷可辨識，取消後可操作或依契約退出。結果：未執行（真人／Release 前置未齊）；證據 ID：無。
- A03：未觸發在途分支不能算該分支通過。結果：未執行（真人／Release 前置未齊）；證據 ID：無。

真人觀察／底層 bytes／終端機還原：未執行。

### H03 寫入預覽；[第 06 份](../../specs/06-file-edit-and-approval.md) EDIT-AC-043、[第 07 份](../../specs/07-budget-and-acceptance.md) BUD-AC-046

操作順序／gate：查看長行差異、BOM／行尾；依次同意、拒絕、空白、取消，測預貼 yes；再讀 bytes 核對，clear／退出。

- A01：完整預覽可讀且未裁切授權。結果：未執行（真人／Release 前置未齊）；證據 ID：無。
- A02：新同意才寫，拒絕／取消不寫，取消後下一回合正常，已確認寫入不被後續失敗抹除。結果：未執行（真人／Release 前置未齊）；證據 ID：無。

真人觀察／底層 bytes／終端機還原：未執行。

### H04 Plan；[第 09 份](../../specs/09-plan-mode-and-task-list.md) PLAN-AC-034

操作順序／gate：查看中文長任務清單，拒絕／取消後重看，明確確認；在下一訊息要求 Execute，再觀察逐筆核准。

- A01：模式／版本可辨識，計畫確認不立即執行、不授權寫入或程序，退出後終端機正常。結果：未執行（真人／Release 前置未齊）；證據 ID：無。

真人觀察／底層 bytes／終端機還原：未執行。

### H05 受控程序；[第 10 份](../../specs/10-controlled-build-and-test.md) BUILD-AC-036

操作順序／gate：看中文長專案路徑、完整權限、來源與原生限制，拒絕與同意 build；在途取消，核對來源／產物及結果。

- A01：核准、結果／截斷／保留說明可理解。結果：未執行（真人／Release 前置未齊）；證據 ID：無。
- A02：來源不變，收尾後才續操作。結果：未執行（真人／Release 前置未齊）；證據 ID：無。
- A03：test／Skipped 正向呈現待第 24 份，不能整項先勾通過。結果：未執行（真人／Release 前置未齊）；證據 ID：無。

真人觀察／底層 bytes／終端機還原：未執行。

### H06 Context／額度；[第 11 份](../../specs/11-context-and-budget-management.md) CTX-AC-034

操作順序／gate：操作中文／尾空白原文 pin、檢視摘要與容量、調額度、取消及退出。

- A01：原文與模型摘要可分辨，估算／實際／API 用量不混淆，調額度不自動續跑。結果：未執行（真人／Release 前置未齊）；證據 ID：無。

真人觀察／底層 bytes／終端機還原：未執行。

### H07 規則；[第 12 份](../../specs/12-project-rules-loading.md) RULE-AC-033

操作順序／gate：查看完整合成規則與來源，選中文 Scope；外部改測試規則後重載，重新確認及給新指示。

- A01：規則與操作核准可區分，失效後不沿用舊確認，來源顯示完整且路徑可辨識。結果：未執行（真人／Release 前置未齊）；證據 ID：無。

真人觀察／底層 bytes／終端機還原：未執行。

### H08 Git Review；[第 13 份](../../specs/13-git-diff-and-code-review.md) GIT-AC-021

操作順序／gate：對合成 Git 三層差異查看長路徑、折行、原始行尾與位置，觀察部分報告，分別取消／EOF。

- A01：staged／工作檔與左右側位置可辨識，部分報告不冒稱完整，退出還原正常。結果：未執行（真人／Release 前置未齊）；證據 ID：無。

真人觀察／底層 bytes／終端機還原：未執行。

### H09 多行編輯；[第 14 份](../../specs/14-terminal-interaction.md) TERM-AC-030

操作順序／gate：中文組字與選字、emoji／Tab 邏輯游標、Fn／F2／F3／F4、長行折行、多行預覽、歷史回取及貼上；另測核准輸入。

- A01：草稿／回取不派送，文字與游標邊界正確，核准仍隱藏且不接受編輯功能鍵旁路。結果：未執行（真人／Release 前置未齊）；證據 ID：無。
- A02：組合不符就記該組合未通過。結果：未執行（真人／Release 前置未齊）；證據 ID：無。

真人觀察／底層 bytes／終端機還原：未執行。

### H10 保存／續接；[第 15 份](../../specs/15-work-save-and-resume.md) SAVE-AC-030

操作順序／gate：保存合成摘要、list／show、clear、重啟續接，另對逐筆指定的測試存檔操作 delete／取消。

- A01：明文保存與歷史／目前資格可辨識。結果：未執行（真人／Release 前置未齊）；證據 ID：無。
- A02：clear 不等於刪存檔，續接重新確認、不恢復舊認證／核准。結果：未執行（真人／Release 前置未齊）；證據 ID：無。

真人觀察／底層 bytes／終端機還原：未執行。

### H11 復原；[第 16 份](../../specs/16-change-checkpoints-and-recovery.md) CKPT-AC-031

操作順序／gate：修改測試檔、查看 checkpoint 差異，拒絕及新確認單檔復原，核對 bytes；檢查新檔指引與規則 reload。

- A01：復原需新確認、取消不變更。結果：未執行（真人／Release 前置未齊）；證據 ID：無。
- A02：新檔不被批次刪除，模式／失效與下一指示要求可理解。結果：未執行（真人／Release 前置未齊）；證據 ID：無。

真人觀察／底層 bytes／終端機還原：未執行。

### H12 技能；[第 17 份](../../specs/17-reusable-workflows-and-skills.md) SKILL-AC-030

操作順序／gate：檢視中文合成技能、明確選用，改正文後觀察失效及重載，取消／停用。

- A01：檢視不等於啟用，來源／Scope／正文可讀。結果：未執行（真人／Release 前置未齊）；證據 ID：無。
- A02：失效後仍需新指示與各次核准。結果：未執行（真人／Release 前置未齊）；證據 ID：無。

真人觀察／底層 bytes／終端機還原：未執行。

### H13 外送；[第 18 份](../../specs/18-mcp-external-tools.md) MCP-AC-030、[第 19 份](../../specs/19-web-search-and-document-fetch.md) WEB-AC-027

操作順序／gate：在已授權 Learn 時段查看中文查詢與長 URL 的完整外送預覽；拒絕、新確認、取消，改規則／Scope 後重看。

- A01：每次目的地、參數與版本可讀，確認隱藏，舊 Permit 不復用，取消／失效後不外送。結果：未執行（真人／Release 前置未齊）；證據 ID：無。

真人觀察／底層 bytes／終端機還原：未執行。

### H14 選模；[第 20 份](../../specs/20-multi-model-and-provider-support.md) MODEL-AC-035

操作順序／gate：檢視三 profile、拒絕及確認切換；看資料清除風險，使用者隱藏輸入新 Key；觀察 Plan、用途與分組用量。

- A01：供應商／目的地可辨識，新認證無回顯。結果：未執行（真人／Release 前置未齊）；證據 ID：無。
- A02：切換後等新指示，舊 Context 不重播，用量與終端機狀態正常。結果：未執行（真人／Release 前置未齊）；證據 ID：無。

真人觀察／底層 bytes／終端機還原：未執行。

### H15 委派；[第 21 份](../../specs/21-multi-agent-delegation.md) MULTI-AC-037

操作順序／gate：明確啟用後觀察兩子長結果與交錯進度，取消／EOF，再測既有核准互動。

- A01：父／子來源可分辨，完整收尾後才續輸入。結果：未執行（真人／Release 前置未齊）；證據 ID：無。
- A02：委派不授權寫入或跳過核准。結果：未執行（真人／Release 前置未齊）；證據 ID：無。
- A03：沒有實際兩子就不能勾掉交錯呈現。結果：未執行（真人／Release 前置未齊）；證據 ID：無。

真人觀察／底層 bytes／終端機還原：未執行。


## 真實服務 S01～S13 交接

目前全部未授權、未執行。執行前逐批填 profile、固定官方目的地、案例、產品額度、最大生成與外送次數、可能成本及停止條件，再取得明確授權。三個精確 profile 分列；Key 只由使用者在正式產品隱藏輸入。quota／認證／協定錯誤、取消或達上限即停止，不自動重試／切模型。Learn connect、兩搜尋與 fetch 各有新核准；兩子並行須有實際開始／完成重疊證據。

### S01 聊天／認證；[第 03 份](../../specs/03-cli-and-context.md) CLI-AC-034、035；[第 07 份](../../specs/07-budget-and-acceptance.md) BUD-AC-047

操作順序／gate：使用已授權 Gemini，一般文字及超過五個完整回合、clear、生成中取消後新訊息；另輪以無功能假值觀察實際認證失敗，再隱藏輸入有效 Key。

- A01：回答／裁切／clear／用量或不可得各自核對。結果：未執行（未另次服務授權）；證據 ID：無。
- A02：只有真 AuthenticationFailed 才完成該分支。結果：未執行（未另次服務授權）；證據 ID：無。
- A03：累計保留、舊 Context 清除、新訊息才生成，無額外認證探測。結果：未執行（未另次服務授權）；證據 ID：無。

本機工具事實／模型觀察／未觸發分支／用量：未執行。

### S02 唯讀與搜尋；[第 04 份](../../specs/04-workspace-and-read-tools.md) WS-AC-033、[第 05 份](../../specs/05-agent-loop.md) LOOP-AC-039、[第 08 份](../../specs/08-code-search-and-partial-read.md) SEARCH-AC-035

操作順序／gate：合成 UTF-8／BOM／中文檔，列檔→定位→搜尋→範圍讀取→回答；檢查受限路徑、截斷與 clear 後重新讀取。

- A01：按實際工具與本機來源核對，模型文字不是讀檔事實。結果：未執行（未另次服務授權）；證據 ID：無。
- A02：未觸發分支保留未觸發，不將合成或離線故障注入列為真服務結果。結果：未執行（未另次服務授權）；證據 ID：無。

本機工具事實／模型觀察／未觸發分支／用量：未執行。

### S03 修改；[第 06 份](../../specs/06-file-edit-and-approval.md) EDIT-AC-044；BUD-AC-047 的讀寫案例

操作順序／gate：新建、完整讀檔後唯一替換，依次同意／拒絕；測 BOM／行尾及在預覽 gate 外部修改合成檔再同意舊候選。

- A01：bytes 與本機紀錄證明寫入／拒絕／Conflict。結果：未執行（未另次服務授權）；證據 ID：無。
- A02：已寫結果與後續模型結果分列，無重複套用。結果：未執行（未另次服務授權）；證據 ID：無。
- A03：取消或模型後續失敗未觸發不得算該分支通過。結果：未執行（未另次服務授權）；證據 ID：無。

本機工具事實／模型觀察／未觸發分支／用量：未執行。

### S04 Plan／build／test；[第 09 份](../../specs/09-plan-mode-and-task-list.md) PLAN-AC-035、[第 10 份](../../specs/10-controlled-build-and-test.md) BUILD-AC-037

操作順序／gate：Plan 探索、submit／修訂、新確認、下一訊息 Execute，真讀寫／任務更新；可信已還原專案的 v2 Validation 逐次核准 build，另測無計畫及失敗停止。

- A01：任務只依合格本機事實完成，失敗後需下一明確指示及新核准。結果：未執行（未另次服務授權）；證據 ID：無。
- A02：test 部分等第 24 份及 BUILD-AC-016～018。結果：未執行（未另次服務授權）；證據 ID：無。
- A03：目前拒絕為獨立負向證據，BUILD-AC-037 整項仍未完成。結果：未執行（未另次服務授權）；證據 ID：無。

本機工具事實／模型觀察／未觸發分支／用量：未執行。

### S05 摘要／額度；[第 11 份](../../specs/11-context-and-budget-management.md) CTX-AC-035

操作順序／gate：手動摘要與五回合／容量觸發，保留原文 pin 與最新任務；觀察生成用途、用量、耗盡及取消。

- A01：原文、摘要與本機事實來源配對，生成不退款或重複計帳，耗盡停止。結果：未執行（未另次服務授權）；證據 ID：無。
- A02：逐次核准仍獨立。結果：未執行（未另次服務授權）；證據 ID：無。

本機工具事實／模型觀察／未觸發分支／用量：未執行。

### S06 規則／技能；[第 12 份](../../specs/12-project-rules-loading.md) RULE-AC-034、[第 17 份](../../specs/17-reusable-workflows-and-skills.md) SKILL-AC-031

操作順序／gate：使用合成根／子規則及技能，明確選用後要求一般任務與 Review；在等待點修改來源，重載／停用後給新指示。

- A01：來源失效、確認撤銷及新核准由本機事實證明。結果：未執行（未另次服務授權）；證據 ID：無。
- A02：模型遵循只記觀察，不提升成安全保證。結果：未執行（未另次服務授權）；證據 ID：無。

本機工具事實／模型觀察／未觸發分支／用量：未執行。

### S07 Review；[第 13 份](../../specs/13-git-diff-and-code-review.md) GIT-AC-022

操作順序／gate：合成 Git 快照，驗真 Gemini 工具配對、來源／行號及報告；嘗試來源變動、覆蓋不足／格式與額度分支。

- A01：報告以固定快照核對。結果：未執行（未另次服務授權）；證據 ID：無。
- A02：未觸發的錯誤 JSON／不足覆蓋維持未觸發，不操改服務回應冒稱真服務。結果：未執行（未另次服務授權）；證據 ID：無。

本機工具事實／模型觀察／未觸發分支／用量：未執行。

### S08 多行／進度；[第 14 份](../../specs/14-terminal-interaction.md) TERM-AC-031

操作順序／gate：多行需求預覽後送出；草稿／歷史回取不送；真讀寫、build 或 Review 觀察進度與取消。

- A01：一次送出為一則訊息，進度對應事實，核准仍各自要求，取消收尾後可繼續。結果：未執行（未另次服務授權）；證據 ID：無。
- A02：IME 正確性另由真人層證明。結果：未執行（未另次服務授權）；證據 ID：無。

本機工具事實／模型觀察／未觸發分支／用量：未執行。

### S09 保存／續接；[第 15 份](../../specs/15-work-save-and-resume.md) SAVE-AC-031

操作順序／gate：保存合成工作摘要後退出，修改測試來源／規則，再重啟、新 Key、新來源確認、新計畫確認與新指示。

- A01：無工具自動重播、原生歷史續傳或舊認證／核准恢復。結果：未執行（未另次服務授權）；證據 ID：無。
- A02：歷史與目前檔案差異可辨識。結果：未執行（未另次服務授權）；證據 ID：無。

本機工具事實／模型觀察／未觸發分支／用量：未執行。

### S10 checkpoint；[第 16 份](../../specs/16-change-checkpoints-and-recovery.md) CKPT-AC-032

操作順序／gate：真模型修改後，本機單檔復原，再給新指示；核對重讀來源與復原後資格。

- A01：新回合依目前來源，不自動續跑。結果：未執行（未另次服務授權）；證據 ID：無。
- A02：本機復原事實保留，模型後續失敗若未觸發另記。結果：未執行（未另次服務授權）；證據 ID：無。

本機工具事實／模型觀察／未觸發分支／用量：未執行。

### S11 三 profile 整合；[第 20 份](../../specs/20-multi-model-and-provider-support.md) MODEL-AC-036

操作順序／gate：每 profile 分開測文字、同 owner 工具往返、摘要、Review、save；由產品明確選模流程測 Gemini 間與雙供應商切換，再重新連線 Learn。

- A01：各組個別記錄，切換不重播、用量歸屬正確。結果：未執行（未另次服務授權）；證據 ID：無。
- A02：Learn 每次另有新確認。結果：未執行（未另次服務授權）；證據 ID：無。
- A03：一組成功不抵其他組，無原生資料／認證跨 owner。結果：未執行（未另次服務授權）；證據 ID：無。

本機工具事實／模型觀察／未觸發分支／用量：未執行。

### S12 Learn；[第 18 份](../../specs/18-mcp-external-tools.md) MCP-AC-031、032、[第 19 份](../../specs/19-web-search-and-document-fetch.md) WEB-AC-028、029

操作順序／gate：已授權後明確 connect；合成公開查詢逐次核准兩搜尋及小型文章 fetch。真 Gemini 另驗搜尋→fetch→引用、指定版本／衝突、來源不足、合成惡意文件指令與額度耗盡。

- A01：固定版本、Schema、傳輸目的地及安全結果相容。結果：未執行（未另次服務授權）；證據 ID：無。
- A02：拒絕／失效不傳送。結果：未執行（未另次服務授權）；證據 ID：無。
- A03：回答語意人工核對。結果：未執行（未另次服務授權）；證據 ID：無。
- A04：未遇到的分支未觸發，不降版、切來源或把外部文字當本機權限。結果：未執行（未另次服務授權）；證據 ID：無。

本機工具事實／模型觀察／未觸發分支／用量：未執行。

### S13 委派；[第 21 份](../../specs/21-multi-agent-delegation.md) MULTI-AC-038

操作順序／gate：各 profile 分別明確啟用，給可分解合成任務；實際取得兩子及父彙整，觀察唯讀工具、取消及共用額度。

- A01：必須有實際並行／工具／彙整證據。結果：未執行（未另次服務授權）；證據 ID：無。
- A02：只看到兩筆進度不足以證明 API 重疊。結果：未執行（未另次服務授權）；證據 ID：無。
- A03：使用安全開始／完成順序與產品可得 metadata 記錄，不記 body／認證。結果：未執行（未另次服務授權）；證據 ID：無。
- A04：若無法證實並行則該斷言未驗證。結果：未執行（未另次服務授權）；證據 ID：無。
- A05：子不使用 Learn，quota／授權不足如實停止。結果：未執行（未另次服務授權）；證據 ID：無。

本機工具事實／模型觀察／未觸發分支／用量：未執行。

## 平台、MTP 與分發

CLI、受控 build、受控 test、唯讀 Git 各有獨立能力證據。26.7.1 僅保留既有准入，未有當次實機結果；其他 OS／架構未實測。平台負向沿既有替身回歸，不偽造平台取得正向。

第 24 份仍擁有 MTP 正向資格，test 保持 capability_unavailable。BUILD-AC-016～018、036／037 的 test 子項及相關真人／服務分支受阻，負向拒絕不能抵正向。

人工分發逐項待填：實際 index 與 staged scan；未暫存／未追蹤內容及文件；擬分享 PR／Issue／附件；完整相依與許可／精確平台聲明；最終產物身分／scan／help／必要功能；另次授權封裝後的容器與完整內容比對。無對外發布，不建立 hook、不提交、push、簽章或上傳。

## 官方直接相依候選的完整發布結果

開發準備從官方 NuGet 精確 URL 下載 14 份公開套件至獨立 feed（來源與 SHA-256 見保留的 `official-feed-metadata.json`），沒有修改使用者套件快取。候選各在獨立 packages／來源完成本地 feed restore，之後改空來源再次離線 restore，再依序發布。

| 候選 | 來源／輸出 | 實際完整結果 | 身分 |
|---|---|---|---|
| C01：Google.GenAI 1.27.0 | `/private/tmp/aiagent-rva-5mvWs0/candidate-builds/genai-1.27.0/clean publish with spaces` | publish 0；八檔／mode／ProcessHost SHA-256 通過；scan 1／blocked，25 檔、1 項 BUD-S003；help 未執行 | `8c5fb2307a51b6823c846517c805b366515f969685643c4c8bdefe200e9d7c10` |
| C02：Google.GenAI 1.27.0 | `/private/tmp/aiagent-rva-5mvWs0/candidate-builds/genai-1.27.0/incremental-publish` | publish 0；八檔／mode／ProcessHost SHA-256 通過；scan 1／blocked，25 檔、1 項 BUD-S003；help 未執行 | `8c5fb2307a51b6823c846517c805b366515f969685643c4c8bdefe200e9d7c10` |
| C03：Google.GenAI 1.28.0 | `/private/tmp/aiagent-rva-5mvWs0/candidate-builds/genai-1.28.0/clean publish with spaces` | publish 0；八檔／mode／ProcessHost SHA-256 通過；scan 1／blocked，25 檔、1 項 BUD-S003；help 未執行 | `a7dc6c3e42272aa2699875ead3a6e55baaf6a05d8e53752063321e4fbbe56b9d` |
| C04：Google.GenAI 1.28.0 | `/private/tmp/aiagent-rva-5mvWs0/candidate-builds/genai-1.28.0/incremental-publish` | publish 0；八檔／mode／ProcessHost SHA-256 通過；scan 1／blocked，25 檔、1 項 BUD-S003；help 未執行 | `a7dc6c3e42272aa2699875ead3a6e55baaf6a05d8e53752063321e4fbbe56b9d` |

每份產物的根 device／inode、完整 manifest、精確 assets 解析（Auth 1.69.0）與狀態保留於其候選根的 `release-results.json`。四份均受阻；候選正式 runtime 功能與真人／服務未接續執行。GenAI 2.0.0 僅完成來源與組件查核，沒有完整發布資格，沒有因版本較新採用。

## 最終自動驗證接續（2026-11-07，Asia/Taipei）

來源為基準 e3c4692 加上本輪未提交變更；未改 index／HEAD。以下與前次失敗分列，沒有以重跑抹除原因。

| ID | 實際命令／入口 | 結果與範圍 |
|---|---|---|
| B02 | `dotnet restore tests/AIAgent.Cli.Tests/AIAgent.Cli.Tests.csproj --source /private/tmp/aiagent-rva-5mvWs0/empty-feed -p:NuGetAudit=false`；`dotnet build tests/AIAgent.Cli.Tests/AIAgent.Cli.Tests.csproj --no-restore` | 各退出 0；build 0 warning／0 error；無新套件或 SDK 變更 |
| M01 | `dotnet test --project tests/AIAgent.Cli.Tests/AIAgent.Cli.Tests.csproj --no-build`，外層沙箱之外、保留產品 sandbox | 1372 通過／0 失敗／0 略過，退出 0；7m16.971s。之後新增 N12 產品入口觀察，最終重驗另列 |
| HLP01 | `dotnet run --project src/AIAgent.Cli/AIAgent.Cli.csproj --no-build -- --help` | 退出 0；Debug help，不抵任何受阻 Release 的 help |
| P01 | `python3 -B -m unittest discover -s tests -p 'test_*.py'` | 76 通過、退出 0、41.943s；真 SDK 發布掃描另列 R02 |
| T01 | `python3 -B tests/terminal_smoke.py` | 退出 0；145 個 PASS；無真人／IME 或真實服務證據 |
| T02 | `python3 -B tests/terminal_smoke.py --group cancellation --repeat 30` | 退出 0；30 輪 × CtrlC／SIGINT／EOF，共 90 個 PASS |
| F01 | `dotnet format src/AIAgent.Cli/AIAgent.Cli.csproj --no-restore --verify-no-changes` | 退出 2；僅三份未修改檔案既有 WHITESPACE：GenerationGateway.cs、McpCoordinator.cs、DelegationSourceLedger.cs；未改這些檔案 |
| F02-first | 測試專案限定本輪七份 C# 檔的 `dotnet format ... --verify-no-changes --include ...` | 退出 2；RvaNativeFixture.cs IMPORTS 排序，已限定修正；後續確認另列，不寫成全專案通過 |
| IDX01 | `python3 -B scripts/check_sensitive_content.py staged` | 退出 0／passed，觀察實際 index 的 409 檔、0 finding；不是僅掃 staged diff，也不涵蓋本輪未暫存／未追蹤內容 |

### 獨立審查與修正證據

獨立唯讀審查提出四項 Important，沒有 Critical 產品問題；本輪已逐項修正：

- Guard 的原生觀測失敗採 present／gone／unknown；unknown 保留不確定，仍清理其他可驗證身分，不回報 guard-clean。
- C 與 C# PID 登記以隱藏 temporary 完整寫入後原子發布；錯誤登記不跳過其他身分；只有 0＋guard-clean 或 1＋guard-reaped 是合格終態。
- N02 新增唯讀 internal 時間觀察，分別核對啟動權、實際傳入剩餘期限、Started、helper ready／同一根身分存活、cleanup 與 finished，沒有縮短或重設產品期限。
- N07 區分缺少回執與實際 false 回執；非崩潰案例必須有真正 TERM、獨立原生終態且 guard 不需代清。control-invalid 是非法控制 byte；control-eof 是父控制端 EOF，不能宣稱驗過任意父程序／OS 崩潰。

Guard 的三項新增故障回歸先取得 2 failure／1 error，修正後 3／3 通過。觀測失敗只在研究 guard 以測試 dylib 注入 EPERM，正常獨立 guard 仍負責其自有程序；不把這項故障注入當產品原生正向。真 Host／文字說明定向 5／5 通過；真 ChatApplication＋WorkspaceFiles＋受控服務的變群組／超限案例 2／2 通過，應用退出 1，且 queued clear／key／model 的讀取會直接讓測試失敗。模型端僅使用離線合成 client。

### FS01：實際檔案系統

保留合成樹 `/private/tmp/aia-rva-fs-d6mj4hz4`；原生 statfs 查得 `apfs`（退出 0）。大小寫別名與 NFC／NFD 名稱各指向同一 inode；非法 UTF-8 名稱回報 EILSEQ。diskutil 查詢退出 1，沒有拿它當成功。這只證明本機卷的語意，不推定其他卷或平台。

### N02：實際時間與身分證據

初次完整回歸記錄 `/private/tmp/aia-fixture-73e35f997e59409b8cd9ed21d5427b6d-evidence.json`：Started／TimedOut、cleanup=true，產品預算 elapsedMs=120060；傳入的剩餘期限 119984 ms。五個 monotonic 階段、helper ready 與最後同一根身分存活的時間戳完整保留於該安全 JSON；測試用 Stopwatch.GetElapsedTime 在當次平台換算並分別驗證執行期限及收尾上限，沒有拿總耗時替代階段證據。最終重驗後的記錄另接續。


### 原生入口與剩餘界線

- N01／N02／N09／N10／N11 使用真正 ControlledDotnetTasks.RunAsync、正式 probe／sandbox／ProcessHost 與固定 recipe；runtime bytes 交換另以隔離實體 runtime 副本的 admission 前後快照查核，不改安裝或正式 runtime。
- N03～N06 的精確「已納入監督」gate 在 Supervisor 直接入口使用真正 Native 觀測及唯讀 callback；N04 另有產品真實管線；N05／N06 另以真 ChatApplication 取得不確定結果、退出 1 與零下一輸入的整合證據，不以合成快照代替真實身分。
- N07／N08 的真 Host 故障以 Started、獨立原生後代觀測及 TERM／終態核對；已收到 false 回執與缺少最終回執分列。父控制端 EOF 不是完整父程序／OS 崩潰驗收。只有刻意 wrapper crash 可讓獨立 guard 代清。
- N12 同時保留直接監督及產品入口的實際結果、guard 角色與未觀測逃離限制；任何 CleanupConfirmed 只涵蓋其契約可觀測集合，不宣稱完整惡意程序樹清除。
- 以上皆是 Debug 來源／runtime 的指定組合；受阻 Release 未啟動原生、真人或服務，也沒有取得其他 OS／架構或真實 MTP 資格。

### 尚未完成的層級

H01～H15、S01～S13 全部未執行；三個精確 profile 與 Learn 各次授權尚未取得。MTP 仍由第 24 份承接。Release 仍 blocked，因此同產物 help／Release 原生／真人／服務未執行；最終包裝、簽章與人工分發未執行，也未授權任何對外發布。

### R02：正式相依的真 SDK 乾淨／增量發布

| 輸出 | 實際結果 | 完整清單身分 |
|---|---|---|
| `/private/tmp/aiagent-pub-pjbn8tdk/first publish with spaces` | publish 0、八檔／mode／四件套雜湊通過；scan 1／blocked，25 檔、1 finding，BUD-S003；help 未執行 | `cb2300f8da90614b86a868f584d9295dc868c6039fde0af98d9a01032706a5b2` |
| `/private/tmp/aiagent-pub-pjbn8tdk/second-publish` | publish 0、八檔／mode／四件套雜湊通過；scan 1／blocked，25 檔、1 finding，BUD-S003；help 未執行 | `cb2300f8da90614b86a868f584d9295dc868c6039fde0af98d9a01032706a5b2` |

以上為 P01 當次結果；之後 SDK 測試環境明確停用 workload 背景更新，P02 的新產物另列。正式相依未改，沒有解除 BUD-S003。

### D12：快速脫離的產品觀察

新增同一案例的產品入口觀察後，定向 1／1 通過，退出 0；7.554s。它與直接 Supervisor 的已知集合結果分列，另有 guard 收尾，不宣稱完整程序樹受支援。

### SRC01：未暫存／未追蹤內容

使用既有 path_rules／ContentMatcher 在記憶體觀察本輪 18 份變更內容（含未追蹤檔），0 finding；沒有新增 scanner 模式或豁免。此項與 index 的 409 檔掃描分開，不抵 PR／附件／最終包裝人工檢查。所有變更檔逐行空白檢查通過；六份 Markdown 的本機檔案連結均存在。

### E02：離線 SDK 子程序環境

新增回歸先確認父環境設定 workload 更新／telemetry 為 0 時，fixture 原先未明確覆蓋 workload 更新設定。修正後子程序明確設定 DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE=1、telemetry opt-out=1；定向發布／身分／環境回歸 19／19 通過。這是測試環境補強，沒有更動產品設定、SDK 或套件；最終完整 Python 與新發布結果另列 P02。

### 最終接續結果

| ID | 命令／入口 | 最終結果 |
|---|---|---|
| M02 | 完整 `dotnet test --project tests/AIAgent.Cli.Tests/AIAgent.Cli.Tests.csproj --no-build`，產品 sandbox 保持啟用 | 退出 0；1372 通過、0 失敗、0 略過；7m21.347s。包含 N12 產品觀察與最後所有審查補強 |
| P02 | `python3 -B -m unittest discover -s tests -p 'test_*.py'`，明確停用 SDK 背景 workload 更新 | 退出 0；77 通過；44.240s。真實掃描仍 blocked，沒有算成 Release 通過 |
| F02 | `dotnet format tests/AIAgent.Cli.Tests/AIAgent.Cli.Tests.csproj --no-restore --verify-no-changes --include` 本輪七份 C# 檔 | 退出 0；只證明本輪範圍，F01 的三份既有差異保持 |

F02 的完整 include 清單：`src/AIAgent.Cli/Processes/ControlledDotnetTasks.cs`、`src/AIAgent.Cli/Processes/DotnetRecipe.cs`、`src/AIAgent.Cli/Processes/ProcessObservation.cs`、`src/AIAgent.ProcessHost/Supervisor.cs`、`tests/AIAgent.Cli.Tests/RvaNativeFixture.cs`、`tests/AIAgent.Cli.Tests/RvaNativeTests.cs`、`tests/AIAgent.Cli.Tests/RvaContractTests.cs`。

### R03：最終離線環境的正式相依發布

| 輸出 | 實際結果 | 完整清單身分 |
|---|---|---|
| `/private/tmp/aiagent-pub-g6t26h3t/first publish with spaces` | publish 0；八檔／mode／四件套通過；scan 1／blocked，25 檔、1 項 BUD-S003；help 未執行 | `aa590b6c76aeb575051880869032142bc2d46f40dbf61f721815ebb2ac285f1d` |
| `/private/tmp/aiagent-pub-g6t26h3t/second-publish` | publish 0；八檔／mode／四件套通過；scan 1／blocked，25 檔、1 項 BUD-S003；help 未執行 | `aa590b6c76aeb575051880869032142bc2d46f40dbf61f721815ebb2ac285f1d` |

M02 的 N02 安全紀錄為 `/private/tmp/aia-fixture-ce553a9c75fe490786e26b8e29085c04-evidence.json`：Started／TimedOut、cleanup=true、elapsedMs=120038；五個階段及同一目標身分的存活門檻均由實際回歸核對。

最終結論：指定 macOS arm64 組合的離線／Debug 原生／PTY 補強已驗證；正式相依及已查直接候選的 Release 完整掃描均仍受阻。同產物 help、Release 原生、真人、真實模型／Learn、MTP 正向與最終分發資格未完成，REVIEW-04 保持未結。沒有提交、推送、封裝、簽章、上傳或分發，也沒有清理保留的 fixture／產物。

### N11：真實 probe 拒絕分支

以產品核准預覽中的自有私人根（驗證固定前綴及 33 字元長度）建立 `tmp/allowed` 目錄，讓真正 SandboxProbe 的允許檔寫入失敗。定向 1／1 通過、退出 0，3.715s：wrapper 已啟動、回報 capability_unavailable／NotStarted／NotVerified，helper ready 不存在、來源不變；沒有回退或目標執行。未修改產品隔離政策或協定。新增此必要負向案例後完整測試接續為 M03，不覆寫 M02。

### M03：最終交付完整回歸

加入 N11 真實 probe 拒絕案例並完成建置後，執行 `dotnet test --project tests/AIAgent.Cli.Tests/AIAgent.Cli.Tests.csproj --no-build`：退出 0，1373 通過／0 失敗／0 略過，7m29.431s。N11 負向案例與其共用 fixture 均納入本次完整回歸。最後限定七份 C# 的格式 verify 退出 0；F01 三份未修改檔案的既有差異保持。P02 的 77 項 Python、T01 的 145 項 PTY 與 T02 的 90 項取消驗證保持，產品與 Python 來源在這些驗證之後沒有功能變更。

本地失敗命令只保留案例次序、錯誤代號、例外類型與框架數量的受控摘要；不保留原始 tracebacks／診斷正文。fixture、來源副本、完整發布目錄及安全 metadata 保留，沒有執行批次清理。
