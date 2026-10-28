$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$sourcePath = Join-Path $repoRoot 'guideXOS\GUI\NotificationManager.cs'
$source = Get-Content -LiteralPath $sourcePath -Raw
$signature = [regex]::Match(
    $source,
    'internal\s+static\s+void\s+ResetForKernelBoot\s*\(\s*\)\s*\{')
if (-not $signature.Success) {
    throw 'ResetForKernelBoot method is missing.'
}

$openBrace = $source.IndexOf('{', $signature.Index)
$depth = 0
$closeBrace = -1
for ($index = $openBrace; $index -lt $source.Length; $index++) {
    if ($source[$index] -eq '{') { $depth++ }
    elseif ($source[$index] -eq '}') {
        $depth--
        if ($depth -eq 0) {
            $closeBrace = $index
            break
        }
    }
}
if ($closeBrace -lt 0) {
    throw 'ResetForKernelBoot method body is unterminated.'
}

$body = $source.Substring($openBrace + 1, $closeBrace - $openBrace - 1)
$assignment = [regex]::Matches(
    $body,
    'Notifications\s*=\s*new\s+List\s*<\s*Notify\s*>\s*\(\s*\)\s*;')
if ($assignment.Count -ne 1) {
    throw "Expected one direct replacement with an empty notification list; found $($assignment.Count)."
}

$remaining = [regex]::Replace(
    $body,
    'Notifications\s*=\s*new\s+List\s*<\s*Notify\s*>\s*\(\s*\)\s*;',
    '')
if ($remaining -match 'Notifications|\.Dispose\s*\(|\b(?:foreach|for|while)\s*\(') {
    throw 'Boot reset must not inspect, enumerate, or dispose the prior notification graph.'
}

Write-Output 'NotificationManager boot-reset regression: PASS (direct replacement; no old-graph access or disposal)'
