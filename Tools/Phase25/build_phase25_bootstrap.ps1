[CmdletBinding()]
param(
    [string]$Source = '',
    [string]$Output = '',
    [string]$Descriptor = ''
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
if ([string]::IsNullOrEmpty($Source)) { $Source = Join-Path $PSScriptRoot 'bootstrap.asm' }
if ([string]::IsNullOrEmpty($Output)) { $Output = Join-Path $root 'out\phase25-bootstrap\guideXOS.Phase25Bootstrap.bin' }
if ([string]::IsNullOrEmpty($Descriptor)) { $Descriptor = Join-Path $root 'out\phase25-bootstrap\guideXOS.Phase25Bootstrap.gxbi' }

$nasm = $null
$candidate = Join-Path $root 'Tools\nasm.exe'
if (Test-Path -LiteralPath $candidate) { $nasm = $candidate }
if (-not $nasm -and $env:NASM -and (Test-Path -LiteralPath $env:NASM)) {
    $nasm = $env:NASM
}
if (-not $nasm) {
    $command = Get-Command nasm -ErrorAction SilentlyContinue
    if ($command) { $nasm = $command.Source }
}
if (-not $nasm) { throw 'NASM is required to build the Phase 25 bootstrap' }

if (-not [System.IO.Path]::IsPathRooted($Source)) { $Source = Join-Path $root $Source }
if (-not [System.IO.Path]::IsPathRooted($Output)) { $Output = Join-Path $root $Output }
if (-not [System.IO.Path]::IsPathRooted($Descriptor)) { $Descriptor = Join-Path $root $Descriptor }
$Source = [System.IO.Path]::GetFullPath($Source)
$Output = [System.IO.Path]::GetFullPath($Output)
$Descriptor = [System.IO.Path]::GetFullPath($Descriptor)

$outputDirectory = Split-Path -Parent $Output
$descriptorDirectory = Split-Path -Parent $Descriptor
New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null
New-Item -ItemType Directory -Force -Path $descriptorDirectory | Out-Null
if (-not (Test-Path -LiteralPath $outputDirectory -PathType Container)) {
    throw "Phase 25 output directory was not created: $outputDirectory"
}
Write-Host "Assembling Phase 25 bootstrap with $nasm"
Write-Host "Phase 25 output: $Output"
$temporaryOutput = Join-Path $outputDirectory ('.phase25-' + [Guid]::NewGuid().ToString('N') + '.tmp')
try {
    # Assemble to a fresh sibling file, then install it. This avoids NASM
    # needing to truncate a stale or temporarily scanned output file.
    & $nasm -f bin $Source -o $temporaryOutput
    if ($LASTEXITCODE -ne 0) { throw "NASM failed: $LASTEXITCODE (output: $temporaryOutput)" }
    if (-not (Test-Path -LiteralPath $temporaryOutput -PathType Leaf)) {
        throw "NASM reported success but did not create its temporary output: $temporaryOutput"
    }
    Move-Item -LiteralPath $temporaryOutput -Destination $Output -Force
    if (-not (Test-Path -LiteralPath $Output -PathType Leaf)) {
        throw "Could not install the Phase 25 output at: $Output"
    }
} finally {
    if (Test-Path -LiteralPath $temporaryOutput) {
        Remove-Item -LiteralPath $temporaryOutput -Force -ErrorAction SilentlyContinue
    }
}
$pythonCommand = Join-Path $env:USERPROFILE '.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe'
if (-not (Test-Path -LiteralPath $pythonCommand)) { $pythonCommand = 'python' }
& $pythonCommand (Join-Path $PSScriptRoot 'build_phase25_descriptor.py') $Output $Descriptor
if ($LASTEXITCODE -ne 0) { throw "GXBI descriptor generation failed: $LASTEXITCODE" }
Get-FileHash -Algorithm SHA256 -LiteralPath $Output
Get-Item -LiteralPath $Output, $Descriptor | Select-Object FullName, Length
