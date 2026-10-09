$ErrorActionPreference='Stop'
foreach($removeBrowser in @('Google\Chrome','Microsoft\Edge')){$removeKey="HKCU:\Software\$removeBrowser\NativeMessagingHosts\com.tubenest.youtube";if(Test-Path -LiteralPath $removeKey){Remove-Item -LiteralPath $removeKey -Force}}
$removeShortcut=Join-Path ([Environment]::GetFolderPath('Programs')) 'TubeNest 安裝與修復.lnk'
if(Test-Path -LiteralPath $removeShortcut){$removeShell=New-Object -ComObject WScript.Shell;$removeLink=$removeShell.CreateShortcut($removeShortcut);if($removeLink.TargetPath -eq (Join-Path $env:LOCALAPPDATA 'TubeNestYouTube\TubeNest-Setup.exe')){Remove-Item -LiteralPath $removeShortcut -Force}}
Write-Host '本機助手註冊已移除。請從瀏覽器移除擴充。程式檔案及你下載的影片仍保留。'
