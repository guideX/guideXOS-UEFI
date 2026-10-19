#!/usr/bin/env pwsh
<#
.SYNOPSIS
    guideXOS Build System - Automated build script for UEFI bootloader and kernel

.DESCRIPTION
    This script automates the complete build process:
    1. Build UEFI bootloader (C++)
    2. Build C# kernel (NativeAOT)
    3. Convert kernel from PE to ELF64 format
    4. Build ramdisk image
    5. Assemble ESP structure
    6. Create bootable disk image or ISO

.PARAMETER SkipBootloader
    Skip building the UEFI bootloader

.PARAMETER SkipKernel
    Skip building the C# kernel

.PARAMETER SkipRamdisk
    Skip building the ramdisk

.PARAMETER SkipConversion
    Skip PE to ELF conversion

.PARAMETER CreateISO
    Create bootable ISO instead of disk image

.PARAMETER Clean
    Clean build outputs before building

.PARAMETER BootloaderOnly
    Limit build to bootloader only (skip kernel, ramdisk, conversion)

.PARAMETER UefiDiagnosticMode
    Optional UEFI regression build variant: Tiny, FirstFrame, Frames, Input,
    InputStress, ContextMenu, Png, Font, Background, BackgroundRotation,
    AppModel, AppRuntime, Ring3, Ring3Phase15, Ring3Phase24, Ring3Phase25, Ring3Phase26, Ring3Phase27, Ring3Phase28, Ring3Phase29, Ring3Phase30, Ring3Phase31, Ring3Phase32, Ring3Phase33, Ring3Phase34, Ring3Phase35, Ring3Phase35R2, Ring3Phase35R9, Ring3Phase35R12Suppressed, Ring3Phase35R13, Ring3Direct, Storage35Q, Storage35P2, Widget, WidgetStress, WidgetSoak, WidgetOnlyPerformance,
    WidgetOnlyClock, WidgetOnlyMonitor, or WidgetOnlyUptime

.EXAMPLE
    .\build.ps1
    Build everything

.EXAMPLE
    .\build.ps1 -SkipBootloader
    Build only kernel and ramdisk (use existing bootloader)

.EXAMPLE
    .\build.ps1 -CreateISO
    Build and create ISO image
#>

param(
    [switch]$SkipBootloader,
    [switch]$SkipKernel,
    [switch]$SkipRamdisk,
    [switch]$SkipConversion,
    [switch]$CreateISO,
    [switch]$Clean,
    [switch]$BootloaderOnly,
  [ValidateSet('', 'Tiny', 'FirstFrame', 'Frames', 'Input', 'InputStress', 'DesktopP3', 'ContextMenu', 'Png', 'Font', 'Background', 'BackgroundRotation', 'AppModel', 'AppRuntime', 'CleanupStress', 'StartMenuStress', 'ForegroundStress', 'FaultBytesProbe', 'Ring3', 'Ring3Phase15', 'Ring3Phase24', 'Ring3Phase25', 'Ring3Phase26', 'Ring3Phase27', 'Ring3Phase28', 'Ring3Phase29', 'Ring3Phase30', 'Ring3Phase31', 'Ring3Phase32', 'Ring3Phase33', 'Ring3Phase34', 'Ring3Phase35', 'Ring3Phase35R2', 'Ring3Phase35R9', 'Ring3Phase35R12Suppressed', 'Ring3Phase35R13', 'TaskManagerMemoryR2Probe', 'TaskManagerMemoryR3Probe', 'TaskManagerMemoryR3ProbeNoEmit', 'Ring3Direct', 'Storage35Q', 'Storage35P2', 'Widget', 'WidgetStress', 'WidgetSoak', 'WidgetOnlyPerformance', 'WidgetOnlyClock', 'WidgetOnlyMonitor', 'WidgetOnlyUptime')]
    [string]$UefiDiagnosticMode = ''
)

$ErrorActionPreference = "Stop"

# If BootloaderOnly is set, skip all other steps except bootloader build and ESP copy
if ($BootloaderOnly) {
    $SkipKernel = $true
    $SkipRamdisk = $true
    $SkipConversion = $true
}

# Color output functions
function Write-Header($text) {
    Write-Host "`n=== $text ===" -ForegroundColor Cyan
}

function Write-Success($text) {
    Write-Host "  ? $text" -ForegroundColor Green
}

function Write-Warning($text) {
    Write-Host "  ? $text" -ForegroundColor Yellow
}

function Write-Info($text) {
    Write-Host "  ? $text" -ForegroundColor Gray
}

function Get-ArtifactSnapshot($path) {
    if (-not (Test-Path $path)) {
        return $null
    }

    $item = Get-Item $path
    $hash = Get-FileHash $path -Algorithm SHA256
    return [pscustomobject]@{
        Path = $item.FullName
        Length = $item.Length
        LastWriteTime = $item.LastWriteTime
        Hash = $hash.Hash
    }
}

function Write-Error($text) {
    Write-Host "  ? $text" -ForegroundColor Red
}

# Paths
$RootDir = $PSScriptRoot
$BootloaderProject = "$RootDir\guideXOSBootLoader\guideXOSBootLoader.vcxproj"
$KernelProject = "$RootDir\guideXOS\guideXOS.csproj"
$RamdiskSrc = "$RootDir\ramdisk_src"
$ESPDir = "$RootDir\ESP"
$ToolsDir = "$RootDir\tools"

# If the bootloader project isn't at the default path, try to discover it
if (-not (Test-Path $BootloaderProject)) {
    $candidateBootProjects = Get-ChildItem -Path "$RootDir\guideXOSBootLoader" -Filter *.vcxproj -Recurse -ErrorAction SilentlyContinue
    if ($candidateBootProjects -and $candidateBootProjects.Count -gt 0) {
        $BootloaderProject = $candidateBootProjects[0].FullName
        Write-Warning "Bootloader project not found at default path; using discovered project: $BootloaderProject"
    }
}

Write-Header "guideXOS Build System"
Write-Info "Root directory: $RootDir"

# Check for required tools
function Test-Command($cmdname) {
    return [bool](Get-Command -Name $cmdname -ErrorAction SilentlyContinue)
}

. (Join-Path $RootDir 'Tools\Find-VcVars64.ps1')

Write-Header "Checking Build Tools"

# Check for MSBuild
$vcvars64 = $null
if (-not $SkipBootloader) {
    $msbuild = $null
    if (Test-Path "C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe") {
        $msbuild = "C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe"
    } elseif (Test-Path "C:\Program Files\Microsoft Visual Studio\18\Professional\MSBuild\Current\Bin\MSBuild.exe") {
        $msbuild = "C:\Program Files\Microsoft Visual Studio\18\Professional\MSBuild\Current\Bin\MSBuild.exe"
    } elseif (Test-Path "C:\Program Files\Microsoft Visual Studio\18\Enterprise\MSBuild\Current\Bin\MSBuild.exe") {
        $msbuild = "C:\Program Files\Microsoft Visual Studio\18\Enterprise\MSBuild\Current\Bin\MSBuild.exe"
    } elseif (Test-Path "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe") {
        $msbuild = "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe"
    } elseif (Test-Path "C:\Program Files\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe") {
        $msbuild = "C:\Program Files\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe"
    } elseif (Test-Path "C:\Program Files\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe") {
        $msbuild = "C:\Program Files\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe"
    } elseif (Test-Command msbuild) {
        $msbuild = "msbuild"
    }
    
    if (-not $msbuild) {
        Write-Error "MSBuild not found! Install Visual Studio 2022 or add MSBuild to PATH"
        exit 1
    }
    Write-Success "MSBuild found: $msbuild"
    $vcvars64 = Find-VcVars64 $msbuild
    
    # Check for EDK II (required for bootloader)
    $edkiiPaths = @(
        "C:\edk2",
        "$RootDir\edk2",
        "$RootDir\edk2-master",
        "$RootDir\edk2-headers",
        "$env:EDK2_PATH"
    )
    
    $edkiiFound = $false
    foreach ($path in $edkiiPaths) {
        if ($path -and (Test-Path "$path\MdePkg\Include\Uefi.h")) {
            Write-Success "EDK II found: $path"
            $edkiiFound = $true
            break
        }
    }
    
    if (-not $edkiiFound) {
        Write-Warning "EDK II not found - bootloader may not build"
        Write-Warning "The bootloader needs UEFI headers (Uefi.h)"
        Write-Warning "Options:"
        Write-Warning "  1. See SETUP_EDK2.md for full EDK II setup"
        Write-Warning "  2. Run: .\build.ps1 -SkipBootloader (use existing bootloader)"
        Write-Warning "  3. Build bootloader in WSL/Linux instead"
    }
}

# Check for .NET SDK
if ((-not $SkipKernel) -or (-not $SkipRamdisk)) {
    # Resolve every candidate under the managed proof's pinned SDK policy. This
    # rejects a host-only dotnet executable and SDKs that cannot satisfy the
    # repository lock, including the bundled runtime SDK when it is too old.
    $sdkPinPath = Join-Path $RootDir 'UserManagedProof\global.json'
    $sdkPinVersion = $null
    $sdkPinRollForward = $null
    $sdkResolutionDirectory = $RootDir
    if (Test-Path -LiteralPath $sdkPinPath -PathType Leaf) {
        try {
            $sdkPin = Get-Content -LiteralPath $sdkPinPath -Raw | ConvertFrom-Json
            $sdkPinVersion = [string]$sdkPin.sdk.version
            $sdkPinRollForward = [string]$sdkPin.sdk.rollForward
            if ([string]::IsNullOrWhiteSpace($sdkPinVersion)) {
                throw 'The SDK version is empty.'
            }
            $sdkResolutionDirectory = Split-Path -Parent $sdkPinPath
        } catch {
            throw "Unable to read the managed proof SDK pin at ${sdkPinPath}: $($_.Exception.Message)"
        }
    }

    $dotnetCandidates = @()
    $dotnetCommand = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($null -ne $dotnetCommand -and $dotnetCommand.Source) {
        $dotnetCandidates += $dotnetCommand.Source
    }
    $dotnetCandidates += @(
        (Join-Path $env:ProgramFiles 'dotnet\dotnet.exe'),
        (Join-Path $env:USERPROFILE '.dotnet\dotnet.exe'),
        (Join-Path $env:LOCALAPPDATA 'Microsoft\dotnet\dotnet.exe'),
        (Join-Path $RootDir 'out\rt\.dotnet\dotnet.exe')
    )
    $dotnetCandidates = @($dotnetCandidates | Where-Object { $_ } | Select-Object -Unique)
    $dotnetExe = $null
    $dotnetVersion = $null
    foreach ($candidate in $dotnetCandidates) {
        if (-not (Test-Path -LiteralPath $candidate -PathType Leaf)) { continue }
        try {
            Push-Location -LiteralPath $sdkResolutionDirectory
            try {
                $candidateOutput = @(& $candidate --version 2>$null)
                $candidateExitCode = $LASTEXITCODE
            } finally {
                Pop-Location
            }
            $candidateVersion = if ($candidateOutput.Count -gt 0) { [string]$candidateOutput[0] } else { '' }
            if ($candidateExitCode -eq 0 -and -not [string]::IsNullOrWhiteSpace($candidateVersion)) {
                $candidateVersion = $candidateVersion.Trim()
                if ($sdkPinRollForward -eq 'disable' -and $candidateVersion -ne $sdkPinVersion) {
                    Write-Info ".NET SDK candidate rejected: $candidate resolves $candidateVersion; the repository requires $sdkPinVersion with roll-forward disabled."
                    continue
                }
                $dotnetExe = $candidate
                $dotnetVersion = $candidateVersion
                break
            }
        } catch {
            # Continue to the next known SDK installation.
        }
    }
    if (-not $dotnetExe) {
        Write-Error ".NET SDK not found! Install an SDK or restore the pinned SDK under out\rt\.dotnet."
        exit 1
    }
    $dotnetDirectory = Split-Path -Parent $dotnetExe
    $env:PATH = "$dotnetDirectory;$env:PATH"
    Write-Success ".NET SDK found: $dotnetVersion"
    if (-not $vcvars64) {
        $vcvars64 = Find-VcVars64 $null
    }
}

# Check for Python
$pythonExe = $null
$pythonExeArgs = @()
if ((-not $SkipRamdisk) -or (-not $SkipConversion) -or (-not $SkipKernel)) {
    $bundledPython = Join-Path $env:USERPROFILE ".cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe"
    if (Test-Path $bundledPython) {
        $pythonExe = $bundledPython
        $pythonExeArgs = @()
        $pythonVersion = & $pythonExe --version
        Write-Success "Bundled Python found: $pythonVersion"
    } elseif (Test-Command py) {
        $pythonExe = "py"
        $pythonExeArgs = @("-3")
        $pythonVersion = & $pythonExe @($pythonExeArgs + @("--version"))
        Write-Success "Python launcher found: $pythonVersion"
    } elseif (Test-Command python) {
        $pythonExe = "python"
        $pythonExeArgs = @()
        $pythonVersion = python --version
        Write-Success "Python found: $pythonVersion"
    } elseif ((-not $SkipRamdisk) -or (-not $SkipKernel)) {
        Write-Error "Python not found! Install Python 3.x"
        exit 1
    } else {
        Write-Warning "Python not found - ramdisk step is skipped, but PE?ELF fallback conversion will not be available"
    }
}

# Check for objcopy (optional but recommended)
$objcopyPath = $null
if (-not $SkipConversion) {
    # Try to find objcopy
    $possiblePaths = @(
        "C:\msys64\mingw64\bin\objcopy.exe",
        "C:\msys64\usr\bin\objcopy.exe",
        "C:\Program Files\LLVM\bin\llvm-objcopy.exe"
    )
    
    foreach ($path in $possiblePaths) {
        if (Test-Path $path) {
            $objcopyPath = $path
            break
        }
    }
    
    if (-not $objcopyPath -and (Test-Command objcopy)) {
        $objcopyPath = "objcopy"
    }
    
    if (-not $objcopyPath -and (Test-Command llvm-objcopy)) {
        $objcopyPath = "llvm-objcopy"
    }
    
    if ($objcopyPath) {
        Write-Success "objcopy found: $objcopyPath"
    } else {
        Write-Warning "objcopy not found - using bundled Python PE-to-ELF converter instead"
    }
}

# Clean build outputs
if ($Clean) {
    Write-Header "Cleaning Build Outputs"
    
    if (Test-Path "$RootDir\guideXOS\bin") {
        Remove-Item -Recurse -Force "$RootDir\guideXOS\bin"
        Write-Success "Cleaned kernel build output"
    }
    
    if (Test-Path "$RootDir\guideXOS\obj") {
        Remove-Item -Recurse -Force "$RootDir\guideXOS\obj"
    }
    
    if (Test-Path "$RootDir\guideXOSBootLoader\x64") {
        Remove-Item -Recurse -Force "$RootDir\guideXOSBootLoader\x64"
        Write-Success "Cleaned bootloader build output"
    }
    
    if (Test-Path $ESPDir) {
        Remove-Item -Recurse -Force $ESPDir
        Write-Success "Cleaned ESP directory"
    }
}

# Step 1: Build UEFI Bootloader
if (-not $SkipBootloader) {
    Write-Header "[1/5] Building UEFI Bootloader"
    
    if (-not (Test-Path $BootloaderProject)) {
        Write-Error "Bootloader project not found: $BootloaderProject"
        exit 1
    }
    
    # NASM is resolved directly from the project or solution Tools path.
    $nasmPath = "$ToolsDir\nasm.exe"
    if (Test-Path $nasmPath) {
        Write-Success "NASM found in Tools directory"
    } elseif (-not (Test-Command nasm)) {
        Write-Warning "NASM not found - assembly files may fail to build"
        Write-Warning "Install NASM or place nasm.exe in the Tools directory"
    }

    # MSBuild on this host can inherit both Path and PATH, which breaks VC tool startup.
    # Keep the canonical Path entry and drop the duplicate PATH entry before launching it.
    $pathBeforeMsbuild = $env:Path
    Remove-Item Env:PATH -ErrorAction SilentlyContinue
    
    Write-Info "Building with MSBuild..."
    try {
        & $msbuild $BootloaderProject `
            /p:Configuration=Release `
            /p:Platform=x64 /p:IntDir=x64\Release\build\ `
            /v:minimal `
            /nologo `
            /t:Rebuild
    } finally {
        if ($null -ne $pathBeforeMsbuild) { $env:Path = $pathBeforeMsbuild }
    }
    
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Bootloader build failed with exit code $LASTEXITCODE"
        exit 1
    }
    
    # Check for output - try .efi first, then .exe (UEFI apps can use either extension)
    $bootloaderOutputEfi = "$RootDir\guideXOSBootLoader\x64\Release\guideXOSBootLoader.efi"
    $bootloaderOutputExe = "$RootDir\guideXOSBootLoader\x64\Release\guideXOSBootLoader.exe"
    
    if (Test-Path $bootloaderOutputEfi) {
        $bootloaderOutput = $bootloaderOutputEfi
    } elseif (Test-Path $bootloaderOutputExe) {
        $bootloaderOutput = $bootloaderOutputExe
        Write-Info "Using .exe output (will be copied as BOOTX64.EFI)"
    } else {
        Write-Error "Bootloader binary not found at: $bootloaderOutputEfi or $bootloaderOutputExe"
        exit 1
    }
    
    $size = (Get-Item $bootloaderOutput).Length
    Write-Success "Bootloader built successfully ($([math]::Round($size/1KB, 2)) KB)"
} else {
    Write-Header "[1/5] Skipping Bootloader Build"
}

# Managed proof identities must be final before the kernel is compiled.
# Their descriptors remain untrusted at runtime; this step only creates the
# kernel's strict compile-time allowlist from the finalized staged bytes.
if (-not $SkipRamdisk) {
    Write-Header "[Managed Proofs] Build and stage managed payload cohort"
    # Phase 24 uses the existing RDSK source path.  Stage only the exact,
    # already-validated Phase 23 image; the kernel still treats both the PE and
    # its compact descriptor as untrusted input and validates them again.
    $phase24Stage = Join-Path $RootDir "Tools\Phase24\stage_phase24_image.ps1"
    $phase23Artifact = Join-Path $RootDir "out\dotnet\phase23-user-managed-proof\publish\guideXOS.UserManagedProof.exe"
    $phase23Map = Join-Path $RootDir "out\dotnet\phase23-user-managed-proof\guidexos.map"
    if ((Test-Path -LiteralPath $phase24Stage) -and
        (Test-Path -LiteralPath $phase23Artifact) -and
        (Test-Path -LiteralPath $phase23Map)) {
        Write-Info "Staging exact Phase 23 image for the Phase 24 kernel loader..."
        & $phase24Stage -Artifact $phase23Artifact -Map $phase23Map -RamdiskSource (Join-Path $RamdiskSrc "Native")
        if ($LASTEXITCODE -ne 0) {
            Write-Error "Phase 24 image staging failed"
            exit 1
        }
    } else {
        Write-Info "Phase 24 image not staged (accepted Phase 23 artifact/map unavailable)"
    }

    $phase25Stage = Join-Path $RootDir "Tools\Phase25\stage_phase25_bootstrap.ps1"
    if (Test-Path -LiteralPath $phase25Stage) {
        Write-Info "Staging the independently reviewed Phase 25 native bootstrap..."
        & $phase25Stage -RamdiskSource (Join-Path $RamdiskSrc "Native")
        if ($LASTEXITCODE -ne 0) {
            Write-Error "Phase 25 bootstrap staging failed"
            exit 1
        }
    }

    $phase26Stage = Join-Path $RootDir "Tools\Phase26\stage_phase26_image.ps1"
    $phase26Artifact = Join-Path $RootDir "out\dotnet\phase26-user-managed-proof\publish\guideXOS.UserManagedProof.exe"
    $phase26Map = Join-Path $RootDir "out\dotnet\phase26-user-managed-proof\guidexos.map"
    if ((Test-Path -LiteralPath $phase26Stage) -and
        (Test-Path -LiteralPath $phase26Artifact) -and
        (Test-Path -LiteralPath $phase26Map)) {
        Write-Info "Staging the separately reviewed Phase 26 managed-entry image..."
        & $phase26Stage -Artifact $phase26Artifact -Map $phase26Map -RamdiskSource (Join-Path $RamdiskSrc "Native")
        if ($LASTEXITCODE -ne 0) {
            Write-Error "Phase 26 image staging failed"
            exit 1
        }
    } else {
        Write-Info "Phase 26 image not staged (accepted Phase 26 artifact/map unavailable)"
    }

    $phase26BootstrapStage = Join-Path $RootDir "Tools\Phase26\stage_phase26_bootstrap.ps1"
    if (Test-Path -LiteralPath $phase26BootstrapStage) {
        Write-Info "Staging the independently reviewed Phase 26 managed-entry bootstrap..."
        & $phase26BootstrapStage -RamdiskSource (Join-Path $RamdiskSrc "Native")
        if ($LASTEXITCODE -ne 0) {
            Write-Error "Phase 26 bootstrap staging failed"
            exit 1
        }
    }

    $phase19RuntimePackRoot = Join-Path $RootDir "out\dotnet\phase19-runtime-pack"
    $phase19PalObject = Join-Path $phase19RuntimePackRoot "guidexos_nativeaot_pal_contract.obj"
    $phase19PalArchive = Join-Path $phase19RuntimePackRoot "guidexos_nativeaot_pal_contract.lib"
    $phase19Manifest = Join-Path $phase19RuntimePackRoot "phase19-manifest.json"
    $phase19ToolchainRecord = Join-Path $phase19RuntimePackRoot "toolchain-record.json"

    function Test-Phase19RuntimePack {
        foreach ($requiredPath in @($phase19PalObject, $phase19PalArchive, $phase19Manifest, $phase19ToolchainRecord)) {
            if (-not (Test-Path -LiteralPath $requiredPath -PathType Leaf)) { return $false }
        }
        try {
            $manifestData = Get-Content -LiteralPath $phase19Manifest -Raw | ConvertFrom-Json
            $objectHash = (Get-FileHash -LiteralPath $phase19PalObject -Algorithm SHA256).Hash.ToUpperInvariant()
            $archiveHash = (Get-FileHash -LiteralPath $phase19PalArchive -Algorithm SHA256).Hash.ToUpperInvariant()
            $sourcePath = Join-Path $RootDir "Tools\Phase19\guidexos_nativeaot_pal_contract.cpp"
            $sourceHash = (Get-FileHash -LiteralPath $sourcePath -Algorithm SHA256).Hash.ToUpperInvariant()
            return $manifestData.target.identity -eq 'guidexos-x64' -and
                $manifestData.status -eq 'CONTRACT_BUILT_SOURCE_RUNTIME_REQUIRED' -and
                $manifestData.artifacts.source.sha256 -eq $sourceHash -and
                $manifestData.artifacts.object.sha256 -eq $objectHash -and
                $manifestData.artifacts.archive.sha256 -eq $archiveHash
        } catch {
            return $false
        }
    }

    if (-not (Test-Phase19RuntimePack)) {
        $phase19PackBuild = Join-Path $RootDir "Tools\build_phase19_runtime_pack.ps1"
        if (-not (Test-Path -LiteralPath $phase19PackBuild -PathType Leaf)) {
            throw "Phase 19 PAL contract artifacts are absent or stale, and the Phase 19 builder is missing: $phase19PackBuild"
        }
        Write-Info "Phase 19 PAL contract artifacts are absent or stale; building the pinned contract pack..."
        try {
            & $phase19PackBuild
            if ($LASTEXITCODE -ne 0) { throw "Phase 19 runtime pack build failed: $LASTEXITCODE" }
        } catch {
            throw "Unable to prepare the Phase 19 PAL contract required by Phase 23. Details: $($_.Exception.Message)"
        }
        if (-not (Test-Phase19RuntimePack)) {
            throw "Phase 19 builder completed without producing a valid PAL contract object, archive, manifest, and toolchain record under $phase19RuntimePackRoot"
        }
    }

    $phase23PrivateIlcRoot = Join-Path $RootDir "out\dotnet\phase23-private-ilc"
    $phase23PrivateIlcPath = Join-Path $phase23PrivateIlcRoot "ilc-published\ilc.exe"
    $phase23PrivateIlcRecord = Join-Path $phase23PrivateIlcRoot "private-ilc-build.json"
    $phase23SourcePatchRoot = Join-Path $RootDir "out\dotnet\phase23-source-patch"
    $phase23SourcePatchRecord = Join-Path $phase23SourcePatchRoot "phase23-ilc-source-patch-record.json"
    $runtimeSourceRoot = Join-Path $RootDir "out\rt"
    $phase21SourcePatchRoot = Join-Path $RootDir "out\dotnet\phase21-source-patch"
    $phase21SourcePatchRecord = Join-Path $phase21SourcePatchRoot "phase21-source-patch-record.json"
    $phase22SourcePatchRoot = Join-Path $RootDir "out\dotnet\phase22-source-patch"
    $phase22SourcePatchRecord = Join-Path $phase22SourcePatchRoot "phase22-source-patch-record.json"
    $phase21SourcePatch = Join-Path $RootDir "Tools\Phase21\apply_phase21_patch.ps1"
    $phase22SourcePatch = Join-Path $RootDir "Tools\Phase22\apply_phase22_patch.ps1"
    $phase23SourcePatch = Join-Path $RootDir "Tools\Phase23\apply_phase23_ilc_patch.ps1"
    $sourcePatchEvidenceReady =
        (Test-Path -LiteralPath $phase21SourcePatchRecord -PathType Leaf) -and
        (Test-Path -LiteralPath $phase22SourcePatchRecord -PathType Leaf) -and
        (Test-Path -LiteralPath $phase23SourcePatchRecord -PathType Leaf)
    if (-not $sourcePatchEvidenceReady) {
        $runtimeStatus = @(& git -C $runtimeSourceRoot status --porcelain=v1 --untracked-files=all)
        if ($LASTEXITCODE -ne 0) {
            throw "Unable to inspect pinned runtime source status: $runtimeSourceRoot"
        }
        if ($runtimeStatus.Count -eq 0) {
            foreach ($patchScript in @($phase21SourcePatch, $phase22SourcePatch, $phase23SourcePatch)) {
                if (-not (Test-Path -LiteralPath $patchScript -PathType Leaf)) {
                    throw "Runtime source-patch evidence is missing, and its preparation script is unavailable: $patchScript"
                }
            }
            Write-Info "Phase 21-23 source-patch evidence is missing; applying the pinned transforms to the clean runtime checkout..."
            try {
                & $phase21SourcePatch -SourceRoot $runtimeSourceRoot -OutputRoot $phase21SourcePatchRoot
                & $phase22SourcePatch -SourceRoot $runtimeSourceRoot -OutputRoot $phase22SourcePatchRoot
                & $phase23SourcePatch -SourceRoot $runtimeSourceRoot -OutputRoot $phase23SourcePatchRoot
            } catch {
                throw "Unable to prepare Phase 21-23 runtime source evidence from a clean pinned checkout. Details: $($_.Exception.Message)"
            }
        } else {
            $recoverSourcePatchEvidence = Join-Path $RootDir "Tools\Phase23\recover_source_patch_evidence.ps1"
            if (-not (Test-Path -LiteralPath $recoverSourcePatchEvidence -PathType Leaf)) {
                throw "Source-patch evidence is missing while out\rt is modified, and the verified-cache recovery script is unavailable: $recoverSourcePatchEvidence"
            }
            Write-Info "Phase 21-23 source-patch evidence is missing; verifying the pinned runtime cache before restoring its records..."
            try {
                & $recoverSourcePatchEvidence -SourceRoot $runtimeSourceRoot -OutputRoot (Join-Path $RootDir "out\dotnet")
            } catch {
                throw "Unable to recover Phase 21-23 source-patch evidence. The runtime cache was preserved. Details: $($_.Exception.Message)"
            }
        }
        foreach ($requiredRecord in @($phase21SourcePatchRecord, $phase22SourcePatchRecord, $phase23SourcePatchRecord)) {
            if (-not (Test-Path -LiteralPath $requiredRecord -PathType Leaf)) {
                throw "Runtime source preparation did not produce its required evidence record: $requiredRecord"
            }
        }
    }

    $phase23PrivateIlcRebuilt = $false
    $phase23PrivateIlcReady = (Test-Path -LiteralPath $phase23PrivateIlcPath -PathType Leaf) -and
        (Test-Path -LiteralPath $phase23PrivateIlcRecord -PathType Leaf)
    if ($phase23PrivateIlcReady) {
        try {
            $phase23PrivateIlcRecordData = Get-Content -LiteralPath $phase23PrivateIlcRecord -Raw | ConvertFrom-Json
            $phase23PrivateIlcHash = (Get-FileHash -LiteralPath $phase23PrivateIlcPath -Algorithm SHA256).Hash.ToUpperInvariant()
            $phase23PrivateIlcReady =
                $phase23PrivateIlcRecordData.runtimeSourceCommit -eq '9d5a6a9aa463d6d10b0b0ba6d5982cc82f363dc3' -and
                $phase23PrivateIlcRecordData.ilcPatchIdentity -eq 'guidexos-ilc-target-v1' -and
                $phase23PrivateIlcRecordData.privateCompiler.sha256 -eq $phase23PrivateIlcHash
        } catch {
            $phase23PrivateIlcReady = $false
        }
    }
    if (-not $phase23PrivateIlcReady) {
        $phase23PrivateIlcBuild = Join-Path $RootDir "Tools\Phase23\build_private_ilcompiler.ps1"
        if (-not (Test-Path -LiteralPath $phase23PrivateIlcBuild -PathType Leaf)) {
            throw "Phase 23 private ILCompiler is absent or incomplete, and its build script is missing: $phase23PrivateIlcBuild"
        }
        Write-Info "Phase 23 private ILCompiler is absent or incomplete; building it from the pinned runtime source..."
        & $phase23PrivateIlcBuild -OutputRoot $phase23PrivateIlcRoot
        if ($LASTEXITCODE -ne 0) { throw "Phase 23 private ILCompiler build failed: $LASTEXITCODE" }
        foreach ($requiredPath in @($phase23PrivateIlcPath, $phase23PrivateIlcRecord)) {
            if (-not (Test-Path -LiteralPath $requiredPath -PathType Leaf)) {
                throw "Phase 23 private ILCompiler build did not produce a required file: $requiredPath"
            }
        }
        $phase23PrivateIlcRebuilt = $true
    }

    $phase23RuntimePack = Join-Path $RootDir "out\dotnet\phase23-runtime-pack"
    $phase23RuntimePackRequiredFiles = @(
        "guidexos-runtime-pack.manifest.json",
        "guidexos_link_shim.obj",
        "packages\microsoft.dotnet.ilcompiler.9.0.0.nupkg",
        "packages\runtime.guidexos-x64.microsoft.dotnet.ilcompiler.9.0.0.nupkg",
        "packages\runtime.win-x64.microsoft.dotnet.ilcompiler.9.0.0.nupkg"
    )
    $phase23RuntimePackReady = (-not $phase23PrivateIlcRebuilt) -and
        (Test-Path -LiteralPath $phase23RuntimePack -PathType Container)
    foreach ($relativePath in $phase23RuntimePackRequiredFiles) {
        if (-not (Test-Path -LiteralPath (Join-Path $phase23RuntimePack $relativePath) -PathType Leaf)) {
            $phase23RuntimePackReady = $false
            break
        }
    }
    if (-not $phase23RuntimePackReady) {
        $phase23RuntimePackStage = Join-Path $RootDir "Tools\Phase23\stage_guidexos_private_pack.ps1"
        if (-not (Test-Path -LiteralPath $phase23RuntimePackStage -PathType Leaf)) {
            throw "Phase 23 runtime pack is absent or incomplete, and its staging script is missing: $phase23RuntimePackStage"
        }
        Write-Info "Phase 23 runtime pack is absent, incomplete, or stale; staging the accepted private compiler/runtime pack..."
        & $phase23RuntimePackStage -OutputRoot $phase23RuntimePack
        if ($LASTEXITCODE -ne 0) { throw "Phase 23 runtime pack staging failed: $LASTEXITCODE" }
        foreach ($relativePath in $phase23RuntimePackRequiredFiles) {
            $requiredPath = Join-Path $phase23RuntimePack $relativePath
            if (-not (Test-Path -LiteralPath $requiredPath -PathType Leaf)) {
                throw "Phase 23 runtime pack staging did not produce a required file: $requiredPath"
            }
        }
    }

    $phase27Build = Join-Path $RootDir "Tools\Phase27\build_phase27_managed_service_proof.ps1"
    $phase27BootstrapBuild = Join-Path $RootDir "Tools\Phase27\build_phase27_bootstrap.ps1"
    $phase27Stage = Join-Path $RootDir "Tools\Phase27\stage_phase27_image.ps1"
    $phase27BuildRoot = Join-Path $RootDir "out\dotnet\phase27-managed-service-proof"
    if ((Test-Path -LiteralPath $phase27Build) -and
        (Test-Path -LiteralPath $phase27BootstrapBuild)) {
        Write-Info "Building the Phase 27 managed App Model service proof..."
        & $phase27Build
        if ($LASTEXITCODE -ne 0) { throw "Phase 27 managed proof build failed: $LASTEXITCODE" }
        & $phase27BootstrapBuild
        if ($LASTEXITCODE -ne 0) { throw "Phase 27 bootstrap build failed: $LASTEXITCODE" }
    }
    if (Test-Path -LiteralPath $phase27Stage) {
        Write-Info "Staging the Phase 27 managed System Information proof..."
        & $phase27Stage -BuildRoot $phase27BuildRoot -RamdiskSource (Join-Path $RamdiskSrc "Native")
        if ($LASTEXITCODE -ne 0) { throw "Phase 27 image staging failed: $LASTEXITCODE" }
    }

    $phase28Build = Join-Path $RootDir "Tools\Phase28\build_phase28_managed_sdk.ps1"
    $phase28Stage = Join-Path $RootDir "Tools\Phase28\stage_phase28_image.ps1"
    $phase28BuildRoot = Join-Path $RootDir "out\dotnet\phase28-managed-sdk"
    if ((Test-Path -LiteralPath $phase28Build) -and
        (Test-Path -LiteralPath $phase28Stage)) {
        Write-Info "Building the Phase 28 reusable managed SDK proof..."
        & $phase28Build
        if ($LASTEXITCODE -ne 0) { throw "Phase 28 managed SDK build failed: $LASTEXITCODE" }
        & $phase28Stage -BuildRoot $phase28BuildRoot -RamdiskSource (Join-Path $RamdiskSrc "Native")
        if ($LASTEXITCODE -ne 0) { throw "Phase 28 image staging failed: $LASTEXITCODE" }
    }

    $phase29Build = Join-Path $RootDir "Tools\Phase29\build_phase29_managed_notification.ps1"
    $phase29Stage = Join-Path $RootDir "Tools\Phase29\stage_phase29_image.ps1"
    $phase29BuildRoot = Join-Path $RootDir "out\dotnet\phase29-managed-notification"
    if ((Test-Path -LiteralPath $phase29Build) -and
        (Test-Path -LiteralPath $phase29Stage)) {
        Write-Info "Building the Phase 29 managed Notifications proof..."
        & $phase29Build
        if ($LASTEXITCODE -ne 0) { throw "Phase 29 managed notification build failed: $LASTEXITCODE" }
        & $phase29Stage -BuildRoot $phase29BuildRoot -RamdiskSource (Join-Path $RamdiskSrc "Native")
        if ($LASTEXITCODE -ne 0) { throw "Phase 29 image staging failed: $LASTEXITCODE" }
    }

    $phase30Build = Join-Path $RootDir "Tools\Phase30\build_phase30_managed_clipboard.ps1"
    $phase30Stage = Join-Path $RootDir "Tools\Phase30\stage_phase30_image.ps1"
    $phase30BuildRoot = Join-Path $RootDir "out\dotnet\phase30-managed-clipboard"
    if ((Test-Path -LiteralPath $phase30Build) -and
        (Test-Path -LiteralPath $phase30Stage)) {
        Write-Info "Building the Phase 30 managed Clipboard proof..."
        & $phase30Build
        if ($LASTEXITCODE -ne 0) { throw "Phase 30 managed clipboard build failed: $LASTEXITCODE" }
        & $phase30Stage -BuildRoot $phase30BuildRoot -RamdiskSource (Join-Path $RamdiskSrc "Native")
        if ($LASTEXITCODE -ne 0) { throw "Phase 30 image staging failed: $LASTEXITCODE" }
    }

    $phase31Build = Join-Path $RootDir "Tools\Phase31\build_phase31_managed_shell.ps1"
    $phase31Stage = Join-Path $RootDir "Tools\Phase31\stage_phase31_image.ps1"
    $phase31BuildRoot = Join-Path $RootDir "out\dotnet\phase31-managed-shell"
    if ((Test-Path -LiteralPath $phase31Build) -and
        (Test-Path -LiteralPath $phase31Stage)) {
        Write-Info "Building the Phase 31 managed Shell proof..."
        & $phase31Build
        if ($LASTEXITCODE -ne 0) { throw "Phase 31 managed Shell build failed: $LASTEXITCODE" }
        & $phase31Stage -BuildRoot $phase31BuildRoot -RamdiskSource (Join-Path $RamdiskSrc "Native")
        if ($LASTEXITCODE -ne 0) { throw "Phase 31 image staging failed: $LASTEXITCODE" }
    }

    $phase33Build = Join-Path $RootDir "Tools\Phase33\build_phase33_managed_shell_action.ps1"
    $phase33Stage = Join-Path $RootDir "Tools\Phase33\stage_phase33_image.ps1"
    $phase33BuildRoot = Join-Path $RootDir "out\dotnet\phase33-managed-shell-action"
    if ((Test-Path -LiteralPath $phase33Build) -and
        (Test-Path -LiteralPath $phase33Stage)) {
        Write-Info "Building the Phase 33 managed Shell object proof..."
        & $phase33Build
        if ($LASTEXITCODE -ne 0) { throw "Phase 33 managed Shell object build failed: $LASTEXITCODE" }
        & $phase33Stage -BuildRoot $phase33BuildRoot -RamdiskSource (Join-Path $RamdiskSrc "Native")
        if ($LASTEXITCODE -ne 0) { throw "Phase 33 image staging failed: $LASTEXITCODE" }
    }

    $phase32Build = Join-Path $RootDir "Tools\Phase32\build_phase32_managed_open_document.ps1"
    $phase32Stage = Join-Path $RootDir "Tools\Phase32\stage_phase32_image.ps1"
    $phase32BuildRoot = Join-Path $RootDir "out\dotnet\phase32-managed-open-document"
    if ((Test-Path -LiteralPath $phase32Build) -and
        (Test-Path -LiteralPath $phase32Stage)) {
        Write-Info "Building the Phase 32 managed OpenDocument proof..."
        & $phase32Build
        if ($LASTEXITCODE -ne 0) { throw "Phase 32 managed OpenDocument build failed: $LASTEXITCODE" }
        & $phase32Stage -BuildRoot $phase32BuildRoot -RamdiskSource (Join-Path $RamdiskSrc "Native")
        if ($LASTEXITCODE -ne 0) { throw "Phase 32 image staging failed: $LASTEXITCODE" }
    }

    $phase34Build = Join-Path $RootDir "Tools\Phase34\build_phase34_managed_resource_read.ps1"
    $phase34Stage = Join-Path $RootDir "Tools\Phase34\stage_phase34_image.ps1"
    $phase34BuildRoot = Join-Path $RootDir "out\dotnet\phase34-managed-resource-read"
    if ((Test-Path -LiteralPath $phase34Build) -and
        (Test-Path -LiteralPath $phase34Stage)) {
        Write-Info "Building the Phase 34 managed scoped resource proof..."
        & $phase34Build
        if ($LASTEXITCODE -ne 0) { throw "Phase 34 managed resource build failed: $LASTEXITCODE" }
        & $phase34Stage -BuildRoot $phase34BuildRoot -RamdiskSource (Join-Path $RamdiskSrc "Native")
        if ($LASTEXITCODE -ne 0) { throw "Phase 34 image staging failed: $LASTEXITCODE" }
    }

    $phase35Build = Join-Path $RootDir "Tools\Phase35\build_phase35_managed_persistent_read.ps1"
    $phase35Stage = Join-Path $RootDir "Tools\Phase35\stage_phase35_image.ps1"
    $phase35BuildRoot = Join-Path $RootDir "out\dotnet\phase35-managed-persistent-read"
    if ((Test-Path -LiteralPath $phase35Build) -and
        (Test-Path -LiteralPath $phase35Stage)) {
        Write-Info "Building the Phase 35 managed Persistent-read proof..."
        & $phase35Build
        if ($LASTEXITCODE -ne 0) { throw "Phase 35 managed Persistent-read build failed: $LASTEXITCODE" }
        & $phase35Stage -BuildRoot $phase35BuildRoot -RamdiskSource (Join-Path $RamdiskSrc "Native")
        if ($LASTEXITCODE -ne 0) { throw "Phase 35 image staging failed: $LASTEXITCODE" }
    }
}

if (-not $SkipKernel) {
    Write-Header "[Managed Artifacts] Generate kernel admission identities"
    $identityTool = Join-Path $RootDir "Tools\ManagedArtifacts\managed_artifacts.py"
    $identitySource = Join-Path $RootDir "guideXOS\obj\ManagedArtifactIdentities.g.cs"
    $identityReport = Join-Path $RootDir "out\h2c-managed-artifact-audit.csv"
    if (-not (Test-Path -LiteralPath $identityTool)) {
        throw "Managed artifact identity tool is missing: $identityTool"
    }
    & $pythonExe @pythonExeArgs $identityTool --stage generated --ramdisk-source (Join-Path $RamdiskSrc "Native") --identity-source $identitySource --report-csv $identityReport
    if ($LASTEXITCODE -ne 0) {
        throw "Managed artifact identity generation failed: $LASTEXITCODE"
    }
}

# Step 2: Build C# Kernel
if (-not $SkipKernel) {
    Write-Header "[2/5] Building C# Kernel (Custom ILCompiler)"

    if (-not (Test-Path $KernelProject)) {
        Write-Error "Kernel project not found: $KernelProject"
        exit 1
    }

    Write-Info "Building kernel with NativeAOT (ILCompiler)..."

    # Use dotnet publish to trigger NativeAOT compilation
    # 'dotnet build' only produces IL, 'dotnet publish' triggers native compilation
    $kernelAssets = Join-Path (Split-Path -Parent $KernelProject) 'obj\project.assets.json'
    $skipKernelRestore = Test-Path $kernelAssets
    if ($skipKernelRestore) {
        Write-Info "Using cached NuGet assets: $kernelAssets"
    } else {
        Write-Info "NuGet assets not found; restore will run for the kernel build"
    }

    $publishPropertyArgs = @()
    $publishPropertySuffix = ''
    if ($UefiDiagnosticMode) {
        $publishPropertyArgs += "-p:UefiDiagnosticMode=$UefiDiagnosticMode"
        $publishPropertySuffix = " -p:UefiDiagnosticMode=$UefiDiagnosticMode"
        Write-Info "UEFI diagnostic build mode: $UefiDiagnosticMode"
    }

    if ($vcvars64) {
        Write-Info "Using Visual C++ environment: $vcvars64"
        $publishArgs = "dotnet publish `"$KernelProject`" -c Release$publishPropertySuffix"
        if ($skipKernelRestore) {
            $publishArgs += " --no-restore"
        }
        $cmdPathPrefix = "set `"PATH=$env:SystemRoot\System32;$dotnetDirectory`" && "
        & "$env:SystemRoot\System32\cmd.exe" /d /s /c "$cmdPathPrefix call `"$vcvars64`" >nul && $publishArgs"
    } else {
        $publishArgs = @($KernelProject, '-c', 'Release') + $publishPropertyArgs
        if ($skipKernelRestore) {
            dotnet publish @publishArgs --no-restore
        } else {
            dotnet publish @publishArgs
        }
    }

    if ($LASTEXITCODE -ne 0) {
        Write-Error "Kernel build failed with exit code $LASTEXITCODE"
        exit 1
    }

    # Prefer the known ILCompiler native output
    $possibleOutputs = @(
        "$RootDir\\bin\\x64\\Release\\net7.0\\win-x64\\native\\guideXOS.exe",
        "$RootDir\\bin\\x64\\Release\\net7.0\\win-x64\\publish\\guideXOS.exe",
        "$RootDir\\bin\\Release\\net7.0\\win-x64\\native\\guideXOS.exe",
        "$RootDir\\guideXOS\\bin\\Release\\net7.0\\win-x64\\native\\guideXOS.exe",
        "$RootDir\\bin\\Release\\net7.0\\win-x64\\guideXOS.exe"
    )

    $kernelOutput = $null
    foreach ($path in $possibleOutputs) {
        if (Test-Path $path) { $kernelOutput = $path; break }
    }

    if ($kernelOutput) {
        $size = (Get-Item $kernelOutput).Length
        Write-Success "Kernel built successfully ($([math]::Round($size/1MB, 2)) MB)"
        Write-Info "Kernel output: $kernelOutput"

        # Copy to a stable location so the conversion step can find it
        $expectedLocation = "$RootDir\\bin\\Release\\net7.0\\win-x64\\native\\guideXOS.exe"
        $expectedDir = Split-Path $expectedLocation -Parent
        if (-not (Test-Path $expectedDir)) {
            New-Item -ItemType Directory -Force -Path $expectedDir | Out-Null
        }
        if ($kernelOutput -ne $expectedLocation) {
            Copy-Item $kernelOutput $expectedLocation -Force
        }
    } else {
        Write-Error "Native kernel binary not found in expected locations"
        foreach ($path in $possibleOutputs) { Write-Warning "  - $path" }
        exit 1
    }

} else {
    Write-Header "[2/5] Skipping Kernel Build"
}

# Step 3: Convert Kernel to ELF
if (-not $SkipConversion) {
    Write-Header "[3/5] Converting Kernel to ELF64 Format"
    
    # The native kernel is in PE format (.exe)
    $kernelPE = "$RootDir\bin\x64\Release\net7.0\win-x64\native\guideXOS.exe"
    if (-not (Test-Path $kernelPE)) {
        Write-Warning "Kernel PE not found at: $kernelPE"
        Write-Info "Trying alternate location..."
        $kernelPE = "$RootDir\bin\Release\net7.0\win-x64\native\guideXOS.exe"
    }
    if (-not (Test-Path $kernelPE)) {
        Write-Info "Trying project-local native output..."
        $kernelPE = "$RootDir\guideXOS\bin\Release\net7.0\win-x64\native\guideXOS.exe"
    }
    $kernelELF = "$RootDir\kernel.elf"
    $kernelMap = "$RootDir\guideXOS\Kernel.map"
    
    if (-not (Test-Path $kernelPE)) {
        Write-Error "Kernel PE binary not found: $kernelPE"
        exit 1
    }

    # For UEFI boot, we need to use KMain as the entry point, not Entry
    # Use our Python PE to ELF converter with map file support
    $peToElf = Join-Path $RootDir "tools\pe_to_elf.py"
    
    if (-not (Test-Path $peToElf)) {
        Write-Error "PE to ELF converter missing: $peToElf"
        exit 1
    }

    if (-not $pythonExe) {
        Write-Error "Python is required for PE?ELF conversion but was not found."
        exit 1
    }

    Write-Info "Converting PE to ELF64 with KMain entry point..."
    
    # Use the enhanced pe_to_elf_v2.py which searches for function prologue
    # This handles NativeAOT RuntimeExport symbols that may point to epilogue
    $peToElfV2 = Join-Path $RootDir "tools\pe_to_elf_v2.py"
    
    # Check if map file exists (contains symbol addresses)
    if (Test-Path $kernelMap) {
        Write-Info "Using map file for entry point: $kernelMap"
        $conversionArgs = @($peToElfV2, $kernelPE, $kernelELF, "--map", $kernelMap, "--symbol", "KMain")
    } else {
        Write-Warning "Map file not found - using PE entry point"
        $conversionArgs = @($peToElfV2, $kernelPE, $kernelELF)
    }
    
    & $pythonExe @pythonExeArgs @conversionArgs
    
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path $kernelELF)) {
        Write-Error "PE?ELF conversion failed"
        exit 1
    }

    $hdr = [System.IO.File]::ReadAllBytes($kernelELF)
    $isElf = ($hdr.Length -ge 4 -and $hdr[0] -eq 0x7F -and $hdr[1] -eq 0x45 -and $hdr[2] -eq 0x4C -and $hdr[3] -eq 0x46)
    if (-not $isElf) {
        Write-Error "Conversion did not produce ELF output"
        exit 1
    }

    $size = (Get-Item $kernelELF).Length
    Write-Success "Kernel converted to ELF64 ($([math]::Round($size/1MB, 2)) MB)"
} else {
    Write-Header "[3/5] Skipping Kernel Conversion"
}

# Step 4: Build Ramdisk
if (-not $SkipRamdisk) {
    Write-Header "[4/5] Building Ramdisk Image"
    
    if (-not (Test-Path $RamdiskSrc)) {
        Write-Warning "Ramdisk source directory not found: $RamdiskSrc"
        Write-Warning "Creating minimal ramdisk structure..."
        
        # Create minimal ramdisk structure
        New-Item -ItemType Directory -Force -Path "$RamdiskSrc\Images" | Out-Null
        New-Item -ItemType Directory -Force -Path "$RamdiskSrc\Fonts" | Out-Null
        
        # Create placeholder files
        Write-Info "Creating placeholder files (you should replace these with real assets)..."
        
        # Create 16x16 placeholder PNGs (minimal valid PNG)
        $pngHeader = [byte[]](0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A)
        Set-Content -Path "$RamdiskSrc\Images\Cursor.png" -Value $pngHeader -Encoding Byte
        Set-Content -Path "$RamdiskSrc\Images\Grab.png" -Value $pngHeader -Encoding Byte
        Set-Content -Path "$RamdiskSrc\Images\Busy.png" -Value $pngHeader -Encoding Byte
        
        # Create placeholder font file
        Set-Content -Path "$RamdiskSrc\Fonts\enludo.btf" -Value "PLACEHOLDER_FONT" -Encoding ASCII
        
        Write-Warning "Created placeholder files - replace with real assets!"
    }

    $ramdiskBuilder = (Resolve-Path "$ToolsDir\ramdisk_builder.py").Path
    $ramdiskOutput = (Join-Path $RootDir "ramdisk.img")
    $ramdiskSource = (Resolve-Path $RamdiskSrc).Path
    
    if (-not (Test-Path $ramdiskBuilder)) {
        Write-Error "Ramdisk builder not found: $ramdiskBuilder"
        exit 1
    }
    
    Write-Info "Building ramdisk with Python..."
    & $pythonExe @pythonExeArgs $ramdiskBuilder $ramdiskSource $ramdiskOutput
    
    if ($LASTEXITCODE -eq 0 -and (Test-Path $ramdiskOutput)) {
        $size = (Get-Item $ramdiskOutput).Length
        Write-Success "Ramdisk built successfully ($([math]::Round($size/1KB, 2)) KB)"
        $identityTool = Join-Path $RootDir "Tools\ManagedArtifacts\managed_artifacts.py"
        $identitySource = Join-Path $RootDir "guideXOS\obj\ManagedArtifactIdentities.g.cs"
        $identityReport = Join-Path $RootDir "out\h2c-managed-artifact-audit.csv"
        $identityStage = if ($SkipKernel) { "generated" } else { "after" }
        $identityVerifyArgs = @($identityTool, "--audit-only", "--stage", $identityStage, "--ramdisk-source", (Join-Path $RamdiskSrc "Native"), "--ramdisk-image", $ramdiskOutput, "--report-csv", $identityReport)
        if (-not $SkipKernel) {
            $identityVerifyArgs += @("--identity-source", $identitySource, "--kernel-built")
        }
        & $pythonExe @pythonExeArgs @identityVerifyArgs
        if ($LASTEXITCODE -ne 0) {
            throw "Final ramdisk managed-artifact verification failed: $LASTEXITCODE"
        }
    } else {
        Write-Error "Ramdisk build failed"
        exit 1
    }
} else {
    Write-Header "[4/5] Skipping Ramdisk Build"
}

# Step 5: Assemble ESP Structure
Write-Header "[5/5] Assembling ESP Structure"

# Create ESP directory structure
New-Item -ItemType Directory -Force -Path "$ESPDir\EFI\BOOT" | Out-Null
Write-Success "Created ESP directory structure"

# Copy bootloader (try .efi first, then .exe)
$bootloaderSrcEfi = "$RootDir\guideXOSBootLoader\x64\Release\guideXOSBootLoader.efi"
$bootloaderSrcExe = "$RootDir\guideXOSBootLoader\x64\Release\guideXOSBootLoader.exe"
$bootloaderDst = "$ESPDir\EFI\BOOT\BOOTX64.EFI"

if (Test-Path $bootloaderSrcEfi) {
    Copy-Item $bootloaderSrcEfi $bootloaderDst -Force
    $bootSrc = Get-ArtifactSnapshot $bootloaderSrcEfi
    $bootDst = Get-ArtifactSnapshot $bootloaderDst
    if ($bootSrc.Hash -ne $bootDst.Hash) {
        Write-Error "Bootloader copy verification failed"
        Write-Error "  source: $($bootSrc.Path)"
        Write-Error "  dest:   $($bootDst.Path)"
        exit 1
    }
    Write-Success "Copied bootloader: BOOTX64.EFI (from .efi)"
    Write-Info "  source: $($bootSrc.Path)"
    Write-Info "  dest:   $($bootDst.Path)"
    Write-Info "  source timestamp/hash: $($bootSrc.LastWriteTime) / $($bootSrc.Hash)"
    Write-Info "  dest timestamp/hash:   $($bootDst.LastWriteTime) / $($bootDst.Hash)"
} elseif (Test-Path $bootloaderSrcExe) {
    Copy-Item $bootloaderSrcExe $bootloaderDst -Force
    $bootSrc = Get-ArtifactSnapshot $bootloaderSrcExe
    $bootDst = Get-ArtifactSnapshot $bootloaderDst
    if ($bootSrc.Hash -ne $bootDst.Hash) {
        Write-Error "Bootloader copy verification failed"
        Write-Error "  source: $($bootSrc.Path)"
        Write-Error "  dest:   $($bootDst.Path)"
        exit 1
    }
    Write-Success "Copied bootloader: BOOTX64.EFI (from .exe)"
    Write-Info "  source: $($bootSrc.Path)"
    Write-Info "  dest:   $($bootDst.Path)"
    Write-Info "  source timestamp/hash: $($bootSrc.LastWriteTime) / $($bootSrc.Hash)"
    Write-Info "  dest timestamp/hash:   $($bootDst.LastWriteTime) / $($bootDst.Hash)"
} elseif ($SkipBootloader -and (Test-Path $bootloaderDst)) {
    Write-Warning "Bootloader build skipped; preserving existing ESP BOOTX64.EFI"
} else {
    Write-Error "Bootloader not found: $bootloaderSrcEfi or $bootloaderSrcExe"
    exit 1
}

# Copy kernel
$kernelSrc = "$RootDir\kernel.elf"
$kernelDst = "$ESPDir\kernel.elf"
if (Test-Path $kernelSrc) {
    Copy-Item $kernelSrc $kernelDst -Force
    $kernelSrcInfo = Get-ArtifactSnapshot $kernelSrc
    $kernelDstInfo = Get-ArtifactSnapshot $kernelDst
    if (-not $kernelDstInfo -or $kernelSrcInfo.Hash -ne $kernelDstInfo.Hash) {
        Write-Error "Kernel copy verification failed"
        Write-Error "  source: $($kernelSrcInfo.Path)"
        Write-Error "  dest:   $kernelDst"
        exit 1
    }
    Write-Success "Copied kernel: kernel.elf"
    Write-Info "  source: $($kernelSrcInfo.Path)"
    Write-Info "  dest:   $($kernelDstInfo.Path)"
    Write-Info "  source timestamp/hash: $($kernelSrcInfo.LastWriteTime) / $($kernelSrcInfo.Hash)"
    Write-Info "  dest timestamp/hash:   $($kernelDstInfo.LastWriteTime) / $($kernelDstInfo.Hash)"
} else {
    Write-Error "Kernel ELF not found: $kernelSrc"
    exit 1
}

# Copy ramdisk
$ramdiskSrc = "$RootDir\ramdisk.img"
$ramdiskDst = "$ESPDir\ramdisk.img"
if (Test-Path $ramdiskSrc) {
    Copy-Item $ramdiskSrc $ramdiskDst -Force
    $ramdiskSrcInfo = Get-ArtifactSnapshot $ramdiskSrc
    $ramdiskDstInfo = Get-ArtifactSnapshot $ramdiskDst
    if (-not $ramdiskDstInfo -or $ramdiskSrcInfo.Hash -ne $ramdiskDstInfo.Hash) {
        Write-Error "Ramdisk copy verification failed"
        Write-Error "  source: $($ramdiskSrcInfo.Path)"
        Write-Error "  dest:   $ramdiskDst"
        exit 1
    }
    Write-Success "Copied ramdisk: ramdisk.img"
    Write-Info "  source SHA-256: $($ramdiskSrcInfo.Hash)"
    Write-Info "  staged SHA-256: $($ramdiskDstInfo.Hash)"
} else {
    Write-Warning "Ramdisk not found: $ramdiskSrc"
}

# Verify ESP structure
Write-Header "Build Summary"
Write-Info "ESP Structure:"
if (Test-Path "$ESPDir\EFI\BOOT\BOOTX64.EFI") {
    $size = (Get-Item "$ESPDir\EFI\BOOT\BOOTX64.EFI").Length
    Write-Success "  ESP\EFI\BOOT\BOOTX64.EFI ($([math]::Round($size/1KB, 2)) KB)"
} else {
    Write-Error "  ESP\EFI\BOOT\BOOTX64.EFI - MISSING"
}

if (Test-Path "$ESPDir\kernel.elf") {
    $size = (Get-Item "$ESPDir\kernel.elf").Length
    Write-Success "  ESP\kernel.elf ($([math]::Round($size/1MB, 2)) MB)"
} else {
    Write-Error "  ESP\kernel.elf - MISSING"
}

if (Test-Path "$ESPDir\ramdisk.img") {
    $size = (Get-Item "$ESPDir\ramdisk.img").Length
    Write-Success "  ESP\ramdisk.img ($([math]::Round($size/1KB, 2)) KB)"
} else {
    Write-Error "  ESP\ramdisk.img - MISSING"
}

# Optional: Create bootable image
if ($CreateISO) {
    Write-Header "Creating Bootable ISO"
    Write-Warning "ISO creation requires external tools (mkisofs/genisoimage)"
    Write-Info "Use WSL or Linux for ISO creation, or create disk image instead"
}

Write-Header "Build Complete!"
Write-Info "ESP directory: $ESPDir"
Write-Info ""
Write-Info "Next steps:"
Write-Info "  1. Test in QEMU:"
Write-Info "     qemu-system-x86_64 -bios OVMF.fd -hda fat:rw:ESP -m 1024M -serial stdio"
Write-Info ""
Write-Info "  2. Create disk image (Linux/WSL):"
Write-Info "     dd if=/dev/zero of=guidexos.img bs=1M count=100"
Write-Info "     mkfs.vfat -F 32 guidexos.img"
Write-Info "     sudo mount -o loop guidexos.img /mnt"
Write-Info "     sudo cp -r ESP/* /mnt/"
Write-Info "     sudo umount /mnt"
Write-Info ""
Write-Info "  3. Test with disk image:"
Write-Info "     qemu-system-x86_64 -bios OVMF.fd -drive file=guidexos.img,format=raw -m 1024M -serial stdio"
