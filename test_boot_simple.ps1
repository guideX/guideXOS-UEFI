#!/usr/bin/env pwsh
# Simple boot test with marker analysis
# Just runs run_qemu.bat and analyzes the console output

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "   Running guideXOS Boot Test" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

Write-Host "Starting QEMU via run_qemu.bat..." -ForegroundColor Green
Write-Host "Watch for these key markers:" -ForegroundColor Yellow
Write-Host ""
Write-Host "  PROG       - Program.KMain() entered" -ForegroundColor White
Write-Host "  ANIManim   - Animator initialized" -ForegroundColor White
Write-Host "  KBDkbd     - Keyboard ready" -ForegroundColor White
Write-Host "  MSEmse     - Mouse ready" -ForegroundColor White
Write-Host "  VMSKIP     - VMware skipped" -ForegroundColor White
Write-Host "  USBSK      - USB skipped" -ForegroundColor White
Write-Host "  IMG123img  - Cursors loaded" -ForegroundColor White
Write-Host "  FNTfnt     - Font skipped" -ForegroundColor White
Write-Host "  WM[]wm     - WindowManager initialized" -ForegroundColor White
Write-Host "  DS[]ds     - Desktop initialized" -ForegroundColor White
Write-Host "  SUB[]sub   - Subsystems initialized" -ForegroundColor White
Write-Host "  CFG[]cfg   - Configuration loaded" -ForegroundColor White
Write-Host "  =LOOP=     - ** ENTERING MAIN LOOP! **" -ForegroundColor Green
Write-Host "  SMAIN      - ** SMain() STARTED! **" -ForegroundColor Green
Write-Host ""
Write-Host "Press Ctrl+C to stop QEMU" -ForegroundColor Yellow
Write-Host "========================================" -ForegroundColor Gray
Write-Host ""

Write-Host "Refreshing build and ESP before QEMU..." -ForegroundColor Cyan
& powershell.exe -ExecutionPolicy Bypass -File "$PSScriptRoot\build.ps1"
if ($LASTEXITCODE -ne 0) {
	Write-Host "Build refresh failed; refusing to start QEMU." -ForegroundColor Red
	exit 1
}

# Run the batch file
.\run_qemu.bat

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Boot test complete" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
