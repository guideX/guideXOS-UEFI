[CmdletBinding()]
param([string]$OutputRoot = '', [string]$RamdiskSource = '')

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path $root 'out\dotnet\phase27-bootstrap'
}
if ([string]::IsNullOrWhiteSpace($RamdiskSource)) {
    $RamdiskSource = Join-Path $root 'ramdisk_src\Native'
}
$nasm = $null
if ($env:NASM -and (Test-Path -LiteralPath $env:NASM)) { $nasm = $env:NASM }
if (-not $nasm) {
    $candidate = Join-Path $root 'Tools\nasm.exe'
    if (Test-Path -LiteralPath $candidate) { $nasm = $candidate }
}
if (-not $nasm) {
    $command = Get-Command nasm -ErrorAction SilentlyContinue
    if ($command) { $nasm = $command.Source }
}
if (-not $nasm) { throw 'NASM is required for the Phase 27 bootstrap.' }

$output = Join-Path $OutputRoot 'guideXOS.Phase27Bootstrap.bin'
$descriptor = Join-Path $OutputRoot 'guideXOS.Phase27Bootstrap.gxbi'
New-Item -ItemType Directory -Force -Path $OutputRoot | Out-Null
& $nasm -f bin (Join-Path $PSScriptRoot 'bootstrap.asm') -o $output
if ($LASTEXITCODE -ne 0) { throw "Phase 27 bootstrap assembly failed: $LASTEXITCODE" }
& python (Join-Path $PSScriptRoot 'build_phase27_bootstrap_descriptor.py') $output $descriptor
if ($LASTEXITCODE -ne 0) { throw "Phase 27 bootstrap descriptor failed: $LASTEXITCODE" }
New-Item -ItemType Directory -Force -Path $RamdiskSource | Out-Null
Copy-Item -LiteralPath $output -Destination (Join-Path $RamdiskSource 'guideXOS.Phase27Bootstrap.bin') -Force
Copy-Item -LiteralPath $descriptor -Destination (Join-Path $RamdiskSource 'guideXOS.Phase27Bootstrap.gxbi') -Force
Get-FileHash -Algorithm SHA256 -LiteralPath $output
