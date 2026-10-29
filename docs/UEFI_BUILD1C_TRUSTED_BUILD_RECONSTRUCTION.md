# UEFI BUILD1C — Trusted Build Chain Reconstruction

**Outcome A — `BUILD1C_FIRST_PROOF_VERIFIED`.** The pinned runtime prerequisites were restored and validated in BUILD_INFRA. The supported Phase 29 pipeline rebuilt, staged, and verified the `success` variant from source in isolated workspaces. Two independent complete runs produced byte-identical final executables. The remaining 40 variants and the full 41-row identity set are not complete.

All changes were confined to `D:\dev\guideXOSUEFI_BUILD_INFRA`. Mainline, APP_MODEL, and MODERN_INPUT were kept read-only. The branch remained `BUILD_INFRA`; there was no branch switch, merge, rebase, cherry-pick, or push.

## 1. Starting state and build environment

- Starting HEAD: `4d093edac6fce2db61940e4eb813f9099ff373c5`; the checkout was clean and already two commits ahead of its remote at the start.
- The preflight estimated 3 GiB for an isolated run and measured about 6.286 TB free on E:. D: had about 65 GB free at the initial check. The large compiler outputs and run evidence went under `E:\guideXOSUEFI_BUILD_INFRA_BUILD1C`; the verified runtime source was copied to BUILD_INFRA's required local `out\rt` path.
- Toolchain captured in the provenance record: Windows PowerShell 5.1.26100.9711; Visual Studio 18 Community with MSVC 14.51.36260.0; Windows SDK 10.0.26100.0; global .NET SDK 10.0.403; pinned runtime dotnet 9.0.100-rc.2.24474.11; Python 3.12.14; NASM 2.15.05; Git 2.54.0.windows.1.
- The helper's preflight and final verification both passed with no bytecode or other generated cache files left in the repository worktree.

## 2. Runtime prerequisite inventory and restoration

The runtime source was copied from the inspected mainline cache into BUILD_INFRA `out\rt`, then revalidated locally. The copy contains 66,731 files and 2,152,315,843 bytes and is at pinned commit `9d5a6a9aa463d6d10b0b0ba6d5982cc82f363dc3`. Recovery validation matched 54 locked tracked files, five required templates, and six allowlisted generated artifacts. Template comparison canonicalizes the repository's CRLF checkout text to LF; the copied runtime template bytes still have to match the locked hashes exactly.

The supported `Tools/Phase23/recover_source_patch_evidence.ps1` procedure reconstructed the Phase 21, 22, and 23 records from the verified cache. Their record hashes are `1F1E7925942C75B04CFD003DA2E451960D3A662DB074D4EB5D5AB02046F780DC`, `31639A631E2A0AF633B999CDB4DC73B56FA7CA0A4005433A748679D7E5FC0229`, and `5B4BB23A13760B7C2A7D9810E8E3B8CABFAA1F0C7644804A33501F0900AA40A4` respectively. The source locks and recovered records are listed in the preflight section of the retained run evidence.

The private ILC is 156,160 bytes with SHA-256 `268E0499447281B75ACBB6935DA5E710190D84285D16E38AF96E610786F1775D`; it matches its build record and the compiler package contents. The materialized Phase 23 pack manifest preserves schema 2 and has SHA-256 `B9E1D6D4078F453C6B495379953BCCC1AA11311B9CF8A5595D8030762B0544B2`. Its original source manifest hash is `B7EC1CF49C73D49A8DEB8E4DDA41E94DC7806ECDC06AE6C65EE586B87465FE3A`. The path-rebased copy points only into BUILD_INFRA or the isolated E: input area.

All seven runtime asset hashes in the pack manifest matched. The three package archives were measured and their `.nuspec` identities and version `9.0.0` checked:

| Package | Bytes | SHA-256 |
|---|---:|---|
| `Microsoft.DotNet.ILCompiler` | 42,562,402 | `E4E0BB1D6057A0BFD9B2F5357001A05B9320E740BF054F377D5426F363E2C41A` |
| `runtime.guidexos-x64.Microsoft.DotNet.ILCompiler` | 5,853,529 | `FDAEA2A4222985FB06A0D42DFFD5838DCF0726FDEA17C0139585925331A250E8` |
| `runtime.win-x64.Microsoft.DotNet.ILCompiler` | 81,801,976 | `0E3E1D91A1994607F6DCED80E961E921D75E015C1F9A77EAD6A7A597EE8DDD2A` |

The production manifest does not authenticate the package archives with package hashes. These SHA-256 values are supplementary measurements of the copied files and are not an independent claim about their feed origin. The existing `Tools/Phase20/verify_guidexos_runtime_pack.ps1` passed against the materialized pack.

## 3. Build and staging procedure

The new `Tools/ManagedArtifacts/Invoke-BUILD1C.ps1` wrapper supports `Preflight`, `Build`, and `Verify`. It calls the supported Phase 29 builder and stage script, uses a fresh workspace for every build, preserves logs, and refuses stale output paths or incomplete records. It does not create artifact or identity metadata itself. `Tools/ManagedArtifacts/build1c_trusted.py` checks pinned inputs and uses the existing Phase 23 verifier and managed identity implementation for validation.

The Phase 29 builder now accepts one selected variant and a scratch shim source path. Phase 26 accepts include directories for the shim compile. These options let BUILD1C place all mutable outputs under its unique run root while still compiling the real PAL contract, generated link shim, runtime, and managed project. No stub, copied proof executable, fabricated build record, or historical staged payload was used.

The final complete run is retained at `E:\guideXOSUEFI_BUILD_INFRA_BUILD1C\runs\phase29-success-verified`. Its preflight record was captured before compilation. Its supplementary provenance records the BUILD_INFRA revision, dirty state, 79-file source-input fingerprint (`A3A296B53EECC0E57C505D94A3B824512592A0F0DAA6F4A4F4B129E10A700BCE`), runtime identities, compiler/tool versions, relevant build script hashes, build record digest, artifact hashes, and build-to-stage agreement. The source revision is the starting commit above; the dirty-state list and file hashes explicitly capture the BUILD1C source changes used by the build.

The Phase 29 success build used `Phase29Mode=Success` and target `guidexos-x64`. The supported stage script produced the GXMI descriptor. Both the build output and staged executable are 706,048 bytes with SHA-256 `275C4F166F6964478A68D08E61B22BAB23507CF2B07F32C1BB7EE90EE52B0F56`. The 260-byte descriptor has SHA-256 `5F02FB7EF7827639B0E1A27F97AF3BEAE9FC8399B5BB0649981958D78EEA0BA9`; its embedded artifact digest matches the executable. Build and stage agree byte-for-byte. The Phase 29 build record has SHA-256 `4646485E6CB1983783068488D234E8700D092236DC48614F42C31D1DBD7B415F`.

Two isolated complete builds (`phase29-success-retry1` and `phase29-success-verified`) were directly compared: all 706,048 bytes matched. A third complete run also recorded the same final executable hash. The final NativeAOT build emitted two warnings: an explicit ILCompiler package-reference advisory and linker warning LNK4210 about a `.CRT` section. The build completed without errors. The separate CoreLib build completed with zero warnings and zero errors.

## 4. Verification results

The Phase 23 static artifact verifier returned `PASS` and passed all 13 negative checks, including wrong runtime identity/hash, unknown PAL symbols, Windows or direct kernel imports, invalid TLS metadata, RWX sections, and descriptor mismatch.

The single-row audit reused the existing native row validation to check record/output hashes, descriptor digest, timestamp normalization, and build/stage equality. Its status remains `compiled allowlist stale` because a one-row build cannot update or satisfy the compiled 41-variant allowlist. The native generator's full audit-only run correctly rejected the partial evidence at the missing `title-failure` mode. No identity source was generated and no full 41-row proof is claimed.

The added BUILD1C test suite passed 13/13 cases. It covers missing runtime and package evidence, wrong source/patch hashes, incorrect tools, stale or mismatched output, interrupted builds, valid single-row verification, repeat invocation, deterministic reporting, BOM JSON input, phase-owned PAL helper handling, and rejection of partial sets. The existing BUILD1A/BUILD1B auditor suite passed 17/17 tests. Combined result: 30/30.

## 5. Failed attempts and remaining blockers

The first isolated compile reached the Phase26 shim and failed with MSVC C1034 because the generated scratch shim could not find the Phase19 PAL header. The builder now passes the Phase29 include directory explicitly; subsequent builds compiled that shim successfully. The first verifier pass also exposed two supported-format issues: PowerShell 5.1 emits UTF-8 JSON with a BOM, and the Phase 29 map contains phase-owned PAL helpers not listed in the Phase 19 runtime manifest. The verifier now reads UTF-8 BOM JSON and accepts only explicitly supplied phase-owned helper names; its default symbol checks remain strict. These fixes are covered by regression tests.

BUILD1C verified only Phase 29 `success`; the remaining 40 payloads and six other phase build records are outstanding. Package archive authenticity still needs an approved trusted-source hash, since the production runtime-pack schema does not provide one. The supplementary provenance is deterministic evidence, not a signature or a replacement for the native identity generator. No QEMU, QAC, or guest execution was run.

Failed and successful run directories, including their logs and evidence, remain under `E:\guideXOSUEFI_BUILD_INFRA_BUILD1C\runs`; no generated EXE, GXMI, runtime archive, or large build tree is committed.

## 6. Full 41-variant rebuild strategy

All seven phase cohorts share the pinned runtime source, private ILC, runtime pack, compiler, and SDK. Each cohort has phase-specific project/build inputs and its own build record; its variants should be built using that phase's supported builder as a unit. Rebuild every cohort from the active checkout's source rather than carrying historical executables or records across branches.

| Cohort | Variants |
|---|---:|
| Phase 29 notifications | 5 |
| Phase 30 clipboard | 9 |
| Phase 31 shell | 5 |
| Phase 32 open document | 3 |
| Phase 33 shell action | 5 |
| Phase 34 resource read | 5 |
| Phase 35 persistent read | 9 |
| **Total** | **41** |

BUILD1D should proceed in independently reviewable phase batches. For each batch, use a fresh output root, capture source and tool fingerprints before building, run the supported builder and stage script, then check every record, EXE, GXMI, and staged byte before accepting the batch. Reuse only verified immutable runtime/package inputs through copy plus local revalidation. Keep outputs and stage roots checkout-local; do not share writable cache junctions. Run phases sequentially until every script has a unique output root and stage path; any future parallel runs must have separate roots and evidence directories.

After all seven records and 41 staged pairs validate, run the unmodified whole-set native generator with its strict defaults and require all 41 identities. Then run the supported kernel build, post-kernel identity audit, and final ramdisk audit. Never treat a partial generator failure as success or promote an incomplete bundle. Do not start the entire set automatically as part of BUILD1C.

## 7. BUILD1D and APP_MODEL recommendation

BUILD1D should first finish the remaining Phase 29 variants and validate the complete Phase 29 record, then process Phases 30–35 in separate captured batches. At each checkpoint, preserve the original build/stage records and provenance beside the batch evidence. Only publish immutable inputs after their source identity and package provenance are approved and every batch passes the native whole-set audit.

APP_MODEL is still not buildable through its hosted compile path. BUILD1C changed only BUILD_INFRA. APP_MODEL still lacks its local pinned runtime tree, Phase 21–23 records, private ILC/runtime pack, seven current build records, 41 locally built/staged payloads, and generated `ManagedArtifactIdentities.g.cs`. BUILD1B also found 94 build-contract input differences versus mainline. The verified runtime may be copied there only through a new isolated copy-and-recheck procedure; the Phase 29 artifact built here does not establish APP_MODEL compatibility. After BUILD1D proves the complete BUILD_INFRA pipeline, provision APP_MODEL's runtime prerequisites locally, rebuild its seven cohorts from APP_MODEL source, pass its own native 41-row audit, and only then retry `build.ps1 -UefiDiagnosticMode AppModel` with post-kernel and ramdisk audits.

## 8. Retained evidence

- Full final run and logs: `E:\guideXOSUEFI_BUILD_INFRA_BUILD1C\runs\phase29-success-verified`
- Package and runtime measurement evidence: [BUILD1C_RUNTIME_PACKAGE_CONTENT_EVIDENCE.json](evidence/BUILD1C_RUNTIME_PACKAGE_CONTENT_EVIDENCE.json)
- Final success build provenance: [BUILD1C_PHASE29_SUCCESS_PROVENANCE.json](evidence/BUILD1C_PHASE29_SUCCESS_PROVENANCE.json)
- Earlier independent success-run provenance: [BUILD1C_PHASE29_SUCCESS_RETRY1_PROVENANCE.json](evidence/BUILD1C_PHASE29_SUCCESS_RETRY1_PROVENANCE.json)

The provenance JSON files preserve the local tool paths and run roots used during this reconstruction. Package hashes in them remain supplementary measurements, as described above.
