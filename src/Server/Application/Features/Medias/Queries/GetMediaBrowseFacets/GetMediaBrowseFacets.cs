using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Models;
using K7.Server.Application.Common.QueryExtensions;
using K7.Server.Application.Features.Medias.Queries.Common;
using K7.Server.Application.Features.Medias.Queries.GetMedias;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos;

namespace K7.Server.Application.Features.Medias.Queries.GetMediaBrowseFacets;

public record GetMediaBrowseFacetsQuery : IRequest<MediaBrowseFacetsDto>
{
    public Guid[]? LibraryIds { get; init; }
    public Guid[]? LibraryGroupIds { get; init; }
    public EnumHashSetQueryParam<MediaType>? MediaTypes { get; init; }
}

public class GetMediaBrowseFacetsQueryHandler(IApplicationDbContext context, IUser currentUser)
    : IRequestHandler<GetMediaBrowseFacetsQuery, MediaBrowseFacetsDto>
{
    private const int MaxValues = 100;

    public async Task<MediaBrowseFacetsDto> Handle(GetMediaBrowseFacetsQuery request, CancellationToken cancellationToken)
    {
        var userId = currentUser.Id;

        var libraryIds = await LibraryGroupFilterHelper.ResolveLibraryIdsAsync(
            context, request.LibraryIds, request.LibraryGroupIds, cancellationToken);
        request = request with { LibraryIds = libraryIds, LibraryGroupIds = null };

        var mediasQuery = context.Medias.AsNoTracking().AsQueryable();
        mediasQuery = ApplyMediaFilters(request, mediasQuery, userId);

        if (userId.HasValue)
        {
            var excludedLibraryIds = context.UserLibraryExclusions
                .Where(e => e.UserId == userId.Value && (e.IsAdminExcluded || e.IsSelfExcluded))
                .Select(e => e.LibraryId);

            mediasQuery = mediasQuery.Where(x =>
                x is MusicAlbum
                    ? x.RemoteIndexedFiles.Any(r => !excludedLibraryIds.Contains(r.LibraryId))
                        || ((MusicAlbum)x).Tracks.Any(t => t.IndexedFiles.Any(f => !excludedLibraryIds.Contains(f.LibraryId))
                            || t.RemoteIndexedFiles.Any(r => !excludedLibraryIds.Contains(r.LibraryId)))
                    : x is MusicArtist
                        ? ((MusicArtist)x).Albums.Any(a => a.RemoteIndexedFiles.Any(r => !excludedLibraryIds.Contains(r.LibraryId))
                            || a.Tracks.Any(t => t.IndexedFiles.Any(f => !excludedLibraryIds.Contains(f.LibraryId))
                                || t.RemoteIndexedFiles.Any(r => !excludedLibraryIds.Contains(r.LibraryId))))
                    : x is MusicTrack
                        ? x.IndexedFiles.Any(f => !excludedLibraryIds.Contains(f.LibraryId))
                            || x.RemoteIndexedFiles.Any(r => !excludedLibraryIds.Contains(r.LibraryId))
                            || ((MusicTrack)x).Album.RemoteIndexedFiles.Any(r => !excludedLibraryIds.Contains(r.LibraryId))
                            || ((MusicTrack)x).Album.Tracks.Any(t => t.IndexedFiles.Any(f => !excludedLibraryIds.Contains(f.LibraryId)))
                    : x is Serie
                        ? x.RemoteIndexedFiles.Any(r => !excludedLibraryIds.Contains(r.LibraryId))
                            || ((Serie)x).Seasons.Any(s => s.Episodes.Any(e => e.IndexedFiles.Any(f => !excludedLibraryIds.Contains(f.LibraryId))
                                || e.RemoteIndexedFiles.Any(r => !excludedLibraryIds.Contains(r.LibraryId))))
                        : x is SerieSeason
                            ? ((SerieSeason)x).Episodes.Any(e => e.IndexedFiles.Any(f => !excludedLibraryIds.Contains(f.LibraryId))
                                || e.RemoteIndexedFiles.Any(r => !excludedLibraryIds.Contains(r.LibraryId)))
                            : x.IndexedFiles.Any(f => !excludedLibraryIds.Contains(f.LibraryId))
                                || x.RemoteIndexedFiles.Any(r => !excludedLibraryIds.Contains(r.LibraryId)));
        }

        var movieRatings = await mediasQuery
            .OfType<Movie>()
            .Where(m => m.ContentRating != null && m.ContentRating != "")
            .Select(m => m.ContentRating!)
            .ToListAsync(cancellationToken);

        var serieRatings = await mediasQuery
            .OfType<Serie>()
            .Where(s => s.ContentRating != null && s.ContentRating != "")
            .Select(s => s.ContentRating!)
            .ToListAsync(cancellationToken);

        var contentRatings = movieRatings
            .Concat(serieRatings)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .Take(MaxValues)
            .ToList();

        var movieStudios = await mediasQuery
            .OfType<Movie>()
            .SelectMany(m => m.Studios)
            .ToListAsync(cancellationToken);

        var serieStudios = await mediasQuery
            .OfType<Serie>()
            .SelectMany(s => s.Studios)
            .ToListAsync(cancellationToken);

        var studios = movieStudios
            .Concat(serieStudios)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .Take(MaxValues)
            .ToList();

        var networks = await mediasQuery
            .OfType<Serie>()
            .Where(s => s.Network != null && s.Network != "")
            .Select(s => s.Network!)
            .Distinct()
            .OrderBy(x => x)
            .Take(MaxValues)
            .ToListAsync(cancellationToken);

        return new MediaBrowseFacetsDto
        {
            ContentRatings = contentRatings,
            Studios = studios,
            Networks = networks
        };
    }

    private static IQueryable<BaseMedia> ApplyMediaFilters(
        GetMediaBrowseFacetsQuery request,
        IQueryable<BaseMedia> query,
        Guid? userId)
    {
        var paginationRequest = new GetMediasWithPaginationQuery
        {
            LibraryIds = request.LibraryIds,
            MediaTypes = request.MediaTypes,
            PageNumber = 1,
            PageSize = 1
        };

        return GetMediasQueryHandler.ApplyFilters(paginationRequest, query, userId);
    }
}
