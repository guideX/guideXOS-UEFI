$ErrorActionPreference = 'Stop'

$testRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$source = Join-Path $testRoot 'x64_unwind_tests.cpp'
$compiler = (Get-Command 'g++.exe' -ErrorAction Stop).Source
$buildRoot = Join-Path ([System.IO.Path]::GetTempPath()) (
    'guideXOS_P23R2_unwind_' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $buildRoot | Out-Null

$target = & $compiler -dumpmachine
if ($LASTEXITCODE -ne 0) {
    throw 'Unable to query the hosted C++ compiler target.'
}

function Invoke-TestBuild([string]$name, [bool]$windowsReference) {
    $output = Join-Path $buildRoot ($name + '.exe')
    $arguments = @(
        '-std=c++17',
        '-Wall',
        '-Wextra',
        '-Werror',
        $source,
        '-o',
        $output
    )
    if ($windowsReference) {
        $arguments += @('-DGUIDEXOS_UNWIND_WINDOWS_REFERENCE', '-lntdll')
    }

    & $compiler @arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Hosted unwind test build '$name' failed with exit code $LASTEXITCODE."
    }

    & $output
    if ($LASTEXITCODE -ne 0) {
        throw "Hosted unwind test run '$name' failed with exit code $LASTEXITCODE."
    }
    Write-Host "Hosted test executable: $output"
}

# First prove the bounded implementation builds and runs without its
# test-only Windows API reference. Then compare supported cases with ntdll.
Invoke-TestBuild 'x64_unwind_tests_bounded_only' $false
if ($target -match 'mingw|windows') {
    Invoke-TestBuild 'x64_unwind_tests_windows_reference' $true
}
