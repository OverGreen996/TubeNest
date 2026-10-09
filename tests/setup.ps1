param([switch]$Run,[string]$OutputDirectory,[string]$ReuseTools)
$ErrorActionPreference='Stop'
$setupRepo=Split-Path $PSScriptRoot -Parent
if(-not $OutputDirectory){$OutputDirectory='test-output\setup-'+[guid]::NewGuid().ToString('N')}
$setupCompiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$setupFramework=Split-Path $setupCompiler
$setupWpf=Join-Path $setupFramework 'WPF'
$setupExe=Join-Path $setupRepo 'tests\SetupHarness.exe'
& $setupCompiler /nologo /target:exe /platform:x64 /main:SetupHarness ("/out:"+$setupExe) /r:System.Core.dll /r:System.Web.Extensions.dll /r:System.IO.Compression.dll /r:System.Xaml.dll ("/r:"+(Join-Path $setupWpf 'PresentationFramework.dll')) ("/r:"+(Join-Path $setupWpf 'PresentationCore.dll')) ("/r:"+(Join-Path $setupWpf 'WindowsBase.dll')) ("/resource:"+(Join-Path $setupRepo 'installer\Setup.xaml')+',Setup.xaml') ("/resource:"+(Join-Path $setupRepo 'dist\setup-payload.zip')+',Payload.zip') ("/resource:"+(Join-Path $setupRepo 'dist\setup-version.txt')+',Version.txt') ("/resource:"+(Join-Path $setupRepo 'extension\icons\128.png')+',Logo.png') (Join-Path $setupRepo 'installer\Setup.cs') (Join-Path $setupRepo 'tests\SetupHarness.cs')
if($LASTEXITCODE -ne 0){throw 'Setup test harness compile failed.'}
if($Run){if($ReuseTools){& $setupExe (Join-Path $setupRepo $OutputDirectory) $ReuseTools}else{& $setupExe (Join-Path $setupRepo $OutputDirectory)};if($LASTEXITCODE -ne 0){throw 'Setup integration test failed.'}}
