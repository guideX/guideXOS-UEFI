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
    $OutputRoot = Join-Path $root 'out\dotnet\phase28-managed-sdk'
}

$phase26Builder = Join-Path $root 'Tools\Phase26\build_phase26_managed_proof.ps1'
$palSource = Join-Path $PSScriptRoot 'guidexos_phase28_pal_contract.cpp'
$project = Join-Path $root 'UserManagedSdkProof\guideXOS.UserManagedSdkProof.csproj'
foreach ($path in @($phase26Builder, $palSource, $Phase23Pack, $project)) {
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Phase 28 build input is absent: $path"
    }
}

$shimSource = Join-Path $PSScriptRoot '_guidexos_phase28_link_shim.cpp'
$shimText = Get-Content -LiteralPath (Join-Path $root 'Tools\Phase23\guidexos_link_shim.cpp') -Raw
$shimDeclaration = @'

extern "C" unsigned long long guidexos_pal_abi_version(void);
extern "C" unsigned long long guidexos_pal_application_identity(
    void* response, unsigned long long responseCapacity);
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
    if (shim_name_is(functionName, "guidexos_pal_application_identity"))
        return (void*)&guidexos_pal_application_identity;
    if (shim_name_is(functionName, "guidexos_pal_service_request"))
        return (void*)&guidexos_pal_service_request;
'@
if (-not $shimText.Contains($shimResolverNeedle)) {
    throw 'The Phase 23 PAL resolver function-name seam is absent.'
}
$shimText = $shimText.Replace($shimResolverNeedle,
    $shimResolverNeedle + "`r`n" + $shimResolverAddition.TrimEnd())

if (Test-Path -LiteralPath $OutputRoot) {
    Remove-Item -LiteralPath $OutputRoot -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $OutputRoot | Out-Null
[IO.File]::WriteAllText($shimSource, $shimText,
    [Text.UTF8Encoding]::new($false))

$modes = @(
    [ordered]@{ Name = 'success'; Mode = 'Success'; Result = 28 },
    [ordered]@{ Name = 'typed-failure'; Mode = 'TypedFailure'; Result = 23 },
    [ordered]@{ Name = 'exit'; Mode = 'Exit'; Result = 73 },
    [ordered]@{ Name = 'failfast'; Mode = 'FailFast'; Result = -1 }
)

function Normalize-PeTimestamps([string]$Path) {
    $bytes = [IO.File]::ReadAllBytes($Path)
    $peOffset = [BitConverter]::ToUInt32($bytes, 0x3C)
    if ($peOffset + 24 -gt $bytes.Length) { throw "PE header is truncated: $Path" }
    ([BitConverter]::GetBytes([uint32]0)).CopyTo($bytes, [int]($peOffset + 8))
    $optionalOffset = $peOffset + 24
    $optionalSize = [BitConverter]::ToUInt16($bytes, [int]($peOffset + 20))
    $sectionCount = [BitConverter]::ToUInt16($bytes, [int]($peOffset + 6))
    $sectionTable = $optionalOffset + $optionalSize
    $debugDirectory = $optionalOffset + 112 + (6 * 8)
    $debugRva = [BitConverter]::ToUInt32($bytes, [int]$debugDirectory)
    $debugSize = [BitConverter]::ToUInt32($bytes, [int]($debugDirectory + 4))
    if ($debugRva -ne 0 -and $debugSize -ge 28) {
        $debugRaw = $null
        for ($index = 0; $index -lt $sectionCount; $index++) {
            $sectionOffset = $sectionTable + ($index * 40)
            $virtualSize = [BitConverter]::ToUInt32($bytes, [int]($sectionOffset + 8))
            $virtualAddress = [BitConverter]::ToUInt32($bytes, [int]($sectionOffset + 12))
            $rawSize = [BitConverter]::ToUInt32($bytes, [int]($sectionOffset + 16))
            $rawPointer = [BitConverter]::ToUInt32($bytes, [int]($sectionOffset + 20))
            $mappedSize = [Math]::Max($virtualSize, $rawSize)
            if ($debugRva -ge $virtualAddress -and
                $debugRva -lt ($virtualAddress + $mappedSize)) {
                $debugRaw = $rawPointer + ($debugRva - $virtualAddress)
                break
            }
        }
        if ($null -eq $debugRaw) { throw "PE debug directory is not section-backed: $Path" }
        $entryCount = [Math]::Floor($debugSize / 28)
        for ($index = 0; $index -lt $entryCount; $index++) {
            $timestampOffset = [int]($debugRaw + ($index * 28) + 4)
            if ($timestampOffset + 4 -gt $bytes.Length) {
                throw "PE debug directory is truncated: $Path"
            }
            ([BitConverter]::GetBytes([uint32]0)).CopyTo($bytes, $timestampOffset)
        }
    }
    [IO.File]::WriteAllBytes($Path, $bytes)
}

$records = @()
foreach ($mode in $modes) {
    $pack = Join-Path $OutputRoot ($mode.Name + '-runtime-pack')
    $output = Join-Path $OutputRoot $mode.Name
    $projectProperties = @('Phase28Mode=' + $mode.Mode)
    & (Join-Path ([Environment]::GetFolderPath('Windows')) 'System32\WindowsPowerShell\v1.0\powershell.exe') -NoProfile -ExecutionPolicy Bypass -File $phase26Builder `
        -Phase23Pack $Phase23Pack -PackRoot $pack -OutputRoot $output `
        -ProjectPath $project -PalSource $palSource -ShimSource $shimSource `
        -ProjectProperties $projectProperties
    if ($LASTEXITCODE -ne 0) {
        throw "Phase 28 $($mode.Name) payload build failed: $LASTEXITCODE"
    }
    $artifact = Get-ChildItem -LiteralPath (Join-Path $output 'publish') `
        -Filter '*.exe' -File | Select-Object -First 1
    if ($null -eq $artifact) { throw "Phase 28 $($mode.Name) payload is absent." }
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
    generatedBy = 'Tools/Phase28/build_phase28_managed_sdk.ps1'
    target = 'guidexos-x64'
    targetOs = 'guidexos'
    sdkProject = (Join-Path $root 'GuideXos.User\GuideXos.User.csproj')
    palHelper = @('guidexos_pal_abi_version',
                  'guidexos_pal_application_identity',
                  'guidexos_pal_service_request')
    identityResponseSize = 40
    serviceRequestSize = 32
    systemInformationResponseSize = 128
    payloads = $records
}
$record | ConvertTo-Json -Depth 12 | Set-Content `
    -LiteralPath (Join-Path $OutputRoot 'phase28-managed-sdk-build.json') -Encoding UTF8
$record | ConvertTo-Json -Depth 12
