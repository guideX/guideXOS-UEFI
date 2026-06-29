# Boot guideXOS in QEMU (PowerShell version)
# Uses the validated bundled QEMU UEFI firmware path.

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "   Booting guideXOS in QEMU" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Change to the directory where this script lives.
Set-Location $PSScriptRoot

Write-Host "Refreshing build and ESP before boot..." -ForegroundColor Cyan
& powershell.exe -ExecutionPolicy Bypass -File "$PSScriptRoot\build.ps1"
if ($LASTEXITCODE -ne 0) {
    Write-Host "  ? build.ps1 failed; refusing to boot stale ESP content." -ForegroundColor Red
    exit 1
}

# Verify files exist
Write-Host "Checking files..." -ForegroundColor Yellow

$filesOK = $true

$qemuFirmwareRoot = Join-Path $PSScriptRoot 'bin\qemu-firmware'
$qemuFirmwareCode = Join-Path $qemuFirmwareRoot 'edk2-x86_64-code.fd'
$qemuFirmwareVars = Join-Path $qemuFirmwareRoot 'edk2-vars.fd'
$qemuFirmwareCodeSource = 'C:\Program Files\qemu\share\edk2-x86_64-code.fd'
$qemuFirmwareVarsSource = 'C:\Program Files\qemu\share\edk2-i386-vars.fd'

New-Item -ItemType Directory -Path $qemuFirmwareRoot -Force | Out-Null

if (-not (Test-Path $qemuFirmwareCodeSource)) {
    Write-Host "  ? Bundled UEFI code firmware not found: $qemuFirmwareCodeSource" -ForegroundColor Red
    $filesOK = $false
} else {
    Write-Host "  ? Bundled UEFI code firmware found" -ForegroundColor Green
}

if (-not (Test-Path $qemuFirmwareVarsSource)) {
    Write-Host "  ? Bundled UEFI vars template not found: $qemuFirmwareVarsSource" -ForegroundColor Red
    $filesOK = $false
} else {
    Write-Host "  ? Bundled UEFI vars template found" -ForegroundColor Green
}

if (-not (Test-Path "ESP\EFI\BOOT\BOOTX64.EFI")) {
    Write-Host "  ? BOOTX64.EFI not found!" -ForegroundColor Red
    $filesOK = $false
} else {
    Write-Host "  ? BOOTX64.EFI found" -ForegroundColor Green
}

if (-not (Test-Path "ESP\kernel.elf")) {
    Write-Host "  ? kernel.elf not found!" -ForegroundColor Red
    $filesOK = $false
} else {
    # Verify ELF
    $bytes = [System.IO.File]::ReadAllBytes("ESP\kernel.elf")
    if ($bytes[0] -eq 0x7F -and $bytes[1] -eq 0x45 -and $bytes[2] -eq 0x4C -and $bytes[3] -eq 0x46) {
        Write-Host "  ? kernel.elf found (valid ELF)" -ForegroundColor Green
    } else {
        Write-Host "  ? kernel.elf is NOT valid ELF!" -ForegroundColor Red
        Write-Host "     Run: .\convert_kernel_manual.ps1" -ForegroundColor Yellow
        $filesOK = $false
    }
}

if (-not (Test-Path "ESP\ramdisk.img")) {
    Write-Host "  ??  ramdisk.img not found (optional)" -ForegroundColor Yellow
} else {
    Write-Host "  ? ramdisk.img found" -ForegroundColor Green
}

if (-not (Test-Path "ESP\startup.nsh")) {
    Write-Host "  Creating ESP\startup.nsh" -ForegroundColor Yellow
    "fs0:", "\EFI\BOOT\BOOTX64.EFI" | Set-Content -Path "ESP\startup.nsh" -Encoding ASCII
} else {
    Write-Host "  ? startup.nsh found" -ForegroundColor Green
}

Write-Host ""

if (-not $filesOK) {
    Write-Host "? Missing required files!" -ForegroundColor Red
    Write-Host ""
    Write-Host "Run these commands:" -ForegroundColor Yellow
    Write-Host "  1. .\build.ps1" -ForegroundColor Cyan
    Write-Host "  2. .\convert_kernel_manual.ps1" -ForegroundColor Cyan
    Write-Host ""
    pause
    exit 1
}

Copy-Item -LiteralPath $qemuFirmwareCodeSource -Destination $qemuFirmwareCode -Force
Copy-Item -LiteralPath $qemuFirmwareVarsSource -Destination $qemuFirmwareVars -Force

Write-Host "Starting QEMU..." -ForegroundColor Green
Write-Host "Press Ctrl+C in this window to exit QEMU" -ForegroundColor Yellow
Write-Host ""

# Launch QEMU with pflash (validated method)
try {
    & "C:\Program Files\qemu\qemu-system-x86_64.exe" `
        -machine pc-q35-8.2 `
        -drive if=pflash,format=raw,readonly=on,file=$qemuFirmwareCode `
        -drive if=pflash,format=raw,file=$qemuFirmwareVars `
        -drive if=none,id=esp,format=raw,file=fat:rw:ESP `
        -device ide-hd,drive=esp `
        -m 1024M `
        -serial stdio `
        -name "guideXOS" `
        -no-reboot `
        -boot menu=off,splash-time=0
} catch {
    Write-Host ""
    Write-Host "? QEMU failed to start!" -ForegroundColor Red
    Write-Host "Error: $_" -ForegroundColor Red
    Write-Host ""
    Write-Host "Make sure QEMU is installed at: C:\Program Files\qemu\" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "QEMU exited." -ForegroundColor Cyan
Write-Host ""
pause
