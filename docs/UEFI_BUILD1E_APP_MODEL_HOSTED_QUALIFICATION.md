# UEFI BUILD1E APP_MODEL Hosted Qualification

**Date:** 2026-10-29  
**Outcome:** `BUILD1E_APP_MODEL_HOSTED_ACCEPTED`  
**Branch:** `APP_MODEL`  
**Starting HEAD:** `9c773d55d2b8ce9203b4b8e1b08018014b8f88c7`  
**Qualified source HEAD:** `9109f7a967a70369bb030dc49033af20ff50065b` (source/tooling commit; the report-only commit follows it).

BUILD1E reconstructed the APP_MODEL branch's own Phase 29–35 managed proof
artifacts, generated and audited the 41-entry identity source, and completed
the supported Release/AppModel project compile. All runnable hosted
regressions passed. App Model lifecycle and service self-tests that run only
from the UEFI diagnostic path remain `GUEST_PENDING`; guest execution was out
of scope.

## Runtime and toolchain restoration

The pinned runtime checkout was restored at `out/rt` from commit
`9d5a6a9aa463d6d10b0b0ba6d5982cc82f363dc3`. Phase 20–23 source and patch
verification passed. The branch-local Phase 19 runtime build, private ILC,
and Phase 23 runtime pack were produced and verified before Phase 29.

| Input | Verified identity |
| --- | --- |
| Runtime source | `out/rt`, commit `9d5a6a9aa463d6d10b0b0ba6d5982cc82f363dc3`; its pinned SDK reports `9.0.100-rc.2.24474.11` |
| Private ILC | target `guidexos-x64`; patch `guidexos-ilc-target-v1`; `ilc.exe` SHA-256 `38BF90172619E78B8FBC20B8620B3F3B8059D1C1CA1D7DAED7984424157F9817` |
| Phase 23 runtime pack | manifest SHA-256 `0DCB73786DA532CAE9D17341DF0FA644EE01BBE41BF0BC8E63A084C31E4792F4`; package SHA-256 `20E5CB565B6560231B5C462A3593FA1FBE145D0D03B5BC7BA83845FCB1F1649E`; target `guidexos-x64` |
| Managed proof SDK | `C:\Users\guideX\.dotnet\dotnet.exe`, SDK `10.0.401`, Roslyn `5.9.0-1.26423.113` |
| Native toolchain | MSVC x64 `14.51.36260.0`; Windows SDK `10.0.26100.0` |

The Phase 23 package manifest marks the package's CoreLib adaptation as
incomplete by design. The supported Phase 26 builder built the
`TARGET_GUIDEXOS` CoreLib adaptation for each proof build and recorded its
assembly hash. Proof build records target `guidexos-x64`; the forbidden
`win-x64` fallback was not used. The private host compiler inputs and the
target runtime package remain separately identified.

The generated runtime build files refer to the protected mainline runtime
checkout for 53 patched source files. All 53 files were SHA-256 compared with
the APP_MODEL `out/rt` copies and matched byte-for-byte; no protected checkout
was written. The managed proof projects, their build records, and executable
outputs all resolve to APP_MODEL. The Phase 19 object hash differed from the
infrastructure build after checkout-path changes, while its tracked source
inputs matched; the subsequent Phase 23 package verifier passed.

`Tools/Phase23/recover_source_patch_evidence.ps1` received two verifier fixes:
it normalizes CRLF checkout copies of locked text templates to canonical LF
before template comparison, and handles an empty PowerShell `Get-Item.Target`
value safely. Runtime source files continue to be checked against their
original locked byte hashes.

## Phase 29–35 reconstruction

The supported phase builders and stagers ran in dependency order. Phase 29–31
build outputs and stage roots were under the BUILD1E E: evidence directory;
Phase 32–35 used their required APP_MODEL `out/dotnet` build roots and E:
stage roots. The only records copied into `out/dotnet` for Phase 29–31 were
the branch-generated build JSON files required by the existing identity
generator; each copy was checked against the original SHA-256. No BUILD_INFRA
executable, GXMI, or identity record was used as APP_MODEL output.

| Phase | Variants | Staging | Wire contract |
| --- | ---: | --- | --- |
| 29 | 5 | PASS | Notification proof records and descriptors accepted |
| 30 | 9 | PASS | Request 32 bytes; set 131096 bytes; response 131296 bytes; clear 20 bytes |
| 31 | 5 | PASS | Shell launch request 2084 bytes; response 16 bytes |
| 32 | 3 | PASS | Shell launch request 2084 bytes; response 16 bytes |
| 33 | 5 | PASS | Shell launch request 2084 bytes; response 16 bytes |
| 34 | 5 | PASS | Resource read request 152 bytes; response 44 bytes |
| 35 | 9 | PASS | Persistent read request 436 bytes; response 24 bytes |
| **Total** | **41** | **PASS** | **All phase checks passed** |

Each variant has a branch-local build record, executable hash, generated GXMI
descriptor, and stage comparison. Per-phase checkpoint files independently
preserve the record and stage hashes. The Phase 29 checkpoint was rechecked
after later phases and remained unchanged.

Per-variant provenance is in
`E:\guideXOSUEFI_APP_MODEL_BUILD1E\provenance\variant-provenance.json` and
`variant-provenance.csv`. It records the APP_MODEL source revision, source
input fingerprint, source-file hashes, project and variant, runtime identity,
toolchain, build record hashes, build/stage hashes, descriptor hashes, and
phase log path. The seven phase fingerprints cover 44–46 tracked source and
build-input files per phase. Authentic APP_MODEL build records are retained
under `provenance\build-records`.

The Phase 32–35 build scripts reclaim their intermediate per-variant adapted
runtime-pack directories. The provenance manifest marks those 22 local
manifest copies unavailable after the prescribed cleanup. Their accepted
build records retain the Phase 23 runtime identity and per-variant adapted
CoreLib hash; the common Phase 23 manifest/package hashes and private ILC
record are retained. This cleanup did not remove any proof executable,
descriptor, build record, stage output, or phase checkpoint.

## Native identity generation

The unmodified native generator accepted all 41 APP_MODEL build/stage pairs:

```text
python -B Tools\ManagedArtifacts\managed_artifacts.py --root D:\dev\guideXOSUEFI_APP_MODEL --ramdisk-source E:\guideXOSUEFI_APP_MODEL_BUILD1E\runs\managed-identity-input\Native --identity-source D:\dev\guideXOSUEFI_APP_MODEL\guideXOS\obj\ManagedArtifactIdentities.g.cs --report-csv E:\guideXOSUEFI_APP_MODEL_BUILD1E\reports\managed-artifacts-generated.csv --stage generated
```

Results: `MANAGED_ARTIFACTS=41`, descriptor agreement `41/41`, and build/stage
agreement `41/41`. The generated identity source SHA-256 is
`D6DDF03058FA0690EF62F62FFFF016723C1514EB747FDF3D1D6AECD23CA682DD`. A
second generation produced byte-identical identity source and CSV; the CSV
SHA-256 was
`204F254E0CFAB041F1E36577515D760FF851C08EF39A04188D998FB7ED0E6834` on both
runs.

After the hosted compile, the native generator's audit-only compiled-allowlist
check passed: `COMPILED_ALLOWLIST_AGREEMENT=41/41`. The complete generated
audit is retained as `reports\managed-artifacts-after-compile.csv`.

All 41 executable and all 41 GXMI descriptor-file hashes equal their
BUILD1D reference hashes. This equality was observed after independent
APP_MODEL builds; it was not used as a source of APP_MODEL build or identity
records. The comparison is retained in
`reports\build1e-vs-build1d-identities.csv`.

## Release/AppModel compile

The supported project command was run after identity generation:

```text
dotnet build guideXOS/guideXOS.csproj --configuration Release -target:Compile -p:UefiDiagnosticMode=AppModel
```

The command used SDK `10.0.401` and Roslyn `5.9.0-1.26423.113`. It compiled
the real `guideXOS.csproj` project and its integrated application model,
instance/lifecycle, service registry, factory, and compatibility adapter
sources. Final result: **0 errors, 135 warnings**. The compiled assembly
`guideXOS/obj/Release/net7.0/win-x64/guideXOS.dll` has SHA-256
`139E627A5ECEE5EED53F7119DF75CB8FCB5A5A460A5CF838707F9F125DD1E705` in the
final compile after the source/tooling commit. The generated identity source
and compiled assembly/PDB are preserved under `runs\hosted-compile-final`;
their source/output hashes are in `artifact-identities.json`.

The first compile after identity generation reached C# and found that the
overlong-input self-test used `new string(char, int)`, which the custom
freestanding CoreLib does not provide. The smallest correction constructs the
same bounded test input through the supported `char[]` constructor in
`ModernAppModel.cs`. The Release/AppModel compile passed after that correction.

This project and `build.ps1` expose the UEFI build path and diagnostic selector;
there is no separate Legacy configuration or Legacy project selector to
compile. The UEFI AppModel compile includes the existing legacy compatibility
adapters.

## AMCONV1 hosted regressions

The nine repeatable source-contract checks pass. They cover the Server Files
record and aliases, C# identities, the request route, alias preservation and
repeat-resolution stability, bounded error codes, typed-action routing, all
six file associations, App Model diagnostic wiring, and the same-instance
taskbar lifecycle invariant. The runner is
`Tools/AMCONV1/validate_amconv1_source.py`; its checks inspect production
sources and do not claim execution of guest self-tests.

| Regression | Result |
| --- | --- |
| AMCONV1 static source checks | PASS, 9/9 |
| Phase 33 shell wire validator | PASS; 2084-byte request, 16-byte response |
| Existing Task Manager memory-details hosted regression | PASS, 83 static assertions across ownership, bounds, unavailable data, and overflow recovery |
| Existing UEFI desktop interaction hosted suite | PASS, 42 assertions |
| App Model descriptor, instance, lifecycle, factory, and service self-tests | `GUEST_PENDING`; they are wired only into the UEFI diagnostic path |

The Task Manager and desktop interaction projects are additional established
hosted suites; they are not substitutes for the App Model guest self-tests.

The desktop interaction project's x64 apphost could not find an installed
.NET 8 x64 runtime. The same built AnyCPU test assembly ran under the installed
x86 .NET 8.0.33 host and passed all 42 assertions. No global runtime or SDK
installation was changed. No standalone hosted runner exists for the App
Model guest self-test suite, and no mock runner was introduced.

## Repeatability, isolation, and remaining validation

- All seven phase checkpoints passed after the later phases; the Phase 29
  checkpoint's staged hashes remained unchanged.
- Repeating native identity generation produced the same source and CSV hashes.
- The BUILD1D comparison covered all 41 variants and found 41/41 executable
  and descriptor-file hashes equal.
- An independent Phase 29 rebuild/restage ran through the official
  five-variant builder in a fresh E: directory. Build/stage agreement passed
  5/5; repeat executable, stage executable, and GXMI file hashes each matched
  the original Phase 29 run 5/5. Details are in
  `reports\phase29-repeat-comparison.json`.
- Protected checkouts were read-only. At final inspection BUILD_INFRA was
  clean; mainline, MODERN_INPUT, and Server retained their observed existing
  status entries. No file was written to those checkouts.
- No QEMU, QAC admission, guest boot, branch switch, merge, rebase,
  cherry-pick, or push was performed.

Guest acceptance remains outstanding by design. There are no hosted compile
blockers. The next step is AMCONV2B's narrow typed folder-activation contract
for `gxos.shell.this-system` and `/Documents`, while preserving the accepted
AMCONV1 Files launch route. Do not infer guest acceptance from these hosted
results.

## Evidence and rebuild baseline

The full branch-specific evidence tree is
`E:\guideXOSUEFI_APP_MODEL_BUILD1E`, separate from BUILD1D. It contains the
phase build/stage logs, checkpoints, generated identity input, compiler logs,
source-check logs, runtime references, identity comparison, and repeatability
outputs. Large generated files remain outside Git.

The generated identity source and compiled hosted assembly/PDB are also
preserved under `runs\hosted-compile-final`; `artifact-identities.json` binds
those copies to their APP_MODEL `obj` outputs.

To rebuild after source changes, first reverify the pinned runtime, patch
evidence, Phase 23 pack, and private ILC. Then run each phase's existing
`Tools/PhaseNN/build_*.ps1` and `stage_phaseNN_image.ps1` in dependency order,
preserving its generated build record and staging output. Re-run the native
identity generator against the complete 41-pair APP_MODEL stage, compile the
Release/AppModel project, and run the post-compile compiled-allowlist audit.
The old identity source is not reusable unless the native generator accepts
the current build records, executable hashes, descriptors, and stage bytes.

The implementation and validation-tooling commit is
`9109f7a967a70369bb030dc49033af20ff50065b`. A report-only commit follows it.
The qualified source HEAD is the implementation/tooling commit; the final
branch HEAD is the report commit shown in the completion message. Both commits
are local to APP_MODEL.
