using System.Collections.Generic;

namespace System {
    /// <summary>
    /// String pooling/interning system to prevent memory leaks from repeated string allocations.
    /// Commonly used strings (percentages, common numbers, etc.) are cached and reused.
    /// </summary>
    public static class StringPool {
        // Pre-cached percentage strings (0% to 100%)
        private static string[] _percentageStrings;
        
        // Pre-cached number strings (0 to 10000) - increased from 1000
        private static string[] _numberStrings;
        
        // Pre-cached common MB/GB strings - simplified to avoid Dictionary issues
        private static string[] _memorySizeCache;
        private static ulong[] _memorySizeCacheKeys;
        private static int _memorySizeCacheCount;
        
        // Maximum cache sizes - significantly increased
        private const int MAX_PERCENTAGE = 100;
        private const int MAX_NUMBER_CACHE = 10000;  // 10K numbers (was 1000)
        private const int MAX_MEMORY_CACHE = 2000;    // 2K memory sizes (was 200)
        
        // Initialization flag
        private static bool _initialized = false;

        
        // Common string literals (no allocation, just references)
        private static string _kbSuffix = " KB";
        private static string _mbSuffix = " MB";
        private static string _gbSuffix = " GB";
        private static string _kbpsSuffix = " KB/s";
        private static string _mbpsSuffix = " MB/s";
        private static string _msSuffix = " ms";
        private static string _percentSuffix = "%";
        private static string _colon = ":";
        private static string _zero = "0";
        
        static StringPool() {
            try {
                // Only allocate arrays - no complex initialization
                _percentageStrings = new string[MAX_PERCENTAGE + 1];
                _numberStrings = new string[MAX_NUMBER_CACHE + 1];
                _memorySizeCache = new string[MAX_MEMORY_CACHE];
                _memorySizeCacheKeys = new ulong[MAX_MEMORY_CACHE];
                _memorySizeCacheCount = 0;
                
                _initialized = true;
            } catch {
                _initialized = false;
            }
        }
        
        /// <summary>
        /// Get a cached percentage string (e.g., "42%")
        /// </summary>
        public static string GetPercentage(int value) {
#if UEFI_DIAGNOSTIC_RING3_PHASE35
            uint previousStringSite =
                Allocator.CurrentDiagnosticStringSite;
            Allocator.CurrentDiagnosticStringSite = 414;
            try {
#endif
            if (!_initialized) {
                return value.ToString() + _percentSuffix;
            }
            
            // Clamp value to valid range
            if (value < 0) value = 0;
            if (value > MAX_PERCENTAGE) value = MAX_PERCENTAGE;
            
            // Lazy populate on first access
            if (_percentageStrings[value] == null) {
                _percentageStrings[value] = value.ToString() + _percentSuffix;
            }
            
            return _percentageStrings[value];
#if UEFI_DIAGNOSTIC_RING3_PHASE35
            } finally {
                Allocator.CurrentDiagnosticStringSite =
                    previousStringSite;
            }
#endif
        }
        
        /// <summary>
        /// Get a cached number string
        /// </summary>
        public static string GetNumber(int value) {
#if UEFI_DIAGNOSTIC_RING3_PHASE35
            uint previousStringSite =
                Allocator.CurrentDiagnosticStringSite;
            Allocator.CurrentDiagnosticStringSite = 412;
            try {
#endif
            if (!_initialized) {
                return value.ToString();
            }
            
            // Check valid range
            if (value < 0 || value > MAX_NUMBER_CACHE) {
                return value.ToString();
            }
            
            // Lazy populate on first access
            if (_numberStrings[value] == null) {
                _numberStrings[value] = value.ToString();
            }
            
            return _numberStrings[value];
#if UEFI_DIAGNOSTIC_RING3_PHASE35
            } finally {
                Allocator.CurrentDiagnosticStringSite =
                    previousStringSite;
            }
#endif
        }
        
        /// <summary>
        /// Get a cached number string (ulong version)
        /// </summary>
        public static string GetNumber(ulong value) {
#if UEFI_DIAGNOSTIC_RING3_PHASE35
            uint previousStringSite =
                Allocator.CurrentDiagnosticStringSite;
            Allocator.CurrentDiagnosticStringSite = 413;
            try {
#endif
            if (!_initialized) {
                return value.ToString();
            }
            
            // Check valid range
            if (value > (ulong)MAX_NUMBER_CACHE) {
                return value.ToString();
            }
            
            int intVal = (int)value;
            
            // Lazy populate on first access
            if (_numberStrings[intVal] == null) {
                _numberStrings[intVal] = value.ToString();
            }
            
            return _numberStrings[intVal];
#if UEFI_DIAGNOSTIC_RING3_PHASE35
            } finally {
                Allocator.CurrentDiagnosticStringSite =
                    previousStringSite;
            }
#endif
        }
        
        /// <summary>
        /// Get or cache a memory size string (e.g., "42 MB")
        /// Uses simple array-based cache instead of Dictionary to avoid initialization issues
        /// </summary>
        public static string GetMemorySize(ulong bytes) {
#if UEFI_DIAGNOSTIC_RING3_PHASE35
            uint previousStringSite =
                Allocator.CurrentDiagnosticStringSite;
            Allocator.CurrentDiagnosticStringSite = 411;
            try {
#endif
            if (!_initialized) {
                ulong mbFallback = bytes / (1024UL * 1024UL);
                if (mbFallback == 0) {
                    return (bytes / 1024UL).ToString() + _kbSuffix;
                }
                return mbFallback.ToString() + _mbSuffix;
            }
            
            // Check cache first (linear search, but optimized for common cases)
            // Most memory sizes are clustered, so we often find within first few entries
            for (int i = 0; i < _memorySizeCacheCount; i++) {
                if (_memorySizeCacheKeys[i] == bytes) {
                    return _memorySizeCache[i];
                }
            }
            
            // Not in cache, compute it
            string result;
            ulong mb = bytes / (1024UL * 1024UL);
            ulong gb = mb / 1024UL;
            
            if (gb > 0) {
                // GB range
                if (gb <= (ulong)MAX_NUMBER_CACHE) {
                    result = GetNumber(gb) + _gbSuffix;
                } else {
                    result = gb.ToString() + _gbSuffix;
                }
            } else if (mb == 0) {
                // KB range
                ulong kb = bytes / 1024UL;
                if (kb == 0) {
                    result = _zero + _kbSuffix;
                } else if (kb <= (ulong)MAX_NUMBER_CACHE) {
                    result = GetNumber(kb) + _kbSuffix;
                } else {
                    result = kb.ToString() + _kbSuffix;
                }
            } else if (mb <= (ulong)MAX_NUMBER_CACHE) {
                // MB range (most common)
                result = GetNumber(mb) + _mbSuffix;
            } else {
                result = mb.ToString() + _mbSuffix;
            }
            
            // Add to cache if we have room
            if (_memorySizeCacheCount < MAX_MEMORY_CACHE) {
                _memorySizeCacheKeys[_memorySizeCacheCount] = bytes;
                _memorySizeCache[_memorySizeCacheCount] = result;
                _memorySizeCacheCount++;
            }
            
            return result;
#if UEFI_DIAGNOSTIC_RING3_PHASE35
            } finally {
                Allocator.CurrentDiagnosticStringSite =
                    previousStringSite;
            }
#endif
        }
        
        /// <summary>
        /// Get a cached KB/s or MB/s string
        /// </summary>
        public static string GetTransferRate(int kbps) {
#if UEFI_DIAGNOSTIC_RING3_PHASE35
            uint previousStringSite =
                Allocator.CurrentDiagnosticStringSite;
            Allocator.CurrentDiagnosticStringSite = 415;
            try {
#endif
            if (!_initialized) {
                if (kbps >= 1024) {
                    return (kbps / 1024).ToString() + _mbpsSuffix;
                }
                return kbps.ToString() + _kbpsSuffix;
            }
            
            if (kbps >= 1024) {
                int mbps = kbps / 1024;
                string mbpsStr = GetNumber(mbps);
                return mbpsStr + _mbpsSuffix;
            }
            string kbpsStr = GetNumber(kbps);
            return kbpsStr + _kbpsSuffix;
#if UEFI_DIAGNOSTIC_RING3_PHASE35
            } finally {
                Allocator.CurrentDiagnosticStringSite =
                    previousStringSite;
            }
#endif
        }
        
        /// <summary>
        /// Format uptime string efficiently (HH:MM:SS)
        /// </summary>
        public static string FormatUptime(ulong ticks) {
#if UEFI_DIAGNOSTIC_RING3_PHASE35
            uint previousStringSite =
                Allocator.CurrentDiagnosticStringSite;
            Allocator.CurrentDiagnosticStringSite = 416;
            try {
#endif
            ulong totalSec = ticks / 1000UL;
            ulong s = totalSec % 60UL;
            ulong m = totalSec / 60UL % 60UL;
            ulong h = totalSec / 3600UL;
            
            if (!_initialized) {
                string hStr = h.ToString();
                string mStr = m < 10 ? _zero + m.ToString() : m.ToString();
                string sStr = s < 10 ? _zero + s.ToString() : s.ToString();
                return hStr + _colon + mStr + _colon + sStr;
            }
            
            // Use pooled number strings (lazy-populated)
            string hStrPool = GetNumber(h);
            string mStrPool = m < 10 ? _zero + GetNumber(m) : GetNumber(m);
            string sStrPool = s < 10 ? _zero + GetNumber(s) : GetNumber(s);
            
            // Concatenate efficiently
            string result = hStrPool + _colon + mStrPool + _colon + sStrPool;
            
            return result;
#if UEFI_DIAGNOSTIC_RING3_PHASE35
            } finally {
                Allocator.CurrentDiagnosticStringSite =
                    previousStringSite;
            }
#endif
        }
        
        /// <summary>
        /// Build a string with percentage suffix efficiently
        /// </summary>
        public static string WithPercentageSuffix(int value) {
            return GetPercentage(value);
        }
        
        /// <summary>
        /// Build a string with unit suffix efficiently
        /// </summary>
        public static string WithSuffix(int value, string suffix) {
#if UEFI_DIAGNOSTIC_RING3_PHASE35
            uint previousStringSite =
                Allocator.CurrentDiagnosticStringSite;
            Allocator.CurrentDiagnosticStringSite = 417;
            try {
#endif
            string numStr = GetNumber(value);
            return numStr + suffix;
#if UEFI_DIAGNOSTIC_RING3_PHASE35
            } finally {
                Allocator.CurrentDiagnosticStringSite =
                    previousStringSite;
            }
#endif
        }
        
        /// <summary>
        /// Clear the memory size cache
        /// </summary>
        public static void ClearMemorySizeCache() {
            if (_initialized) {
                _memorySizeCacheCount = 0;
            }
        }
        
        /// <summary>
        /// Get cache statistics for debugging
        /// </summary>
        public static string GetCacheStats() {
            if (!_initialized) {
                return "StringPool not initialized";
            }
            
            // Count how many entries are actually populated
            int percentPopulated = 0;
            for (int i = 0; i <= MAX_PERCENTAGE; i++) {
                if (_percentageStrings[i] != null) percentPopulated++;
            }
            
            int numberPopulated = 0;
            for (int i = 0; i <= MAX_NUMBER_CACHE; i++) {
                if (_numberStrings[i] != null) numberPopulated++;
            }
            
            return "StringPool - Pct: " + percentPopulated.ToString() + "/" + (MAX_PERCENTAGE + 1).ToString() + 
                   ", Num: " + numberPopulated.ToString() + "/" + (MAX_NUMBER_CACHE + 1).ToString() +
                   ", Mem: " + _memorySizeCacheCount.ToString() + "/" + MAX_MEMORY_CACHE.ToString();
        }
    }
}
