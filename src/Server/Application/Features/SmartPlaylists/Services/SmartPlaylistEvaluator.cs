using K7.Server.Application.Features.Medias.Services;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Metadatas.Files;
using K7.Server.Domain.Entities.Playlists;
using K7.Server.Domain.Entities.Ratings;
using K7.Server.Domain.Enums;

namespace K7.Server.Application.Features.SmartPlaylists.Services;

public static class SmartPlaylistEvaluator
{
    public static IQueryable<BaseMedia> ApplyRules(
        IQueryable<BaseMedia> query,
        SmartPlaylist smartPlaylist,
        Guid userId)
    {
        query = query.Where(m => m.Type == smartPlaylist.MediaType);

        query = MediaRuleEvaluator.ApplyFilter(query, smartPlaylist.RuleFilter, userId);

        query = ApplyOrdering(query, smartPlaylist);

        if (smartPlaylist.Limit.HasValue)
            query = query.Take(smartPlaylist.Limit.Value);

        return query;
    }

    private static IQueryable<BaseMedia> ApplyOrdering(IQueryable<BaseMedia> query, SmartPlaylist sp)
    {
        var desc = sp.OrderDescending;
        return sp.OrderBy switch
        {
            SmartPlaylistOrderBy.Title => desc ? query.OrderByDescending(m => m.Title) : query.OrderBy(m => m.Title),
            SmartPlaylistOrderBy.DateAdded => desc ? query.OrderByDescending(m => m.Created) : query.OrderBy(m => m.Created),
            SmartPlaylistOrderBy.Year => desc ? query.OrderByDescending(m => m.ReleaseDate) : query.OrderBy(m => m.ReleaseDate),
            SmartPlaylistOrderBy.Random => query.OrderBy(_ => EF.Functions.Random()),
            SmartPlaylistOrderBy.ArtistName => desc
                ? query.OrderByDescending(m => ((MusicTrack)m).Artist!.Title ?? ((MusicTrack)m).Album!.Artist!.Title)
                : query.OrderBy(m => ((MusicTrack)m).Artist!.Title ?? ((MusicTrack)m).Album!.Artist!.Title),
            SmartPlaylistOrderBy.AlbumTitle => desc
                ? query.OrderByDescending(m => ((MusicTrack)m).Album.Title)
                : query.OrderBy(m => ((MusicTrack)m).Album.Title),
            SmartPlaylistOrderBy.TrackNumber => desc
                ? query.OrderByDescending(m => ((MusicTrack)m).TrackNumber)
                : query.OrderBy(m => ((MusicTrack)m).TrackNumber),
            SmartPlaylistOrderBy.PlayCount => desc
                ? query.OrderByDescending(m => m.UserMediaStates.Sum(s => s.PlayCount))
                : query.OrderBy(m => m.UserMediaStates.Sum(s => s.PlayCount)),
            SmartPlaylistOrderBy.Rating => desc
                ? query.OrderByDescending(m => m.Ratings.Where(r => r.Source == RatingSource.LocalUser).Select(r => r.Value).FirstOrDefault())
                : query.OrderBy(m => m.Ratings.Where(r => r.Source == RatingSource.LocalUser).Select(r => r.Value).FirstOrDefault()),
            SmartPlaylistOrderBy.LastPlayed => desc
                ? query.OrderByDescending(m => m.UserMediaStates.Max(s => s.LastInteractedAt))
                : query.OrderBy(m => m.UserMediaStates.Max(s => s.LastInteractedAt)),
            SmartPlaylistOrderBy.Duration => desc
                ? query.OrderByDescending(m => m.IndexedFiles.Select(f => ((AudioFileMetadata)f.FileMetadata!).Duration).FirstOrDefault())
                : query.OrderBy(m => m.IndexedFiles.Select(f => ((AudioFileMetadata)f.FileMetadata!).Duration).FirstOrDefault()),
            _ => query.OrderByDescending(m => m.Created)
        };
    }
}
