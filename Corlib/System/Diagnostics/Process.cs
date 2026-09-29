using Internal.Runtime.CompilerHelpers;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;

namespace System.Diagnostics {
    public static unsafe class Process {
        public static void Start(byte[] exe) {
            fixed (byte* ptr = exe) {
                DOSHeader* doshdr = (DOSHeader*)ptr;
                NtHeaders64* nthdr = (NtHeaders64*)(ptr + doshdr->e_lfanew);

                // Require relocations; loader will rebase if needed
                if (!nthdr->OptionalHeader.BaseRelocationTable.VirtualAddress) return;
                // Must have an entry point
                if (nthdr->OptionalHeader.AddressOfEntryPoint == 0) return;

                byte* newPtr = (byte*)StartupCodeHelpers.malloc(nthdr->OptionalHeader.SizeOfImage);
                memset(newPtr, 0, (int)nthdr->OptionalHeader.SizeOfImage);
                memcpy(newPtr, ptr, nthdr->OptionalHeader.SizeOfHeaders);

                DOSHeader* newdoshdr = (DOSHeader*)newPtr;
                NtHeaders64* newnthdr = (NtHeaders64*)(newPtr + newdoshdr->e_lfanew);

                IntPtr moduleSeg = IntPtr.Zero;
                SectionHeader* sections = ((SectionHeader*)(newPtr + newdoshdr->e_lfanew + sizeof(NtHeaders64)));
                for (int i = 0; i < newnthdr->FileHeader.NumberOfSections; i++) {
                    if (*(ulong*)sections[i].Name == 0x73656C75646F6D2E) moduleSeg = (IntPtr)((ulong)newPtr + sections[i].VirtualAddress);
                    memcpy((byte*)((ulong)newPtr + sections[i].VirtualAddress), ptr + sections[i].PointerToRawData, sections[i].SizeOfRawData);
                }
                long delta = (long)((ulong)newPtr - newnthdr->OptionalHeader.ImageBase);
                if (delta != 0) FixImageRelocations(newdoshdr, newnthdr, delta);

                // Resolve imports if any
                if (newnthdr->OptionalHeader.ImportTable.VirtualAddress != 0) {
                    ResolveImports(newPtr, newnthdr);
                }

                delegate*<void> p = (delegate*<void>)((ulong)newPtr + newnthdr->OptionalHeader.AddressOfEntryPoint);
                // Initialize module tables if provided
                StartupCodeHelpers.InitializeModules(moduleSeg);
                StartThread(p);
                //StartupCodeHelpers.free((IntPtr)ptr);
            }
        }

        [DllImport("*")]
        static unsafe extern void memset(byte* ptr, int c, int count);

        [DllImport("*")]
        static unsafe extern void memcpy(byte* dest, byte* src, ulong count);

#if Kernel
        static void StartThread(delegate*<void> func) {
            guideXOS.API.API_StartThread(func);
        }
#else
        [DllImport("StartThread")]
        static extern void StartThread(delegate*<void> func);
#endif

        static void FixImageRelocations(DOSHeader* dos_header, NtHeaders64* nt_header, long delta) {
            ulong size;
            long* intruction;
            DataDirectory* reloc_block =
                (DataDirectory*)(nt_header->OptionalHeader.BaseRelocationTable.VirtualAddress +
                    (ulong)dos_header);

            while (reloc_block->VirtualAddress) {
                size = (ulong)((reloc_block->Size - sizeof(DataDirectory)) / sizeof(ushort));
                ushort* fixup = (ushort*)((ulong)reloc_block + (ulong)sizeof(DataDirectory));
                for (ulong i = 0; i < size; i++, fixup++) {
                    if (10 == *fixup >> 12) {
                        intruction = (long*)(reloc_block->VirtualAddress + (ulong)dos_header + (*fixup & 0xfffu));
                        *intruction += delta;
                    }
                }
                reloc_block = (DataDirectory*)(reloc_block->Size + (ulong)reloc_block);
            }
        }

        // ---- Import resolution using Win32 shim ----
        static void ResolveImports(byte* moduleBase, NtHeaders64* nt) {
            uint impRva = nt->OptionalHeader.ImportTable.VirtualAddress;
            if (impRva == 0) return;
            ImportDescriptor* desc = (ImportDescriptor*)(moduleBase + impRva);
            for (;; desc++) {
                if (desc->OriginalFirstThunk == 0 && desc->FirstThunk == 0 && desc->Name == 0) break;
                sbyte* dllNamePtr = (sbyte*)(moduleBase + desc->Name);
                string dllName = ReadAsciiZ((byte*)dllNamePtr);
                uint iltRva = desc->OriginalFirstThunk != 0 ? desc->OriginalFirstThunk : desc->FirstThunk;
                ThunkData64* ilt = (ThunkData64*)(moduleBase + iltRva);
                ulong* iat = (ulong*)(moduleBase + desc->FirstThunk);
                for (int i = 0; ; i++) {
                    ulong val = ilt[i].Value;
                    if (val == 0) break;
                    ulong func = 0;
                    if ((val & 0x8000000000000000UL) != 0) {
                        // Ordinal import
                        ushort ord = (ushort)(val & 0xFFFF);
                        // Represent ordinal as #nnn
                        string proc = "#" + ord.ToString();
                        func = guideXOS.Compat.Win32Shim.Resolve(dllName, proc);
                        proc.Dispose();
                    } else {
                        ImportByName* ibn = (ImportByName*)(moduleBase + (uint)val);
                        string name = ReadAsciiZ(&ibn->Name[0]);
                        func = guideXOS.Compat.Win32Shim.Resolve(dllName, name);
                        name.Dispose();
                    }
                    // If unresolved, leave zero in IAT (callers may guard or crash).
                    iat[i] = func;
                }
                dllName.Dispose();
            }
        }

        static string ReadAsciiZ(byte* p) {
            if (p == null) return string.Empty;
            int len = 0; byte* q = p; while (*q != 0) { len++; q++; }
            char[] chars = new char[len];
            for (int i = 0; i < len; i++) chars[i] = (char)p[i];
            return new string(chars);
        }
    }
}
