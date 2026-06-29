param(
    [ValidateSet('Default', 'SafeNormalDesktop')]
    [string]$Mode = 'Default',
    [switch]$CaptureScreenshot,
    [switch]$GuiVisible,
    [int]$CaptureSeconds = 120,
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
$runStamp = Get-Date -Format 'yyyyMMdd_HHmmss_fff'
$runId = "UEFI_RUN_ID_${runStamp}_PID$PID"
$modeLabel = if ($Mode -eq 'SafeNormalDesktop') { 'SAFE_NORMAL_DESKTOP_UEFI' } else { 'TINY_UEFI' }
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
            -Old 'private const bool UEFI_ENABLE_SAFE_NORMAL_DESKTOP_FIRST_FRAME = false;' `
            -New 'private const bool UEFI_ENABLE_SAFE_NORMAL_DESKTOP_FIRST_FRAME = true;' `
            -Label 'UEFI_ENABLE_SAFE_NORMAL_DESKTOP_FIRST_FRAME'
    }

    [System.IO.File]::WriteAllText($programPath, $patched)
    $programPatched = $true

    Write-Host "[uefi-run] Run ID: $runId" -ForegroundColor Cyan
    Write-Host "[uefi-run] Mode: $Mode" -ForegroundColor Cyan
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

    Remove-Item Env:PATH -ErrorAction SilentlyContinue

    $serialRelative = $serialLog.Substring($root.Length).TrimStart('\', '/') -replace '\\', '/'
    $qemuArgs = @(
        '-drive', 'if=pflash,format=raw,readonly=on,file=OVMF.fd'
        '-drive', 'file=fat:rw:ESP,format=raw'
        '-m', '1024M'
        '-serial', ('file:' + $serialRelative)
        '-no-reboot'
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
    } else {
        'SMAIN_DISPATCH_REASON=TINY_UEFI'
    }
    $safeFrameComplete = $serialText.Contains('SAFE_NORMAL_DESKTOP_FRAME_COMPLETE')
    $safeLoopEnter = $serialText.Contains('SAFE_NORMAL_DESKTOP_LOOP_ENTER')
    $safeFaultFramebufferInvalid = $serialText.Contains('SAFE_NORMAL_DESKTOP_FAULT=FRAMEBUFFER_INVALID')
    $safeFaultException = $serialText.Contains('SAFE_NORMAL_DESKTOP_FAULT=EXCEPTION')
    $dispatchReasonPresent = $serialText.Contains($expectedDispatchReason)
    $runIdPresent = $serialText.Contains($runId)

    $validRun = $runIdPresent -and $dispatchReasonPresent -and ($faultLines.Count -eq 0)
    if ($Mode -eq 'SafeNormalDesktop') {
        $validRun = $validRun -and $safeFrameComplete -and $safeLoopEnter -and -not $safeFaultFramebufferInvalid -and -not $safeFaultException
    }

    Write-Host "[uefi-run] Validation:" -ForegroundColor Cyan
    Write-Host "[uefi-run]   Run ID present: $runIdPresent" -ForegroundColor Cyan
    Write-Host "[uefi-run]   Dispatch reason present: $dispatchReasonPresent" -ForegroundColor Cyan
    Write-Host "[uefi-run]   SMAIN_DISPATCH_REASON=TINY_UEFI: $($serialText.Contains('SMAIN_DISPATCH_REASON=TINY_UEFI'))" -ForegroundColor Cyan
    Write-Host "[uefi-run]   SMAIN_DISPATCH_REASON=SAFE_NORMAL_DESKTOP_UEFI: $($serialText.Contains('SMAIN_DISPATCH_REASON=SAFE_NORMAL_DESKTOP_UEFI'))" -ForegroundColor Cyan
    Write-Host "[uefi-run]   SAFE_NORMAL_DESKTOP_FRAME_COMPLETE: $safeFrameComplete" -ForegroundColor Cyan
    Write-Host "[uefi-run]   SAFE_NORMAL_DESKTOP_LOOP_ENTER: $safeLoopEnter" -ForegroundColor Cyan
    Write-Host "[uefi-run]   SAFE_NORMAL_DESKTOP_FAULT=FRAMEBUFFER_INVALID: $safeFaultFramebufferInvalid" -ForegroundColor Cyan
    Write-Host "[uefi-run]   SAFE_NORMAL_DESKTOP_FAULT=EXCEPTION: $safeFaultException" -ForegroundColor Cyan
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
        "SAFE_NORMAL_DESKTOP_FRAME_COMPLETE=$safeFrameComplete"
        "SAFE_NORMAL_DESKTOP_LOOP_ENTER=$safeLoopEnter"
        "SAFE_NORMAL_DESKTOP_FAULT_FRAMEBUFFER_INVALID=$safeFaultFramebufferInvalid"
        "SAFE_NORMAL_DESKTOP_FAULT_EXCEPTION=$safeFaultException"
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
