[CmdletBinding()]
param(
    [switch]$NoRestore
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root 'UserManagedProof\guideXOS.UserManagedProof.csproj'
$output = Join-Path $root 'bin\UserManagedProof'
$publishArgs = @('publish', $project, '-c', 'Release', '-o', $output)
if ($NoRestore) { $publishArgs += '--no-restore' }

Push-Location (Join-Path $root 'UserManagedProof')
try {
    & dotnet @publishArgs
    if ($LASTEXITCODE -ne 0) { throw "NativeAOT user payload publish failed with exit code $LASTEXITCODE" }
} finally {
    Pop-Location
}

$image = Join-Path $output 'guideXOS.UserManagedProof.exe'
if (-not (Test-Path -LiteralPath $image)) { throw "NativeAOT artifact not found: $image" }
$map = Join-Path $output 'guideXOS.UserManagedProof.map.xml'
if (-not (Test-Path -LiteralPath $map)) {
    $map = Join-Path $root 'UserManagedProof\obj\Release\net9.0\win-x64\native\guideXOS.UserManagedProof.map.xml'
}
$manifest = Join-Path $output 'managed-image-manifest.json'
$inspectArgs = @((Join-Path $root 'Tools\inspect_managed_image.py'), $image, '--output', $manifest)
if (Test-Path -LiteralPath $map) { $inspectArgs += @('--map', $map) }
& python @inspectArgs
if ($LASTEXITCODE -ne 0) { throw "Managed image inspection failed with exit code $LASTEXITCODE" }

$probe = Join-Path $output 'managed-loader-probe.json'
& python (Join-Path $root 'Tools\phase17_loader_probe.py') $image $manifest --output $probe
if ($LASTEXITCODE -ne 0) { throw "Managed image loader probe failed with exit code $LASTEXITCODE" }

Get-Item -LiteralPath $image, $manifest, $probe | Select-Object FullName, Length, LastWriteTime
