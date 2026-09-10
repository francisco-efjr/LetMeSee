@echo off
chcp 65001 >nul
title LetMeSee

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\run.ps1"

if %errorlevel% neq 0 (
    echo.
    echo O processo foi finalizado.
    pause
)
