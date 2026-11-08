using System;
using guideXOS.Graph;

internal static unsafe class Program {
    private const byte Canary = 0xCD;
    private const uint Red = 0xFFFF0000;
    private const uint Green = 0xFF00FF00;
    private const uint Blue = 0xFF0000FF;

    private static int _assertions;

    private static int Main() {
        try {
            TestLayoutValidation();
            TestPackedAndPaddedSurfaces();
            TestLargeNonPowerOfTwoPitch();
            TestPixelFormats();
            TestChangedPixelPresenter();
            Console.WriteLine($"PASS: {_assertions} framebuffer contract assertions");
            return 0;
        } catch (Exception ex) {
            Console.Error.WriteLine($"FAIL: {ex.Message}");
            return 1;
        }
    }

    private static void TestLayoutValidation() {
        var storage = new byte[64];
        fixed (byte* p = storage) {
            ulong address = (ulong)(p + 16);
            Check(CreateLayout(address, 3, 2, 12, 24,
                GopPixelFormat.PixelBlueGreenRedReserved8BitPerColor,
                0, 0, 0, 0, out _), "packed layout accepted");
            Check(CreateLayout(address, 3, 2, 20, 40,
                GopPixelFormat.PixelBlueGreenRedReserved8BitPerColor,
                0, 0, 0, 0, out _), "padded layout accepted");
            Check(!CreateLayout(address, 3, 2, 8, 24,
                GopPixelFormat.PixelBlueGreenRedReserved8BitPerColor,
                0, 0, 0, 0, out _), "short pitch rejected");
            Check(!CreateLayout(address, 3, 2, 12, 23,
                GopPixelFormat.PixelBlueGreenRedReserved8BitPerColor,
                0, 0, 0, 0, out _), "short capacity rejected");
            Check(!CreateLayout(address, 0, 2, 12, 24,
                GopPixelFormat.PixelBlueGreenRedReserved8BitPerColor,
                0, 0, 0, 0, out _), "zero width rejected");
            Check(!CreateLayout(address, 3, 2, 12, 24,
                GopPixelFormat.PixelBltOnly, 0, 0, 0, 0, out _),
                "BltOnly rejected");
            Check(!CreateLayout(0x00007FFFFFFFFFFCUL, 1, 1, 4, 8,
                GopPixelFormat.PixelBlueGreenRedReserved8BitPerColor,
                0, 0, 0, 0, out _), "canonical range overflow rejected");
            Check(!CreateLayout(address, 3, 2, 12, 24,
                GopPixelFormat.PixelBitMask,
                0x00FF0000, 0x00FF0000, 0x000000FF, 0xFF000000,
                out _), "overlapping channel masks rejected");
            Check(!CreateLayout(address, 3, 2, 12, 24,
                GopPixelFormat.PixelBitMask,
                0x00F50000, 0x0000FF00, 0x000000FF, 0xFF000000,
                out _), "noncontiguous channel mask rejected");
            Check(CreateLayout(address, 3, 2, 12, 24,
                GopPixelFormat.PixelBitMask,
                0x3FF00000, 0x000FFC00, 0x000003FF, 0xC0000000,
                out _), "valid 10:10:10 masks accepted");

            Check(!FramebufferSurface.TryCreateLayout(address, 1,
                uint.MaxValue, uint.MaxValue - 3, ulong.MaxValue,
                (uint)GopPixelFormat.PixelBlueGreenRedReserved8BitPerColor,
                0, 0, 0, 0, out _),
                "near-maximum pitch/height product is rejected before pointer access");
        }
    }

    private static void TestPackedAndPaddedSurfaces() {
        const int prefix = 16;
        const int width = 3;
        const int height = 2;
        const uint pitch = 20;
        const int capacity = (int)(pitch * height);
        var bytes = NewCanaryBuffer(prefix, capacity, 16);

        fixed (byte* p = bytes) {
            byte* basePointer = p + prefix;
            FramebufferSurfaceLayout layout = MakeLayout(basePointer, width,
                height, pitch, capacity,
                GopPixelFormat.PixelBlueGreenRedReserved8BitPerColor);
            var graphics = new Graphics(width, height, basePointer);
            Check(graphics.ConfigurePhysicalSurface(layout),
                "physical surface configured");
            graphics.SetPhysicalSurfaceActive(true);
            Check(graphics.HasPhysicalSurface, "physical surface active");

            graphics.Clear(0xFF101010);
            graphics.DrawPoint(0, 0, Red);
            graphics.DrawPoint(width - 1, height - 1, Blue);
            graphics.FillRectangle(-1, -1, 3, 2, Green);
            Check(graphics.GetPoint(0, 0) == Green,
                "clipped fill reaches visible first pixel");
            Check(graphics.GetPoint(1, 0) == Green,
                "clipped fill reaches visible adjacent pixel");
            Check(graphics.GetPoint(2, 0) == 0xFF101010,
                "clipped fill does not exceed right edge");
            Check(graphics.GetPoint(2, 1) == Blue,
                "last visible pixel and row round trip");
            Check(graphics.GetPoint(-1, 0) == 0 &&
                graphics.GetPoint(width, 0) == 0 &&
                graphics.GetPoint(0, height) == 0,
                "out-of-range reads are safe");
            graphics.DrawPoint(-1, 0, Red);
            graphics.DrawPoint(width, height - 1, Red);
            AssertCanariesAndPadding(bytes, prefix, capacity, pitch, width, height,
                "point/fill operations");

            graphics.Clear(0xFF000000);
            Check(graphics.GetPoint(0, 0) == 0xFF000000 &&
                graphics.GetPoint(width - 1, height - 1) == 0xFF000000,
                "full physical clear covers visible pixels");
            AssertCanariesAndPadding(bytes, prefix, capacity, pitch, width, height,
                "physical clear");

            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    graphics.DrawPoint(x, y,
                        0xFF000000u | (uint)(y * 16 + x + 1));
            graphics.Copy(0, 0, 0, 1, width, 1);
            Check(graphics.GetPoint(0, 0) == 0xFF000011u &&
                graphics.GetPoint(2, 0) == 0xFF000013u,
                "physical copy respects pitch and clipping");
            graphics.Copy(1, 0, 0, 0, width - 1, 1);
            Check(graphics.GetPoint(1, 0) == 0xFF000011u &&
                graphics.GetPoint(2, 0) == 0xFF000012u,
                "overlapping physical copy preserves source pixels");
            AssertCanariesAndPadding(bytes, prefix, capacity, pitch, width, height,
                "physical copy");
        }

        TestPackedPaddedVisibleEquivalence();
    }

    private static void TestPackedPaddedVisibleEquivalence() {
        const int width = 7;
        const int height = 5;
        byte[] packedBytes = new byte[width * height * 4];
        byte[] paddedBytes = NewCanaryBuffer(8, (width * 4 + 12) * height, 8);
        fixed (byte* packed = packedBytes)
        fixed (byte* padded = paddedBytes) {
            var packedLayout = MakeLayout(packed, width, height, width * 4,
                (ulong)(width * height * 4),
                GopPixelFormat.PixelBlueGreenRedReserved8BitPerColor);
            uint paddedPitch = (uint)(width * 4 + 12);
            var paddedLayout = MakeLayout(padded + 8, width, height,
                paddedPitch, (ulong)(paddedPitch * height),
                GopPixelFormat.PixelBlueGreenRedReserved8BitPerColor);
            var tightGraphics = new Graphics(width, height, packed);
            var strideGraphics = new Graphics(width, height, padded + 8);
            Check(tightGraphics.ConfigurePhysicalSurface(packedLayout) &&
                strideGraphics.ConfigurePhysicalSurface(paddedLayout),
                "packed and padded graphics surfaces configured");
            tightGraphics.SetPhysicalSurfaceActive(true);
            strideGraphics.SetPhysicalSurfaceActive(true);
            tightGraphics.Clear(0xFF202020);
            strideGraphics.Clear(0xFF202020);
            tightGraphics.FillRectangle(-2, 1, 6, 3, Red);
            strideGraphics.FillRectangle(-2, 1, 6, 3, Red);
            tightGraphics.DrawPoint(width - 1, height - 1, Blue);
            strideGraphics.DrawPoint(width - 1, height - 1, Blue);
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    Check(tightGraphics.GetPoint(x, y) ==
                        strideGraphics.GetPoint(x, y),
                        $"packed/padded visible image match at {x},{y}");
            AssertCanariesAndPadding(paddedBytes, 8,
                paddedPitch * height, paddedPitch, width, height,
                "equivalent padded render");
        }
    }

    private static void TestLargeNonPowerOfTwoPitch() {
        const int width = 1366;
        const int height = 3;
        const uint pitch = 1408 * 4;
        const int capacity = (int)(pitch * height);
        const int prefix = 16;
        byte[] bytes = NewCanaryBuffer(prefix, capacity, 16);
        fixed (byte* p = bytes) {
            byte* basePointer = p + prefix;
            FramebufferSurfaceLayout layout = MakeLayout(basePointer, width,
                height, pitch, capacity,
                GopPixelFormat.PixelBlueGreenRedReserved8BitPerColor);
            var graphics = new Graphics(width, height, basePointer);
            Check(graphics.ConfigurePhysicalSurface(layout),
                "1366x3 padded physical layout accepted");
            graphics.SetPhysicalSurfaceActive(true);
            for (int y = 0; y < height; y++) {
                graphics.DrawPoint(0, y, Red);
                graphics.DrawPoint(width - 1, y, Blue);
                Check(FramebufferSurface.TryGetByteOffset(layout, width - 1,
                    y, out ulong offset), "last pixel byte offset valid");
                Check(offset == (ulong)y * pitch + (ulong)(width - 1) * 4,
                    "last pixel follows actual physical pitch");
            }
            Check(graphics.GetPoint(0, 2) == Red &&
                graphics.GetPoint(width - 1, 2) == Blue,
                "first and last pixel on final row are addressable");
            AssertCanariesAndPadding(bytes, prefix, capacity, pitch, width, height,
                "1366-wide framebuffer");
        }
    }

    private static void TestPixelFormats() {
        const uint color = 0xFF123456;
        var buffer = new byte[128];
        fixed (byte* p = buffer) {
            var bgra = MakeLayout(p + 16, 1, 1, 4, 4,
                GopPixelFormat.PixelBlueGreenRedReserved8BitPerColor);
            var rgba = MakeLayout(p + 32, 1, 1, 4, 4,
                GopPixelFormat.PixelRedGreenBlueReserved8BitPerColor);
            var masks = MakeLayout(p + 48, 1, 1, 4, 4,
                GopPixelFormat.PixelBitMask, 0x3FF00000, 0x000FFC00,
                0x000003FF, 0xC0000000);
            Check(FramebufferSurface.PackColor(color, bgra) == 0xFF123456,
                "BGR-reserved format writes B,G,R bytes");
            Check(FramebufferSurface.PackColor(color, rgba) == 0xFF563412,
                "RGB-reserved format writes R,G,B bytes");
            Check(FramebufferSurface.TryWritePixel((uint*)(p + 16), bgra,
                0, 0, color) && *(uint*)(p + 16) == 0xFF123456,
                "BGRA native pixel stored correctly");
            Check(FramebufferSurface.TryWritePixel((uint*)(p + 32), rgba,
                0, 0, color) && *(uint*)(p + 32) == 0xFF563412,
                "RGBA native pixel stored correctly");
            Check(FramebufferSurface.TryWritePixel((uint*)(p + 48), masks,
                0, 0, color), "valid bit-mask pixel written");
            Check(FramebufferSurface.TryReadPixel((uint*)(p + 48), masks,
                0, 0, out uint unpacked), "valid bit-mask pixel read");
            Check(ChannelDistance((unpacked >> 16) & 0xFF,
                    (color >> 16) & 0xFF) <= 1 &&
                ChannelDistance((unpacked >> 8) & 0xFF,
                    (color >> 8) & 0xFF) <= 1 &&
                ChannelDistance(unpacked & 0xFF, color & 0xFF) <= 1,
                "10:10:10 bit masks round-trip color");
        }
    }

    private static void TestChangedPixelPresenter() {
        const int width = 3;
        const int height = 2;
        const uint pitch = 20;
        const int prefix = 8;
        const int capacity = (int)(pitch * height);
        byte[] destination = NewCanaryBuffer(prefix, capacity, 8);
        uint[] current = { Red, Green, Blue, Blue, Red, Green };
        uint[] previous = { Red, Green, Blue, Blue, Red, 0xFF010203 };
        fixed (byte* p = destination)
        fixed (uint* now = current)
        fixed (uint* old = previous) {
            byte* basePointer = p + prefix;
            FramebufferSurfaceLayout layout = MakeLayout(basePointer, width,
                height, pitch, capacity,
                GopPixelFormat.PixelBlueGreenRedReserved8BitPerColor);
            Check(FramebufferSurface.Clear((uint*)basePointer, layout, Red),
                "presenter destination initialized");
            Check(FramebufferSurface.PresentChanged((uint*)basePointer,
                layout, now, old), "changed pixels presented");
            Check(*(uint*)(basePointer + pitch + 8) == 0xFF00FF00,
                "changed last-row pixel updated at pitch offset");
            Check(*(uint*)(basePointer + pitch) == 0xFFFF0000,
                "unchanged previous pixel remains intact");
            AssertCanariesAndPadding(destination, prefix, capacity, pitch,
                width, height, "changed-pixel presenter");
        }
    }

    private static FramebufferSurfaceLayout MakeLayout(byte* basePointer,
        uint width, uint height, uint pitch, ulong capacity,
        GopPixelFormat format, uint redMask = 0, uint greenMask = 0,
        uint blueMask = 0, uint reservedMask = 0) {
        Check(CreateLayout((ulong)basePointer, width, height, pitch, capacity,
            format, redMask, greenMask, blueMask, reservedMask,
            out FramebufferSurfaceLayout layout), "test surface layout valid");
        return layout;
    }

    private static bool CreateLayout(ulong address, uint width, uint height,
        uint pitch, ulong capacity, GopPixelFormat format,
        uint redMask, uint greenMask, uint blueMask, uint reservedMask,
        out FramebufferSurfaceLayout layout) =>
        FramebufferSurface.TryCreateLayout(address, width, height, pitch,
            capacity, (uint)format, redMask, greenMask, blueMask,
            reservedMask, out layout);

    private static byte[] NewCanaryBuffer(int prefix, int capacity, int suffix) {
        var bytes = new byte[prefix + capacity + suffix];
        Array.Fill(bytes, Canary);
        return bytes;
    }

    private static void AssertCanariesAndPadding(byte[] bytes, int prefix,
        ulong capacity, uint pitch, uint width, uint height, string context) {
        for (int i = 0; i < prefix; i++)
            Check(bytes[i] == Canary, $"{context}: leading canary intact");
        int capacityEnd = checked(prefix + (int)capacity);
        for (int i = capacityEnd; i < bytes.Length; i++)
            Check(bytes[i] == Canary, $"{context}: trailing canary intact");
        uint visibleRowBytes = width * 4;
        for (uint y = 0; y < height; y++) {
            ulong start = (ulong)prefix + (ulong)y * pitch + visibleRowBytes;
            ulong end = (ulong)prefix + (ulong)(y + 1) * pitch;
            for (ulong offset = start; offset < end; offset++)
                Check(bytes[(long)offset] == Canary,
                    $"{context}: row {y} padding intact");
        }
    }

    private static uint ChannelDistance(uint a, uint b) =>
        a > b ? a - b : b - a;

    private static void Check(bool condition, string message) {
        _assertions++;
        if (!condition) throw new InvalidOperationException(message);
    }
}
