[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('Preflight', 'Build', 'Verify')]
    [string]$Mode,

    [Parameter(Mandatory = $true)]
    [string]$WorkspaceRoot,

    [ValidateSet('success', 'title-failure', 'body-failure', 'invalid-type', 'failfast')]
    [string]$Variant = 'success',

    [Parameter(Mandatory = $true)]
    [string]$Phase23Pack,

    [Parameter(Mandatory = $true)]
    [string]$PackageEvidence
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
# Phase scripts run Python helpers that import tracked modules. Keep their
# bytecode caches out of the repository worktree during a BUILD1C run.
$env:PYTHONDONTWRITEBYTECODE = '1'

$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$WorkspaceRoot = [IO.Path]::GetFullPath($WorkspaceRoot)
$Phase23Pack = [IO.Path]::GetFullPath($Phase23Pack)
$PackageEvidence = [IO.Path]::GetFullPath($PackageEvidence)
$pythonScript = Join-Path $PSScriptRoot 'build1c_trusted.py'
$pythonCommand = Join-Path $env:USERPROFILE '.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe'
if (-not (Test-Path -LiteralPath $pythonCommand -PathType Leaf)) {
    $python = Get-Command python -ErrorAction SilentlyContinue
    if ($null -eq $python) { throw 'Python 3 is required for deterministic BUILD1C validation.' }
    $pythonCommand = $python.Source
}
$powershellCommand = Join-Path ([Environment]::GetFolderPath('Windows')) 'System32\WindowsPowerShell\v1.0\powershell.exe'

function Invoke-Contract([string]$ContractMode) {
    $arguments = @(
        $pythonScript,
        $ContractMode,
        '--root', $root,
        '--workspace', $WorkspaceRoot,
        '--phase23-pack', $Phase23Pack,
        '--package-evidence', $PackageEvidence,
        '--variant', $Variant
    )
    $output = & $pythonCommand -B @arguments
    $exitCode = $LASTEXITCODE
    if ($exitCode -ne 0) {
        if ($output) { $output | ForEach-Object { Write-Output $_ } }
        throw "BUILD1C $ContractMode validation failed with exit code $exitCode."
    }
    return (($output -join "`n") | ConvertFrom-Json)
}

if ($Mode -eq 'Preflight') {
    $report = Invoke-Contract 'preflight'
    $report | ConvertTo-Json -Depth 30
    exit 0
}

if ($Mode -eq 'Verify') {
    $report = Invoke-Contract 'verify'
    $report | ConvertTo-Json -Depth 40
    exit 0
}

$preflightArguments = @(
    $pythonScript,
    'preflight',
    '--root', $root,
    '--workspace', $WorkspaceRoot,
    '--phase23-pack', $Phase23Pack,
    '--package-evidence', $PackageEvidence,
    '--variant', $Variant
)
$preflightOutput = & $pythonCommand -B @preflightArguments
$preflightExitCode = $LASTEXITCODE
if ($preflightExitCode -ne 0) {
    if ($preflightOutput) { $preflightOutput | ForEach-Object { Write-Output $_ } }
    throw "BUILD1C preflight failed with exit code $preflightExitCode."
}
$preflightJson = ($preflightOutput -join "`n") | ConvertFrom-Json
if ($preflightJson.ready -ne $true) { throw 'BUILD1C preflight did not authorize the build.' }

$evidenceRoot = Join-Path $WorkspaceRoot 'evidence'
$buildParent = Join-Path $WorkspaceRoot 'build'
New-Item -ItemType Directory -Path $evidenceRoot -Force | Out-Null
New-Item -ItemType Directory -Path $buildParent -Force | Out-Null
$preflightPath = Join-Path $evidenceRoot 'preflight.json'
$preflightJson | ConvertTo-Json -Depth 30 | Set-Content -LiteralPath $preflightPath -Encoding UTF8

$buildRoot = Join-Path $buildParent 'phase29'
$shimPath = Join-Path $buildParent 'phase29-generated-link-shim.cpp'
$buildScript = Join-Path $root 'Tools\Phase29\build_phase29_managed_notification.ps1'
$stageScript = Join-Path $root 'Tools\Phase29\stage_phase29_image.ps1'
$buildLog = Join-Path $evidenceRoot 'phase29-build.log'
$stageLog = Join-Path $evidenceRoot 'phase29-stage.log'

Push-Location $root
try {
    & $powershellCommand -NoProfile -ExecutionPolicy Bypass -File $buildScript `
        -Phase23Pack $Phase23Pack -OutputRoot $buildRoot -Variant $Variant -ShimSourcePath $shimPath 2>&1 |
        Tee-Object -FilePath $buildLog
    if ($LASTEXITCODE -ne 0) { throw "Supported Phase 29 builder failed with exit code $LASTEXITCODE." }

    & $powershellCommand -NoProfile -ExecutionPolicy Bypass -File $stageScript `
        -BuildRoot $buildRoot -RamdiskSource (Join-Path $WorkspaceRoot 'stage') 2>&1 |
        Tee-Object -FilePath $stageLog
    if ($LASTEXITCODE -ne 0) { throw "Supported Phase 29 stage script failed with exit code $LASTEXITCODE." }
}
finally {
    Pop-Location
}

$verification = Invoke-Contract 'verify'
$verification | ConvertTo-Json -Depth 40
