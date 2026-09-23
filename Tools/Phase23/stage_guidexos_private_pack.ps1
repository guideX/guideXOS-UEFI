[CmdletBinding()]
param(
    [string]$RuntimeSourceRoot = '',
    [string]$PrivateIlcRoot = '',
    [string]$OutputRoot = '',
    [string]$RuntimePackVersion = '9.0.0'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
if ([string]::IsNullOrWhiteSpace($RuntimeSourceRoot)) { $RuntimeSourceRoot = Join-Path $root 'out\rt' }
if ([string]::IsNullOrWhiteSpace($PrivateIlcRoot)) { $PrivateIlcRoot = Join-Path $root 'out\dotnet\phase23-private-ilc' }
if ([string]::IsNullOrWhiteSpace($OutputRoot)) { $OutputRoot = Join-Path $root 'out\dotnet\phase23-runtime-pack' }

$runtimeCommit = '9d5a6a9aa463d6d10b0b0ba6d5982cc82f363dc3'
$runtimeBuild = Join-Path $RuntimeSourceRoot 'artifacts\obj\coreclr\guidexos.x64.Release'
$privateIlc = Join-Path $PrivateIlcRoot 'ilc-published'
$privateIlcRecord = Join-Path $PrivateIlcRoot 'private-ilc-build.json'
$phase19PalObject = Join-Path $root 'out\dotnet\phase19-runtime-pack\guidexos_nativeaot_pal_contract.obj'
$phase19PalLib = Join-Path $root 'out\dotnet\phase19-runtime-pack\guidexos_nativeaot_pal_contract.lib'
$phase19PalContractPath = Join-Path $root 'Tools\Phase19\pal-contract.json'
$linkShimObject = Join-Path $OutputRoot 'guidexos_link_shim.obj'
$compilerPackageCache = Join-Path $env:USERPROFILE ".nuget\packages\microsoft.dotnet.ilcompiler\$RuntimePackVersion"
$runtimePackageCache = Join-Path $env:USERPROFILE ".nuget\packages\runtime.win-x64.microsoft.dotnet.ilcompiler\$RuntimePackVersion"
$work = Join-Path $OutputRoot '_work'
$packageSource = Join-Path $OutputRoot 'packages'
$compilerPackageId = 'Microsoft.DotNet.ILCompiler'
$runtimePackageId = 'runtime.guidexos-x64.Microsoft.DotNet.ILCompiler'

foreach ($path in @($runtimeBuild, $privateIlc, $privateIlcRecord, $phase19PalObject, $phase19PalLib, $phase19PalContractPath, $compilerPackageCache, $runtimePackageCache)) {
    if (-not (Test-Path -LiteralPath $path)) { throw "Required Phase 23 input is absent: $path" }
}
& powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'build_guidexos_link_shim.ps1') -OutputRoot $OutputRoot
if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $linkShimObject -PathType Leaf)) { throw 'The GUIDEXOS link shim was not produced.' }

$actualCommit = (& git -C $RuntimeSourceRoot rev-parse HEAD).Trim()
if ($actualCommit -ne $runtimeCommit) { throw "Runtime source mismatch: expected $runtimeCommit, found $actualCommit." }
$privateIlcRecordObject = Get-Content -LiteralPath $privateIlcRecord -Raw | ConvertFrom-Json
if ($privateIlcRecordObject.ilcPatchIdentity -ne 'guidexos-ilc-target-v1') { throw 'The private ILC build record has the wrong patch identity.' }
$phase19PalContract = Get-Content -LiteralPath $phase19PalContractPath -Raw | ConvertFrom-Json
$phase19PalSymbols = @(
    @($phase19PalContract.categories | ForEach-Object { $_.symbols | ForEach-Object { $_.name } })
    @($phase19PalContract.localHelpers | ForEach-Object { $_.name })
)

if (Test-Path -LiteralPath $work) { Remove-Item -LiteralPath $work -Recurse -Force }
if (Test-Path -LiteralPath $packageSource) { Remove-Item -LiteralPath $packageSource -Recurse -Force }
New-Item -ItemType Directory -Force -Path $work, $packageSource | Out-Null

function Expand-Nupkg([string]$packagePath, [string]$destination) {
    New-Item -ItemType Directory -Force -Path $destination | Out-Null
    $zipPath = Join-Path $destination '__package.zip'
    Copy-Item -LiteralPath $packagePath -Destination $zipPath -Force
    Expand-Archive -LiteralPath $zipPath -DestinationPath $destination -Force
    Remove-Item -LiteralPath $zipPath -Force
}

function Pack-Nupkg([string]$source, [string]$packageId, [string]$version) {
    $nuspec = Get-ChildItem -LiteralPath $source -Filter '*.nuspec' -File | Select-Object -First 1
    if ($null -eq $nuspec) { throw "Package nuspec is absent: $source" }
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

function Replace-Text([string]$path, [string]$old, [string]$new) {
    $text = Get-Content -LiteralPath $path -Raw
    if (-not $text.Contains($old)) { throw "Expected packaging seam was absent in `${path}: $old" }
    [IO.File]::WriteAllText($path, $text.Replace($old, $new), [Text.UTF8Encoding]::new($false))
}

function Replace-TextOptional([string]$path, [string]$old, [string]$new) {
    $text = Get-Content -LiteralPath $path -Raw
    if ($text.Contains($old)) {
        [IO.File]::WriteAllText($path, $text.Replace($old, $new), [Text.UTF8Encoding]::new($false))
        return $true
    }
    return $false
}

$compilerWork = Join-Path $work 'compiler'
$runtimeWork = Join-Path $work 'runtime'
$hostWork = Join-Path $work 'host'
Expand-Nupkg (Join-Path $compilerPackageCache 'microsoft.dotnet.ilcompiler.9.0.0.nupkg') $compilerWork
Expand-Nupkg (Join-Path $runtimePackageCache 'runtime.win-x64.microsoft.dotnet.ilcompiler.9.0.0.nupkg') $runtimeWork
Expand-Nupkg (Join-Path $runtimePackageCache 'runtime.win-x64.microsoft.dotnet.ilcompiler.9.0.0.nupkg') $hostWork

# The private compiler is a host tool. Replace only its executable/tool payload;
# the package build tasks and package identity remain the stock 9.0.0 contract.
$compilerTools = Join-Path $compilerWork 'tools'
$buildTasksBackup = Join-Path $work 'buildtasks'
Copy-Item -LiteralPath (Join-Path $compilerTools 'netstandard') -Destination $buildTasksBackup -Recurse -Force
if (Test-Path -LiteralPath $compilerTools) { Remove-Item -LiteralPath $compilerTools -Recurse -Force }
Copy-Item -LiteralPath $privateIlc -Destination $compilerTools -Recurse -Force
New-Item -ItemType Directory -Force -Path (Join-Path $compilerTools 'netstandard') | Out-Null
Copy-Item -Path (Join-Path $buildTasksBackup '*') -Destination (Join-Path $compilerTools 'netstandard') -Recurse -Force

# The SDK uses the host RID package for IlcToolsPath.  Keep that package
# explicitly host-only, but replace its executable payload with the same
# source-built private compiler so stock ILC cannot enter the build through
# the host-tool lookup.
$hostTools = Join-Path $hostWork 'tools'
if (Test-Path -LiteralPath $hostTools) { Remove-Item -LiteralPath $hostTools -Recurse -Force }
Copy-Item -LiteralPath $privateIlc -Destination $hostTools -Recurse -Force

$runtimeJsonPath = Join-Path $compilerWork 'runtime.json'
$runtimeJson = Get-Content -LiteralPath $runtimeJsonPath -Raw | ConvertFrom-Json
if ($null -ne $runtimeJson.runtimes.PSObject.Properties['guidexos-x64']) {
    $runtimeJson.runtimes.PSObject.Properties.Remove('guidexos-x64')
}
$runtimeJson.runtimes | Add-Member -NotePropertyName 'guidexos-x64' -NotePropertyValue ([pscustomobject]@{
    '#import' = @()
    'Microsoft.DotNet.ILCompiler' = [pscustomobject]@{ $runtimePackageId = "[$RuntimePackVersion, )" }
})
$runtimeJson | ConvertTo-Json -Depth 16 | Set-Content -LiteralPath $runtimeJsonPath -Encoding UTF8

# The stock target file hard-codes Unix versus Windows in the object writer
# and final-link target. GUIDEXOS gets the PE/COFF side of those decisions,
# then imports its own stripped linker contract.
$nativeTargets = Join-Path $compilerWork 'build\Microsoft.NETCore.Native.targets'
$publishTargets = Join-Path $compilerWork 'build\Microsoft.NETCore.Native.Publish.targets'
Replace-Text $publishTargets "and '`$(_targetOS)' != 'win'`"`r`n      Text=`"Cross-OS native compilation is not supported.`"" "and '`$(_targetOS)' != 'win' and '`$(_targetOS)' != 'guidexos'`"`r`n      Text=`"Cross-OS native compilation is not supported.`""
Replace-Text $nativeTargets "<NativeObjectExt Condition=`"'`$(_targetOS)' == 'win'`">.obj</NativeObjectExt>" "<NativeObjectExt Condition=`"'`$(_targetOS)' == 'win' or '`$(_targetOS)' == 'guidexos'`">.obj</NativeObjectExt>"
Replace-Text $nativeTargets "<NativeObjectExt Condition=`"'`$(_targetOS)' != 'win'`">.o</NativeObjectExt>" "<NativeObjectExt Condition=`"'`$(_targetOS)' != 'win' and '`$(_targetOS)' != 'guidexos'`">.o</NativeObjectExt>"
Replace-Text $nativeTargets "<LibFileExt Condition=`"'`$(_targetOS)' == 'win'`">.lib</LibFileExt>" "<LibFileExt Condition=`"'`$(_targetOS)' == 'win' or '`$(_targetOS)' == 'guidexos'`">.lib</LibFileExt>"
Replace-Text $nativeTargets "<LibFileExt Condition=`"'`$(_targetOS)' != 'win'`">.a</LibFileExt>" "<LibFileExt Condition=`"'`$(_targetOS)' != 'win' and '`$(_targetOS)' != 'guidexos'`">.a</LibFileExt>"
Replace-Text $nativeTargets "<NativeBinaryExt Condition=`"'`$(IsNativeExecutable)' == 'true' and '`$(_targetOS)' == 'win'`">.exe</NativeBinaryExt>" "<NativeBinaryExt Condition=`"'`$(IsNativeExecutable)' == 'true' and ('`$(_targetOS)' == 'win' or '`$(_targetOS)' == 'guidexos')`">.exe</NativeBinaryExt>"
Replace-Text $nativeTargets "<NativeBinaryExt Condition=`"'`$(IsNativeExecutable)' == 'true' and '`$(_targetOS)' != 'win'`"></NativeBinaryExt>" "<NativeBinaryExt Condition=`"'`$(IsNativeExecutable)' == 'true' and '`$(_targetOS)' != 'win' and '`$(_targetOS)' != 'guidexos'`"></NativeBinaryExt>"
Replace-Text $nativeTargets "<NativeBinaryExt Condition=`"'`$(IsNativeExecutable)' != 'true' and '`$(_targetOS)' == 'win' and '`$(NativeLib)' == 'Shared'`">.dll</NativeBinaryExt>" "<NativeBinaryExt Condition=`"'`$(IsNativeExecutable)' != 'true' and ('`$(_targetOS)' == 'win' or '`$(_targetOS)' == 'guidexos') and '`$(NativeLib)' == 'Shared'`">.dll</NativeBinaryExt>"
Replace-Text $nativeTargets "<NativeBinaryExt Condition=`"'`$(IsNativeExecutable)' != 'true' and '`$(_targetOS)' != 'win' and '`$(_IsApplePlatform)' != 'true' and '`$(NativeLib)' == 'Shared'`">.so</NativeBinaryExt>" "<NativeBinaryExt Condition=`"'`$(IsNativeExecutable)' != 'true' and '`$(_targetOS)' != 'win' and '`$(_targetOS)' != 'guidexos' and '`$(_IsApplePlatform)' != 'true' and '`$(NativeLib)' == 'Shared'`">.so</NativeBinaryExt>"
Replace-Text $nativeTargets "<NativeBinaryExt Condition=`"'`$(IsNativeExecutable)' != 'true' and '`$(_targetOS)' == 'win' and '`$(NativeLib)' == 'Static'`">.lib</NativeBinaryExt>" "<NativeBinaryExt Condition=`"'`$(IsNativeExecutable)' != 'true' and ('`$(_targetOS)' == 'win' or '`$(_targetOS)' == 'guidexos') and '`$(NativeLib)' == 'Static'`">.lib</NativeBinaryExt>"
Replace-Text $nativeTargets "<NativeBinaryExt Condition=`"'`$(IsNativeExecutable)' != 'true' and '`$(_targetOS)' != 'win' and '`$(NativeLib)' == 'Static'`">.a</NativeBinaryExt>" "<NativeBinaryExt Condition=`"'`$(IsNativeExecutable)' != 'true' and '`$(_targetOS)' != 'win' and '`$(_targetOS)' != 'guidexos' and '`$(NativeLib)' == 'Static'`">.a</NativeBinaryExt>"
Replace-Text $nativeTargets "<NativeSymbolExt Condition=`"'`$(NativeSymbolExt)' == '' and '`$(_targetOS)' == 'win'`">.pdb</NativeSymbolExt>" "<NativeSymbolExt Condition=`"'`$(NativeSymbolExt)' == '' and ('`$(_targetOS)' == 'win' or '`$(_targetOS)' == 'guidexos')`">.pdb</NativeSymbolExt>"
Replace-Text $nativeTargets "<ExportsFileExt Condition=`"'`$(_targetOS)' == 'win'`">.def</ExportsFileExt>" "<ExportsFileExt Condition=`"'`$(_targetOS)' == 'win' or '`$(_targetOS)' == 'guidexos'`">.def</ExportsFileExt>"
Replace-Text $nativeTargets "<ExportsFileExt Condition=`"'`$(_targetOS)' != 'win'`">.exports</ExportsFileExt>" "<ExportsFileExt Condition=`"'`$(_targetOS)' != 'win' and '`$(_targetOS)' != 'guidexos'`">.exports</ExportsFileExt>"
Replace-Text $nativeTargets "<SharedLibrary Condition=`"'`$(_targetOS)' == 'win'`">`$(FrameworkLibPath)\Framework`$(LibFileExt)</SharedLibrary>" "<SharedLibrary Condition=`"'`$(_targetOS)' == 'win' or '`$(_targetOS)' == 'guidexos'`">`$(FrameworkLibPath)\Framework`$(LibFileExt)</SharedLibrary>"
Replace-Text $nativeTargets "<SharedLibrary Condition=`"'`$(_targetOS)' != 'win'`">`$(FrameworkLibPath)\libframework`$(LibFileExt)</SharedLibrary>" "<SharedLibrary Condition=`"'`$(_targetOS)' != 'win' and '`$(_targetOS)' != 'guidexos'`">`$(FrameworkLibPath)\libframework`$(LibFileExt)</SharedLibrary>"
Replace-Text $nativeTargets "  <Import Project=`"`$(MSBuildThisFileDirectory)Microsoft.NETCore.Native.Unix.targets`" Condition=`"'`$(_targetOS)' != 'win'`" />" "  <Import Project=`"`$(MSBuildThisFileDirectory)Microsoft.NETCore.Native.Guidexos.targets`" Condition=`"'`$(_targetOS)' == 'guidexos'`" />`n  <Import Project=`"`$(MSBuildThisFileDirectory)Microsoft.NETCore.Native.Unix.targets`" Condition=`"'`$(_targetOS)' != 'win' and '`$(_targetOS)' != 'guidexos'`" />"

# The preceding exact replacements cover the format properties.  LinkNative
# has a few repeated conditions; apply only to the linker target block.
$nativeText = Get-Content -LiteralPath $nativeTargets -Raw
$beforeLink = $nativeText.IndexOf('  <Target Name="LinkNative"')
$afterLink = $nativeText.IndexOf('  <Target Name="CreateLib"')
if ($beforeLink -lt 0 -or $afterLink -lt 0) { throw 'Unable to locate LinkNative target in the private package.' }
$prefix = $nativeText.Substring(0, $beforeLink)
$linkBlock = $nativeText.Substring($beforeLink, $afterLink - $beforeLink)
$suffix = $nativeText.Substring($afterLink)
$linkBlock = $linkBlock.Replace("'`$(_targetOS)' != 'win'", "'`$(_targetOS)' != 'win' and '`$(_targetOS)' != 'guidexos'")
$linkBlock = $linkBlock.Replace("'`$(_targetOS)' == 'win'", "('`$(_targetOS)' == 'win' or '`$(_targetOS)' == 'guidexos')")
$nativeText = $prefix + $linkBlock + $suffix
[IO.File]::WriteAllText($nativeTargets, $nativeText, [Text.UTF8Encoding]::new($false))

$createLibStart = $nativeText.IndexOf('  <Target Name="CreateLib"')
$publishStart = $nativeText.IndexOf('  <Import Project="$(MSBuildThisFileDirectory)Microsoft.NETCore.Native.Publish.targets"')
if ($createLibStart -lt 0 -or $publishStart -lt 0) { throw 'Unable to locate CreateLib target in the private package.' }
$prefix = $nativeText.Substring(0, $createLibStart)
$createBlock = $nativeText.Substring($createLibStart, $publishStart - $createLibStart)
$suffix = $nativeText.Substring($publishStart)
$createBlock = $createBlock.Replace("'`$(_targetOS)' != 'win'", "'`$(_targetOS)' != 'win' and '`$(_targetOS)' != 'guidexos'")
$createBlock = $createBlock.Replace("'`$(_targetOS)' == 'win'", "('`$(_targetOS)' == 'win' or '`$(_targetOS)' == 'guidexos')")
$nativeText = $prefix + $createBlock + $suffix
[IO.File]::WriteAllText($nativeTargets, $nativeText, [Text.UTF8Encoding]::new($false))

Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'runtime\Microsoft.NETCore.Native.Guidexos.targets') -Destination (Join-Path $compilerWork 'build\Microsoft.NETCore.Native.Guidexos.targets') -Force

$sdk = Join-Path $runtimeWork 'sdk'
$runtimeObjects = [ordered]@{
    'Runtime.WorkstationGC.lib' = Join-Path $runtimeBuild 'nativeaot\Runtime\Full\Runtime.WorkstationGC.lib'
    'Runtime.VxsortDisabled.lib' = Join-Path $runtimeBuild 'nativeaot\Runtime\Full\Runtime.VxsortDisabled.lib'
    'standalonegc-disabled.lib' = Join-Path $runtimeBuild 'nativeaot\Runtime\Full\standalonegc-disabled.lib'
    'zlibstatic.lib' = Join-Path $runtimeBuild '_deps\fetchzlibng-build\zlibstatic.lib'
    'guidexos_nativeaot_pal_contract.obj' = $phase19PalObject
    'guidexos_link_shim.obj' = $linkShimObject
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
        class = if ($item.Key -eq 'Runtime.WorkstationGC.lib') { 'NativeAOT runtime + workstation GC' } elseif ($item.Key -eq 'bootstrapper.obj') { 'startup object' } elseif ($item.Key -eq 'guidexos_nativeaot_pal_contract.obj') { 'Phase 19 PAL ABI object' } elseif ($item.Key -eq 'guidexos_link_shim.obj') { 'single GUIDEXOS legacy-symbol veneer' } elseif ($item.Key -eq 'zlibstatic.lib') { 'compiler/runtime helper library' } else { 'NativeAOT runtime helper library' }
        source = $item.Value
        sha256 = (Get-FileHash -LiteralPath (Join-Path $sdk $item.Key) -Algorithm SHA256).Hash.ToUpperInvariant()
    })
}

# Retain only the bounded framework closure used by the first proof. These are
# target managed inputs; the compiler host remains in the separate compiler
# package and is never treated as a target runtime dependency.
$frameworkKeep = @{
    'System.Private.CoreLib.dll' = $true; 'System.Private.CoreLib.pdb' = $true; 'System.Private.CoreLib.xml' = $true
    'System.Private.DisabledReflection.dll' = $true; 'System.Private.DisabledReflection.pdb' = $true; 'System.Private.DisabledReflection.xml' = $true
    'System.Private.TypeLoader.dll' = $true; 'System.Private.TypeLoader.pdb' = $true; 'System.Private.TypeLoader.xml' = $true
    'System.Private.StackTraceMetadata.dll' = $true; 'System.Private.StackTraceMetadata.pdb' = $true; 'System.Private.StackTraceMetadata.xml' = $true
    'System.Runtime.dll' = $true; 'System.Runtime.Extensions.dll' = $true; 'System.Runtime.InteropServices.dll' = $true
}
$frameworkDirectory = Join-Path $runtimeWork 'framework'
if (Test-Path -LiteralPath $frameworkDirectory -PathType Container) {
    Get-ChildItem -LiteralPath $frameworkDirectory -File | Where-Object { -not $frameworkKeep.ContainsKey($_.Name) } | Remove-Item -Force
}
foreach ($unneededDirectory in @('mibc', 'tools', 'package', '_rels')) {
    $unneededPath = Join-Path $runtimeWork $unneededDirectory
    if (Test-Path -LiteralPath $unneededPath) { Remove-Item -LiteralPath $unneededPath -Recurse -Force }
}
$sdkManagedKeep = @{
    'System.Private.CoreLib.dll' = $true; 'System.Private.CoreLib.pdb' = $true; 'System.Private.CoreLib.xml' = $true
    'System.Private.DisabledReflection.dll' = $true; 'System.Private.DisabledReflection.pdb' = $true; 'System.Private.DisabledReflection.xml' = $true
    'System.Private.TypeLoader.dll' = $true; 'System.Private.TypeLoader.pdb' = $true; 'System.Private.TypeLoader.xml' = $true
    'System.Private.StackTraceMetadata.dll' = $true; 'System.Private.StackTraceMetadata.pdb' = $true; 'System.Private.StackTraceMetadata.xml' = $true
}
Get-ChildItem -LiteralPath $sdk -File | Where-Object { $_.Name -notin @($runtimeObjects.Keys) -and -not $sdkManagedKeep.ContainsKey($_.Name) } | Remove-Item -Force

$compilerArchive = Pack-Nupkg $compilerWork $compilerPackageId $RuntimePackVersion
$runtimeArchive = Pack-Nupkg $runtimeWork $runtimePackageId $RuntimePackVersion
$hostRuntimeArchive = Pack-Nupkg $hostWork 'runtime.win-x64.Microsoft.DotNet.ILCompiler' $RuntimePackVersion

$manifest = [ordered]@{
    schema = 2
    packageIdentity = $runtimePackageId
    compilerPackageIdentity = $compilerPackageId
    target = 'guidexos-x64'
    targetOs = 'guidexos'
    architecture = 'x64'
    version = $RuntimePackVersion
    sourceCommit = $runtimeCommit
    privateIlcPatchIdentity = $privateIlcRecordObject.ilcPatchIdentity
    privateIlcBuildRecord = (Resolve-Path $privateIlcRecord).Path
    phase19PalSchema = 1
    phase19PalSymbolCount = 20
    phase19PalSymbols = $phase19PalSymbols
    phase17DescriptorVersion = 1
    nativeCompiler = 'MSVC x64 cl.exe'
    objectFormat = 'COFF'
    callingConvention = 'Microsoft x64 ABI'
    finalLinker = 'MSVC link.exe'
    defaultLibraries = 'disabled via /NODEFAULTLIB'
    sdkNativeLibraries = @()
    directPInvokes = @()
    entryPoint = 'wmain'
    stockWinX64Fallback = $false
    customSourceAdaptationComplete = $true
    managedCoreLibAdaptationComplete = $false
    tlsModel = 'X3: user GS base owns a guideXOS TLS vector at offset 0x58; vector[_tls_index] selects the process/thread-private runtime block; the loader owns initialization and cleanup; no Windows TLS imports'
    flsModel = 'Phase 19 process-private slot table in the current user-runtime thread block; cleanup is a future user-thread teardown action'
    threadStaticModel = 'NativeAOT inlined thread statics use the guideXOS GS vector and _tls_index; current-thread state is user-runtime memory, never a kernel scheduler object'
    note = 'Private compiler and target pack are explicit. CoreLib remains the next reachability-driven closure to validate; this pack is not an execution authorization. PE TLS directory emission is intentionally not used; the Phase 17 loader descriptor and future bootstrap own TLS construction.'
    packages = @(
        [ordered]@{ id = $compilerPackageId; version = $RuntimePackVersion; path = $compilerArchive; role = 'private host compiler and GUIDEXOS build/link targets' }
        [ordered]@{ id = $runtimePackageId; version = $RuntimePackVersion; path = $runtimeArchive; role = 'target-native GUIDEXOS runtime assets' }
        [ordered]@{ id = 'runtime.win-x64.Microsoft.DotNet.ILCompiler'; version = $RuntimePackVersion; path = $hostRuntimeArchive; role = 'host-only Windows RID compiler support; never selected as target runtime' }
    )
    assets = $assets
}
$manifestPath = Join-Path $OutputRoot 'guidexos-runtime-pack.manifest.json'
$manifest | ConvertTo-Json -Depth 16 | Set-Content -LiteralPath $manifestPath -Encoding UTF8
$manifest | ConvertTo-Json -Depth 8
Write-Output "Wrote $manifestPath"
