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
        foreach (var tvdbLanguage in BuildLanguagePriority(language, fallbackLanguage, originalLanguage))
        {
            var translation = await client.GetSeriesTranslationAsync(seriesId, tvdbLanguage, cancellationToken);
            if (!string.IsNullOrWhiteSpace(translation?.Name))
                return (translation.Name, translation.Overview ?? baseOverview);
        }

        return (baseTitle ?? string.Empty, baseOverview);
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
        foreach (var tvdbLanguage in BuildLanguagePriority(language, fallbackLanguage, originalLanguage))
        {
            var translation = await client.GetSeasonTranslationAsync(seasonId, tvdbLanguage, cancellationToken);
            if (!string.IsNullOrWhiteSpace(translation?.Name))
                return (translation.Name, translation.Overview ?? baseOverview);
        }

        return (baseTitle ?? string.Empty, baseOverview);
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
        foreach (var tvdbLanguage in BuildLanguagePriority(language, fallbackLanguage, originalLanguage))
        {
            var translation = await client.GetEpisodeTranslationAsync(episodeId, tvdbLanguage, cancellationToken);
            if (!string.IsNullOrWhiteSpace(translation?.Name))
                return (translation.Name, translation.Overview ?? baseOverview);
        }

        return (baseTitle ?? string.Empty, baseOverview);
    }

    internal static IReadOnlyList<string> BuildLanguagePriority(
        string language,
        string? fallbackLanguage,
        string? originalLanguage)
    {
        var languages = new List<string>();
        AddIfNew(languages, TvdbLanguageHelper.ToTvdbLanguage(language));

        if (!string.IsNullOrWhiteSpace(fallbackLanguage))
            AddIfNew(languages, TvdbLanguageHelper.ToTvdbLanguage(fallbackLanguage));

        if (!string.IsNullOrWhiteSpace(originalLanguage))
            AddIfNew(languages, TvdbLanguageHelper.ToTvdbLanguage(originalLanguage));

        return languages;
    }

    private static void AddIfNew(List<string> languages, string tvdbLanguage)
    {
        if (languages.Any(l => string.Equals(l, tvdbLanguage, StringComparison.OrdinalIgnoreCase)))
            return;

        languages.Add(tvdbLanguage);
    }
}
