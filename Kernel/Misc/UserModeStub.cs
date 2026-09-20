namespace guideXOS.Misc {
    // The real transition is supplied by native_stubs.asm.  Keep this type so
    // old source references remain harmless, but do not provide a managed
    // fallback that could make a failed CPL3 transition look successful.
    internal static unsafe class UserModeStub { }
}
