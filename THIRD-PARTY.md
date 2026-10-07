# 第三方工具

TubeNest 本機助手使用 Windows 內建 .NET Framework，透過獨立程序執行以下官方工具。下載包不包含這些工具的二進位檔，首次安裝由官方 GitHub Release 下載，核對發行檔提供的 SHA-256。工具不加入系統 PATH，不設為常駐服務。

| 工具 | 用途 | 官方來源 | 授權 |
|---|---|---|---|
| yt-dlp.exe | YouTube 解析、驗證與下載 | https://github.com/yt-dlp/yt-dlp | 原始碼 Unlicense；PyInstaller 發行執行檔包含 GPLv3+ 等第三方元件，依官方 THIRD_PARTY_LICENSES 說明 |
| FFmpeg／ffprobe | 原始影音合併、軌道與完整解碼驗證 | https://github.com/yt-dlp/FFmpeg-Builds | 本安装器選用 GPL build，依其隨附 GPL 授權及建置配置 |
| Deno | 執行 yt-dlp 的 YouTube JavaScript 挑戰處理 | https://github.com/denoland/deno | MIT，另依官方 third_party notices |

初始 yt-dlp 固定為已測試的 `2026.08.19`，Deno 固定為 `v2.9.7`；FFmpeg 取官方最新 GPL build。`Update.cmd` 會改取各工具最新官方發行。工具更新後的行為可能不同，驗證紀錄只代表所列版本。

原始碼及授權：[yt-dlp license](https://github.com/yt-dlp/yt-dlp/blob/master/LICENSE)、[yt-dlp 第三方授權](https://github.com/yt-dlp/yt-dlp/blob/master/THIRD_PARTY_LICENSES.txt)、[FFmpeg 建置](https://github.com/yt-dlp/FFmpeg-Builds)、[FFmpeg legal](https://ffmpeg.org/legal.html)、[Deno license](https://github.com/denoland/deno/blob/main/LICENSE.md)。安裝器保留下載壓縮檔內的 LICENSE／COPYING／NOTICE 到安裝資料夾的 licenses 子目錄。

Playwright 只供開發測試，不包含在使用者下載包。新版沒有 Mediabunny、googlevideo、遠端網站程式或雲端下載服務。
