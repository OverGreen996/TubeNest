$ErrorActionPreference='Stop'
foreach($removeBrowser in @('Google\Chrome','Microsoft\Edge')){$removeKey="HKCU:\Software\$removeBrowser\NativeMessagingHosts\com.tubenest.youtube";if(Test-Path -LiteralPath $removeKey){Remove-Item -LiteralPath $removeKey -Force}}
Write-Host '本機助手註冊已移除。請從瀏覽器移除擴充。程式檔案及你下載的影片仍保留。'
