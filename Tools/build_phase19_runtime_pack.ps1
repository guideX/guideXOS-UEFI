[CmdletBinding()]
param(
    [switch]$Clean,
    [switch]$BuildBaseline,
    [string]$AdvancedServerReferenceRoot
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$phaseRoot = Join-Path $root 'Tools\Phase19'
$outRoot = Join-Path $root 'out\dotnet\phase19-runtime-pack'
$lockPath = Join-Path $phaseRoot 'runtime-pack.lock.json'
$contractPath = Join-Path $phaseRoot 'pal-contract.json'
$contractSource = Join-Path $phaseRoot 'guidexos_nativeaot_pal_contract.cpp'
$contractHeader = Join-Path $phaseRoot 'guidexos_nativeaot_pal_contract.h'
$stockManifest = Join-Path $root 'bin\UserManagedProof\managed-image-manifest.json'
$runtimeCommit = '9d5a6a9aa463d6d10b0b0ba6d5982cc82f363dc3'

function Get-Hash([string]$path) {
    return (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToUpperInvariant()
}

function Require-File([string]$path) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Required file not found: $path" }
}

function Find-VcVars64 {
    $candidates = @(
        (Join-Path ${env:ProgramFiles} 'Microsoft Visual Studio\18\Enterprise\VC\Auxiliary\Build\vcvars64.bat'),
        (Join-Path ${env:ProgramFiles} 'Microsoft Visual Studio\18\Professional\VC\Auxiliary\Build\vcvars64.bat'),
        (Join-Path ${env:ProgramFiles} 'Microsoft Visual Studio\18\Community\VC\Auxiliary\Build\vcvars64.bat'),
        (Join-Path ${env:ProgramFiles} 'Microsoft Visual Studio\2022\Enterprise\VC\Auxiliary\Build\vcvars64.bat'),
        (Join-Path ${env:ProgramFiles} 'Microsoft Visual Studio\2022\Professional\VC\Auxiliary\Build\vcvars64.bat'),
        (Join-Path ${env:ProgramFiles} 'Microsoft Visual Studio\2022\Community\VC\Auxiliary\Build\vcvars64.bat')
    )
    foreach ($candidate in $candidates) {
        if (Test-Path -LiteralPath $candidate -PathType Leaf) { return $candidate }
    }
    throw 'VS vcvars64.bat not found; install/use the pinned native-tools environment.'
}

function Invoke-NativeTools([string]$command) {
    $vcvars = Find-VcVars64
    $full = "call `"$vcvars`" && $command"
    & cmd.exe /d /s /c $full
    if ($LASTEXITCODE -ne 0) { throw "Native tool command failed with exit code $LASTEXITCODE" }
}

if ($Clean) {
    if (Test-Path -LiteralPath $outRoot) {
        $resolvedOut = (Resolve-Path -LiteralPath $outRoot).Path
        $resolvedRoot = (Resolve-Path -LiteralPath (Join-Path $root 'out')).Path
        if (-not $resolvedOut.StartsWith($resolvedRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
            throw "Refusing to clean outside the repository output directory: $resolvedOut"
        }
        Remove-Item -LiteralPath $resolvedOut -Recurse -Force
    }
}
New-Item -ItemType Directory -Force -Path $outRoot | Out-Null

Require-File $lockPath
Require-File $contractPath
Require-File $contractSource
Require-File $contractHeader
$lock = Get-Content -LiteralPath $lockPath -Raw | ConvertFrom-Json
$contract = Get-Content -LiteralPath $contractPath -Raw | ConvertFrom-Json
if ($lock.targetIdentity -ne 'guidexos-x64' -or $contract.targetIdentity -ne 'guidexos-x64') { throw 'Phase 19 target identity is not guidexos-x64.' }

$sdkVersion = (& dotnet --version).Trim()
if ($sdkVersion -ne $lock.sdk.version) { throw "Pinned SDK mismatch: expected $($lock.sdk.version), got $sdkVersion" }

$profile = [Environment]::GetFolderPath('UserProfile')
$ilcompilerPackage = Join-Path $profile '.nuget\packages\microsoft.dotnet.ilcompiler\9.0.0'
$runtimePackage = Join-Path $profile '.nuget\packages\runtime.win-x64.microsoft.dotnet.ilcompiler\9.0.0'
Require-File (Join-Path $ilcompilerPackage 'build\Microsoft.NETCore.Native.Windows.targets')
Require-File (Join-Path $ilcompilerPackage 'build\WindowsAPIs.txt')
Require-File (Join-Path $runtimePackage 'sdk\bootstrapper.obj')
Require-File (Join-Path $runtimePackage 'sdk\Runtime.WorkstationGC.lib')

$lockedFiles = @(
    @{ path = Join-Path $ilcompilerPackage 'build\Microsoft.NETCore.Native.Windows.targets'; hash = 'F001FBC7933D8C57F7B0AF6B9261A63AAB52ADDFFA4350E674B694E525F57264' },
    @{ path = Join-Path $ilcompilerPackage 'build\WindowsAPIs.txt'; hash = '54B672743B6D5E3D97D5B7FD90577CA72887592EB411C0593158D56CB3800DCF' },
    @{ path = Join-Path $runtimePackage 'sdk\bootstrapper.obj'; hash = 'E97995D4179E4B493232CE386DC8D2780E53E5EAF8724E663E598EA096FD7685' },
    @{ path = Join-Path $runtimePackage 'sdk\Runtime.WorkstationGC.lib'; hash = '0E6A134AD4150CD604317A47860DAE82EB30AAE4D9CDB14144E06454E7BB1948' },
    @{ path = Join-Path $runtimePackage 'sdk\eventpipe-disabled.lib'; hash = 'E93CB51344EE86484AC09E8DC13B80FAE269719432775E3E4F012C6C18184027' },
    @{ path = Join-Path $runtimePackage 'sdk\Runtime.VxsortDisabled.lib'; hash = '75779AE574059A3F05A71D992A4B146EFC93B2E2DF5A5C80D765DAFBB507017B' },
    @{ path = Join-Path $runtimePackage 'sdk\standalonegc-disabled.lib'; hash = '7CE4836B9D161503F0A78E7BC4B9952181EE47CAEAE41B5B7B497FD1B4001CE7' }
)
foreach ($item in $lockedFiles) {
    $actual = Get-Hash $item.path
    if ($actual -ne $item.hash) { throw "Locked toolchain hash mismatch: $($item.path) expected $($item.hash), got $actual" }
}

if ($AdvancedServerReferenceRoot) {
    $referenceCandidates = @(
        $AdvancedServerReferenceRoot,
        (Join-Path $AdvancedServerReferenceRoot 'out\dotnet\gc-feasibility-baseline\nativeaot-runtime')
    )
    $referenceGitRoot = $null
    foreach ($candidate in $referenceCandidates) {
        if (Test-Path -LiteralPath $candidate -PathType Container) {
            $previousErrorAction = $ErrorActionPreference
            $ErrorActionPreference = 'Continue'
            & git -C $candidate rev-parse --verify $runtimeCommit 2>$null | Out-Null
            $probeExitCode = $LASTEXITCODE
            $ErrorActionPreference = $previousErrorAction
            if ($probeExitCode -eq 0) {
                $referenceGitRoot = (& git -C $candidate rev-parse --show-toplevel).Trim()
                break
            }
        }
    }
    if (-not $referenceGitRoot) { throw "Advanced Server reference does not contain locked runtime commit $runtimeCommit" }
}

$objectPath = Join-Path $outRoot 'guidexos_nativeaot_pal_contract.obj'
$archivePath = Join-Path $outRoot 'guidexos_nativeaot_pal_contract.lib'
$symbolsPath = Join-Path $outRoot 'guidexos_nativeaot_pal_contract.symbols.txt'
$directivesPath = Join-Path $outRoot 'guidexos_nativeaot_pal_contract.directives.txt'
$sourcePathForCommand = $contractSource
$objectPathForCommand = $objectPath
$archivePathForCommand = $archivePath
$symbolsPathForCommand = $symbolsPath
$directivesPathForCommand = $directivesPath

$compile = "cl.exe /nologo /c /TP /GS- /GR- /EHs-c- /Zl /Oi- /Od /Ob0 /Brepro /Fo`"$objectPathForCommand`" `"$sourcePathForCommand`""
Invoke-NativeTools $compile
$archive = "lib.exe /nologo /OUT:`"$archivePathForCommand`" `"$objectPathForCommand`""
Invoke-NativeTools $archive
$symbols = "dumpbin.exe /nologo /symbols `"$archivePathForCommand`" > `"$symbolsPathForCommand`""
Invoke-NativeTools $symbols
$directives = "dumpbin.exe /nologo /directives `"$archivePathForCommand`" > `"$directivesPathForCommand`""
Invoke-NativeTools $directives

$symbolText = Get-Content -LiteralPath $symbolsPath -Raw
if ($symbolText -match 'UNDEF') { throw 'PAL contract archive contains undefined symbols.' }
if ($symbolText -match '(?i)kernel32|advapi32|ole32|bcrypt|ucrt|msvcrt|win32') { throw 'PAL contract archive contains a forbidden Windows/CRT dependency name.' }

if ($BuildBaseline -or -not (Test-Path -LiteralPath $stockManifest -PathType Leaf)) {
    & powershell.exe -ExecutionPolicy Bypass -File (Join-Path $root 'Tools\build_managed_user_proof.ps1') -NoRestore
    if ($LASTEXITCODE -ne 0) { throw 'Stock Phase 17 baseline build failed.' }
}
Require-File $stockManifest

$manifestPath = Join-Path $outRoot 'phase19-manifest.json'
$auditArgs = @(
    (Join-Path $phaseRoot 'phase19_runtime_audit.py'),
    '--stock-manifest', $stockManifest,
    '--pal-contract', $contractPath,
    '--output', $manifestPath,
    '--target', 'guidexos-x64',
    '--repository', $root,
    '--contract-source', $contractSource,
    '--contract-object', $objectPath,
    '--contract-archive', $archivePath
)
& python @auditArgs
if ($LASTEXITCODE -ne 0) { throw 'Phase 19 runtime audit failed.' }

$toolchainRecord = [ordered]@{
    sdk = $sdkVersion
    ilcompilerPackage = 'Microsoft.DotNet.ILCompiler 9.0.0'
    runtimePackage = 'runtime.win-x64.Microsoft.DotNet.ILCompiler 9.0.0'
    sourceRepository = $lock.nativeAot.sourceRepository
    sourceCommit = $lock.nativeAot.sourceCommit
    stockTarget = $lock.nativeAot.stockTarget
    globalSdkModified = $false
    advancedServerReferenceRoot = $AdvancedServerReferenceRoot
    advancedServerRuntimeGitRoot = $referenceGitRoot
    nativeTools = (Find-VcVars64)
    lockedFiles = @($lockedFiles | ForEach-Object { [ordered]@{ path = $_.path; sha256 = (Get-Hash $_.path) } })
    contractObject = $objectPath
    contractArchive = $archivePath
}
$toolchainRecord | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $outRoot 'toolchain-record.json') -Encoding UTF8

Write-Host "Phase 19 contract pack built: $archivePath"
Write-Host "Phase 19 outcome: Outcome C - bounded CoreCLR/NativeAOT runtime source-build prerequisite"
Write-Host "No custom managed payload was built or executed."
