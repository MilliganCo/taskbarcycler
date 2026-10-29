@echo off
setlocal EnableExtensions
chcp 65001 >nul
cd /d "%~dp0"

echo TaskbarCycler — установка
call "%~dp0build.cmd"
if errorlevel 1 exit /b 1

set "APPDIR=%LOCALAPPDATA%\TaskbarCycler"

taskkill /IM TaskbarCycler.exe /F >nul 2>&1
if not exist "%APPDIR%" mkdir "%APPDIR%"

copy /Y "%~dp0TaskbarCycler.exe" "%APPDIR%\TaskbarCycler.exe" >nul
if errorlevel 1 (
  echo [ERROR] Не удалось скопировать TaskbarCycler.exe.
  pause
  exit /b 1
)

start "" "%APPDIR%\TaskbarCycler.exe" --install-startup

echo.
echo [OK] Установлено в "%APPDIR%".
echo Утилита запущена и добавлена в автозапуск текущего пользователя.
echo Значок появился в системном трее.
timeout /t 3 >nul
exit /b 0
