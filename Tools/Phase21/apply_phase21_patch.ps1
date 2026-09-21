[CmdletBinding()]
param([string]$SourceRoot = '', [string]$OutputRoot = '')

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
if ([string]::IsNullOrWhiteSpace($SourceRoot)) { $SourceRoot = Join-Path $root 'out\rt' }
if ([string]::IsNullOrWhiteSpace($OutputRoot)) { $OutputRoot = Join-Path $root 'out\dotnet\phase21-source-patch' }
$phaseLock = Get-Content -Raw (Join-Path $PSScriptRoot 'runtime-source.lock.json') | ConvertFrom-Json
$phase20Lock = Get-Content -Raw (Join-Path $root 'Tools\Phase20\runtime-source.lock.json') | ConvertFrom-Json
$gcDir = '$' + '{GC_DIR}'
$runtimeDir = '$' + '{RUNTIME_DIR}'
$targetDetailsTarget = '$' + '{TARGETDETAILS_TARGET}'

if (-not (Test-Path -LiteralPath (Join-Path $SourceRoot 'build.cmd') -PathType Leaf)) { throw "Pinned runtime source is absent: $SourceRoot" }
$actualCommit = (& git -C $SourceRoot rev-parse HEAD).Trim()
if ($actualCommit -ne [string]$phaseLock.runtimeCommit -or $actualCommit -ne [string]$phase20Lock.source.commit) { throw 'Pinned runtime source mismatch.' }
if (@(& git -C $SourceRoot status --short).Count -ne 0) { throw 'Runtime source must be clean before applying Phase 21.' }

function SourcePath([string]$relativePath) { Join-Path $SourceRoot ($relativePath -replace '/', '\') }
function Sha256([string]$path) { (Get-FileHash -Algorithm SHA256 -LiteralPath $path).Hash.ToUpperInvariant() }
foreach ($item in $phaseLock.sourceFiles) {
    if ((Sha256 (SourcePath $item.path)) -ne ([string]$item.sha256).ToUpperInvariant()) { throw "Source context mismatch for $($item.path)." }
}

$changes = [System.Collections.Generic.List[object]]::new()
function Rewrite([string]$relativePath, [string]$pattern, [string]$replacement, [string]$name) {
    $path = SourcePath $relativePath
    $original = [IO.File]::ReadAllText($path)
    $lf = [char]10
    $crlf = ([char]13).ToString() + $lf
    $hadCrlf = $original.Contains($crlf)
    $text = $original.Replace($crlf, $lf.ToString())
    $matches = [regex]::Matches($text, $pattern)
    if ($matches.Count -ne 1) { throw "Transform '$name' expected one match in $relativePath, found $($matches.Count)." }
    $updated = [regex]::Replace($text, $pattern, $replacement, 1)
    if ($hadCrlf) { $updated = $updated.Replace($lf.ToString(), $crlf) }
    [IO.File]::WriteAllText($path, $updated, [Text.UTF8Encoding]::new($false))
    $changes.Add([ordered]@{ operation = 'rewrite'; path = $relativePath; name = $name })
}
function RewriteAll([string]$relativePath, [string]$pattern, [string]$replacement, [string]$name, [int]$expected) {
    $path = SourcePath $relativePath
    $original = [IO.File]::ReadAllText($path)
    $lf = [char]10
    $crlf = ([char]13).ToString() + $lf
    $hadCrlf = $original.Contains($crlf)
    $text = $original.Replace($crlf, $lf.ToString())
    $count = ([regex]::Matches($text, $pattern)).Count
    if ($count -ne $expected) { throw "Transform '$name' expected $expected matches in $relativePath, found $count." }
    $updated = [regex]::Replace($text, $pattern, $replacement)
    if ($hadCrlf) { $updated = $updated.Replace($lf.ToString(), $crlf) }
    [IO.File]::WriteAllText($path, $updated, [Text.UTF8Encoding]::new($false))
    $changes.Add([ordered]@{ operation = 'rewrite-all'; path = $relativePath; name = $name; count = $count })
}

$hostOsText = '$' + '{CLR_CMAKE_HOST_OS}'
Rewrite 'eng/native/configureplatform.cmake' '(?ms)^if \(NOT DEFINED CLR_CMAKE_TARGET_OS.*?^endif\(\)\r?\n' (@'
if (NOT DEFINED CLR_CMAKE_TARGET_OS OR CLR_CMAKE_TARGET_OS STREQUAL "" )
  set(CLR_CMAKE_TARGET_OS __HOST_OS__)
endif()

if(CLR_CMAKE_TARGET_OS STREQUAL guidexos)
    set(CLR_CMAKE_TARGET_GUIDEXOS 1)
endif(CLR_CMAKE_TARGET_OS STREQUAL guidexos)
'@).Replace('__HOST_OS__', $hostOsText) 'declare GUIDEXOS target variable'

Rewrite 'eng/build.ps1' '\[ValidateSet\("windows","linux","osx","android","browser","wasi"\)\]\[string\]\$os' '[ValidateSet("windows","linux","osx","android","browser","wasi","guidexos")][string]$os' 'accept GUIDEXOS target selector'

Rewrite 'eng/native/configureplatform.cmake' '(?ms)^if\(CLR_CMAKE_TARGET_OS STREQUAL windows\).*?^if \(NOT \(CLR_CMAKE_TARGET_OS STREQUAL CLR_CMAKE_HOST_OS\) AND NOT CLR_CMAKE_TARGET_WASI\)' @'
if(CLR_CMAKE_TARGET_OS STREQUAL windows)
    set(CLR_CMAKE_TARGET_WIN32 1)
endif()

if(CLR_CMAKE_TARGET_GUIDEXOS AND NOT CLR_CMAKE_HOST_OS STREQUAL windows)
    message(FATAL_ERROR "GUIDEXOS target builds require a Windows host toolchain in Phase 21")
endif()

if (NOT (CLR_CMAKE_TARGET_OS STREQUAL CLR_CMAKE_HOST_OS) AND NOT CLR_CMAKE_TARGET_WASI AND NOT CLR_CMAKE_TARGET_GUIDEXOS)
'@ 'allow Windows-host GUIDEXOS target'

Rewrite 'eng/native/configurecompiler.cmake' '(?ms)^elseif\(CLR_CMAKE_TARGET_WASI\).*?^endif\(CLR_CMAKE_TARGET_UNIX\)' @'
elseif(CLR_CMAKE_TARGET_WASI)
  add_compile_definitions($<$<NOT:$<BOOL:$<TARGET_PROPERTY:IGNORE_DEFAULT_TARGET_OS>>>:TARGET_WASI>)
elseif(CLR_CMAKE_TARGET_GUIDEXOS)
  add_compile_definitions($<$<NOT:$<BOOL:$<TARGET_PROPERTY:IGNORE_DEFAULT_TARGET_OS>>>:TARGET_GUIDEXOS>)
else(CLR_CMAKE_TARGET_UNIX)
  add_compile_definitions($<$<NOT:$<BOOL:$<TARGET_PROPERTY:IGNORE_DEFAULT_TARGET_OS>>>:TARGET_WINDOWS>)
endif(CLR_CMAKE_TARGET_UNIX)
'@ 'define TARGET_GUIDEXOS for native C++'

Rewrite 'Directory.Build.props' '(?m)^    <TargetsWindows Condition=.*\r?\n    <TargetsUnix Condition=.*\r?\n  </PropertyGroup>' @'
    <TargetsWindows Condition="'$(TargetOS)' == 'windows'">true</TargetsWindows>
    <TargetsGuidexos Condition="'$(TargetOS)' == 'guidexos'">true</TargetsGuidexos>
    <TargetsUnix Condition="'$(TargetsFreeBSD)' == 'true' or '$(Targetsillumos)' == 'true' or '$(TargetsSolaris)' == 'true' or '$(TargetsHaiku)' == 'true' or '$(TargetsLinux)' == 'true' or '$(TargetsNetBSD)' == 'true' or '$(TargetsOSX)' == 'true' or '$(TargetsMacCatalyst)' == 'true' or '$(TargetstvOS)' == 'true' or '$(TargetsiOS)' == 'true' or '$(TargetsAndroid)' == 'true'">true</TargetsUnix>
  </PropertyGroup>
'@ 'define TargetsGuidexos managed property'

$targetDetailsTarget = '$' + '{TARGETDETAILS_TARGET}'
Rewrite 'src/coreclr/clrdefinitions.cmake' '(?ms)  elseif \(TARGETDETAILS_OS MATCHES "\^win"\).*?  endif\(\(TARGETDETAILS_OS MATCHES "\^unix"\)\)' (@'
  elseif (TARGETDETAILS_OS MATCHES "^guidexos")
    target_compile_definitions(__TARGET_DETAILS__ PRIVATE TARGET_GUIDEXOS)
  elseif (TARGETDETAILS_OS MATCHES "^win")
    target_compile_definitions(__TARGET_DETAILS__ PRIVATE TARGET_WINDOWS)
  endif((TARGETDETAILS_OS MATCHES "^unix"))
'@).Replace('__TARGET_DETAILS__', $targetDetailsTarget) 'define GUIDEXOS in custom target definitions'

Rewrite 'src/coreclr/nativeaot/Directory.Build.props' '(?m)^    <DefineConstants Condition="''\$\(TargetsWindows\)''==''true''">TARGET_WINDOWS;\$\(DefineConstants\)</DefineConstants>\r?\n    <DefineConstants Condition="''\$\(TargetsUnix\)''==''true''">TARGET_UNIX;\$\(DefineConstants\)</DefineConstants>\r?\n  </PropertyGroup>' @'
    <DefineConstants Condition="'$(TargetsWindows)'=='true'">TARGET_WINDOWS;$(DefineConstants)</DefineConstants>
    <DefineConstants Condition="'$(TargetsGuidexos)'=='true'">TARGET_GUIDEXOS;$(DefineConstants)</DefineConstants>
    <DefineConstants Condition="'$(TargetsUnix)'=='true'">TARGET_UNIX;$(DefineConstants)</DefineConstants>
  </PropertyGroup>
'@ 'define GUIDEXOS in NativeAOT managed build'

RewriteAll 'src/coreclr/gc/env/gcenv.base.h' '#ifdef TARGET_UNIX' '#if defined(TARGET_UNIX) || defined(TARGET_GUIDEXOS)' 'portable GC aliases' 2
Rewrite 'src/coreclr/gc/env/gcenv.structs.h' '#define __GCENV_STRUCTS_INCLUDED__' @'
#define __GCENV_STRUCTS_INCLUDED__

#ifdef TARGET_GUIDEXOS
#include <atomic>
#endif
'@ 'include atomic GUIDEXOS lock storage'
Rewrite 'src/coreclr/gc/env/gcenv.structs.h' '#else // TARGET_UNIX' @'
#elif defined(TARGET_GUIDEXOS)

class EEThreadId
{
    uint64_t m_id = 0;
public:
    bool IsCurrentThread() { return false; }
    void SetToCurrentThread() { m_id = 0; }
    void Clear() { m_id = 0; }
};

#else // TARGET_UNIX or TARGET_GUIDEXOS
'@ 'target-private EEThreadId'
Rewrite 'src/coreclr/gc/env/gcenv.structs.h' '(?ms)#else\r?\n\r?\n#pragma pack\(push, 8\)' @'
#elif defined(TARGET_GUIDEXOS)

typedef struct _GUIDEXOS_CRITICAL_SECTION {
    std::atomic<uint32_t> state;
} CRITICAL_SECTION, RTL_CRITICAL_SECTION, *PRTL_CRITICAL_SECTION;

#else

#pragma pack(push, 8)
'@ 'atomic GUIDEXOS critical-section storage'

Rewrite 'src/coreclr/nativeaot/Runtime/CMakeLists.txt' 'if \(WIN32\)' 'if (CLR_CMAKE_TARGET_WIN32)' 'select runtime source by target OS'
$guidexosBranch = @'
  set(ASM_SUFFIX asm)
elseif (CLR_CMAKE_TARGET_GUIDEXOS)
  include_directories(guidexos)
  set(GC_HEADERS
    __GC_DIR__/env/gcenv.guidexos.inl)
  list(APPEND COMMON_RUNTIME_SOURCES
    __GC_DIR__/guidexos/gcenv.guidexos.cpp
  )
  if (CLR_CMAKE_HOST_WIN32)
    set(ASM_SUFFIX asm)
  else()
    set(ASM_SUFFIX S)
  endif()
else()
'@.Replace('__GC_DIR__', $gcDir)
Rewrite 'src/coreclr/nativeaot/Runtime/CMakeLists.txt' '  set\(ASM_SUFFIX asm\)\r?\nelse\(\)' $guidexosBranch 'select GUIDEXOS GC environment'

Rewrite 'src/coreclr/nativeaot/Runtime/Full/CMakeLists.txt' 'if\(WIN32\)' 'if(CLR_CMAKE_HOST_WIN32)' 'separate host assembler selection'
$asmBlock = @'
    set(PREPROCESSOR_FLAGS -EP -nologo)
    if (CLR_CMAKE_TARGET_WIN32)
      set(ASM_OFFSETS_CPP __RUNTIME_DIR__/windows/AsmOffsets.cpp)
    elseif (CLR_CMAKE_TARGET_GUIDEXOS)
      set(ASM_OFFSETS_CPP __RUNTIME_DIR__/guidexos/AsmOffsets.cpp)
    else()
      message(FATAL_ERROR "No host-Windows AsmOffsets source is defined for this target")
    endif()
else()
'@.Replace('__RUNTIME_DIR__', $runtimeDir)
Rewrite 'src/coreclr/nativeaot/Runtime/Full/CMakeLists.txt' '    set\(PREPROCESSOR_FLAGS -EP -nologo\)\r?\n    set\(ASM_OFFSETS_CPP .*?windows/AsmOffsets\.cpp\)\r?\nelse\(\)' $asmBlock 'select GUIDEXOS AsmOffsets'

Rewrite 'src/native/libs/CMakeLists.txt' '(?m)^    if \(CLR_CMAKE_TARGET_BROWSER OR CLR_CMAKE_TARGET_WASI\)\r?\n        # skip for now' @'
    if (CLR_CMAKE_TARGET_BROWSER OR CLR_CMAKE_TARGET_WASI OR CLR_CMAKE_TARGET_GUIDEXOS)
        # skip for now
'@ 'exclude unsupported native security libraries from first GUIDEXOS runtime'

function Add-Template([string]$templateRelativePath, [string]$runtimeRelativePath) {
    $template = Join-Path $PSScriptRoot ('runtime\' + ($templateRelativePath -replace '/', '\'))
    $destination = SourcePath $runtimeRelativePath
    if (-not (Test-Path -LiteralPath $template -PathType Leaf)) { throw "Missing template: $template" }
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $destination) | Out-Null
    if (Test-Path -LiteralPath $destination) { throw "Refusing to overwrite: $runtimeRelativePath" }
    Copy-Item -LiteralPath $template -Destination $destination
    $changes.Add([ordered]@{ operation = 'add'; path = $runtimeRelativePath; template = $templateRelativePath })
}
foreach ($item in $phaseLock.repositoryOwnedTemplates) { Add-Template ([string]$item) ([string]$item) }

$finalStatus = @(& git -C $SourceRoot status --short --untracked-files=all)
$changedFiles = @(& git -C $SourceRoot diff --name-only | ForEach-Object { $_ -replace '/', '\' })
$untrackedFiles = @($finalStatus | Where-Object { $_ -match '^\?\?\s+' } | ForEach-Object { ($_ -replace '^\?\?\s+', '') -replace '/', '\' })
$allChanged = @($changedFiles + $untrackedFiles | Sort-Object -Unique)
$expectedChanged = @($phaseLock.sourceFiles.path + $phaseLock.repositoryOwnedTemplates | ForEach-Object { $_ -replace '/', '\' } | Sort-Object -Unique)
foreach ($path in $allChanged) { if ($expectedChanged -notcontains $path) { throw "Unexpected runtime file changed: $path" } }
foreach ($path in $expectedChanged) { if ($allChanged -notcontains $path) { throw "Expected runtime file did not change: $path" } }

$record = [ordered]@{
    schema = 1
    phase = 21
    target = $phaseLock.target
    targetOsValue = $phaseLock.targetOsValue
    source = [ordered]@{
        checkout = (Resolve-Path $SourceRoot).Path
        commit = $actualCommit
        canonicalRepository = $phase20Lock.source.repository
        cleanBeforeApply = $true
    }
    changes = @($changes)
    runtimeFilesChanged = $allChanged
    sourceDiffStat = @(& git -C $SourceRoot diff --stat)
    sourceDiffCheck = (& git -C $SourceRoot diff --check | Out-String).Trim()
}
New-Item -ItemType Directory -Force -Path $OutputRoot | Out-Null
$recordPath = Join-Path $OutputRoot 'phase21-source-patch-record.json'
$record | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $recordPath -Encoding UTF8
$record | ConvertTo-Json -Depth 5
Write-Output "Wrote $recordPath"
