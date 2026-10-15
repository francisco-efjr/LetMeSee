@echo off
setlocal
chcp 65001 >nul
title LetMeSee - Diagnostico
"%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe" -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\diagnose.ps1"
set "result=%errorlevel%"
echo.
pause
exit /b %result%
