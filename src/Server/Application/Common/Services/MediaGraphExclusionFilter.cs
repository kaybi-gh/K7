using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Entities.Medias;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.Common.Services;

/// <summary>
/// Drops user-hidden children from a loaded media graph so detail pages match browse.
/// A hidden episode stays out of its season, a hidden season out of its series,
/// and a hidden track out of its album.
/// </summary>
public static class MediaGraphExclusionFilter
{
    public static async Task ApplyAsync(
        IApplicationDbContext context,
        BaseMedia entity,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var excludedIds = await context.UserMediaExclusions
            .AsNoTracking()
            .Where(e => e.UserId == userId && (e.IsAdminExcluded || e.IsSelfExcluded))
            .Select(e => e.MediaId)
            .ToListAsync(cancellationToken);

        if (excludedIds.Count == 0)
            return;

        Apply(entity, excludedIds.ToHashSet());
    }

    internal static void Apply(BaseMedia entity, HashSet<Guid> excludedIds)
    {
        switch (entity)
        {
            case Serie serie:
                PruneSeasons(serie, excludedIds);
                break;
            case SerieSeason season:
                PruneEpisodes(season, excludedIds);
                break;
            case MusicAlbum album:
                PruneTracks(album, excludedIds);
                break;
            case MusicArtist artist:
                PruneAlbums(artist, excludedIds);
                break;
        }
    }

    private static void PruneSeasons(Serie serie, HashSet<Guid> excludedIds)
    {
        for (var i = serie.Seasons.Count - 1; i >= 0; i--)
        {
            var season = serie.Seasons[i];
            if (excludedIds.Contains(season.Id) || excludedIds.Contains(season.SerieId))
            {
                serie.Seasons.RemoveAt(i);
                continue;
            }

            PruneEpisodes(season, excludedIds);
        }
    }

    private static void PruneEpisodes(SerieSeason season, HashSet<Guid> excludedIds)
    {
        for (var i = season.Episodes.Count - 1; i >= 0; i--)
        {
            var episode = season.Episodes[i];
            if (excludedIds.Contains(episode.Id) || excludedIds.Contains(episode.SerieId))
                season.Episodes.RemoveAt(i);
        }
    }

    private static void PruneAlbums(MusicArtist artist, HashSet<Guid> excludedIds)
    {
        for (var i = artist.Albums.Count - 1; i >= 0; i--)
        {
            var album = artist.Albums[i];
            if (excludedIds.Contains(album.Id))
            {
                artist.Albums.RemoveAt(i);
                continue;
            }

            PruneTracks(album, excludedIds);
        }
    }

    private static void PruneTracks(MusicAlbum album, HashSet<Guid> excludedIds)
    {
        for (var i = album.Tracks.Count - 1; i >= 0; i--)
        {
            var track = album.Tracks[i];
            if (excludedIds.Contains(track.Id) || excludedIds.Contains(track.AlbumId))
                album.Tracks.RemoveAt(i);
        }
    }
}
