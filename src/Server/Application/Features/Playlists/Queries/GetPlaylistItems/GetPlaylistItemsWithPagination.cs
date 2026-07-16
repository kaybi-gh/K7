using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Mappings;
using K7.Server.Application.Common.Models;
using K7.Server.Application.Common.Security;
using K7.Server.Application.Common.Exceptions;
using K7.Server.Application.Features.Restrictions.Services;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Playlists;

namespace K7.Server.Application.Features.Playlists.Queries.GetPlaylistItems;

[Authorize(Roles = $"{Roles.Guest},{Roles.User},{Roles.Administrator}")]
public record GetPlaylistItemsWithPaginationQuery : IRequest<PaginatedList<PlaylistItem>>
{
    public required Guid PlaylistId { get; init; }
    public required int PageNumber { get; init; } = 1;
    public required int PageSize { get; init; } = PagingDefaults.DefaultPageSize;
}

public class GetPlaylistItemsWithPaginationQueryHandler(IApplicationDbContext context, IUser currentUser)
    : IRequestHandler<GetPlaylistItemsWithPaginationQuery, PaginatedList<PlaylistItem>>
{
    public async Task<PaginatedList<PlaylistItem>> Handle(GetPlaylistItemsWithPaginationQuery request, CancellationToken cancellationToken)
    {
        var userId = currentUser.Id;
        if (userId is null)
            throw new ForbiddenAccessException();

        var playlist = await context.Playlists
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == request.PlaylistId && p.UserId == userId.Value, cancellationToken);

        Guard.Against.NotFound(request.PlaylistId, playlist);

        var query = context.PlaylistItems
            .Include(i => i.Media)
                .ThenInclude(m => m.Pictures)
                    .ThenInclude(p => p.Variants)
            .Include(i => i.Media)
                .ThenInclude(m => m.IndexedFiles)
            .Include(i => i.Media)
                .ThenInclude(m => m.PersonRoles)
                    .ThenInclude(r => r.Person)
            .Include(i => i.Media)
                .ThenInclude(m => m.Ratings)
            .Include(i => (i.Media as MusicTrack)!.Album)
                .ThenInclude(a => a!.PersonRoles)
                    .ThenInclude(r => r.Person)
            .Include(i => (i.Media as MusicTrack)!.Album)
                .ThenInclude(a => a!.Pictures)
                    .ThenInclude(p => p.Variants)
            .Include(i => (i.Media as MusicTrack)!.AudioAnalysis)
            .Where(i => i.PlaylistId == request.PlaylistId)
            .OrderBy(i => i.Order)
            .AsNoTracking();

        if (userId is { } currentUserId)
        {
            var excludedLibraryIds = context.UserLibraryExclusions
                .Where(e => e.UserId == currentUserId && (e.IsAdminExcluded || e.IsSelfExcluded))
                .Select(e => e.LibraryId);

            var excludedMediaIds = context.UserMediaExclusions
                .Where(e => e.UserId == currentUserId && (e.IsAdminExcluded || e.IsSelfExcluded))
                .Select(e => e.MediaId);

            query = query
                .Where(i => !excludedMediaIds.Contains(i.MediaId))
                .Where(i => !i.Media.IndexedFiles.All(f => excludedLibraryIds.Contains(f.LibraryId)));

            var restrictionProfile = await context.ContentRestrictionProfiles
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Users.Any(u => u.Id == currentUserId), cancellationToken);

            if (restrictionProfile is not null)
            {
                var restrictedMediaIds = ContentRestrictionEvaluator.GetRestricted(
                    context.Medias.AsNoTracking(), restrictionProfile)
                    .Select(m => m.Id);

                query = query.Where(i => !restrictedMediaIds.Contains(i.MediaId));
            }
        }

        return await query.AsSplitQuery().PaginatedListAsync(request.PageNumber, request.PageSize);
    }
}
