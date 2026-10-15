# Shared trust checks. Compatible with Windows PowerShell 5.1; no network or execution on import.
Set-StrictMode -Version 2.0

function Get-DependencyPin {
    param([Parameter(Mandatory = $true)][string]$Path)
    $config = Get-Content -LiteralPath $Path -Raw -ErrorAction Stop | ConvertFrom-Json -ErrorAction Stop
    $pin = $config.runtime
    if ($config.schemaVersion -ne 1 -or $pin.reviewed -isnot [bool] -or $pin.reviewed -ne $true -or $pin.sha256 -notmatch '^[0-9a-fA-F]{64}$') {
        throw 'No reviewed SHA256 pin is configured. Installation is blocked until config/dependencies.json is reviewed.'
    }
    $uri = [Uri]$pin.url
    if (-not $uri.IsAbsoluteUri -or $uri.Scheme -ne 'https' -or $uri.Host -ne 'github.com' -or
        -not $uri.IsDefaultPort -or $uri.UserInfo -or $uri.Query -or $uri.Fragment -or
        $uri.AbsolutePath -notmatch '^/francisco-efjr/LetMeSee/releases/download/[^/]+/LetMeSee-bin\.zip$') {
        throw 'The runtime URL must be an HTTPS release asset in the official LetMeSee repository.'
    }
    return $pin
}

function Assert-FileHash {
    param([Parameter(Mandatory = $true)][string]$Path, [Parameter(Mandatory = $true)][string]$Expected)
    if ($Expected -notmatch '^[0-9a-fA-F]{64}$') { throw 'A valid SHA256 pin is required.' }
    $actual = (Get-FileHash -LiteralPath $Path -Algorithm SHA256 -ErrorAction Stop).Hash
    if (-not [string]::Equals($actual, $Expected, [StringComparison]::OrdinalIgnoreCase)) {
        throw ('SHA256 mismatch: ' + [IO.Path]::GetFileName($Path) + '. Nothing was executed or installed.')
    }
}

function ConvertTo-SafeRelativePath {
    param([Parameter(Mandatory = $true)][string]$Path)
    $normalized = $Path.Replace('\', '/')
    if ([string]::IsNullOrWhiteSpace($normalized) -or $normalized.StartsWith('/') -or
        $normalized -match '[\x00-\x1f<>:"|?*]') { throw ('Unsafe archive or manifest path: ' + $Path) }
    $parts = $normalized.Split('/')
    foreach ($part in $parts) {
        if (-not $part -or $part -eq '.' -or $part -eq '..' -or $part -match '[. ]$' -or
            $part -match '^(?i:CON|PRN|AUX|NUL|CONIN\$|CONOUT\$|CLOCK\$|COM[1-9\u00b9\u00b2\u00b3]|LPT[1-9\u00b9\u00b2\u00b3])(?:\.|$)') {
            throw ('Unsafe archive or manifest path: ' + $Path)
        }
    }
    return $normalized
}

function Assert-NoReparsePath {
    param([Parameter(Mandatory = $true)][string]$Path)
    $current = [IO.Path]::GetFullPath($Path)
    while ($current) {
        if (Test-Path -LiteralPath $current) {
            $item = Get-Item -LiteralPath $current -Force -ErrorAction Stop
            if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
                throw ('Symbolic links and reparse points are not allowed: ' + $current)
            }
        }
        $parent = [IO.Directory]::GetParent($current)
        if ($null -eq $parent) { break }
        $current = $parent.FullName
    }
}

function Get-SafeChildPath {
    param([string]$Root, [string]$RelativePath)
    $relative = ConvertTo-SafeRelativePath -Path $RelativePath
    $prefix = [IO.Path]::GetFullPath($Root).TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    $full = [IO.Path]::GetFullPath((Join-Path $Root $relative.Replace('/', [IO.Path]::DirectorySeparatorChar)))
    if (-not $full.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) { throw 'Path escapes the runtime directory.' }
    return $full
}

function Expand-VerifiedArchive {
    param([Parameter(Mandatory = $true)][string]$ArchivePath, [Parameter(Mandatory = $true)][string]$DestinationPath)
    Add-Type -AssemblyName System.IO.Compression.FileSystem -ErrorAction Stop
    if (Test-Path -LiteralPath $DestinationPath) { throw 'Extraction requires a new empty staging directory.' }
    Assert-NoReparsePath -Path $DestinationPath
    $zip = [IO.Compression.ZipFile]::OpenRead($ArchivePath)
    try {
        if ($zip.Entries.Count -gt 5000) { throw 'Archive has too many entries.' }
        $known = New-Object 'System.Collections.Generic.Dictionary[string,bool]' ([StringComparer]::OrdinalIgnoreCase)
        [long]$total = 0
        # Validate every path and declared size before writing any archive content.
        foreach ($entry in $zip.Entries) {
            $directory = $entry.FullName.EndsWith('/') -or $entry.FullName.EndsWith('\')
            $name = ConvertTo-SafeRelativePath -Path $entry.FullName.TrimEnd('/', '\')
            if ($name -eq 'runtime-files.sha256') { throw 'The runtime manifest must be generated locally from the verified archive.' }
            if ($known.ContainsKey($name)) { throw ('Duplicate archive path: ' + $name) }
            $known.Add($name, $directory)
            $unixType = ($entry.ExternalAttributes -shr 16) -band 0xF000
            if ($unixType -notin @(0, 0x8000, 0x4000) -or ($entry.ExternalAttributes -band 0x400) -ne 0) {
                throw ('Links or special files are not allowed in archives: ' + $name)
            }
            $total += $entry.Length
            if ($entry.Length -gt 536870912 -or $total -gt 1073741824) { throw 'Archive exceeds the allowed expanded size.' }
            if ($entry.Length -gt 1048576 -and $entry.Length -gt (500 * [Math]::Max(1, $entry.CompressedLength))) {
                throw 'Archive exceeds the allowed compression ratio.'
            }
            [void](Get-SafeChildPath -Root $DestinationPath -RelativePath $name)
        }
        foreach ($name in $known.Keys) {
            $parent = $name
            while ($parent.LastIndexOf('/') -ge 0) {
                $parent = $parent.Substring(0, $parent.LastIndexOf('/'))
                if ($known.ContainsKey($parent) -and -not $known[$parent]) { throw ('File/directory collision: ' + $parent) }
            }
        }
        [void][IO.Directory]::CreateDirectory($DestinationPath)
        $buffer = New-Object byte[] 65536
        foreach ($entry in $zip.Entries) {
            $name = $entry.FullName.TrimEnd('/', '\')
            $target = Get-SafeChildPath -Root $DestinationPath -RelativePath $name
            if ($entry.FullName.EndsWith('/') -or $entry.FullName.EndsWith('\')) {
                [void][IO.Directory]::CreateDirectory($target)
                continue
            }
            [void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target))
            $entryStream = $entry.Open()
            $output = $null
            try {
                $output = New-Object IO.FileStream($target, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
                [long]$written = 0
                while (($count = $entryStream.Read($buffer, 0, $buffer.Length)) -gt 0) {
                    $written += $count
                    if ($written -gt $entry.Length) { throw 'Archive entry exceeds its declared size.' }
                    $output.Write($buffer, 0, $count)
                }
                if ($written -ne $entry.Length) { throw 'Archive entry is truncated.' }
            } finally {
                if ($null -ne $output) { $output.Dispose() }
                $entryStream.Dispose()
            }
        }
    } finally { $zip.Dispose() }
}

function Get-RuntimeFiles {
    param([string]$RuntimeDirectory)
    Assert-NoReparsePath -Path $RuntimeDirectory
    $pending = New-Object 'System.Collections.Generic.Queue[string]'
    $pending.Enqueue($RuntimeDirectory)
    while ($pending.Count -gt 0) {
        foreach ($item in @(Get-ChildItem -LiteralPath $pending.Dequeue() -Force -ErrorAction Stop)) {
            if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw ('Runtime contains a reparse point: ' + $item.Name) }
            if ($item.PSIsContainer) { $pending.Enqueue($item.FullName) }
            elseif ($item.FullName -ne (Join-Path $RuntimeDirectory 'runtime-files.sha256')) { $item }
        }
    }
}

function Write-RuntimeManifest {
    param([Parameter(Mandatory = $true)][string]$RuntimeDirectory)
    $root = [IO.Path]::GetFullPath($RuntimeDirectory).TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    $lines = New-Object 'System.Collections.Generic.List[string]'
    foreach ($file in @(Get-RuntimeFiles -RuntimeDirectory $RuntimeDirectory)) {
        $relative = ConvertTo-SafeRelativePath -Path $file.FullName.Substring($root.Length).Replace('\', '/')
        $lines.Add((Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256 -ErrorAction Stop).Hash.ToLowerInvariant() + '  ' + $relative)
    }
    $lines.Sort([StringComparer]::Ordinal)
    [IO.File]::WriteAllLines((Join-Path $root 'runtime-files.sha256'), $lines.ToArray(), (New-Object Text.UTF8Encoding($false)))
}

function Assert-RuntimeIntegrity {
    param([Parameter(Mandatory = $true)][string]$RuntimeDirectory, [string[]]$RequiredFiles = @('uxplay.exe'))
    $manifest = Join-Path $RuntimeDirectory 'runtime-files.sha256'
    if (-not (Test-Path -LiteralPath $manifest -PathType Leaf)) { throw 'Runtime integrity manifest is missing. Run scripts/download_deps.ps1 explicitly.' }
    Assert-NoReparsePath -Path $manifest
    $entries = New-Object 'System.Collections.Generic.Dictionary[string,string]' ([StringComparer]::OrdinalIgnoreCase)
    foreach ($line in [IO.File]::ReadAllLines($manifest)) {
        if ($line -notmatch '^(?<hash>[a-fA-F0-9]{64})  (?<path>.+)$') { throw 'Invalid runtime manifest format.' }
        $hash = $Matches.hash
        $relative = ConvertTo-SafeRelativePath -Path $Matches.path
        if ($relative -eq 'runtime-files.sha256' -or $entries.ContainsKey($relative)) { throw 'Duplicate or self-referencing runtime manifest entry.' }
        $entries.Add($relative, $hash)
    }
    foreach ($required in $RequiredFiles) {
        $safeRequired = ConvertTo-SafeRelativePath -Path $required
        if (-not $entries.ContainsKey($safeRequired)) { throw ('Runtime manifest does not include ' + $safeRequired + '.') }
    }
    $files = @(Get-RuntimeFiles -RuntimeDirectory $RuntimeDirectory)
    $prefix = [IO.Path]::GetFullPath($RuntimeDirectory).TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    foreach ($file in $files) {
        $relative = $file.FullName.Substring($prefix.Length).Replace('\', '/')
        if (-not $entries.ContainsKey($relative)) { throw ('Unverified runtime file: ' + $relative) }
    }
    foreach ($relative in $entries.Keys) {
        $target = Get-SafeChildPath -Root $RuntimeDirectory -RelativePath $relative
        Assert-NoReparsePath -Path $target
        Assert-FileHash -Path $target -Expected $entries[$relative]
    }
}

function Move-RuntimeDirectory {
    param([string]$Source, [string]$Destination)
    # Directory.Move refuses an existing destination instead of merging trees.
    [IO.Directory]::Move($Source, $Destination)
}

function Install-VerifiedRuntimeDirectory {
    param([Parameter(Mandatory = $true)][string]$Stage, [Parameter(Mandatory = $true)][string]$Destination)
    Assert-NoReparsePath -Path $Stage
    Assert-NoReparsePath -Path $Destination
    $backup = $null
    if (Test-Path -LiteralPath $Destination) {
        $backup = Join-Path ([IO.Path]::GetDirectoryName($Destination)) ('.runtime-backup-' + [Guid]::NewGuid().ToString('N'))
        Move-RuntimeDirectory -Source $Destination -Destination $backup
    }
    try { Move-RuntimeDirectory -Source $Stage -Destination $Destination }
    catch {
        $promotionError = $_.Exception.Message
        if ($backup) {
            try { Move-RuntimeDirectory -Source $backup -Destination $Destination }
            catch { throw ('Runtime promotion failed: ' + $promotionError + ' Rollback also failed. Previous runtime is preserved at: ' + $backup + '. ' + $_.Exception.Message) }
        }
        if ($backup) { throw ('Runtime promotion failed; previous runtime restored: ' + $promotionError) }
        throw ('Runtime promotion failed: ' + $promotionError)
    }
    if ($backup) {
        try { Remove-Item -LiteralPath $backup -Recurse -Force -ErrorAction Stop }
        catch { Write-Warning ('Runtime installed, but previous backup could not be removed: ' + $backup) }
    }
}
