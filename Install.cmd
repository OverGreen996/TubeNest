@echo off
if exist "%~dp0TubeNest-Setup.exe" (
 start "" "%~dp0TubeNest-Setup.exe"
 exit /b
)
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\Install.ps1"
if errorlevel 1 echo Installation failed. See the message above.
pause
