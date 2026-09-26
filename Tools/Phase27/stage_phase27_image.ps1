[CmdletBinding()]
param([string]$BuildRoot = '', [string]$RamdiskSource = '')

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
if ([string]::IsNullOrWhiteSpace($BuildRoot)) {
    $BuildRoot = Join-Path $root 'out\dotnet\phase27-managed-service-proof'
}
if ([string]::IsNullOrWhiteSpace($RamdiskSource)) {
    $RamdiskSource = Join-Path $root 'ramdisk_src\Native'
}

$record = Get-Content -Raw (Join-Path $BuildRoot 'phase27-managed-service-proof-build.json') | ConvertFrom-Json
$stage = @(
    @{ kind = 'success'; item = $record.success; base = 'guideXOS.Phase27ManagedServiceProof'; failure = $false },
    @{ kind = 'failure'; item = $record.failure; base = 'guideXOS.Phase27ManagedFailureProof'; failure = $true }
)
$descriptorBuilder = Join-Path $PSScriptRoot 'build_phase27_descriptor.py'
New-Item -ItemType Directory -Force -Path $RamdiskSource | Out-Null

foreach ($entry in $stage) {
    $artifact = [string]$entry.item.path
    $map = Join-Path (Split-Path -Parent (Split-Path -Parent $artifact)) 'guidexos.map'
    if (-not (Test-Path -LiteralPath $map)) {
        throw "Phase 27 $($entry.kind) link map is absent: $map"
    }
    $exeTarget = Join-Path $RamdiskSource ($entry.base + '.exe')
    $descriptorTarget = Join-Path $RamdiskSource ($entry.base + '.gxmi')
    Copy-Item -LiteralPath $artifact -Destination $exeTarget -Force
    $descriptorArgs = @($descriptorBuilder, $artifact, $map, $descriptorTarget)
    if ($entry.failure) { $descriptorArgs += '--failure' }
    $pythonCommand = Join-Path $env:USERPROFILE '.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe'
    if (-not (Test-Path -LiteralPath $pythonCommand)) { $pythonCommand = 'python' }
    & $pythonCommand @descriptorArgs
    if ($LASTEXITCODE -ne 0) { throw "Phase 27 $($entry.kind) descriptor generation failed: $LASTEXITCODE" }
    Get-FileHash -Algorithm SHA256 -LiteralPath $exeTarget
    Get-Item -LiteralPath $descriptorTarget | Select-Object FullName, Length
}
