using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Entities;

namespace K7.Server.Application.Helpers;

/// <summary>
/// Read-only detection of probable duplicate medias.
///
/// Media creation is a find-or-create over a fuzzy identity (provider external id lookup,
/// then title/artist/year or folder consensus). A database unicity constraint is not possible:
/// provider ids change over time and two distinct medias can legitimately share title + year.
/// The per-identity lock (IMediaIdentityLock) strongly reduces the duplicate probability but
/// cannot eliminate it, because two different identity keys can designate the same media.
/// Detection is therefore the safety net: it flags suspicious medias without ever rejecting
/// or merging anything (merging is a separate, deliberately postponed effort).
///
/// Two heuristics, both translated to SQL (Postgres and Sqlite):
/// - DuplicateExternalId: two medias share the same (ProviderName, Value) external id. This is
///   the most reliable signal. A local media and its federated copy can legitimately carry the
///   same provider id, so only medias with the same PeerServerId are compared.
/// - SuspectedDuplicateMedia: two medias of the same type share a normalized title (trim +
///   case-insensitive) and release year within the same library. Noisier (homonym movies do
///   exist), hence surfaced with a lower severity as a suspicion, not a certainty.
/// </summary>
public static class DuplicateMediaDiagnosticHelper
{
    /// <summary>
    /// Ids of medias sharing an external id (ProviderName, Value) with another media of the
    /// same PeerServerId. Correlated EXISTS (semi-join) rather than a self-join to avoid
    /// materializing a cartesian product; the database can serve it from the external id index.
    /// </summary>
    public static IQueryable<Guid> QueryDuplicateExternalIdMediaIds(IApplicationDbContext context) =>
        context.Medias
            .AsNoTracking()
            .Where(m => m.ExternalIds.Any(e =>
                context.ExternalIds.Any(other =>
                    other.MediaId != null
                    && other.MediaId != m.Id
                    && other.ProviderName == e.ProviderName
                    && other.Value == e.Value
                    && other.Media!.PeerServerId == m.PeerServerId)))
            .Select(m => m.Id);

    /// <summary>
    /// Ids of medias sharing type + normalized title + release year with another media
    /// available in the same (local) library. Lower-confidence signal.
    /// </summary>
    public static IQueryable<Guid> QuerySuspectedDuplicateMediaIds(
        IApplicationDbContext context,
        Guid? libraryId)
    {
        var availability = LocalAvailability(context, libraryId);

        return context.Medias
            .AsNoTracking()
            .Where(m => m.Title != null && m.ReleaseDate != null)
            .Where(m => availability.Any(a => a.MediaId == m.Id
                && availability.Any(other => other.LibraryId == a.LibraryId
                    && other.MediaId != m.Id
                    && context.Medias.Any(o => o.Id == other.MediaId
                        && o.Type == m.Type
                        && o.Title != null
                        && o.ReleaseDate != null
                        && o.Title!.Trim().ToLower() == m.Title!.Trim().ToLower()
                        && o.ReleaseDate!.Value.Year == m.ReleaseDate!.Value.Year))))
            .Select(m => m.Id);
    }

    public static async Task<HashSet<Guid>> GetDuplicateExternalIdMediaIdsAsync(
        IApplicationDbContext context,
        IReadOnlyCollection<Guid>? limitToMediaIds,
        CancellationToken cancellationToken = default)
    {
        var query = QueryDuplicateExternalIdMediaIds(context);

        if (limitToMediaIds is not null)
            query = query.Where(id => limitToMediaIds.Contains(id));

        var ids = await query.ToListAsync(cancellationToken);
        return ids.ToHashSet();
    }

    public static async Task<HashSet<Guid>> GetSuspectedDuplicateMediaIdsAsync(
        IApplicationDbContext context,
        Guid? libraryId,
        IReadOnlyCollection<Guid>? limitToMediaIds,
        CancellationToken cancellationToken = default)
    {
        var query = QuerySuspectedDuplicateMediaIds(context, libraryId);

        if (limitToMediaIds is not null)
            query = query.Where(id => limitToMediaIds.Contains(id));

        var ids = await query.ToListAsync(cancellationToken);
        return ids.ToHashSet();
    }

    public static Task<Dictionary<Guid, int>> GetDuplicateExternalIdCountsByLibraryAsync(
        IApplicationDbContext context,
        CancellationToken cancellationToken = default) =>
        CountByLibraryAsync(context, QueryDuplicateExternalIdMediaIds(context), cancellationToken);

    public static Task<Dictionary<Guid, int>> GetSuspectedDuplicateCountsByLibraryAsync(
        IApplicationDbContext context,
        CancellationToken cancellationToken = default) =>
        CountByLibraryAsync(context, QuerySuspectedDuplicateMediaIds(context, libraryId: null), cancellationToken);

    private static IQueryable<MediaLibraryAvailability> LocalAvailability(
        IApplicationDbContext context,
        Guid? libraryId)
    {
        var availability = context.MediaLibraryAvailabilities
            .AsNoTracking()
            .Where(a => !context.Libraries.Any(l => l.Id == a.LibraryId && l.PeerServerId != null));

        if (libraryId.HasValue)
            availability = availability.Where(a => a.LibraryId == libraryId.Value);

        return availability;
    }

    private static async Task<Dictionary<Guid, int>> CountByLibraryAsync(
        IApplicationDbContext context,
        IQueryable<Guid> flaggedMediaIds,
        CancellationToken cancellationToken)
    {
        var counts = await LocalAvailability(context, libraryId: null)
            .Where(a => flaggedMediaIds.Contains(a.MediaId))
            .Select(a => new { a.LibraryId, a.MediaId })
            .Distinct()
            .GroupBy(x => x.LibraryId)
            .Select(g => new { LibraryId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        return counts.ToDictionary(x => x.LibraryId, x => x.Count);
    }
}
