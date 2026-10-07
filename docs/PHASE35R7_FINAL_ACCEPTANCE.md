# Phase 35R7 Final Acceptance

Date: 2026-10-06

## Decision

**Outcome F — qualification incomplete. Phase 35 is not accepted. Phase 36 remains gated.**

The R7 artifact-cohort and staging blocker is resolved for the current build. The latest guest ran the rebuilt cohort, the isolated NotFound requester launched and returned typed `NotFound` (result 3), 25 managed lifetimes returned 35, the App Model reset proof passed, and storage/handle/free-failure counters returned to zero. Exact fixture reads returned 32 and 31 bytes; the existing empty value returned zero bytes with end-of-value true. Cross-scope denial, stale-context rejection before backend lookup, raw malformed rejection, FailFast/replacement, and the four-primary-return marker passed. The same guest also reports `PHASE35_STRESS_ALLOCATOR_STABLE=0`; a successful read leaves six live allocator-backed strings after process cleanup. Memory rises by `0xE000` per normal stress lifetime, `0x15E000` over 25. These direct unresolved gates make the harness emit `PHASE35_COMPLETE=0` and stop before same-image reboot and the remaining acceptance matrix.

## R1–R6 evidence and resolution

Earlier allocator, GC reachability, string lifetime, and R6 address-ledger evidence are preserved in:

- `Docs/PHASE35R1_ALLOCATOR_LIFETIME_CLOSEOUT.md`
- `Docs/PHASE35R4_GC_REACHABILITY_CLOSEOUT.md`
- `Docs/PHASE35R5_STRING_LIFETIME_CLOSEOUT.md`
- `Docs/PHASE35R6_STRING_ADDRESS_LEDGER_CLOSEOUT.md`

The apparent four-string-per-read retention was caused by diagnostic marker concatenation and disappeared after replacing those markers with bounded numeric UART formatting. R6 matrix runs therefore showed zero live request strings after successful reads. That finding remains valid for those runs and is not rewritten here.

R7 also removed the per-allocation run dump that was still producing page-by-page serial output. The latest full run has bounded numeric allocator summaries and a bounded string ledger. After discovering that old freed rows filled the ledger over long runs, post-process snapshots now recycle only records proven non-live after emitting that snapshot; live records remain in the ledger.

## Preflight and preservation

- Repository: `D:\dev\guideXOSUEFI`
- Branch: `main`; upstream `origin/main`; starting ahead/behind `0/0`
- Starting HEAD: `1d7d0a0b69145cdb2d786b367905e85f69e6429d`
- Ending HEAD: same (no commit)
- Root was already dirty with accumulated Phase 35R work. No reset, clean, stash, rebase, branch/worktree creation, branch switch, detach, push, or commit was performed.
- Nested `out\rt` HEAD: `9d5a6a9aa463d6d10b0b0ba6d5982cc82f363dc3`; detached and already extensively modified/untracked before this work. Its changes were preserved.

## Artifact cohort and staging

The live artifact registry and Phase 35 build scripts define nine Phase 35 variants: `success`, `failfast`, `stale-owner`, `cross-scope`, `malformed`, `no-read`, `one-read`, `two-read`, `not-found`. No obsolete R1–R6 forensic payload is required. The full managed registry has 41 rows: 32 baseline artifacts plus these 9 Phase 35 additions. The pipeline rebuilt managed payloads, canonicalized them, wrote GXMI descriptors/staging metadata, regenerated kernel admission identities, compiled the diagnostic kernel, built the ramdisk, and staged EFI in that order.

The fresh audit `out\h2c-managed-artifact-audit.csv` has 41 rows. Phase 35 has 9/9 rows passing build/staged equality, executable/descriptor digest equality, generated/compiled admission identity equality, and ramdisk-copy equality. No acceptance VM was launched until this audit passed. Phase 32 success, failfast, and stale-owner artifacts were regenerated and validated in the same pipeline. The prior `PHASE32_IMAGE_NOT_STAGED` message came from the shared managed loader labelling Phase 35 dispatch kinds 14–22 as Phase 32; the diagnostic classification was fixed in `Kernel\Misc\ManagedImage.cs`. Phase 32 remains staged as a real control dependency.

## Finalized guest identity

- Kernel (`kernel.elf`, also `ESP\kernel.elf`): SHA-256 `E8FB04D48A1DA70EAC52BE672A68512322678BD7E4C86EC0D51777D90C4E3996`
- Ramdisk (`ramdisk.img`, also `ESP\ramdisk.img`): SHA-256 `59384D12B83D89D243C7BDF46B1F75F2102A624FC628CACF492BF4A84424B08E`
- EFI bootloader: SHA-256 `655DA5BE258E0FA04CD1DBBF2DFBE8668FFAAE1480010E488979C6C915B599D0`
- Project and ESP kernel hashes agree; project and ESP ramdisk hashes agree.
- Phase 35 flags are defined by `Tools\Phase35\phase35_flags.py`; the nine descriptor and admission flags are audited with the digests in the CSV. Guest ledger marker: `STRING_LEDGER_READY=1`, version `R6-1`.
- Disposable AHCI image/run id: `c9b7a40c6c44400685726b9dcc69ddee`; serial log: `out\phase35q\serial-c9b7a40c6c44400685726b9dcc69ddee.log` (about 42.6 MB; no dynamic diagnostic marker strings were reintroduced).

## Guest results

| Gate | Result |
|---|---|
| NotFound requester launch | PASS; it ran in R7 matrix and returned typed NotFound |
| No-read / one-read / two-read | PASS in R7 matrix; the corrected ledger did not saturate |
| Four primary normal returns | PASS marker, `PHASE35_FOUR_PRIMARY_RETURNS=4` |
| 25 lifetime stress | PASS, 25 requester returns were 35 |
| FailFast/replacement | PASS marker `PHASE35_FAILFAST_REPLACEMENT_RETURN_35=1` |
| Stale owner / malformed raw request | PASS markers `PHASE35_STALE_OWNER_REJECTED=1`, `PHASE35_MALFORMED_FAIL_CLOSED=1` |
| App Model reset | PASS: Temporary cleared, Persistent retained, managed reset proof passed |
| Active storage requests / Persistent handles | 0 / 0 |
| `freeInvalid` / `freeCorrupt` / `freeNoPages` | 0 / 0 / 0 |
| Allocator stability | FAIL: `PHASE35_STRESS_ALLOCATOR_STABLE=0`, memory delta `0x15E000` over stress |
| Request string lifetime | FAIL/unresolved: request `0xC1` has six live strings at stage 3 after process cleanup |
| Full completion | FAIL as expected from above; `PHASE35_COMPLETE=0` |
| Same-image reboot and `seedWrites=0` | Not reached because the full acceptance marker was false |

The primary one-read matrix case (request `0x3`) has the same six survivor sizes, lengths, and content hashes at addresses `0x8600000`, `0x8601000`, `0x85FF000`, `0x8603000`, `0x8604000`, and `0x85FE000`; the last normal stress success is request `0xC1` and its rows are recorded around lines 310,435–310,442. All match allocator run starts, all have zero Dispose/free attempts, and all have `CreationSite=0`:

| Address | Bytes | String length | Allocation sequence | Content hash |
|---|---:|---:|---:|---|
| `0x7F4F000` | `0x80` | `0x33` | `0x54155` | `0x94A661B97507E172` |
| `0x7F51000` | `0x68` | `0x26` | `0x54156` | `0x7B75BBA2453D7996` |
| `0x7F52000` | `0x210` | `0xFB` | `0x54157` | `0x2DB56378C1962B98` |
| `0x7F50000` | `0x80` | `0x32` | `0x54158` | `0xADE827DD03D3AE1F` |
| `0x7F54000` | `0x18` | `0x1` | `0x5415A` | `0xAF63AE4C86019E62` |
| `0x7F55000` | `0x60` | `0x24` | `0x5415B` | `0xD82EC95D355533BA` |

This proves allocator-backed string retention after the requester process cleanup for this success run. It does not yet prove which layer owns each string or that all six are PersistentRead-local. Do not add production disposal changes until allocation sites/lifetimes are traced; all have unclassified site `0` in this capture.

### R8 accounting amendment (2026-10-06)

R8 inspection of the R7 serial artifact confirms these six string objects each match a one-page allocator run start. The same artifact's allocator forensic output is only an aggregate (`count`/`retainedPages`/`netPages`) because per-run rows are disabled in Phase 35 mode. The initial one-read requester reports eight retained pages, leaving two pages beyond the six strings unclassified in that run; later normal success requests report six. R7's separate 25-lifetime delta still averages 14 pages per stress lifetime, but no run-by-run 14-page inventory can be reconstructed from the serial data. No string creation site or semantic identity is proven from the `CreationSite=0` rows, and no repair was made. Full details and the next diagnostic requirement are in `PHASE35R8_ALLOCATOR_ACCOUNTING_CLOSEOUT.md`.

## Remaining required acceptance

Not completed: exact raw 32-byte destination, short-buffer semantics, empty existing value, maximum 65,536-byte value and hash, public SDK local-invalid/no ABI entry, exact NotFound retained-string/storage/handle/fault table, cross-scope byte isolation, replacement read details beyond the aggregate marker, diagnostics-off no-read/one/two slopes, five-lifetime slope, 8 MiB contiguous allocation, 100-lifetime run, exact all-fault counters, same-image reboot with `seedWrites=0`, default and 1280-MiB DSDT mapping regressions, mutation rejection/reproducibility, fresh Phase 34/33/32/31/30/29/28/27/26 controls, AppModel/Compatibility/Phase 8–11/Lifecycle/Taskbar grouping matrix, storage-specific Phase 10 matrix, and successful ordinary full build after runtime acceptance.

Because the full gate failed, no production closeout, commit, or Phase 36 enablement is justified. The accumulated working tree stays uncommitted and preserved.

## Changed files in this continuation

- `Kernel\Misc\Allocator.cs`: aggregate-only allocation-run diagnostic output; recycle only ledger entries verified non-live at post-process snapshot.
- `Kernel\Misc\ManagedImage.cs`: correct phase classification for managed payload kinds 14–22.
- `Docs\PHASE35R7_FINAL_ACCEPTANCE.md`: this closeout.

Other source and runtime changes present in the root and nested repositories predate this continuation and remain part of the accumulated dirty state. No Git operations that mutate history or stage/commit content were performed.


