[CmdletBinding()]
param()
Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'dependency_security.ps1')
$problems = 0
Write-Host 'LetMeSee - local diagnostics (no configuration changes)'
try {
    Assert-RuntimeIntegrity -RuntimeDirectory (Join-Path (Split-Path -Parent $PSScriptRoot) 'bin')
    Write-Host '[OK] Runtime integrity verified.'
} catch { $problems++; Write-Warning $_.Exception.Message }
try {
    Get-NetConnectionProfile -ErrorAction Stop | Select-Object InterfaceAlias, NetworkCategory | Format-Table -AutoSize
    Get-NetIPAddress -AddressFamily IPv4 -ErrorAction Stop |
        Where-Object { $_.IPAddress -notlike '127.*' -and $_.IPAddress -notlike '169.254.*' } |
        Select-Object InterfaceAlias, IPAddress | Format-Table -AutoSize
    $rules = @(Get-NetFirewallRule -ErrorAction Stop | Where-Object { $_.Group -eq 'LetMeSee' })
    if ($rules.Count -eq 0) { $problems++; Write-Warning 'No LetMeSee rules. Use configurar_firewall.bat only on a trusted Private network.' }
    else { $rules | Select-Object DisplayName, Enabled, Profile, Direction, Action | Format-Table -AutoSize }
    $bonjour = Get-Service -Name 'Bonjour Service' -ErrorAction SilentlyContinue
    if ($null -eq $bonjour) { $problems++; Write-Warning 'Bonjour is not installed. Discovery may be unavailable.' }
    else { $bonjour | Select-Object Name, Status | Format-Table -AutoSize }
} catch { $problems++; Write-Warning ('Network diagnostics failed: ' + $_.Exception.Message) }
Write-Host 'Use the same trusted local network. Guest Wi-Fi or VPN isolation may block discovery.'
if ($problems -gt 0) { exit 1 }
exit 0
