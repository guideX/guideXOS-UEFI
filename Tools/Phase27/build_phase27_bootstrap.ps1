[CmdletBinding()]
param([string]$OutputRoot = '', [string]$RamdiskSource = '')

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path $root 'out\phase27-bootstrap'
}
if ([string]::IsNullOrWhiteSpace($RamdiskSource)) {
    $RamdiskSource = Join-Path $root 'ramdisk_src\Native'
}
$nasm = $null
if (-not $nasm) {
    $candidate = Join-Path $root 'Tools\nasm.exe'
    if (Test-Path -LiteralPath $candidate) { $nasm = $candidate }
}
if (-not $nasm -and $env:NASM -and (Test-Path -LiteralPath $env:NASM)) { $nasm = $env:NASM }
if (-not $nasm) {
    $command = Get-Command nasm -ErrorAction SilentlyContinue
    if ($command) { $nasm = $command.Source }
}
if (-not $nasm) { throw 'NASM is required for the Phase 27 bootstrap.' }

$OutputRoot = [System.IO.Path]::GetFullPath($OutputRoot)
New-Item -ItemType Directory -Force -Path $OutputRoot | Out-Null
$output = Join-Path $OutputRoot 'guideXOS.Phase27Bootstrap.bin'
$descriptor = Join-Path $OutputRoot 'guideXOS.Phase27Bootstrap.gxbi'
$temporaryOutput = Join-Path $OutputRoot ('.phase27-' + [Guid]::NewGuid().ToString('N') + '.tmp')
try {
    & $nasm -f bin (Join-Path $PSScriptRoot 'bootstrap.asm') -o $temporaryOutput
    if ($LASTEXITCODE -ne 0) { throw "Phase 27 bootstrap assembly failed: $LASTEXITCODE" }
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
    throw "Phase 27 bootstrap output was not created: $output"
}
$pythonCommand = Join-Path $env:USERPROFILE '.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe'
if (-not (Test-Path -LiteralPath $pythonCommand)) { $pythonCommand = 'python' }
& $pythonCommand (Join-Path $PSScriptRoot 'build_phase27_bootstrap_descriptor.py') $output $descriptor
if ($LASTEXITCODE -ne 0) { throw "Phase 27 bootstrap descriptor failed: $LASTEXITCODE" }
New-Item -ItemType Directory -Force -Path $RamdiskSource | Out-Null
Copy-Item -LiteralPath $output -Destination (Join-Path $RamdiskSource 'guideXOS.Phase27Bootstrap.bin') -Force
Copy-Item -LiteralPath $descriptor -Destination (Join-Path $RamdiskSource 'guideXOS.Phase27Bootstrap.gxbi') -Force
Get-FileHash -Algorithm SHA256 -LiteralPath $output
