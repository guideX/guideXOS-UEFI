param(
    [string]$TaskManagerSourcePath = ''
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($TaskManagerSourcePath)) {
    $TaskManagerSourcePath = Join-Path $repoRoot `
        'guideXOS\DefaultApps\TaskManager.cs'
}

$taskManagerSource = Get-Content -LiteralPath $TaskManagerSourcePath -Raw
$stringPoolSource = Get-Content -LiteralPath (Join-Path $repoRoot `
    'Corlib\System\StringPool.cs') -Raw
$stringSource = Get-Content -LiteralPath (Join-Path $repoRoot `
    'Corlib\System\String.cs') -Raw

function Get-CSharpMethodBody {
    param(
        [string]$Source,
        [string]$SignaturePattern
    )

    $match = [regex]::Match($Source, $SignaturePattern)
    if (-not $match.Success) {
        throw "Required method was not found: $SignaturePattern"
    }

    $openBrace = $Source.IndexOf('{', $match.Index + $match.Length)
    if ($openBrace -lt 0) {
        throw "Method body opening brace was not found: $SignaturePattern"
    }

    $depth = 1
    for ($index = $openBrace + 1; $index -lt $Source.Length; $index++) {
        if ($Source[$index] -eq '{') {
            $depth++
        } elseif ($Source[$index] -eq '}') {
            $depth--
            if ($depth -eq 0) {
                return $Source.Substring($openBrace + 1,
                    $index - $openBrace - 1)
            }
        }
    }

    throw "Method body closing brace was not found: $SignaturePattern"
}

function Assert-Regression {
    param(
        [bool]$Condition,
        [string]$Message
    )

    if (-not $Condition) {
        throw "FAIL: $Message"
    }
}

$drawMemoryDetails = Get-CSharpMethodBody $taskManagerSource `
    'private\s+void\s+DrawMemoryDetails\s*\('
$drawProcesses = Get-CSharpMethodBody $taskManagerSource `
    'private\s+void\s+DrawProcesses\s*\('
$drawPerformance = Get-CSharpMethodBody $taskManagerSource `
    'private\s+void\s+DrawPerformance\s*\('
$drawCpuDetail = Get-CSharpMethodBody $taskManagerSource `
    'private\s+void\s+DrawCpuDetail\s*\('
$drawMemDetail = Get-CSharpMethodBody $taskManagerSource `
    'private\s+void\s+DrawMemDetail\s*\('
$drawDiskDetail = Get-CSharpMethodBody $taskManagerSource `
    'private\s+void\s+DrawDiskDetail\s*\('
$drawNetDetail = Get-CSharpMethodBody $taskManagerSource `
    'private\s+void\s+DrawNetDetail\s*\('
$ownedUptime = Get-CSharpMethodBody $taskManagerSource `
    'private\s+static\s+string\s+FormatOwnedUptime\s*\('
$stringConcat = Get-CSharpMethodBody $stringSource `
    'public\s+static\s+string\s+Concat\s*\(\s*string\s+a\s*,\s*string\s+b\s*\)'
$getNumber = Get-CSharpMethodBody $stringPoolSource `
    'public\s+static\s+string\s+GetNumber\s*\(\s*ulong\s+value\s*\)'

Assert-Regression ($getNumber -match 'return\s+_numberStrings\[intVal\]\s*;') `
    'StringPool number lookup must retain its shared cached return behavior'
Assert-Regression ($drawMemoryDetails -notmatch 'StringPool\.Get') `
    'Memory Details labels must not retain shared StringPool return values'
Assert-Regression ($taskManagerSource -notmatch
    'StringPool\.(GetMemorySize|FormatUptime)\s*\(') `
    'Task Manager must not retain exact-value memory cache strings or uptime concatenations'
Assert-Regression ($drawProcesses -match
    'FormatOwnedMemorySize\s*\(\s*bytes\s*\)' -and
    $drawProcesses -match 'memText\.Dispose\s*\(') `
    'the changing Processes memory estimate must be formatted and released within its draw call'
Assert-Regression ($drawPerformance -notmatch
    '(?:string|uint|int)\s*\[\]\s+nav(?:Labels|Colors|Values)\s*=') `
    'Performance redraw must not allocate navigation arrays per frame'
Assert-Regression ($taskManagerSource -match
    'static\s+readonly\s+string\[\]\s+PerformanceNavLabels' -and
    $taskManagerSource -match
    'static\s+readonly\s+uint\[\]\s+PerformanceNavColors') `
    'Performance navigation labels and colors must use fixed shared arrays'
Assert-Regression ($drawCpuDetail -match
    'FormatOwnedUptime\s*\(' -and $drawCpuDetail -match
    'uptimeStr\.Dispose\s*\(' -and $ownedUptime -match
    'finally' -and $ownedUptime -match 'prefix\.Dispose\s*\(') `
    'CPU uptime text and its intermediate strings must be released after drawing'
Assert-Regression ($drawMemDetail -notmatch 'StringPool\.GetMemorySize' -and
    $drawMemDetail -match 'usedMBStr\.Dispose\s*\(' -and
    $drawMemDetail -match 'availStr\.Dispose\s*\(' -and
    $drawMemDetail -match 'totalStr\.Dispose\s*\(' -and
    $drawMemDetail -match 'tagBytes\.Dispose\s*\(') `
    'Performance memory-size labels must not grow the exact-value cache'
Assert-Regression ($drawDiskDetail -match 'respStr\.Dispose\s*\(' -and
    $drawDiskDetail -match 'readStr\.Dispose\s*\(' -and
    $drawDiskDetail -match 'writeStr\.Dispose\s*\(' -and
    $drawNetDetail -match 'sendStr\.Dispose\s*\(' -and
    $drawNetDetail -match 'recvStr\.Dispose\s*\(' -and
    $drawNetDetail -match 'sentStr\.Dispose\s*\(' -and
    $drawNetDetail -match 'recvBytesStr\.Dispose\s*\(') `
    'fresh Disk and Network tab text must be released after drawing'
Assert-Regression ($drawMemoryDetails -notmatch '_md\w+Str\s*\?\.\s*Dispose\s*\(') `
    'Memory Details must not dispose pooled labels during drawing'
Assert-Regression ($drawMemoryDetails -match 'DisposeMemoryDetailLabels\s*\(') `
    'previous owned labels must be released before the next refresh'

$calculateMemoryPercent = Get-CSharpMethodBody $taskManagerSource `
    'private\s+static\s+int\s+CalculateMemoryPercent\s*\('
$updateMemoryStats = Get-CSharpMethodBody $taskManagerSource `
    'private\s+void\s+UpdateMemoryDetailStats\s*\('
$ownedLabelFactory = Get-CSharpMethodBody $taskManagerSource `
    'private\s+static\s+string\s+CreateOwnedMemoryLabel\s*\('
$chartDispose = Get-CSharpMethodBody $taskManagerSource `
    'public\s+override\s+void\s+Dispose\s*\('
$disposeMatches = [regex]::Matches($taskManagerSource, `
    'public\s+override\s+void\s+Dispose\s*\(')
Assert-Regression ($disposeMatches.Count -ge 2) `
    'chart and Task Manager disposal methods must be present'
$windowDispose = Get-CSharpMethodBody `
    $taskManagerSource.Substring($disposeMatches[1].Index) `
    'public\s+override\s+void\s+Dispose\s*\('
$chartEnsureSize = Get-CSharpMethodBody $taskManagerSource `
    'public\s+void\s+EnsureSize\s*\('
$sampleOwnerBytes = Get-CSharpMethodBody $taskManagerSource `
    'private\s+void\s+SampleOwnerBytes\s*\('
$refreshSystemSnapshot = Get-CSharpMethodBody $taskManagerSource `
    'private\s+void\s+RefreshSystemSnapshot\s*\('
$serviceBackendsSource = Get-Content -LiteralPath (Join-Path $repoRoot `
    'guideXOS\OS\ApplicationServiceBackends.cs') -Raw
$systemInformationBackend = Get-CSharpMethodBody $serviceBackendsSource `
    'public\s+override\s+ApplicationServiceResult<SystemInformationSnapshot>\s+GetSnapshot\s*\('
$applicationFactoriesSource = Get-Content -LiteralPath (Join-Path $repoRoot `
    'guideXOS\OS\ApplicationFactories.cs') -Raw
$createApplicationServices = Get-CSharpMethodBody $applicationFactoriesSource `
    'protected\s+bool\s+TryCreateApplicationServices\s*\('
$applicationInstanceSource = Get-Content -LiteralPath (Join-Path $repoRoot `
    'guideXOS\OS\ApplicationInstance.cs') -Raw
$completeApplicationLaunch = Get-CSharpMethodBody $applicationInstanceSource `
    'internal\s+static\s+bool\s+TryCompleteLaunch\s*\('
$notifyWindowForeground = Get-CSharpMethodBody $applicationInstanceSource `
    'internal\s+static\s+void\s+NotifyWindowForeground\s*\('
$windowManagerSource = Get-Content -LiteralPath (Join-Path $repoRoot `
    'guideXOS\GUI\WindowManager.cs') -Raw
$focusWindow = Get-CSharpMethodBody $windowManagerSource `
    'public\s+static\s+void\s+FocusWindow\s*\('
$taskbarSource = Get-Content -LiteralPath (Join-Path $repoRoot `
    'guideXOS\GUI\Taskbar.cs') -Raw

$disposeLabels = Get-CSharpMethodBody $taskManagerSource `
    'private\s+void\s+DisposeMemoryDetailLabels\s*\('
Assert-Regression ($windowDispose -match 'DisposeMemoryDetailLabels\s*\(') `
    'window close must release all privately owned Memory Details labels'
Assert-Regression ($windowDispose -match '_cpuChart\s*\?\.\s*Dispose\s*\(' -and
    $windowDispose -match '_memChart\s*\?\.\s*Dispose\s*\(' -and
    $windowDispose -match '_diskChart\s*\?\.\s*Dispose\s*\(' -and
    $windowDispose -match '_netChart\s*\?\.\s*Dispose\s*\(') `
    'window close must release all four chart owners'
Assert-Regression ($chartDispose -match 'DisposeBuffers\s*\(' -and
    $chartDispose -match 'base\.Dispose\s*\(') `
    'chart disposal must release its buffers and chart object'
Assert-Regression ($chartEnsureSize -match 'DisposeBuffers\s*\(') `
    'chart resize must release the replaced image and graphics buffers'
Assert-Regression ($refreshSystemSnapshot -match 'finally' -and
    $refreshSystemSnapshot -match 'result\.Dispose\s*\(') `
    'Task Manager must dispose every system snapshot result'
Assert-Regression ($refreshSystemSnapshot -match
    'snapshot\.MemorySizeBytes\s*==\s*0' -and
    $refreshSystemSnapshot -match 'Allocator\.MemorySize' -and
    $refreshSystemSnapshot -match 'Allocator\.MemoryInUse' -and
    $refreshSystemSnapshot -match
    'new\s+SystemInformationSnapshot\s*\(') `
    'zeroed generic scalar projection must recover bounded memory counters from the allocator'
Assert-Regression ($refreshSystemSnapshot -match 'Timer\.Ticks' -and
    $refreshSystemSnapshot -match 'ThreadPool\.CPUUsage' -and
    $refreshSystemSnapshot -match 'ThreadPool\.ThreadCount' -and
    $refreshSystemSnapshot -match 'snapshot\.OsName' -and
    $refreshSystemSnapshot -match 'snapshot\.OsVersion' -and
    $refreshSystemSnapshot -match 'snapshot\.Architecture') `
    'snapshot fallback must restore scalar counters while preserving service text fields'
Assert-Regression ($systemInformationBackend -match 'valid\.Dispose\s*\(') `
    'system information service must dispose its context validation result'
Assert-Regression ($sampleOwnerBytes -match 'finally' -and
    $sampleOwnerBytes -match 'snap\.Dispose\s*\(') `
    'owner sampling must release its allocator snapshot on every exit'
Assert-Regression ($windowDispose -match '_ownerIds\s*\?\.\s*Dispose\s*\(' -and
    $windowDispose -match '_lastOwnerBytes\s*\?\.\s*Dispose\s*\(' -and
    $windowDispose -match '_ownerKBps\s*\?\.\s*Dispose\s*\(' -and
    $windowDispose -match '_leakHistory\s*\?\.\s*Dispose\s*\(' -and
    $windowDispose -match '_blameOwnerIds\s*\?\.\s*Dispose\s*\(' -and
    $windowDispose -match '_ownerHistoryStart\s*\?\.\s*Dispose\s*\(' -and
    $windowDispose -match '_ownerGrowthCounter\s*\?\.\s*Dispose\s*\(') `
    'window close must release every Task Manager owned sampling array'
Assert-Regression ($updateMemoryStats -match
    '_leakHistoryNext\+\+' -and
    $updateMemoryStats -match '_leakHistoryCount\s*<\s*_leakHistoryMaxSamples' -and
    $updateMemoryStats -notmatch '_leakHistory\.(Add|RemoveAt)\s*\(') `
    'leak history must reuse its bounded owned ring buffer'
Assert-Regression ($createApplicationServices -match
    'serviceResult\s*!=\s*null\)\s*serviceResult\.Dispose\s*\(') `
    'successful factory service validation results must be released after use'
Assert-Regression ($completeApplicationLaunch -match
    'resumed\.Dispose\s*\(' -and $completeApplicationLaunch -match
    'activated\.Dispose\s*\(') `
    'application launch must release lifecycle results after reading them'
Assert-Regression ($notifyWindowForeground -match 'result\.Dispose\s*\(' -and
    $focusWindow -match 'result\.Dispose\s*\(' -and
    ([regex]::Matches($taskbarSource,
        'lifecycle\.Dispose\s*\(').Count -ge 2)) `
    'window and taskbar focus paths must release consumed lifecycle results'
Assert-Regression ($ownedLabelFactory -match 'value\s*\+\s*string\.Empty') `
    'literal labels must be copied into privately owned strings'
Assert-Regression ($stringConcat -match 'return\s+new\s+string\s*\(ptr,\s*0,\s*Length\s*\)') `
    'the custom string concatenation path must allocate a fresh label string'

$labelFields = @(
    '_mdFreeCallsStr', '_mdFreeSuccessStr', '_mdFailPtrStr',
    '_mdFailNoPagesStr', '_mdAllocPagesStr', '_mdFreedPagesStr',
    '_mdPagesInUseStr', '_mdNetGrowthStr', '_mdLeakStr',
    '_mdFreeAllocRatioStr', '_mdHeapSizeStr', '_mdHeapUsedStr',
    '_mdHeapFreeStr', '_mdHeapUtilStr'
)
foreach ($field in $labelFields) {
    $escapedField = [regex]::Escape($field)
    Assert-Regression ([regex]::IsMatch($drawMemoryDetails,
        $escapedField + '\s*=\s*(FormatOwnedMemoryNumber|' +
        'FormatOwnedMemoryPercent|FormatOwnedMemorySize|' +
        'CreateOwnedMemoryLabel)\s*\(')) `
        "$field must be formatted into an owned label string"
    Assert-Regression ([regex]::IsMatch($disposeLabels,
        $escapedField + '\s*\?\.\s*Dispose\s*\(')) `
        "$field's owned label must be released on refresh and close"
    Assert-Regression ([regex]::IsMatch($disposeLabels,
        $escapedField + '\s*=\s*null\s*;')) `
        "$field must be cleared after its owned allocation is released"
}

$disposeCall = $drawMemoryDetails.IndexOf('DisposeMemoryDetailLabels()')
$firstLabelAssignment = $drawMemoryDetails.IndexOf(
    '_mdFreeCallsStr = FormatOwnedMemoryNumber')
Assert-Regression ($disposeCall -ge 0 -and $firstLabelAssignment -gt $disposeCall) `
    'refresh must release prior labels before replacing them during drawing'

Assert-Regression ($drawMemoryDetails -match
    '_systemSnapshotMemoryInUseBytes\s*<=\s*_systemSnapshotMemorySizeBytes') `
    'heap values must be checked against total memory before presentation'
Assert-Regression ($drawMemoryDetails -match 'MemorySizeBytes\s*>\s*0') `
    'an unavailable zero-sized heap must not be divided or subtracted'
Assert-Regression ($drawMemoryDetails -match
    'CalculateMemoryPercent\s*\(\s*heapUsed\s*,\s*heapSize\s*\)') `
    'heap utilization must use overflow-safe percentage arithmetic'
Assert-Regression ($drawMemoryDetails -notmatch 'heapUsed\s*\*\s*100') `
    'heap utilization must not multiply untrusted snapshot values'
Assert-Regression ($calculateMemoryPercent -match
    'total\s*-\s*used' -and $calculateMemoryPercent -match
    'remainder\s*>?=\s*threshold' -and $calculateMemoryPercent -match
    'remainder\s*\+=\s*used') `
    'percentage helper must use bounded remainder addition'
Assert-Regression ($calculateMemoryPercent -notmatch '\*\s*100') `
    'percentage helper must not multiply by 100'
Assert-Regression ($drawMemoryDetails -match
    'CalculateMemoryPercent\s*\(\s*_cumulativeFreedPages\s*,\s*_cumulativeAllocatedPages\s*\)') `
    'free/allocation ratio must also avoid overflow'
Assert-Regression ($updateMemoryStats -match
    '_cumulativeAllocatedPages\s*>=\s*_cumulativeFreedPages' -and
    $updateMemoryStats -match
    '_cumulativeAllocatedPages\s*-\s*_cumulativeFreedPages' -and
    $updateMemoryStats -match ':\s*0UL') `
    'net growth must clamp inconsistent cumulative totals before unsigned subtraction'
Assert-Regression ($drawMemoryDetails -match 'CreateOwnedMemoryLabel\("N/A"\)') `
    'unavailable system memory must render a recoverable N/A value'
Assert-Regression ($drawMemoryDetails -match
    '_lastMemoryLabelSnapshotValid\s*!=\s*memorySnapshotValid' -and
    $drawMemoryDetails -match
    '_lastMemoryLabelSnapshotValid\s*=\s*memorySnapshotValid') `
    'heap label refresh must observe invalid-to-valid snapshot recovery'

# Compile the exact production helper body and exercise both ordinary and
# hostile ulong inputs. BigInteger is used only by this host-side oracle.
$runtimePercentMethod = "public static int CalculateMemoryPercent(ulong used, ulong total) {`n$calculateMemoryPercent`n}"
$runtimeType = @"
using System;
public static class TaskManagerMemoryDetailsArithmetic {
$runtimePercentMethod
}
"@
Add-Type -TypeDefinition $runtimeType -Language CSharp

$arithmeticCases = @(
    @{ Name = 'zero total and zero used'; Used = [ulong]0; Total = [ulong]0 },
    @{ Name = 'zero total with nonzero used'; Used = [ulong]7; Total = [ulong]0 },
    @{ Name = 'zero used'; Used = [ulong]0; Total = [ulong]17 },
    @{ Name = 'one third'; Used = [ulong]1; Total = [ulong]3 },
    @{ Name = 'two thirds'; Used = [ulong]2; Total = [ulong]3 },
    @{ Name = 'used equals total'; Used = [ulong]99; Total = [ulong]99 },
    @{ Name = 'used exceeds total'; Used = [ulong]100; Total = [ulong]99 },
    @{ Name = 'maximum used and total'; Used = [ulong]::MaxValue; Total = [ulong]::MaxValue },
    @{ Name = 'maximum used with total one'; Used = [ulong]::MaxValue; Total = [ulong]1 },
    @{ Name = 'near maximum ratio'; Used = ([ulong]::MaxValue - [ulong]1); Total = [ulong]::MaxValue },
    @{ Name = 'multiplication overflow boundary'; Used = [ulong]::MaxValue; Total = [ulong]::MaxValue - [ulong]12345 }
)
foreach ($case in $arithmeticCases) {
    $actual = [TaskManagerMemoryDetailsArithmetic]::CalculateMemoryPercent(
        [ulong]$case.Used, [ulong]$case.Total)
    if ($case.Total -eq 0) {
        $expected = 0
    } elseif ($case.Used -ge $case.Total) {
        $expected = 100
    } else {
        $usedBig = [System.Numerics.BigInteger]::Parse([string]$case.Used)
        $totalBig = [System.Numerics.BigInteger]::Parse([string]$case.Total)
        $expected = [int](($usedBig * 100) / $totalBig)
    }
    Assert-Regression ($actual -eq $expected) `
        "$($case.Name): expected $expected percent, got $actual"
}

# An invalid sample must not poison the next valid sample.
$invalidThenValid = @(
    @{ Used = [ulong]500; Total = [ulong]0; Expected = 0 },
    @{ Used = [ulong]3; Total = [ulong]4; Expected = 75 }
)
foreach ($case in $invalidThenValid) {
    $actual = [TaskManagerMemoryDetailsArithmetic]::CalculateMemoryPercent(
        [ulong]$case.Used, [ulong]$case.Total)
    Assert-Regression ($actual -eq $case.Expected) `
        "recovery sample: expected $($case.Expected) percent, got $actual"
}

Write-Output 'Task Manager Memory Details regression: PASS (ownership, bounds, unavailable data, overflow arithmetic)'
