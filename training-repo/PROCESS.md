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
