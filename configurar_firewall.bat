@echo off
chcp 65001 >nul
title LetMeSee - Configurar Firewall

echo ========================================================
echo        LETMESEE - CONFIGURACAO DE FIREWALL
echo ========================================================
echo.
echo Solicitando permissao de Administrador...
echo (Clique em "Sim" na tela de confirmacao para prosseguir)
echo.

powershell -NoProfile -ExecutionPolicy Bypass -Command "Start-Process powershell -ArgumentList '-NoProfile -ExecutionPolicy Bypass -File \"\"%~dp0scripts\setup_firewall.ps1\"\"' -Verb RunAs"

if %errorlevel% neq 0 (
    echo.
    echo [ERRO] A solicitacao de administrador foi cancelada ou falhou.
    echo Por favor, clique com o botao direito e selecione "Executar como Administrador".
    echo.
    pause
)
