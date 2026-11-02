using System;

namespace guideXOS.Misc {
    internal struct ExecutionContextControlState {
        internal ulong Rip;
        internal ulong Cs;
        internal ulong Rflags;
        internal ulong Rsp;
        internal ulong Ss;
        internal bool HasHardwareRspSs;
    }

    internal static class ExecutionContextCapture {
        private const ulong CpuStackPairBytes = 3UL * sizeof(ulong);
        private const ulong MicrosoftX64EntryFrameBytes = 5UL * sizeof(ulong);

        // Captures the architectural control frame without reading fields that
        // the CPU did not push. For same-CPL frames, RSP is the address just
        // above the three-qword RIP/CS/RFLAGS frame.
        internal static bool TryCaptureControlState(
                ulong rip, ulong cs, ulong rflags, ulong frameAddress,
                bool hasHardwareRspSs, ulong hardwareRsp, ulong hardwareSs,
                out ExecutionContextControlState state) {
            state = default;
            if (rip == 0 || cs == 0 || (rflags & 2UL) == 0 ||
                    frameAddress == 0) return false;

            ulong rsp;
            ulong ss = 0;
            if (hasHardwareRspSs) {
                if (hardwareRsp == 0 || hardwareSs == 0) return false;
                rsp = hardwareRsp;
                ss = hardwareSs;
            } else {
                if (frameAddress > ulong.MaxValue - CpuStackPairBytes)
                    return false;
                rsp = frameAddress + CpuStackPairBytes;
            }
            if (rsp == 0) return false;

            state = new ExecutionContextControlState {
                Rip = rip,
                Cs = cs,
                Rflags = rflags,
                Rsp = rsp,
                Ss = ss,
                HasHardwareRspSs = hasHardwareRspSs
            };
            return true;
        }

        // A newly-created Windows x64 ABI function entry has a return address
        // at [RSP] and 32 bytes of caller-provided home space above it. RSP is
        // 8 modulo 16 before the first instruction.
        internal static bool TryCreateMicrosoftX64FunctionEntryRsp(
                ulong stackBase, ulong stackTop, out ulong rsp) {
            rsp = 0;
            if (stackBase == 0 || stackTop <= stackBase ||
                    stackTop - stackBase < MicrosoftX64EntryFrameBytes)
                return false;
            ulong candidate = stackTop - MicrosoftX64EntryFrameBytes;
            if (candidate < stackBase || (candidate & 0xFUL) != 8UL)
                return false;
            rsp = candidate;
            return true;
        }
    }
}
