[CmdletBinding()]
param(
    [string]$OutputRoot = '',
    [Parameter(Mandatory = $true)][string]$NuGetFeed
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$runtimeRoot = Join-Path $root 'out\rt'
$runtimeCommitBefore = (& git -C $runtimeRoot rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0) { throw "Unable to read the pinned runtime commit: $runtimeRoot" }
$runtimeStatusBefore = @(& git -C $runtimeRoot status --porcelain)
if ($LASTEXITCODE -ne 0) { throw "Unable to read the pinned runtime status: $runtimeRoot" }
if ($runtimeStatusBefore.Count -ne 58) {
    throw "Expected the 58 preserved pinned-runtime status entries; found $($runtimeStatusBefore.Count)."
}
$runtimeStatusTextBefore = [string]::Join("`n", $runtimeStatusBefore)
$sourceCommit = (& git -C $root rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0) { throw "Unable to read the BUILD_INFRA source commit: $root" }
$project = Join-Path $root 'Tests\NativeAotUnwind\GcEhFixture\guideXOS.NativeAotGcEhFixture.csproj'
if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path $root 'out\dotnet\phase23r4-gc-eh-fixture-reproduction'
}
$OutputRoot = [IO.Path]::GetFullPath($OutputRoot)
if (Test-Path -LiteralPath $OutputRoot) {
    throw "Refusing to overwrite existing fixture evidence: $OutputRoot"
}
if (-not (Test-Path -LiteralPath $project -PathType Leaf)) {
    throw "Managed fixture project is absent: $project"
}
if (-not (Test-Path -LiteralPath $NuGetFeed -PathType Container)) {
    throw "The selected NuGet feed is absent: $NuGetFeed"
}

$runtimeCompilerPackage = Join-Path $NuGetFeed 'runtime.guidexos-x64.microsoft.dotnet.ilcompiler.9.0.0.nupkg'
$ilCompilerPackage = Join-Path $NuGetFeed 'microsoft.dotnet.ilcompiler.9.0.0.nupkg'
foreach ($package in @($runtimeCompilerPackage, $ilCompilerPackage)) {
    if (-not (Test-Path -LiteralPath $package -PathType Leaf)) {
        throw "Pinned NativeAOT package is absent: $package"
    }
}

$vsRoot = 'C:\Program Files\Microsoft Visual Studio\18\Community\VC\Tools\MSVC'
$linker = Get-ChildItem -LiteralPath $vsRoot -Filter link.exe -File -Recurse |
    Where-Object { $_.FullName -match '\\Hostx64\\x64\\link\.exe$' } |
    Sort-Object FullName -Descending | Select-Object -First 1
$libCreator = Get-ChildItem -LiteralPath $vsRoot -Filter lib.exe -File -Recurse |
    Where-Object { $_.FullName -match '\\Hostx64\\x64\\lib\.exe$' } |
    Sort-Object FullName -Descending | Select-Object -First 1
if ($null -eq $linker -or $null -eq $libCreator) {
    throw 'The pinned Visual Studio x64 link tools were not found.'
}
$nasm = Get-Command nasm.exe -ErrorAction Stop
$syscallSource = Join-Path $root 'Tools\Phase26\guidexos_phase26_syscall.asm'
if (-not (Test-Path -LiteralPath $syscallSource -PathType Leaf)) {
    throw "Fixture PAL syscall veneer source is absent: $syscallSource"
}

$publishDirectory = Join-Path $OutputRoot 'publish'
$baseIntermediate = Join-Path $OutputRoot 'obj\'
$outputPath = Join-Path $OutputRoot 'bin\'
$mapPath = Join-Path $OutputRoot 'gc-eh-fixture.map'
$syscallObject = Join-Path $OutputRoot 'guidexos_pal_syscall5_fixture.obj'
$packagesPath = Join-Path $OutputRoot 'nuget-cache'
$nugetConfig = Join-Path $OutputRoot 'NuGet.Config'
$recordPath = Join-Path $OutputRoot 'gc-eh-fixture-build.json'
New-Item -ItemType Directory -Force -Path $OutputRoot, $publishDirectory | Out-Null

& $nasm.Source -f win64 -o $syscallObject $syscallSource
if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $syscallObject -PathType Leaf)) {
    throw 'The fixture-only PAL syscall veneer could not be assembled.'
}

$escapedFeed = [System.Security.SecurityElement]::Escape((Resolve-Path $NuGetFeed).Path)
$globalNuGetCache = Join-Path $env:USERPROFILE '.nuget\packages'
if (-not (Test-Path -LiteralPath $globalNuGetCache -PathType Container)) {
    throw "Global package cache is absent: $globalNuGetCache"
}
$escapedGlobalCache = [System.Security.SecurityElement]::Escape((Resolve-Path $globalNuGetCache).Path)
$config = "<?xml version=`"1.0`" encoding=`"utf-8`"?><configuration><packageSources><clear/><add key=`"fixture-cache`" value=`"$escapedFeed`"/><add key=`"global-cache`" value=`"$escapedGlobalCache`"/></packageSources><packageSourceMapping><packageSource key=`"fixture-cache`"><package pattern=`"Microsoft.DotNet.ILCompiler`"/><package pattern=`"runtime.guidexos-x64.Microsoft.DotNet.ILCompiler`"/><package pattern=`"runtime.win-x64.Microsoft.DotNet.ILCompiler`"/></packageSource><packageSource key=`"global-cache`"><package pattern=`"*`"/></packageSource></packageSourceMapping></configuration>"
[IO.File]::WriteAllText($nugetConfig, $config, [Text.UTF8Encoding]::new($false))

$common = @(
    '-p:Phase23Guidexos=true',
    '-p:RuntimeIdentifier=guidexos-x64',
    '-p:RuntimeFrameworkVersion=9.0.0',
    '-p:TargetLatestRuntimePatch=false',
    '-p:UseAppHost=false',
    '-p:PublishAot=true',
    '-p:SelfContained=true',
    '-p:IlcUseEnvironmentalTools=true',
    '-p:UseSharedCompilation=false',
    "-p:Phase23LinkMap=$mapPath",
    "-p:Phase23R4SyscallObject=$syscallObject",
    "-p:CppLinker=$($linker.FullName)",
    "-p:CppLibCreator=$($libCreator.FullName)",
    "-p:BaseIntermediateOutputPath=$baseIntermediate",
    "-p:OutputPath=$outputPath",
    "-p:PublishDir=$publishDirectory\",
    "-p:RestorePackagesPath=$packagesPath",
    "-p:RuntimeIdentifierGraphPath=$(Join-Path $root 'Tools\Phase22\runtime-identifier.graph.json')",
    '-p:NuGetAudit=false',
    '-v:minimal'
)

$restoreLog = Join-Path $OutputRoot 'restore.log'
$restoreArgs = @('restore', $project, '--configfile', $nugetConfig, '--force-evaluate', '--no-cache') + $common
& dotnet @restoreArgs 2>&1 | Tee-Object -FilePath $restoreLog
if ($LASTEXITCODE -ne 0) {
    throw "NativeAOT fixture restore failed with exit code $LASTEXITCODE."
}

$publishLog = Join-Path $OutputRoot 'publish.log'
$publishArgs = @('publish', $project, '--no-restore') + $common
& dotnet @publishArgs 2>&1 | Tee-Object -FilePath $publishLog
if ($LASTEXITCODE -ne 0) {
    throw "NativeAOT fixture publish failed with exit code $LASTEXITCODE."
}

$artifact = Get-ChildItem -LiteralPath $publishDirectory -Filter '*.exe' -File | Select-Object -First 1
if ($null -eq $artifact) {
    throw "NativeAOT fixture output is absent: $publishDirectory"
}
$nativeObject = Join-Path $baseIntermediate 'Release\net9.0\guidexos-x64\native\guideXOS.NativeAotGcEhFixture.obj'
$ilcMap = Join-Path $baseIntermediate 'Release\net9.0\guidexos-x64\native\guideXOS.NativeAotGcEhFixture.map.xml'
foreach ($path in @($nativeObject, $ilcMap, $mapPath)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Expected generated NativeAOT evidence is absent: $path"
    }
}
$runtimeCommitAfter = (& git -C $runtimeRoot rev-parse HEAD).Trim()
$runtimeStatusAfter = @(& git -C $runtimeRoot status --porcelain)
if ($LASTEXITCODE -ne 0) { throw "Unable to verify the pinned runtime status: $runtimeRoot" }
if ($runtimeCommitAfter -ne $runtimeCommitBefore -or
    [string]::Join("`n", $runtimeStatusAfter) -cne $runtimeStatusTextBefore) {
    throw 'The pinned runtime checkout changed during fixture generation; preserving the incomplete evidence for inspection.'
}
$record = [ordered]@{
    schema = 1
    fixture = 'guideXOS.NativeAotGcEhFixture'
    generatedBy = 'Tools/Phase23/build_nativeaot_gc_eh_fixture.ps1'
    buildInfraSourceCommit = $sourceCommit
    managedSources = [ordered]@{
        project = [ordered]@{
            path = $project
            sha256 = (Get-FileHash -LiteralPath $project -Algorithm SHA256).Hash
        }
        program = [ordered]@{
            path = (Join-Path (Split-Path -Parent $project) 'Program.cs')
            sha256 = (Get-FileHash -LiteralPath (Join-Path (Split-Path -Parent $project) 'Program.cs') -Algorithm SHA256).Hash
        }
    }
    toolchain = [ordered]@{
        dotnetVersion = (& dotnet --version).Trim()
        linker = $linker.FullName
        libraryCreator = $libCreator.FullName
    }
    runtimeSource = [ordered]@{
        path = $runtimeRoot
        commit = $runtimeCommitBefore
        preexistingStatusEntries = $runtimeStatusBefore.Count
        unchangedDuringBuild = $true
    }
    target = 'guidexos-x64'
    targetOs = 'guidexos'
    nugetFeed = (Resolve-Path $NuGetFeed).Path
    packages = [ordered]@{
        ilCompiler = [ordered]@{
            path = $ilCompilerPackage
            sha256 = (Get-FileHash -LiteralPath $ilCompilerPackage -Algorithm SHA256).Hash
        }
        runtimeIlCompiler = [ordered]@{
            path = $runtimeCompilerPackage
            sha256 = (Get-FileHash -LiteralPath $runtimeCompilerPackage -Algorithm SHA256).Hash
        }
    }
    fixtureLinkSupport = [ordered]@{
        source = $syscallSource
        sourceSha256 = (Get-FileHash -LiteralPath $syscallSource -Algorithm SHA256).Hash
        object = $syscallObject
        objectSha256 = (Get-FileHash -LiteralPath $syscallObject -Algorithm SHA256).Hash
        purpose = 'Resolve the existing Phase 19 PAL syscall5 import for metadata-only fixture linking; the image is never executed.'
    }
    nativeObject = [ordered]@{
        path = $nativeObject
        length = (Get-Item -LiteralPath $nativeObject).Length
        sha256 = (Get-FileHash -LiteralPath $nativeObject -Algorithm SHA256).Hash
    }
    ilcMapXml = [ordered]@{
        path = $ilcMap
        length = (Get-Item -LiteralPath $ilcMap).Length
        sha256 = (Get-FileHash -LiteralPath $ilcMap -Algorithm SHA256).Hash
    }
    artifact = [ordered]@{
        path = $artifact.FullName
        length = $artifact.Length
        sha256 = (Get-FileHash -LiteralPath $artifact.FullName -Algorithm SHA256).Hash
    }
    map = [ordered]@{
        path = $mapPath
        length = (Get-Item -LiteralPath $mapPath).Length
        sha256 = (Get-FileHash -LiteralPath $mapPath -Algorithm SHA256).Hash
    }
    linkResponse = (Join-Path $baseIntermediate 'Release\net9.0\guidexos-x64\native\link.rsp')
    managedExecution = $false
}
$record | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $recordPath -Encoding UTF8
Write-Output (Get-Content -LiteralPath $recordPath -Raw)
