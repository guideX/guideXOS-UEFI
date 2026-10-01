[CmdletBinding()]
param(
    [string]$Phase23Pack = '',
    [string]$OutputRoot = '',
    [switch]$CleanupCompletedPriorPhasesOnly
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
if ([string]::IsNullOrWhiteSpace($Phase23Pack)) {
    $Phase23Pack = Join-Path $root 'out\dotnet\phase23-runtime-pack'
}
$expectedOutputRoot = [IO.Path]::GetFullPath(
    (Join-Path $root 'out\dotnet\phase34-managed-resource-read'))
if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = $expectedOutputRoot
}
$OutputRoot = [IO.Path]::GetFullPath($OutputRoot)
if (-not [string]::Equals($OutputRoot, $expectedOutputRoot,
        [StringComparison]::OrdinalIgnoreCase)) {
    throw "Phase 34 build output must stay in $expectedOutputRoot"
}

# The earlier accepted NativeAOT cohorts keep their per-mode runtime pack,
# package cache, and compiler obj trees after staging. These are disposable
# build intermediates; retain each proof executable, descriptor, map, and
# build record while reclaiming the large caches before the Phase 34 cohort.
$dotnetRoot = [IO.Path]::GetFullPath((Join-Path $root 'out\dotnet'))
$intermediateDirectories = @()
$phaseNames = @(
    'phase27-managed-service-proof',
    'phase28-managed-sdk',
    'phase29-managed-notification',
    'phase30-managed-clipboard',
    'phase31-managed-shell',
    'phase32-managed-open-document',
    'phase33-managed-shell-action')
if ($CleanupCompletedPriorPhasesOnly) {
    $phaseNames = $phaseNames[0..4]
}
foreach ($phaseName in $phaseNames) {
    $phaseRoot = Join-Path $dotnetRoot $phaseName
    if (-not (Test-Path -LiteralPath $phaseRoot -PathType Container)) { continue }
    $phaseResolved = (Resolve-Path -LiteralPath $phaseRoot).Path
    if (-not $phaseResolved.StartsWith($dotnetRoot + [IO.Path]::DirectorySeparatorChar,
            [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to inspect managed intermediates outside $dotnetRoot"
    }
    foreach ($child in Get-ChildItem -LiteralPath $phaseResolved -Directory) {
        if ($child.Name.EndsWith('-runtime-pack', [StringComparison]::OrdinalIgnoreCase)) {
            $intermediateDirectories += $child.FullName
            continue
        }
        foreach ($cacheName in @('obj', 'packages-cache')) {
            $cachePath = Join-Path $child.FullName $cacheName
            if (Test-Path -LiteralPath $cachePath -PathType Container) {
                $intermediateDirectories += $cachePath
            }
        }
    }
}
foreach ($intermediatePath in $intermediateDirectories) {
    $resolvedIntermediate = (Resolve-Path -LiteralPath $intermediatePath).Path
    if (-not $resolvedIntermediate.StartsWith($dotnetRoot + [IO.Path]::DirectorySeparatorChar,
            [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to remove managed intermediate outside $dotnetRoot"
    }
    Remove-Item -LiteralPath $resolvedIntermediate -Recurse -Force
}
Write-Host "PHASE34_RECLAIMED_MANAGED_INTERMEDIATE_DIRS=$($intermediateDirectories.Count)"
if ($CleanupCompletedPriorPhasesOnly) { return }

$phase26Builder = Join-Path $root 'Tools\Phase26\build_phase26_managed_proof.ps1'
$palSource = Join-Path $root 'Tools\Phase31\guidexos_phase31_pal_contract.cpp'
$shimSource = Join-Path $root 'Tools\Phase31\_guidexos_phase31_link_shim.cpp'
$project = Join-Path $root 'UserManagedResourceReadProof\guideXOS.UserManagedResourceReadProof.csproj'
foreach ($path in @($phase26Builder, $palSource, $shimSource,
        $Phase23Pack, $project)) {
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Phase 34 build input is absent: $path"
    }
}

if (Test-Path -LiteralPath $OutputRoot) {
    $resolvedOutput = (Resolve-Path -LiteralPath $OutputRoot).Path
    if (-not [string]::Equals($resolvedOutput, $expectedOutputRoot,
            [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to remove unexpected Phase 34 output path: $resolvedOutput"
    }
    Remove-Item -LiteralPath $resolvedOutput -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $OutputRoot | Out-Null

$modes = @(
    [ordered]@{ Name = 'success'; Mode = 'Success'; Result = 34 },
    [ordered]@{ Name = 'failfast'; Mode = 'FailFast'; Result = -1 },
    [ordered]@{ Name = 'stale-owner'; Mode = 'StaleOwner'; Result = 34 },
    [ordered]@{ Name = 'cross-scope'; Mode = 'CrossScope'; Result = 34 },
    [ordered]@{ Name = 'malformed'; Mode = 'Malformed'; Result = 34 }
)

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
        -ProjectProperties @('Phase34Mode=' + $mode.Mode)
    if ($LASTEXITCODE -ne 0) {
        throw "Phase 34 $($mode.Name) payload build failed: $LASTEXITCODE"
    }
    $artifact = Get-ChildItem -LiteralPath (Join-Path $output 'publish') `
        -Filter '*.exe' -File | Select-Object -First 1
    if ($null -eq $artifact) {
        throw "Phase 34 $($mode.Name) payload is absent."
    }
    Normalize-PeTimestamps $artifact.FullName
    $payloadRoot = Join-Path (Join-Path $OutputRoot 'payloads') $mode.Name
    $archiveArtifact = Join-Path (Join-Path $payloadRoot 'publish') $artifact.Name
    $archiveMap = Join-Path $payloadRoot 'guidexos.map'
    $archiveLinkResponse = Join-Path $payloadRoot `
        'obj\Release\net9.0\guidexos-x64\native\link.rsp'
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $archiveArtifact), `
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
    generatedBy = 'Tools/Phase34/build_phase34_managed_resource_read.ps1'
    target = 'guidexos-x64'
    targetOs = 'guidexos'
    sdkProject = (Join-Path $root 'GuideXos.User\GuideXos.User.csproj')
    resourceServiceId = 8
    metadataOperationId = 1
    readOperationId = 2
    resourceNameEncoding = 'ASCII'
    maxResourceNameLength = 96
    maxResourcePayloadLength = 65536
    requestSize = 152
    responseHeaderSize = 44
    proofResourceName = 'diagnostic.fixture'
    proofResourceApplicationId = 'selftest.phase8.services'
    proofResourceLength = 54
    payloads = $records
}
$record | ConvertTo-Json -Depth 12 | Set-Content `
    -LiteralPath (Join-Path $OutputRoot `
        'phase34-managed-resource-read-build.json') -Encoding UTF8
$record | ConvertTo-Json -Depth 12
