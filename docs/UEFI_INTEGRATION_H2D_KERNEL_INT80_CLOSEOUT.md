# guideXOS C# UEFI — H2d kernel `int 0x80` closeout

Date: 2026-09-29  
Repository: `D:\dev\guideXOSUEFI`  
Branch: `main`  
Starting and ending HEAD: `22d3f5fcd24509382a5180dbea56ef726cfc39e3`  
Outcome: **F — the original CPL0 syscall producer is fixed, but cleanup stress is incomplete.**  
Phase 33 remains paused.

## Result

The H2c `RING3_ABI_KERNEL_GATE` producer was a shared NativeAOT P/Invoke resolver. In the kernel, `ThrowHelpers.ThrowIndexOutOfRangeException` called `[DllImport("Error")]`; the resolver emitted a three-byte `CD 80 C3` trampoline into a stack local and invoked it from CPL0. The saved CS and caller bytes prove that path. The kernel now uses typed direct APIs for `Error`, `GetTime`, and `StartThread`, and its generic dynamic P/Invoke resolver fails closed. The Ring 3 syscall gate remains strict.

After that repair, the minimum reproductions pass on the current source and current final diagnostic image:

- Start visibility only: **100/100** open/close cycles, no app launch and no ABI/fault markers.
- Start plus foreground switching: **50/50** cycles; the same Calculator handle was reactivated every time.

The requested Notepad cleanup stress did not complete. At Start operation 73, QEMU recorded a kernel #GP sequence that recursed through the #GP handler, then a #PF and a triple fault. QMP reported guest status `shutdown`. The original ABI gate did not recur, but this separate guest failure prevents the remaining stress and release gates from being claimed. The closeout is therefore Outcome F; do not resume Phase 33.

## Preflight and repository state

| Item | Result |
|---|---|
| Root repository | `D:\dev\guideXOSUEFI` |
| Branch / HEAD | `main` / `22d3f5fcd24509382a5180dbea56ef726cfc39e3` |
| Upstream / ahead-behind | `origin/main` / `0 0` |
| Root source status | Modified H2d files listed below; this closeout document is the only root untracked file |
| Commit / push / branch / worktree / stash | None |
| Nested `out\rt` | Detached HEAD `9d5a6a9aa463d6d10b0b0ba6d5982cc82f363dc3`; pre-existing modified and untracked runtime files remain. This task did not build or write there. |
| `D:\dev\guideXOSServer` | Clean on `main...origin/main`; not modified |
| `D:\dev\guideXOS` | Existing `main` state is ahead by 1 with pre-existing untracked paths; not modified |

The ordinary full `build.ps1` was not run. Diagnostic builds used `-SkipBootloader -SkipRamdisk` so the nested runtime tree and accepted H2c inputs were not rebuilt.

## Preserved H2c failure and trigger timeline

Authoritative log: `out\h2c-cleanup-stress-first-failure-preserved.log`  
SHA-256: `AE85D061E7401E6CA24307E46FCA2D276145D2052395F0579143D2EE61CCD1FC`

The first gate is at line 10699. The relevant sequence is:

1. `APP_RUNTIME_START_TOGGLE;registered=1;before=0` and `START_MENU_OPENED` (lines 10604–10605).
2. Start click edge at `x=30;y=780` (line 10609).
3. Window cleanup pass-1 begin/end/exit markers continue while the Start opening work is in flight; the final pass markers immediately precede the gate (lines 10697–10698).
4. `RING3_ABI_KERNEL_GATE` is emitted at line 10699.

Captured gate state:

| Field | Value |
|---|---|
| Reason | enum `0x02`, `KERNEL_ORIGINATED_INT80` |
| Saved RIP / CS / RSP | `0x27F656` / `0x8` / `0x27F5E0` |
| CR3 | `0x3DD73000` |
| Saved RAX candidate | `0` (the gate rejected before syscall dispatch; no valid syscall operation was accepted) |
| Saved RDI / RSI / RDX | `1` / `0x10113ED0` / `1` |
| Current CPU / scheduler thread | CPU 0; kernel thread, not a user thread; not terminated |
| Current process | absent |
| Allocator owner | `0x1000FFF` |
| Window cleanup | pass 1 running, pending false |
| Saved direct return candidate | `0x1000933A`; the frame chain is explicitly nonmonotonic (`previous RBP=0x487`) and is not a reliable call chain |

Bytes around the saved RIP end in `CD 80`. The bytes are in the stack-local executable trampoline, not a kernel text symbol. The H2c kernel ELF hash was `7A624E8001D7686F1397CF52BD67B2E49D90D4CA78747D2B4221BFA633C22348`; the H2c map was not preserved, so the return candidate is not assigned an H2c symbol.

## Complete `int 0x80` inventory

The build-input search covered `Corlib`, `Kernel`, `guideXOS`, and `Tools`, including literal instructions and emitted bytes. The current ForegroundStress kernel ELF disassembles to **24** `int $0x80` instructions, all matching the user-payload templates below. No kernel text helper contains an executable `int 0x80` after the repair.

| Source / symbol | Sites | Intended privilege | Callers and disposition |
|---|---:|---|---|
| `guideXOS/native_stubs.asm`, `R3PayloadStart`–`R3PayloadEnd` | 4 (source lines 405, 419, 436, 441) | CPL3 | `Process.TryCreate` obtains payload start/size, copies it to the user-code page, and creates a user thread. |
| `guideXOS/native_stubs.asm`, `R3DirectPayloadStart`–`R3DirectPayloadEnd` | 3 (447, 452, 457) | CPL3 | Same copy-and-enter path for the direct-return Ring 3 proof payload. |
| `guideXOS/native_stubs.asm`, `R3InvalidPayloadStart`–`R3InvalidPayloadEnd` | 8 (465, 470, 474, 479, 483, 487, 489, 492) | CPL3 | Same payload-copy path for negative syscall validation. |
| `guideXOS/native_stubs.asm`, `R3InvalidServicePayloadStart`–`R3InvalidServicePayloadEnd` | 9 (501, 507, 512, 517, 529, 535, 541, 547, 550) | CPL3 | Same payload-copy path for invalid service-request validation. |
| `Tools/Phase25/bootstrap.asm` | 4 (169, 187, 226, 233) | CPL3 | Phase 25 Ring 3 bootstrap tests. |
| `Tools/Phase26/bootstrap.asm`; `Tools/Phase26/guidexos_phase26_syscall.asm` | 2 + 1 (109, 118; 25) | CPL3 | Phase 26 bootstrap and its syscall wrapper. |
| `Tools/Phase27/bootstrap.asm` | 2 (106, 115) | CPL3 | Phase 27 Ring 3 bootstrap tests. |
| `Corlib/Internal/Runtime/CompilerHelpers/InteropHelpers.cs`, non-`Kernel` branch | One emitted `CD 80 C3` byte template, not a literal assembly mnemonic | CPL3 only | Managed user-mode P/Invoke resolver; kernel callers have been removed and the `Kernel` branch fails closed. |
| `Kernel/Misc/IDT.cs`, `Kernel/Misc/UserDemo.cs` | No executable sites | Diagnostic/comment only | Gate text and comments only; no production instruction. |

The 24 static instructions are stored in the kernel ELF as byte templates. `Kernel/Misc/Process.cs` copies the selected template to the user-code physical page before `Thread.CreateUser`; their ELF storage location does not make them CPL0 call sites. The final ForegroundStress disassembly contained 24 sites and no kernel resolver trampoline.

## Start foreground route and exact original producer

The Start menu is a kernel UI object. When `StartMenu.OnSetVisible(true)` runs, it:

1. records the visibility operation and calls the base visibility hook;
2. calls `ApplicationInstanceRegistry.NotifyShellForeground()` directly;
3. the registry deactivates the currently active application instance and clears the active foreground owner;
4. moves Start to the front and builds the blurred background cache.

This foreground notification is an internal App Model call. It does not itself issue a syscall and does not install a Ring 3 callback. The current application is allowed to be deactivated from a kernel thread; the architectural error was reaching a Ring 3 ABI instruction from that context.

The original producer was **H2d-A — direct kernel execution of a user ABI helper**:

```text
kernel Start/cleanup work
  → managed exception helper
  → ThrowHelpers.Error("Error") via [DllImport("Error")]
  → InteropHelpers.ResolvePInvoke
  → stack-local bytes CD 80 C3
  → int 0x80 while saved CS = 0x8
```

`ThrowHelpers.ThrowIndexOutOfRangeException` was active in the failed path. The saved return candidate near `Allocator.FindOrAddOwner` is not strong enough to establish that the allocator caused the exception; the H2c frame chain is corrupt/nonmonotonic. The exact control-flow reason for that managed exception remains unproven. The proven defect is that its kernel exception path used the shared dynamic P/Invoke resolver that emitted the user-mode trampoline.

## Architectural repair

| Kernel caller | Old shared route | New kernel route |
|---|---|---|
| `ThrowHelpers.Error` | `[DllImport("Error")]` to the dynamic resolver | `guideXOS.API.API_Error` |
| `DateTime.GetTime` | `[DllImport("GetTime")]` | `guideXOS.API.API_GetTime` |
| `Process.StartThread` | `[DllImport("StartThread")]` | `guideXOS.API.API_StartThread` |
| Any unexpected dynamic kernel P/Invoke | Could reach the same resolver | `ResolvePInvoke` under `Kernel` calls `Panic.Error("Unsupported dynamic kernel P/Invoke", false)` and returns; it does not emit `CD 80 C3` |

The corresponding Ring 3 services remain available through `Kernel/API.cs` dispatch and `Ring3Abi.Dispatch`. The `int 0x80` gate remains CPL3-only: a kernel-originated vector `0x80` still emits `RING3_ABI_KERNEL_GATE` and panics.

## Minimum reproduction results

### Gate 8 / Gate 19: Start visibility only

Final-source run: `out\h2d-start-only-final.serial.log`  
SHA-256: `00278AA55E63BCF61EA14A4F80ADA61CA5850DDA7A4DB422239683848A936340`

- 100/100 Start open/close cycles; 100 open toggles, 100 close toggles, 100 `START_MENU_OPENED` markers.
- No launches; zero ABI gate, #UD, #PF, #GP, or panic markers in the serial log.
- Graphics invariants true; mouse routed, no dropped mouse packets; keyboard transitions balanced. The GUI-key marker is false because this workload dismisses Start through the QMP monitor key path.

### Gate 9 / Gate 19: Start plus foreground switching

Final current ESP kernel: SHA-256 `D983F1CC9A06BC86A68DCBCA5818FF9431237213FA5A5A098D973482A959F3F4` (kernel and `ESP\kernel.elf` match).  
Final-source run: `out\h2d-foreground-stress-final2.serial.log`  
SHA-256: `F8D1DADCB7C55F752DF7B945DE4AF4069F47429B8926D4BBB105E67066734332`

- Calculator launched once; 50/50 Start open, Escape close, and taskbar reactivation cycles.
- Every taskbar activation restored the same handle, `4294967302`.
- Start opened 51 times including the launch setup; no ABI gate or guest fault marker.
- Graphics invariants true; mouse routed with zero dropped mouse packets and balanced keyboard transitions.

## Cleanup stress: separate unresolved post-fix failure

The original cleanup workload was resumed with the H2d service-path repair. The diagnostic serial stream stops during Start operation 73 at `APP_RUNTIME_START_BLUR_TMP_ALLOC_BEGIN`; it does not reach the corresponding allocation-complete or blur-complete markers. The workload never emitted its completion marker and did not reach 50/50 Notepad cycles.

Preserved serial log: `out\h2d-cleanup-qemu-debug.serial.log`  
SHA-256: `C331AA164C322D7EF386411A739A9C2990586B08324DCF354D8ED845CA550DC3`

Preserved QEMU trace: `out\h2d-cleanup-qemu-debug.serial.qemu-debug.log`  
SHA-256: `6BA65A575B1C7079C604320560E667673E4D66EFF5EFE2B9AEAEC9639CBBE919` (124,602,630 bytes)

Observed evidence:

- QMP reported guest status `shutdown`; QEMU recorded `Triple fault`.
- The first QEMU #GP record had CPL0 RIP `0xB86617010F662EFE`, a noncanonical address, so it cannot be mapped to a kernel symbol.
- A later #GP record was at `0x1000D471`. The map used during that run resolved the address within `RhpStelemRef`; the instruction at that offset attempted `movzx ecx, word ptr [rcx]` with `RCX=0x9058EBD08EE88EE0`. This is a noncanonical object/type pointer observation, not a proven origin of the earlier invalid RIP.
- The #GP handler then recursed at `isr13` (`0x10001FD4`) while the recorded stack moved down by `0x30` per entry. A later #PF record had `CR2=0xFFFFFFFFFFFFFFF8` with `RSP=0x20`, followed by the triple fault.
- QEMU recorded 21,838 vector-13 records and 2,090 vector-14 records in this recursive failure stream. These are repeated low-level records, not counts of independent application faults. No vector-6 record occurred.
- The serial stream contains no `RING3_ABI_KERNEL_GATE`, `ALLOC_OWNER_COUNT_MISMATCH`, or `ALLOC_FREE_INVALID` marker. The task did not complete, so allocator counters and the rest of the cleanup matrix cannot be closed out.
- The one nested-cleanup request and one pass-2 marker were observed before the failure, but the complete workload did not reach its runner-side final balance check.

At analysis time, the map used with cleanup image hash `282499587442C6329720880B4CEB923DFC36F042254E72928F477487407EBF4B` placed the secondary `0x1000D471` event inside `RhpStelemRef`. That map was subsequently replaced by the final ForegroundStress map and was not saved separately. Do not symbolize this old QEMU address with the current map: the current build places `RhpStelemRef` at a different address. The root producer of the noncanonical RIP and the malformed object/type pointer remains unidentified; this secondary failure is not attributed to the fixed CPL0 syscall path.

The allocator was returned to the accepted H2c source unchanged. Temporary H2d bounds changes and large-allocation serial telemetry were removed because the evidence did not justify changing allocator ownership semantics.

## Remaining gates and release decision

| Gate / area | H2d status |
|---|---|
| 0–7: preflight, evidence, inventory, ABI trace, privilege proof | Original producer proven and repaired; see sections above. H2c ELF map was not retained, and the H2c frame-chain return candidate is not reliable. |
| 8: Start only, 100 cycles | Pass, final source |
| 9: Start plus Calculator foreground switching, 50 cycles | Pass, same handle throughout, final source/current kernel |
| 10–12: Notepad lifecycle, cleanup minimization, allocator causality | Incomplete; cleanup run triple-faulted at operation 73. Allocator cause is not established. |
| 13: delegate/function-pointer audit | No bad callback is indicated by the proven H2d-A producer; a broad unrelated callback audit was not completed. |
| 14–18: address-space, classification, repair, direct APIs, gate | Original producer classified H2d-A and repaired. The separate post-fix invalid-RIP fault is unresolved. |
| 19: minimum reproductions after fix | Pass: 100 Start-only and 50 foreground switches. |
| 20–25: Notepad/Calculator/mixed/Phase32/Phase31/Phase32 | Not completed after the cleanup triple fault. |
| 26–27: historical PF closure | Not closed. The observed #PF is part of the current #GP-handler cascade, not proof about the historical PF. |
| 28: nested cleanup second pass | Pass-2 markers were observed once, but the full final cleanup workload did not finish. |
| 29: complete allocator telemetry | Not closed; no invalid-free/owner-mismatch markers before the abort, but full deltas were not measured. |
| 30: App Model, lifecycle, grouping | No fresh H2d run. H2c acceptance remains prior evidence only. |
| 31: artwork/input/taskbar controls | Start artwork/graphics and Calculator taskbar restoration passed in the two minimum repros; the complete control matrix was not run. |
| 32: ordinary full build | Not run. The final diagnostic ForegroundStress build completed with the ramdisk and bootloader skipped. |

Prior accepted H2c evidence remains: Phase 29, Phase 30, Phase 31, and Phase 32 proofs passed before H2d; managed artifact identities were 22/22. They were not rerun as fresh post-H2d release gates.

Final diagnostic ForegroundStress build log: `out\h2d-foreground-stress-build-final.log`. It reports `Build Complete!`, `MANAGED_ARTIFACTS=22`, descriptor agreement `22/22`, and staging agreement `22/22`. This diagnostic log does not provide a fresh allowlist agreement result. The accepted H2c allowlist result remains prior evidence, not a fresh H2d check.

| Artifact | SHA-256 |
|---|---|
| `kernel.elf` and `ESP\kernel.elf` (current final diagnostic image) | `D983F1CC9A06BC86A68DCBCA5818FF9431237213FA5A5A098D973482A959F3F4` |
| `ramdisk.img` and `ESP\ramdisk.img` (not rebuilt) | `21292E9610FF76D2C33C3A89A1A91CBA43B661A08BBB663233C06E866ADF8E28` |
| `ESP\EFI\BOOT\BOOTX64.EFI` (not rebuilt) | `AB8CCCA702F4D10BEFB0C9464408B03221C327192C3558A99509215EF2856D86` |

**Phase 33 decision: remain paused.** Resume only after the post-fix #GP/triple-fault producer is identified and repaired, then complete the remaining requested stress, proof, telemetry, App Model, and ordinary full-build gates. The historical PF remains open; do not infer its producer from this separate fault cascade.

## Source files changed

- `Corlib/Internal/Runtime/CompilerHelpers/InteropHelpers.cs`
- `Corlib/Internal/Runtime/CompilerHelpers/ThrowHelpers.cs`
- `Corlib/System/DateTime.cs`
- `Corlib/System/Diagnostics/Process.cs`
- `Kernel/Misc/IDT.cs`
- `build.ps1`
- `guideXOS/GUI/StartMenu.cs`
- `guideXOS/Program.cs`
- `guideXOS/guideXOS.csproj`
- `run_uefi_validation.ps1`
- `Docs/UEFI_INTEGRATION_H2D_KERNEL_INT80_CLOSEOUT.md`

`Kernel/Misc/Allocator.cs` has no final diff. No commit or push was made.
