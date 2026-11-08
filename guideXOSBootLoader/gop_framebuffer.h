#pragma once

#include <Uefi.h>
#include <Protocol/GraphicsOutput.h>
#include <stdint.h>

namespace guideXOS {
namespace boot {

struct GopFramebufferLayout
{
    EFI_PHYSICAL_ADDRESS Base;
    UINT64 SizeBytes;
    UINT32 Width;
    UINT32 Height;
    UINT32 PixelsPerScanLine;
    UINT32 PitchBytes;
    UINT32 PixelFormat;
    UINT32 RedMask;
    UINT32 GreenMask;
    UINT32 BlueMask;
    UINT32 ReservedMask;
};

static constexpr UINT64 MAX_IDENTITY_MAPPED_ADDRESS =
    0x00007FFFFFFFFFFFULL;

static inline bool IsContiguousGopMask(UINT32 mask)
{
    if (mask == 0) return false;
    while ((mask & 1u) == 0) mask >>= 1;
    while ((mask & 1u) != 0) mask >>= 1;
    return mask == 0;
}

static inline bool IsSupportedGopPixelFormat(UINT32 pixelFormat,
    UINT32 redMask, UINT32 greenMask, UINT32 blueMask,
    UINT32 reservedMask)
{
    if (pixelFormat == PixelRedGreenBlueReserved8BitPerColor ||
        pixelFormat == PixelBlueGreenRedReserved8BitPerColor)
        return true;

    if (pixelFormat != PixelBitMask ||
        !IsContiguousGopMask(redMask) ||
        !IsContiguousGopMask(greenMask) ||
        !IsContiguousGopMask(blueMask) ||
        (reservedMask != 0 && !IsContiguousGopMask(reservedMask)))
        return false;

    return (redMask & greenMask) == 0 &&
        (redMask & blueMask) == 0 &&
        (greenMask & blueMask) == 0 &&
        (reservedMask & redMask) == 0 &&
        (reservedMask & greenMask) == 0 &&
        (reservedMask & blueMask) == 0;
}

static inline bool TryBuildGopFramebufferLayout(
    EFI_PHYSICAL_ADDRESS base, UINT64 sizeBytes,
    UINT32 width, UINT32 height, UINT32 pixelsPerScanLine,
    UINT32 pixelFormat, UINT32 redMask, UINT32 greenMask,
    UINT32 blueMask, UINT32 reservedMask,
    GopFramebufferLayout* outLayout)
{
    if (!outLayout) return false;
    *outLayout = {};
    if (base == 0 || sizeBytes == 0 || width == 0 || height == 0 ||
        width > 0xFFFFu || height > 0xFFFFu ||
        pixelsPerScanLine < width || pixelsPerScanLine > 0x3FFFFFFFu ||
        !IsSupportedGopPixelFormat(pixelFormat, redMask, greenMask,
            blueMask, reservedMask) ||
        base > MAX_IDENTITY_MAPPED_ADDRESS ||
        sizeBytes - 1 > MAX_IDENTITY_MAPPED_ADDRESS - base)
        return false;

    const UINT64 pitchBytes = (UINT64)pixelsPerScanLine * 4ull;
    if (pitchBytes > 0xFFFFFFFFull ||
        (UINT64)height > 0xFFFFFFFFFFFFFFFFull / pitchBytes)
        return false;
    const UINT64 requiredBytes = pitchBytes * height;
    if (requiredBytes == 0 || requiredBytes > sizeBytes ||
        base + sizeBytes > MAX_IDENTITY_MAPPED_ADDRESS - 0xFFFULL)
        return false;

    outLayout->Base = base;
    outLayout->SizeBytes = sizeBytes;
    outLayout->Width = width;
    outLayout->Height = height;
    outLayout->PixelsPerScanLine = pixelsPerScanLine;
    outLayout->PitchBytes = (UINT32)pitchBytes;
    outLayout->PixelFormat = pixelFormat;
    outLayout->RedMask = redMask;
    outLayout->GreenMask = greenMask;
    outLayout->BlueMask = blueMask;
    outLayout->ReservedMask = reservedMask;
    return true;
}

static inline bool TryGetGopPixelOffset(const GopFramebufferLayout& layout,
    UINT32 x, UINT32 y, UINT64* outByteOffset)
{
    if (!outByteOffset || layout.Base == 0 || x >= layout.Width ||
        y >= layout.Height || layout.PitchBytes == 0 ||
        layout.SizeBytes < 4) return false;
    const UINT64 rowOffset = (UINT64)y * layout.PitchBytes;
    const UINT64 pixelOffset = (UINT64)x * 4ull;
    if (rowOffset > 0xFFFFFFFFFFFFFFFFull - pixelOffset)
        return false;
    const UINT64 byteOffset = rowOffset + pixelOffset;
    if (byteOffset > layout.SizeBytes - 4ull) return false;
    *outByteOffset = byteOffset;
    return true;
}

static inline bool TryGetMaskFields(UINT32 mask, UINT32* shift,
    UINT32* bits, UINT64* maximum)
{
    if (!shift || !bits || !maximum || mask == 0) return false;
    *shift = 0;
    *bits = 0;
    *maximum = 0;
    UINT32 value = mask;
    while ((value & 1u) == 0) {
        value >>= 1;
        ++*shift;
    }
    while ((value & 1u) != 0) {
        value >>= 1;
        ++*bits;
    }
    if (value != 0 || *bits == 0) return false;
    *maximum = *bits == 32 ? 0xFFFFFFFFull :
        ((1ull << *bits) - 1ull);
    return true;
}

static inline UINT32 PackGopChannel(UINT8 value, UINT32 mask)
{
    UINT32 shift;
    UINT32 bits;
    UINT64 maximum;
    if (!TryGetMaskFields(mask, &shift, &bits, &maximum)) return 0;
    const UINT64 scaled = ((UINT64)value * maximum + 127ull) / 255ull;
    return (UINT32)((scaled << shift) & mask);
}

static inline UINT32 PackArgbForGop(UINT32 argb,
    const GopFramebufferLayout& layout)
{
    const UINT32 red = (argb >> 16) & 0xFFu;
    const UINT32 green = (argb >> 8) & 0xFFu;
    const UINT32 blue = argb & 0xFFu;
    if (layout.PixelFormat == PixelBlueGreenRedReserved8BitPerColor)
        return 0xFF000000u | (red << 16) | (green << 8) | blue;
    if (layout.PixelFormat == PixelRedGreenBlueReserved8BitPerColor)
        return 0xFF000000u | (blue << 16) | (green << 8) | red;
    if (layout.PixelFormat != PixelBitMask) return 0;

    UINT32 pixel = PackGopChannel((UINT8)red, layout.RedMask) |
        PackGopChannel((UINT8)green, layout.GreenMask) |
        PackGopChannel((UINT8)blue, layout.BlueMask);
    if (layout.ReservedMask != 0) pixel |= layout.ReservedMask;
    return pixel;
}

static inline bool TryWriteGopPixel(volatile UINT32* framebuffer,
    const GopFramebufferLayout& layout, UINT32 x, UINT32 y, UINT32 argb)
{
    UINT64 byteOffset;
    if (!framebuffer || !TryGetGopPixelOffset(layout, x, y,
            &byteOffset)) return false;
    framebuffer[byteOffset / 4ull] = PackArgbForGop(argb, layout);
    return true;
}

static inline void FillGopRect(const GopFramebufferLayout& layout,
    UINT32 x, UINT32 y, UINT32 width, UINT32 height, UINT32 argb)
{
    if (x >= layout.Width || y >= layout.Height || width == 0 || height == 0)
        return;
    UINT32 right = width > layout.Width - x ? layout.Width : x + width;
    UINT32 bottom = height > layout.Height - y ? layout.Height : y + height;
    volatile UINT32* framebuffer =
        (volatile UINT32*)(UINTN)layout.Base;
    for (UINT32 row = y; row < bottom; ++row)
        for (UINT32 column = x; column < right; ++column)
            TryWriteGopPixel(framebuffer, layout, column, row, argb);
}

} // namespace boot
} // namespace guideXOS
