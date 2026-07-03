using K7.Clients.Shared.Models;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities;
using K7.Shared.Dtos.Entities.Medias;
using K7.Shared.Dtos.Home;
using K7.Shared.Interfaces;

namespace K7.Clients.Shared.Mappings;

public static class LiteMediaMappings
{
    public static MediaCardViewModel? ToCardViewModel(
        this LiteMediaDto item,
        IK7ServerService apiClient,
        Func<int, string> seasonFormatter,
        bool useParentTitle = false,
        bool preferEpisodeStill = false)
    {
        var kind = item switch
        {
            LiteMusicArtistDto => MediaCardKind.Cover,
            LiteMusicAlbumDto => MediaCardKind.Cover,
            LiteMusicTrackDto => MediaCardKind.Cover,
            LiteMovieDto => MediaCardKind.Poster,
            LiteSerieDto => MediaCardKind.Serie,
            LiteSerieSeasonDto => MediaCardKind.Season,
            LiteSerieEpisodeDto => MediaCardKind.Episode,
            _ => (MediaCardKind?)null
        };

        if (kind is null) return null;

        var userState = item.UserState;

        var episodeDto = item as LiteSerieEpisodeDto;
        var trackDto = item as LiteMusicTrackDto;
        var seasonDto = item as LiteSerieSeasonDto;

        MetadataPictureDto? bestPicture;
        if (episodeDto is not null)
        {
            bestPicture = episodeDto.Pictures?.FirstOrDefault(p => p.Type == MetadataPictureType.Still);
        }
        else
        {
            var pictureSource = item.Pictures;

            bestPicture = pictureSource?.FirstOrDefault(p => p.Type == MetadataPictureType.Cover)
                ?? pictureSource?.FirstOrDefault(p => p.Type == MetadataPictureType.Poster)
                ?? pictureSource?.FirstOrDefault(p => p.Type == MetadataPictureType.Still)
                ?? pictureSource?.FirstOrDefault();
        }

        var backdropPicture = item.Pictures?.FirstOrDefault(p => p.Type == MetadataPictureType.Backdrop);

        string? cardTitle;
        if (useParentTitle)
        {
            if (episodeDto is not null)
            {
                cardTitle = episodeDto.SerieTitle ?? item.Title;
            }
            else
            {
                cardTitle = trackDto?.AlbumTitle ?? item.Title;
            }
        }
        else
        {
            cardTitle = episodeDto is not null
                ? $"S{episodeDto.SeasonNumber:D2}E{episodeDto.EpisodeNumber:D2} \u2014 {item.Title}"
                : item.Title;
        }

        return new MediaCardViewModel
        {
            Id = item.Id.ToString(),
            ParentId = episodeDto?.SerieId.ToString() ?? seasonDto?.SerieId.ToString() ?? trackDto?.AlbumId.ToString(),
            SeasonNumber = seasonDto?.SeasonNumber ?? episodeDto?.SeasonNumber,
            EpisodeNumber = episodeDto?.EpisodeNumber,
            Kind = kind.Value,
            MediaType = GetMediaType(item),
            UserRating = item.UserRating,
            Title = cardTitle,
            AdditionalInformations = GetAdditionalInfo(item, seasonDto, episodeDto, seasonFormatter, preferEpisodeStill),
            PictureUrl = apiClient.GetAbsoluteUri(bestPicture?.GetUri(MetadataPictureSize.Small)?.OriginalString)?.AbsoluteUri,
            BackdropUrl = apiClient.GetAbsoluteUri(backdropPicture?.GetUri(MetadataPictureSize.Medium)?.OriginalString)?.AbsoluteUri,
            Watched = userState?.IsCompleted ?? false,
            Progress = userState?.ProgressPercentage ?? 0,
            SerieSeasonCount = episodeDto?.SerieSeasonCount ?? 1,
            SerieReleaseYear = episodeDto?.SerieReleaseDate?.Year,
            ReleaseYear = item.ReleaseDate?.Year
        };
    }

    private static string? GetAdditionalInfo(
        LiteMediaDto item,
        LiteSerieSeasonDto? season,
        LiteSerieEpisodeDto? episode,
        Func<int, string> seasonFormatter,
        bool preferEpisodeStill = false)
    {
        if (episode is not null && preferEpisodeStill && !string.IsNullOrEmpty(episode.SerieTitle))
            return episode.SerieTitle;

        if (episode is not null)
            return $"S{episode.SeasonNumber:D2}E{episode.EpisodeNumber:D2}";

        if (season is not null)
            return seasonFormatter(season.SeasonNumber);

        return item.ReleaseDate?.Year.ToString();
    }

    public static MediaCardViewModel ToCardViewModel(this HomeFeedItemDto item, IK7ServerService apiClient)
    {
        var kind = item.MediaType switch
        {
            MediaType.MusicAlbum or MediaType.MusicTrack => MediaCardKind.Cover,
            MediaType.Serie => MediaCardKind.Serie,
            MediaType.SerieSeason => MediaCardKind.Season,
            MediaType.SerieEpisode => MediaCardKind.Episode,
            _ => MediaCardKind.Poster
        };

        var bestPicture = item.MediaType == MediaType.SerieEpisode
            ? item.Pictures?.FirstOrDefault(p => p.Type == MetadataPictureType.Still)
            : item.Pictures?.FirstOrDefault(p => p.Type == MetadataPictureType.Cover)
                ?? item.Pictures?.FirstOrDefault(p => p.Type == MetadataPictureType.Poster)
                ?? item.Pictures?.FirstOrDefault(p => p.Type == MetadataPictureType.Still)
                ?? item.Pictures?.FirstOrDefault();

        var backdropPicture = item.Pictures?.FirstOrDefault(p => p.Type == MetadataPictureType.Backdrop);

        return new MediaCardViewModel
        {
            Id = item.Id.ToString(),
            Kind = kind,
            MediaType = item.MediaType,
            Title = item.Title,
            AdditionalInformations = item.AdditionalInfo ?? item.ReleaseDate?.Year.ToString(),
            PictureUrl = apiClient.GetAbsoluteUri(bestPicture?.GetUri(MetadataPictureSize.Small)?.OriginalString)?.AbsoluteUri,
            BackdropUrl = apiClient.GetAbsoluteUri(backdropPicture?.GetUri(MetadataPictureSize.Medium)?.OriginalString)?.AbsoluteUri,
            Watched = item.Watched,
            Progress = item.Progress,
            GroupCount = item.GroupCount,
            NavigationTarget = item.NavigationTarget,
            Overview = item.Overview,
            Genres = item.Genres,
            ContentRating = item.ContentRating,
            RuntimeMinutes = item.RuntimeMinutes,
            Rating = item.Rating,
            ReleaseYear = item.ReleaseDate?.Year
        };
    }

    public static bool HasHeroDetails(this MediaCardViewModel item) =>
        item.Overview is not null
        || item.Genres is not null
        || item.Rating is not null
        || item.ContentRating is not null
        || item.RuntimeMinutes is not null;

    public static MediaCardViewModel WithHeroDetailsFromMedia(
        this MediaCardViewModel source,
        MediaDto media,
        IK7ServerService apiClient)
    {
        var backdropPicture = media.Pictures?.FirstOrDefault(p => p.Type == MetadataPictureType.Backdrop);
        var backdropUrl = backdropPicture is not null
            ? apiClient.GetAbsoluteUri(backdropPicture.GetUri(MetadataPictureSize.Medium)?.OriginalString)?.AbsoluteUri
            : source.BackdropUrl;

        return source with
        {
            Overview = GetOverview(media),
            Genres = media.Genres,
            ContentRating = GetContentRating(media),
            RuntimeMinutes = GetRuntimeMinutes(media),
            Rating = GetBestRating(media.Ratings),
            ReleaseYear = source.ReleaseYear ?? media.ReleaseDate?.Year,
            BackdropUrl = backdropUrl ?? source.BackdropUrl
        };
    }

    private static string? GetOverview(MediaDto media) => media switch
    {
        MovieDto movie => movie.TagLine ?? movie.Overview,
        SerieDto serie => serie.Overview,
        SerieEpisodeDto episode => episode.Overview,
        MusicAlbumDto album => album.Overview,
        _ => null
    };

    private static string? GetContentRating(MediaDto media) => media switch
    {
        MovieDto movie => movie.ContentRating,
        SerieDto serie => serie.ContentRating,
        _ => null
    };

    private static int? GetRuntimeMinutes(MediaDto media) => media switch
    {
        SerieEpisodeDto episode => episode.Runtime,
        _ => null
    };

    private static double? GetBestRating(IReadOnlyList<RatingDto>? ratings)
    {
        var rating = ratings?.FirstOrDefault(r => r.MaximumValue is > 0);
        if (rating?.Value is null || rating.MaximumValue is null or 0)
            return null;

        return Math.Round(rating.Value.Value / rating.MaximumValue.Value * 10, 1);
    }

    private static MediaType GetMediaType(LiteMediaDto item) => item switch
    {
        LiteMovieDto => MediaType.Movie,
        LiteMusicAlbumDto => MediaType.MusicAlbum,
        LiteMusicTrackDto => MediaType.MusicTrack,
        LiteMusicArtistDto => MediaType.MusicArtist,
        LiteSerieDto => MediaType.Serie,
        LiteSerieSeasonDto => MediaType.SerieSeason,
        LiteSerieEpisodeDto => MediaType.SerieEpisode,
        _ => throw new InvalidOperationException($"Unsupported media type: {item.GetType().Name}")
    };
}
