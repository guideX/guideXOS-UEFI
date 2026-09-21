[CmdletBinding()]
param(
    [string]$SourceRoot = '',
    [string]$OutputRoot = ''
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
if ([string]::IsNullOrWhiteSpace($SourceRoot)) { $SourceRoot = Join-Path $root 'out\rt' }
if ([string]::IsNullOrWhiteSpace($OutputRoot)) { $OutputRoot = Join-Path $root 'out\dotnet\phase21-gc-inventory' }

$lock = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'runtime-source.lock.json') -Raw | ConvertFrom-Json
if (-not (Test-Path -LiteralPath $SourceRoot -PathType Container)) { throw "Runtime source is absent: $SourceRoot" }
$actualCommit = (& git -C $SourceRoot rev-parse HEAD).Trim()
if ($actualCommit -ne [string]$lock.runtimeCommit) {
    throw "Runtime source mismatch. Expected $($lock.runtimeCommit), found $actualCommit."
}

function SourcePath([string]$relativePath) { Join-Path $SourceRoot ($relativePath -replace '/', '\') }
$windowsCppRelative = 'src/coreclr/gc/windows/gcenv.windows.cpp'
$windowsInlRelative = 'src/coreclr/gc/env/gcenv.windows.inl'
$windowsCpp = SourcePath $windowsCppRelative
$windowsInl = SourcePath $windowsInlRelative
if (-not (Test-Path -LiteralPath $windowsCpp -PathType Leaf)) { throw "GC source is absent: $windowsCppRelative" }
if (-not (Test-Path -LiteralPath $windowsInl -PathType Leaf)) { throw "GC inline source is absent: $windowsInlRelative" }

function Get-MethodNames([string]$text) {
    $names = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
    foreach ($match in [regex]::Matches($text, '\b((?:GCToOSInterface|CLRCriticalSection|GCEvent)::[A-Za-z0-9_]+)\s*\(')) {
        [void]$names.Add($match.Groups[1].Value)
    }
    @($names | Sort-Object)
}

function Get-Category([string]$method) {
    if ($method.StartsWith('CLRCriticalSection::') -or $method.StartsWith('GCEvent::')) { return 'synchronization' }
    $name = $method.Substring($method.IndexOf('::') + 2)
    if ($name -match '^Virtual|WriteWatch|MemoryLimit|MemoryMaxAddress|GetPhysicalMemoryLimit|GetMemoryStatus') { return 'virtual-memory' }
    if ($name -match 'Performance|TimeStamp|Sleep|Yield') { return 'timing' }
    if ($name -match 'Processor|CPU|Numa|Affinity|Cache|GCCPUGroups|GCNuma|BoostThreadPriority') { return 'processor-topology' }
    if ($name -match 'ThreadId|ProcessId') { return 'identity' }
    if ($name -eq 'DebugBreak') { return 'diagnostics' }
    return 'runtime-state'
}

function Get-Requirement([string]$method) {
    $required = @(
        'GCToOSInterface::Initialize',
        'GCToOSInterface::Shutdown',
        'GCToOSInterface::VirtualReserve',
        'GCToOSInterface::VirtualCommit',
        'GCToOSInterface::VirtualRelease',
        'GCToOSInterface::VirtualDecommit',
        'GCToOSInterface::GetVirtualMemoryLimit',
        'GCToOSInterface::GetVirtualMemoryMaxAddress',
        'GCToOSInterface::GetTotalProcessorCount',
        'GCToOSInterface::QueryPerformanceCounter',
        'GCToOSInterface::QueryPerformanceFrequency',
        'GCToOSInterface::GetLowPrecisionTimeStamp',
        'CLRCriticalSection::Initialize',
        'CLRCriticalSection::Destroy',
        'CLRCriticalSection::Enter',
        'CLRCriticalSection::Leave',
        'GCEvent::GCEvent',
        'GCEvent::CloseEvent',
        'GCEvent::Set',
        'GCEvent::Reset',
        'GCEvent::Wait',
        'GCEvent::CreateAutoEventNoThrow',
        'GCEvent::CreateManualEventNoThrow',
        'GCEvent::CreateOSAutoEventNoThrow',
        'GCEvent::CreateOSManualEventNoThrow'
    )
    if ($required -contains $method) { return 'required-for-workstation-correctness-or-startup' }
    if ($method -match 'VirtualReserveAndCommitLargePages|WriteWatch|GetCacheSizePerLogicalCpu|BoostThreadPriority|SetThreadAffinity|SetCurrentThreadIdealAffinity|SetGCThreadsAffinitySet|GetCurrentThreadIdealProc|GetProcessorForHeap|GetNumaInfo|CanEnableGCNumaAware|CanEnableGCCPUGroups|GetCPUGroupInfo|ParseGCHeapAffinitizeRangesEntry') { return 'optional-optimization-or-server-only' }
    if ($method -match 'GetCurrentThreadIdForLogging|GetCurrentProcessId|DebugBreak') { return 'diagnostics-only' }
    if ($method -match 'FlushProcessWriteBuffers|Sleep|YieldThread') { return 'required-only-for-multithreaded-runtime-or-suspension' }
    if ($method -match 'GetCurrentProcessorNumber|CanGetCurrentProcessorNumber') { return 'optional-with-single-processor-configuration' }
    if ($method -match 'GetPhysicalMemoryLimit|GetMemoryStatus') { return 'policy-or-heap-sizing-input; first runtime may use bounded defaults' }
    return 'unsupported-in-first-guideXOS-runtime-until-callers-are-enabled'
}

$expectedCppCount = 53
$cppSourceSymbols = Get-MethodNames (Get-Content -LiteralPath $windowsCpp -Raw)
if ($cppSourceSymbols.Count -ne $expectedCppCount) {
    throw "Pinned GC OS source-symbol count changed: expected $expectedCppCount symbols in gcenv.windows.cpp, found $($cppSourceSymbols.Count)."
}
$cppMethodNames = @($cppSourceSymbols | Where-Object { $_ -ne 'GCEvent::Impl' })
$methodNames = @(Get-MethodNames ((Get-Content -LiteralPath $windowsCpp -Raw) + "`n" + (Get-Content -LiteralPath $windowsInl -Raw)) | Where-Object { $_ -ne 'GCEvent::Impl' })

$gcFiles = @(Get-ChildItem -LiteralPath (SourcePath 'src/coreclr/gc') -Recurse -File | Where-Object { $_.Extension -in '.cpp', '.h', '.inl' })
$records = foreach ($method in $methodNames) {
    $referenceCount = 0
    $referenceFiles = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
    $escaped = [regex]::Escape($method)
    foreach ($file in $gcFiles) {
        $text = Get-Content -LiteralPath $file.FullName -Raw
        $matches = [regex]::Matches($text, $escaped + '\s*\(')
        if ($matches.Count -gt 0) {
            $referenceCount += $matches.Count
            [void]$referenceFiles.Add($file.FullName.Substring($SourceRoot.Length + 1).Replace('\', '/'))
        }
    }
    [ordered]@{
        method = $method
        category = Get-Category $method
        requirement = Get-Requirement $method
        referenceCount = $referenceCount
        referenceFiles = @($referenceFiles | Sort-Object)
    }
}

$inlinePageSize = [ordered]@{
    method = 'GCToOSInterface::GetPageSize'
    category = 'virtual-memory'
    requirement = 'required-for-workstation-startup'
    source = $windowsInlRelative
    note = 'Inline page-size contract; not counted in the 53 out-of-line gcenv.windows.cpp methods.'
}

$syncMethods = @($records | Where-Object { $_.category -eq 'synchronization' } | ForEach-Object { $_.method })
$workstationRequired = @($records | Where-Object { $_.requirement -eq 'required-for-workstation-correctness-or-startup' -or $_.requirement -eq 'required-for-workstation-startup' } | ForEach-Object { $_.method })
$optionalMethods = @($records | Where-Object { $_.requirement -eq 'optional-optimization-or-server-only' -or $_.requirement -eq 'optional-with-single-processor-configuration' } | ForEach-Object { $_.method })
$disabledOptimizations = @(
    'large pages', 'write watch', 'NUMA awareness', 'CPU groups',
    'processor affinity', 'thread-priority boosting', 'cache-size discovery',
    'Windows memory notifications'
)

$inventory = [ordered]@{
    schema = 1
    generatedBy = 'Tools/Phase21/phase21_gc_inventory.ps1'
    generatedAtUtc = [DateTime]::UtcNow.ToString('o')
    source = [ordered]@{
        checkout = (Resolve-Path $SourceRoot).Path
        commit = $actualCommit
        windowsImplementation = $windowsCppRelative
        windowsInlineImplementation = $windowsInlRelative
        outOfLineSourceSymbolCount = $cppSourceSymbols.Count
        outOfLineCallableMethodCount = $cppMethodNames.Count
        semanticMethodCountIncludingInlinePageSize = $methodNames.Count
    }
    operations = $records
    inlineOperations = @($inlinePageSize)
    summary = [ordered]@{
        synchronizationMethodCount = $syncMethods.Count
        synchronizationMethods = $syncMethods
        workstationRequiredMethods = $workstationRequired
        optionalMethods = $optionalMethods
        disabledOptimizations = $disabledOptimizations
        virtualMemory = [ordered]@{
            existingPhase19ConceptualOperations = @('reserve', 'commit', 'release')
            requiredForFirstWorkstationRuntime = @('reserve', 'commit', 'release', 'decommit', 'page-size', 'virtual-memory-limit')
            unsupportedOrDeferred = @('reset', 'large-pages', 'write-watch', 'NUMA', 'virtual-memory-query')
            note = 'Decommit is a correctness-relevant GC operation and cannot be treated as a successful no-op. The current skeleton returns failure until the existing VM PAL semantics are extended or the GC configuration proves it unnecessary.'
        }
        threads = [ordered]@{
            firstPayload = 'A one-managed-thread payload does not justify a general thread-store ABI, but the GC source contains helper/suspension paths that must be disabled or bounded explicitly.'
            laterRuntime = @('dedicated or helper GC threads', 'thread registration', 'suspension coordination', 'thread-store enumeration')
            currentStatus = 'Not implemented in Phase 21; no fake multi-thread protocol is introduced.'
        }
        suspension = [ordered]@{
            firstPayload = 'Only safe when runtime configuration prevents helper-thread and concurrent-suspension paths.'
            generalWorkstationGC = 'Requires a real wait/wake and thread-cooperation design; it cannot be represented by a process-global Windows event object.'
        }
    }
    synchronizationDecision = [ordered]@{
        result = 'Option 2 proposed for Phase 22'
        userLocal = @(
            'atomic compare/exchange lock ownership with acquire/release ordering',
            'atomic event state word for manual-reset and auto-reset state',
            'bounded spin before blocking',
            'release publication before Set/wake'
        )
        semantics = @(
            'CLRCriticalSection requires mutual exclusion and recursive/ownership behavior as defined by the GC wrapper; the uncontended path is user-local.',
            'GCEvent requires manual-reset and auto-reset state, Set, Reset, timeout-zero polling, and a blocking wait when unsignaled.',
            'The blocking path needs address/value wait and wake-one/wake-all semantics; it does not need HANDLEs, event objects, or Win32 wait APIs.',
            'Timeouts must be monotonic and bounded; a wake must not lose a state publication under acquire/release ordering.'
        )
        proposedKernelPrimitives = @(
            [ordered]@{ name = 'guidexos_pal_wait'; semantic = 'wait(address, expectedValue, timeout)' ; reason = 'Blocks only when the atomic value still equals the expected value and returns on change or timeout.' },
            [ordered]@{ name = 'guidexos_pal_wake'; semantic = 'wake(address, count)' ; reason = 'Wakes one or all waiters associated with the address; no object namespace is exposed.' }
        )
        currentPhase19ContractChanged = $false
        currentPhase19ExternalSymbolCount = 20
        decisionGate = 'Do not add these symbols until a compile/runtime call-site trace proves the GC cannot be configured into a one-thread, non-blocking first payload.'
    }
}

New-Item -ItemType Directory -Force -Path $OutputRoot | Out-Null
$outputPath = Join-Path $OutputRoot 'gc-os-inventory.json'
$inventory | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $outputPath -Encoding UTF8
$inventory | ConvertTo-Json -Depth 4
Write-Output "Wrote $outputPath"
