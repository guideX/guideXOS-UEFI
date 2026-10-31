# AMCONV2C — Filesystem Mount Lifetime and Typed Inspection Errors

**Outcome: `AMCONV2C_PARTIAL`**

The active C# filesystem now has an enforceable lease boundary for typed folder
launches. The folder request, factory inspection, Computer Files window, and
child document I/O retain the selected mount instead of following a later
`File.Instance` replacement. The production mount-binding and lifetime code
passes hosted checks. The full GUI/application-instance factory path remains
guest-only, and the existing `GetFiles` APIs do not report enough information
to distinguish every real backend failure from an empty or missing directory.

## 1. Branch and HEAD

- Starting branch: `APP_MODEL`
- Starting HEAD: `04aa6d7bbad03a521a8dd53906ca453687e0db1f`
- Starting worktree: clean; branch was already six commits ahead of
  `origin/APP_MODEL`.
- Ending branch: `APP_MODEL`; no branch switch occurred.
- Ending HEAD: the local AMCONV2C closeout commit. Its exact SHA is reported
  with the final completion message because this file is part of that commit.

The initial managed identity evidence was present and applicable. The
source-input roots recorded by BUILD1E do not include the application-model
and filesystem files changed in this phase.

## 2. Filesystem ownership audit

`File.Instance` is initialized during boot by ramdisk/filesystem setup in
`Kernel/Misc/EntryPoint.cs`, `guideXOS/Program.cs`, and
`Kernel/Drivers/RamDiskManager.cs`. The `FileSystem` base constructor also
registers instances through `File.Instance`. The no-argument `AutoFS()` path
selects an implementation and makes a final assignment of that implementation
to `File.Instance`. The disk-argument constructor also registers its wrapper
through the base constructor, but it does not make that final assignment to
its inner implementation; it is used by drive-specific filesystems.

Production replacements are spread across boot recovery, Disk Manager,
HD Installer, Virtual Disk Manager, virtual-disk auto-mounting, USB file
helpers, and test/example code. They all write through the public
`File.Instance` property, so that property is now the single active-mount
boundary. Replacing the active mount does not itself dispose the previous
filesystem. Current explicit filesystem disposal is concentrated in
`DriveInfo.Dispose`, owned-drive cleanup in `ComputerFiles.Dispose`, and the
drive-filesystem factory's failed-construction cleanup. Those routes now pass
through the `FileSystem.Dispose` override.

Other applications still use the global `File` helpers and can observe the
currently active mount. Ordinary Computer Files windows also keep their
existing global/drive behavior. AMCONV2C binds typed folder windows and their
document children to a selected mount; it does not convert every legacy global
filesystem caller into a lease owner.

Filesystem implementations retain different backing resources: FAT and EXT2
can retain a `Disk`, TarFS retains its disk and archive cache, and RdskFS reads
the ramdisk's raw base pointer. The block-device abstraction exposes media
availability and I/O results, but no shared disk lease is part of this
change. A filesystem lease therefore protects the `FileSystem` object and its
normal disposal path; it does not pin removable media or prove the lifetime of
external raw storage.

No asynchronous folder enumeration or deferred filesystem operation was found
in the folder path. Filesystem calls are synchronous in the current GUI path.
The changes nevertheless keep I/O outside the mount-state lock and clone a
short operation lease around folder listings, reinspection, and associated
document reads/writes. The hosted tests are deterministic state-transition
tests; they do not claim concurrent thread-safety qualification of the UEFI
runtime or UI.

## 3. Enforced mount lifetime contract

`Kernel/FS/FileSystemMountLifetime.cs` owns the active filesystem identity,
monotonic generation, and lease accounting. Its lock protects only state
transitions and counters. No filesystem operation runs under that lock.

`File.Instance` is now a property routed through this manager. Acquisition
fails when there is no active mount, disposal has been requested, or the
generation space is exhausted. Replacement advances the generation; existing
leases remain valid against the old filesystem, while new acquisitions use
only the replacement. Generation wrap is prevented by failing closed.

`FileSystem.Dispose()` requests disposal through the manager. Disposal is
deferred while leases remain and the final release calls the base object
disposal exactly once. Lease release is idempotent, counts cannot become
negative, and a released or disposed mount cannot be reacquired. Replacing a
mount does not implicitly dispose the old one; its owner must still request
disposal when appropriate.

The guarantee applies to current production subclasses, none of which
override `FileSystem.Dispose()`. A future subclass that bypasses the base
override, an external raw-pointer owner, or a separately removed disk would
need its own coordinated lifetime contract.

## 4. APIs and folder-launch integration

The new internal APIs are `FileSystemMountLifetime.TryAcquireActive`,
`FileSystemMountLease.TryClone`, `FileSystemMountLease.IsValid`,
`FileSystemMountLease.IsCurrentMount`, and the manager's release/disposal
transitions. `LaunchRequest` can attach, transfer, clone through its owner, and
release a `FileSystemMountLease`; `WithTargetAppId` transfers that ownership
when it creates a canonical request.

`ShellFolderMountBinding` is a UI-independent production boundary. It accepts
only the typed C# `gxos.shell.root` filesystem target, acquires the active
mount before inspection, and attaches the same lease to the launch request.
Factory reinspection clones the request lease while it performs I/O, so
request termination cannot dispose the filesystem mid-call. The factory then
clones ownership for the `ComputerFiles` window and passes the exact same
filesystem and canonical relative path. Opening a folder does not assign to
`File.Instance`.

The folder window retains its lease until disposal, releases it after cached
entries are cleaned up, and takes a temporary operation lease during each
directory enumeration. If that lease can no longer be cloned, the folder
window returns no entries and does not fall back to a changed global mount.
Associated document opens clone the source lease before using the shell
service. The resulting launch context carries that lease into document
factories. Notepad reads and saves through the retained source filesystem with
short-lived operation leases.

The existing `MultiInstance` policy for Computer Files is unchanged. Each
window has independent mount ownership; closing one owner leaves other owners
usable. Folder construction and bind failures release any lease acquired so
far. The normal drive chooser and the Hard Disk/USB factory branch remain
separate and unchanged.

## 5. Typed inspection errors

`ShellFolderDirectoryProbeResult` and `ShellFolderTargetFailure` distinguish
malformed input, unavailable or invalidated mount, missing path, non-directory
entry, permission denial, I/O failure, backend unavailability, and success.
`ShellFolderMountBinding` maps these to existing launch errors:

| Inspection result | Launch result |
| --- | --- |
| Malformed path | `MalformedRequest` |
| Unsupported shell identity | `UnsupportedTarget` |
| Missing directory or existing non-directory | `ResourceUnavailable` |
| Permission denied | `PermissionDenied` |
| No mount | `ResourceUnavailable` |
| Invalid retained mount or I/O/backend failure | `BackendUnavailable` |
| Existing directory on the retained mount | Continue through the existing factory |

Diagnostics are bounded and do not include exception details. Unknown shell
identifiers remain `NotFound` through the existing shell resolver. The
unsupported Server identifiers remain `UnsupportedTarget`.

The production probe uses `FileSystem.GetFiles` and disposes each returned
`FileInfo` and list. It maps thrown exceptions to I/O failure and a null list
to backend unavailable. The current FAT, EXT2, TarFS, and RdskFS `GetFiles`
contracts can also return an empty list for missing paths or internal
conditions without exposing a typed I/O result. As a result, a real backend
error that is suppressed by an implementation may still look like “missing”;
the fake probe verifies the mapping contract but cannot repair that API limit.
Real permission-denied behavior is likewise not established by these
filesystem implementations.

## 6. Hosted coverage and qualification

The AMCONV2B hosted project now links the production
`FileSystemMountLifetime`, `ShellFolderMountBinding`, and path-contract
implementations. Its controlled filesystem test double exercises the
production lifetime manager and binding code. It does not instantiate the
kernel's custom-CoreLib `FileSystem` object or run the UEFI UI/factory stack.

The harness passed **59/59 runtime assertions**. Coverage includes canonical
path and UTF bounds, traversal rejection before I/O, missing/non-directory and
typed probe outcomes, acquire/release and double release, replacement with
existing owners, deferred disposal, independent owner leases, repeated
cycles, production binding to one selected mount, same-mount reinspection
after replacement, invalidated-request rejection without fallback, backend
failure cleanup, and unsupported Server-root rejection.

The source-contract validator passed **21/21 checks**. These checks inspect
production wiring and are not a substitute for runtime integration tests.
The harness does not launch the real `ComputerFilesApplicationFactory`, create
real windows, or execute `ApplicationInstanceRegistry` lifecycle/termination
paths. Those full application-model diagnostics remain `GUEST_PENDING`.

## 7. Build and regression results

The supported Release/AppModel compile passed with **0 errors and 135
warnings**, matching the established 135-warning baseline. No warning was
reported from the changed lifetime, binding, factory, or application files.
The retained BUILD1E inputs passed the read-only managed-artifact audit:

- `MANAGED_ARTIFACTS=41`
- `DESCRIPTOR_AGREEMENT=41/41`
- `BUILD_STAGING_AGREEMENT=41/41`
- `COMPILED_ALLOWLIST_AGREEMENT=41/41`

No identity records were regenerated or bypassed.

| Regression | Result |
| --- | --- |
| AMCONV2C mount/binding runtime harness | PASS, 59/59 assertions |
| AMCONV2B/AMCONV2C source-contract checks | PASS, 21/21 |
| AMCONV1 source checks | PASS, 9/9 |
| Phase 33 shell wire layout | PASS, 2,084-byte request and 16-byte response |
| Task Manager Memory Details hosted regression | PASS, 83 static assertions |
| UEFI desktop interaction hosted suite | PASS, 42 assertions under x86 .NET 8.0.33 |
| App Model guest lifecycle/service diagnostics | `GUEST_PENDING` |

No QEMU or QAC was used.

## 8. Server boundary

No Server mapping was enabled. `gxos.shell.this-system`,
`gxos.shell.documents`, Pictures, Music, Network, Settings, Control Panel,
Desktop, and Trash remain unsupported. The C# mount lease establishes no
Server VFS namespace or capability equivalence. AMCONV1 Files aliases and the
C# Root route remain intact.

## 9. Remaining risks and AMCONV2D scope

AMCONV2C establishes and exercises the mount manager and typed binding, but
the full production factory/window/instance lifecycle is not yet hosted. The
hosted fake models disposal state through the same manager hooks; it does not
prove that a UEFI runtime shutdown or a real filesystem implementation
behaves identically. The protected contract also relies on current filesystem
subclasses continuing to use the base `Dispose` implementation. Existing
non-folder global `File` consumers are not individually lease-bound, and
replacement leaves old mounts alive until their owners request disposal.

AMCONV2D should add a minimal typed directory-enumeration result to the
filesystem API and adapt FAT/EXT2/TarFS/RdskFS to report missing, permission,
media, and transport failures accurately. It should then add deterministic
hosted coverage around the real application factory and instance registry,
including two `MultiInstance` folder windows, window ownership, failed
construction, initialization termination, close during an injected blocked
filesystem operation, unchanged global mount identity, and final release.
The test should use production `FileSystem.Dispose` behavior where the hosted
runtime can support it, and verify the UEFI lock primitive separately. Guest
diagnostics should remain pending until run in the authorized UEFI acceptance
environment. Any Server filesystem mapping requires its own namespace and
capability proof.

## 10. Repository protection and local commit

All source, test, and documentation edits are in
`D:\dev\guideXOSUEFI_APP_MODEL`. The protected mainline, BUILD_INFRA,
MODERN_INPUT, and Server checkouts were read for baseline/compatibility checks
only; their existing status entries were left untouched. No branch switch,
merge, rebase, cherry-pick, push, or destructive cleanup of BUILD1E evidence
occurred. The AMCONV2C changes are committed locally on `APP_MODEL`.

During the read-only status checks, BUILD_INFRA's `BUILD_INFRA` branch advanced
from `6a7f8e745d9a7a1f8b7882c577715cb7797b9097` to
`a80e735b8af87ddec9d361578be003ee8ba1d015`. Its reflog records a commit at
2026-10-30 22:19 -0700 titled `P23R3 verify bounded x64 epilogue unwinding`.
This task issued no write or Git mutation command in that checkout; its
pre-existing worktree changes were not touched.
