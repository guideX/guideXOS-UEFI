using System;
using guideXOS.GUI;
using guideXOS.Misc;

namespace guideXOS.FontTests {
    internal static class FontPreferenceConfigurationChecks {
        private static int _checks;
        private static int _failures;

        public static int Run(byte[] fontBytes, IFont bitmap) {
            _checks = 0;
            _failures = 0;

            SharedFontProviderKind provider;
            int size;
            bool hasSettings;
            bool legacyRead = SharedFontPreferenceConfig.TryRead(
                "icon_size=32\nshow_widgets_on_startup=true\n",
                out provider, out size, out hasSettings);
            Check(legacyRead && !hasSettings &&
                  provider == SharedFontProviderKind.BitmapAtlas && size == 20,
                  "older configuration without font keys keeps Bitmap defaults");

            bool trueTypeRead = SharedFontPreferenceConfig.TryRead(
                "icon_size=32\nfont_provider=truetype\nfont_size=14\n",
                out provider, out size, out hasSettings);
            Check(trueTypeRead && hasSettings &&
                  provider == SharedFontProviderKind.TrueType && size == 14,
                  "TrueType provider and supported size load from existing key-value settings");

            string persisted = SharedFontPreferenceConfig.Append(
                "icon_size=48\nshow_widgets_on_startup=false\n",
                SharedFontProviderKind.TrueType, 18);
            bool roundTrip = SharedFontPreferenceConfig.TryRead(persisted,
                out provider, out size, out hasSettings);
            Check(roundTrip && hasSettings &&
                  provider == SharedFontProviderKind.TrueType && size == 18 &&
                  persisted.IndexOf("icon_size=48\n",
                      StringComparison.Ordinal) >= 0 &&
                  persisted.IndexOf("font_provider=truetype\n",
                      StringComparison.Ordinal) >= 0 &&
                  persisted.IndexOf("font_size=18\n",
                      StringComparison.Ordinal) >= 0,
                  "font preference round-trip retains existing settings keys");

            SharedTextFontSelection restoredSelection =
                new SharedTextFontSelection();
            SharedTextFont restoredFont = restoredSelection.Resolve(bitmap);
            bool restoredProvider = roundTrip &&
                restoredSelection.TrySelectTrueType(fontBytes, size, bitmap,
                    out restoredFont);
            Check(restoredProvider && restoredFont != null &&
                  restoredFont.Provider == SharedFontProviderKind.TrueType &&
                  restoredFont.TrueTypeFontSize == 18,
                  "loaded TrueType settings restore the provider at the saved size");
            restoredSelection.SelectBitmap(bitmap);
            Check(restoredSelection.Selected != null &&
                  restoredSelection.Selected.Provider ==
                      SharedFontProviderKind.BitmapAtlas,
                  "restored TrueType preferences return safely to Bitmap");

            bool invalidSize = SharedFontPreferenceConfig.TryRead(
                "font_provider=truetype\nfont_size=13\n",
                out provider, out size, out hasSettings);
            Check(!invalidSize && hasSettings &&
                  provider == SharedFontProviderKind.BitmapAtlas && size == 20,
                  "unsupported persisted size falls back to Bitmap defaults");

            bool invalidProvider = SharedFontPreferenceConfig.TryRead(
                "font_provider=other\nfont_size=16\n",
                out provider, out size, out hasSettings);
            Check(!invalidProvider && hasSettings &&
                  provider == SharedFontProviderKind.BitmapAtlas && size == 16,
                  "unknown provider falls back to Bitmap while retaining valid size");

            bool partial = SharedFontPreferenceConfig.TryRead(
                "font_provider=truetype\n",
                out provider, out size, out hasSettings);
            Check(!partial && hasSettings &&
                  provider == SharedFontProviderKind.BitmapAtlas && size == 20,
                  "partial persisted font settings safely select Bitmap");

            string normalized = SharedFontPreferenceConfig.Append(
                "icon_size=32\n", SharedFontProviderKind.TrueType, 13);
            bool normalizedRead = SharedFontPreferenceConfig.TryRead(normalized,
                out provider, out size, out hasSettings);
            Check(normalizedRead && provider == SharedFontProviderKind.TrueType &&
                  size == 20,
                  "writer normalizes an unsupported size before persistence");

            Console.WriteLine("Font preference configuration checks: " +
                _checks + ", failures: " + _failures);
            return _failures;
        }

        private static void Check(bool condition, string description) {
            _checks++;
            if (condition) return;
            _failures++;
            Console.Error.WriteLine("FONT SETTINGS FAIL: " + description);
        }
    }
}
