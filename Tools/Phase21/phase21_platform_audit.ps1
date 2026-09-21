[CmdletBinding()]
param(
    [string]$SourceRoot = '',
    [string]$OutputRoot = ''
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
if ([string]::IsNullOrWhiteSpace($SourceRoot)) { $SourceRoot = Join-Path $root 'out\rt' }
if ([string]::IsNullOrWhiteSpace($OutputRoot)) { $OutputRoot = Join-Path $root 'out\dotnet\phase21-platform-audit' }
$lock = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'runtime-source.lock.json') -Raw | ConvertFrom-Json
$actualCommit = (& git -C $SourceRoot rev-parse HEAD).Trim()
if ($actualCommit -ne [string]$lock.runtimeCommit) { throw "Runtime source mismatch. Expected $($lock.runtimeCommit), found $actualCommit." }

function SourcePath([string]$relativePath) { Join-Path $SourceRoot ($relativePath -replace '/', '\') }
function ReadSource([string]$relativePath) { Get-Content -LiteralPath (SourcePath $relativePath) -Raw }
function FileRecord([string]$relativePath, [string]$classification, [string]$reason) {
    $path = SourcePath $relativePath
    [ordered]@{
        path = $relativePath
        present = (Test-Path -LiteralPath $path -PathType Leaf)
        classification = $classification
        reason = $reason
        lines = if (Test-Path -LiteralPath $path -PathType Leaf) { (Get-Content -LiteralPath $path).Count } else { 0 }
    }
}

$coreLibRoot = SourcePath 'src/libraries/System.Private.CoreLib/src'
$windowsCoreLibFiles = @(Get-ChildItem -LiteralPath $coreLibRoot -Recurse -File -Filter '*.Windows.cs' | ForEach-Object { $_.FullName.Substring($SourceRoot.Length + 1).Replace('\', '/') } | Sort-Object)
$minimalCoreLib = @(
    (FileRecord 'src/libraries/System.Private.CoreLib/src/System/Diagnostics/Stopwatch.Windows.cs' 'target-abstraction-required' 'The GC and runtime timing path must not bind to QueryPerformanceCounter through Windows interop.'),
    (FileRecord 'src/libraries/System.Private.CoreLib/src/System/Runtime/InteropServices/NativeLibrary.cs' 'unreachable-or-future-loader-abstraction' 'NativeLibrary is not needed for Main() => 42 unless managed dynamic loading is deliberately reached.'),
    (FileRecord 'src/libraries/System.Private.CoreLib/src/System/Runtime/InteropServices/RuntimeInformation.Windows.cs' 'target-abstraction-required-if-reached' 'RuntimeInformation must not report Windows for a GUIDEXOS target.'),
    (FileRecord 'src/libraries/System.Private.CoreLib/src/System/Runtime/InteropServices/Marshal.Windows.cs' 'unreachable-or-future-target-abstraction' 'Marshal Windows interop is outside the first payload surface.'),
    (FileRecord 'src/libraries/System.Private.CoreLib/src/Internal/Win32/RegistryKey.cs' 'unreachable' 'Registry/Win32 APIs are not on the first payload path and remain unsupported.'),
    (FileRecord 'src/libraries/System.Private.CoreLib/src/System/Environment.Windows.cs' 'future-target-abstraction' 'Environment and process/file queries are not part of the first payload contract.')
)

$startupFiles = @(
    (FileRecord 'src/coreclr/nativeaot/Runtime/startup.cpp' 'runtime-startup-hook' 'Managed startup and module initialization entry path.'),
    (FileRecord 'src/coreclr/nativeaot/Runtime/PalRedhawk.h' 'Phase19-PAL-boundary' 'PAL declarations consumed by startup/runtime code.'),
    (FileRecord 'src/coreclr/nativeaot/Runtime/PalRedhawkFunctions.h' 'Phase19-PAL-boundary' 'PAL function table and external symbol surface.'),
    (FileRecord 'src/coreclr/nativeaot/Runtime/CMakeLists.txt' 'target-source-selection' 'Selects GUIDEXOS GC source and excludes Windows PAL sources.'),
    (FileRecord 'src/coreclr/nativeaot/Runtime/Full/CMakeLists.txt' 'host-target-assembler-selection' 'Uses host Windows preprocessing while selecting GUIDEXOS target AsmOffsets.'),
    (FileRecord 'src/coreclr/gc/guidexos/gcenv.guidexos.cpp' 'GC-environment-skeleton' 'GUIDEXOS workstation GC OS/synchronization implementation.'),
    (FileRecord 'src/coreclr/nativeaot/Runtime/guidexos/GuidexosGcPal.h' 'GC-PAL-declaration' 'Only the existing Phase 19 VM/thread/time declarations are visible to GC.')
)

$nativeTargets = ReadSource 'src/coreclr/nativeaot/BuildIntegration/Microsoft.NETCore.Native.targets'
$windowsTargets = ReadSource 'src/coreclr/nativeaot/BuildIntegration/Microsoft.NETCore.Native.Windows.targets'
$unixTargets = ReadSource 'src/coreclr/nativeaot/BuildIntegration/Microsoft.NETCore.Native.Unix.targets'
$packProps = ReadSource 'src/installer/pkg/sfx/Microsoft.NETCore.App/Microsoft.NETCore.App.Runtime.props'
$packaging = [ordered]@{
    targetProperty = 'TargetOS=guidexos and RuntimeIdentifier=guidexos-x64 must produce a non-Windows asset graph.'
    currentSelection = [ordered]@{
        nativeTargetsImportsWindowsWhen = "`$(_targetOS) == 'win'"
        nativeTargetsImportsUnixOtherwise = $true
        guidexosSpecificTargetFile = $false
        windowsTargetsPresent = $true
        unixTargetsPresent = $true
    }
    requiredForPhase22 = @(
        'Add explicit guidexos-x64 runtime identifier and asset naming.',
        'Add a GUIDEXOS NativeAOT build-integration target file or a target-native third branch.',
        'Select a guideXOS linker/object/archive model without Microsoft.NETCore.Native.Windows.targets.',
        'Stage CoreLib, ILCompiler, runtime native objects, and PAL symbols under guidexos-x64.',
        'Add a verifier that rejects win-x64 fallback and Windows runtime files.'
    )
    windowsFallbackSignals = @('win-x64', 'Microsoft.NETCore.Native.Windows.targets', 'KERNEL32', 'ADVAPI32', 'bcrypt', 'ole32')
    packPropsContainsWindowsRuntimeFallback = ($packProps -match 'win-x64')
}

$sourceBuildRecordPath = Join-Path $root 'out\dotnet\phase21-source-build\source-build-record.json'
$sourceBuild = if (Test-Path -LiteralPath $sourceBuildRecordPath -PathType Leaf) { Get-Content -LiteralPath $sourceBuildRecordPath -Raw | ConvertFrom-Json } else { $null }
$result = [ordered]@{
    schema = 1
    generatedBy = 'Tools/Phase21/phase21_platform_audit.ps1'
    generatedAtUtc = [DateTime]::UtcNow.ToString('o')
    source = [ordered]@{ checkout = (Resolve-Path $SourceRoot).Path; commit = $actualCommit }
    coreLib = [ordered]@{
        allWindowsSuffixedFileCount = $windowsCoreLibFiles.Count
        representativeWindowsFiles = $windowsCoreLibFiles | Select-Object -First 40
        minimalMain42Audit = $minimalCoreLib
        conclusion = 'The current CoreLib source graph contains extensive Windows-specific interop. Phase 21 does not port it; the first payload must keep those APIs unreachable or add exact GUIDEXOS target abstractions in a later phase.'
    }
    startup = [ordered]@{
        hooks = $startupFiles
        phase19Covered = @('VM reserve/commit/release declarations', 'thread identity declaration', 'monotonic time declarations')
        GCSpecific = @('GC initialization', 'GC environment selection', 'GC timing', 'GC synchronization', 'GC decommit semantics')
        futureCoreLib = @('fail-fast managed surface', 'entropy/time managed wrappers', 'TLS/FLS managed reachability')
        unresolved = @('full process/thread startup implementation', 'target-native TLS/FLS', 'full managed exception path')
    }
    packaging = $packaging
    sourceBuild = $sourceBuild
    status = 'reachability-and-packaging-audit-complete; no-custom-pack-produced'
}

New-Item -ItemType Directory -Force -Path $OutputRoot | Out-Null
$outputPath = Join-Path $OutputRoot 'platform-reachability-audit.json'
$result | ConvertTo-Json -Depth 16 | Set-Content -LiteralPath $outputPath -Encoding UTF8
$result | ConvertTo-Json -Depth 5
Write-Output "Wrote $outputPath"
