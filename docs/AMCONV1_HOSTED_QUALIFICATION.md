# AMCONV1 Hosted Qualification Closeout

**Date:** 2026-10-28
**Outcome:** `AMCONV1_BUILD_BLOCKED`
**Branch:** `APP_MODEL`
**Starting HEAD:** `592262a611c24a5f0c5befccf54c203e7c41b558`

## Result

AMCONV1 shell compatibility source checks and the Phase 33 wire-layout
regression passed. Hosted C# compilation remains blocked before C# compilation
because the required generated identity source and its validated managed proof
build evidence are absent. The safe recovery path needs a full managed NativeAOT
proof rebuild, so it was not attempted. No AMCONV1 source corrections were
needed.

## Generated dependency investigation

`guideXOS/obj/ManagedArtifactIdentities.g.cs` is emitted by
`Tools/ManagedArtifacts/managed_artifacts.py --stage generated`. The supported
`build.ps1` pipeline builds and stages managed proof artifacts before invoking
that generator. The generator does not derive identities from executable files
alone: it validates every staged EXE/GXMI pair against the final build output
and build record, including the executable length and SHA-256, descriptor
flags/hash, timestamp normalization, and byte-for-byte build/stage agreement.
It covers 41 proof variants across Phases 29–35.

Phase 29 metadata is produced by
`Tools/Phase29/build_phase29_managed_notification.ps1`. It uses the Phase 23
runtime pack, the Phase 26 managed proof builder, and
`UserManagedNotificationProof` sources to build five modes. It records each
final executable path, length, and SHA-256 in
`out/dotnet/phase29-managed-notification/phase29-managed-notification-build.json`.
`Tools/Phase29/stage_phase29_image.ps1` consumes that record to stage each
executable and generate its GXMI descriptor.

The APP_MODEL checkout contains tracked staged proof payloads and descriptors,
but has none of the Phase 29–35 build records, no Phase 23 runtime pack, no
`out/rt` pinned runtime source checkout, and no Phase 21–23 source-patch
evidence. Its `out` directory contains only the isolated AMCONV1 compile
artifacts. The required Phase 29 JSON exists in the protected `main` checkout,
along with Phase 30–35 records, but those records point to build outputs that
do not byte-match APP_MODEL's staged payloads. Passing that metadata location
to the supported generator failed closed on the Phase 29 success payload.
The external checkout was read-only throughout.

The top-level `build.ps1` contains a supported rebuild sequence, including the
Phase 19–23 runtime preparation and Phase 27–35 proof build/stage steps. The
Phase 21–23 documentation describes patching the pinned runtime source, but
assumes the `out/rt` checkout is already present. There is no smaller
standalone identity-source bootstrap from a clean checkout. Restoring the
pinned CoreCLR source cache and its accepted patch evidence, producing the
Phase 23 pack, then rebuilding and restaging all 41 variants is a substantial
unrelated reconstruction. It was not undertaken. No identities or metadata
were fabricated, and no validation was bypassed.

## Validation

- **Hosted C# compile:** blocked. The isolated Release/AppModel compile reached
  Roslyn and failed with `CS2001` for
  `guideXOS/obj/ManagedArtifactIdentities.g.cs`. The supported identity
  generator could not proceed because local build records are absent and the
  available external record failed its build/stage equality check.
- **AMCONV1 source contract checks:** PASS, 9/9. Checked Server ID and aliases,
  the retained C# shell/app IDs, canonical request path, alias preservation,
  error codes, typed-action guard and service routing, repeated resolution,
  and App Model descriptor/instance/lifecycle diagnostic wiring. These are
  source checks, not execution of the C# self-tests.
- **Phase 33 shell wire regression:** PASS. Request sizes match at 2084 bytes,
  response size is 16 bytes, and `PHASE33_WIRE_LAYOUT_PASS=1`.
- **App Model, lifecycle, instance-registry, and Phase 33 guest self-tests:**
  `GUEST_PENDING`. They are wired into the UEFI AppModel/diagnostic path; this
  checkout has no standalone hosted test runner for them. No guest was launched.
- **NativeAOT/freestanding review:** static review found the compatibility
  mapping uses bounded string comparisons and the existing shell registry,
  launch request, and factory path. It adds no reflection, dynamic loading,
  unsafe code, or platform service dependency. Compilation remains unverified.
- **UEFI/Legacy compile-time contracts:** the AMCONV1 commit changes only
  `ModernAppModel.cs`, `ApplicationShellServices.cs`, and its baseline report;
  it does not change project configuration, platform bootstraps, or compile
  selectors. Source boundaries are unchanged, but compilation could not verify
  them.
- **Whitespace:** `git diff --check` passed for the closeout changes.

## Scope and next step

No QEMU or guest was run. No branch switch, merge, rebase, cherry-pick, push,
protected mainline edit, Server edit, or source correction was made. Preserve
the existing staged payloads and ignored build artifacts.

AMCONV1 is not hosted-accepted. Before implementation work on AMCONV2, restore
the matching, validated managed-artifact build chain and complete the hosted
compile. The proposed narrow AMCONV2 shell-target design can remain a planning
item until that gate is green.
