[CmdletBinding()]
param(
    [string]$PackRoot = '',
    [string]$OutputRoot = '',
    [string]$ProjectPath = '',
    [string[]]$ProjectProperties = @()
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
if ([string]::IsNullOrWhiteSpace($PackRoot)) { $PackRoot = Join-Path $root 'out\dotnet\phase23-runtime-pack' }
if ([string]::IsNullOrWhiteSpace($OutputRoot)) { $OutputRoot = Join-Path $root 'out\dotnet\phase23-user-managed-proof' }

$manifestPath = Join-Path $PackRoot 'guidexos-runtime-pack.manifest.json'
$feed = Join-Path $PackRoot 'packages'
$project = if ([string]::IsNullOrWhiteSpace($ProjectPath)) {
    Join-Path $root 'UserManagedProof\guideXOS.UserManagedProof.csproj'
} else { $ProjectPath }
$publishDirectory = Join-Path $OutputRoot 'publish'
$recordPath = Join-Path $OutputRoot 'user-managed-proof-build.json'
$packagesPath = Join-Path $OutputRoot 'packages-cache'
$nugetConfig = Join-Path $OutputRoot 'NuGet.Config'

foreach ($path in @($manifestPath, $feed, $project)) {
    if (-not (Test-Path -LiteralPath $path)) { throw "Required Phase 23 payload input is absent: $path" }
}
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
if ($manifest.target -ne 'guidexos-x64' -or $manifest.targetOs -ne 'guidexos') { throw 'The staged pack is not a GUIDEXOS target pack.' }
if ($manifest.stockWinX64Fallback -ne $false) { throw 'The staged pack permits a stock win-x64 fallback.' }
if ($manifest.sdkNativeLibraries.Count -ne 0 -or $manifest.directPInvokes.Count -ne 0) { throw 'The staged pack contains forbidden default SDK/PInvoke inputs.' }

$vsRoot = 'C:\Program Files\Microsoft Visual Studio\18\Community\VC\Tools\MSVC'
$linker = Get-ChildItem -LiteralPath $vsRoot -Filter link.exe -File -Recurse | Where-Object { $_.FullName -match '\\Hostx64\\x64\\link\.exe$' } | Sort-Object FullName -Descending | Select-Object -First 1
$libCreator = Get-ChildItem -LiteralPath $vsRoot -Filter lib.exe -File -Recurse | Where-Object { $_.FullName -match '\\Hostx64\\x64\\lib\.exe$' } | Sort-Object FullName -Descending | Select-Object -First 1
if ($null -eq $linker -or $null -eq $libCreator) { throw 'The VS 18 x64 linker tools were not found.' }

if (Test-Path -LiteralPath $OutputRoot) { Remove-Item -LiteralPath $OutputRoot -Recurse -Force }
New-Item -ItemType Directory -Force -Path $publishDirectory | Out-Null
$configText = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'NuGet.Phase23.config') -Raw
$configText = $configText.Replace('__PHASE23_FEED__', $feed)
[IO.File]::WriteAllText($nugetConfig, $configText, [Text.UTF8Encoding]::new($false))
$baseIntermediate = Join-Path $OutputRoot 'obj\'
$outputPath = Join-Path $OutputRoot 'bin\'
$mapPath = Join-Path $OutputRoot 'guidexos.map'

$common = @(
    '-p:Phase23Guidexos=true',
    '-p:RuntimeIdentifier=guidexos-x64',
    '-p:RuntimeFrameworkVersion=9.0.0',
    '-p:TargetLatestRuntimePatch=false',
    '-p:UseAppHost=false',
    '-p:PublishAot=true',
    '-p:SelfContained=true',
    '-p:IlcUseEnvironmentalTools=true',
    "-p:Phase23LinkMap=$mapPath",
    "-p:CppLinker=$($linker.FullName)",
    "-p:CppLibCreator=$($libCreator.FullName)",
    "-p:BaseIntermediateOutputPath=$baseIntermediate",
    "-p:OutputPath=$outputPath",
    "-p:PublishDir=$publishDirectory\",
    "-p:RestorePackagesPath=$packagesPath",
    '-v:minimal'
)
foreach ($property in $ProjectProperties) {
    if ([string]::IsNullOrWhiteSpace($property)) { continue }
    $common += if ($property.StartsWith('-p:')) { $property } else { '-p:' + $property }
}

$restoreArgs = @('restore', $project, '--configfile', $nugetConfig, '--force-evaluate', '--no-cache') + $common
& dotnet @restoreArgs 2>&1 | Tee-Object -FilePath (Join-Path $OutputRoot 'restore.log')
if ($LASTEXITCODE -ne 0) { throw "Phase 23 payload restore failed with exit code $LASTEXITCODE." }

$publishArgs = @('publish', $project, '--no-restore') + $common
& dotnet @publishArgs 2>&1 | Tee-Object -FilePath (Join-Path $OutputRoot 'publish.log')
if ($LASTEXITCODE -ne 0) { throw "Phase 23 payload publish failed with exit code $LASTEXITCODE." }

$artifact = Get-ChildItem -LiteralPath $publishDirectory -Filter '*.exe' -File | Select-Object -First 1
if ($null -eq $artifact) { throw "The Phase 23 publish did not produce a native executable in $publishDirectory." }
$record = [ordered]@{
    schema = 1
    generatedBy = 'Tools/Phase23/build_user_managed_proof.ps1'
    target = 'guidexos-x64'
    targetOs = 'guidexos'
    packManifest = (Resolve-Path $manifestPath).Path
    packageFeed = (Resolve-Path $feed).Path
    linker = $linker.FullName
    libraryCreator = $libCreator.FullName
    project = $project
    artifact = [ordered]@{
        path = $artifact.FullName
        length = $artifact.Length
        sha256 = (Get-FileHash -LiteralPath $artifact.FullName -Algorithm SHA256).Hash.ToUpperInvariant()
    }
    nativeIntermediateOutput = (Resolve-Path $baseIntermediate -ErrorAction SilentlyContinue).Path
    restoreSources = @($feed)
    fallback = 'forbidden: win-x64'
    managedExecution = $false
}
$record | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $recordPath -Encoding UTF8
$record | ConvertTo-Json -Depth 8
Write-Output "Wrote $recordPath"
