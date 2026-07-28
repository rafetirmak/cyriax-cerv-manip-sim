@echo off
cd /d "%~dp0"
where dotnet >nul 2>nul
if errorlevel 1 (
  echo .NET SDK was not found. Install .NET 8 SDK and try again.
  pause
  exit /b 1
)
dotnet run --project CervicalCalibrationTool.csproj
pause
