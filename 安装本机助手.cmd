@echo off
chcp 65001 >nul
setlocal
cd /d "%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0install.ps1"
set "install_exit_code=%errorlevel%"
echo.
if not "%install_exit_code%"=="0" (
  echo Installation did not complete. Review the error above.
) else (
  echo Installation completed. You can return to Chrome.
)
pause
exit /b %install_exit_code%
