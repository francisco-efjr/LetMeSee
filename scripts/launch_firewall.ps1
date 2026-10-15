[CmdletBinding()]
param([switch]$Remove)
Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'
try {
    $powershell = Join-Path ([Environment]::GetFolderPath('Windows')) 'System32\WindowsPowerShell\v1.0\powershell.exe'
    $script = Join-Path $PSScriptRoot 'setup_firewall.ps1'
    $arguments = '-NoProfile -NonInteractive -ExecutionPolicy Bypass -File "' + $script + '"'
    if ($Remove) { $arguments += ' -Remove' }
    $child = Start-Process -FilePath $powershell -ArgumentList $arguments -Verb RunAs -Wait -PassThru -ErrorAction Stop
    exit $child.ExitCode
} catch {
    Write-Error ('Firewall permission was declined or the command failed: ' + $_.Exception.Message) -ErrorAction Continue
    exit 1
}
