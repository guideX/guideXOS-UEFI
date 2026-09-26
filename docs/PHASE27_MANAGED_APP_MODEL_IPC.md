# Phase 27: Managed App Model IPC from Ring 3 NativeAOT

Phase 27 is the first useful managed user-mode proof. A GUIDEXOS NativeAOT
program executes at CPL3, calls the existing operation-5 Ring 3 service ABI,
requests App Model System Information, validates the copied response in managed
code, and returns `27` only after the response has been consumed successfully.

The Phase 26 `Main() => 42` payload remains intact as the control artifact. No
Server or Legacy code is changed.

## Wire contract

Both managed payloads use explicit, `Pack = 1` value-only structures. No
managed reference, managed string, array, or kernel pointer crosses the ABI.

| Structure | Size | Fields |
| --- | ---: | --- |
| `GuideXosServiceRequest` | 32 | version, service id, operation id, request length, user response address, response capacity, reserved |
| `GuideXosSystemInformationResponse` | 128 | version, size, uptime, total memory, memory in use, thread count, CPU percentage, three bounded lengths, reserved, OS/version/architecture byte arrays |

The request selects service `SystemInformation` (`3`) and snapshot operation
`1` through Ring 3 operation `5` (`ServiceRequest`). The native helper is
`guidexos_pal_service_request`; it forwards to the existing
`guidexos_pal_syscall5(5, request, requestLength, 0, 0, 0)` transition. It is a
target-native C ABI helper, not a Windows DLL import or a privileged managed
shortcut.

## Ownership and validation

`Program.Main` places both structures in stack/local unmanaged-call storage and
passes only their addresses for the immediate transition. The kernel first
validates the readable request range, copies the request into kernel storage,
validates the writable response range and capacity, derives the current
scheduled process, derives its diagnostic `ApplicationInstance`, creates and
validates the `ApplicationServiceContext`, executes the existing System
Information backend, serializes a fixed-width response, and copies it out.
The kernel retains no user pointer after the call.

The managed wrapper validates response version `1`, size `128`, reserved and
bounded-string fields, the x64 architecture marker, and the memory invariant
`MemoryInUseBytes <= MemorySizeBytes`. The managed `Validate` step additionally
checks nonzero total memory, nonnegative thread/CPU values, CPU `<= 100`, and
the x64 platform invariant. Success is therefore `27`; request/transport
failure is `21`; managed validation failure is `22`.

The custom AOT path can zero scalar members of the generic snapshot projection.
The existing backend remains the authority for identity, capability, and text;
the ABI serializer recovers only the scalar counters from the same kernel
allocator/thread-pool sources when that projection is zero. The bounded
`PHASE27_RESPONSE_SCALAR_FALLBACK` marker records this condition.

## Payloads and static evidence

The distinct success and controlled-invalid-request payloads are staged as:

| Payload | Size | SHA-256 | Descriptor flags |
| --- | ---: | --- | --- |
| `ramdisk_src/Native/guideXOS.Phase27ManagedServiceProof.exe` | 705,536 | `FD5D7D68B0FE5E21D632B0D0F654360D544A5B907E96AFA0D223C3E1341088B4` | `0x3F` |
| `ramdisk_src/Native/guideXOS.Phase27ManagedFailureProof.exe` | 705,536 | `A0FE36338AFF2F4EA610A5EA5A23AF321810E46EE20DD6D3A3648241DB6F0A11` | `0x7F` |

Both images use base `0x401000000000`, image size `0xC9000`, entry RVA
`0x1430`, and managed entry VA `0x401000001430`. In the success map,
`Program.Main` is RVA `0x65C80`, VA `0x401000065C80`. The native PAL helper is at
RVA `0x1610`. Static verification passes with zero direct kernel imports,
zero Windows/foreign imports, no relocations, no TLS directory, and zero RWX
sections. Descriptor flag/hash/artifact-byte mutations are rejected.

The machine-readable verifier results are:

- `out/dotnet/phase27-success-verification-final.json`
- `out/dotnet/phase27-failure-verification-final.json`
- `out/dotnet/phase27-success-inspection.json`
- `out/dotnet/phase27-failure-inspection.json`

## Runtime evidence

The authoritative Ring3Phase27 QEMU run is
`bin/uefi-run-logs/phase27-authoritative-final.serial.log`. It records:

- bootstrap hash match and validated Phase 27 selection;
- four successful service lifetimes, each with service request, response copy,
  timer preemption/resume, normal exit, and cleanup;
- `PHASE27_REPEATED_LIFETIMES=4`;
- `PHASE27_TYPED_FAILURE_RESULT=21` and `PHASE27_TYPED_FAILURE_PASS=1`;
- stale ApplicationInstance and stale service-context rejection;
- `PHASE27_MANAGED_CLEANUP_BALANCED=1`,
  `PHASE27_BOOTSTRAP_CLEANUP_BALANCED=1`, and
  `PHASE27_PROCESS_CLEANUP_BALANCED=1`;
- zero stale user threads, mappings, and service contexts;
- no CPU fault, page fault, or general-protection marker.

Phase 25 static control verification passes, and the preserved Phase 26 image
remains the known-good managed control with SHA-256
`303B1822D120121B8453F5847E7C071ED6699BC0505A92894D3A2D84FB748E0F`.

## Build and verification

The full Phase 27 build is:

```powershell
& .\build.ps1 -SkipBootloader -UefiDiagnosticMode Ring3Phase27
```

The payload-only rebuild/staging and static verifier are under
`Tools/Phase27`. The kernel path remains the Phase 14 ABI path; Phase 27 adds
only the managed caller, its target-native PAL helper, distinct descriptors,
loader allowlists, lifecycle proof, and diagnostics.

Phase 28 should begin only after this bounded System Information proof. GUI,
filesystem, networking, general P/Invoke, arbitrary managed threads, and a
managed App Model SDK remain out of scope.
