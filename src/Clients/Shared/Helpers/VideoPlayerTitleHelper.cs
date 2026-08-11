using K7.Shared;
using K7.Shared.Dtos.Entities.Medias;

namespace K7.Clients.Shared.Helpers;

public static class VideoPlayerTitleHelper
{
    public static string FormatMovie(string? title, int? releaseYear)
    {
        if (string.IsNullOrWhiteSpace(title))
            return releaseYear is int year ? $"({year})" : string.Empty;

        return releaseYear is int y ? $"{title} ({y})" : title;
    }

    public static string FormatMovie(MovieDto movie) =>
        FormatMovie(movie.Title, movie.ReleaseDate?.Year);

    public static string FormatEpisode(
        string? serieName,
        string? episodeTitle,
        int seasonNumber,
        int episodeNumber) =>
        MediaDisplayTitles.FormatEpisode(serieName, episodeTitle, seasonNumber, episodeNumber);

    public static string FormatEpisode(SerieEpisodeDto episode) =>
        FormatEpisode(episode.SerieTitle, episode.Title, episode.SeasonNumber, episode.EpisodeNumber);

    public static string FormatEpisode(LiteSerieEpisodeDto episode) =>
        FormatEpisode(episode.SerieTitle, episode.Title, episode.SeasonNumber, episode.EpisodeNumber);

    public static string FormatFromMedia(MediaDto media) => media switch
    {
        MovieDto movie => FormatMovie(movie),
        SerieEpisodeDto episode => FormatEpisode(episode),
        _ => media.Title ?? string.Empty
    };
}
