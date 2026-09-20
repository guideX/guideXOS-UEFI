using System;

namespace guideXOS.Misc {
    internal static unsafe class Ring3Abi {
        private static bool _enteredMarker;
        internal const ulong AbiVersion = 1;
        internal const ulong Ping = 1;
        internal const ulong Exit = 2;
        internal const ulong ValidateRead = 3;
        internal const ulong ValidateWrite = 4;
        internal const ulong Success = 0;
        internal const ulong InvalidOperation = unchecked((ulong)-38L);
        internal const ulong InvalidPointer = unchecked((ulong)-14L);

        private static void Marker(string text) {
            for (int i = 0; i < text.Length; i++) Native.Out8(0x3F8, (byte)text[i]);
            Native.Out8(0x3F8, (byte)'\n');
        }

        internal static void Dispatch(IDT.IDTStackGeneric* stack) {
            if (stack == null) return;
            Ring3Process process;
            if (!Ring3Process.TryGetCurrent(out process)) {
                stack->rs.rax = InvalidOperation;
                Marker("RING3_CALLER_VALID=0");
                return;
            }

            if (!_enteredMarker) {
                _enteredMarker = true;
                Marker("RING3_ENTER_CPL=3");
            }
            Marker("RING3_SYSCALL_ENTER=1");
            Marker("RING3_CALLER_VALID=1");
            process.MarkRunning();
            ulong operation = stack->rs.rax;
            switch (operation) {
                case Ping:
                    stack->rs.rax = AbiVersion;
                    Marker("RING3_PING_RESULT=success");
                    break;

                case ValidateRead:
                    if (PageTable.ValidateReadableUserRange(process.Space.Pml4,
                                                             stack->rs.rdi,
                                                             stack->rs.rsi)) {
                        stack->rs.rax = Success;
                    } else {
                        stack->rs.rax = InvalidPointer;
                        Marker("RING3_INVALID_POINTER_REJECTED=1");
                    }
                    break;

                case ValidateWrite:
                    if (PageTable.ValidateWritableUserRange(process.Space.Pml4,
                                                             stack->rs.rdi,
                                                             stack->rs.rsi)) {
                        stack->rs.rax = Success;
                    } else {
                        stack->rs.rax = InvalidPointer;
                        Marker("RING3_INVALID_POINTER_REJECTED=1");
                    }
                    break;

                case Exit:
                    stack->rs.rax = Success;
                    process.Exit((int)stack->rs.rdi);
                    process.PrepareDirectReturn(stack);
                    break;

                default:
                    stack->rs.rax = InvalidOperation;
                    Marker("RING3_INVALID_OPERATION_REJECTED=1");
                    break;
            }
        }
    }
}
