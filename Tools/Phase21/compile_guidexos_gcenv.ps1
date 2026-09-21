[CmdletBinding()]
param(
    [string]$SourceRoot = '',
    [string]$OutputRoot = '',
    [string]$StockCompileCommandsPath = ''
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
if ([string]::IsNullOrWhiteSpace($SourceRoot)) { $SourceRoot = Join-Path $root 'out\rt' }
if ([string]::IsNullOrWhiteSpace($OutputRoot)) { $OutputRoot = Join-Path $root 'out\dotnet\phase21-gc-compile' }
if ([string]::IsNullOrWhiteSpace($StockCompileCommandsPath)) {
    $StockCompileCommandsPath = Join-Path $SourceRoot 'artifacts\obj\coreclr\windows.x64.Release\compile_commands.json'
}

$lock = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'runtime-source.lock.json') -Raw | ConvertFrom-Json
if (-not (Test-Path -LiteralPath $SourceRoot -PathType Container)) { throw "Runtime source is absent: $SourceRoot" }
$actualCommit = (& git -C $SourceRoot rev-parse HEAD).Trim()
if ($actualCommit -ne [string]$lock.runtimeCommit) { throw "Runtime source mismatch. Expected $($lock.runtimeCommit), found $actualCommit." }
if (-not (Test-Path -LiteralPath $StockCompileCommandsPath -PathType Leaf)) { throw "Stock compile_commands.json is absent: $StockCompileCommandsPath" }

$stockEntries = Get-Content -LiteralPath $StockCompileCommandsPath -Raw | ConvertFrom-Json
$entry = @($stockEntries | Where-Object { [string]$_.file -match '(?i)[\\/]gc[\\/]windows[\\/]gcenv\.windows\.cpp$' }) | Select-Object -First 1
if ($null -eq $entry) { throw 'No stock compile command for gcenv.windows.cpp was found.' }

$sourceFile = Join-Path $SourceRoot 'src\coreclr\gc\guidexos\gcenv.guidexos.cpp'
if (-not (Test-Path -LiteralPath $sourceFile -PathType Leaf)) { throw "GUIDEXOS GC source is absent: $sourceFile" }
New-Item -ItemType Directory -Force -Path $OutputRoot | Out-Null
$objectFile = Join-Path $OutputRoot 'gcenv.guidexos.cpp.obj'
$command = [string]$entry.command
$stockSourceInCommand = ([string]$entry.file).Replace('/', '\')
$command = $command.Replace($stockSourceInCommand, $sourceFile)
$command = $command.Replace('TARGET_WINDOWS', 'TARGET_GUIDEXOS')
$command = $command.Replace('gcenv.windows.cpp', 'gcenv.guidexos.cpp')
$command = [regex]::Replace($command, '/Fo\S+', '/Fo"' + $objectFile + '"')
$command = [regex]::Replace($command, '/Fd\S+', '/Fd"' + (Join-Path $OutputRoot 'gcenv.guidexos.cpp.pdb') + '"')
$command = $command + ' /I"' + (Join-Path $SourceRoot 'src\coreclr\gc\env') + '" /I"' + (Join-Path $SourceRoot 'src\coreclr\nativeaot\Runtime\guidexos') + '"'

$vcvars = 'C:\Program Files\Microsoft Visual Studio\18\Community\VC\Auxiliary\Build\vcvars64.bat'
if (-not (Test-Path -LiteralPath $vcvars -PathType Leaf)) { throw "The required native x64 compiler environment was not found: $vcvars" }
$buildCommand = 'call "' + $vcvars + '" >nul && set "VisualStudioVersion=17.0" && set "SkipVCEnvInit=1" && ' + $command
$exitCode = 0
Push-Location $SourceRoot
try {
    & cmd.exe /d /s /c $buildCommand
    $exitCode = $LASTEXITCODE
}
finally { Pop-Location }

$compileEntry = [ordered]@{
    directory = (Resolve-Path $SourceRoot).Path.Replace('\', '/')
    command = $command
    file = $sourceFile.Replace('\', '/')
    output = $objectFile.Replace('\', '/')
}
$compileCommandsPath = Join-Path $OutputRoot 'compile_commands.json'
@($compileEntry) | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $compileCommandsPath -Encoding UTF8
$record = [ordered]@{
    schema = 1
    generatedBy = 'Tools/Phase21/compile_guidexos_gcenv.ps1'
    generatedAtUtc = [DateTime]::UtcNow.ToString('o')
    sourceCommit = $actualCommit
    source = $sourceFile
    stockCompileCommands = (Resolve-Path $StockCompileCommandsPath).Path
    command = $buildCommand
    exitCode = $exitCode
    object = $objectFile
    compileCommands = $compileCommandsPath
    windowsGcSourceExcluded = ($command -notmatch '(?i)gcenv\.windows\.cpp')
    targetDefinePresent = ($command -match '(?i)TARGET_GUIDEXOS')
    windowsTargetDefineAbsent = ($command -notmatch '(?i)TARGET_WINDOWS')
    targetWindowsHeaderDependencyAbsent = ((Get-Content -LiteralPath $sourceFile -Raw) -notmatch '(?i)windows\.h')
}
$recordPath = Join-Path $OutputRoot 'gcenv-compile-record.json'
$record | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $recordPath -Encoding UTF8
$record | ConvertTo-Json -Depth 5
Write-Output "Wrote $recordPath"
if ($exitCode -ne 0) { throw "GUIDEXOS GC environment compile failed with exit code $exitCode." }
