param(
    [int]$CaptureSeconds = 120,
    [ValidateSet('DrawImage', 'FillRectangle')]
    [string]$Step10RedMode = 'DrawImage',
    [ValidateSet('DrawImage', 'FillRectangle')]
    [string]$Step10GreenMode = 'FillRectangle',
    [ValidateSet('DrawImage', 'FillRectangle')]
    [string]$Step10WhiteMode = 'FillRectangle',
    [switch]$SkipWindowTraversal,
    [switch]$SkipCursorDraw,
    [switch]$CursorPlaceholder
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

$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$programPath = Join-Path $root 'guideXOS\Program.cs'
$buildScript = Join-Path $root 'build.ps1'
$qemuExe = 'C:\Program Files\qemu\qemu-system-x86_64.exe'
$probeRoot = Join-Path $root 'bin\probe-logs'
$runStamp = Get-Date -Format 'yyyyMMdd_HHmmss'
$runId = "STEP_PROBE_RUN_ID_$runStamp"
$serialLog = Join-Path $probeRoot "serial_output_step_probe_$runId.txt"
$stderrLog = Join-Path $probeRoot "qemu_stderr_step_probe_$runId.txt"
$summaryLog = Join-Path $probeRoot "step_probe_summary_$runId.txt"

New-Item -ItemType Directory -Path $probeRoot -Force | Out-Null

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

    Write-Host "[probe] Run ID: $runId" -ForegroundColor Cyan
    Write-Host "[probe] Step 10 red mode: $Step10RedMode" -ForegroundColor Cyan
    Write-Host "[probe] Step 10 green mode: $Step10GreenMode" -ForegroundColor Cyan
    Write-Host "[probe] Step 10 white mode: $Step10WhiteMode" -ForegroundColor Cyan
    Write-Host "[probe] Step 11 skip window traversal: $SkipWindowTraversal" -ForegroundColor Cyan
    Write-Host "[probe] Step 13 skip cursor draw: $SkipCursorDraw" -ForegroundColor Cyan
    Write-Host "[probe] Step 13 cursor placeholder: $CursorPlaceholder" -ForegroundColor Cyan
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

    # The local PowerShell host can surface both Path and PATH; QEMU Start-Process
    # inherits that duplicate environment and fails before launch. Keep the canonical
    # Path entry and drop PATH for the probe child process.
    Remove-Item Env:PATH -ErrorAction SilentlyContinue

    Write-Host "[probe] Launching QEMU with the repo's pflash UEFI path..." -ForegroundColor Cyan
    Write-Host "[probe] Serial log: $serialLog" -ForegroundColor Cyan

    $serialRelative = $serialLog.Substring($root.Length).TrimStart('\', '/') -replace '\\', '/'

    $qemuArgs = @(
        '-drive', 'if=pflash,format=raw,readonly=on,file=OVMF.fd'
        '-drive', 'file=fat:rw:ESP,format=raw'
        '-m', '1024M'
        '-serial', ('file:' + $serialRelative)
        '-no-reboot'
        '-name', 'guideXOS'
    )

    $qemuProcess = Start-Process -FilePath $qemuExe `
        -ArgumentList $qemuArgs `
        -WorkingDirectory $root `
        -WindowStyle Hidden `
        -PassThru `
        -RedirectStandardError $stderrLog

    $deadline = (Get-Date).AddSeconds($CaptureSeconds)
    while ((Get-Date) -lt $deadline) {
        if ($qemuProcess.HasExited) {
            break
        }

        Start-Sleep -Milliseconds 500
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
    $step14EnterPresent = $serialText.Contains('NORM_STEP_014_ENTER')
    $step14ExitPresent = $serialText.Contains('NORM_STEP_014_EXIT')
    $step13SkipCursorDrawEnabled = $serialText.Contains('SMAIN_DIAG_NORMAL_DESKTOP_STEP_PROBE_SKIP_CURSOR_DRAW=1')
    $step13CursorPlaceholderEnabled = $serialText.Contains('SMAIN_DIAG_NORMAL_DESKTOP_STEP_PROBE_CURSOR_PLACEHOLDER=1')
    $step13AEnterPresent = $serialText.Contains('NORM_STEP_013_A_ENTER')
    $step13AExitPresent = $serialText.Contains('NORM_STEP_013_A_EXIT')
    $step13BEnterPresent = $serialText.Contains('NORM_STEP_013_B_ENTER')
    $step13BExitPresent = $serialText.Contains('NORM_STEP_013_B_EXIT')
    $step13CEnterPresent = $serialText.Contains('NORM_STEP_013_C_ENTER')
    $step13CExitPresent = $serialText.Contains('NORM_STEP_013_C_EXIT')
    $step13CursorEnabledPresent = $serialText.Contains('NORM_STEP_013_CURSOR_ENABLED=1') -or $serialText.Contains('NORM_STEP_013_CURSOR_ENABLED=0')
    $step13CursorEnabledOnPresent = $serialText.Contains('NORM_STEP_013_CURSOR_ENABLED=1')
    $step13CursorEnabledOffPresent = $serialText.Contains('NORM_STEP_013_CURSOR_ENABLED=0')
    $step13CursorPlaceholderStatePresent = $serialText.Contains('NORM_STEP_013_CURSOR_PLACEHOLDER=1') -or $serialText.Contains('NORM_STEP_013_CURSOR_PLACEHOLDER=0')
    $step13CursorPlaceholderOnPresent = $serialText.Contains('NORM_STEP_013_CURSOR_PLACEHOLDER=1')
    $step13CursorPlaceholderOffPresent = $serialText.Contains('NORM_STEP_013_CURSOR_PLACEHOLDER=0')
    $step13CursorPlaceholderEnterPresent = $serialText.Contains('NORM_STEP_013_CURSOR_PLACEHOLDER_ENTER')
    $step13CursorPlaceholderExitPresent = $serialText.Contains('NORM_STEP_013_CURSOR_PLACEHOLDER_EXIT')
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
        Write-Host "[probe] Step 13 skip cursor draw enabled: $step13SkipCursorDrawEnabled" -ForegroundColor Green
        Write-Host "[probe] Step 13 cursor placeholder enabled: $step13CursorPlaceholderEnabled" -ForegroundColor Green
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
        Write-Host "[probe] NORM_STEP_013_CURSOR_PLACEHOLDER_ENTER present: $step13CursorPlaceholderEnterPresent" -ForegroundColor Green
        Write-Host "[probe] NORM_STEP_013_CURSOR_PLACEHOLDER_EXIT present: $step13CursorPlaceholderExitPresent" -ForegroundColor Green
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
            "STEP13_SKIP_CURSOR_DRAW_ENABLED=$step13SkipCursorDrawEnabled"
            "STEP13_CURSOR_PLACEHOLDER_ENABLED=$step13CursorPlaceholderEnabled"
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
            "NORM_STEP_013_A_ENTER_PRESENT=$step13AEnterPresent"
            "NORM_STEP_013_A_EXIT_PRESENT=$step13AExitPresent"
            "NORM_STEP_013_B_ENTER_PRESENT=$step13BEnterPresent"
            "NORM_STEP_013_B_EXIT_PRESENT=$step13BExitPresent"
            "NORM_STEP_013_C_ENTER_PRESENT=$step13CEnterPresent"
            "NORM_STEP_013_C_EXIT_PRESENT=$step13CExitPresent"
            "NORM_STEP_013_CURSOR_ENABLED_PRESENT=$step13CursorEnabledPresent"
            "NORM_STEP_013_CURSOR_ENABLED_ON_PRESENT=$step13CursorEnabledOnPresent"
            "NORM_STEP_013_CURSOR_ENABLED_OFF_PRESENT=$step13CursorEnabledOffPresent"
            "NORM_STEP_013_CURSOR_PLACEHOLDER_PRESENT=$step13CursorPlaceholderStatePresent"
            "NORM_STEP_013_CURSOR_PLACEHOLDER_ON_PRESENT=$step13CursorPlaceholderOnPresent"
            "NORM_STEP_013_CURSOR_PLACEHOLDER_OFF_PRESENT=$step13CursorPlaceholderOffPresent"
            "NORM_STEP_013_CURSOR_PLACEHOLDER_ENTER_PRESENT=$step13CursorPlaceholderEnterPresent"
            "NORM_STEP_013_CURSOR_PLACEHOLDER_EXIT_PRESENT=$step13CursorPlaceholderExitPresent"
            "NORM_STEP_013_CURSOR_DRAW_ENTER_PRESENT=$step13CursorDrawEnterPresent"
            "NORM_STEP_013_CURSOR_DRAW_PRIMITIVE_ENTER_PRESENT=$step13CursorDrawPrimitiveEnterPresent"
            "NORM_STEP_013_CURSOR_DRAW_PRIMITIVE_EXIT_PRESENT=$step13CursorDrawPrimitiveExitPresent"
            "NORM_STEP_013_CURSOR_DRAW_EXIT_PRESENT=$step13CursorDrawExitPresent"
            "NORM_STEP_013_CURSOR_DRAW_SKIPPED_PRESENT=$step13CursorDrawSkippedPresent"
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
