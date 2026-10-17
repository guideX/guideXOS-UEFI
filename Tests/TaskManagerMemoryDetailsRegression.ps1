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
$stringConcat = Get-CSharpMethodBody $stringSource `
    'public\s+static\s+string\s+Concat\s*\(\s*string\s+a\s*,\s*string\s+b\s*\)'
$getNumber = Get-CSharpMethodBody $stringPoolSource `
    'public\s+static\s+string\s+GetNumber\s*\(\s*ulong\s+value\s*\)'

Assert-Regression ($getNumber -match 'return\s+_numberStrings\[intVal\]\s*;') `
    'StringPool number lookup must retain its shared cached return behavior'
Assert-Regression ($drawMemoryDetails -notmatch 'StringPool\.Get') `
    'Memory Details labels must not retain shared StringPool return values'
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
$windowDispose = Get-CSharpMethodBody $taskManagerSource `
    'public\s+override\s+void\s+Dispose\s*\('

$disposeLabels = Get-CSharpMethodBody $taskManagerSource `
    'private\s+void\s+DisposeMemoryDetailLabels\s*\('
Assert-Regression ($windowDispose -match 'DisposeMemoryDetailLabels\s*\(') `
    'window close must release all privately owned Memory Details labels'
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

Assert-Regression ($drawMemoryDetails -match 'IsWithinBounds\s*\(') `
    'heap values must be validated before presentation'
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
Assert-Regression ($updateMemoryStats -match '~0UL\s*-\s*_cumulativeFreedPages') `
    'allocator page totals must saturate instead of overflowing'
Assert-Regression ($drawMemoryDetails -match 'CreateOwnedMemoryLabel\("N/A"\)') `
    'unavailable system memory must render a recoverable N/A value'

Write-Output 'Task Manager Memory Details regression: PASS (ownership, bounds, unavailable data)'
