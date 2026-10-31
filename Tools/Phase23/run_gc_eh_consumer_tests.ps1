[CmdletBinding()]
param(
    [string]$FixtureRoot = '',
    [string]$OutputRoot = ''
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
if ([string]::IsNullOrWhiteSpace($FixtureRoot)) {
    $FixtureRoot = Join-Path $root 'out\dotnet\phase23r4-gc-eh-fixture-reproduction'
}
$FixtureRoot = [IO.Path]::GetFullPath($FixtureRoot)
if (-not (Test-Path -LiteralPath $OutputRoot)) {
    if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
        $OutputRoot = Join-Path $FixtureRoot 'hosted-consumer-test'
    }
}
$OutputRoot = [IO.Path]::GetFullPath($OutputRoot)
if (Test-Path -LiteralPath $OutputRoot) {
    throw "Refusing to overwrite existing consumer-test evidence: $OutputRoot"
}
$fixture = Join-Path $FixtureRoot 'publish\guideXOS.NativeAotGcEhFixture.exe'
$map = Join-Path $FixtureRoot 'gc-eh-fixture.map'
$archive = Join-Path $FixtureRoot 'nuget-cache\runtime.win-x64.microsoft.dotnet.ilcompiler\9.0.0\sdk\Runtime.WorkstationGC.lib'
$source = Join-Path $root 'Tests\NativeAotUnwind\code_manager_consumer_tests.cpp'
foreach ($path in @($fixture, $map, $archive, $source)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Required hosted consumer input is absent: $path"
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
$exe = Join-Path $OutputRoot 'code_manager_consumer_tests.exe'
$obj = Join-Path $OutputRoot 'code_manager_consumer_tests.obj'
$response = Join-Path $OutputRoot 'compile.rsp'
$batch = Join-Path $OutputRoot 'compile.cmd'
$log = Join-Path $OutputRoot 'run.log'
$recordPath = Join-Path $OutputRoot 'code-manager-consumer-result.json'
$arguments = @(
    '/nologo',
    '/std:c++17',
    '/EHsc',
    '/W4',
    '/DWIN32_LEAN_AND_MEAN',
    '/DNOMINMAX',
    "`"$source`"",
    "`"$archive`"",
    'ntdll.lib',
    "/Fo:`"$obj`"",
    "/Fe:`"$exe`""
)
[IO.File]::WriteAllLines($response, $arguments, [Text.UTF8Encoding]::new($false))
$batchText = @"
@echo off
call "$vcvars" x64 >nul
if errorlevel 1 exit /b %errorlevel%
"$($compiler.FullName)" @"$response"
exit /b %errorlevel%
"@
[IO.File]::WriteAllText($batch, $batchText, [Text.UTF8Encoding]::new($false))
$cmd = Join-Path ([Environment]::SystemDirectory) 'cmd.exe'
& $cmd /d /s /c $batch
if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $exe -PathType Leaf)) {
    throw "The hosted NativeAOT consumer harness failed to compile (exit $LASTEXITCODE)."
}

$runOutput = & $exe $fixture $map 2>&1
$runCode = $LASTEXITCODE
$runOutput | Tee-Object -FilePath $log
if ($runCode -ne 0) {
    throw "The hosted NativeAOT consumer harness failed (exit $runCode)."
}
$record = [ordered]@{
    schema = 1
    outcome = 'PASS'
    consumer = 'pinned Windows CoffNativeCodeManager from runtime.win-x64 NativeAOT 9.0.0 archive'
    archive = [ordered]@{
        path = $archive
        sha256 = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash
    }
    fixture = [ordered]@{
        path = $fixture
        sha256 = (Get-FileHash -LiteralPath $fixture -Algorithm SHA256).Hash
    }
    source = [ordered]@{
        path = $source
        sha256 = (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash
    }
    executable = [ordered]@{
        path = $exe
        sha256 = (Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash
    }
    output = @($runOutput | ForEach-Object { [string]$_ })
    authenticGcInfo = [ordered]@{
        outcome = 'PASS'
        deadRootsExpireAcrossSafepoints = $true
        registerRoots = @('RBX', 'RSI', 'RDI')
        stackRoots = $true
        callbackRootSlotUpdates = $true
        multiFrameEnumeration = $true
    }
    r13Probe = [ordered]@{
        outcome = 'PASS'
        unwindInfo = 'test-only valid x64 overlay; compiler-generated GC info retained'
        recoveredValue = $true
        selectedLocation = 'writable stack save slot'
    }
    exceptionMetadata = [ordered]@{
        outcome = 'PASS'
        typedClauses = 1
        handlerDataLocation = $true
        handlerAndTargetTypeBounds = $true
        associatedCatchFunclet = $true
        fullExceptionPropagation = $false
    }
    codeManagerPublication = $false
    managedExecution = $false
    realCompactingGc = $false
    fullExceptionPropagation = $false
}
$record | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $recordPath -Encoding UTF8
Write-Output (Get-Content -LiteralPath $recordPath -Raw)
