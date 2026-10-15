<#
.SYNOPSIS
    Opt-in inbound access for a verified runtime on private local networks only.
#>
[CmdletBinding()]
param([switch]$Remove)
Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'dependency_security.ps1')

$ruleNames = @('LetMeSee-AirPlay-TCP', 'LetMeSee-AirPlay-UDP', 'LetMeSee-Bonjour-mDNS')
$legacyNames = @(
    'LetMeSee (App)', 'LetMeSee (UxPlay App)', 'LetMeSee (mDNS 5353)', 'LetMeSee (AirPlay TCP)', 'LetMeSee (AirPlay UDP)',
    'LetMeSeeYourScreen (UxPlay App)', 'LetMeSeeYourScreen (mDNS 5353)', 'LetMeSeeYourScreen (AirPlay TCP)', 'LetMeSeeYourScreen (AirPlay UDP)',
    'Espelho iOS (UxPlay App)', 'Espelho iOS (mDNS 5353)', 'Espelho iOS (AirPlay TCP)', 'Espelho iOS (AirPlay UDP)'
)

function Remove-LetMeSeeRules {
    # Enumerate once so absence is harmless and actual access/command errors remain errors.
    $existing = @(Get-NetFirewallRule -ErrorAction Stop)
    foreach ($rule in $existing) {
        if ($ruleNames -contains $rule.Name -or $legacyNames -contains $rule.DisplayName) {
            $rule | Remove-NetFirewallRule -ErrorAction Stop
        }
    }
}

try {
    $admin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
    if (-not $admin) { throw 'Administrator permission is required. Run configurar_firewall.bat and accept the Windows prompt.' }
    if ($Remove) {
        Remove-LetMeSeeRules
        Write-Host 'LetMeSee firewall rules removed.'
        exit 0
    }
    $runtime = Join-Path (Split-Path -Parent $PSScriptRoot) 'bin'
    Assert-RuntimeIntegrity -RuntimeDirectory $runtime
    $uxplay = [IO.Path]::GetFullPath((Join-Path $runtime 'uxplay.exe'))
    Remove-LetMeSeeRules
    try {
        New-NetFirewallRule -Name $ruleNames[0] -DisplayName 'LetMeSee - AirPlay TCP (private local network)' -Group 'LetMeSee' `
            -Direction Inbound -Action Allow -Enabled True -Profile Private -RemoteAddress LocalSubnet `
            -Program $uxplay -Protocol TCP -LocalPort 7000,7001,7100 -EdgeTraversalPolicy Block -ErrorAction Stop | Out-Null
        New-NetFirewallRule -Name $ruleNames[1] -DisplayName 'LetMeSee - AirPlay UDP (private local network)' -Group 'LetMeSee' `
            -Direction Inbound -Action Allow -Enabled True -Profile Private -RemoteAddress LocalSubnet `
            -Program $uxplay -Protocol UDP -LocalPort 6000,6001,7011 -EdgeTraversalPolicy Block -ErrorAction Stop | Out-Null

        # Bonjour is never installed here. Only a valid Apple-signed existing service gets a scoped mDNS rule.
        $bonjour = Get-CimInstance -ClassName Win32_Service -Filter "Name='Bonjour Service'" -ErrorAction Stop
        if ($null -ne $bonjour) {
            $bonjourExe = $null
            if ($bonjour.PathName -match '^"([^"]+\.exe)"(?:\s|$)') { $bonjourExe = $Matches[1] }
            elseif ($bonjour.PathName -match '^([^\s]+\.exe)(?:\s|$)') { $bonjourExe = $Matches[1] }
            if ($bonjourExe -and (Test-Path -LiteralPath $bonjourExe -PathType Leaf)) {
                Assert-NoReparsePath -Path $bonjourExe
                $signature = Get-AuthenticodeSignature -LiteralPath $bonjourExe -ErrorAction Stop
                if ($signature.Status -eq 'Valid' -and $signature.SignerCertificate.Subject -match '(?:^|,\s*)O=Apple Inc\.?(?:,|$)') {
                    New-NetFirewallRule -Name $ruleNames[2] -DisplayName 'LetMeSee - Bonjour mDNS (private local network)' -Group 'LetMeSee' `
                        -Direction Inbound -Action Allow -Enabled True -Profile Private -RemoteAddress LocalSubnet `
                        -Program $bonjourExe -Protocol UDP -LocalPort 5353 -EdgeTraversalPolicy Block -ErrorAction Stop | Out-Null
                } else { Write-Warning 'Bonjour signature could not be validated as Apple. No mDNS rule was added.' }
            } else { Write-Warning 'Bonjour service has an unexpected executable path. No mDNS rule was added.' }
        } else { Write-Warning 'Bonjour is not installed. Obtain it separately from its trusted publisher if discovery is unavailable.' }
    } catch {
        Remove-LetMeSeeRules
        throw
    }
    Write-Host 'Firewall configured: verified programs, Private profile, LocalSubnet only. Public networks remain blocked.'
    exit 0
} catch {
    Write-Error ('Firewall configuration failed: ' + $_.Exception.Message) -ErrorAction Continue
    exit 1
}
