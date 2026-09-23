[CmdletBinding()]
param(
    [string]$SourceRoot = '',
    [string]$OutputRoot = '',
    [string]$BuildTarget = 'Runtime.WorkstationGC',
    [switch]$ConfigureOnly
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
if ([string]::IsNullOrWhiteSpace($SourceRoot)) { $SourceRoot = Join-Path $root 'out\rt' }
if ([string]::IsNullOrWhiteSpace($OutputRoot)) { $OutputRoot = Join-Path $root 'out\dotnet\phase22-native-runtime' }
$lock = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'runtime-source.lock.json') -Raw | ConvertFrom-Json
$patchRecordPath = Join-Path $root 'out\dotnet\phase22-source-patch\phase22-source-patch-record.json'
if (-not (Test-Path -LiteralPath $patchRecordPath -PathType Leaf)) { throw "Phase 22 patch record is absent: $patchRecordPath" }
if (-not (Test-Path -LiteralPath (Join-Path $SourceRoot 'src\coreclr\build-runtime.cmd') -PathType Leaf)) { throw "Runtime source is absent: $SourceRoot" }
$actualCommit = (& git -C $SourceRoot rev-parse HEAD).Trim()
if ($actualCommit -ne [string]$lock.runtimeCommit) { throw "Runtime source mismatch. Expected $($lock.runtimeCommit), found $actualCommit." }

$vcvars = 'C:\Program Files\Microsoft Visual Studio\18\Community\VC\Auxiliary\Build\vcvars64.bat'
if (-not (Test-Path -LiteralPath $vcvars -PathType Leaf)) { throw "The required native x64 compiler environment was not found: $vcvars" }
$cmake = 'C:\mingw64\bin\cmake.exe'
if (-not (Test-Path -LiteralPath $cmake -PathType Leaf)) { throw "The required CMake executable was not found: $cmake" }
New-Item -ItemType Directory -Force -Path $OutputRoot | Out-Null
$logPath = Join-Path $OutputRoot 'native-build.log'
$buildDirectory = Join-Path $SourceRoot 'artifacts\obj\coreclr\guidexos.x64.Release'
$configureCommand = 'call "' + $vcvars + '" >nul && set "VisualStudioVersion=17.0" && set "SkipVCEnvInit=1" && set "NumberOfCores=1" && build-runtime.cmd -x64 -Release -component nativeaot -os guidexos -outputrid guidexos-x64 -configureonly'
$buildCommand = $configureCommand
if (-not $ConfigureOnly) {
    if ([string]::IsNullOrWhiteSpace($BuildTarget)) { throw 'BuildTarget cannot be empty unless ConfigureOnly is specified.' }
    $buildCommand += ' && "' + $cmake + '" --build "' + $buildDirectory + '" --target ' + $BuildTarget + ' --config Release --clean-first --'
}

$exitCode = 0
Push-Location (Join-Path $SourceRoot 'src\coreclr')
try {
    $previousErrorActionPreference = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    & cmd.exe /d /s /c $buildCommand *> $logPath
    $exitCode = $LASTEXITCODE
    $ErrorActionPreference = $previousErrorActionPreference
    Get-Content -LiteralPath $logPath
}
finally { Pop-Location }

$buildDir = $buildDirectory
$compileCommands = Join-Path $buildDir 'compile_commands.json'
$entries = @()
if (Test-Path -LiteralPath $compileCommands -PathType Leaf) { $entries = @(Get-Content -LiteralPath $compileCommands -Raw | ConvertFrom-Json) }
$record = [ordered]@{
    schema = 1
    generatedBy = 'Tools/Phase22/build_guidexos_native_runtime.ps1'
    generatedAtUtc = [DateTime]::UtcNow.ToString('o')
    target = [string]$lock.target
    targetOs = [string]$lock.targetOsValue
    sourceCommit = $actualCommit
    sourcePatchRecord = (Resolve-Path $patchRecordPath).Path
    host = [ordered]@{ os = 'windows'; compiler = 'MSVC x64'; vcvars = $vcvars }
    command = $buildCommand
    buildTarget = if ($ConfigureOnly) { 'configure-only' } else { $BuildTarget }
    exitCode = $exitCode
    buildDirectory = $buildDir
    compileCommands = $compileCommands
    compileCommandCount = @($entries).Count
    windowsGcCompileEntries = @($entries | Where-Object { [string]$_.file -match '(?i)[\\/]gc[\\/]windows[\\/]gcenv\.windows\.cpp$' }).Count
    guidexosGcCompileEntries = @($entries | Where-Object { [string]$_.file -match '(?i)[\\/]gc[\\/]guidexos[\\/]gcenv\.guidexos\.cpp$' }).Count
    windowsPalCompileEntries = @($entries | Where-Object { [string]$_.file -match '(?i)[\\/]Runtime[\\/]windows[\\/]PalRedhawk' }).Count
    targetWindowsHeaderEntries = @($entries | Where-Object { [string]$_.command -match '(?i)windows\.h' }).Count
    status = if ($exitCode -eq 0) { 'completed' } else { 'failed' }
}

if (-not $ConfigureOnly -and $exitCode -eq 0) {
    $archivePath = Join-Path $buildDir 'nativeaot\Runtime\Full\Runtime.WorkstationGC.lib'
    if (Test-Path -LiteralPath $archivePath -PathType Leaf) {
        $membersPath = Join-Path $OutputRoot 'Runtime.WorkstationGC.members.txt'
        $symbolsPath = Join-Path $OutputRoot 'Runtime.WorkstationGC.symbols.txt'
        $unresolvedPath = Join-Path $OutputRoot 'Runtime.WorkstationGC.unresolved-lines.txt'

        $archiveCommand = 'call "' + $vcvars + '" >nul && lib /LIST "' + $archivePath + '" > "' + $membersPath + '" && dumpbin /symbols "' + $archivePath + '" > "' + $symbolsPath + '"'
        & cmd.exe /d /s /c $archiveCommand
        if ($LASTEXITCODE -ne 0) { throw "Native archive inspection failed for $archivePath" }

        Select-String -LiteralPath $symbolsPath -Pattern '\bUNDEF\b' |
            ForEach-Object Line |
            Set-Content -LiteralPath $unresolvedPath -Encoding UTF8

        $members = @(Get-Content -LiteralPath $membersPath | Where-Object { $_ -match '\.obj$' })
        $record.archive = [ordered]@{
            path = $archivePath
            size = (Get-Item -LiteralPath $archivePath).Length
            sha256 = (Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash.ToUpperInvariant()
            memberCount = $members.Count
            guidexosGcMembers = @($members | Where-Object { $_ -match '(?i)gc[\\/]guidexos[\\/]gcenv\.guidexos\.cpp' }).Count
            windowsGcMembers = @($members | Where-Object { $_ -match '(?i)gc[\\/]windows[\\/]gcenv\.windows\.cpp' }).Count
            windowsPalMembers = @($members | Where-Object { $_ -match '(?i)nativeaot[\\/]Runtime[\\/]windows[\\/]PalRedhawk' }).Count
            membersPath = $membersPath
            unresolvedSymbolsPath = $unresolvedPath
        }
    }
}
$recordPath = Join-Path $OutputRoot 'native-build-record.json'
$record | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $recordPath -Encoding UTF8
$record | ConvertTo-Json -Depth 6
Write-Output "Wrote $recordPath"
if ($exitCode -ne 0) { exit $exitCode }
