using K7.Server.Application.Features.Medias.Services;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Metadatas.Files;
using K7.Server.Domain.Entities.Playlists;
using K7.Server.Domain.Entities.Ratings;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Models;

namespace K7.Server.Application.Features.DynamicPlaylists.Services;

public static class DynamicPlaylistEvaluator
{
    public static IQueryable<BaseMedia> ApplyRules(
        IQueryable<BaseMedia> query,
        DynamicPlaylist dynamicPlaylist,
        Guid userId) =>
        ApplyRules(
            query,
            dynamicPlaylist.MediaType,
            dynamicPlaylist.RuleFilter,
            dynamicPlaylist.OrderBy,
            dynamicPlaylist.OrderDescending,
            dynamicPlaylist.Limit,
            userId);

    public static IQueryable<BaseMedia> ApplyRules(
        IQueryable<BaseMedia> query,
        MediaType mediaType,
        RuleGroup ruleFilter,
        DynamicPlaylistOrderBy orderBy,
        bool orderDescending,
        int? limit,
        Guid userId)
    {
        query = query.Where(m => m.Type == mediaType);
        query = MediaRuleEvaluator.ApplyFilter(query, ruleFilter, userId);
        query = ApplyOrdering(query, orderBy, orderDescending);

        if (limit is > 0)
            query = query.Take(limit.Value);

        return query;
    }

    private static IQueryable<BaseMedia> ApplyOrdering(
        IQueryable<BaseMedia> query,
        DynamicPlaylistOrderBy orderBy,
        bool desc)
    {
        return orderBy switch
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
