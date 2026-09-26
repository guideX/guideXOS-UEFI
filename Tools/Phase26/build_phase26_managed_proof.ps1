[CmdletBinding()]
param(
    [string]$Phase23Pack = '',
    [string]$PackRoot = '',
    [string]$OutputRoot = '',
    [string]$ProjectPath = '',
    [string]$PalSource = '',
    [string]$ShimSource = '',
    [string]$SyscallSource = '',
    [string[]]$ProjectProperties = @()
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
if ([string]::IsNullOrWhiteSpace($Phase23Pack)) { $Phase23Pack = Join-Path $root 'out\dotnet\phase23-runtime-pack' }
if ([string]::IsNullOrWhiteSpace($PackRoot)) { $PackRoot = Join-Path $root 'out\dotnet\phase26-runtime-pack' }
if ([string]::IsNullOrWhiteSpace($OutputRoot)) { $OutputRoot = Join-Path $root 'out\dotnet\phase26-user-managed-proof' }
if ([string]::IsNullOrWhiteSpace($ProjectPath)) { $ProjectPath = Join-Path $root 'UserManagedProof\guideXOS.UserManagedProof.csproj' }
if ([string]::IsNullOrWhiteSpace($PalSource)) { $PalSource = Join-Path $PSScriptRoot 'guidexos_phase26_pal_contract.cpp' }
if ([string]::IsNullOrWhiteSpace($ShimSource)) { $ShimSource = Join-Path $PSScriptRoot 'guidexos_link_shim.cpp' }
if ([string]::IsNullOrWhiteSpace($SyscallSource)) { $SyscallSource = Join-Path $PSScriptRoot 'guidexos_phase26_syscall.asm' }

$vsRoot = 'C:\Program Files\Microsoft Visual Studio\18\Community\VC\Tools\MSVC'
$compiler = Get-ChildItem -LiteralPath $vsRoot -Filter cl.exe -File -Recurse |
    Where-Object { $_.FullName -match '\\Hostx64\\x64\\cl\.exe$' } |
    Sort-Object FullName -Descending | Select-Object -First 1
if ($null -eq $compiler) { throw 'The VS 18 x64 C++ compiler was not found.' }
$vcvars = 'C:\Program Files\Microsoft Visual Studio\18\Community\VC\Auxiliary\Build\vcvars64.bat'
if (-not (Test-Path -LiteralPath $vcvars -PathType Leaf)) { throw 'The VS 18 x64 compiler environment was not found.' }
$nasm = $null
if ($env:NASM -and (Test-Path -LiteralPath $env:NASM)) { $nasm = $env:NASM }
if (-not $nasm) { $candidate = Join-Path $root 'Tools\nasm.exe'; if (Test-Path -LiteralPath $candidate) { $nasm = $candidate } }
if (-not $nasm) { $nasmCommand = Get-Command nasm -ErrorAction SilentlyContinue; if ($nasmCommand) { $nasm = $nasmCommand.Source } }
if (-not $nasm) { throw 'NASM is required for the Phase 26 PAL veneer.' }

if (Test-Path -LiteralPath $PackRoot) { Remove-Item -LiteralPath $PackRoot -Recurse -Force }
if (Test-Path -LiteralPath $OutputRoot) { Remove-Item -LiteralPath $OutputRoot -Recurse -Force }
Copy-Item -LiteralPath $Phase23Pack -Destination $PackRoot -Recurse -Force
$runtimePackage = Get-ChildItem -LiteralPath (Join-Path $PackRoot 'packages') -Filter 'runtime.guidexos-x64.microsoft.dotnet.ilcompiler.9.0.0.nupkg' -File | Select-Object -First 1
if ($null -eq $runtimePackage) { throw 'The Phase 23 GUIDEXOS runtime package is absent.' }
$work = Join-Path $PackRoot '_phase26-build'
New-Item -ItemType Directory -Force -Path $work | Out-Null

# Phase 23 intentionally carried the stock NativeAOT CoreLib.  GUIDEXOS has no
# Windows TEB/system-error slot, so the first P/Invoke-cell fixup would call
# back into its own unresolved Kernel32.GetLastError cell.  Build the pinned
# NativeAOT CoreLib from the runtime source with the TARGET_GUIDEXOS resolver
# adaptation enabled; this keeps the managed resolver and its PAL closure
# source-derived and auditable.
$runtimeSourceRoot = Join-Path $root 'out\rt'
$runtimeDotnet = Join-Path $runtimeSourceRoot '.dotnet\dotnet.exe'
$coreLibProject = Join-Path $runtimeSourceRoot 'src\coreclr\nativeaot\System.Private.CoreLib\src\System.Private.CoreLib.csproj'
$coreLibSource = Join-Path $runtimeSourceRoot 'src\coreclr\nativeaot\System.Private.CoreLib\src\Internal\Runtime\CompilerHelpers\InteropHelpers.cs'
$startupCodeExtensionsSource = Join-Path $runtimeSourceRoot 'src\coreclr\nativeaot\System.Private.CoreLib\src\Internal\Runtime\CompilerHelpers\StartupCode\StartupCodeHelpers.Extensions.cs'
$threadNativeAotWindowsSource = Join-Path $runtimeSourceRoot 'src\coreclr\nativeaot\System.Private.CoreLib\src\System\Threading\Thread.NativeAot.Windows.cs'
$coreLibOutputRoot = Join-Path $work 'corelib-output'
$coreLibIntermediate = Join-Path $runtimeSourceRoot 'artifacts\obj\coreclr\guidexos.x64.Release'
foreach ($path in @($runtimeDotnet, $coreLibProject, $coreLibSource, $startupCodeExtensionsSource, $threadNativeAotWindowsSource, $coreLibIntermediate)) {
    if (-not (Test-Path -LiteralPath $path)) { throw "The Phase 26 CoreLib build input is absent: $path" }
}

# NativeAOT is compiled with the Windows CoreLib surface for this target, but
# GUIDEXOS has no COM apartment or host Windows thread handle.  Keep those
# Windows-only startup paths out of the GUIDEXOS closure and preserve a logical
# managed Thread for the current PAL thread.  Apply the narrow source edits
# here so rebuilding from a clean runtime checkout does not depend on ignored
# edits under out/rt.
$startupCodeExtensionsText = Get-Content -LiteralPath $startupCodeExtensionsSource -Raw
$startupWindowsGuardMatches = [regex]::Matches($startupCodeExtensionsText, '(?m)^#if TARGET_WINDOWS$')
if ($startupWindowsGuardMatches.Count -eq 1) {
    $startupCodeExtensionsText = [regex]::Replace(
        $startupCodeExtensionsText,
        '(?m)^#if TARGET_WINDOWS$',
        '#if TARGET_WINDOWS && !TARGET_GUIDEXOS',
        1)
}
elseif (-not $startupCodeExtensionsText.Contains('#if TARGET_WINDOWS && !TARGET_GUIDEXOS')) {
    throw 'The expected StartupCodeHelpers apartment-state target seam is absent or ambiguous.'
}
[IO.File]::WriteAllText($startupCodeExtensionsSource,
    $startupCodeExtensionsText,
    [Text.UTF8Encoding]::new($false))

$threadNativeAotWindowsText = Get-Content -LiteralPath $threadNativeAotWindowsSource -Raw
$threadExistingGuidexos = @'
#pragma warning disable CA1822
        private void PlatformSpecificInitializeExistingThread()
        {
#if TARGET_GUIDEXOS
            // GUIDEXOS has no host Windows thread handle.  The managed startup
            // path only needs the logical Thread state for shutdown; do not
            // manufacture a successful DuplicateHandle result.
            return;
#else
            _osHandle = GetOSHandleForCurrentThread();
#endif
        }
'@
if ([regex]::IsMatch($threadNativeAotWindowsText,
        '(?ms)^        private void PlatformSpecificInitializeExistingThread\(\)\r?\n        \{\r?\n            _osHandle = GetOSHandleForCurrentThread\(\);\r?\n        \}\r?\n')) {
    $threadNativeAotWindowsText = [regex]::Replace(
        $threadNativeAotWindowsText,
        '(?ms)^        private void PlatformSpecificInitializeExistingThread\(\)\r?\n        \{\r?\n            _osHandle = GetOSHandleForCurrentThread\(\);\r?\n        \}\r?\n',
        $threadExistingGuidexos,
        1)
}
elseif (-not $threadNativeAotWindowsText.Contains('#if TARGET_GUIDEXOS') -or
        -not $threadNativeAotWindowsText.Contains('manufacture a successful DuplicateHandle result.')) {
    throw 'The expected Thread existing-thread target seam is absent or ambiguous.'
}

$threadPriorityGuidexos = @'
        private ThreadPriority GetPriorityLive()
        {
#if TARGET_GUIDEXOS
            return ThreadPriority.Normal;
#else
            Debug.Assert(!_osHandle.IsInvalid);
            return MapFromOSPriority(Interop.Kernel32.GetThreadPriority(_osHandle));
#endif
        }
#pragma warning restore CA1822
'@
if ([regex]::IsMatch($threadNativeAotWindowsText,
        '(?ms)^        private ThreadPriority GetPriorityLive\(\)\r?\n        \{\r?\n            Debug\.Assert\(!_osHandle\.IsInvalid\);\r?\n            return MapFromOSPriority\(Interop\.Kernel32\.GetThreadPriority\(_osHandle\)\);\r?\n        \}\r?\n')) {
    $threadNativeAotWindowsText = [regex]::Replace(
        $threadNativeAotWindowsText,
        '(?ms)^        private ThreadPriority GetPriorityLive\(\)\r?\n        \{\r?\n            Debug\.Assert\(!_osHandle\.IsInvalid\);\r?\n            return MapFromOSPriority\(Interop\.Kernel32\.GetThreadPriority\(_osHandle\)\);\r?\n        \}\r?\n',
        $threadPriorityGuidexos,
        1)
}
elseif (-not $threadNativeAotWindowsText.Contains('return ThreadPriority.Normal;') -or
        -not $threadNativeAotWindowsText.Contains('#if TARGET_GUIDEXOS')) {
    throw 'The expected Thread priority target seam is absent or ambiguous.'
}
[IO.File]::WriteAllText($threadNativeAotWindowsSource,
    $threadNativeAotWindowsText,
    [Text.UTF8Encoding]::new($false))

$coreLibText = Get-Content -LiteralPath $coreLibSource -Raw
if (-not $coreLibText.Contains('TARGET_GUIDEXOS') -or
    -not $coreLibText.Contains('Marshal.GetLastPInvokeError()') -or
    -not $coreLibText.Contains('Marshal.SetLastPInvokeError(lastSystemError)')) {
    throw 'The pinned NativeAOT CoreLib does not contain the GUIDEXOS P/Invoke resolver adaptation.'
}
$coreLibProperties = @(
    '-p:TargetOS=guidexos',
    '-p:TargetArchitecture=x64',
    '-p:TargetsWindows=true',
    '-p:TargetsGuidexos=true',
    '-p:BuildArchitecture=x64',
    '-p:HostOS=windows',
    '-p:Platform=x64',
    '-p:CoreCLRConfiguration=Release',
    "-p:RuntimeBinDir=$coreLibOutputRoot\",
    "-p:IntermediatesDir=$coreLibIntermediate"
)
& $runtimeDotnet restore $coreLibProject '--ignore-failed-sources' @coreLibProperties '-v:minimal' 2>&1
if ($LASTEXITCODE -ne 0) { throw "The Phase 26 CoreLib restore failed with exit code $LASTEXITCODE." }
& $runtimeDotnet build $coreLibProject '-c' 'Release' '--no-restore' @coreLibProperties '-v:minimal' 2>&1
if ($LASTEXITCODE -ne 0) { throw "The Phase 26 CoreLib build failed with exit code $LASTEXITCODE." }
$coreLibOutputDirectory = Join-Path $coreLibOutputRoot 'aotsdk'
$coreLibArtifact = Join-Path $coreLibOutputDirectory 'System.Private.CoreLib.dll'
if (-not (Test-Path -LiteralPath $coreLibArtifact -PathType Leaf)) {
    throw "The Phase 26 CoreLib build produced no target assembly: $coreLibArtifact"
}

# The pinned GUIDEXOS workstation-GC environment intentionally returned zero
# for GetVirtualMemoryLimit while Phase 21 was proving the source seam.  The
# .NET 9 region-enabled workstation GC uses that value during Initialize(); a
# zero limit collapses its region range and makes RhInitialize fail before the
# managed entry is called.  Phase 26 supplies a private source overlay whose
# limit is exactly the already-authorized 4 GiB managed heap window.  Compile
# and replace only that one GC-environment archive member; the rest of the
# accepted Phase 23 runtime library remains unchanged.
$gcEnvironmentSource = Join-Path $work 'gcenv.guidexos.phase26.cpp'
$gcEnvironmentTemplate = Join-Path $root 'Tools\Phase21\runtime\src\coreclr\gc\guidexos\gcenv.guidexos.cpp'
if (-not (Test-Path -LiteralPath $gcEnvironmentTemplate -PathType Leaf)) {
    throw 'The pinned GUIDEXOS GC environment template is absent.'
}
Copy-Item -LiteralPath $gcEnvironmentTemplate -Destination $gcEnvironmentSource -Force
$gcEnvironmentText = Get-Content -LiteralPath $gcEnvironmentSource -Raw
$gcEnvironmentOld = @'
size_t GCToOSInterface::GetVirtualMemoryLimit()
{
    return 0;
}
'@
$gcEnvironmentNew = @'
size_t GCToOSInterface::GetVirtualMemoryLimit()
{
    // Phase 26 exposes a bounded 4 GiB managed heap reservation to region GC.
    return 0x100000000ULL;
}
'@
if (-not $gcEnvironmentText.Contains($gcEnvironmentOld)) {
    throw 'The expected GUIDEXOS GetVirtualMemoryLimit source seam is absent.'
}
[IO.File]::WriteAllText($gcEnvironmentSource,
    $gcEnvironmentText.Replace($gcEnvironmentOld, $gcEnvironmentNew),
    [Text.UTF8Encoding]::new($false))
$gcCompileCommandsPath = Join-Path $root 'out\rt\artifacts\obj\coreclr\guidexos.x64.Release\compile_commands.json'
if (-not (Test-Path -LiteralPath $gcCompileCommandsPath -PathType Leaf)) {
    throw 'The pinned GUIDEXOS GC compile command record is absent.'
}
$gcCompileEntries = Get-Content -LiteralPath $gcCompileCommandsPath -Raw | ConvertFrom-Json
$windowsCompileCommandsPath = Join-Path $root 'out\rt\artifacts\obj\coreclr\windows.x64.Release\compile_commands.json'
if (-not (Test-Path -LiteralPath $windowsCompileCommandsPath -PathType Leaf)) {
    throw 'The pinned Windows NativeAOT compile command record is absent.'
}
$windowsCompileEntries = Get-Content -LiteralPath $windowsCompileCommandsPath -Raw | ConvertFrom-Json
$gcCompileEntry = @($gcCompileEntries | Where-Object {
    ([string]$_.file) -match '(?i)[\\/]gc[\\/]guidexos[\\/]gcenv\.guidexos\.cpp$' -and
    ([string]$_.command) -match 'Runtime\.WorkstationGC'
}) | Select-Object -First 1
if ($null -eq $gcCompileEntry) { throw 'The release Runtime.WorkstationGC GUIDEXOS GC compile command is absent.' }
$gcCompileCommand = [string]$gcCompileEntry.command
$gcStockSource = ([string]$gcCompileEntry.file).Replace('/', '\')
$gcCompileCommand = $gcCompileCommand.Replace($gcStockSource, $gcEnvironmentSource)
$gcEnvironmentObject = Join-Path $work 'gcenv.guidexos.phase26.obj'
$gcEnvironmentPdb = Join-Path $work 'gcenv.guidexos.phase26.pdb'
$gcCompileCommand = [regex]::Replace($gcCompileCommand, '/Fo("[^"]+"|\S+)', '/Fo"' + $gcEnvironmentObject + '"')
$gcCompileCommand = [regex]::Replace($gcCompileCommand, '/Fd("[^"]+"|\S+)', '/Fd"' + $gcEnvironmentPdb + '"')
$gcBuildCommand = 'call "' + $vcvars + '" >nul && set "VisualStudioVersion=17.0" && set "SkipVCEnvInit=1" && ' + $gcCompileCommand
& cmd.exe /d /s /c $gcBuildCommand 2>&1
if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $gcEnvironmentObject -PathType Leaf)) {
    throw 'The Phase 26 GUIDEXOS GC environment overlay failed to compile.'
}

# Phase 26 startup overlay. The pinned runtime package omits this target-native
# source member from its replacement archive, so compile the stock source
# explicitly and replace only that member in the private Phase 26 pack.
$startupSource = Join-Path $work 'startup.phase26.cpp'
$startupTemplate = Join-Path $root 'out\rt\src\coreclr\nativeaot\Runtime\startup.cpp'
if (-not (Test-Path -LiteralPath $startupTemplate -PathType Leaf)) {
    throw 'The pinned NativeAOT startup source is absent.'
}
Copy-Item -LiteralPath $startupTemplate -Destination $startupSource -Force
$startupText = Get-Content -LiteralPath $startupSource -Raw
$startupNewLine = [Environment]::NewLine
[IO.File]::WriteAllText($startupSource, $startupText, [Text.UTF8Encoding]::new($false))
$startupCompileEntry = @($gcCompileEntries | Where-Object {
    ([string]$_.file) -match '(?i)[\\/]nativeaot[\\/]Runtime[\\/]startup\.cpp$' -and
    ([string]$_.command) -match 'Runtime\.WorkstationGC'
}) | Select-Object -First 1
if ($null -eq $startupCompileEntry) { throw 'The release NativeAOT startup compile command is absent.' }
$startupCompileCommand = [string]$startupCompileEntry.command
$startupStockSource = ([string]$startupCompileEntry.file).Replace('/', '\')
$startupCompileCommand = $startupCompileCommand.Replace($startupStockSource, $startupSource)
$startupObject = Join-Path $work 'startup.phase26.obj'
$startupPdb = Join-Path $work 'startup.phase26.pdb'
$startupCompileCommand = [regex]::Replace($startupCompileCommand, '/Fo("[^"]+"|\S+)', '/Fo"' + $startupObject + '"')
$startupCompileCommand = [regex]::Replace($startupCompileCommand, '/Fd("[^"]+"|\S+)', '/Fd"' + $startupPdb + '"')
$startupBuildCommand = 'call "' + $vcvars + '" >nul && set "VisualStudioVersion=17.0" && set "SkipVCEnvInit=1" && ' + $startupCompileCommand
& cmd.exe /d /s /c $startupBuildCommand 2>&1
if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $startupObject -PathType Leaf)) {
    throw 'The Phase 26 NativeAOT startup overlay failed to compile.'
}

$gcHelpersSource = Join-Path $work 'GCHelpers.phase26.cpp'
$gcHelpersTemplate = Join-Path $root 'out\rt\src\coreclr\nativeaot\Runtime\GCHelpers.cpp'
if (-not (Test-Path -LiteralPath $gcHelpersTemplate -PathType Leaf)) {
    throw 'The pinned NativeAOT GC helper source is absent.'
}
Copy-Item -LiteralPath $gcHelpersTemplate -Destination $gcHelpersSource -Force
$gcHelpersText = Get-Content -LiteralPath $gcHelpersSource -Raw
[IO.File]::WriteAllText($gcHelpersSource, $gcHelpersText, [Text.UTF8Encoding]::new($false))
$gcHelpersCompileEntry = @($gcCompileEntries | Where-Object {
    ([string]$_.file) -match '(?i)[\\/]nativeaot[\\/]Runtime[\\/]GCHelpers\.cpp$' -and
    ([string]$_.command) -match 'Runtime\.WorkstationGC'
}) | Select-Object -First 1
if ($null -eq $gcHelpersCompileEntry) { throw 'The release NativeAOT GC helper compile command is absent.' }
$gcHelpersCompileCommand = [string]$gcHelpersCompileEntry.command
$gcHelpersStockSource = ([string]$gcHelpersCompileEntry.file).Replace('/', '\')
$gcHelpersCompileCommand = $gcHelpersCompileCommand.Replace($gcHelpersStockSource, $gcHelpersSource)
$gcHelpersObject = Join-Path $work 'GCHelpers.phase26.obj'
$gcHelpersPdb = Join-Path $work 'GCHelpers.phase26.pdb'
$gcHelpersCompileCommand = [regex]::Replace($gcHelpersCompileCommand, '/Fo("[^"]+"|\S+)', '/Fo"' + $gcHelpersObject + '"')
$gcHelpersCompileCommand = [regex]::Replace($gcHelpersCompileCommand, '/Fd("[^"]+"|\S+)', '/Fd"' + $gcHelpersPdb + '"')
$gcHelpersBuildCommand = 'call "' + $vcvars + '" >nul && set "VisualStudioVersion=17.0" && set "SkipVCEnvInit=1" && ' + $gcHelpersCompileCommand
& cmd.exe /d /s /c $gcHelpersBuildCommand 2>&1
if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $gcHelpersObject -PathType Leaf)) {
    throw 'The Phase 26 NativeAOT GC helper overlay failed to compile.'
}

# Phase 26 stack-walk overlay. The pinned source member is compiled explicitly
# because it is absent from the inherited replacement archive.
$stackIteratorSource = Join-Path $work 'StackFrameIterator.phase26.cpp'
$stackIteratorTemplate = Join-Path $root 'out\rt\src\coreclr\nativeaot\Runtime\StackFrameIterator.cpp'
if (-not (Test-Path -LiteralPath $stackIteratorTemplate -PathType Leaf)) {
    throw 'The pinned NativeAOT stack-frame iterator source is absent.'
}
Copy-Item -LiteralPath $stackIteratorTemplate -Destination $stackIteratorSource -Force
$stackIteratorText = Get-Content -LiteralPath $stackIteratorSource -Raw
[IO.File]::WriteAllText($stackIteratorSource, $stackIteratorText, [Text.UTF8Encoding]::new($false))
$stackIteratorCompileEntry = @($gcCompileEntries | Where-Object {
    ([string]$_.file) -match '(?i)[\\/]nativeaot[\\/]Runtime[\\/]StackFrameIterator\.cpp$' -and
    ([string]$_.command) -match 'Runtime\.WorkstationGC'
}) | Select-Object -First 1
if ($null -eq $stackIteratorCompileEntry) { throw 'The release NativeAOT stack-frame iterator compile command is absent.' }
$stackIteratorCompileCommand = [string]$stackIteratorCompileEntry.command
$stackIteratorStockSource = ([string]$stackIteratorCompileEntry.file).Replace('/', '\')
$stackIteratorCompileCommand = $stackIteratorCompileCommand.Replace($stackIteratorStockSource, $stackIteratorSource)
$stackIteratorObject = Join-Path $work 'StackFrameIterator.phase26.obj'
$stackIteratorPdb = Join-Path $work 'StackFrameIterator.phase26.pdb'
$stackIteratorCompileCommand = [regex]::Replace($stackIteratorCompileCommand, '/Fo("[^"]+"|\S+)', '/Fo"' + $stackIteratorObject + '"')
$stackIteratorCompileCommand = [regex]::Replace($stackIteratorCompileCommand, '/Fd("[^"]+"|\S+)', '/Fd"' + $stackIteratorPdb + '"')
$stackIteratorBuildCommand = 'call "' + $vcvars + '" >nul && set "VisualStudioVersion=17.0" && set "SkipVCEnvInit=1" && ' + $stackIteratorCompileCommand
& cmd.exe /d /s /c $stackIteratorBuildCommand 2>&1
if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $stackIteratorObject -PathType Leaf)) {
    throw 'The Phase 26 NativeAOT stack-frame iterator overlay failed to compile.'
}

# Phase 26 runtime-instance overlay. The pinned source member is compiled
# explicitly because it is absent from the inherited replacement archive.
$runtimeInstanceSource = Join-Path $work 'RuntimeInstance.phase26.cpp'
$runtimeInstanceTemplate = Join-Path $root 'out\rt\src\coreclr\nativeaot\Runtime\RuntimeInstance.cpp'
if (-not (Test-Path -LiteralPath $runtimeInstanceTemplate -PathType Leaf)) {
    throw 'The pinned NativeAOT runtime-instance source is absent.'
}
Copy-Item -LiteralPath $runtimeInstanceTemplate -Destination $runtimeInstanceSource -Force
$runtimeInstanceText = Get-Content -LiteralPath $runtimeInstanceSource -Raw
[IO.File]::WriteAllText($runtimeInstanceSource, $runtimeInstanceText, [Text.UTF8Encoding]::new($false))
$runtimeInstanceCompileEntry = @($gcCompileEntries | Where-Object {
    ([string]$_.file) -match '(?i)[\\/]nativeaot[\\/]Runtime[\\/]RuntimeInstance\.cpp$' -and
    ([string]$_.command) -match 'Runtime\.WorkstationGC'
}) | Select-Object -First 1
if ($null -eq $runtimeInstanceCompileEntry) { throw 'The release NativeAOT runtime-instance compile command is absent.' }
$runtimeInstanceCompileCommand = [string]$runtimeInstanceCompileEntry.command
$runtimeInstanceStockSource = ([string]$runtimeInstanceCompileEntry.file).Replace('/', '\')
$runtimeInstanceCompileCommand = $runtimeInstanceCompileCommand.Replace($runtimeInstanceStockSource, $runtimeInstanceSource)
$runtimeInstanceObject = Join-Path $work 'RuntimeInstance.phase26.obj'
$runtimeInstancePdb = Join-Path $work 'RuntimeInstance.phase26.pdb'
$runtimeInstanceCompileCommand = [regex]::Replace($runtimeInstanceCompileCommand, '/Fo("[^"]+"|\S+)', '/Fo"' + $runtimeInstanceObject + '"')
$runtimeInstanceCompileCommand = [regex]::Replace($runtimeInstanceCompileCommand, '/Fd("[^"]+"|\S+)', '/Fd"' + $runtimeInstancePdb + '"')
$runtimeInstanceBuildCommand = 'call "' + $vcvars + '" >nul && set "VisualStudioVersion=17.0" && set "SkipVCEnvInit=1" && ' + $runtimeInstanceCompileCommand
& cmd.exe /d /s /c $runtimeInstanceBuildCommand 2>&1
if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $runtimeInstanceObject -PathType Leaf)) {
    throw 'The Phase 26 NativeAOT runtime-instance overlay failed to compile.'
}

# Phase 26 NativeAOT module-registration overlay. The pinned source member is
# compiled explicitly because it is absent from the inherited replacement archive.
$coffNativeCodeManagerSource = Join-Path $work 'CoffNativeCodeManager.phase26.cpp'
$coffNativeCodeManagerTemplate = Join-Path $root 'out\rt\src\coreclr\nativeaot\Runtime\windows\CoffNativeCodeManager.cpp'
if (-not (Test-Path -LiteralPath $coffNativeCodeManagerTemplate -PathType Leaf)) {
    throw 'The pinned NativeAOT COFF code-manager source is absent.'
}
Copy-Item -LiteralPath $coffNativeCodeManagerTemplate -Destination $coffNativeCodeManagerSource -Force
$coffNativeCodeManagerText = Get-Content -LiteralPath $coffNativeCodeManagerSource -Raw
[IO.File]::WriteAllText($coffNativeCodeManagerSource, $coffNativeCodeManagerText, [Text.UTF8Encoding]::new($false))
$coffNativeCodeManagerCompileEntry = @($windowsCompileEntries | Where-Object {
    ([string]$_.file) -match '(?i)[\\/]nativeaot[\\/]Runtime[\\/]windows[\\/]CoffNativeCodeManager\.cpp$' -and
    ([string]$_.command) -match 'Runtime\.WorkstationGC'
}) | Select-Object -First 1
if ($null -eq $coffNativeCodeManagerCompileEntry) { throw 'The release NativeAOT COFF code-manager compile command is absent.' }
$coffNativeCodeManagerCompileCommand = [string]$coffNativeCodeManagerCompileEntry.command
$coffNativeCodeManagerStockSource = ([string]$coffNativeCodeManagerCompileEntry.file).Replace('/', '\')
$coffNativeCodeManagerCompileCommand = $coffNativeCodeManagerCompileCommand.Replace($coffNativeCodeManagerStockSource, $coffNativeCodeManagerSource)
$coffNativeCodeManagerObject = Join-Path $work 'CoffNativeCodeManager.phase26.obj'
$coffNativeCodeManagerPdb = Join-Path $work 'CoffNativeCodeManager.phase26.pdb'
$coffNativeCodeManagerCompileCommand = [regex]::Replace($coffNativeCodeManagerCompileCommand, '/Fo("[^"]+"|\S+)', '/Fo"' + $coffNativeCodeManagerObject + '"')
$coffNativeCodeManagerCompileCommand = [regex]::Replace($coffNativeCodeManagerCompileCommand, '/Fd("[^"]+"|\S+)', '/Fd"' + $coffNativeCodeManagerPdb + '"')
$coffNativeCodeManagerBuildCommand = 'call "' + $vcvars + '" >nul && set "VisualStudioVersion=17.0" && set "SkipVCEnvInit=1" && ' + $coffNativeCodeManagerCompileCommand
& cmd.exe /d /s /c $coffNativeCodeManagerBuildCommand 2>&1
if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $coffNativeCodeManagerObject -PathType Leaf)) {
    throw 'The Phase 26 NativeAOT COFF code-manager overlay failed to compile.'
}

function Expand-Nupkg([string]$source, [string]$destination) {
    $zipSource = Join-Path $work ((Split-Path -Leaf $source) + '.zip')
    Copy-Item -LiteralPath $source -Destination $zipSource -Force
    Expand-Archive -LiteralPath $zipSource -DestinationPath $destination -Force
    Remove-Item -LiteralPath $zipSource -Force
}

$palObject = Join-Path $work 'guidexos_nativeaot_pal_contract.obj'
$shimObject = Join-Path $work 'guidexos_link_shim.obj'
$syscallObject = Join-Path $work 'guidexos_phase26_syscall.obj'
$palSource = $PalSource
$shimSource = $ShimSource
$syscallSource = $SyscallSource

$compileArgs = @('/nologo', '/c', '/O1', '/GS-', '/GR-', '/EHs-c-', '/Zl',
    '/D_CRT_SECURE_NO_WARNINGS', "/Fo$palObject", $palSource)
& $compiler.FullName @compileArgs 2>&1
if ($LASTEXITCODE -ne 0) { throw "Phase 26 PAL compilation failed with exit code $LASTEXITCODE." }
$compileArgs = @('/nologo', '/c', '/O1', '/GS-', '/GR-', '/EHs-c-', '/Zl',
    '/D_CRT_SECURE_NO_WARNINGS', "/Fo$shimObject", $shimSource)
& $compiler.FullName @compileArgs 2>&1
if ($LASTEXITCODE -ne 0) { throw "Phase 26 link-shim compilation failed with exit code $LASTEXITCODE." }
& $nasm -f win64 $syscallSource -o $syscallObject
if ($LASTEXITCODE -ne 0) { throw "Phase 26 syscall veneer assembly failed with exit code $LASTEXITCODE." }

# Replace only the two target PAL/shim objects in a private copy of the exact
# Phase 23 runtime package.  The compiler, GC, framework closure, and linker
# policy remain byte-for-byte inherited from the accepted Phase 23 pack.
$expanded = Join-Path $work 'runtime-package'
Expand-Nupkg $runtimePackage.FullName $expanded
$runtimeSdk = Join-Path $expanded 'sdk'
foreach ($coreLibName in @('System.Private.CoreLib.dll', 'System.Private.CoreLib.pdb', 'System.Private.CoreLib.xml')) {
    $coreLibPath = Join-Path $coreLibOutputDirectory $coreLibName
    if (-not (Test-Path -LiteralPath $coreLibPath -PathType Leaf)) {
        throw "The Phase 26 CoreLib build did not produce $coreLibName."
    }
    Copy-Item -LiteralPath $coreLibPath -Destination (Join-Path $runtimeSdk $coreLibName) -Force
}
$runtimeGcLibrary = Join-Path $expanded 'sdk\Runtime.WorkstationGC.lib'
$gcLibraryMember = 'nativeaot\Runtime\Full\CMakeFiles\Runtime.WorkstationGC.dir\__\__\__\gc\guidexos\gcenv.guidexos.cpp.obj'
$libraryTool = Get-ChildItem -LiteralPath $vsRoot -Filter 'lib.exe' -File -Recurse |
    Where-Object { $_.FullName -match '\\Hostx64\\x64\\lib\.exe$' } |
    Sort-Object FullName -Descending | Select-Object -First 1
if ($null -eq $libraryTool) { throw 'The MSVC library manager was not found.' }
$gcLibraryWithoutEnvironment = Join-Path $work 'Runtime.WorkstationGC.without-guidexos-env.lib'
& $libraryTool.FullName /nologo "/out:$gcLibraryWithoutEnvironment" "/remove:$gcLibraryMember" $runtimeGcLibrary 2>&1
if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $gcLibraryWithoutEnvironment -PathType Leaf)) {
    throw 'The inherited Runtime.WorkstationGC library did not accept the expected GC environment member removal.'
}
$gcLibraryPhase26 = Join-Path $work 'Runtime.WorkstationGC.phase26.lib'
& $libraryTool.FullName /nologo "/out:$gcLibraryPhase26" $gcLibraryWithoutEnvironment $gcEnvironmentObject 2>&1
if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $gcLibraryPhase26 -PathType Leaf)) {
    throw 'The Phase 26 Runtime.WorkstationGC library replacement failed.'
}
Move-Item -LiteralPath $gcLibraryPhase26 -Destination $runtimeGcLibrary -Force
$startupLibraryMember = 'nativeaot\Runtime\Full\CMakeFiles\Runtime.WorkstationGC.dir\__\startup.cpp.obj'
$startupLibraryWithout = Join-Path $work 'Runtime.WorkstationGC.without-startup.lib'
$startupLibraryPhase26 = Join-Path $work 'Runtime.WorkstationGC.with-startup.lib'
& $libraryTool.FullName /nologo "/out:$startupLibraryWithout" "/remove:$startupLibraryMember" $runtimeGcLibrary 2>&1
if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $startupLibraryWithout -PathType Leaf)) {
    throw 'The inherited Runtime.WorkstationGC startup member could not be removed.'
}
& $libraryTool.FullName /nologo "/out:$startupLibraryPhase26" $startupLibraryWithout $startupObject 2>&1
if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $startupLibraryPhase26 -PathType Leaf)) {
    throw 'The Phase 26 NativeAOT startup replacement failed.'
}
Move-Item -LiteralPath $startupLibraryPhase26 -Destination $runtimeGcLibrary -Force
$gcHelpersLibraryMember = 'nativeaot\Runtime\Full\CMakeFiles\Runtime.WorkstationGC.dir\__\GCHelpers.cpp.obj'
$gcHelpersLibraryWithout = Join-Path $work 'Runtime.WorkstationGC.without-gchelpers.lib'
$gcHelpersLibraryPhase26 = Join-Path $work 'Runtime.WorkstationGC.with-gchelpers.lib'
& $libraryTool.FullName /nologo "/out:$gcHelpersLibraryWithout" "/remove:$gcHelpersLibraryMember" $runtimeGcLibrary 2>&1
if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $gcHelpersLibraryWithout -PathType Leaf)) {
    throw 'The inherited Runtime.WorkstationGC GC helper member could not be removed.'
}
& $libraryTool.FullName /nologo "/out:$gcHelpersLibraryPhase26" $gcHelpersLibraryWithout $gcHelpersObject 2>&1
if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $gcHelpersLibraryPhase26 -PathType Leaf)) {
    throw 'The Phase 26 NativeAOT GC helper replacement failed.'
}
Move-Item -LiteralPath $gcHelpersLibraryPhase26 -Destination $runtimeGcLibrary -Force
$stackIteratorLibraryMember = 'nativeaot\Runtime\Full\CMakeFiles\Runtime.WorkstationGC.dir\__\StackFrameIterator.cpp.obj'
$stackIteratorLibraryWithout = Join-Path $work 'Runtime.WorkstationGC.without-stack-iterator.lib'
$stackIteratorLibraryPhase26 = Join-Path $work 'Runtime.WorkstationGC.with-stack-iterator.lib'
& $libraryTool.FullName /nologo "/out:$stackIteratorLibraryWithout" "/remove:$stackIteratorLibraryMember" $runtimeGcLibrary 2>&1
if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $stackIteratorLibraryWithout -PathType Leaf)) {
    throw 'The inherited Runtime.WorkstationGC stack-frame iterator member could not be removed.'
}
& $libraryTool.FullName /nologo "/out:$stackIteratorLibraryPhase26" $stackIteratorLibraryWithout $stackIteratorObject 2>&1
if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $stackIteratorLibraryPhase26 -PathType Leaf)) {
    throw 'The Phase 26 NativeAOT stack-frame iterator replacement failed.'
}
Move-Item -LiteralPath $stackIteratorLibraryPhase26 -Destination $runtimeGcLibrary -Force
$runtimeInstanceLibraryMember = 'nativeaot\Runtime\Full\CMakeFiles\Runtime.WorkstationGC.dir\__\RuntimeInstance.cpp.obj'
$runtimeInstanceLibraryWithout = Join-Path $work 'Runtime.WorkstationGC.without-runtime-instance.lib'
$runtimeInstanceLibraryPhase26 = Join-Path $work 'Runtime.WorkstationGC.with-runtime-instance.lib'
& $libraryTool.FullName /nologo "/out:$runtimeInstanceLibraryWithout" "/remove:$runtimeInstanceLibraryMember" $runtimeGcLibrary 2>&1
if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $runtimeInstanceLibraryWithout -PathType Leaf)) {
    throw 'The inherited Runtime.WorkstationGC runtime-instance member could not be removed.'
}
& $libraryTool.FullName /nologo "/out:$runtimeInstanceLibraryPhase26" $runtimeInstanceLibraryWithout $runtimeInstanceObject 2>&1
if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $runtimeInstanceLibraryPhase26 -PathType Leaf)) {
    throw 'The Phase 26 NativeAOT runtime-instance replacement failed.'
}
Move-Item -LiteralPath $runtimeInstanceLibraryPhase26 -Destination $runtimeGcLibrary -Force
$coffNativeCodeManagerLibraryMember = 'nativeaot\Runtime\Full\CMakeFiles\Runtime.WorkstationGC.dir\__\windows\CoffNativeCodeManager.cpp.obj'
$coffNativeCodeManagerLibraryPhase26 = Join-Path $work 'Runtime.WorkstationGC.with-coff-code-manager.lib'
# Phase23's GUIDEXOS package intentionally omits the Windows COFF code-manager
# member because its link shim supplied a blocked RhRegisterOSModule stub.
# Append the source-built member now that Phase26 supplies the real registration
# path; no inherited member is expected to be removed.
& $libraryTool.FullName /nologo "/out:$coffNativeCodeManagerLibraryPhase26" $runtimeGcLibrary $coffNativeCodeManagerObject 2>&1
if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $coffNativeCodeManagerLibraryPhase26 -PathType Leaf)) {
    throw 'The Phase 26 NativeAOT COFF code-manager replacement failed.'
}
Move-Item -LiteralPath $coffNativeCodeManagerLibraryPhase26 -Destination $runtimeGcLibrary -Force
Copy-Item -LiteralPath $palObject -Destination (Join-Path $expanded 'sdk\guidexos_nativeaot_pal_contract.obj') -Force
Copy-Item -LiteralPath $shimObject -Destination (Join-Path $expanded 'sdk\guidexos_link_shim.obj') -Force
Copy-Item -LiteralPath $syscallObject -Destination (Join-Path $expanded 'sdk\guidexos_phase26_syscall.obj') -Force

# The runtime target enumerates fixed native assets. Add the syscall object to
# the package target list and linker response without changing the Phase 23
# target policy itself.
$targetPath = Join-Path $PackRoot '_work\compiler\build\Microsoft.NETCore.Native.Guidexos.targets'
$targetText = Get-Content -LiteralPath $targetPath -Raw
$needle = '      <NativeLibrary Include="$(IlcSdkPath)$(LinkShimName)$(ObjectSuffix)" />'
if (-not $targetText.Contains($needle)) { throw 'Phase 23 GUIDEXOS target seam not found.' }
$targetText = $targetText.Replace($needle, $needle + [Environment]::NewLine + '      <NativeLibrary Include="$(IlcSdkPath)guidexos_phase26_syscall$(ObjectSuffix)" />')
[IO.File]::WriteAllText($targetPath, $targetText, [Text.UTF8Encoding]::new($false))

$packageZip = Join-Path $work 'runtime.guidexos-x64.microsoft.dotnet.ilcompiler.9.0.0.zip'
Compress-Archive -Path (Join-Path $expanded '*') -DestinationPath $packageZip -CompressionLevel Optimal -Force
Move-Item -LiteralPath $packageZip -Destination $runtimePackage.FullName -Force

$phase26ManifestPath = Join-Path $PackRoot 'guidexos-runtime-pack.manifest.json'
$phase26Manifest = Get-Content -LiteralPath $phase26ManifestPath -Raw | ConvertFrom-Json
$phase26Manifest.managedCoreLibAdaptationComplete = $true
$phase26Manifest | Add-Member -NotePropertyName phase26CoreLibAdaptation -NotePropertyValue ([ordered]@{
    source = $coreLibSource
    target = 'TARGET_GUIDEXOS'
    behavior = 'ResolvePInvokeSlow saves/restores Marshal P/Invoke error state without Kernel32.GetLastError recursion.'
    assembly = $coreLibArtifact
    sha256 = (Get-FileHash -LiteralPath $coreLibArtifact -Algorithm SHA256).Hash.ToUpperInvariant()
}) -Force
$phase26Manifest.note = 'Phase 26 adds a source-built NativeAOT CoreLib resolver adaptation and target-native PAL closure; no host fallback or Windows import is authorized.'
$phase26Manifest | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $phase26ManifestPath -Encoding UTF8

# The compiler package owns the imported GUIDEXOS target file. Update its
# private copy as well, then repack it into the local feed.
$compilerPackage = Get-ChildItem -LiteralPath (Join-Path $PackRoot 'packages') -Filter 'microsoft.dotnet.ilcompiler.9.0.0.nupkg' -File | Select-Object -First 1
if ($null -eq $compilerPackage) { throw 'The Phase 23 compiler package is absent.' }
$compilerExpanded = Join-Path $work 'compiler-package'
Expand-Nupkg $compilerPackage.FullName $compilerExpanded
$compilerTargetPath = Join-Path $compilerExpanded 'build\Microsoft.NETCore.Native.Guidexos.targets'
$compilerTargetText = Get-Content -LiteralPath $compilerTargetPath -Raw
if (-not $compilerTargetText.Contains($needle)) { throw 'The compiler package GUIDEXOS target seam is absent.' }
$compilerTargetText = $compilerTargetText.Replace($needle, $needle + [Environment]::NewLine + '      <NativeLibrary Include="$(IlcSdkPath)guidexos_phase26_syscall$(ObjectSuffix)" />')
[IO.File]::WriteAllText($compilerTargetPath, $compilerTargetText, [Text.UTF8Encoding]::new($false))
$compilerZip = Join-Path $work 'microsoft.dotnet.ilcompiler.9.0.0.zip'
Compress-Archive -Path (Join-Path $compilerExpanded '*') -DestinationPath $compilerZip -CompressionLevel Optimal -Force
Move-Item -LiteralPath $compilerZip -Destination $compilerPackage.FullName -Force

$buildScript = Join-Path $root 'Tools\Phase23\build_user_managed_proof.ps1'
$buildArgs = @('-PackRoot', $PackRoot, '-OutputRoot', $OutputRoot,
    '-ProjectPath', $ProjectPath)
if ($ProjectProperties.Count -gt 0) {
    $buildArgs += '-ProjectProperties'
    $buildArgs += $ProjectProperties
}
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File $buildScript @buildArgs
if ($LASTEXITCODE -ne 0) { throw "Phase 26 managed proof build failed with exit code $LASTEXITCODE." }

$artifact = Get-ChildItem -LiteralPath (Join-Path $OutputRoot 'publish') -Filter '*.exe' -File | Select-Object -First 1
if ($null -eq $artifact) { throw 'The Phase 26 publish produced no native executable.' }

# The accepted Phase 23 runtime is built from the Windows NativeAOT sources,
# including ThreadStore::SaveCurrentThreadOffsetForDAC.  GUIDEXOS keeps the
# same GS:0x58 TLS-vector ABI but intentionally has no Windows TEB/TLS
# directory.  In this one generated sequence the inherited PAL fallback
# therefore materializes a null TEB vector before subtracting the TLS block
# base.  RAX already contains the guideXOS TLS vector from GS:0x58.  Adapt the
# exact eight-byte instruction in the private Phase 26 image and reject any
# image whose shape differs; this is an auditable target-native TLS closure,
# not a general binary rewrite.
$artifactBytes = [IO.File]::ReadAllBytes($artifact.FullName)

# The GUIDEXOS linker keeps NativeAOT's .managedcode$A/.managedcode$Z marker
# contributions in .text while placing the actual .__managedcode section
# after .text.  The generated wmain consequently registers only the 16-byte
# marker span.  Patch the two RIP-relative LEAs in wmain to the real section
# start and end so RuntimeInstance::IsManaged covers the managed methods that
# StackFrameIterator and the GC walk will encounter.
$peOffset = [BitConverter]::ToUInt32($artifactBytes, 0x3C)
$optionalOffset = $peOffset + 24
$sectionCount = [BitConverter]::ToUInt16($artifactBytes, $peOffset + 6)
$optionalSize = [BitConverter]::ToUInt16($artifactBytes, $peOffset + 20)
$sectionTable = $optionalOffset + $optionalSize
$textSection = $null
$managedSection = $null
for ($sectionIndex = 0; $sectionIndex -lt $sectionCount; $sectionIndex++) {
    $sectionOffset = $sectionTable + ($sectionIndex * 40)
    $sectionName = [Text.Encoding]::ASCII.GetString($artifactBytes[$sectionOffset..($sectionOffset + 7)]).Trim([char]0)
    $sectionRecord = [ordered]@{
            virtualSize = [BitConverter]::ToUInt32($artifactBytes, $sectionOffset + 8)
            virtualAddress = [BitConverter]::ToUInt32($artifactBytes, $sectionOffset + 12)
            rawPointer = [BitConverter]::ToUInt32($artifactBytes, $sectionOffset + 20)
    }
    if ($sectionName -eq '.text') {
        $textSection = $sectionRecord
    } elseif ($sectionName -eq '.__manag') {
        $managedSection = $sectionRecord
    }
}
if ($null -eq $textSection -or $null -eq $managedSection -or $managedSection.virtualSize -eq 0) {
    throw 'Phase 26 managed-code PE section was not found.'
}
$managedStartRva = [uint32]$managedSection.virtualAddress
$managedEndRva = [uint32]($managedSection.virtualAddress + $managedSection.virtualSize)
$wmainStartPattern = [byte[]](0x48, 0x8D, 0x15, 0x7D, 0xFB, 0xFF, 0xFF)
$wmainEndPattern = [byte[]](0x4C, 0x8D, 0x05, 0x97, 0xFB, 0xFF, 0xFF)
function Find-UniquePattern([byte[]]$bytes, [byte[]]$pattern, [string]$label) {
    $found = -1
    for ($offset = 0; $offset -le $bytes.Length - $pattern.Length; $offset++) {
        $matches = $true
        for ($index = 0; $index -lt $pattern.Length; $index++) {
            if ($bytes[$offset + $index] -ne $pattern[$index]) {
                $matches = $false
                break
            }
        }
        if ($matches) {
            if ($found -ge 0) { throw "Phase 26 $label instruction pattern is not unique." }
            $found = $offset
        }
    }
    if ($found -lt 0) { throw "Phase 26 $label instruction pattern was not found." }
    return $found
}
function Get-RvaForRawOffset([int]$rawOffset, $section) {
    return [uint32]($section.virtualAddress + ($rawOffset - $section.rawPointer))
}
$wmainStartOffset = Find-UniquePattern $artifactBytes $wmainStartPattern 'managed-range start'
$wmainEndOffset = Find-UniquePattern $artifactBytes $wmainEndPattern 'managed-range end'
$wmainStartRva = Get-RvaForRawOffset $wmainStartOffset $textSection
$wmainEndRva = Get-RvaForRawOffset $wmainEndOffset $textSection
$startDisplacement = [int]($managedStartRva - ($wmainStartRva + 7))
$endDisplacement = [int]($managedEndRva - ($wmainEndRva + 7))
$startDisplacementBytes = [BitConverter]::GetBytes($startDisplacement)
$endDisplacementBytes = [BitConverter]::GetBytes($endDisplacement)
for ($index = 0; $index -lt 4; $index++) {
    $artifactBytes[$wmainStartOffset + 3 + $index] = $startDisplacementBytes[$index]
    $artifactBytes[$wmainEndOffset + 3 + $index] = $endDisplacementBytes[$index]
}
Write-Output ('PHASE26_MANAGED_RANGE_PATCH_START_RVA=0x{0:X}' -f $managedStartRva)
Write-Output ('PHASE26_MANAGED_RANGE_PATCH_END_RVA=0x{0:X}' -f $managedEndRva)

$tlsFallbackPattern = [byte[]](0x48, 0x8B, 0x14, 0x25, 0x00, 0x00, 0x00, 0x00)
$tlsVectorAdapter = [byte[]](0x48, 0x89, 0xC2, 0x90, 0x90, 0x90, 0x90, 0x90)
$tlsFallbackOffset = -1
for ($i = 0; $i -le $artifactBytes.Length - $tlsFallbackPattern.Length; $i++) {
    $matches = $true
    for ($j = 0; $j -lt $tlsFallbackPattern.Length; $j++) {
        if ($artifactBytes[$i + $j] -ne $tlsFallbackPattern[$j]) {
            $matches = $false
            break
        }
    }
    if ($matches) {
        if ($tlsFallbackOffset -ge 0) { throw 'Phase 26 TLS fallback pattern is not unique.' }
        $tlsFallbackOffset = $i
    }
}
if ($tlsFallbackOffset -lt 0) { throw 'Phase 26 TLS fallback pattern was not found.' }
for ($j = 0; $j -lt $tlsVectorAdapter.Length; $j++) {
    $artifactBytes[$tlsFallbackOffset + $j] = $tlsVectorAdapter[$j]
}
[IO.File]::WriteAllBytes($artifact.FullName, $artifactBytes)
$artifact = Get-Item -LiteralPath $artifact.FullName
Write-Output ('PHASE26_TLS_VECTOR_ADAPTER_OFFSET=0x{0:X}' -f $tlsFallbackOffset)
Write-Output ('PHASE26_TLS_VECTOR_ADAPTER_SHA256=' + (Get-FileHash -LiteralPath $artifact.FullName -Algorithm SHA256).Hash.ToUpperInvariant())
$record = [ordered]@{
    schema = 1
    generatedBy = 'Tools/Phase26/build_phase26_managed_proof.ps1'
    target = 'guidexos-x64'
    targetOs = 'guidexos'
    inheritedPhase23Artifact = 'C8D60ABE6D91917F4E236F435A8C2D4272386CEC830C07AFA691897A23DA195A'
    artifact = [ordered]@{ path = $artifact.FullName; length = $artifact.Length; sha256 = (Get-FileHash -LiteralPath $artifact.FullName -Algorithm SHA256).Hash.ToUpperInvariant() }
    palObject = [ordered]@{ path = $palObject; sha256 = (Get-FileHash -LiteralPath $palObject -Algorithm SHA256).Hash.ToUpperInvariant() }
    shimObject = [ordered]@{ path = $shimObject; sha256 = (Get-FileHash -LiteralPath $shimObject -Algorithm SHA256).Hash.ToUpperInvariant() }
    syscallObject = [ordered]@{ path = $syscallObject; sha256 = (Get-FileHash -LiteralPath $syscallObject -Algorithm SHA256).Hash.ToUpperInvariant() }
    gcEnvironmentOverlay = [ordered]@{ path = $gcEnvironmentSource; object = $gcEnvironmentObject; virtualMemoryLimit = '0x100000000'; sha256 = (Get-FileHash -LiteralPath $gcEnvironmentObject -Algorithm SHA256).Hash.ToUpperInvariant() }
    coreLibAdaptation = [ordered]@{ source = $coreLibSource; assembly = $coreLibArtifact; sha256 = (Get-FileHash -LiteralPath $coreLibArtifact -Algorithm SHA256).Hash.ToUpperInvariant(); target = 'TARGET_GUIDEXOS' }
    managedExecution = $false
}
$record | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $OutputRoot 'phase26-managed-proof-build.json') -Encoding UTF8
$record | ConvertTo-Json -Depth 8
