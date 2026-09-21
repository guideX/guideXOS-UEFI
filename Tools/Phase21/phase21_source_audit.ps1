[CmdletBinding()]
param(
    [string]$SourceRoot = '',
    [string]$OutputRoot = '',
    [string]$CompileCommandsPath = ''
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
if ([string]::IsNullOrWhiteSpace($SourceRoot)) { $SourceRoot = Join-Path $root 'out\rt' }
if ([string]::IsNullOrWhiteSpace($OutputRoot)) { $OutputRoot = Join-Path $root 'out\dotnet\phase21-source-audit' }

$lock = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'runtime-source.lock.json') -Raw | ConvertFrom-Json
$patchRecordPath = Join-Path $root 'out\dotnet\phase21-source-patch\phase21-source-patch-record.json'
if (-not (Test-Path -LiteralPath $SourceRoot -PathType Container)) { throw "Runtime source is absent: $SourceRoot" }
if (-not (Test-Path -LiteralPath $patchRecordPath -PathType Leaf)) { throw "Phase 21 patch record is absent: $patchRecordPath" }
$patchRecord = Get-Content -LiteralPath $patchRecordPath -Raw | ConvertFrom-Json
$actualCommit = (& git -C $SourceRoot rev-parse HEAD).Trim()
if ($actualCommit -ne [string]$lock.runtimeCommit) { throw "Runtime source mismatch. Expected $($lock.runtimeCommit), found $actualCommit." }

function SourcePath([string]$relativePath) { Join-Path $SourceRoot ($relativePath -replace '/', '\') }
function ReadSource([string]$relativePath) { Get-Content -LiteralPath (SourcePath $relativePath) -Raw }
function Require([bool]$condition, [string]$message) { if (-not $condition) { throw "Phase 21 source audit failed: $message" } }

$requiredPaths = @(
    'eng/native/configureplatform.cmake',
    'eng/native/configurecompiler.cmake',
    'eng/build.ps1',
    'Directory.Build.props',
    'src/coreclr/clrdefinitions.cmake',
    'src/coreclr/nativeaot/Directory.Build.props',
    'src/coreclr/nativeaot/Runtime/CMakeLists.txt',
    'src/coreclr/nativeaot/Runtime/Full/CMakeLists.txt',
    'src/native/libs/CMakeLists.txt',
    'src/coreclr/gc/env/gcenv.base.h',
    'src/coreclr/gc/env/gcenv.structs.h',
    'src/coreclr/gc/env/gcenv.guidexos.inl',
    'src/coreclr/gc/guidexos/gcenv.guidexos.cpp',
    'src/coreclr/nativeaot/Runtime/guidexos/AsmOffsets.cpp',
    'src/coreclr/nativeaot/Runtime/guidexos/GuidexosGcPal.h'
)
foreach ($path in $requiredPaths) { Require (Test-Path -LiteralPath (SourcePath $path) -PathType Leaf) "required path is absent: $path" }

$platform = ReadSource 'eng/native/configureplatform.cmake'
$compiler = ReadSource 'eng/native/configurecompiler.cmake'
$rootProps = ReadSource 'Directory.Build.props'
$clrDefinitions = ReadSource 'src/coreclr/clrdefinitions.cmake'
$nativeAotProps = ReadSource 'src/coreclr/nativeaot/Directory.Build.props'
$runtimeCmake = ReadSource 'src/coreclr/nativeaot/Runtime/CMakeLists.txt'
$fullCmake = ReadSource 'src/coreclr/nativeaot/Runtime/Full/CMakeLists.txt'
$nativeLibsCmake = ReadSource 'src/native/libs/CMakeLists.txt'
$gcBase = ReadSource 'src/coreclr/gc/env/gcenv.base.h'
$gcStructs = ReadSource 'src/coreclr/gc/env/gcenv.structs.h'
$gcGuidexos = ReadSource 'src/coreclr/gc/guidexos/gcenv.guidexos.cpp'
$gcInline = ReadSource 'src/coreclr/gc/env/gcenv.guidexos.inl'
$guidexosFiles = @(
    'src/coreclr/gc/guidexos/gcenv.guidexos.cpp',
    'src/coreclr/gc/env/gcenv.guidexos.inl',
    'src/coreclr/nativeaot/Runtime/guidexos/AsmOffsets.cpp',
    'src/coreclr/nativeaot/Runtime/guidexos/GuidexosGcPal.h'
)
$guidexosText = ($guidexosFiles | ForEach-Object { ReadSource $_ }) -join "`n"

Require ($platform -match '(?m)CLR_CMAKE_TARGET_OS STREQUAL guidexos') 'target OS value is not recognized'
Require ($platform -match '(?m)set\(CLR_CMAKE_TARGET_GUIDEXOS 1\)') 'CLR_CMAKE_TARGET_GUIDEXOS is not defined'
Require ($platform -match '(?m)CLR_CMAKE_HOST_OS STREQUAL windows') 'host OS identity is not visible in target selection'
Require ($platform -match '(?m)CLR_CMAKE_TARGET_GUIDEXOS AND NOT CLR_CMAKE_HOST_OS STREQUAL windows') 'Windows-host-only GUIDEXOS guard is absent'
Require ($compiler -match '(?m)elseif\(CLR_CMAKE_TARGET_GUIDEXOS\)') 'compiler target branch is absent'
Require ($compiler -match '(?m)TARGET_GUIDEXOS') 'TARGET_GUIDEXOS is not emitted'
Require ((ReadSource 'eng/build.ps1') -match '(?m)ValidateSet\("windows","linux","osx","android","browser","wasi","guidexos"\)') 'build front-end does not accept guidexos'
Require ($rootProps -match '(?m)TargetsGuidexos') 'TargetsGuidexos managed property is absent'
Require ($nativeAotProps -match '(?m)TargetsGuidexos.*TARGET_GUIDEXOS') 'NativeAOT managed TARGET_GUIDEXOS definition is absent'
Require ($clrDefinitions -match '(?m)TARGETDETAILS_OS MATCHES "\^guidexos"') 'custom target definition does not recognize guidexos'
Require ($gcBase -match '(?m)defined\(TARGET_UNIX\) \|\| defined\(TARGET_GUIDEXOS\)') 'portable GC aliases were not extended'
Require ($gcStructs -match '(?m)TARGET_GUIDEXOS') 'GC structures do not have a GUIDEXOS path'
Require ($runtimeCmake -match '(?m)CLR_CMAKE_TARGET_WIN32') 'Runtime CMake still keys target selection on host WIN32'
Require ($runtimeCmake -match '(?m)elseif \(CLR_CMAKE_TARGET_GUIDEXOS\)') 'Runtime CMake GUIDEXOS branch is absent'
Require ($runtimeCmake -match '(?m)gcenv\.guidexos\.cpp') 'Runtime CMake does not select gcenv.guidexos.cpp'
Require ($fullCmake -match '(?m)CLR_CMAKE_HOST_WIN32') 'Full runtime assembler selection lost host/target separation'
Require ($fullCmake -match '(?ms)CLR_CMAKE_TARGET_GUIDEXOS.*?guidexos/AsmOffsets\.cpp') 'GUIDEXOS AsmOffsets selection is absent'
Require ($nativeLibsCmake -match '(?m)CLR_CMAKE_TARGET_BROWSER OR CLR_CMAKE_TARGET_WASI OR CLR_CMAKE_TARGET_GUIDEXOS') 'unsupported native security libraries are not excluded for GUIDEXOS'

$guidexosBranch = [regex]::Match($runtimeCmake, '(?ms)elseif \(CLR_CMAKE_TARGET_GUIDEXOS\)(.*?)(?=\nelse\(\))')
Require $guidexosBranch.Success 'could not isolate GUIDEXOS runtime source branch'
Require ($guidexosBranch.Groups[1].Value -notmatch 'gcenv\.windows|PalRedhawkCommon|PalRedhawkMinWin|windows/CoffNativeCodeManager') 'GUIDEXOS branch references a Windows target source'
$windowsBranch = [regex]::Match($runtimeCmake, '(?ms)if \(CLR_CMAKE_TARGET_WIN32\)(.*?)(?=\nelseif \(CLR_CMAKE_TARGET_GUIDEXOS\))')
Require $windowsBranch.Success 'could not isolate Windows runtime source branch'
Require ($windowsBranch.Groups[1].Value -match 'gcenv\.windows\.cpp') 'Windows branch no longer names its own GC source'

$forbiddenTargetPatterns = @(
    '(?i)windows\.h', '(?i)\bKERNEL32(?:\.dll|\.lib)?\b', '(?i)\bADVAPI32(?:\.dll|\.lib)?\b',
    '(?i)\bbcrypt(?:\.dll|\.lib)?\b', '(?i)\bole32(?:\.dll|\.lib)?\b',
    '(?i)WaitForSingleObject', '(?i)CreateEvent(?:W|A)?\b', '(?i)LoadLibrary(?:W|A)?\b',
    '(?i)win-x64', '(?i)gcenv\.windows\.cpp'
)
$forbiddenMatches = @()
foreach ($pattern in $forbiddenTargetPatterns) {
    if ($guidexosText -match $pattern) { $forbiddenMatches += $pattern }
}
Require ($forbiddenMatches.Count -eq 0) ("GUIDEXOS target files contain forbidden Windows surface: " + ($forbiddenMatches -join ', '))

$compileEvidence = [ordered]@{
    supplied = $false
    path = $null
    guidexosSourceEntries = 0
    windowsGcEntries = 0
    forbiddenEntryMatches = @()
}
if (-not [string]::IsNullOrWhiteSpace($CompileCommandsPath)) {
    if (-not (Test-Path -LiteralPath $CompileCommandsPath -PathType Leaf)) { throw "Compile commands file is absent: $CompileCommandsPath" }
    $compileText = Get-Content -LiteralPath $CompileCommandsPath -Raw
    $compileEvidence.supplied = $true
    $compileEvidence.path = (Resolve-Path $CompileCommandsPath).Path
    $compileEvidence.guidexosSourceEntries = ([regex]::Matches($compileText, '(?i)gc[\\/]guidexos[\\/]gcenv\.guidexos\.cpp')).Count
    $compileEvidence.windowsGcEntries = ([regex]::Matches($compileText, '(?i)gc[\\/]windows[\\/]gcenv\.windows\.cpp')).Count
    foreach ($pattern in @('(?i)gc[\\/]windows[\\/]gcenv\.windows\.cpp', '(?i)Runtime[\\/]windows[\\/]PalRedhawk', '(?i)KERNEL32', '(?i)ADVAPI32', '(?i)bcrypt', '(?i)ole32', '(?i)win-x64')) {
        if ($compileText -match $pattern) { $compileEvidence.forbiddenEntryMatches += $pattern }
    }
    Require ($compileEvidence.guidexosSourceEntries -gt 0) 'compile commands do not contain gcenv.guidexos.cpp'
    Require ($compileEvidence.windowsGcEntries -eq 0) 'compile commands contain gcenv.windows.cpp'
    Require ($compileEvidence.forbiddenEntryMatches.Count -eq 0) 'compile commands contain forbidden Windows target entries'
}

$trackedChanged = @(& git -C $SourceRoot diff --name-only | ForEach-Object { $_ -replace '/', '\' } | Sort-Object)
$untrackedChanged = @(& git -C $SourceRoot status --short --untracked-files=all | Where-Object { $_ -match '^\?\?\s+' } | ForEach-Object { ($_ -replace '^\?\?\s+', '') -replace '/', '\' } | Sort-Object)
$expectedTracked = @($lock.sourceFiles | ForEach-Object { $_.path -replace '/', '\' } | Sort-Object)
$expectedUntracked = @($lock.repositoryOwnedTemplates | ForEach-Object { $_ -replace '/', '\' } | Sort-Object)
Require ((@($trackedChanged) -join '|') -eq (@($expectedTracked) -join '|')) 'tracked runtime diff does not match the approved source patch set'
Require ((@($untrackedChanged) -join '|') -eq (@($expectedUntracked) -join '|')) 'untracked runtime templates do not match the approved source patch set'

$result = [ordered]@{
    schema = 1
    generatedBy = 'Tools/Phase21/phase21_source_audit.ps1'
    generatedAtUtc = [DateTime]::UtcNow.ToString('o')
    target = $lock.target
    source = [ordered]@{
        checkout = (Resolve-Path $SourceRoot).Path
        commit = $actualCommit
        patchRecord = (Resolve-Path $patchRecordPath).Path
        patchRecordCommit = [string]$patchRecord.source.commit
        trackedFilesChanged = $trackedChanged
        untrackedFilesChanged = $untrackedChanged
    }
    platformIdentity = [ordered]@{
        targetOsValue = 'guidexos'
        cmakeTargetVariable = 'CLR_CMAKE_TARGET_GUIDEXOS'
        nativeDefine = 'TARGET_GUIDEXOS'
        managedProperty = 'TargetsGuidexos'
        hostBuildGuard = 'CLR_CMAKE_HOST_WIN32'
        targetRuntimeSelection = 'CLR_CMAKE_TARGET_GUIDEXOS'
    }
    windowsExclusion = [ordered]@{
        windowsGcSource = 'excluded from GUIDEXOS branch'
        windowsPalSources = 'excluded from GUIDEXOS branch'
        targetWindowsHeaders = 'absent from repository-owned GUIDEXOS source'
        forbiddenPatterns = $forbiddenMatches
        compileEvidence = $compileEvidence
    }
    failClosed = [ordered]@{
        missingGcBlockingWait = 'explicit abort in gcenv.guidexos.cpp'
        missingGcDecommit = 'returns failure; never reports a false success'
        missingGcReset = 'returns failure; never reports a false success'
        missingWindowsFallback = $true
        runtimePackProduced = $false
    }
    status = 'source-selection-proof-passed'
}

New-Item -ItemType Directory -Force -Path $OutputRoot | Out-Null
$outputPath = Join-Path $OutputRoot 'phase21-source-audit.json'
$result | ConvertTo-Json -Depth 16 | Set-Content -LiteralPath $outputPath -Encoding UTF8
$result | ConvertTo-Json -Depth 5
Write-Output "Wrote $outputPath"
