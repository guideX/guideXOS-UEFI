using System;
using guideXOS.Misc;

internal static class Program {
    private static int _passed;
    private static int _failed;

    private static void Check(string name, bool value) {
        if (value) {
            _passed++;
            Console.WriteLine("PASS " + name);
        } else {
            _failed++;
            Console.WriteLine("FAIL " + name);
        }
    }

    private static bool Capture(ulong rip, ulong cs, ulong flags,
            ulong frame, bool pushed, ulong rsp, ulong ss,
            out ExecutionContextControlState state) =>
        ExecutionContextCapture.TryCaptureControlState(
            rip, cs, flags, frame, pushed, rsp, ss, out state);

    private static int Main() {
        Check("same-CPL interrupt derives RSP above RIP/CS/RFLAGS",
            Capture(0x1000, 0x08, 0x202, 0x8000, false, 0, 0,
                out ExecutionContextControlState sameCpl) &&
            sameCpl.Rsp == 0x8018 && sameCpl.Rip == 0x1000 &&
            sameCpl.Cs == 0x08 && sameCpl.Rflags == 0x202 &&
            !sameCpl.HasHardwareRspSs);

        Check("privilege transition uses hardware-pushed RSP and SS",
            Capture(0x2000, 0x1B, 0x202, 0x9000, true, 0x7000, 0x23,
                out ExecutionContextControlState privilege) &&
            privilege.Rsp == 0x7000 && privilege.Ss == 0x23 &&
            privilege.HasHardwareRspSs);

        Check("same-CPL IST frame uses hardware-pushed RSP and SS",
            Capture(0x3000, 0x08, 0x202, 0xA000, true, 0xB000, 0x10,
                out ExecutionContextControlState ist) &&
            ist.Rsp == 0xB000 && ist.Ss == 0x10);

        Check("zero instruction pointer is rejected transactionally",
            !Capture(0, 0x08, 0x202, 0x8000, false, 0, 0,
                out ExecutionContextControlState zeroRip) &&
            zeroRip.Rip == 0 && zeroRip.Rsp == 0);

        Check("zero code selector is rejected",
            !Capture(0x1000, 0, 0x202, 0x8000, false, 0, 0,
                out _));

        Check("RFLAGS reserved bit must be set",
            !Capture(0x1000, 0x08, 0x200, 0x8000, false, 0, 0,
                out _));

        Check("zero frame address is rejected",
            !Capture(0x1000, 0x08, 0x202, 0, false, 0, 0,
                out _));

        Check("same-CPL frame address overflow is rejected",
            !Capture(0x1000, 0x08, 0x202, ulong.MaxValue - 8, false, 0, 0,
                out _));

        Check("zero RSP in a hardware-pushed frame is rejected",
            !Capture(0x1000, 0x1B, 0x202, 0x8000, true, 0, 0x23,
                out _));

        Check("missing SS in a hardware-pushed frame is rejected",
            !Capture(0x1000, 0x1B, 0x202, 0x8000, true, 0x7000, 0,
                out _));

        Check("new-thread ABI entry reserves return address and home space",
            ExecutionContextCapture.TryCreateMicrosoftX64FunctionEntryRsp(
                0x1000, 0x2000, out ulong initialRsp) &&
            initialRsp == 0x1FD8 && (initialRsp & 0xFUL) == 8UL &&
            initialRsp + 40 == 0x2000);

        Check("misaligned new-thread stack top is rejected",
            !ExecutionContextCapture.TryCreateMicrosoftX64FunctionEntryRsp(
                0x1000, 0x1FF8, out ulong misalignedRsp) &&
            misalignedRsp == 0);

        Check("stack smaller than ABI entry frame is rejected",
            !ExecutionContextCapture.TryCreateMicrosoftX64FunctionEntryRsp(
                0x1000, 0x1027, out ulong shortRsp) && shortRsp == 0);

        Check("empty or reversed stack interval is rejected",
            !ExecutionContextCapture.TryCreateMicrosoftX64FunctionEntryRsp(
                0x2000, 0x2000, out _));

        Console.WriteLine("RESULT passed=" + _passed + " failed=" + _failed);
        return _failed == 0 ? 0 : 1;
    }
}
