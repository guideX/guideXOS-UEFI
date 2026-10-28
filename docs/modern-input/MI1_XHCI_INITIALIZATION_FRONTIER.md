# MI1 — xHCI Initialization Frontier

## Checkout and starting point

- Checkout: `D:\dev\guideXOSUEFI_MODERN_INPUT`
- Starting branch: `MODERN_INPUT`
- Starting HEAD: `829f8d565c10ad4d4aacfa96f8d49140dc768ba3`
- Starting worktree: clean
- Starting commit: `Add BM3B UEFI pointer fault capture diagnostics`
- Mainline checkout and release artifacts: not accessed or modified
- Recent MI/R5 reports in this checkout: none found under `docs/` or in the tracked tree

The requested R5 implementation is not present in this starting checkout. A tracked-source and history search found no xHCI driver, scratchpad/DMA implementation, or Modern Input report. Therefore R5 behavior described in the assignment could not be independently confirmed and was not recreated or assumed.

## Existing implementation inventory

| Stage | Finding in this checkout | Status |
|---|---|---|
| PCI xHCI discovery and MMIO validation | `PCI.Initialize()` populates PCI devices. The new UEFI diagnostic identifies class `0x0C`, subclass `0x03`, programming interface `0x30` candidates. No xHCI BAR/MMIO validation or mapping exists. | Partially implemented; hardware discovery unverified |
| Capability interpretation | No xHCI capability-register reader. | Missing |
| Firmware ownership handoff | EHCI has a legacy BIOS handoff path. No xHCI ownership handoff implementation exists. | Missing for xHCI |
| Halt and reset | No xHCI run/stop or reset sequence. | Missing |
| Page size and scratchpad setup | No xHCI page-size or scratchpad code is present. The assignment's ASUS two-buffer observation and R5 fix cannot be verified from this checkout. | Missing / unverified |
| DMA translation and lifetime | No xHCI DMA preparation, physical-address validation, or retained DMA buffers are present. No arbitrary kernel pointer is programmed by the added diagnostic. | Missing for xHCI |
| Device Context Base Address Array | No DCBAA implementation. | Missing |
| Command and event rings | No xHCI command/event ring implementation. | Missing |
| Controller start and ready state | No xHCI start or ready-state verification. | Missing |
| Root-hub ports | EHCI port scanning exists; no xHCI Supported Protocol or root-port handling exists. EHCI initialization is not called by the UEFI entry path. | Missing for xHCI |
| USB enumeration | Existing `EHCI.InitPort()` performs descriptor/configuration requests. `USB.SendAndReceive()` routes only USB 2.0 devices to EHCI. The UEFI entry path does not start this stack. | Partial EHCI implementation; UEFI/xHCI path missing |
| HID identification and reports | HID boot keyboard/mouse parsing exists and is called by the EHCI device dispatcher. There is no xHCI transfer path, and the UEFI entry path does not initialize HID/USB. | Partial; xHCI integration missing |

The UEFI entry path in `Kernel/Misc/EntryPoint.cs` retires firmware input after ExitBootServices, initializes native PS/2 where available, and enumerates PCI. It does not call `EHCI.Initialize()`, `Hub.Initialize()`, `HID.Initialize()`, or `USB.StartPolling()`. The xHCI frontier is therefore before controller MMIO access, ownership handoff, or DMA setup.

## Confirmed R5 functionality

None can be confirmed from the assigned checkout. The source tree and available history do not contain the stated R5 xHCI code or its reports. This is a checkout/source-baseline discrepancy, not evidence that the R5 implementation failed on hardware. No R5 behavior was reimplemented or represented as working.

## Earliest unverified or failing stage

The earliest demonstrated boundary is after generic PCI enumeration and before xHCI controller initialization. On UEFI boot, xHCI PCI candidates can now be listed by BDF and vendor/device ID, but this kernel has no xHCI driver to select a controller or validate/map its BAR. No xHCI MMIO or DMA registers are touched.

The current report distinguishes two possible observations:

- `[USB][XHCI] candidate ...` followed by `stop=driver-unavailable stage=pci-discovery-complete`: an xHCI PCI function was enumerated, but there is no driver.
- `stop=no-controller-found stage=pci-discovery-complete`: the existing PCI scan produced no xHCI-class candidate. This does not prove no physical xHCI controller exists; PCI scan coverage and machine evidence remain to be checked.

The cause beyond this boundary cannot be established without the missing driver baseline or physical-machine diagnostics.

## Production change

Added `USB.ReportXhciInitializationFrontier()` and call it only in the UEFI path after `PCI.Initialize()` completes. It emits one line per matching PCI xHCI function and a bounded stop summary. The method is read-only: it does not read or write controller BARs, perform ownership handoff, allocate DMA, or start USB polling.

## Safety constraints preserved

- No controller MMIO is mapped or accessed by the new diagnostic.
- No address is assumed DMA-capable and no DMA buffer is prepared.
- No firmware input protocol is reused after ExitBootServices.
- No EHCI/xHCI initialization behavior or normal legacy boot flow was changed.
- Unsupported xHCI initialization is reported as unavailable rather than guessed.
- No QEMU guest was launched and no physical hardware was tested.

## Hosted validation

No xHCI hosted tests were available or run: the xHCI implementation and test fixtures are absent. The existing `Tests/` tree contains no xHCI test harness, so DMA address/alignment/overflow, scratchpad, and controller-failure regression cases cannot be exercised against this checkout.

`dotnet build guideXOS/guideXOS.csproj -p:SkipISO=true -p:BaseOutputPath=artifacts/mi1-validation-20261028/bin/ -p:NativeOutputPath=artifacts/mi1-validation-20261028/native/` compiled the kernel project successfully with 0 errors and 142 warnings. The standard build prerequisite `guideXOS/obj/ManagedArtifactIdentities.g.cs` was absent. The repository generator could not create it because `out/dotnet/phase29-managed-notification/phase29-managed-notification-build.json` is missing. For the compile check only, a temporary stub for that unrelated generated class was supplied under the newly created ignored `guideXOS/obj/`; it was not used as a production artifact and was removed after the build. The kernel project was not published with NativeAOT, no ISO was packaged, and no guest was booted. The isolated managed build output remains under `guideXOS/artifacts/mi1-validation-20261028/`.

`git diff --check` passed. No xHCI hardware behavior was validated.

## Physical-hardware requirements

Physical qualification remains pending on SARAH_DT, AIDA_LPT, ASUS VivoBook, and other UEFI x86-64 systems. The new line should be captured over the available boot diagnostics/serial output to establish whether PCI enumeration sees the controller. This diagnostic alone does not qualify input, controller initialization, enumeration, or HID reports.

## Recommended MI2 frontier

First reconcile the MODERN_INPUT source baseline with the reviewed R5 xHCI implementation and its report/tests, since neither is present at the MI1 starting commit. Then add hosted tests for xHCI capability parsing and DMA address/alignment/overflow rejection before enabling any controller register writes. Keep hardware bring-up gated until those safety tests pass and an identified physical-machine run can capture the controller stage results.

## Final run record

- Outcome: `MI1_DIAGNOSTIC_FRONTIER_ESTABLISHED`
- Changed source and report: `Kernel/Misc/EntryPoint.cs`, `Kernel/Misc/USB.cs`, and this report
- ISO produced: no (ISO packaging is disabled for the isolated build)
- QEMU guest launched: no
- Physical hardware tested: no
- Push status: not pushed
