using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.Restrictions.Services;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Restrictions;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.Services;

public interface IMediaAccessGuard
{
    Task EnsureAccessAsync(Guid mediaId, CancellationToken cancellationToken = default);
    Task EnsureAccessByIndexedFileAsync(Guid indexedFileId, CancellationToken cancellationToken = default);
    Task<bool> CanAccessAsync(Guid mediaId, Guid userId, CancellationToken cancellationToken = default);
}

public class MediaAccessGuard(IApplicationDbContext context, IUser currentUser) : IMediaAccessGuard
{
    public async Task EnsureAccessAsync(Guid mediaId, CancellationToken cancellationToken = default)
    {
        if (currentUser.Id is not { } userId)
            return;

        if (!await CanAccessAsync(mediaId, userId, cancellationToken))
            throw new NotFoundException(mediaId.ToString(), nameof(BaseMedia));
    }

    public async Task<bool> CanAccessAsync(Guid mediaId, Guid userId, CancellationToken cancellationToken = default)
    {
        var check = await context.Medias
            .AsNoTracking()
            .Where(m => m.Id == mediaId)
            .Select(m => new
            {
                IsMediaExcluded = context.UserMediaExclusions
                    .Any(e => e.UserId == userId && e.MediaId == mediaId && (e.IsAdminExcluded || e.IsSelfExcluded)),
                HasNonExcludedFile = !m.IndexedFiles.Any()
                    || m.IndexedFiles.Any(f => !context.UserLibraryExclusions
                        .Any(e => e.UserId == userId && e.LibraryId == f.LibraryId && (e.IsAdminExcluded || e.IsSelfExcluded)))
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (check is null)
            return false;

        if (check.IsMediaExcluded || !check.HasNonExcludedFile)
            return false;

        var profile = await ResolveRestrictionProfileAsync(userId, cancellationToken);
        if (profile is null)
            return true;

        return !await ContentRestrictionEvaluator.GetRestricted(
            context.Medias.AsNoTracking().Where(m => m.Id == mediaId), profile)
            .AnyAsync(cancellationToken);
    }

    public async Task EnsureAccessByIndexedFileAsync(Guid indexedFileId, CancellationToken cancellationToken = default)
    {
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

    private async Task<ContentRestrictionProfile?> ResolveRestrictionProfileAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var sharedProfileId = await currentUser.GetSharedProfileIdAsync(cancellationToken);
        if (sharedProfileId is { } profileId)
        {
            return await context.SharedProfiles
                .AsNoTracking()
                .Where(p => p.Id == profileId)
                .Select(p => p.ContentRestrictionProfile)
                .FirstOrDefaultAsync(cancellationToken);
        }

        return await context.ContentRestrictionProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Users.Any(u => u.Id == userId), cancellationToken);
    }
}
