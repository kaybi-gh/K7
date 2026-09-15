using K7.Server.Application.Common;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Enums;
using K7.Shared.CustomNav;
using K7.Shared.Dtos.CustomNav;

namespace K7.Server.Application.Features.CustomNav;

internal static class CustomNavAccess
{
    public static async Task<HashSet<Guid>> GetAccessibleLibraryGroupIdsAsync(
        IApplicationDbContext context,
        Guid? userId,
        CancellationToken cancellationToken)
    {
        var query = context.LibraryGroups.AsNoTracking().AsQueryable();

        if (userId is { } id)
        {
            var excludedLibraryIds = context.UserLibraryExclusions
                .Where(e => e.UserId == id && (e.IsAdminExcluded || e.IsSelfExcluded))
                .Select(e => e.LibraryId);

            query = query.Where(g => g.Libraries.Any(l => !excludedLibraryIds.Contains(l.Id)));
        }

        return await query.Select(g => g.Id).ToHashSetAsync(cancellationToken);
    }

    public static async Task<CustomNavLayoutDto> SanitizeAsync(
        IApplicationDbContext context,
        IIdentityService identityService,
        CustomNavLayoutDto layout,
        Guid? userId,
        CancellationToken cancellationToken)
    {
        var groupIds = await GetAccessibleLibraryGroupIdsAsync(context, userId, cancellationToken);
        var collectionIds = await GetAccessibleCollectionIdsAsync(context, userId, cancellationToken);
        var playlistIds = await GetAccessiblePlaylistIdsAsync(context, userId, cancellationToken);
        var allowAdmin = userId is { } id
            && await UserCapabilityEvaluator.HasAsync(
                context,
                identityService,
                id,
                Capability.CanAccessAdmin,
                cancellationToken);
        return CustomNavSanitizer.Sanitize(layout, groupIds, collectionIds, playlistIds, allowAdmin);
    }

    public static async Task<HashSet<Guid>> GetAccessibleCollectionIdsAsync(
        IApplicationDbContext context,
        Guid? userId,
        CancellationToken cancellationToken)
    {
        if (userId is not { } id)
            return [];

        return await context.Collections.AsNoTracking()
            .Where(c => c.UserId == id || c.IsPublic)
            .Select(c => c.Id)
            .ToHashSetAsync(cancellationToken);
    }

    public static async Task<HashSet<Guid>> GetAccessiblePlaylistIdsAsync(
        IApplicationDbContext context,
        Guid? userId,
        CancellationToken cancellationToken)
    {
        if (userId is not { } id)
            return [];

        return await context.Playlists.AsNoTracking()
            .Where(p => p.UserId == id)
            .Select(p => p.Id)
            .ToHashSetAsync(cancellationToken);
    }
}
