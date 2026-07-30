using K7.Server.Application.Common.Extensions;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Mappings;
using K7.Server.Application.Common.Models;
using K7.Server.Application.Common.Services;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities.Playlists;
using K7.Shared.Dtos.Requests;

namespace K7.Server.Application.Features.Playlists.Queries.GetPlaylists;

public record GetPlaylistsWithPaginationQuery : IRequest<PaginatedList<LitePlaylistDto>>
{
    public required int PageNumber { get; init; } = 1;
    public required int PageSize { get; init; } = PagingDefaults.DefaultPageSize;
    public MediaType? MediaType { get; init; }
    public LibraryItemOrderingOption? OrderBy { get; init; }
}

public class GetPlaylistsWithPaginationQueryHandler(IApplicationDbContext context, IUser currentUser)
    : IRequestHandler<GetPlaylistsWithPaginationQuery, PaginatedList<LitePlaylistDto>>
{
    public async Task<PaginatedList<LitePlaylistDto>> Handle(
        GetPlaylistsWithPaginationQuery request,
        CancellationToken cancellationToken)
    {
        var pageSize = PagingDefaults.ClampPageSize(request.PageSize);
        var pageNumber = PagingDefaults.ClampPageNumber(request.PageNumber);

        if (currentUser.Id is not { } userId)
            return new PaginatedList<LitePlaylistDto>([], 0, pageNumber, pageSize);

        var query = context.Playlists
            .Include(p => p.UserStates.Where(s => s.UserId == userId))
            .Include(p => p.CoverPicture)
                .ThenInclude(c => c!.Variants)
            .AsQueryable();

        var sharedProfileId = await currentUser.GetSharedProfileIdAsync(cancellationToken);
        if (sharedProfileId is { } profileId)
        {
            query = query.Where(p =>
                p.UserId == userId
                || context.SharedProfilePlaylists.Any(sp =>
                    sp.SharedProfileId == profileId && sp.PlaylistId == p.Id));
        }
        else
        {
            query = query.Where(p => p.UserId == userId);
        }

        if (request.MediaType.HasValue)
            query = query.Where(p => p.MediaType == request.MediaType.Value);

        query = query
            .ApplyOrdering(request.OrderBy, userId)
            .AsNoTracking();

        var page = await query.PaginatedListAsync(pageNumber, pageSize, cancellationToken);
        if (page.Items.Count == 0)
            return new PaginatedList<LitePlaylistDto>([], page.TotalCount, page.PageNumber, pageSize);

        var playlistIds = page.Items.Select(p => p.Id).ToList();
        var itemCounts = await PlaylistLiteProjectionHelper.GetItemCountsByPlaylistIdAsync(
            context,
            playlistIds,
            cancellationToken);

        var playlistIdsNeedingPreviews = page.Items
            .Where(p => p.CoverPicture is null)
            .Select(p => p.Id)
            .ToList();
        var previewPictures = await PlaylistLiteProjectionHelper.GetPreviewPicturesByPlaylistIdAsync(
            context,
            playlistIdsNeedingPreviews,
            cancellationToken);

        var dtos = page.Items
            .Select(p => p.ToLitePlaylistDto(
                itemCounts.GetValueOrDefault(p.Id),
                previewPictures.GetValueOrDefault(p.Id) ?? []))
            .ToList();

        return new PaginatedList<LitePlaylistDto>(dtos, page.TotalCount, page.PageNumber, pageSize);
    }
}
