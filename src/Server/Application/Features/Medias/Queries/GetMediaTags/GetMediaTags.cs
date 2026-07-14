using FluentValidation.Results;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Models;
using K7.Server.Application.Common.QueryExtensions;
using K7.Server.Application.Features.Medias.Queries.Common;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos;
using K7.Shared.Dtos.Requests;

namespace K7.Server.Application.Features.Medias.Queries.GetMediaTags;

public record GetMediaTagsQuery : IRequest<MediaTagsDto>
{
    public Guid[]? LibraryIds { get; init; }
    public Guid[]? LibraryGroupIds { get; init; }
    public EnumHashSetQueryParam<MediaType>? MediaTypes { get; init; }
    public EnumHashSetQueryParam<MetadataTagKind>? Kinds { get; init; }
    public string? SearchText { get; init; }
    public bool? UnwatchedOnly { get; init; }
    public EnumHashSetQueryParam<MediaTagOrderingOption>? OrderBy { get; init; }
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 100;
    public int Limit { get; init; } = 100;
}

public class GetMediaTagsQueryHandler(IApplicationDbContext context, IUser currentUser)
    : IRequestHandler<GetMediaTagsQuery, MediaTagsDto>
{
    private const int MaxLimit = 100;
    private const int MaxPageSize = 100;

    private static readonly MetadataTagKind[] DefaultKinds =
    [
        MetadataTagKind.ContentRating,
        MetadataTagKind.Studio,
        MetadataTagKind.Network
    ];

    public async Task<MediaTagsDto> Handle(GetMediaTagsQuery request, CancellationToken cancellationToken)
    {
        if (MediaOrderingHelper.RequiresUserPlayCount(request.OrderBy) && currentUser.Id is null)
            throw new K7.Server.Application.Common.Exceptions.ValidationException([new ValidationFailure("OrderBy", "UserPlayCount ordering requires an authenticated user.")]);

        var limit = Math.Clamp(request.Limit, 1, MaxLimit);
        var pageNumber = Math.Max(request.PageNumber, 1);
        var pageSize = Math.Clamp(request.PageSize, 1, MaxPageSize);
        var search = request.SearchText?.Trim();
        var kinds = ResolveKinds(request.Kinds);
        var userId = currentUser.Id;

        var mediasQuery = await BrowseMediaScope.GetMediasQueryAsync(
            context,
            request.LibraryIds,
            request.LibraryGroupIds,
            request.MediaTypes,
            userId,
            request.UnwatchedOnly,
            cancellationToken);

        var mediaIds = mediasQuery.Select(m => m.Id);

        var results = new List<MediaTagKindValuesDto>(kinds.Length);
        foreach (var kind in kinds)
        {
            results.Add(kind == MetadataTagKind.Genre
                ? await GetGenreValuesAsync(mediasQuery, userId, search, request.OrderBy, pageNumber, pageSize, cancellationToken)
                : await GetTagValuesAsync(mediaIds, kind, search, limit, cancellationToken));
        }

        return new MediaTagsDto { Kinds = results };
    }

    private static MetadataTagKind[] ResolveKinds(EnumHashSetQueryParam<MetadataTagKind>? kinds) =>
        kinds is { Count: > 0 } ? kinds.ToArray() : DefaultKinds;

    private async Task<MediaTagKindValuesDto> GetTagValuesAsync(
        IQueryable<Guid> mediaIds,
        MetadataTagKind kind,
        string? search,
        int limit,
        CancellationToken cancellationToken)
    {
        var query = context.MediaMetadataTags.AsNoTracking()
            .Where(mmt => mediaIds.Contains(mmt.MediaId) && mmt.MetadataTag.Kind == kind);

        if (!string.IsNullOrEmpty(search))
        {
            var term = EfLikeQueryExtensions.ToLowerSearchTerm(search);
            query = query.Where(mmt => mmt.MetadataTag.DisplayName != null
                && mmt.MetadataTag.DisplayName.ToLower().Contains(term));
        }

        var values = await query
            .Select(mmt => mmt.MetadataTag.DisplayName)
            .Distinct()
            .OrderBy(name => name)
            .Take(limit)
            .Select(name => new MediaTagValueDto { DisplayName = name, MediaCount = 0 })
            .ToListAsync(cancellationToken);

        return new MediaTagKindValuesDto
        {
            Kind = kind,
            Values = values,
            TotalCount = values.Count,
            PageNumber = 1,
            PageSize = values.Count
        };
    }

    private async Task<MediaTagKindValuesDto> GetGenreValuesAsync(
        IQueryable<BaseMedia> mediasQuery,
        Guid? userId,
        string? search,
        EnumHashSetQueryParam<MediaTagOrderingOption>? orderBy,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var pairsQuery = BuildMetadataGenrePairsQuery(mediasQuery);

        if (!string.IsNullOrEmpty(search))
        {
            var term = EfLikeQueryExtensions.ToLowerSearchTerm(search);
            pairsQuery = pairsQuery.Where(p => p.DisplayName.ToLower().Contains(term));
        }

        var groupedQuery = pairsQuery
            .GroupBy(p => p.DisplayName)
            .Select(g => new GenreAggregateRow
            {
                DisplayName = g.Key,
                MediaCount = g.Select(x => x.Id).Distinct().Count(),
                UserPlayCount = userId.HasValue
                    ? g.Select(x => x.Id).Distinct().Sum(id =>
                        context.UserMediaStates
                            .Where(s => s.UserId == userId.Value && s.MediaId == id)
                            .Select(s => (int?)s.PlayCount)
                            .FirstOrDefault() ?? 0)
                    : 0
            });

        var ordered = ApplyGenreOrdering(orderBy, groupedQuery);
        var totalCount = await ordered.CountAsync(cancellationToken);

        var values = await ordered
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(g => new MediaTagValueDto
            {
                DisplayName = g.DisplayName,
                MediaCount = g.MediaCount
            })
            .ToListAsync(cancellationToken);

        return new MediaTagKindValuesDto
        {
            Kind = MetadataTagKind.Genre,
            Values = values,
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    private static IQueryable<GenrePairProjection> BuildMetadataGenrePairsQuery(IQueryable<BaseMedia> mediasQuery) =>
        mediasQuery
            .SelectMany(m => m.MetadataTags.Where(mt => mt.MetadataTag.Kind == MetadataTagKind.Genre),
                (m, mt) => new GenrePairProjection { Id = m.Id, DisplayName = mt.MetadataTag.DisplayName })
            .Concat(mediasQuery.OfType<SerieSeason>()
                .SelectMany(s => s.Serie.MetadataTags.Where(mt => mt.MetadataTag.Kind == MetadataTagKind.Genre),
                    (s, mt) => new GenrePairProjection { Id = s.Id, DisplayName = mt.MetadataTag.DisplayName }))
            .Concat(mediasQuery.OfType<SerieEpisode>()
                .SelectMany(e => e.Serie.MetadataTags.Where(mt => mt.MetadataTag.Kind == MetadataTagKind.Genre),
                    (e, mt) => new GenrePairProjection { Id = e.Id, DisplayName = mt.MetadataTag.DisplayName }))
            .Concat(mediasQuery.OfType<MusicTrack>()
                .SelectMany(t => t.Album.MetadataTags.Where(mt => mt.MetadataTag.Kind == MetadataTagKind.Genre),
                    (t, mt) => new GenrePairProjection { Id = t.Id, DisplayName = mt.MetadataTag.DisplayName }))
            .Concat(mediasQuery.OfType<MusicArtist>()
                .SelectMany(a => a.Albums, (a, al) => new { a.Id, al })
                .SelectMany(x => x.al.MetadataTags.Where(mt => mt.MetadataTag.Kind == MetadataTagKind.Genre),
                    (x, mt) => new GenrePairProjection { Id = x.Id, DisplayName = mt.MetadataTag.DisplayName }));

    private static IOrderedQueryable<GenreAggregateRow> ApplyGenreOrdering(
        HashSet<MediaTagOrderingOption>? orderBy,
        IQueryable<GenreAggregateRow> queryable)
    {
        if (orderBy is null || orderBy.Count == 0)
            return queryable.OrderByDescending(x => x.MediaCount);

        IOrderedQueryable<GenreAggregateRow>? ordered = null;

        foreach (var order in orderBy)
        {
            ordered = order switch
            {
                MediaTagOrderingOption.MediaCountAsc => ordered is null
                    ? queryable.OrderBy(x => x.MediaCount)
                    : ordered.ThenBy(x => x.MediaCount),
                MediaTagOrderingOption.MediaCountDesc => ordered is null
                    ? queryable.OrderByDescending(x => x.MediaCount)
                    : ordered.ThenByDescending(x => x.MediaCount),
                MediaTagOrderingOption.UserPlayCountAsc => ordered is null
                    ? queryable.OrderBy(x => x.UserPlayCount)
                    : ordered.ThenBy(x => x.UserPlayCount),
                MediaTagOrderingOption.UserPlayCountDesc => ordered is null
                    ? queryable.OrderByDescending(x => x.UserPlayCount)
                    : ordered.ThenByDescending(x => x.UserPlayCount),
                _ => throw new InvalidOperationException($"Unsupported tag ordering option: {order}")
            };
        }

        return ordered ?? queryable.OrderByDescending(x => x.MediaCount);
    }

    private sealed class GenreAggregateRow
    {
        public required string DisplayName { get; init; }
        public int MediaCount { get; init; }
        public int UserPlayCount { get; init; }
    }

    private sealed class GenrePairProjection
    {
        public Guid Id { get; init; }
        public required string DisplayName { get; init; }
    }
}