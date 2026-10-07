$ErrorActionPreference='Stop'
$buildRoot=Split-Path $PSScriptRoot -Parent
$buildCompiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $buildCompiler /nologo /target:winexe /optimize+ ("/out:"+(Join-Path $buildRoot 'native\TubeNestHost.exe')) /r:System.Web.Extensions.dll /r:System.Windows.Forms.dll /r:System.Core.dll (Join-Path $buildRoot 'native\TubeNestHost.cs')
if($LASTEXITCODE -ne 0){throw '本機助手編譯失敗。'}
$buildStage=Join-Path $buildRoot 'dist\TubeNest-1.0.0-Windows'
$buildResolved=[IO.Path]::GetFullPath($buildStage)
if(-not $buildResolved.StartsWith([IO.Path]::GetFullPath((Join-Path $buildRoot 'dist')).TrimEnd('\')+'\',[StringComparison]::OrdinalIgnoreCase)){throw '發布位置錯誤。'}
if(Test-Path -LiteralPath $buildResolved){Remove-Item -LiteralPath $buildResolved -Recurse -Force}
New-Item -ItemType Directory -Path $buildStage -Force|Out-Null
foreach($buildName in @('extension','licenses','docs')){Copy-Item -LiteralPath (Join-Path $buildRoot $buildName) -Destination $buildStage -Recurse}
New-Item -ItemType Directory -Path (Join-Path $buildStage 'native'),(Join-Path $buildStage 'scripts') -Force|Out-Null
Copy-Item -LiteralPath (Join-Path $buildRoot 'native\TubeNestHost.exe') -Destination (Join-Path $buildStage 'native')
foreach($buildName in @('Install.ps1','Uninstall.ps1')){Copy-Item -LiteralPath (Join-Path $buildRoot ('scripts\'+$buildName)) -Destination (Join-Path $buildStage 'scripts')}
foreach($buildName in @('Install.cmd','Update.cmd','Uninstall.cmd','README.md','THIRD-PARTY.md','VALIDATION.md')){Copy-Item -LiteralPath (Join-Path $buildRoot $buildName) -Destination $buildStage}
$buildZip=Join-Path $buildRoot 'dist\TubeNest-1.0.0-Windows.zip'
Compress-Archive -LiteralPath $buildStage -DestinationPath $buildZip -Force
$buildHash=(Get-FileHash -LiteralPath $buildZip -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText($buildZip+'.sha256',"$buildHash  TubeNest-1.0.0-Windows.zip`n",(New-Object Text.UTF8Encoding $false))
Write-Host "已建立：$buildZip"
Write-Host "SHA256：$buildHash"
