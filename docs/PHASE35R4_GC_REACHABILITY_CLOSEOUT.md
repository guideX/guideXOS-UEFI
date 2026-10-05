# Phase 35R4: `RhpNewArray` retention and reclamation audit

Date: 2026-10-05  
Status: allocation-boundary forensic result; no production teardown change made  
Outcome: **F — all 26 request-attributed retained allocations in the fresh proof run are `System.String` objects allocated by the kernel's GC-unaware `RhpNewArray` override. Two earlier owner-0 survivors are `object[]` and `int[]`. Their concrete managed caller sites and safe reclamation ownership remain unresolved.**

## Preflight and protected evidence

- Root HEAD at the start of the phase: `5fac894f3b70ab10491710fd8ea414a4d1f6a4ca` (`main`), tracking `origin/main`, ahead/behind `0/0`.
- Root worktree: `D:/dev/guideXOSUEFI`; the existing Phase 35R, allocator diagnostics, and DSDT edits were preserved.
- Nested runtime checkout `out/rt`: HEAD `9d5a6a9aa463d6d10b0b0ba6d5982cc82f363dc3`, detached, with existing modifications and untracked files preserved.
- Protected R3 serial log SHA-256 remains `D93EC34D646A474BAA67DE245969CBF3316F4B40F67464C64A7CB6C943FC3D14`.
- No reset, clean, stash, rebase, branch, worktree, commit, or push was done.

## Allocation path

The active kernel CoreLib override is `Corlib/Internal/Runtime/CompilerHelpers/StartupCodeHelpers.cs`, method `RhpNewArray(EEType* pEEType, int length)`. It calculates `BaseSize + length * ComponentSize`, rounds up to 8 bytes, calls kernel `malloc`, zeroes the block, writes the EEType pointer and length, then returns the object reference. It has no GC registration, allocation context, segment, or collector call. `RhpNewFast` uses the same malloc model.

The diagnostic malloc import reaches `Allocator.Allocate`; every request of 1–4096 bytes receives a dedicated one-page run. Thus each surviving object below starts at its allocator page-run address; none is a slot in a GC slab. A larger object would receive multiple allocator pages.

The separate Phase 35 requester executable contains `Runtime.WorkstationGC` (`RhpNewArray`, `RhpNewArrayRare`, `RhpGcAlloc`, and `gcwks.cpp`). That does not make the kernel malloc objects GC objects. In the first fresh kernel build, `RhpNewArray` starts at `0x10011978` in `guideXOS/Kernel.map`; `callerAddress=0x100119E9` is the helper's return from its internal malloc call, not its managed caller. In the confirming build the corresponding values were `0x10011980` and `0x100119F1`. R3 recorded the internal helper call as `0x100110D1`.

## Fresh allocation-boundary capture

A diagnostic-only hook was added after `RhpNewArray` initializes the object. For request-scoped arrays and strings it records allocator ID, object address, EEType and component EEType addresses/codes, component size, element count, aligned object bytes, rank, classification, generation, and allocator return address. Request-0 logging was briefly enabled for arrays during the confirmation attempt; ordinary startup produced tens of thousands of events, so it was narrowed back to request-scoped capture. At the original-success matrix point it scans live native-runtime malloc runs newer than the proof baseline, checks for an EEType address in the kernel image range, and emits array/string metadata and owner. The hook prints fixed scalar fields, does not format managed type names, and does not free memory. The code is gated by `UEFI_DIAGNOSTIC_RING3_PHASE35`.

The targeted kernel builds succeeded with:

```powershell
.\build.ps1 -SkipBootloader -SkipRamdisk -UefiDiagnosticMode Ring3Phase35R2
```

Then a fresh one-boot Phase 35Q matrix completed with:

```powershell
.\Scripts\run_phase35q_storage_validation.ps1 -SkipBuild -Phase35R2Matrix -TimeoutSeconds 1800
```

The script restored the generated fixture image to its initial SHA-256 before cleanup. The first clean fresh serial log is `out/phase35q/serial-ce48f9c57272411a9232bd6c49c787b7.log`. It contains 2,074 allocation-boundary events for the six proof reads and a live dump of 28 objects (`truncated=0`). A second confirming guest run completed with all four matrix results passing and the same 28-row live set; its expanded request-0 trace reached 78 MB because kernel startup creates many arrays, so that trace is not used for counts. The original-success request produced 28 net allocator pages: 26 request-attributed strings plus two earlier owner-0 arrays. Requests 1–6 retained 4, 4, 6, 4, 4, and 4 runs respectively. The subsequent no-read, one-read, and two-read controls added 0, 4, and 8 pages. All were in one guest boot, not isolated cold boots.

## Retained request objects

All 26 live request objects have EEType `0x100ECA60` in the first clean run (and `0x100ECA68` in the second build), EEType element code `0x14`, component size 2, `IsString=true`, and allocator return address within `RhpNewArray` (`0x100119E9` first run; `0x100119F1` second run). The length is read from the initialized string header. With `BaseSize=22`, object bytes are `align8(22 + 2 * length)`.

| Request | Result | Object address | Length (UTF-16 chars) | Object bytes | Allocator run |
|---:|---|---:|---:|---:|---|
| 1 | success | `0x08597000` | 2 | 32 | same address, 1 page |
| 1 | success | `0x08598000` | 30 | 88 | same address, 1 page |
| 1 | success | `0x08599000` | 1 | 24 | same address, 1 page |
| 1 | success | `0x0859A000` | 27 | 80 | same address, 1 page |
| 2 | success | `0x0859B000` | 2 | 32 | same address, 1 page |
| 2 | success | `0x0859C000` | 30 | 88 | same address, 1 page |
| 2 | success | `0x0859D000` | 1 | 24 | same address, 1 page |
| 2 | success | `0x0859E000` | 27 | 80 | same address, 1 page |
| 3 | NotFound | `0x08594000` | 51 | 128 | same address, 1 page |
| 3 | NotFound | `0x08595000` | 50 | 128 | same address, 1 page |
| 3 | NotFound | `0x08596000` | 38 | 104 | same address, 1 page |
| 3 | NotFound | `0x0859F000` | 251 | 528 | same address, 1 page |
| 3 | NotFound | `0x085A1000` | 1 | 24 | same address, 1 page |
| 3 | NotFound | `0x085A2000` | 36 | 96 | same address, 1 page |
| 4 | success | `0x085A5000` | 2 | 32 | same address, 1 page |
| 4 | success | `0x085A6000` | 30 | 88 | same address, 1 page |
| 4 | success | `0x085A7000` | 1 | 24 | same address, 1 page |
| 4 | success | `0x085A8000` | 27 | 80 | same address, 1 page |
| 5 | success | `0x085A9000` | 2 | 32 | same address, 1 page |
| 5 | success | `0x085AA000` | 30 | 88 | same address, 1 page |
| 5 | success | `0x085AB000` | 1 | 24 | same address, 1 page |
| 5 | success | `0x085AC000` | 27 | 80 | same address, 1 page |
| 6 | success | `0x085BD000` | 1 | 24 | same address, 1 page |
| 6 | success | `0x085BE000` | 29 | 80 | same address, 1 page |
| 6 | success | `0x085BF000` | 1 | 24 | same address, 1 page |
| 6 | success | `0x085C0000` | 27 | 80 | same address, 1 page |

The tables join on allocator allocation ID. Request 3's six strings were created before the service returned NotFound, establishing that strings constructed while decoding or resolving a missing path also remain allocated. This disproves a four-allocation-only-on-success model. Matching lengths across successful requests do not prove equal semantic values or caller sites.

## Earlier owner-0 survivors

The live dump found two arrays allocated before request attribution and still live after the original-success proof. Their EEType and bounds are known, but the dump does not identify their initialization stage or frequency across boots.

| Object address | EEType | Component type | Rank / length | Object bytes | Owner |
|---:|---|---|---:|---:|---:|
| `0x07A67000` | SZARRAY `0x100F5258` | `System.Object`, 8 bytes | 1 / 4 | 56 | 0 |
| `0x07A6A000` | SZARRAY `0x100F4AC0` | `System.Int32`, 4 bytes | 1 / 4 | 40 | 0 |

The earlier R3 owner-0 addresses were `0x07A52000` and `0x07A55000`; the first clean fresh run used `0x07A67000` and `0x07A6A000`, and the expanded-trace confirmation observed `0x07A6C000` and `0x07A6F000`. The differing addresses show they are not stable physical identities across boots. The samples do not show who creates them or how often they recur.

## GC, roots, teardown, and reuse

- The kernel override is not a tracing-GC allocation path. These 28 objects have no kernel GC generations, roots, handles, finalizer state, collection count, logical death, or collector reclamation decision. GC reachability is not applicable to these kernel-heap objects; they remain allocated even after managed references can no longer reach them.
- The requester's workstation GC does not manage these kernel malloc blocks. A requester `GC.Collect` would not establish their fate.
- `ManagedImageProcess.Cleanup` releases mapped image pages, TLS pages, VM reservations, descriptors, and address-space state. The allocator is global and has no requester-generation sweep. Diagnostic owner/generation fields do not encode a safe object-to-process ownership graph, so process exit does not prove these global malloc pages are private or safe to release.
- In the fresh run, the original proof's 26 request strings and two owner-0 arrays accounted for the 28 net pages. The no/one/two-read controls then added 0/4/8 pages. This reproduces the proportional count observation in the same boot but does not test three reads, repeated reads in one process, reuse/plateau, forced GC, or 25 lifetime string identities.
- The 65,536-byte scratch-buffer identity and public exact-result array identity were not traced to allocator IDs. The retained objects above are small strings (maximum 528 aligned bytes); this run does not establish whether unrelated scratch storage exists or how it is owned.
- Static arrays exist in the proof/runtime, including `Ring3Process.Slots`, `Generations`, proof payload helpers, and CoreLib caches. No evidence links those static arrays to the 26 string objects or establishes an unbounded static accumulation.

## Managed creation sites

`System.String.Ctor(char* ptr, int index, int length)` directly calls `StartupCodeHelpers.RhpNewArray` for the string EEType. Other `System.String` methods allocate through `FastAllocateString`, which calls `Allocator.Allocate` directly. The allocation-boundary data establishes the object type and request window, but it does not report the managed stack/caller method for each `RhpNewArray` invocation. Request decoding constructs `characters` and a `relativePath`; the source provides plausible paths, but the captured strings' contents and caller frames were not recorded. Therefore no precise statement such as “this row is the path” or “this row is the error text” is supported.

| Allocation boundary | Concrete EEType | Managed caller into boundary | Retained |
|---|---|---|---|
| kernel `RhpNewArray` → `malloc` → `Allocator.Allocate` | `System.String` for the 26 request-attributed objects | unresolved; `0x100119E9` is the internal malloc return | all 26 |
| kernel `RhpNewArray` → `malloc` → `Allocator.Allocate` | `System.Object[]` and `System.Int32[]` for owner-0 objects | unresolved; same internal helper return | 2 |

## Reclamation decision

The missing layer visible in source is an ownership/lifetime mechanism for objects allocated by the kernel's GC-unaware `malloc`. The current global allocator does not retain an object-to-process ownership graph. A process-generation sweep could free global/kernel allocations and is not justified by these observations.

No production teardown change was made. To choose a safe repair, capture the managed caller return address at the actual `RhpNewArray` boundary (the captured allocator return is only the internal malloc return; a bounded caller frame or direct callsite token is still needed) and trace object content only as needed to connect identities to operations. Then audit who owns strings made by path decoding and storage results. Decide whether they belong on a real GC heap, an explicit process/private heap, or a caller-owned allocation with explicit disposal. Reclamation can change after that ownership model is established.

## Phase 35R4 gate status

| Gate group | Result |
|---|---|
| 0 preflight and evidence preservation | complete; protected R3 log hash unchanged |
| 1 active kernel `RhpNewArray` source audit | complete |
| 2–9 type metadata, object identity, and owner-0 snapshot | concrete type, size, length and current identity captured; semantic caller and cross-boot frequency unresolved |
| 10–17 GC/root/stack/static audit | kernel GC inapplicable to these malloc objects; requester GC/root telemetry not needed for kernel blocks; managed creator frames remain absent |
| 18–23 proof arrays, scratch/result identities, and page liveness | 28 live objects matched to allocator runs; scratch/result identity questions unresolved |
| 24–25 heap scope and safe owner reclamation | global kernel allocator; owner sweep unsafe/unproven |
| 26–30 fresh boots and repeated-read/reuse experiments | one clean fresh boot with original/no/one/two-read matrix, plus a confirming boot with the same matrix; no isolated repeated-read/reuse or 25-lifetime identity run |
| 31–38 teardown, pressure, plateau, repair, and post-fix proof | not reached; no production repair justified |
| 39–41 stress, DSDT validation, Phase 35 completion | gated |

Phase 36 remains gated. R4 resolves the key GC premise and identifies all 28 current live objects, but does not establish their precise managed creators or a safe reclamation owner. **R4 is not complete; no teardown/reclamation change is justified.**