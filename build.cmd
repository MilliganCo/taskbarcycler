@echo off
setlocal EnableExtensions
chcp 65001 >nul
cd /d "%~dp0"

set "FRAMEWORK=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319"
if not exist "%FRAMEWORK%\csc.exe" set "FRAMEWORK=%WINDIR%\Microsoft.NET\Framework\v4.0.30319"

if not exist "%FRAMEWORK%\csc.exe" (
  echo [ERROR] Не найден штатный компилятор .NET Framework csc.exe.
  echo Убедитесь, что в Windows включен .NET Framework 4.x.
  pause
  exit /b 1
)

set "WPF=%FRAMEWORK%\WPF"
if not exist "%WPF%\UIAutomationClient.dll" (
  echo [ERROR] Не найден UIAutomationClient.dll: "%WPF%"
  pause
  exit /b 1
)

"%FRAMEWORK%\csc.exe" /nologo /target:winexe /optimize+ /platform:anycpu ^
  /out:"%~dp0TaskbarCycler.exe" ^
  /reference:System.dll ^
  /reference:System.Core.dll ^
  /reference:System.Windows.Forms.dll ^
  /reference:System.Drawing.dll ^
  /reference:"%WPF%\WindowsBase.dll" ^
  /reference:"%WPF%\UIAutomationClient.dll" ^
  /reference:"%WPF%\UIAutomationTypes.dll" ^
  "%~dp0TaskbarCycler.cs"

if errorlevel 1 (
  echo.
  echo [ERROR] Сборка не удалась.
  pause
  exit /b 1
)

echo [OK] Готово: "%~dp0TaskbarCycler.exe"
exit /b 0
