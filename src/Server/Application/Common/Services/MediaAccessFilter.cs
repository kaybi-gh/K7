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

        query = query.Where(x =>
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

        var excludedMediaIds = context.UserMediaExclusions
            .Where(e => e.UserId == userId && (e.IsAdminExcluded || e.IsSelfExcluded))
            .Select(e => e.MediaId);

        return query.WhereNotUserExcluded(excludedMediaIds);
    }

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
