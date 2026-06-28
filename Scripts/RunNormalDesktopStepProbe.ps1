param(
    [int]$CaptureSeconds = 120
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

    Set-Alias -Name 'cmd.exe' -Value "$env:SystemRoot\System32\cmd.exe" -Scope Global

    Write-Host "[probe] Run ID: $runId" -ForegroundColor Cyan
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

    $faultLines = $serialLines | Where-Object { $_ -match 'CR2=|ERR=|RIP=|#PF|PAGE FAULT|FAULT|EXCEPTION' }
    $cr2Lines = $serialLines | Where-Object { $_ -match 'CR2=' }
    $errLines = $serialLines | Where-Object { $_ -match 'ERR=' }
    $ripLines = $serialLines | Where-Object { $_ -match 'RIP=' }

    if ($validRun) {
        Write-Host "[probe] Valid fresh run detected." -ForegroundColor Green
        Write-Host "[probe] Last step enter: $lastEnter" -ForegroundColor Green
        Write-Host "[probe] Matching exit present: $matchingExitPresent" -ForegroundColor Green
        Write-Host "[probe] NORM_STEP_003_EXIT present: $($serialText.Contains('NORM_STEP_003_EXIT'))" -ForegroundColor Green
        Write-Host "[probe] NORM_STEP_004_EXIT present: $($serialText.Contains('NORM_STEP_004_EXIT'))" -ForegroundColor Green
        if ($faultLines.Count -gt 0) {
            Write-Host "[probe] Fault lines:" -ForegroundColor Yellow
            $faultLines | ForEach-Object { Write-Host "[probe]   $_" -ForegroundColor Yellow }
        } else {
            Write-Host "[probe] Fault lines: none detected" -ForegroundColor Green
        }
        Write-Host "[probe] CR2 lines: $($cr2Lines.Count)" -ForegroundColor Green
        Write-Host "[probe] ERR lines: $($errLines.Count)" -ForegroundColor Green
        Write-Host "[probe] RIP lines: $($ripLines.Count)" -ForegroundColor Green

        $summary = @(
            "RUN_ID=$runId"
            "SERIAL_LOG=$serialLog"
            "LAST_ENTER=$lastEnter"
            "MATCHING_EXIT=$matchingExit"
            "MATCHING_EXIT_PRESENT=$matchingExitPresent"
            "NORM_STEP_003_EXIT_PRESENT=$($serialText.Contains('NORM_STEP_003_EXIT'))"
            "NORM_STEP_004_EXIT_PRESENT=$($serialText.Contains('NORM_STEP_004_EXIT'))"
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
    Remove-Item Alias:cmd.exe -ErrorAction SilentlyContinue

    if ($qemuProcess -and -not $qemuProcess.HasExited) {
        Stop-Process -Id $qemuProcess.Id -Force -ErrorAction SilentlyContinue
    }

    if ($programPatched) {
        [System.IO.File]::WriteAllText($programPath, $originalProgram)
        Write-Host "[probe] Restored Program.cs defaults." -ForegroundColor Cyan
    }
}
