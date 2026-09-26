[CmdletBinding()]
param([string]$BuildRoot = '', [string]$RamdiskSource = '')

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
if ([string]::IsNullOrWhiteSpace($BuildRoot)) {
    $BuildRoot = Join-Path $root 'out\dotnet\phase30-managed-clipboard'
}
if ([string]::IsNullOrWhiteSpace($RamdiskSource)) {
    $RamdiskSource = Join-Path $root 'ramdisk_src\Native'
}

$record = Get-Content -Raw (Join-Path $BuildRoot 'phase30-managed-clipboard-build.json') |
    ConvertFrom-Json
$descriptorBuilder = Join-Path $PSScriptRoot 'build_phase30_descriptor.py'
$pythonCommand = Join-Path $env:USERPROFILE '.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe'
if (-not (Test-Path -LiteralPath $pythonCommand)) { $pythonCommand = 'python' }
$wireValidator = Join-Path $PSScriptRoot 'validate_phase30_wire.py'
& $pythonCommand $wireValidator
if ($LASTEXITCODE -ne 0) { throw "Phase 30 clipboard wire-layout validation failed: $LASTEXITCODE" }
$baseNames = @{
    'success' = 'guideXOS.Phase30ManagedClipboardProof'
    'reader' = 'guideXOS.Phase30ClipboardReaderProof'
    'cross-writer' = 'guideXOS.Phase30ClipboardCrossWriterProof'
    'overwrite' = 'guideXOS.Phase30ClipboardOverwriteProof'
    'empty' = 'guideXOS.Phase30ClipboardEmptyProof'
    'clear' = 'guideXOS.Phase30ClipboardClearProof'
    'oversize' = 'guideXOS.Phase30ClipboardOversizeProof'
    'malformed-length' = 'guideXOS.Phase30ClipboardMalformedLengthProof'
    'failfast' = 'guideXOS.Phase30ClipboardFailFastProof'
}
New-Item -ItemType Directory -Force -Path $RamdiskSource | Out-Null

foreach ($payload in $record.payloads) {
    $artifact = [string]$payload.path
    $mode = [string]$payload.name
    $base = $baseNames[$mode]
    if ([string]::IsNullOrWhiteSpace($base)) { throw "Unknown Phase 30 payload mode: $mode" }
    $map = Join-Path (Split-Path -Parent (Split-Path -Parent $artifact)) 'guidexos.map'
    if (-not (Test-Path -LiteralPath $map)) { throw "Phase 30 map is absent: $map" }
    $exeTarget = Join-Path $RamdiskSource ($base + '.exe')
    $descriptorTarget = Join-Path $RamdiskSource ($base + '.gxmi')
    Copy-Item -LiteralPath $artifact -Destination $exeTarget -Force
    & $pythonCommand @($descriptorBuilder, $artifact, $map, $descriptorTarget, $mode)
    if ($LASTEXITCODE -ne 0) { throw "Phase 30 $mode descriptor failed: $LASTEXITCODE" }
    Get-FileHash -Algorithm SHA256 -LiteralPath $exeTarget
    Get-Item -LiteralPath $descriptorTarget | Select-Object FullName, Length
}
