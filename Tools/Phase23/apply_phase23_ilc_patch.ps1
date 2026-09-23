[CmdletBinding()]
param(
    [string]$SourceRoot = '',
    [string]$OutputRoot = ''
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
if ([string]::IsNullOrWhiteSpace($SourceRoot)) { $SourceRoot = Join-Path $root 'out\rt' }
if ([string]::IsNullOrWhiteSpace($OutputRoot)) { $OutputRoot = Join-Path $root 'out\dotnet\phase23-source-patch' }

$lock = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'runtime-source.lock.json') -Raw | ConvertFrom-Json
$phase21Lock = Get-Content -LiteralPath (Join-Path $root 'Tools\Phase21\runtime-source.lock.json') -Raw | ConvertFrom-Json
$phase22Lock = Get-Content -LiteralPath (Join-Path $root 'Tools\Phase22\runtime-source.lock.json') -Raw | ConvertFrom-Json
$phase21RecordPath = Join-Path $root ($lock.phase21PatchRecord -replace '/', '\')
$phase22RecordPath = Join-Path $root ($lock.phase22PatchRecord -replace '/', '\')

if (-not (Test-Path -LiteralPath $SourceRoot -PathType Container)) { throw "Runtime source is absent: $SourceRoot" }
if (-not (Test-Path -LiteralPath $phase21RecordPath -PathType Leaf)) { throw "Phase 21 patch record is absent: $phase21RecordPath" }
if (-not (Test-Path -LiteralPath $phase22RecordPath -PathType Leaf)) { throw "Phase 22 patch record is absent: $phase22RecordPath" }

$actualCommit = (& git -C $SourceRoot rev-parse HEAD).Trim()
if ($actualCommit -ne [string]$lock.runtimeCommit -or
    $actualCommit -ne [string]$phase21Lock.runtimeCommit -or
    $actualCommit -ne [string]$phase22Lock.runtimeCommit) {
    throw "Runtime source mismatch. Expected pinned commit $($lock.runtimeCommit), found $actualCommit."
}

function SourcePath([string]$relativePath) { Join-Path $SourceRoot ($relativePath -replace '/', '\') }
function ReadSource([string]$relativePath) { Get-Content -LiteralPath (SourcePath $relativePath) -Raw }
function HashFile([string]$path) { (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToUpperInvariant() }

function RewriteExact([string]$relativePath, [string]$old, [string]$new, [string]$description) {
    $path = SourcePath $relativePath
    $text = ReadSource $relativePath
    if ($text.Contains($new)) { return }
    $count = ([regex]::Matches($text, [regex]::Escape($old))).Count
    if ($count -ne 1) { throw "Cannot apply '$description' to ${relativePath}: expected one exact match, found $count." }
    [IO.File]::WriteAllText($path, $text.Replace($old, $new), [Text.UTF8Encoding]::new($false))
}

function RewriteRegex([string]$relativePath, [string]$pattern, [string]$replacement, [string]$description) {
    $path = SourcePath $relativePath
    $text = ReadSource $relativePath
    if ($text.Contains($replacement)) { return }
    $matches = [regex]::Matches($text, $pattern)
    if ($matches.Count -ne 1) { throw "Cannot apply '$description' to ${relativePath}: expected one regex match, found $($matches.Count)." }
    [IO.File]::WriteAllText($path, [regex]::Replace($text, $pattern, $replacement, 1), [Text.UTF8Encoding]::new($false))
}

$nl = "`r`n"

if (-not (ReadSource 'src/coreclr/tools/Common/TypeSystem/Common/TargetDetails.cs').Contains('        Guidexos,')) {
    RewriteRegex 'src/coreclr/tools/Common/TypeSystem/Common/TargetDetails.cs' `
        '        Windows,(\r?\n)' `
        ('        Windows,$1        Guidexos,$1') `
        'add the distinct GUIDEXOS target OS enum'
}
if (-not (ReadSource 'src/coreclr/tools/Common/TypeSystem/Common/TargetDetails.cs').Contains('public bool IsWindowsAbiCompatible')) {
    RewriteRegex 'src/coreclr/tools/Common/TypeSystem/Common/TargetDetails.cs' `
        '(?s)(        public bool IsWindows\r?\n        \{\r?\n            get\r?\n            \{\r?\n                return OperatingSystem == TargetOS\.Windows;\r?\n            \}\r?\n        \}\r?\n)' `
        ('$1' + $nl + '        /// <summary>' + $nl + '        /// Returns True when the target deliberately uses the Microsoft x64 ABI.' + $nl + '        /// This is an ABI property, not an operating-system identity.' + $nl + '        /// </summary>' + $nl + '        public bool IsWindowsAbiCompatible' + $nl + '        {' + $nl + '            get' + $nl + '            {' + $nl + '                return OperatingSystem == TargetOS.Windows || OperatingSystem == TargetOS.Guidexos;' + $nl + '            }' + $nl + '        }' + $nl + $nl + '        /// <summary>' + $nl + '        /// Returns True when the target emits PE/COFF-compatible native objects.' + $nl + '        /// </summary>' + $nl + '        public bool UsesCoffObjectFormat' + $nl + '        {' + $nl + '            get' + $nl + '            {' + $nl + '                return IsWindowsAbiCompatible;' + $nl + '            }' + $nl + '        }' + $nl) `
        'add explicit ABI and object-format properties'
}

RewriteExact 'src/coreclr/tools/Common/CommandLineHelpers.cs' '                "win" or "windows" => TargetOS.Windows,' ('                "win" or "windows" => TargetOS.Windows,' + $nl + '                "guidexos" => TargetOS.Guidexos,') 'parse the GUIDEXOS target token'
RewriteExact 'src/coreclr/tools/aot/ILCompiler/ILCompilerRootCommand.cs' 'string[] ValidOS = new string[] { "windows", "linux", "freebsd", "osx", "maccatalyst", "ios", "iossimulator", "tvos", "tvossimulator" };' 'string[] ValidOS = new string[] { "windows", "guidexos", "linux", "freebsd", "osx", "maccatalyst", "ios", "iossimulator", "tvos", "tvossimulator" };' 'advertise the private target in ILC help'

RewriteExact 'src/coreclr/tools/aot/ILCompiler/Program.cs' '            TargetOS targetOS = Get(_command.TargetOS);' ('            TargetOS targetOS = Get(_command.TargetOS);' + $nl + '            if (targetOS == TargetOS.Guidexos && targetArchitecture != TargetArchitecture.X64)' + $nl + '                throw new CommandLineException("GUIDEXOS currently supports only x64");') 'reject unsupported GUIDEXOS architectures'
RewriteExact 'src/coreclr/tools/aot/ILCompiler/Program.cs' ('                    ((targetOS == TargetOS.Linux && targetArchitecture is TargetArchitecture.X64 or TargetArchitecture.ARM64) ||' + $nl + '                     (targetOS == TargetOS.Windows && targetArchitecture is TargetArchitecture.X64 or TargetArchitecture.ARM64)))') ('                    (targetOS is TargetOS.Linux or TargetOS.Windows or TargetOS.Guidexos) &&' + $nl + '                     targetArchitecture is TargetArchitecture.X64 or TargetArchitecture.ARM64)') 'enable bounded inlined TLS for GUIDEXOS x64'

RewriteExact 'src/coreclr/tools/Common/Compiler/DependencyAnalysis/Target_X64/TargetRegisterMap.cs' ('                case TargetOS.Windows:' + $nl) ('                case TargetOS.Windows:' + $nl + '                case TargetOS.Guidexos:' + $nl) 'select Microsoft x64 argument registers for GUIDEXOS'
RewriteExact 'src/coreclr/tools/aot/ILCompiler.RyuJit/Compiler/RyuJitCompilationBuilder.cs' 'new NativeAotNameMangler(context.Target.IsWindows ? (NodeMangler)new WindowsNodeMangler(context.Target) : (NodeMangler)new UnixNodeMangler())' 'new NativeAotNameMangler(context.Target.IsWindowsAbiCompatible ? (NodeMangler)new WindowsNodeMangler(context.Target) : (NodeMangler)new UnixNodeMangler())' 'select the PE/COFF-compatible name mangler by ABI'
RewriteExact 'src/coreclr/tools/aot/ILCompiler.RyuJit/JitInterface/CorInfoImpl.RyuJit.cs' 'if ((target.IsWindows && target.Architecture is TargetArchitecture.X64 or TargetArchitecture.ARM64) ||' 'if ((target.IsWindowsAbiCompatible && target.Architecture is TargetArchitecture.X64 or TargetArchitecture.ARM64) ||' 'select bounded x64 ABI thread-static lowering'
RewriteExact 'src/coreclr/tools/Common/JitInterface/JitConfigProvider.cs' 'targetOSComponent = target.OperatingSystem == TargetOS.Windows ? "win" : "unix";' 'targetOSComponent = target.IsWindowsAbiCompatible ? "win" : "unix";' 'select JIT ABI component without changing target identity'
RewriteExact 'src/coreclr/tools/Common/JitInterface/CorInfoImpl.cs' 'return target.IsWindows ? CORINFO_OS.CORINFO_WINNT :' 'return target.IsWindowsAbiCompatible ? CORINFO_OS.CORINFO_WINNT :' 'pass the selected ABI to RyuJit'
RewriteExact 'src/coreclr/tools/Common/TypeSystem/Interop/UnmanagedCallingConventions.cs' '=> context.Target.IsWindows ? UnmanagedCallingConventions.Stdcall : UnmanagedCallingConventions.Cdecl;' '=> context.Target.IsWindowsAbiCompatible ? UnmanagedCallingConventions.Stdcall : UnmanagedCallingConventions.Cdecl;' 'select the Microsoft calling convention only for the explicit ABI'

RewriteExact 'src/coreclr/tools/aot/ILCompiler.Compiler/Compiler/ObjectWriter/ObjectWriter.cs' 'if (_nodeFactory.Target.OperatingSystem == TargetOS.Windows)' 'if (_nodeFactory.Target.UsesCoffObjectFormat)' 'select PE/COFF native sections explicitly'
RewriteExact 'src/coreclr/tools/aot/ILCompiler.Compiler/Compiler/ObjectWriter/ObjectWriter.cs' 'factory.Target.OperatingSystem == TargetOS.Windows ? new CoffObjectWriter(factory, options) :' 'factory.Target.UsesCoffObjectFormat ? new CoffObjectWriter(factory, options) :' 'select the COFF object writer explicitly'
RewriteExact 'src/coreclr/tools/aot/ILCompiler.Compiler/Compiler/UserDefinedTypeDescriptor.cs' 'if (NodeFactory.Target.OperatingSystem != TargetOS.Windows)' 'if (!NodeFactory.Target.UsesCoffObjectFormat)' 'select target static layout by object format'
RewriteExact 'src/coreclr/tools/aot/ILCompiler.Compiler/Compiler/UserDefinedTypeDescriptor.cs' 'if (NodeFactory.Target.OperatingSystem == TargetOS.Windows)' 'if (NodeFactory.Target.UsesCoffObjectFormat)' 'select COFF static field regions explicitly'

$coffFiles = @(
    'src/coreclr/tools/aot/ILCompiler.Compiler/Compiler/ExportsFileWriter.cs',
    'src/coreclr/tools/aot/ILCompiler.Compiler/Compiler/RuntimeConfigurationRootProvider.cs',
    'src/coreclr/tools/aot/ILCompiler.Compiler/Compiler/DependencyAnalysis/DictionaryLayoutNode.cs',
    'src/coreclr/tools/aot/ILCompiler.Compiler/Compiler/DependencyAnalysis/EETypeNode.cs',
    'src/coreclr/tools/aot/ILCompiler.Compiler/Compiler/DependencyAnalysis/ExternalReferencesTableNode.cs',
    'src/coreclr/tools/aot/ILCompiler.Compiler/Compiler/DependencyAnalysis/FatFunctionPointerNode.cs',
    'src/coreclr/tools/aot/ILCompiler.Compiler/Compiler/DependencyAnalysis/GCStaticEETypeNode.cs',
    'src/coreclr/tools/aot/ILCompiler.Compiler/Compiler/DependencyAnalysis/GCStaticsPreInitDataNode.cs',
    'src/coreclr/tools/aot/ILCompiler.Compiler/Compiler/DependencyAnalysis/GenericCompositionNode.cs',
    'src/coreclr/tools/aot/ILCompiler.Compiler/Compiler/DependencyAnalysis/GenericVarianceNode.cs',
    'src/coreclr/tools/aot/ILCompiler.Compiler/Compiler/DependencyAnalysis/InterfaceDispatchMapNode.cs',
    'src/coreclr/tools/aot/ILCompiler.Compiler/Compiler/DependencyAnalysis/MethodExceptionHandlingInfoNode.cs',
    'src/coreclr/tools/aot/ILCompiler.Compiler/Compiler/DependencyAnalysis/ModulesSectionNode.cs',
    'src/coreclr/tools/aot/ILCompiler.Compiler/Compiler/DependencyAnalysis/NativeLayoutSignatureNode.cs',
    'src/coreclr/tools/aot/ILCompiler.Compiler/Compiler/DependencyAnalysis/ReadyToRunHeaderNode.cs',
    'src/coreclr/tools/aot/ILCompiler.Compiler/Compiler/DependencyAnalysis/RuntimeFieldHandleNode.cs',
    'src/coreclr/tools/aot/ILCompiler.Compiler/Compiler/DependencyAnalysis/RuntimeMethodHandleNode.cs',
    'src/coreclr/tools/aot/ILCompiler.Compiler/Compiler/DependencyAnalysis/SealedVTableNode.cs',
    'src/coreclr/tools/aot/ILCompiler.Compiler/Compiler/DependencyAnalysis/TypeThreadStaticIndexNode.cs',
    'src/coreclr/tools/aot/ILCompiler.Compiler/Compiler/DependencyAnalysis/UnboxingStubNode.cs'
)
foreach ($relativePath in $coffFiles) {
    $path = SourcePath $relativePath
    $text = ReadSource $relativePath
    $updated = $text
    $updated = $updated.Replace('factory.Target.IsWindows', 'factory.Target.UsesCoffObjectFormat')
    $updated = $updated.Replace('_target.IsWindows', '_target.UsesCoffObjectFormat')
    $updated = $updated.Replace('_owningMethod.Context.Target.IsWindows', '_owningMethod.Context.Target.UsesCoffObjectFormat')
    $updated = $updated.Replace('_type.Context.Target.IsWindows', '_type.Context.Target.UsesCoffObjectFormat')
    $updated = $updated.Replace('_context.Target.IsWindows', '_context.Target.UsesCoffObjectFormat')
    if ($updated -ne $text) { [IO.File]::WriteAllText($path, $updated, [Text.UTF8Encoding]::new($false)) }
}

$expectedPhase21 = @($phase21Lock.sourceFiles.path | ForEach-Object { $_ -replace '/', '\' })
$expectedPhase22 = @(
    'src/coreclr/CMakeLists.txt',
    'src/coreclr/inc/daccess.h',
    'src/coreclr/nativeaot/CMakeLists.txt',
    'src/coreclr/nativeaot/Runtime/PalRedhawk.h',
    'src/coreclr/nativeaot/Runtime/gcenv.h',
    'src/coreclr/nativeaot/Runtime/startup.cpp',
    'src/coreclr/nativeaot/Runtime/regdisplay.h',
    'src/coreclr/nativeaot/Runtime/Full/CMakeLists.txt'
    ) | ForEach-Object { $_ -replace '/', '\' }
$expectedPhase23 = @($lock.changedFiles | ForEach-Object { $_ -replace '/', '\' })
$actualTracked = @(& git -C $SourceRoot diff --name-only | ForEach-Object { $_ -replace '/', '\' } | Sort-Object)
$expectedTracked = @($expectedPhase21 + $expectedPhase22 + $expectedPhase23 | Sort-Object -Unique)
if ((@($actualTracked) -join '|') -ne (@($expectedTracked) -join '|')) { throw 'Phase 23 ILC patch produced an unexpected runtime source diff.' }

New-Item -ItemType Directory -Force -Path $OutputRoot | Out-Null
$fileRecords = foreach ($relativePath in $expectedPhase23) {
    $path = SourcePath $relativePath
    [ordered]@{ path = $relativePath.Replace('\', '/'); sha256 = HashFile $path }
}
$record = [ordered]@{
    schema = 1
    generatedBy = 'Tools/Phase23/apply_phase23_ilc_patch.ps1'
    generatedAtUtc = [DateTime]::UtcNow.ToString('o')
    patchIdentity = [string]$lock.ilcPatchIdentity
    target = [string]$lock.target
    targetOs = [string]$lock.targetOsValue
    sourceCommit = $actualCommit
    phase21PatchRecord = (Resolve-Path $phase21RecordPath).Path
    phase22PatchRecord = (Resolve-Path $phase22RecordPath).Path
    changedFiles = $fileRecords
    semanticModel = [ordered]@{
        targetIdentity = 'TargetOS.Guidexos; IsWindows remains false'
        architecture = 'GUIDEXOS is accepted only for TargetArchitecture.X64'
        abi = 'Microsoft x64 argument/register ABI is selected through IsWindowsAbiCompatible'
        objectFormat = 'COFF is selected through UsesCoffObjectFormat; no Windows target alias is introduced'
        runtimeSemantics = 'Windows-only P/Invoke, Win32 resources, CFG, and command-line-W behavior remain gated on actual Windows'
    }
}
$recordPath = Join-Path $OutputRoot 'phase23-ilc-source-patch-record.json'
$record | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $recordPath -Encoding UTF8
$record | ConvertTo-Json -Depth 8
Write-Output "Wrote $recordPath"
