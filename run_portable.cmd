@echo off
setlocal EnableExtensions
chcp 65001 >nul
cd /d "%~dp0"

if not exist "%~dp0TaskbarCycler.exe" (
  call "%~dp0build.cmd"
  if errorlevel 1 exit /b 1
)

start "" "%~dp0TaskbarCycler.exe"
exit /b 0
