@echo off
chcp 65001 >nul
setlocal
cd /d "%~dp0"

set OUT=%~dp0Publish\PMS.Server
echo Publishing PMS to:
echo   %OUT%
echo.

dotnet publish "%~dp0PMS.web\PMS.web.csproj" ^
  -c Release ^
  -r win-x64 ^
  --self-contained true ^
  -p:PublishReadyToRun=true ^
  -p:DeleteExistingFiles=true ^
  -o "%OUT%"

if errorlevel 1 (
  echo.
  echo PUBLISH FAILED
  pause
  exit /b 1
)

echo.
echo ========================================
echo  Done. Folder ready:
echo  %OUT%
echo  Copy that folder to the server, then run start-pms.cmd
echo  See DEPLOY.txt inside the folder.
echo ========================================
explorer "%OUT%"
pause
