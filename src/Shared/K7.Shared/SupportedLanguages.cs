namespace K7.Shared;

public static class SupportedLanguages
{
    /// <summary>
    /// Languages available for the application UI (must have translations).
    /// </summary>
    public static IReadOnlyList<LanguageOption> Interface { get; } =
    [
        new("fr", "Fran\u00e7ais", "fr"),
        new("en", "English", "gb")
    ];

    /// <summary>
    /// Languages available for metadata fetching (TMDb, MusicBrainz, etc.)
    /// and subtitle/audio track identification.
    /// </summary>
    public static IReadOnlyList<LanguageOption> Metadata { get; } =
    [
        new("fr", "Fran\u00e7ais", "fr"),
        new("en", "English", "gb"),
        new("de", "Deutsch", "de"),
        new("es", "Espa\u00f1ol", "es"),
        new("it", "Italiano", "it"),
        new("ja", "\u65E5\u672C\u8A9E", "jp"),
        new("ko", "\uD55C\uAD6D\uC5B4", "kr"),
        new("pt", "Portugu\u00eas", "br"),
        new("ru", "\u0420\u0443\u0441\u0441\u043A\u0438\u0439", "ru"),
        new("zh", "\u4E2D\u6587", "cn"),
        new("ar", "\u0627\u0644\u0639\u0631\u0628\u064A\u0629", "sa"),
        new("nl", "Nederlands", "nl"),
        new("pl", "Polski", "pl"),
        new("sv", "Svenska", "se"),
        new("da", "Dansk", "dk"),
        new("fi", "Suomi", "fi"),
        new("no", "Norsk", "no"),
        new("tr", "T\u00fcrk\u00e7e", "tr"),
        new("uk", "\u0423\u043A\u0440\u0430\u0457\u043D\u0441\u044C\u043A\u0430", "ua"),
        new("hi", "\u0939\u093F\u0928\u094D\u0926\u0940", "in"),
        new("th", "\u0E44\u0E17\u0E22", "th"),
        new("vi", "Ti\u1EBFng Vi\u1EC7t", "vn"),
        new("el", "\u0395\u03BB\u03BB\u03B7\u03BD\u03B9\u03BA\u03AC", "gr"),
        new("he", "\u05E2\u05D1\u05E8\u05D9\u05EA", "il"),
        new("cs", "\u010Ce\u0161tina", "cz"),
        new("hu", "Magyar", "hu"),
        new("ro", "Rom\u00E2n\u0103", "ro"),
        new("id", "Bahasa Indonesia", "id")
    ];

    /// <summary>
    /// Attempts to find a language option by its ISO 639-1 code from the Metadata list.
    /// Returns null if not found.
    /// </summary>
    public static LanguageOption? FindByCode(string code)
    {
        for (var i = 0; i < Metadata.Count; i++)
        {
            if (string.Equals(Metadata[i].Code, code, StringComparison.OrdinalIgnoreCase))
                return Metadata[i];
        }

        return null;
    }

    /// <summary>
    /// Returns the native label for a language code, or the raw code if not found.
    /// </summary>
    public static string GetDisplayLabel(string code)
    {
        var option = FindByCode(code);
        return option?.NativeLabel ?? code;
    }
}

public sealed record LanguageOption(string Code, string NativeLabel, string CountryCode);