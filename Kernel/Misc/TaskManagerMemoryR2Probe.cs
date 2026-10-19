using guideXOS.Kernel.Drivers;
using guideXOS.OS;

namespace guideXOS.Misc {
#if UEFI_TASKMANAGER_MEMORY_R2_PROBE
    // Numeric-only R13 witness for one interactive Task Manager lifetime.
    internal static class TaskManagerMemoryR2Probe {
        private static ulong _baselineSequence;
        private static ulong _closedTick;
        private static ulong _deferredPollCount;
        private static ulong _lifetimeIndex;
        private static ulong _fallbackStartTick;
        private static ulong _fallbackInvalidStartTick;
        private static int _fallbackStage;
        private static bool _fallbackInvalidRendered;
        private static bool _fallbackRecoveryRendered;
        private static bool _fallbackRecoveryRefreshMissingReported;
        private static bool _fallbackRecoveryStateReported;
        private static bool _fallbackServiceStateReported;
        private static bool _fallbackTaskManagerStateReported;
        private static int _phase;

        internal static void BeginBeforeLaunch() {
            if (_phase != 0 && _phase != 7) return;
            _lifetimeIndex++;
            _closedTick = 0;
            _deferredPollCount = 0;
            _baselineSequence = Allocator.DiagnosticAllocationSequence;
            Allocator.CaptureDiagnosticRunBaseline();
            EmitMarker("B0_BEFORE_TASK_MANAGER");
            int snapshot = (int)(_lifetimeIndex * 10UL);
            Allocator.DumpDiagnosticSnapshot(snapshot);
            Allocator.DumpDiagnosticRunsSince(_baselineSequence);
            Allocator.DumpDiagnosticSiteTotalsSince(_baselineSequence,
                snapshot);
            Allocator.DumpDiagnosticHelperTotalsSince(_baselineSequence,
                snapshot);
            _phase = 1;
        }

        internal static void ObserveBeforeRefresh(int tab) {
            if (_phase != 1) return;
            EmitMarker("B1_TASK_MANAGER_OPEN");
            DumpPhase(1, false);
            _phase = 2;
        }

        internal static void ObserveAfterRefresh(int tab) {
            if (_phase == 2 && tab == 3) {
                EmitMarker("B2_MEMORY_DETAILS_REFRESHED");
                DumpPhase(2, false);
                _phase = 4;
                return;
            }
            if (_phase == 4 && tab != 3) {
                EmitMarker("B3_LEFT_MEMORY_DETAILS");
                DumpPhase(3, false);
                _phase = 5;
            }
        }

        internal static void MarkWindowDisposed() {
            if (_phase < 5 || _phase >= 6) return;
            EmitMarker("B4_TASK_MANAGER_CLOSED");
            DumpPhase(4, false);
            _closedTick = Timer.Ticks;
            _phase = 6;
        }

        internal static void PollDeferredCleanup() {
            if (_phase != 6) return;
            ulong now = Timer.Ticks;
            if (_deferredPollCount == 0 || _deferredPollCount % 60 == 0)
                EmitWaitState(now);
            _deferredPollCount++;
            if (now < _closedTick || now - _closedTick < 2000) return;
            EmitMarker("B5_DEFERRED_CLEANUP");
            DumpPhase(5, true);
            _phase = 7;
        }

        internal static void ApplyFallbackProbe(int tab,
                ref ulong memorySizeBytes, ref ulong memoryInUseBytes,
                ref bool available) {
            if (tab != 3 || !available) return;

            ulong now = Timer.Ticks;
            if (_fallbackStage == 0) {
                _fallbackStartTick = now;
                _fallbackStage = 1;
                return;
            }
            if (_fallbackStage == 1 && now >= _fallbackStartTick &&
                    now - _fallbackStartTick >= 750) {
                _fallbackInvalidStartTick = now;
                _fallbackStage = 2;
                EmitFallbackMarker("R2_TASKMANAGER_NA_INVALID_BEGIN");
            }
            if (_fallbackStage == 2) {
                if (now >= _fallbackInvalidStartTick &&
                        now - _fallbackInvalidStartTick < 1000) {
                    memorySizeBytes = 1UL;
                    memoryInUseBytes = 2UL;
                    // Model production rejection of malformed statistics:
                    // the UI receives an unavailable snapshot and must recover
                    // when the next valid service result arrives.
                    available = false;
                    return;
                }
                _fallbackStage = 3;
                EmitFallbackMarker("R2_TASKMANAGER_NA_SNAPSHOT_VALID");
            }
        }

        internal static void ObserveFallbackLabels(bool snapshotValid,
                bool labelsRefreshed, bool snapshotAvailable,
                bool snapshotWithinBounds, ulong memorySizeBytes,
                ulong memoryInUseBytes, bool lastSnapshotAvailable,
                bool lastSnapshotValid, ulong now,
                ulong lastLabelUpdateTicks) {
            if (_fallbackStage == 3 && _fallbackInvalidRendered &&
                    !_fallbackRecoveryStateReported) {
                _fallbackRecoveryStateReported = true;
                EmitFallbackState(snapshotValid, labelsRefreshed,
                    snapshotAvailable, snapshotWithinBounds,
                    memorySizeBytes, memoryInUseBytes,
                    lastSnapshotAvailable, lastSnapshotValid, now,
                    lastLabelUpdateTicks);
            }
            if (_fallbackStage == 2 && !_fallbackInvalidRendered &&
                    labelsRefreshed &&
                    !snapshotValid) {
                _fallbackInvalidRendered = true;
                EmitFallbackMarker("R2_TASKMANAGER_NA_INVALID_RENDERED");
                return;
            }
            if (_fallbackStage == 3 && _fallbackInvalidRendered &&
                    !_fallbackRecoveryRendered && snapshotValid &&
                    labelsRefreshed) {
                _fallbackRecoveryRendered = true;
                EmitFallbackMarker("R2_TASKMANAGER_NA_RECOVERY_RENDERED");
                return;
            }
            if (_fallbackStage == 3 && _fallbackInvalidRendered &&
                    !_fallbackRecoveryRendered &&
                    !_fallbackRecoveryRefreshMissingReported &&
                    snapshotValid && !labelsRefreshed) {
                _fallbackRecoveryRefreshMissingReported = true;
                EmitFallbackMarker(
                    "R2_TASKMANAGER_NA_RECOVERY_NO_LABEL_REFRESH");
            }
        }

        internal static void ObserveServiceSnapshot(ulong allocatorMemorySize,
                ulong allocatorMemoryInUse,
                SystemInformationSnapshot snapshot,
                SystemInformationSnapshot resultValue) {
            if (_fallbackStage != 3 || _fallbackServiceStateReported) return;
            _fallbackServiceStateReported = true;
            EmitText("R2_TASKMANAGER_SERVICE_SNAPSHOT;allocatorSize=0x");
            EmitHex(allocatorMemorySize);
            EmitText(";allocatorUsed=0x");
            EmitHex(allocatorMemoryInUse);
            EmitText(";snapshotSize=0x");
            EmitHex(snapshot.MemorySizeBytes);
            EmitText(";snapshotUsed=0x");
            EmitHex(snapshot.MemoryInUseBytes);
            EmitText(";resultSize=0x");
            EmitHex(resultValue.MemorySizeBytes);
            EmitText(";resultUsed=0x");
            EmitHex(resultValue.MemoryInUseBytes);
            Native.Out8(0x3F8, (byte)'\n');
        }

        internal static void ObserveTaskManagerSnapshot(
                bool usedScalarFallback, ulong localMemorySize,
                ulong localMemoryInUse, ulong storedMemorySize,
                ulong storedMemoryInUse, bool available) {
            if (_fallbackStage != 3 ||
                    _fallbackTaskManagerStateReported) return;
            _fallbackTaskManagerStateReported = true;
            EmitText("R2_TASKMANAGER_REFRESH_SNAPSHOT;fallback=0x");
            EmitHex(usedScalarFallback ? 1UL : 0UL);
            EmitText(";localSize=0x");
            EmitHex(localMemorySize);
            EmitText(";localUsed=0x");
            EmitHex(localMemoryInUse);
            EmitText(";storedSize=0x");
            EmitHex(storedMemorySize);
            EmitText(";storedUsed=0x");
            EmitHex(storedMemoryInUse);
            EmitText(";available=0x");
            EmitHex(available ? 1UL : 0UL);
            Native.Out8(0x3F8, (byte)'\n');
        }

        private static void DumpPhase(int phase, bool includeLiveRuns) {
            int snapshot = (int)(_lifetimeIndex * 10UL +
                (ulong)phase);
            Allocator.DumpDiagnosticRunsSince(_baselineSequence);
            Allocator.DumpDiagnosticSiteTotalsSince(_baselineSequence,
                snapshot);
            Allocator.DumpDiagnosticHelperTotalsSince(_baselineSequence,
                snapshot);
            if (includeLiveRuns) {
                Allocator.DumpDiagnosticLiveRunsSince(_baselineSequence,
                    snapshot);
                Allocator.DumpStringLedger(0, 5, _baselineSequence);
                Allocator.DumpDiagnosticSnapshot(snapshot);
            }
        }

        private static void EmitMarker(string marker) {
            EmitText("R2_TASKMANAGER_CYCLE=0x");
            EmitHex(_lifetimeIndex);
            Native.Out8(0x3F8, (byte)'\n');
            const string prefix = "R2_TASKMANAGER_PHASE=";
            for (int i = 0; i < prefix.Length; i++)
                Native.Out8(0x3F8, (byte)prefix[i]);
            for (int i = 0; i < marker.Length; i++)
                Native.Out8(0x3F8, (byte)marker[i]);
            Native.Out8(0x3F8, (byte)'\n');
        }

        private static void EmitWaitState(ulong now) {
            EmitText("R2_TASKMANAGER_WAIT;closed=0x");
            EmitHex(_closedTick);
            EmitText(";now=0x");
            EmitHex(now);
            EmitText(";polls=0x");
            EmitHex(_deferredPollCount);
            Native.Out8(0x3F8, (byte)'\n');
        }

        private static void EmitText(string text) {
            for (int i = 0; i < text.Length; i++)
                Native.Out8(0x3F8, (byte)text[i]);
        }

        private static void EmitFallbackMarker(string marker) {
            EmitText(marker);
            Native.Out8(0x3F8, (byte)'\n');
        }

        private static void EmitFallbackState(bool snapshotValid,
                bool labelsRefreshed, bool snapshotAvailable,
                bool snapshotWithinBounds, ulong memorySizeBytes,
                ulong memoryInUseBytes, bool lastSnapshotAvailable,
                bool lastSnapshotValid, ulong now,
                ulong lastLabelUpdateTicks) {
            EmitText("R2_TASKMANAGER_NA_RECOVERY_STATE;valid=0x");
            EmitHex(snapshotValid ? 1UL : 0UL);
            EmitText(";refreshed=0x");
            EmitHex(labelsRefreshed ? 1UL : 0UL);
            EmitText(";available=0x");
            EmitHex(snapshotAvailable ? 1UL : 0UL);
            EmitText(";bounds=0x");
            EmitHex(snapshotWithinBounds ? 1UL : 0UL);
            EmitText(";size=0x");
            EmitHex(memorySizeBytes);
            EmitText(";used=0x");
            EmitHex(memoryInUseBytes);
            EmitText(";lastAvailable=0x");
            EmitHex(lastSnapshotAvailable ? 1UL : 0UL);
            EmitText(";lastValid=0x");
            EmitHex(lastSnapshotValid ? 1UL : 0UL);
            EmitText(";now=0x");
            EmitHex(now);
            EmitText(";lastLabel=0x");
            EmitHex(lastLabelUpdateTicks);
            Native.Out8(0x3F8, (byte)'\n');
        }

        private static void EmitHex(ulong value) {
            const string digits = "0123456789ABCDEF";
            for (int shift = 60; shift >= 0; shift -= 4)
                Native.Out8(0x3F8,
                    (byte)digits[(int)((value >> shift) & 0xFUL)]);
        }
    }
#endif
}
