using System;

namespace guideXOS.Kernel.Drivers {
    /// <summary>
    /// Bounded helpers for validating PCI memory BARs before a driver maps or
    /// dereferences their register window.
    /// </summary>
    internal static unsafe class PciMmioResource {
        internal const ulong PageBytes = 0x1000UL;
        internal const ulong MaximumCanonicalAddress = 0x00007FFFFFFFFFFFUL;
        internal const ulong EfiMemoryMappedIo = 11UL;
        internal const ulong Required825xxBytes = 0x5406UL;

        internal static bool TryDecodeBar(uint low, uint high,
                                          out ulong baseAddress,
                                          out bool is64Bit) {
            baseAddress = 0;
            is64Bit = false;

            // I/O BARs are not register memory.  Memory type 1 (below 1 MB)
            // and type 3 are reserved for this driver contract.
            if (low == 0 || low == 0xFFFFFFFFU || (low & 1U) != 0)
                return false;

            uint memoryType = (low >> 1) & 3U;
            if (memoryType == 1U || memoryType == 3U) return false;
            is64Bit = memoryType == 2U;

            ulong decoded = (ulong)(low & 0xFFFFFFF0U);
            if (is64Bit) decoded |= (ulong)high << 32;
            if (decoded == 0 || decoded > MaximumCanonicalAddress)
                return false;

            baseAddress = decoded;
            return true;
        }

        internal static bool TryDecodeSize(uint maskLow, uint maskHigh,
                                           bool is64Bit, out ulong size) {
            size = 0;
            ulong decoded;
            if (is64Bit) {
                ulong mask = ((ulong)maskHigh << 32) |
                             (ulong)(maskLow & 0xFFFFFFF0U);
                decoded = unchecked(~mask + 1UL);
            } else {
                uint mask = maskLow & 0xFFFFFFF0U;
                decoded = unchecked((uint)(~mask + 1U));
            }

            if (decoded < 16UL || (decoded & (decoded - 1UL)) != 0)
                return false;
            size = decoded;
            return true;
        }

        internal static bool IsValidBarRange(ulong baseAddress,
                                             ulong barSize,
                                             ulong requiredBytes) {
            if (baseAddress == 0 || barSize < 16UL || requiredBytes == 0 ||
                requiredBytes > barSize ||
                (barSize & (barSize - 1UL)) != 0 ||
                (baseAddress & (barSize - 1UL)) != 0 ||
                (baseAddress & (PageBytes - 1UL)) != 0 ||
                baseAddress > MaximumCanonicalAddress ||
                barSize - 1UL > MaximumCanonicalAddress - baseAddress)
                return false;

            return true;
        }

        internal static bool TryGetPageRange(ulong address, ulong length,
                                             out ulong pageBase,
                                             out ulong pageLength) {
            pageBase = 0;
            pageLength = 0;
            if (address == 0 || length == 0 ||
                address > MaximumCanonicalAddress ||
                length - 1UL > MaximumCanonicalAddress - address)
                return false;

            pageBase = address & ~(PageBytes - 1UL);
            ulong lastPage = (address + length - 1UL) &
                             ~(PageBytes - 1UL);
            pageLength = lastPage - pageBase + PageBytes;
            return pageLength != 0;
        }

        internal static bool IsRangeContained(ulong outerBase,
                                              ulong outerLength,
                                              ulong innerBase,
                                              ulong innerLength) {
            if (outerLength == 0 || innerLength == 0 ||
                outerBase > 0xFFFFFFFFFFFFFFFFUL - (outerLength - 1UL) ||
                innerBase > 0xFFFFFFFFFFFFFFFFUL - (innerLength - 1UL) ||
                innerBase < outerBase)
                return false;

            ulong innerLast = innerBase + innerLength - 1UL;
            ulong outerLast = outerBase + outerLength - 1UL;
            return innerLast <= outerLast;
        }

        internal static bool IsCoveredByEfiMmioMap(ulong mapAddress,
                                                   ulong entryCount,
                                                   ulong descriptorSize,
                                                   ulong rangeBase,
                                                   ulong rangeLength) {
            const ulong minimumDescriptorSize = 40UL;
            const ulong maximumDescriptorSize = 4096UL;
            const ulong maximumEntryCount = 65536UL;
            if (mapAddress == 0 || entryCount == 0 ||
                entryCount > maximumEntryCount ||
                descriptorSize < minimumDescriptorSize ||
                descriptorSize > maximumDescriptorSize ||
                entryCount > 0xFFFFFFFFFFFFFFFFUL / descriptorSize)
                return false;

            ulong mapBytes = entryCount * descriptorSize;
            if (mapAddress > MaximumCanonicalAddress || mapBytes == 0 ||
                mapBytes - 1UL > MaximumCanonicalAddress - mapAddress ||
                rangeLength == 0 ||
                rangeBase > 0xFFFFFFFFFFFFFFFFUL - (rangeLength - 1UL))
                return false;

            byte* map = (byte*)mapAddress;
            for (ulong i = 0; i < entryCount; i++) {
                byte* descriptor = map + i * descriptorSize;
                uint type = *(uint*)descriptor;
                if (type != EfiMemoryMappedIo) continue;

                ulong physicalStart = *(ulong*)(descriptor + 8);
                ulong pageCount = *(ulong*)(descriptor + 24);
                if (pageCount == 0 ||
                    pageCount > 0xFFFFFFFFFFFFFFFFUL / PageBytes)
                    continue;

                ulong descriptorBytes = pageCount * PageBytes;
                if (IsRangeContained(physicalStart, descriptorBytes,
                                     rangeBase, rangeLength))
                    return true;
            }
            return false;
        }
    }
}
