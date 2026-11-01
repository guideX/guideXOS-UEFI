using guideXOS.Graph;
using guideXOS.Misc;

namespace guideXOS.GUI {
    /// <summary>
    /// Shared layout and draw adapter for the UEFI desktop's icon labels.
    /// Hosted qualification can supply an explicitly selected TrueType
    /// handle while production supplies the bitmap default handle.
    /// </summary>
    internal static class UefiDesktopLabelRenderer {
        public static bool TryMeasure(SharedTextFont font, string text,
                                      out int width, out int height) {
            width = 0;
            height = 0;
            if (font == null || text == null) return false;
            SharedTextMeasurement measurement;
            SharedFontMetrics metrics;
            if (!font.MeasureText(text, out measurement) ||
                !font.GetFontMetrics(out metrics)) return false;
            width = measurement.Width;
            height = metrics.LineHeight;
            return true;
        }

        public static bool Draw(Graphics graphics, SharedTextFont font,
                                UefiDesktopIconGeometry geometry,
                                string text, uint color = 0xFFFFFFFFu) {
            if (graphics == null || font == null || text == null) return false;
            return font.DrawText(graphics, geometry.HitX, geometry.LabelY,
                text, color, geometry.LabelX, geometry.LabelY,
                geometry.LabelWidth, geometry.LabelHeight,
                SharedTextAlignment.Center, geometry.HitWidth);
        }
    }
}
