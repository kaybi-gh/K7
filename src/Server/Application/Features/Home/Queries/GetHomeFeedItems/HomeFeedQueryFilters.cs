using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.QueryExtensions;
using K7.Server.Application.Common.Services;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities;

namespace K7.Server.Application.Features.Home.Queries.GetHomeFeedItems;

internal static class HomeFeedQueryFilters
{
    public static IQueryable<BaseMedia> ApplyFamilyFilter(IQueryable<BaseMedia> query, HashSet<MediaType>? mediaTypes)
    {
        if (mediaTypes is not { Count: > 0 })
            return query;

        // MediaTypes acts as a family selector:
        // Serie -> include Serie, SerieSeason, SerieEpisode
        // MusicAlbum -> include MusicAlbum, MusicTrack
        // Movie -> include Movie
        var includeMovies = mediaTypes.Contains(MediaType.Movie);
        var includeSeries = mediaTypes.Contains(MediaType.Serie)
                            || mediaTypes.Contains(MediaType.SerieEpisode)
                            || mediaTypes.Contains(MediaType.SerieSeason);
        var includeMusic = mediaTypes.Contains(MediaType.MusicAlbum)
                           || mediaTypes.Contains(MediaType.MusicTrack);

        return query.Where(x =>
            (includeMovies && x is Movie) ||
            (includeSeries && (x is Serie || x is SerieSeason || x is SerieEpisode)) ||
            (includeMusic && (x is MusicAlbum || x is MusicTrack)));
    }

    public static IQueryable<BaseMedia> ApplyLibraryFilter(
        IApplicationDbContext context, IQueryable<BaseMedia> query, Guid[]? libraryIds) =>
        query.WhereAvailableInLibraries(context, libraryIds ?? []);

    public static Task<IQueryable<BaseMedia>> ApplyUserExclusionsAsync(
        MediaAccessFilter mediaAccessFilter, IQueryable<BaseMedia> query, Guid userId, CancellationToken cancellationToken) =>
        mediaAccessFilter.ApplyAllAsync(query, userId, cancellationToken);

    public static async Task<IReadOnlyDictionary<Guid, IReadOnlyList<MetadataPictureSize>>> GetPictureSizesAsync(
        IApplicationDbContext context, IEnumerable<BaseMedia> medias, CancellationToken cancellationToken) =>
        await MetadataPictureSizesHelper.GetAvailableSizesByPictureIdsAsync(
            context,
            MetadataPictureSizesHelper.ExtractPictureIdsFromMedias(medias),
            cancellationToken);
}
