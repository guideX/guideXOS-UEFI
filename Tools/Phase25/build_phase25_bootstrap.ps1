[CmdletBinding()]
param(
    [string]$Source = '',
    [string]$Output = '',
    [string]$Descriptor = ''
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
if ([string]::IsNullOrEmpty($Source)) { $Source = Join-Path $PSScriptRoot 'bootstrap.asm' }
if ([string]::IsNullOrEmpty($Output)) { $Output = Join-Path $root 'out\dotnet\phase25-bootstrap\guideXOS.Phase25Bootstrap.bin' }
if ([string]::IsNullOrEmpty($Descriptor)) { $Descriptor = Join-Path $root 'out\dotnet\phase25-bootstrap\guideXOS.Phase25Bootstrap.gxbi' }

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
if (-not $nasm) { throw 'NASM is required to build the Phase 25 bootstrap' }

New-Item -ItemType Directory -Force -Path (Split-Path -Parent $Output) | Out-Null
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $Descriptor) | Out-Null
& $nasm -f bin $Source -o $Output
if ($LASTEXITCODE -ne 0) { throw "NASM failed: $LASTEXITCODE" }
$pythonCommand = Join-Path $env:USERPROFILE '.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe'
if (-not (Test-Path -LiteralPath $pythonCommand)) { $pythonCommand = 'python' }
& $pythonCommand (Join-Path $PSScriptRoot 'build_phase25_descriptor.py') $Output $Descriptor
if ($LASTEXITCODE -ne 0) { throw "GXBI descriptor generation failed: $LASTEXITCODE" }
Get-FileHash -Algorithm SHA256 -LiteralPath $Output
Get-Item -LiteralPath $Output, $Descriptor | Select-Object FullName, Length
