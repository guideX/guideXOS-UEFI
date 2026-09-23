[CmdletBinding()]
param(
    [string]$RuntimeSourceRoot = '',
    [string]$OutputRoot = ''
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
if ([string]::IsNullOrWhiteSpace($RuntimeSourceRoot)) { $RuntimeSourceRoot = Join-Path $root 'out\rt' }
if ([string]::IsNullOrWhiteSpace($OutputRoot)) { $OutputRoot = Join-Path $root 'out\dotnet\phase22-runtime-pack' }

$runtimeCommit = '9d5a6a9aa463d6d10b0b0ba6d5982cc82f363dc3'
$runtimeVersion = '9.0.0'
$runtimeBuild = Join-Path $RuntimeSourceRoot 'artifacts\obj\coreclr\guidexos.x64.Release'
$work = Join-Path $OutputRoot '_work'
$packageSource = Join-Path $OutputRoot 'packages'
$runtimePackageCache = Join-Path $env:USERPROFILE '.nuget\packages\runtime.win-x64.microsoft.dotnet.ilcompiler\9.0.0'
$compilerPackageCache = Join-Path $env:USERPROFILE '.nuget\packages\microsoft.dotnet.ilcompiler\9.0.0'

foreach ($path in @($runtimeBuild, $runtimePackageCache, $compilerPackageCache)) {
    if (-not (Test-Path -LiteralPath $path -PathType Container)) { throw "Required source is absent: $path" }
}
$actualCommit = (& git -C $RuntimeSourceRoot rev-parse HEAD).Trim()
if ($actualCommit -ne $runtimeCommit) { throw "Runtime source mismatch: expected $runtimeCommit, found $actualCommit." }

if (Test-Path -LiteralPath $work) { Remove-Item -LiteralPath $work -Recurse -Force }
if (Test-Path -LiteralPath $packageSource) { Remove-Item -LiteralPath $packageSource -Recurse -Force }
New-Item -ItemType Directory -Force -Path $work, $packageSource | Out-Null

function Expand-Nupkg([string]$packagePath, [string]$destination) {
    New-Item -ItemType Directory -Force -Path $destination | Out-Null
    # Expand-Archive only accepts the .zip extension even though a nupkg is
    # a ZIP container. Keep the conversion inside generated staging state.
    $zipPath = Join-Path $destination '__package.zip'
    Copy-Item -LiteralPath $packagePath -Destination $zipPath -Force
    Expand-Archive -LiteralPath $zipPath -DestinationPath $destination -Force
    Remove-Item -LiteralPath $zipPath -Force
}

function Pack-Nupkg([string]$source, [string]$packageId, [string]$version) {
    $nuspec = Get-ChildItem -LiteralPath $source -Filter '*.nuspec' -File | Select-Object -First 1
    if ($null -eq $nuspec) { throw "Package nuspec is absent: $source" }
    # The official package's repository signature cannot be preserved after
    # rebuilding the archive.  Removing it makes the private package
    # unsigned and explicit rather than shipping a malformed signature entry.
    Get-ChildItem -LiteralPath $source -Filter '.signature.p7s' -File -ErrorAction SilentlyContinue | Remove-Item -Force
    $nuspecText = Get-Content -LiteralPath $nuspec.FullName -Raw
    $nuspecText = [regex]::Replace($nuspecText, '<id>[^<]+</id>', "<id>$packageId</id>", 1)
    $nuspecText = [regex]::Replace($nuspecText, '<title>[^<]+</title>', "<title>$packageId</title>", 1)
    [IO.File]::WriteAllText($nuspec.FullName, $nuspecText, [Text.UTF8Encoding]::new($false))
    $archive = Join-Path $packageSource ("{0}.{1}.nupkg" -f $packageId.ToLowerInvariant(), $version)
    $zipArchive = Join-Path $packageSource ("{0}.{1}.zip" -f $packageId.ToLowerInvariant(), $version)
    Compress-Archive -Path (Join-Path $source '*') -DestinationPath $zipArchive -CompressionLevel Optimal -Force
    Move-Item -LiteralPath $zipArchive -Destination $archive -Force
    return $archive
}

$compilerWork = Join-Path $work 'compiler'
$runtimeWork = Join-Path $work 'runtime'
Expand-Nupkg (Join-Path $compilerPackageCache 'microsoft.dotnet.ilcompiler.9.0.0.nupkg') $compilerWork
Expand-Nupkg (Join-Path $runtimePackageCache 'runtime.win-x64.microsoft.dotnet.ilcompiler.9.0.0.nupkg') $runtimeWork

$runtimeJsonPath = Join-Path $compilerWork 'runtime.json'
$runtimeJson = Get-Content -LiteralPath $runtimeJsonPath -Raw | ConvertFrom-Json
$runtimeJson.runtimes | Add-Member -NotePropertyName 'guidexos-x64' -NotePropertyValue ([pscustomobject]@{
    '#import' = @()
    'Microsoft.DotNet.ILCompiler' = [pscustomobject]@{ 'runtime.guidexos-x64.Microsoft.DotNet.ILCompiler' = "[$runtimeVersion, )" }
})
$runtimeJson | ConvertTo-Json -Depth 16 | Set-Content -LiteralPath $runtimeJsonPath -Encoding UTF8

$runtimeNuspecPath = Get-ChildItem -LiteralPath $runtimeWork -Filter '*.nuspec' -File | Select-Object -First 1
$runtimeNuspec = Get-Content -LiteralPath $runtimeNuspecPath.FullName -Raw
$runtimeNuspec = $runtimeNuspec.Replace('runtime.win-x64.Microsoft.DotNet.ILCompiler', 'runtime.guidexos-x64.Microsoft.DotNet.ILCompiler')
[IO.File]::WriteAllText($runtimeNuspecPath.FullName, $runtimeNuspec, [Text.UTF8Encoding]::new($false))
$privateNuspecPath = Join-Path $runtimeWork 'runtime.guidexos-x64.Microsoft.DotNet.ILCompiler.nuspec'
if ($runtimeNuspecPath.FullName -ne $privateNuspecPath) {
    Move-Item -LiteralPath $runtimeNuspecPath.FullName -Destination $privateNuspecPath -Force
}

$sdk = Join-Path $runtimeWork 'sdk'
$runtimeObjects = [ordered]@{
    'Runtime.WorkstationGC.lib' = Join-Path $runtimeBuild 'nativeaot\Runtime\Full\Runtime.WorkstationGC.lib'
    'Runtime.VxsortDisabled.lib' = Join-Path $runtimeBuild 'nativeaot\Runtime\Full\Runtime.VxsortDisabled.lib'
    'standalonegc-disabled.lib' = Join-Path $runtimeBuild 'nativeaot\Runtime\Full\standalonegc-disabled.lib'
    'zlibstatic.lib' = Join-Path $runtimeBuild '_deps\fetchzlibng-build\zlibstatic.lib'
}
$bootstrapObject = Get-ChildItem -LiteralPath (Join-Path $runtimeBuild 'nativeaot\Bootstrap\base\CMakeFiles\bootstrapper.dir') -Filter 'main.cpp.obj' -File -Recurse | Select-Object -First 1
if ($null -eq $bootstrapObject) { throw 'The target-native bootstrapper object was not produced.' }
$runtimeObjects['bootstrapper.obj'] = $bootstrapObject.FullName

$assets = [System.Collections.Generic.List[object]]::new()
foreach ($item in $runtimeObjects.GetEnumerator()) {
    if (-not (Test-Path -LiteralPath $item.Value -PathType Leaf)) { throw "Target asset is absent: $($item.Value)" }
    Copy-Item -LiteralPath $item.Value -Destination (Join-Path $sdk $item.Key) -Force
    $assets.Add([ordered]@{
        name = $item.Key
        class = if ($item.Key -eq 'Runtime.WorkstationGC.lib') { 'NativeAOT runtime + workstation GC' } elseif ($item.Key -eq 'bootstrapper.obj') { 'startup object' } elseif ($item.Key -eq 'zlibstatic.lib') { 'compiler/runtime helper library' } else { 'NativeAOT runtime helper library' }
        source = $item.Value
        sha256 = (Get-FileHash -LiteralPath (Join-Path $sdk $item.Key) -Algorithm SHA256).Hash.ToUpperInvariant()
    })
}

# The target package must not become a renamed copy of the full Windows
# runtime pack. Keep only the managed startup inputs that the Main() => 42
# proof can reach, and the native assets explicitly staged above. The host
# compiler remains in the separate Microsoft.DotNet.ILCompiler package and is
# resolved through the host RID.
$frameworkKeep = @{
    'System.Private.CoreLib.dll' = $true
    'System.Private.CoreLib.pdb' = $true
    'System.Private.CoreLib.xml' = $true
    'System.Private.DisabledReflection.dll' = $true
    'System.Private.DisabledReflection.pdb' = $true
    'System.Private.DisabledReflection.xml' = $true
    'System.Private.TypeLoader.dll' = $true
    'System.Private.TypeLoader.pdb' = $true
    'System.Private.TypeLoader.xml' = $true
    'System.Private.StackTraceMetadata.dll' = $true
    'System.Private.StackTraceMetadata.pdb' = $true
    'System.Private.StackTraceMetadata.xml' = $true
    'System.Runtime.dll' = $true
    'System.Runtime.Extensions.dll' = $true
    'System.Runtime.InteropServices.dll' = $true
}
$frameworkDirectory = Join-Path $runtimeWork 'framework'
if (Test-Path -LiteralPath $frameworkDirectory -PathType Container) {
    Get-ChildItem -LiteralPath $frameworkDirectory -File | Where-Object { -not $frameworkKeep.ContainsKey($_.Name) } | Remove-Item -Force
}
foreach ($unneededDirectory in @('mibc', 'tools', 'package', '_rels')) {
    $unneededPath = Join-Path $runtimeWork $unneededDirectory
    if (Test-Path -LiteralPath $unneededPath) { Remove-Item -LiteralPath $unneededPath -Recurse -Force }
}
Get-ChildItem -LiteralPath $sdk -File | Where-Object { $_.Name -notin @($runtimeObjects.Keys) } | Remove-Item -Force

$compilerArchive = Pack-Nupkg $compilerWork 'Microsoft.DotNet.ILCompiler' $runtimeVersion
$runtimeArchive = Pack-Nupkg $runtimeWork 'runtime.guidexos-x64.Microsoft.DotNet.ILCompiler' $runtimeVersion

$manifest = [ordered]@{
    schema = 1
    packageIdentity = 'runtime.guidexos-x64.Microsoft.DotNet.ILCompiler'
    target = 'guidexos-x64'
    targetOs = 'guidexos'
    architecture = 'x64'
    version = $runtimeVersion
    sourceCommit = $runtimeCommit
    sourcePatchRecord = (Resolve-Path (Join-Path $root 'out\dotnet\phase22-source-patch\phase22-source-patch-record.json')).Path
    palSchema = 1
    palSymbolCount = 20
    nativeCompiler = 'MSVC x64 cl.exe'
    objectFormat = 'COFF'
    archiveFormat = 'COFF static library (.lib)'
    finalLinker = 'MSVC link.exe (not reached by this staging step)'
    customSourceAdaptationComplete = $false
    stockWinX64Fallback = $false
    managedCoreLibAdaptationComplete = $false
    note = 'Preliminary target-native asset staging. Managed CoreLib and final NativeAOT linker contract remain fail-closed until validated.'
    packages = @(
        [ordered]@{ id = 'Microsoft.DotNet.ILCompiler'; version = $runtimeVersion; path = $compilerArchive; role = 'managed compiler/build targets; private runtime.json maps guidexos-x64' }
        [ordered]@{ id = 'runtime.guidexos-x64.Microsoft.DotNet.ILCompiler'; version = $runtimeVersion; path = $runtimeArchive; role = 'target-native runtime assets' }
    )
    assets = $assets
}
$manifestPath = Join-Path $OutputRoot 'guidexos-runtime-pack.manifest.json'
$manifest | ConvertTo-Json -Depth 16 | Set-Content -LiteralPath $manifestPath -Encoding UTF8
$manifest | ConvertTo-Json -Depth 8
Write-Output "Wrote $manifestPath"
