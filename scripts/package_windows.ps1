<#
.SYNOPSIS
    Builds the public Windows core portable ZIP. No runtime download or binary execution.
.DESCRIPTION
    Only the fixed application/configuration allowlist is copied. Legacy AirPlay,
    Android engines, development tools and additional files are never packaged.
    Compatible with Windows PowerShell 5.1 and PowerShell 7.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('\A(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)(?:-[0-9A-Za-z]+(?:[.-][0-9A-Za-z]+)*)?\z')]
    [ValidateLength(5, 64)]
    [string]$Version,
    [string]$SourceRoot = (Split-Path -Parent $PSScriptRoot),
    [string]$OutputDirectory
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression -ErrorAction Stop
Add-Type -AssemblyName System.IO.Compression.FileSystem -ErrorAction Stop

function Assert-PackageSource {
    param([string]$Root, [string]$RelativePath)
    $current = $Root
    foreach ($part in @('') + $RelativePath.Split('/')) {
        if ($part) { $current = Join-Path $current $part }
        $item = Get-Item -LiteralPath $current -Force -ErrorAction Stop
        if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw ('Package source cannot be a link or reparse point: ' + $current)
        }
    }
    if (-not (Test-Path -LiteralPath $current -PathType Leaf)) {
        throw ('Required package source is not a file: ' + $current)
    }
    return $current
}

function Get-PackageStreamHash {
    param([IO.Stream]$Stream)
    $sha = [Security.Cryptography.SHA256]::Create()
    try { return [BitConverter]::ToString($sha.ComputeHash($Stream)).Replace('-', '').ToLowerInvariant() }
    finally { $sha.Dispose() }
}

$root = [IO.Path]::GetFullPath($SourceRoot)
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $root 'dist' }
$output = [IO.Path]::GetFullPath($OutputDirectory)
$archiveName = 'LetMeSee-' + $Version + '-windows-x64-portable.zip'
$archivePath = Join-Path $output $archiveName
$checksumPath = Join-Path $output 'SHA256SUMS-windows.txt'
$stage = $null
$temporaryZip = $null
$temporaryChecksum = $null

# Keep this list explicit: wildcards could accidentally redistribute a native runtime.
$mapping = @(
    @{ Source = 'LetMeSee.exe'; Target = 'LetMeSee.exe' },
    @{ Source = 'installer/app_icon.ico'; Target = 'app_icon.ico' },
    @{ Source = 'iniciar.bat'; Target = 'iniciar.bat' },
    @{ Source = 'configurar_firewall.bat'; Target = 'configurar_firewall.bat' },
    @{ Source = 'diagnostico.bat'; Target = 'diagnostico.bat' },
    @{ Source = 'README.md'; Target = 'README.md' },
    @{ Source = 'SECURITY.md'; Target = 'SECURITY.md' },
    @{ Source = 'THIRD_PARTY.md'; Target = 'THIRD_PARTY.md' },
    @{ Source = 'docs/releases/LEIA-ME-WINDOWS.txt'; Target = 'LEIA-ME-WINDOWS.txt' }
)
foreach ($name in @('dependency_security.ps1', 'diagnose.ps1', 'download_deps.ps1', 'install_bonjour.ps1', 'launch_firewall.ps1', 'prepare_android.ps1', 'run.ps1', 'setup_firewall.ps1')) {
    $mapping += @{ Source = 'scripts/' + $name; Target = 'scripts/' + $name }
}
foreach ($name in @('android-runtime.json', 'dependencies.json')) {
    $mapping += @{ Source = 'config/' + $name; Target = 'config/' + $name }
}

try {
    # Validate every required source before creating an output or replacing a prior build.
    foreach ($entry in $mapping) { $entry.Path = Assert-PackageSource -Root $root -RelativePath $entry.Source }
    if (Test-Path -LiteralPath $output) {
        $outputItem = Get-Item -LiteralPath $output -Force
        if (-not $outputItem.PSIsContainer -or ($outputItem.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw 'The package output directory must be a real directory, not a link.'
        }
    } else { [void][IO.Directory]::CreateDirectory($output) }
    foreach ($destination in @($archivePath, $checksumPath)) {
        if (Test-Path -LiteralPath $destination) { throw ('Refusing to overwrite an existing release artifact: ' + $destination) }
    }

    $nonce = [Guid]::NewGuid().ToString('N')
    $stage = Join-Path $output ('.windows-package-' + $nonce)
    [void][IO.Directory]::CreateDirectory($stage)
    $expected = New-Object 'System.Collections.Generic.Dictionary[string,string]' ([StringComparer]::Ordinal)
    foreach ($entry in @($mapping | Sort-Object Target)) {
        $source = Assert-PackageSource -Root $root -RelativePath $entry.Source
        $destination = Join-Path $stage $entry.Target
        [void][IO.Directory]::CreateDirectory((Split-Path -Parent $destination))
        [IO.File]::Copy($source, $destination, $false)
        $sourceHash = (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash.ToLowerInvariant()
        $copiedHash = (Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($sourceHash -cne $copiedHash) { throw ('Source changed while copying: ' + $entry.Source) }
        $expected.Add($entry.Target, $copiedHash)
    }
    $manifestLines = @($expected.Keys | Sort-Object | ForEach-Object { $expected[$_] + '  ' + $_ })
    $manifestText = ($manifestLines -join "`n") + "`n"
    $utf8 = New-Object Text.UTF8Encoding($false)
    $manifestPath = Join-Path $stage 'package-files.sha256'
    [IO.File]::WriteAllText($manifestPath, $manifestText, $utf8)

    $temporaryZip = Join-Path $output ('.' + $archiveName + '.' + $nonce + '.tmp')
    $zip = [IO.Compression.ZipFile]::Open($temporaryZip, [IO.Compression.ZipArchiveMode]::Create)
    try {
        foreach ($name in @(@($expected.Keys) + 'package-files.sha256' | Sort-Object)) {
            $zipEntry = $zip.CreateEntry($name, [IO.Compression.CompressionLevel]::Optimal)
            $zipEntry.LastWriteTime = [DateTimeOffset]::new(1980, 1, 1, 0, 0, 0, [TimeSpan]::Zero)
            $sourceStream = $null
            $entryStream = $null
            try {
                $sourceStream = [IO.File]::OpenRead((Join-Path $stage $name))
                $entryStream = $zipEntry.Open()
                $sourceStream.CopyTo($entryStream)
            } finally {
                try { if ($null -ne $entryStream) { $entryStream.Dispose() } }
                finally { if ($null -ne $sourceStream) { $sourceStream.Dispose() } }
            }
        }
    } finally { $zip.Dispose() }

    # Reopen the produced ZIP: exact inventory and per-entry hashes must match staging.
    $zip = [IO.Compression.ZipFile]::OpenRead($temporaryZip)
    try {
        if ($zip.Entries.Count -ne $expected.Count + 1) { throw 'Package ZIP inventory count mismatch.' }
        $seen = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::Ordinal)
        foreach ($zipEntry in $zip.Entries) {
            $name = $zipEntry.FullName
            if (-not $seen.Add($name)) { throw ('Duplicate package ZIP entry: ' + $name) }
            $stream = $zipEntry.Open()
            try {
                if ($name -ceq 'package-files.sha256') {
                    $reader = New-Object IO.StreamReader($stream, $utf8, $true)
                    try { if ($reader.ReadToEnd() -cne $manifestText) { throw 'Package manifest content mismatch.' } }
                    finally { $reader.Dispose() }
                } else {
                    if (-not $expected.ContainsKey($name)) { throw ('Unexpected package ZIP entry: ' + $name) }
                    if ((Get-PackageStreamHash -Stream $stream) -cne $expected[$name]) { throw ('Package ZIP checksum mismatch: ' + $name) }
                }
            } finally { $stream.Dispose() }
        }
    } finally { $zip.Dispose() }

    $archiveHash = (Get-FileHash -LiteralPath $temporaryZip -Algorithm SHA256).Hash.ToLowerInvariant()
    $checksumText = $archiveHash + '  ' + $archiveName + "`n"
    $temporaryChecksum = Join-Path $output ('.SHA256SUMS-windows.' + $nonce + '.tmp')
    [IO.File]::WriteAllText($temporaryChecksum, $checksumText, $utf8)
    if ([IO.File]::ReadAllText($temporaryChecksum, $utf8) -cne $checksumText) { throw 'Package checksum file validation failed.' }
    [IO.File]::Move($temporaryZip, $archivePath)
    $temporaryZip = $null
    [IO.File]::Move($temporaryChecksum, $checksumPath)
    $temporaryChecksum = $null
    Write-Host ('Verified public core portable package: ' + $archivePath)
    [PSCustomObject]@{ ArchivePath = $archivePath; ChecksumPath = $checksumPath; SHA256 = $archiveHash; FileCount = $expected.Count + 1 }
} finally {
    foreach ($temporaryFile in @($temporaryZip, $temporaryChecksum)) {
        if ($temporaryFile -and (Test-Path -LiteralPath $temporaryFile)) { Remove-Item -LiteralPath $temporaryFile -Force }
    }
    if ($stage -and (Test-Path -LiteralPath $stage)) { Remove-Item -LiteralPath $stage -Recurse -Force }
}
