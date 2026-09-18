namespace guideXOS.OS {
    /// <summary>
    /// Phase 8 service registry scaffold.  Contract assertions are kept next
    /// to the later registry self-test so contract and lifecycle failures stay
    /// deterministic in the guest diagnostic.
    /// </summary>
    public static class ApplicationServiceRegistry {
        internal static bool RunContractSelfTest() {
            int passed = 0;
            int failed = 0;
            string failure = null;

            Check(ApplicationNotificationRequest.TryCreate(
                    "title", "body",
                    ApplicationNotificationSeverity.Info, out _),
                "notification request shape", ref passed, ref failed,
                ref failure);
            Check(!ApplicationNotificationRequest.TryCreate(
                    "title",
                    Repeat('x', ApplicationNotificationRequest.MaxBodyLength + 1),
                    ApplicationNotificationSeverity.Info, out _),
                "notification body bound", ref passed, ref failed,
                ref failure);
            Check(ApplicationSettingValue.Boolean(true).IsValid &&
                  ApplicationSettingValue.Int32(4).IsValid &&
                  ApplicationSettingValue.String("session").IsValid,
                "setting value kinds", ref passed, ref failed,
                ref failure);
            Check(!ApplicationSettingValue.String(
                    Repeat('x', ApplicationSettingValue.MaxStringLength + 1)).IsValid,
                "setting string bound", ref passed, ref failed,
                ref failure);
            Check(SystemInformationSnapshot.IsBoundedText(
                    "guideXOS", SystemInformationSnapshot.MaxOsNameLength),
                "system snapshot text bound", ref passed, ref failed,
                ref failure);
            SystemInformationSnapshot snapshot = new SystemInformationSnapshot(
                1, 100, 50, 2, 25, "guideXOS", "Phase8", "x86_64");
            Check(snapshot.IsWithinBounds(), "system snapshot shape",
                ref passed, ref failed, ref failure);

            // Registry lookup/context assertions are deliberately not included
            // until Task 3 supplies the fixed table and lifecycle validator.
            return failed == 0;
        }

        private static string Repeat(char value, int count) {
            char[] chars = new char[count];
            for (int i = 0; i < chars.Length; i++) chars[i] = value;
            return new string(chars);
        }

        private static void Check(bool condition, string name, ref int passed,
                                  ref int failed, ref string failure) {
            if (condition) {
                passed++;
                return;
            }
            failed++;
            if (failure == null) failure = name;
        }
    }
}
