namespace K7.Server.Infrastructure.MediaProcessing.MetadataProvider.Tvdb;

internal static class TvdbLanguageHelper
{
    private static readonly Dictionary<string, string> Iso6391ToTvdb = new(StringComparer.OrdinalIgnoreCase)
    {
        ["en"] = "eng",
        ["fr"] = "fra",
        ["de"] = "deu",
        ["es"] = "spa",
        ["it"] = "ita",
        ["pt"] = "por",
        ["nl"] = "nld",
        ["pl"] = "pol",
        ["ru"] = "rus",
        ["ja"] = "jpn",
        ["ko"] = "kor",
        ["zh"] = "zho",
        ["sv"] = "swe",
        ["da"] = "dan",
        ["no"] = "nor",
        ["fi"] = "fin",
        ["cs"] = "ces",
        ["hu"] = "hun",
        ["tr"] = "tur",
        ["ar"] = "ara",
    };

    public static string ToTvdbLanguage(string language)
    {
        if (string.IsNullOrWhiteSpace(language))
            return "eng";

        var normalized = language.Trim();
        if (normalized.Length >= 3 && normalized.Length <= 3)
            return normalized.ToLowerInvariant();

        var twoLetter = normalized.Length >= 2 ? normalized[..2] : normalized;
        return Iso6391ToTvdb.TryGetValue(twoLetter, out var tvdb) ? tvdb : "eng";
    }
}
