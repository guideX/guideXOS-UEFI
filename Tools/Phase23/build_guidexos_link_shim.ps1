[CmdletBinding()]
param(
    [string]$OutputRoot = ''
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
if ([string]::IsNullOrWhiteSpace($OutputRoot)) { $OutputRoot = Join-Path $root 'out\dotnet\phase23-runtime-pack' }
$source = Join-Path $PSScriptRoot 'guidexos_link_shim.cpp'
$object = Join-Path $OutputRoot 'guidexos_link_shim.obj'
$vsRoot = 'C:\Program Files\Microsoft Visual Studio\18\Community\VC\Tools\MSVC'
$compiler = Get-ChildItem -LiteralPath $vsRoot -Filter cl.exe -File -Recurse | Where-Object { $_.FullName -match '\\Hostx64\\x64\\cl\.exe$' } | Sort-Object FullName -Descending | Select-Object -First 1
if ($null -eq $compiler) { throw 'The VS 18 x64 C++ compiler was not found.' }
if (-not (Test-Path -LiteralPath $source)) { throw "The GUIDEXOS link shim source is absent: $source" }
New-Item -ItemType Directory -Force -Path $OutputRoot | Out-Null

$args = @(
    '/nologo', '/c', '/O1', '/GS-', '/GR-', '/EHs-c-', '/Zl', '/D_CRT_SECURE_NO_WARNINGS',
    "/Fo$object", $source
)
& $compiler.FullName @args 2>&1
if ($LASTEXITCODE -ne 0) { throw "GUIDEXOS link shim compilation failed with exit code $LASTEXITCODE." }
if (-not (Test-Path -LiteralPath $object -PathType Leaf)) { throw "The GUIDEXOS link shim object was not produced: $object" }

$record = [ordered]@{
    schema = 1
    generatedBy = 'Tools/Phase23/build_guidexos_link_shim.ps1'
    source = $source
    compiler = $compiler.FullName
    object = $object
    sha256 = (Get-FileHash -LiteralPath $object -Algorithm SHA256).Hash.ToUpperInvariant()
    imports = @()
    role = 'single target-side veneer from legacy Redhawk/compiler symbol names to the Phase 19 GUIDEXOS PAL contract'
}
$record | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $OutputRoot 'guidexos-link-shim.json') -Encoding UTF8
$record | ConvertTo-Json -Depth 8
