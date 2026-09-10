@echo off
chcp 65001 >nul
title LetMeSee

:: 1. Se a interface grafica compilada ja existir, inicia imediatamente
if exist "%~dp0LetMeSee.exe" (
    start "" "%~dp0LetMeSee.exe"
    exit /b 0
)

:: 2. Se o arquivo C# existir, compila a interface nativa em 1 segundo
if exist "%~dp0src\LetMeSeeApp.cs" (
    if exist "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe" (
        call "%~dp0build_ui.bat"
        if exist "%~dp0LetMeSee.exe" (
            start "" "%~dp0LetMeSee.exe"
            exit /b 0
        )
    )
)

:: 3. Modo fallback via console
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\run.ps1"

if %errorlevel% neq 0 (
    echo.
    echo O processo foi finalizado.
    pause
)
