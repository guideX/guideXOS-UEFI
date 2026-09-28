# UEFI Integration H2b — Forensic Closeout

## Decision

**Outcome F: H2b is not accepted; Phase 33 remains paused.** The historical vector 6 event was not reproduced. Targeted AppRuntime stress first produced a current vector 14 page fault after 37 completed Notepad close cycles. A later frame-chain-diagnostic stress image completed 37 Notepad cycles, then panicked on a kernel-originated `int 0x80` gate during the next Start-menu action; the producer and relationship between these current anomalies remain unresolved. A later fresh Phase 31 attempt also failed before the proof lifetimes because regenerated proof payloads did not match the kernel's fixed hash allowlist. Earlier Phase 31, Phase 32, and AppModel controls passed against the then-allowlisted payload set, but they do not satisfy fresh controls for the newly regenerated image or override the guest fault.

No H2b closeout commit is made while the current lifecycle fault remains unexplained. Do not start Phase 33 from this worktree.

## Starting state and preserved evidence

- Root started at 996c300b5130b5e11aeceeff74dda5ac2cb74d77, branch main, upstream origin/main, ahead/behind 0/0.
- out/phase32-current-allowlisted.log remains preserved; SHA-256 200CFBF0270C2FDE1B874711937460BA0F8E81FB5E750FB5A8DA9B8C29CAF00C.
- The nested out/rt checkout started detached at 9d5a6a9aa463d6d10b0b0ba6d5982cc82f363dc3 with pre-existing modifications and untracked files. No Server or Legacy files were intentionally touched.
- The historical run records #UD at RIP 0xA02C6, CS 0x8, RFLAGS 0x10216, RSP 0x48C0D00, RBP 0, CR3 0x3DD73000, RAX 0x50001FF, and PTE 0xA0023. Its caller return address maps into WindowManager.CleanupClosedWindows, near the indirect virtual w.Dispose() at WindowManager.cs:695.
- That original log has no instruction bytes, exact window, ApplicationInstance, taskbar row, or focus state. Do not infer any of those from later runs.

## Historical #UD and vector 6 byte capture

The original instruction-level provenance is permanently unavailable from that run. **The historical RIP 0xA02C6 cannot be disassembled retrospectively because its instruction bytes were not captured. Current instrumentation would capture a recurrence, but no recurrence occurred under the H2b stress matrix.** The stress matrix did not complete cleanly, so this sentence is not an Outcome B claim.

The vector 6 handler still records 16 bytes at the interrupted RIP, checking mappings before each byte read. A safe diagnostic-only probe intentionally executed UD2 and produced FAULT_BYTES=0F0BC390C3488D442408C3488B0424C3 in out/h2b-fault-bytes-probe.log. The first bytes match the probe's UD2 instruction and following code bytes; saved register sentinels and CR3/page-walk context remained intact, and serial output completed. This validates the logger only. No artificial production #UD was injected. The logger was not removed or weakened.

## Earlier page-fault evidence (before instruction-byte capture)

The latest stress evidence is out/h2b-cleanup-stress-recursive-lock-fault-preserved.log, SHA-256 53CB7426EA8588401C839B18BCBAAC15DAE3771383A80C15DF42387B6B236170, with matching preserved image out/h2b-cleanup-stress-recursive-lock-kernel.elf (SHA-256 F9E85245D8D47CB4D8553C55920E548031AB5F781E08CC85AF9CC1DF05C70435) and map out/h2b-cleanup-stress-recursive-lock-Kernel.map (SHA-256 051D5D499B882006B764645D8206AE92B1448587410CB9106E02DCE4821BA81D).

After 37 completed Notepad close cycles, the next Start Menu toggle faulted before the next app selection. The last completed close had removed its window and taskbar projection, with zero visible groups/buttons/icons/active handle and all three allocator free counters at zero. The log then contains two vector 14 records:

| Field | First record | Second record |
|---|---:|---:|
| Error code | 0 | 0 |
| CR2 | 0x47E24E75 | 0x47E24AB5 |
| RIP | 0x1000148B | 0x1000148B |
| CS | 0x8 | 0x8 |
| RFLAGS | 0x10046 | 0x10002 |
| RSP | 0x27F790 | 0x27F400 |
| CR3 | 0x3DD73000 | 0x3DD73000 |
| RAX | 0x404C000 | 0x404C000 |

The first record also shows RCX 0x27F7E0, RDX 0x27F8B0, RBX 0x116E80, RBP 0x27F870; the second shows RCX 0x310, RDX 0, RBX 1, RBP 0x27F4B0. The page-table walk reports a non-present PDE for each CR2. No vector 6 or vector 13 record occurred.

The preserved map names RIP 0x1000148B as __GetGCStaticBase_guideXOS_guideXOS_OS_ApplicationAssociationRegistry. The bytes in the matching ELF at that address begin with a RIP-relative LEA; the following instruction is a load. The recorded RIP is therefore not itself a memory-access instruction, and the reported CR2/RAX do not identify the following load's address. This mismatch leaves the precise faulting operation and cause unresolved. Do not attribute this fault to Notepad disposal, ApplicationAssociationRegistry, the historical w.Dispose() call, or a specific window. It is a current guest fault and a release blocker, not a historical #UD reproduction.

No faulting window, ApplicationInstance, taskbar row, focus window, or cleanup pass identity is available for this PF. The failure happened after a completed Notepad cleanup and at Start Menu toggle, outside the logged dispose interval.

## Reproduced instrumented page fault and byte-level decode

The later `-SkipBuild` cleanup stress run used the exact instrumented kernel in the current ESP. Its serial log is preserved as `out/h2b-cleanup-stress-qmp-state-fault-preserved.log` (SHA-256 `A5AD206FD24543337B4204A0738261BD329A763410285D083DBEB92996329E85`). The preserved kernel is `out/h2b-cleanup-stress-qmp-state-kernel-preserved.elf` (SHA-256 `1A699BAEA94DD99B757FDD9550BBA227ACC631335C2109B958EF0290E56B9C4D`); the matching ESP kernel has the same hash. The preserved map is `out/h2b-cleanup-stress-qmp-state-Kernel-preserved.map` (SHA-256 `FACB782D0EFDB73893399B7BC1AFF471CE7720EECA8BC0F5E67E684EA53FF44D`).

After the 37th completed Notepad close, cleanup reported `windows=7`, taskbar reconciliation reported `entries=0`, and the taskbar visual reported zero groups/buttons/icons/active handle. The next `APP_RUNTIME_START_TOGGLE;registered=1;before=0` was followed immediately by vector 14. The handler captured:

| Field | Captured value |
|---|---:|
| Vector / error | `0x0E` / `0` |
| CR2 | `0xFFFFFFFFFFFFFF8B` |
| RIP / CS | `0x10001355` / `0x8` |
| RFLAGS / RSP / RBP | `0x10046` / `0x27F790` / `0x27F870` |
| CR3 | `0x3DD73000` |
| RAX / RCX | `0` / `0x27F7E0` |
| FAULT_BYTES | `00488B00C3488D05B7BA4700488B00C3` |
| PML4 entry for CR2 | `0` (not present) |

Disassembly at the captured RIP is `add %cl,-0x75(%rax)` (`add byte ptr [rax-0x75], cl`), followed by `add %al,%bl`, then the next function's `lea`/`mov`/`ret`. With captured `RAX=0`, the first instruction's memory operand is `0xFFFFFFFFFFFFFF8B`, exactly the logged CR2. The ELF/map place `0x10001355` at the final displacement byte of `__GetGCStaticBase_guideXOS_guideXOS_Kernel_Drivers_ACPI`, whose entry is `0x1000134F`; its expected `mov (%rax),%rax` begins at `0x10001356`. The CPU therefore began decoding in the middle of that helper's `lea` instruction. This proves the immediate PF instruction and an instruction-boundary/control-flow anomaly in this current run. It does not identify what set RIP to that address, and it does not prove the historical #UD had the same producer.

The immediately preceding completed disposal record names the Notepad window (`serialObject=260669440`, `ownerId=109`, `type=Notepad`, `title=Notepad`, `disposeDispatch=Notepad.Dispose->Window.Dispose`, `appId=none`). This window was already removed and its taskbar projection reconciled before the Start Menu toggle. No exact fault-time window, ApplicationInstance, focus target, or full call chain was captured; do not assign the PF to that Notepad object. The run contains no vector 6/#UD record. The vector 14 byte logger emitted its line before subsequent panic handling produced repeated vector 13 records.

There are 5,108 vector 13 records at RIP `0x1000B640` after the first PF. The QMP send then failed, but the host-side catch recorded `qemuAtInputFailure=exited; qemuExitCode=0` before the harness's `finally` cleanup. This distinguishes the event from a QMP disconnect while QEMU was still running; the stress harness counted 37 completed Notepad cycles and no other cohort. The repeated #GP stream is a secondary failure during fatal-fault reporting/handling whose exact cause remains unclassified.

An earlier preserved pre-recursive-lock PF and a separate reader-lock/input PF remain in out; they are distinct snapshots. None proves the original #UD producer.

## Later frame-chain stress attempt: kernel-originated ABI-gate panic

The next `-CleanupStress` run rebuilt and staged the frame-chain diagnostic kernel and regenerated payload set. Its serial log is preserved as `out/h2b-cleanup-stress-framechain-fault-preserved.log` (SHA-256 `40DB1B64F78AE3EE5DCEA984936B82BB17266EA84AA288509B9A54B442BDABF5`). The matching kernel is `out/h2b-cleanup-stress-framechain-kernel-preserved.elf` (SHA-256 `F4212AF662FCF381AC433FC96266E55A49BDC55F15D38C57FE6C891316DFCAE8`), map `out/h2b-cleanup-stress-framechain-Kernel-preserved.map` (SHA-256 `9435CDE9F58F73E261E9257D023F1D1345F2F540B4D644A884F8DDE267BEE06D`), and ramdisk `out/h2b-cleanup-stress-framechain-ramdisk-preserved.img` (SHA-256 `B6A6414E21C0A9C04AF987A5F3B53E762474D488A4EB6ED61341C402DCDBCF8B`). Root and ESP kernel copies matched. The protected `out/phase32-current-allowlisted.log` hash remained `200CFBF0270C2FDE1B874711937460BA0F8E81FB5E750FB5A8DA9B8C29CAF00C`.

The host harness reports `INPUT_INJECTION_FAILED`: Notepad cleanup stress completed 37 cycles, Calculator/mixed/Computer Files cycles were `0/0/0`, and Start Menu opened 37 times. On the next Start-menu action, immediately after a completed cleanup and before the next `START_MENU_OPENED` marker, the guest emitted one `RING3_ABI_KERNEL_GATE` and entered `Panic.Error("Kernel invoked the Ring3 ABI gate")`. Captured gate fields were `ABI_GATE_RIP=0x27F716`, `ABI_GATE_CS=0x8`, and a raw `ABI_GATE_RSP` slot value `0x27F6B8`. The harness recorded `qemuAtInputFailure=running; qemuExitCode=unknown`; no live QEMU process or QMP connection remained at the subsequent host inspection.

This is not a vector 6/#UD record, and `FAULT_BYTES=` was not expected or emitted by this gate path. No `FAULT_FRAME` lines were emitted because the frame-chain logger is currently attached to CPU exception breadcrumbs, not the `0x80` panic path. `STACK_WINDOW_UNAVAILABLE` was emitted by the stack-neighborhood check. Since CS is ring 0, the processor did not push an RSP/SS pair for this same-privilege software interrupt; therefore the logged `ABI_GATE_RSP` is a raw generic-frame slot, not a trusted interrupted RSP. The logged RIP `0x27F716` lies within the boot stack mapping `0x200000..0x280000` and outside the kernel ELF's preferred load range beginning at `0x10000000`. This is evidence of an unexpected kernel return/control location in the stack region if the saved frame is valid, but no instruction bytes or stack contents were captured, so there is no disassembly and no proven producer. Do not equate this panic with the historical #UD or the separate vector 14 PF.

Immediately before the gate, the last launch record named `instance-1-42` for `gxos.builtin.notepad`. The just-cleaned Notepad window was `serialObject=260816896`, `ownerId=109`, concrete type `Notepad`, selected already disposed, with `Notepad.Dispose->Window.Dispose`; its ApplicationInstance fields were already detached/`none`, it was removed from the manager list, and taskbar reconciliation reached zero. An `Unsaved changes` `Window` with `ownerId=110` was also closed. These are adjacent records only; no exact fault-time window, ApplicationInstance, focus target, or cleanup pass identity was captured. The panic followed `APP_RUNTIME_START_TOGGLE`, so the adjacent Notepad disposal is not established as its cause.

The stress summary sampled `freeInvalid` as zero at boot, idle, before and after Notepad, one Notepad, and ten Notepad. Per-window cleanup records also remained zero for `freeInvalid`, `freeNoPages`, and `freeCorrupt`; taskbar cleanup returned to zero entries. This validates allocator telemetry for the captured 37-cycle cohort but does not satisfy the remaining 50-cycle Notepad gate or any other stress cohort. No #UD recurred; the original historical RIP bytes remain unavailable.

## Cleanup, dispatch, and ownership audit

- The cleanup path serializes manager operations, disposes a selected closed window, detaches App Model ownership, removes it from the manager list, and reconciles taskbar projection. Completed Phase 32 and completed Notepad cycles show no stale taskbar entry.
- The bounded cleanup record stores the concrete type label, EEType/method-table pointer, object address, allocator owner ID, AppInstance handle/generation, lifecycle/owns-window state, taskbar relationship, pre/post disposed state, and a type-specific dispatch label. In the 37-cycle AppRuntime run, Notepad objects used `Notepad.Dispose -> Window.Dispose`; the final object was `serialObject=260669440`, `ownerId=109`, already disposed at selection, detached from App Model/taskbar, still present in `WindowManager.Windows` at index 7, then removed before the cleanup call to virtual `Dispose`. Calculator cleanup records used `Window.Dispose`; Task Manager records used `TaskManager.Dispose -> Window.Dispose`. The source confirms Computer Files has an override that delegates to `Window.Dispose`, but the stress run ended before a Computer Files cleanup. The dispatch label is type-derived from the audited override table; a raw resolved vtable target address was not captured. The current fault does not identify any disposed object as its cause.
- Task Manager uses the authoritative manager removal/disposal helpers. Those helpers do not synchronously reconcile taskbar state; the regular cleanup pass performs reconciliation. Completed controls show the resulting projection empty after target cleanup. A full desktop launch/activate/close matrix was not completed.
- _nextWindowOwnerId is incremented under the WindowManager lock and does not derive from List<T>.IndexOf. It is a signed 32-bit counter that resets to 1 after signed wrap, so no alias was observed in this test range but lifetime-wide uniqueness beyond wrap is not guaranteed. The failed AppRuntime run logged 40 distinct Notepad cleanup-selection IDs, from 2 through 109, including setup/baseline selections; the 37 scripted stress closes showed no active owner alias.
- Remaining Window list `IndexOf` uses are transient membership or current-position lookups, including `Window.Index`, compatibility/presence checks, current z-order, and diagnostic reference lookup. No durable allocator owner ID is derived from a positional index. A source search found structural `Windows.Add`/`RemoveAt` operations only in `WindowManager`; Task Manager uses `DisposeAndRemoveWindow` / `DisposeAndRemoveWindowAt`. Those helpers run under the manager lock, call `Dispose` so App Model ownership is detached, then remove every identical list reference. They rely on the regular cleanup reconciliation for taskbar projection rather than synchronously reconciling it in the helper. No runtime test specifically sampled focus during a Task Manager removal.
- Allocator ownership uses the stable owner ID carried by each window; registration and allocator context set/restore are performed under manager operations. No owner ID wrap was approached in testing.
- Serialized cleanup code holds the reentrant manager lock, marks a pending pass on same-thread nested requests, and iterates after the active pass instead of recursing; `finally` clears running/pending state on ordinary managed exits. Fresh Phase 32, AppRuntime cleanup stress, and the later failed Phase 31 attempt recorded zero nested requests and zero second-pass starts, so the pending-second-pass behavior, exception-path projection reconciliation, SMP lock ownership, and starvation behavior are source-audited but not dynamically demonstrated. The prior nested marker came from the broken lock interleaving and is not accepted as current proof. No evidence of a lost closed window appeared in completed runs.
- The H2b audit found System.Threading.Monitor maps every managed object lock to the same API lock. Previously ThreadPool.Lock() skipped nested acquisition while API_Unlock() released the outer lock. ThreadPool.Lock() now tracks same-CPU recursion depth and releases at the outermost exit; the API always enters it when managed locking is available. This corrected repeated Phase 32 cleanup/balance inconsistencies. Validation used QEMU single-vCPU runs; SMP atomic acquisition and starvation behavior remain unproven.

## Allocator invalid-free telemetry

Allocator.Free has one _freeFailInvalidPtr++ site. It rejects an address below the allocator arena, an unaligned address, or an address beyond the arena. freeNoPages and freeCorrupt have separate rejected-free/corrupt-run sites.

The nonzero historical counter was a real production invalid free, not an intentional negative test. Base Corlib/System/Object.Dispose() passed arbitrary managed object references to native stdlib.free; NativeAOT objects/strings outside the allocator arena were rejected. The bounded first-unique caller telemetry identified stdlib.free from the String.Concat path. The fix in Corlib/System/Object.cs routes through Allocator.FreeManagedObjectIfAllocatorRun, which accepts only the exact start of a live allocator run. Resource-owning overrides retain their explicit release behavior.

No intentional diagnostic increment source was found. The old Phase 32 count of 2726 is classified as production invalid frees. With the fix, captured counter values remained zero in fresh Phase 31, fresh Phase 32, AppModel/lifecycle/grouping, and the AppRuntime Notepad stress through all 37 completed closes. freeNoPages and freeCorrupt also remained zero. The requested full baseline table is incomplete:

| Measurement | Captured result |
|---|---:|
| Boot / idle | 0 in the current diagnostic runs |
| One Calculator open/close | Not measured after final changes |
| One Notepad open/close | 0 |
| 10 Calculator cycles | Not measured |
| 10 Notepad cycles | 0 in the current AppRuntime stress run |
| Fresh Phase 31 delta | 0 |
| Fresh Phase 32 delta | 0 |
| AppRuntime delta | 0 through 37 completed Notepad closes before the later kernel ABI-gate panic |

Calculator baselines are missing because the PF stopped the targeted cohort before Calculator and mixed-app runs. The counter itself did not rise in the completed production close cycles.

## Targeted stress matrix

| Cohort | Required | Completed evidence | Result |
|---|---:|---:|---|
| Notepad open/close | 50 | Earlier run captured vector 14 after 37 cycles; later frame-chain image reached 37 cycles then hit `RING3_ABI_KERNEL_GATE` at the next Start Menu action | Failed/incomplete |
| Calculator open/close | 50 | 0 | Not run |
| Alternating Calculator/Notepad | 25 | 0 | Not run |
| Phase 32-style launch/termination | 10 | 10 clean post-recursive-lock sequences, each completed; one direct control plus nine saved sequence logs | Passed |
| Natural nested-cleanup pressure | Exercise if production triggers it | No nested request in final-lock run; previous marker preceded recursion correction | Not demonstrated |

No vector 6, vector 13, or other cleanup fault occurred in the 37 completed Notepad cycles; the separate vector 14 guest fault prevents a clean stress result. Cleanup counters and taskbar projection returned to the observed baseline after each completed close. No evidence identifies the PF with the immediately preceding Notepad object.

## Fresh controls

- Phase 31: out/h2b-phase31-recursive-lock.log passed four primary 31 results, negative cases, FailFast/replacement 31, cleanup, App Model/process balance, hash match, and zero guest fault. freeInvalid=0. This predates the latest payload regeneration. The later run out/h2b-phase31-post-h2b-final-build.log is not a passing fresh control: all five proof payloads were read, but runtime rejected them with ARTIFACT_HASH; successful return lifetimes=0 and RING3_PHASE31_COMPLETE=0. The failure is an artifact/allowlist mismatch before the proof code ran, not a Phase 31 lifecycle result.
- Phase 32: out/h2b-phase32-recursive-lock.log passed five 32 lifetimes, negative cases, stale-owner, FailFast/replacement, six target terminations, cleanup-end, windows=6:6, App Model/process balance, zero guest fault, and freeInvalid=0.
- Repeated Phase 32 sequences: out/h2b-phase32-seq-02.log through ...-10.log are nine additional clean runs after the recursive-lock correction. seq-01 predates the correction and is excluded.
- AppModel: out/h2b-appmodel-recursive-lock.log passed. Lifecycle self-test 15/15, taskbar/grouping 16/16, zero stale projection, no cleanup fault.
- Calculator/Notepad/Computer Files normal desktop cycle, taskbar activate behavior, Start artwork, graphics, and a dedicated ThreadPool regression: not completed as a separate current matrix. Relevant UI/cleanup code ran in the controls above, but that does not substitute for these gates.

## Ordinary build and generated artifacts

The latest ordinary `build.ps1` run completed with exit code 0 and `Build Complete!` in `out/h2b-full-build-post-byte-capture.log`. Phase 26 host SDK build summaries reported zero errors. The build emitted 234 warning lines, mostly the repeated NativeAOT package-reference warning (`Delete explicit Microsoft.DotNet.ILCompiler package reference`) and linker/native warnings; none stopped payload staging.

The ordinary build regenerated the ramdisk from 559 files. At completion, `ramdisk.img` and `ESP/ramdisk.img` both hashed to `D2E99849278D67B6DB56CA991FDD6840CD6D8A05C4BF280BD5520FA33A0020B6`, `kernel.elf` and `ESP/kernel.elf` both hashed to `341629F410BB4620D28898C5CD6C006D9231C03B31387618A9C0670D0287694F`, and the staged bootloader pair hashed to `9C6303E11E3C82D5347CA33687347AA53CC6EC2CCD41DF6B57EB89582175769D`. EFI staging completed. A subsequent Phase 31 diagnostic build completed and staged its own diagnostic image; the current kernel pair hash is `F2F615964092CB859DC916BCB28CD12BBDE44C2F5ADE934C6D459DEBFB25272C`, its ramdisk pair hash is `1782073C2105383D82BA0E8414E6E3D5E55FA3EE8F1DE124B9B797FB5064906B`, and the staged bootloader hash is `F4E1206AF8BBFF64AFBA46CB7EF0CF17AE3FFB2E748D82572069E7B35D3AD7B5`. The guest emitted heartbeat with `freeInvalid=0` during the failed Phase 31 artifact check; it had no guest fault and the host stopped the run after confirming the rejection, before its validation timeout.

Phase 29–32 `.exe`/`.gxmi` pairs and `ramdisk.img` are tracked outputs in the repository and intentionally regenerated by the build/staging workflow. A post-build audit checked all 14 Phase 29/30 payloads: the source executable hash matched its build record and staged executable, and rebuilding each descriptor reproduced the staged `.gxmi` byte-for-byte. The Phase 30 wire-layout validation passed. All five Phase 31 and all three Phase 32 static artifact validators passed during staging. A separate current-image audit compared each of the 22 Phase 29–32 staged executable SHA-256 values with both its descriptor SHA and the compiled `ManagedImage` allowlist: descriptor agreement was 22/22, but allowlist agreement was 0/22. The fresh Phase 31 QEMU run reproduced this mismatch dynamically: its regenerated success executable SHA-256 was `1A3D8150CBD52B9D69D1F5FB2EC397EF9C397265CEA6A0F7707DFE7EA316277B`, and its descriptor/build record agreed, while the running kernel allowlist expected `4F2EA0980776DC1209B39014DA2B1F72C1534FE6CBA8C7E8AB475F33B7A6CD05`. The kernel emitted `PHASE26_HASH_MATCH=0` and `PHASE31_IMAGE_CREATE_REJECTED=ARTIFACT_HASH`. Thus build-time descriptor/static validation does not establish runtime allowlist compatibility for freshly regenerated payloads. Phase 32's new payloads were statically found mismatched but not dynamically run. The tracked generated files remain in place; do not discard them merely because they are generated. Resolve the generator/allowlist provenance mismatch before treating a fresh ordinary build as a usable Phase 31/32 release image.

## Git and closeout boundary

Final root HEAD remains `996c300b5130b5e11aeceeff74dda5ac2cb74d77`, branch `main`, upstream `origin/main`, ahead/behind `0/0`. The worktree has 20 modified source/build files, 44 modified tracked Phase 29–32 `.exe`/`.gxmi` payloads, modified tracked `ramdisk.img`, and this untracked closeout report. The 20 source/build paths are `Corlib/System/Object.cs`; `Kernel/API.cs`; `Kernel/Libraries/stdlib.cs`; `Kernel/Misc/Allocator.cs`; `Kernel/Misc/EntryPoint.cs`; `Kernel/Misc/IDT.cs`; `Kernel/Misc/ManagedImage.cs`; `Kernel/Misc/NativeBootstrap.cs`; `Kernel/Misc/PNG.cs`; `Kernel/Misc/Process.cs`; `Kernel/Misc/Ring3Abi.cs`; `Kernel/Misc/Ring3Proof.cs`; `Kernel/Misc/Threading.cs`; `Kernel/Tools/USBMassHash.cs`; `build.ps1`; `guideXOS/GUI/Window.cs`; `guideXOS/GUI/WindowManager.cs`; `guideXOS/guideXOS.csproj`; `guideXOS/native_stubs.asm`; and `run_uefi_validation.ps1`. `git diff --check` passed. The nested `out/rt` checkout remains detached at `9d5a6a9aa463d6d10b0b0ba6d5982cc82f363dc3` with 58 modified/untracked paths; it was not cleaned or committed. The Server checkout has no tracked changes, but its final status includes unrelated untracked PacMan validation files/logs and an AppModel regression output directory; H2b did not edit or clean those entries. No Legacy checkout exists at the inspected workspace roots. The protected Phase 32 allowlisted log still hashes to `200CFBF0270C2FDE1B874711937460BA0F8E81FB5E750FB5A8DA9B8C29CAF00C`.

Because this is Outcome F, there is no H2b commit or push. The latest documentation/source and generated-artifact edits remain uncommitted. Run git diff --check at final review, but do not convert this incomplete forensic result into a clean acceptance.

## Phase 33 gate

Phase 33 remains paused. The allocator production invalid-free producer is fixed, and earlier Phase 31/32/AppModel controls passed; the current vector 14 PF, later kernel-originated ABI-gate panic after 37 Notepad cycles, incomplete stress matrix, and fresh Phase 31 runtime hash rejection block Outcome A/B and release. Next work should instrument the kernel-originated `0x80` panic path to capture bounded bytes around its saved RIP and validate the correct same-privilege stack frame, then resolve the current PF/control-flow anomaly, align regenerated Phase 29–32 payloads with the runtime allowlist, and rerun fresh controls. Keep the historical #UD instruction unavailable unless a preserved historical byte source is actually found.
