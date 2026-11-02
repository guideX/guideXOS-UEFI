[CmdletBinding()]
param(
    [string]$RuntimeSourceRoot = 'D:\dev\guideXOSUEFI_P23R8_RUNTIME_STAGING',
    [string]$PartitionedRuntimeSourceRoot = 'D:\dev\guideXOSUEFI_BUILD_INFRA\out\dotnet\phase23r13-runtime-partition-source',
    [string]$OutputRoot = '',
    [switch]$ResumeExistingSource
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path $repo 'out\dotnet\phase23r14-hijack-target-contract'
}
$OutputRoot = [IO.Path]::GetFullPath($OutputRoot)
if ((Test-Path -LiteralPath $OutputRoot) -and -not $ResumeExistingSource) {
    throw "Refusing to overwrite P23R14 output: $OutputRoot"
}
if ($ResumeExistingSource -and -not (Test-Path -LiteralPath $OutputRoot -PathType Container)) {
    throw "Resume requested but P23R14 output does not exist: $OutputRoot"
}

$RuntimeSourceRoot = [IO.Path]::GetFullPath($RuntimeSourceRoot)
$pinnedRuntime = '9d5a6a9aa463d6d10b0b0ba6d5982cc82f363dc3'
$runtimeHead = (git -C $RuntimeSourceRoot rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0 -or $runtimeHead -ne $pinnedRuntime) {
    throw "Runtime source must be pinned at $pinnedRuntime; found $runtimeHead"
}

$protectedTrees = @(
    'D:\dev\guideXOSUEFI_BUILD_INFRA\out\rt',
    'D:\dev\guideXOSUEFI_P23R8_RUNTIME_STAGING'
)
function Get-StatusSnapshot([string]$Path) {
    $statusLines = @(git -C $Path status --porcelain=v1 --untracked-files=all)
    if ($LASTEXITCODE -ne 0) { throw "Unable to read protected status: $Path" }
    $text = if ($statusLines.Count -eq 0) { '' } else { ($statusLines -join "`n") + "`n" }
    $bytes = [Text.Encoding]::UTF8.GetBytes($text)
    $sha = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes))
    return [pscustomobject]@{ Path = $Path; Count = $statusLines.Count; Text = $text; Sha256 = $sha }
}
$protectedBefore = @($protectedTrees | ForEach-Object { Get-StatusSnapshot $_ })
if ($protectedBefore[0].Count -ne 65 -or $protectedBefore[1].Count -ne 67) {
    throw "Protected runtime status counts changed: out/rt=$($protectedBefore[0].Count), staging=$($protectedBefore[1].Count)"
}

$PartitionedRuntimeSourceRoot = [IO.Path]::GetFullPath($PartitionedRuntimeSourceRoot)
foreach ($required in @(
    (Join-Path $PartitionedRuntimeSourceRoot 'src\coreclr\nativeaot\Runtime\thread.bootstrap.cpp'),
    (Join-Path $PartitionedRuntimeSourceRoot 'src\coreclr\nativeaot\Runtime\threadstore.bootstrap.cpp'),
    (Join-Path $PartitionedRuntimeSourceRoot 'eng\native\configureplatform.cmake'),
    (Join-Path $repo 'out\dotnet\phase23r13-source-partition-build\source-partition-verification.log')
)) {
    if (-not (Test-Path -LiteralPath $required -PathType Leaf)) {
        throw "Required P23R13 partition provenance input is absent: $required"
    }
}
$overlaySourceRoot = Join-Path $OutputRoot 'p23r14-runtime-source'
if (-not (Test-Path -LiteralPath $OutputRoot)) {
    New-Item -ItemType Directory -Path $OutputRoot | Out-Null
}
$baselineBuildRoot = Join-Path $repo 'out\dotnet\phase23r13-partition-workstationgc'
$baselineLinkRoot = Join-Path $OutputRoot 'p23r13-baseline-link-diagnostics'
$baselineRecordPath = Join-Path $baselineLinkRoot 'bootstrap-link-partitioning-record.json'
if (-not ($ResumeExistingSource -and (Test-Path -LiteralPath $baselineRecordPath -PathType Leaf))) {
    & (Join-Path $repo 'Tools\Phase23\run_bootstrap_link_partitioning_experiment.ps1') `
        -RuntimeSourceRoot $RuntimeSourceRoot `
        -RuntimeBuildRoot $baselineBuildRoot `
        -OutputRoot $baselineLinkRoot
}
if (-not (Test-Path -LiteralPath $baselineRecordPath -PathType Leaf)) {
    throw 'P23R13 baseline link diagnostics were not produced.'
}
$baselineRecord = Get-Content -LiteralPath $baselineRecordPath -Raw | ConvertFrom-Json
if ($baselineRecord.unresolvedExternalCount -ne 48 -or $baselineRecord.linkExitCode -eq 0) {
    throw "Expected the P23R13 48-symbol standard-link baseline; observed $($baselineRecord.unresolvedExternalCount)."
}

if (-not (Test-Path -LiteralPath $overlaySourceRoot -PathType Container)) {
    if ($ResumeExistingSource) { throw 'Resume requested but the P23R14 source copy is missing.' }
    $copyLog = Join-Path $OutputRoot 'copy-p23r13-source.log'
    $copyOutput = & robocopy.exe $PartitionedRuntimeSourceRoot $overlaySourceRoot /E /COPY:DAT /DCOPY:DAT /R:0 /W:0 /MT:8 /XJ 2>&1
    $copyExit = $LASTEXITCODE
    $copyOutput | Set-Content -LiteralPath $copyLog -Encoding UTF8
    if ($copyExit -ge 8) {
        Get-Content -LiteralPath $copyLog -Tail 80
        throw "Unable to copy the existing P23R13 partitioned runtime source (robocopy exit $copyExit)."
    }
}
elseif (-not $ResumeExistingSource) {
    throw "Refusing to reuse an existing P23R14 source copy without -ResumeExistingSource: $overlaySourceRoot"
}

$patchPaths = @(
    (Join-Path $repo 'Tools\Phase23\runtime-overlay\p23r14_guidexos_hijack_unsupported.patch')
)
Push-Location $overlaySourceRoot
try {
    foreach ($patchPath in $patchPaths) {
        if (-not (Test-Path -LiteralPath $patchPath -PathType Leaf)) {
            throw "Required runtime overlay patch is missing: $patchPath"
        }
        & git apply --reverse --check --unidiff-zero $patchPath
        if ($LASTEXITCODE -eq 0) { continue }
        & git apply --check $patchPath
        if ($LASTEXITCODE -ne 0) { throw "Overlay does not apply cleanly: $patchPath" }
        & git apply $patchPath
        if ($LASTEXITCODE -ne 0) { throw "Overlay application failed: $patchPath" }
        & git apply --reverse --check --unidiff-zero $patchPath
        if ($LASTEXITCODE -ne 0) { throw "Applied overlay cannot be reversed cleanly: $patchPath" }
    }
}
finally {
    Pop-Location
}

$vsRoot = 'C:\Program Files\Microsoft Visual Studio\18\Community'
$vcvars = Join-Path $vsRoot 'VC\Auxiliary\Build\vcvars64.bat'
$vsTools = Join-Path $vsRoot 'VC\Tools\MSVC\14.51.36231\bin\Hostx64\x64'
$compiler = Join-Path $vsTools 'cl.exe'
$linker = Join-Path $vsTools 'link.exe'
$dumpbin = Join-Path $vsTools 'dumpbin.exe'
$cmake = 'C:\mingw64\bin\cmake.exe'
$ninja = 'C:\mingw64\bin\ninja.exe'
foreach ($path in @($vcvars, $compiler, $linker, $dumpbin, $cmake, $ninja)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Required pinned x64 build tool is unavailable: $path" }
}

$runtimeBuildRoot = Join-Path $OutputRoot 'workstationgc-build'
$runtimeArchivePath = Join-Path $runtimeBuildRoot 'nativeaot\Runtime\Full\Runtime.WorkstationGC.lib'
$configureCommand = Join-Path $OutputRoot 'configure-workstationgc.cmd'
$configureLog = Join-Path $OutputRoot 'configure-workstationgc.log'
$buildLog = Join-Path $OutputRoot 'runtime-build.log'
$configureLines = @(
    '@echo off',
    'setlocal',
    ('call "{0}" x64 >nul' -f $vcvars),
    'if errorlevel 1 exit /b %errorlevel%',
    ('"{0}" -S "{1}" -B "{2}" -G Ninja -DCMAKE_BUILD_TYPE=Release -DCMAKE_C_COMPILER="{3}" -DCMAKE_CXX_COMPILER="{3}" -DCMAKE_MAKE_PROGRAM="{4}" "-DCMAKE_CXX_FLAGS=/DWIN32 /D_WINDOWS /EHsc /Gw" -DCLR_CMAKE_HOST_ARCH=x64 -DCLR_CMAKE_TARGET_ARCH=x64 -DCLR_CMAKE_TARGET_OS=guidexos -DCLR_CMAKE_ENABLE_CODE_COVERAGE=OFF' -f $cmake, (Join-Path $overlaySourceRoot 'src\coreclr'), $runtimeBuildRoot, $compiler, $ninja),
    'if errorlevel 1 exit /b %errorlevel%',
    ('"{0}" --build "{1}" --target Runtime.WorkstationGC -- -v > "{2}" 2>&1' -f $cmake, $runtimeBuildRoot, $buildLog),
    'if errorlevel 1 exit /b %errorlevel%',
    'exit /b 0'
)
if (-not ($ResumeExistingSource -and (Test-Path -LiteralPath $runtimeArchivePath -PathType Leaf))) {
    Set-Content -LiteralPath $configureCommand -Value $configureLines -Encoding ASCII
    $buildOutput = & cmd.exe /d /s /c ('"{0}"' -f $configureCommand) 2>&1
    $buildExit = $LASTEXITCODE
    $buildOutput | Set-Content -LiteralPath $configureLog -Encoding UTF8
    if ($buildExit -ne 0) {
        Get-Content -LiteralPath $configureLog -Tail 50
        Get-Content -LiteralPath $buildLog -Tail 80 -ErrorAction SilentlyContinue
        throw "Isolated source-partitioned WorkstationGC build failed with exit code $buildExit."
    }
}

if (-not (Test-Path -LiteralPath $runtimeArchivePath -PathType Leaf)) {
    throw "Fresh WorkstationGC archive was not produced: $runtimeArchivePath"
}
$objectRoot = Join-Path $runtimeBuildRoot 'nativeaot\Runtime\Full\CMakeFiles\Runtime.WorkstationGC.dir'
$stackBoundsObject = Get-ChildItem -LiteralPath $objectRoot -Filter 'PalRedhawkStackBounds.cpp.obj' -Recurse -File | Select-Object -First 1
if ($null -eq $stackBoundsObject) { throw 'Fresh target stack-bounds PAL object was not found.' }
$stackBoundsSourceRoot = Join-Path $OutputRoot 'stack-bounds-test-object-root'
$stackBoundsObjectRelativePath = 'artifacts\obj\coreclr\guidexos.x64.Release\nativeaot\Runtime\Full\CMakeFiles\Runtime.WorkstationGC.dir\__\guidexos\PalRedhawkStackBounds.cpp.obj'
$stackBoundsObjectCopy = Join-Path $stackBoundsSourceRoot $stackBoundsObjectRelativePath
New-Item -ItemType Directory -Path (Split-Path -Parent $stackBoundsObjectCopy) -Force | Out-Null
Copy-Item -LiteralPath $stackBoundsObject.FullName -Destination $stackBoundsObjectCopy

$stackBoundsDescriptorRoot = Join-Path $OutputRoot 'stack-bounds-descriptor-tests'
$descriptorRecordPath = Join-Path $stackBoundsDescriptorRoot 'stack-bounds-descriptor-test-record.json'
if (-not ($ResumeExistingSource -and (Test-Path -LiteralPath $descriptorRecordPath -PathType Leaf))) {
    & (Join-Path $repo 'Tools\Phase23\run_stack_bounds_descriptor_tests.ps1') `
        -SourceRoot $stackBoundsSourceRoot `
        -OutputRoot $stackBoundsDescriptorRoot
}
if (-not (Test-Path -LiteralPath $descriptorRecordPath -PathType Leaf)) {
    throw 'Descriptor/PAL regression did not produce its test record.'
}
$stackBoundsUnsupportedRoot = Join-Path $OutputRoot 'stack-bounds-unsupported-tests'
$unsupportedStackBoundsRecordPath = Join-Path $stackBoundsUnsupportedRoot 'stack-bounds-test-record.json'
if (-not ($ResumeExistingSource -and (Test-Path -LiteralPath $unsupportedStackBoundsRecordPath -PathType Leaf))) {
    & (Join-Path $repo 'Tools\Phase23\run_stack_bounds_unsupported_tests.ps1') `
        -SourceRoot $stackBoundsSourceRoot `
        -OutputRoot $stackBoundsUnsupportedRoot
}
if (-not (Test-Path -LiteralPath $unsupportedStackBoundsRecordPath -PathType Leaf)) {
    throw 'Unsupported stack-bounds regression did not produce its test record.'
}
$hostedContextRoot = Join-Path $OutputRoot 'hosted-worker-context-tests'
$hostedContextRecordPath = Join-Path $hostedContextRoot 'hosted-finalizer-context-test-record.json'
if (-not ($ResumeExistingSource -and (Test-Path -LiteralPath $hostedContextRecordPath -PathType Leaf))) {
    & (Join-Path $repo 'Tools\Phase23\run_hosted_finalizer_context_tests.ps1') `
        -OutputRoot $hostedContextRoot
}
if (-not (Test-Path -LiteralPath $hostedContextRecordPath -PathType Leaf)) {
    throw 'Hosted worker/fiber context regression did not produce its test record.'
}
$unwindLog = Join-Path $OutputRoot 'x64-unwind-tests.log'
$unwindOutput = @()
$unwindExit = 0
if ($ResumeExistingSource -and (Test-Path -LiteralPath $unwindLog -PathType Leaf)) {
    $unwindOutput = @(Get-Content -LiteralPath $unwindLog)
}
else {
    $unwindOutput = @(& (Join-Path $repo 'Tests\NativeAotUnwind\run_x64_unwind_tests.ps1') 2>&1)
    $unwindExit = $LASTEXITCODE
    $unwindOutput | Set-Content -LiteralPath $unwindLog -Encoding UTF8
}
$unwindPassLines = @($unwindOutput | Where-Object { $_ -match '^RESULT PASS tests=69 failed=0' })
if ($unwindExit -ne 0 -or $unwindPassLines.Count -ne 2) {
    Get-Content -LiteralPath $unwindLog -Tail 80
    throw 'Bounded/Windows-reference x64 unwind regressions did not both pass.'
}

$archiveSymbolsPath = Join-Path $OutputRoot 'runtime-archive-symbols.txt'
& $dumpbin /nologo /linkermember:2 $runtimeArchivePath 2>&1 |
    Set-Content -LiteralPath $archiveSymbolsPath -Encoding UTF8
if ($LASTEXITCODE -ne 0) { throw 'Unable to inspect the freshly rebuilt runtime archive.' }
$archiveSymbolText = Get-Content -LiteralPath $archiveSymbolsPath -Raw
foreach ($symbol in @('PalGetHijackTarget', 'PalRegisterHijackCallback', 'PalHijack', 'PalAreShadowStacksEnabled')) {
    if ($archiveSymbolText -notmatch [regex]::Escape($symbol)) {
        throw "Fresh archive is missing target hijack contract symbol: $symbol"
    }
}

$linkProbeRoot = Join-Path $OutputRoot 'normal-link-diagnostics'
$normalLinkRecordPath = Join-Path $linkProbeRoot 'bootstrap-link-partitioning-record.json'
if (-not ($ResumeExistingSource -and (Test-Path -LiteralPath $normalLinkRecordPath -PathType Leaf))) {
    & (Join-Path $repo 'Tools\Phase23\run_bootstrap_link_partitioning_experiment.ps1') `
        -RuntimeSourceRoot $RuntimeSourceRoot `
        -RuntimeBuildRoot $runtimeBuildRoot `
        -OutputRoot $linkProbeRoot
}
if (-not (Test-Path -LiteralPath $normalLinkRecordPath -PathType Leaf)) {
    throw 'Normal bootstrap link diagnostics could not be regenerated.'
}

$compileDatabasePath = Join-Path $runtimeBuildRoot 'compile_commands.json'
$compileDatabase = Get-Content -LiteralPath $compileDatabasePath -Raw | ConvertFrom-Json
$providerEntry = $compileDatabase | Where-Object {
    $_.command -match 'Runtime\.WorkstationGC\.dir.*PalRedhawkHijackUnsupported\.cpp'
} | Select-Object -First 1
if ($null -eq $providerEntry) { throw 'No target-PAL compile command was emitted for the hijack provider.' }
$compileCommand = [string]$providerEntry.command
$compileCut = $compileCommand.LastIndexOf(' -c ', [StringComparison]::Ordinal)
if ($compileCut -lt 0) { throw 'Target-PAL compile command has no final source argument.' }
$compilePrefix = $compileCommand.Substring(0, $compileCut)
$compilerMatch = [regex]::Match($compilePrefix, '^(?:"(?<path>[^"]+)"|(?<path>\S+))')
if (-not $compilerMatch.Success) { throw 'Unable to resolve compiler from the target-PAL compile command.' }
$compiler = $compilerMatch.Groups['path'].Value
$compilePrefix = [regex]::Replace($compilePrefix, '(?i)/Fo(?:"[^"]*"|\S+)', ('/Fo"{0}"' -f (Join-Path $OutputRoot 'hijack-contract-tests.obj')))
$compilePrefix = [regex]::Replace($compilePrefix, '(?i)/Fd(?:"[^"]*"|\S+)', ('/Fd"{0}"' -f (Join-Path $OutputRoot 'hijack-contract-tests.pdb')))
$testSource = Join-Path $repo 'Tests\NativeAotUnwind\bootstrap_hijack_target_contract_tests.cpp'
$testObject = Join-Path $OutputRoot 'hijack-contract-tests.obj'
$compileCommandFile = Join-Path $OutputRoot 'compile-hijack-contract-tests.cmd'
$compileLines = @(
    '@echo off',
    'setlocal',
    ('call "{0}" x64 >nul' -f $vcvars),
    'if errorlevel 1 exit /b %errorlevel%',
    ('cd /d "{0}"' -f $providerEntry.directory),
    'if errorlevel 1 exit /b %errorlevel%',
    ('{0} -c "{1}"' -f $compilePrefix, $testSource),
    'exit /b %errorlevel%'
)
Set-Content -LiteralPath $compileCommandFile -Value $compileLines -Encoding ASCII
$compileOutput = & cmd.exe /d /s /c ('"{0}"' -f $compileCommandFile) 2>&1
$compileExit = $LASTEXITCODE
$compileOutput | Set-Content -LiteralPath (Join-Path $OutputRoot 'compile-hijack-contract-tests.log') -Encoding UTF8
if ($compileExit -ne 0) {
    Get-Content -LiteralPath (Join-Path $OutputRoot 'compile-hijack-contract-tests.log') -Tail 60
    throw "Hijack contract test compilation failed with exit code $compileExit."
}

$providerObject = Get-ChildItem -LiteralPath $objectRoot -Filter 'PalRedhawkHijackUnsupported.cpp.obj' -Recurse -File | Select-Object -First 1
$registrationObject = Get-ChildItem -LiteralPath $objectRoot -Filter 'PalRedhawkHijackRegistrationUnsupported.cpp.obj' -Recurse -File | Select-Object -First 1
if ($null -eq $providerObject -or $null -eq $registrationObject) {
    throw 'Fresh target hijack provider object files were not found in the WorkstationGC build.'
}

$testExecutable = Join-Path $OutputRoot 'bootstrap_hijack_target_contract_tests.exe'
$testLinkLog = Join-Path $OutputRoot 'link-hijack-contract-tests.log'
$testLinkCommand = '"{0}" /nologo /OUT:"{1}" /INCREMENTAL:NO /OPT:REF /SUBSYSTEM:CONSOLE "{2}" "{3}" "{4}" kernel32.lib > "{5}" 2>&1' -f $linker, $testExecutable, $testObject, $providerObject.FullName, $registrationObject.FullName, $testLinkLog
$testLinkCommandFile = Join-Path $OutputRoot 'link-hijack-contract-tests.cmd'
$testLinkLines = @(
    '@echo off',
    'setlocal',
    ('call "{0}" x64 >nul' -f $vcvars),
    'if errorlevel 1 exit /b %errorlevel%',
    $testLinkCommand,
    'exit /b %errorlevel%'
)
Set-Content -LiteralPath $testLinkCommandFile -Value $testLinkLines -Encoding ASCII
$testLinkOutput = & cmd.exe /d /s /c ('"{0}"' -f $testLinkCommandFile) 2>&1
$testLinkExit = $LASTEXITCODE
$testLinkOutput | ForEach-Object { Write-Output $_ }
if ($testLinkExit -ne 0) {
    Get-Content -LiteralPath $testLinkLog -Tail 60 -ErrorAction SilentlyContinue
    throw "Hijack contract test link failed with exit code $testLinkExit."
}

$testLog = Join-Path $OutputRoot 'hijack-contract-tests.log'
$testOutput = & $testExecutable 2>&1
$testExit = $LASTEXITCODE
$testOutput | Set-Content -LiteralPath $testLog -Encoding UTF8
if ($testExit -ne 0) {
    Get-Content -LiteralPath $testLog -Raw
    throw "Hijack contract behavioral tests failed with exit code $testExit."
}

$hostTestSource = Join-Path $repo 'Tests\NativeAotUnwind\bootstrap_hijack_windows_host_tests.cpp'
$hostTestObject = Join-Path $OutputRoot 'hijack-windows-host-tests.obj'
$hostTestExecutable = Join-Path $OutputRoot 'bootstrap_hijack_windows_host_tests.exe'
$hostTestCompileCommandFile = Join-Path $OutputRoot 'compile-hijack-windows-host-tests.cmd'
$hostTestCompileLines = @(
    '@echo off',
    'setlocal',
    ('call "{0}" x64 >nul' -f $vcvars),
    'if errorlevel 1 exit /b %errorlevel%',
    ('"{0}" /nologo /std:c++17 /EHsc /W4 /WX /c /Fo"{1}" "{2}"' -f $compiler, $hostTestObject, $hostTestSource),
    'exit /b %errorlevel%'
)
Set-Content -LiteralPath $hostTestCompileCommandFile -Value $hostTestCompileLines -Encoding ASCII
$hostTestCompileOutput = & cmd.exe /d /s /c ('"{0}"' -f $hostTestCompileCommandFile) 2>&1
$hostTestCompileExit = $LASTEXITCODE
$hostTestCompileOutput | Set-Content -LiteralPath (Join-Path $OutputRoot 'compile-hijack-windows-host-tests.log') -Encoding UTF8
if ($hostTestCompileExit -ne 0) {
    Get-Content -LiteralPath (Join-Path $OutputRoot 'compile-hijack-windows-host-tests.log') -Tail 60
    throw "Windows-host hijack behavior compilation failed with exit code $hostTestCompileExit."
}
$hostTestLinkLog = Join-Path $OutputRoot 'link-hijack-windows-host-tests.log'
$hostTestLinkCommand = '"{0}" /nologo /OUT:"{1}" /INCREMENTAL:NO /OPT:REF /SUBSYSTEM:CONSOLE "{2}" kernel32.lib > "{3}" 2>&1' -f $linker, $hostTestExecutable, $hostTestObject, $hostTestLinkLog
$hostTestLinkCommandFile = Join-Path $OutputRoot 'link-hijack-windows-host-tests.cmd'
$hostTestLinkLines = @(
    '@echo off',
    'setlocal',
    ('call "{0}" x64 >nul' -f $vcvars),
    'if errorlevel 1 exit /b %errorlevel%',
    $hostTestLinkCommand,
    'exit /b %errorlevel%'
)
Set-Content -LiteralPath $hostTestLinkCommandFile -Value $hostTestLinkLines -Encoding ASCII
$hostTestLinkOutput = & cmd.exe /d /s /c ('"{0}"' -f $hostTestLinkCommandFile) 2>&1
$hostTestLinkExit = $LASTEXITCODE
$hostTestLinkOutput | ForEach-Object { Write-Output $_ }
if ($hostTestLinkExit -ne 0) {
    Get-Content -LiteralPath $hostTestLinkLog -Tail 60 -ErrorAction SilentlyContinue
    throw "Windows-host hijack behavior link failed with exit code $hostTestLinkExit."
}
$hostTestLog = Join-Path $OutputRoot 'hijack-windows-host-tests.log'
$hostTestOutput = & $hostTestExecutable 2>&1
$hostTestExit = $LASTEXITCODE
$hostTestOutput | Set-Content -LiteralPath $hostTestLog -Encoding UTF8
if ($hostTestExit -ne 0) {
    Get-Content -LiteralPath $hostTestLog -Raw
    throw "Windows-host hijack behavior tests failed with exit code $hostTestExit."
}

$protectedAfter = @($protectedTrees | ForEach-Object { Get-StatusSnapshot $_ })
for ($index = 0; $index -lt $protectedBefore.Count; $index++) {
    if ($protectedBefore[$index].Text -cne $protectedAfter[$index].Text) {
        throw "Protected runtime checkout changed during P23R14: $($protectedTrees[$index])"
    }
}

$normalLinkRecordPath = Join-Path $linkProbeRoot 'bootstrap-link-partitioning-record.json'
$normalLinkRecord = Get-Content -LiteralPath $normalLinkRecordPath -Raw | ConvertFrom-Json
$sourcePartitionLog = Join-Path $OutputRoot 'source-partition-verification.log'
$sourcePartitionMap = Join-Path $linkProbeRoot 'forced_link.map'
$sourcePartitionVerifier = Join-Path $repo 'Tools\Phase23\verify_bootstrap_source_partition.py'
$sourcePartitionOutput = & python $sourcePartitionVerifier `
    --baseline-runtime-root (Join-Path $RuntimeSourceRoot 'src\coreclr\nativeaot\Runtime') `
    --partitioned-runtime-root (Join-Path $overlaySourceRoot 'src\coreclr\nativeaot\Runtime') `
    --link-map $sourcePartitionMap 2>&1
$sourcePartitionExit = $LASTEXITCODE
$sourcePartitionOutput | Set-Content -LiteralPath $sourcePartitionLog -Encoding UTF8
if ($sourcePartitionExit -ne 0) {
    Get-Content -LiteralPath $sourcePartitionLog -Tail 80
    throw 'P23R13 source-partition provenance verification failed.'
}
$testSummary = [ordered]@{
    schema = 1
    generatedBy = 'Tools/Phase23/run_bootstrap_hijack_target_contract_tests.ps1'
    runtimeCommit = $runtimeHead
    p23r6PatchSha256 = (Get-FileHash -LiteralPath (Join-Path $repo 'Tools\Phase23\runtime-overlay\p23r6_runtime.patch') -Algorithm SHA256).Hash.ToUpperInvariant()
    p23r7PatchSha256 = (Get-FileHash -LiteralPath (Join-Path $repo 'Tools\Phase23\runtime-overlay\p23r7_runtime.patch') -Algorithm SHA256).Hash.ToUpperInvariant()
    p23r8PatchSha256 = (Get-FileHash -LiteralPath (Join-Path $repo 'Tools\Phase23\runtime-overlay\p23r8_runtime.patch') -Algorithm SHA256).Hash.ToUpperInvariant()
    p23r13PatchSha256 = (Get-FileHash -LiteralPath (Join-Path $repo 'Tools\Phase23\runtime-overlay\p23r13_bootstrap_source_partition.patch') -Algorithm SHA256).Hash.ToUpperInvariant()
    p23r14PatchSha256 = (Get-FileHash -LiteralPath $patchPaths[0] -Algorithm SHA256).Hash.ToUpperInvariant()
    partitionedRuntimeSourceRoot = $PartitionedRuntimeSourceRoot
    partitionedRuntimeSourceSha256 = (Get-FileHash -LiteralPath (Join-Path $PartitionedRuntimeSourceRoot 'src\coreclr\nativeaot\Runtime\thread.bootstrap.cpp') -Algorithm SHA256).Hash.ToUpperInvariant()
    sourcePartitionVerificationLog = $sourcePartitionLog
    sourcePartitionVerificationLogSha256 = (Get-FileHash -LiteralPath $sourcePartitionLog -Algorithm SHA256).Hash.ToUpperInvariant()
    sourcePartitionVerificationExitCode = $sourcePartitionExit
    runtimeArchive = $runtimeArchivePath
    runtimeArchiveSha256 = (Get-FileHash -LiteralPath $runtimeArchivePath -Algorithm SHA256).Hash.ToUpperInvariant()
    runtimeBuildLog = $buildLog
    descriptorPalTestRecord = Join-Path $stackBoundsDescriptorRoot 'stack-bounds-descriptor-test-record.json'
    unsupportedStackBoundsTestRecord = Join-Path $stackBoundsUnsupportedRoot 'stack-bounds-test-record.json'
    hostedWorkerContextTestRecord = Join-Path $hostedContextRoot 'hosted-finalizer-context-test-record.json'
    unwindTestLog = $unwindLog
    unwindTestExitCode = $unwindExit
    contractTestSource = $testSource
    contractTestExecutable = $testExecutable
    contractTestExecutableSha256 = (Get-FileHash -LiteralPath $testExecutable -Algorithm SHA256).Hash.ToUpperInvariant()
    contractTestLog = $testLog
    contractTestExitCode = $testExit
    windowsHostTestSource = $hostTestSource
    windowsHostTestExecutable = $hostTestExecutable
    windowsHostTestExecutableSha256 = (Get-FileHash -LiteralPath $hostTestExecutable -Algorithm SHA256).Hash.ToUpperInvariant()
    windowsHostTestLog = $hostTestLog
    windowsHostTestExitCode = $hostTestExit
    bootstrapNormalLinkExitCode = $normalLinkRecord.linkExitCode
    bootstrapUnresolvedExternalCount = $normalLinkRecord.unresolvedExternalCount
    bootstrapUnresolvedSymbols = $normalLinkRecord.unresolvedSymbols
    p23r13BaselineArchiveSha256 = $baselineRecord.runtimeArchiveSha256
    p23r13BaselineLinkExitCode = $baselineRecord.linkExitCode
    p23r13BaselineUnresolvedExternalCount = $baselineRecord.unresolvedExternalCount
    p23r13BaselineUnresolvedSymbols = $baselineRecord.unresolvedSymbols
    bootstrapExecutableCreated = (Test-Path -LiteralPath (Join-Path $linkProbeRoot 'bootstrap_threadstore_link_probe.exe') -PathType Leaf)
    bootstrapExecutableRan = $false
    forcedDiagnosticExecutableWasRun = $normalLinkRecord.forcedDiagnosticExecutableWasRun
    protectedTreeStatus = @($protectedAfter | ForEach-Object {
        [ordered]@{ path = $_.Path; count = $_.Count; sha256 = $_.Sha256 }
    })
    outcome = 'P23R14_HIJACK_ARCHITECTURE_BLOCKED'
    scope = 'Target PAL rejects hijack registration, returns no target, and fail-fasts on attempted suspension or unknown CET query. Windows host OS APC/context behavior is tested separately; this is not NativeAOT ThreadStore execution.'
}
$summaryPath = Join-Path $OutputRoot 'p23r14-hijack-contract-record.json'
$testSummary | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $summaryPath -Encoding UTF8
$testSummary | ConvertTo-Json -Depth 12
