[CmdletBinding()]
param([string]$RamdiskSource = '')

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
if ([string]::IsNullOrWhiteSpace($RamdiskSource)) { $RamdiskSource = Join-Path $root 'ramdisk_src\Native' }
$outputRoot = Join-Path $root 'out\phase26-bootstrap'
$output = Join-Path $outputRoot 'guideXOS.Phase26Bootstrap.bin'
$descriptor = Join-Path $outputRoot 'guideXOS.Phase26Bootstrap.gxbi'
New-Item -ItemType Directory -Force -Path $outputRoot | Out-Null
$nasm = $null
if (-not $nasm) { $candidate = Join-Path $root 'Tools\nasm.exe'; if (Test-Path -LiteralPath $candidate) { $nasm = $candidate } }
if (-not $nasm -and $env:NASM -and (Test-Path -LiteralPath $env:NASM)) { $nasm = $env:NASM }
if (-not $nasm) { $command = Get-Command nasm -ErrorAction SilentlyContinue; if ($command) { $nasm = $command.Source } }
if (-not $nasm) { throw 'NASM is required for the Phase 26 bootstrap.' }
$temporaryOutput = Join-Path $outputRoot ('.phase26-' + [Guid]::NewGuid().ToString('N') + '.tmp')
try {
    & $nasm -f bin (Join-Path $PSScriptRoot 'bootstrap.asm') -o $temporaryOutput
    if ($LASTEXITCODE -ne 0) { throw "Phase 26 bootstrap assembly failed: $LASTEXITCODE" }
    if (-not (Test-Path -LiteralPath $temporaryOutput -PathType Leaf)) {
        throw "NASM reported success but did not create its temporary output: $temporaryOutput"
    }
    Move-Item -LiteralPath $temporaryOutput -Destination $output -Force
} finally {
    if (Test-Path -LiteralPath $temporaryOutput) {
        Remove-Item -LiteralPath $temporaryOutput -Force -ErrorAction SilentlyContinue
    }
}
if (-not (Test-Path -LiteralPath $output -PathType Leaf)) {
    throw "Phase 26 bootstrap output was not created: $output"
}
$pythonCommand = Join-Path $env:USERPROFILE '.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe'
if (-not (Test-Path -LiteralPath $pythonCommand)) { $pythonCommand = 'python' }
& $pythonCommand (Join-Path $PSScriptRoot 'build_phase26_bootstrap_descriptor.py') $output $descriptor
if ($LASTEXITCODE -ne 0) { throw "Phase 26 bootstrap descriptor failed: $LASTEXITCODE" }
New-Item -ItemType Directory -Force -Path $RamdiskSource | Out-Null
Copy-Item -LiteralPath $output -Destination (Join-Path $RamdiskSource 'guideXOS.Phase26Bootstrap.bin') -Force
Copy-Item -LiteralPath $descriptor -Destination (Join-Path $RamdiskSource 'guideXOS.Phase26Bootstrap.gxbi') -Force
Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $RamdiskSource 'guideXOS.Phase26Bootstrap.bin')
