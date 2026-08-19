using K7.Server.Application.Helpers;

namespace K7.Server.Infrastructure.MediaProcessing.MetadataProvider.Tvdb;

internal static class TvdbTranslationResolver
{
    internal static async Task<(string Title, string? Overview)> ResolveSeriesTextAsync(
        TvdbApiClient client,
        int seriesId,
        string? baseTitle,
        string? baseOverview,
        string? originalLanguage,
        string language,
        string? fallbackLanguage,
        CancellationToken cancellationToken)
    {
        _ = originalLanguage;
        return await ResolveAsync(
            lang => client.GetSeriesTranslationAsync(seriesId, lang, cancellationToken),
            baseTitle,
            baseOverview,
            language,
            fallbackLanguage);
    }

    internal static async Task<(string Title, string? Overview)> ResolveSeasonTextAsync(
        TvdbApiClient client,
        int seasonId,
        string? baseTitle,
        string? baseOverview,
        string? originalLanguage,
        string language,
        string? fallbackLanguage,
        CancellationToken cancellationToken)
    {
        _ = originalLanguage;
        return await ResolveAsync(
            lang => client.GetSeasonTranslationAsync(seasonId, lang, cancellationToken),
            baseTitle,
            baseOverview,
            language,
            fallbackLanguage);
    }

    internal static async Task<(string Title, string? Overview)> ResolveEpisodeTextAsync(
        TvdbApiClient client,
        int episodeId,
        string? baseTitle,
        string? baseOverview,
        string? originalLanguage,
        string language,
        string? fallbackLanguage,
        CancellationToken cancellationToken)
    {
        _ = originalLanguage;
        return await ResolveAsync(
            lang => client.GetEpisodeTranslationAsync(episodeId, lang, cancellationToken),
            baseTitle,
            baseOverview,
            language,
            fallbackLanguage);
    }

    internal static IReadOnlyList<string> BuildLanguagePriority(
        string language,
        string? fallbackLanguage,
        string? originalLanguage = null)
    {
        _ = originalLanguage;
        var languages = new List<string>();
        AddIfNew(languages, TvdbLanguageHelper.ToTvdbLanguage(language));

        if (!string.IsNullOrWhiteSpace(fallbackLanguage))
            AddIfNew(languages, TvdbLanguageHelper.ToTvdbLanguage(fallbackLanguage));

        return languages;
    }

    internal static (string Title, string? Overview) PickTranslatedText(
        string? baseTitle,
        string? baseOverview,
        string language,
        IEnumerable<(string? Name, string? Overview)> translations)
    {
        string? title = null;
        string? overview = null;

        foreach (var translation in translations)
        {
            if (title is null && MetadataLocalizedText.IsUsable(translation.Name, language))
                title = translation.Name;
            if (overview is null && MetadataLocalizedText.IsUsable(translation.Overview, language))
                overview = translation.Overview;
            if (title is not null && overview is not null)
                break;
        }

        title = MetadataLocalizedText.Prefer(title, baseTitle, language) ?? baseTitle ?? string.Empty;
        overview = MetadataLocalizedText.Prefer(overview, baseOverview, language) ?? baseOverview;
        return (title, overview);
    }

    private static async Task<(string Title, string? Overview)> ResolveAsync(
        Func<string, Task<TvdbTranslation?>> fetchTranslation,
        string? baseTitle,
        string? baseOverview,
        string language,
        string? fallbackLanguage)
    {
        var translations = new List<(string? Name, string? Overview)>();
        foreach (var tvdbLanguage in BuildLanguagePriority(language, fallbackLanguage))
        {
            var translation = await fetchTranslation(tvdbLanguage);
            if (translation is null)
                continue;

            translations.Add((translation.Name, translation.Overview));
            if (MetadataLocalizedText.IsUsable(translation.Name, language)
                && (string.IsNullOrWhiteSpace(translation.Overview)
                    || MetadataLocalizedText.IsUsable(translation.Overview, language)))
            {
                break;
            }
        }

        return PickTranslatedText(baseTitle, baseOverview, language, translations);
    }

    private static void AddIfNew(List<string> languages, string tvdbLanguage)
    {
        if (languages.Any(l => string.Equals(l, tvdbLanguage, StringComparison.OrdinalIgnoreCase)))
            return;

        languages.Add(tvdbLanguage);
    }
}
