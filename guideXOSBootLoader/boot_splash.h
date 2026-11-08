#pragma once

#include <Uefi.h>
#include "gop_framebuffer.h"

namespace guideXOS {
namespace boot {

/// <summary>
/// Simple boot splash screen handler
/// </summary>
class BootSplash {
public:
    /// <summary>
    /// Draw a simple guideXOS boot splash on the framebuffer
    /// Draws a teal gradient background
    /// </summary>
    static void DrawSplash(
        const GopFramebufferLayout& layout
    );

private:
    /// <summary>
    /// Simple 8x8 bitmap font for boot messages (placeholder)
    /// </summary>
    static const UINT8 SimpleFontData[256 * 8];
};

} // namespace boot
} // namespace guideXOS



