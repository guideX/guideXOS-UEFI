namespace guideXOS.Misc {
    /// <summary>
    /// Reads and writes font preference keys in the existing guideXOS
    /// key=value configuration file. Missing keys are an older configuration
    /// and retain the bitmap default.
    /// </summary>
    internal static class SharedFontPreferenceConfig {
        internal static bool TryRead(string content,
                                     out SharedFontProviderKind provider,
                                     out int trueTypeSize,
                                     out bool hasFontSettings) {
            provider = SharedFontProviderKind.BitmapAtlas;
            trueTypeSize = SharedTextFontSelection.DefaultTrueTypeFontSize;
            hasFontSettings = false;
            if (content == null) return true;

            string providerValue = FindValue(content, "font_provider=");
            string sizeValue = FindValue(content, "font_size=");
            hasFontSettings = providerValue != null || sizeValue != null;
            if (!hasFontSettings) return true;

            SharedFontProviderKind parsedProvider;
            bool validProvider = TryParseProvider(providerValue,
                out parsedProvider);
            int parsedSize;
            bool validSize = TryParseInteger(sizeValue, out parsedSize) &&
                SharedTextFont.IsSupportedTrueTypeSize(parsedSize);

            if (validSize) trueTypeSize = parsedSize;
            if (validProvider && validSize) provider = parsedProvider;
            return validProvider && validSize;
        }

        internal static string Append(string content,
                                      SharedFontProviderKind provider,
                                      int trueTypeSize) {
            if (content == null) content = string.Empty;
            if (!SharedTextFont.IsSupportedTrueTypeSize(trueTypeSize))
                trueTypeSize = SharedTextFontSelection.DefaultTrueTypeFontSize;
            if (provider != SharedFontProviderKind.TrueType)
                provider = SharedFontProviderKind.BitmapAtlas;

            string result = content;
            if (result.Length > 0 && result[result.Length - 1] != '\n' &&
                    result[result.Length - 1] != '\r') result += "\n";
            result += "font_provider=" +
                (provider == SharedFontProviderKind.TrueType
                    ? "truetype" : "bitmap") + "\n";
            result += "font_size=" + trueTypeSize.ToString() + "\n";
            return result;
        }

        private static string FindValue(string content, string key) {
            int lineStart = 0;
            while (lineStart < content.Length) {
                int lineEnd = lineStart;
                while (lineEnd < content.Length && content[lineEnd] != '\n' &&
                        content[lineEnd] != '\r') lineEnd++;
                int keyEnd = lineStart + key.Length;
                if (lineEnd >= keyEnd && Matches(content, lineStart, key))
                    return content.Substring(keyEnd, lineEnd - keyEnd);
                while (lineEnd < content.Length &&
                        (content[lineEnd] == '\n' || content[lineEnd] == '\r'))
                    lineEnd++;
                lineStart = lineEnd;
            }
            return null;
        }

        private static bool TryParseProvider(string value,
                out SharedFontProviderKind provider) {
            provider = SharedFontProviderKind.BitmapAtlas;
            if (Matches(value, 0, "bitmap") && value.Length == 6) return true;
            if (Matches(value, 0, "truetype") && value.Length == 8) {
                provider = SharedFontProviderKind.TrueType;
                return true;
            }
            return false;
        }

        private static bool TryParseInteger(string value, out int parsed) {
            parsed = 0;
            if (value == null || value.Length == 0) return false;
            for (int i = 0; i < value.Length; i++) {
                char current = value[i];
                if (current < '0' || current > '9') return false;
                int digit = current - '0';
                if (parsed > (int.MaxValue - digit) / 10) return false;
                parsed = parsed * 10 + digit;
            }
            return true;
        }

        private static bool Matches(string value, int start, string expected) {
            if (value == null || start < 0 ||
                    start > value.Length - expected.Length) return false;
            for (int i = 0; i < expected.Length; i++) {
                char actual = value[start + i];
                char wanted = expected[i];
                if (actual >= 'A' && actual <= 'Z') actual += (char)32;
                if (wanted >= 'A' && wanted <= 'Z') wanted += (char)32;
                if (actual != wanted) return false;
            }
            return true;
        }
    }
}
