using K7.Shared;

namespace K7.Server.Application.Helpers;

/// <summary>
/// Detects whether metadata text is in a script compatible with the library language.
/// Latin-script languages (fr, en, ...) should not keep CJK/Cyrillic/Arabic titles
/// when a localized or fallback translation exists.
/// </summary>
public static class MetadataLanguageScript
{
    public static bool IsCompatible(string? text, string? language)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        return UsesLatinMetadata(language) == IsMostlyLatin(text);
    }

    public static bool UsesLatinMetadata(string? language)
    {
        var key = LanguageKey(language);
        return key is not ("ja" or "zh" or "ko" or "ru" or "uk" or "bg" or "ar" or "he" or "el" or "th");
    }

    public static bool IsMostlyLatin(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var letters = 0;
        var nonLatin = 0;
        foreach (var c in value)
        {
            if (!char.IsLetter(c))
                continue;

            letters++;
            // Beyond Latin Extended-B: CJK, Cyrillic, Arabic, etc.
            if (c > 0x024F)
                nonLatin++;
        }

        return letters > 0 && nonLatin * 2 < letters;
    }

    public static string? LanguageKey(string? language)
    {
        if (string.IsNullOrWhiteSpace(language))
            return null;

        var normalized = LanguageNormalizer.NormalizeToIso6391(language);
        if (normalized is not null)
            return normalized;

        var trimmed = language.Trim();
        return trimmed.Length >= 2 ? trimmed[..2].ToLowerInvariant() : trimmed.ToLowerInvariant();
    }
}
