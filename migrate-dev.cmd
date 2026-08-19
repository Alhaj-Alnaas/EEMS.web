@echo off
chcp 65001 >nul
setlocal
cd /d "%~dp0"

set ASPNETCORE_ENVIRONMENT=Development

echo ========================================
echo  EF migrate - Development (local DB)
echo  Uses: PMS.web\appsettings.Development.json
echo ========================================
echo.

dotnet ef database update ^
  --project "%~dp0DataAccess\DataAccess.csproj" ^
  --startup-project "%~dp0PMS.web\PMS.web.csproj" ^
  --context DataContext

if errorlevel 1 (
  echo.
  echo MIGRATION FAILED
  pause
  exit /b 1
)

echo.
echo Done. Database should be ready on LocalDB: PMSDB_Dev
echo Server=(localdb)\MSSQLLocalDB
pause
