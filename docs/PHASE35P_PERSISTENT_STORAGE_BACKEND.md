# Phase 35P Persistent Storage Backend Audit

**Outcome: C — no suitable durable backing store is available in the current
UEFI production path.** Phase 35 remains blocked. No storage implementation,
fixture, managed SDK surface, or write authority was added. `Persistent`
continues to return `ResourceUnavailable`; it is not redirected to
`Temporary`.

**Audit date:** 2026-10-01

This is a new prerequisite audit. The historical Phase 35 blocker report in
[`PHASE35_MANAGED_STORAGE_READ.md`](PHASE35_MANAGED_STORAGE_READ.md) is
preserved unchanged and still accurately describes the state before Phase
35P.

## Repository preflight

| Item | Starting state |
| --- | --- |
| Repository | `D:\dev\guideXOSUEFI` |
| Branch / HEAD | `main` / `5ba7da79dff96fc1fe55c23d82273fdedcf937b2` |
| Subject | `Document Phase 35 storage read blocker` |
| Upstream / divergence | `origin/main`, 0 ahead / 0 behind |
| Root worktree | clean; no untracked files |
| Nested runtime | `out\rt`, detached at `9d5a6a9aa463d6d10b0b0ba6d5982cc82f363dc3`; 58 pre-existing dirty paths (54 modified, 4 untracked) |

The nested runtime's existing state is protected. No runtime files were read
for editing or changed.

## Persistent semantics

Phase 10 defines the names `Persistent` and `Temporary`, but does not claim a
working persistent backend. Its accepted contract says temporary values are
AppId-scoped, shared between instances, retained across requester
termination, and cleared by the explicit App Model reset/reboot boundary.
Persistent writes and deletes are unavailable. The Phase 10 design also keeps
"persistent app-local storage" deferred until it has an implementation.

For Phase 35P, the honest intended meaning of `Persistent` is therefore:

- A value survives requester/process exit and replacement
  `ApplicationInstance`s with the same AppId.
- An App Model reset does not clear it. Reset is the explicit clearing
  boundary for `Temporary`, not durable application data.
- Reboot durability is required before advertising durable persistent app
  storage. No reboot-survival promise can be proven by the current backend.

These are the semantics required for a future durable backend; they are not
claims about the current implementation. The current `Persistent` namespace
has no stored values, so requester replacement, reset-survival, and reboot
survival cannot be exercised for it.

## Live storage architecture audit

The Phase 10 service is service 9,
`CSharpApplicationStorageService` in
`guideXOS/OS/ApplicationStorageServices.cs`. It validates context and
requests, then returns `ResourceUnavailable` for `Persistent` Exists, Read,
Write, Delete, and Enumerate. The existing result vocabulary makes the
unavailable state explicit. Temporary remains a separate in-memory table
keyed by exact `ApplicationId`; its namespace is reset by
`ResetForAppModel`.

The production UEFI boot path installs a `Ramdisk` from the boot-provided
initrd and mounts `RdskFS` (`Kernel/Misc/EntryPoint.cs` and
`guideXOS/Program.cs`). `RdskFS` reads packed files from the ramdisk but has
empty `WriteAllBytes` and `Delete` methods. This is the live UEFI file backend;
it is not a writable persistent volume. It is repopulated as boot input and
does not survive reboot as writable application state.

| Candidate | Source and lifecycle | Read / write / durability | Decision for UEFI Persistent |
| --- | --- | --- | --- |
| Boot initrd + `RdskFS` | UEFI loader supplies a RAM-backed image; `EntryPoint` mounts it for the kernel file API. | Reads packed files. `WriteAllBytes` and `Delete` are no-ops; no flush or durable commit exists. | Not writable or durable. |
| `MiniRamFs` scratch disk | Optional kernel-allocated memory disk created by `RamDiskManager.CreateAndMountScratch`; not mounted by the production UEFI path. | Read/write sectors and filesystem entries in RAM only. No flush to persistent media. | Volatile; not a Persistent backend. |
| FAT/FAT32 | `Kernel/FS/FAT.cs` implements directory, allocation, write, and delete operations over a `Disk`. No app-data mount/root is registered by the UEFI path. | Filesystem writes call the underlying block device. Durability depends entirely on that device and its successful sync. FAT name lookup is case-insensitive. | Filesystem capability alone does not provide a UEFI durable device or the service's exact case-sensitive path contract. |
| `FileDisk` virtual images | Loads an image file into a byte array; virtual-disk managers keep mount records and switch the global `File.Instance`. | Sector writes modify the in-memory image. `Sync` writes the complete image back through the active guest `File` API; under the UEFI `RdskFS` this reaches its no-op writer. Sync logs failures but does not return a typed durability result. | Not a production persistent volume. No host path is used as a substitute. |
| IDE / SATA | `EntryPoint` initializes IDE and SATA only in the Legacy boot branch. The UEFI branch does not initialize or bind these devices as the application file backend. | IDE has sector writes and issues ATA cache flush, but that code is not the selected UEFI path and has no service-level flush result. | Not available to this UEFI service path. |
| USB mass storage | `USBMSCBot.USBDisk` is created on explicit device use; it is not mounted as app storage. | Reads are supported; `Write` returns `false` by design. | Read-only and not a candidate. |

The `Disk` abstraction has `Read` and `Write`, but no flush/sync operation.
FAT's `WriteSector` also discards the block device's boolean write result, so
filesystem mutations do not produce a reliable typed I/O failure. The IDE
driver issues ATA `CacheFlush`, but there is no service-level commit result;
its readiness polling has no timeout. These are additional durability and
failure-reporting prerequisites even if a UEFI block device is added.

The kernel file API has one active `File.Instance`; `VirtualDiskAutoMount`
tracks image mount-point labels but does not provide a path-resolving VFS
namespace for application storage. Its images are files on the current
filesystem, not an independently durable device. The older Legacy boot branch
can select an IDE-backed FAT filesystem, but that branch is not evidence for
the UEFI production build being audited here.

`SystemMode` describes an installed/live distinction and contains a
`CanWriteSettings` probe, but that probe writes and rereads a file through the
current `File` backend; it does not establish flush success or reboot
durability. `ApplicationSettingsService` is separately implemented as a
bounded in-memory store keyed by AppId. It is session state, not an app-data
filesystem. Package resources are service 8 and remain separate immutable
data; they are not a Persistent storage fallback.

## Namespace, confinement, and fixture decision

No persistent root or mount is selected because no legitimate durable UEFI
backing volume is available. No ApplicationId-to-directory encoding or
collision rule is selected. The service context currently bounds ApplicationId
to a nonempty value of at most 96 UTF-16 code units; it does not supply a
filesystem-safe character whitelist. Raw IDs therefore cannot be used as
directory names without a separate safe encoding.

The existing Phase 10 relative-path validator remains the front-door rule:
1–192 UTF-16 code units, segments 1–64, relative only, no colon, empty/dot/
dot-dot segments, controls, or `< > " | ? *`. It does not prove confinement
after combining a namespace root with a path. A future backend must confine
each resolved component beneath a trusted app root. FAT32 and the packed
`RdskFS` format have no symlink/reparse-point semantics, but that fact does
not create an app root or establish path confinement here.

No trusted fixture was created. There is no persistent seed path, seed timing,
overwrite policy, AppId, relative key, byte sequence, or hash to report. The
Temporary `state.bin`/`survive.bin` fixtures and the Resources service 8
`diagnostic.fixture` are separate proofs and are not reused.

## Persistent service results and acceptance gates

| Gate | Result in this audit |
| --- | --- |
| Persistent availability | `Unavailable`, reported as `ResourceUnavailable`; no fallback |
| Persistent Exists / Read | Both return `ResourceUnavailable` after request/context validation; no lookup occurs |
| Persistent Write / Delete / Enumerate | Continue to return `ResourceUnavailable` |
| Exact content, chunking, empty/missing values | Not applicable to Persistent without a backend. Existing Temporary semantics remain documented in Phase 10/35 audit. |
| Trusted fixture and idempotence | None; no safe backing to seed |
| Requester exit / replacement AppId | Temporary survival is covered by the prior Phase 10 self-test; Persistent survival is unproven because it has no entries |
| Cross-AppId isolation / stale context | App Model context validation remains before storage; no Persistent backend was reached or changed |
| App Model reset | Clears Temporary; there is no Persistent store to preserve. Intended Persistent behavior is survival across reset. |
| Reboot | No reboot-persistence claim or proof is available |
| Temporary / Resources separation | Preserved; no namespaces were modified or merged |
| I/O failure / missing volume | The selected Persistent adapter returns `ResourceUnavailable`; there is no disk I/O attempt and therefore no typed disk-I/O failure to inject |
| Runtime stability and regression cohort | Not rerun. No production source changed, so this blocker audit adds no new runtime evidence. Existing accepted Phase 10 and Phase 34 records remain historical evidence only. |

The Phase 10 App Model markers continue to be the relevant existing
diagnostics, including `PHASE10_STORAGE_PERSISTENT_UNAVAILABLE_OK=1`,
`PHASE10_STORAGE_PATH_CONFINEMENT_OK=1`, `PHASE10_STORAGE_APP_SCOPE_OK=1`,
and `PHASE10_STORAGE_RESET_OK=1`. This phase did not execute those tests or
claim new marker output. No Phase 34 boot was run.

## Closeout and next step

Only this audit document is changed. No service/backend code, NativeAOT
runtime code, managed Phase 35 SDK, fixture, build artifact, ramdisk, or EFI
image was changed. No full build or regression test was run because the
production source is unchanged and the audited backend cannot meet the
acceptance gate. The historical Phase 35 blocker report remains intact.

The next prerequisite is a supported UEFI block-device path with a trusted
app-data root, bounded write/read operations, and an observable successful
flush/sync contract. Once that exists, seed a trusted deterministic fixture
and prove application scope, replacement-instance behavior, reset behavior,
and reboot persistence before resuming the managed Phase 35 read API. Phase 36
managed writes remain paused.
