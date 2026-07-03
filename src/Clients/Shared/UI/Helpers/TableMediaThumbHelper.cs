using K7.Server.Domain.Enums;

namespace K7.Clients.Shared.UI.Helpers;

public static class TableMediaThumbHelper
{
    public enum Size
    {
        Standard,
        Compact
    }

    public static MediaType? ParseMediaType(string? value) =>
        Enum.TryParse<MediaType>(value, ignoreCase: true, out var type) ? type : null;

    public static (int MaxWidth, int MaxHeight) GetMaxSize(MediaType? mediaType, Size size)
    {
        var maxHeight = size == Size.Compact ? 32 : 40;

        return mediaType switch
        {
            MediaType.SerieEpisode => ((int)Math.Round(maxHeight * 16.0 / 9.0), maxHeight),
            MediaType.Movie or MediaType.Serie or MediaType.SerieSeason =>
                ((int)Math.Round(maxHeight * 2.0 / 3.0), maxHeight),
            MediaType.MusicAlbum or MediaType.MusicTrack or MediaType.MusicArtist => (maxHeight, maxHeight),
            _ => (maxHeight, maxHeight)
        };
    }

    public static string BuildStyle(MediaType? mediaType, Size size)
    {
        var (width, height) = GetMaxSize(mediaType, size);
        return FormattableString.Invariant(
            $"--table-media-thumb-width:{width}px;--table-media-thumb-height:{height}px;--table-media-thumb-max-width:{width}px;--table-media-thumb-max-height:{height}px");
    }
}
