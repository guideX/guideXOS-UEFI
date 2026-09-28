using System.Runtime;
using System.Runtime.InteropServices;
namespace guideXOS.Kernel.Libraries {
    /// <summary>
    /// The stdlib.h header defines four variable types, several macros, and various functions for performing general functions.
    /// </summary>
#pragma warning disable CS8981
    public static unsafe class stdlib {
#pragma warning restore CS8981
#if UEFI_DIAGNOSTIC_RING3_PHASE32 || UEFI_DIAGNOSTIC_APP_RUNTIME
        [DllImport("*")]
        private static extern ulong ReadFreeCallerReturnAddress();
#endif

        /// <summary>
        /// Malloc
        /// </summary>
        /// <param name="size"></param>
        /// <returns></returns>
        [RuntimeExport("malloc")]
        public static void* malloc(ulong size) {
            return (void*)Allocator.Allocate(size);
        }
        /// <summary>
        /// Free
        /// </summary>
        /// <param name="ptr"></param>
        [RuntimeExport("free")]
        public static void free(void* ptr) {
#if UEFI_DIAGNOSTIC_RING3_PHASE32 || UEFI_DIAGNOSTIC_APP_RUNTIME
            Allocator.Free((System.IntPtr)ptr, "stdlib.free",
                ReadFreeCallerReturnAddress());
#else
            Allocator.Free((System.IntPtr)ptr);
#endif
        }
        /// <summary>
        /// Realloc
        /// </summary>
        /// <param name="ptr"></param>
        /// <param name="size"></param>
        /// <returns></returns>
        [RuntimeExport("realloc")]
        public static void* realloc(void* ptr, ulong size) {
            return (void*)Allocator.Reallocate((System.IntPtr)ptr, size);
        }
        /// <summary>
        /// Calloc
        /// </summary>
        /// <param name="num"></param>
        /// <param name="size"></param>
        /// <returns></returns>
        [RuntimeExport("calloc")]
        public static void* calloc(ulong num, ulong size) {
            void* ptr = (void*)Allocator.Allocate(num * size);
            Native.Stosb(ptr, 0, num * size);
            return ptr;
        }
        /// <summary>
        /// Kmalloc
        /// </summary>
        /// <param name="size"></param>
        /// <returns></returns>
        [RuntimeExport("kmalloc")]
        public static void* kmalloc(ulong size) {
            return (void*)Allocator.Allocate(size);
        }
        /// <summary>
        /// Kfree
        /// </summary>
        /// <param name="ptr"></param>
        [RuntimeExport("kfree")]
        public static void kfree(void* ptr) {
            Allocator.Free((System.IntPtr)ptr, "stdlib.kfree");
        }
        /// <summary>
        /// KRealloc
        /// </summary>
        /// <param name="ptr"></param>
        /// <param name="size"></param>
        /// <returns></returns>
        [RuntimeExport("krealloc")]
        public static void* krealloc(void* ptr, ulong size) {
            return (void*)Allocator.Reallocate((System.IntPtr)ptr, size);
        }
        /// <summary>
        /// Kalloc
        /// </summary>
        /// <param name="num"></param>
        /// <param name="size"></param>
        /// <returns></returns>
        [RuntimeExport("kcalloc")]
        public static void* kcalloc(ulong num, ulong size) {
            void* ptr = (void*)Allocator.Allocate(num * size);
            Native.Stosb(ptr, 0, num * size);
            return ptr;
        }
    }
}
