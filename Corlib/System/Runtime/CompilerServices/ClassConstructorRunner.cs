using System.Runtime.InteropServices;

namespace System.Runtime.CompilerServices {
    // A class responsible for running static constructors. The compiler will call into this
    // code to ensure static constructors run and that they only run once.
    [McgIntrinsics]
    internal static class ClassConstructorRunner {
        private static unsafe IntPtr CheckStaticClassConstructionReturnNonGCStaticBase(ref StaticClassConstructionContext context, IntPtr nonGcStaticBase) {
            CheckStaticClassConstruction(ref context);
            return nonGcStaticBase;
        }

        private static unsafe object CheckStaticClassConstructionReturnGCStaticBase(ref StaticClassConstructionContext context, object gcStaticBase) {
            CheckStaticClassConstruction(ref context);
            return gcStaticBase;
        }

        private static unsafe void CheckStaticClassConstruction(ref StaticClassConstructionContext context) {
            // Very simplified class constructor runner. In real world, the class constructor runner
            // would need to be able to deal with potentially multiple threads racing to initialize
            // a single class, and would need to be able to deal with potential deadlocks
            // between class constructors.

            if (context.initialized == 1)
                return;

            context.initialized = 1;

            // Run the class constructor.
#if UEFI_TASKMANAGER_MEMORY_R3_PROBE
            // The allocator return address identifies this dispatcher for every
            // allocation inside the constructor. Preserve the concrete constructor
            // entry address as a fixed numeric provenance value for the R3 ledger.
            uint previousCreationSite =
                Allocator.CurrentDiagnosticManagedCreationSite;
            Allocator.CurrentDiagnosticManagedCreationSite = unchecked(
                (uint)(long)context.cctorMethodAddress);
            try {
                ((delegate*<void>)context.cctorMethodAddress)();
            } finally {
                Allocator.CurrentDiagnosticManagedCreationSite =
                    previousCreationSite;
            }
#else
            ((delegate*<void>)context.cctorMethodAddress)();
#endif
        }
    }
}
