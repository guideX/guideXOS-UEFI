# AMCONV2D — Filesystem Inspection and Folder Lifecycle

**Outcome: `AMCONV2D_PARTIAL`**

The folder launch path now consumes a typed, non-mutating directory inspection
contract. FAT can report directory, missing path, non-directory, unavailable
media, invalid input, and disk I/O outcomes from its typed path resolver;
AutoFS delegates to the exact selected implementation. Legacy backends that
cannot distinguish a missing path from a suppressed failure return
`Indeterminate`. The folder activation and mount-lifetime boundary passed
hosted tests, but the real FAT adapter and full application factory/window
lifecycle were not executed in a hosted runtime. The active boot-time RdskFS
therefore fails closed for typed folder activation until it can provide a
bounded, trustworthy inspection result.

## 1. Checkout and HEAD

- **Working directory:** `D:\dev\guideXOSUEFI_APP_MODEL`
- **Branch:** `APP_MODEL`; no branch switch occurred.
- **Starting HEAD:** `8f9510bddb9ad1c95f2636aae9f33c18fe71d2d8`
- **Starting worktree:** clean; branch was seven commits ahead of
  `origin/APP_MODEL`.
- **Ending HEAD:** this AMCONV2D commit. Its exact SHA is reported with the
  completion message because the documentation is part of that commit.

## 2. Files changed

- `Kernel/FS/FileSystemDirectoryInspection.cs` — typed result and bounded
  relative-path validation.
- `Kernel/FS/FileSystem.cs` — conservative internal inspection default;
  `GetFiles` is unchanged.
- `Kernel/FS/FAT.cs` — typed inspection through the existing FAT path resolver
  and disk status.
- `Kernel/FS/AutoFS.cs` — delegates inspection to its selected filesystem.
- `Kernel/Kernel.projitems` — includes the new production source file.
- `guideXOS/OS/ShellFolderTargetPath.cs` and
  `guideXOS/OS/ShellFolderMountBinding.cs` — map typed inspection outcomes
  into the existing folder activation error vocabulary.
- `Tests/AMCONV2BFolderTargetTests/AMCONV2BFolderTargetTests.csproj`,
  `MountLifetimeTestStubs.cs`, and `Program.cs` — exercise the production
  mount binding and result translation with a controlled filesystem.
- `Tests/AMCONV2DFatInspectionTests/AMCONV2DFatInspectionTests.csproj`,
  `FatHostedStubs.cs`, and `Program.cs` — execute the production FAT driver and
  block-device adapter against a bounded synthetic FAT12 image.
- `Tools/AMCONV2B/validate_amconv2b_source.py` — extend source-contract checks
  for the typed inspection path and backend delegation.
- `docs/AMCONV2D_FILESYSTEM_INSPECTION_AND_LIFECYCLE.md` — this report.

## 3. Existing backend behavior

`File.GetFiles` delegates to the active `File.Instance`; each backend keeps
its existing `GetFiles` contract for current callers. Several implementations
return an empty list for both absence and failure, so the old folder probe
could not infer that an empty result meant “missing.”

| Backend | Existing inspection behavior and source of result |
| --- | --- |
| FAT | `GetFiles` returns an empty list when `FindPath` does not find a directory and does not check the iterator's final disk status. Separately, `TryGetFiles` and the FAT path resolver retain not-found and `DiskIoResult` outcomes. |
| AutoFS | Selects RdskFS, TarFS, FAT, or EXT2 and delegates `GetFiles`. The wrapper itself has no additional path/error information. |
| RdskFS | Reconstructs directories from packed file paths. Null/invalid raw data, invalid counts, malformed/truncated records, and no matching child can all yield an empty list. It reads a raw pointer with no associated length available to this adapter. |
| EXT2 | `ResolvePathToInode` returns zero for missing paths and non-directory ancestors; `GetFiles` then returns an empty list. Legacy `Disk.Read` results are ignored during its path and inode reads. |
| TarFS | `GetFiles` filters an in-memory cache. The parameterless mount skips cache initialization; cache construction can also stop on missing media, allocation failure, malformed archive data, or the fixed entry bound without a typed status. |
| CloudFS | HTTP errors, null responses, non-success status codes, and JSON parse failures become an empty list. `GetFiles` does not preserve enough status to classify them. |
| MiniRamFs | Supports only root enumeration and returns an empty list for other paths; the legacy disk read path has no typed inspection result. |
| NTFS / FAT32 source | NTFS is commented out, and `FAT32.cs` is an old commented implementation; neither is a live filesystem implementation in this build. |

The C# boot path currently assigns `new RdskFS()` to `File.Instance`. A name
or empty list from that backend is not adequate evidence for a typed folder
launch.

## 4. Typed inspection contract

`FileSystemDirectoryInspection` carries a status and an optional numeric
backend code. The internal statuses distinguish:

- `Directory`
- `NotFound`
- `NotDirectory`
- `AccessDenied`
- `FilesystemUnavailable` and `MountUnavailable`
- `MountInvalidated`
- `IoFailure`
- `InvalidPath`
- `Indeterminate`

The base implementation validates the bounded relative path and otherwise
returns `Indeterminate`. It does not inspect by calling `GetFiles`, because
that could turn an empty result into a false missing-path claim. Backends
without adequate evidence inherit this conservative behavior.

The FAT override validates the path before doing I/O, calls `FindPath` on the
same FAT object, and checks `_stickyIoResult` after the read. It does not
enumerate directory contents, create directories, modify `File.Instance`, or
change `GetFiles`. FAT `DiskIoResult` does not include access denial, so FAT
does not claim that distinction. Unsupported or unrecognized mount formats
remain indeterminate. `AutoFS` delegates to the exact selected `_impl` and
returns indeterminate if it has no implementation.

The numeric backend code is carried only by the internal typed result. Raw
exception text is caught at the binding boundary and is never copied into an
application error.

## 5. Application error translation

| Inspection outcome | Existing application-model result |
| --- | --- |
| Existing directory | Continue through `LaunchRequest.ForShellObject` and the existing Computer Files factory. |
| Missing path or existing file | `ResourceUnavailable` |
| Invalid path | `MalformedRequest` |
| Access denied | `PermissionDenied` |
| Filesystem/media or active mount unavailable | `ResourceUnavailable` |
| Invalidated retained mount, I/O failure, unavailable backend, or indeterminate result | `BackendUnavailable` |

Unexpected backend exceptions translate to indeterminate failure; exception
details are not surfaced to applications.

## 6. Backend coverage and remaining indeterminate outcomes

- **Typed and implemented:** FAT path lookup can report directory, not-found,
  non-directory, invalid path, media unavailable, and disk I/O failure when
  its typed disk interface provides that evidence. The production FAT and Disk
  sources were exercised in the hosted synthetic-image test.
- **Delegated:** AutoFS preserves the result of the selected implementation;
  FAT-backed AutoFS receives the FAT distinctions above.
- **Representable but not reported by current adapters:** access denial,
  mount invalidation during backend inspection, and mount unavailability.
  The mount lease manager handles invalidated request leases before backend
  I/O; the backend result type keeps these statuses available for future
  adapters.
- **Conservative `Indeterminate`:** RdskFS, EXT2, TarFS, CloudFS, and MiniRamFs.
  Their current inspection paths lack a bounded complete scan or typed I/O
  metadata. An indeterminate result is not translated to `NotFound`.

This is selected backend coverage, not qualification of every filesystem
driver. In particular, the hosted harness does not execute the production FAT
class against a test disk.

## 7. Folder activation integration

The existing typed route remains in use: `LaunchRequest.ForShellObject`,
`LaunchResult`, `ApplicationShellResult`, `ApplicationFactoryRegistry`,
`ApplicationInstanceRegistry`, and the existing Computer Files factory.

For a supported C# Root request, `ShellFolderMountBinding` first validates the
typed shell identity, acquires the active mount lease, validates the canonical
folder path, and inspects through that lease's `FileSystem`. A rejected
inspection disposes the acquisition before returning. Successful inspection
attaches the lease to the request. Factory reinspection clones that same
lease, so replacing `File.Instance` during synchronous inspection cannot
redirect the request to a new filesystem.

The Computer Files factory clones the request lease for the window before
construction. Construction failure releases the outstanding window lease in
the factory catch path. Once constructed, `ComputerFiles.Dispose` clears cached
entries before releasing its mount lease. Its operation lease protects each
synchronous listing. Instance-owned request disposal releases the request
lease on launch failure or application termination. These cleanup paths were
reviewed in production source; actual factory/window/registry execution was
not hosted in this phase.

The C# Root folder branch remains separate from ordinary Computer Files
application launch, the existing drive chooser, Hard Disk and USB selection,
and file-association dispatch. Folder targets do not fall through to a file
association or arbitrary application-name path. No second registry or launch
pipeline was added.

## 8. Lease ownership findings

- Leases retain the exact filesystem instance selected before inspection.
- Replacement changes the active mount for new requests and leaves existing
  leases usable until their owners release them.
- Synchronous filesystem I/O occurs outside the mount-state lock while a
  cloned operation lease keeps the resource alive.
- Inspection rejection releases the not-yet-attached acquisition.
- Request, factory-window, operation, and document-child leases have separate
  owners; release is idempotent in the existing lease manager.
- Hosted tests verify independent leases, delayed disposal, repeated release,
  replacement, invalidated-request rejection, and repeated acquire/replace/
  release cycles.
- Factory failure, actual window-constructor failure, and application
  termination ownership were source-audited but not behaviorally exercised in
  a hosted application-model runner. These remain guest-pending lifecycle
  checks.

## 9. Hosted behavioral tests

`Tests/AMCONV2BFolderTargetTests` passed **68/68 runtime assertions**. It links
the production path validator, shell folder binding, typed result contract,
and mount-lifetime manager. Its controlled filesystem injects typed backend
outcomes; it does not instantiate RdskFS, the production application factory,
a GUI window, or the application instance registry.

`Tests/AMCONV2DFatInspectionTests` passed **11/11 runtime assertions**. It
compiles the production `FAT.cs`, `Disk.cs`, and inspection contract against a
minimal hosted `FileSystem` base stub, then mounts a bounded synthetic FAT12
image. This verifies the real FAT path resolver's directory, file,
not-found, invalid-path, media-unavailable, transport-failure, unsupported
read, and unsupported-format behavior. It does not exercise an actual disk
transport or removable-media lifecycle.

The 79 assertions across both projects cover canonical/boundary/malformed input, directory,
missing, file-as-folder, permission, media/backend unavailable, I/O,
indeterminate and invalid-path mappings, unsupported Server root, active-root
binding against the selected fake mount, mount replacement, reinspection on
the retained mount, invalidated request rejection, independent leases,
deferred disposal, repeated release, and repeated lifecycle cycles. Every
failure path in the binding harness checks that its acquired lease count
returns to zero. The production FAT test emitted two `CA2014` analyzer
warnings in the existing directory-entry routines at `FAT.cs:888` and
`FAT.cs:902`; those `stackalloc` sites were not changed by AMCONV2D.

No behavioral assertion is counted for source-text inspection. AutoFS
delegation and the other legacy backend limitations are compile- and
source-reviewed; only FAT has production backend runtime coverage.

## 10. Source and compatibility checks

| Check | Result |
| --- | --- |
| AMCONV2B/2C source-contract validator, including new inspection checks | PASS, 25/25 |
| AMCONV1 source regressions | PASS, 9/9 |
| Phase 33 shell wire validator | PASS; request 2,084 bytes, response 16 bytes |
| Desktop interaction hosted suite | PASS, 42 assertions |
| Task Manager Memory Details regression | PASS; existing qualification records 83 static assertions (the script emits a summary rather than a per-assertion count) |

The source checks preserve the AMCONV1 Files aliases, ordinary launch and
file-association paths, the existing drive chooser and Hard Disk/USB paths,
and the Phase 33 wire contract.

## 11. Artifact audit and Release/AppModel compile

The read-only managed artifact audit passed after compilation:

- `MANAGED_ARTIFACTS=41`
- `DESCRIPTOR_AGREEMENT=41/41`
- `BUILD_STAGING_AGREEMENT=41/41`
- `COMPILED_ALLOWLIST_AGREEMENT=41/41`

The supported command completed successfully:

```text
dotnet build guideXOS/guideXOS.csproj --configuration Release -target:Compile -p:UefiDiagnosticMode=AppModel
```

Result: **0 errors, 135 warnings**, matching the prior compiler baseline of
135 warnings. No warnings were reported from the changed filesystem
inspection, binding, or test files. Existing warning causes include net7.0
end-of-support diagnostics, nullable annotations outside nullable contexts,
obsolete legacy graphics APIs, equality operators without matching overrides,
unused legacy fields, unreachable conditional code, stackalloc analyzer
warnings, and the custom freestanding metadata setup. No managed identity or
artifact evidence was regenerated; the audit confirmed the retained evidence
still agrees.

## 12. Guest-only verification gaps

The production Computer Files factory, window construction/close lifecycle,
application instance transitions, and actual UEFI lock/runtime behavior were
not executed in the hosted runner. Existing UEFI lifecycle and service
diagnostics remain **`GUEST_PENDING`**.

The boot-time active C# filesystem is RdskFS. Since RdskFS inherits the
conservative indeterminate result, typed activation against that production
mount returns `BackendUnavailable` rather than claiming that the root exists.
The hosted “root activation” assertion uses a controlled test mount and does
not establish a successful guest root activation.

## 13. Server compatibility and repository protection

No Server mapping was added. These remain unsupported:

- `gxos.shell.this-system`, `gxos.shell.documents`, Pictures, Music, Network
- Settings, Control Panel, Desktop, and Trash

Server `/` is not assumed to equal C# `/`. No Server folders are created and
no Server/C# application IDs are globally aliased.

All writes, builds, and generated test outputs were confined to
`D:\dev\guideXOSUEFI_APP_MODEL`. Status snapshots of the protected mainline,
BUILD_INFRA, MODERN_INPUT, and Server checkouts showed only their previously
retained or unrelated untracked artifacts; this task issued no write command
there. No branch switch, merge, rebase, cherry-pick, destructive cleanup,
global SDK change, QEMU, or QAC operation occurred. Nothing was pushed.

## 14. Recommended AMCONV2E scope

1. Add a bounded RDSK length and integrity contract or another source-owned
   way to prove a complete scan before reporting directory or not-found.
2. Add typed inspection adapters for EXT2, TarFS, CloudFS, and MiniRamFs only
   when their readers preserve sufficient path, media, and backend status.
3. Extend the production FAT hosted fixture with malformed directory records,
   bounded directory chains, and nested-path cases; add an AutoFS dispatch
   fixture where practical.
4. Add a hosted seam for the production Computer Files factory and instance
   registry, then verify two folder windows, factory and constructor failures,
   window close, termination, and exactly-once lease release.
5. Run the existing guest-only lifecycle diagnostics in the authorized UEFI
   acceptance environment; continue to keep Server mappings unsupported.
