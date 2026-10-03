[CmdletBinding()]
param(
    [ValidateRange(60, 7200)]
    [int]$TimeoutSeconds = 2400,
    [switch]$SkipBuild,
    [switch]$Phase35P2,
    [switch]$PreserveFailedImage
)

$ErrorActionPreference = 'Stop'
Set-Location (Split-Path -Parent $PSScriptRoot)
$root = (Get-Location).Path
$workDir = Join-Path $root 'out\phase35q'
New-Item -ItemType Directory -Path $workDir -Force | Out-Null

$runId = [Guid]::NewGuid().ToString('N')
$imageName = "durable-$runId.img"
$imagePath = Join-Path $workDir $imageName
$baselinePath = Join-Path $workDir "durable-$runId.baseline.img"
$varsPath = Join-Path $workDir "ovmf-vars-$runId.fd"
$codePath = Join-Path $workDir "ovmf-code-$runId.fd"
$serialName = "serial-$runId.log"
$serialPath = Join-Path $workDir $serialName
$qemuLogName = "qemu-$runId.log"
$qemuLogRelative = "out/phase35q/$qemuLogName"
$imageRelative = "out/phase35q/$imageName"
$serialRelative = "out/phase35q/$serialName"

$qemuPath = 'C:\Program Files\qemu\qemu-system-x86_64.exe'
$qemuCodeSource = 'C:\Program Files\qemu\share\edk2-x86_64-code.fd'
$qemuVarsSource = 'C:\Program Files\qemu\share\edk2-i386-vars.fd'
$python = (Get-Command python.exe -ErrorAction Stop).Source
$qemu = $null
$qmp = $null
$imageShaBefore = ''
$imageShaMutated = ''
$imageShaRestored = ''
$proofSucceeded = $false

function Get-Sha256([string]$Path) {
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToUpperInvariant()
}

function Read-QmpLine {
    param($Connection)
    $line = $Connection.Reader.ReadLine()
    if ($null -eq $line) { throw 'QMP connection closed.' }
    return $line | ConvertFrom-Json
}

function Send-QmpCommand {
    param($Connection, [string]$Command)
    $Connection.Writer.WriteLine((@{ execute = $Command } | ConvertTo-Json -Compress))
    while ($true) {
        $reply = Read-QmpLine $Connection
        if ($reply.error) { throw "QMP $Command failed: $($reply.error.desc)" }
        if ($null -ne $reply.PSObject.Properties['return']) { return $reply }
    }
}

function Connect-Qmp {
    param([int]$Port, $Process)
    for ($attempt = 0; $attempt -lt 100; $attempt++) {
        $Process.Refresh()
        if ($Process.HasExited) { throw "QEMU exited before QMP connected ($($Process.ExitCode))." }
        $client = [System.Net.Sockets.TcpClient]::new()
        try {
            $client.Connect('127.0.0.1', $Port)
            $client.ReceiveTimeout = 5000
            $stream = $client.GetStream()
            $stream.ReadTimeout = 5000
            $reader = [System.IO.StreamReader]::new($stream)
            $writer = [System.IO.StreamWriter]::new($stream)
            $writer.AutoFlush = $true
            $connection = @{ Client = $client; Reader = $reader; Writer = $writer }
            $null = Read-QmpLine $connection # greeting
            $null = Send-QmpCommand $connection 'qmp_capabilities'
            return $connection
        } catch {
            $client.Dispose()
            if ($attempt -eq 99) { throw }
            Start-Sleep -Milliseconds 100
        }
    }
}

function Get-MarkerCount([string]$Content, [string]$Pattern) {
    return [regex]::Matches($Content, $Pattern).Count
}

try {
    if (-not (Test-Path -LiteralPath $qemuPath)) { throw "QEMU not found: $qemuPath" }
    if (-not (Test-Path -LiteralPath $qemuCodeSource) -or -not (Test-Path -LiteralPath $qemuVarsSource)) {
        throw 'QEMU EDK2 firmware images were not found under C:\Program Files\qemu\share.'
    }

    if ($SkipBuild) {
        Write-Host 'Reusing the currently staged Storage35Q diagnostic kernel and EFI artifacts.'
    } else {
        $diagnosticMode = if ($Phase35P2) { 'Storage35P2' } else { 'Storage35Q' }
        Write-Host "Building the $diagnosticMode diagnostic kernel and ordinary EFI artifacts..."
        & (Join-Path $root 'build.ps1') -UefiDiagnosticMode $diagnosticMode
        if ($LASTEXITCODE -ne 0) { throw "build.ps1 failed with exit code $LASTEXITCODE" }
    }
    if (-not (Test-Path -LiteralPath (Join-Path $root 'ESP\EFI\BOOT\BOOTX64.EFI')) -or
        -not (Test-Path -LiteralPath (Join-Path $root 'ESP\kernel.elf'))) {
        throw 'The Storage35Q build did not stage the EFI boot image and kernel.'
    }

    Copy-Item -LiteralPath $qemuCodeSource -Destination $codePath
    Copy-Item -LiteralPath $qemuVarsSource -Destination $varsPath
    & $python (Join-Path $PSScriptRoot 'new_phase35q_fat16_image.py') $imagePath
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $imagePath)) {
        throw 'Could not create the unique disposable FAT16 test image.'
    }
    Copy-Item -LiteralPath $imagePath -Destination $baselinePath
    $imageShaBefore = Get-Sha256 $imagePath
    Write-Host "Disposable image: $imagePath"
    Write-Host "Initial image SHA-256: $imageShaBefore"

    $qmpPort = Get-Random -Minimum 44000 -Maximum 44999
    $qemuArgs = @(
        '-machine', 'pc-q35-8.2',
        '-drive', "if=pflash,format=raw,readonly=on,file=$($codePath.Replace('\','/'))",
        '-drive', "if=pflash,format=raw,file=$($varsPath.Replace('\','/'))",
        '-drive', 'if=none,id=esp,format=raw,file=fat:rw:ESP',
        '-device', 'ide-hd,drive=esp',
        '-drive', "if=none,id=phase35q,format=raw,cache=writeback,file=$imageRelative",
        '-device', 'ahci,id=phase35q-ahci',
        '-device', 'ide-hd,drive=phase35q,bus=phase35q-ahci.0,serial=GX35Q0001',
        '-m', '1024M',
        '-serial', "file:$serialRelative",
        '-name', 'guideXOS-Phase35Q',
        '-no-shutdown',
        '-d', 'guest_errors,cpu_reset',
        '-D', $qemuLogRelative,
        '-boot', 'menu=off,splash-time=0',
        '-display', 'none',
        '-qmp', "tcp:127.0.0.1:$qmpPort,server=on,wait=off"
    )
    $argumentString = $qemuArgs -join ' '
    Write-Host 'Starting QEMU with the generated image on the explicit AHCI test controller.'
    $qemu = Start-Process -FilePath $qemuPath -ArgumentList $argumentString `
        -WorkingDirectory $root -WindowStyle Hidden -PassThru
    $qmp = Connect-Qmp $qmpPort $qemu

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $resetCount = 0
    $bootCount = 0
    $appModelCompleteCount = 0
    $appModelResetSent = $false
    $lastProgress = Get-Date
    while ((Get-Date) -lt $deadline) {
        Start-Sleep -Milliseconds 250
        $qemu.Refresh()
        if ($qemu.HasExited) { throw "QEMU exited during proof ($($qemu.ExitCode))." }
        $content = if (Test-Path -LiteralPath $serialPath) {
            Get-Content -LiteralPath $serialPath -Raw -ErrorAction SilentlyContinue
        } else { '' }
        if ($null -eq $content) { $content = '' }

        if ($content -match '(?m)^35Q_FAIL=') {
            $failure = [regex]::Matches($content, '(?m)^35Q_FAIL=[^\r\n]*')[-1].Value
            throw "Guest durability proof failed: $failure"
        }
        if ($Phase35P2 -and $content -match '(?m)^APP_MODEL_FAIL=') {
            $failure = [regex]::Matches($content, '(?m)^APP_MODEL_FAIL=[^\r\n]*')[-1].Value
            throw "Guest Phase 35P2 App Model proof failed: $failure"
        }
        if ($content -match '(?im)(CPU_FAULT_[A-Z_]+|#PF|#GP|#UD|ABI.*PANIC|PANIC:|ALLOC_FREE_INVALID)') {
            $fault = [regex]::Match($content, '(?im)(CPU_FAULT_[A-Z_]+|#PF|#GP|#UD|ABI.*PANIC|PANIC:|ALLOC_FREE_INVALID)').Value
            throw "Guest fault or allocator invariant was reported: $fault"
        }

        $currentBootCount = Get-MarkerCount $content '(?m)^35Q_BOOT_STAGE='
        if ($currentBootCount -gt $bootCount) {
            $bootCount = $currentBootCount
            Write-Host "  guest boot $bootCount reached the durable-storage diagnostic"
            $lastProgress = Get-Date
        }

        $requestCount = Get-MarkerCount $content '(?m)^35Q_REBOOT_REQUEST='
        while ($resetCount -lt $requestCount) {
            Write-Host "  resetting guest with the same backing image (request $($resetCount + 1))"
            $null = Send-QmpCommand $qmp 'system_reset'
            $resetCount++
            $lastProgress = Get-Date
        }

        if ($Phase35P2) {
            $currentAppModelCompleteCount = Get-MarkerCount $content '(?m)^APP_MODEL_COMPLETE\r?$'
            if ($currentAppModelCompleteCount -gt $appModelCompleteCount) {
                $appModelCompleteCount = $currentAppModelCompleteCount
                Write-Host "  App Model storage proof completed on guest boot $appModelCompleteCount"
                if ($appModelCompleteCount -eq 1 -and -not $appModelResetSent) {
                    Write-Host '  resetting guest after the first Persistent seed and lifecycle proof'
                    $null = Send-QmpCommand $qmp 'system_reset'
                    $appModelResetSent = $true
                } elseif ($appModelCompleteCount -ge 2) {
                    $proofSucceeded = $true
                    break
                }
                $lastProgress = Get-Date
            }
        } elseif ($content -match '(?m)^35Q_COMPLETE=1') {
            $proofSucceeded = $true
            break
        }

        if (((Get-Date) - $lastProgress).TotalSeconds -ge 45) {
            Write-Host '  waiting for guest disk I/O or the next same-image reboot'
            $lastProgress = Get-Date
        }
    }
    if (-not $proofSucceeded) { throw "Storage35Q guest proof timed out after $TimeoutSeconds seconds." }

    # Close QEMU before hashing the image; Windows holds the opened drive file
    # exclusively while the VM is running.
    $qmp.Writer.WriteLine((@{ execute = 'quit' } | ConvertTo-Json -Compress))
    if (-not $qemu.WaitForExit(15000)) { throw 'QEMU did not exit cleanly after proof completion.' }
    $qmp.Client.Dispose()
    $qmp = $null
    $qemu.Close()
    $qemu = $null

    $finalContent = Get-Content -LiteralPath $serialPath -Raw
    $requiredMarkers = @(
        '35Q_RDSKFS_BOOT=PASS',
        '35Q_RANGE_VALIDATION=PASS',
        '35Q_READ_ONLY_CONTRACT=PASS',
        '35Q_USB_WRITE_POLICY=READ_ONLY',
        '35Q_RAW_WRITE_FLUSH_READ_CYCLES=25:PASS',
        '35Q_RAW_POST_REBOOT=PASS',
        '35Q_RAW_RESTORE_POST_REBOOT=PASS',
        '35Q_FAT_MOUNT=PASS:FAT16',
        '35Q_FAT_POST_REBOOT_A=PASS:',
        '35Q_FAT_POST_REBOOT_B=PASS:',
        '35Q_FAT_POST_REBOOT_LONGER=65536,',
        '35Q_FAT_DIRECTORY_POST_REBOOT=PASS',
        '35Q_COMPLETE=1'
    )
    if ($Phase35P2) {
        $requiredMarkers += @(
            'PHASE35P2_BACKEND_AVAILABLE=1',
            'PHASE35P2_BACKEND_WRITABLE=1',
            'PHASE35P2_SELECTED_VOLUME=GX35Q0001,filesystem=FAT16,label=GX35Q TEST,volumeId=35355131',
            'PHASE35P2_ROOT=apps/persist',
            'PHASE35P2_FIXTURE_SHA256=BEFA57E7EF0799D031A0188A3D0883F0F342B8F8AE90B3330652DA04ADBA739D',
            'PHASE35P2_NAMESPACE_ENCODING=PASS',
            'PHASE35P2_CROSS_SCOPE=PASS',
            'PHASE35P2_STALE_CONTEXT=PASS',
            'PHASE35P2_READ_WRITE_LIFECYCLE=PASS',
            'PHASE35P2_VALUE_BOUNDARIES=PASS',
            'PHASE35P2_OFFSET_BOUNDARIES=PASS',
            'PHASE35P2_DELETE=PASS',
            'PHASE35P2_FAILURE_PROPAGATION=PASS',
            'PHASE35P2_ENUMERATE=PASS',
            'PHASE35P2_RESET=PASS',
            'PHASE35P2_REBOOT_DELETE=PASS',
            'PHASE35P2_TEMPORARY_SEPARATION=PASS',
            'PHASE35P2_TEMPORARY_RESET=PASS',
            'APP_MODEL_COMPLETE'
        )
    }
    foreach ($marker in $requiredMarkers) {
        if ($finalContent.IndexOf($marker, [System.StringComparison]::Ordinal) -lt 0) {
            throw "Required guest marker is missing: $marker"
        }
    }
    $allocatorRows = [regex]::Matches($finalContent, '(?m)^35Q_ALLOCATOR_COUNTS=[^\r\n]*')
    if ($allocatorRows.Count -lt 6) { throw "Expected six allocator invariant snapshots; saw $($allocatorRows.Count)." }
    foreach ($row in $allocatorRows) {
        if ($row.Value -notmatch 'freeInvalid=0,freeCorrupt=0,freeNoPages=0$') {
            throw "Allocator invariant failed: $($row.Value)"
        }
    }
    $rawCycleMarker = [regex]::Match($finalContent, '(?m)^35Q_RAW_WRITE_FLUSH_READ_CYCLES=([0-9]+):PASS\r?$')
    if (-not $rawCycleMarker.Success -or [int]$rawCycleMarker.Groups[1].Value -ne 25) {
        throw 'The raw durability cycle count did not equal 25.'
    }
    if ($Phase35P2) {
        $seedPreflightRows = [regex]::Matches($finalContent,
            '(?m)^PHASE35P2_SEED_PREFLIGHT=[^\r\n]*')
        if ($seedPreflightRows.Count -lt 2 -or
                $seedPreflightRows[0].Value -notmatch 'status=Seeded,seedWrites=1,sha256=BEFA57E7EF0799D031A0188A3D0883F0F342B8F8AE90B3330652DA04ADBA739D$' -or
                $seedPreflightRows[1].Value -notmatch 'status=Verified,seedWrites=0,sha256=BEFA57E7EF0799D031A0188A3D0883F0F342B8F8AE90B3330652DA04ADBA739D$') {
            throw 'The fixture seed state was not logged before reads, or the verification boot reseeded/mismatched it.'
        }
        if ((Get-MarkerCount $finalContent '(?m)^APP_MODEL_COMPLETE\r?$') -lt 2 -or
                (Get-MarkerCount $finalContent '(?m)^35Q_COMPLETE=1\r?$') -lt 2) {
            throw 'Phase 35P2 did not complete App Model verification before and after the same-image guest reset.'
        }
        if ($finalContent -notmatch '(?m)^PHASE35P2_FIXTURE=selftest\.phase10\.persistent/state\.bin,status=Seeded,seedWrites=1\r?$' -or
                $finalContent -notmatch '(?m)^PHASE35P2_FIXTURE=selftest\.phase10\.persistent/state\.bin,status=Verified,seedWrites=0\r?$') {
            throw 'The fixture was not seeded once and verified without reseeding after reboot.'
        }
        if ($finalContent -notmatch '(?m)^PHASE35P2_REBOOT_DELETE=PASS\r?$') {
            throw 'The explicit Persistent Delete was not observed as absent after reboot.'
        }
    }

    $imageShaMutated = Get-Sha256 $imagePath
    Write-Host "QEMU proof complete; image SHA-256 after guest writes: $imageShaMutated"
} finally {
    if ($qemu -and -not $qemu.HasExited) {
        try {
            if ($qmp) { $null = Send-QmpCommand $qmp 'quit' }
        } catch { }
        try {
            $qemu.Refresh()
            if (-not $qemu.HasExited) {
                Stop-Process -Id $qemu.Id -Force -ErrorAction SilentlyContinue
            }
            $null = $qemu.WaitForExit(10000)
        } catch { }
    }
    if ($qmp -and $qmp.Client) { $qmp.Client.Dispose() }

    if ($PreserveFailedImage -and -not $proofSucceeded -and
            (Test-Path -LiteralPath $imagePath)) {
        Write-Host "Preserving failed disposable image for inspection: $imagePath"
        if (Test-Path -LiteralPath $baselinePath) {
            Remove-Item -LiteralPath $baselinePath -Force
        }
    } elseif ((Test-Path -LiteralPath $baselinePath) -and (Test-Path -LiteralPath $imagePath)) {
        [System.IO.File]::Copy($baselinePath, $imagePath, $true)
        $imageShaRestored = Get-Sha256 $imagePath
        if ($imageShaBefore -and $imageShaRestored -ne $imageShaBefore) {
            throw "Test image restoration failed: before=$imageShaBefore after=$imageShaRestored"
        }
        Remove-Item -LiteralPath $imagePath -Force
        Remove-Item -LiteralPath $baselinePath -Force
    }
    Remove-Item -LiteralPath $varsPath, $codePath -Force -ErrorAction SilentlyContinue
}

if (-not $proofSucceeded) { throw 'Storage35Q proof did not complete.' }
Write-Host "Pristine fixture SHA-256 restored before cleanup: $imageShaRestored"
Write-Host "Serial proof log retained: $serialPath"
if ($Phase35P2) {
    Write-Host 'Storage35Q and Phase35P2 same-image reboot validation completed.'
} else {
    Write-Host 'Storage35Q same-image reboot validation completed.'
}
