@echo off
chcp 65001 >nul
title LetMeSee - Diagnostico de Rede

echo ========================================================
echo        LETMESEE - DIAGNOSTICO DE CONEXAO
echo ========================================================
echo.

powershell -NoProfile -ExecutionPolicy Bypass -Command "& {
    Write-Host '[1] Verificando Binarios...' -ForegroundColor Cyan
    $bin = Join-Path '%~dp0' 'bin\uxplay.exe'
    if (Test-Path $bin) {
        Write-Host '    -> uxplay.exe presente em bin/' -ForegroundColor Green
    } else {
        Write-Host '    [AVISO] uxplay.exe nao encontrado em bin/' -ForegroundColor Yellow
    }

    Write-Host ''
    Write-Host '[2] Enderecos IP deste Computador:' -ForegroundColor Cyan
    Get-NetIPAddress -AddressFamily IPv4 | Where-Object { $_.IPAddress -notlike '127.*' -and $_.IPAddress -notlike '169.254.*' } | ForEach-Object {
        Write-Host ('    Interface: ' + $_.InterfaceAlias + ' | IP: ' + $_.IPAddress) -ForegroundColor White
    }

    Write-Host ''
    Write-Host '[3] Verificando Regras de Firewall...' -ForegroundColor Cyan
    $fwApp = netsh advfirewall firewall show rule name='LetMeSee (UxPlay App)' 2>&1
    if ($fwApp -match 'LetMeSee' -or $fwApp -match 'Espelho iOS' -or $fwApp -match 'LetMeSeeYourScreen') {
        Write-Host '    -> Regra do LetMeSee encontrada no Firewall!' -ForegroundColor Green
    } else {
        Write-Host '    [ALERTA] Regra do Firewall nao identificada.' -ForegroundColor Yellow
        Write-Host '    Execute ''configurar_firewall.bat'' como Administrador.' -ForegroundColor Yellow
    }

    Write-Host ''
    Write-Host '[4] Validacoes de Ambiente Corporativo:' -ForegroundColor Cyan
    Write-Host '    - O iPhone e o PC devem estar conectados no mesmo Wi-Fi corporativo.' -ForegroundColor Gray
    Write-Host '    - Em uso com VPN, certifique-se de habilitar ''Allow Local LAN Access''.' -ForegroundColor Gray
    Write-Host '    - Redes Guest/Visitantes costumam isolar dispositivos (Client Isolation).' -ForegroundColor Gray
}"

echo.
echo ========================================================
echo Diagnostico concluido.
pause
