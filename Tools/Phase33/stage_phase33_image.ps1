[CmdletBinding()]
param([string]$BuildRoot = '', [string]$RamdiskSource = '')

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
if ([string]::IsNullOrWhiteSpace($BuildRoot)) {
    $BuildRoot = Join-Path $root 'out\dotnet\phase33-managed-shell-action'
}
if ([string]::IsNullOrWhiteSpace($RamdiskSource)) {
    $RamdiskSource = Join-Path $root 'ramdisk_src\Native'
}

$record = Get-Content -Raw (Join-Path $BuildRoot `
    'phase33-managed-shell-action-build.json') | ConvertFrom-Json
$descriptorBuilder = Join-Path $PSScriptRoot 'build_phase33_descriptor.py'
$pythonCommand = Join-Path $env:USERPROFILE `
    '.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe'
if (-not (Test-Path -LiteralPath $pythonCommand)) { $pythonCommand = 'python' }
$wireValidator = Join-Path $PSScriptRoot 'validate_phase33_wire.py'
& $pythonCommand -B $wireValidator
if ($LASTEXITCODE -ne 0) { throw "Phase 33 wire validation failed: $LASTEXITCODE" }
$baseNames = @{
    'success' = 'guideXOS.Phase33ManagedShellActionProof'
    'failfast' = 'guideXOS.Phase33ShellActionFailFastProof'
    'stale-owner' = 'guideXOS.Phase33ShellActionStaleOwnerProof'
    'invalid-action' = 'guideXOS.Phase33ShellActionInvalidActionProof'
    'malformed' = 'guideXOS.Phase33ShellActionMalformedProof'
}
New-Item -ItemType Directory -Force -Path $RamdiskSource | Out-Null

foreach ($payload in $record.payloads) {
    $artifact = [string]$payload.path
    $mode = [string]$payload.name
    $base = $baseNames[$mode]
    if ([string]::IsNullOrWhiteSpace($base)) { throw "Unknown Phase 33 mode: $mode" }
    $payloadRoot = Split-Path -Parent (Split-Path -Parent $artifact)
    $map = Join-Path $payloadRoot 'guidexos.map'
    $linkResponse = Join-Path $payloadRoot `
        'obj\Release\net9.0\guidexos-x64\native\link.rsp'
    if (-not (Test-Path -LiteralPath $map)) { throw "Phase 33 map is absent: $map" }
    if (-not (Test-Path -LiteralPath $linkResponse)) { throw "Phase 33 link response is absent: $linkResponse" }
    $exeTarget = Join-Path $RamdiskSource ($base + '.exe')
    $descriptorTarget = Join-Path $RamdiskSource ($base + '.gxmi')
    Copy-Item -LiteralPath $artifact -Destination $exeTarget -Force
    & $pythonCommand -B @($descriptorBuilder, $artifact, $map, $descriptorTarget, $mode)
    if ($LASTEXITCODE -ne 0) { throw "Phase 33 $mode descriptor failed: $LASTEXITCODE" }
    $verification = Join-Path $payloadRoot 'phase33-verify.json'
    & $pythonCommand -B @((Join-Path $PSScriptRoot 'verify_phase33.py'),
        $exeTarget, $descriptorTarget, $map, $linkResponse,
        '--mode', $mode, '--output', $verification)
    if ($LASTEXITCODE -ne 0) { throw "Phase 33 $mode artifact validation failed: $LASTEXITCODE" }
    Write-Host "PHASE33_ARTIFACT_VALIDATION_$mode=PASS"
    Get-FileHash -Algorithm SHA256 -LiteralPath $exeTarget
    Get-Item -LiteralPath $descriptorTarget | Select-Object FullName, Length
}
