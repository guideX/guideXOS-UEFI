[CmdletBinding()]
param(
    [string]$RamdiskSource = '',
    [string]$Output = '',
    [string]$Descriptor = ''
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
if ([string]::IsNullOrEmpty($RamdiskSource)) { $RamdiskSource = Join-Path $root 'ramdisk_src\Native' }
if ([string]::IsNullOrEmpty($Output)) { $Output = Join-Path $root 'out\dotnet\phase25-bootstrap\guideXOS.Phase25Bootstrap.bin' }
if ([string]::IsNullOrEmpty($Descriptor)) { $Descriptor = Join-Path $root 'out\dotnet\phase25-bootstrap\guideXOS.Phase25Bootstrap.gxbi' }

& (Join-Path $PSScriptRoot 'build_phase25_bootstrap.ps1') -Output $Output -Descriptor $Descriptor
if ($LASTEXITCODE -ne 0) { throw "Phase 25 bootstrap build failed: $LASTEXITCODE" }
New-Item -ItemType Directory -Force -Path $RamdiskSource | Out-Null
Copy-Item -LiteralPath $Output -Destination (Join-Path $RamdiskSource 'guideXOS.Phase25Bootstrap.bin') -Force
Copy-Item -LiteralPath $Descriptor -Destination (Join-Path $RamdiskSource 'guideXOS.Phase25Bootstrap.gxbi') -Force
Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $RamdiskSource 'guideXOS.Phase25Bootstrap.bin')
