# Phase 35R6 String Address Ledger Closeout

Date: 2026-10-05

## Outcome

**F — exact retained-string lifecycle is not fully reconciled.** The success-path defect discovered in an earlier instrumented run was eliminated by removing four transient diagnostic string constructions from the PersistentRead ABI logging path. R6 ledger runs after that change show zero retained request strings after one and two successful reads. The required isolated NotFound address table and remaining acceptance gates are not complete. No production ownership changes were made.

## Repository and preservation

- Repository: `D:\dev\guideXOSUEFI`
- Branch: `main`
- Starting and ending HEAD: `1d7d0a0b69145cdb2d786b367905e85f69e6429d`
- Upstream: `origin/main`, 0 ahead / 0 behind.
- User-specified starting HEAD `5fac894f3b70ab10491710fd8ea414a4d1f6a4ca` did not match actual HEAD.
- The root worktree contains accumulated Phase 35R changes plus current R6 work. No reset, clean, stash, rebase, branch, worktree, detach, commit, or push was performed.
- Nested `out\rt` at initial inspection was detached at `9d5a6a9aa463d6d10b0ba6d5982cc82f363dc3` and already dirty (53 tracked changes and untracked files).

## Instrumentation and diagnostic build

- Ledger feature marker: `STRING_LEDGER_READY=1`
- Ledger version: `STRING_LEDGER_VERSION=R6-1`
- Explicit diagnostic build mode: `Ring3Phase35R2` (NativeAOT publish followed by PE-to-ELF conversion; the later ordinary build helper also explicitly rebuilt this mode).
- Kernel SHA-256: `20B294BA86CF8180F732507AC181FCC49FDF4149C1F3A13F16C5D93588B0F46E`
- Map: `guideXOS\Kernel.map`
- The ledger uses bounded static scalar records and numeric site IDs. It records allocator-backed string object/run addresses, requested bytes, length, sequence, creation site/return address, Dispose events, free attempts/results, and live state. It emits post-request and post-process snapshots.
- Caller return-address values resolve inside `RhpNewArray`; they do not reliably unwind to the managed caller. Explicit callsite IDs are authoritative.

## Successful-read evidence

The successful-read evidence is in serial run `out\phase35q\serial-82d0f5bf66e54a5cb7366196c2218397.log`, using diagnostic kernel SHA `CBAAD163712A28FF04B71EAED35B4185D3DE9545A756EE0D8F3270E22E10B7A4`. It contained ready/version markers and one guest run with no-read, one-read, and two-read requesters. Post-request and post-process live counts for both reads were zero. Matrix results for all three passed. (That kernel preceded the later NotFound payload-dispatch/source additions; it contains the same R6 lifecycle instrumentation and success cleanup implementation.)

A later explicit rebuild produced SHA `20B294BA86CF8180F732507AC181FCC49FDF4149C1F3A13F16C5D93588B0F46E`. Its first matrix run, `out\phase35q\serial-7701101622314d968d6d709ff99590a7.log`, again passed no-read, one-read, and two-read with zero request-local live strings post-request and post-process. The fourth NotFound payload failed to stage. Subsequent attempts used stale/different RDSK/tar inputs and did not provide a valid NotFound result; see below.

The earlier pre-fix R6 ledger run matched four address-keyed retained strings to diagnostic marker creation sites: decimal read count (`0x191`), `RING3_PERSISTENT_READ_BYTES=32` (`0x192`), decimal final count (`0x193`), and `RING3_PERSISTENT_READ_END=1` (`0x194`). These dynamically constructed marker strings had no Dispose/free attempts. Their purpose was serial logging, not Persistent namespace or FAT paths. The source repair replaced those concatenations with bounded stack-buffer decimal output to UART. No lifetime/ownership change was made.

Because the complete final-kernel NotFound cohort did not run, this closeout does not publish the earlier four addresses as an authoritative final-kernel four-survivor table. They are preserved as pre-fix diagnostic evidence only. Exact hashes, lengths, run starts, and free state are present in the cited historical serial log if further audit is needed.

| Case | Result on a ledger-ready guest run | Live strings after request | Live strings after process |
|---|---|---:|---:|
| No read | PASS in SHA `20B294...` run | 0 | 0 |
| One successful read | PASS in SHA `20B294...` run | 0 | 0 |
| Two successful reads | PASS in SHA `20B294...` run | 0 per read | 0 per read |
| NotFound | Not proven; payload creation failed before process start | unavailable | unavailable |

## Address and ownership findings

- Decoded ABI path has creation site `101` and Dispose site `102`; source path disposes it in `finally`. Successful request snapshots show it does not remain live after cleanup. A complete NotFound address match has not been obtained.
- FAT split/substring components use site `301`; observed success-path components were disposed/freed.
- PersistentFatBackend composed FAT path uses site `201`, with matching cleanup instrumentation at `202`; successful fixture reads leave no request-local strings.
- The observed pre-fix retained strings were not ApplicationId encoding, persistent path components, or FAT traversal strings. No four-address final table is claimed after the production logging change because all four classes disappear.
- The ledger requires object/run start equality; the prior tracked string addresses in the pre-fix run matched allocator run starts. Post-fix runs had no survivors requiring a row-level equality table.
- No alias/last-use analysis for the four logging values was needed after removing their allocations; they were diagnostic-only values.
- `object[]` / `int[]` owner-0 arrays were not reconciled in this R6 continuation.

## Root cause and change

The four-page-per-read slope was produced by four temporary dynamically allocated strings in PersistentRead serial diagnostics. They were never disposed (Class A / diagnostic-only retention), and their intended lifetime was only the marker emission. The smallest repair was direct bounded numeric serial formatting in `Kernel\Misc\Ring3Abi.cs`. No general ownership abstraction or allocator sweep was introduced. The change removes the transient strings at their producer and has no string alias/use-after-free hazard.

This repair is verified with the R6 ledger for no-read, one-read, and two-read. Diagnostics-off slopes, five/25/100 lifetimes, contiguous 8 MiB, fault counters, and other Phase 35 acceptance gates were not completed.

## NotFound and staging limitation

The isolated NotFound case was added as payload kind 9 / phase32 dispatch kind 22 with identity flag `0xE4000000`. Its executable and descriptor were staged under `ramdisk_src\Native`; the managed artifact audit reported 41/41 descriptor agreement and 41/41 build/staging agreement. However, the guest serial run still emitted `File.ReadAllBytes returned NULL` for the matrix payloads and `PHASE32_IMAGE_NOT_STAGED` before requester execution. The R6 runner uses a UEFI boot path and loads `ESP\ramdisk.img`; its `-UseFreshlyBuiltKernel` branch does not rebuild/restage all boot inputs. An intermediate attempt also refreshed the GRUB tar, which is not the image used by this UEFI run. These attempts are excluded from lifecycle conclusions.

Required next step: stage the finalized full payload cohort to `ramdisk_src\Native`, regenerate `ManagedArtifactIdentities.g.cs`, build and audit `ramdisk.img`, copy it to `ESP\ramdisk.img`, stage the same freshly built kernel and bootloader, then perform a fresh guest boot. Keep the NotFound request isolated and capture its ledger and cleanup snapshots before making any ownership changes.

## Remaining gates

Not completed: same-build/run NotFound address table; two-read address table beyond post-fix zero-live counts; local public-SDK reject; exact-buffer raw ABI read; owner-0 array repetition; isolated NotFound string class and six-page reconciliation; diagnostics-disabled runs; five/25/100 lifetime slopes; contiguous 8 MiB; allocator/fault counter closure; full Phase 35R semantics and reset/reboot proof; DSDT regressions; complete managed artifact closure; regression cohort; ordinary full build.

- Phase 35 remains incomplete.
- Phase 36 remains gated.
