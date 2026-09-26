using GuideXos;

namespace GuideXos.Phase30ManagedClipboardProof;

internal static class Program
{
    private const string MainText = "Phase 30 managed clipboard";
    private const string CrossProcessText = "Phase 30 cross-process clipboard";
    private const string OverwriteText = "Phase 30 overwrite value 2";

    internal static int Main()
    {
#if GUIDEXOS_PHASE30_FAILFAST
        GuideXosResult failFastWrite = GuideXosClipboard.TrySetText(
            CrossProcessText);
        if (failFastWrite.Failed)
            return 31;
        GuideXosProcess.FailFast(0x30);
        return 32;
#elif GUIDEXOS_PHASE30_READER
        return ReadExpected(CrossProcessText) ? 30 : 31;
#elif GUIDEXOS_PHASE30_OVERWRITE
        GuideXosResult overwrite = GuideXosClipboard.TrySetText(OverwriteText);
        if (overwrite.Failed)
            return 31;
        return ReadExpected(OverwriteText) ? 30 : 32;
#elif GUIDEXOS_PHASE30_EMPTY
        GuideXosResult emptyWrite = GuideXosClipboard.TrySetText(string.Empty);
        if (emptyWrite.Failed ||
            GuideXosClipboard.TryGetText(out GuideXosClipboardText empty).Failed ||
            !empty.HasValue || empty.Text.Length != 0 ||
            empty.SourceApplicationId.Length == 0)
            return 31;
        return 30;
#elif GUIDEXOS_PHASE30_CLEAR
        GuideXosResult clearWrite = GuideXosClipboard.TrySetText(
            "Phase 30 clear value");
        if (clearWrite.Failed || GuideXosClipboard.TryClear().Failed ||
            GuideXosClipboard.TryGetText(out GuideXosClipboardText cleared).Failed ||
            cleared.HasValue || cleared.Text.Length != 0 ||
            cleared.SourceApplicationId.Length != 0)
            return 31;
        return 30;
#elif GUIDEXOS_PHASE30_OVERSIZE
        const string baseline = "Phase 30 oversize baseline";
        if (GuideXosClipboard.TrySetText(baseline).Failed)
            return 31;
        GuideXosResult oversize = GuideXosClipboard.TrySetText(
            new string('x', GuideXosClipboard.MaxTextLength + 1));
        if (oversize.Status != GuideXosStatus.InvalidArgument ||
            !ReadExpected(baseline))
            return 32;
        return 30;
#elif GUIDEXOS_PHASE30_MALFORMED_LENGTH
        GuideXosResult malformed = GuideXosClipboard.TrySetText(
            "Phase 30 malformed length");
        if (malformed.Status != GuideXosStatus.InvalidArgument ||
            !ReadExpected(CrossProcessText))
            return 31;
        return 30;
#elif GUIDEXOS_PHASE30_CROSS_WRITER
        GuideXosResult writer = GuideXosClipboard.TrySetText(CrossProcessText);
        return writer.Succeeded ? 30 : 31;
#else
        GuideXosResult set = GuideXosClipboard.TrySetText(MainText);
        if (set.Failed || !ReadExpected(MainText))
            return 31;
        return 30;
#endif
    }

    private static bool ReadExpected(string expected)
    {
        GuideXosResult result = GuideXosClipboard.TryGetText(
            out GuideXosClipboardText clipboard);
        return result.Succeeded && clipboard.HasValue &&
               clipboard.Text == expected &&
               clipboard.SourceApplicationId.Length > 0 &&
               clipboard.Generation > 0;
    }
}
