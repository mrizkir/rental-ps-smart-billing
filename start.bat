@echo off
setlocal
cd /d "%~dp0"

echo === 1. git pull ===
git pull
if errorlevel 1 (
  echo git pull gagal.
  pause
  exit /b 1
)

echo.
echo === 2. Python TV service ===
start "TV Service" /D "%~dp0python" cmd /k "call venv\Scripts\activate.bat && python tv_service.py"

echo.
echo === 3. Aplikasi desktop (.NET) ===
cd /d "%~dp0app"
dotnet run -- --verbose

endlocal
