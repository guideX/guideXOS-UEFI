[CmdletBinding()]
param([string]$BuildRoot = '', [string]$RamdiskSource = '')

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
if ([string]::IsNullOrWhiteSpace($BuildRoot)) {
    $BuildRoot = Join-Path $root 'out\dotnet\phase29-managed-notification'
}
if ([string]::IsNullOrWhiteSpace($RamdiskSource)) {
    $RamdiskSource = Join-Path $root 'ramdisk_src\Native'
}

$record = Get-Content -Raw (Join-Path $BuildRoot 'phase29-managed-notification-build.json') |
    ConvertFrom-Json
$descriptorBuilder = Join-Path $PSScriptRoot 'build_phase29_descriptor.py'
$pythonCommand = Join-Path $env:USERPROFILE '.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe'
if (-not (Test-Path -LiteralPath $pythonCommand)) {
    $pythonCommand = 'python'
}
$baseNames = @{
    'success' = 'guideXOS.Phase29ManagedNotificationProof'
    'title-failure' = 'guideXOS.Phase29NotificationTitleFailureProof'
    'body-failure' = 'guideXOS.Phase29NotificationBodyFailureProof'
    'invalid-type' = 'guideXOS.Phase29NotificationInvalidTypeProof'
    'failfast' = 'guideXOS.Phase29NotificationFailFastProof'
}
New-Item -ItemType Directory -Force -Path $RamdiskSource | Out-Null

foreach ($payload in $record.payloads) {
    $artifact = [string]$payload.path
    $mode = [string]$payload.name
    $base = $baseNames[$mode]
    if ([string]::IsNullOrWhiteSpace($base)) { throw "Unknown Phase 29 payload mode: $mode" }
    $map = Join-Path (Split-Path -Parent (Split-Path -Parent $artifact)) 'guidexos.map'
    if (-not (Test-Path -LiteralPath $map)) { throw "Phase 29 map is absent: $map" }
    $exeTarget = Join-Path $RamdiskSource ($base + '.exe')
    $descriptorTarget = Join-Path $RamdiskSource ($base + '.gxmi')
    Copy-Item -LiteralPath $artifact -Destination $exeTarget -Force
    & $pythonCommand @($descriptorBuilder, $artifact, $map, $descriptorTarget, $mode)
    if ($LASTEXITCODE -ne 0) { throw "Phase 29 $mode descriptor failed: $LASTEXITCODE" }
    Get-FileHash -Algorithm SHA256 -LiteralPath $exeTarget
    Get-Item -LiteralPath $descriptorTarget | Select-Object FullName, Length
}
