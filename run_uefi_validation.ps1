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

.PARAMETER ContextMenu
    Run the normal desktop context-menu interaction workload through QMP.

.PARAMETER Png
    Build and run the bounded post-EBS PNG decode/render proof.

.PARAMETER Font
    Build and run the bounded normal guideXOS bitmap-font initialization and
    canonical DrawString proof.

.PARAMETER Background
    Build and run the bounded normal wallpaper decode/scale/render proof.

.PARAMETER BackgroundRotation
    Build and run five bounded normal background transitions.

.PARAMETER TimeoutSeconds
    Host-side validation limit. The guest has no corresponding timeout.

.PARAMETER GuiVisible
    Show the QEMU graphics window instead of using -display none.

.PARAMETER SkipBuild
    Reuse the existing ESP and only run the selected QEMU validation. Use
    this after a successful build when iterating on the host-side workload.

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
    [switch]$Font,
    [switch]$Background,
    [switch]$BackgroundRotation,
    [Alias('Input')]
    [switch]$NativeInput,
    [Alias('InputStress')]
    [switch]$NativeInputStress,
    [switch]$ContextMenu,
    [int]$Frames = 0,
    [ValidateRange(1, 86400)]
    [int]$TimeoutSeconds = 300,
    [switch]$GuiVisible,
    [switch]$SkipBuild,
    [string]$SerialLog = ''
)

$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

$selectorCount = @(
    $(if ($Tiny) { 1 } else { 0 }),
    $(if ($FirstFrame) { 1 } else { 0 }),
    $(if ($Png) { 1 } else { 0 }),
    $(if ($Font) { 1 } else { 0 }),
    $(if ($Background) { 1 } else { 0 }),
    $(if ($BackgroundRotation) { 1 } else { 0 }),
    $(if ($NativeInput) { 1 } else { 0 }),
    $(if ($NativeInputStress) { 1 } else { 0 }),
    $(if ($ContextMenu) { 1 } else { 0 }),
    [int]($Frames -gt 0)
) | Measure-Object -Sum | Select-Object -ExpandProperty Sum

if ($selectorCount -gt 1) {
    throw 'Select only one of -Tiny, -FirstFrame, -Png, -Font, -Background, -BackgroundRotation, -Frames, -Input, -InputStress, or -ContextMenu.'
}
if ($Frames -lt 0) {
    throw '-Frames cannot be negative.'
}
if ($Frames -gt 0 -and $Frames -ne 300) {
    throw 'The bounded diagnostic validation target is fixed at 300 frames.'
}
if ($Frames -eq 0 -and -not $Tiny -and -not $FirstFrame -and -not $Png -and
    -not $Font -and
    -not $Background -and -not $BackgroundRotation -and
    -not $NativeInput -and -not $NativeInputStress -and -not $ContextMenu -and
    -not $Continuous) {
    $Continuous = $true
}

$diagnosticMode = ''
if ($Tiny) {
    $diagnosticMode = 'Tiny'
} elseif ($FirstFrame) {
    $diagnosticMode = 'FirstFrame'
} elseif ($Png) {
    $diagnosticMode = 'Png'
} elseif ($Font) {
    $diagnosticMode = 'Font'
} elseif ($Background) {
    $diagnosticMode = 'Background'
} elseif ($BackgroundRotation) {
    $diagnosticMode = 'BackgroundRotation'
} elseif ($Frames -gt 0) {
    $diagnosticMode = 'Frames'
} elseif ($NativeInput) {
    $diagnosticMode = 'Input'
} elseif ($NativeInputStress) {
    $diagnosticMode = 'InputStress'
} elseif ($ContextMenu) {
    $diagnosticMode = 'ContextMenu'
}
$isBoundedDiagnostic = $diagnosticMode -in @('Tiny', 'FirstFrame', 'Frames', 'Png', 'Font', 'Background', 'BackgroundRotation')
$isInputValidation = $diagnosticMode -in @('Input', 'InputStress', 'ContextMenu')
$isContinuousValidation = -not $isBoundedDiagnostic
$diagnosticCompletionMarker = switch ($diagnosticMode) {
    'Tiny' { 'UTINY_COMPLETE'; break }
    'FirstFrame' { 'NORMAL_FRAME_COMPLETE'; break }
    'Frames' { 'MULTIFRAME_COMPLETE'; break }
    'Png' { 'PNG_PROBE_COMPLETE'; break }
    'Font' { 'FONT_PROBE_COMPLETE'; break }
    'Background' { 'BACKGROUND_PROBE_COMPLETE'; break }
    'BackgroundRotation' { 'BACKGROUND_ROTATION_COMPLETE'; break }
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
if ($SkipBuild) {
    Write-Host 'Reusing existing ESP (build skipped)...' -ForegroundColor Yellow
} else {
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

function Reset-QmpPointer {
    param($Qmp)
    # The native relative PS/2 cursor starts at a bounded screen coordinate.
    # Repeated negative reports deterministically return it to (0,0).
    for ($i = 0; $i -lt 12; $i++) {
        Send-QmpRelative $Qmp -100 -100
        # Leave a small drain interval between reports; the guest deliberately
        # keeps PS/2 parsing on the render thread instead of the IRQ handler.
        Start-Sleep -Milliseconds 3
    }
    Start-Sleep -Milliseconds 30
}

function Set-QmpPointer {
    param($Qmp, [int]$X, [int]$Y)
    Reset-QmpPointer $Qmp
    # Keep each relative report within the native PS/2 range. QEMU accepts
    # larger QMP values, but can coalesce or drop a single oversized move
    # before the guest's bounded render-thread parser sees it.
    $remainingX = $X
    while ($remainingX -gt 0) {
        $stepX = [Math]::Min(100, $remainingX)
        Send-QmpRelative $Qmp $stepX 0
        $remainingX -= $stepX
        Start-Sleep -Milliseconds 2
    }
    $remainingY = $Y
    while ($remainingY -gt 0) {
        $stepY = [Math]::Min(100, $remainingY)
        Send-QmpRelative $Qmp 0 $stepY
        $remainingY -= $stepY
        Start-Sleep -Milliseconds 2
    }
    # Allow the guest to drain the final positioning reports before the
    # button-down transition is sent.
    Start-Sleep -Milliseconds 120
}

function Send-QmpMouseClick {
    param($Qmp, [string]$Button)
    Send-QmpEvents $Qmp @((New-QmpButtonEvent $Button $true))
    # Hold long enough for at least several 16ms desktop frames. This keeps
    # down/up from collapsing into one ProcessPendingInput drain.
    Start-Sleep -Milliseconds 90
    Send-QmpEvents $Qmp @((New-QmpButtonEvent $Button $false))
    Start-Sleep -Milliseconds 90
}

function Open-QmpContextMenu {
    param($Qmp, [int]$X = 400, [int]$Y = 220)
    Set-QmpPointer $Qmp $X $Y
    Send-QmpMouseClick $Qmp 'right'
    # Let at least one guest frame draw the popup before moving the pointer.
    Start-Sleep -Milliseconds 55
}

function Get-ContextMarkerCount {
    param([string]$Pattern)
    if (-not (Test-Path -LiteralPath $serialPath)) { return 0 }
    $content = Get-Content -LiteralPath $serialPath -Raw -ErrorAction SilentlyContinue
    if (-not $content) { return 0 }
    return [regex]::Matches($content, $Pattern).Count
}

function Wait-ForContextMarkerCount {
    param(
        [string]$Pattern,
        [int]$Minimum,
        [int]$TimeoutMilliseconds = 4000
    )
    $deadline = (Get-Date).AddMilliseconds($TimeoutMilliseconds)
    do {
        if ((Get-ContextMarkerCount $Pattern) -ge $Minimum) { return }
        Start-Sleep -Milliseconds 20
    } while ((Get-Date) -lt $deadline)
    throw "Guest did not emit marker '$Pattern' count $Minimum."
}

function Send-QmpContextMenuWorkload {
    param($Qmp)

    # Four corner opens prove the normal menu clamping logic. Escape is the
    # existing Window global-key dismissal path.
    # Keep the desktop edge probes just above the recovered 40px taskbar so
    # the bottom probes exercise RightMenu clamping, not TaskbarMenu routing.
    foreach ($edge in @(
        @{ X = 0; Y = 0 }, @{ X = 1279; Y = 0 },
        @{ X = 0; Y = 720 }, @{ X = 1279; Y = 720 })) {
        $openedBefore = Get-ContextMarkerCount '(?m)^CONTEXT_MENU_OPENED='
        Open-QmpContextMenu $Qmp $edge.X $edge.Y
        Wait-ForContextMarkerCount '(?m)^CONTEXT_MENU_OPENED=' ($openedBefore + 1)
        $dismissedBefore = Get-ContextMarkerCount '(?m)^CONTEXT_MENU_DISMISSED=ESCAPE'
        Send-QmpEvents $Qmp @((New-QmpKeyEvent 'esc' $true))
        Send-QmpEvents $Qmp @((New-QmpKeyEvent 'esc' $false))
        Wait-ForContextMarkerCount '(?m)^CONTEXT_MENU_DISMISSED=ESCAPE' ($dismissedBefore + 1)
    }

    # Exercise the recovered taskbar popup once, including its keyboard
    # dismissal path, without invoking the heavyweight Task Manager command.
    $taskbarOpenedBefore = Get-ContextMarkerCount '(?m)^TASKBAR_CONTEXT_MENU_OPENED='
    Open-QmpContextMenu $Qmp 400 790
    Wait-ForContextMarkerCount '(?m)^TASKBAR_CONTEXT_MENU_OPENED=' ($taskbarOpenedBefore + 1)
    $taskbarDismissedBefore = Get-ContextMarkerCount '(?m)^TASKBAR_CONTEXT_MENU_DISMISSED=ESCAPE'
    Send-QmpEvents $Qmp @((New-QmpKeyEvent 'esc' $true))
    Send-QmpEvents $Qmp @((New-QmpKeyEvent 'esc' $false))
    Wait-ForContextMarkerCount '(?m)^TASKBAR_CONTEXT_MENU_DISMISSED=ESCAPE' ($taskbarDismissedBefore + 1)

    # 25 safe activations through the existing Icon Size submenu. This menu
    # command only updates the desktop's existing icon-size state.
    for ($i = 0; $i -lt 25; $i++) {
        $openedBefore = Get-ContextMarkerCount '(?m)^CONTEXT_MENU_OPENED='
        Open-QmpContextMenu $Qmp 400 220
        Wait-ForContextMarkerCount '(?m)^CONTEXT_MENU_OPENED=' ($openedBefore + 1)
        # Hover Display Options, Performance Widget, and Icon Size in turn.
        Set-QmpPointer $Qmp 410 220
        Start-Sleep -Milliseconds 35
        Set-QmpPointer $Qmp 410 248
        Start-Sleep -Milliseconds 35
        Set-QmpPointer $Qmp 410 276
        Start-Sleep -Milliseconds 50
        # The submenu is to the right of the 220px menu. Select 32px.
        Send-QmpRelative $Qmp 212 62
        Start-Sleep -Milliseconds 80
        $activatedBefore = Get-ContextMarkerCount '(?m)^CONTEXT_MENU_ACTIVATED=ICON_SIZE_32'
        Send-QmpMouseClick $Qmp 'left'
        Wait-ForContextMarkerCount '(?m)^CONTEXT_MENU_ACTIVATED=ICON_SIZE_32' ($activatedBefore + 1)
    }

    # 50 click-away dismissals. The outside click is consumed by the popup and
    # is not forwarded to the desktop underneath it.
    for ($i = 0; $i -lt 50; $i++) {
        $openedBefore = Get-ContextMarkerCount '(?m)^CONTEXT_MENU_OPENED='
        Open-QmpContextMenu $Qmp 400 220
        Wait-ForContextMarkerCount '(?m)^CONTEXT_MENU_OPENED=' ($openedBefore + 1)
        Set-QmpPointer $Qmp 700 470
        Start-Sleep -Milliseconds 40
        $dismissedBefore = Get-ContextMarkerCount '(?m)^CONTEXT_MENU_DISMISSED=CLICK_AWAY'
        Send-QmpMouseClick $Qmp 'left'
        Wait-ForContextMarkerCount '(?m)^CONTEXT_MENU_DISMISSED=CLICK_AWAY' ($dismissedBefore + 1)
    }

    # 25 Escape dismissals through the existing keyboard pipeline.
    for ($i = 0; $i -lt 25; $i++) {
        $openedBefore = Get-ContextMarkerCount '(?m)^CONTEXT_MENU_OPENED='
        Open-QmpContextMenu $Qmp 400 220
        Wait-ForContextMarkerCount '(?m)^CONTEXT_MENU_OPENED=' ($openedBefore + 1)
        $dismissedBefore = Get-ContextMarkerCount '(?m)^CONTEXT_MENU_DISMISSED=ESCAPE'
        Send-QmpEvents $Qmp @((New-QmpKeyEvent 'esc' $true))
        Send-QmpEvents $Qmp @((New-QmpKeyEvent 'esc' $false))
        Wait-ForContextMarkerCount '(?m)^CONTEXT_MENU_DISMISSED=ESCAPE' ($dismissedBefore + 1)
    }

    # Leave the guest with a normal desktop pointer and no pressed buttons.
    Set-QmpPointer $Qmp 80 120
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

            if ($isInputValidation -and $continuousEntered -and
                $content -match 'CONTINUOUS_HEARTBEAT_FRAME=' -and
                -not $inputInjected) {
                try {
                    Write-Host '  injecting bounded native keyboard/mouse workload' -ForegroundColor Green
                    if ($diagnosticMode -eq 'ContextMenu') {
                        Send-QmpContextMenuWorkload $qmp
                    } else {
                        Send-QmpWorkload $qmp $NativeInputStress
                    }
                    $inputInjected = $true
                    if ($diagnosticMode -eq 'ContextMenu') {
                        $status = 'CONTEXT_MENU_COMPLETE'
                        break
                    }
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
                '(?im)(CONTINUOUS_DESKTOP_FAULT=[^\r\n]*|PNG_PROBE_FAIL[^\r\n]*|PNG_PROBE_ALPHA_RENDER_OK=0|BACKGROUND_PROBE_FAIL[^\r\n]*|BACKGROUND_ROTATION_FAIL[^\r\n]*|BACKGROUND_PROBE_RENDER_OK=0|BACKGROUND_ROTATION_RENDER_OK=0|FONT_PROBE_FAIL[^\r\n]*|FONT_PROBE_INIT_OK=0|FONT_PROBE_MEASURE_OK=0|FONT_RENDER_OK=0|CONTEXT_MENU_BOUNDS=[^\r\n]*,ok=0|CONTEXT_MENU_DRAWN=[^\r\n]*,font=0|TASKBAR_CONTEXT_MENU_BOUNDS=[^\r\n]*,ok=0|TASKBAR_CONTEXT_MENU_DRAWN=[^\r\n]*,font=0|CPU_FAULT_[A-Z_]+|#UD|#GP|#PF|GENERAL_PROTECTION|PAGE_FAULT|PANIC:|UEFI_FRAME_FAULT_CONTEXT)')
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
    'MOUSE_LEFT_DOWN', 'MOUSE_LEFT_UP', 'MOUSE_RIGHT_DOWN', 'MOUSE_RIGHT_UP')) {
    $matches = [regex]::Matches($finalContent, "INPUT_STATS_${name}=(\d+)")
    $inputStat[$name] = if ($matches.Count -gt 0) {
        [UInt64]$matches[$matches.Count - 1].Groups[1].Value
    } else { 0 }
}

$contextValidation = $null
if ($diagnosticMode -eq 'ContextMenu') {
    $contextMenuOpened = [regex]::Matches($finalContent, '(?m)^CONTEXT_MENU_OPENED=').Count
    $contextMenuDrawn = [regex]::Matches($finalContent, '(?m)^CONTEXT_MENU_DRAWN=[^\r\n]*,font=1').Count
    $contextMenuGoodBounds = [regex]::Matches($finalContent, '(?m)^CONTEXT_MENU_BOUNDS=[^\r\n]*,ok=1').Count
    $contextMenuBadBounds = [regex]::Matches($finalContent, '(?m)^CONTEXT_MENU_BOUNDS=[^\r\n]*,ok=0').Count
    $contextMenuHover0 = [regex]::Matches($finalContent, '(?m)^CONTEXT_MENU_HOVER_INDEX=0').Count
    $contextMenuHover1 = [regex]::Matches($finalContent, '(?m)^CONTEXT_MENU_HOVER_INDEX=1').Count
    $contextMenuHover2 = [regex]::Matches($finalContent, '(?m)^CONTEXT_MENU_HOVER_INDEX=2').Count
    $contextMenuHoverSubmenu = [regex]::Matches($finalContent, '(?m)^CONTEXT_MENU_HOVER_INDEX=102').Count
    $contextMenuHoverReset = [regex]::Matches($finalContent, '(?m)^CONTEXT_MENU_HOVER_INDEX=-1').Count
    $contextMenuCommands = [regex]::Matches($finalContent, '(?m)^CONTEXT_MENU_ACTIVATED=ICON_SIZE_32').Count
    $contextMenuClickAway = [regex]::Matches($finalContent, '(?m)^CONTEXT_MENU_DISMISSED=CLICK_AWAY').Count
    $contextMenuEscape = [regex]::Matches($finalContent, '(?m)^CONTEXT_MENU_DISMISSED=ESCAPE').Count
    $taskbarMenuOpened = [regex]::Matches($finalContent, '(?m)^TASKBAR_CONTEXT_MENU_OPENED=').Count
    $taskbarMenuDrawn = [regex]::Matches($finalContent, '(?m)^TASKBAR_CONTEXT_MENU_DRAWN=[^\r\n]*,font=1').Count
    $taskbarMenuGoodBounds = [regex]::Matches($finalContent, '(?m)^TASKBAR_CONTEXT_MENU_BOUNDS=[^\r\n]*,ok=1').Count
    $taskbarMenuEscape = [regex]::Matches($finalContent, '(?m)^TASKBAR_CONTEXT_MENU_DISMISSED=ESCAPE').Count
    $contextMenuInputMatches = [regex]::Matches($finalContent, 'CONTEXT_MENU_INPUT_STATS=(\d+),(\d+)')
    $contextMenuLastRightDown = if ($contextMenuInputMatches.Count -gt 0) {
        [UInt64]$contextMenuInputMatches[$contextMenuInputMatches.Count - 1].Groups[1].Value
    } else { 0 }
    $contextMenuLastRightUp = if ($contextMenuInputMatches.Count -gt 0) {
        [UInt64]$contextMenuInputMatches[$contextMenuInputMatches.Count - 1].Groups[2].Value
    } else { 0 }
    $taskbarInputMatches = [regex]::Matches($finalContent, 'TASKBAR_CONTEXT_MENU_INPUT_STATS=(\d+),(\d+)')
    $taskbarLastRightDown = if ($taskbarInputMatches.Count -gt 0) {
        [UInt64]$taskbarInputMatches[$taskbarInputMatches.Count - 1].Groups[1].Value
    } else { 0 }
    $taskbarLastRightUp = if ($taskbarInputMatches.Count -gt 0) {
        [UInt64]$taskbarInputMatches[$taskbarInputMatches.Count - 1].Groups[2].Value
    } else { 0 }
    $lastRightDown = if ($contextMenuLastRightDown -gt $taskbarLastRightDown) {
        $contextMenuLastRightDown
    } else { $taskbarLastRightDown }
    $lastRightUp = if ($contextMenuLastRightUp -gt $taskbarLastRightUp) {
        $contextMenuLastRightUp
    } else { $taskbarLastRightUp }
    $contextMenuExpectedOpens = 104
    $contextMenuPass =
        $status -eq 'CONTEXT_MENU_COMPLETE' -and
        $contextMenuOpened -ge $contextMenuExpectedOpens -and
        $contextMenuDrawn -ge $contextMenuExpectedOpens -and
        $contextMenuGoodBounds -ge $contextMenuExpectedOpens -and
        $contextMenuBadBounds -eq 0 -and
        $contextMenuHover0 -gt 0 -and $contextMenuHover1 -gt 0 -and
        $contextMenuHover2 -gt 0 -and $contextMenuHoverSubmenu -gt 0 -and
        $contextMenuHoverReset -gt 0 -and $contextMenuCommands -ge 25 -and
        $contextMenuClickAway -ge 50 -and $contextMenuEscape -ge 25 -and
        $taskbarMenuOpened -ge 1 -and $taskbarMenuDrawn -ge 1 -and
        $taskbarMenuGoodBounds -ge 1 -and $taskbarMenuEscape -ge 1 -and
        $lastRightDown -ge ($contextMenuOpened + $taskbarMenuOpened) -and
        $lastRightDown -eq $lastRightUp
    $contextValidation = [ordered]@{
        pass = $contextMenuPass
        desktopOpened = $contextMenuOpened
        desktopDrawn = $contextMenuDrawn
        desktopGoodBounds = $contextMenuGoodBounds
        desktopBadBounds = $contextMenuBadBounds
        hover = "$contextMenuHover0/$contextMenuHover1/$contextMenuHover2/$contextMenuHoverSubmenu/$contextMenuHoverReset"
        iconSize32 = $contextMenuCommands
        clickAway = $contextMenuClickAway
        escape = $contextMenuEscape
        taskbarOpened = $taskbarMenuOpened
        taskbarDrawn = $taskbarMenuDrawn
        taskbarGoodBounds = $taskbarMenuGoodBounds
        taskbarEscape = $taskbarMenuEscape
        rightDownUp = "$lastRightDown/$lastRightUp"
    }
    if ($status -eq 'CONTEXT_MENU_COMPLETE' -and -not $contextMenuPass) {
        $status = 'CONTEXT_MENU_VALIDATION_FAILED'
    }
}

Write-Host ''
Write-Host '========================================' -ForegroundColor Cyan
Write-Host '   Validation Summary' -ForegroundColor Cyan
Write-Host '========================================' -ForegroundColor Cyan
Write-Host "Status: $status" -ForegroundColor $(if ($status -in @('TIMEOUT_SUCCESS', 'DIAGNOSTIC_COMPLETE', 'CONTEXT_MENU_COMPLETE')) { 'Green' } else { 'Red' })
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
    Write-Host "Mouse right down/up: $($inputStat.MOUSE_RIGHT_DOWN)/$($inputStat.MOUSE_RIGHT_UP)" -ForegroundColor Gray
    Write-Host "GUI key routed: $($finalContent -match 'INPUT_GUI_KEY_ROUTED')" -ForegroundColor Gray
    Write-Host "GUI mouse routed: $($finalContent -match 'INPUT_GUI_MOUSE_ROUTED')" -ForegroundColor Gray
    if ($inputInjectionError) {
        Write-Host "Input error: $inputInjectionError" -ForegroundColor Red
    }
}
if ($contextValidation) {
    Write-Host "Context menu validation: $($contextValidation.pass)" -ForegroundColor $(if ($contextValidation.pass) { 'Green' } else { 'Red' })
    Write-Host "Desktop opens/draws/good-bounds/bad-bounds: $($contextValidation.desktopOpened)/$($contextValidation.desktopDrawn)/$($contextValidation.desktopGoodBounds)/$($contextValidation.desktopBadBounds)" -ForegroundColor Gray
    Write-Host "Hover indices (0/1/2/submenu/reset): $($contextValidation.hover)" -ForegroundColor Gray
    Write-Host "Icon Size 32 activations/click-away/Escape: $($contextValidation.iconSize32)/$($contextValidation.clickAway)/$($contextValidation.escape)" -ForegroundColor Gray
    Write-Host "Taskbar opens/draws/good-bounds/Escape: $($contextValidation.taskbarOpened)/$($contextValidation.taskbarDrawn)/$($contextValidation.taskbarGoodBounds)/$($contextValidation.taskbarEscape)" -ForegroundColor Gray
    Write-Host "Right-button down/up: $($contextValidation.rightDownUp)" -ForegroundColor Gray
}
if ($faultText) {
    Write-Host "Fault: $faultText" -ForegroundColor Red
}
Write-Host "Serial log: $serialPath" -ForegroundColor Cyan

if ($status -in @('FAULT', 'QEMU_EXITED', 'TIMEOUT_NO_PROGRESS', 'TIMEOUT_NO_INPUT', 'INPUT_INJECTION_FAILED', 'CONTEXT_MENU_VALIDATION_FAILED')) {
    exit 1
}
exit 0
