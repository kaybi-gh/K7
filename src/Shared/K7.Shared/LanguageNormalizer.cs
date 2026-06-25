namespace K7.Shared;

/// <summary>
/// Normalizes various language representations (ISO 639-2/B, ISO 639-2/T, full names,
/// common abbreviations like VFF/VFQ) to ISO 639-1 two-letter codes.
/// </summary>
public static class LanguageNormalizer
{
    /// <summary>
    /// Attempts to normalize a language string to its ISO 639-1 code.
    /// Returns null if the input cannot be recognized.
    /// </summary>
    public static string? NormalizeToIso6391(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return null;

        var trimmed = input.Trim();

        if (Aliases.TryGetValue(trimmed, out var code))
            return code;

        // If it's already a 2-letter code we recognize, return it as-is
        if (trimmed.Length == 2)
        {
            var lower = trimmed.ToLowerInvariant();
            if (Iso6391Codes.Contains(lower))
                return lower;
        }

        return null;
    }

    /// <summary>
    /// Normalizes a language string, falling back to the original input if unrecognized.
    /// </summary>
    public static string NormalizeOrPassthrough(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return "und";

        return NormalizeToIso6391(input) ?? input.Trim().ToLowerInvariant();
    }

    /// <summary>
    /// Returns true when a normalized language code represents an unknown or missing language.
    /// </summary>
    public static bool IsUndetermined(string? language) =>
        string.IsNullOrWhiteSpace(language)
        || language.Equals("und", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Attempts to infer an ISO 639-1 code from a track title (e.g. "Français Complets (Colors)").
    /// </summary>
    public static string? InferFromTrackTitle(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return null;

        var trimmedTitle = title.Trim();
        var prefix = TrimTitleForLanguageHint(trimmedTitle);

        if (TryNormalizeTitleWords(prefix) is { } prefixLanguage)
            return prefixLanguage;

        if (!string.Equals(prefix, trimmedTitle, StringComparison.Ordinal)
            && TryNormalizeTitleWords(trimmedTitle) is { } fullTitleLanguage)
        {
            return fullTitleLanguage;
        }

        return FindLanguageAliasInText(trimmedTitle);
    }

    /// <summary>
    /// Normalizes a container language tag, inferring from the track title when missing.
    /// </summary>
    public static string ResolveSubtitleLanguage(string? containerLanguage, string? trackTitle)
    {
        var language = NormalizeOrPassthrough(containerLanguage);
        if (IsUndetermined(language) && InferFromTrackTitle(trackTitle) is { } inferred)
            return inferred;

        return language;
    }

    private static string TrimTitleForLanguageHint(string title)
    {
        var end = title.Length;
        foreach (var separator in TitleHintSeparators)
        {
            var idx = title.IndexOf(separator, StringComparison.Ordinal);
            if (idx >= 0 && idx < end)
                end = idx;
        }

        var dashIdx = title.IndexOf(" - ", StringComparison.Ordinal);
        if (dashIdx >= 0 && dashIdx < end)
            end = dashIdx;

        return title[..end].Trim();
    }

    private static string? TryNormalizeTitleWords(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return null;

        var words = title.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var maxWords = Math.Min(words.Length, 3);

        for (var wordCount = maxWords; wordCount >= 1; wordCount--)
        {
            var phrase = string.Join(' ', words.AsSpan(0, wordCount).ToArray());
            var code = NormalizeToIso6391(phrase);
            if (code is not null && !IsUndetermined(code))
                return code;
        }

        return null;
    }

    private static string? FindLanguageAliasInText(string text)
    {
        foreach (var (alias, code) in TitleScanAliases)
        {
            if (IsUndetermined(code))
                continue;

            if (ContainsWholeWord(text, alias))
                return code;
        }

        return null;
    }

    private static bool ContainsWholeWord(string text, string word)
    {
        if (string.IsNullOrEmpty(word))
            return false;

        var index = 0;
        while ((index = text.IndexOf(word, index, StringComparison.OrdinalIgnoreCase)) >= 0)
        {
            var beforeOk = index == 0 || !char.IsLetterOrDigit(text[index - 1]);
            var end = index + word.Length;
            var afterOk = end >= text.Length || !char.IsLetterOrDigit(text[end]);
            if (beforeOk && afterOk)
                return true;

            index++;
        }

        return false;
    }

    private static readonly char[] TitleHintSeparators = ['(', '[', '|'];

    private static readonly HashSet<string> Iso6391Codes = new(StringComparer.OrdinalIgnoreCase)
    {
        "fr", "en", "de", "es", "it", "ja", "ko", "pt", "ru", "zh",
        "ar", "nl", "pl", "sv", "da", "fi", "no", "tr", "uk", "hi",
        "th", "vi", "el", "he", "cs", "hu", "ro", "id", "nb", "nn",
        "ca", "hr", "sr", "sk", "sl", "bg", "et", "lv", "lt", "ms",
        "ta", "te", "ml", "ka", "am", "sw", "zu", "af", "sq", "hy",
        "az", "eu", "be", "bn", "bs", "my", "km", "gl", "gu", "ha",
        "is", "ga", "kn", "kk", "lo", "mk", "mn", "ne", "fa", "pa",
        "si", "so", "tl", "ur", "uz", "cy"
    };

    private static readonly Dictionary<string, string> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        // French
        ["fr"] = "fr",
        ["fra"] = "fr",
        ["fre"] = "fr",
        ["french"] = "fr",
        ["francais"] = "fr",
        ["français"] = "fr",
        ["vff"] = "fr",
        ["vf"] = "fr",
        ["vfq"] = "fr",
        ["vfi"] = "fr",

        // English
        ["en"] = "en",
        ["eng"] = "en",
        ["english"] = "en",
        ["anglais"] = "en",

        // German
        ["de"] = "de",
        ["deu"] = "de",
        ["ger"] = "de",
        ["german"] = "de",
        ["deutsch"] = "de",
        ["allemand"] = "de",

        // Spanish
        ["es"] = "es",
        ["spa"] = "es",
        ["spanish"] = "es",
        ["espanol"] = "es",
        ["espagnol"] = "es",
        ["castellano"] = "es",
        ["lat"] = "es", // "latino" often means Latin American Spanish

        // Italian
        ["it"] = "it",
        ["ita"] = "it",
        ["italian"] = "it",
        ["italiano"] = "it",
        ["italien"] = "it",

        // Japanese
        ["ja"] = "ja",
        ["jpn"] = "ja",
        ["japanese"] = "ja",
        ["japonais"] = "ja",

        // Korean
        ["ko"] = "ko",
        ["kor"] = "ko",
        ["korean"] = "ko",
        ["coreen"] = "ko",

        // Portuguese
        ["pt"] = "pt",
        ["por"] = "pt",
        ["portuguese"] = "pt",
        ["portugais"] = "pt",
        ["portugues"] = "pt",

        // Russian
        ["ru"] = "ru",
        ["rus"] = "ru",
        ["russian"] = "ru",
        ["russe"] = "ru",

        // Chinese
        ["zh"] = "zh",
        ["zho"] = "zh",
        ["chi"] = "zh",
        ["chinese"] = "zh",
        ["chinois"] = "zh",
        ["cmn"] = "zh", // Mandarin

        // Arabic
        ["ar"] = "ar",
        ["ara"] = "ar",
        ["arabic"] = "ar",
        ["arabe"] = "ar",

        // Dutch
        ["nl"] = "nl",
        ["nld"] = "nl",
        ["dut"] = "nl",
        ["dutch"] = "nl",
        ["neerlandais"] = "nl",
        ["nederlands"] = "nl",
        ["flemish"] = "nl",

        // Polish
        ["pl"] = "pl",
        ["pol"] = "pl",
        ["polish"] = "pl",
        ["polonais"] = "pl",

        // Swedish
        ["sv"] = "sv",
        ["swe"] = "sv",
        ["swedish"] = "sv",
        ["suedois"] = "sv",

        // Danish
        ["da"] = "da",
        ["dan"] = "da",
        ["danish"] = "da",
        ["danois"] = "da",

        // Finnish
        ["fi"] = "fi",
        ["fin"] = "fi",
        ["finnish"] = "fi",
        ["finnois"] = "fi",

        // Norwegian
        ["no"] = "no",
        ["nor"] = "no",
        ["nob"] = "no",
        ["nno"] = "no",
        ["norwegian"] = "no",
        ["norvegien"] = "no",
        ["nb"] = "no",
        ["nn"] = "no",

        // Turkish
        ["tr"] = "tr",
        ["tur"] = "tr",
        ["turkish"] = "tr",
        ["turc"] = "tr",

        // Ukrainian
        ["uk"] = "uk",
        ["ukr"] = "uk",
        ["ukrainian"] = "uk",
        ["ukrainien"] = "uk",

        // Hindi
        ["hi"] = "hi",
        ["hin"] = "hi",
        ["hindi"] = "hi",

        // Thai
        ["th"] = "th",
        ["tha"] = "th",
        ["thai"] = "th",

        // Vietnamese
        ["vi"] = "vi",
        ["vie"] = "vi",
        ["vietnamese"] = "vi",
        ["vietnamien"] = "vi",

        // Greek
        ["el"] = "el",
        ["ell"] = "el",
        ["gre"] = "el",
        ["greek"] = "el",
        ["grec"] = "el",

        // Hebrew
        ["he"] = "he",
        ["heb"] = "he",
        ["hebrew"] = "he",
        ["hebreu"] = "he",

        // Czech
        ["cs"] = "cs",
        ["ces"] = "cs",
        ["cze"] = "cs",
        ["czech"] = "cs",
        ["tcheque"] = "cs",

        // Hungarian
        ["hu"] = "hu",
        ["hun"] = "hu",
        ["hungarian"] = "hu",
        ["hongrois"] = "hu",

        // Romanian
        ["ro"] = "ro",
        ["ron"] = "ro",
        ["rum"] = "ro",
        ["romanian"] = "ro",
        ["roumain"] = "ro",

        // Indonesian
        ["id"] = "id",
        ["ind"] = "id",
        ["indonesian"] = "id",
        ["indonesien"] = "id",

        // Croatian
        ["hr"] = "hr",
        ["hrv"] = "hr",
        ["croatian"] = "hr",
        ["croate"] = "hr",

        // Serbian
        ["sr"] = "sr",
        ["srp"] = "sr",
        ["serbian"] = "sr",
        ["serbe"] = "sr",

        // Slovak
        ["sk"] = "sk",
        ["slk"] = "sk",
        ["slo"] = "sk",
        ["slovak"] = "sk",
        ["slovaque"] = "sk",

        // Slovenian
        ["sl"] = "sl",
        ["slv"] = "sl",
        ["slovenian"] = "sl",
        ["slovene"] = "sl",

        // Bulgarian
        ["bg"] = "bg",
        ["bul"] = "bg",
        ["bulgarian"] = "bg",
        ["bulgare"] = "bg",

        // Estonian
        ["et"] = "et",
        ["est"] = "et",
        ["estonian"] = "et",
        ["estonien"] = "et",

        // Latvian
        ["lv"] = "lv",
        ["lav"] = "lv",
        ["latvian"] = "lv",
        ["letton"] = "lv",

        // Lithuanian
        ["lt"] = "lt",
        ["lit"] = "lt",
        ["lithuanian"] = "lt",
        ["lituanien"] = "lt",

        // Catalan
        ["ca"] = "ca",
        ["cat"] = "ca",
        ["catalan"] = "ca",

        // Persian / Farsi
        ["fa"] = "fa",
        ["fas"] = "fa",
        ["per"] = "fa",
        ["persian"] = "fa",
        ["farsi"] = "fa",
        ["persan"] = "fa",

        // Bengali
        ["bn"] = "bn",
        ["ben"] = "bn",
        ["bengali"] = "bn",

        // Malay
        ["ms"] = "ms",
        ["msa"] = "ms",
        ["may"] = "ms",
        ["malay"] = "ms",
        ["malais"] = "ms",

        // Tamil
        ["ta"] = "ta",
        ["tam"] = "ta",
        ["tamil"] = "ta",
        ["tamoul"] = "ta",

        // Telugu
        ["te"] = "te",
        ["tel"] = "te",
        ["telugu"] = "te",

        // Undetermined
        ["und"] = "und",
        ["undetermined"] = "und",
        ["unknown"] = "und"
    };

    private static readonly (string Alias, string Code)[] TitleScanAliases =
        Aliases
            .Where(entry => !IsUndetermined(entry.Value))
            .OrderByDescending(entry => entry.Key.Length)
            .Select(entry => (entry.Key, entry.Value))
            .ToArray();
}
