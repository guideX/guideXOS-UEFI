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
    [switch]$BootloaderOnly
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

function Find-VcVars64($msbuildPath) {
    $candidateVcVars = @()
    if ($msbuildPath -and $msbuildPath -ne "msbuild") {
        $msbuildExe = Get-Item $msbuildPath
        $vsInstallDir = Split-Path (Split-Path (Split-Path (Split-Path $msbuildExe.FullName -Parent) -Parent) -Parent) -Parent
        $candidateVcVars += (Join-Path $vsInstallDir "VC\Auxiliary\Build\vcvars64.bat")
    }
    $candidateVcVars += @(
        "C:\Program Files\Microsoft Visual Studio\18\Community\VC\Auxiliary\Build\vcvars64.bat",
        "C:\Program Files\Microsoft Visual Studio\18\Professional\VC\Auxiliary\Build\vcvars64.bat",
        "C:\Program Files\Microsoft Visual Studio\18\Enterprise\VC\Auxiliary\Build\vcvars64.bat",
        "C:\Program Files\Microsoft Visual Studio\2022\Community\VC\Auxiliary\Build\vcvars64.bat",
        "C:\Program Files\Microsoft Visual Studio\2022\Professional\VC\Auxiliary\Build\vcvars64.bat",
        "C:\Program Files\Microsoft Visual Studio\2022\Enterprise\VC\Auxiliary\Build\vcvars64.bat"
    )
    foreach ($path in $candidateVcVars) {
        if ($path -and (Test-Path $path)) {
            return $path
        }
    }
    return $null
}

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
if (-not $SkipKernel) {
    if (-not (Test-Command dotnet)) {
        Write-Error ".NET SDK not found! Install .NET 7 SDK"
        exit 1
    }
    $dotnetVersion = dotnet --version
    Write-Success ".NET SDK found: $dotnetVersion"
    if (-not $vcvars64) {
        $vcvars64 = Find-VcVars64 $null
    }
}

# Check for Python
$pythonExe = $null
$pythonExeArgs = @()
if ((-not $SkipRamdisk) -or (-not $SkipConversion)) {
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
    } elseif (-not $SkipRamdisk) {
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
    Remove-Item Env:PATH -ErrorAction SilentlyContinue
    
    Write-Info "Building with MSBuild..."
    & $msbuild $BootloaderProject `
        /p:Configuration=Release `
        /p:Platform=x64 /p:IntDir=x64\Release\build\ `
        /v:minimal `
        /nologo `
        /t:Rebuild
    
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

    if ($vcvars64) {
        Write-Info "Using Visual C++ environment: $vcvars64"
        $publishArgs = "dotnet publish `"$KernelProject`" -c Release"
        if ($skipKernelRestore) {
            $publishArgs += " --no-restore"
        }
        $cmdPathPrefix = "set `"PATH=$env:SystemRoot\System32;C:\Program Files\dotnet`" && "
        & "$env:SystemRoot\System32\cmd.exe" /d /s /c "$cmdPathPrefix call `"$vcvars64`" >nul && $publishArgs"
    } else {
        if ($skipKernelRestore) {
            dotnet publish $KernelProject -c Release --no-restore
        } else {
            dotnet publish $KernelProject -c Release
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
    Write-Success "Copied ramdisk: ramdisk.img"
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
