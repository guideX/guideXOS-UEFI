[CmdletBinding()]
param(
    [string]$Phase23Pack = '',
    [string]$OutputRoot = '',
    [switch]$Phase35R2MatrixOnly,
    [string]$PreservedBuildRoot = ''
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
if ([string]::IsNullOrWhiteSpace($Phase23Pack)) {
    $Phase23Pack = Join-Path $root 'out\dotnet\phase23-runtime-pack'
}
$expectedOutputRoot = [IO.Path]::GetFullPath(
    (Join-Path $root 'out\dotnet\phase35-managed-persistent-read'))
if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = $expectedOutputRoot
}
$OutputRoot = [IO.Path]::GetFullPath($OutputRoot)
if (-not [string]::Equals($OutputRoot, $expectedOutputRoot,
        [StringComparison]::OrdinalIgnoreCase)) {
    throw "Phase 35 build output must stay in $expectedOutputRoot"
}

$phase26Builder = Join-Path $root 'Tools\Phase26\build_phase26_managed_proof.ps1'
$palSource = Join-Path $PSScriptRoot 'guidexos_phase35_pal_contract.cpp'
$project = Join-Path $root 'UserManagedPersistentStorageProof\guideXOS.UserManagedPersistentStorageProof.csproj'
$phase31Shim = Join-Path $root 'Tools\Phase31\_guidexos_phase31_link_shim.cpp'
$shimRoot = Join-Path $root 'out\phase35r-tools'
$shimSource = Join-Path $shimRoot '_guidexos_phase35_link_shim.cpp'
foreach ($path in @($phase26Builder, $palSource, $project, $phase31Shim,
        $Phase23Pack)) {
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Phase 35 build input is absent: $path"
    }
}

# Derive a Phase 35-only NativeAOT resolver shim. Older managed cohorts keep
# their original import surface; the new helper exists only in Phase 35.
$shimText = Get-Content -LiteralPath $phase31Shim -Raw
$phase19Header = Join-Path $root 'Tools\Phase19\guidexos_nativeaot_pal_contract.h'
if (-not (Test-Path -LiteralPath $phase19Header)) {
    throw "Phase 19 PAL contract header is absent: $phase19Header"
}
$phase19Include = $phase19Header.Replace('\', '/')
$shimText = $shimText.Replace(
    '#include "..\Phase19\guidexos_nativeaot_pal_contract.h"',
    '#include "' + $phase19Include + '"')
$declarationNeedle = 'extern "C" unsigned long long guidexos_pal_service_request('
$declaration = @'
extern "C" unsigned long long guidexos_pal_persistent_storage_read(
    unsigned long long request, unsigned long long requestLength);
'@
if (-not $shimText.Contains('guidexos_pal_persistent_storage_read')) {
    if (-not $shimText.Contains($declarationNeedle)) {
        throw 'Phase 31 PAL resolver declaration seam is absent.'
    }
    $declarationEnd = '    unsigned long long request, unsigned long long requestLength);'
    $shimText = $shimText.Replace($declarationEnd,
        $declarationEnd + "`r`n" + $declaration.TrimEnd())
    $resolverNeedle = @'
    if (shim_name_is(functionName, "guidexos_pal_service_request"))
        return (void*)&guidexos_pal_service_request;
'@
    $resolverAddition = @'
    if (shim_name_is(functionName, "guidexos_pal_persistent_storage_read"))
        return (void*)&guidexos_pal_persistent_storage_read;
'@
    if (-not $shimText.Contains($resolverNeedle)) {
        throw 'Phase 31 PAL resolver function-name seam is absent.'
    }
    $shimText = $shimText.Replace($resolverNeedle,
        $resolverNeedle + $resolverAddition)
}
New-Item -ItemType Directory -Force -Path $shimRoot | Out-Null
[IO.File]::WriteAllText($shimSource, $shimText,
    [Text.UTF8Encoding]::new($false))

if (Test-Path -LiteralPath $OutputRoot) {
    $resolvedOutput = (Resolve-Path -LiteralPath $OutputRoot).Path
    if (-not [string]::Equals($resolvedOutput, $expectedOutputRoot,
            [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to remove unexpected Phase 35 output path: $resolvedOutput"
    }
    Remove-Item -LiteralPath $resolvedOutput -Recurse -Force
}
 $preservedPayloads = @()
if ($Phase35R2MatrixOnly) {
    if ([string]::IsNullOrWhiteSpace($PreservedBuildRoot)) {
        throw 'Phase35R2MatrixOnly requires PreservedBuildRoot.'
    }
    $PreservedBuildRoot = (Resolve-Path -LiteralPath $PreservedBuildRoot).Path
    if (-not (Test-Path -LiteralPath (Join-Path $PreservedBuildRoot `
            'phase35-managed-persistent-read-build.json'))) {
        throw "Preserved Phase 35 build record is absent: $PreservedBuildRoot"
    }
    $preservedRecord = Get-Content -Raw (Join-Path $PreservedBuildRoot `
        'phase35-managed-persistent-read-build.json') | ConvertFrom-Json
    $preservedPayloads = @($preservedRecord.payloads)
    Copy-Item -LiteralPath $PreservedBuildRoot -Destination $OutputRoot `
        -Recurse
}
New-Item -ItemType Directory -Force -Path $OutputRoot | Out-Null

$allModes = @(
    [ordered]@{ Name = 'success'; Mode = 'Success'; Result = 35 },
    [ordered]@{ Name = 'failfast'; Mode = 'FailFast'; Result = -1 },
    [ordered]@{ Name = 'stale-owner'; Mode = 'StaleOwner'; Result = 35 },
    [ordered]@{ Name = 'cross-scope'; Mode = 'CrossScope'; Result = 35 },
    [ordered]@{ Name = 'malformed'; Mode = 'Malformed'; Result = 35 },
    [ordered]@{ Name = 'no-read'; Mode = 'NoRead'; Result = 35 },
    [ordered]@{ Name = 'one-read'; Mode = 'OneRead'; Result = 35 },
    [ordered]@{ Name = 'two-read'; Mode = 'TwoRead'; Result = 35 }
)
$modes = if ($Phase35R2MatrixOnly) {
    @($allModes | Where-Object { $_.Name -in @('no-read', 'one-read', 'two-read') })
} else { $allModes }

. (Join-Path $PSScriptRoot '..\ManagedArtifacts\Normalize-PeTimestamps.ps1')
$records = @()
foreach ($mode in $modes) {
    $pack = Join-Path $OutputRoot ($mode.Name + '-runtime-pack')
    $output = Join-Path $OutputRoot $mode.Name
    & (Join-Path ([Environment]::GetFolderPath('Windows')) `
        'System32\WindowsPowerShell\v1.0\powershell.exe') `
        -NoProfile -ExecutionPolicy Bypass -File $phase26Builder `
        -Phase23Pack $Phase23Pack -PackRoot $pack -OutputRoot $output `
        -ProjectPath $project -PalSource $palSource -ShimSource $shimSource `
        -ProjectProperties @('Phase35Mode=' + $mode.Mode)
    if ($LASTEXITCODE -ne 0) {
        throw "Phase 35 $($mode.Name) payload build failed: $LASTEXITCODE"
    }
    $artifact = Get-ChildItem -LiteralPath (Join-Path $output 'publish') `
        -Filter '*.exe' -File | Select-Object -First 1
    if ($null -eq $artifact) {
        throw "Phase 35 $($mode.Name) payload is absent."
    }
    Normalize-PeTimestamps $artifact.FullName
    $payloadRoot = Join-Path (Join-Path $OutputRoot 'payloads') $mode.Name
    $archiveArtifact = Join-Path (Join-Path $payloadRoot 'publish') $artifact.Name
    $archiveMap = Join-Path $payloadRoot 'guidexos.map'
    $archiveLinkResponse = Join-Path $payloadRoot `
        'obj\Release\net9.0\guidexos-x64\native\link.rsp'
    New-Item -ItemType Directory -Force -Path `
        (Split-Path -Parent $archiveArtifact), `
        (Split-Path -Parent $archiveLinkResponse) | Out-Null
    Copy-Item -LiteralPath $artifact.FullName -Destination $archiveArtifact -Force
    Copy-Item -LiteralPath (Join-Path $output 'guidexos.map') `
        -Destination $archiveMap -Force
    Copy-Item -LiteralPath (Join-Path $output `
        'obj\Release\net9.0\guidexos-x64\native\link.rsp') `
        -Destination $archiveLinkResponse -Force
    foreach ($recordName in @('phase26-managed-proof-build.json',
            'user-managed-proof-build.json')) {
        Copy-Item -LiteralPath (Join-Path $output $recordName) `
            -Destination (Join-Path $payloadRoot $recordName) -Force
    }
    $records += [ordered]@{
        name = $mode.Name
        mode = $mode.Mode
        result = $mode.Result
        path = $archiveArtifact
        length = $artifact.Length
        sha256 = (Get-FileHash -LiteralPath $artifact.FullName `
            -Algorithm SHA256).Hash.ToUpperInvariant()
        map = $archiveMap
        linkResponse = $archiveLinkResponse
        project = $project
    }
    Remove-Item -LiteralPath $pack, $output -Recurse -Force
}

$record = [ordered]@{
    schema = 1
    generatedBy = 'Tools/Phase35/build_phase35_managed_persistent_read.ps1'
    target = 'guidexos-x64'
    targetOs = 'guidexos'
    sdkProject = (Join-Path $root 'GuideXos.User\GuideXos.User.csproj')
    persistentServiceId = 9
    ring3OperationId = 7
    requestOperationId = 1
    pathEncoding = 'UTF-16LE code units'
    maxRelativePathLength = 192
    maxPathSegmentLength = 64
    maxPersistentValueLength = 65536
    requestSize = 436
    responseSize = 24
    proofApplicationId = 'selftest.phase10.persistent'
    proofPath = 'state.bin'
    proofLength = 32
    proofSha256 = 'BEFA57E7EF0799D031A0188A3D0883F0F342B8F8AE90B3330652DA04ADBA739D'
    palHelpers = @('guidexos_pal_abi_version',
        'guidexos_pal_service_request',
        'guidexos_pal_persistent_storage_read')
    payloads = @($preservedPayloads) + @($records)
}
$record | ConvertTo-Json -Depth 12 | Set-Content `
    -LiteralPath (Join-Path $OutputRoot `
        'phase35-managed-persistent-read-build.json') -Encoding UTF8
$record | ConvertTo-Json -Depth 12
