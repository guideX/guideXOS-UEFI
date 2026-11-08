namespace guideXOS.Graph {
    /// <summary>
    /// UEFI GOP pixel formats. These values are part of the UEFI ABI.
    /// </summary>
    public enum GopPixelFormat : uint {
        PixelRedGreenBlueReserved8BitPerColor = 0,
        PixelBlueGreenRedReserved8BitPerColor = 1,
        PixelBitMask = 2,
        PixelBltOnly = 3,
    }

    /// <summary>
    /// Validated description of a directly addressable 32-bit GOP framebuffer.
    /// Width/height are visible pixels; PitchBytes is the physical scanline size.
    /// </summary>
    public struct FramebufferSurfaceLayout {
        public uint Width;
        public uint Height;
        public uint PitchBytes;
        public ulong CapacityBytes;
        public ulong BaseAddress;
        public uint PixelFormat;
        public uint RedMask;
        public uint GreenMask;
        public uint BlueMask;
        public uint ReservedMask;
        internal uint RedMaximum;
        internal uint GreenMaximum;
        internal uint BlueMaximum;
        internal byte RedShift;
        internal byte GreenShift;
        internal byte BlueShift;
        internal byte ReservedShift;
        internal byte RedBits;
        internal byte GreenBits;
        internal byte BlueBits;
        internal byte ReservedBits;
        internal bool Validated;
    }

    /// <summary>
    /// Bounds-checked access to a physical GOP framebuffer. Logical images and
    /// back buffers remain tightly packed and should not use these functions.
    /// </summary>
    public static unsafe class FramebufferSurface {
        private const ulong MaximumIdentityMappedAddress = 0x00007FFFFFFFFFFFUL;
        private const ulong MaximumUInt64 = 0xFFFFFFFFFFFFFFFFUL;

        public static bool TryCreateLayout(
            ulong baseAddress,
            uint visibleWidth,
            uint visibleHeight,
            uint pitchBytes,
            ulong capacityBytes,
            uint pixelFormat,
            uint redMask,
            uint greenMask,
            uint blueMask,
            uint reservedMask,
            out FramebufferSurfaceLayout layout) {
            layout = default;
            if (baseAddress == 0 || visibleWidth == 0 || visibleHeight == 0 ||
                visibleWidth > 0x7FFFFFFFu || visibleHeight > 0x7FFFFFFFu ||
                pitchBytes == 0 || (pitchBytes & 3u) != 0 || capacityBytes < 4 ||
                baseAddress > MaximumIdentityMappedAddress ||
                capacityBytes - 1 > MaximumIdentityMappedAddress - baseAddress ||
                (ulong)visibleWidth > MaximumUInt64 / 4UL) return false;

            ulong visibleRowBytes = (ulong)visibleWidth * 4UL;
            if (pitchBytes < visibleRowBytes ||
                (ulong)visibleHeight > MaximumUInt64 / pitchBytes) return false;

            ulong requiredBytes = (ulong)pitchBytes * visibleHeight;
            if (requiredBytes == 0 || requiredBytes > capacityBytes) return false;

            layout.BaseAddress = baseAddress;
            layout.Width = visibleWidth;
            layout.Height = visibleHeight;
            layout.PitchBytes = pitchBytes;
            layout.CapacityBytes = capacityBytes;
            layout.PixelFormat = pixelFormat;
            layout.RedMask = redMask;
            layout.GreenMask = greenMask;
            layout.BlueMask = blueMask;
            layout.ReservedMask = reservedMask;

            if (pixelFormat == (uint)GopPixelFormat.PixelRedGreenBlueReserved8BitPerColor ||
                pixelFormat == (uint)GopPixelFormat.PixelBlueGreenRedReserved8BitPerColor) {
                layout.Validated = true;
                return true;
            }

            if (pixelFormat != (uint)GopPixelFormat.PixelBitMask ||
                redMask == 0 || greenMask == 0 || blueMask == 0 ||
                !TryGetMaskFields(redMask, false, out layout.RedShift,
                    out layout.RedBits, out layout.RedMaximum) ||
                !TryGetMaskFields(greenMask, false, out layout.GreenShift,
                    out layout.GreenBits, out layout.GreenMaximum) ||
                !TryGetMaskFields(blueMask, false, out layout.BlueShift,
                    out layout.BlueBits, out layout.BlueMaximum) ||
                (reservedMask != 0 && !TryGetMaskFields(reservedMask, true,
                    out layout.ReservedShift, out layout.ReservedBits, out _)) ||
                (redMask & greenMask) != 0 || (redMask & blueMask) != 0 ||
                (greenMask & blueMask) != 0 || (reservedMask & redMask) != 0 ||
                (reservedMask & greenMask) != 0 || (reservedMask & blueMask) != 0) {
                layout = default;
                return false;
            }

            layout.Validated = true;
            return true;
        }

        public static bool IsLayoutValid(FramebufferSurfaceLayout layout) =>
            layout.Validated && layout.Width != 0 && layout.Height != 0 &&
            layout.PitchBytes != 0 && layout.CapacityBytes >= 4 &&
            layout.BaseAddress != 0 &&
            layout.BaseAddress <= MaximumIdentityMappedAddress &&
            layout.CapacityBytes - 1 <=
                MaximumIdentityMappedAddress - layout.BaseAddress &&
            layout.PitchBytes >= (ulong)layout.Width * 4UL &&
            (layout.PitchBytes & 3u) == 0 &&
            layout.Height <= MaximumUInt64 / layout.PitchBytes &&
            layout.CapacityBytes >=
                (ulong)layout.PitchBytes * layout.Height;

        public static bool TryGetByteOffset(
            FramebufferSurfaceLayout layout, int x, int y, out ulong byteOffset) {
            byteOffset = 0;
            if (!IsLayoutValid(layout) || x < 0 || y < 0 ||
                (uint)x >= layout.Width || (uint)y >= layout.Height) return false;

            ulong rowOffset = (ulong)(uint)y * layout.PitchBytes;
            ulong pixelOffset = (ulong)(uint)x * 4UL;
            if (rowOffset > MaximumUInt64 - pixelOffset) return false;
            byteOffset = rowOffset + pixelOffset;
            return byteOffset <= layout.CapacityBytes - 4UL;
        }

        public static bool TryReadPixel(
            uint* framebuffer, FramebufferSurfaceLayout layout,
            int x, int y, out uint argb) {
            argb = 0;
            if (!IsExpectedBase(framebuffer, layout) ||
                !TryGetByteOffset(layout, x, y,
                    out ulong byteOffset)) return false;
            uint native = *(uint*)((byte*)framebuffer + (nint)byteOffset);
            argb = UnpackColor(native, layout);
            return true;
        }

        public static bool TryWritePixel(
            uint* framebuffer, FramebufferSurfaceLayout layout,
            int x, int y, uint argb) {
            if (!IsExpectedBase(framebuffer, layout) ||
                !TryGetByteOffset(layout, x, y,
                    out ulong byteOffset)) return false;
            *(uint*)((byte*)framebuffer + (nint)byteOffset) = PackColor(argb, layout);
            return true;
        }

        public static bool Clear(uint* framebuffer,
            FramebufferSurfaceLayout layout, uint argb) {
            if (!IsExpectedBase(framebuffer, layout) ||
                !IsLayoutValid(layout) ||
                !IsSupportedPixelFormat(layout)) return false;
            uint native = PackColor(argb, layout);
            for (uint y = 0; y < layout.Height; y++) {
                ulong rowOffset = (ulong)y * layout.PitchBytes;
                if (rowOffset > layout.CapacityBytes ||
                    (ulong)layout.Width * 4UL > layout.CapacityBytes - rowOffset)
                    return false;
                uint* row = (uint*)((byte*)framebuffer + (nint)rowOffset);
                for (uint x = 0; x < layout.Width; x++) row[x] = native;
            }
            return true;
        }

        public static bool FillRectangle(uint* framebuffer,
            FramebufferSurfaceLayout layout, int x, int y,
            int width, int height, uint argb) {
            if (!IsExpectedBase(framebuffer, layout) ||
                !IsLayoutValid(layout) ||
                !IsSupportedPixelFormat(layout) || width <= 0 || height <= 0)
                return false;

            long x0 = x;
            long y0 = y;
            long x1 = (long)x + width;
            long y1 = (long)y + height;
            if (x0 < 0) x0 = 0;
            if (y0 < 0) y0 = 0;
            if (x1 > layout.Width) x1 = layout.Width;
            if (y1 > layout.Height) y1 = layout.Height;
            if (x1 <= x0 || y1 <= y0) return true;

            uint native = PackColor(argb, layout);
            for (long rowIndex = y0; rowIndex < y1; rowIndex++) {
                ulong rowOffset = (ulong)rowIndex * layout.PitchBytes;
                ulong lastPixelEnd = (ulong)x1 * 4UL;
                if (rowOffset > layout.CapacityBytes ||
                    lastPixelEnd > layout.CapacityBytes - rowOffset) return false;
                uint* row = (uint*)((byte*)framebuffer + (nint)rowOffset);
                for (long column = x0; column < x1; column++)
                    row[column] = native;
            }
            return true;
        }

        public static bool PresentChanged(uint* framebuffer,
            FramebufferSurfaceLayout layout, uint* source, uint* previous) {
            if (!IsExpectedBase(framebuffer, layout) || source == null ||
                !IsLayoutValid(layout) || !IsSupportedPixelFormat(layout))
                return false;

            ulong logicalRowPixels = layout.Width;
            for (uint y = 0; y < layout.Height; y++) {
                ulong logicalRow = (ulong)y * logicalRowPixels;
                ulong physicalRowOffset = (ulong)y * layout.PitchBytes;
                if (physicalRowOffset > layout.CapacityBytes ||
                    (ulong)layout.Width * 4UL >
                        layout.CapacityBytes - physicalRowOffset) return false;
                uint* destination = (uint*)((byte*)framebuffer +
                    (nint)physicalRowOffset);
                for (uint x = 0; x < layout.Width; x++) {
                    ulong index = logicalRow + x;
                    uint color = source[index];
                    if (previous == null || color != previous[index])
                        destination[x] = PackColor(color, layout);
                }
            }
            return true;
        }

        private static bool IsExpectedBase(uint* framebuffer,
            FramebufferSurfaceLayout layout) => framebuffer != null &&
            (ulong)framebuffer == layout.BaseAddress;

        public static bool IsSupportedPixelFormat(FramebufferSurfaceLayout layout) =>
            layout.Validated &&
            (layout.PixelFormat == (uint)GopPixelFormat.PixelRedGreenBlueReserved8BitPerColor ||
             layout.PixelFormat == (uint)GopPixelFormat.PixelBlueGreenRedReserved8BitPerColor ||
             layout.PixelFormat == (uint)GopPixelFormat.PixelBitMask);

        public static uint PackColor(uint argb,
            FramebufferSurfaceLayout layout) {
            uint alpha = 0xFFu;
            uint red = (argb >> 16) & 0xFFu;
            uint green = (argb >> 8) & 0xFFu;
            uint blue = argb & 0xFFu;
            if (layout.PixelFormat ==
                    (uint)GopPixelFormat.PixelBlueGreenRedReserved8BitPerColor)
                return (alpha << 24) | (red << 16) | (green << 8) | blue;
            if (layout.PixelFormat ==
                    (uint)GopPixelFormat.PixelRedGreenBlueReserved8BitPerColor)
                return (alpha << 24) | (blue << 16) | (green << 8) | red;
            if (layout.PixelFormat != (uint)GopPixelFormat.PixelBitMask)
                return 0;

            uint packed = PackChannel((byte)red, layout.RedMask,
                    layout.RedShift, layout.RedMaximum) |
                PackChannel((byte)green, layout.GreenMask,
                    layout.GreenShift, layout.GreenMaximum) |
                PackChannel((byte)blue, layout.BlueMask,
                    layout.BlueShift, layout.BlueMaximum);
            if (layout.ReservedMask != 0) packed |= layout.ReservedMask;
            return packed;
        }

        private static uint UnpackColor(uint native,
            FramebufferSurfaceLayout layout) {
            if (layout.PixelFormat ==
                    (uint)GopPixelFormat.PixelBlueGreenRedReserved8BitPerColor)
                return 0xFF000000u | (native & 0x00FFFFFFu);
            if (layout.PixelFormat ==
                    (uint)GopPixelFormat.PixelRedGreenBlueReserved8BitPerColor) {
                uint red = native & 0xFFu;
                uint green = (native >> 8) & 0xFFu;
                uint blue = (native >> 16) & 0xFFu;
                return 0xFF000000u | (red << 16) | (green << 8) | blue;
            }

            uint r = UnpackChannel(native, layout.RedMask,
                layout.RedShift, layout.RedMaximum);
            uint g = UnpackChannel(native, layout.GreenMask,
                layout.GreenShift, layout.GreenMaximum);
            uint b = UnpackChannel(native, layout.BlueMask,
                layout.BlueShift, layout.BlueMaximum);
            return 0xFF000000u | (r << 16) | (g << 8) | b;
        }

        private static uint PackChannel(byte value, uint mask,
            byte shift, uint maximum) {
            if (mask == 0 || maximum == 0) return 0;
            ulong scaled = ((ulong)value * maximum + 127UL) / 255UL;
            return (uint)((scaled << shift) & mask);
        }

        private static uint UnpackChannel(uint value, uint mask,
            byte shift, uint maximum) {
            if (mask == 0 || maximum == 0) return 0;
            ulong component = (value & mask) >> shift;
            return (uint)((component * 255UL + maximum / 2UL) / maximum);
        }

        private static bool TryGetMaskFields(uint mask, bool optional,
            out byte shift, out byte bits, out uint maximum) {
            shift = 0;
            bits = 0;
            maximum = 0;
            if (mask == 0) return optional;

            uint shifted = mask;
            while ((shifted & 1u) == 0) {
                shifted >>= 1;
                shift++;
            }
            uint run = shifted;
            while ((run & 1u) != 0) {
                run >>= 1;
                bits++;
            }
            if (run != 0 || bits == 0) return false;
            maximum = bits == 32 ? 0xFFFFFFFFu : (1u << bits) - 1u;
            return true;
        }
    }
}
