# Phase 35R15 — Final Managed Persistent-Read Acceptance

**Decision: Outcome A — Phase 35 accepted.** The final managed-read, authority/isolation, allocator, service-regression, artifact, reboot, desktop-smoke, and ordinary-build gates pass. Phase 36 may be planned separately; no Phase 36 write capability was started here.

## Scope and source state

R15 completed the remaining qualification. It added bounded test coverage and evidence reporting; it adds no public storage capability. The public surface remains read-only.

- Live repository: D:\dev\guideXOSUEFI; starting HEAD cd3c1a88105b6629527c8daebf7eaf4520e2094a; branch main, upstream origin/main.
- The live root began with 15 pre-existing dirty entries. They were preserved. The only source changes transferred for R15 are the seven files listed under “R15 source changes”; all generated binaries, firmware, ramdisks, images, and build outputs stayed in the sandbox.
- Live out/rt remained at 9d5a6a9aa463d6d10b0b0ba6d5982cc82f363dc3 with its 58 pre-existing dirty entries.
- Containment lane: ordinary copy at C:\dev\guideXOSUEFI_Phase35R13_SANDBOX; no Git worktree was used. A content comparison covered 1,216 tracked source/document files. The sandbox matched the live accepted source except for the seven intentional R15 source changes. The sandbox runtime copy matched the live nested runtime at preflight. Available disk space was 303,844,265,984 bytes; prior R13/R14 evidence and disk images were retained.
- No .phase file was present at preflight.

The R15 evidence set is under C:\dev\guideXOSUEFI_Phase35R13_SANDBOX\out\phase35r15-evidence. It includes the exact preflight record, build logs and output snapshots, the corrected dual-boot transcript and serial log, the dedicated six-string census, reproducibility JSON, and disposable mutation-test copies. The first attempt to invoke the final diagnostic boot used the ordinary kernel and therefore did not execute the Phase 35 diagnostic workload; it is retained but classified unqualified. The corrected diagnostic-kernel dual-boot run is the passing result used below.

## Public SDK contract

~~~csharp
public static GuideXosResult TryReadBytes(
    string path, out byte[] value, out GuideXosStorageResultCode result)
~~~

The method is GuideXosStorage.TryReadBytes. It reads the current application's Persistent namespace. The kernel derives the calling process, owning ApplicationInstance, service context, ApplicationId, selected volume, and Persistent root. Ring 3 supplies only a bounded relative path and destination details; it cannot supply those authorities.

Bounds are 192 UTF-16 code units per relative path, 64 UTF-16 code units per segment, and 65,536 bytes per value. There is no managed size-query, Write, Delete, or Enumerate API. Results retain the typed service vocabulary, including Success, NotFound, InvalidContext, InvalidRequest, ResourceUnavailable, Unsupported, BackendFailure, and InvalidBuffer. A transport-successful NotFound is a successful GuideXosResult with typed result NotFound and an empty managed array. Local path rejection returns InvalidArgument / InvalidRequest before a PersistentRead ABI call.

Every successful read returns an independent managed array. Caller mutation cannot modify the Persistent value or a later returned array. Temporary is cleared on App Model reset; Persistent survives requester replacement, App Model reset, and reboot. Resource access stays in the separate resource service.

## Managed read and isolation gates

Evidence is from the corrected Phase35R diagnostic run, with the same disposable AHCI image used for both boots. Each boot emitted 34 managed PHASE35_MAIN_RETURN=35 results and one expected PHASE35_FAILFAST_EXIT=-1 result.

| Gate | Result |
|---|---|
| Fixture read | PASS. AppId selftest.phase10.persistent, key state.bin, 32 bytes 0x35..0x54; SHA-256 BEFA57E7EF0799D031A0188A3D0883F0F342B8F8AE90B3330652DA04ADBA739D. Managed return 35 on both boots. |
| Copy isolation | PASS. Mutate the first returned array, read again, require !ReferenceEquals(first, second), then verify the second array still contains all original bytes. Both the success and two-read proof variants contain this check. |
| Empty existing value | PASS. empty.bin returns typed Success, a zero-length array, and service end-of-value true. It is not NotFound. |
| Missing value | PASS. missing.phase35 launches and executes PersistentRead; typed NotFound, empty result, no unrelated exception, and no handle/request leak. |
| Local invalid paths | PASS. Empty, rooted, traversal, forbidden ?, colon, 193-unit path, and 65-unit segment are rejected locally as InvalidArgument / InvalidRequest without issuing PersistentRead. |
| Raw malformed records | PASS. Wrong record size, operation, path length, zero/oversized capacity, traversal path, null/invalid destination, unmapped destination, destination overflow, invalid/unmapped response destination, response overflow, and offset overflow are rejected before unsafe copying. The current raw test matrix contains 12 malformed probes. |
| Raw buffers and bounds | PASS. Exact 32-byte and short 31-byte reads preserve the existing short-read behavior (31 bytes returned, end false); exact read reports 32 and end true. Zero/unmapped/overflowing addresses are rejected and a mapped user buffer is accepted. Phase35P2 separately passes offset zero, middle, exact end, beyond end, and arithmetic-overflow checks. |
| Maximum value | PASS. Managed read validates all 65,536 pattern bytes; SHA-256 138258426EF34FA0B2F63915382FCD402BD5C25ECED993F3BDAC1D00077003D5; end-of-value true. Internal service write request of 65,537 bytes is rejected before backend invocation. |
| Cross-AppId | PASS. App B receives typed NotFound for App A's relative key; no App A data or namespace escape. |
| Stale owner/context | PASS. Stale authority is rejected before backend lookup. A fresh owner with the same AppId can read the existing value. |
| FailFast/replacement | PASS. FailFast exits -1, cleanup invalidates the old requester, replacement derives fresh context and returns 35. |
| Lifecycle | PASS. Existing authorized Running, Activated, and Inactive service states remain accepted; Suspended, Closing, terminated, and stale contexts are rejected. No lifecycle state was widened. |

The Phase35 service self-test also reports Temporary cleared and Persistent retained across App Model reset, and verifies namespace encoding, replacement, failure propagation, read-only, and unavailable-media behavior. It preserves the existing unsupported-operation results.

## Dedicated NotFound allocator census

A fresh isolated allocator-ledger run used the exact key missing.phase35. Diagnostic paths were **enabled**. The six producer IDs below were all constructed; none is represented as “intentionally not constructed.” The full requester and the isolated NotFound request each produced the same exact counts:

| Producer | Length / observed string hash | Allocations | Dispose | Accepted frees | Successful frees |
|---|---|---:|---:|---:|---:|
| 0x1FE backend ApplicationId marker | 51 / 94A661B97507E172 | 1 | 1 | 1 | 1 |
| 0x1FF backend relative-key marker | 38 / 7B75BBA2453D7996 | 1 | 1 | 1 | 1 |
| 0x200 encoded backend file-path marker | 251 / 2DB56378C1962B98 | 1 | 1 | 1 | 1 |
| 0x213 service failure diagnostic | 50 / ADE827DD03D3AE1F | 1 | 1 | 1 | 1 |
| 0x208 typed NotFound code | 1 / AF63AE4C86019E62 | 1 | 1 | 1 | 1 |
| 0x209 ABI typed-result marker | 36 / D82EC95D355533BA | 1 | 1 | 1 | 1 |
| **Per scenario total** | | **6** | **6** | **6** | **6** |

Both scenarios finished with zero retained request strings, zero dropped ledger records, zero retained request-local pages/runs, and zero invalid frees. The runner's R15 parser now requires both scenario blocks and checks each allocation identity plus exactly one Dispose and accepted free. No allocating serial-diagnostic concatenation was reintroduced.

## Phase 8–11 service regression controls

All suites below ran freshly in the corrected dual-boot Phase35R guest. Counts are per boot. The registry's Phase 8 successful path has 26 top-level Check assertions; its single success marker is emitted only when the full self-test returns true. Phase 9 retains three explicit service markers plus its contract and request-session checks inside that same registry self-test.

| Suite | Passed | Failed | Coverage/result |
|---|---:|---:|---|
| Phase 8 service registry | 26 | 0 | Registration/lookup, notification, settings, system information, generation/context validation and invalidation, lifecycle authorization, cleanup. APP_MODEL_SERVICES_SELFTEST_OK=1 on both boots. |
| Phase 9 asynchronous services | 5/5 top-level groups; 3/3 explicit markers | 0 | Dialogs, Open/Save file dialog where supported, Shell launch/object paths, Begin/Observe/Cancel request-session lifecycle, bounded/stale handles, and cleanup. Dialog, file-service, and Shell markers each equal 1 on both boots. Unsupported results remain unsupported. |
| Phase 10 resources/storage | 6/6 registry markers and 12/12 P2 operation markers per boot; reboot-delete 1/1 on boot 2 | 0 required failures | Resource metadata/chunk reads, path confinement, Persistent availability, AppId scope, reset, namespace encoding, cross-scope, stale context, read/write lifecycle, value and offset boundaries, delete, failure propagation, enumerate, Temporary/Persistent separation, and Temporary reset. Boot 1 reboot-delete is the expected pre-verification state; boot 2 passes. Read-only and unavailable media return their existing typed results. |
| Phase 11 clipboard | 5/5 | 0 | Contract, Set/Get/Clear self-test, copy isolation, generation, source identity/lifecycle, empty/invalid input, reset, and stale-context rejection. All five clipboard markers equal 1 on both boots. |

## Earlier managed controls and startup

Phase 26–34 results are reused from the fresh R14 controls because R15 changed only Phase35 proof/harness and artifact-audit files; the ABI changes are gated to Phase35 proof variants. Those controls were fresh individual QEMU runs at 1280 MiB and emitted exact completion markers with no vector 3 or App Model failure:

| Phase | Expected return |
|---|---:|
| 26 | 42 |
| 27 | 27 |
| 28 | 28 |
| 29 | 29 |
| 30 | 30 |
| 31 | 31 |
| 32 | 32 |
| 33 | 33 |
| 34 | 34 |

The R14 evidence records the per-phase serial-log names and kernel hashes. The R15 ordinary build rebuilt the full 41-artifact registry. App Model Compatibility passes with zero Legacy backend calls. Lifecycle is 15/15; taskbar grouping is 16/16 with no stale projection. Both boots initialize widgets, Start/taskbar, and notifications without an unexpected breakpoint or vector 3. The accepted bounded DSDT mapping controls at 1024 MiB and 1280 MiB remain valid; R15 did not change bootloader or ACPI code. The normal desktop smoke remains healthy; no cursor-artwork or icon-interaction work was attempted.

## Managed artifact identity, reproducibility, and mutation rejection

The live registry contains 41 managed artifacts. The final ordinary build reports 41/41 agreement across canonical executable, GXMI descriptor, generated identity, compiled allowlist, build staging, and ramdisk staging. No identity was manually repaired.

Reproducibility was compared across all 41 entries using two coherent pairs: the 32 unchanged Phase29–34 entries from ordinary builds #1 and #2, and all nine current Phase35 entries from the focused nine-mode cohort build and ordinary build #3, built from the same final Phase35 source hashes. Results:

| Comparison | Agreement |
|---|---:|
| Raw executable bytes | 41/41 |
| Canonical PE bytes | 41/41 |
| Descriptor file and embedded executable identity | 41/41 |
| Compiled admission identity | 41/41 |
| Build/staged executable | 41/41 |
| Relevant PE sections | 41/41 |
| CodeView, checksum, COFF/debug timestamps | 41/41 |

Raw EFI payload bytes matched for all 41. Thus canonical reproducibility and byte-for-byte reproducibility both pass; no metadata-only difference was observed in this pair. The per-artifact first/second raw and canonical hashes, descriptor digests, sections, CodeView, checksum, and timestamps are in out\phase35r15-evidence\managed-artifact-reproducibility-r15-final.json.

Disposable copies accepted the baseline and rejected each mutation: executable byte (ARTIFACT_HASH), descriptor digest (ARTIFACT_HASH), wrong proof flags/mode (PHASE35_MODE_FLAGS), incorrect admitted identity (compiled identity mismatch), and stale executable/descriptor pairing (CONTRACT_FLAGS_OR_SIZE). Protected accepted artifacts were not mutated. Evidence is in out\phase35r15-evidence\mutation-rejection-r15-final.json.

## Stress, persistence, reboot, and smoke results

The corrected two-boot run reused one disposable 16 MiB FAT16 AHCI image with the same disk identity. Boot 1 seeded only the initial fixture (seedWrites=1). After orderly QMP reboot, boot 2 reported seedWrites=0, verified the unchanged 32-byte fixture SHA above, returned 35 from a fresh requester, and passed PHASE35P2_REBOOT_DELETE=PASS. Boot 1's reboot-delete precondition marker is expected to be FAIL before the verification boot; boot 2 is the acceptance result.

Each boot passes 25/25 normal requester lifetimes (all return 35), allocator-stable, zero retained request-local objects, zero active Persistent requests/handles, 8 MiB contiguous allocation, zero invalid/corrupt/no-page frees, and no unexpected PF/GP/UD/BP/vector-3/ABI panic. The diagnostic stress window reports 175 read attempts and 150 successful reads per boot. The isolated allocator census is recorded separately above.

## Final ordinary build

The contained .\build.ps1 run completed with Build Complete!, zero build errors, a compiled kernel, generated ramdisk, and staged EFI. The 41-entry artifact agreement passed. The existing warning set persists: 194 compiler/linker warning records and 49 ILCompiler warning : records; no fatal diagnostics occurred; the known warning set was not made an acceptance blocker. Ordinary build #3 artifact hashes and the full warning log are preserved in the evidence directory. A subsequent kernel-only diagnostic build was used for QEMU; it did not replace the archived ordinary build outputs used for the final full-build result.

## R15 source changes

Only these source files contain the reviewed R15 qualification changes:

- GuideXos.User/GuideXosInternalAbi.cs — adds raw malformed destination/response/offset/capacity probes.
- Kernel/Misc/Ring3ProofPhase35.cs — updates expected Phase35 service-read counts for the maximum-value check.
- UserManagedPersistentStorageProof/Program.cs — asserts distinct returned arrays, adds colon rejection, and reads/verifies the maximum value.
- Scripts/run_phase35q_storage_validation.ps1 — tightens Phase35 failure checks and validates both six-string scenarios.
- Tools/ManagedArtifacts/managed_artifacts.py — verifies all eight compiled digest words against generated identities.
- guideXOS/OS/ApplicationServiceRegistry.cs — records the failing boundary-contract step for diagnostics.
- guideXOS/Program.cs — emits granular Phase 10 failure diagnostics.

The acceptance report is the only documentation change. No binaries, generated identity tables, fixture images, ramdisk, EFI, or nested runtime files are transferred from the sandbox.

## Acceptance and deferred capabilities

All Phase35R15 gates are PASS. **Phase 35 is Outcome A — accepted.** Managed app-local Persistent storage is qualified for bounded reads with kernel-derived authority, AppId scope, independent copies, typed errors, lifecycle enforcement, reset/reboot persistence, and allocator stability. Managed Write, Delete, and Enumerate remain unsupported by this public API; no Phase 36 storage-write capability is included. Existing unsupported Phase 9 operations remain unsupported. Cursor artwork, desktop icon hit-testing, clicking, dragging, and position persistence remain deferred to the separate Desktop Polish P3 phase.
