using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.QueryExtensions;
using K7.Server.Application.Features.Restrictions.Services;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Restrictions;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.Common.Services;

/// <summary>
/// Centralizes the per-user media visibility filtering shared by the media list, search and home feed
/// queries: library exclusions, media exclusions and content restriction profiles. Keeping this logic
/// in one place avoids the predicates drifting apart between endpoints.
/// </summary>
public sealed class MediaAccessFilter(IApplicationDbContext context)
{
    /// <summary>
    /// Applies the library-level and media-level exclusions for the given user. Does not apply the
    /// content restriction profile (see <see cref="ApplyAllAsync"/> or <see cref="GetRestrictionProfileAsync"/>).
    /// </summary>
    public IQueryable<BaseMedia> ApplyExclusions(IQueryable<BaseMedia> query, Guid userId)
    {
        var excludedLibraryIds = context.UserLibraryExclusions
            .Where(e => e.UserId == userId && (e.IsAdminExcluded || e.IsSelfExcluded))
            .Select(e => e.LibraryId);

        query = query.WhereAvailableOutsideExcludedLibraries(context, excludedLibraryIds);

        var excludedMediaIds = context.UserMediaExclusions
            .Where(e => e.UserId == userId && (e.IsAdminExcluded || e.IsSelfExcluded))
            .Select(e => e.MediaId);

        return query.WhereNotUserExcluded(excludedMediaIds);
    }

    public IQueryable<Guid> GetAccessibleMediaIds(Guid userId) =>
        ApplyExclusions(context.Medias, userId).Select(m => m.Id);

    public async Task<ContentRestrictionProfile?> GetRestrictionProfileAsync(
        Guid userId,
        CancellationToken cancellationToken = default) =>
        await context.ContentRestrictionProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Users.Any(u => u.Id == userId), cancellationToken);

    /// <summary>
    /// Applies exclusions and the content restriction profile in one pass.
    /// </summary>
    public async Task<IQueryable<BaseMedia>> ApplyAllAsync(
        IQueryable<BaseMedia> query,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        query = ApplyExclusions(query, userId);

        var restrictionProfile = await GetRestrictionProfileAsync(userId, cancellationToken);
        if (restrictionProfile is not null)
            query = ContentRestrictionEvaluator.ApplyRestriction(query, restrictionProfile);

        return query;
    }
}
