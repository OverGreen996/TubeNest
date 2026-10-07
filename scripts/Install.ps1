param([switch]$RefreshTools,[switch]$ToolsOnly,[string]$TargetRoot)
$ErrorActionPreference='Stop'
$ProgressPreference='SilentlyContinue'
[Net.ServicePointManager]::SecurityProtocol=[Net.SecurityProtocolType]::Tls12
$installSource=Split-Path $PSScriptRoot -Parent
if(-not $TargetRoot){$TargetRoot=Join-Path $env:LOCALAPPDATA 'TubeNestYouTube'}
$installTarget=[IO.Path]::GetFullPath($TargetRoot)
if(-not [Environment]::Is64BitOperatingSystem -or $env:PROCESSOR_ARCHITECTURE -eq 'ARM64'){throw '目前支援 Windows 10／11 x64。'}
$installTools=Join-Path $installTarget 'native\tools'
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
}
function Install-Tool([string]$Repo,[string]$AssetName,[string[]]$Files,[string]$Tag){
 if(-not $RefreshTools -and -not @($Files | Where-Object{-not(Test-Path -LiteralPath (Join-Path $installTools $_))}).Count){return}
 $installApi=if($RefreshTools -or -not $Tag){"https://api.github.com/repos/$Repo/releases/latest"}else{"https://api.github.com/repos/$Repo/releases/tags/$Tag"}
 $installRelease=Invoke-RestMethod -Uri $installApi -Headers @{'User-Agent'='TubeNest-YouTube/1.0';Accept='application/vnd.github+json'} -TimeoutSec 60
 $installAsset=@($installRelease.assets | Where-Object{$_.name -eq $AssetName})
 if($installAsset.Count -ne 1 -or $installAsset[0].browser_download_url -notmatch '^https://github\.com/(yt-dlp/yt-dlp|yt-dlp/FFmpeg-Builds|denoland/deno)/releases/download/' -or $installAsset[0].digest -notmatch '^sha256:([a-fA-F0-9]{64})$'){throw "無法核對官方 $AssetName 發行檔案。"}
 $installExpected=$Matches[1]
 $installWork=Join-Path $installTarget ('setup-tmp-'+[guid]::NewGuid().ToString('N'))
 New-Item -ItemType Directory -Path $installWork | Out-Null
 try{
  $installArchive=Join-Path $installWork $AssetName
  Write-Host "下載 $AssetName ($($installRelease.tag_name))…"
  Invoke-WebRequest -UseBasicParsing -Uri $installAsset[0].browser_download_url -OutFile $installArchive -TimeoutSec 900
  if((Get-FileHash -LiteralPath $installArchive -Algorithm SHA256).Hash -ne $installExpected){throw '下載檔案指紋不符，已停止。'}
  if($AssetName -like '*.zip'){
   $installExtract=Join-Path $installWork 'extract';Expand-Archive -LiteralPath $installArchive -DestinationPath $installExtract
   foreach($installFile in $Files){$installFound=@(Get-ChildItem -LiteralPath $installExtract -Recurse -File -Filter $installFile);if($installFound.Count -ne 1){throw "找不到唯一 $installFile。"};Copy-Item -LiteralPath $installFound[0].FullName -Destination (Join-Path $installTools $installFile) -Force}
   $installNotices=Join-Path $installTarget ('licenses\'+$Repo.Replace('/','-'));New-Item -ItemType Directory -Path $installNotices -Force | Out-Null
   Get-ChildItem -LiteralPath $installExtract -Recurse -File | Where-Object{$_.Name -match 'LICENSE|COPYING|NOTICE'} | ForEach-Object{Copy-Item -LiteralPath $_.FullName -Destination $installNotices -Force}
  }else{Copy-Item -LiteralPath $installArchive -Destination (Join-Path $installTools $Files[0]) -Force}
 }finally{
  $installResolved=[IO.Path]::GetFullPath($installWork)
  if($installResolved.StartsWith($installTarget.TrimEnd('\')+'\',[StringComparison]::OrdinalIgnoreCase) -and [IO.Path]::GetFileName($installResolved) -match '^setup-tmp-[a-f0-9]{32}$'){Remove-Item -LiteralPath $installResolved -Recurse -Force}
 }
}
Install-Tool 'yt-dlp/yt-dlp' 'yt-dlp.exe' @('yt-dlp.exe') '2026.08.19'
Install-Tool 'yt-dlp/FFmpeg-Builds' 'ffmpeg-master-latest-win64-gpl.zip' @('ffmpeg.exe','ffprobe.exe') ''
Install-Tool 'denoland/deno' 'deno-x86_64-pc-windows-msvc.zip' @('deno.exe') 'v2.9.7'
foreach($installFile in @('yt-dlp.exe','ffmpeg.exe','ffprobe.exe','deno.exe')){$installArg=if($installFile -like 'ff*'){'-version'}else{'--version'};$installResult=& (Join-Path $installTools $installFile) $installArg 2>&1;if($LASTEXITCODE -ne 0){throw "$installFile 無法執行。"};Write-Host ($installResult | Select-Object -First 1)}
if(-not $ToolsOnly){
 $installHost=Join-Path $installTarget 'native\TubeNestHost.exe';if(-not(Test-Path -LiteralPath $installHost)){throw '缺少本機助手，請使用完整 Windows 包。'}
 $installManifest=Join-Path $installTarget 'native\com.tubenest.youtube.json'
 $installJson=@{name='com.tubenest.youtube';description='TubeNest YouTube on-demand downloader';path=$installHost;type='stdio';allowed_origins=@('chrome-extension://fhjcedbbonnlkilcklhodjejolannode/')}|ConvertTo-Json
 [IO.File]::WriteAllText($installManifest,$installJson,(New-Object Text.UTF8Encoding $false))
 foreach($installBrowser in @('Google\Chrome','Microsoft\Edge')){$installKey="HKCU:\Software\$installBrowser\NativeMessagingHosts\com.tubenest.youtube";New-Item -Path $installKey -Force|Out-Null;Set-Item -LiteralPath $installKey -Value $installManifest}
 Write-Host "安裝完成。開啟 edge://extensions 或 chrome://extensions，啟用開發人員模式並載入："
 Write-Host (Join-Path $installTarget 'extension')
}
