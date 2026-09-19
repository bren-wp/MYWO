@echo off
setlocal
cd /d "%~dp0"
where dotnet >nul 2>nul || (
  echo [MYWO] .NET 10 SDK nije pronaden.
  echo Instalirajte .NET 10 SDK i pokrenite ovu datoteku ponovno.
  pause
  exit /b 1
)
powershell -NoProfile -ExecutionPolicy Bypass -File ".\scripts\validate-source.ps1" || exit /b 1
powershell -NoProfile -ExecutionPolicy Bypass -File ".\scripts\build-release.ps1" -Version 0.9.0 || exit /b 1
echo.
echo MYWO v0.9.0 release je izgraden u .\dist
explorer ".\dist"
endlocal
