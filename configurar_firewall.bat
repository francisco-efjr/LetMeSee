@echo off
setlocal
chcp 65001 >nul
title LetMeSee - Firewall
"%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe" -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\launch_firewall.ps1" %*
set "result=%errorlevel%"
if not "%result%"=="0" (
    echo.
    echo [ERRO] O firewall nao foi configurado. Consulte a mensagem acima.
    pause
)
exit /b %result%
