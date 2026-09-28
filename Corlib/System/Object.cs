using Internal.Runtime;
using Internal.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace System {
    public unsafe class Object {
        // The layout of object is a contract with the compiler.
        internal unsafe EEType* m_pEEType;

        [StructLayout(LayoutKind.Sequential)]
        private class RawData {
            public byte Data;
        }

        internal ref byte GetRawData() {
            return ref Unsafe.As<RawData>(this).Data;
        }

        internal uint GetRawDataSize() {
            return m_pEEType->BaseSize - (uint)sizeof(ObjHeader) - (uint)sizeof(EEType*);
        }

        public Object() { }
        ~Object() { }

        /// <summary>
        /// Compare object references without invoking value equality. This is
        /// used by generic collections to distinguish object identity even
        /// when the runtime's generic operator lowering is unavailable.
        /// </summary>
        public static bool ReferenceEquals(object left, object right) {
            return Unsafe.As<object, IntPtr>(ref left) ==
                   Unsafe.As<object, IntPtr>(ref right);
        }

        public virtual bool Equals(object o)
            => false;

        public virtual int GetHashCode()
            => 0;

        public virtual string ToString()
            => "System.Object";

        public virtual void Dispose() {
            // Managed objects are owned and reclaimed by the NativeAOT GC.
            // Only release this reference when it is itself the start of a live
            // allocator run. Static/frozen strings and interior GC objects are
            // ignored; resource-owning types still release resources in overrides.
            var obj = this;
            Allocator.FreeManagedObjectIfAllocatorRun(
                Unsafe.As<object, IntPtr>(ref obj));
        }

        public static implicit operator bool(object obj) => obj != null;

        public static implicit operator IntPtr(object obj) => Unsafe.As<object, IntPtr>(ref obj);

    }
}
