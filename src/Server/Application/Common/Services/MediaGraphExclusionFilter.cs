using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Entities.Medias;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.Common.Services;

/// <summary>
/// Drops user-hidden children from a loaded media graph so detail pages match browse.
/// A hidden episode stays out of its season, a hidden season out of its series,
/// and a hidden track out of its album. Library exclusions drop the same children
/// when their files all sit in an excluded library.
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

        var excludedLibraryIds = await context.UserLibraryExclusions
            .AsNoTracking()
            .Where(e => e.UserId == userId && (e.IsAdminExcluded || e.IsSelfExcluded))
            .Select(e => e.LibraryId)
            .ToListAsync(cancellationToken);

        if (excludedIds.Count > 0)
            Apply(entity, excludedIds.ToHashSet());

        if (excludedLibraryIds.Count > 0)
            ApplyLibraryExclusions(entity, excludedLibraryIds.ToHashSet());
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

    private static void ApplyLibraryExclusions(BaseMedia entity, HashSet<Guid> excludedLibraryIds)
    {
        StripExcludedFiles(entity, excludedLibraryIds);

        switch (entity)
        {
            case MusicAlbum album:
                PruneTracksByLibrary(album, excludedLibraryIds);
                break;
            case MusicArtist artist:
                PruneAlbumsByLibrary(artist, excludedLibraryIds);
                break;
            case Serie serie:
                PruneSeasonsByLibrary(serie, excludedLibraryIds);
                break;
            case SerieSeason season:
                PruneEpisodesByLibrary(season, excludedLibraryIds);
                break;
        }
    }

    private static void PruneAlbumsByLibrary(MusicArtist artist, HashSet<Guid> excludedLibraryIds)
    {
        for (var i = artist.Albums.Count - 1; i >= 0; i--)
        {
            var album = artist.Albums[i];
            var hadTracks = album.Tracks.Count > 0;
            PruneTracksByLibrary(album, excludedLibraryIds);
            StripExcludedFiles(album, excludedLibraryIds);
            if (hadTracks
                && album.Tracks.Count == 0
                && album.IndexedFiles.Count == 0
                && album.RemoteIndexedFiles.Count == 0)
            {
                artist.Albums.RemoveAt(i);
            }
        }
    }

    private static void PruneSeasonsByLibrary(Serie serie, HashSet<Guid> excludedLibraryIds)
    {
        for (var i = serie.Seasons.Count - 1; i >= 0; i--)
        {
            var season = serie.Seasons[i];
            var hadEpisodes = season.Episodes.Count > 0;
            PruneEpisodesByLibrary(season, excludedLibraryIds);
            StripExcludedFiles(season, excludedLibraryIds);
            if (hadEpisodes
                && season.Episodes.Count == 0
                && season.IndexedFiles.Count == 0
                && season.RemoteIndexedFiles.Count == 0)
            {
                serie.Seasons.RemoveAt(i);
            }
        }
    }

    private static void PruneEpisodesByLibrary(SerieSeason season, HashSet<Guid> excludedLibraryIds)
    {
        for (var i = season.Episodes.Count - 1; i >= 0; i--)
        {
            if (!KeepDespiteLibraryExclusions(season.Episodes[i], excludedLibraryIds))
                season.Episodes.RemoveAt(i);
        }
    }

    private static void PruneTracksByLibrary(MusicAlbum album, HashSet<Guid> excludedLibraryIds)
    {
        for (var i = album.Tracks.Count - 1; i >= 0; i--)
        {
            if (!KeepDespiteLibraryExclusions(album.Tracks[i], excludedLibraryIds))
                album.Tracks.RemoveAt(i);
        }
    }

    private static bool KeepDespiteLibraryExclusions(BaseMedia media, HashSet<Guid> excludedLibraryIds)
    {
        var hadLocal = media.IndexedFiles.Count > 0;
        var hadRemote = media.RemoteIndexedFiles.Count > 0;
        StripExcludedFiles(media, excludedLibraryIds);
        if (!hadLocal && !hadRemote)
            return true;

        return media.IndexedFiles.Count > 0 || media.RemoteIndexedFiles.Count > 0;
    }

    private static void StripExcludedFiles(BaseMedia media, HashSet<Guid> excludedLibraryIds)
    {
        if (media.IndexedFiles.Count > 0)
        {
            media.IndexedFiles = media.IndexedFiles
                .Where(f => !excludedLibraryIds.Contains(f.LibraryId))
                .ToList();
        }

        if (media.RemoteIndexedFiles.Count > 0)
        {
            media.RemoteIndexedFiles = media.RemoteIndexedFiles
                .Where(f => !excludedLibraryIds.Contains(f.LibraryId))
                .ToList();
        }
    }
}
