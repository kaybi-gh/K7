using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Ratings;

namespace K7.Server.Application.Common.Services;

/// <summary>
/// Trims loaded ratings to what one user may see: provider ratings plus that user's own
/// rating. A ratings Include cannot be filtered per user reliably on the TPH hierarchy, so
/// handlers load them all and call this before mapping. Without it the DTOs carry every
/// user's rating (a privacy leak) and the UI shows whichever one comes first.
/// </summary>
public static class MediaRatingScope
{
    public static void ScopeToUser(BaseMedia? media, Guid? userId)
    {
        if (media is null)
            return;

        Visit(media, userId, new HashSet<BaseMedia>(ReferenceEqualityComparer.Instance));
    }

    public static void ScopeToUser(IEnumerable<BaseMedia?> medias, Guid? userId)
    {
        var visited = new HashSet<BaseMedia>(ReferenceEqualityComparer.Instance);
        foreach (var media in medias)
        {
            if (media is not null)
                Visit(media, userId, visited);
        }
    }

    private static void Visit(BaseMedia? media, Guid? userId, HashSet<BaseMedia> visited)
    {
        if (media is null || !visited.Add(media))
            return;

        if (media.Ratings is { Count: > 0 } ratings)
        {
            media.Ratings = ratings
                .Where(r => r is not UserRating userRating || (userId is { } id && userRating.UserId == id))
                .ToList();
        }

        // Navigations that were not included are null at runtime, whatever their declared type.
        switch (media)
        {
            case MusicAlbum album:
                Visit(album.Artist, userId, visited);
                VisitAll(album.Tracks, userId, visited);
                VisitCredits(album.ArtistCredits, userId, visited);
                break;
            case MusicTrack track:
                Visit(track.Album, userId, visited);
                Visit(track.Artist, userId, visited);
                VisitCredits(track.ArtistCredits, userId, visited);
                break;
            case MusicArtist artist:
                VisitAll(artist.Albums, userId, visited);
                VisitCredits(artist.ArtistCredits, userId, visited);
                break;
            case Serie serie:
                VisitAll(serie.Seasons, userId, visited);
                break;
            case SerieSeason season:
                Visit(season.Serie, userId, visited);
                VisitAll(season.Episodes, userId, visited);
                break;
            case SerieEpisode episode:
                Visit(episode.Serie, userId, visited);
                Visit(episode.Season, userId, visited);
                break;
        }
    }

    private static void VisitAll(IEnumerable<BaseMedia>? medias, Guid? userId, HashSet<BaseMedia> visited)
    {
        if (medias is null)
            return;

        foreach (var media in medias)
            Visit(media, userId, visited);
    }

    private static void VisitCredits(IEnumerable<MusicArtistCredit>? credits, Guid? userId, HashSet<BaseMedia> visited)
    {
        if (credits is null)
            return;

        foreach (var credit in credits)
        {
            Visit(credit.MusicArtist, userId, visited);
            Visit(credit.Media, userId, visited);
        }
    }
}
