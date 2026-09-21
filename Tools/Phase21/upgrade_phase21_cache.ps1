[CmdletBinding()]
param(
    [string]$SourceRoot = '',
    [string]$OutputRoot = ''
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
if ([string]::IsNullOrWhiteSpace($SourceRoot)) { $SourceRoot = Join-Path $root 'out\rt' }
if ([string]::IsNullOrWhiteSpace($OutputRoot)) { $OutputRoot = Join-Path $root 'out\dotnet\phase21-cache-upgrade' }

$lock = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'runtime-source.lock.json') -Raw | ConvertFrom-Json
$actualCommit = (& git -C $SourceRoot rev-parse HEAD).Trim()
if ($actualCommit -ne [string]$lock.runtimeCommit) { throw "Runtime source mismatch. Expected $($lock.runtimeCommit), found $actualCommit." }

function SourcePath([string]$relativePath) { Join-Path $SourceRoot ($relativePath -replace '/', '\') }
function ReadSource([string]$relativePath) { [IO.File]::ReadAllText((SourcePath $relativePath)) }
function WriteSource([string]$relativePath, [string]$text, [bool]$hadCrlf) {
    if ($hadCrlf) { $text = $text.Replace("`n", "`r`n") }
    [IO.File]::WriteAllText((SourcePath $relativePath), $text, [Text.UTF8Encoding]::new($false))
}
function RewriteExact([string]$relativePath, [string]$pattern, [string]$replacement, [string]$name) {
    $path = SourcePath $relativePath
    $original = [IO.File]::ReadAllText($path)
    $hadCrlf = $original.Contains("`r`n")
    $text = $original.Replace("`r`n", "`n")
    $matches = [regex]::Matches($text, $pattern)
    if ($matches.Count -ne 1) { throw "Upgrade '$name' expected one match in $relativePath, found $($matches.Count)." }
    WriteSource $relativePath ([regex]::Replace($text, $pattern, $replacement, 1)) $hadCrlf
}

$expectedTracked = @($lock.sourceFiles | ForEach-Object { $_.path -replace '/', '\' } | Sort-Object)
$expectedTemplates = @($lock.repositoryOwnedTemplates | ForEach-Object { $_ -replace '/', '\' } | Sort-Object)
$tracked = @(& git -C $SourceRoot diff --name-only | ForEach-Object { $_ -replace '/', '\' } | Sort-Object)
$untracked = @(& git -C $SourceRoot status --short --untracked-files=all | Where-Object { $_ -match '^\?\?\s+' } | ForEach-Object { ($_ -replace '^\?\?\s+', '') -replace '/', '\' } | Sort-Object)
if ((@($tracked) -join '|') -ne (@($expectedTracked) -join '|')) { throw 'Existing Phase 21 cache has an unexpected tracked diff.' }
if ((@($untracked) -join '|') -ne (@($expectedTemplates) -join '|')) { throw 'Existing Phase 21 cache has unexpected untracked files.' }

$structs = ReadSource 'src/coreclr/gc/env/gcenv.structs.h'
if ($structs -notmatch '(?m)bool IsCurrentThread\(\) \{ return false; \}') { throw 'Cache is not at the original Phase 21 thread-identity skeleton.' }
$gc = ReadSource 'src/coreclr/gc/guidexos/gcenv.guidexos.cpp'
if ($gc -notmatch '(?m)^#include <cstdlib>\s*$' -or $gc -notmatch '(?m)std::abort\(\);') { throw 'Cache is not at the original Phase 21 fail-closed skeleton.' }
$pal = ReadSource 'src/coreclr/nativeaot/Runtime/guidexos/GuidexosGcPal.h'
if ($pal -match '(?m)guidexos_pal_fail_fast') { throw 'Cache already contains the Phase 21 PAL fail-fast update.' }

$threadReplacement = @'
#elif defined(TARGET_GUIDEXOS)

extern "C" uint64_t guidexos_pal_thread_id(void);

class EEThreadId
{
    uint64_t m_id = 0;
public:
    bool IsCurrentThread() { return m_id != 0 && m_id == guidexos_pal_thread_id(); }
    void SetToCurrentThread() { m_id = guidexos_pal_thread_id(); }
    void Clear() { m_id = 0; }
};
'@
RewriteExact 'src/coreclr/gc/env/gcenv.structs.h' '(?ms)#elif defined\(TARGET_GUIDEXOS\)\r?\n\r?\nclass EEThreadId\r?\n\{\r?\n    uint64_t m_id = 0;\r?\npublic:\r?\n    bool IsCurrentThread\(\) \{ return false; \}\r?\n    void SetToCurrentThread\(\) \{ m_id = 0; \}\r?\n    void Clear\(\) \{ m_id = 0; \}\r?\n\};' $threadReplacement 'route GC thread identity through PAL'

RewriteExact 'src/coreclr/gc/guidexos/gcenv.guidexos.cpp' '(?m)^#include <cstdlib>\r?\n' '' 'remove direct CRT abort include'
$failFastReplacement = @'
        guidexos_pal_fail_fast(0x47585553u, nullptr);
        for (;;)
        {
            YieldProcessor();
        }
'@
RewriteExact 'src/coreclr/gc/guidexos/gcenv.guidexos.cpp' '(?m)^        std::abort\(\);$' $failFastReplacement 'route unsupported GC paths through PAL fail-fast'
$palReplacement = @'
    uint64_t guidexos_pal_monotonic_frequency(void);
    void guidexos_pal_fail_fast(uint32_t reason, void* context);
'@
RewriteExact 'src/coreclr/nativeaot/Runtime/guidexos/GuidexosGcPal.h' '(?m)^    uint64_t guidexos_pal_monotonic_frequency\(void\);$' $palReplacement 'declare existing PAL fail-fast symbol'

$currentTracked = @(& git -C $SourceRoot diff --name-only | ForEach-Object { $_ -replace '/', '\' } | Sort-Object)
$currentUntracked = @(& git -C $SourceRoot status --short --untracked-files=all | Where-Object { $_ -match '^\?\?\s+' } | ForEach-Object { ($_ -replace '^\?\?\s+', '') -replace '/', '\' } | Sort-Object)
if ((@($currentTracked) -join '|') -ne (@($expectedTracked) -join '|')) { throw 'Cache upgrade changed the approved tracked file set.' }
if ((@($currentUntracked) -join '|') -ne (@($expectedTemplates) -join '|')) { throw 'Cache upgrade changed the approved template file set.' }

$record = [ordered]@{
    schema = 1
    phase = 21
    operation = 'upgrade-existing-phase21-cache'
    source = [ordered]@{ checkout = (Resolve-Path $SourceRoot).Path; commit = $actualCommit }
    changes = @(
        'GC EEThreadId now uses guidexos_pal_thread_id for ownership checks.'
        'Unsupported GC paths now use guidexos_pal_fail_fast instead of std::abort.'
    )
    trackedFilesChanged = $currentTracked
    templateFilesChanged = $currentUntracked
    sourceDiffCheck = (& git -C $SourceRoot diff --check | Out-String).Trim()
}
New-Item -ItemType Directory -Force -Path $OutputRoot | Out-Null
$recordPath = Join-Path $OutputRoot 'phase21-cache-upgrade-record.json'
$record | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $recordPath -Encoding UTF8
$record | ConvertTo-Json -Depth 5
Write-Output "Wrote $recordPath"
