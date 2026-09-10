<#
.SYNOPSIS
    Configura regras de entrada no Windows Firewall para o LetMeSee.
.DESCRIPTION
    Aplica liberação de rede para o uxplay.exe e portas de serviço (mDNS 5353, TCP 7000/7100, UDP 6000-6001/7011).
    Requer privilégios administrativos.
#>

[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$Host.UI.RawUI.WindowTitle = "LetMeSee - Configuracao de Firewall"

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "         LETMESEE - CONFIGURACAO DE FIREWALL              " -ForegroundColor Yellow
Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host ""

$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Host "[ERRO] Este script requer privilégios de Administrador." -ForegroundColor Red
    Write-Host "Execute o arquivo 'configurar_firewall.bat' como Administrador." -ForegroundColor Yellow
    Write-Host ""
    Read-Host "Pressione ENTER para sair..."
    exit 1
}

$ScriptDir = Split-Path -Parent $PSScriptRoot
$UxPlayExe = Join-Path $ScriptDir "bin\uxplay.exe"

Write-Host "[1/3] Aplicando regra de executável (uxplay.exe)..." -ForegroundColor Green
try {
    netsh advfirewall firewall delete rule name="Espelho iOS (UxPlay App)" | Out-Null
    netsh advfirewall firewall delete rule name="LetMeSeeYourScreen (UxPlay App)" | Out-Null
    netsh advfirewall firewall delete rule name="LetMeSee (UxPlay App)" | Out-Null
    
    netsh advfirewall firewall add rule name="LetMeSee (UxPlay App)" dir=in action=allow program="$UxPlayExe" enable=yes profile=any | Out-Null
    Write-Host "      -> Regra de executável aplicada com sucesso." -ForegroundColor Gray
} catch {
    Write-Host "      [AVISO] Não foi possível adicionar a regra do aplicativo: $_" -ForegroundColor Yellow
}

Write-Host "[2/3] Liberando porta de descoberta mDNS / Bonjour (UDP 5353)..." -ForegroundColor Green
try {
    netsh advfirewall firewall delete rule name="Espelho iOS (mDNS 5353)" | Out-Null
    netsh advfirewall firewall delete rule name="LetMeSeeYourScreen (mDNS 5353)" | Out-Null
    netsh advfirewall firewall delete rule name="LetMeSee (mDNS 5353)" | Out-Null

    netsh advfirewall firewall add rule name="LetMeSee (mDNS 5353)" dir=in action=allow protocol=UDP localport=5353 profile=any | Out-Null
    Write-Host "      -> Porta UDP 5353 liberada." -ForegroundColor Gray
} catch {
    Write-Host "      [AVISO] Não foi possível liberar a porta 5353: $_" -ForegroundColor Yellow
}

Write-Host "[3/3] Liberando portas de transmissão AirPlay (TCP 7000,7100 / UDP 6000-6001,7011)..." -ForegroundColor Green
try {
    netsh advfirewall firewall delete rule name="Espelho iOS (AirPlay TCP)" | Out-Null
    netsh advfirewall firewall delete rule name="LetMeSeeYourScreen (AirPlay TCP)" | Out-Null
    netsh advfirewall firewall delete rule name="LetMeSee (AirPlay TCP)" | Out-Null
    netsh advfirewall firewall add rule name="LetMeSee (AirPlay TCP)" dir=in action=allow protocol=TCP localport=7000,7100 profile=any | Out-Null

    netsh advfirewall firewall delete rule name="Espelho iOS (AirPlay UDP)" | Out-Null
    netsh advfirewall firewall delete rule name="LetMeSeeYourScreen (AirPlay UDP)" | Out-Null
    netsh advfirewall firewall delete rule name="LetMeSee (AirPlay UDP)" | Out-Null
    netsh advfirewall firewall add rule name="LetMeSee (AirPlay UDP)" dir=in action=allow protocol=UDP localport=6000,6001,7011 profile=any | Out-Null
    Write-Host "      -> Portas de streaming liberadas." -ForegroundColor Gray
} catch {
    Write-Host "      [AVISO] Não foi possível liberar as portas de streaming: $_" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "  FIREWALL CONFIGURADO COM SUCESSO!                       " -ForegroundColor Green
Write-Host "  O serviço agora pode receber conexões de rede local.    " -ForegroundColor White
Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host ""
Start-Sleep -Seconds 3
