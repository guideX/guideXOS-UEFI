param(
    [ValidateSet('Default', 'SafeNormalDesktop', 'NormalDesktopFirstFrame', 'MultiFrameNormalDesktop', 'AbiTaskbar', 'AbiCursor')]
    [string]$Mode = 'Default',
    [ValidateSet(120, 300)]
    [int]$FrameTarget = 120,
    [switch]$CaptureScreenshot,
    [switch]$GuiVisible,
    [int]$CaptureSeconds = 120,
    [switch]$SafeCursorImageFallback,
    [string]$ScreenshotPath
)

$ErrorActionPreference = 'Stop'

function Get-FileSnapshot {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    $item = Get-Item -LiteralPath $Path
    [pscustomobject]@{
        Path = $item.FullName
        Length = $item.Length
        LastWriteTime = $item.LastWriteTime
        Sha256 = (Get-FileHash -LiteralPath $item.FullName -Algorithm SHA256).Hash
    }
}

function Assert-SingleReplacement {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Text,

        [Parameter(Mandatory = $true)]
        [string]$Old,

        [Parameter(Mandatory = $true)]
        [string]$New,

        [Parameter(Mandatory = $true)]
        [string]$Label
    )

    $count = [regex]::Matches($Text, [regex]::Escape($Old)).Count
    if ($count -ne 1) {
        throw "Expected exactly one $Label replacement, found $count."
    }

    return $Text.Replace($Old, $New)
}

function Insert-AfterAnchor {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Text,

        [Parameter(Mandatory = $true)]
        [string]$Anchor,

        [Parameter(Mandatory = $true)]
        [string]$Insertion,

        [Parameter(Mandatory = $true)]
        [string]$Label
    )

    $count = [regex]::Matches($Text, [regex]::Escape($Anchor)).Count
    if ($count -ne 1) {
        throw "Expected exactly one $Label anchor, found $count."
    }

    return $Text.Replace($Anchor, $Anchor + "`r`n        " + $Insertion)
}

function Get-LastMatchingLine {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Text,

        [Parameter(Mandatory = $true)]
        [string]$Pattern
    )

    $escapedPattern = [regex]::Escape($Pattern)
    $matches = [regex]::Matches($Text, "(?m)^.*$escapedPattern.*$")
    if ($matches.Count -gt 0) {
        return $matches[$matches.Count - 1].Value
    }

    return $null
}

function Read-SharedText {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

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

function Ensure-NativeScreenshotHelpers {
    if (-not ("UefiRunNativeMethods" -as [type])) {
        Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class UefiRunNativeMethods
{
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);

    [DllImport("user32.dll")]
    public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder text, int count);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowTextLength(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    public static IntPtr FindWindowHandleByTitleSubstring(string[] titleHints)
    {
        IntPtr found = IntPtr.Zero;
        EnumWindows((hWnd, lParam) =>
        {
            if (!IsWindowVisible(hWnd))
            {
                return true;
            }

            int length = GetWindowTextLength(hWnd);
            if (length <= 0)
            {
                return true;
            }

            var builder = new System.Text.StringBuilder(length + 1);
            GetWindowText(hWnd, builder, builder.Capacity);
            string title = builder.ToString();
            if (string.IsNullOrWhiteSpace(title))
            {
                return true;
            }

            if (titleHints != null)
            {
                foreach (string hint in titleHints)
                {
                    if (!string.IsNullOrWhiteSpace(hint) && title.IndexOf(hint, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        found = hWnd;
                        return false;
                    }
                }
            }

            return true;
        }, IntPtr.Zero);

        return found;
    }
}
"@
    }
}

function Get-QemuWindowHandle {
    param(
        [Parameter(Mandatory = $true)]
        [System.Diagnostics.Process]$Process,

        [int]$TimeoutSeconds = 20
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        try {
            $Process.Refresh()
            if ($Process.MainWindowHandle -ne 0) {
                return [IntPtr]$Process.MainWindowHandle
            }
        } catch {
        }

        Start-Sleep -Milliseconds 250
    }

    try {
        Ensure-NativeScreenshotHelpers
        $fallbackHandle = [UefiRunNativeMethods]::FindWindowHandleByTitleSubstring(@('guideXOS', 'QEMU', 'OVMF'))
        if ($fallbackHandle -ne [IntPtr]::Zero) {
            return $fallbackHandle
        }
    } catch {
    }

    return [IntPtr]::Zero
}

function Get-FreeTcpPort {
    $listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 0)
    try {
        $listener.Start()
        return ([System.Net.IPEndPoint]$listener.LocalEndpoint).Port
    } finally {
        $listener.Stop()
    }
}

function Save-QmpScreenshot {
    param(
        [Parameter(Mandatory = $true)]
        [int]$Port,

        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    $client = [System.Net.Sockets.TcpClient]::new()
    try {
        $client.Connect([System.Net.IPAddress]::Loopback, $Port)
        $stream = $client.GetStream()
        $reader = [System.IO.StreamReader]::new($stream, [System.Text.Encoding]::ASCII)
        $writer = [System.IO.StreamWriter]::new($stream, [System.Text.Encoding]::ASCII)
        $writer.AutoFlush = $true
        $writer.NewLine = "`n"

        [void]$reader.ReadLine()
        $writer.WriteLine('{"execute":"qmp_capabilities"}')
        [void]$reader.ReadLine()

        $qmpPath = $Path -replace '\\', '/'
        $command = '{"execute":"screendump","arguments":{"filename":"' + $qmpPath + '","format":"png"}}'
        $writer.WriteLine($command)
        $response = $reader.ReadLine()
        if (-not $response -or $response -notmatch '"return"') {
            throw "QMP screendump did not return success. Response: $response"
        }
    } finally {
        $client.Close()
    }
}

function Save-WindowScreenshot {
    param(
        [Parameter(Mandatory = $true)]
        [IntPtr]$WindowHandle,

        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    Ensure-NativeScreenshotHelpers

    if ($WindowHandle -eq [IntPtr]::Zero) {
        throw "Cannot capture screenshot because the window handle is zero."
    }

    $rect = New-Object UefiRunNativeMethods+RECT
    if (-not [UefiRunNativeMethods]::GetWindowRect($WindowHandle, [ref]$rect)) {
        throw "GetWindowRect failed for QEMU window."
    }

    $width = $rect.Right - $rect.Left
    $height = $rect.Bottom - $rect.Top
    if ($width -le 0 -or $height -le 0) {
        throw "Invalid QEMU window size: ${width}x${height}."
    }

    [UefiRunNativeMethods]::ShowWindow($WindowHandle, 9) | Out-Null
    [UefiRunNativeMethods]::SetForegroundWindow($WindowHandle) | Out-Null
    Start-Sleep -Milliseconds 500

    $bitmap = New-Object System.Drawing.Bitmap($width, $height)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    try {
        $graphics.CopyFromScreen($rect.Left, $rect.Top, 0, 0, $bitmap.Size)
        $bitmap.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
    } finally {
        $graphics.Dispose()
        $bitmap.Dispose()
    }
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
$runStamp = Get-Date -Format 'yyyyMMdd_HHmmss_fff'
$runId = "UEFI_RUN_ID_${runStamp}_PID$PID"
$modeLabel = if ($Mode -eq 'SafeNormalDesktop') {
    'SAFE_NORMAL_DESKTOP_UEFI'
} elseif ($Mode -eq 'NormalDesktopFirstFrame') {
    'NORMAL_DESKTOP_UEFI_FIRST_FRAME'
} elseif ($Mode -eq 'AbiTaskbar') {
    'UEFI_ABI_PROBE_TASKBAR'
} elseif ($Mode -eq 'AbiCursor') {
    'UEFI_ABI_PROBE_CURSOR'
} elseif ($Mode -eq 'MultiFrameNormalDesktop') {
    'MULTIFRAME_NORMAL_DESKTOP_UEFI'
} else {
    'TINY_UEFI'
}
$serialLog = Join-Path $logRoot "serial_$runId.txt"
$stderrLog = Join-Path $logRoot "qemu_stderr_$runId.txt"
$summaryLog = Join-Path $logRoot "summary_$runId.txt"
if (-not $ScreenshotPath) {
    $ScreenshotPath = Join-Path $logRoot "qmp_screendump_$runId.png"
}
$qmpPort = $null
if ($CaptureScreenshot) {
    $qmpPort = Get-FreeTcpPort
}

New-Item -ItemType Directory -Path $logRoot -Force | Out-Null
New-Item -ItemType Directory -Path $qemuFirmwareRoot -Force | Out-Null

if (-not (Test-Path $qemuExe)) {
    throw "QEMU executable not found: $qemuExe"
}

$originalProgram = [System.IO.File]::ReadAllText($programPath)
$programPatched = $false
$qemuProcess = $null

try {
    $patched = $originalProgram
    $patched = Insert-AfterAnchor -Text $patched `
        -Anchor 'SerialBreadcrumb("SMAIN_BEFORE_RENDERLOOP_DISPATCH");' `
        -Insertion "SerialBreadcrumb(`"$runId`");" `
        -Label 'SMAIN_BEFORE_RENDERLOOP_DISPATCH'

    if ($Mode -eq 'SafeNormalDesktop') {
        $patched = Assert-SingleReplacement -Text $patched `
            -Old 'private const bool UEFI_ALLOW_NORMAL_DESKTOP_RENDER_PATH = false;' `
            -New 'private const bool UEFI_ALLOW_NORMAL_DESKTOP_RENDER_PATH = true;' `
            -Label 'UEFI_ALLOW_NORMAL_DESKTOP_RENDER_PATH'
        $patched = Assert-SingleReplacement -Text $patched `
            -Old 'private const bool UEFI_USE_TINY_RENDER_LOOP_BYPASS = true;' `
            -New 'private const bool UEFI_USE_TINY_RENDER_LOOP_BYPASS = false;' `
            -Label 'UEFI_USE_TINY_RENDER_LOOP_BYPASS'
        $patched = Assert-SingleReplacement -Text $patched `
            -Old 'private const bool UEFI_ENABLE_SAFE_NORMAL_DESKTOP_FIRST_FRAME = false;' `
            -New 'private const bool UEFI_ENABLE_SAFE_NORMAL_DESKTOP_FIRST_FRAME = true;' `
            -Label 'UEFI_ENABLE_SAFE_NORMAL_DESKTOP_FIRST_FRAME'
    } elseif ($Mode -eq 'NormalDesktopFirstFrame') {
        $patched = Assert-SingleReplacement -Text $patched `
            -Old 'private const bool UEFI_ALLOW_NORMAL_DESKTOP_RENDER_PATH = false;' `
            -New 'private const bool UEFI_ALLOW_NORMAL_DESKTOP_RENDER_PATH = true;' `
            -Label 'UEFI_ALLOW_NORMAL_DESKTOP_RENDER_PATH'
        $patched = Assert-SingleReplacement -Text $patched `
            -Old 'private const bool UEFI_USE_TINY_RENDER_LOOP_BYPASS = true;' `
            -New 'private const bool UEFI_USE_TINY_RENDER_LOOP_BYPASS = false;' `
            -Label 'UEFI_USE_TINY_RENDER_LOOP_BYPASS'
        $patched = Assert-SingleReplacement -Text $patched `
            -Old 'private const bool UEFI_ENABLE_NORMAL_DESKTOP_FIRST_FRAME_PROBE = false;' `
            -New 'private const bool UEFI_ENABLE_NORMAL_DESKTOP_FIRST_FRAME_PROBE = true;' `
            -Label 'UEFI_ENABLE_NORMAL_DESKTOP_FIRST_FRAME_PROBE'
    } elseif ($Mode -eq 'MultiFrameNormalDesktop') {
        $patched = Assert-SingleReplacement -Text $patched `
            -Old 'private const bool UEFI_ALLOW_NORMAL_DESKTOP_RENDER_PATH = false;' `
            -New 'private const bool UEFI_ALLOW_NORMAL_DESKTOP_RENDER_PATH = true;' `
            -Label 'UEFI_ALLOW_NORMAL_DESKTOP_RENDER_PATH'
        $patched = Assert-SingleReplacement -Text $patched `
            -Old 'private const bool UEFI_USE_TINY_RENDER_LOOP_BYPASS = true;' `
            -New 'private const bool UEFI_USE_TINY_RENDER_LOOP_BYPASS = false;' `
            -Label 'UEFI_USE_TINY_RENDER_LOOP_BYPASS'
        $patched = Assert-SingleReplacement -Text $patched `
            -Old 'private const bool UEFI_ENABLE_NORMAL_DESKTOP_BOUNDED = false;' `
            -New 'private const bool UEFI_ENABLE_NORMAL_DESKTOP_BOUNDED = true;' `
            -Label 'UEFI_ENABLE_NORMAL_DESKTOP_BOUNDED'
        $patched = Assert-SingleReplacement -Text $patched `
            -Old 'private const int UEFI_NORMAL_DESKTOP_BOUNDED_FRAME_TARGET = 120;' `
            -New "private const int UEFI_NORMAL_DESKTOP_BOUNDED_FRAME_TARGET = $FrameTarget;" `
            -Label 'UEFI_NORMAL_DESKTOP_BOUNDED_FRAME_TARGET'
    } elseif ($Mode -eq 'AbiTaskbar') {
        $patched = Assert-SingleReplacement -Text $patched `
            -Old 'private const int UEFI_ABI_PROBE_TARGET = 0;' `
            -New 'private const int UEFI_ABI_PROBE_TARGET = 1;' `
            -Label 'UEFI_ABI_PROBE_TARGET_TASKBAR'
    } elseif ($Mode -eq 'AbiCursor') {
        $patched = Assert-SingleReplacement -Text $patched `
            -Old 'private const int UEFI_ABI_PROBE_TARGET = 0;' `
            -New 'private const int UEFI_ABI_PROBE_TARGET = 2;' `
            -Label 'UEFI_ABI_PROBE_TARGET_CURSOR'
    }
    $safeCursorImageFallbackValue = if ($SafeCursorImageFallback) { 'true' } else { 'false' }
    $patched = Assert-SingleReplacement -Text $patched `
        -Old 'private const bool UEFI_SAFE_CURSOR_IMAGE_FALLBACK = false;' `
        -New "private const bool UEFI_SAFE_CURSOR_IMAGE_FALLBACK = $safeCursorImageFallbackValue;" `
        -Label 'UEFI_SAFE_CURSOR_IMAGE_FALLBACK'

    [System.IO.File]::WriteAllText($programPath, $patched)
    $programPatched = $true

    Write-Host "[uefi-run] Run ID: $runId" -ForegroundColor Cyan
    Write-Host "[uefi-run] Mode: $Mode" -ForegroundColor Cyan
    Write-Host "[uefi-run] Frame target: $FrameTarget" -ForegroundColor Cyan
    Write-Host "[uefi-run] Safe cursor image fallback: $SafeCursorImageFallback" -ForegroundColor Cyan
    Write-Host "[uefi-run] Expected dispatch reason: $modeLabel" -ForegroundColor Cyan
    Write-Host "[uefi-run] Screenshot capture requested: $CaptureScreenshot" -ForegroundColor Cyan
    Write-Host "[uefi-run] Screenshot path: $ScreenshotPath" -ForegroundColor Cyan
    Write-Host "[uefi-run] Building via build.ps1..." -ForegroundColor Cyan

    & powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File $buildScript
    if ($LASTEXITCODE -ne 0) {
        throw "build.ps1 failed with exit code $LASTEXITCODE"
    }

    Write-Host "[uefi-run] Build complete. Kernel snapshots:" -ForegroundColor Cyan
    $kernelRoot = Get-FileSnapshot -Path (Join-Path $root 'kernel.elf')
    $kernelEsp = Get-FileSnapshot -Path (Join-Path $root 'ESP\kernel.elf')
    foreach ($snap in @($kernelRoot, $kernelEsp)) {
        Write-Host ("[uefi-run]   {0}" -f $snap.Path) -ForegroundColor Gray
        Write-Host ("[uefi-run]     mtime:  {0}" -f $snap.LastWriteTime.ToString('o')) -ForegroundColor Gray
        Write-Host ("[uefi-run]     sha256: {0}" -f $snap.Sha256) -ForegroundColor Gray
        Write-Host ("[uefi-run]     bytes:  {0}" -f $snap.Length) -ForegroundColor Gray
    }
    if ($kernelRoot.Sha256 -ne $kernelEsp.Sha256) {
        throw "kernel.elf and ESP\kernel.elf hashes do not match."
    }

    if (Test-Path $serialLog) {
        Remove-Item -LiteralPath $serialLog -Force
    }
    if (Test-Path $stderrLog) {
        Remove-Item -LiteralPath $stderrLog -Force
    }
    if (Test-Path $ScreenshotPath) {
        Remove-Item -LiteralPath $ScreenshotPath -Force
    }

    Copy-Item -LiteralPath $qemuFirmwareCodeSource -Destination $qemuFirmwareCode -Force
    Copy-Item -LiteralPath $qemuFirmwareVarsSource -Destination $qemuFirmwareVars -Force

    Remove-Item Env:PATH -ErrorAction SilentlyContinue

    $serialRelative = $serialLog.Substring($root.Length).TrimStart('\', '/') -replace '\\', '/'
    $qemuArgs = @(
        '-machine', 'pc-q35-8.2'
        '-drive', ('if=pflash,format=raw,readonly=on,file=' + $qemuFirmwareCode)
        '-drive', ('if=pflash,format=raw,file=' + $qemuFirmwareVars)
        '-drive', 'if=none,id=esp,format=raw,file=fat:rw:ESP'
        '-device', 'ide-hd,drive=esp'
        '-m', '1024M'
        '-serial', ('file:' + $serialRelative)
        '-no-reboot'
        '-boot', 'menu=off,splash-time=0'
        '-name', 'guideXOS'
    )
    if ($qmpPort) {
        $qemuArgs += @('-qmp', "tcp:127.0.0.1:$qmpPort,server,nowait")
    }

    $windowStyle = if ($GuiVisible) { 'Normal' } else { 'Hidden' }
    Write-Host "[uefi-run] Launching QEMU via pflash..." -ForegroundColor Cyan
    Write-Host "[uefi-run] Serial log: $serialLog" -ForegroundColor Cyan

    $qemuProcess = Start-Process -FilePath $qemuExe `
        -ArgumentList $qemuArgs `
        -WorkingDirectory $root `
        -WindowStyle $windowStyle `
        -PassThru `
        -RedirectStandardError $stderrLog

    $deadline = (Get-Date).AddSeconds($CaptureSeconds)
    while ((Get-Date) -lt $deadline) {
        if ($qemuProcess.HasExited) {
            break
        }

        if (Test-Path -LiteralPath $serialLog) {
            try {
                $liveSerial = Read-SharedText -Path $serialLog
                if ($liveSerial.Contains('MULTIFRAME_HALT_ENTER') -or
                    $liveSerial.Contains('NORMAL_FRAME_HALT_ENTER') -or
                    $liveSerial.Contains('SAFE_NORMAL_DESKTOP_LOOP_ENTER')) {
                    break
                }
            } catch {
                # The serial writer can briefly hold the file exclusively;
                # leave the next poll to retry without affecting QEMU.
            }
        }

        Start-Sleep -Milliseconds 500
    }

    $screenshotCaptured = $false
    if ($CaptureScreenshot -and $qemuProcess -and -not $qemuProcess.HasExited) {
        try {
            if ($qmpPort) {
                Save-QmpScreenshot -Port $qmpPort -Path $ScreenshotPath
                $screenshotCaptured = $true
                Write-Host "[uefi-run] QMP screenshot captured: $ScreenshotPath" -ForegroundColor Cyan
            } elseif ($GuiVisible) {
                $qemuWindowHandle = Get-QemuWindowHandle -Process $qemuProcess
                if ($qemuWindowHandle -ne [IntPtr]::Zero) {
                    Save-WindowScreenshot -WindowHandle $qemuWindowHandle -Path $ScreenshotPath
                    $screenshotCaptured = $true
                    Write-Host "[uefi-run] Window screenshot captured: $ScreenshotPath" -ForegroundColor Cyan
                } else {
                    Write-Host "[uefi-run] Screenshot skipped: QEMU window handle was not available." -ForegroundColor Yellow
                }
            } else {
                Write-Host "[uefi-run] Screenshot skipped: no QMP port and GUI is hidden." -ForegroundColor Yellow
            }
        } catch {
            Write-Host "[uefi-run] Screenshot capture failed: $_" -ForegroundColor Yellow
        }
    }

    if ($qemuProcess -and -not $qemuProcess.HasExited) {
        Stop-Process -Id $qemuProcess.Id -Force -ErrorAction SilentlyContinue
        $qemuProcess.WaitForExit()
    }

    if (-not (Test-Path $serialLog)) {
        throw "QEMU did not create the expected serial log: $serialLog"
    }

    $serialText = [System.IO.File]::ReadAllText($serialLog)
    $serialLines = $serialText -split "`r?`n"
    $faultLines = $serialLines | Where-Object { $_ -match 'VEC=|CR2=|ERR=|RIP=' }
    $vecLines = $serialLines | Where-Object { $_ -match 'VEC=' }
    $cr2Lines = $serialLines | Where-Object { $_ -match 'CR2=' }
    $errLines = $serialLines | Where-Object { $_ -match 'ERR=' }
    $ripLines = $serialLines | Where-Object { $_ -match 'RIP=' }
    $lastVecLine = Get-LastMatchingLine -Text $serialText -Pattern 'VEC='
    $lastErrLine = Get-LastMatchingLine -Text $serialText -Pattern 'ERR='
    $lastCr2Line = Get-LastMatchingLine -Text $serialText -Pattern 'CR2='
    $lastRipLine = Get-LastMatchingLine -Text $serialText -Pattern 'RIP='

    $expectedDispatchReason = if ($Mode -eq 'SafeNormalDesktop') {
        'SMAIN_DISPATCH_REASON=SAFE_NORMAL_DESKTOP_UEFI'
    } elseif ($Mode -eq 'NormalDesktopFirstFrame') {
        'SMAIN_DISPATCH_REASON=NORMAL_DESKTOP_UEFI_FIRST_FRAME'
    } elseif ($Mode -eq 'MultiFrameNormalDesktop') {
        'SMAIN_DISPATCH_REASON=MULTIFRAME_NORMAL_DESKTOP_UEFI'
    } elseif ($Mode -eq 'AbiTaskbar' -or $Mode -eq 'AbiCursor') {
        'SMAIN_DISPATCH_REASON=UEFI_ABI_PROBE'
    } else {
        'SMAIN_DISPATCH_REASON=TINY_UEFI'
    }
    $safeFrameComplete = $serialText.Contains('SAFE_NORMAL_DESKTOP_FRAME_COMPLETE')
    $safeLoopEnter = $serialText.Contains('SAFE_NORMAL_DESKTOP_LOOP_ENTER')
    $safeFaultFramebufferInvalid = $serialText.Contains('SAFE_NORMAL_DESKTOP_FAULT=FRAMEBUFFER_INVALID')
    $safeFaultException = $serialText.Contains('SAFE_NORMAL_DESKTOP_FAULT=EXCEPTION')
    $normalFrameComplete = $serialText.Contains('NORMAL_FRAME_COMPLETE')
    $normalFrameHalt = $serialText.Contains('NORMAL_FRAME_HALT_ENTER')
    $normalFrameFault = $serialLines | Where-Object { $_ -match 'NORMAL_FRAME_FAULT=' }
    $multiFrameBegin = $serialText.Contains('MULTIFRAME_BEGIN')
    $multiFrameTargetLine = Get-LastMatchingLine -Text $serialText -Pattern 'MULTIFRAME_TARGET='
    $multiFrameTargetPresent = $serialText.Contains("MULTIFRAME_TARGET=$FrameTarget")
    $multiFrameComplete = $serialText.Contains('MULTIFRAME_COMPLETE')
    $multiFrameHalt = $serialText.Contains('MULTIFRAME_HALT_ENTER')
    $multiFrameFault = $serialLines | Where-Object { $_ -match '^MULTIFRAME_FAULT=' }
    $multiFrameMarkers = @($serialLines | Where-Object { $_ -match '^FRAME=\d+$' })
    $multiFrameCheckpointLines = @($serialLines | Where-Object { $_ -match '^MULTIFRAME_CHECKPOINT_FRAME=' })
    $multiFrameIdentityChanged = $serialText.Contains('MULTIFRAME_GFX_IDENTITY_CHANGED=1')
    $multiFrameCanonicalInvalid = $serialText.Contains('MULTIFRAME_CANONICAL_GFX_VALID=0')
    $multiFrameStackDrift = $serialText.Contains('MULTIFRAME_STACK_DRIFT=1')
    $multiFrameTimerStalled = $serialText.Contains('MULTIFRAME_TIMER_TICKING=0')
    $multiFrameTimerAdvanced = $serialText.Contains('MULTIFRAME_TIMER_TICKING=1')
    $multiFramePixelInvalid = $serialText.Contains('MULTIFRAME_PIXEL_SAMPLE_VALID=0')
    $multiFrameLastCompletedLine = Get-LastMatchingLine -Text $serialText -Pattern 'MULTIFRAME_LAST_COMPLETED_FRAME='
    $multiFrameFaultStageLine = Get-LastMatchingLine -Text $serialText -Pattern 'FRAME_STAGE='
    $acpiInitialized = $serialText.Contains('[ACPI] ACPI Initialized')
    $pciEnumerated = $serialText.Contains('[PCI] Devices enumerated')
    $abiProbeMode = $Mode -eq 'AbiTaskbar' -or $Mode -eq 'AbiCursor'
    $abiProbeStarted = $serialText.Contains('SMAIN_DISPATCH_UEFI_ABI_PROBE')
    $abiProbeSuccess = if ($Mode -eq 'AbiTaskbar') {
        $serialText.Contains('ABI_PROBE_TASKBAR_SUCCESS')
    } elseif ($Mode -eq 'AbiCursor') {
        $serialText.Contains('ABI_PROBE_CURSOR_SUCCESS')
    } else {
        $false
    }
    $dispatchReasonPresent = $serialText.Contains($expectedDispatchReason)
    $runIdPresent = $serialText.Contains($runId)

    $validRun = $runIdPresent -and $dispatchReasonPresent -and ($faultLines.Count -eq 0)
    if ($abiProbeMode) {
        # A pre-repair ABI fault is an expected forensic result. Require the
        # probe to reach its dispatch point and record either success or a
        # CPU fault frame; do not turn the controlled reproduction into a
        # failed script invocation.
        $validRun = $runIdPresent -and $dispatchReasonPresent -and $abiProbeStarted -and ($abiProbeSuccess -or $faultLines.Count -gt 0)
    }
    if ($Mode -eq 'SafeNormalDesktop') {
        $validRun = $validRun -and $safeFrameComplete -and $safeLoopEnter -and -not $safeFaultFramebufferInvalid -and -not $safeFaultException
    } elseif ($Mode -eq 'NormalDesktopFirstFrame') {
        $validRun = $validRun -and $normalFrameComplete -and $normalFrameHalt -and ($normalFrameFault.Count -eq 0)
    } elseif ($Mode -eq 'MultiFrameNormalDesktop') {
        $expectedFrameMarkers = @(1, 2, 3, 10, 30, 60, 90, 120, 180, 240, $FrameTarget) |
            Where-Object { $_ -le $FrameTarget } | Select-Object -Unique
        $frameMarkersPresent = $true
        foreach ($expectedFrame in $expectedFrameMarkers) {
            if (-not ($serialText.Contains("FRAME=$expectedFrame`r`n") -or $serialText.Contains("FRAME=$expectedFrame`n"))) {
                $frameMarkersPresent = $false
            }
        }
        $expectedCheckpointFrames = @(1, 10, 60, $FrameTarget) | Select-Object -Unique
        $checkpointsPresent = $true
        foreach ($checkpointFrame in $expectedCheckpointFrames) {
            if (-not ($serialText.Contains("MULTIFRAME_CHECKPOINT_FRAME=$checkpointFrame"))) {
                $checkpointsPresent = $false
            }
        }
        $validRun = $validRun -and $multiFrameBegin -and $multiFrameTargetPresent -and
            $multiFrameComplete -and $multiFrameHalt -and ($multiFrameFault.Count -eq 0) -and
             $frameMarkersPresent -and $checkpointsPresent -and
             $serialText.Contains("MULTIFRAME_LAST_COMPLETED_FRAME=$FrameTarget") -and
             $acpiInitialized -and $pciEnumerated -and
             $multiFrameTimerAdvanced -and -not $multiFramePixelInvalid -and
            -not $multiFrameIdentityChanged -and -not $multiFrameCanonicalInvalid -and
            -not $multiFrameStackDrift -and -not $multiFrameTimerStalled
    }

    Write-Host "[uefi-run] Validation:" -ForegroundColor Cyan
    Write-Host "[uefi-run]   Run ID present: $runIdPresent" -ForegroundColor Cyan
    Write-Host "[uefi-run]   Dispatch reason present: $dispatchReasonPresent" -ForegroundColor Cyan
    Write-Host "[uefi-run]   SMAIN_DISPATCH_REASON=TINY_UEFI: $($serialText.Contains('SMAIN_DISPATCH_REASON=TINY_UEFI'))" -ForegroundColor Cyan
    Write-Host "[uefi-run]   SMAIN_DISPATCH_REASON=SAFE_NORMAL_DESKTOP_UEFI: $($serialText.Contains('SMAIN_DISPATCH_REASON=SAFE_NORMAL_DESKTOP_UEFI'))" -ForegroundColor Cyan
    Write-Host "[uefi-run]   SMAIN_DISPATCH_REASON=NORMAL_DESKTOP_UEFI_FIRST_FRAME: $($serialText.Contains('SMAIN_DISPATCH_REASON=NORMAL_DESKTOP_UEFI_FIRST_FRAME'))" -ForegroundColor Cyan
    Write-Host "[uefi-run]   SAFE_NORMAL_DESKTOP_FRAME_COMPLETE: $safeFrameComplete" -ForegroundColor Cyan
    Write-Host "[uefi-run]   SAFE_NORMAL_DESKTOP_LOOP_ENTER: $safeLoopEnter" -ForegroundColor Cyan
    Write-Host "[uefi-run]   SAFE_NORMAL_DESKTOP_FAULT=FRAMEBUFFER_INVALID: $safeFaultFramebufferInvalid" -ForegroundColor Cyan
    Write-Host "[uefi-run]   SAFE_NORMAL_DESKTOP_FAULT=EXCEPTION: $safeFaultException" -ForegroundColor Cyan
    Write-Host "[uefi-run]   NORMAL_FRAME_COMPLETE: $normalFrameComplete" -ForegroundColor Cyan
    Write-Host "[uefi-run]   NORMAL_FRAME_HALT_ENTER: $normalFrameHalt" -ForegroundColor Cyan
    Write-Host "[uefi-run]   NORMAL_FRAME_FAULT lines: $($normalFrameFault.Count)" -ForegroundColor Cyan
    Write-Host "[uefi-run]   MULTIFRAME_BEGIN: $multiFrameBegin" -ForegroundColor Cyan
    Write-Host "[uefi-run]   MULTIFRAME_TARGET=${FrameTarget}: $multiFrameTargetPresent" -ForegroundColor Cyan
     Write-Host "[uefi-run]   MULTIFRAME frame markers: $($multiFrameMarkers.Count)" -ForegroundColor Cyan
     Write-Host "[uefi-run]   MULTIFRAME checkpoints: $($multiFrameCheckpointLines.Count)" -ForegroundColor Cyan
     Write-Host "[uefi-run]   MULTIFRAME expected frame markers present: $frameMarkersPresent" -ForegroundColor Cyan
     Write-Host "[uefi-run]   MULTIFRAME expected checkpoints present: $checkpointsPresent" -ForegroundColor Cyan
    Write-Host "[uefi-run]   MULTIFRAME_COMPLETE: $multiFrameComplete" -ForegroundColor Cyan
    Write-Host "[uefi-run]   MULTIFRAME_HALT_ENTER: $multiFrameHalt" -ForegroundColor Cyan
    Write-Host "[uefi-run]   MULTIFRAME fault lines: $($multiFrameFault.Count)" -ForegroundColor Cyan
    Write-Host "[uefi-run]   MULTIFRAME identity changed: $multiFrameIdentityChanged" -ForegroundColor Cyan
    Write-Host "[uefi-run]   MULTIFRAME canonical graphics invalid: $multiFrameCanonicalInvalid" -ForegroundColor Cyan
    Write-Host "[uefi-run]   MULTIFRAME stack drift: $multiFrameStackDrift" -ForegroundColor Cyan
    Write-Host "[uefi-run]   MULTIFRAME timer stalled: $multiFrameTimerStalled" -ForegroundColor Cyan
    Write-Host "[uefi-run]   MULTIFRAME timer advanced: $multiFrameTimerAdvanced" -ForegroundColor Cyan
    Write-Host "[uefi-run]   MULTIFRAME pixel sample invalid: $multiFramePixelInvalid" -ForegroundColor Cyan
    Write-Host "[uefi-run]   MULTIFRAME last completed line: $multiFrameLastCompletedLine" -ForegroundColor Cyan
     Write-Host "[uefi-run]   MULTIFRAME fault stage line: $multiFrameFaultStageLine" -ForegroundColor Cyan
     Write-Host "[uefi-run]   ACPI initialized: $acpiInitialized" -ForegroundColor Cyan
     Write-Host "[uefi-run]   PCI devices enumerated: $pciEnumerated" -ForegroundColor Cyan
    Write-Host "[uefi-run]   ABI probe started: $abiProbeStarted" -ForegroundColor Cyan
    Write-Host "[uefi-run]   ABI probe success: $abiProbeSuccess" -ForegroundColor Cyan
    Write-Host "[uefi-run]   Fault VEC lines: $($vecLines.Count)" -ForegroundColor Cyan
    Write-Host "[uefi-run]   Fault ERR lines: $($errLines.Count)" -ForegroundColor Cyan
    Write-Host "[uefi-run]   Fault CR2 lines: $($cr2Lines.Count)" -ForegroundColor Cyan
    Write-Host "[uefi-run]   Fault RIP lines: $($ripLines.Count)" -ForegroundColor Cyan
    Write-Host "[uefi-run]   Screenshot captured: $screenshotCaptured" -ForegroundColor Cyan
    if ($screenshotCaptured) {
        Write-Host "[uefi-run]   Screenshot path: $ScreenshotPath" -ForegroundColor Cyan
    }

    if ($validRun) {
        Write-Host "[uefi-run] Valid run detected." -ForegroundColor Green
    } else {
        Write-Host "[uefi-run] Invalid run detected." -ForegroundColor Red
        if ($faultLines.Count -gt 0) {
            Write-Host "[uefi-run] Fault lines:" -ForegroundColor Yellow
            $faultLines | ForEach-Object { Write-Host "[uefi-run]   $_" -ForegroundColor Yellow }
        }
        throw "Validation failed for mode $Mode."
    }

    $summary = @(
        "RUN_ID=$runId"
        "MODE=$Mode"
        "EXPECTED_DISPATCH_REASON=$expectedDispatchReason"
        "RUN_ID_PRESENT=$runIdPresent"
        "DISPATCH_REASON_PRESENT=$dispatchReasonPresent"
        "TINY_UEFI_PRESENT=$($serialText.Contains('SMAIN_DISPATCH_REASON=TINY_UEFI'))"
        "SAFE_NORMAL_DESKTOP_PRESENT=$($serialText.Contains('SMAIN_DISPATCH_REASON=SAFE_NORMAL_DESKTOP_UEFI'))"
        "NORMAL_DESKTOP_UEFI_FIRST_FRAME_PRESENT=$($serialText.Contains('SMAIN_DISPATCH_REASON=NORMAL_DESKTOP_UEFI_FIRST_FRAME'))"
        "SAFE_NORMAL_DESKTOP_FRAME_COMPLETE=$safeFrameComplete"
        "SAFE_NORMAL_DESKTOP_LOOP_ENTER=$safeLoopEnter"
        "SAFE_NORMAL_DESKTOP_FAULT_FRAMEBUFFER_INVALID=$safeFaultFramebufferInvalid"
        "SAFE_NORMAL_DESKTOP_FAULT_EXCEPTION=$safeFaultException"
        "NORMAL_FRAME_COMPLETE=$normalFrameComplete"
        "NORMAL_FRAME_HALT_ENTER=$normalFrameHalt"
        "NORMAL_FRAME_FAULT_LINES=$($normalFrameFault.Count)"
        "FRAME_TARGET=$FrameTarget"
        "MULTIFRAME_BEGIN=$multiFrameBegin"
        "MULTIFRAME_TARGET_PRESENT=$multiFrameTargetPresent"
        "MULTIFRAME_TARGET_LINE=$multiFrameTargetLine"
        "MULTIFRAME_FRAME_MARKERS=$($multiFrameMarkers.Count)"
        "MULTIFRAME_CHECKPOINTS=$($multiFrameCheckpointLines.Count)"
        "MULTIFRAME_COMPLETE=$multiFrameComplete"
        "MULTIFRAME_HALT_ENTER=$multiFrameHalt"
        "MULTIFRAME_FAULT_LINES=$($multiFrameFault.Count)"
        "MULTIFRAME_GFX_IDENTITY_CHANGED=$multiFrameIdentityChanged"
        "MULTIFRAME_CANONICAL_GFX_INVALID=$multiFrameCanonicalInvalid"
        "MULTIFRAME_STACK_DRIFT=$multiFrameStackDrift"
        "MULTIFRAME_TIMER_STALLED=$multiFrameTimerStalled"
        "MULTIFRAME_TIMER_ADVANCED=$multiFrameTimerAdvanced"
        "MULTIFRAME_PIXEL_SAMPLE_INVALID=$multiFramePixelInvalid"
        "MULTIFRAME_LAST_COMPLETED_LINE=$multiFrameLastCompletedLine"
         "MULTIFRAME_FAULT_STAGE_LINE=$multiFrameFaultStageLine"
         "ACPI_INITIALIZED=$acpiInitialized"
         "PCI_ENUMERATED=$pciEnumerated"
        "ABI_PROBE_STARTED=$abiProbeStarted"
        "ABI_PROBE_SUCCESS=$abiProbeSuccess"
        "FAULT_VEC_LINES=$($vecLines.Count)"
        "FAULT_ERR_LINES=$($errLines.Count)"
        "FAULT_CR2_LINES=$($cr2Lines.Count)"
        "FAULT_RIP_LINES=$($ripLines.Count)"
        "KERNEL_ELF_SHA256=$($kernelRoot.Sha256)"
        "ESP_KERNEL_ELF_SHA256=$($kernelEsp.Sha256)"
        "SERIAL_LOG=$serialLog"
        "SCREENSHOT_REQUESTED=$CaptureScreenshot"
        "SCREENSHOT_CAPTURED=$screenshotCaptured"
        "SCREENSHOT_PATH=$ScreenshotPath"
        "LAST_VEC_LINE=$lastVecLine"
        "LAST_ERR_LINE=$lastErrLine"
        "LAST_CR2_LINE=$lastCr2Line"
        "LAST_RIP_LINE=$lastRipLine"
    )
    [System.IO.File]::WriteAllLines($summaryLog, $summary)
    Write-Host "[uefi-run] Summary written to: $summaryLog" -ForegroundColor Cyan
    Write-Host "[uefi-run] Serial log: $serialLog" -ForegroundColor Cyan
    Write-Host "[uefi-run] Kernel ELF hash: $($kernelRoot.Sha256)" -ForegroundColor Cyan
    Write-Host "[uefi-run] ESP/kernel.elf hash: $($kernelEsp.Sha256)" -ForegroundColor Cyan
    if ($screenshotCaptured) {
        Write-Host "[uefi-run] Screenshot path: $ScreenshotPath" -ForegroundColor Cyan
    }
}
finally {
    if ($qemuProcess -and -not $qemuProcess.HasExited) {
        Stop-Process -Id $qemuProcess.Id -Force -ErrorAction SilentlyContinue
    }

    if ($programPatched) {
        [System.IO.File]::WriteAllText($programPath, $originalProgram)
        Write-Host "[uefi-run] Restored Program.cs defaults." -ForegroundColor Cyan
    }
}
