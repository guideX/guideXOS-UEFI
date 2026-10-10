# Phase 35R11 Same-Key NotFound Closeout

Date: 2026-10-10

## Outcome

**Outcome F — the exact same-key capture, six-string ownership set, and all runtime gates remain unresolved. No kernel/managed source or artifacts were changed and no build or guest run was performed. Phase 35 remains incomplete; Phase 36 remains gated.**

The three NotFound diagnostic concatenations and synchronous serial borrow contract are confirmed from the live source. The other three retained strings cannot be attributed from the existing fallback-site ledger. The prior six fingerprints remain useful historical evidence, but the two captures used different missing keys and cannot answer the R11 question.

## Preflight and preservation

- Repository: `D:\dev\guideXOSUEFI`; branch: `main`.
- Starting and ending HEAD: `47839f17972d43eb22758bc8595d2c9bd80d8e47`; `git show -s --format=%s HEAD` returned `...`.
- Upstream: `origin/main`; local tracking state: 0 ahead / 0 behind.
- Root worktree status at preflight: two untracked reports, `Docs/PHASE35R10_NOTFOUND_STRING_LIFETIME.md` and `Docs/PHASE35R11_SAME_KEY_NOTFOUND_CLOSEOUT.md`; no modified or deleted root files. Both were preserved. The current `APP_MODEL_CONVERGENCE.md` state was not changed.
- Nested `out\rt` HEAD: `9d5a6a9aa463d6d10b0b0ba6d5982cc82f363dc3`; it has extensive pre-existing tracked modifications and untracked runtime sources. It was not changed.
- No topology operation, source/artifact mutation, build, guest run, or push occurred. Historical R1–R10 reports were preserved.

## Canonical key and prior evidence

The canonical key is `missing.phase35`, already used by the normal `CheckMissing` path in `UserManagedPersistentStorageProof/Program.cs`. The isolated compile-time NotFound branch in that same file still uses `missing.phase35r6`. This source-level difference is verified. The key was not changed because no coherent artifact rebuild and capture could be performed without overwriting shared protected outputs.

The latest retained R9 comparison remains:

| Slot | Full requester `missing.phase35` (length, bytes, hash) | Isolated `missing.phase35r6` (length, bytes, hash) | Exact fingerprint match |
|---:|---|---|---|
| 1 | (51, `0x80`, `94A661B97507E172`) | (51, `0x80`, `94A661B97507E172`) | Yes |
| 2 | (50, `0x80`, `ADE827DD03D3AE1F`) | (50, `0x80`, `ADE827DD03D3AE1F`) | Yes |
| 3 | (38, `0x68`, `7B75BBA2453D7996`) | (40, `0x68`, `8EBD40DD8DB592EE`) | No |
| 4 | (251, `0x210`, `2DB56378C1962B98`) | (260, `0x220`, `E8FCED6A2CF131F9`) | No |
| 5 | (1, `0x18`, `AF63AE4C86019E62`) | (1, `0x18`, `AF63AE4C86019E62`) | Yes |
| 6 | (36, `0x60`, `D82EC95D355533BA`) | (36, `0x60`, `D82EC95D355533BA`) | Yes |

This is 4/6 for the old unequal-key captures only. It is not an R11 same-key result. No same-key capture, exact-match count, allocation-order reconciliation, or R11 readiness marker exists.

## Three confirmed diagnostic temporaries

Live callsites are in `guideXOS/OS/PersistentFatBackend.cs`, inside `PersistentFatBackend.TryReadValue`'s `_fat.TryGetFileLength` failure branch (method begins at line 285 in this checkout):

| Purpose / content in prior capture | Producer expression | Consumer | Owner after return |
|---|---|---|---|
| Application ID; `(51, 0x80, 94A661B97507E172)` | `"P35_DIAG_APPLICATION_ID=" + applicationId` at line 316 | `Ring3Abi.Phase35DiagnosticMarker(string)` | Temporary concatenation has no local owner and no following Dispose; caller remains responsible |
| Relative key; `(40, 0x68, 8EBD40DD8DB592EE)` for old key | `"P35_DIAG_RELATIVE_PATH=" + relativePath` at line 318 | Same marker at line 319 | Temporary concatenation has no local owner and no following Dispose; caller remains responsible |
| Encoded FAT path; `(260, 0x220, E8FCED6A2CF131F9)` for old key | `"P35_DIAG_FILE_PATH=" + filePath` at line 320 | Same marker at line 321 | Concatenation is distinct from `filePath`; caller does not retain or dispose it. `filePath` itself is disposed in the method `finally` at line 367 |

The first marker call is at line 317. The exact prior string contents/hashes are recorded in the R10 report and derive from `missing.phase35r6` for rows 2 and 3. The `filePath` source object has a distinct, explicit cleanup path; that does not reclaim the concatenated marker argument.

The marker implementation is `Ring3Abi.Phase35DiagnosticMarker(string)` at line 256, which calls `Marker(string)` at line 249. `Marker` loops over `text.Length`, reads each character and writes it to UART port `0x3F8`, writes a newline, then returns. It stores no reference, calls no Dispose, and transfers no ownership. This proves a synchronous borrow contract: caller retains ownership and must dispose its temporary after output.

## Three unresolved strings

The length-251 object `(251, 0x210, 2DB56378C1962B98)` and length-1 object `(1, 0x18, AF63AE4C86019E62)` remain exact fingerprints only. The third remaining slot depends on a controlled same-key comparison and exclusion. Their producer, purpose, logical owner, transfer/alias behavior, last consumer, expected disposal, and free result are **unresolved**. R9 gives constructor fallback site `401` and caller address inside `RhpNewArray`; neither identifies the upstream creator. No identity is assigned by exclusion.

## Build and run boundary

The Phase 35 managed builder was inspected before any build invocation. It accepts only the fixed shared output path `out\dotnet\phase35-managed-persistent-read` and removes that directory recursively at lines 16–24 and 81–88. Its staging script writes every cohort payload and descriptor into shared `ramdisk_src\Native`. The normal `build.ps1` flow rebuilds and stages the kernel, ramdisk, EFI files, and has cleanup paths for shared `guideXOS\bin`, `guideXOS\obj`, `guideXOSBootLoader\x64`, and `ESP`. The R9 runner uses those shared files. There is no established isolated-output parameter covering the full managed-to-guest sequence, and the root plus nested runtime contain pre-existing work that must be preserved.

Therefore the accepted sequence (managed cohort, identities/descriptors, admission identities, kernel, ramdisk, EFI staging, audit, guest) was not started. In particular, no source instrumentation, canonical-key rebuild, diagnostic suppression, post-fix ownership change, or guest evidence is claimed. This preserves the checkout but leaves R11 incomplete.

## Gate results

- Same-key full vs isolated NotFound: **not run**; current source keys differ.
- Full/isolated six-string R11 tables: **unavailable**; only the historical unequal-key table above exists.
- Diagnostic marker/readiness: **not built or run**.
- Exact producer site IDs/caller-before-`RhpNewArray`: **not captured**; site 401 remains fallback.
- Success-vs-NotFound controlled allocation diff: **not run**.
- FAT missing-branch and backend NotFound-return instrumentation: **not run**.
- Suppression-only-three count; backend-only, raw ABI, public SDK, and full proof layer counts: **not run**.
- Post-fix NotFound, Success, full requester, five/25 lifetime slopes, invalid-free and fault counters: **not run**.
- 8-MiB allocation, reboot/seed-write, DSDT, artifact reproducibility/mutation, managed regressions, App Model regressions, and ordinary full build: **still outstanding**.
- Production vs diagnostic classification of the remaining three: **unresolved**. No cleanup or lifetime repair made; alias safety not established.

## Decision

- Outcome: **F**.
- Phase 35: incomplete; continue the ownership investigation.
- Phase 36: remains gated.
- Next step: establish a reviewed isolated build/staging lane that leaves shared payload, kernel, EFI, and nested runtime state intact; then instrument actual string producers, rebuild the complete cohort, and capture full and isolated `missing.phase35` requests with identical ABI inputs. Suppress only the three confirmed marker constructions for a separate attribution run. Do not change ownership until the remaining three have producer, owner, lifetime, and alias proof.
