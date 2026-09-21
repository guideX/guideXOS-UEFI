[CmdletBinding()]
param(
    [string]$SourceRoot = '',
    [string]$OutputRoot = ''
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
if ([string]::IsNullOrWhiteSpace($SourceRoot)) {
    $SourceRoot = Join-Path $root 'out\rt'
}
if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path $root 'out\dotnet\phase20-source-audit'
}

if (-not (Test-Path -LiteralPath $SourceRoot -PathType Container)) {
    throw "Pinned runtime source is absent: $SourceRoot. Acquire the exact source commit from Tools\Phase20\runtime-source.lock.json; no stock SDK fallback is allowed."
}

$lock = Get-Content (Join-Path $PSScriptRoot 'runtime-source.lock.json') -Raw | ConvertFrom-Json
$expectedCommit = [string]$lock.source.commit
$actualCommit = (& git -C $SourceRoot rev-parse HEAD).Trim()
if ($actualCommit -ne $expectedCommit) {
    throw "Pinned runtime source mismatch. Expected $expectedCommit but found $actualCommit in $SourceRoot."
}

$requiredFiles = @(
    'src/coreclr/nativeaot/Runtime/CMakeLists.txt',
    'src/coreclr/nativeaot/Runtime/Full/CMakeLists.txt',
    'src/coreclr/nativeaot/Runtime/PalRedhawk.h',
    'src/coreclr/nativeaot/Runtime/PalRedhawkFunctions.h',
    'src/coreclr/nativeaot/Runtime/windows/PalRedhawkCommon.cpp',
    'src/coreclr/nativeaot/Runtime/windows/PalRedhawkMinWin.cpp',
    'src/coreclr/gc/windows/gcenv.windows.cpp',
    'src/coreclr/gc/env/gcenv.os.h',
    'src/coreclr/gc/env/gcenv.windows.inl',
    'src/coreclr/nativeaot/BuildIntegration/Microsoft.NETCore.Native.targets',
    'src/coreclr/nativeaot/BuildIntegration/Microsoft.NETCore.Native.Windows.targets',
    'src/libraries/System.Private.CoreLib/src/System/Diagnostics/Stopwatch.Windows.cs',
    'src/libraries/System.Private.CoreLib/src/System/Runtime/InteropServices/NativeLibrary.cs',
    'src/libraries/System.Private.CoreLib/src/System/Runtime/InteropServices/RuntimeInformation.Windows.cs',
    'src/libraries/System.Private.CoreLib/src/System/Runtime/InteropServices/Marshal.Windows.cs'
)

function Get-SourcePath([string]$relativePath) {
    return Join-Path $SourceRoot ($relativePath -replace '/', '\')
}

foreach ($relativePath in $requiredFiles) {
    if (-not (Test-Path -LiteralPath (Get-SourcePath $relativePath) -PathType Leaf)) {
        throw "Pinned source is incomplete; required audit file is absent: $relativePath"
    }
}

function Get-FileRecord([string]$relativePath) {
    $path = Get-SourcePath $relativePath
    $item = Get-Item -LiteralPath $path
    $hash = (Get-FileHash -Algorithm SHA256 -LiteralPath $path).Hash
    [ordered]@{
        path = $relativePath
        bytes = [int64]$item.Length
        lines = (Get-Content -LiteralPath $path).Count
        sha256 = $hash
    }
}

function Get-UniqueRegexMatches([string]$text, [string]$pattern) {
    $values = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
    foreach ($match in [regex]::Matches($text, $pattern)) {
        [void]$values.Add($match.Groups[1].Value)
    }
    return @($values | Sort-Object)
}

$palHeaderText = Get-Content -LiteralPath (Get-SourcePath 'src/coreclr/nativeaot/Runtime/PalRedhawk.h') -Raw
$palFunctionsText = Get-Content -LiteralPath (Get-SourcePath 'src/coreclr/nativeaot/Runtime/PalRedhawkFunctions.h') -Raw
$palWindowsText = Get-Content -LiteralPath (Get-SourcePath 'src/coreclr/nativeaot/Runtime/windows/PalRedhawkMinWin.cpp') -Raw
$palCommonText = Get-Content -LiteralPath (Get-SourcePath 'src/coreclr/nativeaot/Runtime/windows/PalRedhawkCommon.cpp') -Raw
$gcWindowsPath = Get-SourcePath 'src/coreclr/gc/windows/gcenv.windows.cpp'
$gcWindowsText = Get-Content -LiteralPath $gcWindowsPath -Raw
$runtimeCmakeText = Get-Content -LiteralPath (Get-SourcePath 'src/coreclr/nativeaot/Runtime/CMakeLists.txt') -Raw
$windowsTargetsText = Get-Content -LiteralPath (Get-SourcePath 'src/coreclr/nativeaot/BuildIntegration/Microsoft.NETCore.Native.Windows.targets') -Raw

$palImports = Get-UniqueRegexMatches $palHeaderText '(?m)REDHAWK_PALIMPORT[^;\r\n]*\b(Pal[A-Za-z0-9_]+)\s*\('
$palExports = Get-UniqueRegexMatches ($palWindowsText + "`n" + $palCommonText) '(?m)REDHAWK_PALEXPORT[^;\r\n]*\b(Pal[A-Za-z0-9_]+)\s*\('
$gcOsMethods = Get-UniqueRegexMatches $gcWindowsText '\b(GCToOSInterface::[A-Za-z0-9_]+|CLRCriticalSection::[A-Za-z0-9_]+|GCEvent::[A-Za-z0-9_]+)\s*\('
$gcWindowsApiNames = Get-UniqueRegexMatches $gcWindowsText '\b((?:Get|Set|Open|Close|Adjust|Lookup|Initialize|Delete|Enter|Leave|Acquire|Release|Virtual|Sleep|Switch|Create|Wait|Reset|Query|SetThread|GetThread|GetLogical|GetNuma|GetProcess|GetCurrent|GetSystem|GetMemory|GetTotal|GetPhysical|IsProcess|LookupPrivilege|OpenProcess|AdjustToken|WriteWatch)[A-Za-z0-9_]*)\s*\('

$contractPath = Join-Path $root 'Tools\Phase19\pal-contract.json'
$contract = Get-Content -LiteralPath $contractPath -Raw | ConvertFrom-Json
$contractSymbols = @($contract.categories | ForEach-Object { $_.symbols } | ForEach-Object { $_.name })
$forbiddenWindowsImports = @(
    'KERNEL32.dll', 'ADVAPI32.dll', 'bcrypt.dll', 'ole32.dll', 'api-ms-win-crt-*',
    'VirtualAlloc', 'VirtualProtect', 'VirtualQuery', 'FlsAlloc', 'FlsGetValue', 'FlsSetValue',
    'QueryPerformanceCounter', 'QueryPerformanceFrequency', 'GetSystemTimeAsFileTime',
    'BCryptGenRandom', 'LoadLibrary', 'GetProcAddress', 'CoWaitForMultipleHandles',
    'RaiseFailFastException', 'WaitForSingleObjectEx', 'CreateEventW'
)

$sourceFiles = @($requiredFiles | ForEach-Object { Get-FileRecord $_ })
$gitStatus = (& git -C $SourceRoot status --short)
$submodules = @(& git -C $SourceRoot submodule status)

$manifest = [ordered]@{
    schema = 1
    generatedBy = 'Tools/Phase20/phase20_source_audit.ps1'
    generatedAtUtc = [DateTime]::UtcNow.ToString('o')
    target = $lock.target
    outcome = $lock.sourceAdaptation.outcome
    status = 'hard-stop-before-runtime-source-modification'
    source = [ordered]@{
        canonicalRepository = $lock.source.repository
        checkout = (Resolve-Path $SourceRoot).Path
        checkoutCommit = $actualCommit
        expectedCommit = $expectedCommit
        clean = (@($gitStatus).Count -eq 0)
        submodules = $submodules
        files = $sourceFiles
    }
    contract = [ordered]@{
        path = 'Tools/Phase19/pal-contract.json'
        externalSymbolCount = $contractSymbols.Count
        externalSymbols = $contractSymbols
        externalSynchronizationSymbols = @()
    }
    audit = [ordered]@{
        nativeAotPalImportNames = $palImports
        nativeAotPalImportCount = $palImports.Count
        nativeAotPalExportNames = $palExports
        nativeAotPalExportCount = $palExports.Count
        gcEnvironmentMethodCount = $gcOsMethods.Count
        gcEnvironmentMethods = $gcOsMethods
        gcWindowsApiNames = $gcWindowsApiNames
        gcWindowsApiNameCount = $gcWindowsApiNames.Count
        windowsCmakeSelectsWindowsPal = ($runtimeCmakeText -match '(?m)^if \(WIN32\)')
        windowsTargetsAddSdkLibraries = ($windowsTargetsText -match 'SdkNativeLibrary Include="kernel32\.lib"')
        forbiddenRuntimeSurface = $forbiddenWindowsImports
    }
    decision = [ordered]@{
        sourceFilesModified = @()
        packageProduced = $false
        payloadBuiltAgainstCustomTarget = $false
        stockWinX64FallbackAllowed = $false
        reason = 'The Windows PAL and GC environment cannot be reduced to the Phase 19 20-symbol contract without a broad runtime/GC/build/CoreLib adaptation. Stop before creating a renamed stock pack.'
        nextBoundedWork = @(
            'Introduce an explicit GUIDEXOS build identity and target-specific runtime source set.',
            'Define the complete required NativeAOT PAL surface before adding any symbol to the contract.',
            'Isolate or redesign GC synchronization before binding GC to the five VM PAL operations.',
            'Create custom target files and package assets only after the source build is no longer WIN32-selected.'
        )
    }
}

New-Item -ItemType Directory -Force -Path $OutputRoot | Out-Null
$outputPath = Join-Path $OutputRoot 'phase20-source-audit.json'
$manifest | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $outputPath -Encoding UTF8
Write-Output ($manifest | ConvertTo-Json -Depth 4)
Write-Output "Wrote $outputPath"
