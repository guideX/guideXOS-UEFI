[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$SourceRoot,
    [string]$OutputRoot = ''
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$SourceRoot = [IO.Path]::GetFullPath($SourceRoot)
if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path $root 'out\dotnet\phase23r7-stack-bounds-pal-hosted-test'
}
$OutputRoot = [IO.Path]::GetFullPath($OutputRoot)
if (Test-Path -LiteralPath $OutputRoot) {
    throw "Refusing to overwrite stack-bounds test evidence: $OutputRoot"
}

$object = Join-Path $SourceRoot 'artifacts\obj\coreclr\guidexos.x64.Release\nativeaot\Runtime\Full\CMakeFiles\Runtime.WorkstationGC.dir\__\guidexos\PalRedhawkStackBounds.cpp.obj'
$source = Join-Path $root 'Tests\NativeAotUnwind\stack_bounds_unsupported_tests.cpp'
foreach ($path in @($object, $source)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Required stack-bounds test input is absent: $path"
    }
}

$vsRoot = 'C:\Program Files\Microsoft Visual Studio\18\Community\VC\Tools\MSVC'
$compiler = Get-ChildItem -LiteralPath $vsRoot -Filter cl.exe -File -Recurse |
    Where-Object { $_.FullName -match '\\Hostx64\\x64\\cl\.exe$' } |
    Sort-Object FullName -Descending | Select-Object -First 1
$vcvars = 'C:\Program Files\Microsoft Visual Studio\18\Community\VC\Auxiliary\Build\vcvars64.bat'
if ($null -eq $compiler -or -not (Test-Path -LiteralPath $vcvars -PathType Leaf)) {
    throw 'The pinned Visual Studio x64 C++ tools are unavailable.'
}

New-Item -ItemType Directory -Path $OutputRoot | Out-Null
$executable = Join-Path $OutputRoot 'stack_bounds_unsupported_tests.exe'
$testObject = Join-Path $OutputRoot 'stack_bounds_unsupported_tests.obj'
$command = 'call "' + $vcvars + '" x64 >nul && "' + $compiler.FullName +
    '" /nologo /std:c++17 /W4 /WX /EHsc "' + $source + '" "' + $object +
    '" /Fo:"' + $testObject + '" /Fe:"' + $executable + '" && "' + $executable + '"'
& cmd.exe /d /s /c $command
$exitCode = $LASTEXITCODE
if ($exitCode -ne 0) {
    throw "Stack-bounds fail-closed hosted test failed with exit code $exitCode."
}

$record = [ordered]@{
    schema = 1
    generatedBy = 'Tools/Phase23/run_stack_bounds_unsupported_tests.ps1'
    sourceRoot = $SourceRoot
    runtimeObject = $object
    testSource = $source
    testObject = $testObject
    executable = $executable
    executableSha256 = (Get-FileHash -LiteralPath $executable -Algorithm SHA256).Hash.ToUpperInvariant()
    cases = 4
    passed = 4
    failed = 0
    providerBehavior = 'unsupported; outputs cleared and false returned'
}
$recordPath = Join-Path $OutputRoot 'stack-bounds-test-record.json'
$record | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $recordPath -Encoding UTF8
$record | ConvertTo-Json -Depth 6
