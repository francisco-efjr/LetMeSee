<#
.SYNOPSIS
    Gerencia e valida os binários do LetMeSee na pasta bin/.
.DESCRIPTION
    Valida a existência do executável uxplay.exe e permite descompactação local
    ou download via URL configurada (LETMESEE_BIN_URL).
#>

[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$Host.UI.RawUI.WindowTitle = "LetMeSee - Verificacao de Dependencias"

$ScriptDir = Split-Path -Parent $PSScriptRoot
$BinDir = Join-Path $ScriptDir "bin"
$UxPlayExe = Join-Path $BinDir "uxplay.exe"
$LocalZip = Join-Path $ScriptDir "bin.zip"

# URL interna para download dos binários pré-compilados do Windows (opcional)
$DEFAULT_DOWNLOAD_URL = if ($env:LETMESEE_BIN_URL) { $env:LETMESEE_BIN_URL } elseif ($env:LETMESEEYOURSCREEN_BIN_URL) { $env:LETMESEEYOURSCREEN_BIN_URL } else { $env:ESPELHO_IOS_BIN_URL }

function Test-UxPlayInstalled {
    return (Test-Path $UxPlayExe)
}

if (Test-UxPlayInstalled) {
    Write-Host "[OK] Dependências do LetMeSee disponíveis." -ForegroundColor Green
    exit 0
}

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "             LETMESEE - DEPENDENCIAS                      " -ForegroundColor Yellow
Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "O executável 'uxplay.exe' não foi encontrado em: $BinDir" -ForegroundColor Yellow
Write-Host ""

# 1. Tentar descompactar pacote local caso exista
if (Test-Path $LocalZip) {
    Write-Host "[1/2] Pacote 'bin.zip' detectado. Extraindo para a pasta bin/..." -ForegroundColor Cyan
    try {
        Expand-Archive -Path $LocalZip -DestinationPath $BinDir -Force
        if (Test-UxPlayInstalled) {
            Write-Host "[OK] Dependências extraídas com sucesso." -ForegroundColor Green
            exit 0
        }
    } catch {
        Write-Host "[ERRO] Falha na extração de bin.zip: $_" -ForegroundColor Red
    }
}

# 2. Tentar download via URL configurada
if (-not [string]::IsNullOrWhiteSpace($DEFAULT_DOWNLOAD_URL)) {
    Write-Host "[2/2] Baixando pacote a partir de: $DEFAULT_DOWNLOAD_URL..." -ForegroundColor Cyan
    $TempZip = Join-Path $env:TEMP "letmesee-bundle.zip"
    try {
        [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
        Invoke-WebRequest -Uri $DEFAULT_DOWNLOAD_URL -OutFile $TempZip -UseBasicParsing
        Write-Host "      Extraindo arquivos..." -ForegroundColor Cyan
        Expand-Archive -Path $TempZip -DestinationPath $BinDir -Force
        Remove-Item $TempZip -Force -ErrorAction SilentlyContinue

        if (Test-UxPlayInstalled) {
            Write-Host "[OK] Dependências instaladas com sucesso." -ForegroundColor Green
            exit 0
        }
    } catch {
        Write-Host "[AVISO] Download automático não pôde ser concluído: $_" -ForegroundColor Yellow
    }
}

Write-Host "----------------------------------------------------------" -ForegroundColor Yellow
Write-Host " INSTRUÇÃO PARA DISPONIBILIZAÇÃO NA EMPRESA:              " -ForegroundColor Yellow
Write-Host "----------------------------------------------------------" -ForegroundColor Yellow
Write-Host "1. Modo Offline (Recomendado):" -ForegroundColor White
Write-Host "   Copie os binários (uxplay.exe e DLLs) para o diretório:" -ForegroundColor Gray
Write-Host "   $BinDir" -ForegroundColor Cyan
Write-Host "   e comite os arquivos no Git interno." -ForegroundColor Gray
Write-Host ""
Write-Host "2. Modo via URL Interna:" -ForegroundColor White
Write-Host "   Defina a variável de ambiente LETMESEE_BIN_URL apontando para o arquivo .zip." -ForegroundColor Gray
Write-Host "----------------------------------------------------------" -ForegroundColor Yellow
Write-Host ""
Read-Host "Pressione ENTER para fechar..."
exit 1
