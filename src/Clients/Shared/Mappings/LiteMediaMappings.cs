using K7.Clients.Shared.Models;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities.Medias;
using K7.Shared.Dtos.Home;
using K7.Shared.Interfaces;

namespace K7.Clients.Shared.Mappings;

public static class LiteMediaMappings
{
    public static MediaCardViewModel? ToCardViewModel(this LiteMediaDto item, IK7ServerService apiClient, Func<int, string> seasonFormatter, bool useParentTitle = false)
    {
        var kind = item switch
        {
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

        var pictureSource = episodeDto is not null
            ? (episodeDto.SeriePictures?.Count > 0 ? episodeDto.SeriePictures : null)
                ?? (episodeDto.SeasonPictures?.Count > 0 ? episodeDto.SeasonPictures : null)
                ?? item.Pictures
            : item.Pictures;

        var bestPicture = pictureSource?.FirstOrDefault(p => p.Type == MetadataPictureType.Poster)
            ?? pictureSource?.FirstOrDefault(p => p.Type == MetadataPictureType.Still)
            ?? pictureSource?.FirstOrDefault();

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
            Title = cardTitle,
            AdditionalInformations = GetAdditionalInfo(item, seasonDto, episodeDto, seasonFormatter),
            PictureUrl = apiClient.GetAbsoluteUri(bestPicture?.GetUri(MetadataPictureSize.Small)?.OriginalString)?.AbsoluteUri,
            BackdropUrl = apiClient.GetAbsoluteUri(backdropPicture?.GetUri(MetadataPictureSize.Medium)?.OriginalString)?.AbsoluteUri,
            Watched = userState?.IsCompleted ?? false,
            Progress = userState?.ProgressPercentage ?? 0,
            SerieSeasonCount = episodeDto?.SerieSeasonCount ?? 1,
            SerieReleaseYear = episodeDto?.SerieReleaseDate?.Year
        };
    }

    private static string? GetAdditionalInfo(
        LiteMediaDto item,
        LiteSerieSeasonDto? season,
        LiteSerieEpisodeDto? episode,
        Func<int, string> seasonFormatter)
    {
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

        var bestPicture = item.Pictures?.FirstOrDefault(p => p.Type == MetadataPictureType.Poster)
            ?? item.Pictures?.FirstOrDefault(p => p.Type == MetadataPictureType.Still)
            ?? item.Pictures?.FirstOrDefault();

        var backdropPicture = item.Pictures?.FirstOrDefault(p => p.Type == MetadataPictureType.Backdrop);

        return new MediaCardViewModel
        {
            Id = item.Id.ToString(),
            Kind = kind,
            Title = item.Title,
            AdditionalInformations = item.AdditionalInfo,
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
}
