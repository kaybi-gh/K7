using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.Restrictions.Services;
using K7.Server.Domain.Entities.Medias;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.Services;

public interface IMediaAccessGuard
{
    Task EnsureAccessAsync(Guid mediaId, CancellationToken cancellationToken = default);
    Task EnsureAccessByIndexedFileAsync(Guid indexedFileId, CancellationToken cancellationToken = default);
}

public class MediaAccessGuard(IApplicationDbContext context, IUser currentUser) : IMediaAccessGuard
{
    public async Task EnsureAccessAsync(Guid mediaId, CancellationToken cancellationToken = default)
    {
        if (currentUser.Id is not { } userId)
            return;

        var isExcluded = await context.UserMediaExclusions
            .AnyAsync(e => e.UserId == userId && e.MediaId == mediaId, cancellationToken);

        if (isExcluded)
            throw new NotFoundException(mediaId.ToString(), nameof(BaseMedia));

        var media = await context.Medias
            .AsNoTracking()
            .Include(m => m.IndexedFiles)
            .FirstOrDefaultAsync(m => m.Id == mediaId, cancellationToken);

        if (media is null)
            return;

        await EnsureNotLibraryExcluded(media, userId, cancellationToken);
        await EnsureNotContentRestricted(media, userId, cancellationToken);
    }

    public async Task EnsureAccessByIndexedFileAsync(Guid indexedFileId, CancellationToken cancellationToken = default)
    {
        if (currentUser.Id is not { } userId)
            return;

        var indexedFile = await context.IndexedFiles
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == indexedFileId, cancellationToken);

        if (indexedFile is null)
            return;

        var excludedLibraryIds = await context.UserLibraryExclusions
            .Where(e => e.UserId == userId)
            .Select(e => e.LibraryId)
            .ToListAsync(cancellationToken);

        if (excludedLibraryIds.Contains(indexedFile.LibraryId))
            throw new NotFoundException(indexedFileId.ToString(), "IndexedFile");

        if (indexedFile.MediaId is not { } mediaId)
            return;

        var isExcluded = await context.UserMediaExclusions
            .AnyAsync(e => e.UserId == userId && e.MediaId == mediaId, cancellationToken);

        if (isExcluded)
            throw new NotFoundException(indexedFileId.ToString(), "IndexedFile");

        var media = await context.Medias
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == mediaId, cancellationToken);

        if (media is not null)
            await EnsureNotContentRestricted(media, userId, cancellationToken);
    }

    private async Task EnsureNotLibraryExcluded(BaseMedia media, Guid userId, CancellationToken cancellationToken)
    {
        var excludedLibraryIds = await context.UserLibraryExclusions
            .Where(e => e.UserId == userId)
            .Select(e => e.LibraryId)
            .ToListAsync(cancellationToken);

        if (excludedLibraryIds.Count == 0)
            return;

        var allFilesExcluded = media.IndexedFiles.Count > 0
            && media.IndexedFiles.All(f => excludedLibraryIds.Contains(f.LibraryId));

        if (allFilesExcluded)
            throw new NotFoundException(media.Id.ToString(), nameof(BaseMedia));
    }

    private async Task EnsureNotContentRestricted(BaseMedia media, Guid userId, CancellationToken cancellationToken)
    {
        var profile = await context.ContentRestrictionProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Users.Any(u => u.Id == userId), cancellationToken);

        if (profile is null)
            return;

        var restricted = ContentRestrictionEvaluator.GetRestricted(
            context.Medias.AsNoTracking().Where(m => m.Id == media.Id),
            profile);

        if (await restricted.AnyAsync(cancellationToken))
            throw new NotFoundException(media.Id.ToString(), nameof(BaseMedia));
    }
}
