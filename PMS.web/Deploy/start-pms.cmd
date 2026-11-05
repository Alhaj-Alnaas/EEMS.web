@echo off
cd /d "%~dp0"

set ASPNETCORE_ENVIRONMENT=Production
set ASPNETCORE_URLS=http://0.0.0.0:5260

rem Launch the web host without keeping this command window open.
start "" PMS.web.exe
exit /b 0
