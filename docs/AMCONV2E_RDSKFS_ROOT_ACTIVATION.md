# AMCONV2E — RdskFS Inspection and C# Root Activation

**Outcome: `AMCONV2E_RDSKFS_PARTIAL`**

RdskFS now provides bounded typed directory inspection from a size-validated
RDSK v1 image, and the existing C# Root folder route accepts a root or
subdirectory only after that inspection proves it exists. The production
`RdskFS` class, inspection code, folder binding, and mount lease boundary pass
hosted image-fixture checks. Full application-instance, GUI window, and UEFI
guest lifecycle acceptance remains pending. RdskFS still cannot report every
backend failure distinctly, and RDSK v1 cannot represent named empty
subdirectories.

## 1. Checkout and revision

- **Working directory:** `D:\dev\guideXOSUEFI_APP_MODEL`
- **Branch:** `APP_MODEL`; no branch switch occurred.
- **Starting HEAD:** `bb805e79f827fa84ed7eb424cb269ba15d6a04fc`
- **Starting worktree:** clean.
- **Ending HEAD:** the local AMCONV2E closeout commit. Its exact SHA is
  included in the completion report because this document is part of that
  commit.
- **Remote:** nothing was pushed.

## 2. Accepted managed-artifact evidence

The existing BUILD1E provenance and generated identity source were retained.
The generated identity source SHA-256 remained
`D6DDF03058FA0690EF62F62FFFF016723C1514EB747FDF3D1D6AECD23CA682DD`.
All seven accepted phase source-input manifests were inspected; none of the
files changed for AMCONV2E are in those managed-proof input sets. The identity
generator was run in audit-only mode before and after the Release/AppModel
build; no identity source, descriptor, manifest, or staged executable was
regenerated or edited.

Both managed-artifact audits passed:

| Audit | Managed artifacts | Descriptor agreement | Build/staging agreement | Compiled allowlist |
| --- | ---: | ---: | ---: | ---: |
| Before build | 41 | 41/41 | 41/41 | 41/41 |
| After build | 41 | 41/41 | 41/41 | 41/41 |

The audit-only CSV reports are under `out/managed-artifacts-amconv2e-prebuild.csv`
and `out/managed-artifacts-amconv2e-postbuild.csv`.

## 3. RdskFS source audit

The RDSK v1 image format is a 12-byte header (`RDSK`, version, file count)
followed by file records containing a path length, path bytes, data length, and
file data. It has no directory records, total-length field, checksum, or
backend read-result channel.

Before this change, RdskFS read `Ramdisk.RawBasePointer` directly and used a
fixed 512 MiB ceiling without knowing the loaded image length. It returned an
empty list for several different cases: an absent pointer, wrong magic, zero
or excessive file count, malformed/truncated records, and a directory with no
matching children. `ReadAllBytes` similarly returned `null` for both absence
and parse/read problems. Those APIs do not support truthful typed lookup by
themselves.

The UEFI bootloader obtains the ramdisk file's `FileSize`, allocates enough
pages, verifies that the file read returned that exact byte count, then stores
the base and length in `UefiBootInfo`. `EntryPoint` already passed both values
to `Ramdisk`; `RamDiskManager` did not, and two same-image remount paths omitted
the size. Those paths now preserve and pass the exact length. Each `RdskFS`
instance captures its image pointer and length when constructed. Its inspection,
`GetFiles`, and `ReadAllBytes` calls use that captured image, so replacing the
global ramdisk pointer cannot redirect an existing leased filesystem object.

## 4. Typed inspection behavior

`RdskFSDirectoryInspector` implements the existing
`FileSystemDirectoryInspection` contract. It does not call `GetFiles`, change
the image, modify `File.Instance`, allocate a file list, or retain inspection
state. It validates the request with the shared relative-path contract, then
checks the RDSK magic, version, bounded file count, each record's path and data
length, and each range against both the trusted image size and the existing
512 MiB ceiling. The scan examines at most 10,000 records and at most 512 path
bytes per record; file contents are skipped by their checked length rather
than read.

| Result | Evidence used by RdskFS |
| --- | --- |
| `Directory` | A complete valid image proves the implicit root, including a valid zero-file image; a complete record table with a file below the requested path proves an implicit parent directory. |
| `NotDirectory` | A complete valid record table contains the requested path as a file, or contains a file at one of its path ancestors. |
| `NotFound` | The complete valid record table contains neither the directory nor a file at that path or an ancestor. |
| `InvalidPath` | The shared typed path validator rejects the request, including traversal, repeated separators, and backslashes. |
| `FilesystemUnavailable` | The retained RdskFS instance has no image pointer. |
| `Indeterminate` | The image length is unavailable, header/version is invalid, count exceeds the existing bound, a record is malformed/truncated/out of bounds, a record path is unsupported, the query is non-ASCII, or file and directory records conflict. |

RDSK v1 stores only files, so a named empty child directory has no encoding.
The current RdskFS namespace therefore reports such a path as `NotFound`; it
does not infer the directory from an empty listing. The mounted root is
intrinsic only after the header and all declared records validate.

The direct-memory backend has no typed access-denied or I/O status. A null
pointer is distinguishable, and exact size checks prevent reads beyond the
loaded image, but a hardware memory fault cannot be converted into a backend
status by this reader. No access-denied or `IoFailure` result is fabricated;
malformed or unavailable evidence remains `Indeterminate` or
`FilesystemUnavailable` as appropriate. Legacy mounts with unknown image size
cannot prove a directory and remain indeterminate.

## 5. C# Root folder activation

The existing typed route is preserved:

1. `gxos.shell.root` resolves as a C# filesystem folder target.
2. `ShellFolderMountBinding` acquires the active mount lease and converts
   canonical `/` to the empty path used for the mounted root.
3. Inspection runs on the filesystem object retained by that lease.
4. The request proceeds only for `Directory`; missing, file, invalid, and
   indeterminate outcomes keep their existing typed launch errors.
5. `ApplicationShellServices` sends the prepared request through
   `LaunchModern` to `ComputerFilesApplicationFactory`.
6. The factory re-inspects using the request lease, clones that same lease for
   the window, and constructs `ComputerFiles` with the selected initial path.

The factory's `csharpFolderRequest` branch remains before its ordinary drive
chooser branch, so a failed typed folder request does not silently open the
chooser. Ordinary Computer Files application launches retain their drive
chooser. Hard Disk and USB volume selection remain distinct. Typed folder
requests do not enter document associations or arbitrary application-name
fallback. No Server shell-object mapping was added; Server root and the other
listed virtual or panel objects remain unsupported.

The hosted tests execute the production `RdskFS` class and the production
inspection and folder-binding code against pinned synthetic RDSK images. The
host runner substitutes only the custom CoreLib `string.FromASCII` decoder
used by legacy `GetFiles`; it does not replace the typed inspector. A test
`Ramdisk` pointer holder and base filesystem shell are used because the UEFI
runtime and GUI are not hosted by this project.

## 6. Mount ownership and cleanup

The AMCONV2C lease contract and `ApplicationInstanceRegistry` were not
replaced. `RdskFS` captures its pointer and length once, and the new inspector
does not read a later `File.Instance` or `Ramdisk.RawBasePointer`. Folder
request and factory reinspection use the same leased `FileSystem` object.
Existing generation checks, window lease cloning, operation leases, and
idempotent release remain unchanged.

The RdskFS hosted fixture verified root and nested binding, replacement of the
global ramdisk pointer while the old mount remained leased, two folder leases
on one mount, a deferred disposal request, and repeated request release. A
rejected folder request returned its acquired lease. Hosted source and runtime
checks also cover stale generations and repeated releases.

The actual `ComputerFilesApplicationFactory`, window constructor and close,
application termination, partial GUI construction failures, and registry
ownership transitions were not executed in a hosted application-model runner.
Their production paths were source-audited. Those lifecycle checks remain
guest-pending; hosted path tests are not GUI acceptance.

## 7. Hosted tests and regressions

| Check | Result |
| --- | --- |
| AMCONV2E RdskFS inspection and Root binding | **32/32 runtime assertions passed** |
| AMCONV2B/C folder path and mount lifetime | **68/68 runtime assertions passed** |
| AMCONV2D production FAT inspection | **11/11 runtime assertions passed** |
| AMCONV2B source-contract validator | **25/25 passed** |
| AMCONV1 compatibility source checks | **9/9 passed** |
| Phase 33 wire regression | **PASS**; 2,084-byte request and 16-byte response |
| Task Manager Memory Details regression | **PASS**; the runner prints a summary, not an assertion count |
| UEFI desktop interaction hosted suite | **42/42 runtime assertions passed** |

The RdskFS cases cover mounted and empty roots, existing and nested implicit
directories, file and ancestor-file lookups, missing paths, traversal,
malformed and truncated images, unsupported versions, excessive counts,
conflicting file/directory records, unavailable and unknown-size images,
unchanged image bytes, and 1,000 repeated inspections with zero managed
allocations. A named empty child directory cannot be represented in RDSK v1.
No raw-memory backend I/O failure can be injected reliably.

The production Computer Files GUI and application-instance lifecycle were not
claimed as hosted tests. Application descriptors, instance transitions,
window termination, and UEFI runtime behavior still require guest acceptance.

## 8. Release/AppModel compile and warning baseline

Command:

```text
dotnet build guideXOS/guideXOS.csproj --configuration Release -target:Compile -p:UefiDiagnosticMode=AppModel
```

Result: **0 errors, 135 warnings**, matching the accepted BUILD1E and
AMCONV2D baseline of 135 warnings. No warning was emitted at a changed source
line or from `RdskFSDirectoryInspector.cs`. Diagnostics mentioning touched
files refer to pre-existing warnings on unchanged lines, including
`Ramdisk.Instance` hiding `Disk.Instance` and existing conditional-code and
legacy PNG warnings in `guideXOS/Program.cs`.

The FAT hosted project emitted its two known `CA2014` warnings at existing
`FAT.cs:888` and `FAT.cs:902` stack allocations. No new test analyzer warning
was introduced.

The generated identity source remained at the accepted hash above. The
Release/AppModel compile updated only normal build outputs; the audit-only
post-build check confirmed all 41 descriptors, stages, and compiled allowlist
identities still agree. The compiled assembly at
`guideXOS/obj/Release/net7.0/win-x64/guideXOS.dll` has SHA-256
`60C96014ED79E79188A868B5EB757C5A2551CAFA2293844E593C3DF9D4196213`.

## 9. Server/C# boundary and repository protection

No Server source or mapping changed. `gxos.shell.this-system`,
`gxos.shell.documents`, Pictures, Music, Network, Settings, Control Panel,
Desktop, and Trash remain unsupported. C# Root activation does not establish
that Server `/` maps to the same filesystem root.

Only `D:\dev\guideXOSUEFI_APP_MODEL` was written. The mainline, BUILD_INFRA,
MODERN_INPUT, and Server checkouts were inspected read-only and received no
write commands. Existing unrelated untracked validation and evidence artifacts
in those checkouts were left in place. Branch remained `APP_MODEL`; there was
no branch switch, merge, rebase, cherry-pick, or push. No QEMU or QAC was used.
Generated test `bin/` and `obj/` output was left in the APP_MODEL checkout and
was not staged as source.

## 10. Remaining limitations and AMCONV2F

AMCONV2F should add a versioned RDSK representation with an explicit image
length and integrity check, explicit directory records (including empty
directories), and a path encoding the reader can fully support. It should add
typed media/read failure reporting only where the raw-memory or loader contract
can prove the distinction. Then run guest acceptance for a real UEFI Root
launch through `ApplicationShellServices`, the Computer Files factory and
window, mount replacement, multiple windows, constructor failure, close,
termination, and exactly-once resource release. Keep Server shell mappings
unsupported until their filesystem namespace is independently established.
