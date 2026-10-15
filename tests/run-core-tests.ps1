$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$projectRoot = Split-Path -Parent $PSScriptRoot
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) {
    $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
}
if (-not (Test-Path -LiteralPath $compiler)) {
    throw '.NET Framework 4.8 compiler not found. Run these tests on Windows with .NET Framework 4.8.'
}

$testRoot = Join-Path ([IO.Path]::GetTempPath()) ('LetMeSee-core-tests-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testRoot | Out-Null
try {
    $testExecutable = Join-Path $testRoot 'CoreTests.exe'
    $sources = @((Join-Path $PSScriptRoot 'CoreTests.cs'))
    $sources += @(Get-ChildItem -LiteralPath (Join-Path $projectRoot 'src\Core') -Filter '*.cs' -File | ForEach-Object FullName)
    $sources += @(Get-ChildItem -LiteralPath (Join-Path $projectRoot 'src\Infrastructure') -Filter '*.cs' -File | ForEach-Object FullName)
    & $compiler /nologo /codepage:65001 /target:exe /optimize+ /warn:4 /warnaserror+ /langversion:5 /reference:System.dll /reference:System.Core.dll /reference:System.Xml.dll "/out:$testExecutable" @sources
    if ($LASTEXITCODE -ne 0) { throw 'Core test compilation failed.' }
    & $testExecutable
    if ($LASTEXITCODE -ne 0) { throw 'Core tests failed.' }
} finally {
    Remove-Item -LiteralPath $testRoot -Recurse -Force -ErrorAction SilentlyContinue
}
