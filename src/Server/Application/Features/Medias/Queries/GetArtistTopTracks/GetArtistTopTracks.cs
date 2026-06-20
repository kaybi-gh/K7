using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Mappings;
using K7.Server.Application.Common.Security;
using K7.Server.Application.Services;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Ratings;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities.Medias;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.Features.Medias.Queries.GetArtistTopTracks;

[Authorize(Roles = $"{Roles.Guest},{Roles.User},{Roles.Administrator}")]
public record GetArtistTopTracksQuery : IRequest<IReadOnlyList<LiteMusicTrackDto>>
{
    public required Guid ArtistId { get; init; }
    public int Count { get; init; } = 10;
}

public class GetArtistTopTracksQueryHandler(
    IApplicationDbContext context,
    IUser currentUser,
    IMediaAccessGuard accessGuard)
    : IRequestHandler<GetArtistTopTracksQuery, IReadOnlyList<LiteMusicTrackDto>>
{
    public async Task<IReadOnlyList<LiteMusicTrackDto>> Handle(
        GetArtistTopTracksQuery request,
        CancellationToken cancellationToken)
    {
        await accessGuard.EnsureAccessAsync(request.ArtistId, cancellationToken);

        var isArtist = await context.Medias
            .AsNoTracking()
            .OfType<MusicArtist>()
            .AnyAsync(a => a.Id == request.ArtistId, cancellationToken);

        if (!isArtist)
            return [];

        var topTrackIds = await GetTopTrackIdsByPlayCountAsync(request.ArtistId, request.Count, cancellationToken);

        if (topTrackIds.Count == 0)
            topTrackIds = await GetTopTrackIdsByUserRatingAsync(request.ArtistId, request.Count, cancellationToken);

        if (topTrackIds.Count == 0)
            topTrackIds = await GetTopTrackIdsByProviderPopularityAsync(request.ArtistId, request.Count, cancellationToken);

        if (topTrackIds.Count == 0)
            topTrackIds = await GetTopTrackIdsFromCatalogAsync(request.ArtistId, request.Count, cancellationToken);

        if (topTrackIds.Count == 0)
            return [];

        return await LoadTracksAsync(topTrackIds, currentUser.Id, cancellationToken);
    }

    private async Task<List<Guid>> GetTopTrackIdsByPlayCountAsync(
        Guid artistId,
        int count,
        CancellationToken cancellationToken)
    {
        return await (
            from state in context.UserMediaStates.AsNoTracking()
            where state.PlayCount > 0
            join track in context.Medias.OfType<MusicTrack>() on state.MediaId equals track.Id
            join album in context.Medias.OfType<MusicAlbum>() on track.AlbumId equals album.Id into albumJoin
            from album in albumJoin.DefaultIfEmpty()
            let resolvedArtistId = track.ArtistId ?? album!.ArtistId
            where resolvedArtistId == artistId
            group state by track.Id into grouped
            orderby grouped.Sum(s => s.PlayCount) descending
            select grouped.Key)
            .Take(count)
            .ToListAsync(cancellationToken);
    }

    private async Task<List<Guid>> GetTopTrackIdsByUserRatingAsync(
        Guid artistId,
        int count,
        CancellationToken cancellationToken)
    {
        return await (
            from track in context.Medias.OfType<MusicTrack>().AsNoTracking()
            join album in context.Medias.OfType<MusicAlbum>() on track.AlbumId equals album.Id into albumJoin
            from album in albumJoin.DefaultIfEmpty()
            let resolvedArtistId = track.ArtistId ?? album!.ArtistId
            where resolvedArtistId == artistId
            join rating in context.Ratings.OfType<UserRating>() on track.Id equals rating.MediaId
            group rating by track.Id into grouped
            orderby grouped.Average(r => r.Value) descending, grouped.Count() descending
            select grouped.Key)
            .Take(count)
            .ToListAsync(cancellationToken);
    }

    private async Task<List<Guid>> GetTopTrackIdsByProviderPopularityAsync(
        Guid artistId,
        int count,
        CancellationToken cancellationToken)
    {
        return await (
            from track in context.Medias.OfType<MusicTrack>().AsNoTracking()
            join album in context.Medias.OfType<MusicAlbum>() on track.AlbumId equals album.Id into albumJoin
            from album in albumJoin.DefaultIfEmpty()
            let resolvedArtistId = track.ArtistId ?? album!.ArtistId
            where resolvedArtistId == artistId
            join rating in context.Ratings.OfType<MetadataProviderRating>() on track.Id equals rating.MediaId
            group rating by track.Id into grouped
            orderby grouped.Max(r => r.RatingCount ?? 0) descending,
                grouped.Max(r => r.MaximumValue > 0 ? r.Value / r.MaximumValue : r.Value) descending
            select grouped.Key)
            .Take(count)
            .ToListAsync(cancellationToken);
    }

    private async Task<List<Guid>> GetTopTrackIdsFromCatalogAsync(
        Guid artistId,
        int count,
        CancellationToken cancellationToken)
    {
        return await (
            from track in context.Medias.OfType<MusicTrack>().AsNoTracking()
            join album in context.Medias.OfType<MusicAlbum>() on track.AlbumId equals album.Id into albumJoin
            from album in albumJoin.DefaultIfEmpty()
            let resolvedArtistId = track.ArtistId ?? album!.ArtistId
            where resolvedArtistId == artistId
            where track.IndexedFiles.Any() || track.RemoteIndexedFiles.Any()
            orderby album!.ReleaseDate descending, track.DiscNumber, track.TrackNumber, track.Title
            select track.Id)
            .Take(count)
            .ToListAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<LiteMusicTrackDto>> LoadTracksAsync(
        IReadOnlyList<Guid> topTrackIds,
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
            .Include(t => t.ArtistCredits)
                .ThenInclude(c => c.MusicArtist)
            .Where(t => topTrackIds.Contains(t.Id))
            .AsSplitQuery()
            .AsQueryable();

        if (userId.HasValue)
        {
            trackQuery = trackQuery.Include(t => t.UserMediaStates.Where(s => s.UserId == userId.Value));
        }

        var tracks = await trackQuery.ToListAsync(cancellationToken);
        var tracksById = tracks.ToDictionary(t => t.Id);

        return topTrackIds
            .Where(tracksById.ContainsKey)
            .Select(id => tracksById[id])
            .Where(t => t.IndexedFiles.Count > 0 || t.RemoteIndexedFiles.Count > 0)
            .Select(t => (LiteMusicTrackDto)t.ToLiteMediaDto())
            .ToList();
    }
}
