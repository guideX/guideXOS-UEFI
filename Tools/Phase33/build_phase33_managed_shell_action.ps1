[CmdletBinding()]
param(
    [string]$Phase23Pack = '',
    [string]$OutputRoot = ''
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
if ([string]::IsNullOrWhiteSpace($Phase23Pack)) {
    $Phase23Pack = Join-Path $root 'out\dotnet\phase23-runtime-pack'
}
$expectedOutputRoot = [IO.Path]::GetFullPath(
    (Join-Path $root 'out\dotnet\phase33-managed-shell-action'))
if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = $expectedOutputRoot
}
$OutputRoot = [IO.Path]::GetFullPath($OutputRoot)
if (-not [string]::Equals($OutputRoot, $expectedOutputRoot,
        [StringComparison]::OrdinalIgnoreCase)) {
    throw "Phase 33 build output must stay in $expectedOutputRoot"
}

$phase26Builder = Join-Path $root 'Tools\Phase26\build_phase26_managed_proof.ps1'
$palSource = Join-Path $root 'Tools\Phase31\guidexos_phase31_pal_contract.cpp'
$shimSource = Join-Path $root 'Tools\Phase31\_guidexos_phase31_link_shim.cpp'
$project = Join-Path $root 'UserManagedShellActionProof\guideXOS.UserManagedShellActionProof.csproj'
foreach ($path in @($phase26Builder, $palSource, $shimSource,
        $Phase23Pack, $project)) {
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Phase 33 build input is absent: $path"
    }
}

if (Test-Path -LiteralPath $OutputRoot) {
    $resolvedOutput = (Resolve-Path -LiteralPath $OutputRoot).Path
    if (-not [string]::Equals($resolvedOutput, $expectedOutputRoot,
            [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to remove unexpected Phase 33 output path: $resolvedOutput"
    }
    Remove-Item -LiteralPath $resolvedOutput -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $OutputRoot | Out-Null

$modes = @(
    [ordered]@{ Name = 'success'; Mode = 'Success'; Result = 33 },
    [ordered]@{ Name = 'failfast'; Mode = 'FailFast'; Result = -1 },
    [ordered]@{ Name = 'stale-owner'; Mode = 'StaleOwner'; Result = 33 },
    [ordered]@{ Name = 'invalid-action'; Mode = 'InvalidAction'; Result = 33 },
    [ordered]@{ Name = 'malformed'; Mode = 'Malformed'; Result = 33 }
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
        -ProjectProperties @('Phase33Mode=' + $mode.Mode)
    if ($LASTEXITCODE -ne 0) {
        throw "Phase 33 $($mode.Name) payload build failed: $LASTEXITCODE"
    }
    $artifact = Get-ChildItem -LiteralPath (Join-Path $output 'publish') `
        -Filter '*.exe' -File | Select-Object -First 1
    if ($null -eq $artifact) {
        throw "Phase 33 $($mode.Name) payload is absent."
    }
    Normalize-PeTimestamps $artifact.FullName
    $records += [ordered]@{
        name = $mode.Name
        mode = $mode.Mode
        result = $mode.Result
        path = $artifact.FullName
        length = $artifact.Length
        sha256 = (Get-FileHash -LiteralPath $artifact.FullName `
            -Algorithm SHA256).Hash.ToUpperInvariant()
        project = $project
    }
}

$record = [ordered]@{
    schema = 1
    generatedBy = 'Tools/Phase33/build_phase33_managed_shell_action.ps1'
    target = 'guidexos-x64'
    targetOs = 'guidexos'
    sdkProject = (Join-Path $root 'GuideXos.User\GuideXos.User.csproj')
    shellServiceId = 7
    launchApplicationIdOperation = 1
    openDocumentOperation = 2
    openShellObjectOperation = 3
    objectId = 'gxos.shell.computerfiles'
    objectTargetLength = 24
    maximumPhase9TargetLength = 1024
    requestSize = 2084
    responseSize = 16
    payloads = $records
}
$record | ConvertTo-Json -Depth 12 | Set-Content `
    -LiteralPath (Join-Path $OutputRoot 'phase33-managed-shell-action-build.json') `
    -Encoding UTF8
$record | ConvertTo-Json -Depth 12
