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

| Run ID | Managed object address | Pages | Requested/object bytes | Type and layout | Creation site | Requester / generation | String hash / final disposal |
|---|---:|---:|---:|---|---|---|---|
| `0x2B657` | `0x7AB7000` | 1 | `0x38` | rank-1 SZ array; length 4; component size 8; component type code `0x14`; EEType `0x100F5490`; metadata class `0x5` | 0 (array path not attributed) | 0 / 0 | — |
| `0x2B658` | `0x7ABA000` | 1 | `0x28` | rank-1 SZ array; length 4; component size 4; component type code `0x09`; EEType `0x100F4CF8`; metadata class `0x4` | 0 (array path not attributed) | 0 / 0 | — |
| `0x2C5E2` | `0x85E4000` | 1 | `0x80` | String; length `0x33` (51); EEType `0x100ECC98`; component type code `0x14`, size 2 | `0x191` (401, `String.Ctor` fallback) | `0x3` / `0x100000001` | `0x94A661B97507E172`; dispose 0, frees 0 |
| `0x2C5E5` | `0x85E5000` | 1 | `0x80` | String; length `0x32` (50); EEType `0x100ECC98`; component type code `0x14`, size 2 | `0x191` (401, `String.Ctor` fallback) | `0x3` / `0x100000001` | `0xADE827DD03D3AE1F`; dispose 0, frees 0 |
| `0x2C5E3` | `0x85E6000` | 1 | `0x68` | String; length `0x26` (38); EEType `0x100ECC98`; component type code `0x14`, size 2 | `0x191` (401, `String.Ctor` fallback) | `0x3` / `0x100000001` | `0x7B75BBA2453D7996`; dispose 0, frees 0 |
| `0x2C5E4` | `0x85E7000` | 1 | `0x210` | String; length `0xFB` (251); EEType `0x100ECC98`; component type code `0x14`, size 2 | `0x191` (401, `String.Ctor` fallback) | `0x3` / `0x100000001` | `0x2DB56378C1962B98`; dispose 0, frees 0 |
| `0x2C5E7` | `0x85E9000` | 1 | `0x18` | String; length 1; EEType `0x100ECC98`; component type code `0x14`, size 2 | `0x191` (401, `String.Ctor` fallback) | `0x3` / `0x100000001` | `0xAF63AE4C86019E62`; dispose 0, frees 0 |
| `0x2C5E8` | `0x85EA000` | 1 | `0x60` | String; length `0x24` (36); EEType `0x100ECC98`; component type code `0x14`, size 2 | `0x191` (401, `String.Ctor` fallback) | `0x3` / `0x100000001` | `0xD82EC95D355533BA`; dispose 0, frees 0 |

The final `STRING_LIVE` snapshot for requester `0x3` repeats all six string rows with `dispose=0`, `freeAttempts=0`, and `freeResult=0`. Their site is no longer zero: `0x191` is the new diagnostic fallback at `Corlib/System/String.cs`, in `String.Ctor(char*, int, int)`, used when no outer allocation-site context was set. This establishes the constructor route. It does **not** identify which higher-level parser/ABI/backend consumer caused each constructor call, why the string remains rooted, or which component is entitled to dispose it.

The two arrays are classified by EEType inspection as rank-one arrays of four 8-byte components and four 4-byte components respectively. Their low-level element type codes and component sizes are recorded above; semantic managed type names were not symbolized in this capture. Both are included in the same eight-page accounting, so they are not unexplained pages.

## Repeat and controls

- **Identical repeat:** B2 delta 6 pages; six new one-page strings; requester `0x9`, lifetime generation `0x200000001`; same six lengths, byte sizes, content hashes, and site `0x191`; final disposal/free counters remain zero. No new arrays survived this repeat.
- **No-read:** zero net live pages and zero new B2 runs; zero service requests.
- **One-read:** zero net live pages and zero new B2 runs; one service request. This control’s request does not produce a net surviving allocator run at B2.
- **NotFound:** six new one-page strings/six pages; requester `0xE`, generation `0x500000001`; same constructor fallback site; all six are still live with no dispose/free attempt at B2.

The different B1 peaks reflect allocations made during each request and its cleanup. The accounting target is the new live run set at B2 relative to that lifetime’s B0, not the temporary B1 peak.

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
