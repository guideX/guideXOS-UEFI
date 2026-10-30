# AMCONV2B Typed Folder Activation

**Outcome:** `AMCONV2B_TYPED_BACKEND_PARTIAL`

The C# application model now carries and launches typed filesystem-folder
requests through its existing shell service, launch request, application
factory, and instance lifecycle. The C# Root binding uses the active C#
`File.Instance` mount. No Server VFS path has been mapped to that mount because
the source trees do not establish that they name the same filesystem.

## Checkout and commits

- **Branch:** `APP_MODEL`
- **Starting HEAD:** `a7090512d534831680ba5b91a6fe78ded3554428`
- **Ending implementation HEAD:** `191f7eb` (`feat: add typed CSharp folder activation`)
- **Completion documentation:** committed separately after the implementation
  commit. The final repository HEAD is recorded in the task completion report.
- **Working tree at start:** clean; branch was already ahead of its remote by
  three commits.

## Filesystem evidence and binding

The C# UEFI boot path assigns a new `RdskFS` to `File.Instance` in
`guideXOS/Program.cs`. The static `guideXOS.FS.File.GetFiles` method delegates
to that instance. The C# shell registry has `gxos.shell.root` with an empty
path and no application handler; it does not identify a Server VFS mount.
`FileSystem` constructors and `AutoFS` can assign the process-global
`File.Instance`, so folder activation must not construct either in its
folder-specific path.

AMCONV2B binds **C# `gxos.shell.root`** to the active C# default mount at the
time the folder request is handled. The root value is represented canonically
as `/`; the backend path for `/` is the empty C# filesystem path. A typed
request can also carry another canonical path rooted in this same C# mount.
This is a C#-local binding. It does not map Server `gxos.shell.this-system`
(`/`) or Server `gxos.shell.documents` (`/Documents`) to C# storage.

The folder path is validated as an absolute slash-separated virtual path. The
validator rejects empty, relative, backslash, repeated-separator, trailing-
separator, control-character, invalid-surrogate, `.` and `..` forms. It checks
both the existing 1,024 UTF-16-code-unit request bound and the shared 1,024-byte
UTF-8 bound. It does not normalize malformed values or create missing
directories.

The backend checks the path before a shell-service launch creates an
application instance. For non-root paths it lists the parent directory and
requires an exact child entry marked `FileAttribute.Directory`. The factory
repeats the check immediately before window construction. Missing paths,
non-directories, and unavailable filesystem results return
`ResourceUnavailable`; malformed paths return `MalformedRequest`. The current
`FileSystem.GetFiles` API does not expose a typed permission result, so its
concrete probe cannot distinguish permission denial from other unavailable
results. The shared contract can preserve `PermissionDenied` if a future probe
can report it.

The filesystem is borrowed from the C# global mount. The folder window uses
that captured `FileSystem` reference for navigation and has
`ownsFileSystem=false`; closing the window disposes its cached `FileInfo`
entries but does not modify or dispose `File.Instance`. The probe also
disposes each returned entry and list. Ordinary Computer Files launches keep
their drive chooser, and the existing Hard Disk and USB drive factory branch
remains separate from folder handling.

The mount API has no lease or reference-count contract. A folder window keeps
the selected object if another subsystem later changes `File.Instance`, but a
future owner that disposes an old mount could invalidate that borrowed
reference. The source review found no disposal on the existing mount
replacement paths; a stable mount lease remains an architectural gap for a
later phase.

## Typed operations and supported targets

`ApplicationShellOperation` now distinguishes application launch, folder
open, device-volume open, system-panel activation, virtual-object open, and
system-action invocation. The adapter uses explicit `ShellObjectKind` cases;
unknown kinds do not fall through to a virtual or application-name launch.

`ApplicationShellOpenRequest.ForShellFolder` carries a shell ID and a separate
canonical folder value. The shell service accepts this form only for the C#
Root shell object, projects it with `ModernShellAdapter` to
`LaunchRequest.ForShellObject`, and routes it through
`ApplicationFactoryRegistry` to `gxos.builtin.files`. Folder targets never use
`LaunchRequest.ForFile` or file-association resolution. Normal Computer Files
application launches still have the application-launch operation and show the
drive chooser.

The existing AMCONV1 compatibility behavior remains:

- `gxos.shell.files`, `Files`, `File Manager`, `FileExplorer`, and the C#
  `File Explorer` alias open the Computer Files application.
- C# `Root` / `gxos.shell.root` opens the active C# filesystem root through a
  typed folder request. A typed C# Root request may carry another canonical
  folder value on the same C# mount.
- Device-volume values remain a distinct operation and are not interpreted as
  folder paths.
- System actions remain separate from folder, panel, and virtual-object
  targets.

These Server targets continue to return `UnsupportedTarget`:

- `gxos.shell.this-system` / `Computer`
- `gxos.shell.documents` / `Documents`
- Pictures, Music, Network
- Settings, Control Panel
- Desktop and Trash

Server Settings remains unsupported because Server registry and label-launch
handler identities conflict. No global alias was added between Server
`gxos.builtin.fileexplorer` and C# `gxos.builtin.files`.

## Build evidence and hosted validation

Before the source edit, the supported managed-artifact generator verified the
retained BUILD1E inputs: 41 managed artifacts, 41/41 descriptor agreements,
and 41/41 build-to-stage agreements. The provenance manifest's 92 distinct
phase source-input hashes all matched the current checkout. After the hosted
compile, the audit reported 41/41 compiled allowlist agreements. The generated
identity source SHA-256 remains
`D6DDF03058FA0690EF62F62FFFF016723C1514EB747FDF3D1D6AECD23CA682DD`.

The supported Release/AppModel command completed with **0 errors and 135
warnings**, matching the BUILD1E baseline of 135 warnings. No warnings came
from AMCONV2B source files. The compiled assembly SHA-256 is
`AD705B58F0E673C99149400DB1463A52E04B859DAAC2DDF151DBA61C53DBF432`.

Executed hosted checks:

- AMCONV2B folder-path contract: **34/34 runtime assertions passed**. The
  harness exercises the production path contract with a fake directory probe;
  it does not instantiate the UEFI filesystem or Computer Files window.
- AMCONV2B source-contract checks: **14/14 passed**.
- AMCONV1 source regressions: **9/9 passed**.
- Phase 33 shell wire regression: **passed**, preserving the 2,084-byte
  request and 16-byte response.
- Task Manager Memory Details regression: **passed**.
- UEFI desktop interaction hosted suite: **42 assertions passed** under the
  available x86 .NET 8 host.

App Model descriptor, instance, lifecycle, and shell-service diagnostics remain
`GUEST_PENDING`; they are wired into the UEFI diagnostic path and were not run
as hosted runtime tests. This phase did not claim guest acceptance. No QEMU or
QAC resources were used.

## Remaining gaps and AMCONV2C

AMCONV2B does not prove a Server-to-C# filesystem namespace binding. The
Server root and Documents values must remain unsupported until a source-owned
contract identifies the exact C# mount and confirms path behavior. Other
Server shell objects still lack matching C# panel or virtual-object backends.

AMCONV2C should establish a stable C# filesystem mount lease or equivalent
ownership contract, add a filesystem probe API that reports missing,
non-directory, permission, and I/O failures distinctly, and execute hosted
factory/lifecycle tests with an injectable filesystem. Those tests should
cover two independent folder launches, path context on each window, failed
factory cleanup, close behavior, and unchanged global mount state. Server
folder mappings should be considered only after the Server and C# owners
identify a precise shared filesystem binding.

## Repository protection

Source, test, and documentation changes are confined to
`D:\dev\guideXOSUEFI_APP_MODEL`. The supported managed-artifact audit also
wrote three CSV reports under
`E:\guideXOSUEFI_APP_MODEL_BUILD1E\reports`:
`managed-artifacts-amconv2b-preflight.csv`,
`managed-artifacts-amconv2b-after.csv`, and
`managed-artifacts-amconv2b-after-compile.csv`. The retained BUILD1E records
were left untouched. The two summarized full-build logs were also saved under
`C:\Users\guideX\AppData\Local\Temp` as
`guidexos-amconv2b-release-build.log` and
`guidexos-amconv2b-release-build-final.log`. The protected mainline, Server,
BUILD_INFRA, and MODERN_INPUT checkouts remained on their observed branches and
HEADs with no tracked-file changes; existing untracked reference artifacts
were left in place. No branch switch, merge, rebase, cherry-pick, push, QEMU,
or QAC operation occurred.
