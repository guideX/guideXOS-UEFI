# guideXOS C# UEFI — Task Manager Memory Details Allocator Qualification R2

**Date:** 2026-10-18
**Repository state:** Live repository remained on `main` at `b4af3c8fd92dcbd35ec594bd33e6b1d87d521d41`; the qualification ran in the ordinary-copy sandbox `C:\gxR2`. No live files were changed, and no commit or push was made.

## R2 objective and current status

R1’s crash correction remains in place. R2 identified and fixed the repeated Processes footer string leak, corrected additional redraw ownership paths, and corrected runtime snapshot recovery. The exact production build and A–F matrix are complete. The v0.2 release blocker remains **open** because the 25-cycle production run still adds live allocator pages and the residual run-level owner has not been isolated. Bare-metal qualification is also unavailable.

## Allocator counter definition

`Allocator.MemoryInUse` is `_Info.PageInUse * PageSize` (`Kernel/Misc/Allocator.cs:295`, with `PageSize = 4096`). The allocator increments `PageInUse` by the number of pages when it records a live run and decrements it when a valid run is freed. It therefore reports pages currently marked live in the global allocator. It is not a cumulative allocation total, high-water mark, reserved-address-space count, per-process measurement, or requested-byte count. Global/kernel allocations are included.

The raw snapshot parser accounts for 262,144 pages (1 GiB): live runs, free pages, and reserved pages. It records live-run count, live pages, free-call and free-success totals, invalid/no-pages/corrupt-free failures, largest contiguous free run, and owner/tag page totals. Requested bytes are not tracked by the allocator (`requestedBytes: null`). The tested runs reported zero free failures and internally consistent page sums.

## Controlled pre-fix matrix

Scenarios A–F used the same pre-fix R1 production candidate (`candidate-pre-fix-r1`) and identical component hashes: kernel `1875F96565320AA007A34316072F5E7E4BDD7E83D3B7C60B74DC1AE2E37B10DA`, ramdisk `F7EFBFF3BC4B845303419DDF523EBC27DBFD01D9174EBEEF5F8A7009E8D780A8`, and EFI `8C0A01ACDEC39C61C139E4AD84DDFC8D110174951CECF07401F0635B888F7FAD`. Each scenario ran in a separate QEMU boot.

| Scenario | Start → final allocator bytes | Live pages, start → final | Live runs, start → final | Net bytes |
|---|---:|---:|---:|---:|
| A — idle desktop, 30 s | 19,083,264 → 19,353,600 | 4,659 → 4,725 | 1,201 → 1,267 | +270,336 |
| B — Processes tab, open/close | 19,144,704 → 27,901,952 | 4,674 → 6,812 | 1,216 → 2,991 | +8,757,248 |
| C — one Memory Details cycle, 60 s cleanup | 19,058,688 → 25,411,584 | 4,653 → 6,204 | 1,195 → 2,383 | +6,352,896 |
| D — five Memory Details cycles | 19,144,704 → 30,531,584 | 4,674 → 7,454 | 1,216 → 2,289 | +11,386,880 |
| E — 25 Memory Details cycles | 19,034,112 → 103,890,944 | 4,647 → 25,364 | 1,189 → 13,479 | +84,856,832 |
| F — 50 mixed tab switches | 19,083,264 → 44,642,304 | 4,659 → 10,899 | 1,201 → 6,630 | +25,559,040 |

All six scenario summaries reported zero invalid, no-pages, or corrupt-run frees and zero matching serial fault lines. Growth occurred on the Processes tab and during mixed tab switching as well as during Memory Details. The pre-fix evidence therefore did not isolate the growth to Memory Details alone. The 25-cycle trace increased throughout the run rather than returning to its initial live-page count.

Evidence is under `out/taskmanager-memorydetails-qualification-r2/evidence-pre-fix-final/{A..F}/summary.json` in the sandbox. Per-cycle D and E live-page trends are also exported as `D/allocator-trend.csv` and `E/allocator-trend.csv`. The earlier E trace stopped at cycle 25 and did not capture a later deferred-cleanup snapshot; the fresh post-fix E run uses the current runner and includes the requested 60-second observation.

## Retained-run attribution and owner

The allocator diagnostic counter was verified against its page map. In the site-tagged B0–B5 probe, the allocator reported 22,286,336 bytes / 1,321 live runs at B0 and 20,729,856 bytes / 1,472 live runs at B5. The exact B5 provenance records contained 155 new retained runs spanning 180 pages; four baseline runs totaling 560 pages had been freed. This reconciles exactly: 155 − 4 = +151 live runs and 180 − 560 = −380 pages, or −1,556,480 bytes. The B5 run rows are preserved in `out/taskmanager-memorydetails-qualification-r2/evidence-r13-string-sites-probe/C/b5-retained-run-inventory.csv`; fields include run ID, address, page count, managed bytes/type, creation site, caller, owner, and generation. A separate current5 probe reported 157 new runs / 182 pages and five baseline frees / 561 pages, reconciling to −379 pages. The bounded buffers did not drop records or allocate per redraw.

A site-tagged diagnostic build identified 140 post-B0 live strings: 138 ordinary String constructor records, one `StringPool.GetNumber(int)` record, and one `StringPool.GetMemorySize(ulong)` record. Eighteen B5 rows in that probe shared length 37 and hash `E0DAA1A59F44630D`, matching the rendered footer `Instances: 1  Active: 0  Suspended: 0`; the current5 probe independently recorded 19 rows with that hash. Per-string address, size, length, allocation sequence, site, hash, and disposal/free state are in `out/taskmanager-memorydetails-qualification-r2/evidence-r13-string-sites-probe/C/b5-live-strings.csv`. This footer is built in `TaskManager.DrawProcesses` on every Processes-tab redraw. The diagnostic traces also showed fresh numeric strings and concatenation results being passed to `Font.DrawString` without a corresponding owner.

The logical owner is the Processes-tab draw call. `Font.DrawString` borrows its input for synchronous rendering; it does not take ownership. The repeated footer and intermediate strings were therefore stranded by `DrawProcesses`, and because that method also draws the Processes tab, the effect appears outside Memory Details. The source and tagged trace establish the allocation path; the separate pre-fix matrix establishes sustained global live-page growth under repeated Task Manager use.

## Source correction

`guideXOS/DefaultApps/TaskManager.cs` now scopes the three fresh numeric conversions and both concatenation results in a `try/finally` around the borrowed `DrawString` call. The final string is disposed first, followed by its intermediate and numeric inputs, after synchronous drawing. These values come from fresh `ToString`/`string.Concat` results, not shared StringPool entries. R2 also replaced the global memory-size lookup that grew `StringPool` by exact numeric key, reused static Performance navigation arrays instead of allocating per redraw, and added deterministic disposal for fresh CPU, memory, disk, network, and uptime text. The missing widget auto-hide update call is restored. R1’s 14 privately owned Memory Details labels and overflow-safe utilization arithmetic remain.

`Corlib/System/StringPool.cs` contains temporary diagnostic site tags guarded by diagnostic build symbols; they are excluded from production. The focused `Tests/TaskManagerMemoryDetailsRegression.ps1` check passed after the final scalar-storage correction. The complete build and production matrix results follow.

## Post-fix qualification and release decision

The full unskipped build and A–F QEMU runs completed against one frozen production candidate. See “Final production qualification” below for the image identity, allocator results, UI checks, and open release decision.

### Runtime invalid-snapshot recovery and scalar projection correction

The first current-source diagnostic G boot correctly displayed `N/A` during a forced malformed snapshot, but the later valid snapshot still displayed `N/A`. Numeric serial tracing isolated two custom NativeAOT value-copy behaviors: the service constructed a 1 GiB snapshot, while `ApplicationServiceResult<SystemInformationSnapshot>.Value` exposed zeroed scalar fields; assigning the reconstructed readonly snapshot struct into a Task Manager instance field also zeroed those scalars. The bounded service text fields remained valid. `Ring3Abi` already uses a direct allocator/thread-pool scalar fallback for the same generic projection behavior.

`TaskManager.RefreshSystemSnapshot` now reconstructs scalar values from `Allocator`, `ThreadPool`, and `Timer` when the projected memory size is zero, clamps in-use memory to total, validates the result, and stores counters in primitive Task Manager fields. It retains the bounded text from the service result. `DrawMemoryDetails` validates scalar bounds before subtraction or percentage calculation; the invalid-to-valid label-state transition still forces a refresh. No shared strings are freed by this path.

The focused regression passed after this change. Diagnostic QEMU G then passed the full invalid-to-valid UI flow: the malformed interval rendered `N/A`; the next valid frame emitted `R2_TASKMANAGER_NA_RECOVERY_RENDERED` and visibly showed **1 GB total, 20 MB in use, 1003 MB free, 2% utilization**. It returned to Processes, closed, waited 60 seconds, and produced no fault markers. Evidence is in `out/taskmanager-memorydetails-qualification-r2/evidence-r2-recovery-finaldiag-inventory-verified/G/`; the recovery screenshot is `fallback-valid-recovery.ppm` and the serial trace is `serial.log`. This is a diagnostic build, not a production boot.

That B0–B5 diagnostic lifetime recorded B0 at 5,458 live pages (`0x1552000` bytes) and B5 at 5,005 live pages (`0x138D000` bytes). B5 provenance contained 82 post-baseline run records totaling 107 currently retained pages and four freed baseline runs totaling 560 pages. The net is **−453 pages**, matching the snapshot delta exactly. The probe's printed `netPages=0` saturates negative values; the independently reported values provide the signed reconciliation. This single lifetime did not show positive net growth after deferred cleanup. It does not resolve the separate 25-cycle production trend.

## Final production qualification — 2026-10-18

### Repository, build, and frozen candidate

- Starting and ending live repository state: branch `main`, HEAD `b4af3c8fd92dcbd35ec594bd33e6b1d87d521d41`. The starting worktree was clean. All source, docs, build outputs, and QEMU evidence described here are in the ordinary-copy sandbox `C:\gxR2`; no live repository file changed. No commit or push was made.
- Full unskipped `build.ps1`: exit code 0 and `Build Complete`. Managed artifact agreement passed 41/41 descriptors, build staging, ramdisk staging, and compiled allowlist. Phase 35 passed 9/9 modes. Build log: `out/taskmanager-memorydetails-qualification-r2/r2-full-production-build-final.log`.
- One frozen production candidate was used for all six A–F boots and the desktop smoke. Allocator class static virtual base: `0x101370A0`.

| Component | SHA-256 |
|---|---|
| Kernel | `EF76CFBC6A78661EF0FBD4C09CAE9F400BF14B2969C53F66BECAED64B454E759` |
| Ramdisk | `F7EFBFF3BC4B845303419DDF523EBC27DBFD01D9174EBEEF5F8A7009E8D780A8` |
| EFI bootloader | `4B88FEB3CCB28D83EE0CF20645DDAC31B48146709BFC89F44AA547012F20F599` |
| Kernel map | `A07A08B2D494CC9902390D368BB247C86B487FD0D23CECFB0A6F0C128A250B75` |

Manifest and frozen ESP: `out/taskmanager-memorydetails-qualification-r2/candidate-r2-final-production/`. Combined evidence and machine-readable summary: `out/taskmanager-memorydetails-qualification-r2/evidence-r2-final-production-scalar-recovery-qualified/final-matrix-summary.{json,csv}`. All A–F and desktop smoke summaries contain the same kernel, ramdisk, and EFI hashes.

### Production scenario results

Each row is a separate cold QEMU boot using the frozen production candidate. Pages are 4 KiB allocator pages. “Final” is after the scenario’s close/cleanup observation; E includes the required 60-second wait. All snapshots reconciled live, free, and reserved pages across the 1 GiB allocator map.

| Scenario | Start: bytes (pages/runs) | Peak bytes | Final: bytes (pages/runs) | Net bytes |
|---|---:|---:|---:|---:|
| A — idle desktop, 30 s | 19,066,880 (4,655/1,197) | 19,337,216 | 19,337,216 (4,721/1,263) | +270,336 |
| B — Processes open/close | 19,066,880 (4,655/1,197) | 21,336,064 | 19,890,176 (4,856/1,371) | +823,296 |
| C — one Memory Details cycle, 60 s cleanup | 19,050,496 (4,651/1,193) | 21,377,024 | 20,250,624 (4,944/1,459) | +1,200,128 |
| D — five Memory Details cycles | 19,091,456 (4,661/1,203) | 21,377,024 | 21,377,024 (5,219/1,734) | +2,285,568 |
| E — 25 Memory Details cycles, then 60 s | 19,173,376 (4,681/1,223) | 28,123,136 | 28,123,136 (6,866/3,381) | +8,949,760 |
| F — 50 mixed tab switches | 19,066,880 (4,655/1,197) | 22,253,568 | 20,480,000 (5,000/1,515) | +1,413,120 |

Scenario E was 27,934,720 bytes / 6,820 pages / 3,335 runs after cycle 25 and 28,123,136 bytes / 6,866 pages / 3,381 runs after the following 60 seconds. The wait added 46 live pages (188,416 bytes). Across all six boots there were no page-map consistency errors, invalid/no-pages/corrupt-run free failures, or matching CPU-fault/panic serial lines. The trend files under E and D include every post-close checkpoint.

| E checkpoint | Live pages | Net pages since B0 | Increment since prior checkpoint |
|---|---:|---:|---:|
| B0 | 4,681 | 0 | — |
| Cycle 1 | 4,876 | +195 | +195 |
| Cycle 5 | 5,233 | +552 | +357 over cycles 2–5 |
| Cycle 10 | 5,655 | +974 | +422 over cycles 6–10 |
| Cycle 15 | 6,031 | +1,350 | +376 over cycles 11–15 |
| Cycle 20 | 6,450 | +1,769 | +419 over cycles 16–20 |
| Cycle 25 | 6,820 | +2,139 | +370 over cycles 21–25 |
| 60 s after cycle 25 | 6,866 | +2,185 | +46 during wait |

After the first cycle, each five-cycle block added 74–89 pages per cycle on average. The observed series did not plateau within 25 cycles. The duration-matched idle control below quantifies background drift but does not explain the remaining stress-run increase.

The 30-second idle control grew by 66 pages. The duration-matched production idle control used the same frozen candidate with Task Manager closed for exactly 900 seconds. It grew from 4,734 to 5,074 live pages: +340 pages / 1,392,640 bytes and +340 live runs, with zero free failures or page-map errors. Production E lasted 786 seconds from B0 to the final post-cleanup snapshot. Scaling the idle rate linearly predicts about 297 pages over that interval, leaving approximately 1,888 pages above that rate. This comparison is descriptive and does not attribute those pages to a specific owner. B, C, and F also ended above their own baselines, and D rose across five repeats. E gained 46 more pages during its final cleanup minute. No plateau was observed.

### Diagnostic 25-lifetime retained-run inventory

A separate `TaskManagerMemoryR2Probe` image ran 25 open/Memory Details/close lifetimes. It used the same production ramdisk and EFI; the diagnostic kernel SHA-256 is `896048658B862D9AE8F3AEA35C05579C4EDAB8A4D31DC2A3C51664DBECCDF882`. Every lifetime emitted B0–B5 once, all 100 UI state checks passed, and there were no serial fault markers. The parser recovered 970 B5 run rows and 25 run totals; all 25 run counts, run pages, and signed B0-to-B5 live-page deltas reconciled exactly. The first lifetime included the one-time invalid-snapshot injection; cycles 2–25 did not.

| Diagnostic lifetime | New retained runs/pages at B5 | Freed baseline runs/pages | B0-to-B5 live-page delta |
|---|---:|---:|---:|
| 1 | 82 / 107 | 4 / 560 | −453 |
| 2 | 33 / 33 | 4 / 560 | −527 |
| 7 | 79 / 79 | 4 / 560 | −481 |
| 13 | 79 / 79 | 4 / 560 | −481 |
| 25 | 33 / 33 | 4 / 560 | −527 |

The full per-run, per-site, per-helper, live-string, and B0/B5 reconciliation exports are under `out/taskmanager-memorydetails-qualification-r2/evidence-r2-final-diagnostic-inventory-25-offset220/inventory25/parsed/`; the raw serial and UI summary are beside them. This inventory narrows the allocator activity mechanically, but not to a logical product owner: allocator owner/tag fields remain zero and caller addresses resolve only to NativeAOT `RhpNewArray` / `RhpNewFast` helpers. The diagnostic emitted about 2.7 MB of serial records and grew 590–639 pages between a B5 sample and the next B0 sample. Its instrumentation therefore contaminates the across-cycle trend, and the per-window negative deltas do not explain the separate production E growth.

The first B attempt was rejected by the runner’s pixel check even though the screenshot showed a closed Task Manager and clean desktop: dark red wallpaper pixels passed a dark-panel threshold. The screenshot and logs were preserved under `evidence-r2-final-production-scalar-recovery/B/`. The check was corrected to require neutral dark-gray pixels, then A–F were rerun in the qualified evidence root. The final B run passed both open and close checks. No product source changed for this runner correction.

### Runtime fallback and desktop interactions

- Runtime G used a diagnostic build, not the production candidate. The malformed snapshot rendered `N/A`; the next valid snapshot recovered visibly to 1 GB total, 20 MB in use, 1003 MB free, and 2% utilization. The UI recovery markers and screenshots are in `evidence-r2-recovery-finaldiag-inventory-verified/G/`. This proves runtime behavior under the diagnostic injection; it is not a production invalid-data boot.
- The six A–F production boots exercised Start Menu launch, Task Manager activation and close, Processes, Memory Details, repeated Memory Details entry/exit, and 50 mixed tab changes. D also passed widget edge reveal and close-control checks.
- A separate production desktop smoke passed Files desktop-icon open/close, confirmed the icon remained visible, and reopened/closed Task Manager through Start Menu. QEMU selected its relative PS/2 mouse; all UI actions therefore also exercised cursor input. The smoke summary reports no serial fault lines.
- Bare metal was not available and was not tested.

### Remaining attribution and release decision

The counter is a current-live-page measure, so E’s positive delta represents allocator runs still marked live after the 60-second cleanup; it is not a cumulative or high-water statistic. The known R1 Processes footer leak and the additional redraw allocation paths were fixed. A separate diagnostic candidate completed 25 B0–B5 lifetimes after those fixes. Its 970 retained-run rows and 25 run totals reconcile exactly to every diagnostic B0/B5 snapshot. This gives per-lifetime allocator run inventories, but it does not attribute the production E trend: the diagnostic runs typically netted −527 pages within each window because 33–79 new pages were retained while 560–561 baseline pages were freed. The production raw snapshots still expose only global owner/tag zero. Diagnostic caller addresses resolve only to generic NativeAOT `RhpNewArray` and `RhpNewFast` helpers, so the logical Task Manager owner remains unknown. The diagnostic image also emitted a 2.7 MB serial trace and gained 590–639 pages between one B5 sample and the next B0 sample; treat its cross-cycle total as instrumentation-contaminated, not as a production trend.

The focused regression passed for ownership, snapshot bounds, unavailable data, overflow arithmetic, and recovery source conditions. `git diff --check` found no whitespace errors; Git printed only expected line-ending normalization notices in the sandbox. The production matrix and both runtime diagnostics had no allocator free errors or unexpected CPU faults. The v0.2 blocker remains **open** because the 25-cycle production run adds about 1,888 pages above a simple duration-scaled idle comparison, the residual has no logical owner attribution, and bare-metal qualification is unavailable.

**R2 outcome: F — allocator accounting is understood, the R1 owner was repaired, and runtime interactions pass, but the residual 25-cycle live growth is not attributed or shown to plateau. Release blocker: OPEN.**
