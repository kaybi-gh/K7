using K7.Clients.Shared.Models;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities.Medias;
using K7.Shared.Interfaces;

namespace K7.Clients.Shared.Mappings;

public static class LiteMediaMappings
{
    public static MediaCardViewModel? ToCardViewModel(this LiteMediaDto item, IK7ServerService apiClient, bool useParentTitle = false)
    {
        var kind = item switch
        {
            LiteMusicAlbumDto => MediaCardKind.Cover,
            LiteMusicTrackDto => MediaCardKind.Cover,
            LiteMovieDto => MediaCardKind.Poster,
            LiteSerieDto => MediaCardKind.Serie,
            LiteSerieEpisodeDto => MediaCardKind.Episode,
            _ => (MediaCardKind?)null
        };

        if (kind is null) return null;

        var userState = item.UserState;

        var episodeDto = item as LiteSerieEpisodeDto;
        var trackDto = item as LiteMusicTrackDto;

        var pictureSource = episodeDto is not null
            ? (episodeDto.SeriePictures?.Count > 0 ? episodeDto.SeriePictures : null)
                ?? (episodeDto.SeasonPictures?.Count > 0 ? episodeDto.SeasonPictures : null)
                ?? item.Pictures
            : item.Pictures;

        var bestPicture = pictureSource?.FirstOrDefault(p => p.Type == MetadataPictureType.Poster)
            ?? pictureSource?.FirstOrDefault(p => p.Type == MetadataPictureType.Still)
            ?? pictureSource?.FirstOrDefault();

        string? cardTitle;
        if (useParentTitle)
        {
            if (episodeDto is not null)
            {
                cardTitle = episodeDto.SeasonNumber > 0 
                    ? $"{episodeDto.SerieTitle} — Saison {episodeDto.SeasonNumber}" 
                    : episodeDto.SerieTitle ?? item.Title;
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
            ParentId = episodeDto?.SerieId.ToString() ?? trackDto?.AlbumId.ToString(),
            Kind = kind.Value,
            Title = cardTitle,
            AdditionalInformations = item.ReleaseDate,
            PictureUrl = apiClient.GetAbsoluteUri(bestPicture?.GetUri(MetadataPictureSize.Small)?.OriginalString)?.AbsoluteUri,
            Watched = userState?.IsCompleted ?? false,
            Progress = userState?.ProgressPercentage ?? 0
        };
    }
}
