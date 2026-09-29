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
if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path $root 'out\dotnet\phase31-managed-shell'
}

$phase26Builder = Join-Path $root 'Tools\Phase26\build_phase26_managed_proof.ps1'
$palSource = Join-Path $PSScriptRoot 'guidexos_phase31_pal_contract.cpp'
$project = Join-Path $root 'UserManagedShellProof\guideXOS.UserManagedShellProof.csproj'
foreach ($path in @($phase26Builder, $palSource, $Phase23Pack, $project)) {
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Phase 31 build input is absent: $path"
    }
}

$shimSource = Join-Path $PSScriptRoot '_guidexos_phase31_link_shim.cpp'
$shimText = Get-Content -LiteralPath (Join-Path $root 'Tools\Phase23\guidexos_link_shim.cpp') -Raw
$shimDeclaration = @'

extern "C" unsigned long long guidexos_pal_abi_version(void);
extern "C" unsigned long long guidexos_pal_service_request(
    unsigned long long request, unsigned long long requestLength);
'@
$shimNeedle = 'void* PalGetProcAddress(HANDLE module, const char* functionName)'
if (-not $shimText.Contains($shimNeedle)) {
    throw 'The Phase 23 PAL resolver seam is absent.'
}
$shimText = $shimText.Replace($shimNeedle, $shimDeclaration + "`r`n" + $shimNeedle)
$shimResolverNeedle = '    if (functionName == 0) return 0;'
$shimResolverAddition = @'
    if (shim_name_is(functionName, "guidexos_pal_abi_version"))
        return (void*)&guidexos_pal_abi_version;
    if (shim_name_is(functionName, "guidexos_pal_service_request"))
        return (void*)&guidexos_pal_service_request;
'@
if (-not $shimText.Contains($shimResolverNeedle)) {
    throw 'The Phase 23 PAL resolver function-name seam is absent.'
}
$shimText = $shimText.Replace($shimResolverNeedle,
    $shimResolverNeedle + "`r`n" + $shimResolverAddition.TrimEnd())
[IO.File]::WriteAllText($shimSource, $shimText,
    [Text.UTF8Encoding]::new($false))

if (Test-Path -LiteralPath $OutputRoot) {
    Remove-Item -LiteralPath $OutputRoot -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $OutputRoot | Out-Null

$modes = @(
    [ordered]@{ Name = 'success'; Mode = 'Success'; Result = 31 },
    [ordered]@{ Name = 'invalid-target'; Mode = 'InvalidTarget'; Result = 41 },
    [ordered]@{ Name = 'oversize'; Mode = 'Oversize'; Result = 42 },
    [ordered]@{ Name = 'failfast'; Mode = 'FailFast'; Result = -1 },
    [ordered]@{ Name = 'stale-owner'; Mode = 'StaleOwner'; Result = 32 }
)

. (Join-Path $PSScriptRoot '..\ManagedArtifacts\Normalize-PeTimestamps.ps1')

$records = @()
foreach ($mode in $modes) {
    $pack = Join-Path $OutputRoot ($mode.Name + '-runtime-pack')
    $output = Join-Path $OutputRoot $mode.Name
    & (Join-Path ([Environment]::GetFolderPath('Windows')) 'System32\WindowsPowerShell\v1.0\powershell.exe') -NoProfile -ExecutionPolicy Bypass -File $phase26Builder `
        -Phase23Pack $Phase23Pack -PackRoot $pack -OutputRoot $output `
        -ProjectPath $project -PalSource $palSource -ShimSource $shimSource `
        -ProjectProperties @('Phase31Mode=' + $mode.Mode)
    if ($LASTEXITCODE -ne 0) {
        throw "Phase 31 $($mode.Name) payload build failed: $LASTEXITCODE"
    }
    $artifact = Get-ChildItem -LiteralPath (Join-Path $output 'publish') -Filter '*.exe' -File |
        Select-Object -First 1
    if ($null -eq $artifact) { throw "Phase 31 $($mode.Name) payload is absent." }
    Normalize-PeTimestamps $artifact.FullName
    $records += [ordered]@{
        name = $mode.Name
        mode = $mode.Mode
        result = $mode.Result
        path = $artifact.FullName
        length = $artifact.Length
        sha256 = (Get-FileHash -LiteralPath $artifact.FullName -Algorithm SHA256).Hash.ToUpperInvariant()
        project = $project
    }
}

$record = [ordered]@{
    schema = 1
    generatedBy = 'Tools/Phase31/build_phase31_managed_shell.ps1'
    target = 'guidexos-x64'
    targetOs = 'guidexos'
    sdkProject = (Join-Path $root 'GuideXos.User\GuideXos.User.csproj')
    palHelper = @('guidexos_pal_abi_version', 'guidexos_pal_service_request')
    shellServiceId = 7
    launchApplicationIdOperation = 1
    targetLength = 1024
    requestSize = 2084
    responseSize = 16
    payloads = $records
}
$record | ConvertTo-Json -Depth 12 | Set-Content `
    -LiteralPath (Join-Path $OutputRoot 'phase31-managed-shell-build.json') -Encoding UTF8
$record | ConvertTo-Json -Depth 12
