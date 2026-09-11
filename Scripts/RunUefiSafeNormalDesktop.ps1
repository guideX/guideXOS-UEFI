[CmdletBinding()]
param(
    [switch]$Tiny,
    [switch]$FirstFrame,
    [ValidateRange(1, 10000)]
    [int]$Frames = 0,
    [switch]$GuiVisible,
    [ValidateRange(10, 600)]
    [int]$TimeoutSeconds = 60
)

$ErrorActionPreference = 'Stop'

if (($Tiny -and $FirstFrame) -or ($Tiny -and $Frames -gt 0) -or ($FirstFrame -and $Frames -gt 0)) {
    throw 'Choose only one bounded mode: -Tiny, -FirstFrame, or -Frames N.'
}

$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$programPath = Join-Path $root 'guideXOS\Program.cs'
$buildScript = Join-Path $root 'build.ps1'
$qemuExe = 'C:\Program Files\qemu\qemu-system-x86_64.exe'
$logRoot = Join-Path $root 'bin\uefi-run-logs'
$qemuFirmwareRoot = Join-Path $root 'bin\qemu-firmware'
$qemuFirmwareCode = Join-Path $qemuFirmwareRoot 'edk2-x86_64-code.fd'
$qemuFirmwareVars = Join-Path $qemuFirmwareRoot 'edk2-vars.fd'
$qemuFirmwareCodeSource = 'C:\Program Files\qemu\share\edk2-x86_64-code.fd'
$qemuFirmwareVarsSource = 'C:\Program Files\qemu\share\edk2-i386-vars.fd'

New-Item -ItemType Directory -Path $logRoot -Force | Out-Null
New-Item -ItemType Directory -Path $qemuFirmwareRoot -Force | Out-Null

if (-not (Test-Path -LiteralPath $qemuExe)) {
    throw "QEMU executable not found: $qemuExe"
}

function Set-RecoveryFlag {
    param(
        [Parameter(Mandatory = $true)] [string]$Text,
        [Parameter(Mandatory = $true)] [string]$Name,
        [Parameter(Mandatory = $true)] [string]$Value
    )

    $pattern = "(?m)^(\s*private const (?:bool|int) $Name = )[^;]+;"
    $matches = [regex]::Matches($Text, $pattern)
    if ($matches.Count -ne 1) {
        throw "Expected exactly one recovery flag named $Name, found $($matches.Count)."
    }

    return [regex]::Replace($Text, $pattern, ('${1}' + $Value + ';'), 1)
}

function Read-SharedText {
    param([Parameter(Mandatory = $true)] [string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        return ''
    }

    $stream = [System.IO.File]::Open(
        $Path,
        [System.IO.FileMode]::Open,
        [System.IO.FileAccess]::Read,
        [System.IO.FileShare]::ReadWrite
    )
    $reader = New-Object System.IO.StreamReader($stream)
    try {
        return $reader.ReadToEnd()
    } finally {
        $reader.Dispose()
        $stream.Dispose()
    }
}

function Get-ModeName {
    if ($Tiny) { return 'Tiny' }
    if ($FirstFrame) { return 'FirstFrame' }
    if ($Frames -gt 0) { return "Frames$Frames" }
    return 'Default'
}

function Get-ExpectedMarkers {
    $common = @(
        '[BOOT_MODE] UEFI',
        '[NATIVEAOT] modules initialized',
        '[ACPI] initialized',
        '[PCI] enumerated',
        '[FRAMEBUFFER] initialized',
        '[FS] mounted',
        '[DESKTOP] initialized',
        '[SMAIN] initialized'
    )

    if ($Tiny) {
        return $common + @(
            'SMAIN_DISPATCH_REASON=TINY_UEFI',
            'UTINY_PIXEL_SAMPLE_VALID=1',
            'UTINY_PATTERN_DRAWN',
            'UTINY_HALT_ENTER'
        )
    }

    if ($FirstFrame) {
        return $common + @(
            'SMAIN_DISPATCH_REASON=NORMAL_DESKTOP_UEFI_FIRST_FRAME',
            'NORMAL_FRAME_COMPLETE',
            'NORMAL_FRAME_HALT_ENTER'
        )
    }

    if ($Frames -gt 0) {
        return $common + @(
            'SMAIN_DISPATCH_REASON=MULTIFRAME_NORMAL_DESKTOP_UEFI',
            "MULTIFRAME_TARGET=$Frames",
            "MULTIFRAME_LAST_COMPLETED_FRAME=$Frames",
            'MULTIFRAME_COMPLETE',
            'MULTIFRAME_HALT_ENTER'
        )
    }

    return $common + @(
        'SMAIN_DISPATCH_REASON=UEFI_DEFAULT_RECOVERY',
        'UEFI_DEFAULT_FRAME_COMPLETE',
        'UEFI_DEFAULT_HALT_ENTER'
    )
}

function Get-MissingMarkers {
    param(
        [Parameter(Mandatory = $true)] [AllowEmptyString()] [string]$Text,
        [Parameter(Mandatory = $true)] [string[]]$Markers
    )

    return @($Markers | Where-Object { -not $Text.Contains($_) })
}

$modeName = Get-ModeName
$runStamp = Get-Date -Format 'yyyyMMdd_HHmmss_fff'
$runId = "UEFI_RUN_ID_${runStamp}_PID$PID"
$serialLog = Join-Path $logRoot "serial_$runId.txt"
$stderrLog = Join-Path $logRoot "qemu_stderr_$runId.txt"
$buildLog = Join-Path $logRoot "build_$runId.txt"
$summaryLog = Join-Path $logRoot "summary_$runId.txt"
$qemuProcess = $null
$originalProgram = [System.IO.File]::ReadAllText($programPath)

try {
    $patchedProgram = $originalProgram
    $patchedProgram = Set-RecoveryFlag $patchedProgram 'UEFI_ENABLE_UTINY_DIAGNOSTIC' $(if ($Tiny) { 'true' } else { 'false' })
    $patchedProgram = Set-RecoveryFlag $patchedProgram 'UEFI_ENABLE_NORMAL_DESKTOP_FIRST_FRAME' $(if ($FirstFrame) { 'true' } else { 'false' })
    $patchedProgram = Set-RecoveryFlag $patchedProgram 'UEFI_ENABLE_NORMAL_DESKTOP_BOUNDED' $(if ($Frames -gt 0) { 'true' } else { 'false' })
    $patchedProgram = Set-RecoveryFlag $patchedProgram 'UEFI_NORMAL_DESKTOP_BOUNDED_FRAME_TARGET' $Frames
    $patchedProgram = Set-RecoveryFlag $patchedProgram 'UEFI_ENABLE_CONTINUOUS_DESKTOP' 'false'

    $runMarker = "        SerialBreadcrumb(`"$runId`");"
    $anchor = '        BootConsole.WriteLine("[SMAIN] initialized");'
    if ([regex]::Matches($patchedProgram, [regex]::Escape($anchor)).Count -ne 1) {
        throw 'Could not find the unique SMain run-id anchor.'
    }
    $patchedProgram = $patchedProgram.Replace($anchor, $anchor + "`r`n" + $runMarker)
    [System.IO.File]::WriteAllText($programPath, $patchedProgram)

    Write-Host "[uefi-run] Run ID: $runId" -ForegroundColor Cyan
    Write-Host "[uefi-run] Mode: $modeName" -ForegroundColor Cyan
    Write-Host '[uefi-run] Continuous desktop remains disabled by source phase gate.' -ForegroundColor Cyan
    Write-Host '[uefi-run] Building fresh kernel and boot media...' -ForegroundColor Cyan

    & powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File $buildScript *> $buildLog
    if ($LASTEXITCODE -ne 0) {
        Get-Content -LiteralPath $buildLog | Select-Object -Last 40 | ForEach-Object { Write-Host "[uefi-run] $_" -ForegroundColor Yellow }
        throw "build.ps1 failed with exit code $LASTEXITCODE"
    }

    Copy-Item -LiteralPath $qemuFirmwareCodeSource -Destination $qemuFirmwareCode -Force
    Copy-Item -LiteralPath $qemuFirmwareVarsSource -Destination $qemuFirmwareVars -Force

    $qemuArgs = @(
        '-machine', 'pc-q35-8.2',
        '-drive', ('if=pflash,format=raw,readonly=on,file=' + $qemuFirmwareCode),
        '-drive', ('if=pflash,format=raw,file=' + $qemuFirmwareVars),
        '-drive', 'if=none,id=esp,format=raw,file=fat:rw:ESP',
        '-device', 'ide-hd,drive=esp',
        '-m', '1024M',
        '-serial', ('file:' + $serialLog),
        '-no-reboot',
        '-boot', 'menu=off,splash-time=0',
        '-name', 'guideXOS'
    )

    $windowStyle = if ($GuiVisible) { 'Normal' } else { 'Hidden' }
    Write-Host '[uefi-run] Launching fresh QEMU instance...' -ForegroundColor Cyan
    $qemuProcess = Start-Process -FilePath $qemuExe `
        -ArgumentList $qemuArgs `
        -WorkingDirectory $root `
        -WindowStyle $windowStyle `
        -PassThru `
        -RedirectStandardError $stderrLog

    $expectedMarkers = @(Get-ExpectedMarkers)
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        if ($qemuProcess.HasExited) {
            break
        }

        $liveText = Read-SharedText $serialLog
        if ((Get-MissingMarkers $liveText $expectedMarkers).Count -eq 0) {
            break
        }

        Start-Sleep -Milliseconds 250
    }

    $serialText = Read-SharedText $serialLog
    $missingMarkers = @(Get-MissingMarkers $serialText $expectedMarkers)
    $faultLines = @($serialText -split "`r?`n" | Where-Object {
        $_ -match 'CPU_FAULT|PAGE_FAULT|GENERAL_PROTECTION|#PF|#GP|MULTIFRAME_FAULT|NORMAL_FRAME_FAULT|UTINY_FAULT'
    })
    $timedOut = $missingMarkers.Count -gt 0

    if ($qemuProcess -and -not $qemuProcess.HasExited) {
        Stop-Process -Id $qemuProcess.Id -Force -ErrorAction SilentlyContinue
        $qemuProcess.WaitForExit()
    }

    $validRun = ($missingMarkers.Count -eq 0) -and ($faultLines.Count -eq 0) -and $serialText.Contains($runId)
    $summary = @(
        "RUN_ID=$runId",
        "MODE=$modeName",
        "VALID=$validRun",
        "TIMED_OUT=$timedOut",
        "CPU_FAULT_LINES=$($faultLines.Count)",
        "MISSING_MARKERS=$($missingMarkers -join ',')",
        "SERIAL_LOG=$serialLog",
        "BUILD_LOG=$buildLog",
        "QEMU_STDERR_LOG=$stderrLog"
    )
    [System.IO.File]::WriteAllLines($summaryLog, $summary)

    Write-Host "[uefi-run] Valid: $validRun" -ForegroundColor $(if ($validRun) { 'Green' } else { 'Red' })
    if ($missingMarkers.Count -gt 0) {
        Write-Host "[uefi-run] Missing markers: $($missingMarkers -join ', ')" -ForegroundColor Yellow
    }
    if ($faultLines.Count -gt 0) {
        Write-Host '[uefi-run] CPU fault diagnostics:' -ForegroundColor Yellow
        $faultLines | Select-Object -Last 12 | ForEach-Object { Write-Host "[uefi-run]   $_" -ForegroundColor Yellow }
    }
    Write-Host "[uefi-run] Serial log: $serialLog" -ForegroundColor Cyan
    Write-Host "[uefi-run] Summary: $summaryLog" -ForegroundColor Cyan

    if (-not $validRun) {
        throw "UEFI $modeName regression failed."
    }
} finally {
    if ($qemuProcess -and -not $qemuProcess.HasExited) {
        Stop-Process -Id $qemuProcess.Id -Force -ErrorAction SilentlyContinue
    }

    [System.IO.File]::WriteAllText($programPath, $originalProgram)
}
