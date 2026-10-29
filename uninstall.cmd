@echo off
setlocal EnableExtensions
chcp 65001 >nul

set "APPDIR=%LOCALAPPDATA%\TaskbarCycler"

taskkill /IM TaskbarCycler.exe /F >nul 2>&1
reg delete "HKCU\Software\Microsoft\Windows\CurrentVersion\Run" /v "TaskbarCycler" /f >nul 2>&1

if exist "%APPDIR%" rmdir /S /Q "%APPDIR%"

echo [OK] TaskbarCycler удален. Автозапуск отключен.
timeout /t 2 >nul
exit /b 0
