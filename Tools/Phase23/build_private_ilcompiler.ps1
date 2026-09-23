[CmdletBinding()]
param(
    [string]$RuntimeSourceRoot = '',
    [string]$OutputRoot = '',
    [string]$HostRuntimeToolsRoot = ''
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
if ([string]::IsNullOrWhiteSpace($RuntimeSourceRoot)) { $RuntimeSourceRoot = Join-Path $root 'out\rt' }
if ([string]::IsNullOrWhiteSpace($OutputRoot)) { $OutputRoot = Join-Path $root 'out\dotnet\phase23-private-ilc' }
if ([string]::IsNullOrWhiteSpace($HostRuntimeToolsRoot)) {
    $HostRuntimeToolsRoot = Join-Path $env:USERPROFILE '.nuget\packages\runtime.win-x64.microsoft.dotnet.ilcompiler\9.0.0\tools'
}

$runtimeCommit = '9d5a6a9aa463d6d10b0b0ba6d5982cc82f363dc3'
$patchRecordPath = Join-Path $root 'out\dotnet\phase23-source-patch\phase23-ilc-source-patch-record.json'
$sourceProject = Join-Path $RuntimeSourceRoot 'src\coreclr\tools\aot\ILCompiler\ILCompiler.csproj'
$hostJit = Join-Path $HostRuntimeToolsRoot 'jitinterface_x64.dll'
$ilcOutput = Join-Path $OutputRoot 'ilc-published'
$recordPath = Join-Path $OutputRoot 'private-ilc-build.json'

foreach ($path in @($RuntimeSourceRoot, $sourceProject, $HostRuntimeToolsRoot, $hostJit, $patchRecordPath)) {
    if (-not (Test-Path -LiteralPath $path)) { throw "Required private ILC input is absent: $path" }
}

$actualCommit = (& git -C $RuntimeSourceRoot rev-parse HEAD).Trim()
if ($actualCommit -ne $runtimeCommit) { throw "Runtime source mismatch: expected $runtimeCommit, found $actualCommit." }
$patchRecord = Get-Content -LiteralPath $patchRecordPath -Raw | ConvertFrom-Json
if ($patchRecord.patchIdentity -ne 'guidexos-ilc-target-v1') { throw 'The private ILC patch identity is not guidexos-ilc-target-v1.' }
if ($patchRecord.sourceCommit -ne $runtimeCommit) { throw 'The private ILC patch record has the wrong runtime source commit.' }

New-Item -ItemType Directory -Force -Path $OutputRoot | Out-Null

$commonProperties = @(
    '/p:DotNetBuildSourceOnly=true',
    '/p:TargetArchitecture=x64',
    '/p:BuildArchitecture=x64',
    '/p:HostOS=windows',
    '/p:TargetOS=windows',
    '/p:PackageRID=win-x64',
    '/p:RuntimeIdentifier=win-x64',
    '/p:PublishAot=false',
    '/p:PublishReadyToRun=false',
    '/p:PublishSingleFile=false',
    '/p:SelfContained=false',
    '/p:NativeAotSupported=true',
    '/p:NETCoreAppToolCurrent=net9.0',
    "/p:RuntimeBinDir=$OutputRoot\",
    "/p:CoreCLRArtifactsPath=$HostRuntimeToolsRoot\"
)

$commands = [System.Collections.Generic.List[object]]::new()
$firstArgs = @('build', $sourceProject, '-c', 'Release', '-v:minimal') + $commonProperties + @('/p:UseAppHost=false')
$commands.Add([ordered]@{ command = 'dotnet'; arguments = $firstArgs })
& dotnet @firstArgs
if ($LASTEXITCODE -ne 0) { throw "Private ILC source build failed with exit code $LASTEXITCODE." }

$secondArgs = @('build', $sourceProject, '-c', 'Release', '-v:minimal') + $commonProperties + @('/p:SelfContained=true', '/p:UseAppHost=true')
$commands.Add([ordered]@{ command = 'dotnet'; arguments = $secondArgs })
& dotnet @secondArgs
if ($LASTEXITCODE -ne 0) { throw "Private ILC apphost build failed with exit code $LASTEXITCODE." }

$ilcPath = Join-Path $ilcOutput 'ilc.exe'
if (-not (Test-Path -LiteralPath $ilcPath -PathType Leaf)) { throw "Private ILC apphost was not produced: $ilcPath" }

$record = [ordered]@{
    schema = 1
    generatedBy = 'Tools/Phase23/build_private_ilcompiler.ps1'
    target = 'guidexos-x64'
    targetOs = 'guidexos'
    runtimeSourceCommit = $runtimeCommit
    ilcPatchIdentity = $patchRecord.patchIdentity
    ilcPatchRecord = (Resolve-Path $patchRecordPath).Path
    ilcPatchRecordSha256 = (Get-FileHash -LiteralPath $patchRecordPath -Algorithm SHA256).Hash.ToUpperInvariant()
    sourceProject = $sourceProject
    hostRuntimeTools = $HostRuntimeToolsRoot
    hostJitInterface = [ordered]@{
        path = $hostJit
        sha256 = (Get-FileHash -LiteralPath $hostJit -Algorithm SHA256).Hash.ToUpperInvariant()
    }
    privateCompiler = [ordered]@{
        path = $ilcPath
        sha256 = (Get-FileHash -LiteralPath $ilcPath -Algorithm SHA256).Hash.ToUpperInvariant()
        length = (Get-Item -LiteralPath $ilcPath).Length
    }
    buildProperties = $commonProperties
    commands = $commands
    installedSdkModified = $false
    note = 'The host JIT/runtime files are host-only compiler inputs. Target runtime assets are supplied by the separate GUIDEXOS package staging step.'
}
$record | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $recordPath -Encoding UTF8
$record | ConvertTo-Json -Depth 8
Write-Output "Wrote $recordPath"
