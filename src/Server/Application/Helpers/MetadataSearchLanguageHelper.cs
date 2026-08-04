namespace K7.Server.Application.Helpers;

/// <summary>
/// Builds the ordered list of metadata search languages from library settings.
/// </summary>
public static class MetadataSearchLanguageHelper
{
    /// <summary>
    /// Returns distinct languages to query, primary first then fallback.
    /// Empty when neither is set (provider default applies).
    /// </summary>
    public static IReadOnlyList<string> ResolveSearchLanguages(string? language, string? fallbackLanguage)
    {
        var languages = new List<string>(2);
        TryAdd(languages, language);
        TryAdd(languages, fallbackLanguage);
        return languages;
    }

    private static void TryAdd(List<string> languages, string? language)
    {
        if (string.IsNullOrWhiteSpace(language))
            return;

        var trimmed = language.Trim();
        foreach (var existing in languages)
        {
            if (SameLanguage(existing, trimmed))
                return;
        }

        languages.Add(trimmed);
    }

    private static bool SameLanguage(string left, string right)
    {
        var leftKey = LanguageKey(left);
        var rightKey = LanguageKey(right);
        return string.Equals(leftKey, rightKey, StringComparison.OrdinalIgnoreCase);
    }

    private static string LanguageKey(string language)
        => language.Length >= 2 ? language[..2] : language;
}
