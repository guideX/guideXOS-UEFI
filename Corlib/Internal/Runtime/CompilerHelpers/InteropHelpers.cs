using System;
using System.Runtime.InteropServices;

namespace Internal.Runtime.CompilerHelpers {
    public static class InteropHelpers {
        [StructLayout(LayoutKind.Sequential)]
        public unsafe struct MethodFixupCell {
            public IntPtr Target;
            public IntPtr MethodName;
            public ModuleFixupCell* Module;
            public CharSet CharSetMangling;
        }

        [StructLayout(LayoutKind.Sequential)]
        public unsafe struct ModuleFixupCell {
            public IntPtr Handle;
            public IntPtr ModuleName;
            public EETypePtr CallingAssemblyType;
            public uint DllImportSearchPathAndCookie;
        }

        public static unsafe IntPtr ResolvePInvoke(MethodFixupCell* pCell) {
#if Kernel
            // The dynamic P/Invoke trampoline is a Ring 3 syscall entry. Kernel
            // imports must be linked directly or routed through typed kernel
            // APIs; fail closed if one reaches the generic resolver.
            guideXOS.Misc.Panic.Error("Unsupported dynamic kernel P/Invoke", false);
            return IntPtr.Zero;
#else
            uint int0x80 = 0xC380CD;
            uint* ptr = &int0x80;
            return ((delegate*<MethodFixupCell*, IntPtr>)ptr)(pCell);
#endif
        }

        public static unsafe string StringToAnsiString(string str, bool bestFit, bool throwOnUnmappableChar) {
            //No Ansi support, Return unicode
            return str;
        }

        public static unsafe char WideCharToAnsiChar(char managedValue, bool bestFit, bool throwOnUnmappableChar) {
            //No Ansi support, Return unicode
            return managedValue;
        }

        public unsafe static void CoTaskMemFree(void* p) {
            //TO-DO
        }
    }
}
