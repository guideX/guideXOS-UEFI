using System;

namespace System.Drawing {
    public sealed class Image {
        public int[] RawData;
        public int Width;
        public int Height;

        public Image(int width, int height) {
            Width = width;
            Height = height;
            RawData = new int[checked(width * height)];
        }
    }

    public struct Color {
        public static uint ToArgb(byte red, byte green, byte blue) =>
            ToArgb(255, red, green, blue);

        public static uint ToArgb(byte alpha, byte red, byte green, byte blue) =>
            ((uint)alpha << 24) | ((uint)red << 16) |
            ((uint)green << 8) | blue;
    }
}

public static unsafe class Native {
    public static void Stosd(void* destination, uint value, ulong count) {
        uint* pixels = (uint*)destination;
        for (ulong i = 0; i < count; i++) pixels[(long)i] = value;
    }

    public static void Movsd(uint* destination, uint* source, ulong count) {
        for (ulong i = 0; i < count; i++) destination[(long)i] = source[(long)i];
    }
}

namespace guideXOS.Graph {
    internal static class TestArrayExtensions {
        public static void Dispose(this int[] _) { }
    }
}
