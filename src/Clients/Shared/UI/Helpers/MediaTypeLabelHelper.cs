using K7.Server.Domain.Enums;
using Microsoft.Extensions.Localization;

namespace K7.Clients.Shared.UI.Helpers;

public static class MediaTypeLabelHelper
{
    public static string Format(string? mediaTypeName, IStringLocalizer localizer) =>
        Enum.TryParse<MediaType>(mediaTypeName, out var type)
            ? Format(type, localizer)
            : mediaTypeName ?? "-";

    public static string Format(MediaType type, IStringLocalizer localizer) => type switch
    {
        MediaType.Movie => localizer["MediaTypeMovies"],
        MediaType.Serie => localizer["MediaTypeSeries"],
        MediaType.SerieSeason => localizer["MediaTypeSerieSeasons"],
        MediaType.SerieEpisode => localizer["MediaTypeSerieEpisodes"],
        MediaType.MusicAlbum => localizer["MediaTypeMusicAlbums"],
        MediaType.MusicTrack => localizer["MediaTypeMusicTracks"],
        MediaType.MusicArtist => localizer["MediaTypeMusicArtists"],
        _ => type.ToString()
    };
}
