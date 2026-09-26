[CmdletBinding()]
param([string]$RamdiskSource = '')

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
if ([string]::IsNullOrWhiteSpace($RamdiskSource)) { $RamdiskSource = Join-Path $root 'ramdisk_src\Native' }
$output = Join-Path $root 'out\dotnet\phase26-bootstrap\guideXOS.Phase26Bootstrap.bin'
$descriptor = Join-Path $root 'out\dotnet\phase26-bootstrap\guideXOS.Phase26Bootstrap.gxbi'
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $output) | Out-Null
$nasm = $null
if ($env:NASM -and (Test-Path -LiteralPath $env:NASM)) { $nasm = $env:NASM }
if (-not $nasm) { $candidate = Join-Path $root 'Tools\nasm.exe'; if (Test-Path -LiteralPath $candidate) { $nasm = $candidate } }
if (-not $nasm) { $command = Get-Command nasm -ErrorAction SilentlyContinue; if ($command) { $nasm = $command.Source } }
if (-not $nasm) { throw 'NASM is required for the Phase 26 bootstrap.' }
& $nasm -f bin (Join-Path $PSScriptRoot 'bootstrap.asm') -o $output
if ($LASTEXITCODE -ne 0) { throw "Phase 26 bootstrap assembly failed: $LASTEXITCODE" }
& python (Join-Path $PSScriptRoot 'build_phase26_bootstrap_descriptor.py') $output $descriptor
if ($LASTEXITCODE -ne 0) { throw "Phase 26 bootstrap descriptor failed: $LASTEXITCODE" }
New-Item -ItemType Directory -Force -Path $RamdiskSource | Out-Null
Copy-Item -LiteralPath $output -Destination (Join-Path $RamdiskSource 'guideXOS.Phase26Bootstrap.bin') -Force
Copy-Item -LiteralPath $descriptor -Destination (Join-Path $RamdiskSource 'guideXOS.Phase26Bootstrap.gxbi') -Force
Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $RamdiskSource 'guideXOS.Phase26Bootstrap.bin')
