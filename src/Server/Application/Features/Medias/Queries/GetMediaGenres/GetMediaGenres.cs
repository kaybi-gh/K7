using FluentValidation.Results;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Models;
using K7.Server.Application.Common.QueryExtensions;
using K7.Server.Application.Features.Medias.Queries.Common;
using K7.Server.Application.Features.Medias.Queries.GetMedias;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos;
using K7.Shared.Dtos.Requests;

namespace K7.Server.Application.Features.Medias.Queries.GetMediaGenres;

public record GetMediaGenresQuery : IRequest<PaginatedList<MediaGenreDto>>
{
    public Guid[]? LibraryIds { get; init; }
    public Guid[]? LibraryGroupIds { get; init; }
    public EnumHashSetQueryParam<MediaType>? MediaTypes { get; init; }
    public bool? UnwatchedOnly { get; init; }
    public EnumHashSetQueryParam<GenreOrderingOption>? OrderBy { get; init; }
    public required int PageNumber { get; init; } = 1;
    public required int PageSize { get; init; } = 10;
}

public class GetMediaGenresQueryHandler(IApplicationDbContext context, IUser currentUser)
    : IRequestHandler<GetMediaGenresQuery, PaginatedList<MediaGenreDto>>
{
    public async Task<PaginatedList<MediaGenreDto>> Handle(GetMediaGenresQuery request, CancellationToken cancellationToken)
    {
        if (MediaOrderingHelper.RequiresUserPlayCount(request.OrderBy) && currentUser.Id is null)
            throw new K7.Server.Application.Common.Exceptions.ValidationException([new ValidationFailure("OrderBy", "UserPlayCount ordering requires an authenticated user.")]);

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

        var mediaGenrePairs = mediasQuery
            .SelectMany(m => m.Genres, (m, genre) => new { m.Id, Genre = genre });

        var genreRows = mediaGenrePairs
            .GroupBy(x => x.Genre)
            .Select(g => new GenreAggregateRow
            {
                Name = g.Key,
                MediaCount = g.Count(),
                UserPlayCount = userId.HasValue
                    ? context.UserMediaStates
                        .Where(s => s.UserId == userId.Value && g.Select(x => x.Id).Contains(s.MediaId))
                        .Sum(s => (int?)s.PlayCount) ?? 0
                    : 0
            });

        var ordered = ApplyGenreOrdering(request.OrderBy, genreRows);
        var totalCount = await ordered.CountAsync(cancellationToken);

        var items = await ordered
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(g => new MediaGenreDto
            {
                Name = g.Name,
                MediaCount = g.MediaCount
            })
            .ToListAsync(cancellationToken);

        return new PaginatedList<MediaGenreDto>(items, totalCount, request.PageNumber, request.PageSize);
    }

    private static IQueryable<BaseMedia> ApplyMediaFilters(
        GetMediaGenresQuery request,
        IQueryable<BaseMedia> query,
        Guid? userId)
    {
        var paginationRequest = new Queries.GetMedias.GetMediasWithPaginationQuery
        {
            LibraryIds = request.LibraryIds,
            MediaTypes = request.MediaTypes,
            UnwatchedOnly = request.UnwatchedOnly,
            PageNumber = 1,
            PageSize = 1
        };

        return GetMediasQueryHandler.ApplyFilters(paginationRequest, query, userId);
    }

    private static IOrderedQueryable<GenreAggregateRow> ApplyGenreOrdering(
        HashSet<GenreOrderingOption>? orderBy,
        IQueryable<GenreAggregateRow> queryable)
    {
        if (orderBy is null || orderBy.Count == 0)
            return queryable.OrderByDescending(x => x.MediaCount);

        IOrderedQueryable<GenreAggregateRow>? ordered = null;

        foreach (var order in orderBy)
        {
            ordered = order switch
            {
                GenreOrderingOption.MediaCountAsc => ordered is null
                    ? queryable.OrderBy(x => x.MediaCount)
                    : ordered.ThenBy(x => x.MediaCount),
                GenreOrderingOption.MediaCountDesc => ordered is null
                    ? queryable.OrderByDescending(x => x.MediaCount)
                    : ordered.ThenByDescending(x => x.MediaCount),
                GenreOrderingOption.UserPlayCountAsc => ordered is null
                    ? queryable.OrderBy(x => x.UserPlayCount)
                    : ordered.ThenBy(x => x.UserPlayCount),
                GenreOrderingOption.UserPlayCountDesc => ordered is null
                    ? queryable.OrderByDescending(x => x.UserPlayCount)
                    : ordered.ThenByDescending(x => x.UserPlayCount),
                _ => throw new InvalidOperationException($"Unsupported genre ordering option: {order}")
            };
        }

        return ordered ?? queryable.OrderByDescending(x => x.MediaCount);
    }

    private sealed class GenreAggregateRow
    {
        public required string Name { get; init; }
        public int MediaCount { get; init; }
        public int UserPlayCount { get; init; }
    }
}
