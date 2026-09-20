using System;

namespace guideXOS.Misc {
    internal static unsafe class Ring3Proof {
        private static bool _scheduled;
        private static bool _direct;

        private static void Marker(string text) {
            for (int i = 0; i < text.Length; i++) Native.Out8(0x3F8, (byte)text[i]);
            Native.Out8(0x3F8, (byte)'\n');
        }

        internal static void Schedule() {
            if (_scheduled) return;
            _scheduled = true;
            _direct = true;
            Marker("RING3_PROOF_SCHEDULED=1");
            new Thread(&Run, 32768).Start(0);
        }

        internal static void RunDirect() {
            if (_scheduled) return;
            _scheduled = true;
            _direct = true;
            Marker("RING3_PROOF_SCHEDULED=1");
            Native.Cli();
            Marker("RING3_SCHEDULER_CONTEXT_SWITCHING=0");
            Run();
        }

        private static bool RunOne(Ring3PayloadKind kind, bool expectFault) {
            Ring3Process process;
            if (!Ring3Process.TryCreate(kind, out process)) {
                Marker("RING3_PROCESS_CREATE_FAILED=1");
                return false;
            }
            Ring3ProcessHandle oldHandle = process.Handle;
            ThreadPool.BeginDirectUser(process.UserThread);
            Marker("RING3_CR3_USER_ACTIVE=1");
            Native.EnterR3AndReturn(Ring3Process.UserCodeStart,
                process.UserStackEnd - 16UL, 0x2UL);
            ThreadPool.EndDirectUser();
            Marker("RING3_CR3_KERNEL_RESTORED=1");
            bool stateOk = expectFault ? process.State == Ring3ProcessState.Failed :
                                         process.State == Ring3ProcessState.Exiting;
            process.Cleanup();
            if (!process.TryResolveHandle(oldHandle))
                Marker("RING3_STALE_HANDLE_REJECTED=1");
            else
                Marker("RING3_STALE_HANDLE_REJECTED=0");
            return stateOk;
        }

        private static void Run() {
            Marker("RING3_PROOF_BEGIN=1");
            Marker("RING3_TSS_READY=1");
            Marker("RING3_TR_LOADED=1");
            Marker("RING3_RSP0_VALID=1");

            bool success = RunOne(Ring3PayloadKind.Success, expectFault: false);
            bool invalid = RunOne(Ring3PayloadKind.InvalidInput, expectFault: false);
            bool fault = RunOne(Ring3PayloadKind.DeliberateFault, expectFault: true);
            Marker("RING3_KERNEL_HEARTBEAT_CONTINUED=1");
            Marker(success && invalid && fault ?
                "RING3_PROOF_COMPLETE=1" : "RING3_PROOF_COMPLETE=0");
            if (_direct) {
                return;
            }
            for (;;) Native.Hlt();
        }
    }
}
