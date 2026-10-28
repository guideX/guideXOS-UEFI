using System.Runtime.InteropServices;

namespace guideXOS.Misc {
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct CPUID {
        public uint EAX;
        public uint EBX;
        public uint ECX;
        public uint EDX;
    }
    
    /// <summary>
    /// CPU feature detection helper
    /// </summary>
    internal static class CPUIDHelper {
        [DllImport("*")]
        private static extern CPUID cpuid(uint leaf, uint subleaf);
        
        [DllImport("*")]
        private static extern bool cpuid_supported();
        
        /// <summary>
        /// Check if CPUID instruction is supported
        /// </summary>
        public static bool IsCPUIDSupported() {
            return cpuid_supported();
        }
        
        /// <summary>
        /// Check if CPU supports SSE (Streaming SIMD Extensions)
        /// CPUID.1:EDX.SSE[bit 25]
        /// </summary>
        public static bool HasSSE() {
            if (!IsCPUIDSupported()) return false;
            var info = cpuid(1, 0);
            return (info.EDX & (1u << 25)) != 0;
        }
        
        /// <summary>
        /// Check if CPU supports SSE2
        /// CPUID.1:EDX.SSE2[bit 26]
        /// </summary>
        public static bool HasSSE2() {
            if (!IsCPUIDSupported()) return false;
            var info = cpuid(1, 0);
            return (info.EDX & (1u << 26)) != 0;
        }
        
        /// <summary>
        /// Check if CPU supports SSE3
        /// CPUID.1:ECX.SSE3[bit 0]
        /// </summary>
        public static bool HasSSE3() {
            if (!IsCPUIDSupported()) return false;
            var info = cpuid(1, 0);
            return (info.ECX & (1u << 0)) != 0;
        }
        
        /// <summary>
        /// Check if CPU supports AVX (Advanced Vector Extensions)
        /// CPUID.1:ECX.AVX[bit 28]
        /// </summary>
        public static bool HasAVX() {
            if (!IsCPUIDSupported()) return false;
            var info = cpuid(1, 0);
            return (info.ECX & (1u << 28)) != 0;
        }
        
        /// <summary>
        /// Check if CPU supports APIC
        /// CPUID.1:EDX.APIC[bit 9]
        /// </summary>
        public static bool HasAPIC() {
            if (!IsCPUIDSupported()) return false;
            var info = cpuid(1, 0);
            return (info.EDX & (1u << 9)) != 0;
        }

        /// <summary>Check CPUID.1:EDX.PAT before reading IA32_PAT.</summary>
        public static bool HasPAT() {
            if (!IsCPUIDSupported() || GetMaxLeaf() < 1) return false;
            var info = cpuid(1, 0);
            return (info.EDX & (1u << 16)) != 0;
        }
        
        /// <summary>
        /// Check if CPU supports PAE (Physical Address Extension)
        /// CPUID.1:EDX.PAE[bit 6]
        /// </summary>
        public static bool HasPAE() {
            if (!IsCPUIDSupported()) return false;
            var info = cpuid(1, 0);
            return (info.EDX & (1u << 6)) != 0;
        }
        
        /// <summary>
        /// Check if CPU supports FXSAVE/FXRSTOR
        /// CPUID.1:EDX.FXSR[bit 24]
        /// </summary>
        public static bool HasFXSR() {
            if (!IsCPUIDSupported()) return false;
            var info = cpuid(1, 0);
            return (info.EDX & (1u << 24)) != 0;
        }
        
        /// <summary>
        /// Get CPU vendor string (GenuineIntel, AuthenticAMD, etc.)
        /// </summary>
        public static string GetVendorString() {
            if (!IsCPUIDSupported()) return "Unknown";
            
            var info = cpuid(0, 0);
            
            // Build vendor string from EBX, EDX, ECX (in that order)
            char[] vendor = new char[12];
            
            // EBX
            vendor[0] = (char)(info.EBX & 0xFF);
            vendor[1] = (char)((info.EBX >> 8) & 0xFF);
            vendor[2] = (char)((info.EBX >> 16) & 0xFF);
            vendor[3] = (char)((info.EBX >> 24) & 0xFF);
            
            // EDX
            vendor[4] = (char)(info.EDX & 0xFF);
            vendor[5] = (char)((info.EDX >> 8) & 0xFF);
            vendor[6] = (char)((info.EDX >> 16) & 0xFF);
            vendor[7] = (char)((info.EDX >> 24) & 0xFF);
            
            // ECX
            vendor[8] = (char)(info.ECX & 0xFF);
            vendor[9] = (char)((info.ECX >> 8) & 0xFF);
            vendor[10] = (char)((info.ECX >> 16) & 0xFF);
            vendor[11] = (char)((info.ECX >> 24) & 0xFF);
            
            return new string(vendor);
        }
        
        /// <summary>
        /// Get maximum supported CPUID leaf
        /// </summary>
        public static uint GetMaxLeaf() {
            if (!IsCPUIDSupported()) return 0;
            var info = cpuid(0, 0);
            return info.EAX;
        }
        
        /// <summary>
        /// Print CPU features to console
        /// </summary>
        public static void PrintCPUInfo() {
            if (!IsCPUIDSupported()) {
                BootConsole.WriteLine("[CPU] CPUID not supported");
                return;
            }
            
            BootConsole.WriteLine($"[CPU] Vendor: {GetVendorString()}");
            BootConsole.WriteLine($"[CPU] Max CPUID Leaf: 0x{GetMaxLeaf():X}");
            BootConsole.Write("[CPU] Features: ");
            
            if (HasSSE()) BootConsole.Write("SSE ");
            if (HasSSE2()) BootConsole.Write("SSE2 ");
            if (HasSSE3()) BootConsole.Write("SSE3 ");
            if (HasAVX()) BootConsole.Write("AVX ");
            if (HasFXSR()) BootConsole.Write("FXSR ");
            if (HasPAE()) BootConsole.Write("PAE ");
            if (HasAPIC()) BootConsole.Write("APIC ");
            
            BootConsole.WriteLine("");
        }
    }
}
