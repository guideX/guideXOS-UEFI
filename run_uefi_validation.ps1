#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Build and validate guideXOS through the cleaned QEMU UEFI path.

.DESCRIPTION
    With no diagnostic selector, this runs the ordinary production UEFI path,
    which is the continuous desktop. Diagnostic selectors create a separate
    build variant and never change the production source defaults.

.PARAMETER Continuous
    Explicitly label the ordinary continuous desktop validation.

.PARAMETER Tiny
    Build and run the UTINY regression path.

.PARAMETER FirstFrame
    Build and run the one-real-frame regression path.

.PARAMETER Frames
    Build and run the bounded real-desktop regression path for this many
    frames. The normal regression value is 300.

.PARAMETER Input
    Run the continuous desktop with a bounded QMP keyboard/mouse workload and
    a diagnostic LoginDialog target for GUI-routing proof.

.PARAMETER InputStress
    Run a shorter continuous desktop input burst with several hundred bounded
    QMP events.

.PARAMETER Png
    Build and run the bounded post-EBS PNG decode/render proof.

.PARAMETER TimeoutSeconds
    Host-side validation limit. The guest has no corresponding timeout.

.PARAMETER GuiVisible
    Show the QEMU graphics window instead of using -display none.

.EXAMPLE
    .\run_uefi_validation.ps1 -Continuous -TimeoutSeconds 300 -GuiVisible

.EXAMPLE
    .\run_uefi_validation.ps1 -Frames 300 -TimeoutSeconds 60
#>

[CmdletBinding()]
param(
    [switch]$Continuous,
    [switch]$Tiny,
    [switch]$FirstFrame,
    [switch]$Png,
    [Alias('Input')]
    [switch]$NativeInput,
    [Alias('InputStress')]
    [switch]$NativeInputStress,
    [int]$Frames = 0,
    [ValidateRange(1, 86400)]
    [int]$TimeoutSeconds = 300,
    [switch]$GuiVisible,
    [string]$SerialLog = ''
)

$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

$selectorCount = @(
    $(if ($Tiny) { 1 } else { 0 }),
    $(if ($FirstFrame) { 1 } else { 0 }),
    $(if ($Png) { 1 } else { 0 }),
    $(if ($NativeInput) { 1 } else { 0 }),
    $(if ($NativeInputStress) { 1 } else { 0 }),
    [int]($Frames -gt 0)
) | Measure-Object -Sum | Select-Object -ExpandProperty Sum

if ($selectorCount -gt 1) {
    throw 'Select only one of -Tiny, -FirstFrame, -Png, -Frames, -Input, or -InputStress.'
}
if ($Frames -lt 0) {
    throw '-Frames cannot be negative.'
}
if ($Frames -gt 0 -and $Frames -ne 300) {
    throw 'The bounded diagnostic validation target is fixed at 300 frames.'
}
if ($Frames -eq 0 -and -not $Tiny -and -not $FirstFrame -and -not $Png -and
    -not $NativeInput -and -not $NativeInputStress -and -not $Continuous) {
    $Continuous = $true
}

$diagnosticMode = ''
if ($Tiny) {
    $diagnosticMode = 'Tiny'
} elseif ($FirstFrame) {
    $diagnosticMode = 'FirstFrame'
} elseif ($Png) {
    $diagnosticMode = 'Png'
} elseif ($Frames -gt 0) {
    $diagnosticMode = 'Frames'
} elseif ($NativeInput) {
    $diagnosticMode = 'Input'
} elseif ($NativeInputStress) {
    $diagnosticMode = 'InputStress'
}
$isBoundedDiagnostic = $diagnosticMode -in @('Tiny', 'FirstFrame', 'Frames', 'Png')
$isInputValidation = $diagnosticMode -in @('Input', 'InputStress')
$isContinuousValidation = -not $isBoundedDiagnostic
$diagnosticCompletionMarker = switch ($diagnosticMode) {
    'Tiny' { 'UTINY_COMPLETE'; break }
    'FirstFrame' { 'NORMAL_FRAME_COMPLETE'; break }
    'Frames' { 'MULTIFRAME_COMPLETE'; break }
    'Png' { 'PNG_PROBE_COMPLETE'; break }
    default { '' }
}

$qemuPath = 'C:\Program Files\qemu\qemu-system-x86_64.exe'
if (-not (Test-Path -LiteralPath $qemuPath)) {
    throw "QEMU not found: $qemuPath"
}

$firmwareRoot = Join-Path $PSScriptRoot 'bin\qemu-firmware'
$firmwareCode = Join-Path $firmwareRoot 'edk2-x86_64-code.fd'
$firmwareVars = Join-Path $firmwareRoot 'edk2-vars.fd'
$firmwareCodeSource = 'C:\Program Files\qemu\share\edk2-x86_64-code.fd'
$firmwareVarsSource = 'C:\Program Files\qemu\share\edk2-i386-vars.fd'

if (-not (Test-Path -LiteralPath $firmwareCodeSource) -or
    -not (Test-Path -LiteralPath $firmwareVarsSource)) {
    throw 'QEMU EDK2 firmware files are missing from C:\Program Files\qemu\share.'
}

Write-Host '========================================' -ForegroundColor Cyan
Write-Host '   guideXOS UEFI Validation' -ForegroundColor Cyan
Write-Host '========================================' -ForegroundColor Cyan
Write-Host "Mode: $(if ($diagnosticMode) { $diagnosticMode } else { 'Continuous default' })" -ForegroundColor Yellow
Write-Host "Timeout: $TimeoutSeconds seconds (host only)" -ForegroundColor Yellow
Write-Host "GUI: $(if ($GuiVisible) { 'visible' } else { 'headless' })" -ForegroundColor Yellow

Write-Host ''
Write-Host 'Refreshing build and ESP...' -ForegroundColor Cyan
$buildArgs = @(
    '-NoProfile',
    '-ExecutionPolicy', 'Bypass',
    '-File', (Join-Path $PSScriptRoot 'build.ps1')
)
if ($diagnosticMode) {
    $buildArgs += @('-UefiDiagnosticMode', $diagnosticMode)
}
& powershell.exe @buildArgs
if ($LASTEXITCODE -ne 0) {
    throw "build.ps1 failed with exit code $LASTEXITCODE"
}

New-Item -ItemType Directory -Path $firmwareRoot -Force | Out-Null
Copy-Item -LiteralPath $firmwareCodeSource -Destination $firmwareCode -Force
Copy-Item -LiteralPath $firmwareVarsSource -Destination $firmwareVars -Force

if (-not (Test-Path -LiteralPath 'ESP\EFI\BOOT\BOOTX64.EFI')) {
    throw 'ESP\EFI\BOOT\BOOTX64.EFI is missing after the build.'
}
if (-not (Test-Path -LiteralPath 'ESP\kernel.elf')) {
    throw 'ESP\kernel.elf is missing after the build.'
}

if ($SerialLog) {
    $serialPath = [System.IO.Path]::GetFullPath($SerialLog)
    if ($serialPath.StartsWith($PSScriptRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        $serialName = $serialPath.Substring($PSScriptRoot.Length).TrimStart('\')
    } else {
        $serialName = $serialPath
    }
} else {
    $serialName = 'serial_uefi_validation_{0}.txt' -f (Get-Date -Format 'yyyyMMdd_HHmmss')
    $serialPath = Join-Path $PSScriptRoot $serialName
}

if (Test-Path -LiteralPath $serialPath) {
    Remove-Item -LiteralPath $serialPath -Force
}

function Read-QmpMessage {
    param([System.IO.StreamReader]$Reader)
    while ($true) {
        $line = $Reader.ReadLine()
        if ($null -eq $line) { return $null }
        if ([string]::IsNullOrWhiteSpace($line)) { continue }
        $message = $line | ConvertFrom-Json
        if ($message.event) { continue }
        return $message
    }
}

function Initialize-Qmp {
    param([int]$Port)
    $client = New-Object System.Net.Sockets.TcpClient
    for ($attempt = 0; $attempt -lt 50; $attempt++) {
        try {
            $client.Connect('127.0.0.1', $Port)
            break
        } catch {
            Start-Sleep -Milliseconds 100
        }
    }
    if (-not $client.Connected) {
        $client.Dispose()
        throw "QMP did not become available on port $Port."
    }

    $stream = $client.GetStream()
    $reader = New-Object System.IO.StreamReader($stream)
    $writer = New-Object System.IO.StreamWriter($stream)
    $writer.NewLine = "`n"
    $writer.AutoFlush = $true
    $null = Read-QmpMessage $reader

    $capabilities = @{ execute = 'qmp_capabilities' } |
        ConvertTo-Json -Compress
    $writer.WriteLine($capabilities)
    $response = Read-QmpMessage $reader
    if ($null -eq $response -or $response.error) {
        $client.Dispose()
        throw 'QMP capability negotiation failed.'
    }
    return @{ Client = $client; Reader = $reader; Writer = $writer }
}

function Send-QmpEvents {
    param($Qmp, [object[]]$Events)
    $request = @{ execute = 'input-send-event'; arguments = @{ events = @($Events) } } |
        ConvertTo-Json -Compress -Depth 12
    $Qmp.Writer.WriteLine($request)
    $response = Read-QmpMessage $Qmp.Reader
    if ($null -eq $response -or $response.error) {
        throw "QMP input-send-event failed: $request"
    }
}

function New-QmpKeyEvent {
    param([string]$KeyCode, [bool]$Down)
    return @{ type = 'key'; data = @{ down = $Down; key = @{ type = 'qcode'; data = $KeyCode } } }
}

function New-QmpButtonEvent {
    param([string]$Button, [bool]$Down)
    return @{ type = 'btn'; data = @{ button = $Button; down = $Down } }
}

function New-QmpRelativeEvent {
    param([string]$Axis, [int]$Value)
    return @{ type = 'rel'; data = @{ axis = $Axis; value = $Value } }
}

function Send-QmpRelative {
    param($Qmp, [int]$DeltaX, [int]$DeltaY)
    $events = @()
    if ($DeltaX -ne 0) { $events += New-QmpRelativeEvent 'x' $DeltaX }
    if ($DeltaY -ne 0) { $events += New-QmpRelativeEvent 'y' $DeltaY }
    if ($events.Count -gt 0) { Send-QmpEvents $Qmp $events }
}

function Send-QmpWorkload {
    param($Qmp, [bool]$Stress)

    # The diagnostic LoginDialog starts with its username field focused. Move
    # from the canonical (0,0) kernel cursor to its username field and click.
    Send-QmpRelative $Qmp 200 200
    Send-QmpRelative $Qmp 220 115
    Send-QmpEvents $Qmp @(
        (New-QmpButtonEvent 'left' $true)
    )
    Start-Sleep -Milliseconds 100
    Send-QmpEvents $Qmp @(
        (New-QmpButtonEvent 'left' $false)
    )
    Send-QmpEvents $Qmp @(
        (New-QmpKeyEvent 'a' $true)
    )
    Send-QmpEvents $Qmp @(
        (New-QmpKeyEvent 'a' $false)
    )
    # Exercise modifier capture and release without leaving Shift latched.
    Send-QmpEvents $Qmp @((New-QmpKeyEvent 'shift' $true))
    Send-QmpEvents $Qmp @(
        (New-QmpKeyEvent 'a' $true),
        (New-QmpKeyEvent 'a' $false)
    )
    Send-QmpEvents $Qmp @((New-QmpKeyEvent 'shift' $false))

    if ($Stress) {
        # Bounded burst: 300 relative reports and 300 key press/release pairs.
        # One command per report avoids QEMU coalescing a large host batch into
        # a much smaller device stream, while the short finite pause lets the
        # guest drain its native queues.
        for ($i = 0; $i -lt 300; $i++) {
            Send-QmpRelative $Qmp 1 1
            Start-Sleep -Milliseconds 2
        }
        for ($i = 0; $i -lt 300; $i++) {
            Send-QmpEvents $Qmp @(
                (New-QmpKeyEvent 'a' $true),
                (New-QmpKeyEvent 'a' $false)
            )
            Start-Sleep -Milliseconds 2
        }
    } else {
        for ($i = 0; $i -lt 100; $i++) { Send-QmpRelative $Qmp 1 1 }
        for ($i = 0; $i -lt 50; $i++) {
            Send-QmpEvents $Qmp @((New-QmpButtonEvent 'left' $true))
            Send-QmpEvents $Qmp @((New-QmpButtonEvent 'left' $false))
        }
        for ($i = 0; $i -lt 100; $i++) {
            Send-QmpEvents $Qmp @((New-QmpKeyEvent 'a' $true))
            Send-QmpEvents $Qmp @((New-QmpKeyEvent 'a' $false))
        }
    }

    # Close the full-screen diagnostic LoginDialog through its existing Cancel
    # control after keyboard proof, then normalize the relative PS/2 cursor to
    # the top-left corner and click the recovered FILES tile.  This preserves
    # the existing keyboard target while proving the desktop hitbox is still
    # routable.
    for ($i = 0; $i -lt 20; $i++) { Send-QmpRelative $Qmp -100 -100 }
    Send-QmpRelative $Qmp 707 498
    Send-QmpEvents $Qmp @((New-QmpButtonEvent 'left' $true))
    Start-Sleep -Milliseconds 250
    Send-QmpEvents $Qmp @((New-QmpButtonEvent 'left' $false))
    for ($i = 0; $i -lt 20; $i++) { Send-QmpRelative $Qmp -100 -100 }
    Send-QmpRelative $Qmp 80 120
    Send-QmpEvents $Qmp @((New-QmpButtonEvent 'left' $true))
    Start-Sleep -Milliseconds 250
    Send-QmpEvents $Qmp @((New-QmpButtonEvent 'left' $false))
}

$qemuArgs = @(
    '-machine', 'pc-q35-8.2',
    '-drive', 'if=pflash,format=raw,readonly=on,file=bin/qemu-firmware/edk2-x86_64-code.fd',
    '-drive', 'if=pflash,format=raw,file=bin/qemu-firmware/edk2-vars.fd',
    '-drive', 'if=none,id=esp,format=raw,file=fat:rw:ESP',
    '-device', 'ide-hd,drive=esp',
    '-m', '1024M',
    '-serial', "file:$serialName",
    '-name', 'guideXOS',
    '-no-reboot',
    '-boot', 'menu=off,splash-time=0',
    '-display', $(if ($GuiVisible) { 'gtk' } else { 'none' })
)

$qmpPort = 0
if ($isInputValidation) {
    $qmpPort = Get-Random -Minimum 43000 -Maximum 43999
    $qemuArgs += @('-qmp', "tcp:127.0.0.1:$qmpPort,server=on,wait=off")
}

Write-Host "Serial log: $serialPath" -ForegroundColor Gray
Write-Host 'Starting QEMU...' -ForegroundColor Green
$qemu = Start-Process -FilePath $qemuPath -ArgumentList $qemuArgs -PassThru
$qmp = $null
if ($isInputValidation) {
    Write-Host "QMP input port: $qmpPort" -ForegroundColor Gray
    $qmp = Initialize-Qmp $qmpPort
}

$startedAt = Get-Date
$deadline = $startedAt.AddSeconds($TimeoutSeconds)
$lastContent = ''
$firstHeartbeatFrame = 0
$lastHeartbeatFrame = 0
$heartbeatCount = 0
$firstHeartbeatTimer = 0
$lastHeartbeatTimer = 0
$dispatchSelected = $false
$continuousEntered = $false
$faultText = $null
$status = 'RUNNING'
$lastProgressReport = $startedAt
$inputInjected = $false
$inputInjectionError = $null

try {
    while ($true) {
        Start-Sleep -Milliseconds 200
        $qemu.Refresh()

        $content = ''
        if (Test-Path -LiteralPath $serialPath) {
            $content = Get-Content -LiteralPath $serialPath -Raw -ErrorAction SilentlyContinue
        }

        if ($content -and $content -ne $lastContent) {
            if ($content -match 'SMAIN_DISPATCH_REASON=UEFI_CONTINUOUS_DESKTOP') {
                $dispatchSelected = $true
            }
            if ($content -match 'CONTINUOUS_DESKTOP_ENTER') {
                $continuousEntered = $true
            }

            if ($isInputValidation -and $continuousEntered -and -not $inputInjected) {
                try {
                    Write-Host '  injecting bounded native keyboard/mouse workload' -ForegroundColor Green
                    Send-QmpWorkload $qmp $NativeInputStress
                    $inputInjected = $true
                } catch {
                    $inputInjectionError = $_.Exception.Message
                    $status = 'INPUT_INJECTION_FAILED'
                    break
                }
            }

            $heartbeatMatches = [regex]::Matches(
                $content,
                'CONTINUOUS_HEARTBEAT_FRAME=(\d+)')
            if ($heartbeatMatches.Count -gt $heartbeatCount) {
                $latest = $heartbeatMatches[$heartbeatMatches.Count - 1]
                $lastHeartbeatFrame = [int]$latest.Groups[1].Value
                if ($firstHeartbeatFrame -eq 0) {
                    $firstHeartbeatFrame = $lastHeartbeatFrame
                }
                $heartbeatCount = $heartbeatMatches.Count
            }

            $timerMatches = [regex]::Matches(
                $content,
                'CONTINUOUS_HEARTBEAT_TIMER=(\d+)')
            if ($timerMatches.Count -gt 0) {
                if ($firstHeartbeatTimer -eq 0) {
                $firstHeartbeatTimer = [UInt64]$timerMatches[0].Groups[1].Value
            }
                $lastHeartbeatTimer = [UInt64]$timerMatches[$timerMatches.Count - 1].Groups[1].Value
            }

            $faultMatches = [regex]::Matches(
                $content,
                '(?im)(CONTINUOUS_DESKTOP_FAULT=[^\r\n]*|PNG_PROBE_FAIL[^\r\n]*|PNG_PROBE_ALPHA_RENDER_OK=0|CPU_FAULT_[A-Z_]+|PANIC:|UEFI_FRAME_FAULT_CONTEXT)')
            if ($faultMatches.Count -gt 0) {
                $faultText = $faultMatches[$faultMatches.Count - 1].Value
                $status = 'FAULT'
                break
            }

            if (-not $isContinuousValidation -and
                $diagnosticCompletionMarker -and
                $content -match $diagnosticCompletionMarker) {
                $status = 'DIAGNOSTIC_COMPLETE'
                break
            }

            $lastContent = $content
        }

        if ($qemu.HasExited) {
            $status = 'QEMU_EXITED'
            break
        }

        if ((Get-Date) -ge $deadline) {
            $heartbeatAdvanced = $heartbeatCount -ge 2 -and
                                  $lastHeartbeatFrame -gt $firstHeartbeatFrame
            if ($qemu.HasExited) {
                $status = 'QEMU_EXITED'
            } elseif ($isContinuousValidation -and $heartbeatAdvanced -and
                      $dispatchSelected -and $continuousEntered) {
                if ($isInputValidation -and -not $inputInjected) {
                    $status = 'TIMEOUT_NO_INPUT'
                } else {
                    $status = 'TIMEOUT_SUCCESS'
                }
            } elseif (-not $isContinuousValidation -and
                      $diagnosticCompletionMarker -and
                      $content -match $diagnosticCompletionMarker) {
                $status = 'DIAGNOSTIC_COMPLETE'
            } else {
                $status = 'TIMEOUT_NO_PROGRESS'
            }
            break
        }

        if (((Get-Date) - $lastProgressReport).TotalSeconds -ge 30) {
            Write-Host "  heartbeat frame=$lastHeartbeatFrame timer=$lastHeartbeatTimer" -ForegroundColor Gray
            $lastProgressReport = Get-Date
        }
    }
} finally {
    $qemu.Refresh()
    if (-not $qemu.HasExited) {
        Stop-Process -Id $qemu.Id -Force -ErrorAction SilentlyContinue
        $null = $qemu.WaitForExit(5000)
    }
    if ($qmp -and $qmp.Client) {
        $qmp.Client.Dispose()
    }
}

$finalContent = ''
if (Test-Path -LiteralPath $serialPath) {
    $finalContent = Get-Content -LiteralPath $serialPath -Raw -ErrorAction SilentlyContinue
}

$stackLowWaterMatches = [regex]::Matches(
    $finalContent,
    'CONTINUOUS_HEARTBEAT_STACK_LOW_WATER=(\d+)')
$graphicsMatches = [regex]::Matches(
    $finalContent,
    'CONTINUOUS_HEARTBEAT_GRAPHICS_VALID=(\d+)')
$lastStackLowWater = if ($stackLowWaterMatches.Count -gt 0) {
    [UInt64]$stackLowWaterMatches[$stackLowWaterMatches.Count - 1].Groups[1].Value
} else { 0 }
$graphicsValid = if (-not $isContinuousValidation) {
    $null
} else {
    $graphicsMatches.Count -gt 0 -and
    ($graphicsMatches | Where-Object { $_.Groups[1].Value -ne '1' }).Count -eq 0
}
$stackTopMatches = [regex]::Matches($finalContent, 'Stack top:\s*0x([0-9A-Fa-f]+)')
$stackTop = if ($stackTopMatches.Count -gt 0) {
    [Convert]::ToUInt64($stackTopMatches[$stackTopMatches.Count - 1].Groups[1].Value, 16)
} else { 0 }

$inputStat = @{}
foreach ($name in @(
    'KEY_IRQ', 'KEY_DROPPED', 'KEY_DOWN', 'KEY_UP', 'MOUSE_IRQ',
    'MOUSE_DROPPED', 'MOUSE_PACKETS', 'MOUSE_MOVES',
    'MOUSE_LEFT_DOWN', 'MOUSE_LEFT_UP')) {
    $matches = [regex]::Matches($finalContent, "INPUT_STATS_${name}=(\d+)")
    $inputStat[$name] = if ($matches.Count -gt 0) {
        [UInt64]$matches[$matches.Count - 1].Groups[1].Value
    } else { 0 }
}

Write-Host ''
Write-Host '========================================' -ForegroundColor Cyan
Write-Host '   Validation Summary' -ForegroundColor Cyan
Write-Host '========================================' -ForegroundColor Cyan
Write-Host "Status: $status" -ForegroundColor $(if ($status -in @('TIMEOUT_SUCCESS', 'DIAGNOSTIC_COMPLETE')) { 'Green' } else { 'Red' })
Write-Host "Dispatch selected: $dispatchSelected" -ForegroundColor Gray
Write-Host "Continuous entered: $continuousEntered" -ForegroundColor Gray
Write-Host "Heartbeats: $heartbeatCount (last frame $lastHeartbeatFrame)" -ForegroundColor Gray
Write-Host "Timer: $firstHeartbeatTimer -> $lastHeartbeatTimer" -ForegroundColor Gray
Write-Host "Stack top: $stackTop" -ForegroundColor Gray
Write-Host "Stack low-water: $lastStackLowWater" -ForegroundColor Gray
Write-Host "Graphics invariants: $(if ($null -eq $graphicsValid) { 'not sampled' } else { $graphicsValid })" -ForegroundColor Gray
if ($isInputValidation) {
    Write-Host "Input injected: $inputInjected" -ForegroundColor Gray
    Write-Host "Keyboard IRQ/down/up/dropped: $($inputStat.KEY_IRQ)/$($inputStat.KEY_DOWN)/$($inputStat.KEY_UP)/$($inputStat.KEY_DROPPED)" -ForegroundColor Gray
    Write-Host "Mouse IRQ/packets/moves/dropped: $($inputStat.MOUSE_IRQ)/$($inputStat.MOUSE_PACKETS)/$($inputStat.MOUSE_MOVES)/$($inputStat.MOUSE_DROPPED)" -ForegroundColor Gray
    Write-Host "Mouse left down/up: $($inputStat.MOUSE_LEFT_DOWN)/$($inputStat.MOUSE_LEFT_UP)" -ForegroundColor Gray
    Write-Host "GUI key routed: $($finalContent -match 'INPUT_GUI_KEY_ROUTED')" -ForegroundColor Gray
    Write-Host "GUI mouse routed: $($finalContent -match 'INPUT_GUI_MOUSE_ROUTED')" -ForegroundColor Gray
    if ($inputInjectionError) {
        Write-Host "Input error: $inputInjectionError" -ForegroundColor Red
    }
}
if ($faultText) {
    Write-Host "Fault: $faultText" -ForegroundColor Red
}
Write-Host "Serial log: $serialPath" -ForegroundColor Cyan

if ($status -in @('FAULT', 'QEMU_EXITED', 'TIMEOUT_NO_PROGRESS', 'TIMEOUT_NO_INPUT', 'INPUT_INJECTION_FAILED')) {
    exit 1
}
exit 0
