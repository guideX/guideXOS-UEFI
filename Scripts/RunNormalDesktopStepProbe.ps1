param(
    [int]$CaptureSeconds = 120,
    [ValidateSet('DrawImage', 'FillRectangle')]
    [string]$Step10RedMode = 'DrawImage',
    [ValidateSet('DrawImage', 'FillRectangle')]
    [string]$Step10GreenMode = 'FillRectangle',
    [ValidateSet('DrawImage', 'FillRectangle')]
    [string]$Step10WhiteMode = 'FillRectangle',
    [switch]$SafeFontPlaceholder,
    [switch]$SkipWindowTraversal,
    [switch]$SkipCursorDraw,
    [switch]$CursorPlaceholder,
    [switch]$ProbeRealCursorImageRendering,
    [switch]$CursorEmptyBodyProbe,
    [switch]$CursorInlineBodyProbe,
    [switch]$CursorStaticBodyProbe,
    [switch]$CursorStaticImageRefProbe,
    [switch]$CursorStaticDimsProbe,
    [switch]$CursorStaticRawDataRefProbe,
    [switch]$CursorStaticFirstPixelProbe,
    [switch]$CursorSourceExistingRefsProbe,
    [switch]$CursorSourceFallbackProbe,
    [switch]$CursorSourcePngProbe,
    [switch]$CursorDrawBusyProbe,
    [switch]$CursorDrawFallbackProbe,
    [switch]$SafeCursorImageFallback,
    [switch]$GuiVisible,
    [string]$GuiScreenshotPath
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

function Get-CursorProbeDims {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Lines,

        [Parameter(Mandatory = $true)]
        [string]$EnterMarker,

        [Parameter(Mandatory = $true)]
        [string]$ExitMarker
    )

    $enterIndex = -1
    for ($i = 0; $i -lt $Lines.Count; $i++) {
        if ($Lines[$i].Contains($EnterMarker)) {
            $enterIndex = $i
        }
    }

    if ($enterIndex -lt 0) {
        return $null
    }

    $values = @()
    for ($i = $enterIndex + 1; $i -lt $Lines.Count; $i++) {
        $line = $Lines[$i]
        if ($line.Contains($ExitMarker)) {
            break
        }

        if ($line -match '^\d+$') {
            $values += [uint64]$line
            if ($values.Count -ge 2) {
                break
            }
        }
    }

    if ($values.Count -ge 2) {
        return [pscustomobject]@{
            Width = $values[0]
            Height = $values[1]
        }
    }

    return $null
}

function Ensure-NativeScreenshotHelpers {
    if (-not ("QemuProbeNativeMethods" -as [type])) {
        Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class QemuProbeNativeMethods
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
        $fallbackHandle = [QemuProbeNativeMethods]::FindWindowHandleByTitleSubstring(@('guideXOS', 'QEMU', 'OVMF'))
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

    $rect = New-Object QemuProbeNativeMethods+RECT
    if (-not [QemuProbeNativeMethods]::GetWindowRect($WindowHandle, [ref]$rect)) {
        throw "GetWindowRect failed for QEMU window."
    }

    $width = $rect.Right - $rect.Left
    $height = $rect.Bottom - $rect.Top
    if ($width -le 0 -or $height -le 0) {
        throw "Invalid QEMU window size: ${width}x${height}."
    }

    [QemuProbeNativeMethods]::ShowWindow($WindowHandle, 9) | Out-Null
    [QemuProbeNativeMethods]::SetForegroundWindow($WindowHandle) | Out-Null
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
$qemuFirmwareRoot = Join-Path $root 'bin\qemu-firmware'
$qemuFirmwareCode = Join-Path $qemuFirmwareRoot 'edk2-x86_64-code.fd'
$qemuFirmwareVars = Join-Path $qemuFirmwareRoot 'edk2-vars.fd'
$qemuFirmwareCodeSource = 'C:\Program Files\qemu\share\edk2-x86_64-code.fd'
$qemuFirmwareVarsSource = 'C:\Program Files\qemu\share\edk2-i386-vars.fd'
$probeRoot = Join-Path $root 'bin\probe-logs'
$runStamp = Get-Date -Format 'yyyyMMdd_HHmmss'
$runId = "STEP_PROBE_RUN_ID_$runStamp"
$serialLog = Join-Path $probeRoot "serial_output_step_probe_$runId.txt"
$stderrLog = Join-Path $probeRoot "qemu_stderr_step_probe_$runId.txt"
$summaryLog = Join-Path $probeRoot "step_probe_summary_$runId.txt"
if (-not $GuiScreenshotPath) {
    $GuiScreenshotPath = Join-Path $probeRoot "qemu_gui_probe_$runId.png"
}
$qmpPort = $null
if ($GuiVisible) {
    $qmpPort = Get-FreeTcpPort
}

$cursorVariantCount = 0
if ($CursorEmptyBodyProbe) { $cursorVariantCount++ }
if ($CursorInlineBodyProbe) { $cursorVariantCount++ }
if ($CursorStaticBodyProbe) { $cursorVariantCount++ }
if ($CursorStaticImageRefProbe) { $cursorVariantCount++ }
if ($CursorStaticDimsProbe) { $cursorVariantCount++ }
if ($CursorStaticRawDataRefProbe) { $cursorVariantCount++ }
if ($CursorStaticFirstPixelProbe) { $cursorVariantCount++ }
if ($CursorSourceExistingRefsProbe) { $cursorVariantCount++ }
if ($CursorSourceFallbackProbe) { $cursorVariantCount++ }
if ($CursorSourcePngProbe) { $cursorVariantCount++ }
if ($CursorDrawBusyProbe) { $cursorVariantCount++ }
if ($CursorDrawFallbackProbe) { $cursorVariantCount++ }
if ($cursorVariantCount -gt 1) {
    throw "Only one cursor probe variant can be enabled per run."
}

$cursorBodyVariant = if ($CursorEmptyBodyProbe) {
    'EmptyBodyCall'
} elseif ($CursorInlineBodyProbe) {
    'InlineBody'
} elseif ($CursorStaticBodyProbe) {
    'StaticBody'
} elseif ($CursorStaticImageRefProbe) {
    'StaticImageRef'
} elseif ($CursorStaticDimsProbe) {
    'StaticDims'
} elseif ($CursorStaticRawDataRefProbe) {
    'StaticRawDataRef'
} elseif ($CursorStaticFirstPixelProbe) {
    'StaticFirstPixel'
} elseif ($CursorSourceExistingRefsProbe) {
    'SourceExistingRefs'
} elseif ($CursorSourceFallbackProbe) {
    'SourceFallback'
} elseif ($CursorSourcePngProbe) {
    'SourcePng'
} elseif ($CursorDrawBusyProbe) {
    'DrawBusy'
} elseif ($CursorDrawFallbackProbe) {
    'DrawFallback'
} else {
    'OriginalBodyCall'
}

New-Item -ItemType Directory -Path $probeRoot -Force | Out-Null
New-Item -ItemType Directory -Path $qemuFirmwareRoot -Force | Out-Null

$originalProgram = [System.IO.File]::ReadAllText($programPath)
$programPatched = $false
$qemuProcess = $null

try {
    $patched = $originalProgram
    $patched = Assert-SingleReplacement -Text $patched `
        -Old 'private const bool UEFI_ALLOW_NORMAL_DESKTOP_RENDER_PATH = false;' `
        -New 'private const bool UEFI_ALLOW_NORMAL_DESKTOP_RENDER_PATH = true;' `
        -Label 'UEFI_ALLOW_NORMAL_DESKTOP_RENDER_PATH'
    $patched = Assert-SingleReplacement -Text $patched `
        -Old 'private const bool UEFI_USE_TINY_RENDER_LOOP_BYPASS = true;' `
        -New 'private const bool UEFI_USE_TINY_RENDER_LOOP_BYPASS = false;' `
        -Label 'UEFI_USE_TINY_RENDER_LOOP_BYPASS'
    $patched = Assert-SingleReplacement -Text $patched `
        -Old 'private const bool NORMAL_DESKTOP_UEFI_STEP_PROBE = false;' `
        -New 'private const bool NORMAL_DESKTOP_UEFI_STEP_PROBE = true;' `
        -Label 'NORMAL_DESKTOP_UEFI_STEP_PROBE'
    $patched = Assert-SingleReplacement -Text $patched `
        -Old 'internal const bool NORMAL_DESKTOP_UEFI_PROBE_SAFE_PLACEHOLDERS_UNTIL_STEP10 = false;' `
        -New 'internal const bool NORMAL_DESKTOP_UEFI_PROBE_SAFE_PLACEHOLDERS_UNTIL_STEP10 = true;' `
        -Label 'NORMAL_DESKTOP_UEFI_PROBE_SAFE_PLACEHOLDERS_UNTIL_STEP10'
    $step12SafeFontPlaceholder = if ($SafeFontPlaceholder) { 'true' } else { 'false' }
    $patched = Assert-SingleReplacement -Text $patched `
        -Old 'internal const bool NORMAL_DESKTOP_UEFI_PROBE_SAFE_FONT_PLACEHOLDER = false;' `
        -New "internal const bool NORMAL_DESKTOP_UEFI_PROBE_SAFE_FONT_PLACEHOLDER = $step12SafeFontPlaceholder;" `
        -Label 'NORMAL_DESKTOP_UEFI_PROBE_SAFE_FONT_PLACEHOLDER'
    $step11SkipWindowTraversal = if ($SkipWindowTraversal) { 'true' } else { 'false' }
    $patched = Assert-SingleReplacement -Text $patched `
        -Old 'private const bool NORMAL_DESKTOP_UEFI_PROBE_SKIP_WINDOW_TRAVERSAL = false;' `
        -New "private const bool NORMAL_DESKTOP_UEFI_PROBE_SKIP_WINDOW_TRAVERSAL = $step11SkipWindowTraversal;" `
        -Label 'NORMAL_DESKTOP_UEFI_PROBE_SKIP_WINDOW_TRAVERSAL'
    $step13SkipCursorDraw = if ($SkipCursorDraw) { 'true' } else { 'false' }
    $patched = Assert-SingleReplacement -Text $patched `
        -Old 'private const bool NORMAL_DESKTOP_UEFI_PROBE_SKIP_CURSOR_DRAW = false;' `
        -New "private const bool NORMAL_DESKTOP_UEFI_PROBE_SKIP_CURSOR_DRAW = $step13SkipCursorDraw;" `
        -Label 'NORMAL_DESKTOP_UEFI_PROBE_SKIP_CURSOR_DRAW'
    $step13CursorPlaceholder = if ($CursorPlaceholder) { 'true' } else { 'false' }
    $patched = Assert-SingleReplacement -Text $patched `
        -Old 'private const bool NORMAL_DESKTOP_UEFI_PROBE_CURSOR_PLACEHOLDER = false;' `
        -New "private const bool NORMAL_DESKTOP_UEFI_PROBE_CURSOR_PLACEHOLDER = $step13CursorPlaceholder;" `
        -Label 'NORMAL_DESKTOP_UEFI_PROBE_CURSOR_PLACEHOLDER'
    $step13RealCursorImageRenderingEnabled = $ProbeRealCursorImageRendering -or $CursorEmptyBodyProbe -or $CursorInlineBodyProbe -or $CursorStaticBodyProbe -or $CursorStaticImageRefProbe -or $CursorStaticDimsProbe -or $CursorStaticRawDataRefProbe -or $CursorStaticFirstPixelProbe -or $CursorSourceExistingRefsProbe -or $CursorSourceFallbackProbe -or $CursorSourcePngProbe -or $CursorDrawBusyProbe -or $CursorDrawFallbackProbe
    $step13RealCursorImageRendering = if ($step13RealCursorImageRenderingEnabled) { 'true' } else { 'false' }
    $patched = Assert-SingleReplacement -Text $patched `
        -Old 'private const bool UEFI_PROBE_REAL_CURSOR_IMAGE_RENDERING = false;' `
        -New "private const bool UEFI_PROBE_REAL_CURSOR_IMAGE_RENDERING = $step13RealCursorImageRendering;" `
        -Label 'UEFI_PROBE_REAL_CURSOR_IMAGE_RENDERING'
    $cursorDrawBusyProbeValue = if ($CursorDrawBusyProbe) { 'true' } else { 'false' }
    $patched = Assert-SingleReplacement -Text $patched `
        -Old 'private const bool UEFI_PROBE_CURSOR_DRAW_BUSY = false;' `
        -New "private const bool UEFI_PROBE_CURSOR_DRAW_BUSY = $cursorDrawBusyProbeValue;" `
        -Label 'UEFI_PROBE_CURSOR_DRAW_BUSY'
    $cursorDrawFallbackProbeValue = if ($CursorDrawFallbackProbe) { 'true' } else { 'false' }
    $patched = Assert-SingleReplacement -Text $patched `
        -Old 'private const bool UEFI_PROBE_CURSOR_DRAW_FALLBACK = false;' `
        -New "private const bool UEFI_PROBE_CURSOR_DRAW_FALLBACK = $cursorDrawFallbackProbeValue;" `
        -Label 'UEFI_PROBE_CURSOR_DRAW_FALLBACK'
    $cursorEmptyBodyCallProbeValue = if ($CursorEmptyBodyProbe) { 'true' } else { 'false' }
    $patched = Assert-SingleReplacement -Text $patched `
        -Old 'private const bool UEFI_PROBE_CURSOR_EMPTY_BODY_CALL = false;' `
        -New "private const bool UEFI_PROBE_CURSOR_EMPTY_BODY_CALL = $cursorEmptyBodyCallProbeValue;" `
        -Label 'UEFI_PROBE_CURSOR_EMPTY_BODY_CALL'
    $cursorInlineBodyProbeValue = if ($CursorInlineBodyProbe) { 'true' } else { 'false' }
    $patched = Assert-SingleReplacement -Text $patched `
        -Old 'private const bool UEFI_PROBE_CURSOR_INLINE_BODY = false;' `
        -New "private const bool UEFI_PROBE_CURSOR_INLINE_BODY = $cursorInlineBodyProbeValue;" `
        -Label 'UEFI_PROBE_CURSOR_INLINE_BODY'
    $cursorStaticBodyProbeValue = if ($CursorStaticBodyProbe) { 'true' } else { 'false' }
    $patched = Assert-SingleReplacement -Text $patched `
        -Old 'private const bool UEFI_PROBE_CURSOR_STATIC_BODY = false;' `
        -New "private const bool UEFI_PROBE_CURSOR_STATIC_BODY = $cursorStaticBodyProbeValue;" `
        -Label 'UEFI_PROBE_CURSOR_STATIC_BODY'
    $cursorStaticImageRefProbeValue = if ($CursorStaticImageRefProbe) { 'true' } else { 'false' }
    $patched = Assert-SingleReplacement -Text $patched `
        -Old 'private const bool UEFI_PROBE_CURSOR_STATIC_IMAGE_REF = false;' `
        -New "private const bool UEFI_PROBE_CURSOR_STATIC_IMAGE_REF = $cursorStaticImageRefProbeValue;" `
        -Label 'UEFI_PROBE_CURSOR_STATIC_IMAGE_REF'
    $cursorStaticDimsProbeValue = if ($CursorStaticDimsProbe) { 'true' } else { 'false' }
    $patched = Assert-SingleReplacement -Text $patched `
        -Old 'private const bool UEFI_PROBE_CURSOR_STATIC_DIMS = false;' `
        -New "private const bool UEFI_PROBE_CURSOR_STATIC_DIMS = $cursorStaticDimsProbeValue;" `
        -Label 'UEFI_PROBE_CURSOR_STATIC_DIMS'
    $cursorStaticRawDataRefProbeValue = if ($CursorStaticRawDataRefProbe) { 'true' } else { 'false' }
    $patched = Assert-SingleReplacement -Text $patched `
        -Old 'private const bool UEFI_PROBE_CURSOR_STATIC_RAWDATA_REF = false;' `
        -New "private const bool UEFI_PROBE_CURSOR_STATIC_RAWDATA_REF = $cursorStaticRawDataRefProbeValue;" `
        -Label 'UEFI_PROBE_CURSOR_STATIC_RAWDATA_REF'
    $cursorStaticFirstPixelProbeValue = if ($CursorStaticFirstPixelProbe) { 'true' } else { 'false' }
    $patched = Assert-SingleReplacement -Text $patched `
        -Old 'private const bool UEFI_PROBE_CURSOR_STATIC_FIRST_PIXEL = false;' `
        -New "private const bool UEFI_PROBE_CURSOR_STATIC_FIRST_PIXEL = $cursorStaticFirstPixelProbeValue;" `
        -Label 'UEFI_PROBE_CURSOR_STATIC_FIRST_PIXEL'
    $cursorSourceExistingRefsProbeValue = if ($CursorSourceExistingRefsProbe) { 'true' } else { 'false' }
    $patched = Assert-SingleReplacement -Text $patched `
        -Old 'private const bool UEFI_PROBE_CURSOR_SOURCE_EXISTING_REFS = false;' `
        -New "private const bool UEFI_PROBE_CURSOR_SOURCE_EXISTING_REFS = $cursorSourceExistingRefsProbeValue;" `
        -Label 'UEFI_PROBE_CURSOR_SOURCE_EXISTING_REFS'
    $cursorSourceFallbackProbeValue = if ($CursorSourceFallbackProbe) { 'true' } else { 'false' }
    $patched = Assert-SingleReplacement -Text $patched `
        -Old 'private const bool UEFI_PROBE_CURSOR_SOURCE_FALLBACK = false;' `
        -New "private const bool UEFI_PROBE_CURSOR_SOURCE_FALLBACK = $cursorSourceFallbackProbeValue;" `
        -Label 'UEFI_PROBE_CURSOR_SOURCE_FALLBACK'
    $cursorSourcePngProbeValue = if ($CursorSourcePngProbe) { 'true' } else { 'false' }
    $patched = Assert-SingleReplacement -Text $patched `
        -Old 'private const bool UEFI_PROBE_CURSOR_SOURCE_PNG = false;' `
        -New "private const bool UEFI_PROBE_CURSOR_SOURCE_PNG = $cursorSourcePngProbeValue;" `
        -Label 'UEFI_PROBE_CURSOR_SOURCE_PNG'
    $safeCursorImageFallbackValue = if ($SafeCursorImageFallback) { 'true' } else { 'false' }
    $patched = Assert-SingleReplacement -Text $patched `
        -Old 'private const bool UEFI_SAFE_CURSOR_IMAGE_FALLBACK = false;' `
        -New "private const bool UEFI_SAFE_CURSOR_IMAGE_FALLBACK = $safeCursorImageFallbackValue;" `
        -Label 'UEFI_SAFE_CURSOR_IMAGE_FALLBACK'
    $step10RedPlaceholder = if ($Step10RedMode -eq 'FillRectangle') { 'true' } else { 'false' }
    $patched = Assert-SingleReplacement -Text $patched `
        -Old 'private const bool NORMAL_DESKTOP_UEFI_PROBE_STEP10_RED_FILLRECT_PLACEHOLDER = false;' `
        -New "private const bool NORMAL_DESKTOP_UEFI_PROBE_STEP10_RED_FILLRECT_PLACEHOLDER = $step10RedPlaceholder;" `
        -Label 'NORMAL_DESKTOP_UEFI_PROBE_STEP10_RED_FILLRECT_PLACEHOLDER'
    $step10GreenPlaceholder = if ($Step10GreenMode -eq 'FillRectangle') { 'true' } else { 'false' }
    $patched = Assert-SingleReplacement -Text $patched `
        -Old 'private const bool NORMAL_DESKTOP_UEFI_PROBE_STEP10_GREEN_FILLRECT_PLACEHOLDER = false;' `
        -New "private const bool NORMAL_DESKTOP_UEFI_PROBE_STEP10_GREEN_FILLRECT_PLACEHOLDER = $step10GreenPlaceholder;" `
        -Label 'NORMAL_DESKTOP_UEFI_PROBE_STEP10_GREEN_FILLRECT_PLACEHOLDER'
    $step10WhitePlaceholder = if ($Step10WhiteMode -eq 'FillRectangle') { 'true' } else { 'false' }
    $patched = Assert-SingleReplacement -Text $patched `
        -Old 'private const bool NORMAL_DESKTOP_UEFI_PROBE_STEP10_WHITE_FILLRECT_PLACEHOLDER = false;' `
        -New "private const bool NORMAL_DESKTOP_UEFI_PROBE_STEP10_WHITE_FILLRECT_PLACEHOLDER = $step10WhitePlaceholder;" `
        -Label 'NORMAL_DESKTOP_UEFI_PROBE_STEP10_WHITE_FILLRECT_PLACEHOLDER'

    $probeAnchor = 'SerialBreadcrumb("NORM_PROBE_ENTER");'
    $probeAnchorCount = [regex]::Matches($patched, [regex]::Escape($probeAnchor)).Count
    if ($probeAnchorCount -ne 1) {
        throw "Expected exactly one NORM_PROBE_ENTER anchor, found $probeAnchorCount."
    }

    $probeMarker = "SerialBreadcrumb(`"$runId`");"
    $patched = $patched.Replace(
        $probeAnchor,
        $probeAnchor + "`r`n        " + $probeMarker
    )

    [System.IO.File]::WriteAllText($programPath, $patched)
    $programPatched = $true

    $windowStyle = if ($GuiVisible) { 'Normal' } else { 'Hidden' }

    Write-Host "[probe] Run ID: $runId" -ForegroundColor Cyan
    Write-Host "[probe] Step 10 red mode: $Step10RedMode" -ForegroundColor Cyan
    Write-Host "[probe] Step 10 green mode: $Step10GreenMode" -ForegroundColor Cyan
    Write-Host "[probe] Step 10 white mode: $Step10WhiteMode" -ForegroundColor Cyan
    Write-Host "[probe] Step 11 skip window traversal: $SkipWindowTraversal" -ForegroundColor Cyan
    Write-Host "[probe] Step 12 safe font placeholder: $SafeFontPlaceholder" -ForegroundColor Cyan
    Write-Host "[probe] Step 13 skip cursor draw: $SkipCursorDraw" -ForegroundColor Cyan
    Write-Host "[probe] Step 13 cursor placeholder: $CursorPlaceholder" -ForegroundColor Cyan
    Write-Host "[probe] Step 13 real cursor image rendering (effective): $step13RealCursorImageRenderingEnabled" -ForegroundColor Cyan
    Write-Host "[probe] Safe cursor image fallback: $SafeCursorImageFallback" -ForegroundColor Cyan
    Write-Host "[probe] Step 13 cursor body variant: $cursorBodyVariant" -ForegroundColor Cyan
    Write-Host "[probe] GUI visible mode: $GuiVisible" -ForegroundColor Cyan
    Write-Host "[probe] GUI screenshot path: $GuiScreenshotPath" -ForegroundColor Cyan
    Write-Host "[probe] Safe placeholders until step 10: enabled" -ForegroundColor Cyan
    Write-Host "[probe] Patched Program.cs for temporary step probe" -ForegroundColor Cyan
    Write-Host "[probe] Building via build.ps1..." -ForegroundColor Cyan
    & powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File $buildScript
    if ($LASTEXITCODE -ne 0) {
        throw "build.ps1 failed with exit code $LASTEXITCODE"
    }

    Write-Host "[probe] Build complete. Kernel snapshots:" -ForegroundColor Cyan
    $kernelRoot = Get-FileSnapshot -Path (Join-Path $root 'kernel.elf')
    $kernelEsp = Get-FileSnapshot -Path (Join-Path $root 'ESP\kernel.elf')
    foreach ($snap in @($kernelRoot, $kernelEsp)) {
        Write-Host ("[probe]   {0}" -f $snap.Path) -ForegroundColor Gray
        Write-Host ("[probe]     mtime:  {0}" -f $snap.LastWriteTime.ToString('o')) -ForegroundColor Gray
        Write-Host ("[probe]     sha256: {0}" -f $snap.Sha256) -ForegroundColor Gray
        Write-Host ("[probe]     bytes:  {0}" -f $snap.Length) -ForegroundColor Gray
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

    Copy-Item -LiteralPath $qemuFirmwareCodeSource -Destination $qemuFirmwareCode -Force
    Copy-Item -LiteralPath $qemuFirmwareVarsSource -Destination $qemuFirmwareVars -Force

    # The local PowerShell host can surface both Path and PATH; QEMU Start-Process
    # inherits that duplicate environment and fails before launch. Keep the canonical
    # Path entry and drop PATH for the probe child process.
    Remove-Item Env:PATH -ErrorAction SilentlyContinue

    Write-Host "[probe] Launching QEMU with the bundled q35 UEFI firmware path..." -ForegroundColor Cyan
    Write-Host "[probe] Serial log: $serialLog" -ForegroundColor Cyan

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
    if ($GuiVisible -and $qmpPort) {
        $qemuArgs += @('-qmp', "tcp:127.0.0.1:$qmpPort,server,nowait")
    }

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

    $guiScreenshotCaptured = $false
    if ($GuiVisible -and $qemuProcess -and -not $qemuProcess.HasExited) {
        try {
            if ($qmpPort) {
                Save-QmpScreenshot -Port $qmpPort -Path $GuiScreenshotPath
                $guiScreenshotCaptured = $true
                Write-Host "[probe] GUI screenshot captured: $GuiScreenshotPath" -ForegroundColor Cyan
            } else {
                $qemuWindowHandle = Get-QemuWindowHandle -Process $qemuProcess
                if ($qemuWindowHandle -ne [IntPtr]::Zero) {
                    Save-WindowScreenshot -WindowHandle $qemuWindowHandle -Path $GuiScreenshotPath
                    $guiScreenshotCaptured = $true
                    Write-Host "[probe] GUI screenshot captured: $GuiScreenshotPath" -ForegroundColor Cyan
                } else {
                    Write-Host "[probe] GUI screenshot skipped: QEMU window handle was not available." -ForegroundColor Yellow
                }
            }
        } catch {
            Write-Host "[probe] GUI screenshot capture failed: $_" -ForegroundColor Yellow
        }
    }

    if ($qemuProcess -and -not $qemuProcess.HasExited) {
        Stop-Process -Id $qemuProcess.Id -Force
        $qemuProcess.WaitForExit()
    }

    if (-not (Test-Path $serialLog)) {
        throw "QEMU did not create the expected serial log: $serialLog"
    }

    $serialText = [System.IO.File]::ReadAllText($serialLog)
    $serialLines = $serialText -split "`r?`n"

    $validRun = $true
    foreach ($required in @(
        $runId,
        'SMAIN_DIAG_NORMAL_DESKTOP_STEP_PROBE=1',
        'SMAIN_DISPATCH_REASON=NORMAL_DESKTOP_UEFI_STEP_PROBE'
    )) {
        if (-not $serialText.Contains($required)) {
            $validRun = $false
            Write-Host "[probe] Missing required breadcrumb: $required" -ForegroundColor Red
        }
    }

    $probeEnterRegex = [regex]'NORM_STEP_[A-Z0-9_]+_ENTER'
    $probeExitRegex = [regex]'NORM_STEP_[A-Z0-9_]+_EXIT'
    $enterMatches = $probeEnterRegex.Matches($serialText)
    $lastEnter = if ($enterMatches.Count -gt 0) { $enterMatches[$enterMatches.Count - 1].Value } else { $null }
    $matchingExit = if ($lastEnter) { $lastEnter -replace '_ENTER$', '_EXIT' } else { $null }
    $matchingExitPresent = $matchingExit -and $serialText.Contains($matchingExit)

    $faultLines = $serialLines | Where-Object { $_ -match 'VEC=|CR2=|ERR=|RIP=|#PF|PAGE FAULT|FAULT|EXCEPTION' }
    $vecLines = $serialLines | Where-Object { $_ -match 'VEC=' }
    $cr2Lines = $serialLines | Where-Object { $_ -match 'CR2=' }
    $errLines = $serialLines | Where-Object { $_ -match 'ERR=' }
    $ripLines = $serialLines | Where-Object { $_ -match 'RIP=' }
    $step8SafePlaceholdersEnabled = $serialText.Contains('SMAIN_DIAG_NORMAL_DESKTOP_STEP_PROBE_SAFE_PLACEHOLDERS_UNTIL_STEP10=1')
    $step10RedPlaceholderEnabled = $serialText.Contains('SMAIN_DIAG_NORMAL_DESKTOP_STEP10_RED_FILLRECT_PLACEHOLDER=1')
    $step10GreenPlaceholderEnabled = $serialText.Contains('SMAIN_DIAG_NORMAL_DESKTOP_STEP10_GREEN_FILLRECT_PLACEHOLDER=1')
    $step10WhitePlaceholderEnabled = $serialText.Contains('SMAIN_DIAG_NORMAL_DESKTOP_STEP10_WHITE_FILLRECT_PLACEHOLDER=1')
    $step11SkipWindowTraversalEnabled = $serialText.Contains('SMAIN_DIAG_NORMAL_DESKTOP_STEP_PROBE_SKIP_WINDOW_TRAVERSAL=1')
    $step12SafeFontPlaceholderEnabled = $serialText.Contains('SMAIN_DIAG_NORMAL_DESKTOP_STEP_PROBE_SAFE_FONT_PLACEHOLDER=1')
    $step8BorderExitPresent = $serialText.Contains('NORM_STEP_008_BUTTON_BORDER_EXIT')
    $step9EnterPresent = $serialText.Contains('NORM_STEP_009_ENTER')
    $step10RedDrawEnterPresent = $serialText.Contains('NORM_STEP_010_RED_DRAW_ENTER')
    $step10RedDrawExitPresent = $serialText.Contains('NORM_STEP_010_RED_DRAW_EXIT')
    $step10GreenAEnterPresent = $serialText.Contains('NORM_STEP_010_GREEN_A_ENTER')
    $step10GreenAExitPresent = $serialText.Contains('NORM_STEP_010_GREEN_A_EXIT')
    $step10GreenBEnterPresent = $serialText.Contains('NORM_STEP_010_GREEN_B_ENTER')
    $step10GreenBExitPresent = $serialText.Contains('NORM_STEP_010_GREEN_B_EXIT')
    $step10GreenCEnterPresent = $serialText.Contains('NORM_STEP_010_GREEN_C_ENTER')
    $step10GreenCExitPresent = $serialText.Contains('NORM_STEP_010_GREEN_C_EXIT')
    $step10GreenImageOkPresent = $serialText.Contains('NORM_STEP_010_GREEN_IMAGE=OK')
    $step10GreenDimensionsReadPresent = $serialText.Contains('NORM_STEP_010_GREEN_DIMENSIONS_READ')
    $step10GreenReceiverCachedPresent = $serialText.Contains('NORM_STEP_010_GREEN_RECEIVER=CACHED')
    $step10GreenDrawOverloadPresent = $serialText.Contains('NORM_STEP_010_GREEN_DRAW_OVERLOAD=DrawImage(int,int,Image,bool-default:true)')
    $step10GreenRawDataOkPresent = $serialText.Contains('NORM_STEP_010_GREEN_RAWDATA=OK')
    $step10GreenFirstPixelReadEnterPresent = $serialText.Contains('NORM_STEP_010_GREEN_FIRST_PIXEL_READ_ENTER')
    $step10GreenFirstPixelReadExitPresent = $serialText.Contains('NORM_STEP_010_GREEN_FIRST_PIXEL_READ_EXIT')
    $step10GreenFirstPixelWriteEnterPresent = $serialText.Contains('NORM_STEP_010_GREEN_FIRST_PIXEL_WRITE_ENTER')
    $step10GreenFirstPixelWriteExitPresent = $serialText.Contains('NORM_STEP_010_GREEN_FIRST_PIXEL_WRITE_EXIT')
    $step10WhiteAEnterPresent = $serialText.Contains('NORM_STEP_010_WHITE_A_ENTER')
    $step10WhiteAExitPresent = $serialText.Contains('NORM_STEP_010_WHITE_A_EXIT')
    $step10WhiteBEnterPresent = $serialText.Contains('NORM_STEP_010_WHITE_B_ENTER')
    $step10WhiteBExitPresent = $serialText.Contains('NORM_STEP_010_WHITE_B_EXIT')
    $step10WhiteCEnterPresent = $serialText.Contains('NORM_STEP_010_WHITE_C_ENTER')
    $step10WhiteCExitPresent = $serialText.Contains('NORM_STEP_010_WHITE_C_EXIT')
    $step10WhiteDrawEnterPresent = $serialText.Contains('NORM_STEP_010_WHITE_DRAW_ENTER')
    $step10WhiteDrawExitPresent = $serialText.Contains('NORM_STEP_010_WHITE_DRAW_EXIT')
    $step11EnterPresent = $serialText.Contains('NORM_STEP_011_ENTER')
    $step11SkipPresent = $serialText.Contains('NORM_STEP_011_SKIP_ZERO_WINDOWS')
    $step11AEnterPresent = $serialText.Contains('NORM_STEP_011_A_ENTER')
    $step11AExitPresent = $serialText.Contains('NORM_STEP_011_A_EXIT')
    $step11BEnterPresent = $serialText.Contains('NORM_STEP_011_B_ENTER')
    $step11BExitPresent = $serialText.Contains('NORM_STEP_011_B_EXIT')
    $step11CEnterPresent = $serialText.Contains('NORM_STEP_011_C_ENTER')
    $step11CExitPresent = $serialText.Contains('NORM_STEP_011_C_EXIT')
    $step11ExitPresent = $serialText.Contains('NORM_STEP_011_EXIT')
    $step12EnterPresent = $serialText.Contains('NORM_STEP_012_ENTER')
    $step12ExitPresent = $serialText.Contains('NORM_STEP_012_EXIT')
    $step12SafeFontPlaceholderPresent = $serialText.Contains('NORM_STEP_012_SAFE_FONT_PLACEHOLDER=1')
    $step12SafeFontPlaceholderExitPresent = $serialText.Contains('NORM_STEP_012_SAFE_FONT_PLACEHOLDER_EXIT')
    $step14EnterPresent = $serialText.Contains('NORM_STEP_014_ENTER')
    $step14ExitPresent = $serialText.Contains('NORM_STEP_014_EXIT')
    $step13SkipCursorDrawEnabled = $serialText.Contains('SMAIN_DIAG_NORMAL_DESKTOP_STEP_PROBE_SKIP_CURSOR_DRAW=1')
    $step13CursorPlaceholderEnabled = $serialText.Contains('SMAIN_DIAG_NORMAL_DESKTOP_STEP_PROBE_CURSOR_PLACEHOLDER=1')
    $step13RealCursorImageRenderingEnabled = $serialText.Contains('SMAIN_DIAG_UEFI_PROBE_REAL_CURSOR_IMAGE_RENDERING=1')
    $step13AEnterPresent = $serialText.Contains('NORM_STEP_013_A_ENTER')
    $step13AExitPresent = $serialText.Contains('NORM_STEP_013_A_EXIT')
    $step13BEnterPresent = $serialText.Contains('NORM_STEP_013_B_ENTER')
    $step13BExitPresent = $serialText.Contains('NORM_STEP_013_B_EXIT')
    $step13CEnterPresent = $serialText.Contains('NORM_STEP_013_C_ENTER')
    $step13CExitPresent = $serialText.Contains('NORM_STEP_013_C_EXIT')
    $step13ExitPresent = $serialText.Contains('NORM_STEP_013_EXIT')
    $step13CursorEnabledPresent = $serialText.Contains('NORM_STEP_013_CURSOR_ENABLED=1') -or $serialText.Contains('NORM_STEP_013_CURSOR_ENABLED=0')
    $step13CursorEnabledOnPresent = $serialText.Contains('NORM_STEP_013_CURSOR_ENABLED=1')
    $step13CursorEnabledOffPresent = $serialText.Contains('NORM_STEP_013_CURSOR_ENABLED=0')
    $step13CursorPlaceholderStatePresent = $serialText.Contains('NORM_STEP_013_CURSOR_PLACEHOLDER=1') -or $serialText.Contains('NORM_STEP_013_CURSOR_PLACEHOLDER=0')
    $step13CursorPlaceholderOnPresent = $serialText.Contains('NORM_STEP_013_CURSOR_PLACEHOLDER=1')
    $step13CursorPlaceholderOffPresent = $serialText.Contains('NORM_STEP_013_CURSOR_PLACEHOLDER=0')
    $step13RealCursorImageRenderingStatePresent = $serialText.Contains('NORM_STEP_013_REAL_CURSOR_IMAGE_RENDERING=1') -or $serialText.Contains('NORM_STEP_013_REAL_CURSOR_IMAGE_RENDERING=0')
    $step13RealCursorImageRenderingOnPresent = $serialText.Contains('NORM_STEP_013_REAL_CURSOR_IMAGE_RENDERING=1')
    $step13RealCursorImageRenderingOffPresent = $serialText.Contains('NORM_STEP_013_REAL_CURSOR_IMAGE_RENDERING=0')
    $step13CursorPlaceholderEnterPresent = $serialText.Contains('NORM_STEP_013_CURSOR_PLACEHOLDER_ENTER')
    $step13CursorPlaceholderExitPresent = $serialText.Contains('NORM_STEP_013_CURSOR_PLACEHOLDER_EXIT')
    $step13CursorImageEnterPresent = $serialText.Contains('NORM_STEP_013_CURSOR_IMG_ENTER')
    $step13CursorImageExitPresent = $serialText.Contains('NORM_STEP_013_CURSOR_IMG_EXIT')
    $cursorDrawBusyEnterPresent = $serialText.Contains('CURSOR_DRAW_BUSY_ENTER')
    $cursorDrawBusyImageOkPresent = $serialText.Contains('CURSOR_DRAW_BUSY_IMAGE_OK')
    $cursorDrawBusyDimsEnterPresent = $serialText.Contains('CURSOR_DRAW_BUSY_DIMS_ENTER')
    $cursorDrawBusyDimsExitPresent = $serialText.Contains('CURSOR_DRAW_BUSY_DIMS_EXIT')
    $cursorDrawBusyDrawImageEnterPresent = $serialText.Contains('CURSOR_DRAW_BUSY_DRAWIMAGE_ENTER')
    $cursorDrawBusyDrawImageExitPresent = $serialText.Contains('CURSOR_DRAW_BUSY_DRAWIMAGE_EXIT')
    $cursorDrawBusyExitPresent = $serialText.Contains('CURSOR_DRAW_BUSY_EXIT')
    $cursorDrawFallbackEnterPresent = $serialText.Contains('CURSOR_DRAW_FALLBACK_ENTER')
    $cursorDrawFallbackImageOkPresent = $serialText.Contains('CURSOR_DRAW_FALLBACK_IMAGE_OK')
    $cursorDrawFallbackDimsEnterPresent = $serialText.Contains('CURSOR_DRAW_FALLBACK_DIMS_ENTER')
    $cursorDrawFallbackDimsExitPresent = $serialText.Contains('CURSOR_DRAW_FALLBACK_DIMS_EXIT')
    $cursorDrawFallbackDrawImageEnterPresent = $serialText.Contains('CURSOR_DRAW_FALLBACK_DRAWIMAGE_ENTER')
    $cursorDrawFallbackDrawImageExitPresent = $serialText.Contains('CURSOR_DRAW_FALLBACK_DRAWIMAGE_EXIT')
    $cursorDrawFallbackExitPresent = $serialText.Contains('CURSOR_DRAW_FALLBACK_EXIT')
    $cursorImgWrapperEnterPresent = $serialText.Contains('CURSOR_IMG_WRAPPER_ENTER')
    $cursorImgBeforeBodyCallPresent = $serialText.Contains('CURSOR_IMG_BEFORE_BODY_CALL')
    $cursorImgEmptyBodyEnterPresent = $serialText.Contains('CURSOR_IMG_EMPTY_BODY_ENTER')
    $cursorImgEmptyBodyExitPresent = $serialText.Contains('CURSOR_IMG_EMPTY_BODY_EXIT')
    $cursorImgInlineBodyEnterPresent = $serialText.Contains('CURSOR_IMG_INLINE_BODY_ENTER')
    $cursorImgInlineBeforeFramebufferGraphicsPresent = $serialText.Contains('CURSOR_IMG_INLINE_BEFORE_FRAMEBUFFER_GRAPHICS')
    $cursorImgInlineAfterFramebufferGraphicsPresent = $serialText.Contains('CURSOR_IMG_INLINE_AFTER_FRAMEBUFFER_GRAPHICS')
    $cursorImgInlineExitPresent = $serialText.Contains('CURSOR_IMG_INLINE_EXIT')
    $cursorImgStaticBodyEnterPresent = $serialText.Contains('CURSOR_IMG_STATIC_BODY_ENTER')
    $cursorImgStaticAfterFramebufferGraphicsPresent = $serialText.Contains('CURSOR_IMG_STATIC_AFTER_FRAMEBUFFER_GRAPHICS')
    $cursorImgStaticBodyExitPresent = $serialText.Contains('CURSOR_IMG_STATIC_BODY_EXIT')
    $cursorSrcCheckEnterPresent = $serialText.Contains('CURSOR_SRC_CHECK_ENTER')
    $cursorSrcCheckExitPresent = $serialText.Contains('CURSOR_SRC_CHECK_EXIT')
    $cursorSrcCursorMovingNullPresent = $serialText.Contains('CURSOR_SRC_CURSOR_MOVING_NULL')
    $cursorSrcCursorMovingOkPresent = $serialText.Contains('CURSOR_SRC_CURSOR_MOVING_OK')
    $cursorSrcCursorBusyNullPresent = $serialText.Contains('CURSOR_SRC_CURSOR_BUSY_NULL')
    $cursorSrcCursorBusyOkPresent = $serialText.Contains('CURSOR_SRC_CURSOR_BUSY_OK')
    $cursorSrcFallbackAvailablePresent = $serialText.Contains('CURSOR_SRC_FALLBACK_AVAILABLE')
    $cursorSrcFallbackNullPresent = $serialText.Contains('CURSOR_SRC_FALLBACK_NULL')
    $cursorSrcLoadPngEnterPresent = $serialText.Contains('CURSOR_SRC_LOADPNG_ENTER')
    $cursorSrcLoadPngExitPresent = $serialText.Contains('CURSOR_SRC_LOADPNG_EXIT')
    $cursorSrcLoadPngNullPresent = $serialText.Contains('CURSOR_SRC_LOADPNG_NULL')
    $cursorSrcLoadPngOkPresent = $serialText.Contains('CURSOR_SRC_LOADPNG_OK')
    $cursorSrcLoadPngDimsEnterPresent = $serialText.Contains('CURSOR_SRC_LOADPNG_DIMS_ENTER')
    $cursorSrcLoadPngDimsExitPresent = $serialText.Contains('CURSOR_SRC_LOADPNG_DIMS_EXIT')
    $cursorImgBodyEnterPresent = $serialText.Contains('CURSOR_IMG_BODY_ENTER')
    $cursorImgBeforeFramebufferGraphicsPresent = $serialText.Contains('CURSOR_IMG_BEFORE_FRAMEBUFFER_GRAPHICS')
    $cursorImgAfterFramebufferGraphicsPresent = $serialText.Contains('CURSOR_IMG_AFTER_FRAMEBUFFER_GRAPHICS')
    $cursorImgBeforeImageObjectPresent = $serialText.Contains('CURSOR_IMG_BEFORE_IMAGE_OBJECT')
    $cursorImgAfterImageObjectPresent = $serialText.Contains('CURSOR_IMG_AFTER_IMAGE_OBJECT')
    $cursorImgBeforeDrawImagePresent = $serialText.Contains('CURSOR_IMG_BEFORE_DRAWIMAGE')
    $cursorImgAfterDrawImagePresent = $serialText.Contains('CURSOR_IMG_AFTER_DRAWIMAGE')
    $cursorImgWrapperExitPresent = $serialText.Contains('CURSOR_IMG_WRAPPER_EXIT')
    $cursorImgProbeEnterPresent = $serialText.Contains('CURSOR_IMG_PROBE_ENTER')
    $cursorImgGraphicsEnterPresent = $serialText.Contains('CURSOR_IMG_GRAPHICS_ENTER')
    $cursorImgGraphicsExitPresent = $serialText.Contains('CURSOR_IMG_GRAPHICS_EXIT')
    $cursorImgSourceImageEnterPresent = $serialText.Contains('CURSOR_IMG_SOURCE_IMAGE_ENTER')
    $cursorImgSourceImageExitPresent = $serialText.Contains('CURSOR_IMG_SOURCE_IMAGE_EXIT')
    $cursorImgObjectCheckEnterPresent = $serialText.Contains('CURSOR_IMG_OBJECT_CHECK_ENTER')
    $cursorImgObjectCheckExitPresent = $serialText.Contains('CURSOR_IMG_OBJECT_CHECK_EXIT')
    $cursorImgBoundsCheckEnterPresent = $serialText.Contains('CURSOR_IMG_BOUNDS_CHECK_ENTER')
    $cursorImgBoundsCheckExitPresent = $serialText.Contains('CURSOR_IMG_BOUNDS_CHECK_EXIT')
    $cursorImgDrawImageEnterPresent = $serialText.Contains('CURSOR_IMG_DRAWIMAGE_ENTER')
    $cursorImgDrawImageExitPresent = $serialText.Contains('CURSOR_IMG_DRAWIMAGE_EXIT')
    $cursorImgFirstDestPixelWriteEnterPresent = $serialText.Contains('CURSOR_IMG_FIRST_DEST_PIXEL_WRITE_ENTER')
    $cursorImgFirstDestPixelWriteExitPresent = $serialText.Contains('CURSOR_IMG_FIRST_DEST_PIXEL_WRITE_EXIT')
    $cursorImgProbeExitPresent = $serialText.Contains('CURSOR_IMG_PROBE_EXIT')
    $step13CursorDrawEnterPresent = $serialText.Contains('NORM_STEP_013_CURSOR_DRAW_ENTER')
    $step13CursorDrawPrimitiveEnterPresent = $serialText.Contains('NORM_STEP_013_CURSOR_DRAW_PRIMITIVE_ENTER')
    $step13CursorDrawPrimitiveExitPresent = $serialText.Contains('NORM_STEP_013_CURSOR_DRAW_PRIMITIVE_EXIT')
    $step13CursorDrawExitPresent = $serialText.Contains('NORM_STEP_013_CURSOR_DRAW_EXIT')
    $step13CursorDrawSkippedPresent = $serialText.Contains('NORM_STEP_013_CURSOR_DRAW_SKIPPED')
    $step13CursorImageNullPresent = $serialText.Contains('NORM_STEP_013_CURSOR_IMAGE=NULL')
    $step13CursorImageOkPresent = $serialText.Contains('NORM_STEP_013_CURSOR_IMAGE=OK')
    $step13CursorMovingNullPresent = $serialText.Contains('NORM_STEP_013_CURSOR_MOVING=NULL')
    $step13CursorMovingOkPresent = $serialText.Contains('NORM_STEP_013_CURSOR_MOVING=OK')
    $step13CursorBusyNullPresent = $serialText.Contains('NORM_STEP_013_CURSOR_BUSY=NULL')
    $step13CursorBusyOkPresent = $serialText.Contains('NORM_STEP_013_CURSOR_BUSY=OK')
    $step13CursorXBoundsOkPresent = $serialText.Contains('NORM_STEP_013_CURSOR_X_BOUNDS=OK')
    $step13CursorXBoundsOutPresent = $serialText.Contains('NORM_STEP_013_CURSOR_X_BOUNDS=OUT')
    $step13CursorYBoundsOkPresent = $serialText.Contains('NORM_STEP_013_CURSOR_Y_BOUNDS=OK')
    $step13CursorYBoundsOutPresent = $serialText.Contains('NORM_STEP_013_CURSOR_Y_BOUNDS=OUT')
    $step14AEnterPresent = $serialText.Contains('NORM_STEP_014_A_ENTER')
    $step14AExitPresent = $serialText.Contains('NORM_STEP_014_A_EXIT')
    $step14BEnterPresent = $serialText.Contains('NORM_STEP_014_B_ENTER')
    $step14BExitPresent = $serialText.Contains('NORM_STEP_014_B_EXIT')
    $step14CEnterPresent = $serialText.Contains('NORM_STEP_014_C_ENTER')
    $step14CExitPresent = $serialText.Contains('NORM_STEP_014_C_EXIT')
    $step14PresentEnterPresent = $serialText.Contains('NORM_STEP_014_PRESENT_ENTER')
    $step14PresentExitPresent = $serialText.Contains('NORM_STEP_014_PRESENT_EXIT')
    $step14FrameBufferAcquireEnterPresent = $serialText.Contains('NORM_STEP_014_FRAMEBUFFER_ACQUIRE_ENTER')
    $step14FrameBufferAcquireOkPresent = $serialText.Contains('NORM_STEP_014_FRAMEBUFFER_ACQUIRE=OK')
    $step14FrameBufferAcquireNullPresent = $serialText.Contains('NORM_STEP_014_FRAMEBUFFER_ACQUIRE=NULL')
    $step14FrameBufferReadyOkPresent = $serialText.Contains('NORM_STEP_014_FRAMEBUFFER_READY=OK')
    $step14FrameBufferReadyInvalidPresent = $serialText.Contains('NORM_STEP_014_FRAMEBUFFER_READY=INVALID')
    $step14PresentTargetGraphicsPresent = $serialText.Contains('NORM_STEP_014_PRESENT_TARGET=GRAPHICS')
    $step14PresentTargetNullPresent = $serialText.Contains('NORM_STEP_014_PRESENT_TARGET=NULL')
    $step14FlushInvalidateEnterPresent = $serialText.Contains('NORM_STEP_014_FLUSH_INVALIDATE_ENTER')
    $step14FlushInvalidateExitPresent = $serialText.Contains('NORM_STEP_014_FLUSH_INVALIDATE_EXIT')
    $step14FrameCompletePresent = $serialText.Contains('NORM_STEP_014_FRAME_COMPLETE')
    $step14LoopEnterPresent = $serialText.Contains('NORM_STEP_014_LOOP_ENTER')
    $step11MarkerSequence = @(
        'NORM_STEP_011_ENTER'
        'NORM_STEP_011_SKIP_ZERO_WINDOWS'
        'NORM_STEP_011_A_ENTER'
        'NORM_STEP_011_WINDOWS=NULL'
        'NORM_STEP_011_WINDOWS=OK'
        'NORM_STEP_011_WINDOWS_COUNT'
        'NORM_STEP_011_WINDOWS_ZERO=1'
        'NORM_STEP_011_WINDOWS_ZERO=0'
        'NORM_STEP_011_A_EXIT'
        'NORM_STEP_011_B_ENTER'
        'NORM_STEP_011_B_TRAVERSAL_BEGIN'
        'NORM_STEP_011_B_FIRST_LOOP_CONDITION_ENTER'
        'NORM_STEP_011_B_FIRST_LOOP_CONDITION_FALSE'
        'NORM_STEP_011_B_FIRST_LOOP_CONDITION_TRUE'
        'NORM_STEP_011_B_PER_WINDOW_BODY_SKIPPED_ZERO_WINDOWS'
        'NORM_STEP_011_B_PER_WINDOW_BODY_ENTER'
        'NORM_STEP_011_B_CALLSITE=WindowManager.DrawAllExceptTaskManager()'
        'NORM_STEP_011_B_PER_WINDOW_BODY_EXIT'
        'NORM_STEP_011_B_TRAVERSAL_EXIT'
        'NORM_STEP_011_B_EXIT'
        'NORM_STEP_011_C_ENTER'
        'NORM_STEP_011_C_CALLSITE=WindowManager.DrawTaskManager()'
        'NORM_STEP_011_C_CALLSITE_EXIT'
        'NORM_STEP_011_C_SKIP_DRAW_TASK_MANAGER'
        'NORM_STEP_011_C_EXIT'
        'NORM_STEP_011_DRAWALL_ENTER'
        'NORM_STEP_011_DRAWALL_WINDOWS=NULL'
        'NORM_STEP_011_DRAWALL_WINDOWS=OK'
        'NORM_STEP_011_DRAWALL_COUNT'
        'NORM_STEP_011_DRAWALL_ZERO=1'
        'NORM_STEP_011_DRAWALL_ZERO=0'
        'NORM_STEP_011_DRAWALL_LOOP_BEGIN'
        'NORM_STEP_011_DRAWALL_LOOP_CONDITION_TRUE'
        'NORM_STEP_011_DRAWALL_WINDOW_BODY_ENTER'
        'NORM_STEP_011_DRAWALL_WINDOW_BODY_SKIP_VISIBLE'
        'NORM_STEP_011_DRAWALL_WINDOW_BODY_SKIP_TASKMANAGER'
        'NORM_STEP_011_DRAWALL_BEFORE_ONDRAW'
        'NORM_STEP_011_DRAWALL_AFTER_ONDRAW'
        'NORM_STEP_011_DRAWALL_EXIT'
        'NORM_STEP_011_DRAWTASK_ENTER'
        'NORM_STEP_011_DRAWTASK_WINDOWS=NULL'
        'NORM_STEP_011_DRAWTASK_WINDOWS=OK'
        'NORM_STEP_011_DRAWTASK_COUNT'
        'NORM_STEP_011_DRAWTASK_ZERO=1'
        'NORM_STEP_011_DRAWTASK_ZERO=0'
        'NORM_STEP_011_DRAWTASK_LOOP_BEGIN'
        'NORM_STEP_011_DRAWTASK_LOOP_CONDITION_TRUE'
        'NORM_STEP_011_DRAWTASK_WINDOW_BODY_ENTER'
        'NORM_STEP_011_DRAWTASK_WINDOW_BODY_SKIP_VISIBLE'
        'NORM_STEP_011_DRAWTASK_BEFORE_ONDRAW'
        'NORM_STEP_011_DRAWTASK_AFTER_ONDRAW'
        'NORM_STEP_011_DRAWTASK_EXIT'
        'NORM_STEP_011_EXIT'
    )
    $step11DeepestMarker = $null
    foreach ($marker in $step11MarkerSequence) {
        if ($serialText.Contains($marker)) {
            $step11DeepestMarker = $marker
        }
    }
    $step13MarkerSequence = @(
        'NORM_STEP_013_ENTER'
        'NORM_STEP_013_A_ENTER'
        'NORM_STEP_013_PROBE_SKIP_CURSOR_DRAW=1'
        'NORM_STEP_013_PROBE_SKIP_CURSOR_DRAW=0'
        'NORM_STEP_013_GLOBAL_SKIP_CURSOR_DRAW=1'
        'NORM_STEP_013_GLOBAL_SKIP_CURSOR_DRAW=0'
        'NORM_STEP_013_CURSOR_PLACEHOLDER=1'
        'NORM_STEP_013_CURSOR_PLACEHOLDER=0'
        'NORM_STEP_013_REAL_CURSOR_IMAGE_RENDERING=1'
        'NORM_STEP_013_REAL_CURSOR_IMAGE_RENDERING=0'
        'NORM_STEP_013_CURSOR_IMAGE=NULL'
        'NORM_STEP_013_CURSOR_IMAGE=OK'
        'NORM_STEP_013_CURSOR_MOVING=NULL'
        'NORM_STEP_013_CURSOR_MOVING=OK'
        'NORM_STEP_013_CURSOR_BUSY=NULL'
        'NORM_STEP_013_CURSOR_BUSY=OK'
        'NORM_STEP_013_A_EXIT'
        'NORM_STEP_013_B_ENTER'
        'NORM_STEP_013_FRAMEBUFFER_SIZE=INVALID'
        'NORM_STEP_013_FRAMEBUFFER_SIZE=OK'
        'NORM_STEP_013_CURSOR_X_BOUNDS=OUT'
        'NORM_STEP_013_CURSOR_X_BOUNDS=OK'
        'NORM_STEP_013_CURSOR_Y_BOUNDS=OUT'
        'NORM_STEP_013_CURSOR_Y_BOUNDS=OK'
        'NORM_STEP_013_B_EXIT'
        'NORM_STEP_013_C_ENTER'
        'NORM_STEP_013_CURSOR_ENABLED=0'
        'NORM_STEP_013_CURSOR_ENABLED=1'
        'NORM_STEP_013_CURSOR_PLACEHOLDER_ENTER'
        'NORM_STEP_013_CURSOR_PLACEHOLDER_EXIT'
        'NORM_STEP_013_CURSOR_DRAW_SKIPPED'
        'NORM_STEP_013_CURSOR_IMG_ENTER'
        'NORM_STEP_013_CURSOR_IMG_EXIT'
        'NORM_STEP_013_CURSOR_DRAW_ENTER'
        'NORM_STEP_013_CURSOR_DRAW_PRIMITIVE_ENTER'
        'NORM_STEP_013_CURSOR_DRAW_PRIMITIVE_EXIT'
        'NORM_STEP_013_CURSOR_DRAW_EXIT'
        'NORM_STEP_013_C_EXIT'
        'NORM_STEP_013_EXIT'
    )
    $step13DeepestMarker = $null
    foreach ($marker in $step13MarkerSequence) {
        if ($serialText.Contains($marker)) {
            $step13DeepestMarker = $marker
        }
    }
    $cursorImgMarkerSequence = @(
        'CURSOR_DRAW_BUSY_ENTER'
        'CURSOR_DRAW_BUSY_IMAGE_NULL'
        'CURSOR_DRAW_BUSY_IMAGE_OK'
        'CURSOR_DRAW_BUSY_DIMS_ENTER'
        'CURSOR_DRAW_BUSY_DIMS_EXIT'
        'CURSOR_DRAW_BUSY_DRAWIMAGE_ENTER'
        'CURSOR_DRAW_BUSY_DRAWIMAGE_EXIT'
        'CURSOR_DRAW_BUSY_EXIT'
        'CURSOR_DRAW_FALLBACK_ENTER'
        'CURSOR_DRAW_FALLBACK_IMAGE_NULL'
        'CURSOR_DRAW_FALLBACK_IMAGE_OK'
        'CURSOR_DRAW_FALLBACK_DIMS_ENTER'
        'CURSOR_DRAW_FALLBACK_DIMS_EXIT'
        'CURSOR_DRAW_FALLBACK_DRAWIMAGE_ENTER'
        'CURSOR_DRAW_FALLBACK_DRAWIMAGE_EXIT'
        'CURSOR_DRAW_FALLBACK_EXIT'
        'CURSOR_IMG_WRAPPER_ENTER'
        'CURSOR_IMG_PROBE_ENTER'
        'CURSOR_IMG_BEFORE_BODY_CALL'
        'CURSOR_IMG_EMPTY_BODY_ENTER'
        'CURSOR_IMG_EMPTY_BODY_EXIT'
        'CURSOR_IMG_INLINE_BODY_ENTER'
        'CURSOR_IMG_INLINE_BEFORE_FRAMEBUFFER_GRAPHICS'
        'CURSOR_IMG_INLINE_AFTER_FRAMEBUFFER_GRAPHICS'
        'CURSOR_IMG_INLINE_EXIT'
        'CURSOR_IMG_STATIC_BODY_ENTER'
        'CURSOR_IMG_STATIC_AFTER_FRAMEBUFFER_GRAPHICS'
        'CURSOR_IMG_STATIC_BODY_EXIT'
        'CURSOR_IMG_STATIC_IMAGE_REF_ENTER'
        'CURSOR_IMG_STATIC_IMAGE_REF_EXIT'
        'CURSOR_IMG_STATIC_IMAGE_NULL_CHECK_ENTER'
        'CURSOR_IMG_STATIC_IMAGE_REF=NULL'
        'CURSOR_IMG_STATIC_IMAGE_REF=OK'
        'CURSOR_IMG_STATIC_IMAGE_NULL_CHECK_EXIT'
        'CURSOR_IMG_STATIC_DIMS_ENTER'
        'CURSOR_IMG_STATIC_DIMS_EXIT'
        'CURSOR_IMG_STATIC_RAWDATA_REF_ENTER'
        'CURSOR_IMG_STATIC_RAWDATA_REF_EXIT'
        'CURSOR_IMG_STATIC_FIRST_PIXEL_ENTER'
        'CURSOR_IMG_STATIC_FIRST_PIXEL_EXIT'
        'CURSOR_IMG_BODY_ENTER'
        'CURSOR_IMG_BEFORE_FRAMEBUFFER_GRAPHICS'
        'CURSOR_IMG_AFTER_FRAMEBUFFER_GRAPHICS'
        'CURSOR_IMG_BEFORE_IMAGE_OBJECT'
        'CURSOR_IMG_AFTER_IMAGE_OBJECT'
        'CURSOR_IMG_BEFORE_DRAWIMAGE'
        'CURSOR_IMG_AFTER_DRAWIMAGE'
        'CURSOR_IMG_GRAPHICS_ENTER'
        'CURSOR_IMG_GRAPHICS_EXIT'
        'CURSOR_IMG_SOURCE_IMAGE_ENTER'
        'CURSOR_IMG_SOURCE_IMAGE_EXIT'
        'CURSOR_IMG_OBJECT_CHECK_ENTER'
        'CURSOR_IMG_GFX=OK'
        'CURSOR_IMG_GFX=NULL'
        'CURSOR_IMG_IMAGE=OK'
        'CURSOR_IMG_IMAGE=NULL'
        'CURSOR_IMG_CURSOR=OK'
        'CURSOR_IMG_CURSOR=NULL'
        'CURSOR_IMG_CURSOR_MOVING=OK'
        'CURSOR_IMG_CURSOR_MOVING=NULL'
        'CURSOR_IMG_CURSOR_BUSY=OK'
        'CURSOR_IMG_CURSOR_BUSY=NULL'
        'CURSOR_IMG_WIDTH'
        'CURSOR_IMG_HEIGHT'
        'CURSOR_IMG_RAWDATA=OK'
        'CURSOR_IMG_RAWDATA=NULL'
        'CURSOR_IMG_FIRST_SOURCE_PIXEL'
        'CURSOR_IMG_FIRST_SOURCE_ALPHA'
        'CURSOR_IMG_OBJECT_CHECK_EXIT'
        'CURSOR_IMG_BOUNDS_CHECK_ENTER'
        'CURSOR_IMG_CURSOR_X'
        'CURSOR_IMG_CURSOR_Y'
        'CURSOR_IMG_GFX_W'
        'CURSOR_IMG_GFX_H'
        'CURSOR_IMG_GFX_VM'
        'CURSOR_IMG_CLIP=OUT'
        'CURSOR_IMG_CLIP=IN'
        'CURSOR_IMG_DRAW_X0'
        'CURSOR_IMG_DRAW_Y0'
        'CURSOR_IMG_DRAW_X1'
        'CURSOR_IMG_DRAW_Y1'
        'CURSOR_IMG_BOUNDS_CHECK_EXIT'
        'CURSOR_IMG_DRAWIMAGE_ENTER'
        'CURSOR_IMG_DRAWIMAGE_OVERLOAD=DrawImage(int,int,Image,bool-default:true)'
        'CURSOR_IMG_ALPHA_BLEND=1'
        'CURSOR_IMG_DEST_PIXEL_READ_ENTER'
        'CURSOR_IMG_DEST_PIXEL_READ_EXIT'
        'CURSOR_IMG_FIRST_DEST_PIXEL'
        'CURSOR_IMG_FIRST_DEST_PIXEL_WRITE_ENTER'
        'CURSOR_IMG_FIRST_DEST_PIXEL_WRITE_EXIT'
        'CURSOR_IMG_DRAWIMAGE_EXIT'
        'CURSOR_IMG_WRAPPER_EXIT'
        'CURSOR_IMG_PROBE_EXIT'
    )
    $cursorImgDeepestMarker = $null
    foreach ($marker in $cursorImgMarkerSequence) {
        if ($serialText.Contains($marker)) {
            $cursorImgDeepestMarker = $marker
        }
    }
    $step14MarkerSequence = @(
        'NORM_STEP_014_ENTER'
        'NORM_STEP_014_A_ENTER'
        'NORM_STEP_014_FRAMEBUFFER_ACQUIRE_ENTER'
        'NORM_STEP_014_FRAMEBUFFER_ACQUIRE=OK'
        'NORM_STEP_014_FRAMEBUFFER_ACQUIRE=NULL'
        'NORM_STEP_014_FRAMEBUFFER_READY=OK'
        'NORM_STEP_014_FRAMEBUFFER_READY=INVALID'
        'NORM_STEP_014_FRAMEBUFFER_ACQUIRE_EXIT'
        'NORM_STEP_014_A_EXIT'
        'NORM_STEP_014_B_ENTER'
        'NORM_STEP_014_PRESENT_TARGET=GRAPHICS'
        'NORM_STEP_014_PRESENT_TARGET=NULL'
        'NORM_STEP_014_B_EXIT'
        'NORM_STEP_014_C_ENTER'
        'NORM_STEP_014_FLUSH_INVALIDATE_ENTER'
        'NORM_STEP_014_FLUSH_INVALIDATE_EXIT'
        'NORM_STEP_014_C_EXIT'
        'NORM_STEP_014_PRESENT_ENTER'
        'NORM_STEP_014_PRESENT_CALLSITE=Framebuffer.Update()'
        'NORM_STEP_014_PRESENT_EXIT'
        'NORM_STEP_014_FRAME_COMPLETE'
        'NORM_STEP_014_EXIT'
        'NORM_STEP_014_LOOP_ENTER'
    )
    $step14DeepestMarker = $null
    foreach ($marker in $step14MarkerSequence) {
        if ($serialText.Contains($marker)) {
            $step14DeepestMarker = $marker
        }
    }
    $whiteMarkerSequence = @(
        'NORM_STEP_010_WHITE_DRAW_ENTER'
        'NORM_STEP_010_WHITE_A_ENTER'
        'NORM_STEP_010_WHITE_A_EXIT'
        'NORM_STEP_010_WHITE_B_ENTER'
        'NORM_STEP_010_WHITE_B_EXIT'
        'NORM_STEP_010_WHITE_C_ENTER'
        'NORM_STEP_010_WHITE_C_EXIT'
        'NORM_STEP_010_WHITE_DRAW_EXIT'
    )
    $whiteDeepestMarker = $null
    foreach ($marker in $whiteMarkerSequence) {
        if ($serialText.Contains($marker)) {
            $whiteDeepestMarker = $marker
        }
    }
    $step10BlueDrawEnterPresent = $serialText.Contains('NORM_STEP_010_BLUE_DRAW_ENTER')
    $lastVecLine = Get-LastMatchingLine -Text $serialText -Pattern 'VEC='
    $lastErrLine = Get-LastMatchingLine -Text $serialText -Pattern 'ERR='
    $lastCr2Line = Get-LastMatchingLine -Text $serialText -Pattern 'CR2='
    $lastRipLine = Get-LastMatchingLine -Text $serialText -Pattern 'RIP='

    if ($validRun) {
        Write-Host "[probe] Valid fresh run detected." -ForegroundColor Green
        Write-Host "[probe] Last step enter: $lastEnter" -ForegroundColor Green
        Write-Host "[probe] Matching exit present: $matchingExitPresent" -ForegroundColor Green
        Write-Host "[probe] Step 8 safe placeholders enabled: $step8SafePlaceholdersEnabled" -ForegroundColor Green
        Write-Host "[probe] Step 10 red placeholder enabled: $step10RedPlaceholderEnabled" -ForegroundColor Green
        Write-Host "[probe] Step 10 green placeholder enabled: $step10GreenPlaceholderEnabled" -ForegroundColor Green
        Write-Host "[probe] Step 10 white placeholder enabled: $step10WhitePlaceholderEnabled" -ForegroundColor Green
        Write-Host "[probe] Step 12 safe font placeholder enabled: $step12SafeFontPlaceholderEnabled" -ForegroundColor Green
        Write-Host "[probe] NORM_STEP_008_BUTTON_BORDER_EXIT present: $step8BorderExitPresent" -ForegroundColor Green
        Write-Host "[probe] NORM_STEP_009_ENTER present: $step9EnterPresent" -ForegroundColor Green
        Write-Host "[probe] NORM_STEP_010_RED_DRAW_ENTER present: $step10RedDrawEnterPresent" -ForegroundColor Green
        Write-Host "[probe] NORM_STEP_010_RED_DRAW_EXIT present: $step10RedDrawExitPresent" -ForegroundColor Green
        Write-Host "[probe] NORM_STEP_010_GREEN_A_ENTER present: $step10GreenAEnterPresent" -ForegroundColor Green
        Write-Host "[probe] NORM_STEP_010_GREEN_A_EXIT present: $step10GreenAExitPresent" -ForegroundColor Green
        Write-Host "[probe] NORM_STEP_010_GREEN_B_ENTER present: $step10GreenBEnterPresent" -ForegroundColor Green
        Write-Host "[probe] NORM_STEP_010_GREEN_B_EXIT present: $step10GreenBExitPresent" -ForegroundColor Green
        Write-Host "[probe] NORM_STEP_010_GREEN_C_ENTER present: $step10GreenCEnterPresent" -ForegroundColor Green
        Write-Host "[probe] NORM_STEP_010_GREEN_C_EXIT present: $step10GreenCExitPresent" -ForegroundColor Green
        Write-Host "[probe] NORM_STEP_010_GREEN_IMAGE=OK present: $step10GreenImageOkPresent" -ForegroundColor Green
        Write-Host "[probe] NORM_STEP_010_GREEN_DIMENSIONS_READ present: $step10GreenDimensionsReadPresent" -ForegroundColor Green
        Write-Host "[probe] NORM_STEP_010_GREEN_RECEIVER=CACHED present: $step10GreenReceiverCachedPresent" -ForegroundColor Green
        Write-Host "[probe] NORM_STEP_010_GREEN_DRAW_OVERLOAD present: $step10GreenDrawOverloadPresent" -ForegroundColor Green
        Write-Host "[probe] NORM_STEP_010_GREEN_RAWDATA=OK present: $step10GreenRawDataOkPresent" -ForegroundColor Green
        Write-Host "[probe] NORM_STEP_010_GREEN_FIRST_PIXEL_READ_ENTER present: $step10GreenFirstPixelReadEnterPresent" -ForegroundColor Green
        Write-Host "[probe] NORM_STEP_010_GREEN_FIRST_PIXEL_READ_EXIT present: $step10GreenFirstPixelReadExitPresent" -ForegroundColor Green
        Write-Host "[probe] NORM_STEP_010_GREEN_FIRST_PIXEL_WRITE_ENTER present: $step10GreenFirstPixelWriteEnterPresent" -ForegroundColor Green
        Write-Host "[probe] NORM_STEP_010_GREEN_FIRST_PIXEL_WRITE_EXIT present: $step10GreenFirstPixelWriteExitPresent" -ForegroundColor Green
        Write-Host "[probe] NORM_STEP_010_WHITE_DRAW_ENTER present: $step10WhiteDrawEnterPresent" -ForegroundColor Green
        Write-Host "[probe] NORM_STEP_010_WHITE_A_ENTER present: $step10WhiteAEnterPresent" -ForegroundColor Green
        Write-Host "[probe] NORM_STEP_010_WHITE_A_EXIT present: $step10WhiteAExitPresent" -ForegroundColor Green
        Write-Host "[probe] NORM_STEP_010_WHITE_B_ENTER present: $step10WhiteBEnterPresent" -ForegroundColor Green
        Write-Host "[probe] NORM_STEP_010_WHITE_B_EXIT present: $step10WhiteBExitPresent" -ForegroundColor Green
        Write-Host "[probe] NORM_STEP_010_WHITE_C_ENTER present: $step10WhiteCEnterPresent" -ForegroundColor Green
        Write-Host "[probe] NORM_STEP_010_WHITE_C_EXIT present: $step10WhiteCExitPresent" -ForegroundColor Green
        Write-Host "[probe] NORM_STEP_010_WHITE_DRAW_EXIT present: $step10WhiteDrawExitPresent" -ForegroundColor Green
        Write-Host "[probe] Deepest white marker reached: $whiteDeepestMarker" -ForegroundColor Green
        Write-Host "[probe] NORM_STEP_011_SKIP_ZERO_WINDOWS present: $step11SkipPresent" -ForegroundColor Green
        Write-Host "[probe] NORM_STEP_011_A_ENTER present: $step11AEnterPresent" -ForegroundColor Green
        Write-Host "[probe] NORM_STEP_011_A_EXIT present: $step11AExitPresent" -ForegroundColor Green
        Write-Host "[probe] NORM_STEP_011_B_ENTER present: $step11BEnterPresent" -ForegroundColor Green
        Write-Host "[probe] NORM_STEP_011_B_EXIT present: $step11BExitPresent" -ForegroundColor Green
        Write-Host "[probe] NORM_STEP_011_C_ENTER present: $step11CEnterPresent" -ForegroundColor Green
        Write-Host "[probe] NORM_STEP_011_C_EXIT present: $step11CExitPresent" -ForegroundColor Green
        Write-Host "[probe] Deepest step 11 marker reached: $step11DeepestMarker" -ForegroundColor Green
        Write-Host "[probe] NORM_STEP_010_BLUE_DRAW_ENTER present: $step10BlueDrawEnterPresent" -ForegroundColor Green
        Write-Host "[probe] NORM_STEP_011_ENTER present: $step11EnterPresent" -ForegroundColor Green
        Write-Host "[probe] NORM_STEP_012_ENTER present: $step12EnterPresent" -ForegroundColor Green
        Write-Host "[probe] NORM_STEP_012_EXIT present: $step12ExitPresent" -ForegroundColor Green
        Write-Host "[probe] NORM_STEP_012_SAFE_FONT_PLACEHOLDER present: $step12SafeFontPlaceholderPresent" -ForegroundColor Green
        Write-Host "[probe] NORM_STEP_012_SAFE_FONT_PLACEHOLDER_EXIT present: $step12SafeFontPlaceholderExitPresent" -ForegroundColor Green
        Write-Host "[probe] Step 13 skip cursor draw enabled: $step13SkipCursorDrawEnabled" -ForegroundColor Green
        Write-Host "[probe] Step 13 cursor placeholder enabled: $step13CursorPlaceholderEnabled" -ForegroundColor Green
        Write-Host "[probe] Step 13 real cursor image rendering enabled: $step13RealCursorImageRenderingEnabled" -ForegroundColor Green
        Write-Host "[probe] NORM_STEP_013_A_ENTER present: $step13AEnterPresent" -ForegroundColor Green
        Write-Host "[probe] NORM_STEP_013_A_EXIT present: $step13AExitPresent" -ForegroundColor Green
        Write-Host "[probe] NORM_STEP_013_B_ENTER present: $step13BEnterPresent" -ForegroundColor Green
        Write-Host "[probe] NORM_STEP_013_B_EXIT present: $step13BExitPresent" -ForegroundColor Green
        Write-Host "[probe] NORM_STEP_013_C_ENTER present: $step13CEnterPresent" -ForegroundColor Green
        Write-Host "[probe] NORM_STEP_013_C_EXIT present: $step13CExitPresent" -ForegroundColor Green
        Write-Host "[probe] NORM_STEP_013_CURSOR_ENABLED present: $step13CursorEnabledPresent" -ForegroundColor Green
        Write-Host "[probe] NORM_STEP_013_CURSOR_ENABLED=1 present: $step13CursorEnabledOnPresent" -ForegroundColor Green
        Write-Host "[probe] NORM_STEP_013_CURSOR_ENABLED=0 present: $step13CursorEnabledOffPresent" -ForegroundColor Green
        Write-Host "[probe] NORM_STEP_013_CURSOR_PLACEHOLDER present: $step13CursorPlaceholderStatePresent" -ForegroundColor Green
        Write-Host "[probe] NORM_STEP_013_CURSOR_PLACEHOLDER=1 present: $step13CursorPlaceholderOnPresent" -ForegroundColor Green
        Write-Host "[probe] NORM_STEP_013_CURSOR_PLACEHOLDER=0 present: $step13CursorPlaceholderOffPresent" -ForegroundColor Green
        Write-Host "[probe] NORM_STEP_013_REAL_CURSOR_IMAGE_RENDERING present: $step13RealCursorImageRenderingStatePresent" -ForegroundColor Green
        Write-Host "[probe] NORM_STEP_013_REAL_CURSOR_IMAGE_RENDERING=1 present: $step13RealCursorImageRenderingOnPresent" -ForegroundColor Green
        Write-Host "[probe] NORM_STEP_013_REAL_CURSOR_IMAGE_RENDERING=0 present: $step13RealCursorImageRenderingOffPresent" -ForegroundColor Green
        Write-Host "[probe] NORM_STEP_013_CURSOR_PLACEHOLDER_ENTER present: $step13CursorPlaceholderEnterPresent" -ForegroundColor Green
        Write-Host "[probe] NORM_STEP_013_CURSOR_PLACEHOLDER_EXIT present: $step13CursorPlaceholderExitPresent" -ForegroundColor Green
        Write-Host "[probe] NORM_STEP_013_CURSOR_IMG_ENTER present: $step13CursorImageEnterPresent" -ForegroundColor Green
        Write-Host "[probe] NORM_STEP_013_CURSOR_IMG_EXIT present: $step13CursorImageExitPresent" -ForegroundColor Green
        Write-Host "[probe] NORM_STEP_013_EXIT present: $step13ExitPresent" -ForegroundColor Green
        Write-Host "[probe] CURSOR_IMG_WRAPPER_ENTER present: $cursorImgWrapperEnterPresent" -ForegroundColor Green
        Write-Host "[probe] CURSOR_IMG_BEFORE_BODY_CALL present: $cursorImgBeforeBodyCallPresent" -ForegroundColor Green
        Write-Host "[probe] CURSOR_IMG_BODY_ENTER present: $cursorImgBodyEnterPresent" -ForegroundColor Green
        Write-Host "[probe] CURSOR_IMG_BEFORE_FRAMEBUFFER_GRAPHICS present: $cursorImgBeforeFramebufferGraphicsPresent" -ForegroundColor Green
        Write-Host "[probe] CURSOR_IMG_AFTER_FRAMEBUFFER_GRAPHICS present: $cursorImgAfterFramebufferGraphicsPresent" -ForegroundColor Green
        Write-Host "[probe] CURSOR_IMG_BEFORE_IMAGE_OBJECT present: $cursorImgBeforeImageObjectPresent" -ForegroundColor Green
        Write-Host "[probe] CURSOR_IMG_AFTER_IMAGE_OBJECT present: $cursorImgAfterImageObjectPresent" -ForegroundColor Green
        Write-Host "[probe] CURSOR_IMG_BEFORE_DRAWIMAGE present: $cursorImgBeforeDrawImagePresent" -ForegroundColor Green
        Write-Host "[probe] CURSOR_IMG_AFTER_DRAWIMAGE present: $cursorImgAfterDrawImagePresent" -ForegroundColor Green
        Write-Host "[probe] CURSOR_IMG_GRAPHICS_ENTER present: $cursorImgGraphicsEnterPresent" -ForegroundColor Green
        Write-Host "[probe] CURSOR_IMG_GRAPHICS_EXIT present: $cursorImgGraphicsExitPresent" -ForegroundColor Green
        Write-Host "[probe] CURSOR_IMG_SOURCE_IMAGE_ENTER present: $cursorImgSourceImageEnterPresent" -ForegroundColor Green
        Write-Host "[probe] CURSOR_IMG_SOURCE_IMAGE_EXIT present: $cursorImgSourceImageExitPresent" -ForegroundColor Green
        Write-Host "[probe] NORM_STEP_013_CURSOR_DRAW_ENTER present: $step13CursorDrawEnterPresent" -ForegroundColor Green
        Write-Host "[probe] NORM_STEP_013_CURSOR_DRAW_PRIMITIVE_ENTER present: $step13CursorDrawPrimitiveEnterPresent" -ForegroundColor Green
        Write-Host "[probe] NORM_STEP_013_CURSOR_DRAW_PRIMITIVE_EXIT present: $step13CursorDrawPrimitiveExitPresent" -ForegroundColor Green
        Write-Host "[probe] NORM_STEP_013_CURSOR_DRAW_EXIT present: $step13CursorDrawExitPresent" -ForegroundColor Green
        Write-Host "[probe] NORM_STEP_013_CURSOR_DRAW_SKIPPED present: $step13CursorDrawSkippedPresent" -ForegroundColor Green
        Write-Host "[probe] NORM_STEP_013_CURSOR_IMAGE=NULL present: $step13CursorImageNullPresent" -ForegroundColor Green
        Write-Host "[probe] NORM_STEP_013_CURSOR_IMAGE=OK present: $step13CursorImageOkPresent" -ForegroundColor Green
        Write-Host "[probe] NORM_STEP_013_CURSOR_MOVING=NULL present: $step13CursorMovingNullPresent" -ForegroundColor Green
        Write-Host "[probe] NORM_STEP_013_CURSOR_MOVING=OK present: $step13CursorMovingOkPresent" -ForegroundColor Green
        Write-Host "[probe] NORM_STEP_013_CURSOR_BUSY=NULL present: $step13CursorBusyNullPresent" -ForegroundColor Green
        Write-Host "[probe] NORM_STEP_013_CURSOR_BUSY=OK present: $step13CursorBusyOkPresent" -ForegroundColor Green
        Write-Host "[probe] NORM_STEP_013_CURSOR_X_BOUNDS=OK present: $step13CursorXBoundsOkPresent" -ForegroundColor Green
        Write-Host "[probe] NORM_STEP_013_CURSOR_X_BOUNDS=OUT present: $step13CursorXBoundsOutPresent" -ForegroundColor Green
        Write-Host "[probe] NORM_STEP_013_CURSOR_Y_BOUNDS=OK present: $step13CursorYBoundsOkPresent" -ForegroundColor Green
        Write-Host "[probe] NORM_STEP_013_CURSOR_Y_BOUNDS=OUT present: $step13CursorYBoundsOutPresent" -ForegroundColor Green
        Write-Host "[probe] Deepest step 13 marker reached: $step13DeepestMarker" -ForegroundColor Green
        Write-Host "[probe] Deepest cursor-image marker reached: $cursorImgDeepestMarker" -ForegroundColor Green
        Write-Host "[probe] CURSOR_IMG_FIRST_DEST_PIXEL_WRITE_ENTER present: $cursorImgFirstDestPixelWriteEnterPresent" -ForegroundColor Green
        Write-Host "[probe] CURSOR_IMG_FIRST_DEST_PIXEL_WRITE_EXIT present: $cursorImgFirstDestPixelWriteExitPresent" -ForegroundColor Green
        Write-Host "[probe] NORM_STEP_014_A_ENTER present: $step14AEnterPresent" -ForegroundColor Green
        Write-Host "[probe] NORM_STEP_014_A_EXIT present: $step14AExitPresent" -ForegroundColor Green
        Write-Host "[probe] NORM_STEP_014_B_ENTER present: $step14BEnterPresent" -ForegroundColor Green
        Write-Host "[probe] NORM_STEP_014_B_EXIT present: $step14BExitPresent" -ForegroundColor Green
        Write-Host "[probe] NORM_STEP_014_C_ENTER present: $step14CEnterPresent" -ForegroundColor Green
        Write-Host "[probe] NORM_STEP_014_C_EXIT present: $step14CExitPresent" -ForegroundColor Green
        Write-Host "[probe] NORM_STEP_014_FRAMEBUFFER_ACQUIRE_ENTER present: $step14FrameBufferAcquireEnterPresent" -ForegroundColor Green
        Write-Host "[probe] NORM_STEP_014_FRAMEBUFFER_ACQUIRE=OK present: $step14FrameBufferAcquireOkPresent" -ForegroundColor Green
        Write-Host "[probe] NORM_STEP_014_FRAMEBUFFER_ACQUIRE=NULL present: $step14FrameBufferAcquireNullPresent" -ForegroundColor Green
        Write-Host "[probe] NORM_STEP_014_FRAMEBUFFER_READY=OK present: $step14FrameBufferReadyOkPresent" -ForegroundColor Green
        Write-Host "[probe] NORM_STEP_014_FRAMEBUFFER_READY=INVALID present: $step14FrameBufferReadyInvalidPresent" -ForegroundColor Green
        Write-Host "[probe] NORM_STEP_014_PRESENT_TARGET=GRAPHICS present: $step14PresentTargetGraphicsPresent" -ForegroundColor Green
        Write-Host "[probe] NORM_STEP_014_PRESENT_TARGET=NULL present: $step14PresentTargetNullPresent" -ForegroundColor Green
        Write-Host "[probe] NORM_STEP_014_FLUSH_INVALIDATE_ENTER present: $step14FlushInvalidateEnterPresent" -ForegroundColor Green
        Write-Host "[probe] NORM_STEP_014_FLUSH_INVALIDATE_EXIT present: $step14FlushInvalidateExitPresent" -ForegroundColor Green
        Write-Host "[probe] NORM_STEP_014_PRESENT_ENTER present: $step14PresentEnterPresent" -ForegroundColor Green
        Write-Host "[probe] NORM_STEP_014_PRESENT_EXIT present: $step14PresentExitPresent" -ForegroundColor Green
        Write-Host "[probe] NORM_STEP_014_FRAME_COMPLETE present: $step14FrameCompletePresent" -ForegroundColor Green
        Write-Host "[probe] NORM_STEP_014_LOOP_ENTER present: $step14LoopEnterPresent" -ForegroundColor Green
        Write-Host "[probe] Deepest step 14 marker reached: $step14DeepestMarker" -ForegroundColor Green
        Write-Host "[probe] GUI screenshot captured: $guiScreenshotCaptured" -ForegroundColor Green
        Write-Host "[probe] NORM_STEP_014_ENTER present: $step14EnterPresent" -ForegroundColor Green
        Write-Host "[probe] NORM_STEP_014_EXIT present: $step14ExitPresent" -ForegroundColor Green
        Write-Host "[probe] NORM_STEP_003_EXIT present: $($serialText.Contains('NORM_STEP_003_EXIT'))" -ForegroundColor Green
        Write-Host "[probe] NORM_STEP_004_EXIT present: $($serialText.Contains('NORM_STEP_004_EXIT'))" -ForegroundColor Green
        Write-Host "[probe] Fault VEC: $lastVecLine" -ForegroundColor Yellow
        Write-Host "[probe] Fault ERR: $lastErrLine" -ForegroundColor Yellow
        Write-Host "[probe] Fault CR2: $lastCr2Line" -ForegroundColor Yellow
        Write-Host "[probe] Fault RIP: $lastRipLine" -ForegroundColor Yellow
        if ($faultLines.Count -gt 0) {
            Write-Host "[probe] Fault lines:" -ForegroundColor Yellow
            $faultLines | ForEach-Object { Write-Host "[probe]   $_" -ForegroundColor Yellow }
        } else {
            Write-Host "[probe] Fault lines: none detected" -ForegroundColor Green
        }
        Write-Host "[probe] VEC lines: $($vecLines.Count)" -ForegroundColor Green
        Write-Host "[probe] CR2 lines: $($cr2Lines.Count)" -ForegroundColor Green
        Write-Host "[probe] ERR lines: $($errLines.Count)" -ForegroundColor Green
        Write-Host "[probe] RIP lines: $($ripLines.Count)" -ForegroundColor Green

        $summary = @(
            "RUN_ID=$runId"
            "STEP10_RED_MODE=$Step10RedMode"
            "STEP10_GREEN_MODE=$Step10GreenMode"
            "SERIAL_LOG=$serialLog"
            "STEP8_SAFE_PLACEHOLDERS_ENABLED=$step8SafePlaceholdersEnabled"
            "STEP11_SKIP_WINDOW_TRAVERSAL_ENABLED=$step11SkipWindowTraversalEnabled"
            "STEP12_SAFE_FONT_PLACEHOLDER_ENABLED=$step12SafeFontPlaceholderEnabled"
            "STEP13_SKIP_CURSOR_DRAW_ENABLED=$step13SkipCursorDrawEnabled"
            "STEP13_CURSOR_PLACEHOLDER_ENABLED=$step13CursorPlaceholderEnabled"
            "STEP13_REAL_CURSOR_IMAGE_RENDERING_ENABLED=$step13RealCursorImageRenderingEnabled"
            "CURSOR_BODY_VARIANT=$cursorBodyVariant"
            "GUI_VISIBLE_ENABLED=$GuiVisible"
            "GUI_SCREENSHOT_PATH=$GuiScreenshotPath"
            "GUI_SCREENSHOT_CAPTURED=$guiScreenshotCaptured"
            "STEP10_RED_PLACEHOLDER_ENABLED=$step10RedPlaceholderEnabled"
            "STEP10_GREEN_PLACEHOLDER_ENABLED=$step10GreenPlaceholderEnabled"
            "STEP10_WHITE_PLACEHOLDER_ENABLED=$step10WhitePlaceholderEnabled"
            "KERNEL_ELF_SHA256=$($kernelRoot.Sha256)"
            "ESP_KERNEL_ELF_SHA256=$($kernelEsp.Sha256)"
            "LAST_ENTER=$lastEnter"
            "MATCHING_EXIT=$matchingExit"
            "MATCHING_EXIT_PRESENT=$matchingExitPresent"
            "STEP11_DEEPEST_MARKER=$step11DeepestMarker"
            "STEP13_DEEPEST_MARKER=$step13DeepestMarker"
            "CURSOR_IMG_DEEPEST_MARKER=$cursorImgDeepestMarker"
            "NORM_STEP_011_ENTER_PRESENT=$step11EnterPresent"
            "NORM_STEP_011_SKIP_ZERO_WINDOWS_PRESENT=$step11SkipPresent"
            "NORM_STEP_011_A_ENTER_PRESENT=$step11AEnterPresent"
            "NORM_STEP_011_A_EXIT_PRESENT=$step11AExitPresent"
            "NORM_STEP_011_B_ENTER_PRESENT=$step11BEnterPresent"
            "NORM_STEP_011_B_EXIT_PRESENT=$step11BExitPresent"
            "NORM_STEP_011_C_ENTER_PRESENT=$step11CEnterPresent"
            "NORM_STEP_011_C_EXIT_PRESENT=$step11CExitPresent"
            "NORM_STEP_011_EXIT_PRESENT=$step11ExitPresent"
            "NORM_STEP_012_ENTER_PRESENT=$step12EnterPresent"
            "NORM_STEP_012_EXIT_PRESENT=$step12ExitPresent"
            "NORM_STEP_012_SAFE_FONT_PLACEHOLDER_PRESENT=$step12SafeFontPlaceholderPresent"
            "NORM_STEP_012_SAFE_FONT_PLACEHOLDER_EXIT_PRESENT=$step12SafeFontPlaceholderExitPresent"
            "NORM_STEP_013_A_ENTER_PRESENT=$step13AEnterPresent"
            "NORM_STEP_013_A_EXIT_PRESENT=$step13AExitPresent"
            "NORM_STEP_013_B_ENTER_PRESENT=$step13BEnterPresent"
            "NORM_STEP_013_B_EXIT_PRESENT=$step13BExitPresent"
            "NORM_STEP_013_C_ENTER_PRESENT=$step13CEnterPresent"
            "NORM_STEP_013_C_EXIT_PRESENT=$step13CExitPresent"
            "NORM_STEP_013_EXIT_PRESENT=$step13ExitPresent"
            "NORM_STEP_013_CURSOR_ENABLED_PRESENT=$step13CursorEnabledPresent"
            "NORM_STEP_013_CURSOR_ENABLED_ON_PRESENT=$step13CursorEnabledOnPresent"
            "NORM_STEP_013_CURSOR_ENABLED_OFF_PRESENT=$step13CursorEnabledOffPresent"
            "NORM_STEP_013_CURSOR_PLACEHOLDER_PRESENT=$step13CursorPlaceholderStatePresent"
            "NORM_STEP_013_CURSOR_PLACEHOLDER_ON_PRESENT=$step13CursorPlaceholderOnPresent"
            "NORM_STEP_013_CURSOR_PLACEHOLDER_OFF_PRESENT=$step13CursorPlaceholderOffPresent"
            "NORM_STEP_013_REAL_CURSOR_IMAGE_RENDERING_PRESENT=$step13RealCursorImageRenderingStatePresent"
            "NORM_STEP_013_REAL_CURSOR_IMAGE_RENDERING_ON_PRESENT=$step13RealCursorImageRenderingOnPresent"
            "NORM_STEP_013_REAL_CURSOR_IMAGE_RENDERING_OFF_PRESENT=$step13RealCursorImageRenderingOffPresent"
            "NORM_STEP_013_CURSOR_PLACEHOLDER_ENTER_PRESENT=$step13CursorPlaceholderEnterPresent"
            "NORM_STEP_013_CURSOR_PLACEHOLDER_EXIT_PRESENT=$step13CursorPlaceholderExitPresent"
            "NORM_STEP_013_CURSOR_IMG_ENTER_PRESENT=$step13CursorImageEnterPresent"
            "NORM_STEP_013_CURSOR_IMG_EXIT_PRESENT=$step13CursorImageExitPresent"
            "CURSOR_IMG_WRAPPER_ENTER_PRESENT=$cursorImgWrapperEnterPresent"
            "CURSOR_IMG_BEFORE_BODY_CALL_PRESENT=$cursorImgBeforeBodyCallPresent"
            "CURSOR_IMG_EMPTY_BODY_ENTER_PRESENT=$cursorImgEmptyBodyEnterPresent"
            "CURSOR_IMG_EMPTY_BODY_EXIT_PRESENT=$cursorImgEmptyBodyExitPresent"
            "CURSOR_IMG_INLINE_BODY_ENTER_PRESENT=$cursorImgInlineBodyEnterPresent"
            "CURSOR_IMG_INLINE_BEFORE_FRAMEBUFFER_GRAPHICS_PRESENT=$cursorImgInlineBeforeFramebufferGraphicsPresent"
            "CURSOR_IMG_INLINE_AFTER_FRAMEBUFFER_GRAPHICS_PRESENT=$cursorImgInlineAfterFramebufferGraphicsPresent"
            "CURSOR_IMG_INLINE_EXIT_PRESENT=$cursorImgInlineExitPresent"
            "CURSOR_IMG_STATIC_BODY_ENTER_PRESENT=$cursorImgStaticBodyEnterPresent"
            "CURSOR_IMG_STATIC_AFTER_FRAMEBUFFER_GRAPHICS_PRESENT=$cursorImgStaticAfterFramebufferGraphicsPresent"
            "CURSOR_IMG_STATIC_BODY_EXIT_PRESENT=$cursorImgStaticBodyExitPresent"
            "CURSOR_SRC_CHECK_ENTER_PRESENT=$cursorSrcCheckEnterPresent"
            "CURSOR_SRC_CHECK_EXIT_PRESENT=$cursorSrcCheckExitPresent"
            "CURSOR_SRC_CURSOR_MOVING_NULL_PRESENT=$cursorSrcCursorMovingNullPresent"
            "CURSOR_SRC_CURSOR_MOVING_OK_PRESENT=$cursorSrcCursorMovingOkPresent"
            "CURSOR_SRC_CURSOR_BUSY_NULL_PRESENT=$cursorSrcCursorBusyNullPresent"
            "CURSOR_SRC_CURSOR_BUSY_OK_PRESENT=$cursorSrcCursorBusyOkPresent"
            "CURSOR_SRC_FALLBACK_AVAILABLE_PRESENT=$cursorSrcFallbackAvailablePresent"
            "CURSOR_SRC_FALLBACK_NULL_PRESENT=$cursorSrcFallbackNullPresent"
            "CURSOR_SRC_LOADPNG_ENTER_PRESENT=$cursorSrcLoadPngEnterPresent"
            "CURSOR_SRC_LOADPNG_EXIT_PRESENT=$cursorSrcLoadPngExitPresent"
            "CURSOR_SRC_LOADPNG_NULL_PRESENT=$cursorSrcLoadPngNullPresent"
            "CURSOR_SRC_LOADPNG_OK_PRESENT=$cursorSrcLoadPngOkPresent"
            "CURSOR_SRC_LOADPNG_DIMS_ENTER_PRESENT=$cursorSrcLoadPngDimsEnterPresent"
            "CURSOR_SRC_LOADPNG_DIMS_EXIT_PRESENT=$cursorSrcLoadPngDimsExitPresent"
            "CURSOR_IMG_STATIC_IMAGE_REF_ENTER_PRESENT=$($serialText.Contains('CURSOR_IMG_STATIC_IMAGE_REF_ENTER'))"
            "CURSOR_IMG_STATIC_IMAGE_REF_EXIT_PRESENT=$($serialText.Contains('CURSOR_IMG_STATIC_IMAGE_REF_EXIT'))"
            "CURSOR_IMG_STATIC_IMAGE_NULL_CHECK_ENTER_PRESENT=$($serialText.Contains('CURSOR_IMG_STATIC_IMAGE_NULL_CHECK_ENTER'))"
            "CURSOR_IMG_STATIC_IMAGE_REF_NULL_PRESENT=$($serialText.Contains('CURSOR_IMG_STATIC_IMAGE_REF=NULL'))"
            "CURSOR_IMG_STATIC_IMAGE_REF_OK_PRESENT=$($serialText.Contains('CURSOR_IMG_STATIC_IMAGE_REF=OK'))"
            "CURSOR_IMG_STATIC_IMAGE_NULL_CHECK_EXIT_PRESENT=$($serialText.Contains('CURSOR_IMG_STATIC_IMAGE_NULL_CHECK_EXIT'))"
            "CURSOR_IMG_STATIC_DIMS_ENTER_PRESENT=$($serialText.Contains('CURSOR_IMG_STATIC_DIMS_ENTER'))"
            "CURSOR_IMG_STATIC_DIMS_EXIT_PRESENT=$($serialText.Contains('CURSOR_IMG_STATIC_DIMS_EXIT'))"
            "CURSOR_IMG_STATIC_RAWDATA_REF_ENTER_PRESENT=$($serialText.Contains('CURSOR_IMG_STATIC_RAWDATA_REF_ENTER'))"
            "CURSOR_IMG_STATIC_RAWDATA_REF_EXIT_PRESENT=$($serialText.Contains('CURSOR_IMG_STATIC_RAWDATA_REF_EXIT'))"
            "CURSOR_IMG_STATIC_FIRST_PIXEL_ENTER_PRESENT=$($serialText.Contains('CURSOR_IMG_STATIC_FIRST_PIXEL_ENTER'))"
            "CURSOR_IMG_STATIC_FIRST_PIXEL_EXIT_PRESENT=$($serialText.Contains('CURSOR_IMG_STATIC_FIRST_PIXEL_EXIT'))"
            "CURSOR_IMG_BODY_ENTER_PRESENT=$cursorImgBodyEnterPresent"
            "CURSOR_IMG_BEFORE_FRAMEBUFFER_GRAPHICS_PRESENT=$cursorImgBeforeFramebufferGraphicsPresent"
            "CURSOR_IMG_AFTER_FRAMEBUFFER_GRAPHICS_PRESENT=$cursorImgAfterFramebufferGraphicsPresent"
            "CURSOR_IMG_BEFORE_IMAGE_OBJECT_PRESENT=$cursorImgBeforeImageObjectPresent"
            "CURSOR_IMG_AFTER_IMAGE_OBJECT_PRESENT=$cursorImgAfterImageObjectPresent"
            "CURSOR_IMG_BEFORE_DRAWIMAGE_PRESENT=$cursorImgBeforeDrawImagePresent"
            "CURSOR_IMG_AFTER_DRAWIMAGE_PRESENT=$cursorImgAfterDrawImagePresent"
            "CURSOR_IMG_GRAPHICS_ENTER_PRESENT=$cursorImgGraphicsEnterPresent"
            "CURSOR_IMG_GRAPHICS_EXIT_PRESENT=$cursorImgGraphicsExitPresent"
            "CURSOR_IMG_SOURCE_IMAGE_ENTER_PRESENT=$cursorImgSourceImageEnterPresent"
            "CURSOR_IMG_SOURCE_IMAGE_EXIT_PRESENT=$cursorImgSourceImageExitPresent"
            "NORM_STEP_013_CURSOR_DRAW_ENTER_PRESENT=$step13CursorDrawEnterPresent"
            "NORM_STEP_013_CURSOR_DRAW_PRIMITIVE_ENTER_PRESENT=$step13CursorDrawPrimitiveEnterPresent"
            "NORM_STEP_013_CURSOR_DRAW_PRIMITIVE_EXIT_PRESENT=$step13CursorDrawPrimitiveExitPresent"
            "NORM_STEP_013_CURSOR_DRAW_EXIT_PRESENT=$step13CursorDrawExitPresent"
            "NORM_STEP_013_CURSOR_DRAW_SKIPPED_PRESENT=$step13CursorDrawSkippedPresent"
            "CURSOR_IMG_PROBE_ENTER_PRESENT=$cursorImgProbeEnterPresent"
            "CURSOR_IMG_OBJECT_CHECK_ENTER_PRESENT=$cursorImgObjectCheckEnterPresent"
            "CURSOR_IMG_OBJECT_CHECK_EXIT_PRESENT=$cursorImgObjectCheckExitPresent"
            "CURSOR_IMG_BOUNDS_CHECK_ENTER_PRESENT=$cursorImgBoundsCheckEnterPresent"
            "CURSOR_IMG_BOUNDS_CHECK_EXIT_PRESENT=$cursorImgBoundsCheckExitPresent"
            "CURSOR_IMG_DRAWIMAGE_ENTER_PRESENT=$cursorImgDrawImageEnterPresent"
            "CURSOR_IMG_DRAWIMAGE_EXIT_PRESENT=$cursorImgDrawImageExitPresent"
            "CURSOR_IMG_FIRST_DEST_PIXEL_WRITE_ENTER_PRESENT=$cursorImgFirstDestPixelWriteEnterPresent"
            "CURSOR_IMG_FIRST_DEST_PIXEL_WRITE_EXIT_PRESENT=$cursorImgFirstDestPixelWriteExitPresent"
            "CURSOR_IMG_WRAPPER_EXIT_PRESENT=$cursorImgWrapperExitPresent"
            "CURSOR_IMG_PROBE_EXIT_PRESENT=$cursorImgProbeExitPresent"
            "NORM_STEP_013_CURSOR_IMAGE_NULL_PRESENT=$step13CursorImageNullPresent"
            "NORM_STEP_013_CURSOR_IMAGE_OK_PRESENT=$step13CursorImageOkPresent"
            "NORM_STEP_013_CURSOR_MOVING_NULL_PRESENT=$step13CursorMovingNullPresent"
            "NORM_STEP_013_CURSOR_MOVING_OK_PRESENT=$step13CursorMovingOkPresent"
            "NORM_STEP_013_CURSOR_BUSY_NULL_PRESENT=$step13CursorBusyNullPresent"
            "NORM_STEP_013_CURSOR_BUSY_OK_PRESENT=$step13CursorBusyOkPresent"
            "NORM_STEP_013_CURSOR_X_BOUNDS_OK_PRESENT=$step13CursorXBoundsOkPresent"
            "NORM_STEP_013_CURSOR_X_BOUNDS_OUT_PRESENT=$step13CursorXBoundsOutPresent"
            "NORM_STEP_013_CURSOR_Y_BOUNDS_OK_PRESENT=$step13CursorYBoundsOkPresent"
            "NORM_STEP_013_CURSOR_Y_BOUNDS_OUT_PRESENT=$step13CursorYBoundsOutPresent"
            "STEP14_DEEPEST_MARKER=$step14DeepestMarker"
            "NORM_STEP_014_A_ENTER_PRESENT=$step14AEnterPresent"
            "NORM_STEP_014_A_EXIT_PRESENT=$step14AExitPresent"
            "NORM_STEP_014_B_ENTER_PRESENT=$step14BEnterPresent"
            "NORM_STEP_014_B_EXIT_PRESENT=$step14BExitPresent"
            "NORM_STEP_014_C_ENTER_PRESENT=$step14CEnterPresent"
            "NORM_STEP_014_C_EXIT_PRESENT=$step14CExitPresent"
            "NORM_STEP_014_FRAMEBUFFER_ACQUIRE_ENTER_PRESENT=$step14FrameBufferAcquireEnterPresent"
            "NORM_STEP_014_FRAMEBUFFER_ACQUIRE_OK_PRESENT=$step14FrameBufferAcquireOkPresent"
            "NORM_STEP_014_FRAMEBUFFER_ACQUIRE_NULL_PRESENT=$step14FrameBufferAcquireNullPresent"
            "NORM_STEP_014_FRAMEBUFFER_READY_OK_PRESENT=$step14FrameBufferReadyOkPresent"
            "NORM_STEP_014_FRAMEBUFFER_READY_INVALID_PRESENT=$step14FrameBufferReadyInvalidPresent"
            "NORM_STEP_014_PRESENT_TARGET_GRAPHICS_PRESENT=$step14PresentTargetGraphicsPresent"
            "NORM_STEP_014_PRESENT_TARGET_NULL_PRESENT=$step14PresentTargetNullPresent"
            "NORM_STEP_014_FLUSH_INVALIDATE_ENTER_PRESENT=$step14FlushInvalidateEnterPresent"
            "NORM_STEP_014_FLUSH_INVALIDATE_EXIT_PRESENT=$step14FlushInvalidateExitPresent"
            "NORM_STEP_014_PRESENT_ENTER_PRESENT=$step14PresentEnterPresent"
            "NORM_STEP_014_PRESENT_EXIT_PRESENT=$step14PresentExitPresent"
            "NORM_STEP_014_FRAME_COMPLETE_PRESENT=$step14FrameCompletePresent"
            "NORM_STEP_014_LOOP_ENTER_PRESENT=$step14LoopEnterPresent"
            "NORM_STEP_008_BUTTON_BORDER_EXIT_PRESENT=$step8BorderExitPresent"
            "NORM_STEP_009_ENTER_PRESENT=$step9EnterPresent"
            "NORM_STEP_010_RED_DRAW_ENTER_PRESENT=$step10RedDrawEnterPresent"
            "NORM_STEP_010_RED_DRAW_EXIT_PRESENT=$step10RedDrawExitPresent"
            "NORM_STEP_010_GREEN_A_ENTER_PRESENT=$step10GreenAEnterPresent"
            "NORM_STEP_010_GREEN_A_EXIT_PRESENT=$step10GreenAExitPresent"
            "NORM_STEP_010_GREEN_B_ENTER_PRESENT=$step10GreenBEnterPresent"
            "NORM_STEP_010_GREEN_B_EXIT_PRESENT=$step10GreenBExitPresent"
            "NORM_STEP_010_GREEN_C_ENTER_PRESENT=$step10GreenCEnterPresent"
            "NORM_STEP_010_GREEN_C_EXIT_PRESENT=$step10GreenCExitPresent"
            "NORM_STEP_010_GREEN_IMAGE_OK_PRESENT=$step10GreenImageOkPresent"
            "NORM_STEP_010_GREEN_DIMENSIONS_READ_PRESENT=$step10GreenDimensionsReadPresent"
            "NORM_STEP_010_GREEN_RECEIVER_CACHED_PRESENT=$step10GreenReceiverCachedPresent"
            "NORM_STEP_010_GREEN_DRAW_OVERLOAD_PRESENT=$step10GreenDrawOverloadPresent"
            "NORM_STEP_010_GREEN_RAWDATA_OK_PRESENT=$step10GreenRawDataOkPresent"
            "NORM_STEP_010_GREEN_FIRST_PIXEL_READ_ENTER_PRESENT=$step10GreenFirstPixelReadEnterPresent"
            "NORM_STEP_010_GREEN_FIRST_PIXEL_READ_EXIT_PRESENT=$step10GreenFirstPixelReadExitPresent"
            "NORM_STEP_010_GREEN_FIRST_PIXEL_WRITE_ENTER_PRESENT=$step10GreenFirstPixelWriteEnterPresent"
            "NORM_STEP_010_GREEN_FIRST_PIXEL_WRITE_EXIT_PRESENT=$step10GreenFirstPixelWriteExitPresent"
            "NORM_STEP_010_WHITE_DRAW_ENTER_PRESENT=$step10WhiteDrawEnterPresent"
            "NORM_STEP_010_WHITE_A_ENTER_PRESENT=$step10WhiteAEnterPresent"
            "NORM_STEP_010_WHITE_A_EXIT_PRESENT=$step10WhiteAExitPresent"
            "NORM_STEP_010_WHITE_B_ENTER_PRESENT=$step10WhiteBEnterPresent"
            "NORM_STEP_010_WHITE_B_EXIT_PRESENT=$step10WhiteBExitPresent"
            "NORM_STEP_010_WHITE_C_ENTER_PRESENT=$step10WhiteCEnterPresent"
            "NORM_STEP_010_WHITE_C_EXIT_PRESENT=$step10WhiteCExitPresent"
            "NORM_STEP_010_WHITE_DRAW_EXIT_PRESENT=$step10WhiteDrawExitPresent"
            "WHITE_DEEPEST_MARKER=$whiteDeepestMarker"
            "NORM_STEP_010_BLUE_DRAW_ENTER_PRESENT=$step10BlueDrawEnterPresent"
            "NORM_STEP_011_ENTER_PRESENT=$step11EnterPresent"
            "NORM_STEP_014_ENTER_PRESENT=$step14EnterPresent"
            "NORM_STEP_014_EXIT_PRESENT=$step14ExitPresent"
            "NORM_STEP_003_EXIT_PRESENT=$($serialText.Contains('NORM_STEP_003_EXIT'))"
            "NORM_STEP_004_EXIT_PRESENT=$($serialText.Contains('NORM_STEP_004_EXIT'))"
            "FAULT_VEC=$lastVecLine"
            "FAULT_ERR=$lastErrLine"
            "FAULT_CR2=$lastCr2Line"
            "FAULT_RIP=$lastRipLine"
            "VEC_LINES=$($vecLines.Count)"
            "FAULT_LINES=$($faultLines.Count)"
            "CR2_LINES=$($cr2Lines.Count)"
            "ERR_LINES=$($errLines.Count)"
            "RIP_LINES=$($ripLines.Count)"
        )
        [System.IO.File]::WriteAllLines($summaryLog, $summary)
        Write-Host "[probe] Summary written to: $summaryLog" -ForegroundColor Cyan
        Write-Host "[probe] Raw serial log: $serialLog" -ForegroundColor Cyan
    } else {
        Write-Host "[probe] INVALID run: missing required breadcrumbs, so no OS inference was made." -ForegroundColor Red
        if ($faultLines.Count -gt 0) {
            Write-Host "[probe] Fault lines observed despite invalidity:" -ForegroundColor Yellow
            $faultLines | ForEach-Object { Write-Host "[probe]   $_" -ForegroundColor Yellow }
        }
        if (Test-Path $stderrLog) {
            $stderrText = [System.IO.File]::ReadAllText($stderrLog)
            if ($stderrText.Trim()) {
                Write-Host "[probe] QEMU stderr:" -ForegroundColor Yellow
                Write-Host $stderrText -ForegroundColor Yellow
            }
        }
        throw "Probe run invalid. Serial log did not contain the required fresh probe markers."
    }
}
finally {
    if ($qemuProcess -and -not $qemuProcess.HasExited) {
        Stop-Process -Id $qemuProcess.Id -Force -ErrorAction SilentlyContinue
    }

    if ($programPatched) {
        [System.IO.File]::WriteAllText($programPath, $originalProgram)
        Write-Host "[probe] Restored Program.cs defaults." -ForegroundColor Cyan
    }
}
