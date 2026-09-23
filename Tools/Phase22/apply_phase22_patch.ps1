[CmdletBinding()]
param(
    [string]$SourceRoot = '',
    [string]$OutputRoot = ''
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
if ([string]::IsNullOrWhiteSpace($SourceRoot)) { $SourceRoot = Join-Path $root 'out\rt' }
if ([string]::IsNullOrWhiteSpace($OutputRoot)) { $OutputRoot = Join-Path $root 'out\dotnet\phase22-source-patch' }

$lock = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'runtime-source.lock.json') -Raw | ConvertFrom-Json
$phase21Lock = Get-Content -LiteralPath (Join-Path $root 'Tools\Phase21\runtime-source.lock.json') -Raw | ConvertFrom-Json
$phase21RecordPath = Join-Path $root ($lock.phase21PatchRecord -replace '/', '\')
if (-not (Test-Path -LiteralPath $SourceRoot -PathType Container)) { throw "Runtime source is absent: $SourceRoot" }
if (-not (Test-Path -LiteralPath $phase21RecordPath -PathType Leaf)) { throw "Phase 21 patch record is absent: $phase21RecordPath" }

$actualCommit = (& git -C $SourceRoot rev-parse HEAD).Trim()
if ($actualCommit -ne [string]$lock.runtimeCommit -or $actualCommit -ne [string]$phase21Lock.runtimeCommit) {
    throw "Runtime source mismatch. Expected pinned commit $($lock.runtimeCommit), found $actualCommit."
}

function SourcePath([string]$relativePath) { Join-Path $SourceRoot ($relativePath -replace '/', '\') }
function Get-Hash([string]$path) { (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToUpperInvariant() }

$phase21ExpectedTracked = @($phase21Lock.sourceFiles.path | ForEach-Object { $_ -replace '/', '\' } | Sort-Object)
$phase22Paths = @(
    'src\coreclr\CMakeLists.txt',
    'src\coreclr\inc\daccess.h',
    'src\coreclr\nativeaot\CMakeLists.txt',
    'src\coreclr\nativeaot\Runtime\PalRedhawk.h',
    'src\coreclr\nativeaot\Runtime\gcenv.h',
    'src\coreclr\nativeaot\Runtime\startup.cpp',
    'src\coreclr\nativeaot\Runtime\regdisplay.h'
)
$phase22Templates = @($lock.repositoryOwnedTemplates | ForEach-Object { $_ -replace '/', '\' } | Sort-Object)
$phase21ActualTracked = @(& git -C $SourceRoot diff --name-only | ForEach-Object { $_ -replace '/', '\' } | Where-Object { $_ -notin $phase22Paths } | Sort-Object)
if ((@($phase21ActualTracked) -join '|') -ne (@($phase21ExpectedTracked) -join '|')) {
    throw 'The existing runtime cache is not exactly the accepted Phase 21 source state.'
}
$phase21ExpectedUntracked = @($phase21Lock.repositoryOwnedTemplates | ForEach-Object { $_ -replace '/', '\' } | Sort-Object)
$phase22ExpectedUntracked = @($phase21ExpectedUntracked + $phase22Templates | Sort-Object)
$phase21ActualUntracked = @(& git -C $SourceRoot status --short --untracked-files=all | Where-Object { $_ -match '^\?\?\s+' } | ForEach-Object { ($_ -replace '^\?\?\s+', '') -replace '/', '\' } | Sort-Object)
if ((@($phase21ActualUntracked) -join '|') -ne (@($phase21ExpectedUntracked) -join '|') -and
    (@($phase21ActualUntracked) -join '|') -ne (@($phase22ExpectedUntracked) -join '|')) {
    throw 'The existing runtime cache has unexpected untracked files; refusing to apply Phase 22.'
}

$relativePath = 'src/coreclr/CMakeLists.txt'
$path = SourcePath $relativePath
$before = Get-Content -LiteralPath $path -Raw
$before = $before.TrimStart([char]0xFEFF)
$oldPattern = 'if\(NOT CLR_CROSS_COMPONENTS_BUILD\)\r?\n  set\(CLR_SINGLE_FILE_HOST_ONLY 1\)'
$newPattern = 'if\(NOT CLR_CROSS_COMPONENTS_BUILD AND NOT CLR_CMAKE_TARGET_GUIDEXOS\)\r?\n  set\(CLR_SINGLE_FILE_HOST_ONLY 1\)'
$new = "if(NOT CLR_CROSS_COMPONENTS_BUILD AND NOT CLR_CMAKE_TARGET_GUIDEXOS)`r`n  set(CLR_SINGLE_FILE_HOST_ONLY 1)"
if ($before -match $newPattern) {
    $after = $before
}
elseif ($before -notmatch $oldPattern) {
    if ($before -match 'CLR_SINGLE_FILE_HOST_ONLY') {
        throw 'The Phase 22 apphost guard context is absent or has already been changed.'
    }
    throw 'The expected CoreCLR apphost guard context was not found.'
}
else {
    $after = [regex]::Replace($before, $oldPattern, $new, 1)
}
[IO.File]::WriteAllText($path, $after, [Text.UTF8Encoding]::new($false))

$runtimeRelativePath = 'src/coreclr/nativeaot/Runtime/CMakeLists.txt'
$runtimePath = SourcePath $runtimeRelativePath
$runtimeBefore = (Get-Content -LiteralPath $runtimePath -Raw).TrimStart([char]0xFEFF)
$runtimeOldPattern = 'else\(\)\r?\n  if\(NOT CLR_CMAKE_TARGET_APPLE AND NOT CLR_CMAKE_TARGET_ARCH_WASM\)'
$runtimeNewPattern = 'elseif \(CLR_CMAKE_TARGET_GUIDEXOS\)\r?\n  # GUIDEXOS is host-Windows configured and does not consume Unix target probes\.\r?\nelse\(\)\r?\n  if\(NOT CLR_CMAKE_TARGET_APPLE AND NOT CLR_CMAKE_TARGET_ARCH_WASM\)'
$runtimeNew = "elseif (CLR_CMAKE_TARGET_GUIDEXOS)`r`n  # GUIDEXOS is host-Windows configured and does not consume Unix target probes.`r`nelse()`r`n  if(NOT CLR_CMAKE_TARGET_APPLE AND NOT CLR_CMAKE_TARGET_ARCH_WASM)"
if ($runtimeBefore -match $runtimeNewPattern) {
    $runtimeAfter = $runtimeBefore
}
elseif ($runtimeBefore -notmatch $runtimeOldPattern) {
    throw 'The expected NativeAOT Unix configure branch was not found.'
}
else {
$runtimeAfter = [regex]::Replace($runtimeBefore, $runtimeOldPattern, $runtimeNew, 1)
}
$featureOldPattern = 'if\(NOT CLR_CMAKE_TARGET_ARCH_WASM\)\r?\n  set\(FEATURE_PERFTRACING 1\)\r?\n  set\(FEATURE_EVENT_TRACE 1\)\r?\nendif\(\)'
$featureNewPattern = 'if\(CLR_CMAKE_TARGET_GUIDEXOS\)\r?\n  # EventPipe and EventTrace are compile-time disabled for the first payload\.\r?\n  set\(FEATURE_PERFTRACING 0\)\r?\n  set\(FEATURE_EVENT_TRACE 0\)\r?\nelseif\(NOT CLR_CMAKE_TARGET_ARCH_WASM\)\r?\n  set\(FEATURE_PERFTRACING 1\)\r?\n  set\(FEATURE_EVENT_TRACE 1\)\r?\nendif\(\)'
$featureNew = "if(CLR_CMAKE_TARGET_GUIDEXOS)`r`n  # EventPipe and EventTrace are compile-time disabled for the first payload.`r`n  set(FEATURE_PERFTRACING 0)`r`n  set(FEATURE_EVENT_TRACE 0)`r`nelseif(NOT CLR_CMAKE_TARGET_ARCH_WASM)`r`n  set(FEATURE_PERFTRACING 1)`r`n  set(FEATURE_EVENT_TRACE 1)`r`nendif()"
if ($runtimeAfter -match $featureNewPattern) {
    $runtimeFinal = $runtimeAfter
}
elseif ($runtimeAfter -notmatch $featureOldPattern) {
    throw 'The expected NativeAOT feature configuration block was not found.'
}
else {
    $runtimeFinal = [regex]::Replace($runtimeAfter, $featureOldPattern, $featureNew, 1)
}
[IO.File]::WriteAllText($runtimePath, $runtimeFinal, [Text.UTF8Encoding]::new($false))

$daccessRelativePath = 'src/coreclr/inc/daccess.h'
$daccessPath = SourcePath $daccessRelativePath
$daccessBefore = (Get-Content -LiteralPath $daccessPath -Raw).TrimStart([char]0xFEFF)
$daccessOldPattern = 'typedef void\* PTR_VOID;\r?\n' +
    'typedef LPVOID\* PTR_PTR_VOID;\r?\n' +
    'typedef const void\* PTR_CVOID;'
$daccessNewPattern = '#if defined\(TARGET_GUIDEXOS\)\r?\n' +
    'typedef void\* PTR_VOID;\r?\n' +
    'typedef void\*\* PTR_PTR_VOID;\r?\n' +
    'typedef const void\* PTR_CVOID;\r?\n' +
    '#else\r?\n' +
    'typedef void\* PTR_VOID;\r?\n' +
    'typedef LPVOID\* PTR_PTR_VOID;\r?\n' +
    'typedef const void\* PTR_CVOID;\r?\n' +
    '#endif'
$daccessNew = "#if defined(TARGET_GUIDEXOS)`r`n" +
    "typedef void* PTR_VOID;`r`n" +
    "typedef void** PTR_PTR_VOID;`r`n" +
    "typedef const void* PTR_CVOID;`r`n" +
    "#else`r`n" +
    "typedef void* PTR_VOID;`r`n" +
    "typedef LPVOID* PTR_PTR_VOID;`r`n" +
    "typedef const void* PTR_CVOID;`r`n" +
    "#endif"
if ($daccessBefore -match $daccessNewPattern) {
    $daccessAfter = $daccessBefore
}
elseif ($daccessBefore -notmatch $daccessOldPattern) {
    throw 'The expected daccess pointer alias block was not found.'
}
else {
    $daccessAfter = [regex]::Replace($daccessBefore, $daccessOldPattern, $daccessNew, 1)
}
[IO.File]::WriteAllText($daccessPath, $daccessAfter, [Text.UTF8Encoding]::new($false))

$nativeAotRelativePath = 'src/coreclr/nativeaot/CMakeLists.txt'
$nativeAotPath = SourcePath $nativeAotRelativePath
$nativeAotBefore = (Get-Content -LiteralPath $nativeAotPath -Raw).TrimStart([char]0xFEFF)
$nativeAotOldPattern = 'if\(WIN32\)\r?\n  add_definitions\(-DUNICODE=1\)\r?\n  add_compile_definitions\(NOMINMAX\)\r?\nendif \(WIN32\)'
$nativeAotNewPattern = 'if\(CLR_CMAKE_TARGET_WIN32\)\r?\n  add_definitions\(-DUNICODE=1\)\r?\n  add_compile_definitions\(NOMINMAX\)\r?\nendif \(CLR_CMAKE_TARGET_WIN32\)'
$nativeAotNew = "if(CLR_CMAKE_TARGET_WIN32)`r`n  add_definitions(-DUNICODE=1)`r`n  add_compile_definitions(NOMINMAX)`r`nendif (CLR_CMAKE_TARGET_WIN32)"
if ($nativeAotBefore -match $nativeAotNewPattern) {
    $nativeAotAfter = $nativeAotBefore
}
elseif ($nativeAotBefore -notmatch $nativeAotOldPattern) {
    throw 'The expected NativeAOT host WIN32 definition block was not found.'
}
else {
    $nativeAotAfter = [regex]::Replace($nativeAotBefore, $nativeAotOldPattern, $nativeAotNew, 1)
}
[IO.File]::WriteAllText($nativeAotPath, $nativeAotAfter, [Text.UTF8Encoding]::new($false))

$gcenvRelativePath = 'src/coreclr/nativeaot/Runtime/gcenv.h'
$gcenvPath = SourcePath $gcenvRelativePath
$gcenvBefore = (Get-Content -LiteralPath $gcenvPath -Raw).TrimStart([char]0xFEFF)
$gcenvNewPattern = '#if defined\(HOST_WINDOWS\) and not target guidexos'
if ($gcenvBefore -match '#if defined\(HOST_WINDOWS\) && !defined\(TARGET_GUIDEXOS\)') {
    $gcenvAfter = $gcenvBefore
}
elseif ($gcenvBefore -notmatch '#ifdef HOST_WINDOWS') {
    throw 'The expected NativeAOT GetClrInstanceId host gate was not found.'
}
else {
    $gcenvAfter = [regex]::Replace($gcenvBefore, '#ifdef HOST_WINDOWS', '#if defined(HOST_WINDOWS) && !defined(TARGET_GUIDEXOS)', 1)
}
[IO.File]::WriteAllText($gcenvPath, $gcenvAfter, [Text.UTF8Encoding]::new($false))

$palRelativePath = 'src/coreclr/nativeaot/Runtime/PalRedhawk.h'
$palPath = SourcePath $palRelativePath
$palBefore = (Get-Content -LiteralPath $palPath -Raw).TrimStart([char]0xFEFF)
$palAfter = $palBefore
$palAfter = [regex]::Replace($palAfter, '#ifdef TARGET_UNIX(?=\r?\n#define DIRECTORY_SEPARATOR_CHAR)', '#if defined(TARGET_UNIX) || defined(TARGET_GUIDEXOS)', 1)
$palAfter = [regex]::Replace($palAfter, '#ifdef TARGET_UNIX(?=\r?\n#define __stdcall)', '#if defined(TARGET_UNIX) || defined(TARGET_GUIDEXOS)', 1)
$palAfter = [regex]::Replace($palAfter, '#ifdef TARGET_UNIX(?=\r?\n#define NULL_AREA_SIZE)', '#if defined(TARGET_UNIX) || defined(TARGET_GUIDEXOS)', 1)
$palAfter = [regex]::Replace($palAfter, '#ifdef TARGET_UNIX(?=\r?\n#define REDHAWK_PALIMPORT)', '#if defined(TARGET_UNIX) || defined(TARGET_GUIDEXOS)', 1)
$palAfter = $palAfter.Replace('#if _WIN32', '#if defined(TARGET_WINDOWS)').Replace('#else // _WIN32', '#else // TARGET_WINDOWS').Replace('#endif // _WIN32', '#endif // TARGET_WINDOWS')
if ($palAfter -eq $palBefore -and $palBefore -notmatch 'defined\(TARGET_GUIDEXOS\)') { throw 'The expected GUIDEXOS PAL target gates were not found.' }
[IO.File]::WriteAllText($palPath, $palAfter, [Text.UTF8Encoding]::new($false))

$startupRelativePath = 'src/coreclr/nativeaot/Runtime/startup.cpp'
$startupPath = SourcePath $startupRelativePath
$startupBefore = (Get-Content -LiteralPath $startupPath -Raw).TrimStart([char]0xFEFF)
if ($startupBefore -notmatch 'HOST_WINDOWS' -and $startupBefore -notmatch 'TARGET_WINDOWS') {
    throw 'The expected NativeAOT startup host gates were not found.'
}
$startupAfter = $startupBefore.Replace('HOST_WINDOWS', 'TARGET_WINDOWS')
$startupAfter = [regex]::Replace(
    $startupAfter,
    '(?m)(#ifdef TARGET_WINDOWS\r?\nEXTERN_C LONG WINAPI RhpVectoredExceptionHandler\(PEXCEPTION_POINTERS pExPtrs\);\r?\n)#else',
    '$1#elif defined(TARGET_UNIX)',
    1)
$startupAfter = [regex]::Replace(
    $startupAfter,
    '(?m)(#ifdef TARGET_WINDOWS\r?\n    AddVectoredExceptionHandler\(1, RhpVectoredExceptionHandler\);\r?\n)#else',
    '$1#elif defined(TARGET_UNIX)',
    1)
[IO.File]::WriteAllText($startupPath, $startupAfter, [Text.UTF8Encoding]::new($false))

$regdisplayRelativePath = 'src/coreclr/nativeaot/Runtime/regdisplay.h'
$regdisplayPath = SourcePath $regdisplayRelativePath
$regdisplayBefore = (Get-Content -LiteralPath $regdisplayPath -Raw).TrimStart([char]0xFEFF)
$regdisplayOldPattern = '#if defined\(TARGET_AMD64\) && defined\(TARGET_WINDOWS\)'
$regdisplayNewPattern = '#if defined\(TARGET_AMD64\) && \(defined\(TARGET_WINDOWS\) \|\| defined\(TARGET_GUIDEXOS\)\)'
$regdisplayNew = '#if defined(TARGET_AMD64) && (defined(TARGET_WINDOWS) || defined(TARGET_GUIDEXOS))'
if ($regdisplayBefore -match $regdisplayNewPattern) {
    $regdisplayAfter = $regdisplayBefore
}
elseif ($regdisplayBefore -notmatch $regdisplayOldPattern) {
    throw 'The expected REGDISPLAY shadow-stack field gate was not found.'
}
else {
    $regdisplayAfter = [regex]::Replace($regdisplayBefore, $regdisplayOldPattern, $regdisplayNew, 1)
}
[IO.File]::WriteAllText($regdisplayPath, $regdisplayAfter, [Text.UTF8Encoding]::new($false))

$inlineTemplate = Join-Path $PSScriptRoot ('runtime\' + ($phase22Templates[0] -replace '/', '\'))
$inlineDestination = SourcePath $phase22Templates[0]
if (-not (Test-Path -LiteralPath $inlineTemplate -PathType Leaf)) { throw "Missing GUIDEXOS inline PAL template: $inlineTemplate" }
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $inlineDestination) | Out-Null
if (Test-Path -LiteralPath $inlineDestination -PathType Leaf) {
    $existingInlineHash = Get-Hash $inlineDestination
    $templateInlineHash = Get-Hash $inlineTemplate
    if ($existingInlineHash -ne $templateInlineHash) {
        if ($existingInlineHash -ne '4A40DBAD4987DB0D7F126F2EBC347F9745F7B512F444E58B267F47D783DE69D6') {
            throw 'The existing GUIDEXOS inline PAL template differs from the accepted Phase 22 template.'
        }
        Copy-Item -LiteralPath $inlineTemplate -Destination $inlineDestination -Force
    }
}
else {
    Copy-Item -LiteralPath $inlineTemplate -Destination $inlineDestination
}

$fullRelativePath = 'src/coreclr/nativeaot/Runtime/Full/CMakeLists.txt'
$fullPath = SourcePath $fullRelativePath
$fullBefore = (Get-Content -LiteralPath $fullPath -Raw).TrimStart([char]0xFEFF)
$fullLines = $fullBefore -split '\r?\n'
$workstationIndex = [Array]::FindIndex($fullLines, [Predicate[string]]{ param($line) $line -match '^add_library\(Runtime\.WorkstationGC STATIC ' })
$serverIndex = [Array]::FindIndex($fullLines, [Predicate[string]]{ param($line) $line -match '^add_library\(Runtime\.ServerGC STATIC ' })
$standaloneDisabledIndex = [Array]::FindIndex($fullLines, [Predicate[string]]{ param($line) $line -match '^add_library\(standalonegc-disabled STATIC ' })
$standaloneEnabledIndex = [Array]::FindIndex($fullLines, [Predicate[string]]{ param($line) $line -match '^add_library\(standalonegc-enabled STATIC ' })
if ($workstationIndex -lt 0 -or $serverIndex -lt 0 -or $standaloneDisabledIndex -lt 0 -or $standaloneEnabledIndex -lt 0) {
    throw 'The expected NativeAOT runtime library declarations were not found.'
}
$dependencyEndIndex = [Array]::FindIndex($fullLines, $standaloneEnabledIndex, [Predicate[string]]{ param($line) $line -match '^if\(CLR_CMAKE_TARGET_WIN32\)' })
if ($dependencyEndIndex -lt 0) { throw 'The NativeAOT runtime library dependency boundary was not found.' }
$replacementLines = @(
    $fullLines[$workstationIndex]
    'if(TARGET aot_eventing_headers)'
    '  add_dependencies(Runtime.WorkstationGC aot_eventing_headers)'
    'endif()'
    ''
    $fullLines[$serverIndex]
    'if(TARGET aot_eventing_headers)'
    '  add_dependencies(Runtime.ServerGC aot_eventing_headers)'
    'endif()'
    ''
    $fullLines[$standaloneDisabledIndex]
    'if(TARGET aot_eventing_headers)'
    '  add_dependencies(standalonegc-disabled aot_eventing_headers)'
    'endif()'
    $fullLines[$standaloneEnabledIndex]
    'if(TARGET aot_eventing_headers)'
    '  add_dependencies(standalonegc-enabled aot_eventing_headers)'
    'endif()'
)
$fullFinalLines = @($fullLines[0..($workstationIndex - 1)]) + $replacementLines + @($fullLines[$dependencyEndIndex..($fullLines.Count - 1)])
$fullFinal = $fullFinalLines -join "`r`n"
[IO.File]::WriteAllText($fullPath, $fullFinal, [Text.UTF8Encoding]::new($false))
$fullCurrent = (Get-Content -LiteralPath $fullPath -Raw).TrimStart([char]0xFEFF)
$fullExpectedPattern = 'if\(TARGET aot_eventing_headers\)\r?\n  add_dependencies\(Runtime\.WorkstationGC aot_eventing_headers\)\r?\nendif\(\)[\s\S]*?add_library\(Runtime\.ServerGC STATIC[\s\S]*?if\(TARGET aot_eventing_headers\)\r?\n  add_dependencies\(Runtime\.ServerGC aot_eventing_headers\)\r?\nendif\(\)'
if ($fullCurrent -notmatch $fullExpectedPattern) { throw 'Failed to apply the NativeAOT eventing-header dependency guards.' }

$diff = @(& git -C $SourceRoot diff --name-only | ForEach-Object { $_ -replace '/', '\' } | Sort-Object)
$expected = @($phase21Lock.sourceFiles.path | ForEach-Object { $_ -replace '/', '\' }) + @(
    $relativePath,
    $runtimeRelativePath,
    $daccessRelativePath,
    $nativeAotRelativePath,
    $gcenvRelativePath,
    $palRelativePath,
    $startupRelativePath,
    $regdisplayRelativePath | ForEach-Object { $_ -replace '/', '\' }
)
$expected = @($expected | ForEach-Object { $_ -replace '/', '\' })
$expected = @($expected | Sort-Object -Unique)
if ((@($diff) -join '|') -ne (@($expected) -join '|')) { throw 'Phase 22 runtime diff contains an unexpected tracked file.' }

New-Item -ItemType Directory -Force -Path $OutputRoot | Out-Null
$record = [ordered]@{
    schema = 1
    generatedBy = 'Tools/Phase22/apply_phase22_patch.ps1'
    generatedAtUtc = [DateTime]::UtcNow.ToString('o')
    target = [string]$lock.target
    targetOs = [string]$lock.targetOsValue
    sourceCommit = $actualCommit
    phase21PatchRecord = (Resolve-Path $phase21RecordPath).Path
    sourceFilesChanged = $diff
    transform = [ordered]@{
        apphost = [ordered]@{
            path = $relativePath
            purpose = 'Exclude host-only static single-file apphost from the GUIDEXOS target-native runtime graph.'
            old = 'if(NOT CLR_CROSS_COMPONENTS_BUILD)'
            new = 'if(NOT CLR_CROSS_COMPONENTS_BUILD AND NOT CLR_CMAKE_TARGET_GUIDEXOS)'
            sha256 = Get-Hash $path
        }
        runtimeConfigure = [ordered]@{
            path = $runtimeRelativePath
            purpose = 'Prevent GUIDEXOS from inheriting Unix target configuration probes on a Windows build host.'
            old = 'else() before the Unix configure block'
            new = 'elseif (CLR_CMAKE_TARGET_GUIDEXOS) before the Unix configure block'
            sha256 = Get-Hash $runtimePath
        }
        firstPayloadFeatures = [ordered]@{
            perfTracing = 'compile-time disabled for GUIDEXOS'
            eventTrace = 'compile-time disabled for GUIDEXOS'
            reason = 'EventPipe source selection requires an OS RID and is outside the first payload contract.'
        }
        eventingHeaderDependency = [ordered]@{
            path = $fullRelativePath
            purpose = 'Make the Runtime libraries independent of the EventPipe header target when tracing is compile-time disabled.'
            condition = 'TARGET aot_eventing_headers'
            sha256 = Get-Hash $fullPath
        }
        pointerAliases = [ordered]@{
            path = $daccessRelativePath
            purpose = 'Provide target-neutral PTR aliases without requiring Windows LPVOID definitions.'
            sha256 = Get-Hash $daccessPath
        }
        guidexosInlinePal = [ordered]@{
            path = $phase22Templates[0]
            purpose = 'Provide compiler-intrinsic interlocked, barrier, page-size, and local-error operations without Windows PAL imports.'
            sha256 = Get-Hash $inlineDestination
        }
        targetSemanticGates = [ordered]@{
            nativeAotCMake = [ordered]@{ path = $nativeAotRelativePath; purpose = 'Do not derive target UNICODE/NOMINMAX behavior from host WIN32.'; sha256 = Get-Hash $nativeAotPath }
            gcenv = [ordered]@{ path = $gcenvRelativePath; purpose = 'Do not use a Windows host TLS instance id for GUIDEXOS.'; sha256 = Get-Hash $gcenvPath }
            palHeader = [ordered]@{ path = $palRelativePath; purpose = 'Select GUIDEXOS character, import, null-area, and TEB semantics.'; sha256 = Get-Hash $palPath }
            startup = [ordered]@{ path = $startupRelativePath; purpose = 'Select startup exception and shutdown behavior from target identity.'; sha256 = Get-Hash $startupPath }
            regdisplay = [ordered]@{ path = $regdisplayRelativePath; purpose = 'Declare the explicit GUIDEXOS x64 Windows-ABI-compatible REGDISPLAY layout required by the host MASM helper set; this does not select Windows target APIs.'; sha256 = Get-Hash $regdisplayPath }
        }
    }
    hostApphostBlocker = [ordered]@{
        component = 'src/native/corehost/apphost/static'
        dependency = 'System.Net.Security.Native -> extra_libs.cmake -> find_library(gssapi_krb5)'
        targetRelation = 'host/apphost only; excluded from GUIDEXOS NativeAOT runtime target'
    }
}
$recordPath = Join-Path $OutputRoot 'phase22-source-patch-record.json'
$record | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $recordPath -Encoding UTF8
$record | ConvertTo-Json -Depth 8
Write-Output "Wrote $recordPath"
