using guideXOS.Kernel.Drivers;
using Internal.Runtime.CompilerServices;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
namespace guideXOS.Misc {
    public unsafe class Thread {
        public bool Terminated;
        public IDT.IDTStackGeneric* Stack;
        public int RunOnWhichCPU;
        public bool IsIdleThread = false;
        public Ring3Process OwnerProcess;
        public ulong KernelStackBase;
        public ulong KernelStackSize;
        public ulong KernelStackTop;
        public bool IsUserThread;

        public Thread(delegate*<void> method, ulong stack_size = 16384) {
            NewThread(method, stack_size);
        }

        private void NewThread(delegate*<void> method, ulong stack_size) {
            Stack = (IDT.IDTStackGeneric*)Allocator.Allocate((ulong)sizeof(IDT.IDTStackGeneric));
            KernelStackSize = stack_size;
            KernelStackBase = (ulong)Allocator.Allocate(stack_size);
            KernelStackTop = KernelStackBase + stack_size;

            Stack->irs.cs = 0x08;
            Stack->irs.ss = 0x10;
            Stack->irs.rsp = KernelStackTop;

            Stack->irs.rsp -= 8;
            *(ulong*)(Stack->irs.rsp) = (ulong)(delegate*<void>)&ThreadPool.Terminate;

            Stack->irs.rflags = 0x202;

            Stack->irs.rip = (ulong)method;

            Terminated = false;
        }

        public static Thread CreateUser(Ring3Process process, ulong rip, ulong rsp,
                                        ulong stack_size) {
            if (process == null) return null;
            Thread thread = new Thread(&ThreadPool.Terminate, stack_size);
            thread.OwnerProcess = process;
            thread.IsUserThread = true;
            thread.Stack->irs.cs = GDT.UserCodeSelector;
            thread.Stack->irs.ss = GDT.UserDataSelector;
            thread.Stack->irs.rip = rip;
            thread.Stack->irs.rsp = rsp;
            thread.Stack->irs.rflags = 0x202;
            return thread;
        }

        public Thread(Action action, ulong stack_size = 16384) {
            NewThread((delegate*<void>)action.m_functionPointer, stack_size);
        }

        public Thread Start() {
            lock (this) {
                //Bootstrap CPU
                this.RunOnWhichCPU = 0;
                ThreadPool.Threads.Add(this);
                return this;
            }
        }

        public Thread Start(int run_on_which_cpu) {
            lock (this) {
                // UEFI bring-up: ACPI MADT parsing may be unavailable or deferred.
                // Guard against null/empty CPU list and fall back to BSP.
                if (ACPI.LocalAPIC_CPUIDs == null || ACPI.LocalAPIC_CPUIDs.Count == 0) {
                    run_on_which_cpu = 0;
                } else {
                    bool hasThatCPU = false;
                    for (int i = 0; i < ACPI.LocalAPIC_CPUIDs.Count; i++) {
                        if (ACPI.LocalAPIC_CPUIDs[i] == run_on_which_cpu) {
                            hasThatCPU = true;
                        }
                    }
                    if (!hasThatCPU) {
                        run_on_which_cpu = 0;
                    }
                }

                this.RunOnWhichCPU = run_on_which_cpu;
                ThreadPool.Threads.Add(this);
                return this;
            }
        }

        public static void Sleep(ulong Millionsecos) {
            Timer.Sleep(Millionsecos);
        }
    }

    internal static unsafe class ThreadPool {
        internal const ulong Ring0ContextSwitchMarker = 0x52494E473352304CUL;
        public static List<Thread> Threads;
        public static bool Initialized = false;
        public static bool Locked = false;
        public static long Locker = 0;
        
        /// <summary>
        /// When true, timer interrupts will perform context switching.
        /// When false, timer interrupts just update Timer.Ticks and return.
        /// This allows boot to complete without being interrupted by scheduling.
        /// </summary>
        public static bool SchedulingEnabled = false;
        public static ulong KernelCr3 { get; private set; }
        private static Thread _directUserThread;
        private static ulong _bootstrapKernelStackTop;

        internal static bool IsDirectUser => _directUserThread != null;

        public static Thread CurrentThread {
            get {
                if (_directUserThread != null) return _directUserThread;
                if (Threads == null || Threads.Count == 0 || Index < 0 || Index >= Threads.Count)
                    return null;
                return Threads[Index];
            }
        }

        public static Ring3Process CurrentProcess => CurrentThread?.OwnerProcess;

        private static int Index {
            get {
                return Indexs[SMP.ThisCPU];
            }
            set {
                Indexs[SMP.ThisCPU] = value;
            }
        }

        public static void Initialize() {
            Native.Cli();
            KernelCr3 = Native.ReadCR3() & PageTable.PageMask;
            _bootstrapKernelStackTop = GDT.KernelStackTop;

            // Debug: entering ThreadPool.Initialize (no wait loop - just blast it out)
            BootConsole.WriteLine("TP1");
            
            //Bootstrap CPU
            if (SMP.ThisCPU == 0) {
                // Debug: bootstrap CPU path
                BootConsole.WriteLine("TP2");
                
                // In UEFI mode, ACPI might not be fully initialized
                // Use a default size of 1 for single-CPU operation
                byte size = 0;

                // Debug: checking ACPI CPUIDs
                BootConsole.WriteLine("TP2A");
                // Guard against null LocalAPIC_CPUIDs (UEFI mode)
                bool hasCpuIds = ACPI.LocalAPIC_CPUIDs != null;

                // Debug: null check done
                BootConsole.WriteLine("TP2B");

                if (hasCpuIds) {
                    int count = ACPI.LocalAPIC_CPUIDs.Count;

                    // Debug: got count - simplified without wait
                    BootConsole.Write("TP2C=");
                    Native.Out8(0x3F8, (byte)('0' + (count % 10)));
                    Native.Out8(0x3F8, (byte)'\n');
                    
                    // Skip the loop entirely for now - just use size = 0 (single CPU)
                    // The loop accessing List[i] may be causing issues
                    // if (count > 0) {
                    //     for (int i = 0; i < count; i++) {
                    //         byte cpuId = ACPI.LocalAPIC_CPUIDs[i];
                    //         if (cpuId > size) size = cpuId;
                    //     }
                    // }
                    
                    // For UEFI single-core boot, just use size = 0
                    // This creates a single-element Indexs array which is fine
                }
                
                // Debug: ACPI CPU IDs processed
                BootConsole.WriteLine("TP2D");

                // Debug: allocating Indexs array
                BootConsole.WriteLine("TP3");
                
                Indexs = new int[size + 1];

                Locked = false;
                Initialized = false;
                Threads = new();

                // Debug: creating idle thread
                BootConsole.WriteLine("TP4");
                
                //At least a thread for each CPU to make Thread Pool work
                var t = new Thread(&IdleThread);
                t.IsIdleThread = true;
                t.Start(0);
                Initialized = true;

                // Debug: idle thread created
                BootConsole.WriteLine("TP5");
            }
            //Application CPU
            else {
                //At least a thread for each CPU to make Thread Pool work
                var t = new Thread(&IdleThread);
                t.IsIdleThread = true;
                t.Start((int)SMP.ThisCPU);
            }

            // Debug: done - enable interrupts now (scheduling still disabled)
            BootConsole.WriteLine("TP6");

            // Debug: about to call Sti
            BootConsole.WriteLine("STI-");

            // Enable interrupts so timer can fire (but scheduling is disabled via SchedulingEnabled flag)
            Native.Sti();

            // Debug: Sti returned! This should print after interrupts are enabled
            BootConsole.WriteLine("OK");

            // Debug: function is about to return
            BootConsole.WriteLine("RET");

            // NOTE: Schedule_Next() moved to explicit StartScheduling() call
            // to prevent thread switching during boot initialization
            // Schedule_Next(); //start scheduling
        }
        
        /// <summary>
        /// Explicitly start thread scheduling.
        /// Call this AFTER all kernel initialization is complete.
        /// </summary>
        public static void StartScheduling() {
            if (!Initialized || Threads == null || Threads.Count == 0) {
                BootConsole.WriteLine("[SCHED] ThreadPool not initialized - scheduling disabled");
                for (; ; ) Native.Hlt();
            }

            BootConsole.WriteLine("[SCHED] Enabling preemptive scheduling");
            
            // Enable context switching in the timer interrupt handler
            SchedulingEnabled = true;

            BootConsole.WriteLine("[SCHED] Using IRQ0-driven scheduling");
            BootConsole.WriteLine("[SCHED] Waiting for timer interrupts");

            // Do NOT call Schedule_Next() directly here.
            // Scheduling occurs from IDT.intr_handler on IRQ 0x20 via ThreadPool.Schedule(stack).
            for (; ; ) {
                Native.Hlt();
            }
        }

        /// <summary>
        /// Enables timer-driven scheduling without taking over the current
        /// kernel thread.  The UEFI desktop owns a continuous render loop,
        /// so the old StartScheduling() handoff is unreachable there.
        /// </summary>
        internal static void EnableScheduling() {
            if (!Initialized || Threads == null || Threads.Count == 0) {
                BootConsole.WriteLine("[SCHED] ThreadPool not initialized - scheduling disabled");
                return;
            }

            SchedulingEnabled = true;
            BootConsole.WriteLine("[SCHED] Enabling preemptive scheduling");
            BootConsole.WriteLine("[SCHED] Using IRQ0-driven scheduling");
        }

        internal static void BeginDirectUser(Thread thread) {
            if (thread == null || thread.OwnerProcess == null ||
                thread.OwnerProcess.Space == null) return;
            _directUserThread = thread;
            GDT.SetKernelStack(thread.KernelStackTop);
            Native.WriteCR3(thread.OwnerProcess.Space.RootPhysical);
        }

        internal static void EndDirectUser() {
            _directUserThread = null;
            GDT.SetKernelStack(_bootstrapKernelStackTop);
            Native.WriteCR3(KernelCr3);
        }

        public static void Terminate() {
            //Console.Write("Thread ");
            //Console.Write(Index.ToString());
            BootConsole.WriteLine(" Has Exited");
            Threads[Index].Terminated = true;
            Schedule_Next();
            Panic.Error("Termination Failed!");
        }

        internal static bool RemoveThread(Thread thread) {
            if (thread == null || Threads == null || Threads.Count == 0)
                return false;
            if (CurrentThread == thread) return false;

            for (int i = 0; i < Threads.Count; i++) {
                if (Threads[i] != thread) continue;
                Threads.RemoveAt(i);
                if (Threads.Count == 0) return true;
                int current = Index;
                if (i < current) current--;
                if (current >= Threads.Count) current = Threads.Count - 1;
                if (current < 0) current = 0;
                Index = current;
                return true;
            }
            return false;
        }

        [DllImport("*")]
        public static extern void Schedule_Next();

        public static void TestThread() {
            BootConsole.WriteLine("Non-Loop Thread Test!");
            return;
        }

        public static void A() {
            for (; ; ) BootConsole.WriteLine("Thread A");
        }

        public static void B() {
            for (; ; ) BootConsole.WriteLine("Thread B");
        }

        public static void IdleThread() {
            for (; ; ) Schedule_Next();
        }

        private static int[] Indexs;

        public static bool CanLock => Unsafe.As<bool, ulong>(ref Initialized);

        public static void Lock() {
            Locker = SMP.ThisCPU;
            Locked = true;

            LocalAPIC.SendAllInterrupt(0x20);
        }

        public static void UnLock() {
            Locked = false;
        }

        public static int ThreadCount => Threads.Count;

        private static uint TickAll;
        private static uint TickIdle;

        public static uint CPUUsage;
        public static void Schedule(IDT.IDTStackGeneric* stack) {
            if (!Initialized) return;
            
            // If scheduling is not enabled, just return without context switching
            // This allows boot to complete with interrupts enabled but no preemption
            if (!SchedulingEnabled) return;

            //Lock all processors except locker CPU
            if (Locked && Locker != SMP.ThisCPU) {
                while (Locked) Native.Nop();
                return;
            }

            //Lock locker CPU
            if (Locked && Locker == SMP.ThisCPU) return;

            Thread current = Threads[Index];
            if (!current.Terminated &&
                current.RunOnWhichCPU == SMP.ThisCPU) {
                Native.Movsb(current.Stack, stack, (ulong)sizeof(IDT.IDTStackGeneric));
                if (current.IsUserThread && current.OwnerProcess != null) {
                    current.OwnerProcess.RecordTimerPreemption(stack);
                }
                if ((stack->irs.cs & 3UL) == 0) {
                    // The native common stub preserves the interrupted
                    // kernel RSP in the synthetic RSP slot used by the
                    // scheduler frame.  Keep that authoritative value for
                    // the ring-0 resume path.
                    Threads[Index].Stack->irs.rsp = stack->irs.rsp;
                }
            }

            int fallbackIndex = -1;
            int preferredIndex = -1;
            for (int step = 1; step <= Threads.Count; step++) {
                int candidate = (Index + step) % Threads.Count;
                Thread candidateThread = Threads[candidate];
                if (candidateThread.Terminated ||
                    candidateThread.RunOnWhichCPU != SMP.ThisCPU)
                    continue;
                if (fallbackIndex < 0) fallbackIndex = candidate;
                if (candidate != Index && !candidateThread.IsIdleThread) {
                    preferredIndex = candidate;
                    break;
                }
            }
            if (preferredIndex >= 0) Index = preferredIndex;
            else if (fallbackIndex >= 0) Index = fallbackIndex;
            else return;

            #region CPU Usage
            if (SMP.ThisCPU == 0) {
                if ((Timer.Ticks % 100) == 0) {
                    if (TickAll != 0 && TickIdle != 0)
                        CPUUsage = 100 - ((TickIdle * 100) / TickAll);
                    TickIdle = 0;
                    TickAll = 0;
                }
            }
            if (Threads[Index].IsIdleThread) {
                TickIdle++;
            }
            TickAll++;
            #endregion

            if ((Threads[Index].Stack->irs.cs & 3UL) == 0UL)
                Threads[Index].Stack->vectorSlot = Ring0ContextSwitchMarker;
            Native.Movsb(stack, Threads[Index].Stack, (ulong)sizeof(IDT.IDTStackGeneric));
            PrepareThreadForRun(Threads[Index]);
        }

        private static void PrepareThreadForRun(Thread thread) {
            if (thread == null) return;
            if (thread.IsUserThread && thread.OwnerProcess != null &&
                !thread.OwnerProcess.IsTerminal &&
                thread.OwnerProcess.Space != null) {
                GDT.SetKernelStack(thread.KernelStackTop);
                Native.WriteCR3(thread.OwnerProcess.Space.RootPhysical);
                thread.OwnerProcess.RecordSchedulerDispatch();
            } else {
                GDT.SetKernelStack(thread.KernelStackTop);
                Native.WriteCR3(KernelCr3);
            }
        }
    }
}
