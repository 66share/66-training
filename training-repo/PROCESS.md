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
