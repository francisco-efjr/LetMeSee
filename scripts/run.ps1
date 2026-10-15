<#
.SYNOPSIS
    Starts the compiled GUI and its session security policies. No implicit downloads.
#>
[CmdletBinding()]
param()
Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'
try {
    $application = Join-Path (Split-Path -Parent $PSScriptRoot) 'LetMeSee.exe'
    if (-not (Test-Path -LiteralPath $application -PathType Leaf)) {
        throw 'LetMeSee.exe is missing. Run build_ui.bat from source, or use the verified installer.'
    }
    $process = Start-Process -FilePath $application -WorkingDirectory (Split-Path -Parent $application) -Wait -PassThru -ErrorAction Stop
    exit $process.ExitCode
} catch {
    Write-Error ('Cannot start LetMeSee: ' + $_.Exception.Message) -ErrorAction Continue
    exit 1
}
