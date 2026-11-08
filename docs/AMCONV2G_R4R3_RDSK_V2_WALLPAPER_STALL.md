# AMCONV2G-R4R3 — RDSK v2 Wallpaper Startup Stall

| Field | Result |
|---|---|
| Outcome | `AMCONV2G_R4R3_STALL_REPAIRED_HOSTED` |
| Branch | `APP_MODEL` |
| Starting HEAD | `dc0bdec67a9622a86dfaaf4307ecf3980054cc27` |
| Ending source/test HEAD | `c9ea291b1daff008021e60c682bbccdd1baa6727` |
| Candidate package root | `out/amconv2g-r4r3/packages/` |
| Guest status | New packages are offline qualified; guest confirmation is pending |

The source-level cause is established as repeated whole-image CRC validation on the RDSK v2 startup path. The wallpaper is read and decoded successfully; the expensive path begins when desktop icon initialization follows. The repair validates the immutable, read-only image once per mount and retains the validation result for subsequent file operations. Candidate A and B both pass hosted regressions and offline package validation. This does not change the historical guest outcomes or claim that the new packages boot.

The source/test commit is `c9ea291b1daff008021e60c682bbccdd1baa6727` (`fix: cache RDSK image validation per mount`). This report is a follow-up documentation-only change.

## Preserved R4R2 guest results

The accepted historical record remains [AMCONV2G-R4R2 guest acceptance results](AMCONV2G_R4R2_GUEST_ACCEPTANCE_RESULTS.md), with evidence under `out/amconv2g-r4r2-20261107/`. It has not been rewritten.

The earlier [R4R1 services and QAC recovery report](AMCONV2G_R4R1_SERVICES_AND_QAC_RECOVERY.md) and [RDSK v2 format and validation report](AMCONV2F_RDSK_V2_FORMAT_AND_VALIDATION.md) remain the implementation and services context for this run.

- **Candidate A, RDSK v1:** passed the guest scenarios exercised in R4R2, including the production Computer Files chooser, Open Root, two separately owned Root windows, navigation from a surviving window, and mount-lease release. This remains a historical pass for those scenarios only.
- **Candidate B, RDSK v2:** reached kernel startup, mounted the filesystem, initialized the framebuffer, found nine wallpapers, then stopped serial output after `[BACKGROUND] loaded Backgrounds/tronporche.png`. It remained on a teal framebuffer for more than four minutes and did not reach AppRuntime or a usable desktop. This remains a historical startup stall.
- The R4R2 package source revision was `a6ce2d2ac977c9b6585584827abf5ffbdbff0fd2`; the accepted loader hash was `C185B725E46D5D2E0E6F441D69B3578AD987FEC238152F60FD59657069DE78C9`. The historical shared kernel ELF hash was `24FA8C323334614FA4800643575BEE1F77707AB2BCEB87BBD46C2EA62BD2BB9F`. Candidate A's prior RDSK hash was `B11822A04AE5BE3A0C858851ECD3BC3C2C8EDCA7C9A4157A1753E0E8C080785E`; Candidate B's was `18AC75ABEA193CD2F80E08D13A0558646303580131EC6847000E9CEAD7D15BD4`.
- The configured UEFI diagnostic mode is AppRuntime.

## Execution frontier and cause

`BackgroundRotationManager.Initialize()` in `guideXOS/GUI/BackgroundRotationManager.cs` first lists `Backgrounds/`, chooses a wallpaper, and calls `TryLoadBackground`. That calls the mounted filesystem's `ReadAllBytes`, then `DecodeAndScale`. In UEFI mode the method calls the production `PngLoader.Load`, checks the decoded image, resizes it to the framebuffer dimensions, and returns the scaled image. `PublishWallpaper` assigns it to `Program.Wallpaper`. Only after that succeeds does the manager write `[BACKGROUND] loaded ...`.

Therefore the last marker proves that the path was selected, a record was resolved, the declared bytes were copied, the decoder was entered and returned successfully, scaling completed, and the wallpaper object was published. It does **not** prove that the framebuffer drew the wallpaper or that desktop rendering completed. In `SetupIcons`, the next expected serial milestone is `[ICON_GRAPHICS] start=...`, followed by `[PNG] image assets initialized` and `[DESKTOP] real icon assets enabled`; later the desktop draw path can emit `[ICON_GRAPHICS] desktop-drawn=...`.

The next startup operation is `RefreshCachedIcons()`. Its first icon property access triggers the `Icons` static constructor. That eagerly creates five `IconsPrivate` sets (sizes 16, 24, 32, 48, and 128). Each set reads 19 PNGs, so icon construction makes 95 filesystem reads before the `[ICON_GRAPHICS]` milestone.

Before this repair, the RDSK reader called `RdskFSDirectoryInspector.ValidateImage` for every `GetFiles`, `ReadAllBytes`, and mount inspection. For v2, validation computes CRC32 over the complete image with eight bit steps for every byte. Candidate B is 56,435,018 bytes, so the 95 icon reads alone requested 5,361,326,710 bytes of repeated image scanning (about 42.9 billion CRC bit steps), in addition to earlier wallpaper discovery/read work. The stop occurs exactly where this source path predicts prolonged CPU-bound startup work. There is no evidence that the guest was stuck in PNG decode or wallpaper drawing.

The evidence classification is:

- **RDSK v2 byte-read defect:** not reproduced. The authentic production reader returned the exact accepted wallpaper extent and digest on repeated reads, and adjacent-file reads remained stable.
- **PNG decoder interaction:** not reproduced. The unchanged production decoder accepted the exact Candidate B bytes in the hosted process and returned 1024×768 pixels.
- **Allocation pressure:** no oversized wallpaper allocation was found. The decoded ARGB image is 3,145,728 bytes; a 1280×800 ARGB framebuffer is 4,096,000 bytes. These bounded calculations do not prove total guest heap availability.
- **Startup sequencing/performance:** established source cause. The RDSK image-wide CRC was repeated for every icon read before the next startup milestone.
- **Rendering:** not reached at the observed frontier; no rendering failure is inferred.
- **Diagnostic visibility:** still limits guest-level certainty about the exact instruction at timeout. No guest run was authorized for R4R3.

## Wallpaper payload comparison

Both accepted ramdisks were inspected with an independent bounded parser and PNG chunk/CRC checker. The parser verified the image extent and every listed record, then extracted the wallpaper independently of `RdskFS`.

| Candidate | RDSK | Record | Offset | Length | Extracted SHA-256 |
|---|---:|---:|---:|---:|---|
| A | v1 | 18 | 16,251,072 | 1,177,051 | `90B8E521205D2052E04560A623ADFA96B058E5A9EE5C489030BE78077FCEFE08` |
| B | v2 | 32 | 17,648,825 | 1,177,051 | `90B8E521205D2052E04560A623ADFA96B058E5A9EE5C489030BE78077FCEFE08` |

Each image contains 600 records with the same pathname set, and all 600 payloads are byte-identical. The wallpaper's declared extent is within each image. Its PNG signature is valid; IHDR, pHYs, iTXt, cHRM, IDAT, and IEND chunks have valid CRCs, and the IDAT zlib stream inflates to 2,360,064 bytes for a 1024×768 image. No wallpaper replacement or payload edit was made.

The production Candidate B hosted test independently checked the v2 reader's record at offset 17,648,825, length 1,177,051, repeated full-length reads and the extracted SHA-256. V1's implicit-directory behavior and v2's explicit-directory and empty-directory behavior remain covered by their existing suites.

## RDSK v1/v2 read behavior

The reader retains separate v1 and v2 operations after common image/version validation. V1 validates each bounded pathname and payload extent in its legacy sequential record table, permits historical trailing bytes, and has no whole-image CRC. `GetV1Files` infers directory parents from paths; `ReadV1File` resolves the full stored pathname. V2 requires the declared image length to equal the captured length, validates its bounded explicit record table, directory parents and contiguous payload extents, then checks CRC32 over the whole image. `GetV2Files` enumerates explicit child records; `ReadV2File` resolves an explicit file record and copies its bounded payload. Both read paths return owned byte arrays. The per-mount cache wraps the common validation step without changing either reader or directory enumeration behavior.

## Repair and regressions

`RdskFS` now keeps a per-mount validation result protected by a lock. Its image pointer and captured size are fixed for that mount, and it has no write operation, so an accepted or rejected full-image validation remains valid for the mount's lifetime. `GetFiles`, `ReadAllBytes`, and `InspectDirectory` reuse that result. The standalone directory inspector still performs full validation; an internal `InspectValidated` entry point preserves the typed path and status checks after the owning mount has validated the image.

The repair retains complete-image CRC32 validation, v1/v2 format dispatch, strict ASCII path handling, typed inspection results, and mount leases. It adds no new RDSK format behavior. The exact Candidate B image test counts one full validation per mount and covers containing-directory enumeration, repeated wallpaper reads, a neighboring thumbnail before and after, retained reads after replacement, and deferred disposal at final lease release.

The hosted regression compiles `Kernel/Misc/PngLoader.cs` unchanged. `PngLoaderHostedImage.cs` supplies only a minimal host-side `System.Drawing.Image` storage type; it does not mock PNG parsing, inflation, filtering, or decoding. The exact Candidate B payload decoded successfully, and dimension/pixel-count and buffer-size calculations passed.

## Hosted verification

| Check | Result |
|---|---:|
| AMCONV1 source compatibility | 9/9 |
| AMCONV2B source contract | 25/25 |
| AMCONV2B/C folder and mount lifecycle | 71 assertions, 0 failures |
| AMCONV2D FAT inspection | 11 assertions, 0 failures |
| AMCONV2E RDSK v1 inspection | 32 assertions, 0 failures |
| AMCONV2F source contract | 10/10 |
| AMCONV2F generator and production RDSK/PNG suite, exact Candidate B supplied | 12 generator assertions; 69 RDSK assertions, 0 failures |
| AMCONV2G root UI routing | 21/21 |
| AMCONV2G services diagnostics | 11 assertions, 0 failures |
| AMCONV2G managed packaging tests | 4/4 |
| Desktop interaction | 42 assertions passed |
| Phase 33 wire regression | Passed; request 2,084 bytes, response 16 bytes |
| Task Manager Memory Details | Passed |
| Release AppModel build | 0 errors, 137 warnings; matches the prior baseline |
| Release AppRuntime build | 0 errors, 141 warnings; matches the prior baseline |

The RDSK test's two existing `PngLoader.cs` compiler warnings (CS0162 and CS0219) remain. Other source suites and their established warnings are unchanged. D/E/F test artifacts were directed to `out/amconv2g-r4r3/hosted-artifacts/`; the pre-existing untracked `bin/obj` outputs in the test directories were left in place.

## NativeAOT and offline packages

Because the RDSK kernel source changed, Release AppModel/AppRuntime builds and the supported NativeAOT pipeline were run. NativeAOT ran in an isolated copy under `out/amconv2g-r4r3/package-pipeline/`. The native PE linked successfully with zero unresolved-symbol diagnostics; no linker force option or symbol stubs were added. The existing map-based `KMain` entry selection was used. The PE is AMD64 with eight sections (4,872,704 bytes; SHA-256 `2E8D2F2BD97F0D36D9B983471F920A6CB8F1A4B98427A0666AB7604C9B4EBB97`). `Kernel.map` is 1,518,655 bytes (SHA-256 `08F18E3272CFD86D6EF02E932E5D88FB660C33D9EE9B27E18C6293597148B9B6`) and contains `KMain` at `0x1004DF78`.

The supported converter produced a 4,894,720-byte ELF64 little-endian `ET_EXEC` for x86-64. The ELF has eight aligned `PT_LOAD` segments, its entry `0x000000001004DF78` lies in an executable load segment, and it has no dynamic or interpreter segment. Kernel SHA-256: `B4625386E177A0749D647E7FD3A18C06FCE7D5C5D82CE8E838100D56D9F8AE05`. The build script's optional ISO step was skipped because the local pipeline lacks `boot/grub/i386-pc/eltorito.img`; the required ESP artifacts were built and independently checked.

Candidate A was assembled first with the fresh kernel, accepted loader, and retained accepted RDSK v1 image. Candidate B then reused the same fresh kernel and loader with the retained accepted RDSK v2 image. The old R4R1/R4R2 candidate packages and manifests were not changed. New manifests are under `out/amconv2g-r4r3/packages/`:

| New package | RDSK hash | Shared kernel hash | ESP tree hash | Offline validation |
|---|---|---|---|---|
| Candidate A, v1 | `B11822A04AE5BE3A0C858851ECD3BC3C2C8EDCA7C9A4157A1753E0E8C080785E` | `B4625386E177A0749D647E7FD3A18C06FCE7D5C5D82CE8E838100D56D9F8AE05` | `D8C516DE5637A0F825E140959F160013C030DE233EAF27E7037EFC9060A38CE9` | PASS |
| Candidate B, v2 | `18AC75ABEA193CD2F80E08D13A0558646303580131EC6847000E9CEAD7D15BD4` | `B4625386E177A0749D647E7FD3A18C06FCE7D5C5D82CE8E838100D56D9F8AE05` | `D29CD1DA68A9262A49AEB584F0BB2D6355F81305BAF28BAC748DC9CA59A25FB9` | PASS |

Both package validators passed both manifest-generation checks and `--check-only` verification. The accepted EFI loader hash is unchanged at `C185B725E46D5D2E0E6F441D69B3578AD987FEC238152F60FD59657069DE78C9`. Each ESP contains exactly the loader, kernel, and ramdisk. Managed descriptor/build/staging/ramdisk/allowlist audits passed 41/41 for each candidate. The manifests record that the package is ready for guest testing and explicitly make no guest-boot claim.

## Next guest acceptance

R4R3 used no QEMU, QAC reservation, live guest, or physical hardware. Guest confirmation remains pending. In a separately authorized phase, use the supported default one-child reservation workflow and do not call unsupported `finishChild` operations or repeatedly rerun a stalled guest.

Prioritize Candidate B's observable startup milestones in order: UEFI loader, kernel startup, RDSK v2 mount, wallpaper lookup, read completion, PNG decode completion, rendering completion, AppRuntime initialization, and usable desktop. Only after desktop readiness, exercise Computer Files launch, production Open Root, explicit directory and empty-directory navigation, multiple window ownership, instance termination, and mount-lease release. Because the kernel is shared, include a Candidate A compatibility smoke run against the new package before proposing Candidate B for guest acceptance. The historical Candidate A lifecycle result remains useful context, but it does not qualify this new shared kernel.

## Repository completion

Only `D:\dev\guideXOSUEFI_APP_MODEL` was modified. The branch remained `APP_MODEL`; there was no branch switch, merge, rebase, cherry-pick, push, or change to protected checkouts. Existing untracked test outputs were preserved. The new source/test commit is recorded above; the documentation is committed separately after review.
