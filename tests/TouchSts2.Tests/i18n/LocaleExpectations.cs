using TouchSts2.Localization;

internal static class LocaleExpectations
{
    internal static void Verify(Action<string, string, string> equal)
    {
        equal("触屏模式", TouchText.Get("zhs", "TouchscreenMode"), "simplified Chinese");
        equal("觸控模式", TouchText.Get("zht", "TouchscreenMode"), "traditional Chinese");
        equal("触屏模式", TouchText.Get("ZHS", "TouchscreenMode"), "case insensitive locale");
    }
}
