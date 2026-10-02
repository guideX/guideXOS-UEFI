# Phase 35Q: UEFI Durable Writable Block Storage

## Scope and status

Phase 35Q adds a truthful block-I/O result model, enables UEFI AHCI discovery, and proves writes through a disposable QEMU SATA image with explicit device flushes and same-image guest resets. It does not bind a disk to Phase 10 Persistent storage. The Phase 35P application-storage blocker remains in force until a later phase defines the persistent root and AppId namespace.

The AHCI implementation uses polling and bounded command waits. FAT writes are write-through at the block API; callers request durability with `TrySync()`, which calls the device's real flush operation. FAT has no journal and these operations do not promise unexpected-power-loss atomicity.

## UEFI storage inventory

| Path | Discovery / backing | Read | Write | Flush | Hotplug | QEMU availability | Use and persistence |
|---|---|---:|---:|---:|---|---|---|
| `RdskFS` / initrd | Boot-provided RAM image; synthetic | Yes | No-op / unsupported in the production path | No | Replaced only by rebooting with a different image | Yes; normal boot ramdisk | Active boot filesystem for production files. RAM image is lost at reboot; boot does not depend on the Phase 35Q disk. |
| `Ramdisk` | Kernel-allocated memory; synthetic | Yes | Yes, memory only | Unsupported | No media removal or rescan model | Yes; in-memory device | Transient block device; never a durable backend. |
| `FileDisk` | Virtual image copied into a byte array; synthetic | Yes | Yes, byte array only | Unsupported | No media removal or rescan model | Source available; not discovered by the standard UEFI boot flow | Existing virtual-disk code; host-file changes are not a guest durability proof. |
| FAT12/16/32 | Filesystem over `Disk`; superfloppy volume at LBA 0 | Yes | Yes when the block backend advertises it | `TrySync()` reaches `Disk.Flush()` | No partition discovery or volume rescan in this phase; backend errors propagate | Yes; mounted on the Phase 35Q AHCI image | FAT metadata and data writes report failures. No partition-table selection, journaling, or crash-atomic transaction is provided. |
| USB mass storage BOT | Enumerated USB mass-storage interface; physical or emulated USB | READ(10) | No; remains read-only | Unsupported | No hotplug rescan; existing enumerated device only | QEMU can emulate USB MSC; not attached in the durability run | Does not qualify as Persistent storage. Phase 35Q checks its declared read-only policy. |
| IDE / ATA PIO | Legacy PCI IDE channels; device I/O | Yes | Yes | ATA cache flush in driver | No hotplug event or rescan path | QEMU IDE exists, but this driver is not initialized in UEFI mode | Legacy initialization path only; not selected for this proof. |
| SATA / AHCI | PCI class 01/subclass 06, AHCI BAR5, SATA ATA ports; actual guest block device (QEMU-emulated in proof) | Yes, ATA READ DMA EXT | Yes, ATA WRITE DMA EXT when ATA DMA is declared | ATA FLUSH CACHE EXT, or FLUSH CACHE fallback | Boot-time enumeration only; no hotplug event/rescan; I/O failures are reported | Yes; Q35 AHCI with disposable host-backed image | Selected UEFI path. Capacity and identity come from ATA IDENTIFY; guest I/O reaches the attached block device. |
| VirtIO block | No implementation found in the active kernel | — | — | — | — | QEMU can emulate it, but guideXOS has no driver | Not available. |
| Firmware Block I/O after `ExitBootServices` | No retained firmware block protocol in the kernel | — | — | — | Firmware protocols are not used after `ExitBootServices` | UEFI firmware may expose it before exit; not available to the running kernel | Not available. |

## Selection and transport

AHCI/SATA is the smallest suitable target in this tree: the kernel already enumerates PCI and contains AHCI structures, QEMU presents a deterministic AHCI controller, and ATA exposes both bounded block commands and explicit cache-flush commands. UEFI now calls `SATA.Initialize()` after PCI enumeration. The driver only accepts attached SATA ATA disks with LBA48 and DMA support, identifies block count, logical block size, model, serial, and supported flush command, then exposes a typed `Disk` implementation.

The UEFI path maps BAR5 as uncached device MMIO, performs the AHCI BIOS/OS ownership handoff, and identifies implemented SATA ports after `ExitBootServices`; it does not depend on firmware Block I/O. Reads use ATA READ DMA EXT (`0x25`) and writes use ATA WRITE DMA EXT (`0x35`) through an AHCI command list/FIS/PRDT. Commands are range-checked and split into batches of at most 128 logical blocks with bounded polling and command-error checks. Flush uses ATA FLUSH CACHE EXT (`0xEA`) when advertised, otherwise ATA FLUSH CACHE (`0xE7`) when available. Warm guest boots clear stale AHCI discovery statics before PCI initialization.

Commands use one PRDT data region per command and at most 128 logical blocks per batch. Command, BIOS handoff, and port-stop waits are bounded. Port interrupts are disabled; command completion and task-file errors are polled. The driver reports media removal, timeout/transport errors, write failure, and flush failure instead of silently succeeding.

Flush selects ATA command `0xEA` (FLUSH CACHE EXT) when IDENTIFY advertises it, otherwise `0xE7` (FLUSH CACHE) when advertised. A device advertising neither is not flush-capable and cannot pass the diagnostic target gate.

## Block and filesystem contracts

`Disk` now reports `BlockSize`, `BlockCount`, `DiskCapabilities` (`Readable`, `Writable`, `FlushSupported`), and availability. `ReadBlocks`, `WriteBlocks`, and `Flush` return `DiskIoResult`; the legacy bool `Read` and `Write` wrappers remain for compatibility. Requests validate null/short buffers, byte-count arithmetic, device capacity, and range before transport access. Empty requests are successful only with a zero-byte buffer and an LBA no greater than capacity.

The typed result set distinguishes success, unsupported operations, read-only media, invalid range/buffer, unavailable media, transport failure, write failure, unsupported flush, and flush failure. A raw transport failure returned from a write maps to `WriteFailure`; flush results stay distinct.

FAT mount reports its `DiskIoResult` and `IsMounted`. `TryWriteAllBytes`, `TryDelete`, `CreateDirectory`, `TryFormat`, and `TrySync` return `FatOperationResult`. FAT propagates data-sector, FAT-copy, directory-sector, allocation, and mount-read failures. `TrySync()` requests the backend flush and only reports success if the flush succeeded and no prior mutation in the current operation failed. `TryFormat()` uses the actual device capacity, checks every write, and requires flush support before formatting. Existing void filesystem entry points remain compatibility wrappers and do not expose the typed result themselves.

FAT overwrite writes a new chain and updates the directory entry before freeing the old chain. This reduces the interval where a file points to already-freed clusters, but does not make multi-sector updates crash-atomic. The implementation supports FAT12, FAT16, and FAT32 superfloppy volumes; MBR partition discovery and journaling are outside this phase.

## Diagnostic image and destructive-I/O gate

`Scripts/new_phase35q_fat16_image.py` creates a new 16 MiB image only when the unique output path does not exist. It creates a FAT16 volume in the first 32,764 sectors and reserves four sectors for raw-storage diagnostics. `Scripts/run_phase35q_storage_validation.ps1` generates a fresh run ID, creates a baseline copy and unique OVMF variable copy, attaches only the named image to an explicit emulated AHCI controller, and requires ATA serial `GX35Q0001`, 32,768 blocks, 512-byte logical blocks, writable capability, and flush capability before writes. It attaches no host physical disk. The QEMU guest is reset through QMP between stages while the exact same image remains attached.

The raw test uses LBA 32,766 (one 512-byte sector). It first saves the original sector to LBA 32,765, performs 25 different pattern → flush → read-back cycles, resets the guest, verifies the last pattern, restores the original sector, flushes, resets again, and verifies restoration. A separate sector at LBA 32,764 carries the diagnostic stage marker. The host script hashes the initial image and guest-mutated image, restores the pristine baseline, checks the restored hash, then removes the fixture and firmware-variable copies. It retains the serial proof log.

The FAT proof uses the same image, through the guest AHCI driver. It mounts FAT16, creates `apps/35qapp`, writes a deterministic 32,768-byte value A and syncs it, resets and verifies A, overwrites with same-length B and syncs, resets and verifies B, then tests a shorter 1,024-byte replacement and a longer 65,536-byte replacement. It syncs and reads back both in the same boot, resets again, verifies all 65,536 bytes, and checks the directory entry. SHA-256 values and individual stage results are recorded in the serial log.

Safe failure injection uses an in-memory sparse overlay around the real device. It rejects writes at FAT-table, file-data, and directory-entry stages, plus a flush failure and a mount-read failure. These injected writes never reach the backing image. Raw range, short-buffer, null-buffer, zero-count, generic read-only, and USB read-only policy checks also run in the guest.

## Production policy and limitations

The proof selects a test disk by exact diagnostic serial and exact geometry. This is not a production volume-selection policy. Production must not silently choose the first writable disk; volume identity and the persistent root remain Phase 35P design work. No application-local store is implemented here, Phase 10 Persistent remains `ResourceUnavailable`, and Temporary and Resource service 8 semantics are unchanged.

FAT data and metadata can be partially updated if a command fails mid-operation. A successful explicit flush plus orderly guest reset is tested; sudden power loss, torn sectors, and recovery from partially updated FAT chains are not. USB remains read-only. The diagnostic target is a QEMU-emulated AHCI SATA device backed by a unique disposable host image accessed only through the guest driver.

## Validation record

### Repository and build

- Repository: `D:\dev\guideXOSUEFI`; branch `main`; starting HEAD `87647fda2825f67f2b03769aee0eeebe9c01c924` (`Document Phase 35P persistent storage blocker`); upstream `origin/main`; preflight worktree clean, 0 ahead / 0 behind.
- Nested runtime: `out\rt` stayed at `9d5a6a9aa463d6d10b0b0ba6d5982cc82f363dc3`. It was already dirty and had untracked files before this work; no runtime source changes were intentionally made.
- The unqualified `.\build.ps1` completed with `Build Complete`, zero errors, and framework-support, NativeAOT/linker, analyzer, unused-field, and obsolete-code warnings. Managed admission reported `MANAGED_ARTIFACTS=32`, `DESCRIPTOR_AGREEMENT=32/32`, and `BUILD_STAGING_AGREEMENT=32/32`.
- Ordinary staged artifact SHA-256: `ESP\kernel.elf` `B65255219A64CCEE37D02269AA545B42E67D1EBCD5967C696D7828DB78CE6583`; `ESP\ramdisk.img` `6E327DA354445DF0AA4511017F679C626440E9A7DD995526D31678A75C6948B8`; `ESP\EFI\BOOT\BOOTX64.EFI` `553D2FB4BEB4C31AC6114B5926F97E1E738B33714CDCE57DA14E1AA6C1A22C3A`.
- Diagnostic modes `Ring3Phase34`, `Ring3Phase33`, `Ring3Phase32`, and `AppModel` were built separately for their guest runs. The ordinary kernel, ramdisk, and bootloader outputs were retained and restored after those checks.

### Durable block and FAT proof

- Device: QEMU `QEMU HARDDISK`, ATA serial `GX35Q0001`, 32,768 logical blocks × 512 bytes = 16 MiB; writable and flush-supported. It was discovered through UEFI PCI/AHCI after `ExitBootServices`.
- Raw fixture: LBA 32,766, one 512-byte sector. Original contents were copied to LBA 32,765; LBA 32,764 carries the proof stage marker. Twenty-five write → explicit flush → exact read-back cycles passed in one boot. The last pattern survived the next guest boot; original data was restored, flushed, and verified after another boot.
- The test image hash moved from `2EFE22E39E112105854DBC4784887CE06A64A040EC98D50FB52DD7A7FE94B451` to `7AE53715ECB155E35EAA84ABF4586F219E617617F3DE38434AE85F2BE057AF99` during guest writes, then returned exactly to the initial hash from the pristine baseline before the fixture was removed. The image and baseline were disposable; proof logs were retained.
- Range, null/short-buffer, and zero-count bounds checks passed. An out-of-range write returned `InvalidRange`. A read-only backend returned `ReadOnly`; USB BOT advertised read-only and its write policy remained disabled.
- Fault injection passed for FAT-table, file-data, directory-entry, flush, and mount-read failures. Write failure propagated as `FatOperationResult.WriteFailure`, flush failure as `FlushFailure`, and mount read failure remained observable. Injected writes used an in-memory sparse overlay and did not reach the real test image.
- FAT16 mounted from the first 32,764 sectors. `apps/35qapp/state.bin` was written and synced as 32,768-byte A, reboot-read successfully with SHA-256 `ae35f852829d85c0cc3dc0f471a72be8c4f6f04a428bc33345112f78f7ebe083`, then overwritten by same-length B and reboot-read with SHA-256 `664e38a6a80bf303f811429c6d9fae760032321dd11b5441006f81df2b6955d9`.
- The same file was resized from 32,768 to 1,024 bytes and then to 65,536 bytes. The longer file hash was `7bb3d3e264e1448268d4ed11cf1dc0b1205fb7a32fcf587ba0438f8bf7e6d434`; its bytes and directory entry survived the final reboot. The short and long replacements were read back in the same boot, and each stage required successful `TrySync()`.
- `35Q_RDSKFS_BOOT=PASS`; USB read-only policy passed. QEMU attached the workspace ESP directory and only the newly generated `out/phase35q/durable-<run-id>.img` on an explicit emulated AHCI controller. No host physical disk was attached.

### Application-model and stability regressions

- App Model: `APP_MODEL_COMPLETE`; validation `True`. The Phase 10 Temporary storage/app-scope/reset self-tests passed (`PHASE10_STORAGE_APP_SCOPE_OK=1`, `PHASE10_STORAGE_RESET_OK=1`); Resource metadata/chunk/path checks passed; service-registry duplicate/stale-context and cleanup checks passed. `PHASE10_STORAGE_PERSISTENT_UNAVAILABLE_OK=1` confirms Persistent remains unavailable.
- Phase 34: `RING3_PHASE34_COMPLETE=1`; main returned 34; the positive scoped read proof made five service requests and seven resource lookups, passed four replacement lifetimes and 25 resource-read lifetimes, and rejected cross-scope access. Fail-fast cleanup passed; malformed, zero-length, unmapped, overflow, and stale-owner requests were rejected, with no backend lookup on the stale/malformed controls.
- Phase 33: `RING3_PHASE33_COMPLETE=1`; main returned 33; valid shell-object service requests and invalid/malformed/stale-owner controls passed; 30 successful returns and cleanup balance passed.
- Phase 32: `RING3_PHASE32_COMPLETE=1`; main returned 32; three successful OpenDocument service requests, five successful return lifetimes, six target/window cleanups, stale requester rejection, and balance checks passed.
- Across the storage proof and Phase 32/33/34/App Model serial logs, counts for `#PF`, `#GP`, `#UD`, ABI panic, and kernel panic were zero. `freeInvalid`, `freeCorrupt`, and `freeNoPages` were zero at every emitted allocator snapshot.
- The existing Phase 35 managed SDK was not resumed and Phase 36 was not started. Existing Phase 35/35P blocker documents were not edited.

### Outcome and remaining boundary

Outcome A: the UEFI guest can discover a writable AHCI block device, report write and flush outcomes, mount/use FAT16, and recover raw and FAT bytes after a full guest reboot. Phase 35P may resume. The immediate next task is Phase 35P Persistent App-Local Storage Backend: define a production volume/root policy and confined AppId namespace, then prove Persistent through replacement instance, App Model reset, and reboot. The Phase 35Q proof does not itself bind any device to Phase 10 Persistent, and it does not establish sudden-power-loss recovery, torn-sector protection, journaling, or multi-sector crash atomicity.
