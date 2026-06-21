@echo off
:: One-click installer for the Mycelium Navisworks add-in.
:: Double-click this file. It self-elevates (the Navisworks Plugins folder lives
:: under Program Files and needs admin), then runs install.ps1, which detects
:: every installed Navisworks, builds the add-in against it, and deploys it.
::
:: To uninstall instead, run:  install.cmd /uninstall

setlocal
set "SCRIPT_DIR=%~dp0"
set "PS_ARGS="
if /I "%~1"=="/uninstall" set "PS_ARGS=-Uninstall"
if /I "%~1"=="-uninstall" set "PS_ARGS=-Uninstall"

:: Already elevated?  net session only succeeds as admin.
net session >nul 2>&1
if %errorlevel%==0 goto run

echo Requesting administrator privileges...
powershell -NoProfile -ExecutionPolicy Bypass -Command ^
  "Start-Process -FilePath '%~f0' -ArgumentList '%~1' -Verb RunAs"
goto :eof

:run
powershell -NoProfile -ExecutionPolicy Bypass -File "%SCRIPT_DIR%install.ps1" %PS_ARGS%
echo.
pause
endlocal
