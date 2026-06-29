@echo off
REM Boot guideXOS in QEMU with the validated bundled UEFI firmware path.

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

echo Checking files...
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

if not exist "ESP\startup.nsh" (
    echo Creating ESP\startup.nsh to chainload EFI\BOOT\BOOTX64.EFI ...
    > "ESP\startup.nsh" echo fs0:
    >> "ESP\startup.nsh" echo \EFI\BOOT\BOOTX64.EFI
) else (
    echo   [OK] startup.nsh found
)

if not exist "bin\qemu-firmware" mkdir "bin\qemu-firmware"
copy /Y "C:\Program Files\qemu\share\edk2-x86_64-code.fd" "bin\qemu-firmware\edk2-x86_64-code.fd" >nul
copy /Y "C:\Program Files\qemu\share\edk2-i386-vars.fd" "bin\qemu-firmware\edk2-vars.fd" >nul

echo.
echo Starting QEMU...
echo Press Ctrl+C in this window to exit QEMU
echo Serial output will appear below:
echo ----------------------------------------
echo.

"C:\Program Files\qemu\qemu-system-x86_64.exe" ^
-machine pc-q35-8.2 ^
-drive if=pflash,format=raw,readonly=on,file=bin\qemu-firmware\edk2-x86_64-code.fd ^
-drive if=pflash,format=raw,file=bin\qemu-firmware\edk2-vars.fd ^
-drive if=none,id=esp,format=raw,file=fat:rw:ESP ^
-device ide-hd,drive=esp ^
-m 1024M ^
-serial stdio ^
-no-reboot ^
-boot menu=off,splash-time=0 ^
-name "guideXOS"

echo.
echo ----------------------------------------
echo QEMU exited.
pause
