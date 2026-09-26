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
    $OutputRoot = Join-Path $root 'out\dotnet\phase27-managed-service-proof'
}

$phase26Builder = Join-Path $root 'Tools\Phase26\build_phase26_managed_proof.ps1'
$palSource = Join-Path $PSScriptRoot 'guidexos_phase27_pal_contract.cpp'
$shimSource = Join-Path $PSScriptRoot '_guidexos_phase27_link_shim.cpp'
$successProject = Join-Path $root 'UserManagedServiceProof\guideXOS.UserManagedServiceProof.csproj'
$failureProject = Join-Path $root 'UserManagedServiceFailure\guideXOS.UserManagedServiceFailure.csproj'

foreach ($path in @($phase26Builder, $palSource,
        $Phase23Pack, $successProject, $failureProject)) {
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Phase 27 build input is absent: $path"
    }
}

$shimText = Get-Content -LiteralPath (Join-Path $root 'Tools\Phase23\guidexos_link_shim.cpp') -Raw
$shimDeclaration = @'

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

$successPack = Join-Path $OutputRoot 'success-runtime-pack'
$successOutput = Join-Path $OutputRoot 'success'
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File $phase26Builder `
    -Phase23Pack $Phase23Pack -PackRoot $successPack -OutputRoot $successOutput `
    -ProjectPath $successProject -PalSource $palSource -ShimSource $shimSource
if ($LASTEXITCODE -ne 0) { throw "Phase 27 success payload build failed: $LASTEXITCODE" }

$failurePack = Join-Path $OutputRoot 'failure-runtime-pack'
$failureOutput = Join-Path $OutputRoot 'failure'
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File $phase26Builder `
    -Phase23Pack $Phase23Pack -PackRoot $failurePack -OutputRoot $failureOutput `
    -ProjectPath $failureProject -PalSource $palSource -ShimSource $shimSource
if ($LASTEXITCODE -ne 0) { throw "Phase 27 failure payload build failed: $LASTEXITCODE" }

function Normalize-PeTimestamps([string]$Path) {
    $bytes = [IO.File]::ReadAllBytes($Path)
    $peOffset = [BitConverter]::ToUInt32($bytes, 0x3C)
    if ($peOffset + 24 -gt $bytes.Length) { throw "PE header is truncated: $Path" }

    # LINK emits the wall-clock timestamp in the COFF header.  The debug
    # directory repeats the same value in IMAGE_DEBUG_DIRECTORY.  Neither
    # field participates in the GUIDEXOS loader contract, so normalize both
    # before recording the exact artifact hash.
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

$successArtifact = Get-ChildItem -LiteralPath (Join-Path $successOutput 'publish') -Filter '*.exe' -File | Select-Object -First 1
$failureArtifact = Get-ChildItem -LiteralPath (Join-Path $failureOutput 'publish') -Filter '*.exe' -File | Select-Object -First 1
if ($null -eq $successArtifact -or $null -eq $failureArtifact) {
    throw 'Phase 27 payload build did not produce both managed artifacts.'
}
Normalize-PeTimestamps $successArtifact.FullName
Normalize-PeTimestamps $failureArtifact.FullName

foreach ($output in @($successOutput, $failureOutput)) {
    $nestedRecordPath = Join-Path $output 'user-managed-proof-build.json'
    if (Test-Path -LiteralPath $nestedRecordPath) {
        $nestedRecord = Get-Content -LiteralPath $nestedRecordPath -Raw | ConvertFrom-Json
        $nestedArtifact = Get-ChildItem -LiteralPath (Join-Path $output 'publish') -Filter '*.exe' -File | Select-Object -First 1
        $nestedRecord.artifact.path = $nestedArtifact.FullName
        $nestedRecord.artifact.length = $nestedArtifact.Length
        $nestedRecord.artifact.sha256 = (Get-FileHash -LiteralPath $nestedArtifact.FullName -Algorithm SHA256).Hash.ToUpperInvariant()
        $nestedRecord | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $nestedRecordPath -Encoding UTF8
    }
}

$record = [ordered]@{
    schema = 1
    generatedBy = 'Tools/Phase27/build_phase27_managed_service_proof.ps1'
    target = 'guidexos-x64'
    targetOs = 'guidexos'
    success = [ordered]@{
        path = $successArtifact.FullName
        length = $successArtifact.Length
        sha256 = (Get-FileHash -LiteralPath $successArtifact.FullName -Algorithm SHA256).Hash.ToUpperInvariant()
        project = $successProject
        managedResult = 27
    }
    failure = [ordered]@{
        path = $failureArtifact.FullName
        length = $failureArtifact.Length
        sha256 = (Get-FileHash -LiteralPath $failureArtifact.FullName -Algorithm SHA256).Hash.ToUpperInvariant()
        project = $failureProject
        managedResult = 21
    }
    palHelper = 'guidexos_pal_service_request'
    palCallingConvention = 'Microsoft x64 ABI'
    ring3Operation = 5
    requestSize = 32
    responseSize = 128
}
$record | ConvertTo-Json -Depth 12 | Set-Content `
    -LiteralPath (Join-Path $OutputRoot 'phase27-managed-service-proof-build.json') -Encoding UTF8
$record | ConvertTo-Json -Depth 12
