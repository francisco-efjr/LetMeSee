<# Explicit Android runtime preparation. Downloads data only; never runs adb/scrcpy or an installer. #>
[CmdletBinding()]
param([string]$ArchivePath, [string]$DestinationDirectory)
Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'dependency_security.ps1')
$stagingRoot = $null
try {
    $root = Split-Path -Parent $PSScriptRoot
    $pin = Get-Content -LiteralPath (Join-Path $root 'config/android-runtime.json') -Raw | ConvertFrom-Json
    $expectedUrl = 'https://github.com/Genymobile/scrcpy/releases/download/v5.0.1/scrcpy-win64-v5.0.1.zip'
    if ($pin.schemaVersion -ne 1 -or $pin.version -ne '5.0.1' -or $pin.url -cne $expectedUrl -or
        $pin.archiveRoot -ne 'scrcpy-win64-v5.0.1' -or $pin.sha256 -notmatch '^[a-fA-F0-9]{64}$') {
        throw 'Android runtime origin/version must match the reviewed official pin.'
    }
    if (-not $DestinationDirectory) {
        $local = [Environment]::GetFolderPath('LocalApplicationData')
        if (-not $local) { throw 'User application data directory is unavailable.' }
        $DestinationDirectory = Join-Path $local 'LetMeSee/engines/android'
    }
    $destination = [IO.Path]::GetFullPath($DestinationDirectory)
    Assert-NoReparsePath -Path $destination
    $parent = [IO.Path]::GetDirectoryName($destination)
    [void][IO.Directory]::CreateDirectory($parent)
    $stagingRoot = Join-Path $parent ('.runtime-staging-' + [Guid]::NewGuid().ToString('N'))
    [void][IO.Directory]::CreateDirectory($stagingRoot)
    $archive = Join-Path $stagingRoot 'android.zip'
    if ($ArchivePath) { Copy-Item -LiteralPath $ArchivePath -Destination $archive }
    else {
        [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
        Write-Host ('Downloading official scrcpy ' + $pin.version + ' (about 11 MB)...')
        Invoke-WebRequest -UseBasicParsing -Uri $pin.url -OutFile $archive -MaximumRedirection 5 -TimeoutSec 120
    }
    Assert-FileHash -Path $archive -Expected $pin.sha256
    $extracted = Join-Path $stagingRoot 'extracted'
    Expand-VerifiedArchive -ArchivePath $archive -DestinationPath $extracted
    $stage = Get-SafeChildPath -Root $extracted -RelativePath $pin.archiveRoot
    foreach ($required in @('scrcpy.exe', 'adb.exe', 'scrcpy-server')) {
        if (-not (Test-Path -LiteralPath (Join-Path $stage $required) -PathType Leaf)) { throw ('Missing required Android component: ' + $required) }
    }
    Write-RuntimeManifest -RuntimeDirectory $stage
    Assert-RuntimeIntegrity -RuntimeDirectory $stage -RequiredFiles @('scrcpy.exe', 'adb.exe', 'scrcpy-server')
    Install-VerifiedRuntimeDirectory -Stage $stage -Destination $destination
    Write-Host 'Android runtime prepared and verified. No device was accessed, no debugging mode enabled, and no firewall rule changed.'
    exit 0
} catch {
    Write-Error ('Android preparation failed: ' + $_.Exception.Message) -ErrorAction Continue
    exit 1
} finally {
    if ($stagingRoot -and (Test-Path -LiteralPath $stagingRoot)) { Remove-Item -LiteralPath $stagingRoot -Recurse -Force }
}
