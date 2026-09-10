<#
.SYNOPSIS
    Gerenciamento e instalacao automatica das dependencias do LetMeSee.
.DESCRIPTION
    Verifica a presenca do executavel uxplay.exe e efetua o download automatico
    do pacote oficial de binarios da Release do GitHub caso nao esteja presente.
#>

[Console]::OutputEncoding = [System.Text.Encoding]::ASCII
$Host.UI.RawUI.WindowTitle = "LetMeSee - Download de Dependencias"

$ScriptDir = Split-Path -Parent $PSScriptRoot
$BinDir = Join-Path $ScriptDir "bin"
$UxPlayExe = Join-Path $BinDir "uxplay.exe"
$LocalZip = Join-Path $ScriptDir "bin.zip"
$BonjourMsi = Join-Path $BinDir "third_party\Bonjour64.msi"

# URL oficial da Release v1.0.0 com os binarios pre-compilados
$OFFICIAL_URL = "https://github.com/francisco-efjr/LetMeSee/releases/download/v1.0.0/LetMeSee-bin.zip"
$DOWNLOAD_URL = if ($env:LETMESEE_BIN_URL) { $env:LETMESEE_BIN_URL } else { $OFFICIAL_URL }

function Test-UxPlayInstalled {
    return (Test-Path $UxPlayExe)
}

function Install-BonjourService {
    $bonjourService = Get-Service -Name "Bonjour Service" -ErrorAction SilentlyContinue
    if (-not $bonjourService -and (Test-Path $BonjourMsi)) {
        Write-Host "Instalando servico Bonjour (necessario para descoberta na rede)..." -ForegroundColor Cyan
        try {
            Start-Process msiexec.exe -ArgumentList "/i `"$BonjourMsi`" /passive /norestart" -Wait
            Write-Host "[OK] Bonjour instalado com sucesso." -ForegroundColor Green
        } catch {
            Write-Host "[AVISO] Nao foi possivel instalar Bonjour automaticamente: $_" -ForegroundColor Yellow
        }
    }
}

# 1. Se os binarios ja estiverem instalados
if (Test-UxPlayInstalled) {
    Install-BonjourService
    exit 0
}

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "        LETMESEE - CONFIGURACAO DE DEPENDENCIAS           " -ForegroundColor Yellow
Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Executavel uxplay.exe nao encontrado localmente." -ForegroundColor Yellow
Write-Host ""

# 2. Verificar se existe um arquivo bin.zip local para descompactar
if (Test-Path $LocalZip) {
    Write-Host "[1/2] Pacote local 'bin.zip' detectado. Extraindo..." -ForegroundColor Cyan
    try {
        Expand-Archive -Path $LocalZip -DestinationPath $BinDir -Force
        if (Test-UxPlayInstalled) {
            Write-Host "[OK] Dependencias extraidas com sucesso." -ForegroundColor Green
            Install-BonjourService
            exit 0
        }
    } catch {
        Write-Host "[ERRO] Falha ao descompactar bin.zip: $_" -ForegroundColor Red
    }
}

# 3. Baixar automaticamente da Release oficial do GitHub
Write-Host "Baixando pacote oficial de binarios (UxPlay + Codecs)..." -ForegroundColor Cyan
Write-Host "Origem: $DOWNLOAD_URL" -ForegroundColor DarkGray
Write-Host ""

$TempZip = Join-Path $env:TEMP "LetMeSee-bin.zip"

try {
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12 -bor [Net.SecurityProtocolType]::Tls13
    
    # Utilizar WebClient com barra de progresso visual no terminal
    $webClient = New-Object System.Net.WebClient
    
    $totalBytes = 0
    Register-ObjectEvent -InputObject $webClient -EventName DownloadProgressChanged -Action {
        $percent = $EventArgs.ProgressPercentage
        Write-Progress -Activity "Baixando dependencias do LetMeSee" -Status "$percent% concluido" -PercentComplete $percent
    } | Out-Null

    $webClient.DownloadFile($DOWNLOAD_URL, $TempZip)
    Write-Progress -Activity "Baixando dependencias do LetMeSee" -Completed

    Write-Host "Extraindo arquivos para a pasta bin/..." -ForegroundColor Cyan
    Expand-Archive -Path $TempZip -DestinationPath $BinDir -Force
    Remove-Item $TempZip -Force -ErrorAction SilentlyContinue

    if (Test-UxPlayInstalled) {
        Write-Host "[OK] Instalacao das dependencias concluida com sucesso!" -ForegroundColor Green
        Install-BonjourService
        Write-Host ""
        Start-Sleep -Seconds 2
        exit 0
    } else {
        throw "O executavel uxplay.exe nao foi encontrado apos a extracao."
    }
} catch {
    Write-Host ""
    Write-Host "[ERRO] Nao foi possivel baixar os binarios automaticamente: $_" -ForegroundColor Red
    Write-Host ""
    Write-Host "Instrucoes para instalacao manual:" -ForegroundColor Yellow
    Write-Host "1. Baixe o pacote em: $DOWNLOAD_URL" -ForegroundColor White
    Write-Host "2. Extraia o conteudo do arquivo zip dentro da pasta:" -ForegroundColor White
    Write-Host "   $BinDir" -ForegroundColor Cyan
    Write-Host ""
    Read-Host "Pressione ENTER para fechar..."
    exit 1
}
