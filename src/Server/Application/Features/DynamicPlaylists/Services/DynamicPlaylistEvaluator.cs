using K7.Server.Application.Features.Medias.Services;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Metadatas.Files;
using K7.Server.Domain.Entities.Playlists;
using K7.Server.Domain.Entities.Ratings;
using K7.Server.Domain.Enums;

namespace K7.Server.Application.Features.DynamicPlaylists.Services;

public static class DynamicPlaylistEvaluator
{
    public static IQueryable<BaseMedia> ApplyRules(
        IQueryable<BaseMedia> query,
        DynamicPlaylist dynamicPlaylist,
        Guid userId)
    {
        query = query.Where(m => m.Type == dynamicPlaylist.MediaType);

        query = MediaRuleEvaluator.ApplyFilter(query, dynamicPlaylist.RuleFilter, userId);

        query = ApplyOrdering(query, dynamicPlaylist);

        if (dynamicPlaylist.Limit.HasValue)
            query = query.Take(dynamicPlaylist.Limit.Value);

        return query;
    }

    private static IQueryable<BaseMedia> ApplyOrdering(IQueryable<BaseMedia> query, DynamicPlaylist sp)
    {
        var desc = sp.OrderDescending;
        return sp.OrderBy switch
        {
            DynamicPlaylistOrderBy.Title => desc ? query.OrderByDescending(m => m.SortTitle ?? m.Title) : query.OrderBy(m => m.SortTitle ?? m.Title),
            DynamicPlaylistOrderBy.DateAdded => desc ? query.OrderByDescending(m => m.Created) : query.OrderBy(m => m.Created),
            DynamicPlaylistOrderBy.Year => desc ? query.OrderByDescending(m => m.ReleaseDate) : query.OrderBy(m => m.ReleaseDate),
            DynamicPlaylistOrderBy.Random => query.OrderBy(_ => EF.Functions.Random()),
            DynamicPlaylistOrderBy.ArtistName => desc
                ? query.OrderByDescending(m => ((MusicTrack)m).Artist!.SortTitle ?? ((MusicTrack)m).Artist!.Title ?? ((MusicTrack)m).Album!.Artist!.SortTitle ?? ((MusicTrack)m).Album!.Artist!.Title)
                : query.OrderBy(m => ((MusicTrack)m).Artist!.SortTitle ?? ((MusicTrack)m).Artist!.Title ?? ((MusicTrack)m).Album!.Artist!.SortTitle ?? ((MusicTrack)m).Album!.Artist!.Title),
            DynamicPlaylistOrderBy.AlbumTitle => desc
                ? query.OrderByDescending(m => ((MusicTrack)m).Album!.SortTitle ?? ((MusicTrack)m).Album!.Title)
                : query.OrderBy(m => ((MusicTrack)m).Album!.SortTitle ?? ((MusicTrack)m).Album!.Title),
            DynamicPlaylistOrderBy.TrackNumber => desc
                ? query.OrderByDescending(m => ((MusicTrack)m).TrackNumber)
                : query.OrderBy(m => ((MusicTrack)m).TrackNumber),
            DynamicPlaylistOrderBy.PlayCount => desc
                ? query.OrderByDescending(m => m.UserMediaStates.Sum(s => s.PlayCount))
                : query.OrderBy(m => m.UserMediaStates.Sum(s => s.PlayCount)),
            DynamicPlaylistOrderBy.Rating => desc
                ? query.OrderByDescending(m => m.Ratings.Where(r => r.Source == RatingSource.LocalUser).Select(r => r.Value).FirstOrDefault())
                : query.OrderBy(m => m.Ratings.Where(r => r.Source == RatingSource.LocalUser).Select(r => r.Value).FirstOrDefault()),
            DynamicPlaylistOrderBy.LastPlayed => desc
                ? query.OrderByDescending(m => m.UserMediaStates.Max(s => s.LastInteractedAt))
                : query.OrderBy(m => m.UserMediaStates.Max(s => s.LastInteractedAt)),
            DynamicPlaylistOrderBy.Duration => desc
                ? query.OrderByDescending(m => m.IndexedFiles.Select(f => ((AudioFileMetadata)f.FileMetadata!).Duration).FirstOrDefault())
                : query.OrderBy(m => m.IndexedFiles.Select(f => ((AudioFileMetadata)f.FileMetadata!).Duration).FirstOrDefault()),
            _ => query.OrderByDescending(m => m.Created)
        };
    }
}
