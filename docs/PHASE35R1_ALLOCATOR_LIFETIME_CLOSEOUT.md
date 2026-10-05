# Phase 35R1 Allocator Lifetime Investigation

## Investigation status

The allocator-stability failure remains unresolved. No fix or post-fix run has
been performed. The earlier Phase 35R Outcome F evidence is preserved and is
not rewritten here.

## Preflight and preserved evidence

- Starting HEAD: `07721246d63634a94b919715cc66be8c3dc5a67a`
- Branch/upstream: `main` / `origin/main`; equal, zero ahead and behind.
- Root worktree: existing Phase 35R modifications and generated artifacts are
  present; none were reset, stashed, rebased, or overwritten.
- Protected serial log SHA-256:
  `9799766EF7D753F634716714E4F89AC8389AE86B4B50947D4E8B6B62BCAF0485`
- Protected durable image SHA-256:
  `7E03DCA6DD33F07F5074DC46390EDA6A2E175DEE2D649EC995CCD61E74CEEC3B`
- `out/rt` is a nested detached Git checkout with extensive pre-existing
  modifications and untracked files. It was inspected read-only and left
  untouched. The adjacent `D:\dev\guideXOS` checkout is clean on `main`;
  `D:\dev\guideXOSServer` has pre-existing staged and unstaged changes on
  `codex/v0.3-scifi-integration` and was left untouched.

## What the preserved 25-lifetime run proves

The stress baseline is `0x3B26000`; the final live usage is `0x3E78000`.
The net increase is `0x352000` = 3,481,600 bytes = 850 pages. Dividing by 25
successful requester lifetimes gives exactly 34 pages (139,264 bytes) per
lifetime. The `Unknown` tag rises by that same 139,264 bytes on each sampled
lifetime. The one-page live-run bucket also rises by 139,264 bytes, indicating
34 one-page allocator runs per lifetime in this run, rather than one 34-page
run.

The guest reports 25 successful return-35 requesters and balanced logical
cleanup. Managed VM page and reservation counts, image-page counts, page-table
counts, and user-stack page counts are all zero-live at the stress baseline
and after every sampled cleanup. The failure therefore is not explained by
those tracked private user mappings.

The existing diagnostic owner scan reports 26 pages (`0x1A000`) still tagged
to a representative requester syscall owner after cleanup. This is a useful
lead, but the current owner key is a folded process handle, has no generation
field, and does not identify the remaining run addresses, allocation callsites,
or whether those runs are kernel GC, service, or backend allocations. It does
not yet prove that those 26 pages are part of the 34-page net increase.

Allocator error counters in the preserved run are `freeInvalid=0`,
`freeCorrupt=0`, and `freeNoPages=0`. Outstanding storage requests and
Persistent handles were zero.

## Managed SDK allocation inventory from source

For a valid successful `GuideXosStorage.TryReadBytes` call, the SDK validates
the path and compatibility, creates a packed request and response on the
managed stack, uses a 65,536-byte `stackalloc` scratch buffer, and allocates
one exact-sized result `byte[]` after success (32 bytes for `state.bin`). It
returns value-type result/status values. The empty-result array is the shared
`Array.Empty<byte>()`. The source contains no per-call path encoding array,
temporary string, service-request object, or managed scratch array on this
success path. Kernel request path decoding and storage service/backend
allocations are separate layers and remain under investigation.

The `17 pages/read` arithmetic (16 scratch plus one result page) is not
supported by the observed allocation data: the scratch is stack allocated in
the requester and the tracked process VM pages are reclaimed. The preserved
run alone cannot exclude other process or kernel allocation interactions, so
no replacement hypothesis is accepted yet.

## DSDT mapping evidence

The preserved serial log came from the 1280-MiB QEMU configuration. It records
ACPI tables mapped around `0x4F77…` and successful ACPI initialization. Those
addresses are above 1 GiB, which is consistent with the bounded bootloader
DSDT mapping change being exercised. The serial evidence does not label the
FADT-referenced DSDT address or signature explicitly, and no default-memory
comparison has been run in this investigation. The independent DSDT regression
gate therefore remains open; the existing mapping fix is preserved.

## Required experiments still open

No-read, one-read, two-read, missing-read, invalid-local-path, raw 32-byte,
raw maximum-size, same-process repeated-read, forced-GC, and diagnostics-disabled
controls have not been performed. The exact retained runs, allocation callers,
owner generations, GC behavior, and page-table ownership after cleanup remain
unattributed. No repair has been selected. Phase 35R validation remains
stopped at Outcome F and Phase 36 remains gated.

## Phase 35R2 allocator provenance matrix (2026-10-04)

The final narrow Phase 35R2 run was booted with provenance enabled. It contains
the required matrix followed by one original-success requester. The serial
log is `out/phase35q/serial-ba88b6be593d4753906d536c377dae8e.log`
(SHA-256 `6390CA311402A8A8F9A6193A48C9F18D678A0B4009A93B2A5D421EB051033D45`).
It emitted `ALLOC_PROVENANCE_READY=1;capacity=262144-live-runs;storage=static-per-page`.
The diagnostic store is fixed-size static allocator metadata, with a 256-run
serial dump cap; the dump reported no truncation. It does not keep snapshots
or allocate a record per event from the allocator under measurement. This
addresses diagnostic-storage growth, although no diagnostics-disabled control
has yet been run.

The matrix used one boot and three fresh managed NativeAOT requesters in
sequence. Each Main returned 35 and the service observed exactly zero, one,
and two reads respectively. The one-read result validated the 32-byte
`state.bin` value. The two-read result mutated its first returned array, then
verified the second read still matched backing data. Process cleanup completed
between snapshots. Process-table counts at B0/B1/B2/B3 were all zero after
setup and cleanup checkpoints.

### Required matrix

Per-lifetime deltas are consecutive snapshots (B1-B0, B2-B1, B3-B2):

| Experiment | Net pages after cleanup | Net bytes | New live runs | Dominant tags |
|---|---:|---:|---:|---|
| no read | 2 | 8,192 | 2 | Unknown (tag 0), global/shared |
| one read | 4 | 16,384 | 4 | Unknown (tag 0), caller label `PersistentStorageRead` |
| two reads | 8 | 32,768 | 8 | Unknown (tag 0), caller label `PersistentStorageRead` |

The allocator snapshots measured B0=`0x3AB0000`, B1=`0x3AB2000`,
B2=`0x3AB6000`, and B3=`0x3ABE000` bytes. Their consecutive deltas agree
with the table. A first dump was captured before `process.Dispose()` and
included one global page that Dispose then freed. Instrumentation order was
corrected to emit retained runs after Dispose. The final run dumps now
reconcile exactly with each snapshot delta and report no truncation.

### Retained-run evidence and ownership

The post-dispose no-read dump lists two one-page live runs, both tag
`Unknown`, owner 0, generation 0, caller label 0; by allocator ownership
these are global/shared:

| Allocation ID | Address | Pages | Owner / generation | Tag / caller label |
|---|---:|---:|---|---|
| `0x2B69E` | `0x7AAE000` | 1 | global / 0 | Unknown / 0 |
| `0x2B69F` | `0x7AB1000` | 1 | global / 0 | Unknown / 0 |

The one-read dump lists four one-page runs at `0x85B3000` through `0x85B6000`
(allocation IDs `0x2CE1D` through `0x2CE20`). Those four are classified as
requester process-private by allocator ownership (the physical role of the
pages is still unknown); they carry owner `-4`
(`0xFFFFFFFFFFFFFFFC`), generation/process handle
`0x0000000200000001`, tag Unknown, and caller label 2
(`PersistentStorageRead`).

The two-read dump lists eight one-page runs at `0x85B7000` through `0x85BE000`
(IDs `0x2DAEE` through `0x2DAF1`, then `0x2DC7F` through `0x2DC82`). These
eight are likewise requester process-private by allocator ownership, with
physical role unknown; they carry owner `-3` (`0xFFFFFFFFFFFFFFFD`), generation/process handle
`0x0000000300000001`, tag Unknown, and caller label 2. In both successful
read experiments, the requester-owned read-call runs alone equal the measured
net delta: four pages per successful read, or 16 KiB per read. Their deeper
allocation source is not identified by the current callsite label.

The corresponding requester handles/generations were
`0x0000000100000001`/1 (no read), `0x0000000200000001`/2 (one read), and
`0x0000000300000001`/3 (two reads). The owner values are distinct across these
generations, despite process-slot reuse. Each exited in terminal state 3.
At each cleanup checkpoint the address-space reference was cleared, live
address spaces were 0, live page tables were 0, and the process table was
empty. The pre-destruction owning `ApplicationInstance` was
`0x0000000100000017` for all three requesters. The evidence distinguishes a
fully absent process record/address space from requester-generation-tagged
allocator runs still present after cleanup.

### Original success-payload comparison

After the required three cases, the same boot ran one requester using the
original Phase 35 success payload (payload kind 1). It returned 35, recorded
six service requests, and left a post-dispose delta of 26 pages (106,496
bytes). Its dump contained exactly 26 one-page live runs, so the dump and
snapshot reconcile. All 26 runs were charged to owner `-6`
(`0xFFFFFFFFFFFFFFFA`), generation/process handle `0x0000000400000001`, tag
Unknown, caller label 2 (`PersistentStorageRead`). The handle was
`0x0000000400000001`, process generation 4, owning ApplicationInstance
`0x0000000100000017`, CR3 `0x7AB6000`, and terminal state 3. After cleanup,
the process table, address spaces, and live page tables were all zero. The
diagnostic dump was taken after `process.Dispose()`; the subsequent allocator
snapshot was taken after the helper returned.

Each listed run is one page and has `freed=0` at the post-dispose snapshot.
The exact allocation sequence ID and starting address are:

| Allocation ID | Address | Allocation ID | Address |
|---|---:|---|---:|
| `0x2EC46` | `0x85E7000` | `0x2EC49` | `0x85E8000` |
| `0x2EC47` | `0x85E9000` | `0x2E966` | `0x85EA000` |
| `0x2E967` | `0x85EB000` | `0x2E968` | `0x85EC000` |
| `0x2E969` | `0x85ED000` | `0x2EAF7` | `0x85EE000` |
| `0x2EAF8` | `0x85EF000` | `0x2EAF9` | `0x85F0000` |
| `0x2EAFA` | `0x85F1000` | `0x2EC48` | `0x85F2000` |
| `0x2EC4B` | `0x85F4000` | `0x2EC4C` | `0x85F5000` |
| `0x2EDDA` | `0x85F8000` | `0x2EDDB` | `0x85F9000` |
| `0x2EDDC` | `0x85FA000` | `0x2EDDD` | `0x85FB000` |
| `0x2EF6B` | `0x85FC000` | `0x2EF6C` | `0x85FD000` |
| `0x2EF6D` | `0x85FE000` | `0x2EF6E` | `0x85FF000` |
| `0x2F052` | `0x8610000` | `0x2F053` | `0x8611000` |
| `0x2F054` | `0x8612000` | `0x2F055` | `0x8613000` |

This did not reproduce 34 pages: the one original-success requester retained
26 pages. The original request path had six PersistentRead ABI entries in
this run; four ended successfully, one returned failure, and one entry had
no matching end marker in the serial trace. The six service-request count is
not interchangeable with successful storage-read count. The result confirms
that the old payload retains many pages on the read syscall path, but does
not yet identify the individual allocator calls or explain the eight-page
difference from the preserved 25-lifetime slope. Since this requester ran
after the no-read/one-read/two-read cases, a first-use/cold-cache difference
also remains possible.

### Interpretation and remaining blocker

The per-read slope in this small matrix is four pages, not 17 pages, and the
no-read lifetime has a two-page net delta. The retained successful-read runs
are attributed to the dead requester generations by allocator owner and
generation, with the PersistentStorageRead syscall label. This is evidence of
retention on the successful read path, but it does not yet prove which
allocation site created those pages, what their buffers contain, whether they
are service/transport/runtime allocations, or which cleanup path should free
them. It also does not explain the previously measured 34 pages per requester
lifetime. The matrix does not establish whether these runs are process-private
GC segments, user buffers, PAL/service transport, or storage/backend pages;
the tag remains Unknown. The harness itself is not shown to retain a process
record or address space, but a diagnostics-off control is still needed to
exclude instrumentation effects.

No root cause or production repair is claimed. The current most likely lead is
four 4-KiB allocations on each one/two-read requester, plus two pages of
no-read/global lifetime growth. The original success payload retained 26
pages in its syscall-labeled owner. The exact 34-page producer remains
unexplained. Do not resume broad Phase 35 acceptance or Phase 36.

Still not run after the required primary matrix: missing-value read, locally
rejected path, raw exact-buffer read, same-process 1/10/25-read behavior,
diagnostics-disabled comparison, five-lifetime slope, original 25-lifetime
stress rerun, fragmentation check, or 100-lifetime amplification. Fault and
allocator error counters were not part of the matrix result record. The next
narrow experiment is to run the original success payload first on a fresh
boot, then break down its syscall-labeled pages by allocator callsite
(including the 64-KiB scratch, 32-byte result, path/request, response and PAL
bridge).

The R2 kernel build succeeded with `Ring3Phase35R2`; the runner completed the
matrix and emitted all three `PASS` results and `ALLOC_MATRIX_COMPLETE=1`.
No ordinary full build, production teardown change, commit, push, or Phase 36
work was performed. The protected failure image still hashes to
`7E03DCA6DD33F07F5074DC46390EDA6A2E175DEE2D649EC995CCD61E74CEEC3B`; its
associated protected log was left in place. Starting and ending root HEAD
remain `07721246d63634a94b919715cc66be8c3dc5a67a` on `main` tracking
`origin/main`.

## Phase 35R3 allocator provenance (2026-10-04)

R3 preserved the original payload as the first requester after backend/fixture
setup on a fresh VM boot. The run log is
`out/phase35q/serial-c7c497ef03b241f18fdf765c98a7f307.log` (SHA-256
`D93EC34D646A474BAA67DE245969CBF3316F4B40F67464C64A7CB6C943FC3D14`). The
pre-launch allocator snapshot recorded bytes/runs/owner and tag totals and
provenance sequence `0x2B64B`; after original-payload cleanup the sequence was
`0x2C9EF`. The completed matrix reported an empty process table, zero live
address spaces, and zero live page tables after each requester cleanup.

### Original-first request reconciliation

The payload entered `PersistentRead` exactly six times. `READ_REQ` sequences
1–6 each have a matching completion marker in this fresh run; the earlier
unmatched-end anomaly did not recur. Five returned success (including the
empty-value read); request 3 returned `NotFound` (service result code 3).
The retained allocation records reconcile as follows:

| Request | Payload path / result | Data destination / capacity | Offset / path length | Allocator events | Freed before return | Retained after return / cleanup | Retained sizes (bytes) |
|---:|---|---|---|---:|---:|---:|---|
| 1 | `state.bin`, success, 32 bytes, end | `0x7FFF007FFDC0` / 32 | 0 / 9 | 401 | 397 | 4 / 4 | 32, 88, 24, 80 |
| 2 | `state.bin`, success, 31 bytes, not end | `0x7FFF007FFDA0` / 31 | 0 / 9 | 401 | 397 | 4 / 4 | 32, 88, 24, 80 |
| 3 | `missing.phase35`, NotFound | `0x7FFF007EFE60` / 65,536 | 0 / 15 | 338 | 332 | 6 / 6 | 128, 104, 528, 128, 24, 96 |
| 4 | `state.bin`, success, 32 bytes, end | `0x7FFF007EFE60` / 65,536 | 0 / 9 | 401 | 397 | 4 / 4 | 32, 88, 24, 80 |
| 5 | `state.bin`, success, 32 bytes, end | `0x7FFF007EFE60` / 65,536 | 0 / 9 | 401 | 397 | 4 / 4 | 32, 88, 24, 80 |
| 6 | `empty.bin`, empty success, end | `0x7FFF007EFE60` / 65,536 | 0 / 9 | 213 | 209 | 4 / 4 | 24, 80, 24, 80 |
| **Total** | | | | **2,155** | **2,129** | **26 / 26** | |

The request-record markers contain buffer pointers/capacities, offset and
path length, but do not print path text. The path identities above are inferred
from the payload source and sequence; the serial result markers independently
show request 3 as NotFound and request 6 as a zero-byte end-of-value success.

All 26 retained allocations are one-page runs with owner `-1`, generation /
process handle `0x0000000100000001`, tag 0, caller label 2, runtime site 6,
and caller return address `0x100110D1`. Their request-time CR3 was
`0x7A5A000`; each returned allocator address was both the recorded physical
and virtual address. Physical runs by request were:

| Request | Retained physical addresses |
|---:|---|
| 1 | `0x8582000–0x8585000` |
| 2 | `0x8586000–0x8589000` |
| 3, NotFound | `0x857F000`, `0x8581000`, `0x858A000`, `0x8580000`, `0x858C000`, `0x858D000` |
| 4 | `0x8590000–0x8593000` |
| 5 | `0x8594000–0x8597000` |
| 6 | `0x85A8000–0x85AB000` |

The two owner-0 interval runs are `0x7A52000` (requested size 56) and
`0x7A55000` (requested size 40); both are one page, tag 0, caller label 0,
runtime site 6, CR3 `0x4E4AF000`, caller return address `0x100110D1`.

Thus the fresh original-first run disproves “every entered request retains
exactly four pages.” The one `NotFound` transaction retained six pages. The
other five transactions retained four each. The final allocator delta is 28
pages: those 26 request-attributed pages plus two owner-0 pages created during
the measurement interval. `2 + (6 × 4) = 26`, so `2 + 4N` does not fit this
six-entry workload. The measured reconciliation is `2 + (5 × 4) + 6 = 28`.
The two additional runs have the same `NativeRuntimeMalloc`/`RhpNewArray`
provenance described below, but their precise global initialization role is
not established.

### Producer and physical role

All 26 requester-attributed retained runs are one-page allocations at the
same explicit allocator category, `NativeRuntimeMalloc` (diagnostic site 6),
reached through `stdlib.malloc` → `Allocator.Allocate`. The captured runtime
caller return address is `0x100110D1`, immediately after the `malloc` call in
the disassembled NativeAOT `RhpNewArray` helper. Requested object sizes vary;
the four successful-read survivor sizes are 32/88/24/80 bytes. This identifies
the physical role as page-backed NativeAOT managed array objects allocated
through `RhpNewArray`; it is not a user data page, page-table page, pinned
caller buffer, FAT sector buffer, or 64-KiB SDK scratch mapping. The C# array
creation callsites and object-reference roots are not yet identified.

Each request-time record has `virtual == physical`, was observed under the
requester CR3, and carries the synthetic diagnostic owner derived from
process handle/generation `0x0000000100000001` (owner `-1`). The two extra
owner-0 runs were observed under kernel CR3 `0x4E4AF000`. After cleanup the
requester CR3 and its address space are destroyed, so the recorded mapping
does not establish the page's post-cleanup mapping in the active kernel root.
No post-disposal translation was captured. All request runs have
`freeSequence=0` at the request boundary and final snapshot.

This attribution is deliberately narrower than a production ownership claim:
the negative owner and generation are R3 diagnostic labels installed while
dispatching this syscall. `Allocator.Free` updates ordinary owner accounting
only for positive owners, and there is no allocator sweep by requester
generation in `Ring3Process.Cleanup`. Process cleanup releases its known
stacks, bootstrap/image allocations, tracked VM pages, address space and page
tables; it cannot prove that NativeAOT array objects are unreachable or that
the GC has released them. The syscall `finally` disposes the result bytes,
result, service result, read request, context result, context and decoded path.
The event trace shows 2,129 request-local allocations freed before return,
while the 26 survivors receive no allocator free. Root reachability/GC
collection and the intended lifetime of each survivor remain unproven, so
there is no exact missing production cleanup path yet.

### Controls and limits

After preserving the original-first result, detailed event recording and
requester-owner labeling were disabled while allocator page totals remained
active. On that same warm boot, the no-read, one-read and two-read controls
added 0, 4 and 8 pages. The slope therefore survives this event-recording
disable switch. It does not replace independent fresh boots for each control.
The earlier fresh-boot R2 matrix had a no-read first-requester delta of 2
pages, then one-read 4 and two-read 8; the R3 diagnostics-off no-read delta is
0 because it followed the original-first run. Three-read was not run. The
standalone raw exact-buffer, standalone missing-value, malformed raw and
locally rejected controls were not run. The original-first embedded NotFound
request left six pages, not four.

The protected historical 34-page log was located by its supplied hash at
`out/phase35q/serial-54cc23f555eb4f44a093057c8f413ac1.log`. It contains 196
`RING3_PERSISTENT_READ_ABI_ENTERED` markers and 156 end markers across the
entire mixed proof run, plus aggregate service counters. It has no request
sequence tied to the specific 34-page requester lifetime, so those aggregate
counts cannot reconstruct that lifetime's request count. Historical 34 pages
cannot be exactly reconciled because per-request provenance did not exist.
The original failure log and image hashes remain unchanged, as does the
protected R2 serial log hash `6390CA311402A8A8F9A6193A48C9F18D678A0B4009A93B2A5D421EB051033D45`.

### R3 outcome

Outcome F. The measured allocation path is NativeAOT managed-array allocation
through `RhpNewArray` and `stdlib.malloc`, but the individual C# arrays, GC
roots/collection state, post-disposal mapping state and precise intended
lifetime are unresolved. No production teardown was changed. Independent
fresh 1/2/3-read boots, fresh diagnostics-off baselines, raw/malformed/local
reject cases, same-process repetition, GC/root analysis, post-disposal mapping
translation, five/25/100-lifetime slopes, fragmentation/8-MiB allocation,
and allocator/fault-counter closeout remain open. Phase 35R acceptance,
artifact closure, regressions, ordinary full build and Phase 36 remain gated.

During this R3 investigation, root HEAD started and ended at
`5fac894f3b70ab10491710fd8ea414a4d1f6a4ca`, branch `main`, tracking
`origin/main`, ahead/behind `0/0`. The outer worktree contains the Phase 35R
source and this report as uncommitted work. The nested `out/rt` checkout
remains detached at `9d5a6a9aa463d6d10b0b0ba6d5982cc82f363dc3` with its prior
dirty state preserved.
