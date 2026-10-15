$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$projectRoot = Split-Path -Parent $PSScriptRoot
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
if (-not (Test-Path -LiteralPath $compiler)) { throw '.NET Framework 4.8 compiler not found. Run on Windows 10/11.' }
$testRoot = Join-Path ([IO.Path]::GetTempPath()) ('LetMeSee-preparation-tests-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testRoot | Out-Null
try {
    $testExecutable = Join-Path $testRoot 'PreparationTests.exe'
    $sources = @((Join-Path $PSScriptRoot 'PreparationTests.cs'))
    $sources += @(Get-ChildItem -LiteralPath (Join-Path $projectRoot 'src\Core') -Filter '*.cs' -File | ForEach-Object FullName)
    $sources += @(Get-ChildItem -LiteralPath (Join-Path $projectRoot 'src\Infrastructure') -Filter '*.cs' -File | ForEach-Object FullName)
    & $compiler /nologo /target:exe /optimize+ /warn:4 /warnaserror+ /codepage:65001 /langversion:5 /reference:System.dll /reference:System.Core.dll /reference:System.Xml.dll "/win32manifest:$(Join-Path $projectRoot 'src\app.manifest')" "/out:$testExecutable" @sources
    if ($LASTEXITCODE -ne 0) { throw 'Preparation test compilation failed.' }
    & $testExecutable
    if ($LASTEXITCODE -ne 0) { throw 'Preparation tests failed.' }
} finally { Remove-Item -LiteralPath $testRoot -Recurse -Force -ErrorAction SilentlyContinue }
