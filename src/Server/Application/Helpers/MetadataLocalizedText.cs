namespace K7.Server.Application.Helpers;

/// <summary>
/// Picks title/overview text for the library metadata language, using a fallback
/// language when the primary provider response is empty or still in the original script.
/// </summary>
public static class MetadataLocalizedText
{
    public static bool IsUsable(string? text, string? language) =>
        MetadataLanguageScript.IsCompatible(text, language);

    public static string? Prefer(string? primary, string? fallback, string? language)
    {
        if (IsUsable(primary, language))
            return primary;

        if (IsUsable(fallback, language))
            return fallback;

        if (!string.IsNullOrWhiteSpace(primary))
            return primary;

        return string.IsNullOrWhiteSpace(fallback) ? null : fallback;
    }

    public static bool ShouldFetchFallback(
        string? title,
        string? overview,
        string? language,
        string? fallbackLanguage)
    {
        if (!HasDistinctFallback(language, fallbackLanguage))
            return false;

        if (!IsUsable(title, language))
            return true;

        return !string.IsNullOrWhiteSpace(overview) && !IsUsable(overview, language);
    }

    public static bool HasDistinctFallback(string? language, string? fallbackLanguage) =>
        MetadataSearchLanguageHelper.ResolveSearchLanguages(language, fallbackLanguage).Count > 1;
}
