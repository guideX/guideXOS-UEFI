[CmdletBinding()]
param(
    [string]$RuntimeSourceRoot = 'D:\dev\guideXOSUEFI_P23R8_RUNTIME_STAGING',
    [string]$RuntimeBuildRoot = '',
    [string]$OutputRoot = ''
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$RuntimeSourceRoot = [IO.Path]::GetFullPath($RuntimeSourceRoot)
if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path $root 'out\dotnet\phase23r13-link-partitioning'
}
$OutputRoot = [IO.Path]::GetFullPath($OutputRoot)
if (Test-Path -LiteralPath $OutputRoot) {
    throw "Refusing to overwrite P23R13 link-partitioning evidence: $OutputRoot"
}
New-Item -ItemType Directory -Path $OutputRoot | Out-Null

$expectedRuntimeCommit = '9d5a6a9aa463d6d10b0b0ba6d5982cc82f363dc3'
$runtimeCommit = (git -C $RuntimeSourceRoot rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0 -or $runtimeCommit -ne $expectedRuntimeCommit) {
    throw "Runtime source must be pinned at $expectedRuntimeCommit; found $runtimeCommit"
}

$runtimeOverlay = Join-Path $root 'Tools\Phase23\runtime-overlay\p23r8_runtime.patch'
git -C $RuntimeSourceRoot apply --reverse --check --unidiff-zero $runtimeOverlay
if ($LASTEXITCODE -ne 0) {
    throw 'The staged source does not contain the expected applied P23R8 runtime overlay.'
}

$vsRoot = 'C:\Program Files\Microsoft Visual Studio\18\Community'
$vcvars = Join-Path $vsRoot 'VC\Auxiliary\Build\vcvars64.bat'
$vsTools = Join-Path $vsRoot 'VC\Tools\MSVC\14.51.36231\bin\Hostx64\x64'
$compiler = Join-Path $vsTools 'cl.exe'
$linker = Join-Path $vsTools 'link.exe'
$assembler = Join-Path $vsTools 'ml64.exe'
$cmake = 'C:\mingw64\bin\cmake.exe'
$ninja = 'C:\mingw64\bin\ninja.exe'
foreach ($path in @($vcvars, $compiler, $linker, $assembler, $cmake, $ninja)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Required pinned x64 build tool is absent: $path"
    }
}

if ([string]::IsNullOrWhiteSpace($RuntimeBuildRoot)) {
    $RuntimeBuildRoot = Join-Path $OutputRoot 'build'
    $configureBuild = Join-Path $OutputRoot 'configure-and-build.cmd'
    $configureLines = @(
        '@echo off',
        'setlocal',
        ('call "{0}" x64 >nul' -f $vcvars),
        'if errorlevel 1 exit /b %errorlevel%',
        ('"{0}" -S "{1}" -B "{2}" -G Ninja -DCMAKE_BUILD_TYPE=Release -DCMAKE_C_COMPILER="{3}" -DCMAKE_CXX_COMPILER="{3}" -DCMAKE_MAKE_PROGRAM="{4}" "-DCMAKE_CXX_FLAGS=/DWIN32 /D_WINDOWS /EHsc /Gw" -DCLR_CMAKE_HOST_ARCH=x64 -DCLR_CMAKE_TARGET_ARCH=x64 -DCLR_CMAKE_TARGET_OS=guidexos -DCLR_CMAKE_ENABLE_CODE_COVERAGE=OFF' -f $cmake, (Join-Path $RuntimeSourceRoot 'src\coreclr'), $RuntimeBuildRoot, $compiler, $ninja),
        'if errorlevel 1 exit /b %errorlevel%',
        ('"{0}" --build "{1}" --target Runtime.WorkstationGC -- -v > "{2}" 2>&1' -f $cmake, $RuntimeBuildRoot, (Join-Path $OutputRoot 'runtime-build.log')),
        'exit /b %errorlevel%'
    )
    Set-Content -LiteralPath $configureBuild -Value $configureLines -Encoding ASCII
    & cmd.exe /d /s /c ('"{0}"' -f $configureBuild)
    if ($LASTEXITCODE -ne 0) {
        Get-Content -LiteralPath (Join-Path $OutputRoot 'runtime-build.log') -Tail 80 -ErrorAction SilentlyContinue
        throw "The isolated Runtime.WorkstationGC /Gw build failed with exit code $LASTEXITCODE."
    }
}

$RuntimeBuildRoot = [IO.Path]::GetFullPath($RuntimeBuildRoot)
$compileDatabase = Join-Path $RuntimeBuildRoot 'compile_commands.json'
$runtimeArchive = Join-Path $RuntimeBuildRoot 'nativeaot\Runtime\Full\Runtime.WorkstationGC.lib'
$testSource = Join-Path $root 'Tests\NativeAotUnwind\bootstrap_threadstore_link_probe.cpp'
$hostPalSource = Join-Path $root 'Tests\NativeAotUnwind\bootstrap_threadstore_link_probe_host_pal.cpp'
$stackSwitchSource = Join-Path $root 'Tests\NativeAotUnwind\bootstrap_threadstore_link_probe_stack_switch.asm'
foreach ($path in @($compileDatabase, $runtimeArchive, $testSource, $hostPalSource, $stackSwitchSource)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Required P23R13 link input is absent: $path"
    }
}

New-Item -ItemType Directory -Path $OutputRoot -Force | Out-Null
$database = Get-Content -LiteralPath $compileDatabase -Raw | ConvertFrom-Json
$compileEntry = $database | Where-Object {
    $_.command -match 'Runtime\.WorkstationGC\.dir.*threadstore\.cpp'
} | Select-Object -First 1
if ($null -eq $compileEntry) {
    throw "No Runtime.WorkstationGC threadstore.cpp command found in $compileDatabase"
}

$compileCommand = [string]$compileEntry.command
$functionLevelCompileEntries = @($database | Where-Object {
    $_.command -match 'Runtime\.WorkstationGC\.dir.*(RuntimeInstance\.cpp|threadstore(?:\.bootstrap)?\.cpp|thread(?:\.bootstrap)?\.cpp)'
})
if ($functionLevelCompileEntries.Count -ne 5 -or
    @($functionLevelCompileEntries | Where-Object { $_.command -notmatch ' /Gy(?: |$)' }).Count -ne 0 -or
    @($functionLevelCompileEntries | Where-Object { $_.command -notmatch ' /Gw(?: |$)' }).Count -ne 0) {
    throw 'The P23R13 archive must compile RuntimeInstance plus both original and bootstrap Thread/ThreadStore units with /Gy and /Gw.'
}
$compileCut = $compileCommand.LastIndexOf(' -c ', [StringComparison]::Ordinal)
if ($compileCut -lt 0) {
    throw 'The selected runtime compile command has no final -c source argument.'
}
$compilePrefix = $compileCommand.Substring(0, $compileCut)
$compileToolMatch = [regex]::Match($compilePrefix, '^(?:"(?<path>[^"]+)"|(?<path>\S+))')
if (-not $compileToolMatch.Success) {
    throw 'Unable to resolve the runtime compiler from compile_commands.json.'
}
$compiler = $compileToolMatch.Groups['path'].Value
$toolDirectory = Split-Path -Parent $compiler
$linker = Join-Path $toolDirectory 'link.exe'
$assembler = Join-Path $toolDirectory 'ml64.exe'
$testObject = Join-Path $OutputRoot 'bootstrap_threadstore_link_probe.obj'
$hostPalObject = Join-Path $OutputRoot 'bootstrap_threadstore_link_probe_host_pal.obj'
$stackSwitchObject = Join-Path $OutputRoot 'bootstrap_threadstore_link_probe_stack_switch.obj'
$pdb = Join-Path $OutputRoot 'bootstrap_threadstore_link_probe.pdb'
$executable = Join-Path $OutputRoot 'bootstrap_threadstore_link_probe.exe'
$forcedExecutable = Join-Path $OutputRoot 'DIAGNOSTIC_NEVER_RUN.exe'
$linkLog = Join-Path $OutputRoot 'link_probe.log'
$forcedLog = Join-Path $OutputRoot 'forced_link_diagnostic.log'
$mapPath = Join-Path $OutputRoot 'forced_link.map'
$commandFile = Join-Path $OutputRoot 'build_link_probe.cmd'

$compilePrefix = [regex]::Replace($compilePrefix, '(?i)/Fo\S+', ('/Fo"{0}"' -f $testObject))
$compilePrefix = [regex]::Replace($compilePrefix, '(?i)/Fd\S+', ('/Fd"{0}"' -f $pdb))
$testCompileCommand = '{0} -c "{1}"' -f $compilePrefix, $testSource
$hostPalCommand = '"{0}" /nologo /TP /W4 /WX /EHsc /MT /GR- /c "{1}" /Fo"{2}"' -f $compiler, $hostPalSource, $hostPalObject
$commonObjects = '"{0}" "{1}" "{2}" "{3}" kernel32.lib' -f $testObject, $hostPalObject, $stackSwitchObject, $runtimeArchive
$normalLink = '"{0}" /nologo /OUT:"{1}" /INCREMENTAL:NO /OPT:REF /SUBSYSTEM:CONSOLE /VERBOSE:LIB /VERBOSE:REF /MAP:"{2}" {3} > "{4}" 2>&1' -f $linker, $executable, (Join-Path $OutputRoot 'link_probe.map'), $commonObjects, $linkLog
$forcedLink = '"{0}" /nologo /OUT:"{1}" /INCREMENTAL:NO /OPT:REF /FORCE:UNRESOLVED /SUBSYSTEM:CONSOLE /VERBOSE:LIB /VERBOSE:REF /MAP:"{2}" {3} > "{4}" 2>&1' -f $linker, $forcedExecutable, $mapPath, $commonObjects, $forcedLog
$commandLines = @(
    '@echo off',
    'setlocal',
    ('call "{0}" x64 >nul' -f $vcvars),
    'if errorlevel 1 exit /b %errorlevel%',
    ('cd /d "{0}"' -f $compileEntry.directory),
    'if errorlevel 1 exit /b %errorlevel%',
    ('cd /d "{0}"' -f $OutputRoot),
    'if errorlevel 1 exit /b %errorlevel%',
    ('"{0}" /nologo /c "{1}"' -f $assembler, $stackSwitchSource),
    'if errorlevel 1 exit /b %errorlevel%',
    $hostPalCommand,
    'if errorlevel 1 exit /b %errorlevel%',
    $testCompileCommand,
    'if errorlevel 1 exit /b %errorlevel%',
    $normalLink,
    ('echo LINK_EXIT=%ERRORLEVEL%>>"{0}"' -f $linkLog),
    $forcedLink,
    'exit /b 0'
)
Set-Content -LiteralPath $commandFile -Value $commandLines -Encoding ASCII

$linkOutput = & cmd.exe /d /s /c ('"{0}"' -f $commandFile) 2>&1
$launcherExit = $LASTEXITCODE
$linkOutput | ForEach-Object { Write-Output $_ }
if ($launcherExit -ne 0) {
    throw "Bootstrap link probe compilation failed with exit code $launcherExit."
}
if (-not (Test-Path -LiteralPath $linkLog -PathType Leaf) -or
    -not (Test-Path -LiteralPath $forcedLog -PathType Leaf) -or
    -not (Test-Path -LiteralPath $mapPath -PathType Leaf)) {
    throw 'The bootstrap link probe did not produce its expected diagnostic logs and map.'
}

$logText = Get-Content -LiteralPath $linkLog -Raw
$unresolved = [Collections.Generic.SortedSet[string]]::new([StringComparer]::Ordinal)
foreach ($match in [regex]::Matches($logText, '(?m)error LNK(?:2019|2001): unresolved external symbol (.+?)(?: referenced in function .*|\r?$)')) {
    [void]$unresolved.Add($match.Groups[1].Value.Trim())
}
$unresolvedCountMatch = [regex]::Match($logText, 'fatal error LNK1120: (\d+) unresolved externals?')
$unresolvedCount = if ($unresolvedCountMatch.Success) { [int]$unresolvedCountMatch.Groups[1].Value } else { 0 }
$linkExitMatch = [regex]::Match($logText, '(?m)^LINK_EXIT=(\d+)\s*$')
$linkExit = if ($linkExitMatch.Success) { [int]$linkExitMatch.Groups[1].Value } else { $null }
$mapText = Get-Content -LiteralPath $mapPath -Raw
$members = [Collections.Generic.SortedSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($match in [regex]::Matches($mapText, 'Runtime\.WorkstationGC:([^\s]+\.obj)')) {
    [void]$members.Add($match.Groups[1].Value)
}
$memberPath = Join-Path $OutputRoot 'forced-map-runtime-members.txt'
$members | Set-Content -LiteralPath $memberPath -Encoding ASCII
$linkErrorMembers = [Collections.Generic.SortedSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($match in [regex]::Matches($logText, 'Runtime\.WorkstationGC\.lib\(([^)]+\.obj)\)\s*:\s*error LNK')) {
    [void]$linkErrorMembers.Add($match.Groups[1].Value)
}
$linkErrorMemberPath = Join-Path $OutputRoot 'normal-link-error-members.txt'
$linkErrorMembers | Set-Content -LiteralPath $linkErrorMemberPath -Encoding ASCII

$record = [ordered]@{
    schema = 1
    generatedBy = 'Tools/Phase23/run_bootstrap_link_partitioning_experiment.ps1'
    runtimeCommit = $runtimeCommit
    runtimeSourceRoot = $RuntimeSourceRoot
    runtimeBuildRoot = $RuntimeBuildRoot
    runtimeArchive = $runtimeArchive
    runtimeArchiveSha256 = (Get-FileHash -LiteralPath $runtimeArchive -Algorithm SHA256).Hash.ToUpperInvariant()
    compileDatabase = $compileDatabase
    compileDatabaseSha256 = (Get-FileHash -LiteralPath $compileDatabase -Algorithm SHA256).Hash.ToUpperInvariant()
    runtimeBuildLog = if (Test-Path -LiteralPath (Join-Path (Split-Path -Parent $RuntimeBuildRoot) 'runtime-build.log')) { Join-Path (Split-Path -Parent $RuntimeBuildRoot) 'runtime-build.log' } else { $null }
    coreCompileCommandsContainGyAndGw = $true
    linkCommandFile = $commandFile
    linkCommandFileSha256 = (Get-FileHash -LiteralPath $commandFile -Algorithm SHA256).Hash.ToUpperInvariant()
    linkLog = $linkLog
    linkLogSha256 = (Get-FileHash -LiteralPath $linkLog -Algorithm SHA256).Hash.ToUpperInvariant()
    linkExitCode = $linkExit
    unresolvedExternalCount = $unresolvedCount
    unresolvedSymbols = @($unresolved)
    forcedDiagnosticMap = $mapPath
    forcedDiagnosticMapSha256 = (Get-FileHash -LiteralPath $mapPath -Algorithm SHA256).Hash.ToUpperInvariant()
    forcedDiagnosticLog = $forcedLog
    forcedDiagnosticLogSha256 = (Get-FileHash -LiteralPath $forcedLog -Algorithm SHA256).Hash.ToUpperInvariant()
    forcedDiagnosticExecutable = $forcedExecutable
    forcedDiagnosticExecutableSha256 = (Get-FileHash -LiteralPath $forcedExecutable -Algorithm SHA256).Hash.ToUpperInvariant()
    forcedDiagnosticExecutableWasRun = $false
    forcedDiagnosticMapRuntimeMemberCount = $members.Count
    forcedDiagnosticMapRuntimeMembers = @($members)
    normalLinkErrorMemberCount = $linkErrorMembers.Count
    normalLinkErrorMembers = @($linkErrorMembers)
    memberEvidenceNote = 'The forced diagnostic map reports runtime members represented in its output image, not a guaranteed count of members loaded by the failed standard link. The normal-link error-member list is a lower bound of members that emitted unresolved references.'
    outcome = if ($unresolvedCount -eq 0 -and $linkExit -eq 0) { 'linked' } else { 'blocked_unresolved_externals' }
    scope = 'Genuine pinned NativeAOT archive link experiment. The forced image exists only to emit diagnostic map evidence and is never executed.'
}
$recordPath = Join-Path $OutputRoot 'bootstrap-link-partitioning-record.json'
$record | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $recordPath -Encoding UTF8
$record | ConvertTo-Json -Depth 8
