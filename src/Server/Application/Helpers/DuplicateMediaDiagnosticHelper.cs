using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Enums;

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
/// - SuspectedDuplicateMedia: two top-level medias of the same type share a normalized title
///   (trim + case-insensitive) and release year within the same library. Scoped to Movie /
///   Serie / MusicAlbum so generic episode/track titles cannot explode into a cartesian product.
/// </summary>
public static class DuplicateMediaDiagnosticHelper
{
    /// <summary>
    /// DateOnly legal range. Postgres accepts wider dates (including +/-infinity); extracting
    /// their year via date_part(...)::int throws 22003 (integer out of range / dtoi4).
    /// </summary>
    private static readonly DateOnly MinReleaseDate = new(1, 1, 1);
    private static readonly DateOnly MaxReleaseDate = new(9999, 12, 31);

    private static readonly MediaType[] SuspectedDuplicateMediaTypes =
    [
        MediaType.Movie,
        MediaType.Serie,
        MediaType.MusicAlbum
    ];

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
    /// Ids of top-level medias sharing type + normalized title + release year with another media
    /// available in the same (local) library. Lower-confidence signal.
    /// </summary>
    public static IQueryable<Guid> QuerySuspectedDuplicateMediaIds(
        IApplicationDbContext context,
        Guid? libraryId) =>
        QuerySuspectedDuplicatePairs(context, libraryId)
            .Select(p => p.MediaId)
            .Distinct();

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

    public static async Task<Dictionary<Guid, int>> GetSuspectedDuplicateCountsByLibraryAsync(
        IApplicationDbContext context,
        CancellationToken cancellationToken = default)
    {
        // Count directly from (LibraryId, MediaId) pairs so we do not re-wrap the self-join in
        // an IN (...) against availability (which re-planned poorly and resurfaced 22003).
        var counts = await QuerySuspectedDuplicatePairs(context, libraryId: null)
            .Distinct()
            .GroupBy(p => p.LibraryId)
            .Select(g => new { LibraryId = g.Key, Count = g.LongCount() })
            .ToListAsync(cancellationToken);

        return counts.ToDictionary(
            x => x.LibraryId,
            x => x.Count > int.MaxValue ? int.MaxValue : (int)x.Count);
    }

    private static IQueryable<SuspectedDuplicatePairProjection> QuerySuspectedDuplicatePairs(
        IApplicationDbContext context,
        Guid? libraryId)
    {
        var availability = LocalAvailability(context, libraryId);

        var keyed =
            from a in availability
            join m in context.Medias.AsNoTracking() on a.MediaId equals m.Id
            where SuspectedDuplicateMediaTypes.Contains(m.Type)
                && m.Title != null
                && m.ReleaseDate != null
                && m.ReleaseDate >= MinReleaseDate
                && m.ReleaseDate <= MaxReleaseDate
            select new
            {
                a.LibraryId,
                MediaId = m.Id,
                m.Type,
                Title = m.Title!.Trim().ToLower(),
                // long so Npgsql emits date_part::bigint instead of ::int (dtoi4 22003).
                Year = (long)m.ReleaseDate!.Value.Year
            };

        return from left in keyed
               join right in keyed
                   on new { left.LibraryId, left.Type, left.Title, left.Year }
                   equals new { right.LibraryId, right.Type, right.Title, right.Year }
               where left.MediaId != right.MediaId
               select new SuspectedDuplicatePairProjection
               {
                   LibraryId = left.LibraryId,
                   MediaId = left.MediaId
               };
    }

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
        // LongCount so a pathological library cannot throw Npgsql 22003 via count(*)::int.
        var counts = await LocalAvailability(context, libraryId: null)
            .Where(a => flaggedMediaIds.Contains(a.MediaId))
            .Select(a => new { a.LibraryId, a.MediaId })
            .Distinct()
            .GroupBy(x => x.LibraryId)
            .Select(g => new { LibraryId = g.Key, Count = g.LongCount() })
            .ToListAsync(cancellationToken);

        return counts.ToDictionary(
            x => x.LibraryId,
            x => x.Count > int.MaxValue ? int.MaxValue : (int)x.Count);
    }

    private sealed class SuspectedDuplicatePairProjection
    {
        public Guid LibraryId { get; set; }
        public Guid MediaId { get; set; }
    }
}
