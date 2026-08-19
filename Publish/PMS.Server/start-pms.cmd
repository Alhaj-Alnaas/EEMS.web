@echo off
chcp 65001 >nul
cd /d "%~dp0"

set ASPNETCORE_ENVIRONMENT=Production
set ASPNETCORE_URLS=http://0.0.0.0:5260

echo ========================================
echo  PMS Server - Push / ADMS ready
echo  URL: http://0.0.0.0:5260
echo  Reader ADMS: http://^<SERVER-IP^>:5260/iclock
echo ========================================
echo.

PMS.web.exe
if errorlevel 1 pause
