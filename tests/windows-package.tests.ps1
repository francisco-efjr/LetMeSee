# Synthetic packaging fixtures only: no downloads, executables, UAC or system changes.
Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression -ErrorAction Stop
Add-Type -AssemblyName System.IO.Compression.FileSystem -ErrorAction Stop
$root = Split-Path -Parent $PSScriptRoot
$packager = Join-Path $root 'scripts/package_windows.ps1'
$fixtureRoot = Join-Path ([IO.Path]::GetTempPath()) ('letmesee-windows-package-tests-' + [Guid]::NewGuid().ToString('N'))
$source = Join-Path $fixtureRoot 'source'
$output = Join-Path $fixtureRoot 'output'
$checks = 0

function Assert-PackageTest {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) { throw $Message }
    $script:checks++
}

try {
    [void][IO.Directory]::CreateDirectory($source)
    $required = @('LetMeSee.exe', 'installer/app_icon.ico', 'iniciar.bat', 'configurar_firewall.bat', 'diagnostico.bat', 'README.md', 'SECURITY.md', 'THIRD_PARTY.md', 'docs/releases/LEIA-ME-WINDOWS.txt',
        'scripts/dependency_security.ps1', 'scripts/diagnose.ps1', 'scripts/download_deps.ps1', 'scripts/install_bonjour.ps1', 'scripts/launch_firewall.ps1', 'scripts/prepare_android.ps1', 'scripts/run.ps1', 'scripts/setup_firewall.ps1',
        'config/android-runtime.json', 'config/dependencies.json')
    foreach ($name in $required) {
        $path = Join-Path $source $name
        [void][IO.Directory]::CreateDirectory((Split-Path -Parent $path))
        [IO.File]::WriteAllText($path, ('fixture only - never executed: ' + $name))
    }
    $contaminants = @('bin/uxplay.exe', 'bin/library.dll', 'bin/third_party/Bonjour64.msi', 'engines/android/adb.exe', 'engines/android/scrcpy.exe', 'runtime.zip', 'unexpected.dll', 'unexpected.exe', 'scripts/development.ps1', 'config/secret.json')
    foreach ($name in $contaminants) {
        $path = Join-Path $source $name
        [void][IO.Directory]::CreateDirectory((Split-Path -Parent $path))
        [IO.File]::WriteAllText($path, 'must not be distributed')
    }

    $result = & $packager -Version '1.2.0' -SourceRoot $source -OutputDirectory $output
    Assert-PackageTest ($result.FileCount -eq 20) 'Unexpected public package inventory size.'
    $zip = [IO.Compression.ZipFile]::OpenRead($result.ArchivePath)
    try {
        $names = @($zip.Entries | ForEach-Object { $_.FullName })
        $expected = @($required | ForEach-Object {
            if ($_ -ceq 'installer/app_icon.ico') { 'app_icon.ico' }
            elseif ($_ -ceq 'docs/releases/LEIA-ME-WINDOWS.txt') { 'LEIA-ME-WINDOWS.txt' }
            else { $_ }
        }) + 'package-files.sha256'
        Assert-PackageTest ((@($names | Sort-Object) -join "`n") -ceq (@($expected | Sort-Object) -join "`n")) 'ZIP must contain the exact public allowlist.'
        Assert-PackageTest (@($names | Where-Object { $_ -match '(^bin/|^engines/|\.dll$|\.msi$|\.zip$|development|secret|unexpected)' }).Count -eq 0) 'Runtime or unreviewed file leaked into public package.'
        $manifestEntry = $zip.GetEntry('package-files.sha256')
        $reader = New-Object IO.StreamReader($manifestEntry.Open())
        try { $manifest = $reader.ReadToEnd() } finally { $reader.Dispose() }
        foreach ($line in @($manifest.Split("`n") | Where-Object { $_ })) {
            Assert-PackageTest ($line -match '^([0-9a-f]{64})  (.+)$') 'Malformed package checksum manifest line.'
            $expectedHash = $Matches[1]; $name = $Matches[2]
            $entry = $zip.GetEntry($name)
            $stream = $entry.Open(); $sha = [Security.Cryptography.SHA256]::Create()
            try { $actual = [BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-', '').ToLowerInvariant() }
            finally { $sha.Dispose(); $stream.Dispose() }
            Assert-PackageTest ($actual -ceq $expectedHash) ('Package manifest hash differs: ' + $name)
        }
    } finally { $zip.Dispose() }
    $actualZipHash = (Get-FileHash -LiteralPath $result.ArchivePath -Algorithm SHA256).Hash.ToLowerInvariant()
    $checksums = [IO.File]::ReadAllText($result.ChecksumPath)
    Assert-PackageTest ($checksums -ceq ($actualZipHash + '  LetMeSee-1.2.0-windows-x64-portable.zip' + "`n")) 'External ZIP checksum does not match.'
    Assert-PackageTest ($result.SHA256 -ceq $actualZipHash) 'Reported artifact hash differs.'

    $rejected = $false
    try { & $packager -Version '1.2.0' -SourceRoot $source -OutputDirectory $output | Out-Null } catch { $rejected = $true }
    Assert-PackageTest $rejected 'An existing release artifact was overwritten.'
    Remove-Item -LiteralPath (Join-Path $source 'LetMeSee.exe')
    $rejected = $false
    $missingOutput = Join-Path $fixtureRoot 'missing-output'
    try { & $packager -Version '1.2.0' -SourceRoot $source -OutputDirectory $missingOutput | Out-Null } catch { $rejected = $true }
    Assert-PackageTest ($rejected -and -not (Test-Path -LiteralPath $missingOutput)) 'Missing required source was accepted or produced output.'
    [IO.File]::WriteAllText((Join-Path $source 'LetMeSee.exe'), 'fixture restored')

    $rejected = $false
    try { & $packager -Version '../1.2.0' -SourceRoot $source -OutputDirectory (Join-Path $fixtureRoot 'invalid-version') | Out-Null } catch { $rejected = $true }
    Assert-PackageTest $rejected 'Unsafe version was accepted.'
    $rejected = $false
    try { & $packager -Version "1.2.0`n" -SourceRoot $source -OutputDirectory (Join-Path $fixtureRoot 'newline-version') | Out-Null } catch { $rejected = $true }
    Assert-PackageTest $rejected 'Version containing a newline was accepted.'

    # Directory junctions need no developer mode/admin on Windows. Elsewhere use a symlink.
    $linkedScripts = Join-Path $source 'scripts'
    $realScripts = Join-Path $fixtureRoot 'real-scripts'
    Move-Item -LiteralPath $linkedScripts -Destination $realScripts
    $linkType = if ([Environment]::OSVersion.Platform -eq [PlatformID]::Win32NT) { 'Junction' } else { 'SymbolicLink' }
    New-Item -ItemType $linkType -Path $linkedScripts -Target $realScripts | Out-Null
    $rejected = $false
    $linkedOutput = Join-Path $fixtureRoot 'linked-output'
    try { & $packager -Version '1.2.0' -SourceRoot $source -OutputDirectory $linkedOutput | Out-Null } catch { $rejected = $true }
    Assert-PackageTest ($rejected -and -not (Test-Path -LiteralPath $linkedOutput)) 'A linked source directory was packaged.'
    if ($linkType -eq 'Junction') { [IO.Directory]::Delete($linkedScripts) }
    else { Remove-Item -LiteralPath $linkedScripts -Force }
    Move-Item -LiteralPath $realScripts -Destination $linkedScripts
    Write-Host ('Windows public core package checks passed: ' + $checks + '.')
} finally {
    if (Test-Path -LiteralPath $fixtureRoot) { Remove-Item -LiteralPath $fixtureRoot -Recurse -Force }
}
