# BUILD_INFRA Recovery Report — 2026-10-29

**Outcome C — `BUILD_COMPATIBILITY_DEFECT`.** The exact SDK and a clean pinned runtime source checkout were recovered, and the supported Phase 19–23 source/build/staging steps ran in an isolated checkout. The resulting Phase 23 pack cannot link even the supported non-executed `Main() => 42` proof: the bootstrapper requires `RhRegisterOSModule`, while the staged target libraries and Phase 23 shim provide no definition. The 41 Phase 29–35 staged pairs remain unauthenticated; no records or identities were fabricated or imported.

## Checkout and preservation

- BUILD_INFRA branch: `BUILD_INFRA`; starting HEAD: `ac92e787c9d30861869f7841340d2a790cf1eb69` (`BUILD1C verify trusted managed build chain`). The ending HEAD is the local report-only commit; its SHA is stated in the execution summary.
- All generation work was performed in the detached clean checkout `E:\guideXOSUEFI_BUILD_INFRA_RECOVERY_20261029\source`, initially at that same HEAD. Its runtime source began clean at the pinned commit before supported Phase 21–23 transforms.
- The primary BUILD_INFRA checkout’s `out\rt`, existing Phase 21–23 evidence, Phase 29–35 staged artifacts, and other local files were not used as build inputs or overwritten. During this task, later primary-checkout status snapshots showed a modified `Tools/Phase26/build_phase26_managed_proof.ps1` and these untracked files: `Tests/ManagedArtifacts/test_build1d_managed.py`, `Tools/ManagedArtifacts/build1d_managed.py`, and `docs/evidence/BUILD1D_BUILD_PLAN.json`. They were not created or modified by this recovery and are excluded from the report commit.
- MODERN_INPUT, APP_MODEL, and mainline were inspected read-only. Current observed HEADs were MODERN_INPUT `3e1b18a92342d8776eb9fbe72fccd009468099d6`, APP_MODEL `9c773d55d2b8ce9203b4b8e1b08018014b8f88c7`, and mainline `a239589403d277adf8d089d714dbfc4e450e0c76`. Existing untracked files in those checkouts were preserved. The MI5 report records MODERN_INPUT HEAD `151482903ca1dca760fdc66b85e729aa16456218`; that report revision does not match the currently observed checkout. The MI5 report itself was read without modification.

## SDK and runtime source

**SDK 10.0.401 is installed side-by-side.** The default system `dotnet` resolves to SDK 10.0.403, but `C:\Users\guideX\.dotnet\dotnet.exe --list-sdks` includes 10.0.401. With that directory first on the process PATH and from `UserManagedProof`, `dotnet --version` returned 10.0.401 under `UserManagedProof/global.json` (`rollForward=disable`). The Phase 19 toolchain record says `sdk=10.0.401` and `globalSdkModified=false`. No host SDK was installed, removed, or changed. The build used the VS 18 x64 compiler environment (`vcvars64.bat`, MSVC 14.51.36231) and CMake 4.3.1.

The isolated runtime source is an official `https://github.com/dotnet/runtime.git` checkout, detached at `9d5a6a9aa463d6d10b0b0ba6d5982cc82f363dc3`. Phase 21’s record reports `cleanBeforeApply=true`; each Phase 21–23 lock and generated record names this commit. The primary `out\rt` cache is dirty (58 status entries at inspection) and was not treated as a clean checkout or source of recovery artifacts.

## Phase 19–23 generation results

| Stage | Result and evidence |
|---|---|
| Phase 19 | `Tools/build_phase19_runtime_pack.ps1 -Clean -BuildBaseline` completed after restoring the fresh checkout’s NuGet assets and loading the VS x64 environment. The PAL source hash is `1EBA39AC56A0A3C399A9C6881A5875944EB0BDA5C9C02810BAB0C55EE94398B1`; generated `.obj` is 3,977 bytes, SHA-256 `879F8A149CEEE2C55DB7CFA896B1878DE2166AF6920C75418BB45933C5B41FCC`; `.lib` is 5,758 bytes, SHA-256 `F73DB46064CC7827EED02DE329D777D5B609EDC1EDAF91EE95817F215083AB13`. Manifest SHA-256: `A6F5E31263BC3E91D967367E2A6A7F6B8BB046B9A7E5842121FC29DF3C00BE5C`. It validates a 20-external-symbol contract plus five local helpers; it does not claim a complete custom runtime. The manifest remains `CONTRACT_BUILT_SOURCE_RUNTIME_REQUIRED`.
| Phase 20 | `phase20_source_audit.ps1` returned the documented hard-stop outcome E: stock source has a broader Windows PAL/GC boundary than the bounded Phase 19 contract. It accepted neither stock nor renamed Windows packs. This was an audit; it did not modify source.
| Phase 21 | `apply_phase21_patch.ps1`, `phase21_gc_inventory.ps1`, and `phase21_source_audit.ps1` ran on the clean pinned checkout. The source-selection audit passed (`source-selection-proof-passed`). Phase 21 patch record SHA-256: `B4548C5D0682FE18E176F79AC8D1A699C7CE518717B7EF0340A40299D9110191`; audit SHA-256: `82D959DF2A408F1F09E93366CC141BE4FB903B7DDE8665D36A41AEBCCD038ECE`.
| Phase 22 | `apply_phase22_patch.ps1` completed. The supported native builder completed 116 Ninja tasks for `Runtime.WorkstationGC`, `Runtime.VxsortDisabled`, `standalonegc-disabled`, `zlibstatic.lib`, and `bootstrapper`. Final `Runtime.WorkstationGC.lib` is 5,590,988 bytes, SHA-256 `C62E0737D064B03FB5150F1789246BCCB0B69B089F72CF33C08FF84382C94273`. `lib /LIST` reports 61 object members, including one GUIDEXOS GC environment object and zero Windows GC or Windows PAL members. Patch record SHA-256: `7B4116FD044CE5B706913E99E2A1D71EEEF9BFA17EEE7510D1D84C4E99BBC5B6`; final build record SHA-256: `BEE54F8EF3EDCB1244C49D2F77EBB9F034AD7A18B6AA238273C0B8C2D4CAE2B2`. A prior supported single-target invocation produced the same member counts but a different archive hash (`CC201957…ECB95`); because its target set differed, this is not a controlled repeat and bit-for-bit reproducibility remains unproven. The compilation database spans configured targets, so its global Windows/GUIDEXOS compile counters are not used as proof of the specific archive’s contents. An earlier relative-path run emitted a misleading empty record after log redirection failed; it is preserved but not accepted. A failed target-name attempt (`zlibstatic`) is also preserved; the valid Ninja target is `zlibstatic.lib`.
| Phase 23 | `apply_phase23_ilc_patch.ps1`, `build_private_ilcompiler.ps1`, and `stage_guidexos_private_pack.ps1` completed. The private `ilc.exe` is 156,160 bytes, SHA-256 `268E0499447281B75ACBB6935DA5E710190D84285D16E38AF96E610786F1775D`; its build record SHA-256 is `71067A538449C4A40DD991136FB38AB194752BCC23637C469184D823A8B814B3`. The pack manifest SHA-256 is `059F9D6E5170D91CDEDD8AE53275E33E7F3E6AAFAFF8FCC71762961A696443D6`. Generated package hashes: host compiler `510671678DDE2D9BDF07F3BCC6D0D8878A3DDCED345754F6D3B5B8FD87AA345A`; GUIDEXOS target pack `E22C8C39DBAF9405044526EF28C00EEFE25709235DF761F860AF865F11CEBEB5`; host-only Windows RID package `8DBA33FED7BC476A3C2F744222E5F6A059066BE54AC5CA798BCA22BF318FDE60`. These are measured outputs from the cached NuGet inputs, not signatures or approved upstream package hashes. The manifest sets `customSourceAdaptationComplete=true` but `managedCoreLibAdaptationComplete=false` and explicitly says the pack is not execution authorization.

### Phase 23 compatibility defect

The supported `build_user_managed_proof.ps1` command restored and compiled the managed project, then failed in MSVC link with `LNK2019: unresolved external symbol RhRegisterOSModule referenced in function wmain` and `LNK1120`. There is no proof executable to inspect or validate.

`out/rt/src/coreclr/nativeaot/Bootstrap/main.cpp` declares and calls `RhRegisterOSModule`. The GUIDEXOS `Runtime.WorkstationGC.lib` contains `RhpRegisterOsModule` (a different symbol), but not `RhRegisterOSModule`. The runtime’s `CoffNativeCodeManager.cpp` and `UnixNativeCodeManager.cpp` define that name for their respective targets; neither target implementation is included in the GUIDEXOS archive. `Tools/Phase23/guidexos_link_shim.cpp` also has no definition for it. Phase 26’s checked-in builder comment assumes Phase 23 provided a blocked shim, but that symbol is absent from the Phase 23 source used here. A placeholder returning failure would make a link appear successful while leaving startup registration unusable; it is not an acceptable production fix. A genuine GUIDEXOS-compatible registration/code-manager implementation or an explicitly reviewed target source selection is required.

The Phase 23 no-execute artifact verifier was not run because no executable was produced. The complete failed proof log and link response remain in the isolated recovery output.

## Phase 29–35 provenance and compatibility

The authoritative cohort source is the BUILD_INFRA checkout’s seven phase builder scripts and their corresponding stage scripts. They generate phase records and final `.exe` outputs; stage scripts generate `.gxmi` descriptors. `Tools/ManagedArtifacts/managed_artifacts.py` is the identity authority: it checks each phase record, build output, staged `.exe`, descriptor flags and embedded digest, normalized PE timestamp rules, and byte-for-byte build/stage equality. It does not establish source provenance by itself. Build records do not carry an immutable BUILD_INFRA source commit or a complete source/toolchain fingerprint, so each recovery batch needs a supplementary pre-build source/input/tool fingerprint and a post-build record/artifact digest.

BUILD1B’s reconciliation report found the same 41 staged EXE/GXMI pairs in BUILD_INFRA, mainline, APP_MODEL, and MODERN_INPUT (pair-set SHA-256 `4A58BB07AFB87523358708181859160C5C9B08032A7BC135CE270F6436161582`). Equality across checkouts does not prove a shared source revision. Its 41-row record/output/stage classification was **36 E1** (record equals current output, staged differs), **4 E2** (record equals staged, current output differs), and **1 M** (all three match). The only M row is Phase34 `malformed`; it still has no provenance. For Phase29 `success`, the historic record/output hash is `F95857C741553ED0E951EC48D5642C164FFE60B8FD3751375BD715F1A15ADFBF`, while the staged EXE is `275C4F166F6964478A68D08E61B22BAB23507CF2B07F32C1BB7EE90EE52B0F56`. The latter matches BUILD1C’s separately rebuilt Phase29 `success` artifact, but BUILD1C proves only that one variant under its own captured input fingerprint. Neither relation authorizes rewriting the old record or copying the artifact into another branch.

BUILD1C rebuilt and staged Phase29 `success` from source in isolated workspaces; three complete runs recorded the same final EXE SHA-256 `275C4F166F6964478A68D08E61B22BAB23507CF2B07F32C1BB7EE90EE52B0F56`, and the GXMI SHA-256 was `5F02FB7EF7827639B0E1A27F97AF3BEAE9FC8399B5BB0649981958D78EEA0BA9`. Its supplementary 79-file source-input fingerprint was `A3A296B53EECC0E57C505D94A3B824512592A0F0DAA6F4A4F4B129E10A700BCE`. That is useful evidence for that one captured build, not a trusted package signature or evidence for the remaining 40 variants.

This isolated clean checkout had no Phase 29–35 build records. The supported identity audit stopped at the exact missing Phase29 record:

```text
python Tools/ManagedArtifacts/managed_artifacts.py --root <isolated-checkout> --stage before --audit-only
managed artifact identity error: Build record is missing: <isolated-checkout>\out\dotnet\phase29-managed-notification\phase29-managed-notification-build.json
```

No `ManagedArtifactIdentities.g.cs` was generated. No Phase 29–35 cohort was started after the shared Phase23 proof failed. Required cohort sizes remain: Phase29 5, Phase30 9, Phase31 5, Phase32 3, Phase33 5, Phase34 5, Phase35 9 (41 total). The primary checkout’s existing staged files and conflicting evidence remain preserved.

## Identity, kernel, tests, and artifacts

- Identity generator: **blocked at missing Phase29 record**; generated C# identity source: **not produced**; identity compatibility validation: **not run**.
- Production kernel build: **not attempted** because Phase23 cannot link and the 41-row identity set is absent.
- Test suites: **none run**. The performed validations were the Phase20/21 source audits, Phase19/22/23 generation and link gates, native archive member inspection, and the fail-closed identity audit described above.
- No QEMU, QAC, guest execution, push, branch switch, merge, rebase, reset, or cherry-pick occurred.
- Recovery outputs and logs remain under `E:\guideXOSUEFI_BUILD_INFRA_RECOVERY_20261029\source\out\dotnet\`; the full pinned runtime checkout is under `...\source\out\rt`. The isolated checkout is retained for review. No generated pack, record, executable, identity source, or kernel was copied into primary BUILD_INFRA or any dependent checkout.

## Safe handoff requirements

1. **BUILD_INFRA shared prerequisite:** resolve `RhRegisterOSModule` with a reviewed, source-built GUIDEXOS-compatible registration/code-manager path. Preserve the current failing link log as the regression baseline. Rebuild the Phase22 archive and Phase23 pack from a clean checkout at the pinned runtime commit; capture the exact source, patch, toolchain, NuGet input, script, output, and package hashes. Re-run the no-execute proof, image inspection, loader probe, and Phase23 verifier. Do not add a false-return placeholder or reuse old packages to pass the link.
2. **BUILD_INFRA cohorts:** only after that gate passes, build each phase’s full cohort in a fresh phase-specific output root from the BUILD_INFRA source. Verify all seven records, 41 build outputs, 41 staged EXEs, and 41 GXMI descriptors individually, including build/stage byte equality and source/toolchain fingerprints. Then run the unmodified whole-set identity generator with `--stage generated`, validate the actual generated C# identities against those artifacts, and only then attempt the supported kernel build and post-kernel/ramdisk audits.
3. **MODERN_INPUT:** keep its checkout at its own current reviewed HEAD and recheck it because the MI5 report’s stored HEAD is stale. Use the installed per-user SDK 10.0.401 explicitly; do not install or change the global SDK pin. Provision/revalidate the shared runtime inputs only from the corrected BUILD_INFRA pipeline. Rebuild all seven cohorts from MODERN_INPUT’s own source and produce its own records, staged pairs, identity source, and audits. Do not transplant BUILD_INFRA, mainline, or APP_MODEL records, executables, descriptors, or generated C# identities.
4. **APP_MODEL:** independently recheck its current HEAD and build contract. BUILD1B recorded 94 build-contract input differences from mainline, so validate APP_MODEL’s inputs locally. Rebuild its seven cohorts and 41 staged pairs from APP_MODEL source, run its own whole-set identity and kernel audits, and only then retry the documented `build.ps1 -UefiDiagnosticMode AppModel` path. Revalidate any copied immutable shared package against the BUILD_INFRA source/build manifest before use; never copy branch-specific outputs.
5. Keep QEMU and QAC disabled until separately authorized. If no reviewed target registration implementation is available, leave the production build explicitly blocked at the Phase23 link gate.

## Reproduction inputs and hashes

All values below identify the source/build definitions at BUILD_INFRA HEAD `ac92e787c9d30861869f7841340d2a790cf1eb69`; runtime-source generated records and output hashes are listed above. Build environment: Windows PowerShell, SDK 10.0.401, VS 18/MSVC x64, CMake 4.3.1, official runtime commit `9d5a6a9aa463d6d10b0b0ba6d5982cc82f363dc3`.

| Input | SHA-256 |
|---|---|
| `Tools/build_phase19_runtime_pack.ps1` | `E2991682887FD83E34103A6C19E136EF15AA6BA59D438651EE2D1A78102C6BAF` |
| `Tools/Phase19/pal-contract.json` | `B7D30BEE0FC9060E2E2D0238D96C1518456F999C4D434C8167CF6C19C52DDA46` |
| Phase20 lock / audit script | `5DC606154D76EE6293AB966586F2BD00E3D67E044E0B204ECB6A3A3B717E2EA5` / `C91E49DC2A732AA664684622964A44B5BD0C6C7597A1494EB065DB18C33EE756` |
| Phase21 lock / apply / source-audit / inventory | `546D1B1788695C0110C44AD3C313CD33F87A5A95CAE9A345452320D818FCF04B` / `069F38F74C5CC47D465701D5A83D47C8DA7496EC45FF3C1D04DA69744E0064EB` / `136B4AB6AFF86EB16760C8FD637B70C17B0836A4CC0A1CC992AA77E459B93251` / `34462BEC69CFBAC29B33190527C3530F6B5349319770987E12122EC12306349B` |
| Phase22 lock / apply / native builder | `4EACCCC0108C5C0C23A8A59853AD750C336FED81E03B61C9A1C8A42BC0BC2113` / `573C6E35D43C7151B6EEBE137B1805F35F21FC7A2DF01697F8FB45E546B4672E` / `C0CC46AA3225C2DCDB3B376EE4EAF0AA8FCBF96C589ADE55902FF98D0579B332` |
| Phase23 lock / ILC patch / private builder / pack staging / proof builder / link shim | `19DF0D20DEFC2FDA9BA09C7AFD7B6C28026AB1D0053D3EB9CCA318D02D03BEAC` / `A40664B56853F4D6795BE215CF16DB50315DA37DDF5BDDE9C93A8FEB1D78169D` / `FA4F7178D2906C3562129CD750C0139B8DE92C4A34AB7EB5979A589251A7FFD3` / `E5061BA2BA3A936102413BE77BEF6FDF276402BB42313A9619A632FAD2D86C3A` / `61957F2E04948967DD9C9BB8E3C7B376B600C6B2B0D91B3AEB603363F3D1B583` / `FCE3F0566FDC415B6622AC33D67B18677539C133AF7D619DE26C2F3AE1D9D46D` |
| Identity generator | `BAE48463DD64B1A7814B975DE59368BD6F7C18F2C0ECFF3A7B3198C5CFF26F2A` |
| Phase29–35 cohort builder scripts, in phase order | `1455D0CD5678D5DF7E5A6F650F04B0A62B407F0AB5325D110CAC09D30A8FE443`; `69AB8B8620C305BF883C10D689A7A2299E520531B606CBC5A76548AE62FBC5BE`; `4468696110FE6D00CFD24752B14B1A300DFD69257C44DD347972E175E2BEDE53`; `15DC0C3D4FC201C6D2625515D0414C7BF1E27DEC1786DDF19BBD70DAC8C2E1DC`; `F67F35A4382E6E84D74A94F16AAE57CD30438FC4F0B7377D39CF2715C1BE18BD`; `46C15785B7B33F43AF612E71F706096F77BD5E0295EA6B8147094DCFDF894B2B`; `82F14221DFD2724A8C2DDA21D9ED7C07E23031100A41B25B55772515EFF77364` |

Successful command forms (run from the isolated checkout after importing the VS x64 environment from `vcvars64.bat` into the current process and prepending `C:\Users\guideX\.dotnet` to process PATH):

```powershell
& 'C:\Users\guideX\.dotnet\dotnet.exe' --version
Push-Location .\UserManagedProof
dotnet restore .\guideXOS.UserManagedProof.csproj
Pop-Location
& .\Tools\build_phase19_runtime_pack.ps1 -Clean -BuildBaseline
& .\Tools\Phase20\phase20_source_audit.ps1 -SourceRoot .\out\rt
& .\Tools\Phase21\apply_phase21_patch.ps1 -SourceRoot .\out\rt
& .\Tools\Phase21\phase21_gc_inventory.ps1 -SourceRoot .\out\rt
& .\Tools\Phase21\phase21_source_audit.ps1 -SourceRoot .\out\rt
& .\Tools\Phase22\apply_phase22_patch.ps1 -SourceRoot .\out\rt
& .\Tools\Phase22\build_guidexos_native_runtime.ps1 -SourceRoot .\out\rt -OutputRoot .\out\dotnet\phase22-native-runtime-absolute\native-pack-dependencies-final -BuildTarget 'Runtime.WorkstationGC Runtime.VxsortDisabled standalonegc-disabled zlibstatic.lib bootstrapper'
& .\Tools\Phase23\apply_phase23_ilc_patch.ps1 -SourceRoot .\out\rt
& .\Tools\Phase23\build_private_ilcompiler.ps1 -RuntimeSourceRoot .\out\rt -OutputRoot .\out\dotnet\phase23-private-ilc
& .\Tools\Phase23\stage_guidexos_private_pack.ps1 -RuntimeSourceRoot .\out\rt -PrivateIlcRoot .\out\dotnet\phase23-private-ilc -OutputRoot .\out\dotnet\phase23-runtime-pack-recovery
& .\Tools\Phase23\build_user_managed_proof.ps1 -PackRoot .\out\dotnet\phase23-runtime-pack-recovery -OutputRoot .\out\dotnet\phase23-user-managed-proof-recovery -ProjectPath .\UserManagedProof\guideXOS.UserManagedProof.csproj
python .\Tools\ManagedArtifacts\managed_artifacts.py --stage before --audit-only
```

The final two commands intentionally fail at the current compatibility defect and missing Phase29 record. The detached checkout and all failed/successful logs are retained; no build or provenance file was copied into dependent branches.
