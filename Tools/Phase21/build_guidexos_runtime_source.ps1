[CmdletBinding()]
param(
    [string]$SourceRoot = '',
    [string]$OutputRoot = ''
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
if ([string]::IsNullOrWhiteSpace($SourceRoot)) { $SourceRoot = Join-Path $root 'out\rt' }
if ([string]::IsNullOrWhiteSpace($OutputRoot)) { $OutputRoot = Join-Path $root 'out\dotnet\phase21-source-build' }
$lock = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'runtime-source.lock.json') -Raw | ConvertFrom-Json
if (-not (Test-Path -LiteralPath (Join-Path $SourceRoot 'src\coreclr\build-runtime.cmd') -PathType Leaf)) { throw "Pinned runtime source is absent: $SourceRoot" }
$actualCommit = (& git -C $SourceRoot rev-parse HEAD).Trim()
if ($actualCommit -ne [string]$lock.runtimeCommit) { throw "Runtime source mismatch. Expected $($lock.runtimeCommit), found $actualCommit." }
$patchRecordPath = Join-Path $root 'out\dotnet\phase21-source-patch\phase21-source-patch-record.json'
if (-not (Test-Path -LiteralPath $patchRecordPath -PathType Leaf)) { throw "Phase 21 patch record is absent: $patchRecordPath" }

$vcvars = 'C:\Program Files\Microsoft Visual Studio\18\Community\VC\Auxiliary\Build\vcvars64.bat'
if (-not (Test-Path -LiteralPath $vcvars -PathType Leaf)) { throw "The required native x64 compiler environment was not found: $vcvars" }
$buildCommand = 'call "' + $vcvars + '" >nul && set "VisualStudioVersion=17.0" && set "SkipVCEnvInit=1" && build-runtime.cmd -x64 -Release -component nativeaot -os guidexos -outputrid guidexos-x64'
$exitCode = 0
Push-Location (Join-Path $SourceRoot 'src\coreclr')
try {
    & cmd.exe /d /s /c $buildCommand
    $exitCode = $LASTEXITCODE
}
finally { Pop-Location }

$status = if ($exitCode -eq 0) { 'completed' } else { 'failed-before-guidexos-runtime-compile' }
$record = [ordered]@{
    schema = 1
    generatedBy = 'Tools/Phase21/build_guidexos_runtime_source.ps1'
    generatedAtUtc = [DateTime]::UtcNow.ToString('o')
    target = 'guidexos-x64'
    source = [ordered]@{
        checkout = (Resolve-Path $SourceRoot).Path
        commit = $actualCommit
        patchRecord = (Resolve-Path $patchRecordPath).Path
    }
    host = [ordered]@{
        os = 'windows'
        compiler = 'MSVC x64'
        vcvars = $vcvars
    }
    command = $buildCommand
    exitCode = $exitCode
    status = $status
    note = 'This invokes the pinned native source build for configuration evidence only. It never renames or accepts a stock win-x64 artifact as guidexos-x64.'
    expectedCurrentBlocker = 'The pinned host/apphost CMake path still requests System.Net.Security.Native and libgssapi_krb5 before the NativeAOT runtime target compiles.'
}
New-Item -ItemType Directory -Force -Path $OutputRoot | Out-Null
$recordPath = Join-Path $OutputRoot 'source-build-record.json'
$record | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $recordPath -Encoding UTF8
$record | ConvertTo-Json -Depth 5
Write-Output "Wrote $recordPath"
if ($exitCode -ne 0) { exit $exitCode }
