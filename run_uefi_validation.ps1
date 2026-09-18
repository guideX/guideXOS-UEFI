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

.PARAMETER ContextMenuSoak
    Run the normal context-menu workload, then keep the UEFI desktop running
    while periodically opening and dismissing the popup for five minutes.

.PARAMETER Widget
    Initialize and interact with the normal UEFI widget subsystem.

.PARAMETER WidgetStress
    Run a bounded widget hover, menu, activation, and dismissal workload.

.PARAMETER WidgetSoak
    Run the widget workload and keep the widget-enabled desktop alive for at
    least ten minutes.

.PARAMETER Png
    Build and run the bounded post-EBS PNG decode/render proof.

.PARAMETER Font
    Build and run the bounded normal guideXOS bitmap-font initialization and
    canonical DrawString proof.

.PARAMETER Background
    Build and run the bounded normal wallpaper decode/scale/render proof.

.PARAMETER BackgroundRotation
    Build and run five bounded normal background transitions.

.PARAMETER AppModel
    Build and run the bounded app-model, file-association, and shell-object
    resolver self-tests.

.PARAMETER AppRuntime
    Run real UEFI Start-menu application launches, close/return cycles, shell
    routes, and file-association opens through QMP input.

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
    [switch]$AppModel,
    [switch]$AppRuntime,
    [Alias('Input')]
    [switch]$NativeInput,
    [Alias('InputStress')]
    [switch]$NativeInputStress,
    [switch]$ContextMenu,
    [switch]$ContextMenuSoak,
    [switch]$Widget,
    [switch]$WidgetStress,
    [switch]$WidgetSoak,
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
    $(if ($AppModel) { 1 } else { 0 }),
    $(if ($AppRuntime) { 1 } else { 0 }),
    $(if ($NativeInput) { 1 } else { 0 }),
    $(if ($NativeInputStress) { 1 } else { 0 }),
    $(if ($ContextMenu) { 1 } else { 0 }),
    $(if ($ContextMenuSoak) { 1 } else { 0 }),
    $(if ($Widget) { 1 } else { 0 }),
    $(if ($WidgetStress) { 1 } else { 0 }),
    $(if ($WidgetSoak) { 1 } else { 0 }),
    [int]($Frames -gt 0)
) | Measure-Object -Sum | Select-Object -ExpandProperty Sum

if ($selectorCount -gt 1) {
    throw 'Select only one validation selector.'
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
    -not $AppModel -and -not $AppRuntime -and
    -not $NativeInput -and -not $NativeInputStress -and -not $ContextMenu -and
    -not $ContextMenuSoak -and -not $Widget -and -not $WidgetStress -and
    -not $WidgetSoak -and
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
} elseif ($AppModel) {
    $diagnosticMode = 'AppModel'
} elseif ($AppRuntime) {
    $diagnosticMode = 'AppRuntime'
} elseif ($Frames -gt 0) {
    $diagnosticMode = 'Frames'
} elseif ($NativeInput) {
    $diagnosticMode = 'Input'
} elseif ($NativeInputStress) {
    $diagnosticMode = 'InputStress'
} elseif ($ContextMenu -or $ContextMenuSoak) {
    $diagnosticMode = 'ContextMenu'
} elseif ($Widget) {
    $diagnosticMode = 'Widget'
} elseif ($WidgetStress) {
    $diagnosticMode = 'WidgetStress'
} elseif ($WidgetSoak) {
    $diagnosticMode = 'WidgetSoak'
}
$isWidgetValidation = $diagnosticMode -in @('Widget', 'WidgetStress', 'WidgetSoak')
$isAppModelValidation = $diagnosticMode -eq 'AppModel'
$isBoundedDiagnostic = $diagnosticMode -in @('Tiny', 'FirstFrame', 'Frames', 'Png', 'Font', 'Background', 'BackgroundRotation', 'AppModel', 'Widget', 'WidgetStress')
$isInputValidation = $diagnosticMode -in @('Input', 'InputStress', 'ContextMenu')
$isStartMenuValidation = $diagnosticMode -in @('Input', 'InputStress')
$isAppRuntimeValidation = $diagnosticMode -eq 'AppRuntime'
$isInteractiveValidation = $isInputValidation -or $isWidgetValidation -or $isAppRuntimeValidation
$isContinuousValidation = -not $isBoundedDiagnostic
$diagnosticCompletionMarker = switch ($diagnosticMode) {
    'Tiny' { 'UTINY_COMPLETE'; break }
    'FirstFrame' { 'NORMAL_FRAME_COMPLETE'; break }
    'Frames' { 'MULTIFRAME_COMPLETE'; break }
    'Png' { 'PNG_PROBE_COMPLETE'; break }
    'Font' { 'FONT_PROBE_COMPLETE'; break }
    'Background' { 'BACKGROUND_PROBE_COMPLETE'; break }
    'BackgroundRotation' { 'BACKGROUND_ROTATION_COMPLETE'; break }
    'AppModel' { 'APP_MODEL_COMPLETE'; break }
    'AppRuntime' { 'APP_RUNTIME_COMPLETE'; break }
    'Widget' { 'WIDGET_COMPLETE'; break }
    'WidgetStress' { 'WIDGET_STRESS_COMPLETE'; break }
    'WidgetSoak' { 'WIDGET_SOAK_COMPLETE'; break }
    default { '' }
}

if ($WidgetSoak -and $TimeoutSeconds -lt 720) {
    $TimeoutSeconds = 720
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

function Send-QmpMonitorKey {
    param($Qmp, [string]$KeyCode)
    $request = @{ execute = 'human-monitor-command'; arguments = @{ 'command-line' = "sendkey $KeyCode" } } |
        ConvertTo-Json -Compress
    $Qmp.Writer.WriteLine($request)
    $response = Read-QmpMessage $Qmp.Reader
    if ($null -eq $response -or $response.error) {
        throw "QMP sendkey failed: $request"
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

    # The UEFI start tile uses the existing mature StartMenu once the app
    # model is initialized. Open it once, then dismiss it through the normal
    # keyboard path so this route is covered without selecting an app.
    # The QEMU UEFI framebuffer is 800px high in this workload, so the
    # 40px taskbar occupies y=760..799.
    Set-QmpPointer $Qmp 30 780
    Send-QmpMouseClick $Qmp 'left'
    # First-show blur/cache construction is intentionally one-time and can be
    # slower than the generic popup marker budget on this NativeAOT guest. The
    # serial file is buffered by QEMU, so validate START_MENU_OPENED from the
    # flushed final log instead of waiting on a live marker.
    Start-Sleep -Milliseconds 1000
    Send-QmpMonitorKey $Qmp 'esc'
    Start-Sleep -Milliseconds 120
}

function Open-QmpStartApplication {
    param(
        $Qmp,
        [string]$Name,
        [int]$Index
    )

    # Let the previous close cleanup finish its render pass before routing a
    # new taskbar click.  The guest is deliberately single-threaded at this
    # boundary, so a short drain avoids racing WindowManager input ownership.
    Start-Sleep -Milliseconds 250
    $startBefore = Get-ContextMarkerCount '(?m)^START_MENU_OPENED$'
    $startOpened = $false
    for ($attempt = 0; $attempt -lt 4 -and -not $startOpened; $attempt++) {
        Set-QmpPointer $Qmp 30 780
        Send-QmpMouseClick $Qmp 'left'
        try {
            Wait-ForContextMarkerCount '(?m)^START_MENU_OPENED$' ($startBefore + 1) 5000
            $startOpened = $true
        } catch {
            if ($attempt -eq 3) { throw }
            Start-Sleep -Milliseconds 700
        }
    }
    # First-show blur construction and the normal fade-in both run on the
    # render thread; allow them to settle before the first menu click.
    Start-Sleep -Milliseconds 1200

    # StartMenu geometry is fixed by the existing UEFI layout: the All
    # Programs button is x=33..173, y=679..707, and rows begin at y=63 with
    # 58px spacing.  The final row is reached through the existing down arrow.
    # StartMenu.OnSetVisible resets the all-programs view every time the
    # popup is hidden.  Historical serial markers therefore cannot be used to
    # infer the state of this fresh activation; always toggle it explicitly.
    $allBefore = Get-ContextMarkerCount '(?m)^APP_RUNTIME_START_ALL_PROGRAMS=visible=1$'
    $allOpened = $false
    for ($attempt = 0; $attempt -lt 4 -and -not $allOpened; $attempt++) {
        Set-QmpPointer $Qmp 80 694
        Send-QmpMouseClick $Qmp 'left'
        try {
            Wait-ForContextMarkerCount '(?m)^APP_RUNTIME_START_ALL_PROGRAMS=visible=1$' ($allBefore + 1) 5000
            $allOpened = $true
        } catch {
            if ($attempt -eq 3) { throw }
            # If the menu consumed the taskbar transition but missed the
            # All Programs edge, reset the popup through its normal Escape
            # path and reopen it before retrying.  This keeps the retry
            # bounded while avoiding a stale _leftDownPrev state in the
            # existing StartMenu input latch.
            Send-QmpMonitorKey $Qmp 'esc'
            Start-Sleep -Milliseconds 800
            $startBefore = Get-ContextMarkerCount '(?m)^START_MENU_OPENED$'
            Set-QmpPointer $Qmp 30 780
            Send-QmpMouseClick $Qmp 'left'
            Wait-ForContextMarkerCount '(?m)^START_MENU_OPENED$' ($startBefore + 1) 5000
            Start-Sleep -Milliseconds 500
        }
    }
    # The all-programs transition rebuilds the list and its blur-backed panel
    # on the render thread. Give that one-time transition a full frame budget
    # before deriving and clicking the requested row.
    # All-programs rendering can rebuild twelve icon entries and the blur
    # backed panel on a slow guest frame.  Give that work enough wall-clock
    # time before routing the first row click.
    Start-Sleep -Milliseconds 1800
    $rowY = 63 + ($Index * 58) + 16
    if ($Index -ge 10) {
        # The last two registered apps are below the visible list viewport.
        # Read the live layout bound emitted by the diagnostic guest, page to
        # the bounded maximum through the existing arrow, then derive the row
        # from the same 58px item pitch.
        $layout = Get-Content -LiteralPath $serialPath -Raw -ErrorAction SilentlyContinue
        $layoutMatches = [regex]::Matches($layout,
            '(?m)^APP_RUNTIME_START_ALL_LAYOUT=apps=\d+;windows=\d+;listH=\d+;maxscroll=(\d+)$')
        if ($layoutMatches.Count -eq 0) {
            throw 'Start all-programs layout marker is missing.'
        }
        $maxScroll = [int]$layoutMatches[$layoutMatches.Count - 1].Groups[1].Value
        for ($page = 0; $page -lt 4; $page++) {
            Set-QmpPointer $Qmp 241 645
            Send-QmpMouseClick $Qmp 'left'
            if ($maxScroll -eq 0) { break }
        }
        # The arrow path rebuilds the list/cache on the render thread.  Drain
        # that redraw before routing the final visible row click.
        Start-Sleep -Milliseconds 900
        $rowY -= $maxScroll
    }

    $selectBefore = Get-ContextMarkerCount (
        '(?m)^APP_RUNTIME_START_SELECT=name=' + [regex]::Escape($Name) + ';')
    $launchBefore = Get-ContextMarkerCount (
        '(?m)^APP_RUNTIME_LAUNCH_OK=.*;name=' + [regex]::Escape($Name) + ';')
    $selectPattern = '(?m)^APP_RUNTIME_START_SELECT=name=' + [regex]::Escape($Name) + ';'
    $selected = $false
    for ($attempt = 0; $attempt -lt 3 -and -not $selected; $attempt++) {
        Set-QmpPointer $Qmp 80 $rowY
        Send-QmpMouseClick $Qmp 'left'
        try {
            Wait-ForContextMarkerCount $selectPattern ($selectBefore + 1) 5000
            $selected = $true
        } catch {
            if ($attempt -eq 2) { throw }
            # A busy menu redraw can leave the first row click without an
            # edge even though the popup is visible. Reset the popup through
            # Escape and rebuild the all-programs view before retrying; this
            # also clears the existing StartMenu button latch.
            Send-QmpMonitorKey $Qmp 'esc'
            Start-Sleep -Milliseconds 700
            $startBefore = Get-ContextMarkerCount '(?m)^START_MENU_OPENED$'
            Set-QmpPointer $Qmp 30 780
            Send-QmpMouseClick $Qmp 'left'
            Wait-ForContextMarkerCount '(?m)^START_MENU_OPENED$' ($startBefore + 1) 5000
            Start-Sleep -Milliseconds 500
            $allBefore = Get-ContextMarkerCount '(?m)^APP_RUNTIME_START_ALL_PROGRAMS=visible=1$'
            Set-QmpPointer $Qmp 80 694
            Send-QmpMouseClick $Qmp 'left'
            Wait-ForContextMarkerCount '(?m)^APP_RUNTIME_START_ALL_PROGRAMS=visible=1$' ($allBefore + 1) 5000
            Start-Sleep -Milliseconds 700
            if ($Index -ge 10) {
                for ($page = 0; $page -lt 4; $page++) {
                    Set-QmpPointer $Qmp 241 645
                    Send-QmpMouseClick $Qmp 'left'
                    if ($maxScroll -eq 0) { break }
                }
                Start-Sleep -Milliseconds 900
            }
        }
    }
    Wait-ForContextMarkerCount (
        '(?m)^APP_RUNTIME_LAUNCH_OK=.*;name=' + [regex]::Escape($Name) + ';') ($launchBefore + 1) 8000

    # Escape is the normal global-key close route.  The cleanup breadcrumb is
    # the point at which the window is removed and its owner memory reclaimed.
    # StartMenu hides immediately after dispatch, but its cleanup is performed
    # by the next render pass.  Let that pass remove the popup before Esc is
    # sent to the launched window.
    Start-Sleep -Milliseconds 350
    Send-QmpMonitorKey $Qmp 'a'
    Start-Sleep -Milliseconds 120
    $closedBefore = Get-ContextMarkerCount '(?m)^APP_RUNTIME_WINDOW_CLOSED='
    Close-QmpLastLaunchedWindow $Qmp
    Wait-ForContextMarkerCount '(?m)^APP_RUNTIME_WINDOW_CLOSED=' ($closedBefore + 1) 6000
    Start-Sleep -Milliseconds 160
}

function Close-QmpLastLaunchedWindow {
    param($Qmp)
    if (-not (Test-Path -LiteralPath $serialPath)) {
        throw 'No runtime serial log is available for launch bounds.'
    }
    $content = Get-Content -LiteralPath $serialPath -Raw -ErrorAction SilentlyContinue
    $matches = [regex]::Matches($content,
        '(?m)^APP_RUNTIME_LAUNCH_OK=.*;bounds=(\d+),(\d+),(\d+),(\d+);')
    if ($matches.Count -eq 0) {
        throw 'Last runtime launch did not expose window bounds.'
    }
    $match = $matches[$matches.Count - 1]
    $x = [int]$match.Groups[1].Value
    $y = [int]$match.Groups[2].Value
    $w = [int]$match.Groups[3].Value
    if ($w -lt 40) { throw 'Last runtime launch exposed invalid window width.' }
    # Window.ComputeButtonRects uses the default 40px bar and a 28px button;
    # click the center of the rightmost close button derived from those fields.
    $closedBefore = Get-ContextMarkerCount '(?m)^APP_RUNTIME_WINDOW_CLOSED='
    for ($attempt = 0; $attempt -lt 2; $attempt++) {
        Set-QmpPointer $Qmp ($x + $w - 22) ($y - 26)
        Send-QmpMouseClick $Qmp 'left'
        try {
            Wait-ForContextMarkerCount '(?m)^APP_RUNTIME_WINDOW_CLOSED=' ($closedBefore + 1) 1800
            return
        } catch {
            # Preserve the normal title-bar proof first, but use the existing
            # global Escape close route when a heavy built-in has not yet
            # accepted title-bar input.  This avoids leaving a valid window
            # behind merely because its specialized OnInput path was busy.
            Send-QmpMonitorKey $Qmp 'esc'
            try {
                Wait-ForContextMarkerCount '(?m)^APP_RUNTIME_WINDOW_CLOSED=' ($closedBefore + 1) 3000
                return
            } catch {
                if ($attempt -eq 1) { throw }
            }
            # A newly-created compatibility window may still be consuming
            # the inherited Start-menu mouse transition.  Reposition and
            # retry once after that bounded input state has drained.
            Start-Sleep -Milliseconds 300
        }
    }
}

function Open-QmpComputerFilesFromHome {
    param($Qmp)
    $routeBefore = Get-ContextMarkerCount '(?m)^APP_RUNTIME_SHELL_ROUTE=COMPUTER_FILES;result=WINDOW$'
    # UEFI Desktop.UpdateUefi draws the FILES tile at x=48..112,y=96..160.
    for ($attempt = 0; $attempt -lt 3; $attempt++) {
        Set-QmpPointer $Qmp 80 120
        Send-QmpMouseClick $Qmp 'left'
        try {
            Wait-ForContextMarkerCount '(?m)^APP_RUNTIME_SHELL_ROUTE=COMPUTER_FILES;result=WINDOW$' ($routeBefore + 1) 6000
            return
        } catch {
            if ($attempt -eq 2) { throw }
            Start-Sleep -Milliseconds 800
        }
    }
}

function Close-QmpTopWindow {
    param($Qmp)
    $closedBefore = Get-ContextMarkerCount '(?m)^APP_RUNTIME_WINDOW_CLOSED='
    $content = Get-Content -LiteralPath $serialPath -Raw -ErrorAction SilentlyContinue
    $errorMatches = [regex]::Matches($content,
        '(?m)^APP_RUNTIME_ERROR_WINDOW_BOUNDS=x=(\d+);y=(\d+);w=(\d+)$')
    if ($errorMatches.Count -gt 0) {
        $errorBounds = $errorMatches[$errorMatches.Count - 1]
        $errorX = [int]$errorBounds.Groups[1].Value
        $errorY = [int]$errorBounds.Groups[2].Value
        $errorW = [int]$errorBounds.Groups[3].Value
        Set-QmpPointer $Qmp ($errorX + $errorW - 22) ($errorY - 26)
        Send-QmpMouseClick $Qmp 'left'
    } else {
        Send-QmpMonitorKey $Qmp 'esc'
    }
    Wait-ForContextMarkerCount '(?m)^APP_RUNTIME_WINDOW_CLOSED=' ($closedBefore + 1) 6000
    Start-Sleep -Milliseconds 160
}

function Close-QmpComputerFilesWindow {
    param($Qmp)
    # Use the last factory launch bounds and the same bounded title-bar/escape
    # retry used for Start launches.  Root routing can leave a fresh shell
    # child at a slightly different clamped origin, so a fixed coordinate is
    # not deterministic under a busy render/input queue.
    Close-QmpLastLaunchedWindow $Qmp
    Start-Sleep -Milliseconds 160
}

function Close-QmpGxmWindow {
    param($Qmp)
    # The diagnostic GXM association route also creates the installer shell
    # probe immediately afterward.  It is therefore the frontmost window;
    # dismiss it through the normal title-bar path before targeting GXM.
    $content = Get-Content -LiteralPath $serialPath -Raw -ErrorAction SilentlyContinue
    $installerBounds = [regex]::Matches($content,
        '(?m)^APP_RUNTIME_INSTALLER_BOUNDS=x=(\d+);y=(\d+);w=(\d+)$')
    if ($installerBounds.Count -gt 0) {
        $installerMatch = $installerBounds[$installerBounds.Count - 1]
        $installerX = [int]$installerMatch.Groups[1].Value
        $installerY = [int]$installerMatch.Groups[2].Value
        $installerW = [int]$installerMatch.Groups[3].Value
        $installerClosedBefore = Get-ContextMarkerCount '(?m)^APP_RUNTIME_WINDOW_CLOSED=title=Install guideXOS to Hard Drive;'
        Set-QmpPointer $Qmp ($installerX + $installerW - 22) ($installerY - 26)
        Send-QmpMouseClick $Qmp 'left'
        Wait-ForContextMarkerCount '(?m)^APP_RUNTIME_WINDOW_CLOSED=title=Install guideXOS to Hard Drive;' ($installerClosedBefore + 1) 6000
        Start-Sleep -Milliseconds 180
    }

    $closedBefore = Get-ContextMarkerCount '(?m)^APP_RUNTIME_WINDOW_CLOSED='
    $gxmBounds = [regex]::Matches($content,
        '(?m)^APP_RUNTIME_GXM_INSTANCE_WINDOW_BOUNDS=x=(\d+);y=(\d+);w=(\d+);h=(\d+);instance=instance-[^;]+$')
    if ($gxmBounds.Count -eq 0) { throw 'GXM instance window bounds marker is missing.' }
    $match = $gxmBounds[$gxmBounds.Count - 1]
    $x = [int]$match.Groups[1].Value
    $y = [int]$match.Groups[2].Value
    $w = [int]$match.Groups[3].Value
    # GXM script controls are populated on the render thread; let the first
    # settled frame compute the title-button hit rectangles.
    Start-Sleep -Milliseconds 450
    for ($attempt = 0; $attempt -lt 2; $attempt++) {
        if ($attempt -eq 0) {
            Set-QmpPointer $Qmp ($x + $w - 22) ($y - 26)
            Send-QmpMouseClick $Qmp 'left'
        } else {
            # GXM has the normal Window title-bar implementation, but its
            # script controls can briefly retain input capture after launch.
            # Escape is the bounded global close fallback for that transition.
            Send-QmpMonitorKey $Qmp 'esc'
        }
        try {
            Wait-ForContextMarkerCount '(?m)^APP_RUNTIME_WINDOW_CLOSED=' ($closedBefore + 1) 6000
            break
        } catch {
            if ($attempt -eq 1) { throw }
            Start-Sleep -Milliseconds 300
        }
    }
    Start-Sleep -Milliseconds 160
}

function Send-QmpComputerFilesSearch {
    param(
        $Qmp,
        [int]$WindowX,
        [int]$WindowY,
        [string]$Text
    )
    # ComputerFiles places its 180px search box at the right side of the
    # toolbar. Center coordinates are derived from the fixed 540px child
    # window width and the existing toolbar padding.
    Set-QmpPointer $Qmp ($WindowX + 442) ($WindowY + 20)
    Send-QmpMouseClick $Qmp 'left'
    for ($i = 0; $i -lt $Text.Length; $i++) {
        Send-QmpMonitorKey $Qmp $Text[$i].ToString().ToLowerInvariant()
        Start-Sleep -Milliseconds 35
    }
    Start-Sleep -Milliseconds 120
}

function Clear-QmpComputerFilesSearch {
    param($Qmp, [int]$Length)
    for ($i = 0; $i -lt $Length; $i++) {
        Send-QmpMonitorKey $Qmp 'backspace'
        Start-Sleep -Milliseconds 25
    }
    Start-Sleep -Milliseconds 100
}

function Get-QmpLatestComputerFilesBounds {
    if (-not (Test-Path -LiteralPath $serialPath)) { return $null }
    $content = Get-Content -LiteralPath $serialPath -Raw -ErrorAction SilentlyContinue
    $matches = [regex]::Matches($content,
        '(?m)^APP_RUNTIME_FILES_WINDOW_BOUNDS=x=(\d+);y=(\d+);w=(\d+);h=(\d+)$')
    if ($matches.Count -eq 0) { return $null }
    $match = $matches[$matches.Count - 1]
    return @{
        X = [int]$match.Groups[1].Value
        Y = [int]$match.Groups[2].Value
        W = [int]$match.Groups[3].Value
        H = [int]$match.Groups[4].Value
    }
}

function Open-QmpFileFromCurrentComputerFiles {
    param(
        $Qmp,
        [ref]$WindowX,
        [ref]$WindowY,
        [ValidateSet('ScriptsText', 'ImagesPng', 'ProgramsGxm')]
        [string]$Fixture
    )

    # The caller has already selected Hard Disk and passes the resulting
    # child origin.  Do not replace it with the older shell-window bounds
    # marker: that would search the drive chooser and make the next click
    # select the first root item instead of the requested directory.
    $x = $WindowX.Value
    $y = $WindowY.Value

    # The Computer Files window uses a 180px left pane and a 48px icon grid.
    # Every Hard Disk selection creates its child at (+20,+20), so track the
    # actual nested window origin instead of assuming one global coordinate.
    if ($Fixture -eq 'ScriptsText') {
        # The RDSK root is laid out from the registered file order: README.md
        # is the first tile, followed by the synthesized directory entries.
        # Scripts is therefore the third tile on the second row for the
        # 48px icon geometry used by ComputerFiles.
        $dirBefore = Get-ContextMarkerCount '(?m)^APP_RUNTIME_FILES_DIR_OPEN=path=Scripts/$'
        Set-QmpPointer $Qmp ($x + 404) ($y + 180)
        Send-QmpMouseClick $Qmp 'left'
        Wait-ForContextMarkerCount '(?m)^APP_RUNTIME_FILES_DIR_OPEN=path=Scripts/$' ($dirBefore + 1) 6000
        Set-QmpPointer $Qmp ($x + 232) ($y + 80)
        $assocBefore = Get-ContextMarkerCount '(?m)^APP_RUNTIME_ASSOC_RESOLVE=name=notepad\.gxm\.txt;'
        $fileBefore = Get-ContextMarkerCount '(?m)^APP_RUNTIME_FILE_OK=path=Scripts/notepad\.gxm\.txt;app=Notepad;'
        Send-QmpMouseClick $Qmp 'left'
        Wait-ForContextMarkerCount '(?m)^APP_RUNTIME_ASSOC_RESOLVE=name=notepad\.gxm\.txt;' ($assocBefore + 1) 6000
        Wait-ForContextMarkerCount '(?m)^APP_RUNTIME_FILE_OK=path=Scripts/notepad\.gxm\.txt;app=Notepad;' ($fileBefore + 1) 8000
        # The factory owns Notepad's implementation bounds, so close the
        # actual last launch rather than deriving a coordinate from the file
        # browser's child origin.  This keeps the association proof stable if
        # the application chooses a different placement or size.
        Close-QmpLastLaunchedWindow $Qmp
        return
    }

    # Return from the current directory to the drive chooser using the
    # existing left-pane Computer Files entry (second icon). The row center
    # is derived from the current window origin and 48px icon pitch.
    Set-QmpPointer $Qmp ($x + 30) ($y + 145)
    Send-QmpMouseClick $Qmp 'left'
    Start-Sleep -Milliseconds 120
    # Select Hard Disk in the chooser and open a fresh root child window.
    Set-QmpPointer $Qmp ($x + 230) ($y + 80)
    Send-QmpMouseClick $Qmp 'left'
    Start-Sleep -Milliseconds 160
    $bounds = Get-QmpLatestComputerFilesBounds
    if ($null -ne $bounds) {
        $x = $bounds.X
        $y = $bounds.Y
    } else {
        $x += 20
        $y += 20
    }
    $WindowX.Value = $x
    $WindowY.Value = $y

    if ($Fixture -eq 'ImagesPng') {
        $dirBefore = Get-ContextMarkerCount '(?m)^APP_RUNTIME_FILES_DIR_OPEN=path=Images/$'
        # Images is the first tile on the second row of the root grid after
        # the fresh Hard Disk child is created.
        Set-QmpPointer $Qmp ($x + 228) ($y + 190)
        Send-QmpMouseClick $Qmp 'left'
        Wait-ForContextMarkerCount '(?m)^APP_RUNTIME_FILES_DIR_OPEN=path=Images/$' ($dirBefore + 1) 6000
        Set-QmpPointer $Qmp ($x + 232) ($y + 80)
        $assocBefore = Get-ContextMarkerCount '(?m)^APP_RUNTIME_ASSOC_RESOLVE=name=audiopause\.png;'
        $fileBefore = Get-ContextMarkerCount '(?m)^APP_RUNTIME_FILE_OK=path=Images/audiopause\.png;app=Image Viewer;'
        Send-QmpMouseClick $Qmp 'left'
        Wait-ForContextMarkerCount '(?m)^APP_RUNTIME_ASSOC_RESOLVE=name=audiopause\.png;' ($assocBefore + 1) 6000
        Wait-ForContextMarkerCount '(?m)^APP_RUNTIME_FILE_OK=path=Images/audiopause\.png;app=Image Viewer;' ($fileBefore + 1) 12000
        # Desktop reuses its ImageViewer singleton at (400,400), which the
        # existing screen clamp places at (400,350), with a 600px client
        # width; close it through the normal title-bar route.
        $closedBefore = Get-ContextMarkerCount '(?m)^APP_RUNTIME_WINDOW_CLOSED='
        Set-QmpPointer $Qmp 978 324
        Send-QmpMouseClick $Qmp 'left'
        Wait-ForContextMarkerCount '(?m)^APP_RUNTIME_WINDOW_CLOSED=' ($closedBefore + 1) 6000

        # The ramdisk also contains a second supported raster fixture.  Open
        # the adjacent first-row tile after the reused viewer has closed so
        # the association path proves a fresh document update as well as the
        # original decode and cleanup path.
        $assocBeforeSecond = Get-ContextMarkerCount '(?m)^APP_RUNTIME_ASSOC_RESOLVE=name=audioplay\.png;'
        $fileBeforeSecond = Get-ContextMarkerCount '(?m)^APP_RUNTIME_FILE_OK=path=Images/audioplay\.png;app=Image Viewer;'
        Set-QmpPointer $Qmp ($x + 320) ($y + 80)
        Send-QmpMouseClick $Qmp 'left'
        Wait-ForContextMarkerCount '(?m)^APP_RUNTIME_ASSOC_RESOLVE=name=audioplay\.png;' ($assocBeforeSecond + 1) 6000
        Wait-ForContextMarkerCount '(?m)^APP_RUNTIME_FILE_OK=path=Images/audioplay\.png;app=Image Viewer;' ($fileBeforeSecond + 1) 12000
        $closedBeforeSecond = Get-ContextMarkerCount '(?m)^APP_RUNTIME_WINDOW_CLOSED='
        Set-QmpPointer $Qmp 978 324
        Send-QmpMouseClick $Qmp 'left'
        Wait-ForContextMarkerCount '(?m)^APP_RUNTIME_WINDOW_CLOSED=' ($closedBeforeSecond + 1) 6000
        # Let the final button-up and window cleanup reach the guest before
        # the workload returns and the serial statistics are sampled.
        Start-Sleep -Milliseconds 500
        return
    }

    $dirBefore = Get-ContextMarkerCount '(?m)^APP_RUNTIME_FILES_DIR_OPEN=path=Programs/$'
    # Programs is the middle tile on the second root row.
    Set-QmpPointer $Qmp ($x + 316) ($y + 190)
    Send-QmpMouseClick $Qmp 'left'
    Wait-ForContextMarkerCount '(?m)^APP_RUNTIME_FILES_DIR_OPEN=path=Programs/$' ($dirBefore + 1) 6000
    Set-QmpPointer $Qmp ($x + 232) ($y + 80)
    Set-QmpPointer $Qmp ($x + 232) ($y + 80)
    $assocBefore = Get-ContextMarkerCount '(?m)^APP_RUNTIME_ASSOC_RESOLVE=name=calculator\.gxm;'
    $fileBefore = Get-ContextMarkerCount '(?m)^APP_RUNTIME_FILE_RESULT=path=Programs/calculator\.gxm;app=GXM;'
    Send-QmpMouseClick $Qmp 'left'
    Wait-ForContextMarkerCount '(?m)^APP_RUNTIME_ASSOC_RESOLVE=name=calculator\.gxm;' ($assocBefore + 1) 6000
    Wait-ForContextMarkerCount '(?m)^APP_RUNTIME_FILE_RESULT=path=Programs/calculator\.gxm;app=GXM;' ($fileBefore + 1) 12000
    Close-QmpGxmWindow $Qmp
}

function Send-QmpAppRuntimeWorkload {
    param($Qmp)

    $apps = @(
        'Calculator', 'Computer Files', 'Console', 'Devices', 'Disk Manager',
        'Display Options', 'Firewall', 'Image Viewer', 'Notepad', 'Paint',
        'Task Manager', 'WAV Player'
    )
    for ($i = 0; $i -lt $apps.Count; $i++) {
        Open-QmpStartApplication $Qmp $apps[$i] $i
    }

    # Repeated launches of a light and a text application expose duplicate
    # registrations, stale state, and owner-memory cleanup defects.
    for ($cycle = 0; $cycle -lt 3; $cycle++) {
        Open-QmpStartApplication $Qmp 'Calculator' 0
        Open-QmpStartApplication $Qmp 'Notepad' 8
    }

    # Console keeps its reusable FConsole backend, but its semantic owner is
    # now a reusable application instance. Prove reuse with two extra cycles.
    for ($cycle = 0; $cycle -lt 2; $cycle++) {
        Open-QmpStartApplication $Qmp 'Console' 2
    }

    # Exercise the actual desktop shell-object route, then the hard-disk
    # installer object. In QEMU no USB storage is present, so probe that
    # unavailable object through Desktop.OnClick's guarded route later.
    Open-QmpComputerFilesFromHome $Qmp
    # Computer Files' first left-pane entry is the available UEFI route for
    # the registered Root shell object (both Desktop and Computer Files
    # entries intentionally resolve to the same root chooser). Use the first
    # icon row so the probe remains valid for the configured 32/48px icon set.
    $rootBefore = Get-ContextMarkerCount '(?m)^APP_RUNTIME_SHELL_ROUTE=ROOT;result=COMPUTER_FILES_ROOT$'
    for ($attempt = 0; $attempt -lt 3; $attempt++) {
        Set-QmpPointer $Qmp 340 260
        Send-QmpMouseClick $Qmp 'left'
        try {
            Wait-ForContextMarkerCount '(?m)^APP_RUNTIME_SHELL_ROUTE=ROOT;result=COMPUTER_FILES_ROOT$' ($rootBefore + 1) 6000
            break
        } catch {
            if ($attempt -eq 2) { throw }
            Start-Sleep -Milliseconds 800
        }
    }
    Close-QmpComputerFilesWindow $Qmp

    Open-QmpComputerFilesFromHome $Qmp
    # Desktop shell route uses a 540px window at (300,200). Hard Disk is the
    # first chooser tile at (508,256); the fresh child is at (320,220).
    Set-QmpPointer $Qmp 530 280
    Send-QmpMouseClick $Qmp 'left'
    Start-Sleep -Milliseconds 180
    # The initial Hard Disk selection above creates the first drive-specific
    # child at the parent shell window origin plus (20,20).
    $computerFilesX = 320
    $computerFilesY = 220
    Open-QmpFileFromCurrentComputerFiles $Qmp ([ref]$computerFilesX) ([ref]$computerFilesY) 'ScriptsText'
    Open-QmpFileFromCurrentComputerFiles $Qmp ([ref]$computerFilesX) ([ref]$computerFilesY) 'ImagesPng'
    Open-QmpFileFromCurrentComputerFiles $Qmp ([ref]$computerFilesX) ([ref]$computerFilesY) 'ProgramsGxm'

    # Close the remaining Computer Files windows until the shell is idle.
    for ($i = 0; $i -lt 6; $i++) {
        try { Close-QmpTopWindow $Qmp } catch { break }
    }

    # The diagnostic shell probe creates the installer before the bounded
    # missing-file MessageBox probes. Dismiss the final error window first so
    # the installer is again the top input target, then close it from its
    # actual runtime bounds. This proves both modal cleanup and installer
    # lifecycle explicitly.
    $content = Get-Content -LiteralPath $serialPath -Raw -ErrorAction SilentlyContinue
    $installerBounds = [regex]::Matches($content,
        '(?m)^APP_RUNTIME_INSTALLER_BOUNDS=x=(\d+);y=(\d+);w=(\d+)$')
    if ($installerBounds.Count -eq 0) { throw 'Installer runtime bounds marker was not emitted.' }
    $installerMatch = $installerBounds[$installerBounds.Count - 1]
    $installerX = [int]$installerMatch.Groups[1].Value
    $installerY = [int]$installerMatch.Groups[2].Value
    $installerW = [int]$installerMatch.Groups[3].Value
    $installerClosed = Get-ContextMarkerCount '(?m)^APP_RUNTIME_WINDOW_CLOSED=title=Install guideXOS to Hard Drive;'
    if ($installerClosed -eq 0) {
        $errorBounds = [regex]::Matches($content,
            '(?m)^APP_RUNTIME_ERROR_WINDOW_BOUNDS=x=(\d+);y=(\d+);w=(\d+)$')
        if ($errorBounds.Count -gt 0) {
            Close-QmpTopWindow $Qmp
            Start-Sleep -Milliseconds 300
        }
        # Window.BarHeight is 40 and the close-button center is six pixels
        # above the title-bar midpoint used by the existing close helper.
        Set-QmpPointer $Qmp ($installerX + $installerW - 22) ($installerY - 26)
        Send-QmpMouseClick $Qmp 'left'
        try {
            Wait-ForContextMarkerCount '(?m)^APP_RUNTIME_WINDOW_CLOSED=title=Install guideXOS to Hard Drive;' 1 4000
        } catch {
            # The installer also has a bounded Cancel button. Use it as a
            # deterministic fallback if a title-bar click is consumed by a
            # still-draining window transition.
            Set-QmpPointer $Qmp ($installerX + 80) ($installerY + 448)
            Send-QmpMouseClick $Qmp 'left'
            Wait-ForContextMarkerCount '(?m)^APP_RUNTIME_WINDOW_CLOSED=title=Install guideXOS to Hard Drive;' 1 4000
        }
    }

    # If the final close click was still in the guest's native input queue,
    # explicitly drain the release transition before ending the workload.
    # PS/2 transition accounting ignores a duplicate release when no button is
    # held, so this is safe in both states and prevents captured input.
    Send-QmpEvents $Qmp @((New-QmpButtonEvent 'left' $false))
    Start-Sleep -Milliseconds 400
    # Normalize either possible transition state with one neutral click in an
    # empty desktop area.  If a prior release was delayed, the down is a
    # duplicate and this pair contributes only the missing up; otherwise it
    # contributes one balanced pair.  The coordinate is outside shell tiles.
    Set-QmpPointer $Qmp 780 400
    Send-QmpEvents $Qmp @((New-QmpButtonEvent 'left' $true))
    Start-Sleep -Milliseconds 220
    Send-QmpEvents $Qmp @((New-QmpButtonEvent 'left' $false))
    Start-Sleep -Milliseconds 1200

    Set-QmpPointer $Qmp 80 120
    Start-Sleep -Milliseconds 250
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
    # Native PS/2 input can be substantially slower than the host while the
    # UEFI guest is rendering blur-backed shell surfaces.  Keep the button
    # asserted long enough for the guest to observe the down transition before
    # sending the release; otherwise QEMU may coalesce the pair.
    Start-Sleep -Milliseconds 1000
    Send-QmpEvents $Qmp @((New-QmpButtonEvent $Button $false))
    Start-Sleep -Milliseconds 450
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
        Start-Sleep -Milliseconds 120
        Set-QmpPointer $Qmp 410 248
        Start-Sleep -Milliseconds 120
        Set-QmpPointer $Qmp 410 276
        Start-Sleep -Milliseconds 180
        # The submenu is to the right of the 220px menu. Select the 32px row
        # (the third 28px row, centered at y=338 for a menu origin of 212).
        Send-QmpRelative $Qmp 212 62
        Start-Sleep -Milliseconds 180
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
    # A final explicit release makes the workload self-normalizing if the
    # guest observed a right-button down but QEMU delayed its matching up
    # while a popup was being rendered. Duplicate releases are ignored by the
    # PS/2 transition counters.
    Send-QmpEvents $Qmp @((New-QmpButtonEvent 'right' $false))
    Send-QmpEvents $Qmp @((New-QmpButtonEvent 'left' $false))
    Start-Sleep -Milliseconds 1000
}

function Send-QmpContextMenuSoak {
    param(
        $Qmp,
        [int]$DurationSeconds = 300
    )

    $deadline = (Get-Date).AddSeconds($DurationSeconds)
    $cycle = 0
    while ((Get-Date) -lt $deadline) {
        $openedBefore = Get-ContextMarkerCount '(?m)^CONTEXT_MENU_OPENED='
        Open-QmpContextMenu $Qmp 400 220
        Wait-ForContextMarkerCount '(?m)^CONTEXT_MENU_OPENED=' ($openedBefore + 1)

        if (($cycle % 2) -eq 0) {
            # Keep the pointer over a real entry before using the existing
            # keyboard dismissal path.
            Set-QmpPointer $Qmp 410 220
            Start-Sleep -Milliseconds 150
            $dismissedBefore = Get-ContextMarkerCount '(?m)^CONTEXT_MENU_DISMISSED=ESCAPE'
            Send-QmpEvents $Qmp @((New-QmpKeyEvent 'esc' $true))
            Send-QmpEvents $Qmp @((New-QmpKeyEvent 'esc' $false))
            Wait-ForContextMarkerCount '(?m)^CONTEXT_MENU_DISMISSED=ESCAPE' ($dismissedBefore + 1)
        } else {
            Set-QmpPointer $Qmp 700 470
            Start-Sleep -Milliseconds 80
            $dismissedBefore = Get-ContextMarkerCount '(?m)^CONTEXT_MENU_DISMISSED=CLICK_AWAY'
            Send-QmpMouseClick $Qmp 'left'
            Wait-ForContextMarkerCount '(?m)^CONTEXT_MENU_DISMISSED=CLICK_AWAY' ($dismissedBefore + 1)
        }

        $cycle++
        Start-Sleep -Seconds 10
    }

    Set-QmpPointer $Qmp 80 120
}

function Send-QmpWidgetWorkload {
    param(
        $Qmp,
        [bool]$Stress = $false
    )

    # The diagnostic widget build makes the normal container visible. Start
    # over the PerformanceWidget, then alternate between the first two real
    # widget rows to exercise hover routing.
    Set-QmpPointer $Qmp 1100 120
    $hoverCycles = if ($Stress) { 50 } else { 3 }
    for ($i = 0; $i -lt $hoverCycles; $i++) {
        Send-QmpRelative $Qmp 0 80
        Send-QmpRelative $Qmp 0 -80
        Start-Sleep -Milliseconds 5
    }
    Set-QmpPointer $Qmp 1100 120

    # Right-click a docked widget and activate the existing Undock command.
    # This proves the normal WidgetContextMenu ownership path.
    $openedBefore = Get-ContextMarkerCount '(?m)^WIDGET_MENU_OPENED='
    Open-QmpContextMenu $Qmp 1100 120
    Wait-ForContextMarkerCount '(?m)^WIDGET_MENU_OPENED=' ($openedBefore + 1)
    Set-QmpPointer $Qmp 1108 132
    $activatedBefore = Get-ContextMarkerCount '(?m)^WIDGET_MENU_ACTIVATED=UNDOCK'
    Send-QmpMouseClick $Qmp 'left'
    Wait-ForContextMarkerCount '(?m)^WIDGET_MENU_ACTIVATED=UNDOCK' ($activatedBefore + 1)

    if (-not $Stress) {
        # Drag the now-standalone widget to the lower-right boundary. The
        # widget menu must clamp there without drawing beyond the framebuffer.
        Set-QmpPointer $Qmp 1100 150
        Send-QmpEvents $Qmp @((New-QmpButtonEvent 'left' $true))
        Start-Sleep -Milliseconds 90
        for ($i = 0; $i -lt 100; $i += 100) {
            Send-QmpRelative $Qmp 100 0
        }
        for ($i = 0; $i -lt 550; $i += 100) {
            Send-QmpRelative $Qmp 0 ([Math]::Min(100, 550 - $i))
        }
        Start-Sleep -Milliseconds 120
        Send-QmpEvents $Qmp @((New-QmpButtonEvent 'left' $false))
        Start-Sleep -Milliseconds 180

        $openedBefore = Get-ContextMarkerCount '(?m)^WIDGET_MENU_OPENED='
        Open-QmpContextMenu $Qmp 1200 700
        Wait-ForContextMarkerCount '(?m)^WIDGET_MENU_OPENED=' ($openedBefore + 1)
        Set-QmpPointer $Qmp 1170 714
        Start-Sleep -Milliseconds 100
        $activatedBefore = Get-ContextMarkerCount '(?m)^WIDGET_MENU_ACTIVATED=DOCK'
        Send-QmpMouseClick $Qmp 'left'
        Wait-ForContextMarkerCount '(?m)^WIDGET_MENU_ACTIVATED=DOCK' ($activatedBefore + 1)

        # Prove click-away dismissal for the widget popup, then prove that the
        # existing desktop and taskbar popups can still be opened afterward.
        Open-QmpContextMenu $Qmp 1200 700
        Set-QmpPointer $Qmp 500 220
        $dismissedBefore = Get-ContextMarkerCount '(?m)^WIDGET_MENU_DISMISSED=CLICK_AWAY'
        Send-QmpMouseClick $Qmp 'left'
        Wait-ForContextMarkerCount '(?m)^WIDGET_MENU_DISMISSED=CLICK_AWAY' ($dismissedBefore + 1)

        Open-QmpContextMenu $Qmp 1200 700
        Set-QmpPointer $Qmp 500 220
        $escapeBefore = Get-ContextMarkerCount '(?m)^WIDGET_MENU_DISMISSED=ESCAPE'
        Send-QmpEvents $Qmp @((New-QmpKeyEvent 'esc' $true))
        Send-QmpEvents $Qmp @((New-QmpKeyEvent 'esc' $false))
        Wait-ForContextMarkerCount '(?m)^WIDGET_MENU_DISMISSED=ESCAPE' ($escapeBefore + 1)

        $desktopPopupBefore = Get-ContextMarkerCount '(?m)^WIDGET_DESKTOP_MENU_OPENED='
        Open-QmpContextMenu $Qmp 500 220
        Wait-ForContextMarkerCount '(?m)^WIDGET_DESKTOP_MENU_OPENED=' ($desktopPopupBefore + 1)
        Set-QmpPointer $Qmp 600 320
        Send-QmpMouseClick $Qmp 'left'

        $taskbarPopupBefore = Get-ContextMarkerCount '(?m)^WIDGET_TASKBAR_MENU_OPENED='
        Open-QmpContextMenu $Qmp 400 790
        Wait-ForContextMarkerCount '(?m)^WIDGET_TASKBAR_MENU_OPENED=' ($taskbarPopupBefore + 1)
        Send-QmpEvents $Qmp @((New-QmpKeyEvent 'esc' $true))
        Send-QmpEvents $Qmp @((New-QmpKeyEvent 'esc' $false))
        Start-Sleep -Milliseconds 120
    } else {
        # After the first Undock, the standalone PerformanceWidget remains at
        # its known diagnostic location. Repeated Dock commands are harmless
        # existing actions and close the reused popup each time.
        for ($i = 0; $i -lt 25; $i++) {
            $openedBefore = Get-ContextMarkerCount '(?m)^WIDGET_MENU_OPENED='
            Open-QmpContextMenu $Qmp 1100 150
            Wait-ForContextMarkerCount '(?m)^WIDGET_MENU_OPENED=' ($openedBefore + 1)
            Set-QmpPointer $Qmp 1108 162
            $activatedBefore = Get-ContextMarkerCount '(?m)^WIDGET_MENU_ACTIVATED=DOCK'
            Send-QmpMouseClick $Qmp 'left'
            Wait-ForContextMarkerCount '(?m)^WIDGET_MENU_ACTIVATED=DOCK' ($activatedBefore + 1)
        }

        for ($i = 0; $i -lt 25; $i++) {
            $openedBefore = Get-ContextMarkerCount '(?m)^WIDGET_MENU_OPENED='
            Open-QmpContextMenu $Qmp 1100 150
            Wait-ForContextMarkerCount '(?m)^WIDGET_MENU_OPENED=' ($openedBefore + 1)
            Set-QmpPointer $Qmp 700 470
            $dismissedBefore = Get-ContextMarkerCount '(?m)^WIDGET_MENU_DISMISSED=CLICK_AWAY'
            Send-QmpMouseClick $Qmp 'left'
            Wait-ForContextMarkerCount '(?m)^WIDGET_MENU_DISMISSED=CLICK_AWAY' ($dismissedBefore + 1)
        }

        Open-QmpContextMenu $Qmp 1100 150
        Set-QmpPointer $Qmp 700 470
        $escapeBefore = Get-ContextMarkerCount '(?m)^WIDGET_MENU_DISMISSED=ESCAPE'
        Send-QmpEvents $Qmp @((New-QmpKeyEvent 'esc' $true))
        Send-QmpEvents $Qmp @((New-QmpKeyEvent 'esc' $false))
        Wait-ForContextMarkerCount '(?m)^WIDGET_MENU_DISMISSED=ESCAPE' ($escapeBefore + 1)

        # Keep popup coexistence covered in the stress build as well.
        $desktopPopupBefore = Get-ContextMarkerCount '(?m)^WIDGET_DESKTOP_MENU_OPENED='
        Open-QmpContextMenu $Qmp 500 220
        Wait-ForContextMarkerCount '(?m)^WIDGET_DESKTOP_MENU_OPENED=' ($desktopPopupBefore + 1)
        Set-QmpPointer $Qmp 900 500
        Send-QmpMouseClick $Qmp 'left'

        $taskbarPopupBefore = Get-ContextMarkerCount '(?m)^WIDGET_TASKBAR_MENU_OPENED='
        Open-QmpContextMenu $Qmp 400 790
        Wait-ForContextMarkerCount '(?m)^WIDGET_TASKBAR_MENU_OPENED=' ($taskbarPopupBefore + 1)
        Send-QmpEvents $Qmp @((New-QmpKeyEvent 'esc' $true))
        Send-QmpEvents $Qmp @((New-QmpKeyEvent 'esc' $false))
    }

    Set-QmpPointer $Qmp 80 120
}

function Send-QmpWidgetSoak {
    param(
        $Qmp,
        [int]$DurationSeconds = 600
    )

    Send-QmpWidgetWorkload $Qmp $false
    Write-Host "  widget-enabled continuous soak: $DurationSeconds seconds" -ForegroundColor Green
    Start-Sleep -Seconds $DurationSeconds
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
if ($isInteractiveValidation) {
    $qmpPort = Get-Random -Minimum 43000 -Maximum 43999
    $qemuArgs += @('-qmp', "tcp:127.0.0.1:$qmpPort,server=on,wait=off")
}

Write-Host "Serial log: $serialPath" -ForegroundColor Gray
Write-Host 'Starting QEMU...' -ForegroundColor Green
$qemu = Start-Process -FilePath $qemuPath -ArgumentList $qemuArgs -PassThru
$qmp = $null
if ($isInteractiveValidation) {
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

            if ($isInteractiveValidation -and $continuousEntered -and
                $content -match 'CONTINUOUS_HEARTBEAT_FRAME=' -and
                -not $inputInjected) {
                try {
                    Write-Host '  injecting bounded native keyboard/mouse workload' -ForegroundColor Green
                    if ($diagnosticMode -eq 'ContextMenu') {
                        Send-QmpContextMenuWorkload $qmp
                    } elseif ($isAppRuntimeValidation) {
                        Send-QmpAppRuntimeWorkload $qmp
                        $status = 'APP_RUNTIME_COMPLETE'
                    } elseif ($isWidgetValidation) {
                        if ($WidgetSoak) {
                            Send-QmpWidgetSoak $qmp 600
                            $status = 'WIDGET_SOAK_COMPLETE'
                        } else {
                            Send-QmpWidgetWorkload $qmp $WidgetStress
                            $status = if ($WidgetStress) {
                                'WIDGET_STRESS_COMPLETE'
                            } else {
                                'WIDGET_COMPLETE'
                            }
                        }
                    } else {
                        Send-QmpWorkload $qmp $NativeInputStress
                    }
                    $inputInjected = $true
                    if ($diagnosticMode -eq 'ContextMenu') {
                        if ($ContextMenuSoak) {
                            Write-Host '  running five-minute context-menu interaction soak' -ForegroundColor Green
                            Send-QmpContextMenuSoak $qmp 300
                        }
                        $status = 'CONTEXT_MENU_COMPLETE'
                        break
                    }
                    if ($isWidgetValidation) {
                        break
                    }
                    if ($isAppRuntimeValidation) {
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
                '(?im)(CONTINUOUS_DESKTOP_FAULT=[^\r\n]*|APP_RUNTIME_FAULT=[^\r\n]*|PNG_PROBE_FAIL[^\r\n]*|PNG_PROBE_ALPHA_RENDER_OK=0|BACKGROUND_PROBE_FAIL[^\r\n]*|BACKGROUND_ROTATION_FAIL[^\r\n]*|BACKGROUND_PROBE_RENDER_OK=0|BACKGROUND_ROTATION_RENDER_OK=0|FONT_PROBE_FAIL[^\r\n]*|FONT_PROBE_INIT_OK=0|FONT_PROBE_MEASURE_OK=0|FONT_RENDER_OK=0|CONTEXT_MENU_BOUNDS=[^\r\n]*,ok=0|CONTEXT_MENU_DRAWN=[^\r\n]*,font=0|TASKBAR_CONTEXT_MENU_BOUNDS=[^\r\n]*,ok=0|TASKBAR_CONTEXT_MENU_DRAWN=[^\r\n]*,font=0|WIDGET_INIT=[^\r\n]*,ok=0|WIDGET_INIT=[^\r\n]*,bounds=0|WIDGET_DRAW=[^\r\n]*bounds=0|WIDGET_MENU_BOUNDS=[^\r\n]*,ok=0|WIDGET_MENU_DRAWN=[^\r\n]*,font=0|WIDGET_RUNTIME_FAULT=[^\r\n]*|CPU_FAULT_[A-Z_]+|#UD|#GP|#PF|GENERAL_PROTECTION|PAGE_FAULT|PANIC:|UEFI_FRAME_FAULT_CONTEXT)')
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
                if ($isInteractiveValidation -and -not $inputInjected) {
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
# QEMU's redirected serial stream can finish writing just after the guest
# process is stopped. Give the host-side file handle a short, bounded settle
# window before evaluating markers; this does not extend the guest workload
# timeout or mask a missing marker.
for ($flushAttempt = 0; $flushAttempt -lt 20; $flushAttempt++) {
    if (Test-Path -LiteralPath $serialPath) {
        $finalContent = Get-Content -LiteralPath $serialPath -Raw -ErrorAction SilentlyContinue
    }
    if (-not $isStartMenuValidation -or $finalContent -match '(?m)^START_MENU_OPENED(?:=|$)') {
        break
    }
    Start-Sleep -Milliseconds 250
}

# Context-menu QMP workloads complete from inside the injection function, so
# refresh the summary counters from the complete serial log before reporting.
$finalHeartbeatMatches = [regex]::Matches(
    $finalContent,
    'CONTINUOUS_HEARTBEAT_FRAME=(\d+)')
if ($finalHeartbeatMatches.Count -gt 0) {
    $firstHeartbeatFrame = [int]$finalHeartbeatMatches[0].Groups[1].Value
    $lastHeartbeatFrame = [int]$finalHeartbeatMatches[$finalHeartbeatMatches.Count - 1].Groups[1].Value
    $heartbeatCount = $finalHeartbeatMatches.Count
}
$finalTimerMatches = [regex]::Matches(
    $finalContent,
    'CONTINUOUS_HEARTBEAT_TIMER=(\d+)')
if ($finalTimerMatches.Count -gt 0) {
    $firstHeartbeatTimer = [UInt64]$finalTimerMatches[0].Groups[1].Value
    $lastHeartbeatTimer = [UInt64]$finalTimerMatches[$finalTimerMatches.Count - 1].Groups[1].Value
}
$validationElapsedSeconds = [int][Math]::Floor(((Get-Date) - $startedAt).TotalSeconds)

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

$widgetValidation = $null
if ($isWidgetValidation) {
    $widgetInitGood = [regex]::Matches($finalContent,
        '(?m)^WIDGET_INIT=[^\r\n]*,ok=1,bounds=1').Count
    $widgetInitBad = [regex]::Matches($finalContent,
        '(?m)^WIDGET_INIT=[^\r\n]*,(?:ok=0|bounds=0)').Count
    $widgetDrawGood = [regex]::Matches($finalContent,
        '(?m)^WIDGET_DRAW=[^\r\n]*,bounds=1,font=1').Count
    $widgetDrawBad = [regex]::Matches($finalContent,
        '(?m)^WIDGET_DRAW=[^\r\n]*(?:bounds=0|font=0)').Count
    $widgetMenuOpened = [regex]::Matches($finalContent,
        '(?m)^WIDGET_MENU_OPENED=').Count
    $widgetMenuDrawn = [regex]::Matches($finalContent,
        '(?m)^WIDGET_MENU_DRAWN=[^\r\n]*,font=1').Count
    $widgetMenuGoodBounds = [regex]::Matches($finalContent,
        '(?m)^WIDGET_MENU_BOUNDS=[^\r\n]*,ok=1').Count
    $widgetMenuBadBounds = [regex]::Matches($finalContent,
        '(?m)^WIDGET_MENU_BOUNDS=[^\r\n]*,ok=0').Count
    $widgetMenuHover = [regex]::Matches($finalContent,
        '(?m)^WIDGET_MENU_HOVER=').Count
    $widgetUndock = [regex]::Matches($finalContent,
        '(?m)^WIDGET_MENU_ACTIVATED=UNDOCK').Count
    $widgetDock = [regex]::Matches($finalContent,
        '(?m)^WIDGET_MENU_ACTIVATED=DOCK').Count
    $widgetClickAway = [regex]::Matches($finalContent,
        '(?m)^WIDGET_MENU_DISMISSED=CLICK_AWAY').Count
    $widgetEscape = [regex]::Matches($finalContent,
        '(?m)^WIDGET_MENU_DISMISSED=ESCAPE').Count
    $widgetDesktopPopup = [regex]::Matches($finalContent,
        '(?m)^WIDGET_DESKTOP_MENU_OPENED=').Count
    $widgetTaskbarPopup = [regex]::Matches($finalContent,
        '(?m)^WIDGET_TASKBAR_MENU_OPENED=').Count
    $widgetFaults = [regex]::Matches($finalContent,
        '(?m)^WIDGET_RUNTIME_FAULT=').Count

    $widgetUpdateCounts = [ordered]@{}
    foreach ($widgetName in @('PerformanceWidget', 'Clock', 'Monitor', 'Uptime')) {
        $updateMatches = [regex]::Matches($finalContent,
            "(?m)^WIDGET_UPDATE=$widgetName,count=(\d+)")
        $widgetUpdateCounts[$widgetName] = if ($updateMatches.Count -gt 0) {
            [int]$updateMatches[$updateMatches.Count - 1].Groups[1].Value
        } else { 0 }
    }
    $widgetUpdatesPass = ($widgetUpdateCounts.Values | Where-Object { $_ -lt 1 }).Count -eq 0

    $hoverCountMatches = [regex]::Matches($finalContent,
        '(?m)^WIDGET_HOVER=[^\r\n]*,count=(\d+)')
    $widgetHoverCount = if ($hoverCountMatches.Count -gt 0) {
        [int]$hoverCountMatches[$hoverCountMatches.Count - 1].Groups[1].Value
    } else { 0 }
    $rightInputMatches = [regex]::Matches($finalContent,
        '(?m)^WIDGET_INPUT=RIGHT,count=(\d+)')
    $widgetRightInputCount = if ($rightInputMatches.Count -gt 0) {
        [int]$rightInputMatches[$rightInputMatches.Count - 1].Groups[1].Value
    } else { 0 }
    $dragStartMatches = [regex]::Matches($finalContent,
        '(?m)^WIDGET_INPUT=DRAG_START,count=(\d+)')
    $widgetDragStarts = if ($dragStartMatches.Count -gt 0) {
        [int]$dragStartMatches[$dragStartMatches.Count - 1].Groups[1].Value
    } else { 0 }
    $dragEndMatches = [regex]::Matches($finalContent,
        '(?m)^WIDGET_INPUT=DRAG_END,count=(\d+)')
    $widgetDragEnds = if ($dragEndMatches.Count -gt 0) {
        [int]$dragEndMatches[$dragEndMatches.Count - 1].Groups[1].Value
    } else { 0 }

    $widgetInitialized = [regex]::Matches($finalContent,
        '(?m)^WIDGETS_INITIALIZED=1,count=4,visible=1').Count -gt 0
    $widgetExpectedStatus = if ($WidgetSoak) {
        'WIDGET_SOAK_COMPLETE'
    } elseif ($WidgetStress) {
        'WIDGET_STRESS_COMPLETE'
    } else {
        'WIDGET_COMPLETE'
    }
    $widgetExpectedMenus = if ($WidgetStress) { 51 } else { 3 }
    $widgetExpectedDock = if ($WidgetStress) { 25 } else { 1 }
    $widgetExpectedClickAway = if ($WidgetStress) { 25 } else { 1 }
    $widgetExpectedHover = if ($WidgetStress) { 50 } else { 1 }
    $widgetSoakTimerDelta = if ($lastHeartbeatTimer -ge $firstHeartbeatTimer) {
        $lastHeartbeatTimer - $firstHeartbeatTimer
    } else { 0 }
    $widgetPass =
        $status -eq $widgetExpectedStatus -and
        $widgetInitialized -and
        $widgetInitGood -ge 4 -and $widgetInitBad -eq 0 -and
        $widgetDrawGood -ge 4 -and $widgetDrawBad -eq 0 -and
        $widgetUpdatesPass -and
        $widgetMenuOpened -ge $widgetExpectedMenus -and
        $widgetMenuDrawn -ge $widgetExpectedMenus -and
        $widgetMenuGoodBounds -ge $widgetExpectedMenus -and
        $widgetMenuBadBounds -eq 0 -and $widgetMenuHover -gt 0 -and
        $widgetUndock -ge 1 -and $widgetDock -ge $widgetExpectedDock -and
        $widgetClickAway -ge $widgetExpectedClickAway -and
        $widgetEscape -ge 1 -and $widgetDesktopPopup -ge 1 -and
        $widgetTaskbarPopup -ge 1 -and $widgetFaults -eq 0 -and
        $widgetHoverCount -ge $widgetExpectedHover -and
        $widgetRightInputCount -ge $(if ($WidgetStress) { 26 } else { 2 }) -and
        $(if ($WidgetStress) { $true } else { $widgetDragStarts -ge 1 -and $widgetDragEnds -ge 1 }) -and
        $(if ($WidgetSoak) { $validationElapsedSeconds -ge 540 } else { $true })
    $widgetValidation = [ordered]@{
        pass = $widgetPass
        initialized = $widgetInitialized
        initGoodBad = "$widgetInitGood/$widgetInitBad"
        drawsGoodBad = "$widgetDrawGood/$widgetDrawBad"
        updates = (($widgetUpdateCounts.GetEnumerator() | ForEach-Object {
            "$($_.Key)=$($_.Value)"
        }) -join ',')
        menuOpenedDrawn = "$widgetMenuOpened/$widgetMenuDrawn"
        menuBoundsGoodBad = "$widgetMenuGoodBounds/$widgetMenuBadBounds"
        menuHover = $widgetMenuHover
        undockDock = "$widgetUndock/$widgetDock"
        clickAwayEscape = "$widgetClickAway/$widgetEscape"
        desktopTaskbarPopup = "$widgetDesktopPopup/$widgetTaskbarPopup"
        hover = $widgetHoverCount
        rightInput = $widgetRightInputCount
        dragStartEnd = "$widgetDragStarts/$widgetDragEnds"
        soakTimerDelta = $widgetSoakTimerDelta
        soakWallSeconds = $validationElapsedSeconds
        runtimeFaults = $widgetFaults
    }
    if ($status -eq $widgetExpectedStatus -and -not $widgetPass) {
        $status = 'WIDGET_VALIDATION_FAILED'
    }
}

$runtimeValidation = $null
$appModelValidation = $null
if ($isAppModelValidation) {
    $appModelDescriptors = [regex]::Matches($finalContent,
        '(?m)^APP_MODEL_MODERN_DESCRIPTOR_COUNT=(\d+)$')
    $appModelFactories = [regex]::Matches($finalContent,
        '(?m)^APP_MODEL_FACTORY_REGISTRATIONS=(\d+)$')
    $appModelFallbacks = [regex]::Matches($finalContent,
        '(?m)^APP_MODEL_FACTORY_FALLBACKS=(\d+)$')
    $appModelCompatCalls = [regex]::Matches($finalContent,
        '(?m)^APP_MODEL_COMPAT_FACADE_CALLS=(\d+)$')
    $appModelCompatTranslations = [regex]::Matches($finalContent,
        '(?m)^APP_MODEL_COMPAT_MODERN_TRANSLATIONS=(\d+)$')
    $appModelCompatLegacy = [regex]::Matches($finalContent,
        '(?m)^APP_MODEL_COMPAT_LEGACY_BACKEND_CALLS=(\d+)$')
    $appModelCompatFailures = [regex]::Matches($finalContent,
        '(?m)^APP_MODEL_COMPAT_FAILURES=(\d+)$')
    $appModelCompatSelfTest = [regex]::Matches($finalContent,
        '(?m)^APP_MODEL_COMPAT_SELFTEST_OK=1$').Count
    $appModelServiceSelfTest = [regex]::Matches($finalContent,
        '(?m)^APP_MODEL_SERVICES_SELFTEST_OK=1$').Count
    $appModelPass =
        $status -in @('APP_MODEL_COMPLETE', 'DIAGNOSTIC_COMPLETE') -and
        $appModelDescriptors.Count -gt 0 -and
        [int]$appModelDescriptors[$appModelDescriptors.Count - 1].Groups[1].Value -eq 12 -and
        $appModelFactories.Count -gt 0 -and
        [int]$appModelFactories[$appModelFactories.Count - 1].Groups[1].Value -eq 12 -and
        $appModelFallbacks.Count -gt 0 -and
        [int]$appModelFallbacks[$appModelFallbacks.Count - 1].Groups[1].Value -eq 0 -and
        $appModelCompatCalls.Count -gt 0 -and
        $appModelCompatTranslations.Count -gt 0 -and
        $appModelCompatFailures.Count -gt 0 -and
        [int]$appModelCompatCalls[$appModelCompatCalls.Count - 1].Groups[1].Value -
            [int]$appModelCompatFailures[$appModelCompatFailures.Count - 1].Groups[1].Value -eq
            [int]$appModelCompatTranslations[$appModelCompatTranslations.Count - 1].Groups[1].Value -and
        $appModelCompatLegacy.Count -gt 0 -and
        [int]$appModelCompatLegacy[$appModelCompatLegacy.Count - 1].Groups[1].Value -eq 0 -and
        [int]$appModelCompatFailures[$appModelCompatFailures.Count - 1].Groups[1].Value -eq 1 -and
        $appModelCompatSelfTest -ge 1 -and
        $appModelServiceSelfTest -ge 1
    $appModelValidation = [ordered]@{
        pass = $appModelPass
        descriptors = if ($appModelDescriptors.Count -gt 0) { [int]$appModelDescriptors[$appModelDescriptors.Count - 1].Groups[1].Value } else { 0 }
        factoryRegistrations = if ($appModelFactories.Count -gt 0) { [int]$appModelFactories[$appModelFactories.Count - 1].Groups[1].Value } else { 0 }
        factoryFallbacks = if ($appModelFallbacks.Count -gt 0) { [int]$appModelFallbacks[$appModelFallbacks.Count - 1].Groups[1].Value } else { 0 }
        compatibilityFacadeCalls = if ($appModelCompatCalls.Count -gt 0) { [int]$appModelCompatCalls[$appModelCompatCalls.Count - 1].Groups[1].Value } else { 0 }
        compatibilityTranslations = if ($appModelCompatTranslations.Count -gt 0) { [int]$appModelCompatTranslations[$appModelCompatTranslations.Count - 1].Groups[1].Value } else { 0 }
        compatibilityLegacyBackendCalls = if ($appModelCompatLegacy.Count -gt 0) { [int]$appModelCompatLegacy[$appModelCompatLegacy.Count - 1].Groups[1].Value } else { 0 }
        compatibilityFailures = if ($appModelCompatFailures.Count -gt 0) { [int]$appModelCompatFailures[$appModelCompatFailures.Count - 1].Groups[1].Value } else { 0 }
        serviceSelfTest = $appModelServiceSelfTest
    }
    if ($status -in @('APP_MODEL_COMPLETE', 'DIAGNOSTIC_COMPLETE') -and -not $appModelPass) {
        $status = 'APP_MODEL_VALIDATION_FAILED'
    }
}
if ($isAppRuntimeValidation) {
    # Phase 6 runs a bounded real-factory lifecycle proof before the host
    # workload.  Scope workload counters after the model-ready boundary so
    # that its intentionally successful launches do not look like duplicate
    # user launches (notably Console reuse).
    $runtimeContent = $finalContent
    $runtimeReadyMarkers = [regex]::Matches($finalContent,
        '(?m)^APP_RUNTIME_MODEL_READY=.*$')
    if ($runtimeReadyMarkers.Count -gt 0) {
        $runtimeReady = $runtimeReadyMarkers[$runtimeReadyMarkers.Count - 1]
        $runtimeContent = $finalContent.Substring(
            $runtimeReady.Index + $runtimeReady.Length)
    }
    $runtimeApps = @(
        'Calculator', 'Computer Files', 'Console', 'Devices', 'Disk Manager',
        'Display Options', 'Firewall', 'Image Viewer', 'Notepad', 'Paint',
        'Task Manager', 'WAV Player'
    )
    $runtimeSelects = 0
    $runtimeLaunches = 0
    foreach ($runtimeApp in $runtimeApps) {
        $escapedRuntimeApp = [regex]::Escape($runtimeApp)
        $runtimeSelects += [regex]::Matches($runtimeContent,
            '(?m)^APP_RUNTIME_START_SELECT=name=' + $escapedRuntimeApp + ';').Count
        $runtimeLaunches += [regex]::Matches($runtimeContent,
            '(?m)^APP_RUNTIME_LAUNCH_OK=.*;name=' + $escapedRuntimeApp + ';').Count
    }
    $runtimeFactoryLaunches = [regex]::Matches($runtimeContent,
        '(?m)^APP_RUNTIME_FACTORY_LAUNCH=app=').Count
    $runtimeFactoryFallbacks = [regex]::Matches($runtimeContent,
        '(?m)^APP_RUNTIME_LAUNCH_BACKEND=compatibility;app=').Count
    $runtimeFactoryFailures = [regex]::Matches($runtimeContent,
        '(?m)^APP_RUNTIME_FACTORY_FAILURE=app=').Count
    $runtimeFactoryReuses = [regex]::Matches($runtimeContent,
        '(?m)^APP_RUNTIME_FACTORY_LAUNCH=app=.*;reused=1;').Count
    $runtimeFactoryWindows = [regex]::Matches($runtimeContent,
        '(?m)^APP_RUNTIME_FACTORY_LAUNCH=app=.*;windows=[1-8]$').Count
    $runtimeTypedExternal = [regex]::Matches($runtimeContent,
        '(?m)^APP_RUNTIME_LAUNCH_BACKEND=typed-external;backend=').Count
    $runtimeTypedShellActions = [regex]::Matches($runtimeContent,
        '(?m)^APP_RUNTIME_LAUNCH_BACKEND=typed-shell-action;action=').Count
    $runtimeInstanceLaunches = [regex]::Matches($runtimeContent,
        '(?m)^APP_RUNTIME_LAUNCH_OK=.*;instance=instance-[^;]+;state=(Loading|Activated);owned=\d+$').Count
    $runtimeInstanceResults = [regex]::Matches($runtimeContent,
        '(?m)^APP_RUNTIME_LAUNCH_RESULT=code=Success;success=1;app=.*;instance=instance-[^;]+;state=Activated;active=\d+;').Count
    $runtimeOwnedLaunches = [regex]::Matches($runtimeContent,
        '(?m)^APP_RUNTIME_LAUNCH_OK=.*;instance=instance-[^;]+;state=(Loading|Activated);owned=[1-8]$').Count
    $runtimeStaleOwnership = [regex]::Matches($runtimeContent,
        '(?m)^APP_RUNTIME_WINDOW_CLOSED=.*;stale=[1-9]\d*$').Count
    $consoleInstanceMatches = [regex]::Matches($runtimeContent,
        '(?m)^APP_RUNTIME_LAUNCH_OK=.*;name=Console;.*;instance=(instance-[^;]+);')
    $consoleInstanceIds = @($consoleInstanceMatches | ForEach-Object {
        $_.Groups[1].Value
    } | Select-Object -Unique)
    $runtimeConsoleInstances = $consoleInstanceIds.Count
    $runtimeConsoleLaunches = $consoleInstanceMatches.Count
    $runtimeCounterMatches = [regex]::Matches($runtimeContent,
        '(?m)^APP_RUNTIME_LAUNCH_RESULT=.*;created=(\d+);reused=(\d+);terminated=(\d+);attach=(\d+);detach=(\d+);stale=(\d+)$')
    $runtimeMaxReused = 0
    $runtimeMaxTerminated = 0
    $runtimeMaxAttach = 0
    $runtimeMaxDetach = 0
    if ($runtimeCounterMatches.Count -gt 0) {
        foreach ($counter in $runtimeCounterMatches) {
            $runtimeMaxReused = [Math]::Max($runtimeMaxReused,
                [int]$counter.Groups[2].Value)
            $runtimeMaxTerminated = [Math]::Max($runtimeMaxTerminated,
                [int]$counter.Groups[3].Value)
            $runtimeMaxAttach = [Math]::Max($runtimeMaxAttach,
                [int]$counter.Groups[4].Value)
            $runtimeMaxDetach = [Math]::Max($runtimeMaxDetach,
                [int]$counter.Groups[5].Value)
        }
    }
    $runtimeCloses = [regex]::Matches($runtimeContent,
        '(?m)^APP_RUNTIME_WINDOW_CLOSED=').Count
    $runtimeAssoc = [regex]::Matches($runtimeContent,
        '(?m)^APP_RUNTIME_ASSOC_RESOLVE=.*;success=1').Count
    $runtimeTxt = [regex]::Matches($runtimeContent,
        '(?m)^APP_RUNTIME_FILE_OK=path=Scripts/notepad\.gxm\.txt;app=Notepad;').Count
    $runtimePng = [regex]::Matches($runtimeContent,
        '(?m)^APP_RUNTIME_FILE_OK=path=Images/audiopause\.png;app=Image Viewer;').Count
    $runtimeGxm = [regex]::Matches($runtimeContent,
        '(?m)^APP_RUNTIME_FILE_RESULT=path=Programs/calculator\.gxm;app=GXM;').Count
    $runtimeGxmInstances = [regex]::Matches($runtimeContent,
        '(?m)^APP_RUNTIME_FILE_RESULT=path=Programs/calculator\.gxm;app=GXM;.*;instance=instance-[^;]+;state=Activated;owned=[1-8]$').Count
    $runtimeGxmWindows = [regex]::Matches($runtimeContent,
        '(?m)^APP_RUNTIME_GXM_INSTANCE_WINDOW_BOUNDS=.*;instance=instance-[^;]+$').Count
    $runtimeBmp = [regex]::Matches($runtimeContent,
        '(?m)^APP_RUNTIME_ASSOC_RESOLVE=name=missing\.bmp;ext=\.bmp;.*;success=1$').Count
    $runtimeWav = [regex]::Matches($runtimeContent,
        '(?m)^APP_RUNTIME_ASSOC_RESOLVE=name=missing\.wav;ext=\.wav;.*;success=1$').Count
    $runtimeMue = [regex]::Matches($runtimeContent,
        '(?m)^APP_RUNTIME_ASSOC_RESOLVE=name=missing\.mue;ext=\.mue;.*;success=1$').Count
    $runtimeFileAssocMarker = [regex]::Matches($runtimeContent,
        '(?m)^APP_RUNTIME_NEGATIVE_FILE_ASSOCIATIONS=5$').Count
    $runtimeFileFails = [regex]::Matches($runtimeContent,
        '(?m)^APP_RUNTIME_FILE_FAIL=').Count
    $runtimeNegativePass = [regex]::Matches($finalContent,
        '(?m)^APP_RUNTIME_NEGATIVE_[A-Z_]+=PASS').Count
    $runtimeShellComputer = [regex]::Matches($runtimeContent,
        '(?m)^APP_RUNTIME_SHELL_ROUTE=COMPUTER_FILES;result=WINDOW$').Count
    $runtimeShellRoot = [regex]::Matches($runtimeContent,
        '(?m)^APP_RUNTIME_SHELL_ROUTE=ROOT;result=COMPUTER_FILES_ROOT$').Count
    $runtimeInstaller = [regex]::Matches($runtimeContent,
        '(?m)^APP_RUNTIME_SHELL_ROUTE=INSTALLER;result=HD_INSTALLER$').Count
    $runtimeInstallerClosed = [regex]::Matches($runtimeContent,
        '(?m)^APP_RUNTIME_WINDOW_CLOSED=title=Install guideXOS to Hard Drive;').Count
    $runtimeUsbUnavailable = [regex]::Matches($runtimeContent,
        '(?m)^APP_RUNTIME_SHELL_ROUTE=USB;result=UNAVAILABLE$').Count
    $runtimeLastClose = [regex]::Matches($runtimeContent,
        '(?m)^APP_RUNTIME_WINDOW_CLOSED=.*;memory=(\d+);corrupt=(\d+)')
    $runtimeLaunchFail = [regex]::Matches($finalContent,
        '(?m)^APP_RUNTIME_LAUNCH_FAIL=').Count
    $runtimeFaults = [regex]::Matches($finalContent,
        '(?m)^APP_RUNTIME_FAULT=').Count
    $runtimeThreadPoolUnlocked = [regex]::Matches($finalContent,
        '(?m)^CONTINUOUS_HEARTBEAT_THREADPOOL_LOCKED=0$').Count -gt 0
    $runtimeBalancedInput =
        $inputStat.KEY_DROPPED -eq 0 -and
        $inputStat.MOUSE_DROPPED -eq 0 -and
        $inputStat.KEY_DOWN -eq $inputStat.KEY_UP -and
        $inputStat.MOUSE_LEFT_DOWN -eq $inputStat.MOUSE_LEFT_UP
    # Reusable built-ins intentionally remain active across the workload; the
    # completed sequence therefore terminates fourteen fresh instances.
    $runtimePass =
        $status -eq 'APP_RUNTIME_COMPLETE' -and
        $runtimeSelects -ge 15 -and $runtimeLaunches -ge 15 -and
        $runtimeInstanceLaunches -ge 15 -and $runtimeInstanceResults -ge 15 -and
        $runtimeOwnedLaunches -ge 15 -and
        $runtimeConsoleLaunches -ge 3 -and $runtimeConsoleInstances -eq 1 -and
        $runtimeFactoryLaunches -ge 20 -and
        $runtimeFactoryFallbacks -eq 0 -and
        $runtimeFactoryReuses -ge 1 -and
        $runtimeFactoryWindows -ge 12 -and
        $runtimeMaxReused -ge 1 -and $runtimeMaxAttach -ge 15 -and
        $runtimeMaxDetach -ge 15 -and $runtimeMaxTerminated -ge 14 -and
        $runtimeStaleOwnership -eq 0 -and
        $runtimeCloses -ge 15 -and $runtimeAssoc -ge 8 -and
        $runtimeTxt -ge 1 -and $runtimePng -ge 1 -and $runtimeGxm -ge 1 -and
        $runtimeGxmInstances -ge 1 -and $runtimeGxmWindows -ge 1 -and
        $runtimeBmp -ge 1 -and $runtimeWav -ge 1 -and $runtimeMue -ge 1 -and
        $runtimeFileFails -ge 5 -and $runtimeFileAssocMarker -ge 1 -and
        $runtimeNegativePass -ge 5 -and
        $runtimeShellComputer -ge 2 -and $runtimeShellRoot -ge 1 -and
        $runtimeInstaller -ge 1 -and
        $runtimeInstallerClosed -ge 1 -and
        $runtimeUsbUnavailable -ge 1 -and $runtimeLaunchFail -ge 1 -and
        $runtimeFaults -eq 0 -and $runtimeThreadPoolUnlocked -and
        $runtimeBalancedInput -and $graphicsValid -eq $true
    $runtimeMemory = if ($runtimeLastClose.Count -gt 0) {
        [UInt64]$runtimeLastClose[$runtimeLastClose.Count - 1].Groups[1].Value
    } else { 0 }
    $runtimeCorrupt = if ($runtimeLastClose.Count -gt 0) {
        [UInt64]$runtimeLastClose[$runtimeLastClose.Count - 1].Groups[2].Value
    } else { 0 }
    $runtimeValidation = [ordered]@{
        pass = $runtimePass
        startSelections = $runtimeSelects
        successfulLaunches = $runtimeLaunches
        factoryLaunches = $runtimeFactoryLaunches
        compatibilityFallbacks = $runtimeFactoryFallbacks
        factoryFailures = $runtimeFactoryFailures
        reusedFactoryActivations = $runtimeFactoryReuses
        factoryWindowLaunches = $runtimeFactoryWindows
        typedExternalLaunches = $runtimeTypedExternal
        typedShellActionLaunches = $runtimeTypedShellActions
        instanceLaunches = $runtimeInstanceLaunches
        instanceResults = $runtimeInstanceResults
        ownedLaunches = $runtimeOwnedLaunches
        consoleLaunches = $runtimeConsoleLaunches
        consoleInstances = $runtimeConsoleInstances
        maxReusedInstances = $runtimeMaxReused
        maxTerminatedInstances = $runtimeMaxTerminated
        maxWindowAttaches = $runtimeMaxAttach
        maxWindowDetaches = $runtimeMaxDetach
        staleOwnershipMarkers = $runtimeStaleOwnership
        closedWindows = $runtimeCloses
        associationResolutions = $runtimeAssoc
        textOpens = $runtimeTxt
        pngOpens = $runtimePng
        bmpDispatches = $runtimeBmp
        wavDispatches = $runtimeWav
        gxmResults = $runtimeGxm
        gxmInstanceResults = $runtimeGxmInstances
        gxmInstanceWindows = $runtimeGxmWindows
        mueDispatches = $runtimeMue
        fileFailures = $runtimeFileFails
        fileAssociationNegativeMarker = $runtimeFileAssocMarker
        negativePasses = $runtimeNegativePass
        computerFilesRoutes = $runtimeShellComputer
        rootRoutes = $runtimeShellRoot
        installerRoutes = $runtimeInstaller
        installerClosed = $runtimeInstallerClosed
        usbUnavailableRoutes = $runtimeUsbUnavailable
        launchFailures = $runtimeLaunchFail
        runtimeFaults = $runtimeFaults
        lastCloseMemory = $runtimeMemory
        lastCloseCorrupt = $runtimeCorrupt
        balancedInput = $runtimeBalancedInput
    }
    if ($status -eq 'APP_RUNTIME_COMPLETE' -and -not $runtimePass) {
        $status = 'APP_RUNTIME_VALIDATION_FAILED'
    }
}

$startMenuOpenedCount = [regex]::Matches(
    $finalContent, '(?m)^START_MENU_OPENED(?:=|$)').Count
if ($isStartMenuValidation -and $inputInjected -and $startMenuOpenedCount -lt 1) {
    $status = 'INPUT_VALIDATION_FAILED'
}

Write-Host ''
Write-Host '========================================' -ForegroundColor Cyan
Write-Host '   Validation Summary' -ForegroundColor Cyan
Write-Host '========================================' -ForegroundColor Cyan
Write-Host "Status: $status" -ForegroundColor $(if ($status -in @('TIMEOUT_SUCCESS', 'DIAGNOSTIC_COMPLETE', 'APP_MODEL_COMPLETE', 'CONTEXT_MENU_COMPLETE', 'APP_RUNTIME_COMPLETE', 'WIDGET_COMPLETE', 'WIDGET_STRESS_COMPLETE', 'WIDGET_SOAK_COMPLETE')) { 'Green' } else { 'Red' })
Write-Host "Dispatch selected: $dispatchSelected" -ForegroundColor Gray
Write-Host "Continuous entered: $continuousEntered" -ForegroundColor Gray
Write-Host "Heartbeats: $heartbeatCount (last frame $lastHeartbeatFrame)" -ForegroundColor Gray
Write-Host "Timer: $firstHeartbeatTimer -> $lastHeartbeatTimer" -ForegroundColor Gray
Write-Host "Stack top: $stackTop" -ForegroundColor Gray
Write-Host "Stack low-water: $lastStackLowWater" -ForegroundColor Gray
Write-Host "Graphics invariants: $(if ($null -eq $graphicsValid) { 'not sampled' } else { $graphicsValid })" -ForegroundColor Gray
if ($isInteractiveValidation) {
    Write-Host "Input injected: $inputInjected" -ForegroundColor Gray
    Write-Host "Keyboard IRQ/down/up/dropped: $($inputStat.KEY_IRQ)/$($inputStat.KEY_DOWN)/$($inputStat.KEY_UP)/$($inputStat.KEY_DROPPED)" -ForegroundColor Gray
    Write-Host "Mouse IRQ/packets/moves/dropped: $($inputStat.MOUSE_IRQ)/$($inputStat.MOUSE_PACKETS)/$($inputStat.MOUSE_MOVES)/$($inputStat.MOUSE_DROPPED)" -ForegroundColor Gray
    Write-Host "Mouse left down/up: $($inputStat.MOUSE_LEFT_DOWN)/$($inputStat.MOUSE_LEFT_UP)" -ForegroundColor Gray
    Write-Host "Mouse right down/up: $($inputStat.MOUSE_RIGHT_DOWN)/$($inputStat.MOUSE_RIGHT_UP)" -ForegroundColor Gray
    Write-Host "GUI key routed: $($finalContent -match 'INPUT_GUI_KEY_ROUTED')" -ForegroundColor Gray
    Write-Host "GUI mouse routed: $($finalContent -match 'INPUT_GUI_MOUSE_ROUTED')" -ForegroundColor Gray
    Write-Host "Start menu opened: $startMenuOpenedCount" -ForegroundColor Gray
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
if ($appModelValidation) {
    Write-Host "App Model validation: $($appModelValidation.pass)" -ForegroundColor $(if ($appModelValidation.pass) { 'Green' } else { 'Red' })
    Write-Host "Descriptors/factories/fallbacks: $($appModelValidation.descriptors)/$($appModelValidation.factoryRegistrations)/$($appModelValidation.factoryFallbacks)" -ForegroundColor Gray
    Write-Host "Compatibility calls/translations/legacy/failures: $($appModelValidation.compatibilityFacadeCalls)/$($appModelValidation.compatibilityTranslations)/$($appModelValidation.compatibilityLegacyBackendCalls)/$($appModelValidation.compatibilityFailures)" -ForegroundColor Gray
}
if ($widgetValidation) {
    Write-Host "Widget validation: $($widgetValidation.pass)" -ForegroundColor $(if ($widgetValidation.pass) { 'Green' } else { 'Red' })
    Write-Host "Init good/bad, draws good/bad: $($widgetValidation.initGoodBad), $($widgetValidation.drawsGoodBad)" -ForegroundColor Gray
    Write-Host "Updates: $($widgetValidation.updates)" -ForegroundColor Gray
    Write-Host "Widget menu opens/draws, bounds good/bad: $($widgetValidation.menuOpenedDrawn), $($widgetValidation.menuBoundsGoodBad)" -ForegroundColor Gray
    Write-Host "Widget menu hover, undock/dock, click-away/Escape: $($widgetValidation.menuHover), $($widgetValidation.undockDock), $($widgetValidation.clickAwayEscape)" -ForegroundColor Gray
    Write-Host "Desktop/taskbar popups, hover, right input: $($widgetValidation.desktopTaskbarPopup), $($widgetValidation.hover), $($widgetValidation.rightInput)" -ForegroundColor Gray
    Write-Host "Drag start/end, soak timer delta/wall seconds, runtime faults: $($widgetValidation.dragStartEnd), $($widgetValidation.soakTimerDelta)/$($widgetValidation.soakWallSeconds), $($widgetValidation.runtimeFaults)" -ForegroundColor Gray
}
if ($runtimeValidation) {
    Write-Host "App runtime validation: $($runtimeValidation.pass)" -ForegroundColor $(if ($runtimeValidation.pass) { 'Green' } else { 'Red' })
    Write-Host "Start selections/launches/closes: $($runtimeValidation.startSelections)/$($runtimeValidation.successfulLaunches)/$($runtimeValidation.closedWindows)" -ForegroundColor Gray
    Write-Host "Factory launches/fallbacks/failures/reuse/window launches: $($runtimeValidation.factoryLaunches)/$($runtimeValidation.compatibilityFallbacks)/$($runtimeValidation.factoryFailures)/$($runtimeValidation.reusedFactoryActivations)/$($runtimeValidation.factoryWindowLaunches)" -ForegroundColor Gray
    Write-Host "Typed external/shell-action launches: $($runtimeValidation.typedExternalLaunches)/$($runtimeValidation.typedShellActionLaunches)" -ForegroundColor Gray
    Write-Host "Associations txt/png/bmp/wav/gxm/mue, failures: $($runtimeValidation.associationResolutions), $($runtimeValidation.textOpens)/$($runtimeValidation.pngOpens)/$($runtimeValidation.bmpDispatches)/$($runtimeValidation.wavDispatches)/$($runtimeValidation.gxmResults)/$($runtimeValidation.mueDispatches), $($runtimeValidation.fileFailures)" -ForegroundColor Gray
    Write-Host "Shell Computer Files/Root/Installer/InstallerClosed/USB unavailable: $($runtimeValidation.computerFilesRoutes)/$($runtimeValidation.rootRoutes)/$($runtimeValidation.installerRoutes)/$($runtimeValidation.installerClosed)/$($runtimeValidation.usbUnavailableRoutes)" -ForegroundColor Gray
    Write-Host "Negative passes/launch failures/runtime faults: $($runtimeValidation.negativePasses)/$($runtimeValidation.launchFailures)/$($runtimeValidation.runtimeFaults)" -ForegroundColor Gray
    Write-Host "Last close memory/corruption, balanced input: $($runtimeValidation.lastCloseMemory)/$($runtimeValidation.lastCloseCorrupt), $($runtimeValidation.balancedInput)" -ForegroundColor Gray
}
if ($faultText) {
    Write-Host "Fault: $faultText" -ForegroundColor Red
}
Write-Host "Serial log: $serialPath" -ForegroundColor Cyan

if ($status -in @('FAULT', 'QEMU_EXITED', 'TIMEOUT_NO_PROGRESS', 'TIMEOUT_NO_INPUT', 'INPUT_INJECTION_FAILED', 'INPUT_VALIDATION_FAILED', 'APP_MODEL_VALIDATION_FAILED', 'CONTEXT_MENU_VALIDATION_FAILED', 'APP_RUNTIME_VALIDATION_FAILED', 'WIDGET_VALIDATION_FAILED')) {
    exit 1
}
exit 0
