@echo off
chcp 65001 >nul
title Compilando LetMeSee.exe...

echo ========================================================
echo         COMPILANDO INTERFACE GRAFICA LETMESEE
echo ========================================================
echo.

set CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe

if not exist "%CSC%" (
    set CSC=C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe
)

if not exist "%CSC%" (
    echo [ERRO] Compilador C# nao encontrado em C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe
    pause
    exit /b 1
)

echo Compilando src\LetMeSeeApp.cs para LetMeSee.exe com icone personalizado...
"%CSC%" /nologo /target:winexe /optimize+ /win32icon:"%~dp0installer\app_icon.ico" /out:"%~dp0LetMeSee.exe" "%~dp0src\LetMeSeeApp.cs" /r:System.dll,System.Drawing.dll,System.Windows.Forms.dll

if %errorlevel% equ 0 (
    echo.
    echo ========================================================
    echo [SUCESSO] LetMeSee.exe gerado com sucesso!
    echo ========================================================
    echo.
) else (
    echo.
    echo [ERRO] Falha na compilacao do LetMeSee.exe.
    pause
    exit /b 1
)
