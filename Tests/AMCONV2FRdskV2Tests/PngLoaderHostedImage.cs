namespace guideXOS { }

namespace System {
    internal static class HostedPngArrayDisposal {
        internal static void Dispose<T>(this T[] value) { }
    }
}

namespace System.Drawing {
    /// <summary>
    /// Minimal host-side storage target for the production PngLoader. The
    /// decoder itself is compiled from Kernel/Misc/PngLoader.cs unchanged.
    /// </summary>
    public sealed class Image {
        public int[] RawData;
        public int Width;
        public int Height;

        public Image(int width, int height) {
            Width = width;
            Height = height;
            RawData = new int[checked(width * height)];
        }

        public void Dispose() {
            RawData = null;
        }
    }
}
