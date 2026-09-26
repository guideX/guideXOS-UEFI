[CmdletBinding()]
param(
    [string]$Artifact = '',
    [string]$Map = '',
    [string]$RamdiskSource = ''
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
if ([string]::IsNullOrWhiteSpace($Artifact)) { $Artifact = Join-Path $root 'out\dotnet\phase26-user-managed-proof\publish\guideXOS.UserManagedProof.exe' }
if ([string]::IsNullOrWhiteSpace($Map)) { $Map = Join-Path $root 'out\dotnet\phase26-user-managed-proof\guidexos.map' }
if ([string]::IsNullOrWhiteSpace($RamdiskSource)) { $RamdiskSource = Join-Path $root 'ramdisk_src\Native' }

$artifactPath = (Resolve-Path -LiteralPath $Artifact).Path
$mapPath = (Resolve-Path -LiteralPath $Map).Path
New-Item -ItemType Directory -Force -Path $RamdiskSource | Out-Null
$exeTarget = Join-Path $RamdiskSource 'guideXOS.Phase26ManagedProof.exe'
$descriptorTarget = Join-Path $RamdiskSource 'guideXOS.Phase26ManagedProof.gxmi'
Copy-Item -LiteralPath $artifactPath -Destination $exeTarget -Force
$pythonCommand = Join-Path $env:USERPROFILE '.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe'
if (-not (Test-Path -LiteralPath $pythonCommand)) { $pythonCommand = 'python' }
& $pythonCommand (Join-Path $PSScriptRoot 'build_phase26_descriptor.py') $artifactPath $mapPath $descriptorTarget
if ($LASTEXITCODE -ne 0) { throw "Phase 26 descriptor generation failed: $LASTEXITCODE" }
Get-FileHash -Algorithm SHA256 -LiteralPath $exeTarget
Get-Item -LiteralPath $descriptorTarget | Select-Object FullName,Length
