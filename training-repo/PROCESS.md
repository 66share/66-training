# PROCESS.md — 練習 3:MCP 對照實驗

## 問題

> 哪些商品庫存低於 5?

分別在「沒有 orderhub MCP 工具」與「有 orderhub MCP 工具」兩種狀態下,
回答同一個問題,記錄實際會怎麼走。

## Before — 沒有 MCP 工具

沒有 `low_stock` 工具可用,回答這個問題實際走了以下步驟:

1. **找業務規則**:grep `src/OrderHub.Core/Domain/Product.cs`,確認「低庫存」
   要看的欄位是 `StockQuantity`,而且要排除下架商品(`IsActive`)——這條規則
   本來就封裝在 `IProductRepository.GetActiveAsync()` / `GetLowStockAsync()`
   裡,但工具不存在時,得自己重新讀一次程式碼才知道規則長怎樣。
2. **找連線資訊**:讀 `src/OrderHub.Web/appsettings.json` 拿連線字串
   (`Server=JPOSDEV160\SQLSERVER2022;Database=OrderHubTraining`)。
3. **手刻一條 SQL**,自己重新組出跟 repository 一樣的邏輯:
   ```sql
   SELECT Sku, Name, StockQuantity
   FROM Products
   WHERE IsActive = 1 AND StockQuantity < 5
   ORDER BY StockQuantity
   ```
4. 用 `sqlcmd -S "JPOSDEV160\SQLSERVER2022" -E -d OrderHubTraining -Q "..."`
   執行,人工讀輸出(還遇到 console 編碼亂碼,商品名稱要對照原始資料才看得懂)。

**共 4 個步驟、3 次工具呼叫(grep + read + sqlcmd)**,而且第 3 步是在
**重新實作一次**已經存在於 `OrderHub.Core` 裡的業務邏輯——如果之後
「低庫存」的定義改了(例如加上「近 30 天有銷量」的條件,像
`GetLowStockAsync` 那樣),這條手刻 SQL 不會自動跟著改,兩邊會出現
兩種答案。

結果(6 筆,人工從 sqlcmd 輸出解析):

| SKU | 商品 | 庫存 |
|---|---|---|
| SKU-1004 | 極光 USB-C 集線器 | 0 |
| SKU-1005 | 極光 筆電支架 | 0 |
| SKU-1048 | 晨光 行動電源 | 2 |
| SKU-1023 | 雲峰 27吋螢幕 | 3 |
| SKU-1014 | 星河 USB-C 集線器 | 4 |
| SKU-1032 | 曜石 機械鍵盤 | 4 |

## After — 有 orderhub MCP 工具

同一個問題,呼叫已註冊的 `low_stock` 工具:

```json
{"jsonrpc":"2.0","id":2,"method":"tools/call","params":{"name":"low_stock","arguments":{"threshold":5}}}
```

**1 個步驟、1 次工具呼叫**,伺服器端 log 顯示整個 request handler
(含 EF Core 查詢)耗時 **1953ms**。回傳:

```json
[
  { "Sku": "SKU-1004", "Name": "極光 USB-C 集線器", "StockQuantity": 0 },
  { "Sku": "SKU-1005", "Name": "極光 筆電支架", "StockQuantity": 0 },
  { "Sku": "SKU-1048", "Name": "晨光 行動電源", "StockQuantity": 2 },
  { "Sku": "SKU-1023", "Name": "雲峰 27吋螢幕", "StockQuantity": 3 },
  { "Sku": "SKU-1014", "Name": "星河 USB-C 集線器", "StockQuantity": 4 },
  { "Sku": "SKU-1032", "Name": "曜石 機械鍵盤", "StockQuantity": 4 }
]
```

結果集合與 Before 完全一致,但商品名稱是結構化 JSON,不用人工從
console 輸出解析亂碼。

> 附註:這次「一次工具呼叫」是直接對已編譯好的 `OrderHub.Mcp` 送
> stdio JSON-RPC 請求測出來的(跟練習 2 的 Inspector 測試走同一支
> server、同一組程式碼)。若要在 Claude Code / Codex 這個 CLI 本身
> 的對話裡重現,需要**重啟 CLI**讓它讀到 `.mcp.json`/`config.toml`
> 裡新加的 `orderhub` server(MCP server 清單是啟動時載入,不會
> 中途熱重載)——這步驟留給下次開新 session 時用 `/mcp`(Claude Code)
> 或 Codex 重啟後親自核對,驗證清單見下方。

## 對照小結

| | Before(無工具) | After(有工具) |
|---|---|---|
| 步驟數 | 4(grep 規則 → 讀 config → 手刻 SQL → sqlcmd 執行解析) | 1(呼叫 `low_stock`) |
| 是否重新實作業務邏輯 | 是,自己組 `WHERE IsActive AND StockQuantity < N` | 否,直接重用 `OrderService`/`IProductRepository` 既有邏輯 |
| 輸出格式 | sqlcmd 純文字(console 編碼亂碼,需人工比對) | 結構化 JSON |
| 規則變動時的風險 | 手刻 SQL 不會自動同步業務規則變更 | 規則只在 `OrderHub.Core` 改一次,工具自動跟著對 |

## 驗證清單狀態

- [ ] Claude Code `/mcp` 看到 `orderhub` + 三個工具(Codex 同理)
      —— **需要重啟 CLI 讀取新寫入的 `.mcp.json` 才能核對,尚未由使用者側確認**
- [x] 對照實驗完成且記錄(見上)
- [x] `.mcp.json` 進 git,獨立 commit

---

# 練習 4 — cancel_order:會改資料的工具

## Annotations 驗證(MCP Inspector)

在 Inspector 的 Tools 列表點開個別工具,標籤如下:

| 工具 | 標註 | Inspector 顯示 |
|---|---|---|
| `get_order` | `ReadOnly = true` | `read-only` |
| `low_stock` | `ReadOnly = true` | `read-only` |
| `customer_orders` | `ReadOnly = true` | `read-only` |
| `cancel_order` | `Destructive = true, Idempotent = false` | `destructive` |

description 逐字相符,`cancel_order` 的 `id` 參數標記為必填(`id *`)。

## 功能驗證

1. **取消一筆待處理訂單(#208,SKU-1001 × 2,取消前庫存 11)**
   - 呼叫 `cancel_order(id=208)` → `訂單 208 已取消,庫存已回補`
   - 回 `/Products` 頁面核對:SKU-1001 現有庫存從 11 變成 **13**,回補正確
     (對應活動 1 客訴 3 修的那條 `CancelOrderAsync` 回補邏輯)

2. **負面案例 —— 清楚拒絕,不是 exception dump**
   - 對同一筆(已取消)訂單 208 再取消一次 →
     `取消失敗:狀態為 Cancelled 的訂單不可取消`
   - 對一筆已出貨訂單(#194)取消 →
     `取消失敗:狀態為 Shipped 的訂單不可取消`
   - 兩次呼叫在 server log 裡都是 `IsError = False`(工具本身執行成功,
     只是回傳「業務規則不允許」的訊息),完全沒有 stack trace——這是
     `OrderService.CancelOrderAsync` 本來就用 `ServiceResult<T>.Fail(...)`
     表達預期內失敗、工具只轉接訊息的結果。

## 權限確認提示 —— 未能在本 session 驗證,需使用者親自操作

`Destructive` annotation 是給 **client** 的提示,決定是否在執行前跳出人工
確認,MCP Inspector 本身沒有這層(它是開發用的直接執行工具,按
Execute 就真的送出去了),所以「按允許之前資料不會被動到」這件事沒辦法
用 Inspector 驗證,只能在真正把 `orderhub` 接進 Claude Code / Codex
的對話裡看到。

而這個 session 的 `.mcp.json` 是稍早才寫入的,MCP server 清單是 CLI
**啟動時**載入、不會中途熱重載,所以我自己這個 session 也連不上
`orderhub`——這一項需要你重啟 CLI 後,親口對 agent 說「幫我取消訂單 X」,
觀察是否跳出權限確認、按允許前資料是否真的沒被動到。

## 驗證清單狀態

- [x] Inspector 中 annotations 如預期(`cancel_order` = destructive,
      三個唯讀工具 = read-only)
- [ ] 對 agent 說「幫我取消訂單 X」,觀察權限確認提示
      —— **需重啟 CLI 讓 `.mcp.json` 生效,待使用者親自驗證**
- [x] 取消一筆待處理訂單成功,`/Products` 庫存回補(11 → 13)
- [x] 重複取消 / 取消已出貨訂單,得到清楚拒絕訊息而非 exception dump
- [x] 獨立 commit;本節記錄對照過程

---

# 練習 5 — Resource 與 Prompt:MCP 不是只有 Tool

## 5a. Resource — `orderhub://discount-rules`

新增 `OrderHubResources.cs`,在 `Program.cs` 接上 `.WithResources<OrderHubResources>()`。
跟 Tool 的關鍵差異:**沒有參數、不查 DB**,單純是一段固定的背景知識
(會員折扣規則),由 client 自己決定何時要不要把它塞進 context——
不像 Tool 是「agent 主動觸發的動作」。

stdio 煙霧測試:

```
capabilities 多了 "resources": { "listChanged": true }
resources/list  → 1 筆:會員折扣規則 / orderhub://discount-rules / text/markdown
resources/read  → markdown 內容與程式碼裡的字串逐字相符
```

Inspector 的 Resources 頁籤也核對過:URIs (1) 底下看得到「會員折扣規則」,
點開後 markdown 正確渲染成標題+清單,不是原始跳脫字元。

## 5b. Prompt — `low_stock_report`

新增 `OrderHubPrompts.cs`,回傳 `ChatMessage`(來自 `Microsoft.Extensions.AI`,
這包是 `ModelContextProtocol` 的既有 transitive 依賴,不用額外加
`PackageReference` 就能編譯),`Program.cs` 接上 `.WithPrompts<OrderHubPrompts>()`。

跟 Tool / Resource 的差異:Prompt 是**預先寫好的一段話範本**,像
slash command 一樣一鍵取用,取代「採購同事每週手動打一次同樣的問題」;
產生的訊息裡會指示 agent 去呼叫 `low_stock` 等既有工具,而不是自己把
資料庫查詢邏輯再寫一次到 prompt 裡——分工上,Prompt 負責「起頭問對問題」,
真正查資料還是靠 Tool。

stdio 煙霧測試(`threshold=5`):

```
capabilities 多了 "prompts": { "listChanged": true }
prompts/list → low_stock_report,參數 threshold(optional,預設 10 寫在說明裡)
prompts/get(threshold=5) → role: user,內文正確帶入「threshold=5」
```

Inspector 的 Prompts 頁籤核對過:填 `threshold=5` 按 Get Prompt,
`[0] role: user` 訊息與帶入值都正確。

## 三種原語的分工小結

| | Tool | Resource | Prompt |
|---|---|---|---|
| 誰觸發 | agent 主動呼叫 | client 決定何時放進 context | 使用者/agent 一鍵取用範本 |
| 有沒有參數/動作 | 有,會執行查詢或修改 | 沒有參數,純資料 | 有參數,但只是套模板產生文字 |
| OrderHub 範例 | `get_order`/`low_stock`/`cancel_order` | `orderhub://discount-rules` | `low_stock_report` |
| 好比 | API 呼叫 | 唯讀的說明文件/知識庫 | slash command / 範本訊息 |

## 驗證清單狀態

- [x] Resource 在 Inspector 列出,內容正確渲染(markdown)
- [x] Prompt 在 Inspector 列出,帶參數取得訊息正確
- [x] stdio 煙霧測試(`resources/list`、`resources/read`、`prompts/list`、
      `prompts/get`)全部無誤,capabilities 正確宣告
- [x] 獨立 commit;本節記錄

---

# 5c. 體會三者的分工

## 實測情況

`@` 選 resource 問折扣問題、`/mcp__orderhub__low_stock_report` 一鍵展開再
自動呼叫 `low_stock`——這兩項是 Claude Code / Codex 這個 **CLI 本身**的
介面行為(resource picker、prompt→slash command),不是 Inspector 能測的
東西。而這個 session 的 `.mcp.json` 是練習 3 才寫入的,MCP server 清單
只在 CLI **啟動時**讀取,所以我這個 session 目前還是連不上 `orderhub`
(用 ToolSearch 找過,沒有任何 orderhub 工具/資源/提示可用)。這兩項
**需要你重啟 CLI 後親自驗證**——其餘(Inspector 讀 resource/prompt)
已經在 5a、5b 做過,結果同樣適用於這裡。

## 思考:折扣規則用 Resource,和讓 agent 自己讀 `OrderService.cs`,差在哪?

- **團隊共用一份答案**:Resource 是 server 端維護的單一文本,所有連上
  `orderhub` 的人(不管用 Claude Code 還是 Codex)問折扣問題時看到的
  都是同一份;讓 agent 自己去讀程式碼,則是每個人、每次都各自「重新
  發現」規則一遍,容易漏看細節——像 `CreateOrderAsync` 目前只在 Gold
  才套用折扣去 snapshot 單價,但 `CalculateTotal` 是全部 tier 都套用
  折扣率計算總額,這個不對稱如果沒讀完整段程式碼很容易漏掉,Resource
  可以把「這裡有個不對稱,兩處算法不同」這種眉角直接寫清楚。
- **省探索成本**:讀 Resource 是一次 O(1) 的查詢;讀程式碼要先找到
  `OrderService.cs`、認出 `GetDiscountRate`/`CalculateSubtotal`/
  `CalculateTotal` 三個方法怎麼互相呼叫,才能拼出完整規則,對 agent
  來說是多好幾步的探索,對人來說也是多讀一次程式碼的成本。
- **但 Resource 本身也會過期**:目前 `DiscountRules()` 是寫死的
  markdown 字串,如果哪天 `OrderService.GetDiscountRate` 改了折扣
  (例如 Silver 從 95 折改 92 折),沒人記得同步更新這段 resource,
  agent 讀到的就是舊規則——而且因為看起來像「官方文件」,答錯的時候
  還更有底氣。跟練習 1「金額別自己算,重用 `OrderService`」是同一堂課:
  真的要避免兩份真相,`DiscountRules()` 應該直接呼叫
  `orderService.GetDiscountRate(tier)` 動態組出文字,而不是手寫死
  百分比。

## 思考:Prompt 範本放 server,和每個人自己打一段話,差在哪?

- **團隊共用最佳問法**:`low_stock_report` 這段話已經包含「用哪個
  工具查、再用其他工具查什麼、輸出要有哪些欄位」——這是問了很多次
  之後才會慢慢寫順的問法。放進 server,新加入的人第一次用就拿到
  同一份熟練問法,不用自己摸索「原來要先查庫存、再查訂單、最後才能
  給補貨建議」這個順序。
- **版本控制**:Prompt 範本是 `OrderHubPrompts.cs` 裡的程式碼,進 git、
  走 code review,改版有紀錄可查;每個人自己在對話框手打的問法則完全
  沒有版本痕跡,沒辦法追蹤「這句話是什麼時候、為什麼這樣問」。
- **改版只要改一個地方**:如果之後這份採購建議報告要多考慮「供應商
  前置期」,只要改 `LowStockReport()` 這一個方法,所有人下次執行
  `/mcp__orderhub__low_stock_report` 就自動套用新版;如果是每個人各自
  手打一段話,要嘛沒人想到要加這個條件,要嘛加了也只有那個人自己知道,
  團隊裡會同時流通好幾種問法、好幾種答案品質。

## 驗證清單狀態

- [ ] Claude Code `@` 選 `orderhub://discount-rules` 問「Gold 會員買
      1000 元商品應付多少?」,agent 用 resource 內容作答
      —— **需重啟 CLI,待使用者親自驗證**
- [ ] Claude Code `/mcp__orderhub__low_stock_report` 一鍵展開並自動
      呼叫 `low_stock` 完成報告 —— **需重啟 CLI,待使用者親自驗證**
- [x] Inspector 驗證 resource/prompt(見 5a、5b,同一支 server 結果適用)
- [x] 5c 第 3 點思考記進 PROCESS.md(見上);獨立 commit
