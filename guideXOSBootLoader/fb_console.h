#pragma once
#include "guidexOSBootInfo.h"
#include "gop_framebuffer.h"
#include <stdint.h>

// Minimal framebuffer text console that works after ExitBootServices.
// Assumptions / design:
// - Uses the validated direct-color GOP layout carried by BootInfo v1.
// - No UEFI services, no dynamic allocation, no CRT/STL.
// - Writes pixels directly to the framebuffer.
// - Fixed 8x16 monochrome bitmap font, ASCII 32..126 only.
// - Safe to call from both bootloader (after ExitBootServices) and kernel
//   as long as both use the same `VBEInfo` and initialize once via `Init`.
// - Uses the shared GOP pitch/format accessor and fails closed if the format
//   or mask metadata is not available to this legacy panic-console path.

class fb_console
{
public:
    enum class PixelFormat
    {
        Unknown,
        ValidatedGop32
    };

    // Initialize console with BootInfo from bootloader or kernel.
    // Can be safely called multiple times; last call wins.
    static bool Init(const guideXOS::BootInfo* bootInfo);

    // Write a single ASCII character. Supports: '\n', '\r'.
    static void PutChar(char c);

    // Write a null-terminated ASCII string.
    static void Write(const char* str);

    // Clear screen and reset cursor to (0,0).
    static void Clear();

private:
    static const guideXOS::BootInfo* s_bootInfo;
    static volatile unsigned char* s_fbBase;
    static unsigned int s_width;
    static unsigned int s_height;
    static PixelFormat s_format;
    static guideXOS::boot::GopFramebufferLayout s_layout;

    static unsigned int s_cursorX; // in character cells
    static unsigned int s_cursorY; // in character cells
    static const unsigned int s_charW; // 8
    static const unsigned int s_charH; // 16

    // Text color (foreground) and background in 0xRRGGBB.
    static unsigned int s_fgColor;
    static unsigned int s_bgColor;

    static void NewLine();
    static void ScrollIfNeeded();
    static void DrawGlyph(unsigned int x, unsigned int y, char c);
    static void PutPixel(unsigned int x, unsigned int y, unsigned int rgb);
    // Returns bitmap row for given character and scanline (0..15). Bit 7 is leftmost pixel.
    static unsigned char GetFontRow(char c, unsigned int row);
};

