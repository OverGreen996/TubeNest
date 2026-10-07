# 商店發布流程

2026-10-08 查核。本專案目前以 GitHub 測試版提供，尚未提交或通過任何擴充商店審核。

## Microsoft Edge Add-ons

1. 用 Microsoft 帳號在 Partner Center 註冊 Edge 擴充開發者帳號，填寫開發者資料；此計畫沒有註冊費。[官方註冊說明](https://learn.microsoft.com/en-us/microsoft-edge/extensions/publish/create-dev-account)
2. 在開發者後台建立新擴充，上传只含 `extension` 內容、根目錄有 `manifest.json` 的 ZIP。Windows 完整安裝包包含本機程式，不能作為商店擴充包。[官方發布流程](https://learn.microsoft.com/en-us/microsoft-edge/extensions/publish/publish-extension)
3. 填寫用途、權限理由、支援連結、隱私政策、截圖及審核者測試步驟，明確說明下載需要另行安裝本機助手。
4. 取得商店核發的擴充 ID 後，更新 `Install.ps1` 的 `allowed_origins` 及 `TubeNestHost.cs` 的來源檢查，重新測試商店版 → 助手 → 下載流程，完成後才送審。商店版 ID 不應假定等於本機測試 ID。

Edge 要求擴充功能可用、穩定且遵守智慧財產權及其他內容政策；試驗或不穩定的內部測試版本不適合直接公開上架。是否核准由商店決定。[Edge 開發者政策](https://learn.microsoft.com/en-us/legal/microsoft-edge/extensions/developer-policies)

## Chrome Web Store

Chrome 需要註冊開發者帳號並支付一次性註冊費。[官方帳號註冊](https://developer.chrome.com/docs/webstore/register/)

Google 官方將協助下載 YouTube 影片列為禁止產品的常見拒絕／下架原因。TubeNest 的核心目的就是 YouTube 下載，因此目前版本不適合直接送 Chrome 商店，不能靠改名、隱藏功能或將下載移到本機程式來承諾通過審核。[Chrome 官方政策排解](https://developer.chrome.com/docs/webstore/troubleshooting/#prohibited-products)

## 上架後的安裝

擴充可由使用者在商店按「取得／新增」，不再需要開發人員模式或手動選 extension 資料夾。本機助手仍須安裝一次；商店不會替擴充安裝或管理 Windows 本機程式。[Microsoft Native Messaging 說明](https://learn.microsoft.com/en-us/microsoft-edge/extensions/developer-guide/native-messaging)
