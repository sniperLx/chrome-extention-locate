@echo off
chcp 65001 >nul
setlocal
cd /d "%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0uninstall.ps1"
set "uninstall_exit_code=%errorlevel%"
echo.
if not "%uninstall_exit_code%"=="0" (
  echo Uninstallation did not complete. Review the error above.
) else (
  echo Uninstallation completed.
)
pause
exit /b %uninstall_exit_code%
