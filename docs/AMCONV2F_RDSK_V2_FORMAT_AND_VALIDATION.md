# AMCONV2F — RDSK v2 Format and Validation

**Outcome: `AMCONV2F_RDSK_V2_PARTIAL`**

RDSK v2 now has explicit directory records, a bounded production reader, an
opt-in deterministic image generator, an integrity check, and hosted coverage
using images built by that generator. The format supports real and named empty
directories while retaining RDSK v1 parsing. The result is partial because v2
pathnames intentionally support strict ASCII only; full UTF-8/Unicode pathname
support was not added to the custom UEFI CoreLib.

## 1. Checkout and revisions

- **Checkout:** `D:\dev\guideXOSUEFI_APP_MODEL`
- **Starting branch:** `APP_MODEL`
- **Starting HEAD:** `597507e7bf48a16f5fec20ff3259ff149e598634`
- **Starting status:** only these pre-existing untracked hosted-test build
  directories were present: `Tests/AMCONV2DFatInspectionTests/bin/obj` and
  `Tests/AMCONV2ERdskInspectionTests/bin/obj`. They were preserved.
- **Starting remote relationship:** `HEAD...origin/APP_MODEL` was `0 ahead, 0
  behind`; the older note that this checkout was ahead did not apply here.
- **Ending branch:** `APP_MODEL`; no branch switch occurred.
- **Ending implementation HEAD:** `23fc86c6df1f7d26391db56d1675866c2a8f8ea1`
  (`feat: add bounded RDSK v2 images`), containing the reader, generator,
  hosted tests, and the initial version of this report.

No reset, rebase, merge, cherry-pick, remote update, or push was performed.

## 2. RDSK v1 compatibility

RDSK v1 remains the default image format. Its 12-byte header and packed file
records are unchanged:

| Offset | Width | Meaning |
| ---: | ---: | --- |
| 0 | 4 bytes | ASCII magic `RDSK` |
| 4 | u32 little-endian | Version `1` |
| 8 | u32 little-endian | File record count |
| 12 onward | variable | Repeated file records |

Each v1 record contains a u16 little-endian pathname byte count, that many
pathname bytes, a u32 little-endian payload length, then payload bytes. The
reader continues to interpret accepted v1 pathname bytes as ASCII, just as the
existing reader did. V1 directories remain implicit parents of file paths; the
root is present for a complete valid image, including a zero-file image. V1
cannot prove the existence of a named empty subdirectory.

The v1 reader now bounds every scan and payload against the image size captured
by the `RdskFS` mount, in addition to the existing format limits. It does not
require exact consumption of the v1 image, preserving compatibility with
trailing bytes. Valid v1 file lookup and synthesized directory enumeration
remain available. Unknown versions are rejected as indeterminate and are never
reparsed as v1.

The current `build.ps1` invocation still calls `ramdisk_builder.py` without a
version switch, so normal boot image construction remains v1. V2 is opt-in and
was not installed as a boot image in this phase.

## 3. RDSK v2 binary format

All integers are unsigned and little-endian. The image has no alignment or
padding requirement. The v2 header is exactly 64 bytes; the record table starts
immediately after it, and payload bytes start immediately after the table.

### Header

| Offset | Width | Field | Required value or meaning |
| ---: | ---: | --- | --- |
| 0 | 4 bytes | Magic | ASCII `RDSK` |
| 4 | u32 | Version | `2` |
| 8 | u32 | HeaderBytes | `64` |
| 12 | u32 | Flags | `0`; unknown bits are rejected |
| 16 | u64 | ImageBytes | Exact captured image length |
| 24 | u32 | RecordCount | Total records, including root; 1–4096 |
| 28 | u32 | DirectoryCount | Directory records, including root |
| 32 | u32 | FileCount | File records |
| 36 | u64 | RecordsOffset | Exactly `64` |
| 44 | u64 | RecordsBytes | Exact encoded record-table byte length; at most 4 MiB |
| 52 | u64 | DataOffset | Exactly `RecordsOffset + RecordsBytes` |
| 60 | u32 | ImageCrc32 | CRC-32 described below |

`DirectoryCount + FileCount` must equal `RecordCount`, using checked range
relationships rather than overflow-prone addition. `ImageBytes` must equal the
captured length and be no greater than 512 MiB. `DataOffset` must be within the
image, and the record table must end exactly at that offset.

### Records

Each record starts with a 28-byte fixed header followed by its name bytes.
`NameBytes` does not include a terminator.

| Record-relative offset | Width | Field | Meaning |
| ---: | ---: | --- | --- |
| 0 | u8 | Kind | `1` directory, `2` file |
| 1 | u8 | Flags | Must be `0` |
| 2 | u16 | NameBytes | `0` only for root; otherwise 1–255 |
| 4 | u32 | ParentRecordIndex | Parent directory record; root uses `0xffffffff` |
| 8 | u64 | PayloadOffset | Absolute image offset for files; zero for directories |
| 16 | u64 | PayloadBytes | File length; zero for directories; at most 100 MiB per file |
| 24 | u32 | Reserved | Must be zero |
| 28 | `NameBytes` | byte sequence | One pathname component under the parent |

Record zero is the sole root directory. It has an empty name, parent
`0xffffffff`, and zero payload offset and length. Every other record has a
nonempty name and a parent index smaller than its own record index. The parent
must be a directory. This strict earlier-parent rule rules out cycles and
forward references.

After the root, records are ordered by nondecreasing parent record index. Names
under the same parent are strictly increasing by unsigned ASCII byte order.
Equal names therefore reject duplicate records and file/directory collisions.
This order also makes generator output stable. Directories and files can be
siblings in the same name-sorted group. Full paths are reconstructed by walking
parent records back to the explicit root.

File payloads are stored in file-record order, tightly packed from `DataOffset`.
Each file's `PayloadOffset` must equal the next expected payload byte. The
reader advances that cursor by `PayloadBytes` with checked arithmetic and
requires the final cursor to equal `ImageBytes`. This disallows overlaps,
unclaimed holes, and trailing data. A zero-length file is a Kind `2` record
with a zero `PayloadBytes`; it remains distinct from a directory record.

## 4. Pathname contract

V2 uses a deliberately narrow encoding contract:

- Every stored name is strict printable 7-bit ASCII, bytes `0x20`–`0x7e`.
- A name component cannot contain `/` or `\`; `.` and `..` are rejected.
- NUL and all control bytes are rejected. Names are 1–255 bytes.
- Canonical paths use `/` as the only separator and are anchored at the
  explicit root. The mount root is represented to the inspection API by the
  empty relative path; the shell value `/` maps to that root.
- Repeated or trailing separators, traversal components, and paths longer
  than 1,023 bytes are rejected. The reader does not normalize them.
- Matching is case-sensitive using ordinal ASCII byte comparison.
- Non-ASCII names and lookups are rejected; no UTF-8 replacement or silent
  truncation occurs. Since v2 accepts ASCII only, Unicode normalization is not
  applicable. There is no full UTF-8 compatibility claim.

The image builder rejects unsupported host names and invalid components before
writing output. If a future CoreLib provides or receives a bounded strict UTF-8
decoder, Unicode can be considered as a separately tested format capability.

## 5. Integrity, limits, and failure behavior

`ImageCrc32` is the standard reflected CRC-32/ISO-HDLC checksum: polynomial
`0xedb88320`, initial value `0xffffffff`, final XOR `0xffffffff`. It is computed
over every image byte, including the header, records, and payload, with bytes
60–63 treated as zero. The generator uses the same coverage and algorithm.

This detects accidental corruption and inconsistent construction. It is not a
cryptographic hash, signature, trusted-origin claim, or protection against an
attacker who can rewrite the image and checksum. Authenticity would require a
separate signed-image capability and key-management contract.

The reader validates the complete header, record table, parent structure,
pathname bytes, payload extents, counts, exact total length, and checksum before
inspection, enumeration, or file retrieval can expose v2 contents. Malformed
images produce `Indeterminate` for typed inspection and no partial listing or
payload. `NotFound` is returned only after a complete valid image proves the
path is absent. A file at the requested directory path, or a file ancestor,
returns `NotDirectory`.

The bounds are 512 MiB per image, 4 MiB for record metadata, 4,096 total
records, 255 bytes per component, 1,023 bytes per canonical path, and 100 MiB
per file. Validation uses a bounded 12 KiB stack workspace for record kinds
and path lengths, and no untrusted count controls an unbounded allocation or
scan. `ReadAllBytes` allocates only after validation and only up to the existing
100 MiB per-file limit. The raw image pointer and length remain captured on the
mount; the current filesystem exposes no write operation for RDSK.

## 6. Reader, image builder, and boot integration

`Kernel/FS/RdskFSDirectoryInspector.cs` now validates and inspects v1 or v2.
`Kernel/FS/RdskFS.cs` dispatches validated v2 images to explicit-directory
enumeration and parent-based file lookup, while retaining the v1 implicit
directory path. `GetFiles` and `ReadAllBytes` validate against the captured
pointer and size before returning entries or bytes.

`Tools/ramdisk_builder.py` now supports `--version 2`. V2 collection sorts
ASCII names by byte order, emits explicit directory records including empty
directories, stores payloads without timestamps or host paths, checks path and
size limits, and rejects symlinks, special files, duplicate names, unsupported
names, and output paths inside the selected source tree. The writer streams
payloads to a temporary output, computes the same CRC, then atomically installs
the completed image. Identical source trees and bytes produce byte-identical
images. The default remains v1.

The production build script continues to use the default v1 path. No boot
packaging or loader switch to v2 was made. The bootloader's exact ramdisk size
continues through `Ramdisk` into each retained `RdskFS` instance.

## 7. Files changed

- `Kernel/FS/RdskFSDirectoryInspector.cs` — bounded version detection, strict
  v2 validation, CRC, and explicit directory inspection.
- `Kernel/FS/RdskFS.cs` — v1/v2 enumeration and reads using the retained image.
- `Tools/ramdisk_builder.py` — deterministic opt-in v2 image generation;
  default v1 retained.
- `Tests/AMCONV2FRdskV2Tests/AMCONV2FRdskV2Tests.csproj` — production source
  hosted compilation.
- `Tests/AMCONV2FRdskV2Tests/Program.cs` — generated-image reader, corruption,
  folder-binding, and mount-lifetime assertions.
- `Tests/AMCONV2FRdskV2Tests/run_tests.py` — creates source fixtures, invokes
  the supported generator for v1/v2 images, checks deterministic output, and
  runs the hosted production reader.
- `Tools/AMCONV2F/validate_amconv2f_source.py` — source-contract checks for
  opt-in generation, production parser wiring, and folder binding.
- `docs/AMCONV2F_RDSK_V2_FORMAT_AND_VALIDATION.md` — this report.

## 8. Hosted validation and regressions

The hosted run used Windows, .NET SDK `10.0.403`, and PowerShell 7.6.5 for the
PowerShell regression. The RDSK v2 runner produced its valid v1 and v2 images
through `Tools/ramdisk_builder.py`; the overlong-path fixture was derived from
a generated empty image and exists only as a negative malformed fixture.

| Check | Result |
| --- | --- |
| AMCONV2F production RDSK reader and typed folder binding | **58/58 assertions passed** |
| AMCONV2F generator, determinism, default-v1, and rejection checks | **12 assertions passed** |
| AMCONV2F source-contract checks | **9/9 passed** |
| AMCONV2B/C folder path, typed binding, and mount lifetime | **68/68 assertions passed** |
| AMCONV2B/C/D source-contract checks | **25/25 passed** |
| AMCONV2D production FAT inspection | **11/11 assertions passed** |
| AMCONV2E production v1 inspection and Root binding | **32/32 assertions passed** |
| AMCONV1 source regressions | **9/9 passed** |
| Phase 33 shell wire regression | **PASS**; request 2,084 bytes and response 16 bytes |
| UEFI desktop interaction suite | **42/42 assertions passed** |
| Task Manager Memory Details regression | **PASS**; ownership, bounds, unavailable data, and overflow arithmetic |

The v2 suite covers explicit root, empty and nested-empty directories,
directories with files, zero-length and nonzero files, case sensitivity,
missing paths, file-versus-directory behavior, traversal, malformed encoding,
overlong path, duplicate directories, file/directory collisions, invalid and
cyclic parents, unknown flags and versions, truncated images, corrupted header
and metadata, checksum mismatch, record-count bounds, table and data offset
overflow, invalid extents, exact image length, repeated generation, and
legacy v1 images. It also checks that malformed v2 data yields no partial
listing or successful folder bind.

The two `CA2014` analyzer warnings printed by the existing FAT hosted suite
remain at unchanged `Kernel/FS/FAT.cs` stack allocations at lines 888 and 902.
They are unrelated to the RDSK changes.

## 9. Folder activation and mount ownership

The hosted project compiles the production `RdskFS`, directory inspector,
folder path contract, `ShellFolderMountBinding`, and mount lifetime manager.
It verifies that C# Root and named empty or populated directories bind on the
verified v2 image; file and missing targets fail with the existing typed
unavailable result. A malformed image cannot produce a successful binding.
The test replaces the active ramdisk after binding and verifies factory-style
reinspection remains on the same retained mount. Three outstanding leases
delay disposal; final release disposes once. Folder activation leaves the
active global mount unchanged.

Existing Computer Files factory source checks still verify the ordinary drive
chooser, Hard Disk/USB selection, and separation from document association and
arbitrary application-name dispatch. The hosted harness does not instantiate
the actual Computer Files GUI factory/window or run
`ApplicationInstanceRegistry` lifecycle transitions. Those remain
`GUEST_PENDING`; no full GUI lifecycle acceptance is claimed.

## 10. Release/AppModel build and managed artifacts

The supported command completed:

```text
dotnet build guideXOS\guideXOS.csproj --configuration Release -target:Compile -p:UefiDiagnosticMode=AppModel
```

Result: **0 errors, 135 warnings**, matching the accepted 135-warning baseline.
There was no warning increase. The two existing FAT `CA2014` warnings are
called out above; no warning suppression or identity bypass was added.

The audit-only managed artifact checks passed both before and after the build:

| Audit | Managed artifacts | Descriptor agreement | Build/staging agreement | Compiled allowlist agreement |
| --- | ---: | ---: | ---: | ---: |
| Before build | 41 | 41/41 | 41/41 | 41/41 |
| After build | 41 | 41/41 | 41/41 | 41/41 |

The generated identity source SHA-256 remains
`D6DDF03058FA0690EF62F62FFFF016723C1514EB747FDF3D1D6AECD23CA682DD`. No
descriptor, staging record, proof executable, manifest, or identity source was
regenerated or edited to accommodate the reader change.

## 11. Unsupported cases and scope

- V2 does not support UTF-8 names, Unicode normalization, or non-ASCII lookup.
- RDSK remains read-only; create, delete, rename, permissions, and mutable
  filesystem operations are out of scope.
- The v2 boot path and image packaging have not been qualified; v1 remains the
  default boot image.
- No Server filesystem mapping was added. This System, Documents, Pictures,
  Music, Network, Settings, Control Panel, Desktop, and Trash remain
  unsupported. An RDSK `/Documents` directory does not establish Server
  `/Documents` equivalence.
- No global Server/C# application ID aliases were added.
- Actual Computer Files window and application-instance lifecycle behavior is
  still guest-only.

## 12. Guest-pending lifecycle and AMCONV2G procedure

AMCONV2F used no QEMU or QAC. For AMCONV2G, use QAC-managed acceptance
resources and an explicitly selected v2 image while keeping the normal v1
production image unchanged. Exercise these through the real guest:

1. Boot the qualified kernel with the v2 test image and confirm the loader's
   captured base and size match the complete image.
2. Open C# Root, a named empty directory, a nested empty directory, and a
   directory containing files through the real shell service, factory,
   Computer Files window, and application-instance registry.
3. Confirm file targets, missing paths, corrupted headers/checksums, and
   unsupported pathname bytes fail without opening the drive chooser or
   falling through to document association.
4. Replace the global ramdisk while a folder window holds a lease; navigate
   and read a file from that retained mount, then close and verify final
   disposal and unchanged ownership transitions.
5. Close or terminate each instance and verify the established
   `ApplicationInstanceRegistry` policy, global `File.Instance`, ordinary
   Computer Files chooser behavior, and v1 boot image behavior remain intact.

The guest result should be recorded separately from hosted acceptance. Only
after that phase should v2 packaging be considered for any normal boot path.

## 13. Repository protection and delivery

Only `D:\dev\guideXOSUEFI_APP_MODEL` was modified. Protected Server and other
development checkouts were not written. The pre-existing untracked AMCONV2D
and AMCONV2E hosted build outputs were left in place; the AMCONV2F hosted
project's generated build outputs were also left as local evidence and were
not staged. No branch switch, merge, rebase, cherry-pick, push, global SDK
change, QEMU, or QAC operation occurred.

## 14. Fresh verification rerun (2026-11-01)

The AMCONV2F implementation was already present when this verification pass
started. The actual checkout was branch `APP_MODEL`, HEAD
`23fc86c6df1f7d26391db56d1675866c2a8f8ea1`, equal to `origin/APP_MODEL`
(zero ahead, zero behind). This differs from the expected task starting HEAD
of `597507e7bf48a16f5fec20ff3259ff149e598634`; that commit is the parent of
the existing implementation commit. No reset or synchronization was used.

The only pre-existing untracked paths reported by Git status were build outputs
under `Tests/AMCONV2DFatInspectionTests/bin` and `obj`,
`Tests/AMCONV2ERdskInspectionTests/bin` and `obj`, and
`Tests/AMCONV2FRdskV2Tests/bin` and `obj`. They were preserved and not staged.

The following checks were rerun from the existing implementation commit:

| Check | Fresh result |
| --- | --- |
| AMCONV2F production reader, typed binding, and generated-image suite | 58/58 assertions passed; 0 failures |
| AMCONV2F generator determinism and rejection checks | 12 assertions passed |
| AMCONV2F source-contract checks | 9/9 passed |
| AMCONV2B/C folder binding and mount lifetime | 68/68 assertions passed |
| AMCONV2B/C/D source-contract checks | 25/25 passed |
| AMCONV2D production FAT inspection | 11/11 assertions passed |
| AMCONV2E production v1 inspection and Root binding | 32/32 assertions passed |
| AMCONV1 source regressions | 9/9 passed |
| Phase 33 managed wire validator | Passed; request 2,084 bytes and response 16 bytes |
| UEFI desktop interaction hosted suite | 42/42 assertions passed |
| Task Manager Memory Details regression | Passed |

The supported Release/AppModel compile completed with **0 errors and 135
warnings**, matching the recorded baseline. The two existing FAT `CA2014`
warnings remained at `Kernel/FS/FAT.cs` lines 888 and 902. Audit-only managed
artifact verification passed both before and after compilation: 41 artifacts,
41/41 descriptor agreements, 41/41 build/staging agreements, 41/41 ramdisk
staging agreements, and 41/41 compiled allowlist agreements. Fresh reports
were written to `out/managed-artifacts-amconv2f-rerun-prebuild.csv` and
`out/managed-artifacts-amconv2f-rerun-postbuild.csv`.

The fresh run used .NET SDK `10.0.403`. It used no QEMU or QAC. The branch,
tracked source, existing managed proof inputs, and pre-existing untracked build
outputs were preserved. No push was performed. At the end of validation, the
source and test implementation remained at
`23fc86c6df1f7d26391db56d1675866c2a8f8ea1`; this report-only verification
addendum is being committed separately.
