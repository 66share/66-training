# PROCESS.md — 我的練習心得

> 一個原則：**寫「具體發生的事」，不寫感想文。**
> 貼上當時真實的 prompt、真實的數字、真實的錯誤訊息——三個月後的你（和你的同事）才用得上。

#### 使用的 agent 與模型：

---

## 通用四問

### 1. 我的任務拆解

（開工前你把任務拆成哪幾步？實際做的時候順序有變嗎？為什麼變？）

- **環境設定**原本規劃：`CLAUDE.md` → `settings.json`（權限）→ hooks → subagents。
- **實際順序有變**：先用 `/init` 產 `CLAUDE.md`，發現它只是起點、缺「危險檔案」與「不要做的事」兩個護欄區塊，於是回頭手動補齊六區塊；教材的通用範例還有幾處跟真實 code 對不上（分層箭頭、port、折扣），改以 `/init` 讀出來的內容為準。
- **修 bug 流程**：重現 → 從 Controller 往下追到 Service/Repository 定位 → 最小修改 → 補回歸測試 → 頁面實測 → commit。
- **為什麼變**：`/agents` 互動精靈在新版被移除，驗證 subagent 的方式從「`/agents` 看清單」改成「直接叫它做事 / 問 Claude 列出 subagent」。

### 2. AI 幫上大忙的地方

（哪件事 agent 做得又快又好？**貼上當時的提問原文**，說明為什麼這樣問有效。）

- **`/init` 讀真實程式碼、撈出文件看不到的坑。** 它產的 `CLAUDE.md` 直接標出一個定價不對稱：`CreateOrderAsync` 只在 Gold 等級套折扣快照單價，`CalculateTotal` 卻對所有等級套折扣。這是純看需求文件絕對發現不了的。
- **分頁 bug 的推理很快到位。** 提問原文：
  > `OrderRepository.GetPagedAsync` 的 `Skip(page * pageSize)` 為 `Skip((page - 1) * pageSize)` 嗎？
  有效的原因：我**帶著具體程式碼片段和自己的假設**去問，而不是丟「分頁怪怪的」。它能直接用「page=1 時算出 Skip(20) 會吃掉第一頁前 20 筆」對齊到症狀「第一頁看不到新訂單」。


### 3. AI 誤導我的地方，與我如何發現

（agent 說錯／改錯／過度自信的時刻。你靠什麼抓到——對照程式碼？頁面實測？跑測試？）

- **過度倚賴文件、漏掉真實型別。** 早期依 README 寫 `CLAUDE.md` 時，把 `ServiceResult<T>` 拿掉了（README 沒提），但 `/init` 讀 code 後證實它真實存在於 `Core/Common`。**怎麼發現**：用 `/init` 讀真實程式碼交叉比對，而不是只信文件。
- **[填入你自己遇到的]**：例如某次 agent 說「改完了」，我頁面重整卻沒變——後來發現是**舊的 `dotnet run` 站還在跑**，新版沒生效。怎麼發現：回終端機看 `dotnet build` 是不是真的 `Build succeeded`，`Ctrl+C` 停掉舊站重跑才對。
- **[填入]**：agent 提測試計畫時是否冒出超出規格的「順便重構 xxx」，我如何要求它砍掉？

### 4. 我會帶回日常工作的一招

（一個具體、可複製的做法，不要寫「要多驗證」這種口號——寫出**操作步驟**。）

- **「先計畫、後動手」的把關流程**，具體三步：
  1. 下任務時明確要求「**先不要改任何檔案，只給我計畫**：要動哪些檔、加哪些測試、每個測試對應規格哪一點」。
  2. 審計畫：正向確認規格每一點都有對應測試；反向掃有沒有「順便 / 一起 / 重構」等超出範圍的動作，有就叫它拿掉。
  3. 確認後才放行，改完用 `test-runner` 跑 `dotnet test` 驗全綠、`code-reviewer` 審 diff。
- 為什麼有效：把範圍蔓延和偷改行為擋在「還沒寫 code 之前」，diff 小、可 review。

## 自我驗證（做到哪個階段答哪題）

### 第一階段 — Agentic Coding

練習 1

1. 我能不看筆記說出三個專案（Web/Core/Infrastructure）各自的職責
2. 我核對過 agent 描述的建單流程，且**至少找出一處不精確或過度簡化的說法**
3. 我知道商業邏輯應該放在哪一層、新增頁面要動哪些地方

練習 2

1. 三個 bug 我都先在頁面上重現過，才開始找程式
2. 我給 agent 的資訊包含具體觀察（頁碼／金額數字／庫存數字），而不是只貼客訴原文
3. 每個修復都回到頁面驗證過症狀消失
4. 每個 bug 都補了一個回歸測試，`dotnet test` 全綠
5. 三個獨立 commit，message 說明症狀與根因
6. （思考題）為什麼原本的測試沒抓到這三個 bug？

練習 3

1. `/Products/LowStock` 不帶參數 → 門檻 10 的結果；帶 `?threshold=3` → 結果隨之改變
2. `?threshold=0`、`?threshold=-1` → 頁面顯示驗證錯誤，不是 500
3. 售出數量欄位排除了 Cancelled 訂單（可用一筆已取消的訂單驗證）
4. 停售（已停售 badge）商品不出現在列表
5. 程式分層與命名跟既有的 Products 功能一致（請 agent 自我 review 一次，並自己確認）
6. 至少 3 個新測試，`dotnet test` 全綠

練習 4

1. 重構後 `dotnet test` 全綠
2. 我能說出這次重構「改善了什麼、沒有改變什麼」
3. 我有在 code review 的角度看過 diff（不是 agent 說好就好）

---

## 附錄：值得留下的對話片段

（貼 1–2 段最有代表性的 prompt 與回應**摘要**——不用貼全文，重點是「我怎麼問」和「它怎麼答」。）

**片段一 — `/fix-bug` 分頁 off-by-one（練習 2）**

- **我怎麼問：**
  `/fix-bug 訂單列表第一頁看不到新訂單`

- **它怎麼答（節錄流程，非逐字）：**
  1. 先複述症狀、要求我確認理解是否正確，才往下查
  2. 追到 `OrderRepository.GetPagedAsync` 的 `Skip(page * pageSize)`，指出 `page` 在全系統是 1-based，但這裡當成 0-based 用，導致 `page=1` 時 `Skip(20)` 把最新的 20 筆訂單整批跳過——正是「新訂單看不到」的根因
  3. 說明根因後停下來等我確認，才動手改成 `Skip((page - 1) * pageSize)`
  4. 自動派 `code-reviewer` 審查、補一個會在修復前失敗的回歸測試、跑 `dotnet test` 全綠
  5. 最後才用「症狀 → 根因 → 修法」格式產出 commit message


**片段二 — 新功能規劃時的架構判斷（低庫存商品頁，練習 3）**

- **我怎麼問：**
  > 採購同事希望有一頁能快速看到「快沒貨」的商品。請實作以下規格……先讀 `ProductsController`、`ProductService`/`IProductService`、`Views/Products/Index.cshtml`，沿用同一套慣例，不要自創寫法。

- **它怎麼答（節錄）：**
  先不寫程式，而是用兩個 Explore agent 平行讀完 Controller/Service/Repository/View/測試慣例，再交給 Plan agent 逼問幾個「沒有既有先例可循」的決策點，最後給出**帶具體理由**的定案，例如：
  > 近 30 天銷量統計放 Repository，不放 Service——「只有 repository 碰 `DbContext`」是硬規則，不是看是不是商業邏輯；`OrderRepository.GetPagedAsync` 本身就是「repository 內部跑兩次查詢、組成一個結果」的既有先例，這次照同樣形狀做。

  > Repository 查詢用明確 `join`，不靠 `OrderItem.Order` 這個 nullable 導覽屬性做條件式過濾——這是專案第一個 `GroupBy`，選最保守、在 EF Core InMemory provider 下翻譯行為最可預期的寫法。

- **代表性：** 遇到「規格沒講、慣例裡也沒先例」的地方（DTO 放哪、驗證用什麼機制、查詢會不會 N+1），它沒有隨便挑一個寫法，而是先攤開兩三個做法的取捨、再定案。

