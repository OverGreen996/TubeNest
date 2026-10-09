$ErrorActionPreference='Stop'
$buildRoot=Split-Path $PSScriptRoot -Parent
$buildCompiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $buildCompiler /nologo /target:winexe /optimize+ ("/out:"+(Join-Path $buildRoot 'native\TubeNestHost.exe')) /r:System.Web.Extensions.dll /r:System.Windows.Forms.dll /r:System.Core.dll (Join-Path $buildRoot 'native\TubeNestHost.cs')
if($LASTEXITCODE -ne 0){throw '本機助手編譯失敗。'}
$buildVersion=(Get-Content -Encoding UTF8 -LiteralPath (Join-Path $buildRoot 'extension\manifest.json') -Raw | ConvertFrom-Json).version
$buildPackageName="TubeNest-$buildVersion-Windows"
$buildStage=Join-Path $buildRoot ("dist\"+$buildPackageName)
$buildResolved=[IO.Path]::GetFullPath($buildStage)
if(-not $buildResolved.StartsWith([IO.Path]::GetFullPath((Join-Path $buildRoot 'dist')).TrimEnd('\')+'\',[StringComparison]::OrdinalIgnoreCase)){throw '發布位置錯誤。'}
if(Test-Path -LiteralPath $buildResolved){Remove-Item -LiteralPath $buildResolved -Recurse -Force}
New-Item -ItemType Directory -Path $buildStage -Force|Out-Null
foreach($buildName in @('extension','licenses','docs')){Copy-Item -LiteralPath (Join-Path $buildRoot $buildName) -Destination $buildStage -Recurse}
New-Item -ItemType Directory -Path (Join-Path $buildStage 'native'),(Join-Path $buildStage 'scripts') -Force|Out-Null
Copy-Item -LiteralPath (Join-Path $buildRoot 'native\TubeNestHost.exe') -Destination (Join-Path $buildStage 'native')
foreach($buildName in @('Install.ps1','Uninstall.ps1')){Copy-Item -LiteralPath (Join-Path $buildRoot ('scripts\'+$buildName)) -Destination (Join-Path $buildStage 'scripts')}
foreach($buildName in @('Install.cmd','Update.cmd','Uninstall.cmd','README.md','THIRD-PARTY.md','VALIDATION.md')){Copy-Item -LiteralPath (Join-Path $buildRoot $buildName) -Destination $buildStage}
$buildPayload=Join-Path $buildRoot 'dist\setup-payload.zip'
Compress-Archive -Path (Join-Path $buildStage '*') -DestinationPath $buildPayload -Force
$buildVersionFile=Join-Path $buildRoot 'dist\setup-version.txt'
[IO.File]::WriteAllText($buildVersionFile,$buildVersion)
$buildSetup=Join-Path $buildRoot ("dist\TubeNest-Setup-$buildVersion.exe")
$buildWpf=Join-Path (Split-Path $buildCompiler) 'WPF'
Add-Type -AssemblyName System.Drawing
$buildIcon=Join-Path $buildRoot 'dist\setup.ico'
$buildBitmap=New-Object Drawing.Bitmap (Join-Path $buildRoot 'extension\icons\128.png')
$buildIconStream=[IO.File]::Create($buildIcon)
try{[Drawing.Icon]::FromHandle($buildBitmap.GetHicon()).Save($buildIconStream)}finally{$buildIconStream.Dispose();$buildBitmap.Dispose()}
& $buildCompiler /nologo /target:winexe /platform:x64 /optimize+ ("/win32icon:"+$buildIcon) ("/out:"+$buildSetup) /r:System.Core.dll /r:System.Web.Extensions.dll /r:System.IO.Compression.dll /r:System.Xaml.dll ("/r:"+(Join-Path $buildWpf 'PresentationFramework.dll')) ("/r:"+(Join-Path $buildWpf 'PresentationCore.dll')) ("/r:"+(Join-Path $buildWpf 'WindowsBase.dll')) ("/win32manifest:"+(Join-Path $buildRoot 'installer\Setup.manifest')) ("/resource:"+(Join-Path $buildRoot 'installer\Setup.xaml')+',Setup.xaml') ("/resource:"+$buildPayload+',Payload.zip') ("/resource:"+$buildVersionFile+',Version.txt') ("/resource:"+(Join-Path $buildRoot 'extension\icons\128.png')+',Logo.png') (Join-Path $buildRoot 'installer\Setup.cs')
if($LASTEXITCODE -ne 0){throw '安裝精靈編譯失敗。'}
$buildSetupHash=(Get-FileHash -LiteralPath $buildSetup -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText($buildSetup+'.sha256',"$buildSetupHash  $([IO.Path]::GetFileName($buildSetup))`n",(New-Object Text.UTF8Encoding $false))
Copy-Item -LiteralPath $buildSetup -Destination (Join-Path $buildStage 'TubeNest-Setup.exe')
$buildZip=Join-Path $buildRoot ("dist\"+$buildPackageName+".zip")
Compress-Archive -LiteralPath $buildStage -DestinationPath $buildZip -Force
$buildHash=(Get-FileHash -LiteralPath $buildZip -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText($buildZip+'.sha256',"$buildHash  $buildPackageName.zip`n",(New-Object Text.UTF8Encoding $false))
Write-Host "已建立：$buildZip"
Write-Host "SHA256：$buildHash"
