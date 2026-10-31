using System.Text;

namespace guideXOS.Kernel.Drivers {
    internal static unsafe class Ramdisk {
        internal static byte* RawBasePointer;
        internal static ulong RawImageSizeBytes;
    }
}

namespace guideXOS.Kernel.Helpers { }

namespace guideXOS.Misc {
    internal static class BootConsole {
        internal static void WriteLine(string message) { }
    }
}

namespace guideXOS.FS {
    internal static class HostedDisposableExtensions {
        internal static void Dispose(this string value) { }
        internal static void Dispose<T>(this System.Collections.Generic.List<T> value) { }
    }

    internal static unsafe class RdskFSHostedAscii {
        internal static string Decode(byte* value, int length) {
            byte[] bytes = new byte[length];
            for (int i = 0; i < length; i++) bytes[i] = value[i];
            return Encoding.ASCII.GetString(bytes);
        }
    }
}
