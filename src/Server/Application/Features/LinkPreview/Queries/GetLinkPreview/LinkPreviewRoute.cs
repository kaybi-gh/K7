namespace K7.Server.Application.Features.LinkPreview.Queries.GetLinkPreview;

public enum LinkPreviewKind
{
    Movie,
    Serie,
    Season,
    Episode,
    Album,
    Artist
}

public readonly record struct LinkPreviewTarget(
    LinkPreviewKind Kind,
    Guid Id,
    int? SeasonNumber = null,
    int? EpisodeNumber = null);

public static class LinkPreviewRoute
{
    public static bool TryParse(string? path, out LinkPreviewTarget target)
    {
        target = default;
        if (string.IsNullOrWhiteSpace(path))
            return false;

        var trimmed = path.Trim();
        if (trimmed.Length > 1)
            trimmed = trimmed.TrimEnd('/');

        var segments = trimmed.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 2)
            return false;

        if (segments[0].Equals("movies", StringComparison.OrdinalIgnoreCase)
            && segments.Length == 2
            && Guid.TryParse(segments[1], out var movieId))
        {
            target = new(LinkPreviewKind.Movie, movieId);
            return true;
        }

        if (segments[0].Equals("persons", StringComparison.OrdinalIgnoreCase))
            return false;

        if (segments[0].Equals("series", StringComparison.OrdinalIgnoreCase)
            && Guid.TryParse(segments[1], out var serieId))
        {
            if (segments.Length == 2)
            {
                target = new(LinkPreviewKind.Serie, serieId);
                return true;
            }

            if (segments.Length >= 4
                && segments[2].Equals("seasons", StringComparison.OrdinalIgnoreCase)
                && int.TryParse(segments[3], out var seasonNumber))
            {
                if (segments.Length == 4)
                {
                    target = new(LinkPreviewKind.Season, serieId, seasonNumber);
                    return true;
                }

                if (segments.Length == 6
                    && segments[4].Equals("episodes", StringComparison.OrdinalIgnoreCase)
                    && int.TryParse(segments[5], out var episodeNumber))
                {
                    target = new(LinkPreviewKind.Episode, serieId, seasonNumber, episodeNumber);
                    return true;
                }
            }

            return false;
        }

        if (segments[0].Equals("music", StringComparison.OrdinalIgnoreCase)
            && segments.Length == 3
            && Guid.TryParse(segments[2], out var musicId))
        {
            if (segments[1].Equals("albums", StringComparison.OrdinalIgnoreCase))
            {
                target = new(LinkPreviewKind.Album, musicId);
                return true;
            }

            if (segments[1].Equals("artists", StringComparison.OrdinalIgnoreCase))
            {
                target = new(LinkPreviewKind.Artist, musicId);
                return true;
            }
        }

        return false;
    }
}
