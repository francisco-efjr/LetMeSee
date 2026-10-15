<#
.SYNOPSIS
    Downloads an explicitly requested, pinned runtime. Never runs its installers.
#>
[CmdletBinding()]
param([string]$ArchivePath)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'dependency_security.ps1')

$root = Split-Path -Parent $PSScriptRoot
$temporaryRoot = $null
$destination = Join-Path $root 'bin'
try {
    if (-not [string]::IsNullOrWhiteSpace($env:LETMESEE_BIN_URL)) {
        throw 'LETMESEE_BIN_URL is not supported. Runtime sources and SHA256 must be reviewed in config/dependencies.json.'
    }
    $pin = Get-DependencyPin -Path (Join-Path $root 'config/dependencies.json')
    Assert-NoReparsePath -Path $root
    if (Test-Path -LiteralPath $destination) { Assert-NoReparsePath -Path $destination }
    $temporaryRoot = Join-Path $root ('.runtime-staging-' + [Guid]::NewGuid().ToString('N'))
    [void][IO.Directory]::CreateDirectory($temporaryRoot)
    $zipPath = Join-Path $temporaryRoot 'runtime.zip'
    if ($ArchivePath) {
        Copy-Item -LiteralPath $ArchivePath -Destination $zipPath
    } else {
        Write-Host ('Downloading pinned runtime from ' + $pin.url)
        [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
        Invoke-WebRequest -Uri $pin.url -OutFile $zipPath -UseBasicParsing -MaximumRedirection 5 -TimeoutSec 120
    }
    Assert-FileHash -Path $zipPath -Expected $pin.sha256
    $stage = Join-Path $temporaryRoot 'bin'
    Expand-VerifiedArchive -ArchivePath $zipPath -DestinationPath $stage
    if (-not (Test-Path -LiteralPath (Join-Path $stage 'uxplay.exe') -PathType Leaf)) {
        throw 'The verified archive does not contain uxplay.exe at its root.'
    }
    Write-RuntimeManifest -RuntimeDirectory $stage
    Assert-RuntimeIntegrity -RuntimeDirectory $stage

    # A complete verified directory replaces the previous runtime; never merge.
    Install-VerifiedRuntimeDirectory -Stage $stage -Destination $destination
    Write-Host 'Runtime verified and installed. No MSI was executed and no firewall rule was changed.'
    Write-Host 'Bonjour, if required, must be obtained and installed separately from its trusted publisher.'
    exit 0
} catch {
    Write-Error ('Runtime installation failed: ' + $_.Exception.Message) -ErrorAction Continue
    exit 1
} finally {
    if ($temporaryRoot -and (Test-Path -LiteralPath $temporaryRoot)) { Remove-Item -LiteralPath $temporaryRoot -Recurse -Force }
}
