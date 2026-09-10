<#
.SYNOPSIS
    Configura regras de entrada no Windows Firewall para o LetMeSee.
.DESCRIPTION
    Aplica liberacao de rede para o uxplay.exe e portas de servico (mDNS 5353, TCP 7000/7100, UDP 6000-6001/7011).
    Tambem instala o servico Bonjour caso necessario. Requer privilegios administrativos.
#>

[Console]::OutputEncoding = [System.Text.Encoding]::ASCII
$Host.UI.RawUI.WindowTitle = "LetMeSee - Configuracao de Firewall"

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "         LETMESEE - CONFIGURACAO DE FIREWALL              " -ForegroundColor Yellow
Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host ""

$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Host "[ERRO] Este script requer privilegios de Administrador." -ForegroundColor Red
    Write-Host "Execute o arquivo 'configurar_firewall.bat' como Administrador." -ForegroundColor Yellow
    Write-Host ""
    Read-Host "Pressione ENTER para sair..."
    exit 1
}

$ScriptDir = Split-Path -Parent $PSScriptRoot
$BinDir = Join-Path $ScriptDir "bin"
$UxPlayExe = Join-Path $BinDir "uxplay.exe"
$BonjourMsi = Join-Path $BinDir "third_party\Bonjour64.msi"

# 1. Instalar o servico Bonjour se o instalador estiver presente
$bonjourService = Get-Service -Name "Bonjour Service" -ErrorAction SilentlyContinue
if (-not $bonjourService -and (Test-Path $BonjourMsi)) {
    Write-Host "[1/4] Instalando servico Bonjour (descoberta AirPlay)..." -ForegroundColor Green
    try {
        Start-Process msiexec.exe -ArgumentList "/i `"$BonjourMsi`" /passive /norestart" -Wait
        Write-Host "      -> Servico Bonjour instalado com sucesso." -ForegroundColor Gray
    } catch {
        Write-Host "      [AVISO] Nao foi possivel instalar o Bonjour: $_" -ForegroundColor Yellow
    }
} else {
    Write-Host "[1/4] Servico Bonjour ja detectado no sistema." -ForegroundColor Green
}

# 2. Configurar regra para o executavel
Write-Host "[2/4] Aplicando regra de executavel (uxplay.exe)..." -ForegroundColor Green
try {
    netsh advfirewall firewall delete rule name="Espelho iOS (UxPlay App)" | Out-Null
    netsh advfirewall firewall delete rule name="LetMeSeeYourScreen (UxPlay App)" | Out-Null
    netsh advfirewall firewall delete rule name="LetMeSee (UxPlay App)" | Out-Null
    
    netsh advfirewall firewall add rule name="LetMeSee (UxPlay App)" dir=in action=allow program="$UxPlayExe" enable=yes profile=any | Out-Null
    Write-Host "      -> Regra de executavel aplicada com sucesso." -ForegroundColor Gray
} catch {
    Write-Host "      [AVISO] Nao foi possivel adicionar a regra do aplicativo: $_" -ForegroundColor Yellow
}

# 3. Liberar mDNS / Bonjour
Write-Host "[3/4] Liberando porta de descoberta mDNS / Bonjour (UDP 5353)..." -ForegroundColor Green
try {
    netsh advfirewall firewall delete rule name="Espelho iOS (mDNS 5353)" | Out-Null
    netsh advfirewall firewall delete rule name="LetMeSeeYourScreen (mDNS 5353)" | Out-Null
    netsh advfirewall firewall delete rule name="LetMeSee (mDNS 5353)" | Out-Null

    netsh advfirewall firewall add rule name="LetMeSee (mDNS 5353)" dir=in action=allow protocol=UDP localport=5353 profile=any | Out-Null
    Write-Host "      -> Porta UDP 5353 liberada." -ForegroundColor Gray
} catch {
    Write-Host "      [AVISO] Nao foi possivel liberar a porta 5353: $_" -ForegroundColor Yellow
}

# 4. Liberar portas de streaming
Write-Host "[4/4] Liberando portas de transmissao AirPlay (TCP 7000,7100 / UDP 6000-6001,7011)..." -ForegroundColor Green
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
    Write-Host "      [AVISO] Nao foi possivel liberar as portas de streaming: $_" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "  FIREWALL CONFIGURADO COM SUCESSO!                       " -ForegroundColor Green
Write-Host "  O servico agora pode receber conexoes de rede local.    " -ForegroundColor White
Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host ""
Start-Sleep -Seconds 3
