using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Mappings;
using K7.Server.Application.Common.QueryExtensions;
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

public class GetTopMusicTracksQueryHandler(IApplicationDbContext context, IUser currentUser)
    : IRequestHandler<GetTopMusicTracksQuery, IReadOnlyList<PlayedMusicTrackDto>>
{
    public async Task<IReadOnlyList<PlayedMusicTrackDto>> Handle(
        GetTopMusicTracksQuery request,
        CancellationToken cancellationToken)
    {
        var count = Math.Clamp(request.Count, 1, 100);
        var tracksQuery = BuildTracksQuery(request.LibraryIds);

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
        var loadedTracks = await LoadTracksAsync(trackIds, currentUser.Id, cancellationToken);

        return loadedTracks
            .Where(t => playCountById.ContainsKey(t.Id))
            .Select(t => new PlayedMusicTrackDto
            {
                Track = (LiteMusicTrackDto)t.ToLiteMediaDto(),
                PlayCount = playCountById[t.Id]
            })
            .ToList();
    }

    private IQueryable<MusicTrack> BuildTracksQuery(Guid[]? libraryIds)
    {
        var query = context.Medias
            .AsNoTracking()
            .OfType<MusicTrack>()
            .WhereHasLibraryAvailability(context);

        if (libraryIds is not { Length: > 0 })
            return query;

        return query.WhereAvailableInLibraries(context, libraryIds);
    }

    private async Task<List<MusicTrack>> LoadTracksAsync(
        IReadOnlyList<Guid> trackIds,
        Guid? userId,
        CancellationToken cancellationToken)
    {
        var trackQuery = context.Medias
            .AsNoTracking()
            .OfType<MusicTrack>()
            .Include(t => t.Pictures)
                .ThenInclude(p => p.Variants)
            .Include(t => t.IndexedFiles)
                .ThenInclude(f => f.FileMetadata)
            .Include(t => t.RemoteIndexedFiles)
            .Include(t => t.Album)
                .ThenInclude(a => a.Pictures)
                    .ThenInclude(p => p.Variants)
            .Include(t => t.Artist)
            .Include(t => t.AudioAnalysis)
            .Where(t => trackIds.Contains(t.Id))
            .AsSplitQuery()
            .AsQueryable();

        if (userId.HasValue)
            trackQuery = trackQuery.Include(t => t.UserMediaStates.Where(s => s.UserId == userId.Value));

        var tracks = await trackQuery.ToListAsync(cancellationToken);
        var tracksById = tracks.ToDictionary(t => t.Id);

        return trackIds
            .Where(tracksById.ContainsKey)
            .Select(id => tracksById[id])
            .Where(t => t.IndexedFiles.Count > 0 || t.RemoteIndexedFiles.Count > 0)
            .ToList();
    }
}
