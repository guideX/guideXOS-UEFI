[CmdletBinding()]
param([string]$BuildRoot = '', [string]$RamdiskSource = '')

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
if ([string]::IsNullOrWhiteSpace($BuildRoot)) {
    $BuildRoot = Join-Path $root 'out\dotnet\phase31-managed-shell'
}
if ([string]::IsNullOrWhiteSpace($RamdiskSource)) {
    $RamdiskSource = Join-Path $root 'ramdisk_src\Native'
}

$record = Get-Content -Raw (Join-Path $BuildRoot 'phase31-managed-shell-build.json') |
    ConvertFrom-Json
$descriptorBuilder = Join-Path $PSScriptRoot 'build_phase31_descriptor.py'
$pythonCommand = Join-Path $env:USERPROFILE '.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe'
if (-not (Test-Path -LiteralPath $pythonCommand)) { $pythonCommand = 'python' }
$wireValidator = Join-Path $PSScriptRoot 'validate_phase31_wire.py'
& $pythonCommand -B $wireValidator
if ($LASTEXITCODE -ne 0) { throw "Phase 31 wire-layout validation failed: $LASTEXITCODE" }
$baseNames = @{
    'success' = 'guideXOS.Phase31ManagedShellLaunchProof'
    'invalid-target' = 'guideXOS.Phase31InvalidTargetProof'
    'oversize' = 'guideXOS.Phase31OversizeTargetProof'
    'failfast' = 'guideXOS.Phase31FailFastProof'
    'stale-owner' = 'guideXOS.Phase31StaleOwnerProof'
}
New-Item -ItemType Directory -Force -Path $RamdiskSource | Out-Null

foreach ($payload in $record.payloads) {
    $artifact = [string]$payload.path
    $mode = [string]$payload.name
    $base = $baseNames[$mode]
    if ([string]::IsNullOrWhiteSpace($base)) { throw "Unknown Phase 31 payload mode: $mode" }
    $payloadRoot = Split-Path -Parent (Split-Path -Parent $artifact)
    $map = Join-Path $payloadRoot 'guidexos.map'
    $linkResponse = Join-Path $payloadRoot `
        'obj\Release\net9.0\guidexos-x64\native\link.rsp'
    if (-not (Test-Path -LiteralPath $map)) { throw "Phase 31 map is absent: $map" }
    if (-not (Test-Path -LiteralPath $linkResponse)) {
        throw "Phase 31 link response is absent: $linkResponse"
    }
    $exeTarget = Join-Path $RamdiskSource ($base + '.exe')
    $descriptorTarget = Join-Path $RamdiskSource ($base + '.gxmi')
    Copy-Item -LiteralPath $artifact -Destination $exeTarget -Force
    & $pythonCommand -B @($descriptorBuilder, $artifact, $map, $descriptorTarget, $mode)
    if ($LASTEXITCODE -ne 0) { throw "Phase 31 $mode descriptor failed: $LASTEXITCODE" }
    $verifier = Join-Path $PSScriptRoot 'verify_phase31.py'
    $verification = Join-Path $payloadRoot 'phase31-verify.json'
    & $pythonCommand -B @($verifier, $exeTarget, $descriptorTarget, $map,
        $linkResponse, '--mode', $mode, '--output', $verification)
    if ($LASTEXITCODE -ne 0) {
        throw "Phase 31 $mode static artifact validation failed: $LASTEXITCODE"
    }
    Write-Host "PHASE31_ARTIFACT_VALIDATION_$mode=PASS"
    Get-FileHash -Algorithm SHA256 -LiteralPath $exeTarget
    Get-Item -LiteralPath $descriptorTarget | Select-Object FullName, Length
}
