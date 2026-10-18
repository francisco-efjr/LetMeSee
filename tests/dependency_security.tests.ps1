# Self-contained security regression tests. No Pester download, elevation, network, or executable launch.
[CmdletBinding()]
param()
Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'
. (Join-Path (Split-Path -Parent $PSScriptRoot) 'scripts/dependency_security.ps1')
Add-Type -AssemblyName System.IO.Compression -ErrorAction Stop
Add-Type -AssemblyName System.IO.Compression.FileSystem
$script:passed = 0
$testRoot = Join-Path $PSScriptRoot ('.security-tests-' + [Guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($testRoot)

function Assert-Throws {
    param([scriptblock]$Action)
    $caught = $false
    try { & $Action | Out-Null } catch { $caught = $true }
    if (-not $caught) { throw 'Expected the operation to reject this input.' }
}

function Test-Case {
    param([string]$Name, [scriptblock]$Action)
    & $Action
    $script:passed++
    Write-Host ('PASS ' + $Name)
}

function New-TestArchive {
    param(
        [string[]]$Names,
        [string]$Content = 'test content',
        [switch]$SymbolicLink,
        [string]$Path = (Join-Path $testRoot ([Guid]::NewGuid().ToString('N') + '.zip')),
        [scriptblock]$ArchiveFactory = {
            param($FileStream)
            New-Object IO.Compression.ZipArchive($FileStream, [IO.Compression.ZipArchiveMode]::Create, $false)
        }
    )
    $stream = $null
    $archive = $null
    try {
        $stream = [IO.File]::Open($Path, [IO.FileMode]::CreateNew)
        $archive = & $ArchiveFactory $stream
        foreach ($name in $Names) {
            $entry = $archive.CreateEntry($name)
            if ($SymbolicLink) { $entry.ExternalAttributes = -1610612736 } # Unix symlink mode: 0xA0000000
            $entryStream = $entry.Open()
            try {
                $bytes = [Text.Encoding]::ASCII.GetBytes($Content)
                $entryStream.Write($bytes, 0, $bytes.Length)
            } finally { $entryStream.Dispose() }
        }
    } finally {
        try { if ($null -ne $archive) { $archive.Dispose() } }
        finally { if ($null -ne $stream) { $stream.Dispose() } }
    }
    return $Path
}

function Assert-FileReleased {
    param([string]$Path)
    # FileShare.None fails on Windows if a previous reader/writer was leaked.
    $stream = [IO.File]::Open($Path, [IO.FileMode]::Open, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
    try { if (-not $stream.CanRead) { throw 'Unable to reopen the archive.' } }
    finally { $stream.Dispose() }
}

function New-TestRuntime {
    $runtime = Join-Path $testRoot ([Guid]::NewGuid().ToString('N'))
    [void][IO.Directory]::CreateDirectory((Join-Path $runtime 'lib/gstreamer-1.0'))
    [IO.File]::WriteAllText((Join-Path $runtime 'uxplay.exe'), 'not executed')
    [IO.File]::WriteAllText((Join-Path $runtime 'lib/gstreamer-1.0/plugin.dll'), 'not loaded')
    Write-RuntimeManifest -RuntimeDirectory $runtime
    return $runtime
}

try {
    Test-Case 'All PowerShell sources parse' {
        foreach ($file in @(Get-ChildItem -LiteralPath (Split-Path -Parent $PSScriptRoot) -Filter '*.ps1' -Recurse)) {
            $tokens = $null
            $parseErrors = $null
            [void][Management.Automation.Language.Parser]::ParseFile($file.FullName, [ref]$tokens, [ref]$parseErrors)
            if ($parseErrors.Count -ne 0) { throw ($file.Name + ': ' + ($parseErrors | Out-String)) }
        }
    }
    Test-Case 'Reviewed official pin loads' {
        $pin = Get-DependencyPin -Path (Join-Path (Split-Path -Parent $PSScriptRoot) 'config/dependencies.json')
        if ($pin.sha256.Length -ne 64) { throw 'Pin was not loaded.' }
    }
    Test-Case 'Unreviewed and missing pins fail closed' {
        foreach ($json in @('{"schemaVersion":1,"runtime":{"reviewed":false,"sha256":"a"}}', '{"schemaVersion":1,"runtime":{}}')) {
            $path = Join-Path $testRoot 'invalid-pin.json'
            [IO.File]::WriteAllText($path, $json)
            Assert-Throws { Get-DependencyPin -Path $path }
        }
    }
    Test-Case 'Arbitrary dependency origin is rejected' {
        $path = Join-Path $testRoot 'origin.json'
        $json = '{"schemaVersion":1,"runtime":{"reviewed":true,"sha256":"' + ('a' * 64) + '","url":"https://example.com/runtime.zip"}}'
        [IO.File]::WriteAllText($path, $json)
        Assert-Throws { Get-DependencyPin -Path $path }
    }
    Test-Case 'SHA256 matches valid data and rejects changed data' {
        $path = Join-Path $testRoot 'hash.txt'
        [IO.File]::WriteAllText($path, 'original')
        $hash = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
        Assert-FileHash -Path $path -Expected $hash
        [IO.File]::WriteAllText($path, 'changed')
        Assert-Throws { Assert-FileHash -Path $path -Expected $hash }
        Assert-Throws { Assert-FileHash -Path $path -Expected '' }
    }
    Test-Case 'ZIP fixture releases its file if archive construction fails' {
        $path = Join-Path $testRoot 'constructor-failure.zip'
        $message = $null
        try {
            New-TestArchive -Names @('uxplay.exe') -Path $path -ArchiveFactory { throw 'Simulated archive constructor failure.' }
        } catch { $message = $_.Exception.Message }
        if ($message -ne 'Simulated archive constructor failure.') { throw 'Archive construction did not preserve the original failure.' }
        Assert-FileReleased -Path $path
    }
    Test-Case 'Safe ZIP extracts into staging and verifies complete manifest' {
        $zip = New-TestArchive -Names @('uxplay.exe', 'lib/gstreamer-1.0/plugin.dll')
        $destination = Join-Path $testRoot 'good-extraction'
        Assert-FileReleased -Path $zip
        Expand-VerifiedArchive -ArchivePath $zip -DestinationPath $destination
        Assert-FileReleased -Path $zip
        Write-RuntimeManifest -RuntimeDirectory $destination
        Assert-RuntimeIntegrity -RuntimeDirectory $destination
        if ((Get-Content -LiteralPath (Join-Path $destination 'runtime-files.sha256')).Count -ne 2) { throw 'Manifest coverage is incomplete.' }
    }
    Test-Case 'ZIP traversal, absolute paths, ADS and Windows path aliases are rejected before extraction' {
        foreach ($name in @('../escape.exe', '..\escape.exe', '/absolute.exe', 'C:\escape.exe', '\\server\share\x.exe', 'dir/../../x.exe',
            'dir/file.dll:stream', 'NUL.dll', 'con.txt', 'COM1.exe', 'dir/LPT9.dll', 'dir./x.dll', 'dir /x.dll', 'dir//x.dll')) {
            $zip = New-TestArchive -Names @('uxplay.exe', $name)
            $destination = Join-Path $testRoot ([Guid]::NewGuid().ToString('N'))
            Assert-Throws { Expand-VerifiedArchive -ArchivePath $zip -DestinationPath $destination }
            Assert-FileReleased -Path $zip
            if (Test-Path -LiteralPath $destination) { throw ('Content was extracted before validation: ' + $name) }
        }
    }
    Test-Case 'ZIP duplicate paths are rejected case-insensitively' {
        $zip = New-TestArchive -Names @('uxplay.exe', 'UXPLAY.EXE')
        Assert-Throws { Expand-VerifiedArchive -ArchivePath $zip -DestinationPath (Join-Path $testRoot 'duplicate') }
        Assert-FileReleased -Path $zip
    }
    Test-Case 'ZIP file/directory collisions and supplied manifests are rejected' {
        foreach ($names in @(@('uxplay.exe', 'lib', 'lib/plugin.dll'), @('uxplay.exe', 'runtime-files.sha256'))) {
            $zip = New-TestArchive -Names $names
            Assert-Throws { Expand-VerifiedArchive -ArchivePath $zip -DestinationPath (Join-Path $testRoot ([Guid]::NewGuid().ToString('N'))) }
        }
    }
    Test-Case 'ZIP symbolic links and excessive compression are rejected' {
        $zip = New-TestArchive -Names @('uxplay.exe') -SymbolicLink
        Assert-Throws { Expand-VerifiedArchive -ArchivePath $zip -DestinationPath (Join-Path $testRoot 'symlink') }
        $zip = New-TestArchive -Names @('uxplay.exe') -Content ('A' * 2097152)
        Assert-Throws { Expand-VerifiedArchive -ArchivePath $zip -DestinationPath (Join-Path $testRoot 'bomb') }
    }
    Test-Case 'Runtime modified executable, nested plugin and deleted file are rejected' {
        foreach ($relative in @('uxplay.exe', 'lib/gstreamer-1.0/plugin.dll')) {
            $runtime = New-TestRuntime
            [IO.File]::WriteAllText((Join-Path $runtime $relative), 'modified')
            Assert-Throws { Assert-RuntimeIntegrity -RuntimeDirectory $runtime }
        }
        $runtime = New-TestRuntime
        Remove-Item -LiteralPath (Join-Path $runtime 'uxplay.exe')
        Assert-Throws { Assert-RuntimeIntegrity -RuntimeDirectory $runtime }
    }
    Test-Case 'Runtime extra DLL, missing manifest and manifest traversal are rejected' {
        $runtime = New-TestRuntime
        [IO.File]::WriteAllText((Join-Path $runtime 'injected.dll'), 'not loaded')
        Assert-Throws { Assert-RuntimeIntegrity -RuntimeDirectory $runtime }
        Remove-Item -LiteralPath (Join-Path $runtime 'injected.dll')
        [IO.File]::AppendAllText((Join-Path $runtime 'runtime-files.sha256'), ('a' * 64) + '  ../escape.exe' + [Environment]::NewLine)
        Assert-Throws { Assert-RuntimeIntegrity -RuntimeDirectory $runtime }
        Remove-Item -LiteralPath (Join-Path $runtime 'runtime-files.sha256')
        Assert-Throws { Assert-RuntimeIntegrity -RuntimeDirectory $runtime }
    }
    Test-Case 'Duplicate and invalid runtime manifest lines are rejected' {
        $runtime = New-TestRuntime
        $manifest = Join-Path $runtime 'runtime-files.sha256'
        $first = [IO.File]::ReadAllLines($manifest)[0]
        [IO.File]::AppendAllText($manifest, $first + [Environment]::NewLine)
        Assert-Throws { Assert-RuntimeIntegrity -RuntimeDirectory $runtime }
        [IO.File]::WriteAllText($manifest, 'not a hash  uxplay.exe')
        Assert-Throws { Assert-RuntimeIntegrity -RuntimeDirectory $runtime }
    }
    Test-Case 'Runtime promotion replaces the complete directory instead of merging' {
        $stage = New-TestRuntime
        $destination = New-TestRuntime
        [IO.File]::WriteAllText((Join-Path $destination 'obsolete.dll'), 'old runtime')
        Install-VerifiedRuntimeDirectory -Stage $stage -Destination $destination
        Assert-RuntimeIntegrity -RuntimeDirectory $destination
        if (Test-Path -LiteralPath (Join-Path $destination 'obsolete.dll')) { throw 'Runtime trees were merged.' }
        if (Test-Path -LiteralPath $stage) { throw 'Staging directory was not promoted.' }
    }
    Test-Case 'Promotion failure restores the previous runtime' {
        $stage = New-TestRuntime
        $destination = New-TestRuntime
        [IO.File]::WriteAllText((Join-Path $destination 'original.txt'), 'previous runtime')
        $script:moveCount = 0
        function Move-RuntimeDirectory {
            param([string]$Source, [string]$Destination)
            $script:moveCount++
            if ($script:moveCount -eq 2) { throw 'Simulated antivirus lock on promotion.' }
            [IO.Directory]::Move($Source, $Destination)
        }
        Assert-Throws { Install-VerifiedRuntimeDirectory -Stage $stage -Destination $destination }
        if (-not (Test-Path -LiteralPath (Join-Path $destination 'original.txt'))) { throw 'Previous runtime was not restored.' }
        if (-not (Test-Path -LiteralPath $stage)) { throw 'Failed promotion removed staging unexpectedly.' }
    }
    Test-Case 'Promotion and rollback failure preserve the only previous runtime backup' {
        $stage = New-TestRuntime
        $destination = New-TestRuntime
        [IO.File]::WriteAllText((Join-Path $destination 'original.txt'), 'previous runtime')
        $script:moveCount = 0
        function Move-RuntimeDirectory {
            param([string]$Source, [string]$Destination)
            $script:moveCount++
            if ($script:moveCount -gt 1) { throw 'Simulated antivirus locks on promotion and rollback.' }
            [IO.Directory]::Move($Source, $Destination)
        }
        Assert-Throws { Install-VerifiedRuntimeDirectory -Stage $stage -Destination $destination }
        $backups = @(Get-ChildItem -LiteralPath $testRoot -Directory -Force -Filter '.runtime-backup-*')
        if ($backups.Count -ne 1 -or -not (Test-Path -LiteralPath (Join-Path $backups[0].FullName 'original.txt'))) {
            throw 'The previous runtime backup was lost after failed rollback.'
        }
    }
    Write-Host ('Security checks passed: ' + $script:passed)
    exit 0
} finally {
    if (Test-Path -LiteralPath $testRoot) { Remove-Item -LiteralPath $testRoot -Recurse -Force }
}
