# Phase 35R8 Allocator Accounting Closeout

Date: 2026-10-06

## Outcome

**Outcome F — the 14-page/lifetime allocator retention is not fully accounted. No reclamation fix is justified. Phase 35 remains incomplete and Phase 36 remains gated.**

R8 did not run a new guest. Inspection of the authoritative R7 serial artifact established that the current guest's allocator forensic output contains totals only; the per-run address/type dump was disabled. Therefore the six retained string runs can be correlated to six pages, but the other eight pages of the one-read R7 delta cannot be enumerated or classified from this evidence. `CreationSite=0` also remains unresolved for those six strings. No source repair or Phase 35 cohort rebuild was made.

## Preflight and preservation

- Repository: `D:\dev\guideXOSUEFI`
- Branch/upstream: `main` / `origin/main`; ahead/behind `0/0`
- Starting and ending HEAD: `1d7d0a0b69145cdb2d786b367905e85f69e6429d` (no commit)
- Root worktree: dirty with the accumulated R1–R7 source/artifact changes; preserved without reset, clean, stash, rebase, branch/worktree change, detach, push, or commit.
- Nested `out\rt`: detached at `9d5a6a9aa463d6d10b0b0ba6d5982cc82f363dc3`; tracked modifications and untracked files were present and left untouched.
- Existing R1–R7 documents, build outputs, and serial evidence remain in place.

## Evidence used

Authoritative artifact: `out\phase35q\serial-c9b7a40c6c44400685726b9dcc69ddee.log`, from R7's coherent 41-artifact cohort. Its reported kernel, ramdisk, and EFI hashes and R7 semantic results are retained in `PHASE35R7_FINAL_ACCEPTANCE.md`.

R7's final normal success lifetime is request `0xC1`; it returned `PHASE35_MAIN_RETURN=35`, serviced six ABI requests, and process cleanup completed. Its post-process string ledger has six live rows. Each string object's address equals its allocator run start, each individual run occupies one page, and every row has `Dispose=0`, `freeAttempts=0`, and `CreationSite=0`:

| Address / run start | Pages | Object bytes | Length | Site | Dispose / free | Content hash |
|---|---:|---:|---:|---:|---|---|
| `0x7F4F000` | 1 | `0x80` | `0x33` | 0 | 0 / 0 | `0x94A661B97507E172` |
| `0x7F51000` | 1 | `0x68` | `0x26` | 0 | 0 / 0 | `0x7B75BBA2453D7996` |
| `0x7F52000` | 1 | `0x210` | `0xFB` | 0 | 0 / 0 | `0x2DB56378C1962B98` |
| `0x7F50000` | 1 | `0x80` | `0x32` | 0 | 0 / 0 | `0xADE827DD03D3AE1F` |
| `0x7F54000` | 1 | `0x18` | `0x1` | 0 | 0 / 0 | `0xAF63AE4C86019E62` |
| `0x7F55000` | 1 | `0x60` | `0x24` | 0 | 0 / 0 | `0xD82EC95D355533BA` |

The final lifetime reports six request operations and six retained pages. Earlier R7 success requests similarly report six retained pages, while the first isolated one-read matrix requester (request `0x3`) reports eight retained pages. The 25-lifetime R7 summary reports `0x15E000 / 25 = 0xE000`, i.e. 14 pages per successful stress lifetime. These runs establish a deterministic stress slope, but the 8-page initial request and 6-page later request show that baselines/one-time activity differ; neither can be used as the demanded single-lifetime 14-page run inventory.

## Required accounting status

| Retained class | Runs | Pages | Status |
|---|---:|---:|---|
| Strings | 6 | 6 | Exact six rows above; `CreationSite=0`, semantic category unknown |
| Arrays | Unknown | Unknown | No current per-run dump |
| Runtime metadata | Unknown | Unknown | No current per-run dump |
| Request/service | Unknown | Unknown | No current per-run dump |
| Other classified | Unknown | Unknown | No current per-run dump |
| Unknown residual | Unknown | 8 in the initial one-read diagnostic; 0 residual relative to the later six-page stress slope | Not attributable to a specific lifetime/run |
| **Total target** | **Unknown** | **14 per stress lifetime** | Aggregate only; not reconciled to runs |

The six string contents were not serialized. The one-character hash `0xAF63AE4C86019E62` is consistent with the known ASCII/UTF-16 character `1`, but this is a hash-based identity only and is not promoted to a semantic proof without an exact fixture comparison. The other hashes do not identify values on their own. R7's string rows have no Dispose/free attempts; this supports “never disposed in the observed lifetime” but does not determine intended owner or safe reclamation point. Caller return addresses resolve to the shared `RhpNewArray` hook, not the managed producer.

No baseline live-page/run table, S1/S2 full allocator snapshots, per-run owner/tag/type/caller inventory, per-operation allocation association, or isolated R8 one-success boot is present in the R7 log. Consequently non-string retained runs, no-read/two-read/NotFound full allocator deltas, the production-SDK/proof split, exact stress operation sequence ownership, and the missing reclamation path remain unknown. No production cleanup or proof lifetime change is made.

## Diagnostic limitation and next narrow action

`Allocator.DumpDiagnosticRunsSince` currently computes aggregate run/page totals and deliberately suppresses the row dump in Phase 35 mode. The R7 capture therefore cannot satisfy R8's requirement to enumerate each run. Its `CreationSite` is sourced from `CurrentDiagnosticStringSite`, which is set only around selected FAT/backend/ABI helpers; unwrapped string producers retain site zero. The creation caller field records the allocator hook return address and does not unwind to managed call sites.

Next action: add an explicit one-success diagnostic boot mode that executes one normal success payload only, captures complete live-run baselines and post-request/post-process rows using bounded numeric logging, and records string producer sites at actual constructor/helper boundaries. Correlate all six strings with hashes and the exact known fixture/path constants, then enumerate every remaining run with run ID/address/pages/tag/owner/generation/allocation primitive/caller/site/type. Only after the measured retained pages reconcile exactly should ownership and cleanup paths be changed. Preserve the existing nine Phase 35 payload variants and use the established full 41-artifact pipeline if diagnostic source changes alter identities.

## R7 closeout amendment

R7's table remains valid evidence for the six strings and its 25-lifetime slope. This R8 inspection adds that the R7 one-read diagnostic totals are aggregate-only and provide no complete retained-run inventory; the later six-page per-success totals do not explain how the slope relates to the first one-read eight-page total. Thus the R8 accounting gate remains open and no R7 result is upgraded to final acceptance.

