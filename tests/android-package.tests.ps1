# No downloads, binary execution, service changes or elevation.
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2.0
$root = Split-Path -Parent $PSScriptRoot
. (Join-Path $root 'scripts/dependency_security.ps1')
$directory = Join-Path $PSScriptRoot ('.android-package-tests-' + [Guid]::NewGuid().ToString('N'))
try {
    [void][IO.Directory]::CreateDirectory($directory)
    foreach ($file in @('adb.exe', 'scrcpy.exe', 'scrcpy-server', 'library.dll')) { [IO.File]::WriteAllText((Join-Path $directory $file), 'fixture only, never executed') }
    Write-RuntimeManifest -RuntimeDirectory $directory
    Assert-RuntimeIntegrity -RuntimeDirectory $directory -RequiredFiles @('adb.exe', 'scrcpy.exe', 'scrcpy-server')
    $legacyRejected = $false
    try { Assert-RuntimeIntegrity -RuntimeDirectory $directory } catch { $legacyRejected = $true }
    if (-not $legacyRejected) { throw 'Default AirPlay requirements were weakened.' }
    [IO.File]::AppendAllText((Join-Path $directory 'scrcpy-server'), 'changed')
    $serverRejected = $false
    try { Assert-RuntimeIntegrity -RuntimeDirectory $directory -RequiredFiles @('adb.exe', 'scrcpy.exe', 'scrcpy-server') } catch { $serverRejected = $true }
    if (-not $serverRejected) { throw 'Changed Android server was accepted.' }
    $pin = Get-Content -LiteralPath (Join-Path $root 'config/android-runtime.json') -Raw | ConvertFrom-Json
    if ($pin.url -cne 'https://github.com/Genymobile/scrcpy/releases/download/v5.0.1/scrcpy-win64-v5.0.1.zip' -or
        $pin.sha256 -cne 'b12a2c4ee8be317422451fc7dcf8ee20a71b5ea7ef9ad73ddd825a25316227a5') { throw 'Reviewed Android source/hash drifted without updating this verification.' }
    Write-Host 'Android package checks passed: 4 (complete inventory, default AirPlay isolation, Android server tamper, reviewed source/hash).'
} finally { if (Test-Path -LiteralPath $directory) { Remove-Item -LiteralPath $directory -Recurse -Force } }
