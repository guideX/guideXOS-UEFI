# Phase 35R9 Complete Post-Cleanup Retained-Run Inventory

Date: 2026-10-06

## Outcome

**Outcome F — the first successful requester’s measured allocator delta is fully reconciled to eight live one-page runs (8 measured, 8 accounted, 0 unexplained, 0 over-accounted). The fresh capture resolves the six string allocation sites to the `String.Ctor(char*, index, length)` fallback tag (`0x191`, decimal 401), but it does not establish the semantic owner or intended post-request lifetime for those strings. No reclamation change is justified. Phase 35 remains incomplete and Phase 36 remains gated.**

R9 narrows the accounting uncertainty recorded in R8: the fresh stress-equivalent success has two arrays and six strings, totaling exactly the eight-page B2-minus-B0 increase. This is a different observed shape from historical R7’s 14-page delta. The repeated identical success adds six more string runs/six pages; no-read and one-read controls add no surviving runs; NotFound adds six strings/six pages. Each capture is evaluated against its own immediately preceding B0.

## Preflight and preservation

- Repository: `D:\dev\guideXOSUEFI`; branch `main`; starting HEAD `1d7d0a0b69145cdb2d786b367905e85f69e6429d`.
- The root worktree already contained accumulated dirty source, artifact, and documentation work. The nested `out\rt` checkout also had existing tracked modifications and untracked files. These were preserved; no reset, clean, stash, rebase, branch change, commit, or push was performed.
- Existing R7 and R8 reports were not modified. This report is a new file.
- One full `Ring3Phase35R9` deterministic artifact-cohort build and guest capture completed. Kernel SHA-256: `F52B538BDA9098CA8864A8D7E539C702C823F611702E9AB3B55943FE686B49A3`. Ramdisk SHA-256: `59384D12B83D89D243C7BDF46B1F75F2102A624FC628CACF492BF4A84424B08E`.
- Authoritative serial capture: `out\phase35q\serial-5464552ef4b14b42b29dd36b0f91f6f3.log`. Guest emitted `PHASE35_R9_RUN_LEDGER_COMPLETE=1`; all workload pass markers were present.
- The host runner initially failed after guest completion because it expected `PHASE35_R9_LIFETIME_ID`, while the actual diagnostic field is `R9_LIFETIME_ID`; its run-row matcher also expected decimal phase/lifetime text while the ledger emits fixed-width hexadecimal. Both parser mismatches were corrected and checked against the retained serial log: lifetime 1, measured 8 pages, 8 rows, 8 accounted pages. PowerShell parser validation passed. The initial runner failure did not represent a guest failure; the completed capture was retained and analyzed directly.

## Capture procedure and workload results

The R9 diagnostic mode emitted allocator readiness, then ran one success request with the historical stress-equivalent service shape and expected six ABI requests. It collected B0 before process creation, B1 before requester cleanup, and B2 after `Cleanup`/`process.Dispose()`. It then ran the same success once more, followed by no-read, one-read, and NotFound controls. The guest reported six service requests for each success, zero for no-read, one for one-read, and one for NotFound. The first success returned status 35 and all workload markers passed.

| Lifetime | Workload | B0 live pages | B1 live pages | B2 live pages | B2 − B0 | New live runs/pages |
|---:|---|---:|---:|---:|---:|---:|
| 1 | First stress-equivalent success, 6 requests | `0x3AB9` (15033) | `0x45F5` (17909) | `0x3AC1` (15041) | 8 | 8 / 8 |
| 2 | Identical success repeat, 6 requests | `0x3AC1` (15041) | `0x45FB` (17915) | `0x3AC7` (15047) | 6 | 6 / 6 |
| 3 | No-read control, 0 service requests | `0x3AC7` | `0x45C0` | `0x3AC7` | 0 | 0 / 0 |
| 4 | One-read control, 1 service request | `0x3AC7` | `0x45EB` | `0x3AC7` | 0 | 0 / 0 |
| 5 | NotFound control, 1 service request | `0x3AC7` | `0x45C6` | `0x3ACD` (15053) | 6 | 6 / 6 |

B1 is the pre-cleanup peak snapshot; the post-cleanup accounting uses B2 minus that lifetime’s B0. R9’s helper counters are consistent with these totals: first success had 8 `RhpNewArray` survivors/8 pages and no `RhpNewFast` survivors; repeat had 6/6; no-read and one-read had 0/0; NotFound had 6/6. Those counters are runtime-helper totals, not a semantic owner claim.

## First success: complete B2 run ledger

Every row below is one live allocator run of one page after process cleanup. The two arrays have no requester owner or generation in the diagnostic ownership fields; the six strings carry requester `0x3`, generation `0x100000001`, and diagnostic lifetime 1. All eight are native-runtime allocations made through `RhpNewArray` (helper 1). All rows have the recorded allocator class `0x6` (`NativeRuntimeMalloc`). The common return address is `0x10012535`.

| Run ID | Address | Pages | Requested/object bytes | Allocation class | Type | Site ID | Caller | Lifetime / owner | Content hash; Dispose / free |
|---|---:|---:|---:|---|---|---:|---|---|---|
| `0x2B657` | `0x7AB7000` | 1 | `0x38` | `0x6 NativeRuntimeMalloc` | `Ring3Process[]`, length 4, EEType `0x100F5490` | 0 (static initialization; unset) | `0x10012535` (`RhpNewArray+0x71`) | lifetime 1; global `Ring3ProcessTable.Slots`; requester/generation 0/0 | — |
| `0x2B658` | `0x7ABA000` | 1 | `0x28` | `0x6 NativeRuntimeMalloc` | `uint[]`, length 4, EEType `0x100F4CF8` | 0 (static initialization; unset) | `0x10012535` (`RhpNewArray+0x71`) | lifetime 1; global `Ring3ProcessTable.Generations`; requester/generation 0/0 | — |
| `0x2C5E2` | `0x85E4000` | 1 | `0x80` | `0x6 NativeRuntimeMalloc` | `System.String`, length 51, EEType `0x100ECC98` | `0x191` (401, `String.Ctor` fallback) | `0x10012535` (`RhpNewArray+0x71`) | lifetime 1; requester `0x3`, generation `0x100000001` | `0x94A661B97507E172`; 0 / 0 |
| `0x2C5E5` | `0x85E5000` | 1 | `0x80` | `0x6 NativeRuntimeMalloc` | `System.String`, length 50, EEType `0x100ECC98` | `0x191` (401, `String.Ctor` fallback) | `0x10012535` (`RhpNewArray+0x71`) | lifetime 1; requester `0x3`, generation `0x100000001` | `0xADE827DD03D3AE1F`; 0 / 0 |
| `0x2C5E3` | `0x85E6000` | 1 | `0x68` | `0x6 NativeRuntimeMalloc` | `System.String`, length 38, EEType `0x100ECC98` | `0x191` (401, `String.Ctor` fallback) | `0x10012535` (`RhpNewArray+0x71`) | lifetime 1; requester `0x3`, generation `0x100000001` | `0x7B75BBA2453D7996`; 0 / 0 |
| `0x2C5E4` | `0x85E7000` | 1 | `0x210` | `0x6 NativeRuntimeMalloc` | `System.String`, length 251, EEType `0x100ECC98` | `0x191` (401, `String.Ctor` fallback) | `0x10012535` (`RhpNewArray+0x71`) | lifetime 1; requester `0x3`, generation `0x100000001` | `0x2DB56378C1962B98`; 0 / 0 |
| `0x2C5E7` | `0x85E9000` | 1 | `0x18` | `0x6 NativeRuntimeMalloc` | `System.String`, length 1, EEType `0x100ECC98` | `0x191` (401, `String.Ctor` fallback) | `0x10012535` (`RhpNewArray+0x71`) | lifetime 1; requester `0x3`, generation `0x100000001` | `0xAF63AE4C86019E62`; 0 / 0 |
| `0x2C5E8` | `0x85EA000` | 1 | `0x60` | `0x6 NativeRuntimeMalloc` | `System.String`, length 36, EEType `0x100ECC98` | `0x191` (401, `String.Ctor` fallback) | `0x10012535` (`RhpNewArray+0x71`) | lifetime 1; requester `0x3`, generation `0x100000001` | `0xD82EC95D355533BA`; 0 / 0 |

The final `STRING_LIVE` snapshot for requester `0x3` repeats all six string rows with `dispose=0`, `freeAttempts=0`, and `freeResult=0`. Their site is no longer zero: `0x191` is the new diagnostic fallback at `Corlib/System/String.cs`, in `String.Ctor(char*, int, int)`, used when no outer allocation-site context was set. This establishes the constructor route. It does **not** identify which higher-level parser/ABI/backend consumer caused each constructor call, why the string remains rooted, or which component is entitled to dispose it.

The exact-build `guideXOS\Kernel.map` resolves EEType `0x100F5490` to `Ring3Process[]` and EEType `0x100F4CF8` to `uint[]`. These are `Ring3ProcessTable.Slots` and `Ring3ProcessTable.Generations`, both fixed-size static tables of capacity four in `Kernel\Misc\Process.cs`. Their first-use initialization explains why these two pages occur only in the first B2 delta; the repeated requester and controls do not add them again. Their concrete types and global lifetime are identified, but their allocator creation site field remains zero.

## Repeat and controls

- **Identical repeat:** B2 delta 6 pages; six new one-page strings; requester `0x9`, lifetime generation `0x200000001`; same six lengths, byte sizes, content hashes, and site `0x191`; final disposal/free counters remain zero. No new arrays survived this repeat.
- **No-read:** zero net live pages and zero new B2 runs; zero service requests.
- **One-read:** zero net live pages and zero new B2 runs; one service request. This control’s request does not produce a net surviving allocator run at B2.
- **NotFound:** six new one-page strings/six pages; requester `0xE`, generation `0x500000001`; same constructor fallback site; all six are still live with no dispose/free attempt at B2.

The different B1 peaks reflect allocations made during each request and its cleanup. The accounting target is the new live run set at B2 relative to that lifetime’s B0, not the temporary B1 peak.

Reconciliation of the saved phase-1 (B1) and phase-2 (B2) run rows for lifetime 1 gives 683 runs / 2,876 pages live at B1 and 8 runs / 8 pages live at B2. Exactly 675 B1 runs / 2,868 pages are absent at B2, so they were reclaimed during `Cleanup()` / `Dispose()`. By helper, B1 had 664 `RhpNewArray` runs / 2,857 pages and 19 `RhpNewFast` runs / 19 pages; B2 retains 8 `RhpNewArray` runs and zero `RhpNewFast` runs. By EEType class, B1 had 326 strings / 2,373 pages, 338 arrays / 484 pages, and 19 other objects / 19 pages; B2 retains six strings / six pages and two arrays / two pages. Thus request/process-private temporary objects are reclaimed, while only the six strings and two identified global tables cross B2.

Across allocations after B0, helper counters report `RhpNewArray`: 4,919 allocations, 4,911 frees, 8 survivors / 8 pages; `RhpNewFast`: 89 allocations, 89 frees, 0 survivors / 0 pages; helper 0: 0 allocations, 0 frees, 0 survivors / 0 pages. All B2 survivors therefore pass through `RhpNewArray`; no `RhpNewFast` survivor or additional helper survivor remains.

## Accounting and phase gate

For the first successful lifetime:

- Measured allocator delta: 8 pages.
- Enumerated surviving runs: 8, all one page.
- Accounted pages: 8.
- Unexplained pages: 0.
- Over-accounted pages: 0.

Thus the allocator-page delta is reconciled. Source-level lifetime ownership is not. The constructor fallback proves where each string was allocated only when no more specific site context was active; the rows do not prove the intended owner or whether any live reference is legitimate after requester cleanup. The array site remains 0, although both concrete array layouts and both page contributions are known. The inventory must not be interpreted as evidence that reclaiming any object is safe.

**No production reclamation/lifetime fix was made. Phase 35 remains incomplete; Phase 36 remains gated.** The next authorized investigation is to map the six string instances to the exact higher-level creation/retention call paths and identify their live roots/ownership contract, then classify the two arrays’ managed element types and creation sites. Only after that semantic accounting and safety evidence should a production lifetime change be proposed.

## Artifacts

- Serial evidence: `out\phase35q\serial-5464552ef4b14b42b29dd36b0f91f6f3.log`
- Diagnostic kernel SHA-256: `F52B538BDA9098CA8864A8D7E539C702C823F611702E9AB3B55943FE686B49A3`
- Diagnostic ramdisk SHA-256: `59384D12B83D89D243C7BDF46B1F75F2102A624FC628CACF492BF4A84424B08E`
- Fixture SHA-256: `BEFA57E7EF0799D031A0188A3D0883F0F342B8F8AE90B3330652DA04ADBA739D`; fixture was seeded by this run (`PHASE35_FIXTURE_PREEXISTING_OR_SEEDED=Seeded`, seed writes 1).

## Continuation audit amendment (2026-10-07)

The live repository preflight for this continuation differs from the preflight recorded above, so the live state is authoritative for this continuation: repository `D:\dev\guideXOSUEFI`, branch `main`, HEAD `4b82358a2fd3714d2307d6550238436e70b6dcce` (subject `...`), upstream `origin/main`, ahead/behind `0/0`. The root worktree had a pre-existing deletion of tracked `APP_MODEL_CONVERGENCE.md` and untracked `Docs/APP_MODEL_CONVERGENCE.md`; both remain untouched. Nested `out\rt` was detached at `9d5a6a9aa463d6d10b0b0ba6d5982cc82f363dc3` with pre-existing tracked and untracked changes; all remain untouched. Ending HEAD is unchanged. No build, guest run, or kernel/managed source edit was performed in this continuation; the fresh R9 build and guest capture cited above remain the evidence used here. R7 and R8 were not edited.

The two first-lifetime non-string runs are fully identifiable from the exact-build `guideXOS\Kernel.map` and live declarations:

| Run ID | EEType / concrete type | Source declaration | Lifetime classification |
|---|---|---|---|
| `0x2B657` | `0x100F5490`, `Ring3Process[]`, length 4 | `Ring3ProcessTable.Slots` (`Kernel/Misc/Process.cs`) | Intentionally global static table, allocated on first process-table use; absent from the identical second-lifetime delta and controls |
| `0x2B658` | `0x100F4CF8`, `uint[]`, length 4 | `Ring3ProcessTable.Generations` (`Kernel/Misc/Process.cs`) | Intentionally global static table, allocated on first process-table use; absent from the identical second-lifetime delta and controls |

The mapped string caller `0x10012535` resolves to `StartupCodeHelpers.RhpNewArray+0x71`, the shared allocation hook at `Corlib/Internal/Runtime/CompilerHelpers/StartupCodeHelpers.cs` (the return address is inside that helper, not an upstream managed call site). Each retained string has constructor fallback site `0x191` / decimal `401`, assigned in `Corlib/System/String.cs` when no more specific diagnostic site is active. Thus the observed allocation route is `String.Ctor(char*, int, int) -> RhpNewArray -> NativeRuntimeMalloc`; the exact higher-level producer, semantic purpose, and logical owner remain unresolved. The six first-success string rows each have `Dispose=0`, allocator-free attempts `0`, and free result `0` at B2. No alias-safe reclamation point can be inferred from these observations.

The first capture therefore has **8 measured pages, 8 accounted pages, 0 unexplained pages**, but it is not a complete ownership/provenance inventory for the six repeating strings: their caller return address is only the allocation helper and site `401` is a constructor fallback, not a producer-site attribution. No reclamation fix is made. Outcome F remains the correct status; Phase 35 remains incomplete and Phase 36 remains gated.

The source-level workload sequence for the first successful `success` payload is: six invalid-path public SDK calls in `CheckInvalidPaths` (empty, absolute, parent traversal, invalid character, overlength path, overlength segment), all rejected locally; then `CheckMissing` triggers the two one-time raw bounds-proof `PersistentRead` ABI entries (32-byte exact buffer and 31-byte short buffer) followed by one `missing.phase35` NotFound request; then one `state.bin` read, mutation of the first returned byte, a second `state.bin` read and independence/content check, and one `empty.bin` read. The guest counters record exactly 6 PersistentRead ABI entries: 5 successful reads and 1 NotFound/error path. The bounds proof compared 32 + 31 fixture bytes; the public proof made 32 content-byte comparisons for the first `state.bin` result and 64 across the repeated `state.bin` result and its two `HasFixtureBytes` checks. `System.GC.Collect` executes once after clearing both returned arrays and once after the empty-value check. The guest's run-level result remained 35. The retained strings are kernel-side `RhpNewArray` allocations during the PersistentRead syscall (`callerLabel=2`); the report cannot separate production storage code from diagnostics at a more specific managed producer site.
