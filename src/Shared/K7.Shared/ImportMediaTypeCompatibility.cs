using K7.Server.Domain.Enums;

namespace K7.Shared;

/// <summary>
/// Maps import source types (episode/movie/music/serie) onto K7 <see cref="MediaType"/>.
/// External-id hits must be the same kind of media as the source item.
/// </summary>
public static class ImportMediaTypeCompatibility
{
    public static string ToImportType(MediaType mediaType) => mediaType switch
    {
        MediaType.Movie => "movie",
        MediaType.MusicTrack => "music",
        MediaType.Serie => "serie",
        MediaType.SerieEpisode => "episode",
        MediaType.SerieSeason => "season",
        MediaType.MusicAlbum => "album",
        MediaType.MusicArtist => "artist",
        _ => mediaType.ToString()
    };

    public static bool IsCompatible(string? sourceMediaType, MediaType k7Type) =>
        IsCompatible(sourceMediaType, ToImportType(k7Type));

    public static bool IsCompatible(string? sourceMediaType, string? k7ImportType)
    {
        if (string.IsNullOrWhiteSpace(sourceMediaType) || string.IsNullOrWhiteSpace(k7ImportType))
            return false;

        return string.Equals(sourceMediaType, k7ImportType, StringComparison.OrdinalIgnoreCase);
    }
}
