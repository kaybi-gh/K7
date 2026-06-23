using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Ratings;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Requests;

namespace K7.Server.Application.Features.Medias.Queries.Common;

public static class MediaOrderingHelper
{
    public static async Task<HashSet<MetadataProvider>> ResolveApplicableProvidersAsync(
        IApplicationDbContext context,
        Guid[]? libraryIds,
        CancellationToken cancellationToken)
    {
        var query = context.Libraries.AsNoTracking().AsQueryable();

        if (libraryIds is { Length: > 0 })
            query = query.Where(l => libraryIds.Contains(l.Id));

        var providerNames = await query
            .Select(l => l.MetadataProviderName)
            .Distinct()
            .ToListAsync(cancellationToken);

        var providers = new HashSet<MetadataProvider>();
        foreach (var name in providerNames)
        {
            if (TryParseMetadataProvider(name) is { } provider)
                providers.Add(provider);
        }

        return providers;
    }

    public static MetadataProvider? TryParseMetadataProvider(string? providerName)
    {
        if (string.IsNullOrWhiteSpace(providerName))
            return null;

        if (providerName.Equals("TMDb", StringComparison.OrdinalIgnoreCase)
            || providerName.Equals("tmdb", StringComparison.OrdinalIgnoreCase))
            return MetadataProvider.TMDb;

        return Enum.TryParse<MetadataProvider>(providerName, ignoreCase: true, out var parsed)
            ? parsed
            : null;
    }

    public static IOrderedQueryable<BaseMedia> ApplyTrendingOrder(
        IQueryable<BaseMedia> queryable,
        IOrderedQueryable<BaseMedia>? existing,
        bool descending,
        HashSet<MetadataProvider> providers,
        DateTime recentCutoff,
        IQueryable<MetadataProviderRating> providerRatings)
    {
        var playCount = descending
            ? (existing is null
                ? queryable.OrderByDescending(x => x is Serie
                    ? ((Serie)x).Seasons.SelectMany(s => s.Episodes).SelectMany(e => e.UserMediaStates).Sum(s => s.PlayCount)
                    : x is MusicAlbum
                        ? ((MusicAlbum)x).Tracks.SelectMany(t => t.UserMediaStates).Sum(s => s.PlayCount)
                        : x.UserMediaStates.Sum(s => s.PlayCount))
                : existing.ThenByDescending(x => x is Serie
                    ? ((Serie)x).Seasons.SelectMany(s => s.Episodes).SelectMany(e => e.UserMediaStates).Sum(s => s.PlayCount)
                    : x is MusicAlbum
                        ? ((MusicAlbum)x).Tracks.SelectMany(t => t.UserMediaStates).Sum(s => s.PlayCount)
                        : x.UserMediaStates.Sum(s => s.PlayCount)))
            : (existing is null
                ? queryable.OrderBy(x => x is Serie
                    ? ((Serie)x).Seasons.SelectMany(s => s.Episodes).SelectMany(e => e.UserMediaStates).Sum(s => s.PlayCount)
                    : x is MusicAlbum
                        ? ((MusicAlbum)x).Tracks.SelectMany(t => t.UserMediaStates).Sum(s => s.PlayCount)
                        : x.UserMediaStates.Sum(s => s.PlayCount))
                : existing.ThenBy(x => x is Serie
                    ? ((Serie)x).Seasons.SelectMany(s => s.Episodes).SelectMany(e => e.UserMediaStates).Sum(s => s.PlayCount)
                    : x is MusicAlbum
                        ? ((MusicAlbum)x).Tracks.SelectMany(t => t.UserMediaStates).Sum(s => s.PlayCount)
                        : x.UserMediaStates.Sum(s => s.PlayCount)));

        var recent = descending
            ? playCount.ThenByDescending(x => x is Serie
                ? ((Serie)x).Seasons.SelectMany(s => s.Episodes).SelectMany(e => e.UserMediaStates)
                    .Where(s => s.LastInteractedAt >= recentCutoff).Max(s => (DateTime?)s.LastInteractedAt) ?? DateTime.MinValue
                : x is MusicAlbum
                    ? ((MusicAlbum)x).Tracks.SelectMany(t => t.UserMediaStates)
                        .Where(s => s.LastInteractedAt >= recentCutoff).Max(s => (DateTime?)s.LastInteractedAt) ?? DateTime.MinValue
                    : x.UserMediaStates.Where(s => s.LastInteractedAt >= recentCutoff).Max(s => (DateTime?)s.LastInteractedAt) ?? DateTime.MinValue)
            : playCount.ThenBy(x => x is Serie
                ? ((Serie)x).Seasons.SelectMany(s => s.Episodes).SelectMany(e => e.UserMediaStates)
                    .Where(s => s.LastInteractedAt >= recentCutoff).Max(s => (DateTime?)s.LastInteractedAt) ?? DateTime.MaxValue
                : x is MusicAlbum
                    ? ((MusicAlbum)x).Tracks.SelectMany(t => t.UserMediaStates)
                        .Where(s => s.LastInteractedAt >= recentCutoff).Max(s => (DateTime?)s.LastInteractedAt) ?? DateTime.MaxValue
                    : x.UserMediaStates.Where(s => s.LastInteractedAt >= recentCutoff).Max(s => (DateTime?)s.LastInteractedAt) ?? DateTime.MaxValue);

        var providerRating = ApplyProviderRatingThenBy(recent, descending, providers, providerRatings);
        return descending
            ? providerRating.ThenByDescending(x => x.Id)
            : providerRating.ThenBy(x => x.Id);
    }

    public static IOrderedQueryable<BaseMedia> ApplyProviderRatingOrder(
        IQueryable<BaseMedia> queryable,
        IOrderedQueryable<BaseMedia>? existing,
        bool descending,
        HashSet<MetadataProvider> providers,
        IQueryable<MetadataProviderRating> providerRatings)
    {
        var ordered = ApplyProviderRatingPrimary(queryable, existing, descending, providers, providerRatings);
        return descending
            ? ordered.ThenByDescending(m => providerRatings
                .Where(r => r.MediaId == m.Id && providers.Contains(r.MetadataProvider))
                .Select(r => r.RatingCount ?? 0)
                .Max())
                .ThenByDescending(x => x.Id)
            : ordered.ThenBy(m => providerRatings
                .Where(r => r.MediaId == m.Id && providers.Contains(r.MetadataProvider))
                .Select(r => r.RatingCount ?? 0)
                .Max())
                .ThenBy(x => x.Id);
    }

    private static IOrderedQueryable<BaseMedia> ApplyProviderRatingPrimary(
        IQueryable<BaseMedia> queryable,
        IOrderedQueryable<BaseMedia>? existing,
        bool descending,
        HashSet<MetadataProvider> providers,
        IQueryable<MetadataProviderRating> providerRatings)
    {
        if (existing is null)
        {
            return descending
                ? queryable.OrderByDescending(m => providerRatings
                    .Where(r => r.MediaId == m.Id && providers.Contains(r.MetadataProvider))
                    .Select(r => r.MaximumValue > r.MinimumValue
                        ? (r.Value - r.MinimumValue) / (r.MaximumValue - r.MinimumValue)
                        : 0.0)
                    .Max())
                : queryable.OrderBy(m => providerRatings
                    .Where(r => r.MediaId == m.Id && providers.Contains(r.MetadataProvider))
                    .Select(r => r.MaximumValue > r.MinimumValue
                        ? (r.Value - r.MinimumValue) / (r.MaximumValue - r.MinimumValue)
                        : 0.0)
                    .Max());
        }

        return descending
            ? existing.ThenByDescending(m => providerRatings
                .Where(r => r.MediaId == m.Id && providers.Contains(r.MetadataProvider))
                .Select(r => r.MaximumValue > r.MinimumValue
                    ? (r.Value - r.MinimumValue) / (r.MaximumValue - r.MinimumValue)
                    : 0.0)
                .Max())
            : existing.ThenBy(m => providerRatings
                .Where(r => r.MediaId == m.Id && providers.Contains(r.MetadataProvider))
                .Select(r => r.MaximumValue > r.MinimumValue
                    ? (r.Value - r.MinimumValue) / (r.MaximumValue - r.MinimumValue)
                    : 0.0)
                .Max());
    }

    private static IOrderedQueryable<BaseMedia> ApplyProviderRatingThenBy(
        IOrderedQueryable<BaseMedia> existing,
        bool descending,
        HashSet<MetadataProvider> providers,
        IQueryable<MetadataProviderRating> providerRatings) =>
        descending
            ? existing.ThenByDescending(m => providerRatings
                .Where(r => r.MediaId == m.Id && providers.Contains(r.MetadataProvider))
                .Select(r => r.MaximumValue > r.MinimumValue
                    ? (r.Value - r.MinimumValue) / (r.MaximumValue - r.MinimumValue)
                    : 0.0)
                .Max())
            : existing.ThenBy(m => providerRatings
                .Where(r => r.MediaId == m.Id && providers.Contains(r.MetadataProvider))
                .Select(r => r.MaximumValue > r.MinimumValue
                    ? (r.Value - r.MinimumValue) / (r.MaximumValue - r.MinimumValue)
                    : 0.0)
                .Max());

    public static bool RequiresUserPlayCount(HashSet<MediaTagOrderingOption>? orderBy) =>
        orderBy is not null && orderBy.Any(o => o is MediaTagOrderingOption.UserPlayCountAsc or MediaTagOrderingOption.UserPlayCountDesc);
}
