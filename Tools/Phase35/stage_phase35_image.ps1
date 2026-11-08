[CmdletBinding()]
param([string]$BuildRoot = '', [string]$RamdiskSource = '')

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
if ([string]::IsNullOrWhiteSpace($BuildRoot)) {
    $BuildRoot = Join-Path $root 'out\dotnet\phase35-managed-persistent-read'
}
if ([string]::IsNullOrWhiteSpace($RamdiskSource)) {
    $RamdiskSource = Join-Path $root 'ramdisk_src\Native'
}

$record = Get-Content -Raw (Join-Path $BuildRoot `
    'phase35-managed-persistent-read-build.json') | ConvertFrom-Json
$descriptorBuilder = Join-Path $PSScriptRoot 'build_phase35_descriptor.py'
$pythonCommand = Join-Path $env:USERPROFILE `
    '.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe'
if (-not (Test-Path -LiteralPath $pythonCommand)) { $pythonCommand = 'python' }
& $pythonCommand -B (Join-Path $PSScriptRoot 'validate_phase35_wire.py')
if ($LASTEXITCODE -ne 0) { throw "Phase 35 wire validation failed: $LASTEXITCODE" }
$verifier = Join-Path $PSScriptRoot 'verify_phase35.py'
$baseNames = @{
    'success' = 'guideXOS.Phase35ManagedPersistentReadProof'
    'failfast' = 'guideXOS.Phase35PersistentReadFailFastProof'
    'stale-owner' = 'guideXOS.Phase35PersistentReadStaleOwnerProof'
    'cross-scope' = 'guideXOS.Phase35PersistentReadCrossScopeProof'
    'malformed' = 'guideXOS.Phase35PersistentReadMalformedProof'
    'no-read' = 'guideXOS.Phase35NoReadProof'
    'one-read' = 'guideXOS.Phase35OneReadProof'
    'two-read' = 'guideXOS.Phase35TwoReadProof'
    'not-found' = 'guideXOS.Phase35NotFoundProof'
}
New-Item -ItemType Directory -Force -Path $RamdiskSource | Out-Null

foreach ($payload in $record.payloads) {
    $artifact = [string]$payload.path
    $mode = [string]$payload.name
    $base = $baseNames[$mode]
    if ([string]::IsNullOrWhiteSpace($base)) { throw "Unknown Phase 35 mode: $mode" }
    $payloadRoot = Split-Path -Parent (Split-Path -Parent $artifact)
    $map = Join-Path $payloadRoot 'guidexos.map'
    $linkResponse = Join-Path $payloadRoot `
        'obj\Release\net9.0\guidexos-x64\native\link.rsp'
    if (-not (Test-Path -LiteralPath $map)) { throw "Phase 35 map is absent: $map" }
    if (-not (Test-Path -LiteralPath $linkResponse)) { throw "Phase 35 link response is absent: $linkResponse" }
    $exeTarget = Join-Path $RamdiskSource ($base + '.exe')
    $descriptorTarget = Join-Path $RamdiskSource ($base + '.gxmi')
    Copy-Item -LiteralPath $artifact -Destination $exeTarget -Force
    & $pythonCommand -B @($descriptorBuilder, $artifact, $map,
        $descriptorTarget, $mode)
    if ($LASTEXITCODE -ne 0) { throw "Phase 35 $mode descriptor failed: $LASTEXITCODE" }
    $verification = Join-Path $payloadRoot 'phase35-verify.json'
    & $pythonCommand -B @($verifier, $exeTarget, $descriptorTarget, $map,
        $linkResponse, '--mode', $mode, '--output', $verification)
    if ($LASTEXITCODE -ne 0) { throw "Phase 35 $mode artifact validation failed: $LASTEXITCODE" }
    Write-Host "PHASE35_ARTIFACT_VALIDATION_$mode=PASS"
    Get-FileHash -Algorithm SHA256 -LiteralPath $exeTarget
    Get-Item -LiteralPath $descriptorTarget | Select-Object FullName, Length
}
