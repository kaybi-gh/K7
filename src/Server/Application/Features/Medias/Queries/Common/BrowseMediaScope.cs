using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Models;
using K7.Server.Application.Common.QueryExtensions;
using K7.Server.Application.Features.Medias.Queries.GetMedias;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Enums;

namespace K7.Server.Application.Features.Medias.Queries.Common;

internal static class BrowseMediaScope
{
    internal static async Task<IQueryable<Guid>> GetMediaIdsAsync(
        IApplicationDbContext context,
        Guid[]? libraryIds,
        Guid[]? libraryGroupIds,
        EnumHashSetQueryParam<MediaType>? mediaTypes,
        Guid? userId,
        bool? unwatchedOnly,
        CancellationToken cancellationToken)
    {
        var mediasQuery = await GetMediasQueryAsync(
            context, libraryIds, libraryGroupIds, mediaTypes, userId, unwatchedOnly, cancellationToken);
        return mediasQuery.Select(m => m.Id);
    }

    internal static async Task<IQueryable<BaseMedia>> GetMediasQueryAsync(
        IApplicationDbContext context,
        Guid[]? libraryIds,
        Guid[]? libraryGroupIds,
        EnumHashSetQueryParam<MediaType>? mediaTypes,
        Guid? userId,
        bool? unwatchedOnly,
        CancellationToken cancellationToken)
    {
        libraryIds = await LibraryGroupFilterHelper.ResolveLibraryIdsAsync(
            context, libraryIds, libraryGroupIds, cancellationToken);

        var mediasQuery = context.Medias.AsNoTracking().AsQueryable();
        mediasQuery = ApplyMediaFilters(context, libraryIds, mediaTypes, unwatchedOnly, mediasQuery, userId);

        if (userId.HasValue)
        {
            var excludedLibraryIds = context.UserLibraryExclusions
                .Where(e => e.UserId == userId.Value && (e.IsAdminExcluded || e.IsSelfExcluded))
                .Select(e => e.LibraryId);

            mediasQuery = mediasQuery.WhereAvailableOutsideExcludedLibraries(context, excludedLibraryIds);
        }

        return mediasQuery;
    }

    private static IQueryable<BaseMedia> ApplyMediaFilters(
        IApplicationDbContext context,
        Guid[]? libraryIds,
        EnumHashSetQueryParam<MediaType>? mediaTypes,
        bool? unwatchedOnly,
        IQueryable<BaseMedia> query,
        Guid? userId)
    {
        var paginationRequest = new GetMediasWithPaginationQuery
        {
            LibraryIds = libraryIds,
            MediaTypes = mediaTypes,
            UnwatchedOnly = unwatchedOnly,
            PageNumber = 1,
            PageSize = 1
        };

        return GetMediasQueryHandler.ApplyFilters(context, paginationRequest, query, userId);
    }
}
