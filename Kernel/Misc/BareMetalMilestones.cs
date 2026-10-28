#if UEFI_DIAGNOSTIC_BM1
using guideXOS.Kernel.Drivers;
using System.Runtime.InteropServices;

namespace guideXOS.Misc {
    /// <summary>
    /// BM1's fixed-record, direct-GOP diagnostics. Every operation is bounded,
    /// allocation-free, font-free, and uses the GOP byte stride.
    /// </summary>
    internal static unsafe class BareMetalMilestones {
        private const uint RecordMagic = 0x314D4247; // "GBM1"
        private const uint FramebufferLayoutValid = 1u << 0;
        private const uint FramebufferDescriptorAccounted = 1u << 1;
        private const uint FramebufferPageMapValid = 1u << 2;
        private const uint FramebufferPixelFormatValid = 1u << 3;
        private const uint FramebufferWritableValid = 1u << 4;
        private const uint FramebufferUncachedValid = 1u << 5;
        private const uint FramebufferValid = FramebufferLayoutValid |
            FramebufferDescriptorAccounted | FramebufferPageMapValid |
            FramebufferPixelFormatValid | FramebufferWritableValid |
            FramebufferUncachedValid;
        private const ulong PteWritable = 1UL << 1;
        private const ulong PteWriteThrough = 1UL << 3;
        private const ulong PteCacheDisable = 1UL << 4;
        private const ushort DiagnosticSerialPort = 0x3F8;
        private const ulong Cr4La57 = 1UL << 12;
        // Version-1 capture flags: LA57, RDI canonical, effective address
        // canonical, and unsigned RDI+8 wrap, respectively.
        private const uint FaultFlagLa57 = 1u << 0;
        private const uint FaultFlagRdiCanonical = 1u << 1;
        private const uint FaultFlagAddressCanonical = 1u << 2;
        private const uint FaultFlagAddressOverflow = 1u << 3;
        private const uint FaultRecordExtensionVersion = 1;
        private const int DisplayPointerCheckpointWordCount = 12;
        private const ulong DisplayStaticBaseObjectSize = 32;
        private const ulong Cr4La57Mask = 1UL << 12;
        private const uint PointerCheckpointPresent = 1u << 31;
        private const uint PointerCheckpointTaggedBase = 1u << 0;
        private const uint PointerCheckpointHasPreInitData = 1u << 1;
        private const uint PointerCheckpointBaseCanonical = 1u << 2;
        private const uint PointerCheckpointBaseAligned = 1u << 3;
        private const uint PointerCheckpointBaseMapped = 1u << 4;
        private const uint PointerCheckpointUseSiteFieldCanonical = 1u << 5;
        private const uint PointerCheckpointUseSiteFieldMapped = 1u << 6;
        private const uint PointerCheckpointUseSiteReferenceRead = 1u << 7;
        private const uint PointerCheckpointUseSiteReferenceCanonical = 1u << 8;
        private const uint PointerCheckpointRootTargetCanonical = 1u << 9;
        private const uint PointerCheckpointRootTargetMapped = 1u << 10;
        private const uint PointerCheckpointRootTargetTypeMatches = 1u << 11;
        private const uint PointerCheckpointObjectFieldCanonical = 1u << 12;
        private const uint PointerCheckpointObjectFieldMapped = 1u << 13;
        private const uint PointerCheckpointObjectReferenceRead = 1u << 14;
        private const uint PointerCheckpointObjectReferenceCanonical = 1u << 15;

        private static UefiBareMetalRecord* _record;
        private static UefiBootInfo* _bootInfo;

#if UEFI_DIAGNOSTIC_BM1
        [DllImport("*")]
        private static extern void InstallBm1EarlyExceptionIdt(
            UefiBareMetalRecord* record);

        [DllImport("*")]
        private static extern void SetBm1EarlyStage(uint stage,
            uint substage);
#endif

#if UEFI_DIAGNOSTIC_BM1_FAULT_TEST
        [DllImport("*")]
        private static extern void TriggerBm1PageFault();
#endif

        // The entry-stage writer deliberately has no references to the
        // managed static fields above. EntryPoint calls it before NativeAOT
        // InitializeModules, when managed statics are not yet usable.
        public static void RecordEarly(UefiBootInfo* bootInfo, uint stage,
                uint substage, bool failed = false) {
            SetBm1EarlyStage(stage, substage);
            WriteSerialStage(stage, substage);
            if (bootInfo == null || bootInfo->Reserved[2] == 0) return;
            if (!IsIdentityMappedRange(bootInfo->Reserved[2],
                    (ulong)sizeof(UefiBootMemoryHandoff))) return;
            UefiBootMemoryHandoff* handoff =
                (UefiBootMemoryHandoff*)bootInfo->Reserved[2];
            UefiBareMetalRecord* record = &handoff->Diagnostics;
            if (record->Magic != 0 && record->Magic != RecordMagic) return;
            record->Magic = RecordMagic;
            record->Stage = stage;
            record->Substage = substage;
            if (failed && stage == 5)
                record->FramebufferFailureCode = substage;
            record->Sequence++;
        }

        // Installs a temporary assembly-only exception table before allocator
        // and NativeAOT module initialization. It uses no managed statics or
        // heap, and leaves the fixed handoff record available to the normal
        // IDT path once runtime startup has completed.
        public static void InstallEarlyExceptionTrap(
                UefiBootMemoryHandoff* handoff) {
            if (handoff == null) return;
            InstallBm1EarlyExceptionIdt(&handoff->Diagnostics);
        }

        public static void Configure(UefiBootInfo* bootInfo,
                UefiBootMemoryHandoff* handoff) {
            _bootInfo = bootInfo;
            _record = handoff == null ? null : &handoff->Diagnostics;
            if (_record == null) return;
            _record->Magic = RecordMagic;
        }

        // Capture the GC static base and field slot without dereferencing the
        // array reference. P0 runs before InitializeModules, when the base is
        // an encoded EEType token and the field slot is not allocated storage.
        public static void RecordDisplayPointerCheckpoint(
                UefiBootInfo* bootInfo, UefiBootMemoryHandoff* handoff,
                uint stageId) {
            if (bootInfo == null || handoff == null) return;
            UefiBareMetalRecord* record = &handoff->Diagnostics;
            if (record->Magic != RecordMagic) return;
            CaptureDisplayPointerCheckpoint(record, bootInfo, stageId);
        }

        public static void RecordDisplayPointerCheckpoint(uint stageId) {
            UefiBareMetalRecord* record = _record;
            UefiBootInfo* bootInfo = _bootInfo;
            if (record == null || bootInfo == null ||
                record->Magic != RecordMagic) return;
            CaptureDisplayPointerCheckpoint(record, bootInfo, stageId);
        }

        private static void CaptureDisplayPointerCheckpoint(
                UefiBareMetalRecord* record, UefiBootInfo* bootInfo,
                uint stageId) {
            int rowIndex;
            switch (stageId) {
                case 0: rowIndex = 0; break;
                case 2: rowIndex = 1; break;
                case 3: rowIndex = 2; break;
                case 4: rowIndex = 3; break;
                case 5: rowIndex = 4; break;
                default: return;
            }

            DisplayManager.GetAvailableResolutionsStaticAddresses(
                out ulong staticBase, out ulong fieldAddress);
            ulong* allWords = record->DisplayPointerCheckpoints;
            ulong* row = allWords + rowIndex * DisplayPointerCheckpointWordCount;
            ulong cr4 = Native.ReadCR4();
            bool la57 = (cr4 & Cr4La57Mask) != 0;
            uint flags = PointerCheckpointPresent;
            ulong useSiteReference = 0;
            ulong rootTarget = 0;
            ulong observedEEType = 0;
            ulong objectFieldAddress = 0;
            ulong objectReference = 0;

            if (stageId == 0) {
                // The initial GC-static cell encodes EEType plus state flags.
                // Preserve the expected static-base EEType, but never read the
                // candidate field slot or the encoded base token.
                if ((staticBase & 1UL) != 0)
                    flags |= PointerCheckpointTaggedBase;
                if ((staticBase & 2UL) != 0)
                    flags |= PointerCheckpointHasPreInitData;
                if ((staticBase & 3UL) == 3UL)
                    observedEEType = staticBase & ~3UL;
            } else if (!la57 && IsCanonicalAddress(staticBase, la57) &&
                    (staticBase & 7UL) == 0) {
                flags |= PointerCheckpointBaseCanonical |
                    PointerCheckpointBaseAligned;

                // First capture the exact slot the generated field access uses.
                // The value is retained as an integer; it is never dereferenced.
                if (IsIdentityMappedRange(staticBase, sizeof(ulong))) {
                    flags |= PointerCheckpointBaseMapped;
                    rootTarget = *(ulong*)staticBase;

                    if (IsCanonicalAddress(fieldAddress, la57)) {
                        flags |= PointerCheckpointUseSiteFieldCanonical;
                        if (IsIdentityMappedRange(fieldAddress, sizeof(ulong))) {
                            flags |= PointerCheckpointUseSiteFieldMapped;
                            useSiteReference = *(ulong*)fieldAddress;
                            flags |= PointerCheckpointUseSiteReferenceRead;
                            if (IsCanonicalAddress(useSiteReference, la57))
                                flags |= PointerCheckpointUseSiteReferenceCanonical;
                        }
                    }

                    // Some runtimes publish a handle slot where generated
                    // NativeAOT accessors expect the static-base object. Validate
                    // the target before reading its EEType or field storage.
                    if (rootTarget != 0 &&
                            IsCanonicalAddress(rootTarget, la57)) {
                        flags |= PointerCheckpointRootTargetCanonical;
                        if (IsIdentityMappedRange(rootTarget,
                                DisplayStaticBaseObjectSize)) {
                            flags |= PointerCheckpointRootTargetMapped;
                            observedEEType = *(ulong*)rootTarget;
                            ulong* allCheckpointWords = record->DisplayPointerCheckpoints;
                            ulong expectedEEType = allCheckpointWords[9];
                            if (expectedEEType != 0 &&
                                    observedEEType == expectedEEType)
                                flags |= PointerCheckpointRootTargetTypeMatches;

                            if ((flags & PointerCheckpointRootTargetTypeMatches) != 0 &&
                                    rootTarget <= ~0UL - 16UL) {
                                objectFieldAddress = rootTarget + 16UL;
                                if (IsCanonicalAddress(objectFieldAddress, la57)) {
                                    flags |= PointerCheckpointObjectFieldCanonical;
                                    if (IsIdentityMappedRange(objectFieldAddress,
                                            sizeof(ulong))) {
                                        flags |= PointerCheckpointObjectFieldMapped;
                                        objectReference = *(ulong*)objectFieldAddress;
                                        flags |= PointerCheckpointObjectReferenceRead;
                                        if (IsCanonicalAddress(objectReference, la57))
                                            flags |= PointerCheckpointObjectReferenceCanonical;
                                    }
                                }
                            }
                        }
                    }
                }
            }

            row[0] = stageId;
            row[1] = flags;
            row[2] = (uint)bootInfo->BootMode;
            row[3] = record->Stage;
            row[4] = record->Substage;
            row[5] = staticBase;
            row[6] = fieldAddress;
            row[7] = useSiteReference;
            row[8] = observedEEType;
            row[9] = stageId == 0 ? observedEEType :
                record->DisplayPointerCheckpoints[9];
            row[10] = objectFieldAddress;
            row[11] = objectReference;

            WriteSerialText('B', 'M', '3', 'C', ':');
            WriteSerialHex(stageId, 8);
            WriteSerialByte(':');
            WriteSerialHex(flags, 8);
            WriteSerialByte(':');
            WriteSerialHex((uint)bootInfo->BootMode, 8);
            WriteSerialByte(':');
            WriteSerialHex(record->Stage, 8);
            WriteSerialByte(':');
            WriteSerialHex(record->Substage, 8);
            WriteSerialByte(':');
            WriteSerialHex(staticBase, 16);
            WriteSerialByte(':');
            WriteSerialHex(fieldAddress, 16);
            WriteSerialByte(':');
            WriteSerialHex(useSiteReference, 16);
            WriteSerialByte(':');
            WriteSerialHex(rootTarget, 16);
            WriteSerialByte(':');
            WriteSerialHex(observedEEType, 16);
            WriteSerialByte(':');
            WriteSerialHex(objectFieldAddress, 16);
            WriteSerialByte(':');
            WriteSerialHex(objectReference, 16);
            WriteSerialByte('\n');
        }

        public static void Record(uint stage, uint substage = 0) {
            UefiBareMetalRecord* record = _record;
            if (record == null || record->Magic != RecordMagic) return;
            record->Stage = stage;
            record->Substage = substage;
            record->Sequence++;
            WriteSerialStage(stage, substage);
            if (_bootInfo != null &&
                (record->FramebufferValidationFlags & FramebufferValid) ==
                    FramebufferValid) {
                DrawMilestoneStrip();
            }
        }

        public static bool ValidateFramebuffer(UefiBootInfo* bootInfo,
                UefiBootMemoryHandoff* handoff) {
            UefiBareMetalRecord* record = _record;
            if (record == null) return false;
            record->Substage = 0;
            record->FramebufferFailureCode = 0;
            record->FramebufferMapPagesChecked = 0;
            record->FramebufferMapLeafFlags = 0;
            record->FramebufferDescriptorType = 0xFFFF_FFFFu;
            record->FramebufferDescriptorBase = 0;
            record->FramebufferDescriptorSize = 0;
            record->FramebufferDescriptorAttributes = 0;
            record->FramebufferDescriptorCount = 0;
            record->FramebufferDescriptorCoveredBytes = 0;
            if (bootInfo == null || handoff == null ||
                !bootInfo->HasFramebuffer || !bootInfo->HasMemoryMap)
                return FailFramebuffer(record, 1);

            record->FramebufferValidationFlags = 0;
            ulong width = bootInfo->FramebufferWidth;
            ulong height = bootInfo->FramebufferHeight;
            ulong pitch = bootInfo->FramebufferPitch;
            ulong framebufferSize = bootInfo->FramebufferSize;
            ulong framebufferBase = bootInfo->FramebufferBase;
            if (framebufferBase == 0 || width == 0 || height == 0 ||
                width > 8192 || height > 8192 || pitch < width * 4UL ||
                (pitch & 3UL) != 0 || height > 0xFFFF_FFFF_FFFF_FFFFUL / pitch)
                return FailFramebuffer(record, 2);

            ulong requiredBytes = pitch * height;
            if (requiredBytes == 0 || requiredBytes > framebufferSize ||
                framebufferBase > 0xFFFF_FFFF_FFFF_FFFFUL - requiredBytes)
                return FailFramebuffer(record, 2);
            record->FramebufferCheckedBytes = requiredBytes;
            record->FramebufferValidationFlags |= FramebufferLayoutValid;

            uint pixelFormat = handoff->GopPixelFormat;
            bool pixelFormatValid = pixelFormat == 0 || pixelFormat == 1;
            if (pixelFormat == 2) {
                pixelFormatValid = IsContiguousMask(handoff->GopRedMask) &&
                    IsContiguousMask(handoff->GopGreenMask) &&
                    IsContiguousMask(handoff->GopBlueMask) &&
                    handoff->GopRedMask != 0 && handoff->GopGreenMask != 0 &&
                    handoff->GopBlueMask != 0 &&
                    (handoff->GopRedMask & handoff->GopGreenMask) == 0 &&
                    (handoff->GopRedMask & handoff->GopBlueMask) == 0 &&
                    (handoff->GopGreenMask & handoff->GopBlueMask) == 0 &&
                    IsContiguousMask(handoff->GopReservedMask) &&
                    (handoff->GopReservedMask & handoff->GopRedMask) == 0 &&
                    (handoff->GopReservedMask & handoff->GopGreenMask) == 0 &&
                    (handoff->GopReservedMask & handoff->GopBlueMask) == 0;
            }
            if (!pixelFormatValid) return FailFramebuffer(record, 3);
            record->FramebufferValidationFlags |= FramebufferPixelFormatValid;

            ulong descriptorSize = bootInfo->MemoryMapDescriptorSize;
            if (!bootInfo->HasMemoryMap || bootInfo->MemoryMap == 0 ||
                bootInfo->MemoryMapEntryCount == 0 ||
                bootInfo->MemoryMapEntryCount > 65536 ||
                descriptorSize < 40 || descriptorSize > 4096 ||
                bootInfo->MemoryMapEntryCount >
                    0xFFFF_FFFF_FFFF_FFFFUL / descriptorSize)
                return FailFramebuffer(record, 4);
            ulong mapBytes = bootInfo->MemoryMapEntryCount * descriptorSize;
            if (bootInfo->MemoryMap >
                    0xFFFF_FFFF_FFFF_FFFFUL - mapBytes)
                return FailFramebuffer(record, 4);
            if (!FindFramebufferDescriptor(bootInfo, record, framebufferBase,
                    requiredBytes)) {
                // GOP may describe an MMIO framebuffer range that firmware
                // does not include in GetMemoryMap. Distinguish that case from
                // a partially covered range: the latter remains a hard stop.
                if (record->FramebufferDescriptorType != 0xFFFF_FFFFu ||
                    record->FramebufferDescriptorCount != 0 ||
                    record->FramebufferDescriptorCoveredBytes != 0)
                    return FailFramebuffer(record, 4);
                WriteSerialText('B', 'M', '1', 'M', ':');
                WriteSerialText('N', 'O', 'N', 'E');
                WriteSerialByte('\n');
            }
            record->FramebufferValidationFlags |=
                FramebufferDescriptorAccounted;

            ulong firstPage = framebufferBase & ~0xFFFUL;
            ulong lastPage = (framebufferBase + requiredBytes - 1) & ~0xFFFUL;
            ulong pageCount = ((lastPage - firstPage) >> 12) + 1;
            if (pageCount == 0 || pageCount > 65536)
                return FailFramebuffer(record, 5);
            ulong cr3 = Native.ReadCR3() & 0x000F_FFFF_FFFF_F000UL;
            if (cr3 == 0) return FailFramebuffer(record, 6);
            for (ulong page = firstPage; ; page += 0x1000UL) {
                ulong physical;
                ulong flags;
                if (!TryTranslate(cr3, page, out physical, out flags) ||
                    physical != page) return FailFramebuffer(record, 7);
                uint leafFlags = (uint)(flags & (PteWritable |
                    PteWriteThrough | PteCacheDisable));
                if (record->FramebufferMapPagesChecked == 0)
                    record->FramebufferMapLeafFlags = leafFlags;
                if ((leafFlags & (uint)PteWritable) == 0)
                    return FailFramebuffer(record, 8);
                if ((leafFlags & (uint)(PteWriteThrough | PteCacheDisable)) !=
                        (uint)(PteWriteThrough | PteCacheDisable))
                    return FailFramebuffer(record, 9);
                record->FramebufferMapPagesChecked++;
                if (page == lastPage) break;
                if (page > 0xFFFF_FFFF_FFFF_FFFFUL - 0x1000UL)
                    return FailFramebuffer(record, 7);
            }
            record->FramebufferValidationFlags |= FramebufferPageMapValid;
            record->FramebufferValidationFlags |= FramebufferWritableValid |
                FramebufferUncachedValid;
            WriteSerialGopInfo(bootInfo, handoff, record, cr3);
            record->Substage = 1;
            return true;
        }

        private static bool FailFramebuffer(UefiBareMetalRecord* record,
                uint failureCode) {
            record->FramebufferFailureCode = failureCode;
            record->Substage = failureCode;
            WriteSerialText('B', 'M', '1', 'G', ':');
            WriteSerialHex(failureCode, 8);
            WriteSerialByte(':');
            WriteSerialHex(_bootInfo == null ? 0 :
                _bootInfo->FramebufferBase, 16);
            WriteSerialByte(':');
            WriteSerialHex(_bootInfo == null ? 0 :
                _bootInfo->FramebufferSize, 16);
            WriteSerialByte(':');
            WriteSerialHex(_bootInfo == null ? 0 :
                _bootInfo->FramebufferWidth, 8);
            WriteSerialByte(':');
            WriteSerialHex(_bootInfo == null ? 0 :
                _bootInfo->FramebufferHeight, 8);
            WriteSerialByte(':');
            WriteSerialHex(_bootInfo == null ? 0 :
                _bootInfo->FramebufferPitch, 8);
            WriteSerialByte(':');
            WriteSerialHex(record->FramebufferDescriptorType, 8);
            WriteSerialByte(':');
            WriteSerialHex(record->FramebufferDescriptorBase, 16);
            WriteSerialByte(':');
            WriteSerialHex(record->FramebufferDescriptorSize, 16);
            WriteSerialByte(':');
            WriteSerialHex(record->FramebufferDescriptorAttributes, 16);
            WriteSerialByte(':');
            WriteSerialHex(record->FramebufferDescriptorCount, 8);
            WriteSerialByte(':');
            WriteSerialHex(record->FramebufferDescriptorCoveredBytes, 16);
            WriteSerialByte(':');
            WriteSerialHex(record->FramebufferMapPagesChecked, 8);
            WriteSerialByte(':');
            WriteSerialHex(record->FramebufferMapLeafFlags, 8);
            WriteSerialByte(':');
            WriteSerialHex(Native.ReadCR3(), 16);
            WriteSerialByte('\n');
            return false;
        }

        private static void WriteSerialStage(uint stage, uint substage) {
            WriteSerialText('B', 'M', '1', ':');
            WriteSerialByte((char)('0' + ((stage / 10u) % 10u)));
            WriteSerialByte((char)('0' + (stage % 10u)));
            WriteSerialByte('.');
            WriteSerialHex(substage, 8);
            WriteSerialByte('\n');
        }

        private static void WriteSerialGopInfo(UefiBootInfo* bootInfo,
                UefiBootMemoryHandoff* handoff,
                UefiBareMetalRecord* record, ulong cr3) {
            WriteSerialText('B', 'M', '1', 'D', ':');
            WriteSerialHex(bootInfo->FramebufferBase, 16);
            WriteSerialByte(':');
            WriteSerialHex(bootInfo->FramebufferSize, 16);
            WriteSerialByte(':');
            WriteSerialHex(bootInfo->FramebufferWidth, 8);
            WriteSerialByte(':');
            WriteSerialHex(bootInfo->FramebufferHeight, 8);
            WriteSerialByte(':');
            WriteSerialHex(bootInfo->FramebufferPitch, 8);
            WriteSerialByte(':');
            WriteSerialHex(handoff->GopPixelFormat, 8);
            WriteSerialByte(':');
            WriteSerialHex(handoff->GopRedMask, 8);
            WriteSerialByte(':');
            WriteSerialHex(handoff->GopGreenMask, 8);
            WriteSerialByte(':');
            WriteSerialHex(handoff->GopBlueMask, 8);
            WriteSerialByte(':');
            WriteSerialHex(handoff->GopReservedMask, 8);
            WriteSerialByte(':');
            WriteSerialHex(record->FramebufferDescriptorType, 8);
            WriteSerialByte(':');
            WriteSerialHex(record->FramebufferDescriptorBase, 16);
            WriteSerialByte(':');
            WriteSerialHex(record->FramebufferDescriptorSize, 16);
            WriteSerialByte(':');
            WriteSerialHex(record->FramebufferDescriptorAttributes, 16);
            WriteSerialByte(':');
            WriteSerialHex(record->FramebufferDescriptorCount, 8);
            WriteSerialByte(':');
            WriteSerialHex(record->FramebufferDescriptorCoveredBytes, 16);
            WriteSerialByte(':');
            WriteSerialHex(record->FramebufferMapLeafFlags, 8);
            WriteSerialByte(':');
            WriteSerialHex(record->FramebufferMapPagesChecked, 8);
            WriteSerialByte(':');
            WriteSerialHex(cr3, 16);
            WriteSerialByte('\n');
        }

        private static void WriteSerialText(char a, char b, char c, char d,
                char e = '\0') {
            Native.Out8(DiagnosticSerialPort, (byte)a);
            Native.Out8(DiagnosticSerialPort, (byte)b);
            Native.Out8(DiagnosticSerialPort, (byte)c);
            Native.Out8(DiagnosticSerialPort, (byte)d);
            if (e != '\0') Native.Out8(DiagnosticSerialPort, (byte)e);
        }

        private static void WriteSerialByte(char value) =>
            Native.Out8(DiagnosticSerialPort, (byte)value);

        private static void WriteSerialHex(ulong value, int digits) {
            for (int shift = (digits - 1) * 4; shift >= 0; shift -= 4)
                WriteSerialByte(HexChar((int)((value >> shift) & 0xFUL)));
        }

        public static void RecordFault(int vector, ulong errorCode, ulong rip,
                ulong cr2, ulong cr3, ulong rsp, ulong rflags, ulong rdi,
                ulong cr4) {
            UefiBareMetalRecord* record = _record;
            if (record == null || record->Magic != RecordMagic ||
                record->FaultFlags != 0) return;
            bool la57 = (cr4 & Cr4La57) != 0;
            bool addressOverflow = rdi > 0xFFFF_FFFF_FFFF_FFF7UL;
            // x86-64 address generation wraps at 64 bits. Track unsigned
            // overflow explicitly while evaluating that architectural value.
            ulong effectiveAddress = unchecked(rdi + 8UL);
            uint captureFlags = la57 ? FaultFlagLa57 : 0u;
            if (IsCanonicalAddress(rdi, la57))
                captureFlags |= FaultFlagRdiCanonical;
            if (IsCanonicalAddress(effectiveAddress, la57))
                captureFlags |= FaultFlagAddressCanonical;
            if (addressOverflow)
                captureFlags |= FaultFlagAddressOverflow;
            record->FaultVector = (uint)vector;
            record->FaultFlags = 1;
            record->FaultErrorCode = errorCode;
            record->FaultRip = rip;
            record->FaultCr2 = cr2;
            record->FaultCr3 = cr3;
            record->FaultRsp = rsp;
            record->FaultRflags = rflags;
            record->FaultRdi = rdi;
            record->FaultCr4 = cr4;
            record->FaultEffectiveAddress = effectiveAddress;
            record->FaultCaptureFlags = captureFlags;
            record->FaultRecordVersion = FaultRecordExtensionVersion;
            record->Substage = 0xFFFF_FFFFu;
            record->Sequence++;
            WriteSerialText('B', 'M', '1', 'F', ':');
            WriteSerialHex((uint)vector, 8);
            WriteSerialText(':', 'E', ':', 'R', ':');
            WriteSerialHex(errorCode, 16);
            WriteSerialByte(':');
            WriteSerialHex(rip, 16);
            WriteSerialByte(':');
            WriteSerialHex(cr2, 16);
            WriteSerialByte(':');
            WriteSerialHex(cr3, 16);
            WriteSerialByte(':');
            WriteSerialHex(rsp, 16);
            WriteSerialByte(':');
            WriteSerialHex(rflags, 16);
            WriteSerialByte(':');
            WriteSerialByte('D');
            WriteSerialByte('=');
            WriteSerialHex(rdi, 16);
            WriteSerialByte(':');
            WriteSerialByte('A');
            WriteSerialByte('=');
            WriteSerialHex(effectiveAddress, 16);
            WriteSerialByte(':');
            WriteSerialByte('4');
            WriteSerialByte('=');
            WriteSerialHex(cr4, 16);
            WriteSerialByte(':');
            WriteSerialByte('C');
            WriteSerialByte('=');
            WriteSerialHex(captureFlags, 8);
            WriteSerialByte(':');
            WriteSerialByte('X');
            WriteSerialByte('=');
            WriteSerialHex(FaultRecordExtensionVersion, 8);
            WriteSerialByte('\n');
            if (_bootInfo != null &&
                (record->FramebufferValidationFlags & FramebufferValid) ==
                    FramebufferValid) DrawFaultScreen();
        }

        private static bool IsCanonicalAddress(ulong address, bool la57) {
            int width = la57 ? 57 : 48;
            ulong signBit = 1UL << (width - 1);
            ulong upper = address >> width;
            ulong expectedUpper = (address & signBit) == 0
                ? 0UL : 0xFFFF_FFFF_FFFF_FFFFUL >> width;
            return upper == expectedUpper;
        }

        public static void RecordTimerTick() {
            UefiBareMetalRecord* record = _record;
            if (record != null && record->Magic == RecordMagic)
                record->HeartbeatTicks++;
        }

        public static void DesktopFrame() {
            UefiBareMetalRecord* record = _record;
            if (record == null || record->Magic != RecordMagic ||
                (record->FramebufferValidationFlags & FramebufferValid) !=
                    FramebufferValid) return;
            if (record->HeartbeatFrames == 0) Record(12);
            record->HeartbeatFrames++;
            if (record->HeartbeatFrames == 2) Record(13);
            record->Stage = record->HeartbeatFrames >= 2 ? 13u : 12u;
            record->Substage = (record->HeartbeatFrames >> 5) & 1u;
            if (record->HeartbeatFrames > 2 &&
                (record->HeartbeatFrames & 31u) == 0)
                WriteSerialStage(13, record->Substage);
            DrawMilestoneStrip();
            DrawHeartbeat();
        }

#if UEFI_DIAGNOSTIC_BM1_FAULT_TEST
        public static void RunSyntheticFaultDisplayTest() {
            TriggerBm1PageFault();
            for (;;) Native.Hlt();
        }
#endif

        private static bool FindFramebufferDescriptor(UefiBootInfo* bootInfo,
                UefiBareMetalRecord* record, ulong baseAddress, ulong byteCount) {
            if (bootInfo->MemoryMap == 0 ||
                bootInfo->MemoryMapEntryCount == 0 ||
                bootInfo->MemoryMapEntryCount > 65536 ||
                bootInfo->MemoryMapDescriptorSize < 40 ||
                bootInfo->MemoryMapDescriptorSize > 4096) return false;
            if (bootInfo->MemoryMapEntryCount >
                0xFFFF_FFFF_FFFF_FFFFUL /
                    bootInfo->MemoryMapDescriptorSize) return false;
            ulong mapBytes = bootInfo->MemoryMapEntryCount *
                bootInfo->MemoryMapDescriptorSize;
            if (bootInfo->MemoryMap > 0xFFFF_FFFF_FFFF_FFFFUL - mapBytes ||
                byteCount == 0 || baseAddress >
                    0xFFFF_FFFF_FFFF_FFFFUL - byteCount)
                return false;
            ulong endAddress = baseAddress + byteCount;
            byte* map = (byte*)bootInfo->MemoryMap;
            ulong cursor = baseAddress;
            const uint maxCoveringDescriptors = 128;
            while (cursor < endAddress) {
                ulong furthestEnd = cursor;
                uint selectedType = 0;
                ulong selectedBase = 0;
                ulong selectedSize = 0;
                ulong selectedAttributes = 0;
                bool selected = false;
                for (ulong i = 0; i < bootInfo->MemoryMapEntryCount; i++) {
                    byte* descriptor = map + i *
                        bootInfo->MemoryMapDescriptorSize;
                    uint type = *(uint*)descriptor;
                    ulong start = *(ulong*)(descriptor + 8);
                    ulong pages = *(ulong*)(descriptor + 24);
                    ulong attributes = *(ulong*)(descriptor + 32);
                    if (pages == 0 ||
                        pages > 0xFFFF_FFFF_FFFF_FFFFUL / 4096UL)
                        continue;
                    ulong length = pages * 4096UL;
                    if (start > 0xFFFF_FFFF_FFFF_FFFFUL - length) continue;
                    ulong descriptorEnd = start + length;
                    if (descriptorEnd <= baseAddress || start >= endAddress)
                        continue;

                    // Preserve an intersecting range even when there is a gap
                    // at the requested base, so a BM1-FB failure retains useful
                    // memory-map evidence in the fixed record.
                    if (record->FramebufferDescriptorCount == 0 &&
                        record->FramebufferDescriptorType == 0xFFFF_FFFFu) {
                        record->FramebufferDescriptorType = type;
                        record->FramebufferDescriptorBase = start;
                        record->FramebufferDescriptorSize = length;
                        record->FramebufferDescriptorAttributes = attributes;
                    }
                    if (start <= cursor && descriptorEnd > furthestEnd) {
                        furthestEnd = descriptorEnd;
                        selectedType = type;
                        selectedBase = start;
                        selectedSize = length;
                        selectedAttributes = attributes;
                        selected = true;
                    }
                }
                if (!selected) return false;
                if (record->FramebufferDescriptorCount == 0) {
                    record->FramebufferDescriptorType = selectedType;
                    record->FramebufferDescriptorBase = selectedBase;
                    record->FramebufferDescriptorSize = selectedSize;
                    record->FramebufferDescriptorAttributes =
                        selectedAttributes;
                }
                record->FramebufferDescriptorCount++;
                cursor = furthestEnd < endAddress ? furthestEnd : endAddress;
                record->FramebufferDescriptorCoveredBytes =
                    cursor - baseAddress;
                if (record->FramebufferDescriptorCount >=
                        maxCoveringDescriptors && cursor < endAddress)
                    return false;
            }
            return record->FramebufferDescriptorCoveredBytes == byteCount;
        }

        private static bool IsIdentityMappedRange(ulong baseAddress,
                ulong byteCount) {
            if (baseAddress == 0 || byteCount == 0 ||
                baseAddress > 0xFFFF_FFFF_FFFF_FFFFUL - byteCount) return false;
            ulong firstPage = baseAddress & ~0xFFFUL;
            ulong lastPage = (baseAddress + byteCount - 1) & ~0xFFFUL;
            ulong pageCount = ((lastPage - firstPage) >> 12) + 1;
            if (pageCount == 0 || pageCount > 16) return false;
            ulong cr3 = Native.ReadCR3() & 0x000F_FFFF_FFFF_F000UL;
            if (cr3 == 0) return false;
            for (ulong page = firstPage; ; page += 0x1000UL) {
                ulong physical;
                ulong flags;
                if (!TryTranslate(cr3, page, out physical, out flags) ||
                    physical != page || (flags & PteWritable) == 0) return false;
                if (page == lastPage) return true;
            }
        }

        private static bool TryTranslate(ulong pml4Address, ulong virtualAddress,
                out ulong physicalAddress, out ulong effectiveFlags) {
            physicalAddress = 0;
            effectiveFlags = 0;
            ulong pml4e = ((ulong*)pml4Address)[(virtualAddress >> 39) & 0x1FFUL];
            if ((pml4e & 1UL) == 0) return false;
            bool writable = (pml4e & PteWritable) != 0;
            ulong* pdpt = (ulong*)(pml4e & 0x000F_FFFF_FFFF_F000UL);
            ulong pdpte = pdpt[(virtualAddress >> 30) & 0x1FFUL];
            if ((pdpte & 1UL) == 0) return false;
            writable &= (pdpte & PteWritable) != 0;
            if ((pdpte & (1UL << 7)) != 0) {
                physicalAddress = (pdpte & 0x000F_FFFF_C000_0000UL) |
                    (virtualAddress & 0x3FFF_FFFFUL);
                effectiveFlags = (writable ? PteWritable : 0) |
                    (pdpte & (PteWriteThrough | PteCacheDisable));
                return true;
            }
            ulong* pd = (ulong*)(pdpte & 0x000F_FFFF_FFFF_F000UL);
            ulong pde = pd[(virtualAddress >> 21) & 0x1FFUL];
            if ((pde & 1UL) == 0) return false;
            writable &= (pde & PteWritable) != 0;
            if ((pde & (1UL << 7)) != 0) {
                physicalAddress = (pde & 0x000F_FFFF_FFE0_0000UL) |
                    (virtualAddress & 0x1F_FFFFUL);
                effectiveFlags = (writable ? PteWritable : 0) |
                    (pde & (PteWriteThrough | PteCacheDisable));
                return true;
            }
            ulong* pt = (ulong*)(pde & 0x000F_FFFF_FFFF_F000UL);
            ulong pte = pt[(virtualAddress >> 12) & 0x1FFUL];
            if ((pte & 1UL) == 0) return false;
            writable &= (pte & PteWritable) != 0;
            physicalAddress = (pte & 0x000F_FFFF_FFFF_F000UL) |
                (virtualAddress & 0xFFFUL);
            effectiveFlags = (writable ? PteWritable : 0) |
                (pte & (PteWriteThrough | PteCacheDisable));
            return true;
        }

        private static bool IsContiguousMask(uint mask) {
            if (mask == 0) return true;
            while ((mask & 1u) == 0) mask >>= 1;
            return (mask & (mask + 1u)) == 0;
        }

        private static uint StageColor(uint stage) {
            switch (stage % 6u) {
                case 1: return 0xFFFF3030u;
                case 2: return 0xFF28C878u;
                case 3: return 0xFF3088F0u;
                case 4: return 0xFFFFC52Eu;
                case 5: return 0xFFE650C8u;
                default: return 0xFF24D7D7u;
            }
        }

        private static void DrawMilestoneStrip() {
            int count = (int)_record->Stage;
            if (count > 13) count = 13;
            for (int i = 1; i <= count; i++) {
                int x = 8 + (i - 1) * 48;
                FillRect(x, 8, 44, 48, StageColor((uint)i));
                DrawChar(x + 4, 16, (char)('0' + (i / 10)), 3, 0xFFFFFFFFu);
                DrawChar(x + 22, 16, (char)('0' + (i % 10)), 3, 0xFFFFFFFFu);
                if (i == count)
                    DrawChar(x + 18, 42,
                        (char)('0' + (_record->Substage % 10u)), 1,
                        0xFFFFFFFFu);
            }
            if (_record->FaultFlags != 0) DrawFaultScreen();
        }

        private static void DrawHeartbeat() {
            int x = (int)_bootInfo->FramebufferWidth - 40;
            int y = (int)_bootInfo->FramebufferHeight - 40;
            uint color = (_record->Substage & 1u) == 0
                ? 0xFF20FF40u : 0xFFFFD020u;
            FillRect(x, y, 32, 32, color);
            FillRect(x + 8, y + 8, 16, 16, 0xFF101010u);
        }

        private static void DrawFaultScreen() {
            uint background = PackPixel(0xFF640000u);
            FillRect(0, 0, (int)_bootInfo->FramebufferWidth,
                (int)_bootInfo->FramebufferHeight, background);
            DrawMilestoneStripWithoutFault();
            DrawFaultValue('V', _record->FaultVector, 72);
            DrawFaultValue('E', _record->FaultErrorCode, 92);
            DrawFaultValue('R', _record->FaultRip, 112);
            DrawFaultValue('D', _record->FaultRdi, 136);
            DrawFaultValue('A', _record->FaultEffectiveAddress, 156);
            DrawFaultValue('4', _record->FaultCr4, 176);
            DrawFaultBit('L', (_record->FaultCaptureFlags & FaultFlagLa57) != 0, 196);
            DrawFaultBit('I', (_record->FaultCaptureFlags & FaultFlagRdiCanonical) != 0, 216);
            DrawFaultBit('N', (_record->FaultCaptureFlags & FaultFlagAddressCanonical) != 0, 236);
            DrawFaultBit('O', (_record->FaultCaptureFlags & FaultFlagAddressOverflow) != 0, 256);
            DrawFaultValue('C', _record->FaultCr2, 276);
            DrawFaultValue('P', _record->FaultCr3, 296);
            DrawFaultValue('S', _record->FaultRsp, 316);
            DrawFaultValue('F', _record->FaultRflags, 336);
            ulong* checkpoints = _record->DisplayPointerCheckpoints;
            DrawFaultCheckpoint('0', checkpoints[5], 'B',
                checkpoints[6], 'S', 360);
            DrawFaultCheckpoint('2', checkpoints[1 * DisplayPointerCheckpointWordCount + 6], 'S',
                checkpoints[1 * DisplayPointerCheckpointWordCount + 7], 'V', 382);
            DrawFaultCheckpoint('3', checkpoints[2 * DisplayPointerCheckpointWordCount + 6], 'S',
                checkpoints[2 * DisplayPointerCheckpointWordCount + 7], 'V', 404);
            DrawFaultCheckpoint('4', checkpoints[3 * DisplayPointerCheckpointWordCount + 6], 'S',
                checkpoints[3 * DisplayPointerCheckpointWordCount + 7], 'V', 426);
            DrawFaultCheckpoint('5', checkpoints[4 * DisplayPointerCheckpointWordCount + 6], 'S',
                checkpoints[4 * DisplayPointerCheckpointWordCount + 7], 'V', 448);
        }

        private static void DrawMilestoneStripWithoutFault() {
            int count = (int)_record->Stage;
            if (count > 13) count = 13;
            for (int i = 1; i <= count; i++) {
                int x = 8 + (i - 1) * 48;
                FillRect(x, 8, 44, 48, StageColor((uint)i));
                DrawChar(x + 4, 16, (char)('0' + (i / 10)), 3, 0xFFFFFFFFu);
                DrawChar(x + 22, 16, (char)('0' + (i % 10)), 3, 0xFFFFFFFFu);
                if (i == count)
                    DrawChar(x + 18, 42,
                        (char)('0' + (_record->Substage % 10u)), 1,
                        0xFFFFFFFFu);
            }
        }

        private static void DrawFaultValue(char label, ulong value, int y) {
            DrawChar(8, y, label, 2, 0xFFFFFFFFu);
            for (int i = 0; i < 16; i++) {
                int shift = 60 - i * 4;
                int digit = (int)((value >> shift) & 0xFUL);
                DrawChar(28 + i * 14, y, HexChar(digit), 2, 0xFFFFFFFFu);
            }
        }

        private static void DrawFaultBit(char label, bool value, int y) {
            DrawChar(8, y, label, 2, 0xFFFFFFFFu);
            DrawChar(28, y, value ? '1' : '0', 2, 0xFFFFFFFFu);
        }

        private static void DrawFaultCheckpoint(char stage, ulong first,
                char firstLabel, ulong second, char secondLabel, int y) {
            DrawChar(8, y, stage, 2, 0xFFFFFFFFu);
            DrawChar(26, y, firstLabel, 2, 0xFFFFFFFFu);
            DrawChar(252, y, secondLabel, 2, 0xFFFFFFFFu);
            for (int i = 0; i < 16; i++) {
                int shift = 60 - i * 4;
                DrawChar(42 + i * 13, y,
                    HexChar((int)((first >> shift) & 0xFUL)), 2,
                    0xFFFFFFFFu);
                DrawChar(268 + i * 13, y,
                    HexChar((int)((second >> shift) & 0xFUL)), 2,
                    0xFFFFFFFFu);
            }
        }

        private static char HexChar(int digit) => digit < 10
            ? (char)('0' + digit) : (char)('A' + digit - 10);

        private static void DrawChar(int x, int y, char value, int scale,
                uint color) {
            ulong glyph = Glyph(value);
            if (glyph == 0) return;
            for (int row = 0; row < 7; row++) {
                uint bits = (uint)((glyph >> ((6 - row) * 5)) & 0x1FUL);
                for (int col = 0; col < 5; col++) {
                    if ((bits & (1u << (4 - col))) != 0)
                        FillRect(x + col * scale, y + row * scale,
                            scale, scale, color);
                }
            }
        }

        // Five-by-seven numeric/label glyphs are ROM-like constants, not
        // managed fonts or allocated strings.
        private static ulong Glyph(char c) {
            switch (c) {
                case '0': return 0b01110100011001110101110011000101110UL;
                case '1': return 0b00100011000010000100001000010001110UL;
                case '2': return 0b01110100010000100010001000100011111UL;
                case '3': return 0b11110000010000101110000010000111110UL;
                case '4': return 0b00010001100101010010111110001000010UL;
                case '5': return 0b11111100001000011110000010000111110UL;
                case '6': return 0b01110100001000011110100011000101110UL;
                case '7': return 0b11111000010001000100010000100001000UL;
                case '8': return 0b01110100011000101110100011000101110UL;
                case '9': return 0b01110100011000101111000010000101110UL;
                case 'A': return 0b01110100011000111111100011000110001UL;
                case 'B': return 0b11110100011000111110100011000111110UL;
                case 'C': return 0b01111100001000010000100001000001111UL;
                case 'D': return 0b11110100011000110001100011000111110UL;
                case 'E': return 0b11111100001000011110100001000011111UL;
                case 'F': return 0b11111100001000011110100001000010000UL;
                case 'I': return 0b11111001000010000100001000010011111UL;
                case 'L': return 0b10000100001000010000100001000011111UL;
                case 'N': return 0b10001110011010110011100011000110001UL;
                case 'O': return 0b01110100011000110001100011000101110UL;
                case 'P': return 0b11110100011000111110100001000010000UL;
                case 'R': return 0b11110100011000111110101001001010001UL;
                case 'S': return 0b01111100001000001110000010000111110UL;
                case 'V': return 0b10001100011000110001100010101000100UL;
                default: return 0;
            }
        }

        private static void FillRect(int x, int y, int width, int height,
                uint argb) {
            if (_bootInfo == null || width <= 0 || height <= 0 ||
                x < 0 || y < 0 || x >= (int)_bootInfo->FramebufferWidth ||
                y >= (int)_bootInfo->FramebufferHeight) return;
            if (width > (int)_bootInfo->FramebufferWidth - x)
                width = (int)_bootInfo->FramebufferWidth - x;
            if (height > (int)_bootInfo->FramebufferHeight - y)
                height = (int)_bootInfo->FramebufferHeight - y;
            uint pixel = PackPixel(argb);
            byte* baseAddress = (byte*)_bootInfo->FramebufferBase;
            for (int row = 0; row < height; row++) {
                uint* line = (uint*)(baseAddress +
                    (ulong)(y + row) * _bootInfo->FramebufferPitch +
                    (ulong)x * 4UL);
                for (int col = 0; col < width; col++) line[col] = pixel;
            }
        }

        private static uint PackPixel(uint argb) {
            uint format = _record->Magic == RecordMagic
                ? ((UefiBootMemoryHandoff*)_bootInfo->Reserved[2])->GopPixelFormat
                : 1u;
            uint red = (argb >> 16) & 0xFFu;
            uint green = (argb >> 8) & 0xFFu;
            uint blue = argb & 0xFFu;
            uint alpha = argb >> 24;
            if (format == 0)
                return (alpha << 24) | (blue << 16) | (green << 8) | red;
            if (format == 2) {
                UefiBootMemoryHandoff* handoff =
                    (UefiBootMemoryHandoff*)_bootInfo->Reserved[2];
                return ScaleToMask(red, handoff->GopRedMask) |
                    ScaleToMask(green, handoff->GopGreenMask) |
                    ScaleToMask(blue, handoff->GopBlueMask) |
                    ScaleToMask(alpha, handoff->GopReservedMask);
            }
            return argb;
        }

        private static uint ScaleToMask(uint component, uint mask) {
            if (mask == 0) return 0;
            int shift = 0;
            while (((mask >> shift) & 1u) == 0) shift++;
            uint normalized = mask >> shift;
            uint max = normalized;
            ulong scaled = ((ulong)component * max + 127UL) / 255UL;
            return (uint)(scaled << shift) & mask;
        }
    }
}
#endif
