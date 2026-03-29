using K7.Clients.Shared.Models;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities.Medias;
using K7.Shared.Interfaces;

namespace K7.Clients.Shared.Mappings;

public static class LiteMediaMappings
{
    public static MediaCardViewModel? ToCardViewModel(this LiteMediaDto item, IK7ServerService apiClient)
    {
        var kind = item switch
        {
            LiteMusicAlbumDto => MediaCardKind.Cover,
            LiteMovieDto => MediaCardKind.Poster,
            LiteSerieDto => MediaCardKind.Serie,
            LiteSerieEpisodeDto => MediaCardKind.Episode,
            _ => (MediaCardKind?)null
        };

        if (kind is null) return null;

        var userState = item.UserState;

        var pictureType = kind == MediaCardKind.Episode
            ? MetadataPictureType.Still
            : MetadataPictureType.Poster;

        var episodeDto = item as LiteSerieEpisodeDto;

        return new MediaCardViewModel
        {
            Id = episodeDto?.SerieId.ToString() ?? item.Id.ToString(),
            Kind = kind.Value,
            Title = episodeDto is not null
                ? $"S{episodeDto.SeasonNumber:D2}E{episodeDto.EpisodeNumber:D2} — {item.Title}"
                : item.Title,
            AdditionalInformations = item.ReleaseDate,
            PictureUrl = apiClient.GetAbsoluteUri(
                item.Pictures?.FirstOrDefault(p => p.Type == pictureType)?
                    .GetUri(MetadataPictureSize.Small)?.OriginalString)?.AbsoluteUri,
            Watched = userState?.IsCompleted ?? false,
            Progress = userState?.ProgressPercentage ?? 0
        };
    }
}
