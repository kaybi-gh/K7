using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.QueryExtensions;
using K7.Server.Application.Common.Services;
using K7.Server.Application.Features.Restrictions.Services;
using K7.Server.Domain.Entities.Medias;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.Services;

public interface IMediaAccessGuard
{
    Task EnsureAccessAsync(Guid mediaId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Same gates as <see cref="EnsureAccessAsync"/> except a user media exclusion.
    /// Unhide must reach the handler while the row is still excluded.
    /// </summary>
    Task EnsureAccessIgnoringMediaExclusionAsync(Guid mediaId, CancellationToken cancellationToken = default);
    Task EnsureAccessByIndexedFileAsync(Guid indexedFileId, CancellationToken cancellationToken = default);
    Task<bool> CanAccessAsync(Guid mediaId, Guid userId, CancellationToken cancellationToken = default);
}

public class MediaAccessGuard(
    IApplicationDbContext context,
    IUser currentUser,
    MediaAccessFilter mediaAccessFilter,
    IShareVisitorAccess shareVisitorAccess)
    : IMediaAccessGuard
{
    public Task EnsureAccessAsync(Guid mediaId, CancellationToken cancellationToken = default) =>
        EnsureAccessCoreAsync(mediaId, ignoreMediaExclusion: false, cancellationToken);

    public Task EnsureAccessIgnoringMediaExclusionAsync(Guid mediaId, CancellationToken cancellationToken = default) =>
        EnsureAccessCoreAsync(mediaId, ignoreMediaExclusion: true, cancellationToken);

    public Task<bool> CanAccessAsync(Guid mediaId, Guid userId, CancellationToken cancellationToken = default) =>
        CanAccessCoreAsync(mediaId, userId, ignoreMediaExclusion: false, useCurrentSharedProfile: true, cancellationToken);

    private async Task EnsureAccessCoreAsync(Guid mediaId, bool ignoreMediaExclusion, CancellationToken cancellationToken)
    {
        // A share visitor never falls through to the regular user rules: it only sees
        // what one of its shares covers.
        if (shareVisitorAccess.IsShareVisitor)
        {
            if (!await CanShareVisitorAccessAsync(mediaId, cancellationToken))
                throw new NotFoundException(mediaId.ToString(), nameof(BaseMedia));

            return;
        }

        if (currentUser.Id is not { } userId)
            return;

        if (!await CanAccessCoreAsync(mediaId, userId, ignoreMediaExclusion, useCurrentSharedProfile: true, cancellationToken))
            throw new NotFoundException(mediaId.ToString(), nameof(BaseMedia));
    }

    /// <summary>
    /// A visitor reaches a media when one of its shares covers it and the user who created
    /// that share can still access it. Sharing never gives more than its creator has.
    /// </summary>
    private async Task<bool> CanShareVisitorAccessAsync(Guid mediaId, CancellationToken cancellationToken)
    {
        var matches = await shareVisitorAccess.GetMatchingSharesAsync(mediaId, cancellationToken);
        foreach (var creatorUserId in matches.Select(m => m.CreatedByUserId).Distinct())
        {
            if (await CanAccessCoreAsync(
                    mediaId,
                    creatorUserId,
                    ignoreMediaExclusion: false,
                    useCurrentSharedProfile: false,
                    cancellationToken))
            {
                return true;
            }
        }

        return false;
    }

    private async Task<bool> CanAccessCoreAsync(
        Guid mediaId,
        Guid userId,
        bool ignoreMediaExclusion,
        bool useCurrentSharedProfile,
        CancellationToken cancellationToken)
    {
        var check = await context.Medias
            .AsNoTracking()
            .Where(m => m.Id == mediaId)
            .Select(m => new
            {
                IsMediaExcluded = context.UserMediaExclusions
                    .Any(e => e.UserId == userId && e.MediaId == mediaId && (e.IsAdminExcluded || e.IsSelfExcluded))
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (check is null)
            return false;

        if (!ignoreMediaExclusion && check.IsMediaExcluded)
            return false;

        var excludedLibraryIds = context.UserLibraryExclusions
            .Where(e => e.UserId == userId && (e.IsAdminExcluded || e.IsSelfExcluded))
            .Select(e => e.LibraryId);

        var hasLibraryAccess = await context.Medias
            .AsNoTracking()
            .Where(m => m.Id == mediaId)
            .WhereLibraryVisibleToUser(context, excludedLibraryIds)
            .AnyAsync(cancellationToken);

        if (!hasLibraryAccess)
            return false;

        var sharedProfileId = useCurrentSharedProfile
            ? await currentUser.GetSharedProfileIdAsync(cancellationToken)
            : null;
        var gates = await mediaAccessFilter.GetContentGatesAsync(userId, sharedProfileId, cancellationToken);
        var mediaQuery = context.Medias.AsNoTracking().Where(m => m.Id == mediaId);

        if (gates.RestrictionProfile is not null
            && await ContentRestrictionEvaluator.GetRestricted(mediaQuery, gates.RestrictionProfile)
                .AnyAsync(cancellationToken))
            return false;

        if (gates.AgeGate is not null
            && !await mediaAccessFilter.ApplyAgeRestriction(mediaQuery, gates.AgeGate)
                .AnyAsync(cancellationToken))
            return false;

        return true;
    }

    public async Task EnsureAccessByIndexedFileAsync(Guid indexedFileId, CancellationToken cancellationToken = default)
    {
        if (shareVisitorAccess.IsShareVisitor)
        {
            var fileMediaId = await context.IndexedFiles
                .AsNoTracking()
                .Where(f => f.Id == indexedFileId)
                .Select(f => f.MediaId)
                .FirstOrDefaultAsync(cancellationToken);

            // A file without a media is in no share: deny instead of the lenient user rule.
            if (fileMediaId is not { } sharedMediaId
                || !await CanShareVisitorAccessAsync(sharedMediaId, cancellationToken))
            {
                throw new NotFoundException(indexedFileId.ToString(), "IndexedFile");
            }

            return;
        }

        if (currentUser.Id is not { } userId)
            return;

        var check = await context.IndexedFiles
            .AsNoTracking()
            .Where(f => f.Id == indexedFileId)
            .Select(f => new
            {
                f.MediaId,
                IsLibraryExcluded = context.UserLibraryExclusions
                    .Any(e => e.UserId == userId && e.LibraryId == f.LibraryId && (e.IsAdminExcluded || e.IsSelfExcluded)),
                IsMediaExcluded = f.MediaId != null && context.UserMediaExclusions
                    .Any(e => e.UserId == userId && e.MediaId == f.MediaId && (e.IsAdminExcluded || e.IsSelfExcluded))
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (check is null)
            return;

        if (check.IsLibraryExcluded)
            throw new NotFoundException(indexedFileId.ToString(), "IndexedFile");

        if (check.IsMediaExcluded)
            throw new NotFoundException(indexedFileId.ToString(), "IndexedFile");

        if (check.MediaId is not { } mediaId)
            return;

        if (!await CanAccessAsync(mediaId, userId, cancellationToken))
            throw new NotFoundException(indexedFileId.ToString(), "IndexedFile");
    }
}
