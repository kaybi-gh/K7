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

        return new MediaCardViewModel
        {
            Id = item.Id.ToString(),
            Kind = kind.Value,
            Title = item.Title,
            AdditionalInformations = item.ReleaseDate,
            PictureUrl = apiClient.GetAbsoluteUri(
                item.Pictures?.FirstOrDefault(p => p.Type == MetadataPictureType.Poster)?
                    .GetUri(MetadataPictureSize.Small)?.OriginalString)?.AbsoluteUri,
            ParentId = item is LiteSerieEpisodeDto ep ? ep.SerieId.ToString() : null,
            Watched = userState?.IsCompleted ?? false,
            Progress = userState?.ProgressPercentage ?? 0
        };
    }
}
