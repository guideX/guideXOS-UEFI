@echo off
REM Boot guideXOS in QEMU with UEFI firmware

echo ========================================
echo    Booting guideXOS in QEMU
echo ========================================
echo.

REM Change to the directory where this script lives.
cd /d "%~dp0"

echo Refreshing build and ESP before boot...
powershell.exe -ExecutionPolicy Bypass -File "build.ps1"
if errorlevel 1 (
    echo ERROR: build.ps1 failed; refusing to boot stale ESP content.
    pause
    exit /b 1
)

REM Verify files exist
echo Checking files...
if not exist "OVMF.fd" (
    echo ERROR: OVMF.fd not found!
    pause
    exit /b 1
) else (
    echo   [OK] OVMF.fd found
)

if not exist "ESP\EFI\BOOT\BOOTX64.EFI" (
    echo ERROR: ESP\EFI\BOOT\BOOTX64.EFI not found!
    echo Run build.ps1 first to build the bootloader.
    pause
    exit /b 1
)
echo   [OK] BOOTX64.EFI found

if not exist "ESP\kernel.elf" (
    echo ERROR: ESP\kernel.elf not found!
    pause
    exit /b 1
)
echo   [OK] kernel.elf found

if not exist "ESP\ramdisk.img" (
    echo WARNING: ESP\ramdisk.img not found - continuing anyway
) else (
    echo   [OK] ramdisk.img found
)

REM Ensure the UEFI Shell will automatically launch our bootloader.
REM Many OVMF setups default to the internal shell when NVRAM vars are missing.
REM Putting startup.nsh at the FS root makes the shell chainload BOOTX64.EFI.
if not exist "ESP\startup.nsh" (
    echo Creating ESP\startup.nsh to chainload EFI\BOOT\BOOTX64.EFI ...
    > "ESP\startup.nsh" echo fs0:
    >> "ESP\startup.nsh" echo \EFI\BOOT\BOOTX64.EFI
) else (
    echo   [OK] startup.nsh found
)

echo.
echo Starting QEMU...
echo Press Ctrl+C in this window to exit QEMU
echo Serial output will appear below:
echo ----------------------------------------
echo.

REM NOTE:
REM Use QEMU's FAT block device and attach it as a real drive.

"C:\Program Files\qemu\qemu-system-x86_64.exe" ^
-machine pc ^
-drive if=pflash,format=raw,readonly=on,file=OVMF.fd ^
-drive if=none,id=esp,format=raw,file=fat:rw:ESP ^
-device ide-hd,drive=esp ^
-m 1024M ^
-serial stdio ^
-no-reboot ^
-name "guideXOS" ^
-boot menu=off,splash-time=0

echo.
echo ----------------------------------------
echo QEMU exited.
pause
