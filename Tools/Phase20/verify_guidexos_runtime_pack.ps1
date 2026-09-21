[CmdletBinding()]
param(
    [string]$PackManifest = ''
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
if ([string]::IsNullOrWhiteSpace($PackManifest)) {
    $PackManifest = Join-Path $root 'out\dotnet\phase20-runtime-pack\guidexos-runtime-pack.manifest.json'
}

if (-not (Test-Path -LiteralPath $PackManifest -PathType Leaf)) {
    throw "No completed guidexos-x64 runtime-pack manifest exists at $PackManifest. Phase 20 is stopped before source adaptation; refusing to use or imply a win-x64 fallback."
}

$manifest = Get-Content -LiteralPath $PackManifest -Raw | ConvertFrom-Json
if ([string]$manifest.target -ne 'guidexos-x64') {
    throw "Runtime-pack identity mismatch: expected guidexos-x64, found $($manifest.target)."
}
if ([bool]$manifest.customSourceAdaptationComplete -ne $true) {
    throw 'Runtime-pack is not marked as produced from the adapted NativeAOT source. Refusing payload build.'
}
if ([bool]$manifest.stockWinX64Fallback -eq $true) {
    throw 'Runtime-pack manifest explicitly records a stock win-x64 fallback. Refusing payload build.'
}

Write-Output "Verified custom runtime-pack identity: $($manifest.target)"
