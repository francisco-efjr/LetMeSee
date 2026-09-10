<#
.SYNOPSIS
    Inicia o receptor AirPlay (UxPlay) com nome personalizado e suporte a Direct3D.
.DESCRIPTION
    Script principal de execução do LetMeSee para estações Windows.
#>

[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

$ScriptDir = Split-Path -Parent $PSScriptRoot
$BinDir = Join-Path $ScriptDir "bin"
$UxPlayExe = Join-Path $BinDir "uxplay.exe"
$DownloadScript = Join-Path $PSScriptRoot "download_deps.ps1"

# 1. Validar disponibilidade dos binários
if (-not (Test-Path $UxPlayExe)) {
    & $DownloadScript
    if (-not (Test-Path $UxPlayExe)) {
        Write-Host "[ERRO] Não foi possível iniciar: dependências ausentes." -ForegroundColor Red
        Read-Host "Pressione ENTER para fechar..."
        exit 1
    }
}

# 2. Identificar usuário e nome do dispositivo
$Usuario = $env:USERNAME
if ([string]::IsNullOrWhiteSpace($Usuario)) {
    $Usuario = $env:COMPUTERNAME
}
$DeviceName = "LetMeSee - $Usuario"
$Host.UI.RawUI.WindowTitle = "LetMeSee: $DeviceName (Aguardando Conexao)"

# 3. Identificar endereços IP locais
$IPs = Get-NetIPAddress -AddressFamily IPv4 -ErrorAction SilentlyContinue | 
       Where-Object { $_.IPAddress -notlike "127.*" -and $_.IPAddress -notlike "169.254.*" } | 
       Select-Object -ExpandProperty IPAddress

Clear-Host
Write-Host "=================================================================" -ForegroundColor Cyan
Write-Host "            LETMESEE - RECEPTOR DE TELA AIRPLAY                  " -ForegroundColor Green
Write-Host "=================================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host " Identificador no iPhone:         " -NoNewline
Write-Host "[$DeviceName]" -ForegroundColor Yellow
Write-Host " Endereço(s) IP deste computador: " -NoNewline
Write-Host ($IPs -join ", ") -ForegroundColor White
Write-Host ""
Write-Host "-----------------------------------------------------------------" -ForegroundColor Gray
Write-Host " INSTRUÇÕES DE CONEXÃO (NO IPHONE / IPAD):                       " -ForegroundColor Yellow
Write-Host "-----------------------------------------------------------------" -ForegroundColor Gray
Write-Host " 1. Conecte o iPhone na mesma rede Wi-Fi deste computador." -ForegroundColor White
Write-Host " 2. Deslize a partir do canto superior direito para abrir a Central de Controle." -ForegroundColor Gray
Write-Host " 3. Toque no ícone [Espelhar a Tela]." -ForegroundColor White
Write-Host " 4. Selecione o dispositivo: '$DeviceName'." -ForegroundColor Yellow
Write-Host "-----------------------------------------------------------------" -ForegroundColor Gray
Write-Host " Caso o dispositivo não apareça, execute 'configurar_firewall.bat'." -ForegroundColor DarkYellow
Write-Host " Para encerrar, feche a janela ou pressione Ctrl+C." -ForegroundColor DarkGray
Write-Host "=================================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Iniciando serviço de recepção..." -ForegroundColor Green
Write-Host ""

# Adicionar pasta bin ao PATH da sessão para carregamento de DLLs
$env:PATH = "$BinDir;$env:PATH"

try {
    # Argumentos do UxPlay:
    # -n: Nome do dispositivo
    # -p: Portas de rede padrão
    # -vs d3d11videosink: Renderizador Direct3D 11 nativo de baixo consumo
    $argsList = @("-n", $DeviceName, "-p", "-vs", "d3d11videosink")
    Start-Process -FilePath $UxPlayExe -ArgumentList $argsList -NoNewWindow -Wait
} catch {
    Write-Host "[ERRO na execução]: $_" -ForegroundColor Red
    Read-Host "Pressione ENTER para fechar..."
}
