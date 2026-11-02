# AMCONV2G-R3 — Production Root UI Reachability

**Outcome A — `AMCONV2G_R3_UI_AND_PACKAGES_READY`**

**Guest status: `QAC_GUEST_PENDING`**

**Qualification date:** 2026-11-02

**Branch:** `APP_MODEL`

## Revision identity and scope

- Starting HEAD: `7991d506e9b376294d1e86ba2b5ed838e6af9a15`.
- The branch stayed on `APP_MODEL`; no reset, branch switch, merge, rebase, cherry-pick, or push occurred.
- The same HEAD was used throughout offline source/build/package qualification.
- Ending code HEAD: `d9bd68abded31029229050381e9e383199bc8e68` (`feat: expose C# root through Computer Files`). The report and revised acceptance procedure are committed in a documentation-only follow-up; its final repository HEAD is recorded in the R3 delivery summary.
- The pre-existing untracked D/E/F test `bin/` and `obj/` directories were preserved. .NET runs used isolated artifact paths under `out/amconv2g-20261102-r3-final/test-artifacts/`.
- Work stayed in this APP_MODEL checkout. Reference checkouts were only read. No QEMU, QAC admission, or guest process was used.

## Production UI reachability audit

The existing Start menu's Computer Files command calls `Desktop.LaunchApplication("Computer Files")`, which uses the normal AppModel registry and `ComputerFilesApplicationFactory`. Its new-window behavior remains the drive chooser. The missing connection identified in R2 was that no production GUI handler submitted the existing typed folder request.

R3 adds a third row, **Open Root**, to the existing Computer Files left navigation pane. A fresh left-button click edge on that row calls `OpenCSharpRootThroughShellService` on the current `ComputerFiles` instance. The handler uses its existing `_serviceContext` and `_services.Shell` to submit:

```csharp
ApplicationShellOpenRequest.ForShellFolder("gxos.shell.root", "/")
```

The handler calls the existing shell service's `Begin` and `Observe`, reports typed result codes and bounded diagnostics through the existing closeable `Desktop.ShowOpenError` window, and disposes the request and result wrappers. It does not construct a `ComputerFiles` window, call the launcher directly, or read/change `File.Instance`.

The existing typed shell dispatch validates and inspects the requested directory against the active mounted filesystem, then routes through the existing `gxos.builtin.files` factory and `ApplicationInstanceRegistry`. The established `MultiInstance` policy remains in force. If the filesystem cannot prove that `/` exists, the request fails; it does not create a directory or fall back to a document association.

`RunTypedRootServiceRouteSelfTest` is added to the existing AppModel shell-service diagnostic self-test. When that diagnostic runs, it submits two typed Root requests, checks distinct instance handles and factory-owned `ComputerFiles` windows, and checks that cleanup returns instance and mount-lease counts to baseline. It also checks that a missing path returns `ResourceUnavailable` without a document fallback or leaked lease, and that `gxos.shell.this-system` is rejected as `UnsupportedTarget`. The test compiles, but it was not executed in a guest during R3.

## Input and navigation reachability

The row uses the same Computer Files mouse input path as the existing navigation entries: `Control.MouseButtons` edge detection in `ComputerFiles.OnInput`. The UEFI mouse path routes PS/2 events through `MouseEventDispatcher.Update` into pointer position and button state. The existing 42-assertion hosted desktop interaction suite passed. Prior Desktop P3 source/guest evidence documents QMP pointer input on this source family; R3 did not launch or claim a guest interaction.

For the next authorized acceptance run:

1. Boot Candidate A through the approved QAC acceptance workflow and wait for the desktop.
2. Open Start and select **Computer Files**. Confirm the ordinary drive chooser still appears.
3. In the Computer Files left pane, click **Open Root**. Confirm a new folder window opens, or that the existing error window reports the typed failure.
4. Correlate serial output: `FILES_ROOT_ACTION=begin`, `SHELL_FOLDER_REQUEST`, folder bind/inspection, `FACTORY_LAUNCH`, `FILES_FOLDER_WINDOW`, and `FILES_ROOT_ACTION=result`. Verify the result identifies `gxos.builtin.files` and the new instance handle.
5. Navigate into a populated directory and use the **Up Level** toolbar button to return to its parent. Close the folder window from its title bar and confirm instance termination and mount-release markers.
6. Boot Candidate B and repeat ordinary Computer Files launch and **Open Root**.
7. Open `WithFiles`, confirm `inside.txt` and `Child` are visible, then use **Up Level**. Open `Empty` and confirm it is visible with no entries; also inspect `Nested/DeepEmpty` and return to the parent.
8. Open a second Root window. Close one window and confirm the other remains usable; close the second and confirm both instances and leases release normally.
9. Shut down through the authorized QAC workflow and verify guest exit and QAC cleanup.

No guest UI, directory listing, factory transition, window ownership, instance termination, serial marker, screenshot, or lease release is claimed as passed by R3. The full native GUI/factory/window lifecycle remains guest-pending.

## Mount replacement boundary

The source review reconfirmed R2: there is no supported production UI or application operation that replaces the active filesystem mount and requests disposal. R3 preserves the current mount binding and lease behavior and introduces no filesystem replacement feature. Hosted B/F tests continue to cover replacement and deferred disposal at the mount-lifetime layer. A guest mount-replacement scenario remains deferred until a supported production owner path exists.

## Hosted and source regressions

| Suite | Result |
|---|---:|
| AMCONV1 compatibility source checks | 9/9 |
| AMCONV2B/C source checks | 25/25 |
| AMCONV2B folder and mount hosted assertions | 71/71, 0 failures |
| AMCONV2D FAT inspection | 11/11, 0 failures |
| AMCONV2E RDSK v1 inspection and binding | 32/32, 0 failures |
| AMCONV2F source checks | 9/9 |
| AMCONV2F generator assertions/rejections | 12 assertions; rejection cases passed |
| AMCONV2F RDSK v2 reader and binding | 58/58, 0 failures |
| AMCONV2G managed packaging tests | 4/4 |
| New Root UI production source-wiring audit | 21/21; explicitly source-only |
| UEFI desktop interaction | 42 assertions passed |
| Phase 33 wire layout | Pass: request 2,084 bytes; response 16 bytes |
| Task Manager Memory Details regression | Pass using the available PowerShell Core host |

The Root UI source audit checks that the real GUI row calls the typed request through the owning shell service and checks existing factory/instance policy wiring. It does not execute a GUI event or the factory. The diagnostic AppModel service/factory lifecycle self-test is present and compiled, but remains unexecuted until an authorized guest run.

Managed artifact qualification passed independently for both package stages: 41 artifacts, descriptor agreement 41/41, build/staging agreement 41/41, ramdisk/staging agreement 41/41, and compiled allowlist agreement 41/41.

## AppModel and NativeAOT build

- Release AppModel: 0 errors, 137 warnings.
- Release AppRuntime: 0 errors, 141 warnings.
- Both counts match the R1R1 baseline; no new warning category was introduced by R3.
- Supported AppRuntime NativeAOT publish succeeded. The linker emitted `LNK4281` for the inherited fixed base `0x10000000`; this is expected under the existing bare-metal layout. The PE has AMD64 PE32+, native subsystem, entry `0x100018B8` (`KMainWrapper`), zero imports, and zero relocation directory.
- No `/FORCE`, `Runtime.WorkstationGC`, `RhpInitialDynamicInterfaceDispatch`, `RhpCidResolve`, or `__InterfaceDispatchMap` dependency was introduced. The link map resolves `KMain` at `0x1004DE14`.
- PE SHA-256: `59930A571A391C543AE995594A646876D53E9E93E53EE2D1D5EC88C9F37AAB89`.
- Converted kernel SHA-256: `862B3A95420E1C02411061610487664100908A4F2FCB2311B633586A99BD7DF1`.
- ELF validation: ELF64 little-endian `ET_EXEC`, `EM_X86_64`, 8 `PT_LOAD` segments, entry `0x1004DE14` inside an executable load segment, and no dynamic or interpreter segments.

## Offline candidate packages

R3 preserved the R1R1 packages and generated new candidates under `out/amconv2g-20261102-r3-final/`. Both were staged through isolated copies of the production `build.ps1`; only bootloader, kernel, conversion, and ramdisk generation were skipped after the fresh kernel and retained, versioned RDSK inputs were independently checked. Each complete three-file ESP tree was staged twice and matched by relative path, size, and SHA-256.

| Candidate | RDSK | EFI loader SHA-256 | Kernel ELF SHA-256 | Ramdisk SHA-256 | ESP tree SHA-256 |
|---|---:|---|---|---|---|
| A | v1 | `C185B725E46D5D2E0E6F441D69B3578AD987FEC238152F60FD59657069DE78C9` | `862B3A95420E1C02411061610487664100908A4F2FCB2311B633586A99BD7DF1` | `B11822A04AE5BE3A0C858851ECD3BC3C2C8EDCA7C9A4157A1753E0E8C080785E` | `C4991782ABE5B6A65CF2700562041D4E0529CA26BAF6BB2CB817D1639082BB79` |
| B | v2, explicit candidate | `C185B725E46D5D2E0E6F441D69B3578AD987FEC238152F60FD59657069DE78C9` | `862B3A95420E1C02411061610487664100908A4F2FCB2311B633586A99BD7DF1` | `18AC75ABEA193CD2F80E08D13A0558646303580131EC6847000E9CEAD7D15BD4` | `CBAC27CF8A23554B9FA86D8A6879F5C155D2A4603A1FFD4A4DB140D0EE61CE3B` |

The manifests report `offline package ready for guest testing` and `guest_boot_claimed: false` for both candidates. Structural validation confirmed exactly `EFI/BOOT/BOOTX64.EFI`, `kernel.elf`, and `ramdisk.img`; the loader is AMD64 PE32+ with the EFI application subsystem; Candidate A selects RDSK v1 and Candidate B selects RDSK v2. The retained v1/v2 ramdisk hashes match the previously accepted images.

Manifests: [Candidate A](../out/amconv2g-20261102-r3-final/candidate-a/package-manifest.json) and [Candidate B](../out/amconv2g-20261102-r3-final/candidate-b/package-manifest.json). The build, audit, and repeatability logs remain with the offline artifacts under the same output root; generated outputs are not committed.

## Remaining guest assertions and R4 scope

Guest-only assertions remain firmware/loader/kernel/runtime startup, real click delivery to **Open Root**, actual shell service and factory execution, folder window ownership, normal instance/lease cleanup, populated and empty directory navigation, and QAC shutdown/cleanup. R4 should be the separately authorized, QAC-managed Candidate A/B matrix above. Keep mount replacement deferred unless a supported production operation becomes available. Nothing was pushed.
