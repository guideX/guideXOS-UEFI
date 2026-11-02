[CmdletBinding()]
param(
    [string]$OutputRoot = ''
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path $repo 'out\dotnet\phase23r15-context-capture-tests'
}
$OutputRoot = [IO.Path]::GetFullPath($OutputRoot)
if (Test-Path -LiteralPath $OutputRoot) {
    throw "Refusing to overwrite execution-context test evidence: $OutputRoot"
}

$cppSource = Join-Path $repo 'Tests\ExecutionContextCapture\execution_context_contract_tests.cpp'
$cppContract = Join-Path $repo 'Tools\Phase23\execution_context_contract.h'
$csProject = Join-Path $repo 'Tests\ExecutionContextCapture\ExecutionContextCapture.Tests.csproj'
$csSource = Join-Path $repo 'Tests\ExecutionContextCapture\Program.cs'
$csProduction = Join-Path $repo 'Kernel\Misc\ExecutionContextCapture.cs'
foreach ($path in @($cppSource, $cppContract, $csProject, $csSource, $csProduction)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Required execution-context test input is absent: $path"
    }
}

$compiler = 'C:\Program Files\Microsoft Visual Studio\18\Community\VC\Tools\MSVC\14.51.36231\bin\Hostx64\x64\cl.exe'
$vcvars = 'C:\Program Files\Microsoft Visual Studio\18\Community\VC\Auxiliary\Build\vcvars64.bat'
foreach ($path in @($compiler, $vcvars)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Required hosted x64 compiler input is absent: $path"
    }
}

New-Item -ItemType Directory -Path $OutputRoot | Out-Null
$cppExe = Join-Path $OutputRoot 'execution_context_contract_tests.exe'
$cppObj = Join-Path $OutputRoot 'execution_context_contract_tests.obj'
$cppLog = Join-Path $OutputRoot 'cpp-test.log'
$cppCommandFile = Join-Path $OutputRoot 'build-and-run-cpp.cmd'
$cppCommand = 'call "' + $vcvars + '" x64 >nul && "' + $compiler +
    '" /nologo /std:c++17 /W4 /WX /EHsc /MT /GR- "' + $cppSource +
    '" /Fo:"' + $cppObj + '" /Fe:"' + $cppExe + '" && "' + $cppExe + '"'
Set-Content -LiteralPath $cppCommandFile -Value @('@echo off', $cppCommand) -Encoding ASCII
$cppOutput = & cmd.exe /d /s /c ('"{0}"' -f $cppCommandFile) 2>&1
$cppExit = $LASTEXITCODE
$cppOutput | Tee-Object -FilePath $cppLog
if ($cppExit -ne 0) {
    throw "C++ execution-context contract tests failed with exit code $cppExit."
}
$cppSummary = $cppOutput | Where-Object { $_ -match '^RESULT passed=' } |
    Select-Object -Last 1
if ($cppSummary -notmatch '^RESULT passed=(\d+) failed=(\d+)') {
    throw 'The C++ execution-context tests did not emit a parseable result line.'
}
$cppPassed = [int]$Matches[1]
$cppFailed = [int]$Matches[2]

$csBin = Join-Path $OutputRoot 'managed\bin'
$csObj = Join-Path $OutputRoot 'managed\obj'
$csLog = Join-Path $OutputRoot 'csharp-test.log'
$csArgs = @(
    'run', '--project', $csProject, '--configuration', 'Release',
    "--property:BaseIntermediateOutputPath=$csObj\",
    "--property:BaseOutputPath=$csBin\"
)
$csOutput = & dotnet @csArgs 2>&1
$csExit = $LASTEXITCODE
$csOutput | Tee-Object -FilePath $csLog
if ($csExit -ne 0) {
    throw "C# execution-context capture tests failed with exit code $csExit."
}
$csSummary = $csOutput | Where-Object { $_ -match '^RESULT passed=' } |
    Select-Object -Last 1
if ($csSummary -notmatch '^RESULT passed=(\d+) failed=(\d+)') {
    throw 'The C# execution-context tests did not emit a parseable result line.'
}
$csPassed = [int]$Matches[1]
$csFailed = [int]$Matches[2]

$record = [ordered]@{
    schema = 1
    generatedBy = 'Tools/Phase23/run_execution_context_capture_tests.ps1'
    cppSource = $cppSource
    cppSourceSha256 = (Get-FileHash -LiteralPath $cppSource -Algorithm SHA256).Hash.ToUpperInvariant()
    cppContract = $cppContract
    cppContractSha256 = (Get-FileHash -LiteralPath $cppContract -Algorithm SHA256).Hash.ToUpperInvariant()
    cppExecutable = $cppExe
    cppExecutableSha256 = (Get-FileHash -LiteralPath $cppExe -Algorithm SHA256).Hash.ToUpperInvariant()
    cppPassed = $cppPassed
    cppFailed = $cppFailed
    csharpProject = $csProject
    csharpProductionSource = $csProduction
    csharpProductionSourceSha256 = (Get-FileHash -LiteralPath $csProduction -Algorithm SHA256).Hash.ToUpperInvariant()
    csharpPassed = $csPassed
    csharpFailed = $csFailed
    scope = 'Hosted contract and frame-normalization qualification; no asynchronous NativeAOT suspension, ThreadStore attachment, or UEFI target execution claim.'
}
$recordPath = Join-Path $OutputRoot 'execution-context-test-record.json'
$record | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $recordPath -Encoding UTF8
$record | ConvertTo-Json -Depth 5
