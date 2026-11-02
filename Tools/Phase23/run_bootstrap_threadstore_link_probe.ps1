[CmdletBinding()]
param(
    [string]$RuntimeBuildRoot = '',
    [string]$OutputRoot = ''
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
if ([string]::IsNullOrWhiteSpace($RuntimeBuildRoot)) {
    $RuntimeBuildRoot = Join-Path $root 'out\dotnet\phase23r11-workstationgc-rebuild1\build'
}
$RuntimeBuildRoot = [IO.Path]::GetFullPath($RuntimeBuildRoot)
if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path $root 'out\dotnet\phase23r11-bootstrap-threadstore-link-probe'
}
$OutputRoot = [IO.Path]::GetFullPath($OutputRoot)
if (Test-Path -LiteralPath $OutputRoot) {
    throw "Refusing to overwrite bootstrap ThreadStore link-probe evidence: $OutputRoot"
}

$compileDatabase = Join-Path $RuntimeBuildRoot 'compile_commands.json'
$runtimeArchive = Join-Path $RuntimeBuildRoot 'nativeaot\Runtime\Full\Runtime.WorkstationGC.lib'
$testSource = Join-Path $root 'Tests\NativeAotUnwind\bootstrap_threadstore_link_probe.cpp'
$hostPalSource = Join-Path $root 'Tests\NativeAotUnwind\bootstrap_threadstore_link_probe_host_pal.cpp'
$stackSwitchSource = Join-Path $root 'Tests\NativeAotUnwind\bootstrap_threadstore_link_probe_stack_switch.asm'
$vcvars = 'C:\Program Files\Microsoft Visual Studio\18\Community\VC\Auxiliary\Build\vcvars64.bat'
foreach ($path in @($compileDatabase, $runtimeArchive, $testSource, $hostPalSource, $stackSwitchSource, $vcvars)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Required bootstrap link-probe input is absent: $path"
    }
}

$database = Get-Content -LiteralPath $compileDatabase -Raw | ConvertFrom-Json
$compileEntry = $database | Where-Object {
    $_.command -match 'Runtime\.WorkstationGC\.dir.*threadstore\.cpp'
} | Select-Object -First 1
if ($null -eq $compileEntry) {
    throw "No Runtime.WorkstationGC threadstore.cpp command found in $compileDatabase"
}
$compileCommand = [string]$compileEntry.command
$compileCut = $compileCommand.LastIndexOf(' -c ', [StringComparison]::Ordinal)
if ($compileCut -lt 0) {
    throw 'The selected threadstore compile command has no final -c source argument.'
}
$compilePrefix = $compileCommand.Substring(0, $compileCut)
$compilerMatch = [regex]::Match($compilePrefix, '^(?:"(?<path>[^"]+)"|(?<path>\S+))')
if (-not $compilerMatch.Success) {
    throw 'Unable to resolve the compiler executable from compile_commands.json.'
}
$compiler = $compilerMatch.Groups['path'].Value
$compilerDirectory = Split-Path -Parent $compiler
$linker = Join-Path $compilerDirectory 'link.exe'
$assembler = Join-Path $compilerDirectory 'ml64.exe'
foreach ($path in @($compiler, $linker, $assembler)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Pinned x64 build tool is unavailable: $path"
    }
}

New-Item -ItemType Directory -Path $OutputRoot | Out-Null
$testObject = Join-Path $OutputRoot 'bootstrap_threadstore_link_probe.obj'
$hostPalObject = Join-Path $OutputRoot 'bootstrap_threadstore_link_probe_host_pal.obj'
$stackSwitchObject = Join-Path $OutputRoot 'bootstrap_threadstore_link_probe_stack_switch.obj'
$pdb = Join-Path $OutputRoot 'bootstrap_threadstore_link_probe.pdb'
$executable = Join-Path $OutputRoot 'bootstrap_threadstore_link_probe.exe'
$linkLog = Join-Path $OutputRoot 'link_probe.log'
$commandFile = Join-Path $OutputRoot 'build_link_probe.cmd'

$compilePrefix = [regex]::Replace($compilePrefix, '(?i)/Fo\S+', ('/Fo"{0}"' -f $testObject))
$compilePrefix = [regex]::Replace($compilePrefix, '(?i)/Fd\S+', ('/Fd"{0}"' -f $pdb))
$hostPalCommand = '"{0}" /nologo /TP /W4 /WX /EHsc /MT /GR- /c "{1}" /Fo"{2}"' -f $compiler, $hostPalSource, $hostPalObject
$testCompileCommand = '{0} -c "{1}"' -f $compilePrefix, $testSource
$lines = @(
    '@echo off',
    'setlocal EnableDelayedExpansion',
    ('call "{0}" x64 >nul' -f $vcvars),
    'if errorlevel 1 exit /b %errorlevel%',
    ('cd /d "{0}"' -f $OutputRoot),
    'if errorlevel 1 exit /b %errorlevel%',
    ('"{0}" /nologo /c "{1}"' -f $assembler, $stackSwitchSource),
    'if errorlevel 1 exit /b %errorlevel%',
    ('cd /d "{0}"' -f $compileEntry.directory),
    'if errorlevel 1 exit /b %errorlevel%',
    $hostPalCommand,
    'if errorlevel 1 exit /b %errorlevel%',
    $testCompileCommand,
    'if errorlevel 1 exit /b %errorlevel%',
    ('"{0}" /nologo /OUT:"{1}" /INCREMENTAL:NO /OPT:REF /SUBSYSTEM:CONSOLE "{2}" "{3}" "{4}" "{5}" kernel32.lib > "{6}" 2>&1' -f $linker, $executable, $testObject, $hostPalObject, $stackSwitchObject, $runtimeArchive, $linkLog),
    'set "LINK_EXIT=%ERRORLEVEL%"',
    ('echo LINK_EXIT=%LINK_EXIT%>>"{0}"' -f $linkLog),
    'if "%LINK_EXIT%"=="0" (',
    ('  "{0}" >> "{1}" 2>&1' -f $executable, $linkLog),
    '  set "RUN_EXIT=!ERRORLEVEL!"',
    ('  echo RUN_EXIT=!RUN_EXIT!>>"{0}"' -f $linkLog),
    ')',
    'exit /b 0'
)
Set-Content -LiteralPath $commandFile -Value $lines -Encoding ASCII

$commandOutput = & cmd.exe /d /s /c ('"{0}"' -f $commandFile) 2>&1
$commandExit = $LASTEXITCODE
$commandOutput | ForEach-Object { Write-Output $_ }
if ($commandExit -ne 0) {
    throw "Bootstrap probe compilation failed with exit code $commandExit."
}
if (-not (Test-Path -LiteralPath $linkLog -PathType Leaf)) {
    throw 'The bootstrap probe did not create its link log.'
}

$logText = Get-Content -LiteralPath $linkLog -Raw
$linkMatch = [regex]::Match($logText, '(?m)^LINK_EXIT=(\d+)\s*$')
if (-not $linkMatch.Success) {
    throw 'The bootstrap probe link log has no recorded linker exit code.'
}
$linkExit = [int]$linkMatch.Groups[1].Value
$unresolvedSymbols = [Collections.Generic.List[string]]::new()
foreach ($match in [regex]::Matches($logText, '(?m)error LNK(?:2019|2001): unresolved external symbol (.+?)(?: referenced in function .*|\r?$)')) {
    $symbol = $match.Groups[1].Value.Trim()
    if (-not $unresolvedSymbols.Contains($symbol)) {
        $unresolvedSymbols.Add($symbol)
    }
}
$unresolvedCountMatch = [regex]::Match($logText, 'fatal error LNK1120: (\d+) unresolved externals?')
$unresolvedCount = if ($unresolvedCountMatch.Success) { [int]$unresolvedCountMatch.Groups[1].Value } else { 0 }
$runMatch = [regex]::Match($logText, '(?m)^RUN_EXIT=(\d+)\s*$')
$runExit = if ($runMatch.Success) { [int]$runMatch.Groups[1].Value } else { $null }
$resultMatch = [regex]::Match($logText, '(?m)^RESULT passed=(\d+) failed=(\d+)')
$record = [ordered]@{
    schema = 1
    generatedBy = 'Tools/Phase23/run_bootstrap_threadstore_link_probe.ps1'
    runtimeBuildRoot = $RuntimeBuildRoot
    runtimeArchive = $runtimeArchive
    runtimeArchiveSha256 = (Get-FileHash -LiteralPath $runtimeArchive -Algorithm SHA256).Hash.ToUpperInvariant()
    testSource = $testSource
    testSourceSha256 = (Get-FileHash -LiteralPath $testSource -Algorithm SHA256).Hash.ToUpperInvariant()
    hostPalSource = $hostPalSource
    hostPalSourceSha256 = (Get-FileHash -LiteralPath $hostPalSource -Algorithm SHA256).Hash.ToUpperInvariant()
    stackSwitchSource = $stackSwitchSource
    stackSwitchSourceSha256 = (Get-FileHash -LiteralPath $stackSwitchSource -Algorithm SHA256).Hash.ToUpperInvariant()
    linkLog = $linkLog
    linkLogSha256 = (Get-FileHash -LiteralPath $linkLog -Algorithm SHA256).Hash.ToUpperInvariant()
    linkExitCode = $linkExit
    unresolvedExternalCount = $unresolvedCount
    unresolvedSymbols = @($unresolvedSymbols)
    executableCreated = Test-Path -LiteralPath $executable -PathType Leaf
    executableRan = $runMatch.Success
    executableExitCode = $runExit
    runtimeChecksPassed = if ($resultMatch.Success) { [int]$resultMatch.Groups[1].Value } else { $null }
    runtimeChecksFailed = if ($resultMatch.Success) { [int]$resultMatch.Groups[2].Value } else { $null }
    outcome = if ($linkExit -eq 0) { 'linked_and_executed' } elseif ($unresolvedCount -gt 0) { 'blocked_unresolved_externals' } else { 'link_failed_other' }
    scope = 'Focused diagnostic only. It attempts to link the genuine pinned ThreadStore and Thread objects with a hosted PAL adapter; a failed link is recorded as a blocker, never as a passing runtime test. No dummy runtime, GC, EH, or module providers are supplied.'
}
$recordPath = Join-Path $OutputRoot 'bootstrap-threadstore-link-probe-record.json'
$record | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $recordPath -Encoding UTF8
$record | ConvertTo-Json -Depth 8
