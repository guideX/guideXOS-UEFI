[CmdletBinding()]
param([string]$BuildRoot = '', [string]$RamdiskSource = '')

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
if ([string]::IsNullOrWhiteSpace($BuildRoot)) {
    $BuildRoot = Join-Path $root 'out\dotnet\phase32-managed-open-document'
}
if ([string]::IsNullOrWhiteSpace($RamdiskSource)) {
    $RamdiskSource = Join-Path $root 'ramdisk_src\Native'
}

$record = Get-Content -Raw (Join-Path $BuildRoot `
    'phase32-managed-open-document-build.json') | ConvertFrom-Json
$descriptorBuilder = Join-Path $PSScriptRoot 'build_phase32_descriptor.py'
$pythonCommand = Join-Path $env:USERPROFILE `
    '.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe'
if (-not (Test-Path -LiteralPath $pythonCommand)) {
    $pythonCommand = 'python'
}
$wireValidator = Join-Path $PSScriptRoot 'validate_phase32_wire.py'
& $pythonCommand -B $wireValidator
if ($LASTEXITCODE -ne 0) {
    throw "Phase 32 wire-layout validation failed: $LASTEXITCODE"
}
$baseNames = @{
    'success' = 'guideXOS.Phase32ManagedOpenDocumentProof'
    'failfast' = 'guideXOS.Phase32OpenDocumentFailFastProof'
    'stale-owner' = 'guideXOS.Phase32OpenDocumentStaleOwnerProof'
}
New-Item -ItemType Directory -Force -Path $RamdiskSource | Out-Null

foreach ($payload in $record.payloads) {
    $artifact = [string]$payload.path
    $mode = [string]$payload.name
    $base = $baseNames[$mode]
    if ([string]::IsNullOrWhiteSpace($base)) {
        throw "Unknown Phase 32 payload mode: $mode"
    }
    $payloadRoot = Split-Path -Parent (Split-Path -Parent $artifact)
    $map = Join-Path $payloadRoot 'guidexos.map'
    $linkResponse = Join-Path $payloadRoot `
        'obj\Release\net9.0\guidexos-x64\native\link.rsp'
    if (-not (Test-Path -LiteralPath $map)) {
        throw "Phase 32 map is absent: $map"
    }
    if (-not (Test-Path -LiteralPath $linkResponse)) {
        throw "Phase 32 link response is absent: $linkResponse"
    }
    $exeTarget = Join-Path $RamdiskSource ($base + '.exe')
    $descriptorTarget = Join-Path $RamdiskSource ($base + '.gxmi')
    Copy-Item -LiteralPath $artifact -Destination $exeTarget -Force
    & $pythonCommand -B @($descriptorBuilder, $artifact, $map,
        $descriptorTarget, $mode)
    if ($LASTEXITCODE -ne 0) {
        throw "Phase 32 $mode descriptor failed: $LASTEXITCODE"
    }
    $verifier = Join-Path $PSScriptRoot 'verify_phase32.py'
    $verification = Join-Path $payloadRoot 'phase32-verify.json'
    & $pythonCommand -B @($verifier, $exeTarget, $descriptorTarget, $map,
        $linkResponse, '--mode', $mode, '--output', $verification)
    if ($LASTEXITCODE -ne 0) {
        throw "Phase 32 $mode artifact validation failed: $LASTEXITCODE"
    }
    Write-Host "PHASE32_ARTIFACT_VALIDATION_$mode=PASS"
    Get-FileHash -Algorithm SHA256 -LiteralPath $exeTarget
    Get-Item -LiteralPath $descriptorTarget | Select-Object FullName, Length
}
