# Phase 35R12: Contained Same-Key NotFound Lifetime Closeout

## Result

**Outcome D.** R12 established a contained build lane, normalized both NotFound proof paths to `missing.phase35`, matched all six baseline strings, classified their producers and owners, and fixed the six request-local lifetimes. The post-fix isolated and full NotFound checks pass with zero live strings. The broader Phase 35 qualification remains incomplete: the 25-lifetime stress returned 35 for all 25 runs but retained a linear 32-KiB-per-run allocator increase, so the guest reported `PHASE35_COMPLETE=0`. Phase 36 remains gated.

No commit was made because Phase 35 did not reach an accepted state.

## Contained build lane

The live repository was copied to the ordinary sibling directory:

`D:\dev\guideXOSUEFI_Phase35R12_SANDBOX`

This is a filesystem copy, not a Git worktree. The copy used `robocopy /XJ` so the live `out\dotnet` junction was not followed; its target was copied to the sandbox as a local directory. The sandbox `out\dotnet` has `Directory` attributes and no link target. The nested `out\rt` repository, including its dirty tracked and untracked files, was copied into the sandbox. Sandbox `out\rt` remained at HEAD `9d5a6a9aa463d6d10b0b0ba6d5982cc82f363dc3` with the same 65 detailed status entries after the builds.

`R12_INPUT_MANIFEST.json` records the source and tool hashes before building, the copied runtime state and hashes, and the runtime-pack inputs. Its pre-build Phase 35 source/tool entries matched the live files. All 887 Phase 23 runtime-pack files matched the live cache inputs. The sandbox cache is local and isolated from the live cache.

Before the sandbox build, the live root was `main` at `0217f13798d4e07f7089da095641e482f8579c63` (`origin/main`, ahead/behind `0/0`), with a clean root status. `Docs\APP_MODEL_CONVERGENCE.md` was clean. The nested runtime was at the HEAD above with 65 detailed status entries.

After all sandbox builds and guest runs, before the deliberate source transfer below, `R12_FINAL_LIVE_COMPARE.json` checked 66,839 protected live files. It found no missing files and no hash mismatches. The live root HEAD, branch, clean status, nested runtime HEAD, and all 65 detailed runtime status entries matched the baseline. The live kernel, ramdisk, and EFI hashes remained:

| Live file | SHA-256 before and after sandbox work |
|---|---|
| `kernel.elf` and `ESP\kernel.elf` | `F52B538BDA9098CA8864A8D7E539C702C823F611702E9AB3B55943FE686B49A3` |
| `ramdisk.img` and `ESP\ramdisk.img` | `59384D12B83D89D243C7BDF46B1F75F2102A624FC628CACF492BF4A84424B08E` |
| `ESP\EFI\BOOT\BOOTX64.EFI` | `3E9C0DEA3577C307DEA161429935F5FD400D7BEF7F79979A17B827DDDE81D58B` |

The initial R11-style smoke build passed before key normalization. Its log is `out\phase35r12-smoke-build.log`; it reported 41 managed artifacts and 41/41 agreement for descriptors, build staging, ramdisk staging, and compiled allowlist. The post-normalization full cohort rebuild is recorded in `out\phase35r12-cohort-build.log` and again reported 41/41 for every agreement. That build produced a coherent ramdisk/kernel/EFI set.

| Sandbox build/run | `kernel.elf` SHA-256 | `ramdisk.img` SHA-256 | EFI SHA-256 |
|---|---|---|---|
| Post-normalization coherent cohort | `2787B530F3EAB06B0A20F62D61D665A3018FD89903664D365326AD330CDAA0C9` | `55B2BB16A58D63CD713E67E3D9566DC6F3A915F1BFD2EF0B15256E942FD9289A` | `3E9C0DEA3577C307DEA161429935F5FD400D7BEF7F79979A17B827DDDE81D58B` |
| Same-key instrumented baseline capture | `B29214C42727813F6CE956F42F4FFD9887EB0832B5DD1984054F0A078A494886` | same | same |
| Three-diagnostic-suppressed capture | `C2B66FC73558C2D1076180A702081CAB3ACA12091C327973671CA564CE4108CE` | same | same |
| Post-fix R9 ledger capture | `ABD3E8A8666ED16ABFB67CDFDC39A06ABF85B991C3B69389F231ED5BCAF3DBCA` | same | same |
| Final Phase35R qualification run | `54AD46187E39B90CE897091B380BB28782F28A1E892B2FBA0CAC651769380001` | same | same |
| Final source-shape compile-only build, including the matching FAT range-error cleanup | `DC387555EED336B00FB1D0E0721BC421FEDFB468CAC2AD94A434CCEAA7809B0F` | same | same |

The first smoke log does not record a kernel hash; the table records the exact hash for the subsequent coherent cohort and every authoritative guest run. Later diagnostic builds skipped the ramdisk build because the 41-row cohort had already been regenerated and audited.

The final compile-only row was produced after the Phase35R guest run to validate the matching range-error cleanup branch. That branch was not exercised by the NotFound guest path; the same ownership pattern in the file-length branch was exercised by the post-fix R9 run.

## Canonical same-key evidence

`UserManagedPersistentStorageProof/Program.cs` now defines one `MissingKey` constant, `missing.phase35`, used by both the full proof and isolated `GUIDEXOS_PHASE35_NOT_FOUND` build. The R12 guest marker reports FNV-64 over the UTF-16 key characters as `507DC8E17B07731E` for both full request `0x3` and isolated request `0xE`. The existing numeric marker helper emits its label and value on adjacent serial lines; the value immediately following each `PHASE35_R12_KEY_FNV64=` label is the hash.

The initial instrumented same-key capture produced six matching strings in both requests. The common return address was `0x100126A9`, which the exact sandbox `Kernel.map` resolves to `String.Format+0x41`. The fixed numeric producer sites below resolve each allocation to its actual source expression; the return address alone is not used as provenance.

| Slot | Content identity / meaning | Length | Requested bytes | Stable content hash | Site | Full request `0x3` | Isolated request `0xE` |
|---|---|---:|---:|---|---:|---|---|
| 1 | `P35_DIAG_APPLICATION_ID=selftest.phase10.persistent` | 51 | `0x80` (128) | `94A661B97507E172` | `0x1FE` / 510 | Match | Match |
| 2 | `P35_DIAG_RELATIVE_PATH=missing.phase35` | 38 | `0x68` (104) | `7B75BBA2453D7996` | `0x1FF` / 511 | Match | Match |
| 3 | `P35_DIAG_FILE_PATH=` plus the encoded backend value path for the same app/key | 251 | `0x210` (528) | `2DB56378C1962B98` | `0x200` / 512 | Match | Match |
| 4 | `Persistent storage operation failed: System.Object` | 50 | `0x80` (128) | `ADE827DD03D3AE1F` | `0x213` / 531 | Match | Match |
| 5 | `3`, the typed NotFound result code | 1 | `0x18` (24) | `AF63AE4C86019E62` | `0x208` / 520 | Match | Match |
| 6 | `RING3_PERSISTENT_READ_TYPED_RESULT=3` | 36 | `0x60` (96) | `D82EC95D355533BA` | `0x209` / 521 | Match | Match |

**Same-key overlap: 6/6.** Addresses differ across requests and are reused between allocations within a request; comparison uses length, requested bytes, content hash, site, and meaning.

Before repair, these six records had zero `Dispose` calls and zero free attempts. The post-fix R9 capture records exactly one `STRING_DISPOSE` and one accepted `STRING_FREE` for each allocation sequence in both requests. Each free guard reports `accepted=1`, `exactRun=1`, and each free result is `0x1000`. Both requests report `STRING_LIVE_COUNT` stage 1 and stage 3 as zero. The corrected sandbox-only R9 telemetry correlates dispose/free records by allocator run ID as well as address, preventing recycled addresses from inflating a previous object's dispose count.

## Producers, ownership, and last use

| String(s) | Producer and owner | Last consumer / disposal point | Classification |
|---|---|---|---|
| Slots 1–3 | `PersistentFatBackend.TryReadValue` creates three distinct concatenated marker strings for the application ID, relative key, and encoded file path. The backend caller owns each temporary. | `Ring3Abi.Phase35DiagnosticMarker` synchronously reads each character to serial and does not store or dispose the input. The backend disposes each local in `finally` after the marker returns. The surrounding backend `finally` separately disposes `filePath`. | Diagnostic backend temporaries |
| Slot 4 | `CSharpApplicationStorageService.PersistentDiagnostic` creates the bounded service failure detail. The failure `ApplicationServiceResult<T>` owns the diagnostic field only when created through `FailureOwnedDiagnostic`. | `Ring3Abi.DispatchPersistentStorageRead` disposes `read` in its `finally`; the result clears and disposes its owned diagnostic. The 50-character diagnostic is within the 192-character bound, so `BoundDiagnostic` returns the same string reference. The inner `System.Object` text is the base `ToString()` result and is not a separately allocated string in this capture. | Request-local service diagnostic |
| Slots 5–6 | `Ring3Abi.DispatchPersistentStorageRead` creates `response.ResultCode.ToString()` and concatenates it into the typed-result marker. The ABI method owns both strings. | The serial marker synchronously borrows the concatenated message; the ABI disposes the message and result string in `finally`. The one-character value is `3`, the NotFound enum value. | Diagnostic ABI temporaries |

No string is global or intentionally persistent; none is aliased to the backend input strings or stored by the serial marker. The public service result's owned-diagnostic path is explicit. The existing public `Failure` factories remain non-owning for literal or borrowed diagnostics. The serial contract was not changed globally.

### Suppression and layer attribution

After the six-way baseline capture and before ownership changes, a sandbox-only build mode suppressed construction of the three backend marker messages. The isolated request emitted `PHASE35_R12_KNOWN_DIAGNOSTICS_SUPPRESSED=1` and retained exactly the other three strings: slot 4 (length 50/site 531), slot 5 (length 1/site 520), and slot 6 (length 36/site 521). This confirms the three-message suppression delta. The authoritative suppression log is `out\phase35q\serial-c3f174f5bcbc41bab2109be6a965e0eb.log`.

Direct backend-only and bare raw-ABI guest controls were not run because site IDs, exact content, and owners resolved every string without ambiguity. The following layer matrix describes source-path reachability, not separate measured runs:

| Producer set | Direct backend-only path | Raw ABI dispatch path | Public SDK path | Full proof |
|---|---|---|---|---|
| Backend messages, sites 510–512 | 3 | 3 | 3 reachable through the ABI call | 3 |
| Service diagnostic, site 531 | 0 | 1 | 1 reachable through the ABI call | 1 |
| ABI code/message, sites 520–521 | 0 | 2 | 2 reachable through the ABI call | 2 |
| Expected total | 3 | 6 | 6 | 6 measured before repair |

The direct backend column bypasses the service and ABI layers by definition. The SDK column means those producers are reached by the public SDK request; the SDK itself did not create any of these six strings.

## Fixes transferred to the live source tree

The live changes are limited to the Phase 35 proof key, the backend/service/ABI ownership fixes, the R9 host-runner guard, and this document:

- Backend diagnostic messages are named locals and disposed in `finally` after synchronous serial output, in both the file-length and range-failure branches.
- Both generic and non-generic `ApplicationServiceResult` types support an explicit owned-diagnostic factory and dispose only diagnostics they own. `CSharpApplicationStorageService.PersistentFailure` uses that factory.
- The ABI's typed-result string and concatenated marker are disposed by their caller after serial output returns.
- The full and isolated proof use the same key constant.
- The host runner skips its unrelated Storage35Q allocator-snapshot assertion for `-Phase35R9Ledger`; the R9-specific ledger checks remain active.

No automatic disposal was added to serial output. R12 site instrumentation, suppression mode, allocator ledger correlation changes, generated identities, payloads, kernel/EFI outputs, ramdisk, caches, and temporary guest images remain in the sandbox.

## Post-fix validation and remaining qualification

The post-fix R9 guest run passed the stress-equivalent full proof, repeat, no-read, one-read success, and isolated NotFound controls. For the isolated NotFound request, the ABI returned typed result `3`. The first full-request lifetime accounted for `2/2` measured pages, with zero unexplained and zero over-accounted pages. The full requester returned `35`, its NotFound subproof passed, and both same-key request ledgers ended with zero live strings. The one-read minimal Success regression passed.

The final Phase35R guest run completed 25 successful return-code lifetimes and emitted `PHASE35_25_LIFETIME_STRESS=PASS`, but `PHASE35_STRESS_ALLOCATOR_STABLE=0`. Unknown one-page allocations rose by exactly 32,768 bytes per lifetime, for `PHASE35_DIAG_STRESS_MEMORY_DELTA=0xC8000` (819,200 bytes) across 25 lifetimes. The first five samples also rose by 32,768 bytes per step; a separate five-run boot was not performed. The target six-string ledgers were clean, and all reported fault counters were zero: `freeInvalid=0`, `freeCorrupt=0`, and `freeNoPages=0`. The guest reported `PHASE35_ACTIVE_STORAGE_REQUESTS=0`, `PHASE35_OPEN_PERSISTENT_HANDLES=0`, and balanced process cleanup, but then emitted `PHASE35_COMPLETE=0` and `RING3_PHASE35_COMPLETE=0`. The runner stopped before the same-image verification reboot.

The first Phase35R boot also emitted `PHASE35P2_REBOOT_DELETE=FAIL`; the other listed P2 storage checks passed. The self-tests reported lifecycle `15/15` and taskbar grouping `16/16`. The R7 acceptance sequence is therefore not complete. Still outstanding are the five-lifetime flat-slope gate, the 25-lifetime allocator-stability gate, diagnostics-off controls, the 8-MiB contiguous allocation, same-image reboot with `seedWrites=0` and post-reboot return 35, DSDT default-memory and 1280-MiB runs, reproducibility and mutation rejection, Phase 26–34 regressions, compatibility checks, and the ordinary full build. Phase 36 remains gated.

## Live repository state after transfer

The live repository remains on `main` at `0217f13798d4e07f7089da095641e482f8579c63`, with no branch, HEAD, upstream, or Git-identity change. The nested runtime remains at `9d5a6a9aa463d6d10b0b0ba6d5982cc82f363dc3` with its original 65 status entries. The root diff contains only the six intended source/script files and this new document; `Docs\APP_MODEL_CONVERGENCE.md` and R9–R11 reports were preserved. No commit or push was performed.

After that compile-only sandbox build, `R12_FINAL_POST_TRANSFER_COMPARE.json` rechecked all 66,839 protected live output/runtime files; none of the six transferred source paths is in that protected-output set. It found no missing files or hash mismatches. The root status matched the six intended source changes plus this document, root HEAD/branch stayed at baseline, and all 65 detailed nested-runtime status entries matched.

The next action is to trace the 32-KiB-per-lifetime unknown allocator increase and the P2 reboot-delete failure, then resume the outstanding R7 qualification in the contained lane.
