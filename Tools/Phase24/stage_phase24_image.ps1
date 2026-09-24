[CmdletBinding()]
param(
    [string]$Artifact = '',
    [string]$Map = '',
    [string]$RamdiskSource = ''
)

$ErrorActionPreference = 'Stop'
$scriptRoot = $PSScriptRoot
if ([string]::IsNullOrEmpty($scriptRoot)) {
    $scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Definition
}
if ([string]::IsNullOrEmpty($Artifact)) {
    $Artifact = Join-Path $scriptRoot '..\..\out\dotnet\phase23-user-managed-proof\publish\guideXOS.UserManagedProof.exe'
}
if ([string]::IsNullOrEmpty($Map)) {
    $Map = Join-Path $scriptRoot '..\..\out\dotnet\phase23-user-managed-proof\guidexos.map'
}
if ([string]::IsNullOrEmpty($RamdiskSource)) {
    $RamdiskSource = Join-Path $scriptRoot '..\..\ramdisk_src\Native'
}
$expectedHash = 'C8D60ABE6D91917F4E236F435A8C2D4272386CEC830C07AFA691897A23DA195A'
$artifactPath = (Resolve-Path -LiteralPath $Artifact).Path
$mapPath = (Resolve-Path -LiteralPath $Map).Path
$hash = (Get-FileHash -Algorithm SHA256 -LiteralPath $artifactPath).Hash
if ($hash -ne $expectedHash) {
    throw "Phase 24 refuses a non-Phase-23 artifact: $hash"
}

New-Item -ItemType Directory -Force -Path $RamdiskSource | Out-Null
$exeTarget = Join-Path $RamdiskSource 'guideXOS.UserManagedProof.exe'
$descriptorTarget = Join-Path $RamdiskSource 'guideXOS.UserManagedProof.gxmi'
Copy-Item -LiteralPath $artifactPath -Destination $exeTarget -Force
& python (Join-Path $PSScriptRoot 'build_phase24_descriptor.py') $artifactPath $mapPath $descriptorTarget
if ($LASTEXITCODE -ne 0) { throw "Phase 24 descriptor generation failed: $LASTEXITCODE" }

Get-FileHash -Algorithm SHA256 -LiteralPath $exeTarget
Get-Item -LiteralPath $descriptorTarget | Select-Object FullName,Length
