<# Explicit, user-requested Bonjour installation. Never called by startup or the installer. #>
[CmdletBinding()]
param()
Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'dependency_security.ps1')
try {
    $admin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
    if (-not $admin) { throw 'Administrator permission is required for this explicit service installation.' }
    if (Get-Service -Name 'Bonjour Service' -ErrorAction SilentlyContinue) {
        Write-Host 'Bonjour is already installed. No package was executed. Check its service status in Windows.'
        exit 0
    }
    $runtime = Join-Path (Split-Path -Parent $PSScriptRoot) 'bin'
    Assert-RuntimeIntegrity -RuntimeDirectory $runtime
    $package = Join-Path $runtime 'third_party/Bonjour64.msi'
    if (-not (Test-Path -LiteralPath $package -PathType Leaf)) { throw 'The verified package does not include Bonjour. Obtain it from its trusted publisher or use another supported route.' }
    Assert-NoReparsePath -Path $package
    $signature = Get-AuthenticodeSignature -LiteralPath $package -ErrorAction Stop
    if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notmatch '(?:^|,\s*)O=Apple Inc\.?(?:,|$)') {
        throw 'The Bonjour package is not currently verifiable with a valid Apple signature. Installation was blocked.'
    }
    $msiexec = Join-Path ([Environment]::SystemDirectory) 'msiexec.exe'
    $process = Start-Process -FilePath $msiexec -ArgumentList ('/i "' + $package + '" /norestart') -Wait -PassThru
    if ($process.ExitCode -eq 3010) { Write-Warning 'Bonjour was installed, but Windows requires a restart. Save your work and restart manually.'; exit 3010 }
    if ($process.ExitCode -ne 0) { throw ('Bonjour installation did not succeed: ' + $process.ExitCode) }
    $service = Get-Service -Name 'Bonjour Service' -ErrorAction Stop
    if ($service.Status -ne 'Running') { throw 'Bonjour is installed but is not running. Review its service state with the administrator.' }
    Write-Host 'Bonjour was installed after explicit user consent. Configure the private local firewall rule and verify the checklist again.'
    exit 0
} catch {
    Write-Error ('Bonjour setup was not completed: ' + $_.Exception.Message) -ErrorAction Continue
    exit 1
}
