using guideXOS.Kernel.Drivers;

namespace guideXOS.Misc {
#if UEFI_TASKMANAGER_MEMORY_R3_PROBE
    // Fixed, numeric-only lifecycle witness. Capture happens at boundaries;
    // evidence is emitted only after B6 has been captured.
    internal static class TaskManagerMemoryR3Probe {
        internal const uint SiteStartMenuCreate = 1001;
        internal const uint SiteStartMenuWindowSnapshot = 1002;
        internal const uint SiteStartMenuDraw = 1003;
        internal const uint SiteStartMenuSelection = 1004;
        internal const uint SiteStartMenuTruncate = 1005;
        internal const uint SiteStartMenuWindowListCreate = 1006;
        internal const uint SiteLaunchRequestForName = 1101;
        internal const uint SiteLaunchConvenienceResult = 1102;
        internal const uint SiteFactoryRequestClone = 1103;
        internal const uint SiteLaunchResolution = 1201;
        internal const uint SiteApplicationFactoryResult = 1301;
        internal const uint SiteFactoryLaunchResult = 1302;
        internal const uint SiteApplicationServiceContext = 1401;
        internal const uint SiteTaskManagerWindowCreate = 1501;
        internal const uint SiteTaskManagerRefresh = 1502;
        internal const uint SiteMemoryDetailsDraw = 1503;
        internal const uint SiteMemoryDetailsLabels = 1504;
        internal const uint SiteRecentProgramSnapshot = 1601;
        internal const uint SiteTaskManagerFactory = 1602;
        internal const uint SiteTaskManagerDispose = 1603;
        internal const uint SiteWindowDispose = 1701;
        internal const uint SiteDesktopUpdate = 1801;
        internal const uint SiteSharedWindowDraw = 1802;
        internal const uint SiteTaskbarDraw = 1803;
        internal const uint SiteWorkspaceSwitcherDraw = 1804;
        internal const uint SiteTaskbarClockFormat = 1805;
        internal const uint SiteTaskbarVisualReport = 1806;

        private struct Life {
            internal Allocator.MemoryR3Snapshot B0, B1, B2, B3, B4, B5, B6;
            internal ulong B0Tick, B1Tick, B2Tick, B3Tick, B4Tick, B5Tick, B6Tick;
            internal ulong BaselineFreedPages;
            internal ulong WindowInstanceId;
            internal int WindowId;
            internal bool HasB0, HasB1, HasB2, HasB3, HasB4, HasB5, HasB6;
        }

        private static Life _life;
        private static Allocator.MemoryR3Snapshot _preMenu;
        private static Allocator.MemoryR3Snapshot _previousB6ForGap;
        private static Allocator.MemoryR3Snapshot _gapStart, _gapEnd;
        private static bool _preMenuCaptured;
        private static bool _previousB6Captured;
        private static bool _gapCaptured;
        private static ulong _preMenuBaselineFreedPages;
        private static ulong _gapBaselineFreedPages;
        private static uint _lifetimeId;
        private static int _phase;
        private static uint _deferredPollCount;
        private static bool _traceAfterPollWritten;
        private static bool _traceAfterTaskbarWritten;
        private static ulong _closedTick;

        internal static void BeforeStartMenuOpen() {
            if (_preMenuCaptured) return;
            Allocator.BeginMemoryR3Interval();
            Allocator.CaptureDiagnosticRunBaseline();
            _preMenu = Allocator.CaptureMemoryR3Snapshot();
            _preMenuCaptured = true;
        }

        internal static void BeginBeforeLaunch() {
            if (_phase != 0) return;
            _life = default(Life);
            _lifetimeId++;
            if (_lifetimeId == 0) _lifetimeId = 1;
            Allocator.CurrentMemoryR3LifetimeId = _lifetimeId;
            Allocator.CurrentMemoryR3WindowId = 0;
            Allocator.CurrentMemoryR3InstanceId = 0;
            Allocator.CurrentMemoryR3SiteId = 0;
            Allocator.BeginMemoryR3Interval();
            if (_preMenuCaptured)
                _preMenuBaselineFreedPages =
                    Allocator.CountMemoryR3BaselineFreedPages();
            _gapCaptured = false;
            if (_previousB6Captured) {
                _gapStart = _previousB6ForGap;
                _gapBaselineFreedPages =
                    Allocator.CountMemoryR3BaselineFreedPages();
            }
            Allocator.CaptureDiagnosticRunBaseline();
            _life.B0 = Allocator.CaptureMemoryR3Snapshot();
            if (_previousB6Captured) {
                _gapEnd = _life.B0;
                _gapCaptured = true;
            }
            _life.B0Tick = Timer.Ticks;
            _life.HasB0 = true;
            _phase = 1;
            TraceBoundary(0);
        }

        internal static void RestoreActiveLifetimeContext() {
            if (_phase >= 1 && _phase <= 6)
                Allocator.CurrentMemoryR3LifetimeId = _lifetimeId;
        }

        internal static void WindowCreated(object manager, int windowId,
                ulong instanceId, ulong firstSequence) {
            if (_phase != 1) return;
            _life.WindowId = windowId;
            _life.WindowInstanceId = instanceId;
            ulong lastSequence = Allocator.DiagnosticAllocationSequence;
            Allocator.StampMemoryR3RunRange(firstSequence, lastSequence,
                windowId, instanceId);
            Allocator.StampMemoryR3Object(manager, SiteTaskManagerWindowCreate,
                windowId, instanceId);
            _life.B1 = Allocator.CaptureMemoryR3Snapshot();
            _life.B1Tick = Timer.Ticks;
            _life.HasB1 = true;
            Allocator.CurrentMemoryR3WindowId = windowId;
            Allocator.CurrentMemoryR3InstanceId = instanceId;
            _phase = 2;
            TraceBoundary(1);
        }

        internal static void StartMenuCreated(object menu, int windowId,
                ulong firstSequence) {
            ulong lastSequence = Allocator.DiagnosticAllocationSequence;
            Allocator.StampMemoryR3RunRange(firstSequence, lastSequence,
                windowId, 0);
            Allocator.StampMemoryR3Object(menu, SiteStartMenuCreate,
                windowId, 0);
        }

        internal static void ObserveBeforeRefresh(int tab) {
            if (_phase == 2 && tab == 3) {
                _life.B2 = Allocator.CaptureMemoryR3Snapshot();
                _life.B2Tick = Timer.Ticks;
                _life.HasB2 = true;
                _phase = 3;
                TraceBoundary(2);
            } else if (_phase == 4 && tab != 3) {
                _life.B4 = Allocator.CaptureMemoryR3Snapshot();
                _life.B4Tick = Timer.Ticks;
                _life.HasB4 = true;
                _phase = 5;
                TraceBoundary(4);
            }
        }

        internal static void ObserveAfterDraw(int tab) {
            if (_phase != 3 || tab != 3) return;
            _life.B3 = Allocator.CaptureMemoryR3Snapshot();
            _life.B3Tick = Timer.Ticks;
            _life.HasB3 = true;
            _phase = 4;
            TraceBoundary(3);
        }

        internal static void WindowCleanupCompleted(int windowId) {
            if (_phase < 2 || _phase >= 6 || windowId != _life.WindowId)
                return;
            _life.B5 = Allocator.CaptureMemoryR3Snapshot();
            _life.B5Tick = Timer.Ticks;
            _life.HasB5 = true;
            _closedTick = _life.B5Tick;
            _deferredPollCount = 0;
            _traceAfterPollWritten = false;
            _traceAfterTaskbarWritten = false;
            _phase = 6;
            TraceBoundary(5);
        }

        internal static void TraceAfterDeferredPoll() {
#if UEFI_TASKMANAGER_MEMORY_R3_TRACE
            if (_phase != 6 || _deferredPollCount != 1 ||
                    _traceAfterPollWritten) return;
            _traceAfterPollWritten = true;
            Write("R3_FRAME;stage=after-deferred-poll;life=");
            Hex(_lifetimeId);
            Newline();
#endif
        }

        internal static void TraceAfterTaskbarDraw() {
#if UEFI_TASKMANAGER_MEMORY_R3_TRACE
            if (_phase != 6 || _deferredPollCount != 1 ||
                    _traceAfterTaskbarWritten) return;
            _traceAfterTaskbarWritten = true;
            Write("R3_FRAME;stage=after-taskbar-draw;life=");
            Hex(_lifetimeId);
            Newline();
#endif
        }

        internal static void PollDeferredCleanup() {
            if (_phase != 6) return;
            ulong now = Timer.Ticks;
            if (_deferredPollCount < 240) _deferredPollCount++;
#if UEFI_TASKMANAGER_MEMORY_R3_TRACE
            if (_deferredPollCount == 1 ||
                    (_deferredPollCount % 60) == 0) {
                Write("R3_WAIT;poll="); Hex(_deferredPollCount);
                Write(";ticks="); Hex(now);
                Write(";sinceClose=");
                Hex(now >= _closedTick ? now - _closedTick : 0);
                Newline();
            }
#endif
            bool timerDelayElapsed = now >= _closedTick &&
                now - _closedTick >= 2000;
            if (!timerDelayElapsed && _deferredPollCount < 240) return;
            _life.B6 = Allocator.CaptureMemoryR3Snapshot();
            _life.B6Tick = now;
            _life.BaselineFreedPages =
                Allocator.CountMemoryR3BaselineFreedPages();
            _life.HasB6 = true;
            _phase = 7;
            TraceBoundary(6);
#if !UEFI_TASKMANAGER_MEMORY_R3_NO_EMIT
            EmitEvidence();
#endif
            _phase = 0;
            Allocator.CurrentMemoryR3SiteId = 0;
            Allocator.CurrentMemoryR3WindowId = 0;
            Allocator.CurrentMemoryR3InstanceId = 0;
            Allocator.CurrentMemoryR3LifetimeId = 0;
        }

        private static void EmitEvidence() {
            Allocator.MemoryR3Snapshot beforeEmit =
                Allocator.CaptureMemoryR3Snapshot();
            if (_preMenuCaptured && _lifetimeId == 1) {
                EmitSnapshot(0, "PREMENU", _preMenu, _preMenu);
                EmitMenuInventory();
            }
            EmitInterLifetimeGap();
            EmitSnapshot(_lifetimeId, "B0", _life.B0, _life.B0);
            EmitSnapshot(_lifetimeId, "B1", _life.B1, _life.B0);
            EmitSnapshot(_lifetimeId, "B2", _life.B2, _life.B0);
            EmitSnapshot(_lifetimeId, "B3", _life.B3, _life.B0);
            EmitSnapshot(_lifetimeId, "B4", _life.B4, _life.B0);
            EmitSnapshot(_lifetimeId, "B5", _life.B5, _life.B0);
            EmitSnapshot(_lifetimeId, "B6", _life.B6, _life.B0);
            EmitLifecycleSummary();
            EmitRetainedRuns();
            Allocator.MemoryR3Snapshot afterEmit =
                Allocator.CaptureMemoryR3Snapshot();
            Write("R3_EMIT;allocPages="); Hex(afterEmit.AllocatedPages -
                beforeEmit.AllocatedPages);
            Write(";freedPages="); Hex(afterEmit.FreedPages -
                beforeEmit.FreedPages);
            Write(";liveDelta="); SignedDelta(beforeEmit.LivePages,
                afterEmit.LivePages);
            Newline();
            Allocator.CaptureDiagnosticRunBaseline();
            _previousB6ForGap = _life.B6;
            _previousB6Captured = true;
        }

        private static void EmitInterLifetimeGap() {
            if (!_gapCaptured) return;
            ulong alloc = _gapEnd.AllocatedPages -
                _gapStart.AllocatedPages;
            ulong freed = _gapEnd.FreedPages - _gapStart.FreedPages;
            ulong liveDelta = _gapEnd.LivePages >= _gapStart.LivePages
                ? _gapEnd.LivePages - _gapStart.LivePages : 0;
            ulong liveNegative = _gapStart.LivePages > _gapEnd.LivePages
                ? _gapStart.LivePages - _gapEnd.LivePages : 0;
            ulong retained = SumRunsLiveAt(_gapStart.AllocationSequence + 1,
                _gapEnd.AllocationSequence, _gapEnd.FreeSequence);
            ulong inventory = retained >= _gapBaselineFreedPages
                ? retained - _gapBaselineFreedPages : 0;
            ulong inventoryNegative = _gapBaselineFreedPages > retained
                ? _gapBaselineFreedPages - retained : 0;
            ulong span = _gapEnd.AllocationSequence -
                _gapStart.AllocationSequence;
            bool complete = span <= (ulong)Allocator.DiagnosticRunCapacity &&
                _gapEnd.DroppedRunRecords == _gapStart.DroppedRunRecords;
            bool reconciles = liveDelta == inventory &&
                liveNegative == inventoryNegative &&
                (alloc >= freed ? alloc - freed == liveDelta :
                    freed - alloc == liveNegative);
            Write("R3_GAP;fromLife="); Hex(_lifetimeId - 1);
            Write(";toLife="); Hex(_lifetimeId);
            Write(";allocPages="); Hex(alloc);
            Write(";freedPages="); Hex(freed);
            Write(";liveDelta="); SignedDelta(_gapStart.LivePages,
                _gapEnd.LivePages);
            Write(";retainedNewPages="); Hex(retained);
            Write(";baselineFreedPages="); Hex(_gapBaselineFreedPages);
            Write(";inventoryNet=");
            if (inventoryNegative != 0) {
                Write("-"); Hex(inventoryNegative);
            } else Hex(inventory);
            Write(";netCheck="); Hex(reconciles ? 1UL : 0UL);
            Write(";captureComplete="); Hex(complete ? 1UL : 0UL);
            Write(";span="); Hex(span);
            Newline();

            ulong first = _gapStart.AllocationSequence + 1;
            ulong last = _gapEnd.AllocationSequence;
            if (last < first) return;
            for (ulong id = first; id <= last; id++) {
                int slot = (int)((id - 1) %
                    (ulong)Allocator.DiagnosticRunCapacity);
                Allocator.MemoryR3RunRecord run =
                    Allocator.GetMemoryR3RunRecord(slot);
                if (run.Id != id || (run.FreeSequence != 0 &&
                        run.FreeSequence <= _gapEnd.FreeSequence)) continue;
                EmitRun("R3_GAP_RUN", run);
            }
        }

        private static ulong SumRunsLiveAt(ulong first, ulong last,
                ulong freeSequence) {
            ulong sum = 0;
            if (last < first) return 0;
            for (ulong id = first; id <= last; id++) {
                int slot = (int)((id - 1) %
                    (ulong)Allocator.DiagnosticRunCapacity);
                Allocator.MemoryR3RunRecord run =
                    Allocator.GetMemoryR3RunRecord(slot);
                if (run.Id == id && (run.FreeSequence == 0 ||
                        run.FreeSequence > freeSequence))
                    sum += run.Pages;
            }
            return sum;
        }

        private static void EmitSnapshot(uint life, string boundary,
                Allocator.MemoryR3Snapshot snapshot,
                Allocator.MemoryR3Snapshot baseline) {
            Write("R3_SNAP;life="); Hex(life);
            Write(";boundary="); Write(boundary);
            Write(";allocPages="); Hex(snapshot.AllocatedPages -
                baseline.AllocatedPages);
            Write(";freedPages="); Hex(snapshot.FreedPages -
                baseline.FreedPages);
            Write(";livePages="); Hex(snapshot.LivePages);
            Write(";liveRuns="); Hex(snapshot.LiveRuns);
            Write(";maxLive="); Hex(snapshot.IntervalMaximumLivePages);
            Write(";everMaxLive="); Hex(snapshot.MaximumLivePages);
            Write(";reusablePages="); Hex(snapshot.ReusablePages);
            Write(";allocSeq="); Hex(snapshot.AllocationSequence);
            Write(";freeSeq="); Hex(snapshot.FreeSequence);
            Write(";freeCalls="); Hex(snapshot.FreeCalls);
            Write(";freeSuccess="); Hex(snapshot.FreeSuccesses);
            Write(";failPtr="); Hex(snapshot.FreeInvalidPointer);
            Write(";failNoPages="); Hex(snapshot.FreeNoPages);
            Write(";failCorrupt="); Hex(snapshot.FreeCorruptRun);
            Write(";dropped="); Hex(snapshot.DroppedRunRecords);
            Newline();
        }

        private static void EmitLifecycleSummary() {
            ulong liveDelta = _life.B6.LivePages >= _life.B0.LivePages
                ? _life.B6.LivePages - _life.B0.LivePages
                : 0;
            ulong signedNegative = _life.B0.LivePages > _life.B6.LivePages
                ? _life.B0.LivePages - _life.B6.LivePages : 0;
            ulong alloc = _life.B6.AllocatedPages - _life.B0.AllocatedPages;
            ulong freed = _life.B6.FreedPages - _life.B0.FreedPages;
            ulong retained = SumNewLiveRuns();
            ulong signedNetFromInventory = retained >=
                _life.BaselineFreedPages
                    ? retained - _life.BaselineFreedPages : 0;
            ulong negativeNetFromInventory =
                _life.BaselineFreedPages > retained
                    ? _life.BaselineFreedPages - retained : 0;
            ulong span = _life.B6.AllocationSequence -
                _life.B0.AllocationSequence;
            bool complete = span <= (ulong)Allocator.DiagnosticRunCapacity &&
                _life.B6.DroppedRunRecords ==
                    _life.B0.DroppedRunRecords;
            Write("R3_LIFE;id="); Hex(_lifetimeId);
            Write(";window="); Hex(unchecked((uint)_life.WindowId));
            Write(";instance="); Hex(_life.WindowInstanceId);
            Write(";B0toB6_alloc="); Hex(alloc);
            Write(";B0toB6_freed="); Hex(freed);
            Write(";B0toB6_net="); SignedDelta(_life.B0.LivePages,
                _life.B6.LivePages);
            Write(";B0toB6_liveRuns="); SignedDelta(_life.B0.LiveRuns,
                _life.B6.LiveRuns);
            Write(";retainedNewPages="); Hex(retained);
            Write(";baselineFreedPages="); Hex(_life.BaselineFreedPages);
            Write(";inventoryNet=");
            if (negativeNetFromInventory != 0) {
                Write("-"); Hex(negativeNetFromInventory);
            } else Hex(signedNetFromInventory);
            Write(";netCheck=");
            bool reconciles = (liveDelta == signedNetFromInventory &&
                    signedNegative == negativeNetFromInventory) &&
                (alloc >= freed ? alloc - freed == liveDelta :
                    freed - alloc == signedNegative);
            Hex(reconciles ? 1UL : 0UL);
            Write(";captureComplete="); Hex(complete ? 1UL : 0UL);
            Write(";span="); Hex(span);
            Write(";missingB2="); Hex(_life.HasB2 ? 0UL : 1UL);
            Write(";missingB3="); Hex(_life.HasB3 ? 0UL : 1UL);
            Write(";missingB4="); Hex(_life.HasB4 ? 0UL : 1UL);
            Write(";deferredTicks="); Hex(_life.B6Tick - _closedTick);
            Write(";deferredPolls="); Hex(_deferredPollCount);
            Newline();
        }

        private static ulong SumNewLiveRuns() {
            ulong sum = 0;
            ulong first = _life.B0.AllocationSequence + 1;
            ulong last = _life.B6.AllocationSequence;
            if (last < first) return 0;
            for (ulong id = first; id <= last; id++) {
                int slot = (int)((id - 1) % (ulong)Allocator.DiagnosticRunCapacity);
                Allocator.MemoryR3RunRecord run =
                    Allocator.GetMemoryR3RunRecord(slot);
                if (run.Id == id && run.FreeSequence == 0)
                    sum += run.Pages;
            }
            return sum;
        }

        private static void EmitRetainedRuns() {
            ulong first = _life.B0.AllocationSequence + 1;
            ulong last = _life.B6.AllocationSequence;
            if (last < first) return;
            for (ulong id = first; id <= last; id++) {
                int slot = (int)((id - 1) % (ulong)Allocator.DiagnosticRunCapacity);
                Allocator.MemoryR3RunRecord run =
                    Allocator.GetMemoryR3RunRecord(slot);
                if (run.Id != id || run.FreeSequence != 0) continue;
                EmitRun("R3_RUN", run);
            }
        }

        private static void EmitMenuInventory() {
            ulong retained = SumLiveRuns(_preMenu.AllocationSequence + 1,
                _life.B0.AllocationSequence, _life.B0.FreeSequence);
            ulong delta = _life.B0.LivePages >= _preMenu.LivePages
                ? _life.B0.LivePages - _preMenu.LivePages : 0;
            ulong negativeDelta = _preMenu.LivePages > _life.B0.LivePages
                ? _preMenu.LivePages - _life.B0.LivePages : 0;
            ulong inventory = retained >= _preMenuBaselineFreedPages
                ? retained - _preMenuBaselineFreedPages : 0;
            ulong inventoryNegative = _preMenuBaselineFreedPages > retained
                ? _preMenuBaselineFreedPages - retained : 0;
            Write("R3_MENU;allocPages="); Hex(_life.B0.AllocatedPages -
                _preMenu.AllocatedPages);
            Write(";freedPages="); Hex(_life.B0.FreedPages -
                _preMenu.FreedPages);
            Write(";liveDelta="); SignedDelta(_preMenu.LivePages,
                _life.B0.LivePages);
            Write(";retainedAtB0="); Hex(retained);
            Write(";baselineFreedPages="); Hex(_preMenuBaselineFreedPages);
            Write(";inventoryNet=");
            if (inventoryNegative != 0) {
                Write("-"); Hex(inventoryNegative);
            } else Hex(inventory);
            bool complete = _life.B0.AllocationSequence -
                _preMenu.AllocationSequence <= (ulong)Allocator.DiagnosticRunCapacity &&
                _life.B0.DroppedRunRecords ==
                    _preMenu.DroppedRunRecords;
            bool reconciles = delta == inventory &&
                negativeDelta == inventoryNegative;
            Write(";netCheck="); Hex(reconciles ? 1UL : 0UL);
            Write(";captureComplete="); Hex(complete ? 1UL : 0UL);
            Newline();

            ulong first = _preMenu.AllocationSequence + 1;
            ulong last = _life.B0.AllocationSequence;
            if (last < first) return;
            for (ulong id = first; id <= last; id++) {
                int slot = (int)((id - 1) % (ulong)Allocator.DiagnosticRunCapacity);
                Allocator.MemoryR3RunRecord run =
                    Allocator.GetMemoryR3RunRecord(slot);
                bool liveAtB0 = run.FreeSequence == 0 ||
                    run.FreeSequence > _life.B0.FreeSequence;
                if (run.Id != id || !liveAtB0) continue;
                EmitRun("R3_MENU_RUN", run);
            }
        }

        private static ulong SumLiveRuns(ulong first, ulong last,
                ulong atFreeSequence) {
            ulong sum = 0;
            if (last < first) return 0;
            for (ulong id = first; id <= last; id++) {
                int slot = (int)((id - 1) % (ulong)Allocator.DiagnosticRunCapacity);
                Allocator.MemoryR3RunRecord run =
                    Allocator.GetMemoryR3RunRecord(slot);
                if (run.Id == id && (run.FreeSequence == 0 ||
                        run.FreeSequence > atFreeSequence))
                    sum += run.Pages;
            }
            return sum;
        }

        private static void EmitRun(string prefix,
                Allocator.MemoryR3RunRecord run) {
            Write(prefix); Write(";id="); Hex(run.Id);
            Write(";addr="); Hex(run.Address);
            Write(";pages="); Hex(run.Pages);
            Write(";requested="); Hex(run.RequestedBytes);
            Write(";kind="); Hex(run.ObjectKind);
            Write(";helper="); Hex(run.ManagedHelper);
            Write(";length="); Hex(run.ManagedLength);
            Write(";eeType="); Hex(run.EEType);
            Write(";site="); Hex(run.SiteId);
            Write(";creationSite="); Hex(run.CreationSiteId);
            Write(";allocOp="); Hex(run.AllocatorOperation);
            Write(";freeSeq="); Hex(run.FreeSequence);
            Write(";disposeCount="); Hex(run.DisposeCount);
            Write(";disposeSite="); Hex(run.DisposeSiteId);
            Write(";owner="); Hex(unchecked((ulong)(long)run.OwnerId));
            Write(";window="); Hex(unchecked((uint)run.WindowId));
            Write(";instance="); Hex(run.WindowInstanceId);
            Write(";lifetime="); Hex(run.LifetimeId);
            Write(";tag="); Hex(run.AllocationTag);
            Write(";caller="); Hex(run.CallerAddress);
            Newline();
        }

        private static void SignedDelta(ulong before, ulong after) {
            if (after >= before) Hex(after - before);
            else { Write("-"); Hex(before - after); }
        }

        private static void TraceBoundary(uint boundary) {
#if UEFI_TASKMANAGER_MEMORY_R3_TRACE
            Write("R3_TRACE;boundary="); Hex(boundary);
            Write(";phase="); Hex((ulong)_phase); Newline();
#endif
        }

        private static void Hex(ulong value) {
            for (int shift = 60; shift >= 0; shift -= 4) {
                int digit = (int)((value >> shift) & 0xFUL);
                byte ch = (byte)(digit < 10 ? '0' + digit : 'A' + digit - 10);
                Native.Out8(0x3F8, ch);
            }
        }

        private static void Write(string text) {
            for (int i = 0; i < text.Length; i++)
                Native.Out8(0x3F8, (byte)text[i]);
        }

        private static void Newline() { Native.Out8(0x3F8, (byte)'\n'); }
    }
#endif
}
