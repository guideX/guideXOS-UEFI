[CmdletBinding()]
param(
    [string]$OutputRoot = ''
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path $root 'out\dotnet\phase23r11-hosted-finalizer-context'
}
$OutputRoot = [IO.Path]::GetFullPath($OutputRoot)
if (Test-Path -LiteralPath $OutputRoot) {
    throw "Refusing to overwrite hosted finalizer-context evidence: $OutputRoot"
}

$testSource = Join-Path $root 'Tests\NativeAotUnwind\hosted_finalizer_context_tests.cpp'
$asmSource = Join-Path $root 'Tests\NativeAotUnwind\hosted_finalizer_context_worker.asm'
foreach ($path in @($testSource, $asmSource)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Required hosted finalizer-context input is absent: $path"
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
$exe = Join-Path $OutputRoot 'hosted_finalizer_context_tests.exe'
$testObject = Join-Path $OutputRoot 'hosted_finalizer_context_tests.obj'
$workerObject = Join-Path $OutputRoot 'hosted_finalizer_context_worker.obj'
$linker = Join-Path (Split-Path -Parent $compiler.FullName) 'link.exe'
$cmdFile = Join-Path $OutputRoot 'build_and_run.cmd'
$lines = @(
    '@echo off',
    ('call "{0}" x64 >nul' -f $vcvars),
    'if errorlevel 1 exit /b %errorlevel%',
    ('cd /d "{0}"' -f $OutputRoot),
    'if errorlevel 1 exit /b %errorlevel%',
    ('ml64.exe /nologo /c "' + $asmSource + '"'),
    'if errorlevel 1 exit /b %errorlevel%',
    ('"{0}" /nologo /W4 /WX /EHsc /MT /O2 /c "{1}" /Fo"{2}"' -f $compiler.FullName, $testSource, $testObject),
    'if errorlevel 1 exit /b %errorlevel%',
    ('"{0}" /OUT:"{1}" /INCREMENTAL:NO /OPT:REF /SUBSYSTEM:CONSOLE "{2}" "{3}" kernel32.lib' -f $linker, $exe, $testObject, $workerObject),
    'if errorlevel 1 exit /b %errorlevel%',
    ('"{0}"' -f $exe),
    'exit /b %errorlevel%'
)
Set-Content -LiteralPath $cmdFile -Value $lines -Encoding ASCII

$output = & cmd.exe /d /s /c ('"{0}"' -f $cmdFile) 2>&1
$exitCode = $LASTEXITCODE
$output | ForEach-Object { Write-Output $_ }
if ($exitCode -ne 0) {
    throw "Hosted finalizer-context tests failed with exit code $exitCode."
}

$summaryLine = $output | Where-Object { $_ -match '^RESULT passed=' } |
    Select-Object -Last 1
if ($summaryLine -notmatch '^RESULT passed=(\d+) failed=(\d+)') {
    throw 'The hosted finalizer-context test did not emit a parseable result line.'
}

$record = [ordered]@{
    schema = 1
    generatedBy = 'Tools/Phase23/run_hosted_finalizer_context_tests.ps1'
    testSource = $testSource
    testSourceSha256 = (Get-FileHash -LiteralPath $testSource -Algorithm SHA256).Hash.ToUpperInvariant()
    asmSource = $asmSource
    executable = $exe
    executableSha256 = (Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash.ToUpperInvariant()
    passed = [int]$Matches[1]
    failed = [int]$Matches[2]
    scope = 'Hosted Windows fiber and OS stack ownership qualification only; no NativeAOT ThreadStore attachment and no production UEFI PAL claim.'
}
$recordPath = Join-Path $OutputRoot 'hosted-finalizer-context-test-record.json'
$record | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $recordPath -Encoding UTF8
$record | ConvertTo-Json -Depth 5
