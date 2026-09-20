using System.Text.Json;

namespace TouchSts2.Localization;

internal static class TouchText
{
    private static readonly Dictionary<string, Dictionary<string, string>> Strings = Load();
    internal static IEnumerable<string> Languages => Strings.Keys;

    private static Dictionary<string, Dictionary<string, string>> Load()
    {
        using var stream = typeof(TouchText).Assembly.GetManifestResourceStream("TouchSts2.Localization.strings.json")
            ?? throw new InvalidOperationException("Missing embedded touch localization.");
        return JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(stream)!;
    }

    internal static string Get(string? language, string key)
    {
        if (language != null && Strings.TryGetValue(language.ToLowerInvariant(), out var table) &&
            table.TryGetValue(key, out var value)) return value;
        return Strings["eng"][key];
    }
}
