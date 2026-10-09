# Chrome / Edge 相容性驗證

2026-10-09，Windows x64，TubeNest 1.0.2 正式擴充內容。

## 測試環境

| 項目 | Chrome | Edge |
|---|---|---|
| 版本 | Google Chrome for Testing 155.0.8059.39（官方 Stable 測試套件） | Microsoft Edge 154.0.4258.62 |
| 設定檔 | 獨立測試設定檔，未登入 | 獨立測試設定檔，未登入 |
| 擴充內容 | 同一份 TubeNest 1.0.2 | 同一份 TubeNest 1.0.2 |

本機沒有安裝一般版 Chrome；使用 Google 官方 Chrome for Testing ZIP 解壓執行，不新增一般瀏覽器安裝、不修改預設瀏覽器、不使用個人登入或瀏覽資料。下載來源由官方 JSON API 取得，核對 Google Storage 的檔案 MD5 完整性資訊，另外保存下載 ZIP 的 SHA-256。該 Windows 測試套件 chrome.exe 為未簽章檔案，未宣稱通過 Authenticode 簽章驗證。

官方來源：[Chrome for Testing 說明](https://developer.chrome.com/docs/automation-and-testing/download-test-binaries) · [版本與下載資訊](https://googlechromelabs.github.io/chrome-for-testing/last-known-good-versions-with-downloads.json)

## 結果

- 同一視窗尺寸、色彩主題及控制資料下，淺色與深色面板的 PNG 檔案均逐位元相同。品牌、標題、資訊、下載設定、畫質選單、下載按鈕、紀錄列與頁尾共 9 個元素的座標、尺寸、字型、文字色、底色及圓角沒有差異。
- 兩邊均通過 320／375／384px 排版、4.5:1 文字對比、載入、重試、保留選定畫質、取消另存、取消工作、顯示資料夾、清除紀錄、說明頁與長標題／長錯誤測試。正常畫質選擇面板在 600px 高度內。
- 真正公開 YouTube 一般影片 `yRRQ6filg-E` 及 Shorts `18NGQq7p3LY` 頁面，兩邊都由正式內容腳本自動加入按鈕，位於分享之後、沒有超出視窗，圖示與文字同色。一般影片按鈕為 88×36px，左右間距各 8px；Shorts 按鈕為直向圖示與標籤。
- Chrome 的真正 MV3 擴充通過：首頁 → SPA 切換影片 → 可信按鈕點擊 → 真正擴充面板 → 已安裝本機助手 → 真實 YouTube 三立影片標題與 1080p 等畫質。此串接測試的導航 HTML 為控制頁；助手解析連到真正 YouTube。真實網站按鈕掛載另依上一項驗證。
- 首次檢查發現測試可能在 Chrome 面板 DOM 尚未完成時就讀取元素，或在 Edge 深淺色過渡結束前讀取背景色；已修正測試等待條件，未修改正式擴充。

## 圖像比對

| 面板主題 | Chrome / Edge 相同 PNG 的 SHA-256 |
|---|---|
| 淺色 | `fdf6a4300e06f479218e73881700348b531cafba074f2c0899d194ecdaddb72e` |
| 深色 | `26ddae0767d2aba82742dd3bf2a3158a86dff5276e8dd519a372d73cf3aa9841` |

網站本身可能依瀏覽器設定、登入狀態及 YouTube 的版型實驗呈現不同配置。此次真實頁面 Chrome 為網站淺色、Edge 為網站深色，TubeNest 都正確沿用各自網站主題；不是聲稱整個 YouTube 頁面跨瀏覽器完全相同。

本次檢查介面、內容腳本、面板與助手串接，沒有重新下載全片，也沒有自動操作另存新檔對話框。一般版 Chrome 的初次人工安裝流程未另執行；下載引擎沿用既有完整影片驗證結果。

## 重跑

開發者先備妥官方 Chrome for Testing 的 Windows x64 解壓檔。預設位置為 `test-output/chrome-compat/chrome-win64/chrome.exe`，也可透過 `TUBENEST_TEST_CHROME` 指定自己的測試版路徑。

```powershell
$env:TUBENEST_TEST_BROWSER='chrome'
node tests/ui.mjs
node tests/ui-states.mjs
node tests/button-native.mjs
node tests/live-page-button.mjs

$env:TUBENEST_TEST_BROWSER='edge'
node tests/ui.mjs
node tests/ui-states.mjs
node tests/live-page-button.mjs

node tests/compare-ui.mjs
```

串接測試需要先安裝 TubeNest 本機助手與工具；網頁與畫質測試需要連線 YouTube。測試瀏覽器、完整設定檔與測試暫存輸出不隨原始碼或 Windows 安裝包發布。
