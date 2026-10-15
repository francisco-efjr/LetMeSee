@echo off
setlocal
chcp 65001 >nul
title LetMeSee
if exist "%~dp0LetMeSee.exe" (
    start "" "%~dp0LetMeSee.exe"
    if errorlevel 1 exit /b 1
    exit /b 0
)
if exist "%~dp0src\LetMeSeeApp.cs" (
    call "%~dp0build_ui.bat"
    if errorlevel 1 goto build_failed
    if not exist "%~dp0LetMeSee.exe" goto build_failed
    start "" "%~dp0LetMeSee.exe"
    if errorlevel 1 exit /b 1
    exit /b 0
)
"%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe" -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\run.ps1"
set "result=%errorlevel%"
if not "%result%"=="0" pause
exit /b %result%
:build_failed
echo [ERRO] A interface nao foi compilada. Consulte a mensagem do build acima.
pause
exit /b 1
