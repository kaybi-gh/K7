using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Mappings;
using K7.Server.Application.Common.QueryExtensions;
using K7.Server.Application.Common.Services;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos;
using K7.Shared.Dtos.Entities.Medias;

namespace K7.Server.Application.Features.Music.Queries.GetTopMusicTracks;

public record GetTopMusicTracksQuery : IRequest<IReadOnlyList<PlayedMusicTrackDto>>
{
    public Guid[]? LibraryIds { get; init; }
    public int Count { get; init; } = 20;
}

public class GetTopMusicTracksQueryHandler(
    IApplicationDbContext context,
    IUser currentUser,
    LiteMediaProjectionService liteMediaProjection,
    MediaAccessFilter mediaAccessFilter)
    : IRequestHandler<GetTopMusicTracksQuery, IReadOnlyList<PlayedMusicTrackDto>>
{
    public async Task<IReadOnlyList<PlayedMusicTrackDto>> Handle(
        GetTopMusicTracksQuery request,
        CancellationToken cancellationToken)
    {
        var count = Math.Clamp(request.Count, 1, 100);
        var tracksQuery = await BuildTracksQueryAsync(request.LibraryIds, cancellationToken);

        var topTracks = await context.UserMediaStates
            .AsNoTracking()
            .Where(s => s.PlayCount > 0)
            .Join(
                tracksQuery,
                s => s.MediaId,
                t => t.Id,
                (s, _) => new { s.MediaId, s.PlayCount })
            .GroupBy(x => x.MediaId)
            .Select(g => new { MediaId = g.Key, PlayCount = g.Sum(x => x.PlayCount) })
            .OrderByDescending(x => x.PlayCount)
            .Take(count)
            .ToListAsync(cancellationToken);

        if (topTracks.Count == 0)
            return [];

        var playCountById = topTracks.ToDictionary(x => x.MediaId, x => x.PlayCount);
        var trackIds = topTracks.Select(x => x.MediaId).ToList();
        var liteTracks = await liteMediaProjection.GetLiteMediaDtosAsync(trackIds, currentUser.Id, cancellationToken);

        return liteTracks
            .OfType<LiteMusicTrackDto>()
            .Where(t => playCountById.ContainsKey(t.Id))
            .Select(t => new PlayedMusicTrackDto
            {
                Track = t,
                PlayCount = playCountById[t.Id]
            })
            .ToList();
    }

    private async Task<IQueryable<MusicTrack>> BuildTracksQueryAsync(
        Guid[]? libraryIds,
        CancellationToken cancellationToken)
    {
        // Stay on BaseMedia for access filters. WhereNotUserExcluded casts to SerieEpisode,
        // which EF cannot coerce once the query is already OfType<MusicTrack>.
        IQueryable<BaseMedia> accessible = context.Medias
            .AsNoTracking()
            .Where(m => m.Type == MediaType.MusicTrack)
            .WhereHasLibraryAvailability(context);

        if (currentUser.Id is { } userId)
        {
            var sharedProfileId = await currentUser.GetSharedProfileIdAsync(cancellationToken);
            accessible = await mediaAccessFilter.ApplyAllAsync(accessible, userId, sharedProfileId, cancellationToken);
        }
        else
        {
            accessible = mediaAccessFilter.ApplyUnavailablePeerExclusion(accessible);
        }

        var accessibleIds = accessible.Select(m => m.Id);
        var tracks = context.Medias
            .OfType<MusicTrack>()
            .AsNoTracking()
            .Where(t => accessibleIds.Contains(t.Id));

        if (libraryIds is not { Length: > 0 })
            return tracks;

        return tracks.WhereAvailableInLibraries(context, libraryIds);
    }
}
