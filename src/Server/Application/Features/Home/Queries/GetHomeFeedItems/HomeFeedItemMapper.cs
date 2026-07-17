using K7.Server.Application.Common.Mappings;
using K7.Server.Application.Common.Services;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities;
using K7.Shared.Dtos.Home;

namespace K7.Server.Application.Features.Home.Queries.GetHomeFeedItems;

internal static class HomeFeedItemMapper
{
    public static HomeFeedItemDto MapTopLevelItem(
        BaseMedia item,
        bool detailed = false,
        IReadOnlyDictionary<Guid, IReadOnlyList<MetadataPictureSize>>? pictureSizes = null)
    {
        var pictures = item.Pictures?.Select(p => p.ToMetadataPictureDto(pictureSizes)).ToList();
        var userState = item.UserMediaStates.FirstOrDefault();

        return new HomeFeedItemDto
        {
            Id = item.Id,
            Title = item.Title ?? "",
            MediaType = item.Type,
            NavigationTarget = item switch
            {
                Movie => $"/movies/{item.Id}",
                Serie => $"/series/{item.Id}",
                MusicAlbum => $"/music/albums/{item.Id}",
                _ => $"/medias/{item.Id}"
            },
            Pictures = pictures,
            ReleaseDate = item.ReleaseDate,
            Watched = userState?.IsCompleted ?? false,
            Progress = userState?.ProgressPercentage ?? 0,
            GroupCount = 1,
            Overview = detailed ? GetOverview(item) : null,
            Genres = detailed && GetGenres(item).Count > 0 ? GetGenres(item) : null,
            ContentRating = detailed ? GetContentRating(item) : null,
            RuntimeMinutes = detailed ? GetRuntimeMinutes(item) : null,
            Rating = detailed ? GetBestRating(item) : null
        };
    }

    public static HomeFeedItemDto MapContinueWatchingItem(
        BaseMedia item,
        bool detailed = false,
        IReadOnlyDictionary<Guid, IReadOnlyList<MetadataPictureSize>>? pictureSizes = null)
    {
        var userState = item.UserMediaStates.FirstOrDefault();
        IList<MetadataPicture>? pictures;
        string navTarget;
        string title;
        string? additionalInfo = null;
        BaseMedia detailSource = item;

        switch (item)
        {
            case SerieEpisode episode:
                pictures = EpisodePictureResolver.ResolveDisplayPictures(episode);
                navTarget = $"/series/{episode.Serie?.Id ?? item.Id}/seasons/{episode.Season?.SeasonNumber ?? 0}#ep-{episode.EpisodeNumber}";
                title = episode.Serie?.Title ?? episode.Title ?? "";
                additionalInfo = $"S{episode.Season?.SeasonNumber ?? 0:D2}E{episode.EpisodeNumber:D2}";
                detailSource = episode.Serie ?? item;
                break;
            default:
                pictures = item.Pictures;
                navTarget = item switch
                {
                    Movie => $"/movies/{item.Id}",
                    _ => $"/medias/{item.Id}"
                };
                title = item.Title ?? "";
                break;
        }

        return new HomeFeedItemDto
        {
            Id = item.Id,
            Title = title,
            MediaType = item.Type,
            NavigationTarget = navTarget,
            Pictures = pictures?.Select(p => p.ToMetadataPictureDto(pictureSizes)).ToList(),
            AdditionalInfo = additionalInfo,
            ReleaseDate = item.ReleaseDate,
            Watched = userState?.IsCompleted ?? false,
            Progress = userState?.ProgressPercentage ?? 0,
            GroupCount = 1,
            Overview = detailed ? GetOverview(detailSource) : null,
            Genres = detailed && GetGenres(detailSource).Count > 0 ? GetGenres(detailSource) : null,
            ContentRating = detailed ? GetContentRating(detailSource) : null,
            RuntimeMinutes = detailed ? GetRuntimeMinutes(item) : null,
            Rating = detailed ? GetBestRating(detailSource) : null
        };
    }

    public static string? GetOverview(BaseMedia item) => item switch
    {
        Movie m => m.Tagline ?? m.Overview,
        Serie s => s.Overview,
        SerieEpisode e => e.Overview,
        MusicAlbum a => a.Overview,
        _ => null
    };

    public static List<string> GetGenres(BaseMedia media) =>
        media.MetadataTags
            .Where(mt => mt.MetadataTag.Kind == MetadataTagKind.Genre)
            .Select(mt => mt.MetadataTag.DisplayName)
            .ToList();

    public static string? GetContentRating(BaseMedia item) =>
        item.MetadataTags
            .FirstOrDefault(mt => mt.MetadataTag.Kind == MetadataTagKind.ContentRating)
            ?.MetadataTag.DisplayName;

    public static int? GetRuntimeMinutes(BaseMedia item) => item switch
    {
        SerieEpisode e => e.Runtime,
        _ => null
    };

    public static double? GetBestRating(BaseMedia item)
    {
        var rating = item.Ratings
            .OfType<K7.Server.Domain.Entities.Ratings.MetadataProviderRating>()
            .FirstOrDefault();
        if (rating is null || rating.MaximumValue == 0)
            return null;
        return Math.Round(rating.Value / rating.MaximumValue * 10, 1);
    }
}
