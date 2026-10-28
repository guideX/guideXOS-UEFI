# UEFI BUILD1A Managed Artifact Bootstrap

## Result

**Outcome A — BUILD1A_BOOTSTRAP_VERIFIED, audit-only scope.** The managed proof dependency contract is documented, a read-only bootstrap auditor is implemented, and its deterministic synthetic tests pass. The auditor does not materialize artifacts. The real Phase 29–35 set inspected here is not a reusable verified set, and APP_MODEL still cannot compile from its current checkout state.

The work ran only in the dedicated `BUILD_INFRA` checkout. It did not build the kernel, change generated outputs, run QEMU, or modify APP_MODEL, MODERN_INPUT, or protected mainline.

## Build dependency graph

The supported full Release/AppModel entry point is:

```powershell
.\build.ps1 -UefiDiagnosticMode AppModel
```

`build.ps1` prepares the pinned runtime/ILCompiler dependencies, runs the Phase 29–35 builders and stage scripts, generates `guideXOS\obj\ManagedArtifactIdentities.g.cs`, then publishes the Release kernel. The C# project includes that generated source unconditionally. The later build flow creates the ramdisk and audits the managed files copied into it.

The relevant dependency chain is:

```text
Phase 20 pinned runtime source
  -> Phase 21 / 22 runtime transforms and evidence
  -> Phase 23 ILC patch, private compiler, and GUIDEXOS runtime pack
  -> Phase 29–35 managed proof executables and build records
  -> phase-specific GXMI descriptors and ramdisk_src/Native staging
  -> managed_artifacts.py validation and generated kernel identities
  -> Release kernel publish
  -> ramdisk image and final managed-artifact audit
```

`build.ps1` names each phase builder and stage script. The relevant phase pairs are:

| Phase | Variants | Build script | Stage script |
|---|---:|---|---|
| 29 | 5 | `Tools/Phase29/build_phase29_managed_notification.ps1` | `Tools/Phase29/stage_phase29_image.ps1` |
| 30 | 9 | `Tools/Phase30/build_phase30_managed_clipboard.ps1` | `Tools/Phase30/stage_phase30_image.ps1` |
| 31 | 5 | `Tools/Phase31/build_phase31_managed_shell.ps1` | `Tools/Phase31/stage_phase31_image.ps1` |
| 32 | 3 | `Tools/Phase32/build_phase32_managed_open_document.ps1` | `Tools/Phase32/stage_phase32_image.ps1` |
| 33 | 5 | `Tools/Phase33/build_phase33_managed_shell_action.ps1` | `Tools/Phase33/stage_phase33_image.ps1` |
| 34 | 5 | `Tools/Phase34/build_phase34_managed_resource_read.ps1` | `Tools/Phase34/stage_phase34_image.ps1` |
| 35 | 9 | `Tools/Phase35/build_phase35_managed_persistent_read.ps1` | `Tools/Phase35/stage_phase35_image.ps1` |

The set totals 41 executable/descriptor pairs. Each phase has a descriptor builder and validation code alongside its build/stage scripts. Phase 34 and 35 proof flags are declared in `Tools/Phase34/phase34_flags.py` and `Tools/Phase35/phase35_flags.py` and imported by the managed identity tool.

## Identity and provenance rules

The authority is `Tools/ManagedArtifacts/managed_artifacts.py`. It loads the seven build records from `out/dotnet`, reads the 41 `.exe`/`.gxmi` pairs under `ramdisk_src/Native`, validates them, and emits the generated C# identity table.

For every row, the generator checks:

- The record's named output exists; the record SHA-256 and byte length match it.
- The staged executable is byte-for-byte equal to the recorded final build output.
- GXMI magic, version, target, machine, header/section-table length, executable length, base flags, and phase/variant flags match the contract.
- The GXMI artifact digest at offset 84 equals the raw executable SHA-256.
- PE COFF and debug-directory timestamps are zero, so the raw hash equals the normalized hash.
- The generated C# source contains all 41 expected variants and compares all eight little-endian digest words for each one.
- The post-kernel audit sees the same 41 executable hashes in the generated identity source. The final ramdisk audit also compares each executable and descriptor to its staged source bytes.

These checks bind the runtime admission allowlist to the exact staged managed files. Matching phase names, filenames, version strings, or target labels alone do not establish identity.

The build records are JSON produced by phase builder scripts. They contain `generatedBy`, target metadata, lengths, SHA-256 values, and absolute paths to projects and outputs. The current generator follows the recorded `path`; it does not make those paths portable. A record's path, project, and source-tree history can refer to another checkout or to a user-local output cache. The standard Phase 23 build flow likewise uses `RootDir\out\rt` and local `out\dotnet` paths. The native identity generator does not authenticate a build-time source commit or sign the records, so a future external store must retain the original records and bind them to a separately approved source/tool fingerprint. The bootstrap prototype adds stricter record producer/target checks but does not claim to add cryptographic authenticity.

## Runtime dependencies

`Tools/Phase23/runtime-source.lock.json` pins the runtime source commit and Phase 21–23 patch identity. The build requires the Phase 21, 22, and 23 source-patch records, a hash-checked private `ilc.exe` record, and the GUIDEXOS runtime-pack manifest plus three NuGet packages and link shim. `Tools/Phase23/recover_source_patch_evidence.ps1` can reconstruct patch records only after checking the pinned commit, the locked 54 modified source files, five required templates, and the allowlisted generated-file set. It is not a read-only command, so BUILD1A did not invoke it against any protected checkout.

The mainline runtime cache was inspected without modification. Its `out/rt` is 2.15 GB, is at `9d5a6a9aa463d6d10b0b0ba6d5982cc82f363dc3`, and passes the recovery-lock checks for all 59 locked source/template files and the expected Git status. Its Phase 21–23 evidence records and private ILC hash validate against the APP_MODEL lock content. The mainline runtime-pack manifest and required package files are present, but the manifest records package names, versions, and absolute paths without SHA-256 values for the `.nupkg` files. Thus the runtime source and private compiler are verified against the current lock, but the full pack is not eligible as a content-addressed shared cache entry.

APP_MODEL and BUILD_INFRA have no local `out/rt`, Phase 21–23 output records, private ILC, or runtime pack. The Phase 20–23 lock JSON objects in mainline and the development checkouts compare semantically equal despite formatting differences. That makes the pinned source snapshot compatible for a **verified copy-and-recheck** path, but not a directly shared path: `build.ps1` expects branch-local `out/rt`, and Phase 23 patch/build records and package manifests contain absolute paths. No source cache or runtime pack was copied.

## Reuse boundaries

| Case | Decision |
|---|---|
| A — receiving payload bytes exactly match a candidate | Necessary, not sufficient. The complete source record, executable, GXMI, generated identity, and current native generator must validate. BUILD1A found 41/41 staged pairs byte-identical across BUILD_INFRA, mainline, APP_MODEL, and MODERN_INPUT, but mainline's build outputs do not match most of those staged executables. |
| B — receiving executable or descriptor differs | Refuse that row for reuse. Rebuild the affected proof variant (or its phase cohort when the phase builder's inputs changed), regenerate the descriptor and build record through the supported scripts, then validate the complete 41-row set and regenerate identities. The kernel identity source must be refreshed before kernel publish. |
| C — pinned runtime cache missing | Reuse requires an exact runtime commit, matching Phase 20–23 locks, the full locked patch/template evidence, private ILC hash agreement, and integrity-checked runtime-pack files. A copy into the receiving checkout followed by the existing recovery/verification path can preserve today's local build contract. A read-only shared reference cannot: the current scripts patch and build under each checkout's `out/rt`. |
| D — ordinary App Model C# source changes only | They do not change Phase 29–35 proof executable identities when the proof projects, `GuideXos.User`, phase contracts, and staging inputs are unchanged. The generated identity table can remain unchanged. However, `build.ps1` currently runs available phase builders on each full build; it has no supported skip/reuse switch, and APP_MODEL presently lacks the records needed by the generator. |
| E — independent APP_MODEL and MODERN_INPUT branches | Their staged proof pairs are exactly the same in this snapshot, but neither checkout has the 41 build records or runtime cache. The candidate mainline build contract also differs in 94 fingerprinted files from APP_MODEL. They cannot be approved to consume a common verified set from filename/hash-of-phase evidence alone. |

For a different managed payload, unchanged rows may remain candidates individually if each one retains a valid record/descriptor/identity chain. The current build scripts do not support partial cohort materialization, and the final generator still requires all 41 rows.

## Artifact store design

`E:` has approximately 5.86 TB free. The proposed `E:\bkup\artifacts\uefi-build-bootstrap` path does not exist. BUILD1A did not create it because no complete trusted Phase 29–35 set is available to seed it, and creating a store without a verified intake contract would be premature.

Recommended layout:

```text
uefi-build-bootstrap/
  objects/sha256/<digest>/                 immutable EXE, GXMI, runtime, and package bytes
  manifests/<bundle-digest>.json            original records and provenance bindings
  runtimes/<bundle-digest>/                  pinned source snapshot and Phase 21–23 evidence
  validation/<run-id>/                      disposable audit output, never authority
```

The manifest should bind the source commit, relevant build/tool input hashes, all original record hashes, all 41 executable and descriptor hashes/lengths/flags, runtime commit and patch-lock digest, private ILC digest, and runtime package digests. Intake must run the existing native identity generator and the phase-specific static validators before publishing a manifest. The store should use content-derived names, atomic no-replace publication, and read-only access for consumers. Failed or interrupted intake stays in a temporary directory and is never visible as a candidate.

Generated `ManagedArtifactIdentities.g.cs`, kernel outputs, `out/dotnet` working directories, ramdisk/ESP outputs, and validation scratch remain checkout-specific. Materialization should copy verified files into an empty isolated target; do not link writable branch paths to shared objects. A manifest/schema/tool/descriptor/flag/runtime-lock change creates a new bundle identity. Existing bundles are never overwritten. This design gives concurrent branches shared immutable source bytes while retaining independent local build outputs. It does not avoid a local runtime source copy under today's `out/rt` contract; eliminating that copy requires an explicit immutable-source/branch-overlay contract change, not a junction.

## Implemented prototype

`Tools/ManagedArtifacts/uefi_build_bootstrap.py` accepts an explicit receiving root and one or more candidate roots. It reports:

- Per-variant build record, output, staged executable, GXMI, generated identity, and native-generator status.
- Candidate and receiving byte identity, including distinct executable and descriptor comparisons.
- A hash fingerprint comparison for the build/proof source contract.
- Runtime lock, recovery evidence, private ILC, runtime-pack presence, and cross-checkout lock compatibility.
- Explicit refusal reasons and whether the complete 41-row set is a reusable candidate.

It is read-only and emits deterministic JSON to stdout. It has no `--materialize` operation and does not search arbitrary drives. Candidate roots must be supplied explicitly. The shared runtime check does not treat packages as shareable when the existing manifest has no package hashes. Future materialization must keep this report as preflight only and rerun `managed_artifacts.py` after copying.

Example:

```powershell
python -B Tools/ManagedArtifacts/uefi_build_bootstrap.py `
  --root D:\dev\guideXOSUEFI_APP_MODEL `
  --candidate-root D:\dev\guideXOSUEFI
```

## Test results

Command:

```powershell
python -B -m unittest discover -s Tests/ManagedArtifacts -p 'test_uefi_build_bootstrap.py' -v
```

Result: **13 tests passed**. Tests cover a complete matching synthetic chain, absent files, bad executable/descriptor hashes, build/stage mismatch, absent runtime dependency, unsupported GXMI version, branch-specific payload mismatch, corrupt and partial candidate sets, unsafe reuse refusal, repeat determinism, and parallel read-only audits. Synthetic executables exist only in marked temporary test fixtures; they are never written to repository build paths or treated as production evidence. The valid-fixture case also invokes the native identity generator in audit-only mode.

Real native generator checks were read-only (`--audit-only`):

- APP_MODEL: rejected because `phase29-managed-notification-build.json` is missing.
- BUILD_INFRA's generator against mainline: rejected because the Phase 29 staged executable differs from its recorded final build output.
- Mainline's own generator against mainline: rejected at the same Phase 29 build/stage mismatch.

No kernel publish, end-to-end compile, QEMU run, or QAC admission was performed. The generator failures are expected evidence of incomplete/mismatched prerequisites, not passing builds.

## APP_MODEL feasibility

The target comparison was performed read-only at APP_MODEL `9c773d55d2b8ce9203b4b8e1b08018014b8f88c7` against mainline `901112adc315bbc24a297c039d6f9880784652ba`.

- Staged bytes: all 41 EXEs and all 41 GXMI descriptors match exactly across BUILD_INFRA, mainline, APP_MODEL, and MODERN_INPUT.
- Mainline candidate records: all 41 recorded build outputs exist and agree with their recorded SHA-256/length. The 41 build output paths are absolute and point outside the mainline checkout into a user-local Phase 35Q cache.
- Build versus stage: 40/41 rows fail exact byte equality. Only Phase 34 `malformed` passes the row-level build/stage/descriptor checks. The other 40 rows must be rebuilt/re-staged to establish a complete set. Mainline's generated identity source matches its currently staged EXEs 41/41, but that does not override the build/stage failures.
- Candidate result: the native generator rejects the first row (Phase 29 success). The bootstrap reports 41/41 same staged pairs but no reusable full set because the source chain fails and 94 fingerprinted build-contract files differ.
- Runtime: the mainline runtime source/patch/private-ILC evidence validates against APP_MODEL's semantically equal locks. The pack's `.nupkg` hashes are not recorded, and the current build expects the cache under APP_MODEL's own `out/rt`; therefore it is not directly consumable as a shared reference. A future isolated copy-and-recheck can use the existing recovery contract, but it was not performed.
- Compile assessment: **APP_MODEL cannot compile from its current state.** It lacks build records, runtime source/patch outputs, Phase 23 private ILC/runtime pack, and `ManagedArtifactIdentities.g.cs`. A valid 41-row proof set plus restored Phase 23 prerequisites would be required before the Release/AppModel publish can run.

No artifacts were copied into APP_MODEL.

## Remaining prerequisites and BUILD1B recommendation

1. Add source/tool input fingerprints and a portable provenance envelope to phase build records. Preserve original records; do not silently rewrite their absolute paths during import.
2. Record SHA-256 and length for each runtime `.nupkg`, link shim, descriptor, and immutable runtime object in the runtime-pack manifest.
3. Define and review the cache manifest schema and its trust anchor before creating the shared store. A local hash-only manifest is useful for corruption detection but is not an independent authenticity signature.
4. Add an explicit audit-by-default build option and an explicit copy/materialize operation that refuses destination collisions, writes only to isolated branch-local output paths, and reruns the native generator as final authority. Do not add symlinks or junctions.
5. Add a disposable-checkout end-to-end host build using genuine verified artifacts. Keep QEMU/QAC out of this build-infrastructure qualification.
6. For runtime reuse, prove both immutable cache bytes and the per-branch patch overlay/record reconstruction path. Do not share a writable `out/rt` tree.

BUILD1B should begin with the portable manifest and package hash contract, then qualify copy-based materialization in an isolated checkout. APP_MODEL and MODERN_INPUT should remain untouched until that flow passes the native generator and hosted compile.
