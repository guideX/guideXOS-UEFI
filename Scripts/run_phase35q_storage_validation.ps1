[CmdletBinding()]
param(
    [ValidateRange(60, 7200)]
    [int]$TimeoutSeconds = 2400,
    [switch]$SkipBuild,
    [switch]$UseFreshlyBuiltKernel,
    [switch]$Phase35P2,
    [switch]$Phase35R,
    [switch]$Phase35R2Matrix,
    [switch]$Phase35R9Ledger,
    [switch]$Phase35R13AllocLedger,
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
$qemuStdoutPath = Join-Path $workDir "qemu-stdout-$runId.log"
$qemuStderrPath = Join-Path $workDir "qemu-stderr-$runId.log"
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

$selectedPhase35ModeCount = 0
if ($Phase35P2) { $selectedPhase35ModeCount++ }
if ($Phase35R) { $selectedPhase35ModeCount++ }
if ($Phase35R2Matrix) { $selectedPhase35ModeCount++ }
if ($Phase35R9Ledger) { $selectedPhase35ModeCount++ }
if ($Phase35R13AllocLedger) { $selectedPhase35ModeCount++ }
if ($selectedPhase35ModeCount -gt 1) {
    throw 'Select only one Phase 35 proof mode.'
}

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

    if ($SkipBuild -and $UseFreshlyBuiltKernel) {
        throw 'Select either SkipBuild or UseFreshlyBuiltKernel, not both.'
    }
    if ($UseFreshlyBuiltKernel) {
        $kernelPath = Join-Path $root 'ESP\kernel.elf'
        if (-not (Test-Path -LiteralPath $kernelPath)) {
            throw "Freshly built diagnostic kernel is absent: $kernelPath"
        }
        $kernelHash = Get-Sha256 $kernelPath
        Write-Host "Using the explicitly rebuilt and staged diagnostic kernel: SHA-256 $kernelHash"
    } elseif ($SkipBuild) {
        Write-Host 'Reusing the currently staged diagnostic kernel and EFI artifacts.'
    } else {
        $diagnosticMode = if ($Phase35R13AllocLedger) { 'Ring3Phase35R13' } elseif ($Phase35R9Ledger) { 'Ring3Phase35R9' } elseif ($Phase35R2Matrix) { 'Ring3Phase35R2' } elseif ($Phase35R) { 'Ring3Phase35' } elseif ($Phase35P2) { 'Storage35P2' } else { 'Storage35Q' }
        Write-Host "Building the $diagnosticMode diagnostic kernel and ordinary EFI artifacts..."
        & (Join-Path $root 'build.ps1') -UefiDiagnosticMode $diagnosticMode
        if ($LASTEXITCODE -ne 0) { throw "build.ps1 failed with exit code $LASTEXITCODE" }
    }
    if (-not (Test-Path -LiteralPath (Join-Path $root 'ESP\EFI\BOOT\BOOTX64.EFI')) -or
        -not (Test-Path -LiteralPath (Join-Path $root 'ESP\kernel.elf'))) {
        throw 'The Storage35Q build did not stage the EFI boot image and kernel.'
    }
    $kernelPath = Join-Path $root 'ESP\kernel.elf'
    $ramdiskPath = Join-Path $root 'ramdisk.img'
    $kernelHash = Get-Sha256 $kernelPath
    $ramdiskHash = Get-Sha256 $ramdiskPath
    Write-Host "Diagnostic kernel SHA-256: $kernelHash"
    Write-Host "Diagnostic ramdisk SHA-256: $ramdiskHash"

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
        # The UEFI allocator spans physical 64 MiB through 1088 MiB. Keep the
        # complete arena backed by guest RAM during the repeated-process proof.
        '-m', '1280M',
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
        -WorkingDirectory $root -WindowStyle Hidden -PassThru `
        -RedirectStandardOutput $qemuStdoutPath `
        -RedirectStandardError $qemuStderrPath
    $qmp = Connect-Qmp $qmpPort $qemu

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $resetCount = 0
    $bootCount = 0
    $appModelCompleteCount = 0
    $appModelResetSent = $false
    $phase35CompleteCount = 0
    $phase35ResetSent = $false
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
        if ($Phase35R -and $content -match '(?m)^PHASE35_COMPLETE=0\r?$') {
            throw 'Guest managed Persistent-read proof reported PHASE35_COMPLETE=0.'
        }
        if ($Phase35R -and $content -match '(?m)^RING3_PHASE35_COMPLETE=0\r?$') {
            throw 'Guest managed Persistent-read proof reported RING3_PHASE35_COMPLETE=0.'
        }
        if ($Phase35R2Matrix -and $content -match '(?m)^PHASE35_ALLOC_MATRIX_RESULT=[^,]+,FAIL\r?$') {
            throw 'Guest allocator matrix reported a failed requester.'
        }
        if ($Phase35R13AllocLedger -and $content -match '(?m)^PHASE35_R13_AUDIT_COMPLETE=0\r?$') {
            throw 'Guest Phase 35R13 allocator audit reported an incomplete requester run.'
        }
        if ($content -match '(?im)(CPU_FAULT_[A-Z_]+|#PF|#GP|#UD|ABI.*PANIC|PANIC:|ALLOC_FREE_INVALID=(?:0x0*[1-9A-Fa-f][0-9A-Fa-f]*|[1-9][0-9]*))') {
            $fault = [regex]::Match($content, '(?im)(CPU_FAULT_[A-Z_]+|#PF|#GP|#UD|ABI.*PANIC|PANIC:|ALLOC_FREE_INVALID=(?:0x0*[1-9A-Fa-f][0-9A-Fa-f]*|[1-9][0-9]*))').Value
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

        if ($Phase35R13AllocLedger) {
            if ($content -match '(?m)^PHASE35_R13_AUDIT_COMPLETE=1\r?$') {
                $proofSucceeded = $true
                break
            }
        } elseif ($Phase35R2Matrix) {
            if ($content -match '(?m)^PHASE35_ALLOC_MATRIX_COMPLETE=1\r?$') {
                $proofSucceeded = $true
                break
            }
        } elseif ($Phase35R9Ledger) {
            if ($content -match '(?m)^PHASE35_R9_RUN_LEDGER_COMPLETE=1\r?$') {
                $proofSucceeded = $true
                break
            }
        } elseif ($Phase35R) {
            $currentAppModelCompleteCount = Get-MarkerCount $content '(?m)^APP_MODEL_COMPLETE\r?$'
            if ($currentAppModelCompleteCount -gt $appModelCompleteCount) {
                $appModelCompleteCount = $currentAppModelCompleteCount
                Write-Host "  App Model storage regression completed on guest boot $appModelCompleteCount"
                $lastProgress = Get-Date
            }

            $currentPhase35CompleteCount = Get-MarkerCount $content '(?m)^RING3_PHASE35_COMPLETE=1\r?$'
            if ($currentPhase35CompleteCount -gt $phase35CompleteCount) {
                $phase35CompleteCount = $currentPhase35CompleteCount
                Write-Host "  managed Persistent-read proof completed on guest boot $phase35CompleteCount"
                $lastProgress = Get-Date
            }

            if ($appModelCompleteCount -ge 1 -and $phase35CompleteCount -ge 1 -and
                    -not $phase35ResetSent) {
                Write-Host '  resetting after the first managed proof to verify the same image without reseeding'
                $null = Send-QmpCommand $qmp 'system_reset'
                $phase35ResetSent = $true
                $lastProgress = Get-Date
            } elseif ($appModelCompleteCount -ge 2 -and
                    $phase35CompleteCount -ge 2 -and $phase35ResetSent) {
                $proofSucceeded = $true
                break
            }
        } elseif ($Phase35P2) {
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
    if ($Phase35R13AllocLedger) {
        $requiredMarkers = @(
            'ALLOC_PROVENANCE_READY=1;capacity=262144-live-runs;storage=static-per-page',
            'PHASE35_PERSISTENT_BACKEND_READY=1',
            'PHASE35_R13_ALLOC_LEDGER_READY=1',
            'PHASE35_R13_STRESS_EQUIVALENT_PASS=1',
            'PHASE35_R13_REPEAT_PASS=1',
            'PHASE35_R13_NO_READ_PASS=1',
            'PHASE35_R13_ONE_READ_PASS=1',
            'PHASE35_R13_NOT_FOUND_PASS=1',
            'PHASE35_R13_FIVE_LIFETIME_PASS=1',
            'PHASE35_25_LIFETIME_STRESS=PASS',
            'PHASE35_STRESS_ALLOCATOR_STABLE=1',
            'PHASE35_ACTIVE_STORAGE_REQUESTS=0',
            'PHASE35_OPEN_PERSISTENT_HANDLES=0',
            'PHASE35_FREE_INVALID=0',
            'PHASE35_FREE_CORRUPT=0',
            'PHASE35_FREE_NO_PAGES=0',
            'PHASE35_R13_8MIB_ALLOCATION=PASS',
            'PHASE35_R13_AUDIT_COMPLETE=1')
        $r13 = @{}
        $scenario = 0
        $inRunTable = $false
        $finalRunTableRows = 0
        foreach ($line in ($finalContent -split "`r?`n")) {
            if ($line -match '^R13_SCENARIO=0x([0-9A-Fa-f]+)$') {
                $scenario = [Convert]::ToInt32($Matches[1], 16)
                if (-not $r13.ContainsKey($scenario)) {
                    $r13[$scenario] = [pscustomobject]@{
                        B0Bytes = $null
                        B1Bytes = $null
                        B2ProcessBytes = $null
                        B2Bytes = $null
                        MainReturn = $null
                        OwnerHandle = $null
                        OwnerGeneration = $null
                        DeltaPages = $null
                        AccountedPages = [UInt64]0
                        UnexplainedPages = $null
                        OveraccountedPages = $null
                        PersistentReads = $null
                        PersistentSuccesses = $null
                        PersistentFailures = $null
                        LifetimePass = $false
                        SiteTotals = [System.Collections.Generic.List[object]]::new()
                        RunRows = [System.Collections.Generic.List[string]]::new()
                        RunCount = 0
                    }
                }
            } elseif ($scenario -ne 0 -and
                    $line -match '^R13_OWNER_HANDLE=0x([0-9A-Fa-f]+)$') {
                $r13[$scenario].OwnerHandle = [Convert]::ToUInt64(
                    $Matches[1], 16)
            } elseif ($scenario -ne 0 -and
                    $line -match '^R13_OWNER_GENERATION=0x([0-9A-Fa-f]+)$') {
                $r13[$scenario].OwnerGeneration = [Convert]::ToUInt64(
                    $Matches[1], 16)
            } elseif ($scenario -ne 0 -and
                    $line -match '^R13_B0_BYTES=0x([0-9A-Fa-f]+)$') {
                $r13[$scenario].B0Bytes = [Convert]::ToUInt64(
                    $Matches[1], 16)
            } elseif ($scenario -ne 0 -and
                    $line -match '^R13_B1_BYTES=0x([0-9A-Fa-f]+)$') {
                $r13[$scenario].B1Bytes = [Convert]::ToUInt64(
                    $Matches[1], 16)
            } elseif ($scenario -ne 0 -and
                    $line -match '^R13_B2_PROCESS_BYTES=0x([0-9A-Fa-f]+)$') {
                $r13[$scenario].B2ProcessBytes = [Convert]::ToUInt64(
                    $Matches[1], 16)
            } elseif ($scenario -ne 0 -and
                    $line -match '^R13_B2_BYTES=0x([0-9A-Fa-f]+)$') {
                $r13[$scenario].B2Bytes = [Convert]::ToUInt64(
                    $Matches[1], 16)
            } elseif ($scenario -ne 0 -and
                    $line -match '^R13_MAIN_RETURN=(-?[0-9]+)$') {
                $r13[$scenario].MainReturn = [int]$Matches[1]
            } elseif ($scenario -ne 0 -and
                    $line -match '^R13_B2_MINUS_B0_PAGES=0x([0-9A-Fa-f]+)$') {
                $r13[$scenario].DeltaPages = [Convert]::ToUInt64(
                    $Matches[1], 16)
            } elseif ($scenario -ne 0 -and
                    $line -match '^R13_UNEXPLAINED_PAGES=0x([0-9A-Fa-f]+)$') {
                $r13[$scenario].UnexplainedPages = [Convert]::ToUInt64(
                    $Matches[1], 16)
            } elseif ($scenario -ne 0 -and
                    $line -match '^R13_OVERACCOUNTED_PAGES=0x([0-9A-Fa-f]+)$') {
                $r13[$scenario].OveraccountedPages = [Convert]::ToUInt64(
                    $Matches[1], 16)
            } elseif ($scenario -ne 0 -and
                    $line -match '^R13_PERSISTENT_READS=([0-9]+)$') {
                $r13[$scenario].PersistentReads = [int]$Matches[1]
            } elseif ($scenario -ne 0 -and
                    $line -match '^R13_PERSISTENT_SUCCESSES=([0-9]+)$') {
                $r13[$scenario].PersistentSuccesses = [int]$Matches[1]
            } elseif ($scenario -ne 0 -and
                    $line -match '^R13_PERSISTENT_FAILURES=([0-9]+)$') {
                $r13[$scenario].PersistentFailures = [int]$Matches[1]
            } elseif ($scenario -ne 0 -and
                    $line -eq 'PHASE35_R13_LIFETIME_PASS=1') {
                $r13[$scenario].LifetimePass = $true
            } elseif ($line -eq 'PHASE35_R13_RUNS_BEGIN=1') {
                $inRunTable = $true
            } elseif ($line -eq 'PHASE35_R13_RUNS_END=1') {
                $inRunTable = $false
            } elseif ($inRunTable -and $scenario -ne 0 -and
                    $line -match '^R13_SITE_TOTAL;phase=0x([0-9A-Fa-f]+);site=0x([0-9A-Fa-f]+);allocations=0x([0-9A-Fa-f]+);frees=0x([0-9A-Fa-f]+);survivors=0x([0-9A-Fa-f]+);pages=0x([0-9A-Fa-f]+)$') {
                $r13[$scenario].SiteTotals.Add([pscustomobject]@{
                    Phase = [Convert]::ToUInt64($Matches[1], 16)
                    Site = [Convert]::ToUInt64($Matches[2], 16)
                    Allocations = [Convert]::ToUInt64($Matches[3], 16)
                    Frees = [Convert]::ToUInt64($Matches[4], 16)
                    Survivors = [Convert]::ToUInt64($Matches[5], 16)
                    Pages = [Convert]::ToUInt64($Matches[6], 16)
                })
            } elseif ($inRunTable -and $scenario -ne 0 -and
                    $line -match '^R9_RUN;phase=([0-9A-Fa-f]+);' -and
                    [Convert]::ToUInt64($Matches[1], 16) -eq 2 -and
                    $line -match ';pages=0x([0-9A-Fa-f]+);' -and
                    $scenario -ne 0) {
                $r13[$scenario].AccountedPages += [Convert]::ToUInt64(
                    $Matches[1], 16)
                $r13[$scenario].RunCount++
                $r13[$scenario].RunRows.Add($line)
                $finalRunTableRows++
            }
        }
        foreach ($id in 1..35) {
            if (-not $r13.ContainsKey($id) -or
                    $null -eq $r13[$id].DeltaPages -or
                    $null -eq $r13[$id].MainReturn -or
                    -not $r13[$id].LifetimePass) {
                throw "R13 scenario $id is missing its lifetime result or measured delta."
            }
            $s = $r13[$id]
            if ($s.MainReturn -ne 35) {
                throw "R13 scenario $id returned $($s.MainReturn), expected 35."
            }
            if ($s.RunCount -eq 0) {
                if ($s.DeltaPages -ne 0 -or $s.SiteTotals.Count -eq 0) {
                    throw "R13 scenario $id has no retained runs but lacks a zero-delta site census."
                }
                foreach ($siteTotal in $s.SiteTotals) {
                    if ($siteTotal.Phase -ne 2 -or
                            $siteTotal.Allocations -ne $siteTotal.Frees -or
                            $siteTotal.Survivors -ne 0 -or
                            $siteTotal.Pages -ne 0) {
                        throw "R13 scenario $id failed the zero-survivor site census at site $($siteTotal.Site)."
                    }
                }
            }
            $unexplained = if ($s.DeltaPages -gt $s.AccountedPages) {
                $s.DeltaPages - $s.AccountedPages
            } else { [UInt64]0 }
            $overAccounted = if ($s.AccountedPages -gt $s.DeltaPages) {
                $s.AccountedPages - $s.DeltaPages
            } else { [UInt64]0 }
            Write-Host "R13 scenario $id : owner=0x$('{0:X}' -f $s.OwnerHandle) generation=$($s.OwnerGeneration) B0=$($s.B0Bytes) B1=$($s.B1Bytes) B2Process=$($s.B2ProcessBytes) B2=$($s.B2Bytes) measuredPages=$($s.DeltaPages) accountedPages=$($s.AccountedPages) unexplainedPages=$unexplained overAccountedPages=$overAccounted retainedRuns=$($s.RunCount) zeroSurvivorCensus=$($s.RunCount -eq 0) persistentReads=$($s.PersistentReads) successes=$($s.PersistentSuccesses) failures=$($s.PersistentFailures)"
            if ($s.AccountedPages -ne $s.DeltaPages -or
                    $s.UnexplainedPages -ne 0 -or
                    $s.OveraccountedPages -ne 0) {
                throw "R13 scenario $id failed live-run page reconciliation."
            }
            foreach ($siteTotal in $s.SiteTotals) {
                Write-Host "R13 site scenario=$id site=$($siteTotal.Site) allocations=$($siteTotal.Allocations) frees=$($siteTotal.Frees) survivors=$($siteTotal.Survivors) pages=$($siteTotal.Pages)"
            }
            foreach ($runRow in $s.RunRows) {
                Write-Host "R13 retained run scenario=$id $runRow"
            }
        }
        Write-Host "R13 retained-run rows across 35 requester lifetimes: $finalRunTableRows"
        $faultVectorMatches = [regex]::Matches($finalContent,
            '(?m)^RING3_FAULT_VECTOR=0x([0-9A-Fa-f]+)\r?$')
        $pfCount = 0
        $gpCount = 0
        $udCount = 0
        foreach ($faultVector in $faultVectorMatches) {
            $vector = [Convert]::ToInt32($faultVector.Groups[1].Value, 16)
            if ($vector -eq 14) { $pfCount++ }
            elseif ($vector -eq 13) { $gpCount++ }
            elseif ($vector -eq 6) { $udCount++ }
        }
        $panicCount = [regex]::Matches($finalContent,
            '(?m)^PANIC:').Count
        if ($pfCount -ne 0 -or $gpCount -ne 0 -or $udCount -ne 0 -or
                $panicCount -ne 0) {
            throw "R13 fault scan failed: #PF=$pfCount #GP=$gpCount #UD=$udCount panic=$panicCount."
        }
        Write-Host 'R13 fault scan: #PF=0 #GP=0 #UD=0 ABI panic=0'

        $serialLines = $finalContent -split "`r?`n"
        $firstScenarioLine = -1
        $secondScenarioLine = $serialLines.Length
        for ($lineIndex = 0; $lineIndex -lt $serialLines.Length; $lineIndex++) {
            if ($firstScenarioLine -lt 0 -and
                    $serialLines[$lineIndex] -eq
                        'R13_SCENARIO=0x0000000000000001') {
                $firstScenarioLine = $lineIndex
                continue
            }
            if ($firstScenarioLine -ge 0 -and
                    $serialLines[$lineIndex] -match '^R13_SCENARIO=') {
                $secondScenarioLine = $lineIndex
                break
            }
        }
        if ($firstScenarioLine -lt 0) {
            throw 'R13 did not emit the first requester block for the NotFound string check.'
        }
        $notFoundStrings = @(
            @{ Hash = '94A661B97507E172'; Length = 51; Site = 0x1FE },
            @{ Hash = '7B75BBA2453D7996'; Length = 38; Site = 0x1FF },
            @{ Hash = '2DB56378C1962B98'; Length = 251; Site = 0x200 },
            @{ Hash = 'ADE827DD03D3AE1F'; Length = 50; Site = 0x213 },
            @{ Hash = 'AF63AE4C86019E62'; Length = 1; Site = 0x208 },
            @{ Hash = 'D82EC95D355533BA'; Length = 36; Site = 0x209 })
        $verifiedStringRequests = @{}
        foreach ($string in $notFoundStrings) {
            $valueLineIndex = -1
            $request = $null
            $address = $null
            $hashPattern = '^STRING_VALUE;request=0x([0-9A-Fa-f]+);address=(0x[0-9A-Fa-f]+);hash=0x' +
                [regex]::Escape($string.Hash) + '$'
            for ($lineIndex = $firstScenarioLine;
                    $lineIndex -lt $secondScenarioLine; $lineIndex++) {
                if ($serialLines[$lineIndex] -match $hashPattern) {
                    $valueLineIndex = $lineIndex
                    $request = $Matches[1]
                    $address = $Matches[2]
                    break
                }
            }
            if ($valueLineIndex -lt 0) {
                throw "R13 first lifetime did not emit NotFound string hash $($string.Hash)."
            }
            $creationLine = $null
            $allocationSequence = $null
            $createPattern = '^STRING_CREATE;request=0x' +
                [regex]::Escape($request) + ';address=' +
                [regex]::Escape($address) +
                ';.*length=0x([0-9A-Fa-f]+);site=0x([0-9A-Fa-f]+);allocSeq=(0x[0-9A-Fa-f]+);'
            for ($lineIndex = $valueLineIndex - 1;
                    $lineIndex -ge $firstScenarioLine; $lineIndex--) {
                if ($serialLines[$lineIndex] -match $createPattern) {
                    $creationLine = $serialLines[$lineIndex]
                    $length = [Convert]::ToInt32($Matches[1], 16)
                    $site = [Convert]::ToInt32($Matches[2], 16)
                    $allocationSequence = $Matches[3]
                    break
                }
            }
            if ($null -eq $creationLine -or
                    $length -ne $string.Length -or
                    $site -ne $string.Site) {
                throw "R13 NotFound string $($string.Hash) has a changed creation length or site."
            }
            $recordPattern = '(?m)^STRING_(DISPOSE|FREE_GUARD|FREE);request=0x' +
                [regex]::Escape($request) + ';address=' +
                [regex]::Escape($address) + ';allocSeq=' +
                [regex]::Escape($allocationSequence) + ';[^\r\n]*\r?$'
            $recordMatches = [regex]::Matches($finalContent, $recordPattern)
            $disposeCount = 0
            $acceptedFreeCount = 0
            $successfulFreeCount = 0
            foreach ($record in $recordMatches) {
                $recordLine = $record.Value.TrimEnd("`r")
                if ($recordLine -match '^STRING_DISPOSE;.*;count=0x0*1$') {
                    $disposeCount++
                } elseif ($recordLine -match
                        '^STRING_FREE_GUARD;.*;accepted=0x0*1;exactRun=0x0*1;reason=0x0+$') {
                    $acceptedFreeCount++
                } elseif ($recordLine -match
                        '^STRING_FREE;.*;result=0x(?!0+$)[0-9A-Fa-f]+$') {
                    $successfulFreeCount++
                }
            }
            if ($disposeCount -ne 1 -or $acceptedFreeCount -ne 1 -or
                    $successfulFreeCount -ne 1) {
                throw "R13 NotFound string $($string.Hash) failed exact Dispose/free counts: Dispose=$disposeCount accepted=$acceptedFreeCount successful=$successfulFreeCount."
            }
            $verifiedStringRequests[$request] = $true
        }
        foreach ($request in $verifiedStringRequests.Keys) {
            $snapshotPattern = '(?m)^STRING_LIVE_COUNT;request=0x' +
                [regex]::Escape($request) + ';stage=0x0*3;count=0x([0-9A-Fa-f]+);dropped=0x([0-9A-Fa-f]+)\r?$'
            $snapshots = [regex]::Matches($finalContent, $snapshotPattern)
            if ($snapshots.Count -ne 1 -or
                    [Convert]::ToUInt64($snapshots[0].Groups[1].Value, 16) -ne 0 -or
                    [Convert]::ToUInt64($snapshots[0].Groups[2].Value, 16) -ne 0) {
                throw "R13 NotFound string request $request did not finish with a clean zero-survivor ledger."
            }
        }
        Write-Host 'R13 NotFound string check: 6/6 exact Dispose and accepted free; zero survivors'
    } elseif ($Phase35R9Ledger) {
        $lifetimeMatch = [regex]::Match($finalContent,
            '(?m)^R9_LIFETIME_ID=0x([0-9A-Fa-f]+)\r?$')
        $deltaMatch = [regex]::Match($finalContent,
            '(?m)^R9_B2_MINUS_B0_PAGES=0x([0-9A-Fa-f]+)\r?$')
        if (-not $lifetimeMatch.Success -or -not $deltaMatch.Success) {
            throw 'R9 did not emit its first-lifetime identity and B2-B0 delta.'
        }
        $lifetimeId = $lifetimeMatch.Groups[1].Value.TrimStart('0')
        if ([string]::IsNullOrEmpty($lifetimeId)) { $lifetimeId = '0' }
        $runPattern = '(?m)^R9_RUN;phase=0*2;[^\r\n]*;pages=0x([0-9A-Fa-f]+);' +
            '[^\r\n]*;lifetime=0x0*' + [regex]::Escape($lifetimeId) +
            ';[^\r\n]*$'
        $runRows = [regex]::Matches($finalContent, $runPattern)
        $accountedPages = [UInt64]0
        foreach ($row in $runRows) {
            $accountedPages += [Convert]::ToUInt64(
                $row.Groups[1].Value, 16)
        }
        $measuredPages = [Convert]::ToUInt64(
            $deltaMatch.Groups[1].Value, 16)
        $unexplainedPages = if ($measuredPages -gt $accountedPages) {
            $measuredPages - $accountedPages
        } else { [UInt64]0 }
        $overAccountedPages = if ($accountedPages -gt $measuredPages) {
            $accountedPages - $measuredPages
        } else { [UInt64]0 }
        Write-Host "R9 first lifetime: measuredPages=$measuredPages accountedPages=$accountedPages unexplainedPages=$unexplainedPages overAccountedPages=$overAccountedPages retainedRuns=$($runRows.Count)"
        if ($accountedPages -ne $measuredPages) {
            Write-Host 'R9_ACCOUNTING=INCOMPLETE'
        } else {
            Write-Host 'R9_ACCOUNTING=RECONCILED'
        }
        $requiredMarkers = @(
            'PHASE35_PERSISTENT_BACKEND_READY=1',
            'PHASE35_R9_RUN_LEDGER_READY=1',
            'PHASE35_R9_STRESS_EQUIVALENT_PASS=1',
            'PHASE35_R9_REPEAT_PASS=1',
            'PHASE35_R9_NO_READ_PASS=1',
            'PHASE35_R9_ONE_READ_PASS=1',
            'PHASE35_R9_NOT_FOUND_PASS=1',
            'PHASE35_R9_RUN_LEDGER_COMPLETE=1')
    } elseif ($Phase35R2Matrix) {
        $requiredMarkers = @(
            'ALLOC_PROVENANCE_READY=1;capacity=262144-live-runs;storage=static-per-page',
            'PHASE35_ALLOC_MATRIX_RESULT=NO_READ,PASS',
            'PHASE35_ALLOC_MATRIX_RESULT=ONE_READ,PASS',
            'PHASE35_ALLOC_MATRIX_RESULT=TWO_READ,PASS',
            'PHASE35_ALLOC_MATRIX_RESULT=NOT_FOUND,PASS',
            'PHASE35_ALLOC_MATRIX_COMPLETE=1')
    } elseif ($Phase35R) {
        $requiredMarkers = @(
            'PHASE35_PERSISTENT_BACKEND_READY=1',
            'PHASE35_FIXTURE_SHA256=BEFA57E7EF0799D031A0188A3D0883F0F342B8F8AE90B3330652DA04ADBA739D',
            'PHASE35P2_BACKEND_AVAILABLE=1',
            'PHASE35P2_BACKEND_WRITABLE=1',
            'PHASE35P2_SELECTED_VOLUME=GX35Q0001,filesystem=FAT16,label=GX35Q TEST,volumeId=892686641',
            'PHASE35P2_ROOT=apps/persist',
            'PHASE35_FOUR_PRIMARY_RETURNS=4',
            'PHASE35_FAILFAST_REPLACEMENT_RETURN_35=1',
            'PHASE35_STALE_OWNER_REJECTED=1',
            'PHASE35_MALFORMED_FAIL_CLOSED=1',
            'PHASE35_25_LIFETIME_STRESS=PASS',
            'PHASE35_STRESS_ALLOCATOR_STABLE=1',
            'PHASE35_APP_MODEL_RESET_PERSISTENCE=PASS',
            'PHASE35_RESET_TEMPORARY_CLEARED=1',
            'PHASE35_RESET_PERSISTENT_RETAINED=1',
            'PHASE35_ACTIVE_STORAGE_REQUESTS=0',
            'PHASE35_OPEN_PERSISTENT_HANDLES=0',
            'PHASE35_PROCESS_CLEANUP_BALANCED=1',
            'PHASE35_DIAGNOSTIC_VALUES_REMOVED=1',
            'PHASE35_COMPLETE=1',
            'RING3_PHASE35_COMPLETE=1',
            'APP_MODEL_LIFECYCLE_SELFTEST=passed=15;failed=0;first=',
            'APP_MODEL_TASKBAR_GROUPING_SELFTEST=passed=16;failed=0;result=PASS;first=',
            'APP_MODEL_TASKBAR_PROJECTION_STALE=0',
            'APP_MODEL_COMPAT_LEGACY_BACKEND_CALLS=0',
            'PHASE9_DIALOG_SELFTEST_OK=1',
            'PHASE9_FILE_SERVICE_SELFTEST_OK=1',
            'PHASE9_SHELL_SERVICE_SELFTEST_OK=1',
            'PHASE10_RESOURCE_STORAGE_SELFTEST_OK=1',
            'PHASE10_RESOURCE_CHUNK_SELFTEST_OK=1',
            'PHASE10_STORAGE_PATH_CONFINEMENT_OK=1',
            'PHASE10_STORAGE_PERSISTENT_AVAILABILITY_OK=1',
            'PHASE10_STORAGE_APP_SCOPE_OK=1',
            'PHASE10_STORAGE_RESET_OK=1',
            'APP_MODEL_COMPLETE'
        )
    } else {
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
    }
    if ($Phase35P2) {
        $requiredMarkers += @(
            'PHASE35P2_BACKEND_AVAILABLE=1',
            'PHASE35P2_BACKEND_WRITABLE=1',
            'PHASE35P2_SELECTED_VOLUME=GX35Q0001,filesystem=FAT16,label=GX35Q TEST,volumeId=892686641',
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
    if ($Phase35R2Matrix) {
        foreach ($marker in @(
                'PHASE35_ALLOC_MATRIX_RESULT=NO_READ,PASS',
                'PHASE35_ALLOC_MATRIX_RESULT=ONE_READ,PASS',
                'PHASE35_ALLOC_MATRIX_RESULT=TWO_READ,PASS',
                'PHASE35_ALLOC_MATRIX_RESULT=NOT_FOUND,PASS',
                'ALLOC_MATRIX_PROCESSES_B0=0',
                'ALLOC_MATRIX_PROCESSES_B1=0',
                'ALLOC_MATRIX_PROCESSES_B2=0',
            'ALLOC_MATRIX_PROCESSES_B3=0',
            'ALLOC_MATRIX_PROCESSES_B4=0',
                'ALLOC_MATRIX_PROCESS_COUNT=0')) {
            if ($finalContent.IndexOf($marker,
                    [System.StringComparison]::Ordinal) -lt 0) {
                throw "Phase 35R2 allocator matrix marker is missing: $marker"
            }
        }
        $matrixRows = [regex]::Matches($finalContent,
            '(?m)^PHASE35_ALLOC_MATRIX_RESULT=[^\r\n]+')
        if ($matrixRows.Count -ne 4) {
            throw "Expected four isolated allocator matrix rows; saw $($matrixRows.Count)."
        }
        $noReadFirst = $finalContent.IndexOf(
            'ALLOC_MATRIX_BEGIN=NO_READ',
            [System.StringComparison]::Ordinal)
        if ($noReadFirst -lt 0) {
            throw 'Fresh boot did not begin with the isolated no-read requester.'
        }
    } elseif ($Phase35R) {
        $fixtureStatusRows = [regex]::Matches($finalContent,
            '(?m)^PHASE35_FIXTURE_PREEXISTING_OR_SEEDED=([^\r\n]+)')
        $fixtureSeedRows = [regex]::Matches($finalContent,
            '(?m)^PHASE35_FIXTURE_SEED_WRITES=([0-9]+)')
        $fixtureHashRows = [regex]::Matches($finalContent,
            '(?m)^PHASE35_FIXTURE_SHA256=([A-Fa-f0-9]{64})')
        $p2PreflightRows = [regex]::Matches($finalContent,
            '(?m)^PHASE35P2_SEED_PREFLIGHT=status=([^,]+),seedWrites=([0-9]+),sha256=([A-Fa-f0-9]{64})')
        if ($fixtureStatusRows.Count -lt 2 -or
                $fixtureStatusRows[0].Groups[1].Value -ne 'Seeded' -or
                $fixtureStatusRows[$fixtureStatusRows.Count - 1].Groups[1].Value -ne 'Verified' -or
                $fixtureSeedRows.Count -lt 2 -or
                $fixtureSeedRows[0].Groups[1].Value -ne '1' -or
                $fixtureSeedRows[$fixtureSeedRows.Count - 1].Groups[1].Value -ne '0' -or
                $fixtureHashRows.Count -lt 2 -or
                $fixtureHashRows[$fixtureHashRows.Count - 1].Groups[1].Value -ne 'BEFA57E7EF0799D031A0188A3D0883F0F342B8F8AE90B3330652DA04ADBA739D' -or
                $p2PreflightRows.Count -lt 2 -or
                $p2PreflightRows[0].Groups[1].Value -ne 'Seeded' -or
                $p2PreflightRows[0].Groups[2].Value -ne '1' -or
                $p2PreflightRows[$p2PreflightRows.Count - 1].Groups[1].Value -ne 'Verified' -or
                $p2PreflightRows[$p2PreflightRows.Count - 1].Groups[2].Value -ne '0' -or
                $p2PreflightRows[$p2PreflightRows.Count - 1].Groups[3].Value -ne 'BEFA57E7EF0799D031A0188A3D0883F0F342B8F8AE90B3330652DA04ADBA739D') {
            throw 'Post-reset managed proof did not consume a preexisting, correctly hashed fixture with seedWrites=0.'
        }
        if ((Get-MarkerCount $finalContent '(?m)^PHASE35_FOUR_PRIMARY_RETURNS=4\r?$') -lt 2 -or
                (Get-MarkerCount $finalContent '(?m)^PHASE35_25_LIFETIME_STRESS=PASS\r?$') -lt 2 -or
                (Get-MarkerCount $finalContent '(?m)^PHASE35_MAIN_RETURN=35\r?$') -lt 8) {
            throw 'The managed read proof did not complete on both sides of the same-image reset.'
        }
        foreach ($marker in @('PHASE35_FREE_INVALID=0',
                'PHASE35_FREE_CORRUPT=0', 'PHASE35_FREE_NO_PAGES=0')) {
            if ($finalContent.IndexOf($marker, [System.StringComparison]::Ordinal) -lt 0) {
                throw "Phase 35 allocator invariant failed: $marker"
            }
        }
    } elseif (-not $Phase35P2 -and -not $Phase35R9Ledger -and
            -not $Phase35R13AllocLedger) {
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
        $rebootDeleteRows = [regex]::Matches($finalContent,
            '(?m)^PHASE35P2_REBOOT_DELETE=(PASS|FAIL)\r?$')
        if ($rebootDeleteRows.Count -ne 2 -or
                $rebootDeleteRows[0].Groups[1].Value -ne 'FAIL' -or
                $rebootDeleteRows[1].Groups[1].Value -ne 'PASS') {
            throw 'Persistent Delete must be absent after reboot: expected FAIL on the seed boot and PASS on the verification boot.'
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
if ($Phase35R9Ledger) {
    Write-Host 'Phase35R9 retained-run capture completed in one guest boot; no reset was requested.'
} elseif ($Phase35R2Matrix) {
    Write-Host 'Phase35R2 allocator provenance run completed in one guest boot; no reset was requested.'
} elseif ($Phase35P2) {
    Write-Host 'Storage35Q and Phase35P2 same-image reboot validation completed.'
} else {
    Write-Host 'Storage35Q same-image reboot validation completed.'
}
