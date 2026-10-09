param([switch]$RefreshTools,[switch]$ToolsOnly,[string]$TargetRoot,[switch]$NoBrowser,[ValidateSet('Edge','Chrome')][string]$Browser='Edge',[switch]$Wizard,[string]$WorkRoot)
$ErrorActionPreference='Stop'
$ProgressPreference='SilentlyContinue'
[Console]::OutputEncoding=New-Object Text.UTF8Encoding $false
function Report-Setup([string]$Stage,[string]$Message,[long]$Received=0,[long]$Total=0,[double]$Speed=0){
 if($Wizard){[Console]::WriteLine('@TN:'+(@{stage=$Stage;message=$Message;received=$Received;total=$Total;speed=$Speed}|ConvertTo-Json -Compress));[Console]::Out.Flush()}else{Write-Host $Message}
}
trap{if($Wizard){Report-Setup 'error' $_.Exception.Message}else{[Console]::Error.WriteLine($_.Exception.Message)};exit 1}
function Read-ToolVersion([string]$Path){
 $toolProcess=New-Object Diagnostics.Process;$toolStarted=$false
 try{
  $toolProcess.StartInfo=New-Object Diagnostics.ProcessStartInfo
  $toolProcess.StartInfo.FileName=$Path;$toolProcess.StartInfo.Arguments=if([IO.Path]::GetFileName($Path) -like 'ff*'){'-version'}else{'--version'}
  $toolProcess.StartInfo.UseShellExecute=$false;$toolProcess.StartInfo.CreateNoWindow=$true
  $toolProcess.StartInfo.RedirectStandardOutput=$true;$toolProcess.StartInfo.RedirectStandardError=$true
  $toolProcess.StartInfo.StandardOutputEncoding=[Text.Encoding]::UTF8;$toolProcess.StartInfo.StandardErrorEncoding=[Text.Encoding]::UTF8
  $null=$toolProcess.Start();$toolStarted=$true;$toolOut=$toolProcess.StandardOutput.ReadToEndAsync();$toolErr=$toolProcess.StandardError.ReadToEndAsync()
  if(-not $toolProcess.WaitForExit(10000)){throw ([IO.Path]::GetFileName($Path)+' 執行檢查逾時。')}
  if($toolProcess.ExitCode -ne 0){throw ([IO.Path]::GetFileName($Path)+' 無法正常執行。')}
  return (($toolOut.Result+$toolErr.Result) -split '\r?\n' | Where-Object{$_} | Select-Object -First 1)
 }finally{if($toolStarted -and -not $toolProcess.HasExited){$toolProcess.Kill();$toolProcess.WaitForExit()};$toolProcess.Dispose()}
}
function Receive-ToolFile([string]$Uri,[string]$Destination,[string]$Name){
 $request=[Net.HttpWebRequest]::Create($Uri);$request.UserAgent='TubeNest-Setup';$request.Timeout=60000;$request.ReadWriteTimeout=30000
 $response=$null;$inputStream=$null;$outputStream=$null
 try{
  $response=$request.GetResponse();$inputStream=$response.GetResponseStream();$outputStream=[IO.File]::Create($Destination)
  $buffer=New-Object byte[] 262144;$received=0L;$clock=[Diagnostics.Stopwatch]::StartNew();$last=0L
  Report-Setup 'download' $Name 0 $response.ContentLength 0
  while(($read=$inputStream.Read($buffer,0,$buffer.Length)) -gt 0){
   $outputStream.Write($buffer,0,$read);$received+=$read
   if($clock.ElapsedMilliseconds-$last -ge 250){Report-Setup 'download' $Name $received $response.ContentLength ($received/[Math]::Max(0.001,$clock.Elapsed.TotalSeconds));$last=$clock.ElapsedMilliseconds}
  }
  if($response.ContentLength -gt 0 -and $received -ne $response.ContentLength){throw '下載不完整，請重試。'}
  Report-Setup 'download' $Name $received $response.ContentLength ($received/[Math]::Max(0.001,$clock.Elapsed.TotalSeconds))
 }finally{if($outputStream){$outputStream.Dispose()};if($inputStream){$inputStream.Dispose()};if($response){$response.Dispose()}}
}
[Net.ServicePointManager]::SecurityProtocol=[Net.SecurityProtocolType]::Tls12
$installSource=Split-Path $PSScriptRoot -Parent
if(-not $TargetRoot){$TargetRoot=Join-Path $env:LOCALAPPDATA 'TubeNestYouTube'}
$installTarget=[IO.Path]::GetFullPath($TargetRoot)
if(-not [Environment]::Is64BitOperatingSystem -or $env:PROCESSOR_ARCHITECTURE -eq 'ARM64'){throw '目前支援 Windows 10／11 x64。'}
$installTools=Join-Path $installTarget 'native\tools'
Report-Setup 'prepare' '準備安裝檔案'
New-Item -ItemType Directory -Path $installTools -Force | Out-Null
if(-not $ToolsOnly){
 foreach($installName in @('extension','native','scripts','licenses','docs')){
  $installFrom=Join-Path $installSource $installName
  if([IO.Path]::GetFullPath($installFrom).TrimEnd('\') -ne (Join-Path $installTarget $installName).TrimEnd('\')){
   New-Item -ItemType Directory -Path (Join-Path $installTarget $installName) -Force | Out-Null
   Copy-Item -Path (Join-Path $installFrom '*') -Destination (Join-Path $installTarget $installName) -Recurse -Force
  }
 }
 foreach($installName in @('Install.cmd','Update.cmd','Uninstall.cmd','README.md','THIRD-PARTY.md')){if($installSource.TrimEnd('\') -ne $installTarget.TrimEnd('\')){Copy-Item -LiteralPath (Join-Path $installSource $installName) -Destination $installTarget -Force}}
 $installWizardFrom=Join-Path $installSource 'TubeNest-Setup.exe';$installWizardTo=Join-Path $installTarget 'TubeNest-Setup.exe'
 if((Test-Path -LiteralPath $installWizardFrom) -and $installSource.TrimEnd('\') -ne $installTarget.TrimEnd('\')){
  if(-not(Test-Path -LiteralPath $installWizardTo) -or (Get-FileHash -LiteralPath $installWizardFrom).Hash -ne (Get-FileHash -LiteralPath $installWizardTo).Hash){Copy-Item -LiteralPath $installWizardFrom -Destination $installWizardTo -Force}
 }
}
function Install-Tool([string]$Repo,[string]$AssetName,[string[]]$Files,[string]$Tag){
 if(-not $RefreshTools -and -not @($Files | Where-Object{-not(Test-Path -LiteralPath (Join-Path $installTools $_))}).Count){
  $usable=$true
  foreach($toolFile in $Files){try{$null=Read-ToolVersion (Join-Path $installTools $toolFile)}catch{$usable=$false}}
  if($usable){Report-Setup 'reuse' ("沿用已安裝的 "+$AssetName);return}
 }
 Report-Setup 'resolve' ("取得官方 "+$AssetName)
 $installApi=if($RefreshTools -or -not $Tag){"https://api.github.com/repos/$Repo/releases/latest"}else{"https://api.github.com/repos/$Repo/releases/tags/$Tag"}
 $installRelease=Invoke-RestMethod -Uri $installApi -Headers @{'User-Agent'='TubeNest-YouTube/1.0';Accept='application/vnd.github+json'} -TimeoutSec 60
 $installAsset=@($installRelease.assets | Where-Object{$_.name -eq $AssetName})
 if($installAsset.Count -ne 1 -or $installAsset[0].browser_download_url -notmatch '^https://github\.com/(yt-dlp/yt-dlp|yt-dlp/FFmpeg-Builds|denoland/deno)/releases/download/' -or $installAsset[0].digest -notmatch '^sha256:([a-fA-F0-9]{64})$'){throw "無法核對官方 $AssetName 發行檔案。"}
 $installExpected=$Matches[1]
 $installWorkBase=if($WorkRoot){[IO.Path]::GetFullPath($WorkRoot)}else{$installTarget}
 $installWork=Join-Path $installWorkBase ('setup-tmp-'+[guid]::NewGuid().ToString('N'))
 New-Item -ItemType Directory -Path $installWork | Out-Null
 try{
  $installArchive=Join-Path $installWork $AssetName
  Receive-ToolFile $installAsset[0].browser_download_url $installArchive $AssetName
  Report-Setup 'verify' ("核對 SHA-256："+$AssetName)
  if((Get-FileHash -LiteralPath $installArchive -Algorithm SHA256).Hash -ne $installExpected){throw '下載檔案指紋不符，已停止。'}
  if($AssetName -like '*.zip'){
   Report-Setup 'extract' ("解壓安裝："+$AssetName)
   $installExtract=Join-Path $installWork 'extract';Expand-Archive -LiteralPath $installArchive -DestinationPath $installExtract
   foreach($installFile in $Files){$installFound=@(Get-ChildItem -LiteralPath $installExtract -Recurse -File -Filter $installFile);if($installFound.Count -ne 1){throw "找不到唯一 $installFile。"};Copy-Item -LiteralPath $installFound[0].FullName -Destination (Join-Path $installTools $installFile) -Force}
   $installNotices=Join-Path $installTarget ('licenses\'+$Repo.Replace('/','-'));New-Item -ItemType Directory -Path $installNotices -Force | Out-Null
   Get-ChildItem -LiteralPath $installExtract -Recurse -File | Where-Object{$_.Name -match 'LICENSE|COPYING|NOTICE'} | ForEach-Object{Copy-Item -LiteralPath $_.FullName -Destination $installNotices -Force}
  }else{Copy-Item -LiteralPath $installArchive -Destination (Join-Path $installTools $Files[0]) -Force}
 }finally{
  $installResolved=[IO.Path]::GetFullPath($installWork)
  if($installResolved.StartsWith($installWorkBase.TrimEnd('\')+'\',[StringComparison]::OrdinalIgnoreCase) -and [IO.Path]::GetFileName($installResolved) -match '^setup-tmp-[a-f0-9]{32}$'){Remove-Item -LiteralPath $installResolved -Recurse -Force}
 }
}
Install-Tool 'yt-dlp/yt-dlp' 'yt-dlp.exe' @('yt-dlp.exe') '2026.08.19'
Install-Tool 'yt-dlp/FFmpeg-Builds' 'ffmpeg-master-latest-win64-gpl.zip' @('ffmpeg.exe','ffprobe.exe') ''
Install-Tool 'denoland/deno' 'deno-x86_64-pc-windows-msvc.zip' @('deno.exe') 'v2.9.7'
foreach($installFile in @('yt-dlp.exe','ffmpeg.exe','ffprobe.exe','deno.exe')){Write-Host (Read-ToolVersion (Join-Path $installTools $installFile))}
if(-not $ToolsOnly){
 Report-Setup 'register' '連接 Chrome 與 Edge 的本機助手'
 $installHost=Join-Path $installTarget 'native\TubeNestHost.exe';if(-not(Test-Path -LiteralPath $installHost)){throw '缺少本機助手，請使用完整 Windows 包。'}
 $installManifest=Join-Path $installTarget 'native\com.tubenest.youtube.json'
 $installJson=@{name='com.tubenest.youtube';description='TubeNest YouTube on-demand downloader';path=$installHost;type='stdio';allowed_origins=@('chrome-extension://fhjcedbbonnlkilcklhodjejolannode/')}|ConvertTo-Json
 [IO.File]::WriteAllText($installManifest,$installJson,(New-Object Text.UTF8Encoding $false))
 foreach($installBrowser in @('Google\Chrome','Microsoft\Edge')){$installKey="HKCU:\Software\$installBrowser\NativeMessagingHosts\com.tubenest.youtube";New-Item -Path $installKey -Force|Out-Null;Set-Item -LiteralPath $installKey -Value $installManifest}
 Report-Setup 'check' '檢查本機助手通訊'
 $installProbe=New-Object Diagnostics.Process
 $installProbe.StartInfo=New-Object Diagnostics.ProcessStartInfo
 $installProbe.StartInfo.FileName=$installHost;$installProbe.StartInfo.Arguments='chrome-extension://fhjcedbbonnlkilcklhodjejolannode/'
 $installProbe.StartInfo.UseShellExecute=$false;$installProbe.StartInfo.CreateNoWindow=$true
 $installProbe.StartInfo.RedirectStandardInput=$true;$installProbe.StartInfo.RedirectStandardOutput=$true;$installProbe.StartInfo.RedirectStandardError=$true
 try{
  $null=$installProbe.Start()
  $installRequest=[Text.Encoding]::UTF8.GetBytes('{"id":"setup-check","action":"cancel","jobId":"setup-no-job"}')
  $installProbe.StandardInput.BaseStream.Write([BitConverter]::GetBytes($installRequest.Length),0,4)
  $installProbe.StandardInput.BaseStream.Write($installRequest,0,$installRequest.Length);$installProbe.StandardInput.BaseStream.Flush()
  function Read-ProbeBytes([int]$Count){
   $bytes=New-Object byte[] $Count;$offset=0
   while($offset -lt $Count){$task=$installProbe.StandardOutput.BaseStream.ReadAsync($bytes,$offset,$Count-$offset);if(-not $task.Wait(5000)){throw '助手通訊逾時，請重試。'};$got=$task.Result;if($got -le 0){throw '助手沒有回應，請重試。'};$offset+=$got}
   return ,$bytes
  }
  $installLength=[BitConverter]::ToInt32((Read-ProbeBytes 4),0);if($installLength -lt 1 -or $installLength -gt 65536){throw '助手回應無效。'}
  $installAnswer=[Text.Encoding]::UTF8.GetString((Read-ProbeBytes $installLength))|ConvertFrom-Json
  if($installAnswer.id -ne 'setup-check' -or $installAnswer.ok -ne $true){throw '助手通訊檢查失敗。'}
  $installProbe.StandardInput.Close();if(-not $installProbe.WaitForExit(5000)){throw '助手未正常退出。'}
 }finally{try{if(-not $installProbe.HasExited){$installProbe.Kill()}}catch{};$installProbe.Dispose()}
 Report-Setup 'ready' '下載助手已安裝並通過通訊檢查'
 if((Test-Path -LiteralPath $installWizardTo) -and $installTarget.TrimEnd('\') -eq (Join-Path $env:LOCALAPPDATA 'TubeNestYouTube').TrimEnd('\')){
  try{$installShell=New-Object -ComObject WScript.Shell;$installShortcut=$installShell.CreateShortcut((Join-Path ([Environment]::GetFolderPath('Programs')) 'TubeNest 安裝與修復.lnk'));$installShortcut.TargetPath=$installWizardTo;$installShortcut.WorkingDirectory=$installTarget;$installShortcut.Description='TubeNest 安裝與修復';$installShortcut.Save()}catch{Write-Host '無法建立開始功能表捷徑，可直接重新開啟 TubeNest 安裝精靈。'}
 }
 if(-not $Wizard){Write-Host "安裝完成。開啟 edge://extensions 或 chrome://extensions，啟用開發人員模式並載入："}
 Write-Host (Join-Path $installTarget 'extension')
 if(-not $NoBrowser){
  $installExtensionPath=Join-Path $installTarget 'extension'
  try{Set-Clipboard -Value $installExtensionPath;Write-Host '擴充資料夾路徑已複製。'}catch{Write-Host '請手動複製上面的擴充資料夾路徑。'}
  $installBrowserRelative=if($Browser -eq 'Chrome'){'Google\Chrome\Application\chrome.exe'}else{'Microsoft\Edge\Application\msedge.exe'}
  $installBrowserUrl=if($Browser -eq 'Chrome'){'chrome://extensions'}else{'edge://extensions'}
  $installBrowserExe=@($env:ProgramFiles,${env:ProgramFiles(x86)},$env:LOCALAPPDATA) | Where-Object{$_} | ForEach-Object{Join-Path $_ $installBrowserRelative} | Where-Object{Test-Path -LiteralPath $_} | Select-Object -First 1
  if($installBrowserExe){try{Start-Process -FilePath $installBrowserExe -ArgumentList $installBrowserUrl -WindowStyle Normal}catch{Write-Host "請自行開啟 $installBrowserUrl"}}else{Write-Host "請自行開啟 $installBrowserUrl"}
  Write-Host '第一次安裝：開啟「開發人員模式」→「載入解壓縮」→ 貼上路徑 →「選擇資料夾」。'
  Write-Host '更新既有 TubeNest：只需點 TubeNest 的「重新載入」，再重新整理 YouTube。'
 }
}
