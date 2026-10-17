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

$disposeLabels = Get-CSharpMethodBody $taskManagerSource `
    'private\s+void\s+DisposeMemoryDetailLabels\s*\('

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
}

Assert-Regression ($drawMemoryDetails -match 'IsWithinBounds\s*\(') `
    'heap values must be validated before presentation'
Assert-Regression ($drawMemoryDetails -match 'MemorySizeBytes\s*>\s*0') `
    'an unavailable zero-sized heap must not be divided or subtracted'
Assert-Regression ($drawMemoryDetails -match 'CreateOwnedMemoryLabel\("N/A"\)') `
    'unavailable system memory must render a recoverable N/A value'

Write-Output 'Task Manager Memory Details regression: PASS (ownership, bounds, unavailable data)'
