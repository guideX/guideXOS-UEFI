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
    [switch]$SkipBootloader,
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
    [switch]$CursorPngNoopLoaderProbe,
    [switch]$CursorPngBytesNoopProbe,
    [switch]$CursorPngHeaderHelperProbe,
    [switch]$CursorPngIhdrHelperProbe,
    [switch]$CursorPngStandaloneS0Probe,
    [switch]$CursorPngStandaloneS1Probe,
    [switch]$CursorPngStandaloneS2Probe,
    [switch]$CursorPngStandaloneS3Probe,
    [switch]$CursorPngStandaloneS4Probe,
    [switch]$CursorPngStandaloneS4AProbe,
    [switch]$CursorPngStandaloneS4B0Probe,
    [switch]$CursorPngStandaloneS4B1Probe,
    [switch]$CursorPngStandaloneS4B2Probe,
    [switch]$CursorPngStandaloneS4B3Probe,
    [switch]$CursorPngStandaloneS4B4Probe,
    [switch]$CursorPngStandaloneS4B5Probe,
    [switch]$CursorPngStandaloneS4B6Probe,
    [switch]$CursorPngStandaloneS4B7RProbe,
    [switch]$CursorPngStandaloneS4B7SProbe,
    [switch]$CursorPngStandaloneS4B7TProbe,
    [switch]$CursorPngStandaloneS4B7T2Probe,
    [switch]$CursorPngStandaloneS4B7Probe,
    [switch]$CursorPngStandaloneS4B8Probe,
    [switch]$CursorPngStandaloneS4BProbe,
    [switch]$CursorPngStandaloneS4C0Probe,
    [switch]$CursorPngStandaloneS4C1Probe,
    [switch]$CursorPngStandaloneS4C2Probe,
    [switch]$CursorPngStandaloneS4C3Probe,
    [switch]$CursorPngStandaloneS4C4Probe,
    [switch]$CursorPngStandaloneS4D0Probe,
    [switch]$CursorPngStandaloneS4D1Probe,
    [switch]$CursorPngStandaloneS4D2Probe,
    [switch]$CursorPngStandaloneS4D3Probe,
    [switch]$CursorPngStandaloneS4E0Probe,
    [switch]$CursorPngStandaloneS4E1Probe,
    [switch]$CursorPngStandaloneS4E2Probe,
    [switch]$CursorPngStandaloneS4E3Probe,
    [switch]$CursorPngStandaloneS4E4Probe,
    [switch]$CursorPngStandaloneS4CProbe,
    [switch]$CursorPngStandaloneS4DProbe,
    [switch]$CursorPngStandaloneS4EProbe,
    [switch]$CursorPngStandaloneS4FProbe,
    [switch]$CursorPngStandaloneS4GProbe,
    [switch]$CursorPngStandaloneS4HProbe,
    [switch]$CursorPngLoadWrapperProbe,
    [switch]$CursorPngLoadAfterIhdrProbe,
    [switch]$CursorPngLoadAfterChunkScanProbe,
    [switch]$CursorPngLoadAfterIdatAggregationProbe,
    [switch]$CursorPngDecompressPreMetaProbe,
    [switch]$CursorPngDecompressNoopProbe,
    [switch]$CursorPngDecompressBytesNoopProbe,
    [switch]$CursorPngDecompressZlibHeaderProbe,
    [switch]$CursorPngDecompressOutputAllocProbe,
    [switch]$CursorPngDecompressDeflateHeaderProbe,
    [switch]$CursorPngDecompressHuffmanSetupProbe,
    [switch]$CursorPngDecompressGateInlineProbe,
    [switch]$CursorPngDecompressGateHelperReturnProbe,
    [switch]$CursorPngDecompressGateBoolOnlyProbe,
    [switch]$CursorPngDecompressGateNoStateCopyProbe,
    [switch]$CursorPngDecompressPostGateFirstInstructionProbe,
    [switch]$CursorPngDecompressAfterInputGateProbe,
    [switch]$CursorPngDecompressPrepNoopProbe,
    [switch]$CursorPngDecompressPrepMetadataProbe,
    [switch]$CursorPngDecompressPrepBytesProbe,
    [switch]$CursorPngDecompressPrepInlineProbe,
    [switch]$CursorPngDecompressPrepBoundaryProbe,
    [switch]$CursorPngDecompressPrepContextProbe,
    [switch]$CursorPngDecompressInflateBoundaryProbe,
    [switch]$CursorPngDecompressInflateBitReaderProbe,
    [switch]$CursorPngDecompressInflateFirstSymbolDecodeProbe,
    [switch]$CursorPngDecompressInflateLiteralWriteProbe,
    [switch]$CursorPngDecompressInflateLengthDistanceProbe,
    [switch]$CursorPngDecompressInflateOneStepProbe,
    [switch]$CursorPngDecompressInflateSmokeProbe,
    [switch]$CursorPngDecompressTinyBoundaryProbe,
    [switch]$CursorPngDecompressTinyBitReaderProbe,
    [switch]$CursorPngDecompressTinyFirstSymbolProbe,
    [switch]$CursorPngDecompressTinyOneOpProbe,
    [switch]$CursorPngDecompressTinyInflateSmokeProbe,
    [switch]$CursorPngLoadAfterDecompressProbe,
    [switch]$CursorPngLoadAfterImageCreateProbe,
    [switch]$CursorDrawBusyProbe,
    [switch]$CursorDrawBusyDirectProbe,
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

function Quote-CmdArg {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Value
    )

    if ($Value -match '[\s`"]') {
        return '"' + ($Value -replace '"', '\"') + '"'
    }

    return $Value
}

function Format-CmdLine {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Exe,

        [Parameter(Mandatory = $true)]
        [string[]]$Args
    )

    $parts = @((Quote-CmdArg -Value $Exe))
    $parts += $Args | ForEach-Object { Quote-CmdArg -Value $_ }
    return ($parts -join ' ')
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

function Get-SerialMarkerValue {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Text,

        [Parameter(Mandatory = $true)]
        [string]$MarkerPrefix
    )

    $pattern = "(?m)^" + [regex]::Escape($MarkerPrefix) + "=(\d+)$"
    $match = [regex]::Match($Text, $pattern)
    if ($match.Success) {
        return [uint64]$match.Groups[1].Value
    }

    return $null
}

function Get-DeepestSerialMarker {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Text,

        [Parameter(Mandatory = $true)]
        [string[]]$Markers
    )

    $deepest = $null
    foreach ($marker in $Markers) {
        if ($Text.Contains($marker)) {
            $deepest = $marker
        }
    }

    return $deepest
}

function Get-CursorProbeDims {
    param(
        [string[]]$Lines,

        [Parameter(Mandatory = $true)]
        [string]$EnterMarker,

        [Parameter(Mandatory = $true)]
        [string]$ExitMarker
    )

    if (-not $Lines -or $Lines.Count -eq 0) {
        return $null
    }

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
if ($CursorPngNoopLoaderProbe) { $cursorVariantCount++ }
if ($CursorPngBytesNoopProbe) { $cursorVariantCount++ }
if ($CursorPngHeaderHelperProbe) { $cursorVariantCount++ }
if ($CursorPngIhdrHelperProbe) { $cursorVariantCount++ }
if ($CursorPngStandaloneS0Probe) { $cursorVariantCount++ }
if ($CursorPngStandaloneS1Probe) { $cursorVariantCount++ }
if ($CursorPngStandaloneS2Probe) { $cursorVariantCount++ }
if ($CursorPngStandaloneS3Probe) { $cursorVariantCount++ }
if ($CursorPngStandaloneS4Probe) { $cursorVariantCount++ }
if ($CursorPngStandaloneS4AProbe) { $cursorVariantCount++ }
if ($CursorPngStandaloneS4B0Probe) { $cursorVariantCount++ }
if ($CursorPngStandaloneS4B1Probe) { $cursorVariantCount++ }
if ($CursorPngStandaloneS4B2Probe) { $cursorVariantCount++ }
if ($CursorPngStandaloneS4B3Probe) { $cursorVariantCount++ }
if ($CursorPngStandaloneS4B4Probe) { $cursorVariantCount++ }
if ($CursorPngStandaloneS4B5Probe) { $cursorVariantCount++ }
if ($CursorPngStandaloneS4B6Probe) { $cursorVariantCount++ }
if ($CursorPngStandaloneS4B7RProbe) { $cursorVariantCount++ }
if ($CursorPngStandaloneS4B7SProbe) { $cursorVariantCount++ }
if ($CursorPngStandaloneS4B7TProbe) { $cursorVariantCount++ }
if ($CursorPngStandaloneS4B7T2Probe) { $cursorVariantCount++ }
if ($CursorPngStandaloneS4B7Probe) { $cursorVariantCount++ }
if ($CursorPngStandaloneS4B8Probe) { $cursorVariantCount++ }
if ($CursorPngStandaloneS4BProbe) { $cursorVariantCount++ }
if ($CursorPngStandaloneS4C0Probe) { $cursorVariantCount++ }
if ($CursorPngStandaloneS4C1Probe) { $cursorVariantCount++ }
if ($CursorPngStandaloneS4C2Probe) { $cursorVariantCount++ }
if ($CursorPngStandaloneS4C3Probe) { $cursorVariantCount++ }
if ($CursorPngStandaloneS4C4Probe) { $cursorVariantCount++ }
if ($CursorPngStandaloneS4D0Probe) { $cursorVariantCount++ }
if ($CursorPngStandaloneS4D1Probe) { $cursorVariantCount++ }
if ($CursorPngStandaloneS4D2Probe) { $cursorVariantCount++ }
if ($CursorPngStandaloneS4D3Probe) { $cursorVariantCount++ }
if ($CursorPngStandaloneS4E0Probe) { $cursorVariantCount++ }
if ($CursorPngStandaloneS4E1Probe) { $cursorVariantCount++ }
if ($CursorPngStandaloneS4E2Probe) { $cursorVariantCount++ }
if ($CursorPngStandaloneS4E3Probe) { $cursorVariantCount++ }
if ($CursorPngStandaloneS4E4Probe) { $cursorVariantCount++ }
if ($CursorPngStandaloneS4CProbe) { $cursorVariantCount++ }
if ($CursorPngStandaloneS4DProbe) { $cursorVariantCount++ }
if ($CursorPngStandaloneS4EProbe) { $cursorVariantCount++ }
if ($CursorPngStandaloneS4FProbe) { $cursorVariantCount++ }
if ($CursorPngStandaloneS4GProbe) { $cursorVariantCount++ }
if ($CursorPngStandaloneS4HProbe) { $cursorVariantCount++ }
if ($CursorPngLoadWrapperProbe) { $cursorVariantCount++ }
if ($CursorPngLoadAfterIhdrProbe) { $cursorVariantCount++ }
if ($CursorPngLoadAfterChunkScanProbe) { $cursorVariantCount++ }
if ($CursorPngLoadAfterIdatAggregationProbe) { $cursorVariantCount++ }
if ($CursorPngDecompressPreMetaProbe) { $cursorVariantCount++ }
if ($CursorPngDecompressNoopProbe) { $cursorVariantCount++ }
if ($CursorPngDecompressBytesNoopProbe) { $cursorVariantCount++ }
    if ($CursorPngDecompressZlibHeaderProbe) { $cursorVariantCount++ }
    if ($CursorPngDecompressOutputAllocProbe) { $cursorVariantCount++ }
    if ($CursorPngDecompressDeflateHeaderProbe) { $cursorVariantCount++ }
    if ($CursorPngDecompressHuffmanSetupProbe) { $cursorVariantCount++ }
    if ($CursorPngDecompressGateInlineProbe) { $cursorVariantCount++ }
    if ($CursorPngDecompressGateHelperReturnProbe) { $cursorVariantCount++ }
    if ($CursorPngDecompressGateBoolOnlyProbe) { $cursorVariantCount++ }
    if ($CursorPngDecompressGateNoStateCopyProbe) { $cursorVariantCount++ }
    if ($CursorPngDecompressPostGateFirstInstructionProbe) { $cursorVariantCount++ }
    if ($CursorPngDecompressAfterInputGateProbe) { $cursorVariantCount++ }
    if ($CursorPngDecompressPrepNoopProbe) { $cursorVariantCount++ }
    if ($CursorPngDecompressPrepMetadataProbe) { $cursorVariantCount++ }
    if ($CursorPngDecompressPrepBytesProbe) { $cursorVariantCount++ }
    if ($CursorPngDecompressPrepInlineProbe) { $cursorVariantCount++ }
    if ($CursorPngDecompressPrepBoundaryProbe) { $cursorVariantCount++ }
    if ($CursorPngDecompressPrepContextProbe) { $cursorVariantCount++ }
    if ($CursorPngDecompressInflateBoundaryProbe) { $cursorVariantCount++ }
if ($CursorPngDecompressInflateBitReaderProbe) { $cursorVariantCount++ }
if ($CursorPngDecompressInflateFirstSymbolDecodeProbe) { $cursorVariantCount++ }
if ($CursorPngDecompressInflateLiteralWriteProbe) { $cursorVariantCount++ }
if ($CursorPngDecompressInflateLengthDistanceProbe) { $cursorVariantCount++ }
if ($CursorPngDecompressInflateOneStepProbe) { $cursorVariantCount++ }
if ($CursorPngDecompressInflateSmokeProbe) { $cursorVariantCount++ }
if ($CursorPngDecompressTinyBoundaryProbe) { $cursorVariantCount++ }
if ($CursorPngDecompressTinyBitReaderProbe) { $cursorVariantCount++ }
if ($CursorPngDecompressTinyFirstSymbolProbe) { $cursorVariantCount++ }
if ($CursorPngDecompressTinyOneOpProbe) { $cursorVariantCount++ }
if ($CursorPngDecompressTinyInflateSmokeProbe) { $cursorVariantCount++ }
if ($CursorPngLoadAfterDecompressProbe) { $cursorVariantCount++ }
if ($CursorPngLoadAfterImageCreateProbe) { $cursorVariantCount++ }
if ($CursorDrawBusyProbe) { $cursorVariantCount++ }
if ($CursorDrawBusyDirectProbe) { $cursorVariantCount++ }
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
} elseif ($CursorPngNoopLoaderProbe) {
    'PngNoopLoader'
} elseif ($CursorPngBytesNoopProbe) {
    'PngBytesNoop'
} elseif ($CursorPngHeaderHelperProbe) {
    'PngHeaderHelper'
} elseif ($CursorPngIhdrHelperProbe) {
    'PngIhdrHelper'
} elseif ($CursorPngStandaloneS0Probe) {
    'PngStandaloneS0'
} elseif ($CursorPngStandaloneS1Probe) {
    'PngStandaloneS1'
} elseif ($CursorPngStandaloneS2Probe) {
    'PngStandaloneS2'
} elseif ($CursorPngStandaloneS3Probe) {
    'PngStandaloneS3'
} elseif ($CursorPngStandaloneS4Probe) {
    'PngStandaloneS4'
} elseif ($CursorPngStandaloneS4AProbe) {
    'PngStandaloneS4A'
} elseif ($CursorPngStandaloneS4B0Probe) {
    'PngStandaloneS4B0'
} elseif ($CursorPngStandaloneS4B1Probe) {
    'PngStandaloneS4B1'
} elseif ($CursorPngStandaloneS4B2Probe) {
    'PngStandaloneS4B2'
} elseif ($CursorPngStandaloneS4B3Probe) {
    'PngStandaloneS4B3'
} elseif ($CursorPngStandaloneS4B4Probe) {
    'PngStandaloneS4B4'
} elseif ($CursorPngStandaloneS4B5Probe) {
    'PngStandaloneS4B5'
} elseif ($CursorPngStandaloneS4B6Probe) {
    'PngStandaloneS4B6'
} elseif ($CursorPngStandaloneS4B7RProbe) {
    'PngStandaloneS4B7R'
    } elseif ($CursorPngStandaloneS4B7SProbe) {
        'PngStandaloneS4B7S'
    } elseif ($CursorPngStandaloneS4B7TProbe) {
        'PngStandaloneS4B7T'
    } elseif ($CursorPngStandaloneS4B7T2Probe) {
        'PngStandaloneS4B7T2'
    } elseif ($CursorPngStandaloneS4B7Probe) {
        'PngStandaloneS4B7'
} elseif ($CursorPngStandaloneS4B8Probe) {
    'PngStandaloneS4B8'
} elseif ($CursorPngStandaloneS4BProbe) {
    'PngStandaloneS4B'
} elseif ($CursorPngStandaloneS4C0Probe) {
    'PngStandaloneS4C0'
} elseif ($CursorPngStandaloneS4C1Probe) {
    'PngStandaloneS4C1'
} elseif ($CursorPngStandaloneS4C2Probe) {
    'PngStandaloneS4C2'
} elseif ($CursorPngStandaloneS4C3Probe) {
    'PngStandaloneS4C3'
} elseif ($CursorPngStandaloneS4C4Probe) {
    'PngStandaloneS4C4'
} elseif ($CursorPngStandaloneS4D0Probe) {
    'PngStandaloneS4D0'
} elseif ($CursorPngStandaloneS4D1Probe) {
    'PngStandaloneS4D1'
} elseif ($CursorPngStandaloneS4D2Probe) {
    'PngStandaloneS4D2'
} elseif ($CursorPngStandaloneS4D3Probe) {
    'PngStandaloneS4D3'
} elseif ($CursorPngStandaloneS4CProbe) {
    'PngStandaloneS4C'
} elseif ($CursorPngStandaloneS4DProbe) {
    'PngStandaloneS4D'
} elseif ($CursorPngStandaloneS4EProbe) {
    'PngStandaloneS4E'
} elseif ($CursorPngStandaloneS4FProbe) {
    'PngStandaloneS4F'
} elseif ($CursorPngStandaloneS4GProbe) {
    'PngStandaloneS4G'
} elseif ($CursorPngStandaloneS4HProbe) {
    'PngStandaloneS4H'
} elseif ($CursorPngLoadWrapperProbe) {
    'PngLoadWrapper'
} elseif ($CursorPngDecompressInflateBoundaryProbe) {
    'PngInflateBoundary'
} elseif ($CursorPngDecompressInflateBitReaderProbe) {
    'PngInflateBitReader'
} elseif ($CursorPngDecompressInflateFirstSymbolDecodeProbe) {
    'PngInflateFirstSymbolDecode'
} elseif ($CursorPngDecompressInflateLiteralWriteProbe) {
    'PngInflateLiteralWrite'
} elseif ($CursorPngDecompressInflateLengthDistanceProbe) {
    'PngInflateLengthDistance'
} elseif ($CursorPngDecompressInflateOneStepProbe) {
    'PngInflateOneStep'
} elseif ($CursorPngDecompressTinyBoundaryProbe) {
    'PngTinyBoundary'
} elseif ($CursorPngDecompressTinyBitReaderProbe) {
    'PngTinyBitReader'
} elseif ($CursorPngDecompressTinyFirstSymbolProbe) {
    'PngTinyFirstSymbol'
} elseif ($CursorPngDecompressTinyOneOpProbe) {
    'PngTinyOneOp'
} elseif ($CursorPngDecompressTinyInflateSmokeProbe) {
    'PngTinyInflateSmoke'
} elseif ($CursorPngLoadAfterIhdrProbe) {
    'PngEntryBaseline'
} elseif ($CursorPngLoadAfterChunkScanProbe) {
    'PngLoadAfterChunkScan'
} elseif ($CursorPngLoadAfterIdatAggregationProbe) {
    'PngLoadAfterIdatAggregation'
} elseif ($CursorPngLoadAfterDecompressProbe) {
    'PngLoadAfterDecompress'
} elseif ($CursorPngLoadAfterImageCreateProbe) {
    'PngLoadAfterImageCreate'
} elseif ($CursorDrawBusyProbe) {
    'DrawBusy'
} elseif ($CursorDrawBusyDirectProbe) {
    'DrawBusyDirect'
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
    $step13RealCursorImageRenderingEnabled = (
        $ProbeRealCursorImageRendering -or
        $CursorEmptyBodyProbe -or
        $CursorInlineBodyProbe -or
        $CursorStaticBodyProbe -or
        $CursorStaticImageRefProbe -or
        $CursorStaticDimsProbe -or
        $CursorStaticRawDataRefProbe -or
        $CursorStaticFirstPixelProbe -or
        $CursorSourceExistingRefsProbe -or
        $CursorSourceFallbackProbe -or
        $CursorSourcePngProbe -or
        $CursorPngNoopLoaderProbe -or
        $CursorPngBytesNoopProbe -or
        $CursorPngHeaderHelperProbe -or
        $CursorPngIhdrHelperProbe -or
        $CursorPngStandaloneS0Probe -or
        $CursorPngStandaloneS1Probe -or
        $CursorPngStandaloneS2Probe -or
        $CursorPngStandaloneS3Probe -or
        $CursorPngStandaloneS4Probe -or
        $CursorPngStandaloneS4AProbe -or
        $CursorPngStandaloneS4B0Probe -or
        $CursorPngStandaloneS4B1Probe -or
        $CursorPngStandaloneS4B2Probe -or
        $CursorPngStandaloneS4B3Probe -or
        $CursorPngStandaloneS4B4Probe -or
        $CursorPngStandaloneS4B5Probe -or
        $CursorPngStandaloneS4B6Probe -or
        $CursorPngStandaloneS4B7RProbe -or
        $CursorPngStandaloneS4B7SProbe -or
        $CursorPngStandaloneS4B7TProbe -or
        $CursorPngStandaloneS4B7T2Probe -or
        $CursorPngStandaloneS4B7Probe -or
        $CursorPngStandaloneS4B8Probe -or
        $CursorPngStandaloneS4BProbe -or
        $CursorPngStandaloneS4C0Probe -or
        $CursorPngStandaloneS4C1Probe -or
        $CursorPngStandaloneS4C2Probe -or
        $CursorPngStandaloneS4C3Probe -or
        $CursorPngStandaloneS4C4Probe -or
        $CursorPngStandaloneS4D0Probe -or
        $CursorPngStandaloneS4D1Probe -or
        $CursorPngStandaloneS4D2Probe -or
        $CursorPngStandaloneS4D3Probe -or
        $CursorPngStandaloneS4E0Probe -or
        $CursorPngStandaloneS4E1Probe -or
        $CursorPngStandaloneS4E2Probe -or
        $CursorPngStandaloneS4E3Probe -or
        $CursorPngStandaloneS4E4Probe -or
        $CursorPngStandaloneS4CProbe -or
        $CursorPngStandaloneS4DProbe -or
        $CursorPngStandaloneS4EProbe -or
        $CursorPngStandaloneS4FProbe -or
        $CursorPngStandaloneS4GProbe -or
        $CursorPngStandaloneS4HProbe -or
        $CursorPngLoadWrapperProbe -or
        $CursorPngLoadAfterIhdrProbe -or
        $CursorPngLoadAfterChunkScanProbe -or
        $CursorPngLoadAfterIdatAggregationProbe -or
        $CursorPngDecompressPreMetaProbe -or
        $CursorPngDecompressNoopProbe -or
        $CursorPngDecompressBytesNoopProbe -or
        $CursorPngDecompressZlibHeaderProbe -or
        $CursorPngDecompressOutputAllocProbe -or
        $CursorPngDecompressDeflateHeaderProbe -or
        $CursorPngDecompressHuffmanSetupProbe -or
        $CursorPngDecompressGateInlineProbe -or
        $CursorPngDecompressGateHelperReturnProbe -or
        $CursorPngDecompressGateBoolOnlyProbe -or
        $CursorPngDecompressGateNoStateCopyProbe -or
        $CursorPngDecompressPostGateFirstInstructionProbe -or
        $CursorPngDecompressAfterInputGateProbe -or
        $CursorPngDecompressPrepNoopProbe -or
        $CursorPngDecompressPrepMetadataProbe -or
        $CursorPngDecompressPrepBytesProbe -or
        $CursorPngDecompressPrepInlineProbe -or
        $CursorPngDecompressPrepBoundaryProbe -or
        $CursorPngDecompressPrepContextProbe -or
        $CursorPngDecompressInflateBoundaryProbe -or
        $CursorPngDecompressInflateBitReaderProbe -or
        $CursorPngDecompressInflateFirstSymbolDecodeProbe -or
        $CursorPngDecompressInflateLiteralWriteProbe -or
        $CursorPngDecompressInflateLengthDistanceProbe -or
        $CursorPngDecompressInflateOneStepProbe -or
        $CursorPngDecompressInflateSmokeProbe -or
        $CursorPngDecompressTinyBoundaryProbe -or
        $CursorPngDecompressTinyBitReaderProbe -or
        $CursorPngDecompressTinyFirstSymbolProbe -or
        $CursorPngDecompressTinyOneOpProbe -or
        $CursorPngDecompressTinyInflateSmokeProbe -or
        $CursorPngLoadAfterDecompressProbe -or
        $CursorPngLoadAfterImageCreateProbe -or
        $CursorDrawBusyProbe -or
        $CursorDrawBusyDirectProbe -or
        $CursorDrawFallbackProbe
    )
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
    $cursorDrawBusyDirectProbeValue = if ($CursorDrawBusyDirectProbe) { 'true' } else { 'false' }
    $patched = Assert-SingleReplacement -Text $patched `
        -Old 'private const bool NORMAL_DESKTOP_UEFI_PROBE_CURSOR_DRAW_BUSY_DIRECT = false;' `
        -New "private const bool NORMAL_DESKTOP_UEFI_PROBE_CURSOR_DRAW_BUSY_DIRECT = $cursorDrawBusyDirectProbeValue;" `
        -Label 'NORMAL_DESKTOP_UEFI_PROBE_CURSOR_DRAW_BUSY_DIRECT'
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
    $cursorPngNoopLoaderProbeValue = if ($CursorPngNoopLoaderProbe) { 'true' } else { 'false' }
    $patched = Assert-SingleReplacement -Text $patched `
        -Old 'private const bool UEFI_PROBE_CURSOR_PNG_NOOP_LOADER = false;' `
        -New "private const bool UEFI_PROBE_CURSOR_PNG_NOOP_LOADER = $cursorPngNoopLoaderProbeValue;" `
        -Label 'UEFI_PROBE_CURSOR_PNG_NOOP_LOADER'
    $cursorPngBytesNoopProbeValue = if ($CursorPngBytesNoopProbe) { 'true' } else { 'false' }
    $patched = Assert-SingleReplacement -Text $patched `
        -Old 'private const bool UEFI_PROBE_CURSOR_PNG_BYTES_NOOP = false;' `
        -New "private const bool UEFI_PROBE_CURSOR_PNG_BYTES_NOOP = $cursorPngBytesNoopProbeValue;" `
        -Label 'UEFI_PROBE_CURSOR_PNG_BYTES_NOOP'
    $cursorPngHeaderHelperProbeValue = if ($CursorPngHeaderHelperProbe) { 'true' } else { 'false' }
    $patched = Assert-SingleReplacement -Text $patched `
        -Old 'private const bool UEFI_PROBE_CURSOR_PNG_HEADER_HELPER = false;' `
        -New "private const bool UEFI_PROBE_CURSOR_PNG_HEADER_HELPER = $cursorPngHeaderHelperProbeValue;" `
        -Label 'UEFI_PROBE_CURSOR_PNG_HEADER_HELPER'
    $cursorPngIhdrHelperProbeValue = if ($CursorPngIhdrHelperProbe) { 'true' } else { 'false' }
    $patched = Assert-SingleReplacement -Text $patched `
        -Old 'private const bool UEFI_PROBE_CURSOR_PNG_IHDR_HELPER = false;' `
        -New "private const bool UEFI_PROBE_CURSOR_PNG_IHDR_HELPER = $cursorPngIhdrHelperProbeValue;" `
        -Label 'UEFI_PROBE_CURSOR_PNG_IHDR_HELPER'
    $cursorPngStandaloneS0ProbeValue = if ($CursorPngStandaloneS0Probe) { 'true' } else { 'false' }
    $patched = Assert-SingleReplacement -Text $patched `
        -Old 'private const bool UEFI_PROBE_CURSOR_PNG_STANDALONE_S0 = false;' `
        -New "private const bool UEFI_PROBE_CURSOR_PNG_STANDALONE_S0 = $cursorPngStandaloneS0ProbeValue;" `
        -Label 'UEFI_PROBE_CURSOR_PNG_STANDALONE_S0'
    $cursorPngStandaloneS1ProbeValue = if ($CursorPngStandaloneS1Probe) { 'true' } else { 'false' }
    $patched = Assert-SingleReplacement -Text $patched `
        -Old 'private const bool UEFI_PROBE_CURSOR_PNG_STANDALONE_S1 = false;' `
        -New "private const bool UEFI_PROBE_CURSOR_PNG_STANDALONE_S1 = $cursorPngStandaloneS1ProbeValue;" `
        -Label 'UEFI_PROBE_CURSOR_PNG_STANDALONE_S1'
    $cursorPngStandaloneS2ProbeValue = if ($CursorPngStandaloneS2Probe) { 'true' } else { 'false' }
    $patched = Assert-SingleReplacement -Text $patched `
        -Old 'private const bool UEFI_PROBE_CURSOR_PNG_STANDALONE_S2 = false;' `
        -New "private const bool UEFI_PROBE_CURSOR_PNG_STANDALONE_S2 = $cursorPngStandaloneS2ProbeValue;" `
        -Label 'UEFI_PROBE_CURSOR_PNG_STANDALONE_S2'
    $cursorPngStandaloneS3ProbeValue = if ($CursorPngStandaloneS3Probe) { 'true' } else { 'false' }
    $patched = Assert-SingleReplacement -Text $patched `
        -Old 'private const bool UEFI_PROBE_CURSOR_PNG_STANDALONE_S3 = false;' `
        -New "private const bool UEFI_PROBE_CURSOR_PNG_STANDALONE_S3 = $cursorPngStandaloneS3ProbeValue;" `
        -Label 'UEFI_PROBE_CURSOR_PNG_STANDALONE_S3'
    $cursorPngStandaloneS4ProbeValue = if ($CursorPngStandaloneS4Probe) { 'true' } else { 'false' }
    $patched = Assert-SingleReplacement -Text $patched `
        -Old 'private const bool UEFI_PROBE_CURSOR_PNG_STANDALONE_S4 = false;' `
        -New "private const bool UEFI_PROBE_CURSOR_PNG_STANDALONE_S4 = $cursorPngStandaloneS4ProbeValue;" `
        -Label 'UEFI_PROBE_CURSOR_PNG_STANDALONE_S4'
    $cursorPngStandaloneS4AProbeValue = if ($CursorPngStandaloneS4AProbe) { 'true' } else { 'false' }
    $patched = Assert-SingleReplacement -Text $patched `
        -Old 'private const bool UEFI_PROBE_CURSOR_PNG_STANDALONE_S4A = false;' `
        -New "private const bool UEFI_PROBE_CURSOR_PNG_STANDALONE_S4A = $cursorPngStandaloneS4AProbeValue;" `
        -Label 'UEFI_PROBE_CURSOR_PNG_STANDALONE_S4A'
    $cursorPngStandaloneS4B0ProbeValue = if ($CursorPngStandaloneS4B0Probe) { 'true' } else { 'false' }
    $patched = Assert-SingleReplacement -Text $patched `
        -Old 'private const bool UEFI_PROBE_CURSOR_PNG_STANDALONE_S4B0 = false;' `
        -New "private const bool UEFI_PROBE_CURSOR_PNG_STANDALONE_S4B0 = $cursorPngStandaloneS4B0ProbeValue;" `
        -Label 'UEFI_PROBE_CURSOR_PNG_STANDALONE_S4B0'
    $cursorPngStandaloneS4B1ProbeValue = if ($CursorPngStandaloneS4B1Probe) { 'true' } else { 'false' }
    $patched = Assert-SingleReplacement -Text $patched `
        -Old 'private const bool UEFI_PROBE_CURSOR_PNG_STANDALONE_S4B1 = false;' `
        -New "private const bool UEFI_PROBE_CURSOR_PNG_STANDALONE_S4B1 = $cursorPngStandaloneS4B1ProbeValue;" `
        -Label 'UEFI_PROBE_CURSOR_PNG_STANDALONE_S4B1'
    $cursorPngStandaloneS4B2ProbeValue = if ($CursorPngStandaloneS4B2Probe) { 'true' } else { 'false' }
    $patched = Assert-SingleReplacement -Text $patched `
        -Old 'private const bool UEFI_PROBE_CURSOR_PNG_STANDALONE_S4B2 = false;' `
        -New "private const bool UEFI_PROBE_CURSOR_PNG_STANDALONE_S4B2 = $cursorPngStandaloneS4B2ProbeValue;" `
        -Label 'UEFI_PROBE_CURSOR_PNG_STANDALONE_S4B2'
    $cursorPngStandaloneS4B3ProbeValue = if ($CursorPngStandaloneS4B3Probe) { 'true' } else { 'false' }
    $patched = Assert-SingleReplacement -Text $patched `
        -Old 'private const bool UEFI_PROBE_CURSOR_PNG_STANDALONE_S4B3 = false;' `
        -New "private const bool UEFI_PROBE_CURSOR_PNG_STANDALONE_S4B3 = $cursorPngStandaloneS4B3ProbeValue;" `
        -Label 'UEFI_PROBE_CURSOR_PNG_STANDALONE_S4B3'
    $cursorPngStandaloneS4B4ProbeValue = if ($CursorPngStandaloneS4B4Probe) { 'true' } else { 'false' }
    $patched = Assert-SingleReplacement -Text $patched `
        -Old 'private const bool UEFI_PROBE_CURSOR_PNG_STANDALONE_S4B4 = false;' `
        -New "private const bool UEFI_PROBE_CURSOR_PNG_STANDALONE_S4B4 = $cursorPngStandaloneS4B4ProbeValue;" `
        -Label 'UEFI_PROBE_CURSOR_PNG_STANDALONE_S4B4'
    $cursorPngStandaloneS4B5ProbeValue = if ($CursorPngStandaloneS4B5Probe) { 'true' } else { 'false' }
    $patched = Assert-SingleReplacement -Text $patched `
        -Old 'private const bool UEFI_PROBE_CURSOR_PNG_STANDALONE_S4B5 = false;' `
        -New "private const bool UEFI_PROBE_CURSOR_PNG_STANDALONE_S4B5 = $cursorPngStandaloneS4B5ProbeValue;" `
        -Label 'UEFI_PROBE_CURSOR_PNG_STANDALONE_S4B5'
    $cursorPngStandaloneS4B6ProbeValue = if ($CursorPngStandaloneS4B6Probe) { 'true' } else { 'false' }
    $patched = Assert-SingleReplacement -Text $patched `
        -Old 'private const bool UEFI_PROBE_CURSOR_PNG_STANDALONE_S4B6 = false;' `
        -New "private const bool UEFI_PROBE_CURSOR_PNG_STANDALONE_S4B6 = $cursorPngStandaloneS4B6ProbeValue;" `
        -Label 'UEFI_PROBE_CURSOR_PNG_STANDALONE_S4B6'
    $cursorPngStandaloneS4B7RProbeValue = if ($CursorPngStandaloneS4B7RProbe) { 'true' } else { 'false' }
    $patched = Assert-SingleReplacement -Text $patched `
        -Old 'private const bool UEFI_PROBE_CURSOR_PNG_STANDALONE_S4B7R = false;' `
        -New "private const bool UEFI_PROBE_CURSOR_PNG_STANDALONE_S4B7R = $cursorPngStandaloneS4B7RProbeValue;" `
        -Label 'UEFI_PROBE_CURSOR_PNG_STANDALONE_S4B7R'
    $cursorPngStandaloneS4B7SProbeValue = if ($CursorPngStandaloneS4B7SProbe) { 'true' } else { 'false' }
    $patched = Assert-SingleReplacement -Text $patched `
        -Old 'private const bool UEFI_PROBE_CURSOR_PNG_STANDALONE_S4B7S = false;' `
        -New "private const bool UEFI_PROBE_CURSOR_PNG_STANDALONE_S4B7S = $cursorPngStandaloneS4B7SProbeValue;" `
        -Label 'UEFI_PROBE_CURSOR_PNG_STANDALONE_S4B7S'
    $cursorPngStandaloneS4B7TProbeValue = if ($CursorPngStandaloneS4B7TProbe) { 'true' } else { 'false' }
    $patched = Assert-SingleReplacement -Text $patched `
        -Old 'private const bool UEFI_PROBE_CURSOR_PNG_STANDALONE_S4B7T = false;' `
        -New "private const bool UEFI_PROBE_CURSOR_PNG_STANDALONE_S4B7T = $cursorPngStandaloneS4B7TProbeValue;" `
        -Label 'UEFI_PROBE_CURSOR_PNG_STANDALONE_S4B7T'
    $cursorPngStandaloneS4B7T2ProbeValue = if ($CursorPngStandaloneS4B7T2Probe) { 'true' } else { 'false' }
    $patched = Assert-SingleReplacement -Text $patched `
        -Old 'private const bool UEFI_PROBE_CURSOR_PNG_STANDALONE_S4B7T2 = false;' `
        -New "private const bool UEFI_PROBE_CURSOR_PNG_STANDALONE_S4B7T2 = $cursorPngStandaloneS4B7T2ProbeValue;" `
        -Label 'UEFI_PROBE_CURSOR_PNG_STANDALONE_S4B7T2'
    $cursorPngStandaloneS4B7UProbeValue = 'false'
    $patched = Assert-SingleReplacement -Text $patched `
        -Old 'private const bool UEFI_PROBE_CURSOR_PNG_STANDALONE_S4B7U = false;' `
        -New "private const bool UEFI_PROBE_CURSOR_PNG_STANDALONE_S4B7U = $cursorPngStandaloneS4B7UProbeValue;" `
        -Label 'UEFI_PROBE_CURSOR_PNG_STANDALONE_S4B7U'
    $cursorPngStandaloneS4C0ProbeValue = if ($CursorPngStandaloneS4C0Probe) { 'true' } else { 'false' }
    $patched = Assert-SingleReplacement -Text $patched `
        -Old 'private const bool UEFI_PROBE_CURSOR_PNG_STANDALONE_S4C0 = false;' `
        -New "private const bool UEFI_PROBE_CURSOR_PNG_STANDALONE_S4C0 = $cursorPngStandaloneS4C0ProbeValue;" `
        -Label 'UEFI_PROBE_CURSOR_PNG_STANDALONE_S4C0'
    $cursorPngStandaloneS4C1ProbeValue = if ($CursorPngStandaloneS4C1Probe) { 'true' } else { 'false' }
    $patched = Assert-SingleReplacement -Text $patched `
        -Old 'private const bool UEFI_PROBE_CURSOR_PNG_STANDALONE_S4C1 = false;' `
        -New "private const bool UEFI_PROBE_CURSOR_PNG_STANDALONE_S4C1 = $cursorPngStandaloneS4C1ProbeValue;" `
        -Label 'UEFI_PROBE_CURSOR_PNG_STANDALONE_S4C1'
    $cursorPngStandaloneS4C2ProbeValue = if ($CursorPngStandaloneS4C2Probe) { 'true' } else { 'false' }
    $patched = Assert-SingleReplacement -Text $patched `
        -Old 'private const bool UEFI_PROBE_CURSOR_PNG_STANDALONE_S4C2 = false;' `
        -New "private const bool UEFI_PROBE_CURSOR_PNG_STANDALONE_S4C2 = $cursorPngStandaloneS4C2ProbeValue;" `
        -Label 'UEFI_PROBE_CURSOR_PNG_STANDALONE_S4C2'
    $cursorPngStandaloneS4C3ProbeValue = if ($CursorPngStandaloneS4C3Probe) { 'true' } else { 'false' }
    $patched = Assert-SingleReplacement -Text $patched `
        -Old 'private const bool UEFI_PROBE_CURSOR_PNG_STANDALONE_S4C3 = false;' `
        -New "private const bool UEFI_PROBE_CURSOR_PNG_STANDALONE_S4C3 = $cursorPngStandaloneS4C3ProbeValue;" `
        -Label 'UEFI_PROBE_CURSOR_PNG_STANDALONE_S4C3'
    $cursorPngStandaloneS4C4ProbeValue = if ($CursorPngStandaloneS4C4Probe) { 'true' } else { 'false' }
    $patched = Assert-SingleReplacement -Text $patched `
        -Old 'private const bool UEFI_PROBE_CURSOR_PNG_STANDALONE_S4C4 = false;' `
        -New "private const bool UEFI_PROBE_CURSOR_PNG_STANDALONE_S4C4 = $cursorPngStandaloneS4C4ProbeValue;" `
        -Label 'UEFI_PROBE_CURSOR_PNG_STANDALONE_S4C4'
    $cursorPngStandaloneS4D0ProbeValue = if ($CursorPngStandaloneS4D0Probe) { 'true' } else { 'false' }
    $patched = Assert-SingleReplacement -Text $patched `
        -Old 'private const bool UEFI_PROBE_CURSOR_PNG_STANDALONE_S4D0 = false;' `
        -New "private const bool UEFI_PROBE_CURSOR_PNG_STANDALONE_S4D0 = $cursorPngStandaloneS4D0ProbeValue;" `
        -Label 'UEFI_PROBE_CURSOR_PNG_STANDALONE_S4D0'
    $cursorPngStandaloneS4D1ProbeValue = if ($CursorPngStandaloneS4D1Probe) { 'true' } else { 'false' }
    $patched = Assert-SingleReplacement -Text $patched `
        -Old 'private const bool UEFI_PROBE_CURSOR_PNG_STANDALONE_S4D1 = false;' `
        -New "private const bool UEFI_PROBE_CURSOR_PNG_STANDALONE_S4D1 = $cursorPngStandaloneS4D1ProbeValue;" `
        -Label 'UEFI_PROBE_CURSOR_PNG_STANDALONE_S4D1'
    $cursorPngStandaloneS4D2ProbeValue = if ($CursorPngStandaloneS4D2Probe) { 'true' } else { 'false' }
    $patched = Assert-SingleReplacement -Text $patched `
        -Old 'private const bool UEFI_PROBE_CURSOR_PNG_STANDALONE_S4D2 = false;' `
        -New "private const bool UEFI_PROBE_CURSOR_PNG_STANDALONE_S4D2 = $cursorPngStandaloneS4D2ProbeValue;" `
        -Label 'UEFI_PROBE_CURSOR_PNG_STANDALONE_S4D2'
    $cursorPngStandaloneS4D3ProbeValue = if ($CursorPngStandaloneS4D3Probe) { 'true' } else { 'false' }
    $patched = Assert-SingleReplacement -Text $patched `
        -Old 'private const bool UEFI_PROBE_CURSOR_PNG_STANDALONE_S4D3 = false;' `
        -New "private const bool UEFI_PROBE_CURSOR_PNG_STANDALONE_S4D3 = $cursorPngStandaloneS4D3ProbeValue;" `
        -Label 'UEFI_PROBE_CURSOR_PNG_STANDALONE_S4D3'
    $cursorPngStandaloneS4E0ProbeValue = if ($CursorPngStandaloneS4E0Probe) { 'true' } else { 'false' }
    $patched = Assert-SingleReplacement -Text $patched `
        -Old 'private const bool UEFI_PROBE_CURSOR_PNG_STANDALONE_S4E0 = false;' `
        -New "private const bool UEFI_PROBE_CURSOR_PNG_STANDALONE_S4E0 = $cursorPngStandaloneS4E0ProbeValue;" `
        -Label 'UEFI_PROBE_CURSOR_PNG_STANDALONE_S4E0'
    $cursorPngStandaloneS4E1ProbeValue = if ($CursorPngStandaloneS4E1Probe) { 'true' } else { 'false' }
    $patched = Assert-SingleReplacement -Text $patched `
        -Old 'private const bool UEFI_PROBE_CURSOR_PNG_STANDALONE_S4E1 = false;' `
        -New "private const bool UEFI_PROBE_CURSOR_PNG_STANDALONE_S4E1 = $cursorPngStandaloneS4E1ProbeValue;" `
        -Label 'UEFI_PROBE_CURSOR_PNG_STANDALONE_S4E1'
    $cursorPngStandaloneS4E2ProbeValue = if ($CursorPngStandaloneS4E2Probe) { 'true' } else { 'false' }
    $patched = Assert-SingleReplacement -Text $patched `
        -Old 'private const bool UEFI_PROBE_CURSOR_PNG_STANDALONE_S4E2 = false;' `
        -New "private const bool UEFI_PROBE_CURSOR_PNG_STANDALONE_S4E2 = $cursorPngStandaloneS4E2ProbeValue;" `
        -Label 'UEFI_PROBE_CURSOR_PNG_STANDALONE_S4E2'
    $cursorPngStandaloneS4E3ProbeValue = if ($CursorPngStandaloneS4E3Probe) { 'true' } else { 'false' }
    $patched = Assert-SingleReplacement -Text $patched `
        -Old 'private const bool UEFI_PROBE_CURSOR_PNG_STANDALONE_S4E3 = false;' `
        -New "private const bool UEFI_PROBE_CURSOR_PNG_STANDALONE_S4E3 = $cursorPngStandaloneS4E3ProbeValue;" `
        -Label 'UEFI_PROBE_CURSOR_PNG_STANDALONE_S4E3'
    $cursorPngStandaloneS4E4ProbeValue = if ($CursorPngStandaloneS4E4Probe) { 'true' } else { 'false' }
    $patched = Assert-SingleReplacement -Text $patched `
        -Old 'private const bool UEFI_PROBE_CURSOR_PNG_STANDALONE_S4E4 = false;' `
        -New "private const bool UEFI_PROBE_CURSOR_PNG_STANDALONE_S4E4 = $cursorPngStandaloneS4E4ProbeValue;" `
        -Label 'UEFI_PROBE_CURSOR_PNG_STANDALONE_S4E4'
    $cursorPngStandaloneS4B7ProbeValue = if ($CursorPngStandaloneS4B7Probe) { 'true' } else { 'false' }
    $patched = Assert-SingleReplacement -Text $patched `
        -Old 'private const bool UEFI_PROBE_CURSOR_PNG_STANDALONE_S4B7 = false;' `
        -New "private const bool UEFI_PROBE_CURSOR_PNG_STANDALONE_S4B7 = $cursorPngStandaloneS4B7ProbeValue;" `
        -Label 'UEFI_PROBE_CURSOR_PNG_STANDALONE_S4B7'
    $cursorPngStandaloneS4B8ProbeValue = if ($CursorPngStandaloneS4B8Probe) { 'true' } else { 'false' }
    $patched = Assert-SingleReplacement -Text $patched `
        -Old 'private const bool UEFI_PROBE_CURSOR_PNG_STANDALONE_S4B8 = false;' `
        -New "private const bool UEFI_PROBE_CURSOR_PNG_STANDALONE_S4B8 = $cursorPngStandaloneS4B8ProbeValue;" `
        -Label 'UEFI_PROBE_CURSOR_PNG_STANDALONE_S4B8'
    $cursorPngStandaloneS4BProbeValue = if ($CursorPngStandaloneS4BProbe) { 'true' } else { 'false' }
    $patched = Assert-SingleReplacement -Text $patched `
        -Old 'private const bool UEFI_PROBE_CURSOR_PNG_STANDALONE_S4B = false;' `
        -New "private const bool UEFI_PROBE_CURSOR_PNG_STANDALONE_S4B = $cursorPngStandaloneS4BProbeValue;" `
        -Label 'UEFI_PROBE_CURSOR_PNG_STANDALONE_S4B'
    $cursorPngStandaloneS4CProbeValue = if ($CursorPngStandaloneS4CProbe) { 'true' } else { 'false' }
    $patched = Assert-SingleReplacement -Text $patched `
        -Old 'private const bool UEFI_PROBE_CURSOR_PNG_STANDALONE_S4C = false;' `
        -New "private const bool UEFI_PROBE_CURSOR_PNG_STANDALONE_S4C = $cursorPngStandaloneS4CProbeValue;" `
        -Label 'UEFI_PROBE_CURSOR_PNG_STANDALONE_S4C'
    $cursorPngStandaloneS4DProbeValue = if ($CursorPngStandaloneS4DProbe) { 'true' } else { 'false' }
    $patched = Assert-SingleReplacement -Text $patched `
        -Old 'private const bool UEFI_PROBE_CURSOR_PNG_STANDALONE_S4D = false;' `
        -New "private const bool UEFI_PROBE_CURSOR_PNG_STANDALONE_S4D = $cursorPngStandaloneS4DProbeValue;" `
        -Label 'UEFI_PROBE_CURSOR_PNG_STANDALONE_S4D'
    $cursorPngStandaloneS4EProbeValue = if ($CursorPngStandaloneS4EProbe) { 'true' } else { 'false' }
    $patched = Assert-SingleReplacement -Text $patched `
        -Old 'private const bool UEFI_PROBE_CURSOR_PNG_STANDALONE_S4E = false;' `
        -New "private const bool UEFI_PROBE_CURSOR_PNG_STANDALONE_S4E = $cursorPngStandaloneS4EProbeValue;" `
        -Label 'UEFI_PROBE_CURSOR_PNG_STANDALONE_S4E'
    $cursorPngStandaloneS4FProbeValue = if ($CursorPngStandaloneS4FProbe) { 'true' } else { 'false' }
    $patched = Assert-SingleReplacement -Text $patched `
        -Old 'private const bool UEFI_PROBE_CURSOR_PNG_STANDALONE_S4F = false;' `
        -New "private const bool UEFI_PROBE_CURSOR_PNG_STANDALONE_S4F = $cursorPngStandaloneS4FProbeValue;" `
        -Label 'UEFI_PROBE_CURSOR_PNG_STANDALONE_S4F'
    $cursorPngStandaloneS4GProbeValue = if ($CursorPngStandaloneS4GProbe) { 'true' } else { 'false' }
    $patched = Assert-SingleReplacement -Text $patched `
        -Old 'private const bool UEFI_PROBE_CURSOR_PNG_STANDALONE_S4G = false;' `
        -New "private const bool UEFI_PROBE_CURSOR_PNG_STANDALONE_S4G = $cursorPngStandaloneS4GProbeValue;" `
        -Label 'UEFI_PROBE_CURSOR_PNG_STANDALONE_S4G'
    $cursorPngStandaloneS4HProbeValue = if ($CursorPngStandaloneS4HProbe) { 'true' } else { 'false' }
    $patched = Assert-SingleReplacement -Text $patched `
        -Old 'private const bool UEFI_PROBE_CURSOR_PNG_STANDALONE_S4H = false;' `
        -New "private const bool UEFI_PROBE_CURSOR_PNG_STANDALONE_S4H = $cursorPngStandaloneS4HProbeValue;" `
        -Label 'UEFI_PROBE_CURSOR_PNG_STANDALONE_S4H'
    $cursorPngLoadWrapperProbeValue = if ($CursorPngLoadWrapperProbe) { 'true' } else { 'false' }
    $patched = Assert-SingleReplacement -Text $patched `
        -Old 'private const bool UEFI_PROBE_CURSOR_PNG_LOAD_WRAPPER = false;' `
        -New "private const bool UEFI_PROBE_CURSOR_PNG_LOAD_WRAPPER = $cursorPngLoadWrapperProbeValue;" `
        -Label 'UEFI_PROBE_CURSOR_PNG_LOAD_WRAPPER'
    $cursorPngLoadAfterIhdrProbeValue = if ($CursorPngLoadAfterIhdrProbe) { 'true' } else { 'false' }
    $patched = Assert-SingleReplacement -Text $patched `
        -Old 'private const bool UEFI_PROBE_CURSOR_PNG_LOAD_AFTER_IHDR = false;' `
        -New "private const bool UEFI_PROBE_CURSOR_PNG_LOAD_AFTER_IHDR = $cursorPngLoadAfterIhdrProbeValue;" `
        -Label 'UEFI_PROBE_CURSOR_PNG_LOAD_AFTER_IHDR'
    $cursorPngLoadAfterChunkScanProbeValue = if ($CursorPngLoadAfterChunkScanProbe) { 'true' } else { 'false' }
    $patched = Assert-SingleReplacement -Text $patched `
        -Old 'private const bool UEFI_PROBE_CURSOR_PNG_LOAD_AFTER_CHUNK_SCAN = false;' `
        -New "private const bool UEFI_PROBE_CURSOR_PNG_LOAD_AFTER_CHUNK_SCAN = $cursorPngLoadAfterChunkScanProbeValue;" `
        -Label 'UEFI_PROBE_CURSOR_PNG_LOAD_AFTER_CHUNK_SCAN'
    $cursorPngLoadAfterIdatAggregationProbeValue = if ($CursorPngLoadAfterIdatAggregationProbe) { 'true' } else { 'false' }
    $patched = Assert-SingleReplacement -Text $patched `
        -Old 'private const bool UEFI_PROBE_CURSOR_PNG_LOAD_AFTER_IDAT_AGGREGATION = false;' `
        -New "private const bool UEFI_PROBE_CURSOR_PNG_LOAD_AFTER_IDAT_AGGREGATION = $cursorPngLoadAfterIdatAggregationProbeValue;" `
        -Label 'UEFI_PROBE_CURSOR_PNG_LOAD_AFTER_IDAT_AGGREGATION'
    $cursorPngDecompressPreMetaProbeValue = if ($CursorPngDecompressPreMetaProbe) { 'true' } else { 'false' }
    $patched = Assert-SingleReplacement -Text $patched `
        -Old 'private const bool UEFI_PROBE_CURSOR_PNG_DECOMPRESS_PREMETA = false;' `
        -New "private const bool UEFI_PROBE_CURSOR_PNG_DECOMPRESS_PREMETA = $cursorPngDecompressPreMetaProbeValue;" `
        -Label 'UEFI_PROBE_CURSOR_PNG_DECOMPRESS_PREMETA'
    $cursorPngDecompressNoopProbeValue = if ($CursorPngDecompressNoopProbe) { 'true' } else { 'false' }
    $patched = Assert-SingleReplacement -Text $patched `
        -Old 'private const bool UEFI_PROBE_CURSOR_PNG_DECOMPRESS_NOOP = false;' `
        -New "private const bool UEFI_PROBE_CURSOR_PNG_DECOMPRESS_NOOP = $cursorPngDecompressNoopProbeValue;" `
        -Label 'UEFI_PROBE_CURSOR_PNG_DECOMPRESS_NOOP'
    $cursorPngDecompressBytesNoopProbeValue = if ($CursorPngDecompressBytesNoopProbe) { 'true' } else { 'false' }
    $patched = Assert-SingleReplacement -Text $patched `
        -Old 'private const bool UEFI_PROBE_CURSOR_PNG_DECOMPRESS_BYTES_NOOP = false;' `
        -New "private const bool UEFI_PROBE_CURSOR_PNG_DECOMPRESS_BYTES_NOOP = $cursorPngDecompressBytesNoopProbeValue;" `
        -Label 'UEFI_PROBE_CURSOR_PNG_DECOMPRESS_BYTES_NOOP'
    $cursorPngDecompressZlibHeaderProbeValue = if ($CursorPngDecompressZlibHeaderProbe) { 'true' } else { 'false' }
    $patched = Assert-SingleReplacement -Text $patched `
        -Old 'private const bool UEFI_PROBE_CURSOR_PNG_DECOMPRESS_ZLIB_HEADER = false;' `
        -New "private const bool UEFI_PROBE_CURSOR_PNG_DECOMPRESS_ZLIB_HEADER = $cursorPngDecompressZlibHeaderProbeValue;" `
        -Label 'UEFI_PROBE_CURSOR_PNG_DECOMPRESS_ZLIB_HEADER'
    $cursorPngDecompressOutputAllocProbeValue = if ($CursorPngDecompressOutputAllocProbe) { 'true' } else { 'false' }
    $patched = Assert-SingleReplacement -Text $patched `
        -Old 'private const bool UEFI_PROBE_CURSOR_PNG_DECOMPRESS_OUTPUT_ALLOC = false;' `
        -New "private const bool UEFI_PROBE_CURSOR_PNG_DECOMPRESS_OUTPUT_ALLOC = $cursorPngDecompressOutputAllocProbeValue;" `
        -Label 'UEFI_PROBE_CURSOR_PNG_DECOMPRESS_OUTPUT_ALLOC'
    $cursorPngDecompressDeflateHeaderProbeValue = if ($CursorPngDecompressDeflateHeaderProbe) { 'true' } else { 'false' }
    $patched = Assert-SingleReplacement -Text $patched `
        -Old 'private const bool UEFI_PROBE_CURSOR_PNG_DECOMPRESS_DEFLATE_HEADER = false;' `
        -New "private const bool UEFI_PROBE_CURSOR_PNG_DECOMPRESS_DEFLATE_HEADER = $cursorPngDecompressDeflateHeaderProbeValue;" `
        -Label 'UEFI_PROBE_CURSOR_PNG_DECOMPRESS_DEFLATE_HEADER'
    $cursorPngDecompressHuffmanSetupProbeValue = if ($CursorPngDecompressHuffmanSetupProbe) { 'true' } else { 'false' }
    $patched = Assert-SingleReplacement -Text $patched `
        -Old 'private const bool UEFI_PROBE_CURSOR_PNG_DECOMPRESS_HUFFMAN_SETUP = false;' `
        -New "private const bool UEFI_PROBE_CURSOR_PNG_DECOMPRESS_HUFFMAN_SETUP = $cursorPngDecompressHuffmanSetupProbeValue;" `
        -Label 'UEFI_PROBE_CURSOR_PNG_DECOMPRESS_HUFFMAN_SETUP'
    $cursorPngDecompressGateInlineProbeValue = if ($CursorPngDecompressGateInlineProbe) { 'true' } else { 'false' }
    $patched = Assert-SingleReplacement -Text $patched `
        -Old 'private const bool UEFI_PROBE_CURSOR_PNG_DECOMPRESS_GATE_INLINE = false;' `
        -New "private const bool UEFI_PROBE_CURSOR_PNG_DECOMPRESS_GATE_INLINE = $cursorPngDecompressGateInlineProbeValue;" `
        -Label 'UEFI_PROBE_CURSOR_PNG_DECOMPRESS_GATE_INLINE'
    $cursorPngDecompressGateHelperReturnProbeValue = if ($CursorPngDecompressGateHelperReturnProbe) { 'true' } else { 'false' }
    $patched = Assert-SingleReplacement -Text $patched `
        -Old 'private const bool UEFI_PROBE_CURSOR_PNG_DECOMPRESS_GATE_HELPER_RETURN = false;' `
        -New "private const bool UEFI_PROBE_CURSOR_PNG_DECOMPRESS_GATE_HELPER_RETURN = $cursorPngDecompressGateHelperReturnProbeValue;" `
        -Label 'UEFI_PROBE_CURSOR_PNG_DECOMPRESS_GATE_HELPER_RETURN'
    $cursorPngDecompressGateBoolOnlyProbeValue = if ($CursorPngDecompressGateBoolOnlyProbe) { 'true' } else { 'false' }
    $patched = Assert-SingleReplacement -Text $patched `
        -Old 'private const bool UEFI_PROBE_CURSOR_PNG_DECOMPRESS_GATE_BOOL_ONLY = false;' `
        -New "private const bool UEFI_PROBE_CURSOR_PNG_DECOMPRESS_GATE_BOOL_ONLY = $cursorPngDecompressGateBoolOnlyProbeValue;" `
        -Label 'UEFI_PROBE_CURSOR_PNG_DECOMPRESS_GATE_BOOL_ONLY'
    $cursorPngDecompressGateNoStateCopyProbeValue = if ($CursorPngDecompressGateNoStateCopyProbe) { 'true' } else { 'false' }
    $patched = Assert-SingleReplacement -Text $patched `
        -Old 'private const bool UEFI_PROBE_CURSOR_PNG_DECOMPRESS_GATE_NO_STATE_COPY = false;' `
        -New "private const bool UEFI_PROBE_CURSOR_PNG_DECOMPRESS_GATE_NO_STATE_COPY = $cursorPngDecompressGateNoStateCopyProbeValue;" `
        -Label 'UEFI_PROBE_CURSOR_PNG_DECOMPRESS_GATE_NO_STATE_COPY'
    $cursorPngDecompressPostGateFirstInstructionProbeValue = if ($CursorPngDecompressPostGateFirstInstructionProbe) { 'true' } else { 'false' }
    $patched = Assert-SingleReplacement -Text $patched `
        -Old 'private const bool UEFI_PROBE_CURSOR_PNG_DECOMPRESS_POST_GATE_FIRST_INSTRUCTION = false;' `
        -New "private const bool UEFI_PROBE_CURSOR_PNG_DECOMPRESS_POST_GATE_FIRST_INSTRUCTION = $cursorPngDecompressPostGateFirstInstructionProbeValue;" `
        -Label 'UEFI_PROBE_CURSOR_PNG_DECOMPRESS_POST_GATE_FIRST_INSTRUCTION'
    $cursorPngDecompressAfterInputGateProbeValue = if ($CursorPngDecompressAfterInputGateProbe) { 'true' } else { 'false' }
    $patched = Assert-SingleReplacement -Text $patched `
        -Old 'private const bool UEFI_PROBE_CURSOR_PNG_DECOMPRESS_AFTER_INPUT_GATE = false;' `
        -New "private const bool UEFI_PROBE_CURSOR_PNG_DECOMPRESS_AFTER_INPUT_GATE = $cursorPngDecompressAfterInputGateProbeValue;" `
        -Label 'UEFI_PROBE_CURSOR_PNG_DECOMPRESS_AFTER_INPUT_GATE'
    $cursorPngDecompressPrepNoopProbeValue = if ($CursorPngDecompressPrepNoopProbe) { 'true' } else { 'false' }
    $patched = Assert-SingleReplacement -Text $patched `
        -Old 'private const bool UEFI_PROBE_CURSOR_PNG_DECOMPRESS_PREP_NOOP = false;' `
        -New "private const bool UEFI_PROBE_CURSOR_PNG_DECOMPRESS_PREP_NOOP = $cursorPngDecompressPrepNoopProbeValue;" `
        -Label 'UEFI_PROBE_CURSOR_PNG_DECOMPRESS_PREP_NOOP'
    $cursorPngDecompressPrepMetadataProbeValue = if ($CursorPngDecompressPrepMetadataProbe) { 'true' } else { 'false' }
    $patched = Assert-SingleReplacement -Text $patched `
        -Old 'private const bool UEFI_PROBE_CURSOR_PNG_DECOMPRESS_PREP_METADATA = false;' `
        -New "private const bool UEFI_PROBE_CURSOR_PNG_DECOMPRESS_PREP_METADATA = $cursorPngDecompressPrepMetadataProbeValue;" `
        -Label 'UEFI_PROBE_CURSOR_PNG_DECOMPRESS_PREP_METADATA'
    $cursorPngDecompressPrepBytesProbeValue = if ($CursorPngDecompressPrepBytesProbe) { 'true' } else { 'false' }
    $patched = Assert-SingleReplacement -Text $patched `
        -Old 'private const bool UEFI_PROBE_CURSOR_PNG_DECOMPRESS_PREP_BYTES = false;' `
        -New "private const bool UEFI_PROBE_CURSOR_PNG_DECOMPRESS_PREP_BYTES = $cursorPngDecompressPrepBytesProbeValue;" `
        -Label 'UEFI_PROBE_CURSOR_PNG_DECOMPRESS_PREP_BYTES'
    $cursorPngDecompressPrepInlineProbeValue = if ($CursorPngDecompressPrepInlineProbe) { 'true' } else { 'false' }
    $patched = Assert-SingleReplacement -Text $patched `
        -Old 'private const bool UEFI_PROBE_CURSOR_PNG_DECOMPRESS_PREP_INLINE = false;' `
        -New "private const bool UEFI_PROBE_CURSOR_PNG_DECOMPRESS_PREP_INLINE = $cursorPngDecompressPrepInlineProbeValue;" `
        -Label 'UEFI_PROBE_CURSOR_PNG_DECOMPRESS_PREP_INLINE'
    $cursorPngDecompressPrepBoundaryProbeValue = if ($CursorPngDecompressPrepBoundaryProbe) { 'true' } else { 'false' }
    $patched = Assert-SingleReplacement -Text $patched `
        -Old 'private const bool UEFI_PROBE_CURSOR_PNG_DECOMPRESS_PREP_BOUNDARY = false;' `
        -New "private const bool UEFI_PROBE_CURSOR_PNG_DECOMPRESS_PREP_BOUNDARY = $cursorPngDecompressPrepBoundaryProbeValue;" `
        -Label 'UEFI_PROBE_CURSOR_PNG_DECOMPRESS_PREP_BOUNDARY'
    $cursorPngDecompressPrepContextProbeValue = if ($CursorPngDecompressPrepContextProbe) { 'true' } else { 'false' }
    $patched = Assert-SingleReplacement -Text $patched `
        -Old 'private const bool UEFI_PROBE_CURSOR_PNG_DECOMPRESS_PREP_CONTEXT = false;' `
        -New "private const bool UEFI_PROBE_CURSOR_PNG_DECOMPRESS_PREP_CONTEXT = $cursorPngDecompressPrepContextProbeValue;" `
        -Label 'UEFI_PROBE_CURSOR_PNG_DECOMPRESS_PREP_CONTEXT'
    $cursorPngDecompressInflateBoundaryProbeValue = if ($CursorPngDecompressInflateBoundaryProbe) { 'true' } else { 'false' }
    $patched = Assert-SingleReplacement -Text $patched `
        -Old 'private const bool UEFI_PROBE_CURSOR_PNG_DECOMPRESS_INFLATE_BOUNDARY = false;' `
        -New "private const bool UEFI_PROBE_CURSOR_PNG_DECOMPRESS_INFLATE_BOUNDARY = $cursorPngDecompressInflateBoundaryProbeValue;" `
        -Label 'UEFI_PROBE_CURSOR_PNG_DECOMPRESS_INFLATE_BOUNDARY'
    $cursorPngDecompressInflateBitReaderProbeValue = if ($CursorPngDecompressInflateBitReaderProbe) { 'true' } else { 'false' }
    $patched = Assert-SingleReplacement -Text $patched `
        -Old 'private const bool UEFI_PROBE_CURSOR_PNG_DECOMPRESS_INFLATE_BITREADER = false;' `
        -New "private const bool UEFI_PROBE_CURSOR_PNG_DECOMPRESS_INFLATE_BITREADER = $cursorPngDecompressInflateBitReaderProbeValue;" `
        -Label 'UEFI_PROBE_CURSOR_PNG_DECOMPRESS_INFLATE_BITREADER'
    $cursorPngDecompressInflateFirstSymbolDecodeProbeValue = if ($CursorPngDecompressInflateFirstSymbolDecodeProbe) { 'true' } else { 'false' }
    $patched = Assert-SingleReplacement -Text $patched `
        -Old 'private const bool UEFI_PROBE_CURSOR_PNG_DECOMPRESS_INFLATE_FIRST_SYMBOL_DECODE = false;' `
        -New "private const bool UEFI_PROBE_CURSOR_PNG_DECOMPRESS_INFLATE_FIRST_SYMBOL_DECODE = $cursorPngDecompressInflateFirstSymbolDecodeProbeValue;" `
        -Label 'UEFI_PROBE_CURSOR_PNG_DECOMPRESS_INFLATE_FIRST_SYMBOL_DECODE'
    $cursorPngDecompressInflateLiteralWriteProbeValue = if ($CursorPngDecompressInflateLiteralWriteProbe) { 'true' } else { 'false' }
    $patched = Assert-SingleReplacement -Text $patched `
        -Old 'private const bool UEFI_PROBE_CURSOR_PNG_DECOMPRESS_INFLATE_LITERAL_WRITE = false;' `
        -New "private const bool UEFI_PROBE_CURSOR_PNG_DECOMPRESS_INFLATE_LITERAL_WRITE = $cursorPngDecompressInflateLiteralWriteProbeValue;" `
        -Label 'UEFI_PROBE_CURSOR_PNG_DECOMPRESS_INFLATE_LITERAL_WRITE'
    $cursorPngDecompressInflateLengthDistanceProbeValue = if ($CursorPngDecompressInflateLengthDistanceProbe) { 'true' } else { 'false' }
    $patched = Assert-SingleReplacement -Text $patched `
        -Old 'private const bool UEFI_PROBE_CURSOR_PNG_DECOMPRESS_INFLATE_LENGTH_DISTANCE = false;' `
        -New "private const bool UEFI_PROBE_CURSOR_PNG_DECOMPRESS_INFLATE_LENGTH_DISTANCE = $cursorPngDecompressInflateLengthDistanceProbeValue;" `
        -Label 'UEFI_PROBE_CURSOR_PNG_DECOMPRESS_INFLATE_LENGTH_DISTANCE'
    $cursorPngDecompressInflateOneStepProbeValue = if ($CursorPngDecompressInflateOneStepProbe) { 'true' } else { 'false' }
    $patched = Assert-SingleReplacement -Text $patched `
        -Old 'private const bool UEFI_PROBE_CURSOR_PNG_DECOMPRESS_INFLATE_ONE_STEP = false;' `
        -New "private const bool UEFI_PROBE_CURSOR_PNG_DECOMPRESS_INFLATE_ONE_STEP = $cursorPngDecompressInflateOneStepProbeValue;" `
        -Label 'UEFI_PROBE_CURSOR_PNG_DECOMPRESS_INFLATE_ONE_STEP'
    $cursorPngDecompressInflateSmokeProbeValue = if ($CursorPngDecompressInflateSmokeProbe) { 'true' } else { 'false' }
    $patched = Assert-SingleReplacement -Text $patched `
        -Old 'private const bool UEFI_PROBE_CURSOR_PNG_DECOMPRESS_INFLATE_SMOKE = false;' `
        -New "private const bool UEFI_PROBE_CURSOR_PNG_DECOMPRESS_INFLATE_SMOKE = $cursorPngDecompressInflateSmokeProbeValue;" `
        -Label 'UEFI_PROBE_CURSOR_PNG_DECOMPRESS_INFLATE_SMOKE'
    $cursorPngDecompressTinyBoundaryProbeValue = if ($CursorPngDecompressTinyBoundaryProbe) { 'true' } else { 'false' }
    $patched = Assert-SingleReplacement -Text $patched `
        -Old 'private const bool UEFI_PROBE_CURSOR_PNG_DECOMPRESS_TINY_BOUNDARY = false;' `
        -New "private const bool UEFI_PROBE_CURSOR_PNG_DECOMPRESS_TINY_BOUNDARY = $cursorPngDecompressTinyBoundaryProbeValue;" `
        -Label 'UEFI_PROBE_CURSOR_PNG_DECOMPRESS_TINY_BOUNDARY'
    $cursorPngDecompressTinyBitReaderProbeValue = if ($CursorPngDecompressTinyBitReaderProbe) { 'true' } else { 'false' }
    $patched = Assert-SingleReplacement -Text $patched `
        -Old 'private const bool UEFI_PROBE_CURSOR_PNG_DECOMPRESS_TINY_BITREADER = false;' `
        -New "private const bool UEFI_PROBE_CURSOR_PNG_DECOMPRESS_TINY_BITREADER = $cursorPngDecompressTinyBitReaderProbeValue;" `
        -Label 'UEFI_PROBE_CURSOR_PNG_DECOMPRESS_TINY_BITREADER'
    $cursorPngDecompressTinyFirstSymbolProbeValue = if ($CursorPngDecompressTinyFirstSymbolProbe) { 'true' } else { 'false' }
    $patched = Assert-SingleReplacement -Text $patched `
        -Old 'private const bool UEFI_PROBE_CURSOR_PNG_DECOMPRESS_TINY_FIRST_SYMBOL = false;' `
        -New "private const bool UEFI_PROBE_CURSOR_PNG_DECOMPRESS_TINY_FIRST_SYMBOL = $cursorPngDecompressTinyFirstSymbolProbeValue;" `
        -Label 'UEFI_PROBE_CURSOR_PNG_DECOMPRESS_TINY_FIRST_SYMBOL'
    $cursorPngDecompressTinyOneOpProbeValue = if ($CursorPngDecompressTinyOneOpProbe) { 'true' } else { 'false' }
    $patched = Assert-SingleReplacement -Text $patched `
        -Old 'private const bool UEFI_PROBE_CURSOR_PNG_DECOMPRESS_TINY_ONE_OP = false;' `
        -New "private const bool UEFI_PROBE_CURSOR_PNG_DECOMPRESS_TINY_ONE_OP = $cursorPngDecompressTinyOneOpProbeValue;" `
        -Label 'UEFI_PROBE_CURSOR_PNG_DECOMPRESS_TINY_ONE_OP'
    $cursorPngDecompressTinyInflateSmokeProbeValue = if ($CursorPngDecompressTinyInflateSmokeProbe) { 'true' } else { 'false' }
    $patched = Assert-SingleReplacement -Text $patched `
        -Old 'private const bool UEFI_PROBE_CURSOR_PNG_DECOMPRESS_TINY_INFLATE_SMOKE = false;' `
        -New "private const bool UEFI_PROBE_CURSOR_PNG_DECOMPRESS_TINY_INFLATE_SMOKE = $cursorPngDecompressTinyInflateSmokeProbeValue;" `
        -Label 'UEFI_PROBE_CURSOR_PNG_DECOMPRESS_TINY_INFLATE_SMOKE'
    $cursorPngLoadAfterDecompressProbeValue = if ($CursorPngLoadAfterDecompressProbe) { 'true' } else { 'false' }
    $patched = Assert-SingleReplacement -Text $patched `
        -Old 'private const bool UEFI_PROBE_CURSOR_PNG_LOAD_AFTER_DECOMPRESS = false;' `
        -New "private const bool UEFI_PROBE_CURSOR_PNG_LOAD_AFTER_DECOMPRESS = $cursorPngLoadAfterDecompressProbeValue;" `
        -Label 'UEFI_PROBE_CURSOR_PNG_LOAD_AFTER_DECOMPRESS'
    $cursorPngLoadAfterImageCreateProbeValue = if ($CursorPngLoadAfterImageCreateProbe) { 'true' } else { 'false' }
    $patched = Assert-SingleReplacement -Text $patched `
        -Old 'private const bool UEFI_PROBE_CURSOR_PNG_LOAD_AFTER_IMAGE_CREATE = false;' `
        -New "private const bool UEFI_PROBE_CURSOR_PNG_LOAD_AFTER_IMAGE_CREATE = $cursorPngLoadAfterImageCreateProbeValue;" `
        -Label 'UEFI_PROBE_CURSOR_PNG_LOAD_AFTER_IMAGE_CREATE'
    $pngProbeEnabled = (
        $CursorSourcePngProbe -or
        $CursorPngNoopLoaderProbe -or
        $CursorPngBytesNoopProbe -or
        $CursorPngHeaderHelperProbe -or
        $CursorPngIhdrHelperProbe -or
        $CursorPngStandaloneS0Probe -or
        $CursorPngStandaloneS1Probe -or
        $CursorPngStandaloneS2Probe -or
        $CursorPngStandaloneS3Probe -or
        $CursorPngStandaloneS4Probe -or
        $CursorPngStandaloneS4AProbe -or
        $CursorPngStandaloneS4B0Probe -or
        $CursorPngStandaloneS4B1Probe -or
        $CursorPngStandaloneS4B2Probe -or
        $CursorPngStandaloneS4B3Probe -or
        $CursorPngStandaloneS4B4Probe -or
        $CursorPngStandaloneS4B5Probe -or
        $CursorPngStandaloneS4B6Probe -or
        $CursorPngStandaloneS4B7RProbe -or
        $CursorPngStandaloneS4B7SProbe -or
        $CursorPngStandaloneS4B7TProbe -or
        $CursorPngStandaloneS4B7T2Probe -or
        $CursorPngStandaloneS4B7Probe -or
        $CursorPngStandaloneS4B8Probe -or
        $CursorPngStandaloneS4BProbe -or
        $CursorPngStandaloneS4C0Probe -or
        $CursorPngStandaloneS4C1Probe -or
        $CursorPngStandaloneS4C2Probe -or
        $CursorPngStandaloneS4C3Probe -or
        $CursorPngStandaloneS4C4Probe -or
        $CursorPngStandaloneS4D0Probe -or
        $CursorPngStandaloneS4D1Probe -or
        $CursorPngStandaloneS4D2Probe -or
        $CursorPngStandaloneS4D3Probe -or
        $CursorPngStandaloneS4CProbe -or
        $CursorPngStandaloneS4DProbe -or
        $CursorPngStandaloneS4EProbe -or
        $CursorPngStandaloneS4FProbe -or
        $CursorPngStandaloneS4GProbe -or
        $CursorPngStandaloneS4HProbe -or
        $CursorPngLoadWrapperProbe -or
        $CursorPngLoadAfterIhdrProbe -or
        $CursorPngLoadAfterChunkScanProbe -or
        $CursorPngLoadAfterIdatAggregationProbe -or
        $CursorPngDecompressPreMetaProbe -or
        $CursorPngDecompressNoopProbe -or
        $CursorPngDecompressBytesNoopProbe -or
        $CursorPngDecompressZlibHeaderProbe -or
        $CursorPngDecompressOutputAllocProbe -or
        $CursorPngDecompressDeflateHeaderProbe -or
        $CursorPngDecompressHuffmanSetupProbe -or
        $CursorPngDecompressGateInlineProbe -or
        $CursorPngDecompressGateHelperReturnProbe -or
        $CursorPngDecompressGateBoolOnlyProbe -or
        $CursorPngDecompressGateNoStateCopyProbe -or
        $CursorPngDecompressPostGateFirstInstructionProbe -or
        $CursorPngDecompressAfterInputGateProbe -or
        $CursorPngDecompressPrepNoopProbe -or
        $CursorPngDecompressPrepMetadataProbe -or
        $CursorPngDecompressPrepBytesProbe -or
        $CursorPngDecompressPrepInlineProbe -or
        $CursorPngDecompressPrepBoundaryProbe -or
        $CursorPngDecompressPrepContextProbe -or
        $CursorPngDecompressInflateBoundaryProbe -or
        $CursorPngDecompressInflateBitReaderProbe -or
        $CursorPngDecompressInflateFirstSymbolDecodeProbe -or
        $CursorPngDecompressInflateLiteralWriteProbe -or
        $CursorPngDecompressInflateLengthDistanceProbe -or
        $CursorPngDecompressInflateOneStepProbe -or
        $CursorPngDecompressInflateSmokeProbe -or
        $CursorPngDecompressTinyBoundaryProbe -or
        $CursorPngDecompressTinyBitReaderProbe -or
        $CursorPngDecompressTinyFirstSymbolProbe -or
        $CursorPngDecompressTinyOneOpProbe -or
        $CursorPngDecompressTinyInflateSmokeProbe -or
        $CursorPngLoadAfterDecompressProbe -or
        $CursorPngLoadAfterImageCreateProbe
    )
    $pngLoaderVariantLabel = if ($CursorPngLoadAfterIhdrProbe) {
        'PNG-ENTRY-BASELINE'
    } elseif ($CursorPngLoadAfterChunkScanProbe) {
        'PNG-G'
    } elseif ($CursorPngLoadAfterIdatAggregationProbe) {
        'PNG-H'
    } elseif ($CursorPngDecompressPreMetaProbe) {
        'PNG-I0'
    } elseif ($CursorPngDecompressNoopProbe) {
        'PNG-I1'
    } elseif ($CursorPngDecompressBytesNoopProbe) {
        'PNG-I2'
    } elseif ($CursorPngDecompressZlibHeaderProbe) {
        'PNG-I3'
    } elseif ($CursorPngDecompressOutputAllocProbe) {
        'PNG-I4'
    } elseif ($CursorPngDecompressDeflateHeaderProbe) {
        'PNG-I5'
    } elseif ($CursorPngDecompressHuffmanSetupProbe) {
        'PNG-I6'
    } elseif ($CursorPngDecompressGateInlineProbe) {
        'PNG-I9A'
    } elseif ($CursorPngDecompressGateHelperReturnProbe) {
        'PNG-I9B'
    } elseif ($CursorPngDecompressGateBoolOnlyProbe) {
        'PNG-I9C'
    } elseif ($CursorPngDecompressGateNoStateCopyProbe) {
        'PNG-I9D'
    } elseif ($CursorPngDecompressPostGateFirstInstructionProbe) {
        'PNG-I9E'
    } elseif ($CursorPngDecompressAfterInputGateProbe) {
        'PNG-I8A'
    } elseif ($CursorPngDecompressPrepNoopProbe) {
        'PNG-I8B'
    } elseif ($CursorPngDecompressPrepMetadataProbe) {
        'PNG-I8C'
    } elseif ($CursorPngDecompressPrepBytesProbe) {
        'PNG-I8D'
    } elseif ($CursorPngDecompressPrepInlineProbe) {
        'PNG-I8E'
    } elseif ($CursorPngDecompressPrepBoundaryProbe) {
        'PNG-I8F'
    } elseif ($CursorPngDecompressPrepContextProbe) {
        'PNG-I8G'
    } elseif ($CursorPngDecompressInflateBoundaryProbe) {
        'PNG-I7A'
    } elseif ($CursorPngDecompressInflateBitReaderProbe) {
        'PNG-I7B'
    } elseif ($CursorPngDecompressInflateFirstSymbolDecodeProbe) {
        'PNG-I7C'
    } elseif ($CursorPngDecompressInflateLiteralWriteProbe) {
        'PNG-I7D'
    } elseif ($CursorPngDecompressInflateLengthDistanceProbe) {
        'PNG-I7E'
    } elseif ($CursorPngDecompressInflateOneStepProbe) {
        'PNG-I7F'
    } elseif ($CursorPngDecompressInflateSmokeProbe) {
        'PNG-I7'
    } elseif ($CursorPngDecompressTinyBoundaryProbe) {
        'PNG-I10A'
    } elseif ($CursorPngDecompressTinyBitReaderProbe) {
        'PNG-I10B'
    } elseif ($CursorPngDecompressTinyFirstSymbolProbe) {
        'PNG-I10C'
    } elseif ($CursorPngDecompressTinyOneOpProbe) {
        'PNG-I10D'
    } elseif ($CursorPngDecompressTinyInflateSmokeProbe) {
        'PNG-I10E'
    } elseif ($CursorPngLoadAfterDecompressProbe) {
        'PNG-I'
    } elseif ($CursorPngLoadAfterImageCreateProbe) {
        'PNG-J'
    } elseif ($CursorPngLoadWrapperProbe -or $CursorSourcePngProbe) {
        'PNG-LOAD-WRAPPER'
    } elseif ($CursorPngNoopLoaderProbe) {
        'PNG-NOOP'
    } elseif ($CursorPngBytesNoopProbe) {
        'PNG-BYTES-NOOP'
    } elseif ($CursorPngHeaderHelperProbe) {
        'PNG-HEADER'
    } elseif ($CursorPngIhdrHelperProbe) {
        'PNG-IHDR'
    } elseif ($CursorPngStandaloneS0Probe) {
        'PNG-S0'
    } elseif ($CursorPngStandaloneS1Probe) {
        'PNG-S1'
    } elseif ($CursorPngStandaloneS2Probe) {
        'PNG-S2'
    } elseif ($CursorPngStandaloneS3Probe) {
        'PNG-S3'
    } elseif ($CursorPngStandaloneS4Probe) {
        'PNG-S4'
    } elseif ($CursorPngStandaloneS4AProbe) {
        'PNG-S4A'
    } elseif ($CursorPngStandaloneS4B0Probe) {
        'PNG-S4B0'
    } elseif ($CursorPngStandaloneS4B1Probe) {
        'PNG-S4B1'
    } elseif ($CursorPngStandaloneS4B2Probe) {
        'PNG-S4B2'
    } elseif ($CursorPngStandaloneS4B3Probe) {
        'PNG-S4B3'
    } elseif ($CursorPngStandaloneS4B4Probe) {
        'PNG-S4B4'
    } elseif ($CursorPngStandaloneS4B5Probe) {
        'PNG-S4B5'
    } elseif ($CursorPngStandaloneS4B6Probe) {
        'PNG-S4B6'
    } elseif ($CursorPngStandaloneS4B7RProbe) {
        'PNG-S4B7R'
    } elseif ($CursorPngStandaloneS4B7SProbe) {
        'PNG-S4B7S'
    } elseif ($CursorPngStandaloneS4B7TProbe) {
        'PNG-S4B7T'
    } elseif ($CursorPngStandaloneS4B7T2Probe) {
        'PNG-S4B7T2'
    } elseif ($CursorPngStandaloneS4B7Probe) {
        'PNG-S4B7'
    } elseif ($CursorPngStandaloneS4B8Probe) {
        'PNG-S4B8'
    } elseif ($CursorPngStandaloneS4BProbe) {
        'PNG-S4B'
    } elseif ($CursorPngStandaloneS4C0Probe) {
        'PNG-S4C0'
    } elseif ($CursorPngStandaloneS4C1Probe) {
        'PNG-S4C1'
    } elseif ($CursorPngStandaloneS4C2Probe) {
        'PNG-S4C2'
    } elseif ($CursorPngStandaloneS4C3Probe) {
        'PNG-S4C3'
    } elseif ($CursorPngStandaloneS4C4Probe) {
        'PNG-S4C4'
    } elseif ($CursorPngStandaloneS4D0Probe) {
        'PNG-S4D0'
    } elseif ($CursorPngStandaloneS4D1Probe) {
        'PNG-S4D1'
} elseif ($CursorPngStandaloneS4D2Probe) {
    'PNG-S4D2'
} elseif ($CursorPngStandaloneS4D3Probe) {
    'PNG-S4D3'
} elseif ($CursorPngStandaloneS4E0Probe) {
    'PNG-S4E0'
} elseif ($CursorPngStandaloneS4E1Probe) {
    'PNG-S4E1'
} elseif ($CursorPngStandaloneS4E2Probe) {
    'PNG-S4E2'
} elseif ($CursorPngStandaloneS4E3Probe) {
    'PNG-S4E3'
} elseif ($CursorPngStandaloneS4E4Probe) {
    'PNG-S4E4'
} elseif ($CursorPngStandaloneS4CProbe) {
    'PNG-S4C'
    } elseif ($CursorPngStandaloneS4DProbe) {
        'PNG-S4D'
    } elseif ($CursorPngStandaloneS4EProbe) {
        'PNG-S4E'
    } elseif ($CursorPngStandaloneS4FProbe) {
        'PNG-S4F'
    } elseif ($CursorPngStandaloneS4GProbe) {
        'PNG-S4G'
    } elseif ($CursorPngStandaloneS4HProbe) {
        'PNG-S4H'
    } else {
        'NONE'
    }
    $pngStep8BypassValue = if ($pngProbeEnabled) { 'true' } else { 'false' }
    $patched = Assert-SingleReplacement -Text $patched `
        -Old 'internal const bool NORMAL_DESKTOP_UEFI_PROBE_BYPASS_STEP8_DRAWIMAGE = false;' `
        -New "internal const bool NORMAL_DESKTOP_UEFI_PROBE_BYPASS_STEP8_DRAWIMAGE = $pngStep8BypassValue;" `
        -Label 'NORMAL_DESKTOP_UEFI_PROBE_BYPASS_STEP8_DRAWIMAGE'
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

    $patchedProgramSnapshot = Get-FileSnapshot -Path $programPath
    $patchedProgramText = [System.IO.File]::ReadAllText($programPath)
    $sourcePathMatchesBuildPath = $programPath -eq (Join-Path $root 'guideXOS\Program.cs')
    if (-not $patchedProgramText.Contains($runId)) {
        throw "Patched Program.cs does not contain the fresh run ID: $runId"
    }
    if (-not $patchedProgramText.Contains('private const bool UEFI_ALLOW_NORMAL_DESKTOP_RENDER_PATH = true;')) {
        throw "Patched Program.cs is missing the normal desktop render-path allow gate."
    }
    if (-not $patchedProgramText.Contains('private const bool UEFI_USE_TINY_RENDER_LOOP_BYPASS = false;')) {
        throw "Patched Program.cs is missing the TinyUEFI bypass disable gate."
    }
    if (-not $patchedProgramText.Contains('private const bool NORMAL_DESKTOP_UEFI_STEP_PROBE = true;')) {
        throw "Patched Program.cs is missing the step-probe enable gate."
    }

    $preflightLog = Join-Path $probeRoot "step_probe_preflight_$runId.txt"
    $preflight = @(
        "RUN_ID=$runId"
        "PROGRAM_PATH=$programPath"
        "BUILD_SCRIPT_PATH=$buildScript"
        "KERNEL_PROJECT_PATH=$(Join-Path $root 'guideXOS\guideXOS.csproj')"
        "SOURCE_PATH_MATCH_BUILD_PATH=$sourcePathMatchesBuildPath"
        "PROGRAM_CONTAINS_RUN_ID=$($patchedProgramText.Contains($runId))"
        "PROGRAM_CONTAINS_ALLOW_NORMAL_GATE=$($patchedProgramText.Contains('private const bool UEFI_ALLOW_NORMAL_DESKTOP_RENDER_PATH = true;'))"
        "PROGRAM_CONTAINS_TINY_BYPASS_GATE=$($patchedProgramText.Contains('private const bool UEFI_USE_TINY_RENDER_LOOP_BYPASS = false;'))"
        "PROGRAM_CONTAINS_STEP_PROBE_GATE=$($patchedProgramText.Contains('private const bool NORMAL_DESKTOP_UEFI_STEP_PROBE = true;'))"
        "PROGRAM_SHA256=$($patchedProgramSnapshot.Sha256)"
        "PROGRAM_BYTES=$($patchedProgramSnapshot.Length)"
        "PROGRAM_MTIME=$($patchedProgramSnapshot.LastWriteTime.ToString('o'))"
    )
    [System.IO.File]::WriteAllLines($preflightLog, $preflight)
    Write-Host "[probe] Preflight source proof: $preflightLog" -ForegroundColor Cyan
    Write-Host "[probe] Program.cs path: $programPath" -ForegroundColor Cyan
    Write-Host "[probe] Program.cs hash: $($patchedProgramSnapshot.Sha256)" -ForegroundColor Cyan
    Write-Host "[probe] Program.cs contains run ID: $($patchedProgramText.Contains($runId))" -ForegroundColor Cyan
    Write-Host "[probe] Source path matches build path: $sourcePathMatchesBuildPath" -ForegroundColor Cyan

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
    Write-Host "[probe] Cursor draw busy direct probe: $CursorDrawBusyDirectProbe" -ForegroundColor Cyan
    Write-Host "[probe] Safe cursor image fallback: $SafeCursorImageFallback" -ForegroundColor Cyan
    Write-Host "[probe] Step 13 cursor body variant: $cursorBodyVariant" -ForegroundColor Cyan
    Write-Host "[probe] GUI visible mode: $GuiVisible" -ForegroundColor Cyan
    Write-Host "[probe] GUI screenshot path: $GuiScreenshotPath" -ForegroundColor Cyan
    Write-Host "[probe] Safe placeholders until step 10: enabled" -ForegroundColor Cyan
    Write-Host "[probe] Patched Program.cs for temporary step probe" -ForegroundColor Cyan
    Write-Host "[probe] Building via build.ps1..." -ForegroundColor Cyan
    $buildArgs = @('-NoLogo', '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $buildScript, '-SkipRamdisk')
    if ($SkipBootloader) {
        $buildArgs += '-SkipBootloader'
    }
    & powershell.exe @buildArgs
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

    $kernelArtifacts = Get-ChildItem -Path $root -Recurse -File -Filter 'kernel.elf' | Sort-Object FullName
    Write-Host "[probe] kernel.elf artifacts under repo: $($kernelArtifacts.Count)" -ForegroundColor Cyan
    foreach ($artifact in $kernelArtifacts) {
        $artifactSnapshot = Get-FileSnapshot -Path $artifact.FullName
        Write-Host ("[probe]   {0}" -f $artifactSnapshot.Path) -ForegroundColor Gray
        Write-Host ("[probe]     mtime:  {0}" -f $artifactSnapshot.LastWriteTime.ToString('o')) -ForegroundColor Gray
        Write-Host ("[probe]     sha256: {0}" -f $artifactSnapshot.Sha256) -ForegroundColor Gray
        Write-Host ("[probe]     bytes:  {0}" -f $artifactSnapshot.Length) -ForegroundColor Gray
    }
    if ($kernelArtifacts.Count -gt 1) {
        Write-Host "[probe] WARNING: multiple plausible boot artifacts exist; verify QEMU is using the intended root/ESP pair above." -ForegroundColor Yellow
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
    Write-Host "[probe] QEMU executable: $qemuExe" -ForegroundColor Cyan
    Write-Host "[probe] QEMU pflash code path: $qemuFirmwareCode" -ForegroundColor Cyan
    Write-Host "[probe] QEMU pflash vars path: $qemuFirmwareVars" -ForegroundColor Cyan
    Write-Host "[probe] QEMU ESP/disk path: fat:rw:ESP" -ForegroundColor Cyan

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

    $qemuCommandLine = Format-CmdLine -Exe $qemuExe -Args $qemuArgs
    Write-Host "[probe] QEMU command line: $qemuCommandLine" -ForegroundColor Cyan

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
        'SMAIN_DISPATCH_REASON=NORMAL_DESKTOP_UEFI_STEP_PROBE',
        'SMAIN_DISPATCH_GATE_STEP_PROBE_SELECTED'
    )) {
        if (-not $serialText.Contains($required)) {
            $validRun = $false
            Write-Host "[probe] Missing required breadcrumb: $required" -ForegroundColor Red
        }
    }

    if ($serialText.Contains('SMAIN_DISPATCH_GATE_LEGACY_SELECTED')) {
        $validRun = $false
        Write-Host "[probe] Unexpected legacy dispatch breadcrumb present: SMAIN_DISPATCH_GATE_LEGACY_SELECTED" -ForegroundColor Red
    }

    if (-not $validRun) {
        Write-Host "[probe] INVALID run: missing required breadcrumbs, so no OS inference was made." -ForegroundColor Red
        if (Test-Path $stderrLog) {
            $stderrText = [System.IO.File]::ReadAllText($stderrLog)
            if ($stderrText.Trim()) {
                Write-Host "[probe] QEMU stderr:" -ForegroundColor Yellow
                Write-Host $stderrText -ForegroundColor Yellow
            }
        }
        throw "Probe run invalid. Serial log did not contain the required fresh probe markers."
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
    $cursorPngProbeEnterPresent = $serialText.Contains('CURSOR_PNG_PROBE_ENTER')
    $cursorPngBeforeDiskCheckPresent = $serialText.Contains('CURSOR_PNG_BEFORE_DISK_CHECK')
    $cursorPngAfterDiskCheckPresent = $serialText.Contains('CURSOR_PNG_AFTER_DISK_CHECK')
    $cursorPngBeforeFileLookupPresent = $serialText.Contains('CURSOR_PNG_BEFORE_FILE_LOOKUP')
    $cursorPngAfterFileLookupPresent = $serialText.Contains('CURSOR_PNG_AFTER_FILE_LOOKUP')
    $cursorPngBytesLength = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'CURSOR_PNG_BYTES_LENGTH'
    $cursorPngHeaderHelperEnterPresent = $serialText.Contains('CURSOR_PNG_HEADER_HELPER_ENTER')
    $cursorPngHeaderHelperBadPresent = $serialText.Contains('CURSOR_PNG_HEADER_HELPER_BAD')
    $cursorPngHeaderHelperOkPresent = $serialText.Contains('CURSOR_PNG_HEADER_HELPER_OK')
    $cursorPngHeaderHelperExitPresent = $serialText.Contains('CURSOR_PNG_HEADER_HELPER_EXIT')
    $cursorPngNoopLoaderEnterPresent = $serialText.Contains('CURSOR_PNG_NOOP_LOADER_ENTER')
    $cursorPngNoopLoaderExitPresent = $serialText.Contains('CURSOR_PNG_NOOP_LOADER_EXIT')
    $cursorPngBytesNoopEnterPresent = $serialText.Contains('CURSOR_PNG_BYTES_NOOP_ENTER')
    $cursorPngBytesNoopNullPresent = $serialText.Contains('CURSOR_PNG_BYTES_NOOP_NULL')
    $cursorPngBytesNoopOkPresent = $serialText.Contains('CURSOR_PNG_BYTES_NOOP_OK')
    $cursorPngBytesNoopExitPresent = $serialText.Contains('CURSOR_PNG_BYTES_NOOP_EXIT')
    $cursorPngIhdrHelperEnterPresent = $serialText.Contains('CURSOR_PNG_IHDR_HELPER_ENTER')
    $cursorPngIhdrHelperBadPresent = $serialText.Contains('CURSOR_PNG_IHDR_HELPER_BAD')
    $cursorPngIhdrHelperOkPresent = $serialText.Contains('CURSOR_PNG_IHDR_HELPER_OK')
    $cursorPngIhdrWidth = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'CURSOR_PNG_IHDR_WIDTH'
    $cursorPngIhdrHeight = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'CURSOR_PNG_IHDR_HEIGHT'
    $cursorPngIhdrHelperExitPresent = $serialText.Contains('CURSOR_PNG_IHDR_HELPER_EXIT')
    $uefiPngProbeLen = $null
    $uefiPngProbeIhdrWidth = $null
    $uefiPngProbeIhdrHeight = $null
    $uefiPngProbeIhdrBitDepth = $null
    $uefiPngProbeIhdrColorType = $null
    $uefiPngProbeIdatChunkCount = $null
    $uefiPngProbeIdatCompressedBytes = $null
    $uefiPngProbeZlibCmf = $null
    $uefiPngProbeZlibFlg = $null
    $uefiPngProbeZlibHeaderOkPresent = $false
    $uefiPngProbeOutputAllocEnterPresent = $false
    $uefiPngProbeOutputAllocOkPresent = $false
    $uefiPngProbeOutputAllocSize = $null
    $uefiPngProbeInflateEnterPresent = $false
    $uefiPngProbeInflateProgressPresent = $false
    $uefiPngProbeInflateBoundsAbortPresent = $false
    $uefiPngProbeInflateFaultPresent = $false
    $uefiPngProbeInflateOkPresent = $false
    $uefiPngProbeInflateExitPresent = $false
    $uefiPngProbeDeepestMarker = $null
    $uefiPngProbeAfterZlibHeaderPresent = $false
    $uefiPngProbeBfinal = $null
    $uefiPngProbeBtype = $null
    $uefiPngProbeRawByte = $null
    $uefiPngProbeDynamicPresent = $false
    $uefiPngProbeHlitBits = $null
    $uefiPngProbeHlit = $null
    $uefiPngProbeHdistBits = $null
    $uefiPngProbeHdist = $null
    $uefiPngProbeHclenBits = $null
    $uefiPngProbeHclen = $null
    $uefiPngProbeCountsOkPresent = $false
    $uefiPngProbeCountsBadPresent = $false
    $uefiPngProbeCodeLenAlphabetOkPresent = $false
    $uefiPngProbeTablesOkPresent = $false
    $uefiPngProbeTablesBadPresent = $false
    $uefiPngProbeFirstSymbolValue = $null
    $uefiPngProbeFirstSymbolLiteralPresent = $false
    $uefiPngProbeFirstSymbolLengthPresent = $false
    $uefiPngProbeFirstSymbolEndPresent = $false
    $uefiPngProbeOneOutputLiteralWritePresent = $false
    $uefiPngProbeOneOutputLenDistPresent = $false
    $uefiPngProbeOneOutputBoundsAbortPresent = $false
    $uefiPngProbeOneOutputOkPresent = $false
    $uefiPngProbeSmokeProgressPresent = $false
    $uefiPngProbeSmokeBoundsAbortPresent = $false
    $uefiPngProbeSmokeOkPresent = $false
    if ($CursorPngStandaloneS0Probe) {
        $uefiPngProbeLen = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S0_LEN'
        $uefiPngProbeIhdrWidth = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S0_IHDR_WIDTH'
        $uefiPngProbeIhdrHeight = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S0_IHDR_HEIGHT'
        $uefiPngProbeIhdrBitDepth = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S0_IHDR_BIT_DEPTH'
        $uefiPngProbeIhdrColorType = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S0_IHDR_COLOR_TYPE'
        $uefiPngProbeDeepestMarker = Get-DeepestSerialMarker -Text $serialText -Markers @(
            'UEFI_PNG_PROBE_S0_ENTER'
            'UEFI_PNG_PROBE_S0_BYTES_OK'
            'UEFI_PNG_PROBE_S0_HEADER_OK'
            'UEFI_PNG_PROBE_S0_IHDR_OK'
            'UEFI_PNG_PROBE_S0_EXIT'
        )
    } elseif ($CursorPngStandaloneS1Probe) {
        $uefiPngProbeLen = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S1_LEN'
        $uefiPngProbeIhdrWidth = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S1_IHDR_WIDTH'
        $uefiPngProbeIhdrHeight = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S1_IHDR_HEIGHT'
        $uefiPngProbeIhdrBitDepth = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S1_IHDR_BIT_DEPTH'
        $uefiPngProbeIhdrColorType = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S1_IHDR_COLOR_TYPE'
        $uefiPngProbeIdatChunkCount = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S1_IDAT_CHUNK_COUNT'
        $uefiPngProbeIdatCompressedBytes = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S1_IDAT_COMPRESSED_BYTES'
        $uefiPngProbeDeepestMarker = Get-DeepestSerialMarker -Text $serialText -Markers @(
            'UEFI_PNG_PROBE_S1_ENTER'
            'UEFI_PNG_PROBE_S1_CHUNK_SCAN_ENTER'
            'UEFI_PNG_PROBE_S1_IDAT_SEEN'
            'UEFI_PNG_PROBE_S1_IEND_SEEN'
            'UEFI_PNG_PROBE_S1_CHUNK_SCAN_EXIT'
            'UEFI_PNG_PROBE_S1_EXIT'
        )
    } elseif ($CursorPngStandaloneS2Probe) {
        $uefiPngProbeLen = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S2_LEN'
        $uefiPngProbeIhdrWidth = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S2_IHDR_WIDTH'
        $uefiPngProbeIhdrHeight = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S2_IHDR_HEIGHT'
        $uefiPngProbeIhdrBitDepth = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S2_IHDR_BIT_DEPTH'
        $uefiPngProbeIhdrColorType = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S2_IHDR_COLOR_TYPE'
        $uefiPngProbeIdatChunkCount = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S2_IDAT_CHUNK_COUNT'
        $uefiPngProbeIdatCompressedBytes = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S2_IDAT_BYTES'
        $uefiPngProbeDeepestMarker = Get-DeepestSerialMarker -Text $serialText -Markers @(
            'UEFI_PNG_PROBE_S2_ENTER'
            'UEFI_PNG_PROBE_S2_IDAT_AGG_ENTER'
            'UEFI_PNG_PROBE_S2_IDAT_BYTES'
            'UEFI_PNG_PROBE_S2_IDAT_AGG_EXIT'
            'UEFI_PNG_PROBE_S2_EXIT'
        )
    } elseif ($CursorPngStandaloneS3Probe) {
        $uefiPngProbeLen = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S3_LEN'
        $uefiPngProbeIhdrWidth = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S3_IHDR_WIDTH'
        $uefiPngProbeIhdrHeight = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S3_IHDR_HEIGHT'
        $uefiPngProbeIhdrBitDepth = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S3_IHDR_BIT_DEPTH'
        $uefiPngProbeIhdrColorType = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S3_IHDR_COLOR_TYPE'
        $uefiPngProbeIdatChunkCount = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S3_IDAT_CHUNK_COUNT'
        $uefiPngProbeIdatCompressedBytes = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S3_IDAT_BYTES'
        $uefiPngProbeZlibCmf = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S3_ZLIB_CMF'
        $uefiPngProbeZlibFlg = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S3_ZLIB_FLG'
        $uefiPngProbeZlibHeaderOkPresent = $serialText.Contains('UEFI_PNG_PROBE_S3_ZLIB_HEADER_OK')
        $uefiPngProbeOutputAllocEnterPresent = $serialText.Contains('UEFI_PNG_PROBE_S3_OUTPUT_ALLOC_ENTER')
        $uefiPngProbeOutputAllocOkPresent = $serialText.Contains('UEFI_PNG_PROBE_S3_OUTPUT_ALLOC_OK')
        $uefiPngProbeOutputAllocSize = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S3_OUTPUT_ALLOC_SIZE'
        $uefiPngProbeDeepestMarker = Get-DeepestSerialMarker -Text $serialText -Markers @(
            'UEFI_PNG_PROBE_S3_ENTER'
            'UEFI_PNG_PROBE_S3_IDAT_AGG_ENTER'
            'UEFI_PNG_PROBE_S3_IDAT_BYTES'
            'UEFI_PNG_PROBE_S3_IDAT_AGG_EXIT'
            'UEFI_PNG_PROBE_S3_ZLIB_HEADER_OK'
            'UEFI_PNG_PROBE_S3_OUTPUT_ALLOC_ENTER'
            'UEFI_PNG_PROBE_S3_OUTPUT_ALLOC_OK'
            'UEFI_PNG_PROBE_S3_OUTPUT_ALLOC_EXIT'
            'UEFI_PNG_PROBE_S3_EXIT'
        )
    } elseif ($CursorPngStandaloneS4Probe) {
        $uefiPngProbeLen = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4_LEN'
        $uefiPngProbeIhdrWidth = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4_IHDR_WIDTH'
        $uefiPngProbeIhdrHeight = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4_IHDR_HEIGHT'
        $uefiPngProbeIhdrBitDepth = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4_IHDR_BIT_DEPTH'
        $uefiPngProbeIhdrColorType = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4_IHDR_COLOR_TYPE'
        $uefiPngProbeIdatChunkCount = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4_IDAT_CHUNK_COUNT'
        $uefiPngProbeIdatCompressedBytes = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4_IDAT_BYTES'
        $uefiPngProbeZlibCmf = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4_ZLIB_CMF'
        $uefiPngProbeZlibFlg = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4_ZLIB_FLG'
        $uefiPngProbeZlibHeaderOkPresent = $serialText.Contains('UEFI_PNG_PROBE_S4_ZLIB_HEADER_OK')
        $uefiPngProbeOutputAllocEnterPresent = $serialText.Contains('UEFI_PNG_PROBE_S4_OUTPUT_ALLOC_ENTER')
        $uefiPngProbeOutputAllocOkPresent = $serialText.Contains('UEFI_PNG_PROBE_S4_OUTPUT_ALLOC_OK')
        $uefiPngProbeOutputAllocSize = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4_OUTPUT_ALLOC_SIZE'
        $uefiPngProbeInflateEnterPresent = $serialText.Contains('UEFI_PNG_PROBE_S4_INFLATE_ENTER')
        $uefiPngProbeInflateProgressPresent = $serialText.Contains('UEFI_PNG_PROBE_S4_INFLATE_PROGRESS')
        $uefiPngProbeInflateBoundsAbortPresent = $serialText.Contains('UEFI_PNG_PROBE_S4_INFLATE_BOUNDS_ABORT')
        $uefiPngProbeInflateFaultPresent = $serialText.Contains('UEFI_PNG_PROBE_S4_INFLATE_FAULT')
        $uefiPngProbeInflateOkPresent = $serialText.Contains('UEFI_PNG_PROBE_S4_INFLATE_OK')
        $uefiPngProbeInflateExitPresent = $serialText.Contains('UEFI_PNG_PROBE_S4_INFLATE_EXIT')
        $uefiPngProbeDeepestMarker = Get-DeepestSerialMarker -Text $serialText -Markers @(
            'UEFI_PNG_PROBE_S4_ENTER'
            'UEFI_PNG_PROBE_S4_IDAT_AGG_ENTER'
            'UEFI_PNG_PROBE_S4_IDAT_BYTES'
            'UEFI_PNG_PROBE_S4_IDAT_AGG_EXIT'
            'UEFI_PNG_PROBE_S4_ZLIB_HEADER_OK'
            'UEFI_PNG_PROBE_S4_OUTPUT_ALLOC_ENTER'
            'UEFI_PNG_PROBE_S4_OUTPUT_ALLOC_OK'
            'UEFI_PNG_PROBE_S4_OUTPUT_ALLOC_EXIT'
            'UEFI_PNG_PROBE_S4_INFLATE_ENTER'
            'UEFI_PNG_PROBE_S4_INFLATE_PROGRESS'
            'UEFI_PNG_PROBE_S4_INFLATE_BOUNDS_ABORT'
            'UEFI_PNG_PROBE_S4_INFLATE_FAULT'
            'UEFI_PNG_PROBE_S4_INFLATE_OK'
            'UEFI_PNG_PROBE_S4_INFLATE_EXIT'
            'UEFI_PNG_PROBE_S4_EXIT'
        )
    } elseif ($CursorPngStandaloneS4AProbe) {
        $uefiPngProbeLen = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4A_LEN'
        $uefiPngProbeIhdrWidth = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4A_IHDR_WIDTH'
        $uefiPngProbeIhdrHeight = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4A_IHDR_HEIGHT'
        $uefiPngProbeIhdrBitDepth = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4A_IHDR_BIT_DEPTH'
        $uefiPngProbeIhdrColorType = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4A_IHDR_COLOR_TYPE'
        $uefiPngProbeIdatChunkCount = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4A_IDAT_CHUNK_COUNT'
        $uefiPngProbeIdatCompressedBytes = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4A_IDAT_BYTES'
        $uefiPngProbeZlibCmf = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4A_ZLIB_CMF'
        $uefiPngProbeZlibFlg = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4A_ZLIB_FLG'
        $uefiPngProbeZlibHeaderOkPresent = $serialText.Contains('UEFI_PNG_PROBE_S4A_ZLIB_HEADER_OK')
        $uefiPngProbeAfterZlibHeaderPresent = $serialText.Contains('UEFI_PNG_PROBE_S4A_AFTER_ZLIB_HEADER')
        $uefiPngProbeDeepestMarker = Get-DeepestSerialMarker -Text $serialText -Markers @(
            'UEFI_PNG_PROBE_S4A_ENTER'
            'UEFI_PNG_PROBE_S4A_LEN'
            'UEFI_PNG_PROBE_S4A_IHDR_WIDTH'
            'UEFI_PNG_PROBE_S4A_IHDR_HEIGHT'
            'UEFI_PNG_PROBE_S4A_IDAT_AGG_ENTER'
            'UEFI_PNG_PROBE_S4A_IDAT_BYTES'
            'UEFI_PNG_PROBE_S4A_IDAT_AGG_EXIT'
            'UEFI_PNG_PROBE_S4A_ZLIB_HEADER_OK'
            'UEFI_PNG_PROBE_S4A_AFTER_ZLIB_HEADER'
            'UEFI_PNG_PROBE_S4A_EXIT'
        )
    } elseif ($CursorPngStandaloneS4B6Probe) {
        $uefiPngProbeLen = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4B6_LEN'
        $uefiPngProbeIhdrWidth = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4B6_IHDR_WIDTH'
        $uefiPngProbeIhdrHeight = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4B6_IHDR_HEIGHT'
        $uefiPngProbeIhdrBitDepth = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4B6_IHDR_BIT_DEPTH'
        $uefiPngProbeIhdrColorType = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4B6_IHDR_COLOR_TYPE'
        $uefiPngProbeIdatChunkCount = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4B6_IDAT_CHUNK_COUNT'
        $uefiPngProbeIdatCompressedBytes = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4B6_IDAT_BYTES'
        $uefiPngProbeZlibCmf = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4B6_ZLIB_CMF'
        $uefiPngProbeZlibFlg = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4B6_ZLIB_FLG'
        $uefiPngProbeZlibHeaderOkPresent = $serialText.Contains('UEFI_PNG_PROBE_S4B6_ZLIB_HEADER_OK')
        $uefiPngProbeAfterZlibHeaderPresent = $serialText.Contains('UEFI_PNG_PROBE_S4B6_AFTER_S4A_SETUP')
        $uefiPngProbeOutputAllocEnterPresent = $false
        $uefiPngProbeOutputAllocOkPresent = $serialText.Contains('UEFI_PNG_PROBE_S4B6_EXPECTED_OUT_OK')
        $uefiPngProbeOutputAllocSize = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4B6_EXPECTED_OUT_LEN'
        $uefiPngProbeDeepestMarker = Get-DeepestSerialMarker -Text $serialText -Markers @(
            'UEFI_PNG_PROBE_S4B6_ENTER'
            'UEFI_PNG_PROBE_S4B6_LEN'
            'UEFI_PNG_PROBE_S4B6_IHDR_WIDTH'
            'UEFI_PNG_PROBE_S4B6_IHDR_HEIGHT'
            'UEFI_PNG_PROBE_S4B6_IDAT_AGG_ENTER'
            'UEFI_PNG_PROBE_S4B6_IDAT_BYTES'
            'UEFI_PNG_PROBE_S4B6_IDAT_AGG_EXIT'
            'UEFI_PNG_PROBE_S4B6_ZLIB_HEADER_OK'
            'UEFI_PNG_PROBE_S4B6_EXPECTED_OUT_LEN'
            'UEFI_PNG_PROBE_S4B6_EXPECTED_OUT_OK'
            'UEFI_PNG_PROBE_S4B6_AFTER_S4A_SETUP'
            'UEFI_PNG_PROBE_S4B6_BEFORE_BLOCK_HEADER'
            'UEFI_PNG_PROBE_S4B6_EXIT'
        )
    } elseif ($CursorPngStandaloneS4B7TProbe) {
        $uefiPngProbeLen = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4B7T_LEN'
        $uefiPngProbeIhdrWidth = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4B7T_IHDR_WIDTH'
        $uefiPngProbeIhdrHeight = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4B7T_IHDR_HEIGHT'
        $uefiPngProbeIhdrBitDepth = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4B7T_IHDR_BIT_DEPTH'
        $uefiPngProbeIhdrColorType = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4B7T_IHDR_COLOR_TYPE'
        $uefiPngProbeIdatChunkCount = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4B7T_IDAT_CHUNK_COUNT'
        $uefiPngProbeIdatCompressedBytes = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4B7T_IDAT_BYTES'
        $uefiPngProbeZlibCmf = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4B7T_ZLIB_CMF'
        $uefiPngProbeZlibFlg = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4B7T_ZLIB_FLG'
        $uefiPngProbeZlibHeaderOkPresent = $serialText.Contains('UEFI_PNG_PROBE_S4B7T_ZLIB_HEADER_OK')
        $uefiPngProbeAfterZlibHeaderPresent = $false
        $uefiPngProbeOutputAllocEnterPresent = $false
        $uefiPngProbeOutputAllocOkPresent = $serialText.Contains('UEFI_PNG_PROBE_S4B7T_EXPECTED_OUT_OK')
        $uefiPngProbeOutputAllocSize = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4B7T_EXPECTED_OUT_LEN'
        $uefiPngProbeRawByte = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4B7T_RAW_BYTE'
        $uefiPngProbeDynamicPresent = $serialText.Contains('UEFI_PNG_PROBE_S4B7T_DYNAMIC')
        $uefiPngProbeDeepestMarker = Get-DeepestSerialMarker -Text $serialText -Markers @(
            'UEFI_PNG_PROBE_S4B7T_ENTER'
            'UEFI_PNG_PROBE_S4B7T_LEN'
            'UEFI_PNG_PROBE_S4B7T_IHDR_WIDTH'
            'UEFI_PNG_PROBE_S4B7T_IHDR_HEIGHT'
            'UEFI_PNG_PROBE_S4B7T_IDAT_AGG_ENTER'
            'UEFI_PNG_PROBE_S4B7T_IDAT_BYTES'
            'UEFI_PNG_PROBE_S4B7T_IDAT_AGG_EXIT'
            'UEFI_PNG_PROBE_S4B7T_ZLIB_HEADER_OK'
            'UEFI_PNG_PROBE_S4B7T_EXPECTED_OUT_LEN'
            'UEFI_PNG_PROBE_S4B7T_EXPECTED_OUT_OK'
            'UEFI_PNG_PROBE_S4B7T_RAW_BYTE'
            'UEFI_PNG_PROBE_S4B7T_EXIT'
        )
    } elseif ($CursorPngStandaloneS4B7T2Probe) {
        $uefiPngProbeLen = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4B7T2_LEN'
        $uefiPngProbeIhdrWidth = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4B7T2_IHDR_WIDTH'
        $uefiPngProbeIhdrHeight = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4B7T2_IHDR_HEIGHT'
        $uefiPngProbeIhdrBitDepth = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4B7T2_IHDR_BIT_DEPTH'
        $uefiPngProbeIhdrColorType = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4B7T2_IHDR_COLOR_TYPE'
        $uefiPngProbeIdatChunkCount = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4B7T2_IDAT_CHUNK_COUNT'
        $uefiPngProbeIdatCompressedBytes = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4B7T2_IDAT_BYTES'
        $uefiPngProbeZlibCmf = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4B7T2_ZLIB_CMF'
        $uefiPngProbeZlibFlg = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4B7T2_ZLIB_FLG'
        $uefiPngProbeZlibHeaderOkPresent = $serialText.Contains('UEFI_PNG_PROBE_S4B7T2_ZLIB_HEADER_OK')
        $uefiPngProbeOutputAllocEnterPresent = $false
        $uefiPngProbeOutputAllocOkPresent = $serialText.Contains('UEFI_PNG_PROBE_S4B7T2_EXPECTED_OUT_OK')
        $uefiPngProbeOutputAllocSize = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4B7T2_EXPECTED_OUT_LEN'
        $uefiPngProbeRawByte = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4B7T2_RAW_BYTE'
        $uefiPngProbeBfinal = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4B7T2_BFINAL'
        $uefiPngProbeBtype = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4B7T2_BTYPE'
        $uefiPngProbeDynamicPresent = $serialText.Contains('UEFI_PNG_PROBE_S4B7T2_DYNAMIC')
        $uefiPngProbeDeepestMarker = Get-DeepestSerialMarker -Text $serialText -Markers @(
            'UEFI_PNG_PROBE_S4B7T2_ENTER'
            'UEFI_PNG_PROBE_S4B7T2_LEN'
            'UEFI_PNG_PROBE_S4B7T2_IHDR_WIDTH'
            'UEFI_PNG_PROBE_S4B7T2_IHDR_HEIGHT'
            'UEFI_PNG_PROBE_S4B7T2_IDAT_AGG_ENTER'
            'UEFI_PNG_PROBE_S4B7T2_IDAT_BYTES'
            'UEFI_PNG_PROBE_S4B7T2_IDAT_AGG_EXIT'
            'UEFI_PNG_PROBE_S4B7T2_ZLIB_HEADER_OK'
            'UEFI_PNG_PROBE_S4B7T2_EXPECTED_OUT_LEN'
            'UEFI_PNG_PROBE_S4B7T2_EXPECTED_OUT_OK'
            'UEFI_PNG_PROBE_S4B7T2_RAW_BYTE'
            'UEFI_PNG_PROBE_S4B7T2_BFINAL'
            'UEFI_PNG_PROBE_S4B7T2_BTYPE'
            'UEFI_PNG_PROBE_S4B7T2_DYNAMIC'
            'UEFI_PNG_PROBE_S4B7T2_EXIT'
        )
    } elseif ($CursorPngStandaloneS4C0Probe) {
        $uefiPngProbeLen = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4C0_LEN'
        $uefiPngProbeIhdrWidth = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4C0_IHDR_WIDTH'
        $uefiPngProbeIhdrHeight = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4C0_IHDR_HEIGHT'
        $uefiPngProbeIhdrBitDepth = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4C0_IHDR_BIT_DEPTH'
        $uefiPngProbeIhdrColorType = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4C0_IHDR_COLOR_TYPE'
        $uefiPngProbeIdatChunkCount = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4C0_IDAT_CHUNK_COUNT'
        $uefiPngProbeIdatCompressedBytes = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4C0_IDAT_BYTES'
        $uefiPngProbeZlibCmf = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4C0_ZLIB_CMF'
        $uefiPngProbeZlibFlg = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4C0_ZLIB_FLG'
        $uefiPngProbeZlibHeaderOkPresent = $serialText.Contains('UEFI_PNG_PROBE_S4C0_ZLIB_HEADER_OK')
        $uefiPngProbeOutputAllocEnterPresent = $false
        $uefiPngProbeOutputAllocOkPresent = $serialText.Contains('UEFI_PNG_PROBE_S4C0_EXPECTED_OUT_OK')
        $uefiPngProbeOutputAllocSize = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4C0_EXPECTED_OUT_LEN'
        $uefiPngProbeBfinal = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4C0_BFINAL'
        $uefiPngProbeBtype = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4C0_BTYPE'
        $uefiPngProbeDynamicPresent = $false
        $uefiPngProbeDeepestMarker = Get-DeepestSerialMarker -Text $serialText -Markers @(
            'UEFI_PNG_PROBE_S4C0_ENTER'
            'UEFI_PNG_PROBE_S4C0_LEN'
            'UEFI_PNG_PROBE_S4C0_IHDR_WIDTH'
            'UEFI_PNG_PROBE_S4C0_IHDR_HEIGHT'
            'UEFI_PNG_PROBE_S4C0_IDAT_AGG_ENTER'
            'UEFI_PNG_PROBE_S4C0_IDAT_BYTES'
            'UEFI_PNG_PROBE_S4C0_IDAT_AGG_EXIT'
            'UEFI_PNG_PROBE_S4C0_ZLIB_HEADER_OK'
            'UEFI_PNG_PROBE_S4C0_EXPECTED_OUT_LEN'
            'UEFI_PNG_PROBE_S4C0_EXPECTED_OUT_OK'
            'UEFI_PNG_PROBE_S4C0_BFINAL'
            'UEFI_PNG_PROBE_S4C0_BTYPE'
            'UEFI_PNG_PROBE_S4C0_AFTER_BLOCK_HEADER'
            'UEFI_PNG_PROBE_S4C0_EXIT'
        )
    } elseif ($CursorPngStandaloneS4C1Probe) {
        $uefiPngProbeLen = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4C1_LEN'
        $uefiPngProbeIhdrWidth = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4C1_IHDR_WIDTH'
        $uefiPngProbeIhdrHeight = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4C1_IHDR_HEIGHT'
        $uefiPngProbeIhdrBitDepth = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4C1_IHDR_BIT_DEPTH'
        $uefiPngProbeIhdrColorType = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4C1_IHDR_COLOR_TYPE'
        $uefiPngProbeIdatChunkCount = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4C1_IDAT_CHUNK_COUNT'
        $uefiPngProbeIdatCompressedBytes = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4C1_IDAT_BYTES'
        $uefiPngProbeZlibCmf = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4C1_ZLIB_CMF'
        $uefiPngProbeZlibFlg = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4C1_ZLIB_FLG'
        $uefiPngProbeZlibHeaderOkPresent = $serialText.Contains('UEFI_PNG_PROBE_S4C1_ZLIB_HEADER_OK')
        $uefiPngProbeOutputAllocEnterPresent = $false
        $uefiPngProbeOutputAllocOkPresent = $serialText.Contains('UEFI_PNG_PROBE_S4C1_EXPECTED_OUT_OK')
        $uefiPngProbeOutputAllocSize = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4C1_EXPECTED_OUT_LEN'
        $uefiPngProbeRawByte = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4C1_RAW_BYTE'
        $uefiPngProbeBfinal = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4C1_BFINAL'
        $uefiPngProbeBtype = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4C1_BTYPE'
        $uefiPngProbeDynamicPresent = $false
        $uefiPngProbeDeepestMarker = Get-DeepestSerialMarker -Text $serialText -Markers @(
            'UEFI_PNG_PROBE_S4C1_ENTER'
            'UEFI_PNG_PROBE_S4C1_LEN'
            'UEFI_PNG_PROBE_S4C1_IHDR_WIDTH'
            'UEFI_PNG_PROBE_S4C1_IHDR_HEIGHT'
            'UEFI_PNG_PROBE_S4C1_IDAT_AGG_ENTER'
            'UEFI_PNG_PROBE_S4C1_IDAT_BYTES'
            'UEFI_PNG_PROBE_S4C1_IDAT_AGG_EXIT'
            'UEFI_PNG_PROBE_S4C1_ZLIB_HEADER_OK'
            'UEFI_PNG_PROBE_S4C1_EXPECTED_OUT_LEN'
            'UEFI_PNG_PROBE_S4C1_EXPECTED_OUT_OK'
            'UEFI_PNG_PROBE_S4C1_RAW_BYTE'
            'UEFI_PNG_PROBE_S4C1_BFINAL'
            'UEFI_PNG_PROBE_S4C1_BTYPE'
            'UEFI_PNG_PROBE_S4C1_AFTER_BLOCK_HEADER'
            'UEFI_PNG_PROBE_S4C1_BEFORE_COUNTS'
            'UEFI_PNG_PROBE_S4C1_EXIT'
        )
    } elseif ($CursorPngStandaloneS4C2Probe) {
        $uefiPngProbeLen = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4C2_LEN'
        $uefiPngProbeIhdrWidth = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4C2_IHDR_WIDTH'
        $uefiPngProbeIhdrHeight = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4C2_IHDR_HEIGHT'
        $uefiPngProbeIhdrBitDepth = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4C2_IHDR_BIT_DEPTH'
        $uefiPngProbeIhdrColorType = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4C2_IHDR_COLOR_TYPE'
        $uefiPngProbeIdatChunkCount = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4C2_IDAT_CHUNK_COUNT'
        $uefiPngProbeIdatCompressedBytes = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4C2_IDAT_BYTES'
        $uefiPngProbeZlibCmf = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4C2_ZLIB_CMF'
        $uefiPngProbeZlibFlg = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4C2_ZLIB_FLG'
        $uefiPngProbeZlibHeaderOkPresent = $serialText.Contains('UEFI_PNG_PROBE_S4C2_ZLIB_HEADER_OK')
        $uefiPngProbeOutputAllocEnterPresent = $false
        $uefiPngProbeOutputAllocOkPresent = $serialText.Contains('UEFI_PNG_PROBE_S4C2_EXPECTED_OUT_OK')
        $uefiPngProbeOutputAllocSize = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4C2_EXPECTED_OUT_LEN'
        $uefiPngProbeRawByte = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4C2_RAW_BYTE'
        $uefiPngProbeBfinal = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4C2_BFINAL'
        $uefiPngProbeBtype = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4C2_BTYPE'
        $uefiPngProbeHlitBits = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4C2_HLIT_BITS'
        $uefiPngProbeHlit = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4C2_HLIT'
        $uefiPngProbeDynamicPresent = $false
        $uefiPngProbeDeepestMarker = Get-DeepestSerialMarker -Text $serialText -Markers @(
            'UEFI_PNG_PROBE_S4C2_ENTER'
            'UEFI_PNG_PROBE_S4C2_LEN'
            'UEFI_PNG_PROBE_S4C2_IHDR_WIDTH'
            'UEFI_PNG_PROBE_S4C2_IHDR_HEIGHT'
            'UEFI_PNG_PROBE_S4C2_IDAT_AGG_ENTER'
            'UEFI_PNG_PROBE_S4C2_IDAT_BYTES'
            'UEFI_PNG_PROBE_S4C2_IDAT_AGG_EXIT'
            'UEFI_PNG_PROBE_S4C2_ZLIB_HEADER_OK'
            'UEFI_PNG_PROBE_S4C2_EXPECTED_OUT_LEN'
            'UEFI_PNG_PROBE_S4C2_EXPECTED_OUT_OK'
            'UEFI_PNG_PROBE_S4C2_RAW_BYTE'
            'UEFI_PNG_PROBE_S4C2_BFINAL'
            'UEFI_PNG_PROBE_S4C2_BTYPE'
            'UEFI_PNG_PROBE_S4C2_AFTER_BLOCK_HEADER'
            'UEFI_PNG_PROBE_S4C2_BEFORE_COUNTS'
            'UEFI_PNG_PROBE_S4C2_HLIT_BITS'
            'UEFI_PNG_PROBE_S4C2_HLIT'
            'UEFI_PNG_PROBE_S4C2_EXIT'
        )
    } elseif ($CursorPngStandaloneS4C3Probe) {
        $uefiPngProbeLen = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4C3_LEN'
        $uefiPngProbeIhdrWidth = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4C3_IHDR_WIDTH'
        $uefiPngProbeIhdrHeight = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4C3_IHDR_HEIGHT'
        $uefiPngProbeIhdrBitDepth = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4C3_IHDR_BIT_DEPTH'
        $uefiPngProbeIhdrColorType = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4C3_IHDR_COLOR_TYPE'
        $uefiPngProbeIdatChunkCount = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4C3_IDAT_CHUNK_COUNT'
        $uefiPngProbeIdatCompressedBytes = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4C3_IDAT_BYTES'
        $uefiPngProbeZlibCmf = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4C3_ZLIB_CMF'
        $uefiPngProbeZlibFlg = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4C3_ZLIB_FLG'
        $uefiPngProbeZlibHeaderOkPresent = $serialText.Contains('UEFI_PNG_PROBE_S4C3_ZLIB_HEADER_OK')
        $uefiPngProbeOutputAllocEnterPresent = $false
        $uefiPngProbeOutputAllocOkPresent = $serialText.Contains('UEFI_PNG_PROBE_S4C3_EXPECTED_OUT_OK')
        $uefiPngProbeOutputAllocSize = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4C3_EXPECTED_OUT_LEN'
        $uefiPngProbeRawByte = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4C3_RAW_BYTE'
        $uefiPngProbeBfinal = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4C3_BFINAL'
        $uefiPngProbeBtype = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4C3_BTYPE'
        $uefiPngProbeHlitBits = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4C3_HLIT_BITS'
        $uefiPngProbeHlit = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4C3_HLIT'
        $uefiPngProbeHdistBits = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4C3_HDIST_BITS'
        $uefiPngProbeHdist = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4C3_HDIST'
        $uefiPngProbeDynamicPresent = $false
        $uefiPngProbeDeepestMarker = Get-DeepestSerialMarker -Text $serialText -Markers @(
            'UEFI_PNG_PROBE_S4C3_ENTER'
            'UEFI_PNG_PROBE_S4C3_LEN'
            'UEFI_PNG_PROBE_S4C3_IHDR_WIDTH'
            'UEFI_PNG_PROBE_S4C3_IHDR_HEIGHT'
            'UEFI_PNG_PROBE_S4C3_IDAT_AGG_ENTER'
            'UEFI_PNG_PROBE_S4C3_IDAT_BYTES'
            'UEFI_PNG_PROBE_S4C3_IDAT_AGG_EXIT'
            'UEFI_PNG_PROBE_S4C3_ZLIB_HEADER_OK'
            'UEFI_PNG_PROBE_S4C3_EXPECTED_OUT_LEN'
            'UEFI_PNG_PROBE_S4C3_EXPECTED_OUT_OK'
            'UEFI_PNG_PROBE_S4C3_RAW_BYTE'
            'UEFI_PNG_PROBE_S4C3_BFINAL'
            'UEFI_PNG_PROBE_S4C3_BTYPE'
            'UEFI_PNG_PROBE_S4C3_AFTER_BLOCK_HEADER'
            'UEFI_PNG_PROBE_S4C3_BEFORE_COUNTS'
            'UEFI_PNG_PROBE_S4C3_HLIT_BITS'
            'UEFI_PNG_PROBE_S4C3_HLIT'
            'UEFI_PNG_PROBE_S4C3_HDIST_BITS'
            'UEFI_PNG_PROBE_S4C3_HDIST'
            'UEFI_PNG_PROBE_S4C3_EXIT'
        )
    } elseif ($CursorPngStandaloneS4C4Probe) {
        $uefiPngProbeLen = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4C4_LEN'
        $uefiPngProbeIhdrWidth = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4C4_IHDR_WIDTH'
        $uefiPngProbeIhdrHeight = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4C4_IHDR_HEIGHT'
        $uefiPngProbeIhdrBitDepth = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4C4_IHDR_BIT_DEPTH'
        $uefiPngProbeIhdrColorType = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4C4_IHDR_COLOR_TYPE'
        $uefiPngProbeIdatChunkCount = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4C4_IDAT_CHUNK_COUNT'
        $uefiPngProbeIdatCompressedBytes = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4C4_IDAT_BYTES'
        $uefiPngProbeZlibCmf = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4C4_ZLIB_CMF'
        $uefiPngProbeZlibFlg = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4C4_ZLIB_FLG'
        $uefiPngProbeZlibHeaderOkPresent = $serialText.Contains('UEFI_PNG_PROBE_S4C4_ZLIB_HEADER_OK')
        $uefiPngProbeOutputAllocEnterPresent = $false
        $uefiPngProbeOutputAllocOkPresent = $serialText.Contains('UEFI_PNG_PROBE_S4C4_EXPECTED_OUT_OK')
        $uefiPngProbeOutputAllocSize = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4C4_EXPECTED_OUT_LEN'
        $uefiPngProbeRawByte = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4C4_RAW_BYTE'
        $uefiPngProbeBfinal = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4C4_BFINAL'
        $uefiPngProbeBtype = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4C4_BTYPE'
        $uefiPngProbeHlitBits = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4C4_HLIT_BITS'
        $uefiPngProbeHlit = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4C4_HLIT'
        $uefiPngProbeHdistBits = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4C4_HDIST_BITS'
        $uefiPngProbeHdist = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4C4_HDIST'
        $uefiPngProbeHclenBits = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4C4_HCLEN_BITS'
        $uefiPngProbeHclen = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4C4_HCLEN'
        $uefiPngProbeCountsOkPresent = $serialText.Contains('UEFI_PNG_PROBE_S4C4_COUNTS_OK')
        $uefiPngProbeCountsBadPresent = $serialText.Contains('UEFI_PNG_PROBE_S4C4_COUNTS_BAD')
        $uefiPngProbeDynamicPresent = $false
        $uefiPngProbeDeepestMarker = Get-DeepestSerialMarker -Text $serialText -Markers @(
            'UEFI_PNG_PROBE_S4C4_ENTER'
            'UEFI_PNG_PROBE_S4C4_LEN'
            'UEFI_PNG_PROBE_S4C4_IHDR_WIDTH'
            'UEFI_PNG_PROBE_S4C4_IHDR_HEIGHT'
            'UEFI_PNG_PROBE_S4C4_IDAT_AGG_ENTER'
            'UEFI_PNG_PROBE_S4C4_IDAT_BYTES'
            'UEFI_PNG_PROBE_S4C4_IDAT_AGG_EXIT'
            'UEFI_PNG_PROBE_S4C4_ZLIB_HEADER_OK'
            'UEFI_PNG_PROBE_S4C4_EXPECTED_OUT_LEN'
            'UEFI_PNG_PROBE_S4C4_EXPECTED_OUT_OK'
            'UEFI_PNG_PROBE_S4C4_RAW_BYTE'
            'UEFI_PNG_PROBE_S4C4_BFINAL'
            'UEFI_PNG_PROBE_S4C4_BTYPE'
            'UEFI_PNG_PROBE_S4C4_AFTER_BLOCK_HEADER'
            'UEFI_PNG_PROBE_S4C4_BEFORE_COUNTS'
            'UEFI_PNG_PROBE_S4C4_HLIT_BITS'
            'UEFI_PNG_PROBE_S4C4_HLIT'
            'UEFI_PNG_PROBE_S4C4_HDIST_BITS'
            'UEFI_PNG_PROBE_S4C4_HDIST'
            'UEFI_PNG_PROBE_S4C4_HCLEN_BITS'
            'UEFI_PNG_PROBE_S4C4_HCLEN'
            'UEFI_PNG_PROBE_S4C4_COUNTS_OK'
            'UEFI_PNG_PROBE_S4C4_COUNTS_BAD'
            'UEFI_PNG_PROBE_S4C4_EXIT'
        )
    } elseif ($CursorPngStandaloneS4D0Probe -or $CursorPngStandaloneS4D1Probe -or $CursorPngStandaloneS4D2Probe -or $CursorPngStandaloneS4D3Probe) {
        $uefiPngProbeS4Variant = if ($CursorPngStandaloneS4D0Probe) {
            'S4D0'
        } elseif ($CursorPngStandaloneS4D1Probe) {
            'S4D1'
        } elseif ($CursorPngStandaloneS4D2Probe) {
            'S4D2'
        } else {
            'S4D3'
        }
        $uefiPngProbePrefix = 'UEFI_PNG_PROBE_' + $uefiPngProbeS4Variant
        $uefiPngProbeLen = Get-SerialMarkerValue -Text $serialText -MarkerPrefix ($uefiPngProbePrefix + '_LEN')
        $uefiPngProbeIhdrWidth = Get-SerialMarkerValue -Text $serialText -MarkerPrefix ($uefiPngProbePrefix + '_IHDR_WIDTH')
        $uefiPngProbeIhdrHeight = Get-SerialMarkerValue -Text $serialText -MarkerPrefix ($uefiPngProbePrefix + '_IHDR_HEIGHT')
        $uefiPngProbeIhdrBitDepth = Get-SerialMarkerValue -Text $serialText -MarkerPrefix ($uefiPngProbePrefix + '_IHDR_BIT_DEPTH')
        $uefiPngProbeIhdrColorType = Get-SerialMarkerValue -Text $serialText -MarkerPrefix ($uefiPngProbePrefix + '_IHDR_COLOR_TYPE')
        $uefiPngProbeIdatChunkCount = Get-SerialMarkerValue -Text $serialText -MarkerPrefix ($uefiPngProbePrefix + '_IDAT_CHUNK_COUNT')
        $uefiPngProbeIdatCompressedBytes = Get-SerialMarkerValue -Text $serialText -MarkerPrefix ($uefiPngProbePrefix + '_IDAT_BYTES')
        $uefiPngProbeZlibCmf = Get-SerialMarkerValue -Text $serialText -MarkerPrefix ($uefiPngProbePrefix + '_ZLIB_CMF')
        $uefiPngProbeZlibFlg = Get-SerialMarkerValue -Text $serialText -MarkerPrefix ($uefiPngProbePrefix + '_ZLIB_FLG')
        $uefiPngProbeZlibHeaderOkPresent = $serialText.Contains($uefiPngProbePrefix + '_ZLIB_HEADER_OK')
        $uefiPngProbeBfinal = Get-SerialMarkerValue -Text $serialText -MarkerPrefix ($uefiPngProbePrefix + '_BFINAL')
        $uefiPngProbeBtype = Get-SerialMarkerValue -Text $serialText -MarkerPrefix ($uefiPngProbePrefix + '_BTYPE')
        $uefiPngProbeHlitBits = Get-SerialMarkerValue -Text $serialText -MarkerPrefix ($uefiPngProbePrefix + '_HLIT_BITS')
        $uefiPngProbeHlit = Get-SerialMarkerValue -Text $serialText -MarkerPrefix ($uefiPngProbePrefix + '_HLIT')
        $uefiPngProbeHdistBits = Get-SerialMarkerValue -Text $serialText -MarkerPrefix ($uefiPngProbePrefix + '_HDIST_BITS')
        $uefiPngProbeHdist = Get-SerialMarkerValue -Text $serialText -MarkerPrefix ($uefiPngProbePrefix + '_HDIST')
        $uefiPngProbeHclenBits = Get-SerialMarkerValue -Text $serialText -MarkerPrefix ($uefiPngProbePrefix + '_HCLEN_BITS')
        $uefiPngProbeHclen = Get-SerialMarkerValue -Text $serialText -MarkerPrefix ($uefiPngProbePrefix + '_HCLEN')
        $uefiPngProbeCodeLenCountRead = Get-SerialMarkerValue -Text $serialText -MarkerPrefix ($uefiPngProbePrefix + '_COUNT_READ')
        $uefiPngProbeCodeLenNonzeroCount = Get-SerialMarkerValue -Text $serialText -MarkerPrefix ($uefiPngProbePrefix + '_NONZERO_COUNT')
        $uefiPngProbeCodeLenMaxValue = Get-SerialMarkerValue -Text $serialText -MarkerPrefix ($uefiPngProbePrefix + '_MAX_VALUE')
        $uefiPngProbeCodeLenValuesOkPresent = $serialText.Contains($uefiPngProbePrefix + '_VALUES_OK')
        $uefiPngProbeCodeLenValuesBadPresent = $serialText.Contains($uefiPngProbePrefix + '_VALUES_BAD')
        $uefiPngProbeCodeLenAlphabetOkPresent = $uefiPngProbeCodeLenValuesOkPresent
        $uefiPngProbeDynamicPresent = $uefiPngProbeBtype -eq 2
        $uefiPngProbeDeepestMarker = Get-DeepestSerialMarker -Text $serialText -Markers @(
            $uefiPngProbePrefix + '_ENTER'
            $uefiPngProbePrefix + '_LEN'
            $uefiPngProbePrefix + '_IHDR_WIDTH'
            $uefiPngProbePrefix + '_IHDR_HEIGHT'
            $uefiPngProbePrefix + '_IDAT_AGG_ENTER'
            $uefiPngProbePrefix + '_IDAT_BYTES'
            $uefiPngProbePrefix + '_IDAT_AGG_EXIT'
            $uefiPngProbePrefix + '_ZLIB_HEADER_OK'
            $uefiPngProbePrefix + '_BFINAL'
            $uefiPngProbePrefix + '_BTYPE'
            $uefiPngProbePrefix + '_HLIT_BITS'
            $uefiPngProbePrefix + '_HLIT'
            $uefiPngProbePrefix + '_HDIST_BITS'
            $uefiPngProbePrefix + '_HDIST'
            $uefiPngProbePrefix + '_HCLEN_BITS'
            $uefiPngProbePrefix + '_HCLEN'
            $uefiPngProbePrefix + '_COUNT_READ'
            $uefiPngProbePrefix + '_NONZERO_COUNT'
            $uefiPngProbePrefix + '_MAX_VALUE'
            $uefiPngProbePrefix + '_VALUES_OK'
            $uefiPngProbePrefix + '_VALUES_BAD'
            $uefiPngProbePrefix + '_AFTER_COUNTS'
            $uefiPngProbePrefix + '_BEFORE_CODELEN_ALPHABET'
            $uefiPngProbePrefix + '_EXIT'
        )
    } elseif ($CursorPngStandaloneS4E0Probe -or $CursorPngStandaloneS4E1Probe -or $CursorPngStandaloneS4E2Probe -or $CursorPngStandaloneS4E3Probe -or $CursorPngStandaloneS4E4Probe) {
        $uefiPngProbeS4Variant = if ($CursorPngStandaloneS4E0Probe) {
            'S4E0'
        } elseif ($CursorPngStandaloneS4E1Probe) {
            'S4E1'
        } elseif ($CursorPngStandaloneS4E2Probe) {
            'S4E2'
        } elseif ($CursorPngStandaloneS4E3Probe) {
            'S4E3'
        } else {
            'S4E4'
        }
        $uefiPngProbePrefix = 'UEFI_PNG_PROBE_' + $uefiPngProbeS4Variant
        $uefiPngProbeLen = Get-SerialMarkerValue -Text $serialText -MarkerPrefix ($uefiPngProbePrefix + '_LEN')
        $uefiPngProbeIhdrWidth = Get-SerialMarkerValue -Text $serialText -MarkerPrefix ($uefiPngProbePrefix + '_IHDR_WIDTH')
        $uefiPngProbeIhdrHeight = Get-SerialMarkerValue -Text $serialText -MarkerPrefix ($uefiPngProbePrefix + '_IHDR_HEIGHT')
        $uefiPngProbeIhdrBitDepth = Get-SerialMarkerValue -Text $serialText -MarkerPrefix ($uefiPngProbePrefix + '_IHDR_BIT_DEPTH')
        $uefiPngProbeIhdrColorType = Get-SerialMarkerValue -Text $serialText -MarkerPrefix ($uefiPngProbePrefix + '_IHDR_COLOR_TYPE')
        $uefiPngProbeIdatChunkCount = Get-SerialMarkerValue -Text $serialText -MarkerPrefix ($uefiPngProbePrefix + '_IDAT_CHUNK_COUNT')
        $uefiPngProbeIdatCompressedBytes = Get-SerialMarkerValue -Text $serialText -MarkerPrefix ($uefiPngProbePrefix + '_IDAT_BYTES')
        $uefiPngProbeZlibCmf = Get-SerialMarkerValue -Text $serialText -MarkerPrefix ($uefiPngProbePrefix + '_ZLIB_CMF')
        $uefiPngProbeZlibFlg = Get-SerialMarkerValue -Text $serialText -MarkerPrefix ($uefiPngProbePrefix + '_ZLIB_FLG')
        $uefiPngProbeZlibHeaderOkPresent = $serialText.Contains($uefiPngProbePrefix + '_ZLIB_HEADER_OK')
        $uefiPngProbeBfinal = Get-SerialMarkerValue -Text $serialText -MarkerPrefix ($uefiPngProbePrefix + '_BFINAL')
        $uefiPngProbeBtype = Get-SerialMarkerValue -Text $serialText -MarkerPrefix ($uefiPngProbePrefix + '_BTYPE')
        $uefiPngProbeHlitBits = Get-SerialMarkerValue -Text $serialText -MarkerPrefix ($uefiPngProbePrefix + '_HLIT_BITS')
        $uefiPngProbeHlit = Get-SerialMarkerValue -Text $serialText -MarkerPrefix ($uefiPngProbePrefix + '_HLIT')
        $uefiPngProbeHdistBits = Get-SerialMarkerValue -Text $serialText -MarkerPrefix ($uefiPngProbePrefix + '_HDIST_BITS')
        $uefiPngProbeHdist = Get-SerialMarkerValue -Text $serialText -MarkerPrefix ($uefiPngProbePrefix + '_HDIST')
        $uefiPngProbeHclenBits = Get-SerialMarkerValue -Text $serialText -MarkerPrefix ($uefiPngProbePrefix + '_HCLEN_BITS')
        $uefiPngProbeHclen = Get-SerialMarkerValue -Text $serialText -MarkerPrefix ($uefiPngProbePrefix + '_HCLEN')
        $uefiPngProbeCodeLenCountRead = Get-SerialMarkerValue -Text $serialText -MarkerPrefix ($uefiPngProbePrefix + '_COUNT_READ')
        $uefiPngProbeCodeLenNonzeroCount = Get-SerialMarkerValue -Text $serialText -MarkerPrefix ($uefiPngProbePrefix + '_NONZERO_COUNT')
        $uefiPngProbeCodeLenZeroCount = Get-SerialMarkerValue -Text $serialText -MarkerPrefix ($uefiPngProbePrefix + '_ZERO_COUNT')
        $uefiPngProbeCodeLenMaxValue = Get-SerialMarkerValue -Text $serialText -MarkerPrefix ($uefiPngProbePrefix + '_MAX_VALUE')
        $uefiPngProbeCodeLenMaxBits = Get-SerialMarkerValue -Text $serialText -MarkerPrefix ($uefiPngProbePrefix + '_MAX_BITS')
        $uefiPngProbeCodeLenLen1Count = Get-SerialMarkerValue -Text $serialText -MarkerPrefix ($uefiPngProbePrefix + '_LEN_1_COUNT')
        $uefiPngProbeCodeLenLen2Count = Get-SerialMarkerValue -Text $serialText -MarkerPrefix ($uefiPngProbePrefix + '_LEN_2_COUNT')
        $uefiPngProbeCodeLenLen3Count = Get-SerialMarkerValue -Text $serialText -MarkerPrefix ($uefiPngProbePrefix + '_LEN_3_COUNT')
        $uefiPngProbeCodeLenLen4Count = Get-SerialMarkerValue -Text $serialText -MarkerPrefix ($uefiPngProbePrefix + '_LEN_4_COUNT')
        $uefiPngProbeCodeLenLen5Count = Get-SerialMarkerValue -Text $serialText -MarkerPrefix ($uefiPngProbePrefix + '_LEN_5_COUNT')
        $uefiPngProbeCodeLenLen6Count = Get-SerialMarkerValue -Text $serialText -MarkerPrefix ($uefiPngProbePrefix + '_LEN_6_COUNT')
        $uefiPngProbeCodeLenLen7Count = Get-SerialMarkerValue -Text $serialText -MarkerPrefix ($uefiPngProbePrefix + '_LEN_7_COUNT')
        $uefiPngProbeCodeLenValuesOkPresent = $serialText.Contains($uefiPngProbePrefix + '_VALUES_OK')
        $uefiPngProbeCodeLenValuesBadPresent = $serialText.Contains($uefiPngProbePrefix + '_VALUES_BAD')
        $uefiPngProbeCodeLenHistogramOkPresent = $serialText.Contains($uefiPngProbePrefix + '_HISTOGRAM_OK')
        $uefiPngProbeCodeLenHistogramBadPresent = $serialText.Contains($uefiPngProbePrefix + '_HISTOGRAM_BAD')
        $uefiPngProbeCodeLenNextCodeOkPresent = $serialText.Contains($uefiPngProbePrefix + '_NEXT_CODE_OK')
        $uefiPngProbeCodeLenNextCodeBadPresent = $serialText.Contains($uefiPngProbePrefix + '_NEXT_CODE_BAD')
        $uefiPngProbeCodeLenCodeSpaceEnd = Get-SerialMarkerValue -Text $serialText -MarkerPrefix ($uefiPngProbePrefix + '_CODE_SPACE_END')
        $uefiPngProbeCodeLenCodeSpaceRemaining = Get-SerialMarkerValue -Text $serialText -MarkerPrefix ($uefiPngProbePrefix + '_CODE_SPACE_REMAINING')
        $uefiPngProbeCodeLenTableEntryCount = Get-SerialMarkerValue -Text $serialText -MarkerPrefix ($uefiPngProbePrefix + '_TABLE_ENTRY_COUNT')
        $uefiPngProbeCodeLenTableBuildOkPresent = $serialText.Contains($uefiPngProbePrefix + '_TABLE_BUILD_OK')
        $uefiPngProbeCodeLenTableBuildBadPresent = $serialText.Contains($uefiPngProbePrefix + '_TABLE_BUILD_BAD')
        $uefiPngProbeCodeLenLookupSmokeSymbol = Get-SerialMarkerValue -Text $serialText -MarkerPrefix ($uefiPngProbePrefix + '_LOOKUP_SMOKE_SYMBOL')
        $uefiPngProbeCodeLenLookupSmokeBits = Get-SerialMarkerValue -Text $serialText -MarkerPrefix ($uefiPngProbePrefix + '_LOOKUP_SMOKE_BITS')
        $uefiPngProbeCodeLenLookupSmokeCode = Get-SerialMarkerValue -Text $serialText -MarkerPrefix ($uefiPngProbePrefix + '_LOOKUP_SMOKE_CODE')
        $uefiPngProbeCodeLenLookupSmokeOkPresent = $serialText.Contains($uefiPngProbePrefix + '_LOOKUP_SMOKE_OK')
        $uefiPngProbeCodeLenLookupSmokeBadPresent = $serialText.Contains($uefiPngProbePrefix + '_LOOKUP_SMOKE_BAD')
        $uefiPngProbeCodeLenAlphabetOkPresent = $uefiPngProbeCodeLenValuesOkPresent
        $uefiPngProbeTablesOkPresent = $uefiPngProbeCodeLenTableBuildOkPresent
        $uefiPngProbeTablesBadPresent = $uefiPngProbeCodeLenTableBuildBadPresent
        $uefiPngProbeDynamicPresent = $uefiPngProbeBtype -eq 2
        $uefiPngProbeDeepestMarker = Get-DeepestSerialMarker -Text $serialText -Markers @(
            $uefiPngProbePrefix + '_ENTER'
            $uefiPngProbePrefix + '_LEN'
            $uefiPngProbePrefix + '_IHDR_WIDTH'
            $uefiPngProbePrefix + '_IHDR_HEIGHT'
            $uefiPngProbePrefix + '_IDAT_AGG_ENTER'
            $uefiPngProbePrefix + '_IDAT_BYTES'
            $uefiPngProbePrefix + '_IDAT_AGG_EXIT'
            $uefiPngProbePrefix + '_ZLIB_HEADER_OK'
            $uefiPngProbePrefix + '_BFINAL'
            $uefiPngProbePrefix + '_BTYPE'
            $uefiPngProbePrefix + '_HLIT_BITS'
            $uefiPngProbePrefix + '_HLIT'
            $uefiPngProbePrefix + '_HDIST_BITS'
            $uefiPngProbePrefix + '_HDIST'
            $uefiPngProbePrefix + '_HCLEN_BITS'
            $uefiPngProbePrefix + '_HCLEN'
            $uefiPngProbePrefix + '_AFTER_BLOCK_HEADER'
            $uefiPngProbePrefix + '_BEFORE_COUNTS'
            $uefiPngProbePrefix + '_AFTER_COUNTS'
            $uefiPngProbePrefix + '_BEFORE_CODELEN_ALPHABET'
            $uefiPngProbePrefix + '_COUNT_READ'
            $uefiPngProbePrefix + '_NONZERO_COUNT'
            $uefiPngProbePrefix + '_ZERO_COUNT'
            $uefiPngProbePrefix + '_MAX_VALUE'
            $uefiPngProbePrefix + '_MAX_BITS'
            $uefiPngProbePrefix + '_LEN_1_COUNT'
            $uefiPngProbePrefix + '_LEN_2_COUNT'
            $uefiPngProbePrefix + '_LEN_3_COUNT'
            $uefiPngProbePrefix + '_LEN_4_COUNT'
            $uefiPngProbePrefix + '_LEN_5_COUNT'
            $uefiPngProbePrefix + '_LEN_6_COUNT'
            $uefiPngProbePrefix + '_LEN_7_COUNT'
            $uefiPngProbePrefix + '_VALUES_OK'
            $uefiPngProbePrefix + '_VALUES_BAD'
            $uefiPngProbePrefix + '_HISTOGRAM_OK'
            $uefiPngProbePrefix + '_HISTOGRAM_BAD'
            $uefiPngProbePrefix + '_NEXT_CODE_OK'
            $uefiPngProbePrefix + '_NEXT_CODE_BAD'
            $uefiPngProbePrefix + '_TABLE_BUILD_ENTER'
            $uefiPngProbePrefix + '_TABLE_ENTRY_COUNT'
            $uefiPngProbePrefix + '_TABLE_BUILD_OK'
            $uefiPngProbePrefix + '_TABLE_BUILD_BAD'
            $uefiPngProbePrefix + '_LOOKUP_SMOKE_OK'
            $uefiPngProbePrefix + '_LOOKUP_SMOKE_BAD'
            $uefiPngProbePrefix + '_AFTER_ALPHABET'
            $uefiPngProbePrefix + '_BEFORE_TABLE_BUILD'
            $uefiPngProbePrefix + '_EXIT'
        )
    } elseif ($CursorPngStandaloneS4B7Probe) {
        $uefiPngProbeLen = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4B7_LEN'
        $uefiPngProbeIhdrWidth = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4B7_IHDR_WIDTH'
        $uefiPngProbeIhdrHeight = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4B7_IHDR_HEIGHT'
        $uefiPngProbeIhdrBitDepth = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4B7_IHDR_BIT_DEPTH'
        $uefiPngProbeIhdrColorType = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4B7_IHDR_COLOR_TYPE'
        $uefiPngProbeIdatChunkCount = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4B7_IDAT_CHUNK_COUNT'
        $uefiPngProbeIdatCompressedBytes = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4B7_IDAT_BYTES'
        $uefiPngProbeZlibCmf = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4B7_ZLIB_CMF'
        $uefiPngProbeZlibFlg = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4B7_ZLIB_FLG'
        $uefiPngProbeZlibHeaderOkPresent = $serialText.Contains('UEFI_PNG_PROBE_S4B7_ZLIB_HEADER_OK')
        $uefiPngProbeAfterZlibHeaderPresent = $serialText.Contains('UEFI_PNG_PROBE_S4B7_AFTER_S4A_SETUP')
        $uefiPngProbeOutputAllocEnterPresent = $false
        $uefiPngProbeOutputAllocOkPresent = $serialText.Contains('UEFI_PNG_PROBE_S4B7_EXPECTED_OUT_OK')
        $uefiPngProbeOutputAllocSize = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4B7_EXPECTED_OUT_LEN'
        $uefiPngProbeBfinal = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4B7_BFINAL'
        $uefiPngProbeBtype = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4B7_BTYPE'
        $uefiPngProbeDynamicPresent = $serialText.Contains('UEFI_PNG_PROBE_S4B7_DYNAMIC')
        $uefiPngProbeDeepestMarker = Get-DeepestSerialMarker -Text $serialText -Markers @(
            'UEFI_PNG_PROBE_S4B7_ENTER'
            'UEFI_PNG_PROBE_S4B7_LEN'
            'UEFI_PNG_PROBE_S4B7_IHDR_WIDTH'
            'UEFI_PNG_PROBE_S4B7_IHDR_HEIGHT'
            'UEFI_PNG_PROBE_S4B7_IDAT_AGG_ENTER'
            'UEFI_PNG_PROBE_S4B7_IDAT_BYTES'
            'UEFI_PNG_PROBE_S4B7_IDAT_AGG_EXIT'
            'UEFI_PNG_PROBE_S4B7_ZLIB_HEADER_OK'
            'UEFI_PNG_PROBE_S4B7_EXPECTED_OUT_LEN'
            'UEFI_PNG_PROBE_S4B7_EXPECTED_OUT_OK'
            'UEFI_PNG_PROBE_S4B7_AFTER_S4A_SETUP'
            'UEFI_PNG_PROBE_S4B7_BEFORE_BLOCK_HEADER'
            'UEFI_PNG_PROBE_S4B7_BFINAL'
            'UEFI_PNG_PROBE_S4B7_BTYPE'
            'UEFI_PNG_PROBE_S4B7_DYNAMIC'
            'UEFI_PNG_PROBE_S4B7_EXIT'
        )
    } elseif ($CursorPngStandaloneS4B8Probe) {
        $uefiPngProbeLen = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4B8_LEN'
        $uefiPngProbeIhdrWidth = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4B8_IHDR_WIDTH'
        $uefiPngProbeIhdrHeight = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4B8_IHDR_HEIGHT'
        $uefiPngProbeIhdrBitDepth = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4B8_IHDR_BIT_DEPTH'
        $uefiPngProbeIhdrColorType = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4B8_IHDR_COLOR_TYPE'
        $uefiPngProbeIdatChunkCount = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4B8_IDAT_CHUNK_COUNT'
        $uefiPngProbeIdatCompressedBytes = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4B8_IDAT_BYTES'
        $uefiPngProbeZlibCmf = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4B8_ZLIB_CMF'
        $uefiPngProbeZlibFlg = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4B8_ZLIB_FLG'
        $uefiPngProbeZlibHeaderOkPresent = $serialText.Contains('UEFI_PNG_PROBE_S4B8_ZLIB_HEADER_OK')
        $uefiPngProbeAfterZlibHeaderPresent = $serialText.Contains('UEFI_PNG_PROBE_S4B8_AFTER_S4A_SETUP')
        $uefiPngProbeOutputAllocEnterPresent = $false
        $uefiPngProbeOutputAllocOkPresent = $serialText.Contains('UEFI_PNG_PROBE_S4B8_EXPECTED_OUT_OK')
        $uefiPngProbeOutputAllocSize = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4B8_EXPECTED_OUT_LEN'
        $uefiPngProbeBfinal = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4B8_BFINAL'
        $uefiPngProbeBtype = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4B8_BTYPE'
        $uefiPngProbeDeepestMarker = Get-DeepestSerialMarker -Text $serialText -Markers @(
            'UEFI_PNG_PROBE_S4B8_ENTER'
            'UEFI_PNG_PROBE_S4B8_LEN'
            'UEFI_PNG_PROBE_S4B8_IHDR_WIDTH'
            'UEFI_PNG_PROBE_S4B8_IHDR_HEIGHT'
            'UEFI_PNG_PROBE_S4B8_IDAT_AGG_ENTER'
            'UEFI_PNG_PROBE_S4B8_IDAT_BYTES'
            'UEFI_PNG_PROBE_S4B8_IDAT_AGG_EXIT'
            'UEFI_PNG_PROBE_S4B8_ZLIB_HEADER_OK'
            'UEFI_PNG_PROBE_S4B8_EXPECTED_OUT_LEN'
            'UEFI_PNG_PROBE_S4B8_EXPECTED_OUT_OK'
            'UEFI_PNG_PROBE_S4B8_AFTER_S4A_SETUP'
            'UEFI_PNG_PROBE_S4B8_BFINAL'
            'UEFI_PNG_PROBE_S4B8_BTYPE'
            'UEFI_PNG_PROBE_S4B8_EXIT'
        )
    } elseif ($CursorPngStandaloneS4BProbe) {
        $uefiPngProbeLen = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4B_LEN'
        $uefiPngProbeIhdrWidth = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4B_IHDR_WIDTH'
        $uefiPngProbeIhdrHeight = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4B_IHDR_HEIGHT'
        $uefiPngProbeIhdrBitDepth = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4B_IHDR_BIT_DEPTH'
        $uefiPngProbeIhdrColorType = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4B_IHDR_COLOR_TYPE'
        $uefiPngProbeIdatChunkCount = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4B_IDAT_CHUNK_COUNT'
        $uefiPngProbeIdatCompressedBytes = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4B_IDAT_BYTES'
        $uefiPngProbeZlibCmf = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4B_ZLIB_CMF'
        $uefiPngProbeZlibFlg = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4B_ZLIB_FLG'
        $uefiPngProbeZlibHeaderOkPresent = $serialText.Contains('UEFI_PNG_PROBE_S4B_ZLIB_HEADER_OK')
        $uefiPngProbeBfinal = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4B_BFINAL'
        $uefiPngProbeBtype = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4B_BTYPE'
        $uefiPngProbeDeepestMarker = Get-DeepestSerialMarker -Text $serialText -Markers @(
            'UEFI_PNG_PROBE_S4B_ENTER'
            'UEFI_PNG_PROBE_S4B_LEN'
            'UEFI_PNG_PROBE_S4B_IHDR_WIDTH'
            'UEFI_PNG_PROBE_S4B_IHDR_HEIGHT'
            'UEFI_PNG_PROBE_S4B_IDAT_AGG_ENTER'
            'UEFI_PNG_PROBE_S4B_IDAT_BYTES'
            'UEFI_PNG_PROBE_S4B_IDAT_AGG_EXIT'
            'UEFI_PNG_PROBE_S4B_ZLIB_HEADER_OK'
            'UEFI_PNG_PROBE_S4B_BFINAL'
            'UEFI_PNG_PROBE_S4B_BTYPE'
            'UEFI_PNG_PROBE_S4B_EXIT'
        )
    } elseif ($CursorPngStandaloneS4CProbe) {
        $uefiPngProbeLen = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4C_LEN'
        $uefiPngProbeIhdrWidth = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4C_IHDR_WIDTH'
        $uefiPngProbeIhdrHeight = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4C_IHDR_HEIGHT'
        $uefiPngProbeIhdrBitDepth = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4C_IHDR_BIT_DEPTH'
        $uefiPngProbeIhdrColorType = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4C_IHDR_COLOR_TYPE'
        $uefiPngProbeIdatChunkCount = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4C_IDAT_CHUNK_COUNT'
        $uefiPngProbeIdatCompressedBytes = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4C_IDAT_BYTES'
        $uefiPngProbeZlibCmf = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4C_ZLIB_CMF'
        $uefiPngProbeZlibFlg = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4C_ZLIB_FLG'
        $uefiPngProbeZlibHeaderOkPresent = $serialText.Contains('UEFI_PNG_PROBE_S4C_ZLIB_HEADER_OK')
        $uefiPngProbeBfinal = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4C_BFINAL'
        $uefiPngProbeBtype = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4C_BTYPE'
        $uefiPngProbeHlit = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4C_HLIT'
        $uefiPngProbeHdist = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4C_HDIST'
        $uefiPngProbeHclen = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4C_HCLEN'
        $uefiPngProbeDeepestMarker = Get-DeepestSerialMarker -Text $serialText -Markers @(
            'UEFI_PNG_PROBE_S4C_ENTER'
            'UEFI_PNG_PROBE_S4C_LEN'
            'UEFI_PNG_PROBE_S4C_IHDR_WIDTH'
            'UEFI_PNG_PROBE_S4C_IHDR_HEIGHT'
            'UEFI_PNG_PROBE_S4C_IDAT_AGG_ENTER'
            'UEFI_PNG_PROBE_S4C_IDAT_BYTES'
            'UEFI_PNG_PROBE_S4C_IDAT_AGG_EXIT'
            'UEFI_PNG_PROBE_S4C_ZLIB_HEADER_OK'
            'UEFI_PNG_PROBE_S4C_BFINAL'
            'UEFI_PNG_PROBE_S4C_BTYPE'
            'UEFI_PNG_PROBE_S4C_HLIT'
            'UEFI_PNG_PROBE_S4C_HDIST'
            'UEFI_PNG_PROBE_S4C_HCLEN'
            'UEFI_PNG_PROBE_S4C_EXIT'
        )
    } elseif ($CursorPngStandaloneS4DProbe) {
        $uefiPngProbeLen = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4D_LEN'
        $uefiPngProbeIhdrWidth = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4D_IHDR_WIDTH'
        $uefiPngProbeIhdrHeight = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4D_IHDR_HEIGHT'
        $uefiPngProbeIhdrBitDepth = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4D_IHDR_BIT_DEPTH'
        $uefiPngProbeIhdrColorType = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4D_IHDR_COLOR_TYPE'
        $uefiPngProbeIdatChunkCount = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4D_IDAT_CHUNK_COUNT'
        $uefiPngProbeIdatCompressedBytes = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4D_IDAT_BYTES'
        $uefiPngProbeZlibCmf = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4D_ZLIB_CMF'
        $uefiPngProbeZlibFlg = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4D_ZLIB_FLG'
        $uefiPngProbeZlibHeaderOkPresent = $serialText.Contains('UEFI_PNG_PROBE_S4D_ZLIB_HEADER_OK')
        $uefiPngProbeBfinal = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4D_BFINAL'
        $uefiPngProbeBtype = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4D_BTYPE'
        $uefiPngProbeHlit = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4D_HLIT'
        $uefiPngProbeHdist = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4D_HDIST'
        $uefiPngProbeHclen = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4D_HCLEN'
        $uefiPngProbeCodeLenAlphabetOkPresent = $serialText.Contains('UEFI_PNG_PROBE_S4D_CODELEN_ALPHABET_OK')
        $uefiPngProbeDeepestMarker = Get-DeepestSerialMarker -Text $serialText -Markers @(
            'UEFI_PNG_PROBE_S4D_ENTER'
            'UEFI_PNG_PROBE_S4D_LEN'
            'UEFI_PNG_PROBE_S4D_IHDR_WIDTH'
            'UEFI_PNG_PROBE_S4D_IHDR_HEIGHT'
            'UEFI_PNG_PROBE_S4D_IDAT_AGG_ENTER'
            'UEFI_PNG_PROBE_S4D_IDAT_BYTES'
            'UEFI_PNG_PROBE_S4D_IDAT_AGG_EXIT'
            'UEFI_PNG_PROBE_S4D_ZLIB_HEADER_OK'
            'UEFI_PNG_PROBE_S4D_BFINAL'
            'UEFI_PNG_PROBE_S4D_BTYPE'
            'UEFI_PNG_PROBE_S4D_HLIT'
            'UEFI_PNG_PROBE_S4D_HDIST'
            'UEFI_PNG_PROBE_S4D_HCLEN'
            'UEFI_PNG_PROBE_S4D_CODELEN_ALPHABET_OK'
            'UEFI_PNG_PROBE_S4D_EXIT'
        )
    } elseif ($CursorPngStandaloneS4EProbe) {
        $uefiPngProbeLen = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4E_LEN'
        $uefiPngProbeIhdrWidth = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4E_IHDR_WIDTH'
        $uefiPngProbeIhdrHeight = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4E_IHDR_HEIGHT'
        $uefiPngProbeIhdrBitDepth = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4E_IHDR_BIT_DEPTH'
        $uefiPngProbeIhdrColorType = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4E_IHDR_COLOR_TYPE'
        $uefiPngProbeIdatChunkCount = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4E_IDAT_CHUNK_COUNT'
        $uefiPngProbeIdatCompressedBytes = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4E_IDAT_BYTES'
        $uefiPngProbeZlibCmf = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4E_ZLIB_CMF'
        $uefiPngProbeZlibFlg = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4E_ZLIB_FLG'
        $uefiPngProbeZlibHeaderOkPresent = $serialText.Contains('UEFI_PNG_PROBE_S4E_ZLIB_HEADER_OK')
        $uefiPngProbeBfinal = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4E_BFINAL'
        $uefiPngProbeBtype = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4E_BTYPE'
        $uefiPngProbeHlit = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4E_HLIT'
        $uefiPngProbeHdist = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4E_HDIST'
        $uefiPngProbeHclen = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4E_HCLEN'
        $uefiPngProbeCodeLenAlphabetOkPresent = $serialText.Contains('UEFI_PNG_PROBE_S4E_CODELEN_ALPHABET_OK')
        $uefiPngProbeTablesOkPresent = $serialText.Contains('UEFI_PNG_PROBE_S4E_TABLES_OK')
        $uefiPngProbeTablesBadPresent = $serialText.Contains('UEFI_PNG_PROBE_S4E_TABLES_BAD')
        $uefiPngProbeDeepestMarker = Get-DeepestSerialMarker -Text $serialText -Markers @(
            'UEFI_PNG_PROBE_S4E_ENTER'
            'UEFI_PNG_PROBE_S4E_LEN'
            'UEFI_PNG_PROBE_S4E_IHDR_WIDTH'
            'UEFI_PNG_PROBE_S4E_IHDR_HEIGHT'
            'UEFI_PNG_PROBE_S4E_IDAT_AGG_ENTER'
            'UEFI_PNG_PROBE_S4E_IDAT_BYTES'
            'UEFI_PNG_PROBE_S4E_IDAT_AGG_EXIT'
            'UEFI_PNG_PROBE_S4E_ZLIB_HEADER_OK'
            'UEFI_PNG_PROBE_S4E_BFINAL'
            'UEFI_PNG_PROBE_S4E_BTYPE'
            'UEFI_PNG_PROBE_S4E_HLIT'
            'UEFI_PNG_PROBE_S4E_HDIST'
            'UEFI_PNG_PROBE_S4E_HCLEN'
            'UEFI_PNG_PROBE_S4E_CODELEN_ALPHABET_OK'
            'UEFI_PNG_PROBE_S4E_CL_BUILD_ENTER'
            'UEFI_PNG_PROBE_S4E_CL_TREE_OK'
            'UEFI_PNG_PROBE_S4E_LENGTHS_OK'
            'UEFI_PNG_PROBE_S4E_LIT_TREE_OK'
            'UEFI_PNG_PROBE_S4E_DIST_TREE_OK'
            'UEFI_PNG_PROBE_S4E_TABLES_OK'
            'UEFI_PNG_PROBE_S4E_TABLES_BAD'
            'UEFI_PNG_PROBE_S4E_EXIT'
        )
    } elseif ($CursorPngStandaloneS4FProbe) {
        $uefiPngProbeLen = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4F_LEN'
        $uefiPngProbeIhdrWidth = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4F_IHDR_WIDTH'
        $uefiPngProbeIhdrHeight = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4F_IHDR_HEIGHT'
        $uefiPngProbeIhdrBitDepth = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4F_IHDR_BIT_DEPTH'
        $uefiPngProbeIhdrColorType = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4F_IHDR_COLOR_TYPE'
        $uefiPngProbeIdatChunkCount = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4F_IDAT_CHUNK_COUNT'
        $uefiPngProbeIdatCompressedBytes = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4F_IDAT_BYTES'
        $uefiPngProbeZlibCmf = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4F_ZLIB_CMF'
        $uefiPngProbeZlibFlg = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4F_ZLIB_FLG'
        $uefiPngProbeZlibHeaderOkPresent = $serialText.Contains('UEFI_PNG_PROBE_S4F_ZLIB_HEADER_OK')
        $uefiPngProbeBfinal = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4F_BFINAL'
        $uefiPngProbeBtype = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4F_BTYPE'
        $uefiPngProbeHlit = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4F_HLIT'
        $uefiPngProbeHdist = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4F_HDIST'
        $uefiPngProbeHclen = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4F_HCLEN'
        $uefiPngProbeCodeLenAlphabetOkPresent = $serialText.Contains('UEFI_PNG_PROBE_S4F_CODELEN_ALPHABET_OK')
        $uefiPngProbeTablesOkPresent = $serialText.Contains('UEFI_PNG_PROBE_S4F_DIST_TREE_OK')
        $uefiPngProbeFirstSymbolValue = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4F_SYMBOL_VALUE'
        $uefiPngProbeFirstSymbolLiteralPresent = $serialText.Contains('UEFI_PNG_PROBE_S4F_LITERAL')
        $uefiPngProbeFirstSymbolLengthPresent = $serialText.Contains('UEFI_PNG_PROBE_S4F_LENGTH')
        $uefiPngProbeFirstSymbolEndPresent = $serialText.Contains('UEFI_PNG_PROBE_S4F_END')
        $uefiPngProbeDeepestMarker = Get-DeepestSerialMarker -Text $serialText -Markers @(
            'UEFI_PNG_PROBE_S4F_ENTER'
            'UEFI_PNG_PROBE_S4F_LEN'
            'UEFI_PNG_PROBE_S4F_IHDR_WIDTH'
            'UEFI_PNG_PROBE_S4F_IHDR_HEIGHT'
            'UEFI_PNG_PROBE_S4F_IDAT_AGG_ENTER'
            'UEFI_PNG_PROBE_S4F_IDAT_BYTES'
            'UEFI_PNG_PROBE_S4F_IDAT_AGG_EXIT'
            'UEFI_PNG_PROBE_S4F_ZLIB_HEADER_OK'
            'UEFI_PNG_PROBE_S4F_BFINAL'
            'UEFI_PNG_PROBE_S4F_BTYPE'
            'UEFI_PNG_PROBE_S4F_HLIT'
            'UEFI_PNG_PROBE_S4F_HDIST'
            'UEFI_PNG_PROBE_S4F_HCLEN'
            'UEFI_PNG_PROBE_S4F_CODELEN_ALPHABET_OK'
            'UEFI_PNG_PROBE_S4F_CL_BUILD_ENTER'
            'UEFI_PNG_PROBE_S4F_CL_TREE_OK'
            'UEFI_PNG_PROBE_S4F_LENGTHS_OK'
            'UEFI_PNG_PROBE_S4F_LIT_TREE_OK'
            'UEFI_PNG_PROBE_S4F_DIST_TREE_OK'
            'UEFI_PNG_PROBE_S4F_SYMBOL_VALUE'
            'UEFI_PNG_PROBE_S4F_LITERAL'
            'UEFI_PNG_PROBE_S4F_LENGTH'
            'UEFI_PNG_PROBE_S4F_END'
            'UEFI_PNG_PROBE_S4F_EXIT'
        )
    } elseif ($CursorPngStandaloneS4GProbe) {
        $uefiPngProbeLen = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4G_LEN'
        $uefiPngProbeIhdrWidth = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4G_IHDR_WIDTH'
        $uefiPngProbeIhdrHeight = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4G_IHDR_HEIGHT'
        $uefiPngProbeIhdrBitDepth = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4G_IHDR_BIT_DEPTH'
        $uefiPngProbeIhdrColorType = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4G_IHDR_COLOR_TYPE'
        $uefiPngProbeIdatChunkCount = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4G_IDAT_CHUNK_COUNT'
        $uefiPngProbeIdatCompressedBytes = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4G_IDAT_BYTES'
        $uefiPngProbeZlibCmf = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4G_ZLIB_CMF'
        $uefiPngProbeZlibFlg = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4G_ZLIB_FLG'
        $uefiPngProbeZlibHeaderOkPresent = $serialText.Contains('UEFI_PNG_PROBE_S4G_ZLIB_HEADER_OK')
        $uefiPngProbeBfinal = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4G_BFINAL'
        $uefiPngProbeBtype = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4G_BTYPE'
        $uefiPngProbeHlit = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4G_HLIT'
        $uefiPngProbeHdist = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4G_HDIST'
        $uefiPngProbeHclen = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4G_HCLEN'
        $uefiPngProbeCodeLenAlphabetOkPresent = $serialText.Contains('UEFI_PNG_PROBE_S4G_CODELEN_ALPHABET_OK')
        $uefiPngProbeTablesOkPresent = $serialText.Contains('UEFI_PNG_PROBE_S4G_DIST_TREE_OK')
        $uefiPngProbeTablesBadPresent = $serialText.Contains('UEFI_PNG_PROBE_S4G_TABLES_BAD')
        $uefiPngProbeOneOutputLiteralWritePresent = $serialText.Contains('UEFI_PNG_PROBE_S4G_LITERAL_WRITE')
        $uefiPngProbeOneOutputLenDistPresent = $serialText.Contains('UEFI_PNG_PROBE_S4G_LEN_DIST')
        $uefiPngProbeOneOutputBoundsAbortPresent = $serialText.Contains('UEFI_PNG_PROBE_S4G_BOUNDS_ABORT')
        $uefiPngProbeOneOutputOkPresent = $serialText.Contains('UEFI_PNG_PROBE_S4G_OK')
        $uefiPngProbeDeepestMarker = Get-DeepestSerialMarker -Text $serialText -Markers @(
            'UEFI_PNG_PROBE_S4G_ENTER'
            'UEFI_PNG_PROBE_S4G_LEN'
            'UEFI_PNG_PROBE_S4G_IHDR_WIDTH'
            'UEFI_PNG_PROBE_S4G_IHDR_HEIGHT'
            'UEFI_PNG_PROBE_S4G_IDAT_AGG_ENTER'
            'UEFI_PNG_PROBE_S4G_IDAT_BYTES'
            'UEFI_PNG_PROBE_S4G_IDAT_AGG_EXIT'
            'UEFI_PNG_PROBE_S4G_ZLIB_HEADER_OK'
            'UEFI_PNG_PROBE_S4G_BFINAL'
            'UEFI_PNG_PROBE_S4G_BTYPE'
            'UEFI_PNG_PROBE_S4G_HLIT'
            'UEFI_PNG_PROBE_S4G_HDIST'
            'UEFI_PNG_PROBE_S4G_HCLEN'
            'UEFI_PNG_PROBE_S4G_CODELEN_ALPHABET_OK'
            'UEFI_PNG_PROBE_S4G_CL_BUILD_ENTER'
            'UEFI_PNG_PROBE_S4G_CL_TREE_OK'
            'UEFI_PNG_PROBE_S4G_LENGTHS_OK'
            'UEFI_PNG_PROBE_S4G_LIT_TREE_OK'
            'UEFI_PNG_PROBE_S4G_DIST_TREE_OK'
            'UEFI_PNG_PROBE_S4G_LITERAL_WRITE'
            'UEFI_PNG_PROBE_S4G_LEN_DIST'
            'UEFI_PNG_PROBE_S4G_BOUNDS_ABORT'
            'UEFI_PNG_PROBE_S4G_OK'
            'UEFI_PNG_PROBE_S4G_EXIT'
        )
    } elseif ($CursorPngStandaloneS4HProbe) {
        $uefiPngProbeLen = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4H_LEN'
        $uefiPngProbeIhdrWidth = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4H_IHDR_WIDTH'
        $uefiPngProbeIhdrHeight = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4H_IHDR_HEIGHT'
        $uefiPngProbeIhdrBitDepth = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4H_IHDR_BIT_DEPTH'
        $uefiPngProbeIhdrColorType = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4H_IHDR_COLOR_TYPE'
        $uefiPngProbeIdatChunkCount = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4H_IDAT_CHUNK_COUNT'
        $uefiPngProbeIdatCompressedBytes = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4H_IDAT_BYTES'
        $uefiPngProbeZlibCmf = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4H_ZLIB_CMF'
        $uefiPngProbeZlibFlg = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4H_ZLIB_FLG'
        $uefiPngProbeZlibHeaderOkPresent = $serialText.Contains('UEFI_PNG_PROBE_S4H_ZLIB_HEADER_OK')
        $uefiPngProbeBfinal = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4H_BFINAL'
        $uefiPngProbeBtype = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4H_BTYPE'
        $uefiPngProbeHlit = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4H_HLIT'
        $uefiPngProbeHdist = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4H_HDIST'
        $uefiPngProbeHclen = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'UEFI_PNG_PROBE_S4H_HCLEN'
        $uefiPngProbeCodeLenAlphabetOkPresent = $serialText.Contains('UEFI_PNG_PROBE_S4H_CODELEN_ALPHABET_OK')
        $uefiPngProbeSmokeProgressPresent = $serialText.Contains('UEFI_PNG_PROBE_S4H_PROGRESS')
        $uefiPngProbeSmokeBoundsAbortPresent = $serialText.Contains('UEFI_PNG_PROBE_S4H_BOUNDS_ABORT')
        $uefiPngProbeSmokeOkPresent = $serialText.Contains('UEFI_PNG_PROBE_S4H_OK')
        $uefiPngProbeDeepestMarker = Get-DeepestSerialMarker -Text $serialText -Markers @(
            'UEFI_PNG_PROBE_S4H_ENTER'
            'UEFI_PNG_PROBE_S4H_LEN'
            'UEFI_PNG_PROBE_S4H_IHDR_WIDTH'
            'UEFI_PNG_PROBE_S4H_IHDR_HEIGHT'
            'UEFI_PNG_PROBE_S4H_IDAT_AGG_ENTER'
            'UEFI_PNG_PROBE_S4H_IDAT_BYTES'
            'UEFI_PNG_PROBE_S4H_IDAT_AGG_EXIT'
            'UEFI_PNG_PROBE_S4H_ZLIB_HEADER_OK'
            'UEFI_PNG_PROBE_S4H_BFINAL'
            'UEFI_PNG_PROBE_S4H_BTYPE'
            'UEFI_PNG_PROBE_S4H_HLIT'
            'UEFI_PNG_PROBE_S4H_HDIST'
            'UEFI_PNG_PROBE_S4H_HCLEN'
            'UEFI_PNG_PROBE_S4H_CODELEN_ALPHABET_OK'
            'UEFI_PNG_PROBE_S4H_PROGRESS'
            'UEFI_PNG_PROBE_S4H_BOUNDS_ABORT'
            'UEFI_PNG_PROBE_S4H_OK'
            'UEFI_PNG_PROBE_S4H_EXIT'
        )
    }
    $cursorPngLoadWrapperEnterPresent = $serialText.Contains('CURSOR_PNG_LOAD_WRAPPER_ENTER')
    $cursorPngLoadWrapperBeforeCallPresent = $serialText.Contains('CURSOR_PNG_LOAD_WRAPPER_BEFORE_CALL')
    $cursorPngLoadWrapperAfterCallPresent = $serialText.Contains('CURSOR_PNG_LOAD_WRAPPER_AFTER_CALL')
    $cursorPngLoadWrapperExitPresent = $serialText.Contains('CURSOR_PNG_LOAD_WRAPPER_EXIT')
    $cursorPngLoadAfterIhdrPresent = $serialText.Contains('UEFI_PROBE_CURSOR_PNG_LOAD_AFTER_IHDR=1')
    $cursorPngLoadAfterChunkScanPresent = $serialText.Contains('UEFI_PROBE_CURSOR_PNG_LOAD_AFTER_CHUNK_SCAN=1')
    $cursorPngLoadAfterIdatAggregationPresent = $serialText.Contains('UEFI_PROBE_CURSOR_PNG_LOAD_AFTER_IDAT_AGGREGATION=1')
    $cursorPngDecompressPreMetaPresent = $serialText.Contains('UEFI_PROBE_CURSOR_PNG_DECOMPRESS_PREMETA=1')
    $cursorPngDecompressNoopPresent = $serialText.Contains('UEFI_PROBE_CURSOR_PNG_DECOMPRESS_NOOP=1')
    $cursorPngDecompressBytesNoopPresent = $serialText.Contains('UEFI_PROBE_CURSOR_PNG_DECOMPRESS_BYTES_NOOP=1')
    $cursorPngDecompressZlibHeaderPresent = $serialText.Contains('UEFI_PROBE_CURSOR_PNG_DECOMPRESS_ZLIB_HEADER=1')
    $cursorPngDecompressOutputAllocPresent = $serialText.Contains('UEFI_PROBE_CURSOR_PNG_DECOMPRESS_OUTPUT_ALLOC=1')
    $cursorPngDecompressDeflateHeaderPresent = $serialText.Contains('UEFI_PROBE_CURSOR_PNG_DECOMPRESS_DEFLATE_HEADER=1')
    $cursorPngDecompressHuffmanSetupPresent = $serialText.Contains('UEFI_PROBE_CURSOR_PNG_DECOMPRESS_HUFFMAN_SETUP=1')
    $cursorPngDecompressInflateSmokePresent = $serialText.Contains('UEFI_PROBE_CURSOR_PNG_DECOMPRESS_INFLATE_SMOKE=1')
    $cursorPngDecompressTinyBoundaryPresent = $serialText.Contains('UEFI_PROBE_CURSOR_PNG_DECOMPRESS_TINY_BOUNDARY=1')
    $cursorPngDecompressTinyBitReaderPresent = $serialText.Contains('UEFI_PROBE_CURSOR_PNG_DECOMPRESS_TINY_BITREADER=1')
    $cursorPngDecompressTinyFirstSymbolPresent = $serialText.Contains('UEFI_PROBE_CURSOR_PNG_DECOMPRESS_TINY_FIRST_SYMBOL=1')
    $cursorPngDecompressTinyOneOpPresent = $serialText.Contains('UEFI_PROBE_CURSOR_PNG_DECOMPRESS_TINY_ONE_OP=1')
    $cursorPngDecompressTinyInflateSmokePresent = $serialText.Contains('UEFI_PROBE_CURSOR_PNG_DECOMPRESS_TINY_INFLATE_SMOKE=1')
    $cursorPngLoadAfterDecompressPresent = $serialText.Contains('UEFI_PROBE_CURSOR_PNG_LOAD_AFTER_DECOMPRESS=1')
    $cursorPngLoadAfterImageCreatePresent = $serialText.Contains('UEFI_PROBE_CURSOR_PNG_LOAD_AFTER_IMAGE_CREATE=1')
    $cursorPngNullPresent = $serialText.Contains('CURSOR_PNG_NULL')
    $cursorPngOkPresent = $serialText.Contains('CURSOR_PNG_OK')
    $cursorPngDimsEnterPresent = $serialText.Contains('CURSOR_PNG_DIMS_ENTER')
    $cursorPngDimsExitPresent = $serialText.Contains('CURSOR_PNG_DIMS_EXIT')
    $cursorPngProbeExitPresent = $serialText.Contains('CURSOR_PNG_PROBE_EXIT')
    $pngDecompressInputGateEnterPresent = $serialText.Contains('PNGDECOMP_INPUT_GATE_ENTER')
    $pngDecompressInputGateOkPresent = $serialText.Contains('PNGDECOMP_INPUT_GATE_OK')
    $pngDecompressInputGateBadLenPresent = $serialText.Contains('PNGDECOMP_INPUT_GATE_BAD_LEN')
    $pngDecompressInputGateBadDimsPresent = $serialText.Contains('PNGDECOMP_INPUT_GATE_BAD_DIMS')
    $pngDecompressInputGateBadFormatPresent = $serialText.Contains('PNGDECOMP_INPUT_GATE_BAD_FORMAT')
    $pngDecompressInputGateBadIdatPresent = $serialText.Contains('PNGDECOMP_INPUT_GATE_BAD_IDAT')
    $pngDecompressInputGateExitPresent = $serialText.Contains('PNGDECOMP_INPUT_GATE_EXIT')
    $pngDecompressTinyBoundaryEnterPresent = $serialText.Contains('PNGDECOMP_TINY_BOUNDARY_ENTER')
    $pngDecompressTinyBoundaryAfterSetupPresent = $serialText.Contains('PNGDECOMP_TINY_BOUNDARY_AFTER_SETUP')
    $pngDecompressTinyBoundaryBeforeReturnPresent = $serialText.Contains('PNGDECOMP_TINY_BOUNDARY_BEFORE_RETURN')
    $pngDecompressTinyBoundaryExitPresent = $serialText.Contains('PNGDECOMP_TINY_BOUNDARY_EXIT')
    $pngDecompressTinyBitReaderEnterPresent = $serialText.Contains('PNGDECOMP_TINY_BITREADER_ENTER')
    $pngDecompressTinyBitReaderBytePos = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'PNGDECOMP_TINY_BITREADER_BYTEPOS'
    $pngDecompressTinyBitReaderBitCount = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'PNGDECOMP_TINY_BITREADER_BITCOUNT'
    $pngDecompressTinyBitReaderExitPresent = $serialText.Contains('PNGDECOMP_TINY_BITREADER_EXIT')
    $pngDecompressTinySymbolEnterPresent = $serialText.Contains('PNGDECOMP_TINY_SYMBOL_ENTER')
    $pngDecompressTinySymbolValue = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'PNGDECOMP_TINY_SYMBOL_VALUE'
    $pngDecompressTinySymbolLiteralPresent = $serialText.Contains('PNGDECOMP_TINY_SYMBOL_LITERAL')
    $pngDecompressTinySymbolLengthPresent = $serialText.Contains('PNGDECOMP_TINY_SYMBOL_LENGTH')
    $pngDecompressTinySymbolEndPresent = $serialText.Contains('PNGDECOMP_TINY_SYMBOL_END')
    $pngDecompressTinySymbolExitPresent = $serialText.Contains('PNGDECOMP_TINY_SYMBOL_EXIT')
    $pngDecompressTinyOneOpEnterPresent = $serialText.Contains('PNGDECOMP_TINY_ONEOP_ENTER')
    $pngDecompressTinyOneOpLiteralPresent = $serialText.Contains('PNGDECOMP_TINY_ONEOP_LITERAL')
    $pngDecompressTinyOneOpLengthPresent = $serialText.Contains('PNGDECOMP_TINY_ONEOP_LENGTH')
    $pngDecompressTinyOneOpBoundsAbortPresent = $serialText.Contains('PNGDECOMP_TINY_ONEOP_BOUNDS_ABORT')
    $pngDecompressTinyOneOpOkPresent = $serialText.Contains('PNGDECOMP_TINY_ONEOP_OK')
    $pngDecompressTinyOneOpExitPresent = $serialText.Contains('PNGDECOMP_TINY_ONEOP_EXIT')
    $pngDecompressTinyInflateEnterPresent = $serialText.Contains('PNGDECOMP_TINY_INFLATE_ENTER')
    $pngDecompressTinyInflateProgressPresent = $serialText.Contains('PNGDECOMP_TINY_INFLATE_PROGRESS')
    $pngDecompressTinyInflateBoundsAbortPresent = $serialText.Contains('PNGDECOMP_TINY_INFLATE_BOUNDS_ABORT')
    $pngDecompressTinyInflateOkPresent = $serialText.Contains('PNGDECOMP_TINY_INFLATE_OK')
    $pngDecompressTinyInflateExitPresent = $serialText.Contains('PNGDECOMP_TINY_INFLATE_EXIT')
    $pngDecompressGateInlineEnterPresent = $serialText.Contains('PNGDECOMP_GATE_INLINE_ENTER')
    $pngDecompressGateInlineOkPresent = $serialText.Contains('PNGDECOMP_GATE_INLINE_OK')
    $pngDecompressGateInlineBeforeReturnPresent = $serialText.Contains('PNGDECOMP_GATE_INLINE_BEFORE_RETURN')
    $pngDecompressGateHelperEnterPresent = $serialText.Contains('PNGDECOMP_GATE_HELPER_ENTER')
    $pngDecompressGateHelperOkPresent = $serialText.Contains('PNGDECOMP_GATE_HELPER_OK')
    $pngDecompressGateHelperReturningPresent = $serialText.Contains('PNGDECOMP_GATE_HELPER_RETURNING')
    $pngDecompressGateBoolEnterPresent = $serialText.Contains('PNGDECOMP_GATE_BOOL_ENTER')
    $pngDecompressGateBoolOkPresent = $serialText.Contains('PNGDECOMP_GATE_BOOL_OK')
    $pngDecompressGateBoolExitPresent = $serialText.Contains('PNGDECOMP_GATE_BOOL_EXIT')
    $pngDecompressGateBoolCallerAfterPresent = $serialText.Contains('PNGDECOMP_GATE_BOOL_CALLER_AFTER')
    $pngDecompressGateNoStateCopyEnterPresent = $serialText.Contains('PNGDECOMP_GATE_NO_STATE_COPY_ENTER')
    $pngDecompressGateNoStateCopyAfterPresent = $serialText.Contains('PNGDECOMP_GATE_NO_STATE_COPY_AFTER')
    $pngDecompressPostGateFirstInstructionPresent = $serialText.Contains('PNGDECOMP_POST_GATE_FIRST_INSTRUCTION')
    $pngDecompressAfterInputGateEnterPresent = $serialText.Contains('PNGDECOMP_AFTER_INPUT_GATE_ENTER')
    $pngDecompressAfterInputGateExitPresent = $serialText.Contains('PNGDECOMP_AFTER_INPUT_GATE_EXIT')
    $pngDecompressPrepRouteEnterPresent = $serialText.Contains('PNGDECOMP_PREP_ROUTE_ENTER')
    $pngDecompressPrepRouteExitPresent = $serialText.Contains('PNGDECOMP_PREP_ROUTE_EXIT')
    $pngDecompressPrepNoopEnterPresent = $serialText.Contains('PNGDECOMP_PREP_NOOP_ENTER')
    $pngDecompressPrepNoopExitPresent = $serialText.Contains('PNGDECOMP_PREP_NOOP_EXIT')
    $pngDecompressPrepMetaEnterPresent = $serialText.Contains('PNGDECOMP_PREP_META_ENTER')
    $pngDecompressPrepMetaDimsOkPresent = $serialText.Contains('PNGDECOMP_PREP_META_DIMS_OK')
    $pngDecompressPrepMetaLenOkPresent = $serialText.Contains('PNGDECOMP_PREP_META_LEN_OK')
    $pngDecompressPrepMetaExitPresent = $serialText.Contains('PNGDECOMP_PREP_META_EXIT')
    $pngDecompressPrepBytesEnterPresent = $serialText.Contains('PNGDECOMP_PREP_BYTES_ENTER')
    $pngDecompressPrepBytesNullPresent = $serialText.Contains('PNGDECOMP_PREP_BYTES_NULL')
    $pngDecompressPrepBytesOkPresent = $serialText.Contains('PNGDECOMP_PREP_BYTES_OK')
    $pngDecompressPrepBytesLen = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'PNGDECOMP_PREP_BYTES_LEN'
    $pngDecompressPrepBytesExitPresent = $serialText.Contains('PNGDECOMP_PREP_BYTES_EXIT')
    $pngDecompressPrepInlineEnterPresent = $serialText.Contains('PNGDECOMP_PREP_INLINE_ENTER')
    $pngDecompressPrepInlineBeforeOutputAllocPresent = $serialText.Contains('PNGDECOMP_PREP_INLINE_BEFORE_OUTPUT_ALLOC')
    $pngDecompressPrepInlineAfterOutputAllocPresent = $serialText.Contains('PNGDECOMP_PREP_INLINE_AFTER_OUTPUT_ALLOC')
    $pngDecompressPrepInlineBeforeBitreaderInitPresent = $serialText.Contains('PNGDECOMP_PREP_INLINE_BEFORE_BITREADER_INIT')
    $pngDecompressPrepInlineAfterBitreaderInitPresent = $serialText.Contains('PNGDECOMP_PREP_INLINE_AFTER_BITREADER_INIT')
    $pngDecompressPrepInlineBeforeTableRefsPresent = $serialText.Contains('PNGDECOMP_PREP_INLINE_BEFORE_TABLE_REFS')
    $pngDecompressPrepInlineAfterTableRefsPresent = $serialText.Contains('PNGDECOMP_PREP_INLINE_AFTER_TABLE_REFS')
    $pngDecompressPrepInlineExitPresent = $serialText.Contains('PNGDECOMP_PREP_INLINE_EXIT')
    $pngDecompressPrepBoundaryEnterPresent = $serialText.Contains('PNGDECOMP_PREP_BOUNDARY_ENTER')
    $pngDecompressPrepBoundaryBeforeCallPresent = $serialText.Contains('PNGDECOMP_PREP_BOUNDARY_BEFORE_CALL')
    $pngDecompressPrepBoundaryAfterCallPresent = $serialText.Contains('PNGDECOMP_PREP_BOUNDARY_AFTER_CALL')
    $pngDecompressPrepBoundaryOkPresent = $serialText.Contains('PNGDECOMP_PREP_BOUNDARY_OK')
    $pngDecompressPrepBoundaryFailPresent = $serialText.Contains('PNGDECOMP_PREP_BOUNDARY_FAIL')
    $pngDecompressPrepBoundaryExitPresent = $serialText.Contains('PNGDECOMP_PREP_BOUNDARY_EXIT')
    $pngDecompressPrepContextEnterPresent = $serialText.Contains('PNGDECOMP_PREP_CONTEXT_ENTER')
    $pngDecompressPrepContextValidateEnterPresent = $serialText.Contains('PNGDECOMP_PREP_CONTEXT_VALIDATE_ENTER')
    $pngDecompressPrepContextValidateExitPresent = $serialText.Contains('PNGDECOMP_PREP_CONTEXT_VALIDATE_EXIT')
    $pngDecompressPrepContextAllocEnterPresent = $serialText.Contains('PNGDECOMP_PREP_CONTEXT_ALLOC_ENTER')
    $pngDecompressPrepContextAllocExitPresent = $serialText.Contains('PNGDECOMP_PREP_CONTEXT_ALLOC_EXIT')
    $pngDecompressPrepContextBitreaderEnterPresent = $serialText.Contains('PNGDECOMP_PREP_CONTEXT_BITREADER_ENTER')
    $pngDecompressPrepContextBitreaderExitPresent = $serialText.Contains('PNGDECOMP_PREP_CONTEXT_BITREADER_EXIT')
    $pngDecompressPrepContextTablesEnterPresent = $serialText.Contains('PNGDECOMP_PREP_CONTEXT_TABLES_ENTER')
    $pngDecompressPrepContextTablesExitPresent = $serialText.Contains('PNGDECOMP_PREP_CONTEXT_TABLES_EXIT')
    $pngDecompressPrepContextOkPresent = $serialText.Contains('PNGDECOMP_PREP_CONTEXT_OK')
    $pngDecompressPrepContextFailPresent = $serialText.Contains('PNGDECOMP_PREP_CONTEXT_FAIL')
    $pngDecompressPrepContextExitPresent = $serialText.Contains('PNGDECOMP_PREP_CONTEXT_EXIT')
    $pngDecompressFirstSymbolBoundaryEnterPresent = $serialText.Contains('PNGDECOMP_FIRST_SYMBOL_BOUNDARY_ENTER')
    $pngDecompressFirstSymbolBoundaryExitPresent = $serialText.Contains('PNGDECOMP_FIRST_SYMBOL_BOUNDARY_EXIT')
    $pngDecompressBitReaderEnterPresent = $serialText.Contains('PNGDECOMP_BITREADER_ENTER')
    $pngDecompressBitReaderBytePos = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'PNGDECOMP_BITREADER_BYTEPOS'
    $pngDecompressBitReaderBitCount = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'PNGDECOMP_BITREADER_BITCOUNT'
    $pngDecompressBitReaderOkPresent = $serialText.Contains('PNGDECOMP_BITREADER_OK')
    $pngDecompressBitReaderBadPresent = $serialText.Contains('PNGDECOMP_BITREADER_BAD')
    $pngDecompressBitReaderExitPresent = $serialText.Contains('PNGDECOMP_BITREADER_EXIT')
    $pngDecompressFirstSymbolDecodeEnterPresent = $serialText.Contains('PNGDECOMP_FIRST_SYMBOL_DECODE_ENTER')
    $pngDecompressFirstSymbolValue = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'PNGDECOMP_FIRST_SYMBOL_VALUE'
    $pngDecompressFirstSymbolLiteralPresent = $serialText.Contains('PNGDECOMP_FIRST_SYMBOL_LITERAL')
    $pngDecompressFirstSymbolEndPresent = $serialText.Contains('PNGDECOMP_FIRST_SYMBOL_END')
    $pngDecompressFirstSymbolLengthPresent = $serialText.Contains('PNGDECOMP_FIRST_SYMBOL_LENGTH')
    $pngDecompressFirstSymbolDecodeExitPresent = $serialText.Contains('PNGDECOMP_FIRST_SYMBOL_DECODE_EXIT')
    $pngDecompressLiteralWriteEnterPresent = $serialText.Contains('PNGDECOMP_LITERAL_WRITE_ENTER')
    $pngDecompressLiteralWriteBoundsOkPresent = $serialText.Contains('PNGDECOMP_LITERAL_WRITE_BOUNDS_OK')
    $pngDecompressLiteralWriteBoundsBadPresent = $serialText.Contains('PNGDECOMP_LITERAL_WRITE_BOUNDS_BAD')
    $pngDecompressLiteralWriteExitPresent = $serialText.Contains('PNGDECOMP_LITERAL_WRITE_EXIT')
    $pngDecompressLenDistEnterPresent = $serialText.Contains('PNGDECOMP_LEN_DIST_ENTER')
    $pngDecompressLengthSymbol = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'PNGDECOMP_LENGTH_SYMBOL'
    $pngDecompressLengthValue = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'PNGDECOMP_LENGTH_VALUE'
    $pngDecompressDistanceSymbol = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'PNGDECOMP_DISTANCE_SYMBOL'
    $pngDecompressDistanceValue = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'PNGDECOMP_DISTANCE_VALUE'
    $pngDecompressLenDistExitPresent = $serialText.Contains('PNGDECOMP_LEN_DIST_EXIT')
    $pngDecompressOneStepEnterPresent = $serialText.Contains('PNGDECOMP_ONE_STEP_ENTER')
    $pngDecompressOneStepLiteralPresent = $serialText.Contains('PNGDECOMP_ONE_STEP_LITERAL')
    $pngDecompressOneStepBackrefPresent = $serialText.Contains('PNGDECOMP_ONE_STEP_BACKREF')
    $pngDecompressOneStepBoundsAbortPresent = $serialText.Contains('PNGDECOMP_ONE_STEP_BOUNDS_ABORT')
    $pngDecompressOneStepOkPresent = $serialText.Contains('PNGDECOMP_ONE_STEP_OK')
    $pngDecompressOneStepExitPresent = $serialText.Contains('PNGDECOMP_ONE_STEP_EXIT')
    $pngLoaderDecompressPreMetaEnterPresent = $serialText.Contains('PNGLOADER_DECOMPRESS_PREMETA_ENTER')
    $pngLoaderDecompressPreMetaLen = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'PNGLOADER_DECOMPRESS_PREMETA_LEN'
    $pngLoaderDecompressPreMetaExpectedOutLen = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'PNGLOADER_DECOMPRESS_PREMETA_EXPECTED_OUT_LEN'
    $pngLoaderDecompressPreMetaExitPresent = $serialText.Contains('PNGLOADER_DECOMPRESS_PREMETA_EXIT')
    $pngDecompressNoopEnterPresent = $serialText.Contains('PNGDECOMP_NOOP_ENTER')
    $pngDecompressNoopExitPresent = $serialText.Contains('PNGDECOMP_NOOP_EXIT')
    $pngDecompressBytesNoopEnterPresent = $serialText.Contains('PNGDECOMP_BYTES_NOOP_ENTER')
    $pngDecompressBytesNoopNullPresent = $serialText.Contains('PNGDECOMP_BYTES_NOOP_NULL')
    $pngDecompressBytesNoopOkPresent = $serialText.Contains('PNGDECOMP_BYTES_NOOP_OK')
    $pngDecompressBytesNoopLen = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'PNGDECOMP_BYTES_NOOP_LEN'
    $pngDecompressBytesNoopExitPresent = $serialText.Contains('PNGDECOMP_BYTES_NOOP_EXIT')
    $pngDecompressZlibHeaderEnterPresent = $serialText.Contains('PNGDECOMP_ZLIB_HEADER_ENTER')
    $pngDecompressZlibLenOkPresent = $serialText.Contains('PNGDECOMP_ZLIB_LEN_OK')
    $pngDecompressZlibLenBadPresent = $serialText.Contains('PNGDECOMP_ZLIB_LEN_BAD')
    $pngDecompressZlibCmf = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'PNGDECOMP_ZLIB_CMF'
    $pngDecompressZlibFlg = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'PNGDECOMP_ZLIB_FLG'
    $pngDecompressZlibMethodOkPresent = $serialText.Contains('PNGDECOMP_ZLIB_METHOD_OK')
    $pngDecompressZlibMethodBadPresent = $serialText.Contains('PNGDECOMP_ZLIB_METHOD_BAD')
    $pngDecompressZlibFcheckOkPresent = $serialText.Contains('PNGDECOMP_ZLIB_FCHECK_OK')
    $pngDecompressZlibFcheckBadPresent = $serialText.Contains('PNGDECOMP_ZLIB_FCHECK_BAD')
    $pngDecompressZlibFdictSetPresent = $serialText.Contains('PNGDECOMP_ZLIB_FDICT_SET')
    $pngDecompressZlibFdictClearPresent = $serialText.Contains('PNGDECOMP_ZLIB_FDICT_CLEAR')
    $pngDecompressZlibHeaderExitPresent = $serialText.Contains('PNGDECOMP_ZLIB_HEADER_EXIT')
    $pngDecompressOutputAllocEnterPresent = $serialText.Contains('PNGDECOMP_OUTPUT_ALLOC_ENTER')
    $pngDecompressOutputAllocSize = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'PNGDECOMP_OUTPUT_ALLOC_SIZE'
    $pngDecompressOutputAllocOkPresent = $serialText.Contains('PNGDECOMP_OUTPUT_ALLOC_OK')
    $pngDecompressOutputAllocNullPresent = $serialText.Contains('PNGDECOMP_OUTPUT_ALLOC_NULL')
    $pngDecompressOutputAllocExitPresent = $serialText.Contains('PNGDECOMP_OUTPUT_ALLOC_EXIT')
    $pngDecompressDeflateEnterPresent = $serialText.Contains('PNGDECOMP_DEFLATE_ENTER')
    $pngDecompressDeflateFirstBlockEnterPresent = $serialText.Contains('PNGDECOMP_DEFLATE_FIRST_BLOCK_ENTER')
    $pngDecompressDeflateBfinal = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'PNGDECOMP_DEFLATE_BFINAL'
    $pngDecompressDeflateBtype = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'PNGDECOMP_DEFLATE_BTYPE'
    $pngDecompressDeflateFirstBlockExitPresent = $serialText.Contains('PNGDECOMP_DEFLATE_FIRST_BLOCK_EXIT')
    $pngDecompressDeflateExitPresent = $serialText.Contains('PNGDECOMP_DEFLATE_EXIT')
    $pngDecompressHuffmanSetupEnterPresent = $serialText.Contains('PNGDECOMP_HUFFMAN_SETUP_ENTER')
    $pngDecompressHuffmanFixedPresent = $serialText.Contains('PNGDECOMP_HUFFMAN_FIXED')
    $pngDecompressHuffmanDynamicPresent = $serialText.Contains('PNGDECOMP_HUFFMAN_DYNAMIC')
    $pngDecompressHuffmanSetupOkPresent = $serialText.Contains('PNGDECOMP_HUFFMAN_SETUP_OK')
    $pngDecompressHuffmanSetupBadPresent = $serialText.Contains('PNGDECOMP_HUFFMAN_SETUP_BAD')
    $pngDecompressHuffmanSetupExitPresent = $serialText.Contains('PNGDECOMP_HUFFMAN_SETUP_EXIT')
    $pngDecompressInflateEnterPresent = $serialText.Contains('PNGDECOMP_INFLATE_ENTER')
    $pngDecompressInflateFirstSymbolEnterPresent = $serialText.Contains('PNGDECOMP_INFLATE_FIRST_SYMBOL_ENTER')
    $pngDecompressInflateFirstSymbolExitPresent = $serialText.Contains('PNGDECOMP_INFLATE_FIRST_SYMBOL_EXIT')
    $pngDecompressInflateProgressPresent = $serialText.Contains('PNGDECOMP_INFLATE_PROGRESS')
    $pngDecompressInflateBoundsAbortPresent = $serialText.Contains('PNGDECOMP_INFLATE_BOUNDS_ABORT')
    $pngDecompressInflateOkPresent = $serialText.Contains('PNGDECOMP_INFLATE_OK')
    $pngDecompressInflateExitPresent = $serialText.Contains('PNGDECOMP_INFLATE_EXIT')
    $loadPngSafeEnterPresent = $serialText.Contains('LOADPNGSAFE_ENTER')
    $loadPngSafeReadFailPresent = $serialText.Contains('LOADPNGSAFE_READ_FAIL')
    $loadPngSafeReadNullPresent = $serialText.Contains('LOADPNGSAFE_READ_NULL')
    $loadPngSafeReadOkPresent = $serialText.Contains('LOADPNGSAFE_READ_OK')
    $loadPngSafeDataTooSmallPresent = $serialText.Contains('LOADPNGSAFE_DATA_TOO_SMALL')
    $loadPngSafeBeforePngLoaderLoadPresent = $serialText.Contains('LOADPNGSAFE_BEFORE_PNGLOADER_LOAD')
    $loadPngSafeOkPresent = $serialText.Contains('LOADPNGSAFE_OK')
    $loadPngSafeNullPresent = $serialText.Contains('LOADPNGSAFE_NULL')
    $loadPngSafeExitPresent = $serialText.Contains('LOADPNGSAFE_EXIT')
    $pngLoaderEntryBaselineEnterPresent = $serialText.Contains('PNGLOADER_ENTRY_BASELINE_ENTER')
    $pngLoaderEntryBaselineBytesOkPresent = $serialText.Contains('PNGLOADER_ENTRY_BASELINE_BYTES_OK')
    $pngLoaderEntryBaselineLen = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'PNGLOADER_ENTRY_BASELINE_LEN'
    $pngLoaderEntryBaselineHeaderOkPresent = $serialText.Contains('PNGLOADER_ENTRY_BASELINE_HEADER_OK')
    $pngLoaderEntryBaselineIhdrOkPresent = $serialText.Contains('PNGLOADER_ENTRY_BASELINE_IHDR_OK')
    $pngLoaderEntryBaselineIhdrWidth = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'PNGLOADER_ENTRY_BASELINE_IHDR_WIDTH'
    $pngLoaderEntryBaselineIhdrHeight = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'PNGLOADER_ENTRY_BASELINE_IHDR_HEIGHT'
    $pngLoaderEntryBaselineExitPresent = $serialText.Contains('PNGLOADER_ENTRY_BASELINE_EXIT')
    $pngLoaderEnterPresent = if ($CursorPngLoadAfterIhdrProbe) { $pngLoaderEntryBaselineEnterPresent } else { $serialText.Contains('PNGLOADER_ENTER') }
    $pngLoaderBytesNullPresent = if ($CursorPngLoadAfterIhdrProbe) { $false } else { $serialText.Contains('PNGLOADER_BYTES_NULL') }
    $pngLoaderBytesOkPresent = if ($CursorPngLoadAfterIhdrProbe) { $pngLoaderEntryBaselineBytesOkPresent } else { $serialText.Contains('PNGLOADER_BYTES_OK') }
    $pngLoaderBytesLength = if ($CursorPngLoadAfterIhdrProbe) { $pngLoaderEntryBaselineLen } else { Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'PNGLOADER_BYTES_LENGTH' }
    $pngLoaderHeaderEnterPresent = if ($CursorPngLoadAfterIhdrProbe) { $false } else { $serialText.Contains('PNGLOADER_HEADER_ENTER') }
    $pngLoaderHeaderOkPresent = if ($CursorPngLoadAfterIhdrProbe) { $pngLoaderEntryBaselineHeaderOkPresent } else { $serialText.Contains('PNGLOADER_HEADER_OK') }
    $pngLoaderHeaderBadPresent = if ($CursorPngLoadAfterIhdrProbe) { $false } else { $serialText.Contains('PNGLOADER_HEADER_BAD') }
    $pngLoaderIhdrEnterPresent = if ($CursorPngLoadAfterIhdrProbe) { $false } else { $serialText.Contains('PNGLOADER_IHDR_ENTER') }
    $pngLoaderIhdrOkPresent = if ($CursorPngLoadAfterIhdrProbe) { $pngLoaderEntryBaselineIhdrOkPresent } else { $serialText.Contains('PNGLOADER_IHDR_OK') }
    $pngLoaderIhdrBadPresent = if ($CursorPngLoadAfterIhdrProbe) { $false } else { $serialText.Contains('PNGLOADER_IHDR_BAD') }
    $pngLoaderIhdrDimsPresent = if ($CursorPngLoadAfterIhdrProbe) { $false } else { $serialText.Contains('PNGLOADER_IHDR_DIMS') }
    $pngLoaderIhdrWidth = if ($CursorPngLoadAfterIhdrProbe) { $pngLoaderEntryBaselineIhdrWidth } else { Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'PNGLOADER_IHDR_WIDTH' }
    $pngLoaderIhdrHeight = if ($CursorPngLoadAfterIhdrProbe) { $pngLoaderEntryBaselineIhdrHeight } else { Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'PNGLOADER_IHDR_HEIGHT' }
    $pngLoaderIhdrBitDepth = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'PNGLOADER_IHDR_BIT_DEPTH'
    $pngLoaderIhdrColorType = Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'PNGLOADER_IHDR_COLOR_TYPE'
    $pngLoaderChunkScanEnterPresent = if ($CursorPngLoadAfterIhdrProbe) { $false } else { $serialText.Contains('PNGLOADER_CHUNK_SCAN_ENTER') }
    $pngLoaderChunkTypeIhdrPresent = if ($CursorPngLoadAfterIhdrProbe) { $false } else { $serialText.Contains('PNGLOADER_CHUNK_TYPE_IHDR') }
    $pngLoaderChunkTypeIdatPresent = if ($CursorPngLoadAfterIhdrProbe) { $false } else { $serialText.Contains('PNGLOADER_CHUNK_TYPE_IDAT') }
    $pngLoaderChunkTypeIendPresent = if ($CursorPngLoadAfterIhdrProbe) { $false } else { $serialText.Contains('PNGLOADER_CHUNK_TYPE_IEND') }
    $pngLoaderChunkScanExitPresent = if ($CursorPngLoadAfterIhdrProbe) { $false } else { $serialText.Contains('PNGLOADER_CHUNK_SCAN_EXIT') }
    $pngLoaderIdatChunkCount = if ($CursorPngLoadAfterIhdrProbe) { $null } else { Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'PNGLOADER_IDAT_CHUNK_COUNT' }
    $pngLoaderIdatCompressedBytes = if ($CursorPngLoadAfterIhdrProbe) { $null } else { Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'PNGLOADER_IDAT_COMPRESSED_BYTES' }
    $pngLoaderIdatBytesEnterPresent = if ($CursorPngLoadAfterIhdrProbe) { $false } else { $serialText.Contains('PNGLOADER_IDAT_BYTES_ENTER') }
    $pngLoaderIdatBytesOkPresent = if ($CursorPngLoadAfterIhdrProbe) { $false } else { $serialText.Contains('PNGLOADER_IDAT_BYTES_OK') }
    $pngLoaderIdatBytesEmptyPresent = if ($CursorPngLoadAfterIhdrProbe) { $false } else { $serialText.Contains('PNGLOADER_IDAT_BYTES_EMPTY') }
    $pngLoaderDecompressEnterPresent = if ($CursorPngLoadAfterIhdrProbe) { $false } else { $serialText.Contains('PNGLOADER_DECOMPRESS_ENTER') }
    $pngLoaderDecompressExitPresent = if ($CursorPngLoadAfterIhdrProbe) { $false } else { $serialText.Contains('PNGLOADER_DECOMPRESS_EXIT') }
    $pngLoaderDecompressNullPresent = if ($CursorPngLoadAfterIhdrProbe) { $false } else { $serialText.Contains('PNGLOADER_DECOMPRESS_NULL') }
    $pngLoaderDecompressOkPresent = if ($CursorPngLoadAfterIhdrProbe) { $false } else { $serialText.Contains('PNGLOADER_DECOMPRESS_OK') }
    $pngLoaderDecompressedBytes = if ($CursorPngLoadAfterIhdrProbe) { $null } else { Get-SerialMarkerValue -Text $serialText -MarkerPrefix 'PNGLOADER_DECOMPRESSED_BYTES' }
    $pngLoaderImageCreateEnterPresent = if ($CursorPngLoadAfterIhdrProbe) { $false } else { $serialText.Contains('PNGLOADER_IMAGE_CREATE_ENTER') }
    $pngLoaderImageCreateExitPresent = if ($CursorPngLoadAfterIhdrProbe) { $false } else { $serialText.Contains('PNGLOADER_IMAGE_CREATE_EXIT') }
    $pngLoaderImageNullPresent = if ($CursorPngLoadAfterIhdrProbe) { $false } else { $serialText.Contains('PNGLOADER_IMAGE_NULL') }
    $pngLoaderImageOkPresent = if ($CursorPngLoadAfterIhdrProbe) { $false } else { $serialText.Contains('PNGLOADER_IMAGE_OK') }
    $pngLoaderExitPresent = if ($CursorPngLoadAfterIhdrProbe) { $pngLoaderEntryBaselineExitPresent } else { $serialText.Contains('PNGLOADER_EXIT') }
    $cursorValidateEnterPresent = $serialText.Contains('CURSOR_VALIDATE_ENTER')
    $cursorValidateExitPresent = $serialText.Contains('CURSOR_VALIDATE_EXIT')
    $cursorValidateNullPresent = $serialText.Contains('CURSOR_VALIDATE_NULL')
    $cursorValidateBadDimsPresent = $serialText.Contains('CURSOR_VALIDATE_BAD_DIMS')
    $cursorValidateBadRawDataPresent = $serialText.Contains('CURSOR_VALIDATE_BAD_RAWDATA')
    $cursorValidateOkPresent = $serialText.Contains('CURSOR_VALIDATE_OK')
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
    $cursorPngDims = Get-CursorProbeDims -Lines $serialLines -EnterMarker 'CURSOR_PNG_DIMS_ENTER' -ExitMarker 'CURSOR_PNG_DIMS_EXIT'
    $cursorPngWidth = if ($cursorPngDims) { $cursorPngDims.Width } else { $null }
    $cursorPngHeight = if ($cursorPngDims) { $cursorPngDims.Height } else { $null }
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
    $cursorPngMarkerSequence = @(
        'CURSOR_PNG_PROBE_ENTER'
        'CURSOR_PNG_BEFORE_DISK_CHECK'
        'CURSOR_PNG_AFTER_DISK_CHECK'
        'CURSOR_PNG_BEFORE_FILE_LOOKUP'
        'CURSOR_PNG_AFTER_FILE_LOOKUP'
        'CURSOR_PNG_BYTES_LENGTH='
        'CURSOR_PNG_HEADER_HELPER_ENTER'
        'CURSOR_PNG_HEADER_HELPER_BAD'
        'CURSOR_PNG_HEADER_HELPER_OK'
        'CURSOR_PNG_HEADER_HELPER_EXIT'
        'CURSOR_PNG_NOOP_LOADER_ENTER'
        'CURSOR_PNG_NOOP_LOADER_EXIT'
        'CURSOR_PNG_BYTES_NOOP_ENTER'
        'CURSOR_PNG_BYTES_NOOP_NULL'
        'CURSOR_PNG_BYTES_NOOP_OK'
        'CURSOR_PNG_BYTES_NOOP_EXIT'
        'CURSOR_PNG_IHDR_HELPER_ENTER'
        'CURSOR_PNG_IHDR_HELPER_BAD'
        'CURSOR_PNG_IHDR_HELPER_OK'
        'CURSOR_PNG_IHDR_WIDTH='
        'CURSOR_PNG_IHDR_HEIGHT='
        'CURSOR_PNG_IHDR_HELPER_EXIT'
        'CURSOR_PNG_LOAD_WRAPPER_ENTER'
        'CURSOR_PNG_LOAD_WRAPPER_BEFORE_CALL'
        'CURSOR_PNG_LOAD_WRAPPER_AFTER_CALL'
        'CURSOR_PNG_LOAD_WRAPPER_EXIT'
        'CURSOR_PNG_NULL'
        'CURSOR_PNG_OK'
        'CURSOR_PNG_DIMS_ENTER'
        'CURSOR_PNG_DIMS_EXIT'
        'CURSOR_PNG_PROBE_EXIT'
    )
    $cursorPngDeepestMarker = $null
    foreach ($marker in $cursorPngMarkerSequence) {
        if ($serialText.Contains($marker)) {
            $cursorPngDeepestMarker = $marker
        }
    }
    $pngLoaderMarkerSequence = if ($CursorPngLoadAfterIhdrProbe) {
        @(
            'PNGLOADER_ENTRY_BASELINE_ENTER'
            'PNGLOADER_ENTRY_BASELINE_BYTES_OK'
            'PNGLOADER_ENTRY_BASELINE_LEN'
            'PNGLOADER_ENTRY_BASELINE_HEADER_OK'
            'PNGLOADER_ENTRY_BASELINE_IHDR_OK'
            'PNGLOADER_ENTRY_BASELINE_IHDR_WIDTH'
            'PNGLOADER_ENTRY_BASELINE_IHDR_HEIGHT'
            'PNGLOADER_ENTRY_BASELINE_EXIT'
        )
    } else {
        @(
            'PNGLOADER_ENTER'
            'PNGLOADER_BYTES_NULL'
            'PNGLOADER_BYTES_OK'
            'PNGLOADER_HEADER_ENTER'
            'PNGLOADER_HEADER_BAD'
            'PNGLOADER_HEADER_OK'
            'PNGLOADER_IHDR_ENTER'
            'PNGLOADER_IHDR_BAD'
            'PNGLOADER_IHDR_OK'
            'PNGLOADER_IHDR_DIMS'
            'PNGLOADER_CHUNK_SCAN_ENTER'
            'PNGLOADER_CHUNK_TYPE_IHDR'
            'PNGLOADER_CHUNK_TYPE_IDAT'
            'PNGLOADER_CHUNK_TYPE_IEND'
            'PNGLOADER_CHUNK_SCAN_EXIT'
            'PNGLOADER_IDAT_BYTES_ENTER'
            'PNGLOADER_IDAT_BYTES_EMPTY'
            'PNGLOADER_IDAT_BYTES_OK'
            'PNGLOADER_DECOMPRESS_PREMETA_ENTER'
            'PNGLOADER_DECOMPRESS_PREMETA_LEN'
            'PNGLOADER_DECOMPRESS_PREMETA_EXPECTED_OUT_LEN'
            'PNGLOADER_DECOMPRESS_PREMETA_EXIT'
            'PNGLOADER_DECOMPRESS_ENTER'
            'PNGLOADER_DECOMPRESS_NULL'
            'PNGLOADER_DECOMPRESS_OK'
            'PNGLOADER_DECOMPRESS_EXIT'
            'PNGLOADER_IMAGE_CREATE_ENTER'
            'PNGLOADER_IMAGE_NULL'
            'PNGLOADER_IMAGE_OK'
            'PNGLOADER_IMAGE_CREATE_EXIT'
            'PNGLOADER_EXIT'
            'PNGLOADER_DECOMPRESSED_BYTES'
        )
    }
    $pngLoaderDeepestMarker = $null
    foreach ($marker in $pngLoaderMarkerSequence) {
        if ($serialText.Contains($marker)) {
            $pngLoaderDeepestMarker = $marker
        }
    }
    $pngDecompressMarkerSequence = @(
        'PNGDECOMP_INPUT_GATE_ENTER'
        'PNGDECOMP_INPUT_GATE_BAD_LEN'
        'PNGDECOMP_INPUT_GATE_BAD_DIMS'
        'PNGDECOMP_INPUT_GATE_BAD_FORMAT'
        'PNGDECOMP_INPUT_GATE_BAD_IDAT'
        'PNGDECOMP_INPUT_GATE_OK'
        'PNGDECOMP_INPUT_GATE_EXIT'
        'PNGDECOMP_GATE_INLINE_ENTER'
        'PNGDECOMP_GATE_INLINE_OK'
        'PNGDECOMP_GATE_INLINE_BEFORE_RETURN'
        'PNGDECOMP_GATE_HELPER_ENTER'
        'PNGDECOMP_GATE_HELPER_OK'
        'PNGDECOMP_GATE_HELPER_RETURNING'
        'PNGDECOMP_GATE_BOOL_ENTER'
        'PNGDECOMP_GATE_BOOL_OK'
        'PNGDECOMP_GATE_BOOL_EXIT'
        'PNGDECOMP_GATE_BOOL_CALLER_AFTER'
        'PNGDECOMP_GATE_NO_STATE_COPY_ENTER'
        'PNGDECOMP_GATE_NO_STATE_COPY_AFTER'
        'PNGDECOMP_POST_GATE_FIRST_INSTRUCTION'
        'PNGDECOMP_PREP_ROUTE_ENTER'
        'PNGDECOMP_PREP_ROUTE_EXIT'
        'PNGDECOMP_AFTER_INPUT_GATE_ENTER'
        'PNGDECOMP_AFTER_INPUT_GATE_EXIT'
        'PNGDECOMP_NOOP_ENTER'
        'PNGDECOMP_NOOP_EXIT'
        'PNGDECOMP_PREP_NOOP_ENTER'
        'PNGDECOMP_PREP_NOOP_EXIT'
        'PNGDECOMP_PREP_META_ENTER'
        'PNGDECOMP_PREP_META_DIMS_OK'
        'PNGDECOMP_PREP_META_LEN_OK'
        'PNGDECOMP_PREP_META_FAIL'
        'PNGDECOMP_PREP_META_EXIT'
        'PNGDECOMP_PREP_BYTES_ENTER'
        'PNGDECOMP_PREP_BYTES_NULL'
        'PNGDECOMP_PREP_BYTES_OK'
        'PNGDECOMP_PREP_BYTES_LEN'
        'PNGDECOMP_PREP_BYTES_EXIT'
        'PNGDECOMP_PREP_INLINE_ENTER'
        'PNGDECOMP_PREP_INLINE_BEFORE_OUTPUT_ALLOC'
        'PNGDECOMP_PREP_INLINE_AFTER_OUTPUT_ALLOC'
        'PNGDECOMP_PREP_INLINE_BEFORE_BITREADER_INIT'
        'PNGDECOMP_PREP_INLINE_AFTER_BITREADER_INIT'
        'PNGDECOMP_PREP_INLINE_BEFORE_TABLE_REFS'
        'PNGDECOMP_PREP_INLINE_AFTER_TABLE_REFS'
        'PNGDECOMP_PREP_INLINE_EXIT'
        'PNGDECOMP_PREP_BOUNDARY_ENTER'
        'PNGDECOMP_PREP_BOUNDARY_BEFORE_CALL'
        'PNGDECOMP_PREP_BOUNDARY_AFTER_CALL'
        'PNGDECOMP_PREP_BOUNDARY_OK'
        'PNGDECOMP_PREP_BOUNDARY_FAIL'
        'PNGDECOMP_PREP_BOUNDARY_EXIT'
        'PNGDECOMP_PREP_CONTEXT_ENTER'
        'PNGDECOMP_PREP_CONTEXT_VALIDATE_ENTER'
        'PNGDECOMP_PREP_CONTEXT_VALIDATE_EXIT'
        'PNGDECOMP_PREP_CONTEXT_ALLOC_ENTER'
        'PNGDECOMP_PREP_CONTEXT_ALLOC_EXIT'
        'PNGDECOMP_PREP_CONTEXT_BITREADER_ENTER'
        'PNGDECOMP_PREP_CONTEXT_BITREADER_EXIT'
        'PNGDECOMP_PREP_CONTEXT_TABLES_ENTER'
        'PNGDECOMP_PREP_CONTEXT_TABLES_EXIT'
        'PNGDECOMP_PREP_CONTEXT_OK'
        'PNGDECOMP_PREP_CONTEXT_FAIL'
        'PNGDECOMP_PREP_CONTEXT_EXIT'
        'PNGDECOMP_BITREADER_ENTER'
        'PNGDECOMP_BITREADER_BYTEPOS'
        'PNGDECOMP_BITREADER_BITCOUNT'
        'PNGDECOMP_BITREADER_BAD'
        'PNGDECOMP_BITREADER_OK'
        'PNGDECOMP_BITREADER_EXIT'
        'PNGDECOMP_FIRST_SYMBOL_DECODE_ENTER'
        'PNGDECOMP_FIRST_SYMBOL_VALUE'
        'PNGDECOMP_FIRST_SYMBOL_LITERAL'
        'PNGDECOMP_FIRST_SYMBOL_END'
        'PNGDECOMP_FIRST_SYMBOL_LENGTH'
        'PNGDECOMP_FIRST_SYMBOL_DECODE_EXIT'
        'PNGDECOMP_LITERAL_WRITE_ENTER'
        'PNGDECOMP_LITERAL_WRITE_BOUNDS_BAD'
        'PNGDECOMP_LITERAL_WRITE_BOUNDS_OK'
        'PNGDECOMP_LITERAL_WRITE_EXIT'
        'PNGDECOMP_LEN_DIST_ENTER'
        'PNGDECOMP_LENGTH_SYMBOL'
        'PNGDECOMP_LENGTH_VALUE'
        'PNGDECOMP_DISTANCE_SYMBOL'
        'PNGDECOMP_DISTANCE_VALUE'
        'PNGDECOMP_LEN_DIST_EXIT'
        'PNGDECOMP_ONE_STEP_ENTER'
        'PNGDECOMP_ONE_STEP_LITERAL'
        'PNGDECOMP_ONE_STEP_BACKREF'
        'PNGDECOMP_ONE_STEP_BOUNDS_ABORT'
        'PNGDECOMP_ONE_STEP_OK'
        'PNGDECOMP_ONE_STEP_EXIT'
        'PNGDECOMP_BYTES_NOOP_ENTER'
        'PNGDECOMP_BYTES_NOOP_NULL'
        'PNGDECOMP_BYTES_NOOP_OK'
        'PNGDECOMP_BYTES_NOOP_LEN'
        'PNGDECOMP_BYTES_NOOP_EXIT'
        'PNGDECOMP_ZLIB_HEADER_ENTER'
        'PNGDECOMP_ZLIB_LEN_BAD'
        'PNGDECOMP_ZLIB_LEN_OK'
        'PNGDECOMP_ZLIB_CMF'
        'PNGDECOMP_ZLIB_FLG'
        'PNGDECOMP_ZLIB_METHOD_BAD'
        'PNGDECOMP_ZLIB_METHOD_OK'
        'PNGDECOMP_ZLIB_FCHECK_BAD'
        'PNGDECOMP_ZLIB_FCHECK_OK'
        'PNGDECOMP_ZLIB_FDICT_SET'
        'PNGDECOMP_ZLIB_FDICT_CLEAR'
        'PNGDECOMP_ZLIB_HEADER_EXIT'
        'PNGDECOMP_OUTPUT_ALLOC_ENTER'
        'PNGDECOMP_OUTPUT_ALLOC_SIZE'
        'PNGDECOMP_OUTPUT_ALLOC_NULL'
        'PNGDECOMP_OUTPUT_ALLOC_OK'
        'PNGDECOMP_OUTPUT_ALLOC_EXIT'
        'PNGDECOMP_DEFLATE_ENTER'
        'PNGDECOMP_DEFLATE_FIRST_BLOCK_ENTER'
        'PNGDECOMP_DEFLATE_BFINAL='
        'PNGDECOMP_DEFLATE_BTYPE='
        'PNGDECOMP_DEFLATE_FIRST_BLOCK_EXIT'
        'PNGDECOMP_DEFLATE_EXIT'
        'PNGDECOMP_HUFFMAN_SETUP_ENTER'
        'PNGDECOMP_HUFFMAN_FIXED'
        'PNGDECOMP_HUFFMAN_DYNAMIC'
        'PNGDECOMP_HUFFMAN_SETUP_BAD'
        'PNGDECOMP_HUFFMAN_SETUP_OK'
        'PNGDECOMP_HUFFMAN_SETUP_EXIT'
        'PNGDECOMP_INFLATE_ENTER'
        'PNGDECOMP_INFLATE_FIRST_SYMBOL_ENTER'
        'PNGDECOMP_INFLATE_FIRST_SYMBOL_EXIT'
        'PNGDECOMP_INFLATE_PROGRESS'
        'PNGDECOMP_INFLATE_BOUNDS_ABORT'
        'PNGDECOMP_INFLATE_OK'
        'PNGDECOMP_INFLATE_EXIT'
        'PNGDECOMP_TINY_BOUNDARY_ENTER'
        'PNGDECOMP_TINY_BOUNDARY_AFTER_SETUP'
        'PNGDECOMP_TINY_BOUNDARY_BEFORE_RETURN'
        'PNGDECOMP_TINY_BOUNDARY_EXIT'
        'PNGDECOMP_TINY_BITREADER_ENTER'
        'PNGDECOMP_TINY_BITREADER_BYTEPOS'
        'PNGDECOMP_TINY_BITREADER_BITCOUNT'
        'PNGDECOMP_TINY_BITREADER_EXIT'
        'PNGDECOMP_TINY_SYMBOL_ENTER'
        'PNGDECOMP_TINY_SYMBOL_VALUE'
        'PNGDECOMP_TINY_SYMBOL_LITERAL'
        'PNGDECOMP_TINY_SYMBOL_LENGTH'
        'PNGDECOMP_TINY_SYMBOL_END'
        'PNGDECOMP_TINY_SYMBOL_EXIT'
        'PNGDECOMP_TINY_ONEOP_ENTER'
        'PNGDECOMP_TINY_ONEOP_LITERAL'
        'PNGDECOMP_TINY_ONEOP_LENGTH'
        'PNGDECOMP_TINY_ONEOP_BOUNDS_ABORT'
        'PNGDECOMP_TINY_ONEOP_OK'
        'PNGDECOMP_TINY_ONEOP_EXIT'
        'PNGDECOMP_TINY_INFLATE_ENTER'
        'PNGDECOMP_TINY_INFLATE_PROGRESS'
        'PNGDECOMP_TINY_INFLATE_BOUNDS_ABORT'
        'PNGDECOMP_TINY_INFLATE_OK'
        'PNGDECOMP_TINY_INFLATE_EXIT'
    )
    $pngDecompressDeepestMarker = $null
    foreach ($marker in $pngDecompressMarkerSequence) {
        if ($serialText.Contains($marker)) {
            $pngDecompressDeepestMarker = $marker
        }
    }
    $loadPngSafeMarkerSequence = @(
        'LOADPNGSAFE_ENTER'
        'LOADPNGSAFE_READ_FAIL'
        'LOADPNGSAFE_READ_NULL'
        'LOADPNGSAFE_READ_OK'
        'LOADPNGSAFE_DATA_TOO_SMALL'
        'LOADPNGSAFE_BEFORE_PNGLOADER_LOAD'
        'LOADPNGSAFE_OK'
        'LOADPNGSAFE_NULL'
        'LOADPNGSAFE_EXIT'
    )
    $loadPngSafeDeepestMarker = $null
    foreach ($marker in $loadPngSafeMarkerSequence) {
        if ($serialText.Contains($marker)) {
            $loadPngSafeDeepestMarker = $marker
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
        Write-Host "[probe] Step 8 PNG bypass enabled: $pngProbeEnabled" -ForegroundColor Green
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
        Write-Host "[probe] Enabled PNG variant: $pngLoaderVariantLabel" -ForegroundColor Green
        Write-Host "[probe] UEFI PNG deepest marker: $uefiPngProbeDeepestMarker" -ForegroundColor Green
        Write-Host "[probe] UEFI PNG byte length: $uefiPngProbeLen" -ForegroundColor Green
        Write-Host "[probe] UEFI PNG IHDR width: $uefiPngProbeIhdrWidth" -ForegroundColor Green
        Write-Host "[probe] UEFI PNG IHDR height: $uefiPngProbeIhdrHeight" -ForegroundColor Green
        Write-Host "[probe] UEFI PNG bit depth: $uefiPngProbeIhdrBitDepth" -ForegroundColor Green
        Write-Host "[probe] UEFI PNG color type: $uefiPngProbeIhdrColorType" -ForegroundColor Green
        Write-Host "[probe] UEFI PNG IDAT chunk count: $uefiPngProbeIdatChunkCount" -ForegroundColor Green
        Write-Host "[probe] UEFI PNG compressed IDAT bytes: $uefiPngProbeIdatCompressedBytes" -ForegroundColor Green
        Write-Host "[probe] UEFI PNG zlib header ok: $uefiPngProbeZlibHeaderOkPresent" -ForegroundColor Green
        Write-Host "[probe] UEFI PNG output alloc ok: $uefiPngProbeOutputAllocOkPresent" -ForegroundColor Green
        Write-Host "[probe] UEFI PNG output alloc size: $uefiPngProbeOutputAllocSize" -ForegroundColor Green
        Write-Host "[probe] UEFI PNG BFINAL: $uefiPngProbeBfinal" -ForegroundColor Green
        Write-Host "[probe] UEFI PNG BTYPE: $uefiPngProbeBtype" -ForegroundColor Green
        Write-Host "[probe] UEFI PNG HLIT: $uefiPngProbeHlit" -ForegroundColor Green
        Write-Host "[probe] UEFI PNG HDIST: $uefiPngProbeHdist" -ForegroundColor Green
        Write-Host "[probe] UEFI PNG HCLEN: $uefiPngProbeHclen" -ForegroundColor Green
        Write-Host "[probe] UEFI PNG code-length count read: $uefiPngProbeCodeLenCountRead" -ForegroundColor Green
        Write-Host "[probe] UEFI PNG code-length nonzero count: $uefiPngProbeCodeLenNonzeroCount" -ForegroundColor Green
        Write-Host "[probe] UEFI PNG code-length zero count: $uefiPngProbeCodeLenZeroCount" -ForegroundColor Green
        Write-Host "[probe] UEFI PNG code-length max value: $uefiPngProbeCodeLenMaxValue" -ForegroundColor Green
        Write-Host "[probe] UEFI PNG code-length max bits: $uefiPngProbeCodeLenMaxBits" -ForegroundColor Green
        Write-Host "[probe] UEFI PNG code-length len1 count: $uefiPngProbeCodeLenLen1Count" -ForegroundColor Green
        Write-Host "[probe] UEFI PNG code-length len2 count: $uefiPngProbeCodeLenLen2Count" -ForegroundColor Green
        Write-Host "[probe] UEFI PNG code-length len3 count: $uefiPngProbeCodeLenLen3Count" -ForegroundColor Green
        Write-Host "[probe] UEFI PNG code-length len4 count: $uefiPngProbeCodeLenLen4Count" -ForegroundColor Green
        Write-Host "[probe] UEFI PNG code-length len5 count: $uefiPngProbeCodeLenLen5Count" -ForegroundColor Green
        Write-Host "[probe] UEFI PNG code-length len6 count: $uefiPngProbeCodeLenLen6Count" -ForegroundColor Green
        Write-Host "[probe] UEFI PNG code-length len7 count: $uefiPngProbeCodeLenLen7Count" -ForegroundColor Green
        Write-Host "[probe] UEFI PNG code-length values ok: $uefiPngProbeCodeLenValuesOkPresent" -ForegroundColor Green
        Write-Host "[probe] UEFI PNG code-length values bad: $uefiPngProbeCodeLenValuesBadPresent" -ForegroundColor Green
        Write-Host "[probe] UEFI PNG code-length histogram ok: $uefiPngProbeCodeLenHistogramOkPresent" -ForegroundColor Green
        Write-Host "[probe] UEFI PNG code-length histogram bad: $uefiPngProbeCodeLenHistogramBadPresent" -ForegroundColor Green
        Write-Host "[probe] UEFI PNG code-length next-code ok: $uefiPngProbeCodeLenNextCodeOkPresent" -ForegroundColor Green
        Write-Host "[probe] UEFI PNG code-length next-code bad: $uefiPngProbeCodeLenNextCodeBadPresent" -ForegroundColor Green
        Write-Host "[probe] UEFI PNG code-length code space end: $uefiPngProbeCodeLenCodeSpaceEnd" -ForegroundColor Green
        Write-Host "[probe] UEFI PNG code-length code space remaining: $uefiPngProbeCodeLenCodeSpaceRemaining" -ForegroundColor Green
        Write-Host "[probe] UEFI PNG code-length table entry count: $uefiPngProbeCodeLenTableEntryCount" -ForegroundColor Green
        Write-Host "[probe] UEFI PNG code-length table build ok: $uefiPngProbeCodeLenTableBuildOkPresent" -ForegroundColor Green
        Write-Host "[probe] UEFI PNG code-length table build bad: $uefiPngProbeCodeLenTableBuildBadPresent" -ForegroundColor Green
        Write-Host "[probe] UEFI PNG code-length lookup smoke symbol: $uefiPngProbeCodeLenLookupSmokeSymbol" -ForegroundColor Green
        Write-Host "[probe] UEFI PNG code-length lookup smoke bits: $uefiPngProbeCodeLenLookupSmokeBits" -ForegroundColor Green
        Write-Host "[probe] UEFI PNG code-length lookup smoke code: $uefiPngProbeCodeLenLookupSmokeCode" -ForegroundColor Green
        Write-Host "[probe] UEFI PNG code-length lookup smoke ok: $uefiPngProbeCodeLenLookupSmokeOkPresent" -ForegroundColor Green
        Write-Host "[probe] UEFI PNG code-length lookup smoke bad: $uefiPngProbeCodeLenLookupSmokeBadPresent" -ForegroundColor Green
        Write-Host "[probe] UEFI PNG inflate ok: $uefiPngProbeInflateOkPresent" -ForegroundColor Green
        Write-Host "[probe] UEFI PNG inflate bounds abort: $uefiPngProbeInflateBoundsAbortPresent" -ForegroundColor Green
        Write-Host "[probe] UEFI PNG inflate fault: $uefiPngProbeInflateFaultPresent" -ForegroundColor Green
        Write-Host "[probe] PNGDECOMP_GATE_INLINE_ENTER present: $pngDecompressGateInlineEnterPresent" -ForegroundColor Green
        Write-Host "[probe] PNGDECOMP_GATE_INLINE_OK present: $pngDecompressGateInlineOkPresent" -ForegroundColor Green
        Write-Host "[probe] PNGDECOMP_GATE_INLINE_BEFORE_RETURN present: $pngDecompressGateInlineBeforeReturnPresent" -ForegroundColor Green
        Write-Host "[probe] PNGDECOMP_GATE_HELPER_ENTER present: $pngDecompressGateHelperEnterPresent" -ForegroundColor Green
        Write-Host "[probe] PNGDECOMP_GATE_HELPER_OK present: $pngDecompressGateHelperOkPresent" -ForegroundColor Green
        Write-Host "[probe] PNGDECOMP_GATE_HELPER_RETURNING present: $pngDecompressGateHelperReturningPresent" -ForegroundColor Green
        Write-Host "[probe] PNGDECOMP_GATE_BOOL_ENTER present: $pngDecompressGateBoolEnterPresent" -ForegroundColor Green
        Write-Host "[probe] PNGDECOMP_GATE_BOOL_OK present: $pngDecompressGateBoolOkPresent" -ForegroundColor Green
        Write-Host "[probe] PNGDECOMP_GATE_BOOL_EXIT present: $pngDecompressGateBoolExitPresent" -ForegroundColor Green
        Write-Host "[probe] PNGDECOMP_GATE_BOOL_CALLER_AFTER present: $pngDecompressGateBoolCallerAfterPresent" -ForegroundColor Green
        Write-Host "[probe] PNGDECOMP_GATE_NO_STATE_COPY_ENTER present: $pngDecompressGateNoStateCopyEnterPresent" -ForegroundColor Green
        Write-Host "[probe] PNGDECOMP_GATE_NO_STATE_COPY_AFTER present: $pngDecompressGateNoStateCopyAfterPresent" -ForegroundColor Green
        Write-Host "[probe] PNGDECOMP_POST_GATE_FIRST_INSTRUCTION present: $pngDecompressPostGateFirstInstructionPresent" -ForegroundColor Green
        Write-Host "[probe] PNGDECOMP_AFTER_INPUT_GATE_ENTER present: $pngDecompressAfterInputGateEnterPresent" -ForegroundColor Green
        Write-Host "[probe] PNGDECOMP_AFTER_INPUT_GATE_EXIT present: $pngDecompressAfterInputGateExitPresent" -ForegroundColor Green
        Write-Host "[probe] PNGDECOMP_PREP_ROUTE_ENTER present: $pngDecompressPrepRouteEnterPresent" -ForegroundColor Green
        Write-Host "[probe] PNGDECOMP_PREP_ROUTE_EXIT present: $pngDecompressPrepRouteExitPresent" -ForegroundColor Green
        Write-Host "[probe] PNGDECOMP_PREP_NOOP_ENTER present: $pngDecompressPrepNoopEnterPresent" -ForegroundColor Green
        Write-Host "[probe] PNGDECOMP_PREP_NOOP_EXIT present: $pngDecompressPrepNoopExitPresent" -ForegroundColor Green
        Write-Host "[probe] PNGDECOMP_PREP_META_ENTER present: $pngDecompressPrepMetaEnterPresent" -ForegroundColor Green
        Write-Host "[probe] PNGDECOMP_PREP_META_DIMS_OK present: $pngDecompressPrepMetaDimsOkPresent" -ForegroundColor Green
        Write-Host "[probe] PNGDECOMP_PREP_META_LEN_OK present: $pngDecompressPrepMetaLenOkPresent" -ForegroundColor Green
        Write-Host "[probe] PNGDECOMP_PREP_META_EXIT present: $pngDecompressPrepMetaExitPresent" -ForegroundColor Green
        Write-Host "[probe] PNGDECOMP_PREP_BYTES_ENTER present: $pngDecompressPrepBytesEnterPresent" -ForegroundColor Green
        Write-Host "[probe] PNGDECOMP_PREP_BYTES_NULL present: $pngDecompressPrepBytesNullPresent" -ForegroundColor Green
        Write-Host "[probe] PNGDECOMP_PREP_BYTES_OK present: $pngDecompressPrepBytesOkPresent" -ForegroundColor Green
        Write-Host "[probe] PNGDECOMP_PREP_BYTES_LEN: $pngDecompressPrepBytesLen" -ForegroundColor Green
        Write-Host "[probe] PNGDECOMP_PREP_BYTES_EXIT present: $pngDecompressPrepBytesExitPresent" -ForegroundColor Green
        Write-Host "[probe] PNGDECOMP_PREP_INLINE_ENTER present: $pngDecompressPrepInlineEnterPresent" -ForegroundColor Green
        Write-Host "[probe] PNGDECOMP_PREP_INLINE_BEFORE_OUTPUT_ALLOC present: $pngDecompressPrepInlineBeforeOutputAllocPresent" -ForegroundColor Green
        Write-Host "[probe] PNGDECOMP_PREP_INLINE_AFTER_OUTPUT_ALLOC present: $pngDecompressPrepInlineAfterOutputAllocPresent" -ForegroundColor Green
        Write-Host "[probe] PNGDECOMP_PREP_INLINE_BEFORE_BITREADER_INIT present: $pngDecompressPrepInlineBeforeBitreaderInitPresent" -ForegroundColor Green
        Write-Host "[probe] PNGDECOMP_PREP_INLINE_AFTER_BITREADER_INIT present: $pngDecompressPrepInlineAfterBitreaderInitPresent" -ForegroundColor Green
        Write-Host "[probe] PNGDECOMP_PREP_INLINE_BEFORE_TABLE_REFS present: $pngDecompressPrepInlineBeforeTableRefsPresent" -ForegroundColor Green
        Write-Host "[probe] PNGDECOMP_PREP_INLINE_AFTER_TABLE_REFS present: $pngDecompressPrepInlineAfterTableRefsPresent" -ForegroundColor Green
        Write-Host "[probe] PNGDECOMP_PREP_INLINE_EXIT present: $pngDecompressPrepInlineExitPresent" -ForegroundColor Green
        Write-Host "[probe] PNGDECOMP_PREP_BOUNDARY_ENTER present: $pngDecompressPrepBoundaryEnterPresent" -ForegroundColor Green
        Write-Host "[probe] PNGDECOMP_PREP_BOUNDARY_BEFORE_CALL present: $pngDecompressPrepBoundaryBeforeCallPresent" -ForegroundColor Green
        Write-Host "[probe] PNGDECOMP_PREP_BOUNDARY_AFTER_CALL present: $pngDecompressPrepBoundaryAfterCallPresent" -ForegroundColor Green
        Write-Host "[probe] PNGDECOMP_PREP_BOUNDARY_OK present: $pngDecompressPrepBoundaryOkPresent" -ForegroundColor Green
        Write-Host "[probe] PNGDECOMP_PREP_BOUNDARY_FAIL present: $pngDecompressPrepBoundaryFailPresent" -ForegroundColor Green
        Write-Host "[probe] PNGDECOMP_PREP_BOUNDARY_EXIT present: $pngDecompressPrepBoundaryExitPresent" -ForegroundColor Green
        Write-Host "[probe] PNGDECOMP_PREP_CONTEXT_ENTER present: $pngDecompressPrepContextEnterPresent" -ForegroundColor Green
        Write-Host "[probe] PNGDECOMP_PREP_CONTEXT_VALIDATE_ENTER present: $pngDecompressPrepContextValidateEnterPresent" -ForegroundColor Green
        Write-Host "[probe] PNGDECOMP_PREP_CONTEXT_VALIDATE_EXIT present: $pngDecompressPrepContextValidateExitPresent" -ForegroundColor Green
        Write-Host "[probe] PNGDECOMP_PREP_CONTEXT_ALLOC_ENTER present: $pngDecompressPrepContextAllocEnterPresent" -ForegroundColor Green
        Write-Host "[probe] PNGDECOMP_PREP_CONTEXT_ALLOC_EXIT present: $pngDecompressPrepContextAllocExitPresent" -ForegroundColor Green
        Write-Host "[probe] PNGDECOMP_PREP_CONTEXT_BITREADER_ENTER present: $pngDecompressPrepContextBitreaderEnterPresent" -ForegroundColor Green
        Write-Host "[probe] PNGDECOMP_PREP_CONTEXT_BITREADER_EXIT present: $pngDecompressPrepContextBitreaderExitPresent" -ForegroundColor Green
        Write-Host "[probe] PNGDECOMP_PREP_CONTEXT_TABLES_ENTER present: $pngDecompressPrepContextTablesEnterPresent" -ForegroundColor Green
        Write-Host "[probe] PNGDECOMP_PREP_CONTEXT_TABLES_EXIT present: $pngDecompressPrepContextTablesExitPresent" -ForegroundColor Green
        Write-Host "[probe] PNGDECOMP_PREP_CONTEXT_OK present: $pngDecompressPrepContextOkPresent" -ForegroundColor Green
        Write-Host "[probe] PNGDECOMP_PREP_CONTEXT_FAIL present: $pngDecompressPrepContextFailPresent" -ForegroundColor Green
        Write-Host "[probe] PNGDECOMP_PREP_CONTEXT_EXIT present: $pngDecompressPrepContextExitPresent" -ForegroundColor Green
        Write-Host "[probe] PNGDECOMP_INPUT_GATE_ENTER present: $pngDecompressInputGateEnterPresent" -ForegroundColor Green
        Write-Host "[probe] PNGDECOMP_INPUT_GATE_OK present: $pngDecompressInputGateOkPresent" -ForegroundColor Green
        Write-Host "[probe] PNGDECOMP_INPUT_GATE_BAD_LEN present: $pngDecompressInputGateBadLenPresent" -ForegroundColor Green
        Write-Host "[probe] PNGDECOMP_INPUT_GATE_BAD_DIMS present: $pngDecompressInputGateBadDimsPresent" -ForegroundColor Green
        Write-Host "[probe] PNGDECOMP_INPUT_GATE_BAD_FORMAT present: $pngDecompressInputGateBadFormatPresent" -ForegroundColor Green
        Write-Host "[probe] PNGDECOMP_INPUT_GATE_BAD_IDAT present: $pngDecompressInputGateBadIdatPresent" -ForegroundColor Green
        Write-Host "[probe] PNGDECOMP_INPUT_GATE_EXIT present: $pngDecompressInputGateExitPresent" -ForegroundColor Green
        Write-Host "[probe] PNGDECOMP_FIRST_SYMBOL_BOUNDARY_ENTER present: $pngDecompressFirstSymbolBoundaryEnterPresent" -ForegroundColor Green
        Write-Host "[probe] PNGDECOMP_FIRST_SYMBOL_BOUNDARY_EXIT present: $pngDecompressFirstSymbolBoundaryExitPresent" -ForegroundColor Green
        Write-Host "[probe] PNGDECOMP_BITREADER_ENTER present: $pngDecompressBitReaderEnterPresent" -ForegroundColor Green
        Write-Host "[probe] PNGDECOMP_BITREADER_BYTEPOS: $pngDecompressBitReaderBytePos" -ForegroundColor Green
        Write-Host "[probe] PNGDECOMP_BITREADER_BITCOUNT: $pngDecompressBitReaderBitCount" -ForegroundColor Green
        Write-Host "[probe] PNGDECOMP_BITREADER_OK present: $pngDecompressBitReaderOkPresent" -ForegroundColor Green
        Write-Host "[probe] PNGDECOMP_BITREADER_BAD present: $pngDecompressBitReaderBadPresent" -ForegroundColor Green
        Write-Host "[probe] PNGDECOMP_BITREADER_EXIT present: $pngDecompressBitReaderExitPresent" -ForegroundColor Green
        Write-Host "[probe] PNGDECOMP_FIRST_SYMBOL_DECODE_ENTER present: $pngDecompressFirstSymbolDecodeEnterPresent" -ForegroundColor Green
        Write-Host "[probe] PNGDECOMP_FIRST_SYMBOL_VALUE: $pngDecompressFirstSymbolValue" -ForegroundColor Green
        Write-Host "[probe] PNGDECOMP_FIRST_SYMBOL_LITERAL present: $pngDecompressFirstSymbolLiteralPresent" -ForegroundColor Green
        Write-Host "[probe] PNGDECOMP_FIRST_SYMBOL_END present: $pngDecompressFirstSymbolEndPresent" -ForegroundColor Green
        Write-Host "[probe] PNGDECOMP_FIRST_SYMBOL_LENGTH present: $pngDecompressFirstSymbolLengthPresent" -ForegroundColor Green
        Write-Host "[probe] PNGDECOMP_FIRST_SYMBOL_DECODE_EXIT present: $pngDecompressFirstSymbolDecodeExitPresent" -ForegroundColor Green
        Write-Host "[probe] PNGDECOMP_LITERAL_WRITE_ENTER present: $pngDecompressLiteralWriteEnterPresent" -ForegroundColor Green
        Write-Host "[probe] PNGDECOMP_LITERAL_WRITE_BOUNDS_OK present: $pngDecompressLiteralWriteBoundsOkPresent" -ForegroundColor Green
        Write-Host "[probe] PNGDECOMP_LITERAL_WRITE_BOUNDS_BAD present: $pngDecompressLiteralWriteBoundsBadPresent" -ForegroundColor Green
        Write-Host "[probe] PNGDECOMP_LITERAL_WRITE_EXIT present: $pngDecompressLiteralWriteExitPresent" -ForegroundColor Green
        Write-Host "[probe] PNGDECOMP_LEN_DIST_ENTER present: $pngDecompressLenDistEnterPresent" -ForegroundColor Green
        Write-Host "[probe] PNGDECOMP_LENGTH_SYMBOL: $pngDecompressLengthSymbol" -ForegroundColor Green
        Write-Host "[probe] PNGDECOMP_LENGTH_VALUE: $pngDecompressLengthValue" -ForegroundColor Green
        Write-Host "[probe] PNGDECOMP_DISTANCE_SYMBOL: $pngDecompressDistanceSymbol" -ForegroundColor Green
        Write-Host "[probe] PNGDECOMP_DISTANCE_VALUE: $pngDecompressDistanceValue" -ForegroundColor Green
        Write-Host "[probe] PNGDECOMP_LEN_DIST_EXIT present: $pngDecompressLenDistExitPresent" -ForegroundColor Green
        Write-Host "[probe] PNGDECOMP_ONE_STEP_ENTER present: $pngDecompressOneStepEnterPresent" -ForegroundColor Green
        Write-Host "[probe] PNGDECOMP_ONE_STEP_LITERAL present: $pngDecompressOneStepLiteralPresent" -ForegroundColor Green
        Write-Host "[probe] PNGDECOMP_ONE_STEP_BACKREF present: $pngDecompressOneStepBackrefPresent" -ForegroundColor Green
        Write-Host "[probe] PNGDECOMP_ONE_STEP_BOUNDS_ABORT present: $pngDecompressOneStepBoundsAbortPresent" -ForegroundColor Green
        Write-Host "[probe] PNGDECOMP_ONE_STEP_OK present: $pngDecompressOneStepOkPresent" -ForegroundColor Green
        Write-Host "[probe] PNGDECOMP_ONE_STEP_EXIT present: $pngDecompressOneStepExitPresent" -ForegroundColor Green
        Write-Host "[probe] CURSOR_PNG_PROBE_ENTER present: $cursorPngProbeEnterPresent" -ForegroundColor Green
        Write-Host "[probe] CURSOR_PNG_BEFORE_DISK_CHECK present: $cursorPngBeforeDiskCheckPresent" -ForegroundColor Green
        Write-Host "[probe] CURSOR_PNG_AFTER_DISK_CHECK present: $cursorPngAfterDiskCheckPresent" -ForegroundColor Green
        Write-Host "[probe] CURSOR_PNG_BEFORE_FILE_LOOKUP present: $cursorPngBeforeFileLookupPresent" -ForegroundColor Green
        Write-Host "[probe] CURSOR_PNG_AFTER_FILE_LOOKUP present: $cursorPngAfterFileLookupPresent" -ForegroundColor Green
        Write-Host "[probe] CURSOR_PNG_BYTES_LENGTH: $cursorPngBytesLength" -ForegroundColor Green
        Write-Host "[probe] CURSOR_PNG_HEADER_HELPER_ENTER present: $cursorPngHeaderHelperEnterPresent" -ForegroundColor Green
        Write-Host "[probe] CURSOR_PNG_HEADER_HELPER_OK present: $cursorPngHeaderHelperOkPresent" -ForegroundColor Green
        Write-Host "[probe] CURSOR_PNG_HEADER_HELPER_BAD present: $cursorPngHeaderHelperBadPresent" -ForegroundColor Green
        Write-Host "[probe] CURSOR_PNG_HEADER_HELPER_EXIT present: $cursorPngHeaderHelperExitPresent" -ForegroundColor Green
        Write-Host "[probe] CURSOR_PNG_NOOP_LOADER_ENTER present: $cursorPngNoopLoaderEnterPresent" -ForegroundColor Green
        Write-Host "[probe] CURSOR_PNG_NOOP_LOADER_EXIT present: $cursorPngNoopLoaderExitPresent" -ForegroundColor Green
        Write-Host "[probe] CURSOR_PNG_BYTES_NOOP_ENTER present: $cursorPngBytesNoopEnterPresent" -ForegroundColor Green
        Write-Host "[probe] CURSOR_PNG_BYTES_NOOP_NULL present: $cursorPngBytesNoopNullPresent" -ForegroundColor Green
        Write-Host "[probe] CURSOR_PNG_BYTES_NOOP_OK present: $cursorPngBytesNoopOkPresent" -ForegroundColor Green
        Write-Host "[probe] CURSOR_PNG_BYTES_NOOP_EXIT present: $cursorPngBytesNoopExitPresent" -ForegroundColor Green
        Write-Host "[probe] CURSOR_PNG_IHDR_HELPER_ENTER present: $cursorPngIhdrHelperEnterPresent" -ForegroundColor Green
        Write-Host "[probe] CURSOR_PNG_IHDR_HELPER_OK present: $cursorPngIhdrHelperOkPresent" -ForegroundColor Green
        Write-Host "[probe] CURSOR_PNG_IHDR_HELPER_BAD present: $cursorPngIhdrHelperBadPresent" -ForegroundColor Green
        Write-Host "[probe] CURSOR_PNG_IHDR_WIDTH: $cursorPngIhdrWidth" -ForegroundColor Green
        Write-Host "[probe] CURSOR_PNG_IHDR_HEIGHT: $cursorPngIhdrHeight" -ForegroundColor Green
        Write-Host "[probe] CURSOR_PNG_IHDR_HELPER_EXIT present: $cursorPngIhdrHelperExitPresent" -ForegroundColor Green
        Write-Host "[probe] CURSOR_PNG_LOAD_WRAPPER_ENTER present: $cursorPngLoadWrapperEnterPresent" -ForegroundColor Green
        Write-Host "[probe] CURSOR_PNG_LOAD_WRAPPER_BEFORE_CALL present: $cursorPngLoadWrapperBeforeCallPresent" -ForegroundColor Green
        Write-Host "[probe] CURSOR_PNG_LOAD_WRAPPER_AFTER_CALL present: $cursorPngLoadWrapperAfterCallPresent" -ForegroundColor Green
        Write-Host "[probe] CURSOR_PNG_LOAD_WRAPPER_EXIT present: $cursorPngLoadWrapperExitPresent" -ForegroundColor Green
        Write-Host "[probe] CURSOR_PNG_NULL present: $cursorPngNullPresent" -ForegroundColor Green
        Write-Host "[probe] CURSOR_PNG_OK present: $cursorPngOkPresent" -ForegroundColor Green
        Write-Host "[probe] CURSOR_PNG_DIMS_ENTER present: $cursorPngDimsEnterPresent" -ForegroundColor Green
        Write-Host "[probe] CURSOR_PNG_DIMS_EXIT present: $cursorPngDimsExitPresent" -ForegroundColor Green
        Write-Host "[probe] CURSOR_PNG_PROBE_EXIT present: $cursorPngProbeExitPresent" -ForegroundColor Green
        Write-Host "[probe] Deepest PNG marker reached: $cursorPngDeepestMarker" -ForegroundColor Green
        Write-Host "[probe] Deepest PNGDECOMP marker reached: $pngDecompressDeepestMarker" -ForegroundColor Green
        if ($CursorPngLoadAfterIhdrProbe) {
            Write-Host "[probe] PNGLOADER_ENTRY_BASELINE_ENTER present: $pngLoaderEnterPresent" -ForegroundColor Green
            Write-Host "[probe] PNGLOADER_ENTRY_BASELINE_BYTES_OK present: $pngLoaderBytesOkPresent" -ForegroundColor Green
            Write-Host "[probe] PNGLOADER_ENTRY_BASELINE_LEN: $pngLoaderBytesLength" -ForegroundColor Green
            Write-Host "[probe] PNGLOADER_ENTRY_BASELINE_HEADER_OK present: $pngLoaderHeaderOkPresent" -ForegroundColor Green
            Write-Host "[probe] PNGLOADER_ENTRY_BASELINE_IHDR_OK present: $pngLoaderIhdrOkPresent" -ForegroundColor Green
            Write-Host "[probe] PNGLOADER_ENTRY_BASELINE_IHDR_WIDTH: $pngLoaderIhdrWidth" -ForegroundColor Green
            Write-Host "[probe] PNGLOADER_ENTRY_BASELINE_IHDR_HEIGHT: $pngLoaderIhdrHeight" -ForegroundColor Green
            Write-Host "[probe] PNGLOADER_ENTRY_BASELINE_EXIT present: $pngLoaderExitPresent" -ForegroundColor Green
        }
        Write-Host "[probe] PNGLOADER_ENTER present: $pngLoaderEnterPresent" -ForegroundColor Green
        Write-Host "[probe] PNGLOADER_BYTES_NULL present: $pngLoaderBytesNullPresent" -ForegroundColor Green
        Write-Host "[probe] PNGLOADER_BYTES_OK present: $pngLoaderBytesOkPresent" -ForegroundColor Green
        Write-Host "[probe] PNGLOADER_HEADER_ENTER present: $pngLoaderHeaderEnterPresent" -ForegroundColor Green
        Write-Host "[probe] PNGLOADER_HEADER_OK present: $pngLoaderHeaderOkPresent" -ForegroundColor Green
        Write-Host "[probe] PNGLOADER_HEADER_BAD present: $pngLoaderHeaderBadPresent" -ForegroundColor Green
        Write-Host "[probe] PNGLOADER_IHDR_ENTER present: $pngLoaderIhdrEnterPresent" -ForegroundColor Green
        Write-Host "[probe] PNGLOADER_IHDR_OK present: $pngLoaderIhdrOkPresent" -ForegroundColor Green
        Write-Host "[probe] PNGLOADER_IHDR_BAD present: $pngLoaderIhdrBadPresent" -ForegroundColor Green
        Write-Host "[probe] PNGLOADER_IHDR_DIMS present: $pngLoaderIhdrDimsPresent" -ForegroundColor Green
        Write-Host "[probe] PNGLOADER_IHDR_WIDTH: $pngLoaderIhdrWidth" -ForegroundColor Green
        Write-Host "[probe] PNGLOADER_IHDR_HEIGHT: $pngLoaderIhdrHeight" -ForegroundColor Green
        Write-Host "[probe] PNGLOADER_IHDR_BIT_DEPTH: $pngLoaderIhdrBitDepth" -ForegroundColor Green
        Write-Host "[probe] PNGLOADER_IHDR_COLOR_TYPE: $pngLoaderIhdrColorType" -ForegroundColor Green
        Write-Host "[probe] PNGLOADER_CHUNK_SCAN_ENTER present: $pngLoaderChunkScanEnterPresent" -ForegroundColor Green
        Write-Host "[probe] PNGLOADER_CHUNK_TYPE_IHDR present: $pngLoaderChunkTypeIhdrPresent" -ForegroundColor Green
        Write-Host "[probe] PNGLOADER_CHUNK_TYPE_IDAT present: $pngLoaderChunkTypeIdatPresent" -ForegroundColor Green
        Write-Host "[probe] PNGLOADER_CHUNK_TYPE_IEND present: $pngLoaderChunkTypeIendPresent" -ForegroundColor Green
        Write-Host "[probe] PNGLOADER_CHUNK_SCAN_EXIT present: $pngLoaderChunkScanExitPresent" -ForegroundColor Green
        Write-Host "[probe] PNGLOADER_IDAT_CHUNK_COUNT: $pngLoaderIdatChunkCount" -ForegroundColor Green
        Write-Host "[probe] PNGLOADER_IDAT_COMPRESSED_BYTES: $pngLoaderIdatCompressedBytes" -ForegroundColor Green
        Write-Host "[probe] PNGLOADER_IDAT_BYTES_ENTER present: $pngLoaderIdatBytesEnterPresent" -ForegroundColor Green
        Write-Host "[probe] PNGLOADER_IDAT_BYTES_OK present: $pngLoaderIdatBytesOkPresent" -ForegroundColor Green
        Write-Host "[probe] PNGLOADER_IDAT_BYTES_EMPTY present: $pngLoaderIdatBytesEmptyPresent" -ForegroundColor Green
        Write-Host "[probe] PNGLOADER_DECOMPRESS_PREMETA_ENTER present: $pngLoaderDecompressPreMetaEnterPresent" -ForegroundColor Green
        Write-Host "[probe] PNGLOADER_DECOMPRESS_PREMETA_LEN: $pngLoaderDecompressPreMetaLen" -ForegroundColor Green
        Write-Host "[probe] PNGLOADER_DECOMPRESS_PREMETA_EXPECTED_OUT_LEN: $pngLoaderDecompressPreMetaExpectedOutLen" -ForegroundColor Green
        Write-Host "[probe] PNGLOADER_DECOMPRESS_PREMETA_EXIT present: $pngLoaderDecompressPreMetaExitPresent" -ForegroundColor Green
        Write-Host "[probe] PNGDECOMP_NOOP_ENTER present: $pngDecompressNoopEnterPresent" -ForegroundColor Green
        Write-Host "[probe] PNGDECOMP_NOOP_EXIT present: $pngDecompressNoopExitPresent" -ForegroundColor Green
        Write-Host "[probe] PNGDECOMP_BYTES_NOOP_ENTER present: $pngDecompressBytesNoopEnterPresent" -ForegroundColor Green
        Write-Host "[probe] PNGDECOMP_BYTES_NOOP_NULL present: $pngDecompressBytesNoopNullPresent" -ForegroundColor Green
        Write-Host "[probe] PNGDECOMP_BYTES_NOOP_OK present: $pngDecompressBytesNoopOkPresent" -ForegroundColor Green
        Write-Host "[probe] PNGDECOMP_BYTES_NOOP_LEN: $pngDecompressBytesNoopLen" -ForegroundColor Green
        Write-Host "[probe] PNGDECOMP_BYTES_NOOP_EXIT present: $pngDecompressBytesNoopExitPresent" -ForegroundColor Green
        Write-Host "[probe] PNGDECOMP_ZLIB_HEADER_ENTER present: $pngDecompressZlibHeaderEnterPresent" -ForegroundColor Green
        Write-Host "[probe] PNGDECOMP_ZLIB_LEN_OK present: $pngDecompressZlibLenOkPresent" -ForegroundColor Green
        Write-Host "[probe] PNGDECOMP_ZLIB_LEN_BAD present: $pngDecompressZlibLenBadPresent" -ForegroundColor Green
        Write-Host "[probe] PNGDECOMP_ZLIB_CMF: $pngDecompressZlibCmf" -ForegroundColor Green
        Write-Host "[probe] PNGDECOMP_ZLIB_FLG: $pngDecompressZlibFlg" -ForegroundColor Green
        Write-Host "[probe] PNGDECOMP_ZLIB_METHOD_OK present: $pngDecompressZlibMethodOkPresent" -ForegroundColor Green
        Write-Host "[probe] PNGDECOMP_ZLIB_METHOD_BAD present: $pngDecompressZlibMethodBadPresent" -ForegroundColor Green
        Write-Host "[probe] PNGDECOMP_ZLIB_FCHECK_OK present: $pngDecompressZlibFcheckOkPresent" -ForegroundColor Green
        Write-Host "[probe] PNGDECOMP_ZLIB_FCHECK_BAD present: $pngDecompressZlibFcheckBadPresent" -ForegroundColor Green
        Write-Host "[probe] PNGDECOMP_ZLIB_FDICT_SET present: $pngDecompressZlibFdictSetPresent" -ForegroundColor Green
        Write-Host "[probe] PNGDECOMP_ZLIB_FDICT_CLEAR present: $pngDecompressZlibFdictClearPresent" -ForegroundColor Green
        Write-Host "[probe] PNGDECOMP_ZLIB_HEADER_EXIT present: $pngDecompressZlibHeaderExitPresent" -ForegroundColor Green
        Write-Host "[probe] PNGDECOMP_OUTPUT_ALLOC_ENTER present: $pngDecompressOutputAllocEnterPresent" -ForegroundColor Green
        Write-Host "[probe] PNGDECOMP_OUTPUT_ALLOC_SIZE: $pngDecompressOutputAllocSize" -ForegroundColor Green
        Write-Host "[probe] PNGDECOMP_OUTPUT_ALLOC_OK present: $pngDecompressOutputAllocOkPresent" -ForegroundColor Green
        Write-Host "[probe] PNGDECOMP_OUTPUT_ALLOC_NULL present: $pngDecompressOutputAllocNullPresent" -ForegroundColor Green
        Write-Host "[probe] PNGDECOMP_OUTPUT_ALLOC_EXIT present: $pngDecompressOutputAllocExitPresent" -ForegroundColor Green
        Write-Host "[probe] PNGDECOMP_DEFLATE_ENTER present: $pngDecompressDeflateEnterPresent" -ForegroundColor Green
        Write-Host "[probe] PNGDECOMP_DEFLATE_FIRST_BLOCK_ENTER present: $pngDecompressDeflateFirstBlockEnterPresent" -ForegroundColor Green
        Write-Host "[probe] PNGDECOMP_DEFLATE_BFINAL: $pngDecompressDeflateBfinal" -ForegroundColor Green
        Write-Host "[probe] PNGDECOMP_DEFLATE_BTYPE: $pngDecompressDeflateBtype" -ForegroundColor Green
        Write-Host "[probe] PNGDECOMP_DEFLATE_FIRST_BLOCK_EXIT present: $pngDecompressDeflateFirstBlockExitPresent" -ForegroundColor Green
        Write-Host "[probe] PNGDECOMP_DEFLATE_EXIT present: $pngDecompressDeflateExitPresent" -ForegroundColor Green
        Write-Host "[probe] PNGDECOMP_HUFFMAN_SETUP_ENTER present: $pngDecompressHuffmanSetupEnterPresent" -ForegroundColor Green
        Write-Host "[probe] PNGDECOMP_HUFFMAN_FIXED present: $pngDecompressHuffmanFixedPresent" -ForegroundColor Green
        Write-Host "[probe] PNGDECOMP_HUFFMAN_DYNAMIC present: $pngDecompressHuffmanDynamicPresent" -ForegroundColor Green
        Write-Host "[probe] PNGDECOMP_HUFFMAN_SETUP_OK present: $pngDecompressHuffmanSetupOkPresent" -ForegroundColor Green
        Write-Host "[probe] PNGDECOMP_HUFFMAN_SETUP_BAD present: $pngDecompressHuffmanSetupBadPresent" -ForegroundColor Green
        Write-Host "[probe] PNGDECOMP_HUFFMAN_SETUP_EXIT present: $pngDecompressHuffmanSetupExitPresent" -ForegroundColor Green
        Write-Host "[probe] PNGDECOMP_INFLATE_ENTER present: $pngDecompressInflateEnterPresent" -ForegroundColor Green
        Write-Host "[probe] PNGDECOMP_INFLATE_FIRST_SYMBOL_ENTER present: $pngDecompressInflateFirstSymbolEnterPresent" -ForegroundColor Green
        Write-Host "[probe] PNGDECOMP_INFLATE_FIRST_SYMBOL_EXIT present: $pngDecompressInflateFirstSymbolExitPresent" -ForegroundColor Green
        Write-Host "[probe] PNGDECOMP_INFLATE_PROGRESS present: $pngDecompressInflateProgressPresent" -ForegroundColor Green
        Write-Host "[probe] PNGDECOMP_INFLATE_BOUNDS_ABORT present: $pngDecompressInflateBoundsAbortPresent" -ForegroundColor Green
        Write-Host "[probe] PNGDECOMP_INFLATE_OK present: $pngDecompressInflateOkPresent" -ForegroundColor Green
        Write-Host "[probe] PNGDECOMP_INFLATE_EXIT present: $pngDecompressInflateExitPresent" -ForegroundColor Green
        Write-Host "[probe] PNGLOADER_DECOMPRESS_ENTER present: $pngLoaderDecompressEnterPresent" -ForegroundColor Green
        Write-Host "[probe] PNGLOADER_DECOMPRESS_OK present: $pngLoaderDecompressOkPresent" -ForegroundColor Green
        Write-Host "[probe] PNGLOADER_DECOMPRESS_NULL present: $pngLoaderDecompressNullPresent" -ForegroundColor Green
        Write-Host "[probe] PNGLOADER_DECOMPRESS_EXIT present: $pngLoaderDecompressExitPresent" -ForegroundColor Green
        Write-Host "[probe] PNGLOADER_DECOMPRESSED_BYTES: $pngLoaderDecompressedBytes" -ForegroundColor Green
        Write-Host "[probe] PNGLOADER_IMAGE_CREATE_ENTER present: $pngLoaderImageCreateEnterPresent" -ForegroundColor Green
        Write-Host "[probe] PNGLOADER_IMAGE_OK present: $pngLoaderImageOkPresent" -ForegroundColor Green
        Write-Host "[probe] PNGLOADER_IMAGE_NULL present: $pngLoaderImageNullPresent" -ForegroundColor Green
        Write-Host "[probe] PNGLOADER_IMAGE_CREATE_EXIT present: $pngLoaderImageCreateExitPresent" -ForegroundColor Green
        Write-Host "[probe] PNGLOADER_EXIT present: $pngLoaderExitPresent" -ForegroundColor Green
        Write-Host "[probe] Deepest PNGLOADER marker reached: $pngLoaderDeepestMarker" -ForegroundColor Green
        Write-Host "[probe] Deepest LOADPNGSAFE marker reached: $loadPngSafeDeepestMarker" -ForegroundColor Green
        Write-Host "[probe] LOADPNGSAFE_ENTER present: $loadPngSafeEnterPresent" -ForegroundColor Green
        Write-Host "[probe] LOADPNGSAFE_READ_FAIL present: $loadPngSafeReadFailPresent" -ForegroundColor Green
        Write-Host "[probe] LOADPNGSAFE_READ_NULL present: $loadPngSafeReadNullPresent" -ForegroundColor Green
        Write-Host "[probe] LOADPNGSAFE_READ_OK present: $loadPngSafeReadOkPresent" -ForegroundColor Green
        Write-Host "[probe] LOADPNGSAFE_DATA_TOO_SMALL present: $loadPngSafeDataTooSmallPresent" -ForegroundColor Green
        Write-Host "[probe] LOADPNGSAFE_BEFORE_PNGLOADER_LOAD present: $loadPngSafeBeforePngLoaderLoadPresent" -ForegroundColor Green
        Write-Host "[probe] LOADPNGSAFE_OK present: $loadPngSafeOkPresent" -ForegroundColor Green
        Write-Host "[probe] LOADPNGSAFE_NULL present: $loadPngSafeNullPresent" -ForegroundColor Green
        Write-Host "[probe] LOADPNGSAFE_EXIT present: $loadPngSafeExitPresent" -ForegroundColor Green
        Write-Host "[probe] CURSOR_VALIDATE_ENTER present: $cursorValidateEnterPresent" -ForegroundColor Green
        Write-Host "[probe] CURSOR_VALIDATE_OK present: $cursorValidateOkPresent" -ForegroundColor Green
        Write-Host "[probe] CURSOR_VALIDATE_NULL present: $cursorValidateNullPresent" -ForegroundColor Green
        Write-Host "[probe] CURSOR_VALIDATE_BAD_DIMS present: $cursorValidateBadDimsPresent" -ForegroundColor Green
        Write-Host "[probe] CURSOR_VALIDATE_BAD_RAWDATA present: $cursorValidateBadRawDataPresent" -ForegroundColor Green
        Write-Host "[probe] CURSOR_VALIDATE_EXIT present: $cursorValidateExitPresent" -ForegroundColor Green
        if ($cursorPngDims) {
            Write-Host "[probe] CURSOR_PNG width: $($cursorPngDims.Width)" -ForegroundColor Green
            Write-Host "[probe] CURSOR_PNG height: $($cursorPngDims.Height)" -ForegroundColor Green
        } else {
            Write-Host "[probe] CURSOR_PNG width: n/a" -ForegroundColor Green
            Write-Host "[probe] CURSOR_PNG height: n/a" -ForegroundColor Green
        }
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
            "STEP8_PNG_BYPASS_ENABLED=$pngProbeEnabled"
            "STEP11_SKIP_WINDOW_TRAVERSAL_ENABLED=$step11SkipWindowTraversalEnabled"
            "STEP12_SAFE_FONT_PLACEHOLDER_ENABLED=$step12SafeFontPlaceholderEnabled"
            "STEP13_SKIP_CURSOR_DRAW_ENABLED=$step13SkipCursorDrawEnabled"
            "STEP13_CURSOR_PLACEHOLDER_ENABLED=$step13CursorPlaceholderEnabled"
            "STEP13_REAL_CURSOR_IMAGE_RENDERING_ENABLED=$step13RealCursorImageRenderingEnabled"
            "CURSOR_BODY_VARIANT=$cursorBodyVariant"
            "PNG_VARIANT=$pngLoaderVariantLabel"
            "UEFI_PNG_PROBE_VARIANT=$pngLoaderVariantLabel"
            "UEFI_PNG_PROBE_DEEPEST_MARKER=$uefiPngProbeDeepestMarker"
            "UEFI_PNG_PROBE_LEN=$uefiPngProbeLen"
            "UEFI_PNG_PROBE_IHDR_WIDTH=$uefiPngProbeIhdrWidth"
            "UEFI_PNG_PROBE_IHDR_HEIGHT=$uefiPngProbeIhdrHeight"
            "UEFI_PNG_PROBE_IHDR_BIT_DEPTH=$uefiPngProbeIhdrBitDepth"
            "UEFI_PNG_PROBE_IHDR_COLOR_TYPE=$uefiPngProbeIhdrColorType"
            "UEFI_PNG_PROBE_IDAT_CHUNK_COUNT=$uefiPngProbeIdatChunkCount"
            "UEFI_PNG_PROBE_IDAT_COMPRESSED_BYTES=$uefiPngProbeIdatCompressedBytes"
            "UEFI_PNG_PROBE_ZLIB_CMF=$uefiPngProbeZlibCmf"
            "UEFI_PNG_PROBE_ZLIB_FLG=$uefiPngProbeZlibFlg"
            "UEFI_PNG_PROBE_ZLIB_HEADER_OK_PRESENT=$uefiPngProbeZlibHeaderOkPresent"
            "UEFI_PNG_PROBE_AFTER_ZLIB_HEADER_PRESENT=$uefiPngProbeAfterZlibHeaderPresent"
            "UEFI_PNG_PROBE_BFINAL=$uefiPngProbeBfinal"
            "UEFI_PNG_PROBE_BTYPE=$uefiPngProbeBtype"
            "UEFI_PNG_PROBE_RAW_BYTE=$uefiPngProbeRawByte"
            "UEFI_PNG_PROBE_DYNAMIC_PRESENT=$uefiPngProbeDynamicPresent"
            "UEFI_PNG_PROBE_HLIT_BITS=$uefiPngProbeHlitBits"
            "UEFI_PNG_PROBE_HLIT=$uefiPngProbeHlit"
            "UEFI_PNG_PROBE_HDIST_BITS=$uefiPngProbeHdistBits"
            "UEFI_PNG_PROBE_HDIST=$uefiPngProbeHdist"
            "UEFI_PNG_PROBE_HCLEN_BITS=$uefiPngProbeHclenBits"
            "UEFI_PNG_PROBE_HCLEN=$uefiPngProbeHclen"
            "UEFI_PNG_PROBE_CODELEN_COUNT_READ=$uefiPngProbeCodeLenCountRead"
            "UEFI_PNG_PROBE_CODELEN_NONZERO_COUNT=$uefiPngProbeCodeLenNonzeroCount"
            "UEFI_PNG_PROBE_CODELEN_ZERO_COUNT=$uefiPngProbeCodeLenZeroCount"
            "UEFI_PNG_PROBE_CODELEN_MAX_VALUE=$uefiPngProbeCodeLenMaxValue"
            "UEFI_PNG_PROBE_CODELEN_MAX_BITS=$uefiPngProbeCodeLenMaxBits"
            "UEFI_PNG_PROBE_CODELEN_LEN_1_COUNT=$uefiPngProbeCodeLenLen1Count"
            "UEFI_PNG_PROBE_CODELEN_LEN_2_COUNT=$uefiPngProbeCodeLenLen2Count"
            "UEFI_PNG_PROBE_CODELEN_LEN_3_COUNT=$uefiPngProbeCodeLenLen3Count"
            "UEFI_PNG_PROBE_CODELEN_LEN_4_COUNT=$uefiPngProbeCodeLenLen4Count"
            "UEFI_PNG_PROBE_CODELEN_LEN_5_COUNT=$uefiPngProbeCodeLenLen5Count"
            "UEFI_PNG_PROBE_CODELEN_LEN_6_COUNT=$uefiPngProbeCodeLenLen6Count"
            "UEFI_PNG_PROBE_CODELEN_LEN_7_COUNT=$uefiPngProbeCodeLenLen7Count"
            "UEFI_PNG_PROBE_CODELEN_VALUES_OK_PRESENT=$uefiPngProbeCodeLenValuesOkPresent"
            "UEFI_PNG_PROBE_CODELEN_VALUES_BAD_PRESENT=$uefiPngProbeCodeLenValuesBadPresent"
            "UEFI_PNG_PROBE_CODELEN_HISTOGRAM_OK_PRESENT=$uefiPngProbeCodeLenHistogramOkPresent"
            "UEFI_PNG_PROBE_CODELEN_HISTOGRAM_BAD_PRESENT=$uefiPngProbeCodeLenHistogramBadPresent"
            "UEFI_PNG_PROBE_CODELEN_NEXT_CODE_OK_PRESENT=$uefiPngProbeCodeLenNextCodeOkPresent"
            "UEFI_PNG_PROBE_CODELEN_NEXT_CODE_BAD_PRESENT=$uefiPngProbeCodeLenNextCodeBadPresent"
            "UEFI_PNG_PROBE_CODELEN_CODE_SPACE_END=$uefiPngProbeCodeLenCodeSpaceEnd"
            "UEFI_PNG_PROBE_CODELEN_CODE_SPACE_REMAINING=$uefiPngProbeCodeLenCodeSpaceRemaining"
            "UEFI_PNG_PROBE_CODELEN_TABLE_ENTRY_COUNT=$uefiPngProbeCodeLenTableEntryCount"
            "UEFI_PNG_PROBE_CODELEN_TABLE_BUILD_OK_PRESENT=$uefiPngProbeCodeLenTableBuildOkPresent"
            "UEFI_PNG_PROBE_CODELEN_TABLE_BUILD_BAD_PRESENT=$uefiPngProbeCodeLenTableBuildBadPresent"
            "UEFI_PNG_PROBE_CODELEN_LOOKUP_SMOKE_SYMBOL=$uefiPngProbeCodeLenLookupSmokeSymbol"
            "UEFI_PNG_PROBE_CODELEN_LOOKUP_SMOKE_BITS=$uefiPngProbeCodeLenLookupSmokeBits"
            "UEFI_PNG_PROBE_CODELEN_LOOKUP_SMOKE_CODE=$uefiPngProbeCodeLenLookupSmokeCode"
            "UEFI_PNG_PROBE_CODELEN_LOOKUP_SMOKE_OK_PRESENT=$uefiPngProbeCodeLenLookupSmokeOkPresent"
            "UEFI_PNG_PROBE_CODELEN_LOOKUP_SMOKE_BAD_PRESENT=$uefiPngProbeCodeLenLookupSmokeBadPresent"
            "UEFI_PNG_PROBE_COUNTS_OK_PRESENT=$uefiPngProbeCountsOkPresent"
            "UEFI_PNG_PROBE_COUNTS_BAD_PRESENT=$uefiPngProbeCountsBadPresent"
            "UEFI_PNG_PROBE_CODELEN_ALPHABET_OK_PRESENT=$uefiPngProbeCodeLenAlphabetOkPresent"
            "UEFI_PNG_PROBE_TABLES_OK_PRESENT=$uefiPngProbeTablesOkPresent"
            "UEFI_PNG_PROBE_TABLES_BAD_PRESENT=$uefiPngProbeTablesBadPresent"
            "UEFI_PNG_PROBE_FIRST_SYMBOL_VALUE=$uefiPngProbeFirstSymbolValue"
            "UEFI_PNG_PROBE_FIRST_SYMBOL_LITERAL_PRESENT=$uefiPngProbeFirstSymbolLiteralPresent"
            "UEFI_PNG_PROBE_FIRST_SYMBOL_LENGTH_PRESENT=$uefiPngProbeFirstSymbolLengthPresent"
            "UEFI_PNG_PROBE_FIRST_SYMBOL_END_PRESENT=$uefiPngProbeFirstSymbolEndPresent"
            "UEFI_PNG_PROBE_ONE_OUTPUT_LITERAL_WRITE_PRESENT=$uefiPngProbeOneOutputLiteralWritePresent"
            "UEFI_PNG_PROBE_ONE_OUTPUT_LEN_DIST_PRESENT=$uefiPngProbeOneOutputLenDistPresent"
            "UEFI_PNG_PROBE_ONE_OUTPUT_BOUNDS_ABORT_PRESENT=$uefiPngProbeOneOutputBoundsAbortPresent"
            "UEFI_PNG_PROBE_ONE_OUTPUT_OK_PRESENT=$uefiPngProbeOneOutputOkPresent"
            "UEFI_PNG_PROBE_SMOKE_PROGRESS_PRESENT=$uefiPngProbeSmokeProgressPresent"
            "UEFI_PNG_PROBE_SMOKE_BOUNDS_ABORT_PRESENT=$uefiPngProbeSmokeBoundsAbortPresent"
            "UEFI_PNG_PROBE_SMOKE_OK_PRESENT=$uefiPngProbeSmokeOkPresent"
            "UEFI_PNG_PROBE_OUTPUT_ALLOC_ENTER_PRESENT=$uefiPngProbeOutputAllocEnterPresent"
            "UEFI_PNG_PROBE_OUTPUT_ALLOC_OK_PRESENT=$uefiPngProbeOutputAllocOkPresent"
            "UEFI_PNG_PROBE_OUTPUT_ALLOC_SIZE=$uefiPngProbeOutputAllocSize"
            "UEFI_PNG_PROBE_INFLATE_ENTER_PRESENT=$uefiPngProbeInflateEnterPresent"
            "UEFI_PNG_PROBE_INFLATE_PROGRESS_PRESENT=$uefiPngProbeInflateProgressPresent"
            "UEFI_PNG_PROBE_INFLATE_BOUNDS_ABORT_PRESENT=$uefiPngProbeInflateBoundsAbortPresent"
            "UEFI_PNG_PROBE_INFLATE_FAULT_PRESENT=$uefiPngProbeInflateFaultPresent"
            "UEFI_PNG_PROBE_INFLATE_OK_PRESENT=$uefiPngProbeInflateOkPresent"
            "UEFI_PNG_PROBE_INFLATE_EXIT_PRESENT=$uefiPngProbeInflateExitPresent"
            "PNGLOADER_DEEPEST_MARKER=$pngLoaderDeepestMarker"
            "PNGLOADER_ENTRY_BASELINE_LEN=$pngLoaderBytesLength"
            "PNGLOADER_ENTRY_BASELINE_IHDR_WIDTH=$pngLoaderIhdrWidth"
            "PNGLOADER_ENTRY_BASELINE_IHDR_HEIGHT=$pngLoaderIhdrHeight"
            "PNGDECOMP_DEEPEST_MARKER=$pngDecompressDeepestMarker"
            "PNGDECOMP_AFTER_INPUT_GATE_ENTER_PRESENT=$pngDecompressAfterInputGateEnterPresent"
            "PNGDECOMP_AFTER_INPUT_GATE_EXIT_PRESENT=$pngDecompressAfterInputGateExitPresent"
            "PNGDECOMP_PREP_ROUTE_ENTER_PRESENT=$pngDecompressPrepRouteEnterPresent"
            "PNGDECOMP_PREP_ROUTE_EXIT_PRESENT=$pngDecompressPrepRouteExitPresent"
            "PNGDECOMP_PREP_NOOP_ENTER_PRESENT=$pngDecompressPrepNoopEnterPresent"
            "PNGDECOMP_PREP_NOOP_EXIT_PRESENT=$pngDecompressPrepNoopExitPresent"
            "PNGDECOMP_PREP_META_ENTER_PRESENT=$pngDecompressPrepMetaEnterPresent"
            "PNGDECOMP_PREP_META_DIMS_OK_PRESENT=$pngDecompressPrepMetaDimsOkPresent"
            "PNGDECOMP_PREP_META_LEN_OK_PRESENT=$pngDecompressPrepMetaLenOkPresent"
            "PNGDECOMP_PREP_META_EXIT_PRESENT=$pngDecompressPrepMetaExitPresent"
            "PNGDECOMP_PREP_BYTES_ENTER_PRESENT=$pngDecompressPrepBytesEnterPresent"
            "PNGDECOMP_PREP_BYTES_NULL_PRESENT=$pngDecompressPrepBytesNullPresent"
            "PNGDECOMP_PREP_BYTES_OK_PRESENT=$pngDecompressPrepBytesOkPresent"
            "PNGDECOMP_PREP_BYTES_LEN=$pngDecompressPrepBytesLen"
            "PNGDECOMP_PREP_BYTES_EXIT_PRESENT=$pngDecompressPrepBytesExitPresent"
            "PNGDECOMP_PREP_INLINE_ENTER_PRESENT=$pngDecompressPrepInlineEnterPresent"
            "PNGDECOMP_PREP_INLINE_AFTER_OUTPUT_ALLOC_PRESENT=$pngDecompressPrepInlineAfterOutputAllocPresent"
            "PNGDECOMP_PREP_INLINE_AFTER_BITREADER_INIT_PRESENT=$pngDecompressPrepInlineAfterBitreaderInitPresent"
            "PNGDECOMP_PREP_INLINE_AFTER_TABLE_REFS_PRESENT=$pngDecompressPrepInlineAfterTableRefsPresent"
            "PNGDECOMP_PREP_INLINE_EXIT_PRESENT=$pngDecompressPrepInlineExitPresent"
            "PNGDECOMP_PREP_BOUNDARY_ENTER_PRESENT=$pngDecompressPrepBoundaryEnterPresent"
            "PNGDECOMP_PREP_BOUNDARY_BEFORE_CALL_PRESENT=$pngDecompressPrepBoundaryBeforeCallPresent"
            "PNGDECOMP_PREP_BOUNDARY_AFTER_CALL_PRESENT=$pngDecompressPrepBoundaryAfterCallPresent"
            "PNGDECOMP_PREP_BOUNDARY_OK_PRESENT=$pngDecompressPrepBoundaryOkPresent"
            "PNGDECOMP_PREP_BOUNDARY_FAIL_PRESENT=$pngDecompressPrepBoundaryFailPresent"
            "PNGDECOMP_PREP_BOUNDARY_EXIT_PRESENT=$pngDecompressPrepBoundaryExitPresent"
            "PNGDECOMP_PREP_CONTEXT_ENTER_PRESENT=$pngDecompressPrepContextEnterPresent"
            "PNGDECOMP_PREP_CONTEXT_VALIDATE_ENTER_PRESENT=$pngDecompressPrepContextValidateEnterPresent"
            "PNGDECOMP_PREP_CONTEXT_VALIDATE_EXIT_PRESENT=$pngDecompressPrepContextValidateExitPresent"
            "PNGDECOMP_PREP_CONTEXT_ALLOC_ENTER_PRESENT=$pngDecompressPrepContextAllocEnterPresent"
            "PNGDECOMP_PREP_CONTEXT_ALLOC_EXIT_PRESENT=$pngDecompressPrepContextAllocExitPresent"
            "PNGDECOMP_PREP_CONTEXT_BITREADER_ENTER_PRESENT=$pngDecompressPrepContextBitreaderEnterPresent"
            "PNGDECOMP_PREP_CONTEXT_BITREADER_EXIT_PRESENT=$pngDecompressPrepContextBitreaderExitPresent"
            "PNGDECOMP_PREP_CONTEXT_TABLES_ENTER_PRESENT=$pngDecompressPrepContextTablesEnterPresent"
            "PNGDECOMP_PREP_CONTEXT_TABLES_EXIT_PRESENT=$pngDecompressPrepContextTablesExitPresent"
            "PNGDECOMP_PREP_CONTEXT_OK_PRESENT=$pngDecompressPrepContextOkPresent"
            "PNGDECOMP_PREP_CONTEXT_FAIL_PRESENT=$pngDecompressPrepContextFailPresent"
            "PNGDECOMP_PREP_CONTEXT_EXIT_PRESENT=$pngDecompressPrepContextExitPresent"
            "PNGDECOMP_INPUT_GATE_ENTER_PRESENT=$pngDecompressInputGateEnterPresent"
            "PNGDECOMP_INPUT_GATE_OK_PRESENT=$pngDecompressInputGateOkPresent"
            "PNGDECOMP_INPUT_GATE_BAD_LEN_PRESENT=$pngDecompressInputGateBadLenPresent"
            "PNGDECOMP_INPUT_GATE_BAD_DIMS_PRESENT=$pngDecompressInputGateBadDimsPresent"
            "PNGDECOMP_INPUT_GATE_BAD_FORMAT_PRESENT=$pngDecompressInputGateBadFormatPresent"
            "PNGDECOMP_INPUT_GATE_BAD_IDAT_PRESENT=$pngDecompressInputGateBadIdatPresent"
            "PNGDECOMP_INPUT_GATE_EXIT_PRESENT=$pngDecompressInputGateExitPresent"
            "PNGDECOMP_GATE_INLINE_ENTER_PRESENT=$pngDecompressGateInlineEnterPresent"
            "PNGDECOMP_GATE_INLINE_OK_PRESENT=$pngDecompressGateInlineOkPresent"
            "PNGDECOMP_GATE_INLINE_BEFORE_RETURN_PRESENT=$pngDecompressGateInlineBeforeReturnPresent"
            "PNGDECOMP_GATE_HELPER_ENTER_PRESENT=$pngDecompressGateHelperEnterPresent"
            "PNGDECOMP_GATE_HELPER_OK_PRESENT=$pngDecompressGateHelperOkPresent"
            "PNGDECOMP_GATE_HELPER_RETURNING_PRESENT=$pngDecompressGateHelperReturningPresent"
            "PNGDECOMP_GATE_BOOL_ENTER_PRESENT=$pngDecompressGateBoolEnterPresent"
            "PNGDECOMP_GATE_BOOL_OK_PRESENT=$pngDecompressGateBoolOkPresent"
            "PNGDECOMP_GATE_BOOL_EXIT_PRESENT=$pngDecompressGateBoolExitPresent"
            "PNGDECOMP_GATE_BOOL_CALLER_AFTER_PRESENT=$pngDecompressGateBoolCallerAfterPresent"
            "PNGDECOMP_GATE_NO_STATE_COPY_ENTER_PRESENT=$pngDecompressGateNoStateCopyEnterPresent"
            "PNGDECOMP_GATE_NO_STATE_COPY_AFTER_PRESENT=$pngDecompressGateNoStateCopyAfterPresent"
            "PNGDECOMP_POST_GATE_FIRST_INSTRUCTION_PRESENT=$pngDecompressPostGateFirstInstructionPresent"
            "PNGDECOMP_FIRST_SYMBOL_BOUNDARY_ENTER_PRESENT=$pngDecompressFirstSymbolBoundaryEnterPresent"
            "PNGDECOMP_FIRST_SYMBOL_BOUNDARY_EXIT_PRESENT=$pngDecompressFirstSymbolBoundaryExitPresent"
            "PNGDECOMP_BITREADER_ENTER_PRESENT=$pngDecompressBitReaderEnterPresent"
            "PNGDECOMP_BITREADER_BYTEPOS=$pngDecompressBitReaderBytePos"
            "PNGDECOMP_BITREADER_BITCOUNT=$pngDecompressBitReaderBitCount"
            "PNGDECOMP_BITREADER_OK_PRESENT=$pngDecompressBitReaderOkPresent"
            "PNGDECOMP_BITREADER_BAD_PRESENT=$pngDecompressBitReaderBadPresent"
            "PNGDECOMP_BITREADER_EXIT_PRESENT=$pngDecompressBitReaderExitPresent"
            "PNGDECOMP_FIRST_SYMBOL_DECODE_ENTER_PRESENT=$pngDecompressFirstSymbolDecodeEnterPresent"
            "PNGDECOMP_FIRST_SYMBOL_VALUE=$pngDecompressFirstSymbolValue"
            "PNGDECOMP_FIRST_SYMBOL_LITERAL_PRESENT=$pngDecompressFirstSymbolLiteralPresent"
            "PNGDECOMP_FIRST_SYMBOL_END_PRESENT=$pngDecompressFirstSymbolEndPresent"
            "PNGDECOMP_FIRST_SYMBOL_LENGTH_PRESENT=$pngDecompressFirstSymbolLengthPresent"
            "PNGDECOMP_FIRST_SYMBOL_DECODE_EXIT_PRESENT=$pngDecompressFirstSymbolDecodeExitPresent"
            "PNGDECOMP_LITERAL_WRITE_ENTER_PRESENT=$pngDecompressLiteralWriteEnterPresent"
            "PNGDECOMP_LITERAL_WRITE_BOUNDS_OK_PRESENT=$pngDecompressLiteralWriteBoundsOkPresent"
            "PNGDECOMP_LITERAL_WRITE_BOUNDS_BAD_PRESENT=$pngDecompressLiteralWriteBoundsBadPresent"
            "PNGDECOMP_LITERAL_WRITE_EXIT_PRESENT=$pngDecompressLiteralWriteExitPresent"
            "PNGDECOMP_LEN_DIST_ENTER_PRESENT=$pngDecompressLenDistEnterPresent"
            "PNGDECOMP_LENGTH_SYMBOL=$pngDecompressLengthSymbol"
            "PNGDECOMP_LENGTH_VALUE=$pngDecompressLengthValue"
            "PNGDECOMP_DISTANCE_SYMBOL=$pngDecompressDistanceSymbol"
            "PNGDECOMP_DISTANCE_VALUE=$pngDecompressDistanceValue"
            "PNGDECOMP_LEN_DIST_EXIT_PRESENT=$pngDecompressLenDistExitPresent"
            "PNGDECOMP_ONE_STEP_ENTER_PRESENT=$pngDecompressOneStepEnterPresent"
            "PNGDECOMP_ONE_STEP_LITERAL_PRESENT=$pngDecompressOneStepLiteralPresent"
            "PNGDECOMP_ONE_STEP_BACKREF_PRESENT=$pngDecompressOneStepBackrefPresent"
            "PNGDECOMP_ONE_STEP_BOUNDS_ABORT_PRESENT=$pngDecompressOneStepBoundsAbortPresent"
            "PNGDECOMP_ONE_STEP_OK_PRESENT=$pngDecompressOneStepOkPresent"
            "PNGDECOMP_ONE_STEP_EXIT_PRESENT=$pngDecompressOneStepExitPresent"
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
            "CURSOR_PNG_PROBE_ENTER_PRESENT=$cursorPngProbeEnterPresent"
            "CURSOR_PNG_BEFORE_DISK_CHECK_PRESENT=$cursorPngBeforeDiskCheckPresent"
            "CURSOR_PNG_AFTER_DISK_CHECK_PRESENT=$cursorPngAfterDiskCheckPresent"
            "CURSOR_PNG_BEFORE_FILE_LOOKUP_PRESENT=$cursorPngBeforeFileLookupPresent"
            "CURSOR_PNG_AFTER_FILE_LOOKUP_PRESENT=$cursorPngAfterFileLookupPresent"
            "CURSOR_PNG_BYTES_LENGTH=$cursorPngBytesLength"
            "CURSOR_PNG_LOAD_AFTER_IHDR_PRESENT=$cursorPngLoadAfterIhdrPresent"
            "CURSOR_PNG_LOAD_AFTER_CHUNK_SCAN_PRESENT=$cursorPngLoadAfterChunkScanPresent"
            "CURSOR_PNG_LOAD_AFTER_IDAT_AGGREGATION_PRESENT=$cursorPngLoadAfterIdatAggregationPresent"
            "CURSOR_PNG_DECOMPRESS_PREMETA_PRESENT=$cursorPngDecompressPreMetaPresent"
            "CURSOR_PNG_DECOMPRESS_NOOP_PRESENT=$cursorPngDecompressNoopPresent"
            "CURSOR_PNG_DECOMPRESS_BYTES_NOOP_PRESENT=$cursorPngDecompressBytesNoopPresent"
            "CURSOR_PNG_DECOMPRESS_ZLIB_HEADER_PRESENT=$cursorPngDecompressZlibHeaderPresent"
            "CURSOR_PNG_DECOMPRESS_OUTPUT_ALLOC_PRESENT=$cursorPngDecompressOutputAllocPresent"
            "CURSOR_PNG_DECOMPRESS_DEFLATE_HEADER_PRESENT=$cursorPngDecompressDeflateHeaderPresent"
            "CURSOR_PNG_DECOMPRESS_HUFFMAN_SETUP_PRESENT=$cursorPngDecompressHuffmanSetupPresent"
            "CURSOR_PNG_DECOMPRESS_INFLATE_SMOKE_PRESENT=$cursorPngDecompressInflateSmokePresent"
            "CURSOR_PNG_HEADER_HELPER_ENTER_PRESENT=$cursorPngHeaderHelperEnterPresent"
            "CURSOR_PNG_HEADER_HELPER_BAD_PRESENT=$cursorPngHeaderHelperBadPresent"
            "CURSOR_PNG_HEADER_HELPER_OK_PRESENT=$cursorPngHeaderHelperOkPresent"
            "CURSOR_PNG_HEADER_HELPER_EXIT_PRESENT=$cursorPngHeaderHelperExitPresent"
            "CURSOR_PNG_NOOP_LOADER_ENTER_PRESENT=$cursorPngNoopLoaderEnterPresent"
            "CURSOR_PNG_NOOP_LOADER_EXIT_PRESENT=$cursorPngNoopLoaderExitPresent"
            "CURSOR_PNG_BYTES_NOOP_ENTER_PRESENT=$cursorPngBytesNoopEnterPresent"
            "CURSOR_PNG_BYTES_NOOP_NULL_PRESENT=$cursorPngBytesNoopNullPresent"
            "CURSOR_PNG_BYTES_NOOP_OK_PRESENT=$cursorPngBytesNoopOkPresent"
            "CURSOR_PNG_BYTES_NOOP_EXIT_PRESENT=$cursorPngBytesNoopExitPresent"
            "CURSOR_PNG_IHDR_HELPER_ENTER_PRESENT=$cursorPngIhdrHelperEnterPresent"
            "CURSOR_PNG_IHDR_HELPER_BAD_PRESENT=$cursorPngIhdrHelperBadPresent"
            "CURSOR_PNG_IHDR_HELPER_OK_PRESENT=$cursorPngIhdrHelperOkPresent"
            "CURSOR_PNG_IHDR_WIDTH=$cursorPngIhdrWidth"
            "CURSOR_PNG_IHDR_HEIGHT=$cursorPngIhdrHeight"
            "CURSOR_PNG_IHDR_HELPER_EXIT_PRESENT=$cursorPngIhdrHelperExitPresent"
            "CURSOR_PNG_LOAD_WRAPPER_ENTER_PRESENT=$cursorPngLoadWrapperEnterPresent"
            "CURSOR_PNG_LOAD_WRAPPER_BEFORE_CALL_PRESENT=$cursorPngLoadWrapperBeforeCallPresent"
            "CURSOR_PNG_LOAD_WRAPPER_AFTER_CALL_PRESENT=$cursorPngLoadWrapperAfterCallPresent"
            "CURSOR_PNG_LOAD_WRAPPER_EXIT_PRESENT=$cursorPngLoadWrapperExitPresent"
            "CURSOR_PNG_NULL_PRESENT=$cursorPngNullPresent"
            "CURSOR_PNG_OK_PRESENT=$cursorPngOkPresent"
            "CURSOR_PNG_DIMS_ENTER_PRESENT=$cursorPngDimsEnterPresent"
            "CURSOR_PNG_DIMS_EXIT_PRESENT=$cursorPngDimsExitPresent"
            "CURSOR_PNG_PROBE_EXIT_PRESENT=$cursorPngProbeExitPresent"
            "CURSOR_PNG_DEEPEST_MARKER=$cursorPngDeepestMarker"
            "ENABLED_PNG_VARIANT=$pngLoaderVariantLabel"
            "PNGLOADER_ENTER_PRESENT=$pngLoaderEnterPresent"
            "PNGLOADER_BYTES_NULL_PRESENT=$pngLoaderBytesNullPresent"
            "PNGLOADER_BYTES_OK_PRESENT=$pngLoaderBytesOkPresent"
            "PNGLOADER_HEADER_ENTER_PRESENT=$pngLoaderHeaderEnterPresent"
            "PNGLOADER_HEADER_OK_PRESENT=$pngLoaderHeaderOkPresent"
            "PNGLOADER_HEADER_BAD_PRESENT=$pngLoaderHeaderBadPresent"
            "PNGLOADER_IHDR_ENTER_PRESENT=$pngLoaderIhdrEnterPresent"
            "PNGLOADER_IHDR_OK_PRESENT=$pngLoaderIhdrOkPresent"
            "PNGLOADER_IHDR_BAD_PRESENT=$pngLoaderIhdrBadPresent"
            "PNGLOADER_IHDR_DIMS_PRESENT=$pngLoaderIhdrDimsPresent"
            "PNGLOADER_IHDR_WIDTH=$pngLoaderIhdrWidth"
            "PNGLOADER_IHDR_HEIGHT=$pngLoaderIhdrHeight"
            "PNGLOADER_IHDR_BIT_DEPTH=$pngLoaderIhdrBitDepth"
            "PNGLOADER_IHDR_COLOR_TYPE=$pngLoaderIhdrColorType"
            "PNGLOADER_CHUNK_SCAN_ENTER_PRESENT=$pngLoaderChunkScanEnterPresent"
            "PNGLOADER_CHUNK_TYPE_IHDR_PRESENT=$pngLoaderChunkTypeIhdrPresent"
            "PNGLOADER_CHUNK_TYPE_IDAT_PRESENT=$pngLoaderChunkTypeIdatPresent"
            "PNGLOADER_CHUNK_TYPE_IEND_PRESENT=$pngLoaderChunkTypeIendPresent"
            "PNGLOADER_CHUNK_SCAN_EXIT_PRESENT=$pngLoaderChunkScanExitPresent"
            "PNGLOADER_IDAT_CHUNK_COUNT=$pngLoaderIdatChunkCount"
            "PNGLOADER_IDAT_COMPRESSED_BYTES=$pngLoaderIdatCompressedBytes"
            "PNGLOADER_DECOMPRESS_PREMETA_ENTER_PRESENT=$pngLoaderDecompressPreMetaEnterPresent"
            "PNGLOADER_DECOMPRESS_PREMETA_LEN=$pngLoaderDecompressPreMetaLen"
            "PNGLOADER_DECOMPRESS_PREMETA_EXPECTED_OUT_LEN=$pngLoaderDecompressPreMetaExpectedOutLen"
            "PNGLOADER_DECOMPRESS_PREMETA_EXIT_PRESENT=$pngLoaderDecompressPreMetaExitPresent"
            "PNGDECOMP_NOOP_ENTER_PRESENT=$pngDecompressNoopEnterPresent"
            "PNGDECOMP_NOOP_EXIT_PRESENT=$pngDecompressNoopExitPresent"
            "PNGDECOMP_BYTES_NOOP_ENTER_PRESENT=$pngDecompressBytesNoopEnterPresent"
            "PNGDECOMP_BYTES_NOOP_NULL_PRESENT=$pngDecompressBytesNoopNullPresent"
            "PNGDECOMP_BYTES_NOOP_OK_PRESENT=$pngDecompressBytesNoopOkPresent"
            "PNGDECOMP_BYTES_NOOP_LEN=$pngDecompressBytesNoopLen"
            "PNGDECOMP_BYTES_NOOP_EXIT_PRESENT=$pngDecompressBytesNoopExitPresent"
            "PNGDECOMP_ZLIB_HEADER_ENTER_PRESENT=$pngDecompressZlibHeaderEnterPresent"
            "PNGDECOMP_ZLIB_LEN_OK_PRESENT=$pngDecompressZlibLenOkPresent"
            "PNGDECOMP_ZLIB_LEN_BAD_PRESENT=$pngDecompressZlibLenBadPresent"
            "PNGDECOMP_ZLIB_CMF=$pngDecompressZlibCmf"
            "PNGDECOMP_ZLIB_FLG=$pngDecompressZlibFlg"
            "PNGDECOMP_ZLIB_METHOD_OK_PRESENT=$pngDecompressZlibMethodOkPresent"
            "PNGDECOMP_ZLIB_METHOD_BAD_PRESENT=$pngDecompressZlibMethodBadPresent"
            "PNGDECOMP_ZLIB_FCHECK_OK_PRESENT=$pngDecompressZlibFcheckOkPresent"
            "PNGDECOMP_ZLIB_FCHECK_BAD_PRESENT=$pngDecompressZlibFcheckBadPresent"
            "PNGDECOMP_ZLIB_FDICT_SET_PRESENT=$pngDecompressZlibFdictSetPresent"
            "PNGDECOMP_ZLIB_FDICT_CLEAR_PRESENT=$pngDecompressZlibFdictClearPresent"
            "PNGDECOMP_ZLIB_HEADER_EXIT_PRESENT=$pngDecompressZlibHeaderExitPresent"
            "PNGDECOMP_OUTPUT_ALLOC_ENTER_PRESENT=$pngDecompressOutputAllocEnterPresent"
            "PNGDECOMP_OUTPUT_ALLOC_SIZE=$pngDecompressOutputAllocSize"
            "PNGDECOMP_OUTPUT_ALLOC_OK_PRESENT=$pngDecompressOutputAllocOkPresent"
            "PNGDECOMP_OUTPUT_ALLOC_NULL_PRESENT=$pngDecompressOutputAllocNullPresent"
            "PNGDECOMP_OUTPUT_ALLOC_EXIT_PRESENT=$pngDecompressOutputAllocExitPresent"
            "PNGDECOMP_DEFLATE_ENTER_PRESENT=$pngDecompressDeflateEnterPresent"
            "PNGDECOMP_DEFLATE_FIRST_BLOCK_ENTER_PRESENT=$pngDecompressDeflateFirstBlockEnterPresent"
            "PNGDECOMP_DEFLATE_BFINAL=$pngDecompressDeflateBfinal"
            "PNGDECOMP_DEFLATE_BTYPE=$pngDecompressDeflateBtype"
            "PNGDECOMP_DEFLATE_FIRST_BLOCK_EXIT_PRESENT=$pngDecompressDeflateFirstBlockExitPresent"
            "PNGDECOMP_DEFLATE_EXIT_PRESENT=$pngDecompressDeflateExitPresent"
            "PNGDECOMP_HUFFMAN_SETUP_ENTER_PRESENT=$pngDecompressHuffmanSetupEnterPresent"
            "PNGDECOMP_HUFFMAN_FIXED_PRESENT=$pngDecompressHuffmanFixedPresent"
            "PNGDECOMP_HUFFMAN_DYNAMIC_PRESENT=$pngDecompressHuffmanDynamicPresent"
            "PNGDECOMP_HUFFMAN_SETUP_OK_PRESENT=$pngDecompressHuffmanSetupOkPresent"
            "PNGDECOMP_HUFFMAN_SETUP_BAD_PRESENT=$pngDecompressHuffmanSetupBadPresent"
            "PNGDECOMP_HUFFMAN_SETUP_EXIT_PRESENT=$pngDecompressHuffmanSetupExitPresent"
            "PNGDECOMP_INFLATE_ENTER_PRESENT=$pngDecompressInflateEnterPresent"
            "PNGDECOMP_INFLATE_FIRST_SYMBOL_ENTER_PRESENT=$pngDecompressInflateFirstSymbolEnterPresent"
            "PNGDECOMP_INFLATE_FIRST_SYMBOL_EXIT_PRESENT=$pngDecompressInflateFirstSymbolExitPresent"
            "PNGDECOMP_INFLATE_PROGRESS_PRESENT=$pngDecompressInflateProgressPresent"
            "PNGDECOMP_INFLATE_BOUNDS_ABORT_PRESENT=$pngDecompressInflateBoundsAbortPresent"
            "PNGDECOMP_INFLATE_OK_PRESENT=$pngDecompressInflateOkPresent"
            "PNGDECOMP_INFLATE_EXIT_PRESENT=$pngDecompressInflateExitPresent"
            "PNGDECOMP_TINY_BOUNDARY_ENTER_PRESENT=$pngDecompressTinyBoundaryEnterPresent"
            "PNGDECOMP_TINY_BOUNDARY_AFTER_SETUP_PRESENT=$pngDecompressTinyBoundaryAfterSetupPresent"
            "PNGDECOMP_TINY_BOUNDARY_BEFORE_RETURN_PRESENT=$pngDecompressTinyBoundaryBeforeReturnPresent"
            "PNGDECOMP_TINY_BOUNDARY_EXIT_PRESENT=$pngDecompressTinyBoundaryExitPresent"
            "PNGDECOMP_TINY_BITREADER_ENTER_PRESENT=$pngDecompressTinyBitReaderEnterPresent"
            "PNGDECOMP_TINY_BITREADER_BYTEPOS=$pngDecompressTinyBitReaderBytePos"
            "PNGDECOMP_TINY_BITREADER_BITCOUNT=$pngDecompressTinyBitReaderBitCount"
            "PNGDECOMP_TINY_BITREADER_EXIT_PRESENT=$pngDecompressTinyBitReaderExitPresent"
            "PNGDECOMP_TINY_SYMBOL_ENTER_PRESENT=$pngDecompressTinySymbolEnterPresent"
            "PNGDECOMP_TINY_SYMBOL_VALUE=$pngDecompressTinySymbolValue"
            "PNGDECOMP_TINY_SYMBOL_LITERAL_PRESENT=$pngDecompressTinySymbolLiteralPresent"
            "PNGDECOMP_TINY_SYMBOL_LENGTH_PRESENT=$pngDecompressTinySymbolLengthPresent"
            "PNGDECOMP_TINY_SYMBOL_END_PRESENT=$pngDecompressTinySymbolEndPresent"
            "PNGDECOMP_TINY_SYMBOL_EXIT_PRESENT=$pngDecompressTinySymbolExitPresent"
            "PNGDECOMP_TINY_ONEOP_ENTER_PRESENT=$pngDecompressTinyOneOpEnterPresent"
            "PNGDECOMP_TINY_ONEOP_LITERAL_PRESENT=$pngDecompressTinyOneOpLiteralPresent"
            "PNGDECOMP_TINY_ONEOP_LENGTH_PRESENT=$pngDecompressTinyOneOpLengthPresent"
            "PNGDECOMP_TINY_ONEOP_BOUNDS_ABORT_PRESENT=$pngDecompressTinyOneOpBoundsAbortPresent"
            "PNGDECOMP_TINY_ONEOP_OK_PRESENT=$pngDecompressTinyOneOpOkPresent"
            "PNGDECOMP_TINY_ONEOP_EXIT_PRESENT=$pngDecompressTinyOneOpExitPresent"
            "PNGDECOMP_TINY_INFLATE_ENTER_PRESENT=$pngDecompressTinyInflateEnterPresent"
            "PNGDECOMP_TINY_INFLATE_PROGRESS_PRESENT=$pngDecompressTinyInflateProgressPresent"
            "PNGDECOMP_TINY_INFLATE_BOUNDS_ABORT_PRESENT=$pngDecompressTinyInflateBoundsAbortPresent"
            "PNGDECOMP_TINY_INFLATE_OK_PRESENT=$pngDecompressTinyInflateOkPresent"
            "PNGDECOMP_TINY_INFLATE_EXIT_PRESENT=$pngDecompressTinyInflateExitPresent"
            "PNGLOADER_IDAT_BYTES_ENTER_PRESENT=$pngLoaderIdatBytesEnterPresent"
            "PNGLOADER_IDAT_BYTES_OK_PRESENT=$pngLoaderIdatBytesOkPresent"
            "PNGLOADER_IDAT_BYTES_EMPTY_PRESENT=$pngLoaderIdatBytesEmptyPresent"
            "PNGLOADER_DECOMPRESS_ENTER_PRESENT=$pngLoaderDecompressEnterPresent"
            "PNGLOADER_DECOMPRESS_OK_PRESENT=$pngLoaderDecompressOkPresent"
            "PNGLOADER_DECOMPRESS_NULL_PRESENT=$pngLoaderDecompressNullPresent"
            "PNGLOADER_DECOMPRESS_EXIT_PRESENT=$pngLoaderDecompressExitPresent"
            "PNGLOADER_DECOMPRESSED_BYTES=$pngLoaderDecompressedBytes"
            "PNGLOADER_IMAGE_CREATE_ENTER_PRESENT=$pngLoaderImageCreateEnterPresent"
            "PNGLOADER_IMAGE_OK_PRESENT=$pngLoaderImageOkPresent"
            "PNGLOADER_IMAGE_NULL_PRESENT=$pngLoaderImageNullPresent"
            "PNGLOADER_IMAGE_CREATE_EXIT_PRESENT=$pngLoaderImageCreateExitPresent"
            "PNGLOADER_EXIT_PRESENT=$pngLoaderExitPresent"
            "LOADPNGSAFE_ENTER_PRESENT=$loadPngSafeEnterPresent"
            "LOADPNGSAFE_READ_FAIL_PRESENT=$loadPngSafeReadFailPresent"
            "LOADPNGSAFE_READ_NULL_PRESENT=$loadPngSafeReadNullPresent"
            "LOADPNGSAFE_READ_OK_PRESENT=$loadPngSafeReadOkPresent"
            "LOADPNGSAFE_DATA_TOO_SMALL_PRESENT=$loadPngSafeDataTooSmallPresent"
            "LOADPNGSAFE_BEFORE_PNGLOADER_LOAD_PRESENT=$loadPngSafeBeforePngLoaderLoadPresent"
            "LOADPNGSAFE_OK_PRESENT=$loadPngSafeOkPresent"
            "LOADPNGSAFE_NULL_PRESENT=$loadPngSafeNullPresent"
            "LOADPNGSAFE_EXIT_PRESENT=$loadPngSafeExitPresent"
            "LOADPNGSAFE_DEEPEST_MARKER=$loadPngSafeDeepestMarker"
            "CURSOR_VALIDATE_ENTER_PRESENT=$cursorValidateEnterPresent"
            "CURSOR_VALIDATE_OK_PRESENT=$cursorValidateOkPresent"
            "CURSOR_VALIDATE_NULL_PRESENT=$cursorValidateNullPresent"
            "CURSOR_VALIDATE_BAD_DIMS_PRESENT=$cursorValidateBadDimsPresent"
            "CURSOR_VALIDATE_BAD_RAWDATA_PRESENT=$cursorValidateBadRawDataPresent"
            "CURSOR_VALIDATE_EXIT_PRESENT=$cursorValidateExitPresent"
            "CURSOR_PNG_WIDTH=$cursorPngWidth"
            "CURSOR_PNG_HEIGHT=$cursorPngHeight"
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
