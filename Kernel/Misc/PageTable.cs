using guideXOS.Misc;

namespace guideXOS {
    public static unsafe class PageTable {
        public enum PageSize { Typical = 4096 }

        public static ulong* PML4;
        public const ulong UserAddressLimit = 0x0000800000000000UL;
        public const ulong PageMask = 0x000F_FFFF_FFFF_F000UL;
        public const ulong MaxUserTransfer = 0x10000UL;

        internal static void Initialize() {
            PML4 = (ulong*)SMP.SharedPageTable;
            Native.Stosb(PML4, 0, 0x1000);
            for (ulong i = (ulong)PageSize.Typical;
                 i < 1024 * 1024 * 1024 * 4UL;
                 i += (ulong)PageSize.Typical) {
                Map(i, i, PageSize.Typical);
            }
            Native.WriteCR3((ulong)PML4);
        }

        public static ulong* CurrentRoot => (ulong*)(Native.ReadCR3() & PageMask);

        public static ulong* GetPage(ulong virtualAddress, PageSize pageSize = PageSize.Typical) =>
            GetPageInternal(PML4, virtualAddress, user: false, pageSize);

        public static ulong* GetPageUser(ulong virtualAddress, PageSize pageSize = PageSize.Typical) =>
            GetPageInternal(PML4, virtualAddress, user: true, pageSize);

        public static ulong* GetPageOnRoot(ulong* rootPml4, ulong virtualAddress, bool user,
                                           PageSize pageSize = PageSize.Typical) =>
            GetPageInternal(rootPml4, virtualAddress, user, pageSize);

        private static ulong* GetPageInternal(ulong* rootPml4, ulong virtualAddress,
                                              bool user, PageSize pageSize) {
            int ignored = 0;
            return GetPageInternal(rootPml4, virtualAddress, user, pageSize,
                                   null, ref ignored);
        }

        private static ulong* GetPageInternal(ulong* rootPml4, ulong virtualAddress,
                                              bool user, PageSize pageSize,
                                              ulong[] allocations, ref int allocationCount) {
            if (rootPml4 == null || (virtualAddress % (ulong)PageSize.Typical) != 0)
                return null;

            ulong pml4Entry = (virtualAddress >> 39) & 0x1FFUL;
            ulong pml3Entry = (virtualAddress >> 30) & 0x1FFUL;
            ulong pml2Entry = (virtualAddress >> 21) & 0x1FFUL;
            ulong pml1Entry = (virtualAddress >> 12) & 0x1FFUL;
            ulong* pml3 = Next(rootPml4, pml4Entry, user, allocations, ref allocationCount);
            if (pml3 == null) return null;
            ulong* pml2 = Next(pml3, pml3Entry, user, allocations, ref allocationCount);
            if (pml2 == null || pageSize != PageSize.Typical) return null;
            ulong* pml1 = Next(pml2, pml2Entry, user, allocations, ref allocationCount);
            return pml1 == null ? null : &pml1[pml1Entry];
        }

        public static void Map(ulong virtualAddress, ulong physicalAddress,
                               PageSize pageSize = PageSize.Typical) =>
            MapOnRoot(PML4, virtualAddress, physicalAddress, user: false,
                      writable: true, executable: true, pageSize: pageSize);

        public static void MapUser(ulong virtualAddress, ulong physicalAddress,
                                   PageSize pageSize = PageSize.Typical) =>
            MapOnRoot(PML4, virtualAddress, physicalAddress, user: true,
                      writable: true, executable: true, pageSize: pageSize);

        public static void MapOnRoot(ulong* rootPml4, ulong virtualAddress, ulong physicalAddress,
                                     bool user, PageSize pageSize = PageSize.Typical) =>
            MapOnRoot(rootPml4, virtualAddress, physicalAddress, user,
                      writable: true, executable: true, pageSize: pageSize);

        public static void MapOnRoot(ulong* rootPml4, ulong virtualAddress, ulong physicalAddress,
                                     bool user, bool writable, bool executable,
                                     PageSize pageSize = PageSize.Typical) {
            if (pageSize != PageSize.Typical) return;
            ulong* pte = GetPageInternal(rootPml4, virtualAddress, user, pageSize);
            if (pte == null) return;
            // Bit 63 is NX when EFER.NXE is enabled.  Keep executable state
            // explicit: Phase 24 relies on RX/R/RW rather than a writable
            // user mapping that is executable by default.
            ulong flags = 0b1UL | (writable ? 0b10UL : 0) | (user ? 0b100UL : 0) |
                          (executable ? 0UL : (1UL << 63));
            *pte = (physicalAddress & PageMask) | flags;
            Native.Invlpg(virtualAddress);
        }

        public static void MapOnRootTracked(ulong* rootPml4, ulong virtualAddress,
                                             ulong physicalAddress, bool user, bool writable,
                                             bool executable, ulong[] allocations,
                                             ref int allocationCount,
                                             PageSize pageSize = PageSize.Typical) {
            if (pageSize != PageSize.Typical) return;
            ulong* pte = GetPageInternal(rootPml4, virtualAddress, user, pageSize,
                                         allocations, ref allocationCount);
            if (pte == null) return;
            ulong flags = 0b1UL | (writable ? 0b10UL : 0) | (user ? 0b100UL : 0) |
                          (executable ? 0UL : (1UL << 63));
            *pte = (physicalAddress & PageMask) | flags;
            Native.Invlpg(virtualAddress);
        }

        public static ulong* Next(ulong* directory, ulong entry) {
            int ignored = 0;
            return Next(directory, entry, user: false, null, ref ignored);
        }

        private static ulong* Next(ulong* directory, ulong entry, bool user,
                                   ulong[] allocations, ref int allocationCount) {
            if (directory == null) return null;
            if ((directory[entry] & 1) != 0) {
                // A process root initially shares supervisor page-table
                // branches with the kernel root.  Never set U/S on a shared
                // branch in place: clone that branch first, then continue
                // down the private user mapping path.
                if (user && allocations != null &&
                    (directory[entry] & 0b100UL) == 0) {
                    if (allocationCount >= allocations.Length) return null;
                    ulong* clone = (ulong*)Allocator.Allocate(0x1000);
                    if (clone == null) return null;
                    Native.Movsb(clone, (void*)(directory[entry] & PageMask), 0x1000);
                    allocations[allocationCount++] = (ulong)clone;
                    directory[entry] = ((ulong)clone & PageMask) |
                                       ((directory[entry] & 0xFFFUL) | 0b100UL);
                    return clone;
                }
                ulong* p = (ulong*)(directory[entry] & PageMask);
                if (user) directory[entry] |= 0b100UL;
                return p;
            }

            ulong* page = (ulong*)Allocator.Allocate(0x1000);
            if (page == null) return null;
            Native.Stosb(page, 0, 0x1000);
            if (allocations != null && allocationCount < allocations.Length)
                allocations[allocationCount++] = (ulong)page;
            directory[entry] = ((ulong)page & PageMask) | 0b11UL |
                               (user ? 0b100UL : 0);
            return page;
        }

        private static bool IsCanonicalUser(ulong address) =>
            address != 0 && address < UserAddressLimit;

        public static bool TryTranslateUser(ulong* rootPml4, ulong virtualAddress,
                                             bool writable, out ulong physicalAddress) {
            physicalAddress = 0;
            if (!IsCanonicalUser(virtualAddress) || rootPml4 == null) return false;
            ulong pml4e = rootPml4[(virtualAddress >> 39) & 0x1FFUL];
            if ((pml4e & 1) == 0 || (pml4e & 4) == 0) return false;
            ulong* pdpt = (ulong*)(pml4e & PageMask);
            ulong pdpte = pdpt[(virtualAddress >> 30) & 0x1FFUL];
            if ((pdpte & 1) == 0 || (pdpte & 4) == 0) return false;
            if ((pdpte & (1UL << 7)) != 0) {
                if (writable && (pdpte & 2) == 0) return false;
                physicalAddress = (pdpte & 0x000FFFFFC0000000UL) |
                                  (virtualAddress & 0x3FFFFFFFUL);
                return true;
            }
            ulong* pd = (ulong*)(pdpte & PageMask);
            ulong pde = pd[(virtualAddress >> 21) & 0x1FFUL];
            if ((pde & 1) == 0 || (pde & 4) == 0) return false;
            if ((pde & (1UL << 7)) != 0) {
                if (writable && (pde & 2) == 0) return false;
                physicalAddress = (pde & 0x000FFFFFFFE00000UL) |
                                  (virtualAddress & 0x1FFFFFUL);
                return true;
            }
            ulong* pt = (ulong*)(pde & PageMask);
            ulong pte = pt[(virtualAddress >> 12) & 0x1FFUL];
            if ((pte & 1) == 0 || (pte & 4) == 0) return false;
            if (writable && (pte & 2) == 0) return false;
            physicalAddress = (pte & PageMask) | (virtualAddress & 0xFFFUL);
            return true;
        }

        internal static bool TryGetUserPageEntry(ulong* rootPml4,
                                                   ulong virtualAddress,
                                                   out ulong* entry) {
            entry = null;
            if (!IsCanonicalUser(virtualAddress) ||
                (virtualAddress & 0xFFFUL) != 0 || rootPml4 == null)
                return false;
            ulong pml4e = rootPml4[(virtualAddress >> 39) & 0x1FFUL];
            if ((pml4e & 0x5UL) != 0x5UL) return false;
            ulong* pdpt = (ulong*)(pml4e & PageMask);
            ulong pdpte = pdpt[(virtualAddress >> 30) & 0x1FFUL];
            if ((pdpte & 0x5UL) != 0x5UL || (pdpte & (1UL << 7)) != 0)
                return false;
            ulong* pd = (ulong*)(pdpte & PageMask);
            ulong pde = pd[(virtualAddress >> 21) & 0x1FFUL];
            if ((pde & 0x5UL) != 0x5UL || (pde & (1UL << 7)) != 0)
                return false;
            ulong* pt = (ulong*)(pde & PageMask);
            ulong* pte = &pt[(virtualAddress >> 12) & 0x1FFUL];
            if ((*pte & 0x5UL) != 0x5UL) return false;
            entry = pte;
            return true;
        }

        internal static bool SetUserPagePermissions(ulong* rootPml4,
                                                      ulong virtualAddress,
                                                      bool writable,
                                                      bool executable) {
            ulong* entry;
            if (!TryGetUserPageEntry(rootPml4, virtualAddress, out entry))
                return false;
            ulong physical = *entry & PageMask;
            *entry = physical | 0x5UL | (writable ? 0x2UL : 0) |
                     (executable ? 0UL : (1UL << 63));
            Native.Invlpg(virtualAddress);
            return true;
        }

        internal static bool UnmapUserPage(ulong* rootPml4, ulong virtualAddress,
                                           out ulong physicalAddress) {
            physicalAddress = 0;
            ulong* entry;
            if (!TryGetUserPageEntry(rootPml4, virtualAddress, out entry))
                return false;
            physicalAddress = *entry & PageMask;
            *entry = 0;
            Native.Invlpg(virtualAddress);
            return true;
        }

        public static bool ValidateUserRange(ulong* rootPml4, ulong address,
                                              ulong length, bool writable) {
            if (length == 0 || length > MaxUserTransfer ||
                address == 0 || address >= UserAddressLimit) return false;
            ulong end = address + length;
            if (end <= address || end > UserAddressLimit) return false;
            ulong cursor = address & ~0xFFFUL;
            ulong last = (end - 1) & ~0xFFFUL;
            while (true) {
                ulong ignored;
                if (!TryTranslateUser(rootPml4, cursor, writable, out ignored)) return false;
                if (cursor == last) break;
                cursor += 0x1000UL;
            }
            return true;
        }

        public static bool ValidateReadableUserRange(ulong* rootPml4, ulong address, ulong length) =>
            ValidateUserRange(rootPml4, address, length, writable: false);

        public static bool ValidateWritableUserRange(ulong* rootPml4, ulong address, ulong length) =>
            ValidateUserRange(rootPml4, address, length, writable: true);
    }
}
