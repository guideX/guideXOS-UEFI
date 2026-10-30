[CmdletBinding()]
param(
    [string]$SourceRoot = '',
    [string]$OutputRoot = ''
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
if ([string]::IsNullOrWhiteSpace($SourceRoot)) { $SourceRoot = Join-Path $root 'out\rt' }
if ([string]::IsNullOrWhiteSpace($OutputRoot)) { $OutputRoot = Join-Path $root 'out\dotnet' }
$OutputRoot = [IO.Path]::GetFullPath($OutputRoot)

$cacheLockPath = Join-Path $PSScriptRoot 'runtime-source-cache-recovery.lock.json'
$cacheLock = Get-Content -LiteralPath $cacheLockPath -Raw | ConvertFrom-Json
$phase20Lock = Get-Content -LiteralPath (Join-Path $root 'Tools\Phase20\runtime-source.lock.json') -Raw | ConvertFrom-Json
$phase21Lock = Get-Content -LiteralPath (Join-Path $root 'Tools\Phase21\runtime-source.lock.json') -Raw | ConvertFrom-Json
$phase22Lock = Get-Content -LiteralPath (Join-Path $root 'Tools\Phase22\runtime-source.lock.json') -Raw | ConvertFrom-Json
$phase23Lock = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'runtime-source.lock.json') -Raw | ConvertFrom-Json

function Normalize-RelativePath([string]$Path) {
    return $Path.Replace('\', '/')
}

function Get-SourcePath([string]$RelativePath) {
    return Join-Path $SourceRoot ($RelativePath.Replace('/', '\'))
}

function Get-Sha256([string]$Path) {
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToUpperInvariant()
}

function Get-NormalizedTemplateSha256([string]$Path) {
    # Git checks these repository-owned text templates out with CRLF on
    # Windows, while their lock entries identify the canonical LF bytes.
    # Normalize only the checkout copy; runtime source files remain byte-
    # for-byte checked against their locked hashes below.
    $text = [IO.File]::ReadAllText($Path).Replace("`r`n", "`n")
    $sha256 = [Security.Cryptography.SHA256]::Create()
    try {
        $bytes = [Text.Encoding]::UTF8.GetBytes($text)
        return [BitConverter]::ToString($sha256.ComputeHash($bytes)).Replace('-', '')
    }
    finally {
        $sha256.Dispose()
    }
}

function Resolve-OutputDirectory([string]$Path) {
    $fullPath = [IO.Path]::GetFullPath($Path)
    $linkTarget = $null
    try {
        $linkTarget = ([IO.DirectoryInfo]::new($fullPath)).LinkTarget
    }
    catch {
        $linkTarget = $null
    }
    if ([string]::IsNullOrWhiteSpace([string]$linkTarget)) {
        $item = Get-Item -LiteralPath $fullPath -Force -ErrorAction SilentlyContinue
        if ($null -ne $item -and $item.PSObject.Properties['Target']) {
            $targetValues = @($item.Target)
            if ($targetValues.Count -gt 0 -and
                -not [string]::IsNullOrWhiteSpace([string]$targetValues[0])) {
                $linkTarget = [string]$targetValues[0]
            }
        }
    }

    if (-not [string]::IsNullOrWhiteSpace([string]$linkTarget)) {
        if (-not [IO.Path]::IsPathRooted([string]$linkTarget)) {
            $linkTarget = Join-Path (Split-Path -Parent $fullPath) ([string]$linkTarget)
        }
        $resolvedTarget = [IO.Path]::GetFullPath([string]$linkTarget)
        try {
            [IO.Directory]::CreateDirectory($resolvedTarget) | Out-Null
        }
        catch {
            throw "Output directory junction '$fullPath' points to '$resolvedTarget', and its target could not be created: $($_.Exception.Message)"
        }
        if (-not (Test-Path -LiteralPath $resolvedTarget -PathType Container)) {
            throw "Output directory junction target could not be initialized: $fullPath -> $resolvedTarget"
        }
        return $resolvedTarget
    }

    if (Test-Path -LiteralPath $fullPath -PathType Container) { return $fullPath }
    if (Test-Path -LiteralPath $fullPath -PathType Leaf) {
        throw "Output directory path is occupied by a file: $fullPath"
    }
    try {
        [IO.Directory]::CreateDirectory($fullPath) | Out-Null
    }
    catch {
        throw "Output directory could not be created: $fullPath. $($_.Exception.Message)"
    }
    if (-not (Test-Path -LiteralPath $fullPath -PathType Container)) {
        throw "Output directory was not created: $fullPath"
    }
    return $fullPath
}

if (-not (Test-Path -LiteralPath $SourceRoot -PathType Container)) {
    throw "Pinned runtime source is absent: $SourceRoot"
}

$expectedCommit = [string]$cacheLock.runtimeCommit
$actualCommit = (& git -C $SourceRoot rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0) { throw 'Unable to read the runtime checkout commit.' }
if ($expectedCommit -ne [string]$phase20Lock.source.commit -or
    $expectedCommit -ne [string]$phase21Lock.runtimeCommit -or
    $expectedCommit -ne [string]$phase22Lock.runtimeCommit -or
    $expectedCommit -ne [string]$phase23Lock.runtimeCommit -or
    $actualCommit -ne $expectedCommit) {
    throw "Runtime source mismatch. Expected pinned commit $expectedCommit, found $actualCommit. No evidence records were written."
}

$cachedStatus = @(& git -C $SourceRoot status --porcelain=v1 --untracked-files=all)
if ($LASTEXITCODE -ne 0) { throw 'Unable to inspect runtime source status.' }
$actualTracked = [System.Collections.Generic.List[string]]::new()
$actualUntracked = [System.Collections.Generic.List[string]]::new()
foreach ($line in $cachedStatus) {
    if ($line.Length -lt 4) { throw "Unrecognized runtime status entry: $line" }
    $statusCode = $line.Substring(0, 2)
    $relativePath = Normalize-RelativePath $line.Substring(3)
    if ($statusCode -eq ' M') {
        $actualTracked.Add($relativePath)
    }
    elseif ($statusCode -eq '??') {
        $actualUntracked.Add($relativePath)
    }
    else {
        throw "Runtime cache has staged or unsupported status '$statusCode' for $relativePath. No evidence records were written."
    }
}

$expectedTracked = @($cacheLock.trackedFiles | ForEach-Object { Normalize-RelativePath ([string]$_.path) } | Sort-Object -Unique)
$actualTrackedSorted = @($actualTracked | Sort-Object -Unique)
if (($actualTrackedSorted -join "`n") -ne ($expectedTracked -join "`n")) {
    throw 'Runtime cache tracked-file set does not match the locked Phase 21/22/23 plus Phase 26 profile. No evidence records were written.'
}

$phase21Templates = @($phase21Lock.repositoryOwnedTemplates | ForEach-Object { Normalize-RelativePath ([string]$_) })
$phase22Templates = @($phase22Lock.repositoryOwnedTemplates | ForEach-Object { Normalize-RelativePath ([string]$_) })
$expectedTemplatePaths = @($phase21Templates + $phase22Templates | Sort-Object -Unique)
$lockedTemplatePaths = @($cacheLock.requiredTemplates | ForEach-Object { Normalize-RelativePath ([string]$_.path) } | Sort-Object -Unique)
if (($expectedTemplatePaths -join "`n") -ne ($lockedTemplatePaths -join "`n")) {
    throw 'Recovery lock does not list the exact Phase 21/22 repository templates. No evidence records were written.'
}

$expectedGenerated = @($cacheLock.allowedGeneratedArtifacts | ForEach-Object { Normalize-RelativePath ([string]$_) } | Sort-Object -Unique)
$expectedUntracked = @($expectedTemplatePaths + $expectedGenerated | Sort-Object -Unique)
$actualUntrackedSorted = @($actualUntracked | Sort-Object -Unique)
if (($actualUntrackedSorted -join "`n") -ne ($expectedUntracked -join "`n")) {
    throw 'Runtime cache untracked-file set does not match the locked templates and known build outputs. No evidence records were written.'
}

foreach ($entry in $cacheLock.trackedFiles) {
    $relativePath = Normalize-RelativePath ([string]$entry.path)
    $path = Get-SourcePath $relativePath
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Locked runtime source file is missing: $relativePath. No evidence records were written."
    }
    if ((Get-Sha256 $path) -ne ([string]$entry.sha256).ToUpperInvariant()) {
        throw "Locked runtime source hash mismatch: $relativePath. No evidence records were written."
    }
}

foreach ($entry in $cacheLock.requiredTemplates) {
    $relativePath = Normalize-RelativePath ([string]$entry.path)
    $templateRoot = if ($phase22Templates -contains $relativePath) {
        Join-Path $root 'Tools\Phase22\runtime'
    }
    else {
        Join-Path $root 'Tools\Phase21\runtime'
    }
    $templatePath = Join-Path $templateRoot ($relativePath.Replace('/', '\'))
    $runtimePath = Get-SourcePath $relativePath
    if (-not (Test-Path -LiteralPath $templatePath -PathType Leaf) -or
        -not (Test-Path -LiteralPath $runtimePath -PathType Leaf)) {
        throw "Required repository template is missing: $relativePath. No evidence records were written."
    }
    $expectedHash = ([string]$entry.sha256).ToUpperInvariant()
    if ((Get-NormalizedTemplateSha256 $templatePath) -ne $expectedHash -or
        (Get-Sha256 $runtimePath) -ne $expectedHash) {
        throw "Required repository template hash mismatch: $relativePath. No evidence records were written."
    }
}

foreach ($relativePath in $expectedGenerated) {
    if (-not (Test-Path -LiteralPath (Get-SourcePath $relativePath) -PathType Leaf)) {
        throw "Known generated runtime output is missing: $relativePath. No evidence records were written."
    }
}

$OutputRoot = Resolve-OutputDirectory $OutputRoot
$phase21RecordPath = Join-Path $OutputRoot 'phase21-source-patch\phase21-source-patch-record.json'
$phase22RecordPath = Join-Path $OutputRoot 'phase22-source-patch\phase22-source-patch-record.json'
$phase23RecordPath = Join-Path $OutputRoot 'phase23-source-patch\phase23-ilc-source-patch-record.json'
$phase21RecordPath = [IO.Path]::GetFullPath($phase21RecordPath)
$phase22RecordPath = [IO.Path]::GetFullPath($phase22RecordPath)
$phase23RecordPath = [IO.Path]::GetFullPath($phase23RecordPath)

foreach ($recordPath in @($phase21RecordPath, $phase22RecordPath, $phase23RecordPath)) {
    $recordDirectory = [IO.Path]::GetDirectoryName($recordPath)
    [IO.Directory]::CreateDirectory($recordDirectory) | Out-Null
    if (-not (Test-Path -LiteralPath $recordDirectory -PathType Container)) {
        throw "Unable to create source-patch evidence directory: $recordDirectory"
    }
}

function Read-AndValidateExistingRecord([string]$Path, [int]$Phase) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { return $null }
    try {
        $record = Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
    }
    catch {
        throw "Existing Phase $Phase source-patch record is unreadable: $Path"
    }
    if ($Phase -eq 21 -and [int]$record.phase -ne 21) {
        throw "Existing Phase 21 source-patch record has the wrong phase: $Path"
    }
    $recordCommit = switch ($Phase) {
        21 { [string]$record.source.commit }
        22 { [string]$record.sourceCommit }
        23 { [string]$record.sourceCommit }
    }
    if ($recordCommit -ne $expectedCommit) {
        throw "Existing Phase $Phase source-patch record references a different runtime commit: $Path"
    }
    if ($Phase -eq 23) {
        if ([string]$record.patchIdentity -ne [string]$phase23Lock.ilcPatchIdentity) {
            throw "Existing Phase 23 source-patch record has the wrong patch identity: $Path"
        }
        $recordHashes = @{}
        foreach ($file in $record.changedFiles) {
            $recordHashes[(Normalize-RelativePath ([string]$file.path))] = ([string]$file.sha256).ToUpperInvariant()
        }
        foreach ($relativePath in $phase23Lock.changedFiles) {
            $normalizedPath = Normalize-RelativePath ([string]$relativePath)
            $runtimePath = Get-SourcePath $normalizedPath
            if (-not $recordHashes.ContainsKey($normalizedPath) -or
                $recordHashes[$normalizedPath] -ne (Get-Sha256 $runtimePath)) {
                throw "Existing Phase 23 record does not match the locked source file $normalizedPath."
            }
        }
        if ($recordHashes.Count -ne @($phase23Lock.changedFiles).Count) {
            throw "Existing Phase 23 source-patch record has an unexpected changed-file set: $Path"
        }
    }
    return $record
}

$existingPhase21 = Read-AndValidateExistingRecord $phase21RecordPath 21
$existingPhase22 = Read-AndValidateExistingRecord $phase22RecordPath 22
$existingPhase23 = Read-AndValidateExistingRecord $phase23RecordPath 23

$lockSha256 = Get-Sha256 $cacheLockPath
$recoveryInfo = [ordered]@{
    method = 'verified-runtime-cache-snapshot'
    lock = 'Tools/Phase23/runtime-source-cache-recovery.lock.json'
    lockSha256 = $lockSha256
    verifiedAtUtc = [DateTime]::UtcNow.ToString('o')
}

if ($null -eq $existingPhase21) {
    $phase21Changed = @($phase21Lock.sourceFiles | ForEach-Object { Normalize-RelativePath ([string]$_.path) })
    $phase21Changed += $phase21Templates
    $phase21Record = [ordered]@{
        schema = 1
        phase = 21
        target = [string]$phase21Lock.target
        targetOsValue = [string]$phase21Lock.targetOsValue
        source = [ordered]@{
            checkout = (Resolve-Path $SourceRoot).Path
            commit = $expectedCommit
            canonicalRepository = [string]$phase20Lock.source.repository
        }
        runtimeFilesChanged = @($phase21Changed | Sort-Object -Unique)
        recoveredFromVerifiedCache = $true
        recovery = $recoveryInfo
    }
    $json = $phase21Record | ConvertTo-Json -Depth 10
    [IO.File]::WriteAllText($phase21RecordPath, $json + "`r`n", [Text.UTF8Encoding]::new($false))
}

if ($null -eq $existingPhase22) {
    $phase22Tracked = @(
        'src/coreclr/CMakeLists.txt',
        'src/coreclr/inc/daccess.h',
        'src/coreclr/nativeaot/CMakeLists.txt',
        'src/coreclr/nativeaot/Runtime/PalRedhawk.h',
        'src/coreclr/nativeaot/Runtime/gcenv.h',
        'src/coreclr/nativeaot/Runtime/startup.cpp',
        'src/coreclr/nativeaot/Runtime/regdisplay.h',
        'src/coreclr/nativeaot/Runtime/Full/CMakeLists.txt'
    )
    $phase22Changed = @($phase21Lock.sourceFiles | ForEach-Object { Normalize-RelativePath ([string]$_.path) }) + $phase22Tracked
    $phase22Record = [ordered]@{
        schema = 1
        generatedBy = 'Tools/Phase23/recover_source_patch_evidence.ps1'
        target = [string]$phase22Lock.target
        targetOs = [string]$phase22Lock.targetOsValue
        sourceCommit = $expectedCommit
        phase21PatchRecord = $phase21RecordPath
        sourceFilesChanged = @($phase22Changed | Sort-Object -Unique)
        recoveredFromVerifiedCache = $true
        recovery = $recoveryInfo
    }
    $json = $phase22Record | ConvertTo-Json -Depth 10
    [IO.File]::WriteAllText($phase22RecordPath, $json + "`r`n", [Text.UTF8Encoding]::new($false))
}

if ($null -eq $existingPhase23) {
    $changedFiles = foreach ($relativePath in $phase23Lock.changedFiles) {
        $normalizedPath = Normalize-RelativePath ([string]$relativePath)
        [ordered]@{
            path = $normalizedPath
            sha256 = Get-Sha256 (Get-SourcePath $normalizedPath)
        }
    }
    $phase23Record = [ordered]@{
        schema = 1
        generatedBy = 'Tools/Phase23/recover_source_patch_evidence.ps1'
        patchIdentity = [string]$phase23Lock.ilcPatchIdentity
        target = [string]$phase23Lock.target
        targetOs = [string]$phase23Lock.targetOsValue
        sourceCommit = $expectedCommit
        phase21PatchRecord = $phase21RecordPath
        phase22PatchRecord = $phase22RecordPath
        changedFiles = @($changedFiles)
        semanticModel = [ordered]@{
            targetIdentity = 'TargetOS.Guidexos; IsWindows remains false'
            architecture = 'GUIDEXOS is accepted only for TargetArchitecture.X64'
            abi = 'Microsoft x64 argument/register ABI is selected through IsWindowsAbiCompatible'
            objectFormat = 'COFF is selected through UsesCoffObjectFormat; no Windows target alias is introduced'
            runtimeSemantics = 'Windows-only P/Invoke, Win32 resources, CFG, and command-line-W behavior remain gated on actual Windows'
        }
        recoveredFromVerifiedCache = $true
        recovery = $recoveryInfo
    }
    $json = $phase23Record | ConvertTo-Json -Depth 10
    [IO.File]::WriteAllText($phase23RecordPath, $json + "`r`n", [Text.UTF8Encoding]::new($false))
}

Write-Output "Recovered missing Phase 21/22/23 source-patch evidence from the verified runtime cache; no runtime source files were changed."
