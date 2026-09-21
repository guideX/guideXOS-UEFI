[CmdletBinding()]
param(
    [string]$SourceRoot = '',
    [string]$OutputRoot = ''
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
if ([string]::IsNullOrWhiteSpace($SourceRoot)) {
    $SourceRoot = Join-Path $root 'out\rt'
}
if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path $root 'out\dotnet\phase20-source-build'
}

$lock = Get-Content (Join-Path $PSScriptRoot 'runtime-source.lock.json') -Raw | ConvertFrom-Json
if (-not (Test-Path -LiteralPath (Join-Path $SourceRoot 'build.cmd') -PathType Leaf)) {
    throw "Pinned runtime source is absent: $SourceRoot. No installed SDK or win-x64 runtime may substitute for it."
}

$actualCommit = (& git -C $SourceRoot rev-parse HEAD).Trim()
if ($actualCommit -ne [string]$lock.source.commit) {
    throw "Pinned runtime source mismatch. Expected $($lock.source.commit) but found $actualCommit."
}

$vcvars = 'C:\Program Files\Microsoft Visual Studio\18\Community\VC\Auxiliary\Build\vcvars64.bat'
if (-not (Test-Path -LiteralPath $vcvars -PathType Leaf)) {
    throw "The required native x64 compiler environment was not found: $vcvars"
}

$buildCommand = 'call "C:\Program Files\Microsoft Visual Studio\18\Community\VC\Auxiliary\Build\vcvars64.bat" >nul && set "VisualStudioVersion=17.0" && set "SkipVCEnvInit=1" && build.cmd -subset Clr.NativeAOTRuntime -arch x64 -c Release -rc Release -lc Release -os windows -ninja /p:IlcUseEnvironmentalTools=true'
Push-Location $SourceRoot
try {
    & cmd.exe /d /s /c $buildCommand
    if ($LASTEXITCODE -ne 0) {
        throw "Pinned NativeAOT source build failed with exit code $LASTEXITCODE."
    }
}
finally {
    Pop-Location
}

New-Item -ItemType Directory -Force -Path $OutputRoot | Out-Null
$record = [ordered]@{
    schema = 1
    generatedBy = 'Tools/Phase20/build_pinned_runtime_source.ps1'
    generatedAtUtc = [DateTime]::UtcNow.ToString('o')
    target = $lock.target
    source = [ordered]@{
        checkout = (Resolve-Path $SourceRoot).Path
        commit = $actualCommit
        canonicalRepository = $lock.source.repository
    }
    command = $buildCommand
    subset = $lock.toolchain.runtimeBuildSubset
    configuration = $lock.toolchain.runtimeBuildConfiguration
    architecture = $lock.toolchain.runtimeBuildArchitecture
    os = $lock.toolchain.runtimeBuildOS
    output = Join-Path $SourceRoot 'artifacts\bin\coreclr\windows.x64.Release\aotsdk'
    customTarget = $false
    customRuntimePack = $false
    note = 'This is the pinned stock Windows NativeAOT source baseline. It is not a guidexos-x64 runtime pack and must not be consumed as one.'
}
$recordPath = Join-Path $OutputRoot 'source-build-record.json'
$record | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $recordPath -Encoding UTF8
Write-Output ($record | ConvertTo-Json -Depth 5)
Write-Output "Wrote $recordPath"
