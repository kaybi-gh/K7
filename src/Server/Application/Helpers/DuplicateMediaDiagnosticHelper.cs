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
/// Two heuristics, both translated to SQL (Postgres and Sqlite) then grouped in process:
/// - DuplicateExternalId: two medias share the same (ProviderName, Value) external id. This is
///   the most reliable signal. A local media and its federated copy can legitimately carry the
///   same provider id, so only medias with the same PeerServerId are compared.
/// - SuspectedDuplicateMedia: two top-level medias of the same type share a normalized title
///   (trim + case-insensitive) and release year within the same library. Scoped to Movie /
///   Serie / MusicAlbum so generic episode/track titles cannot form huge groups. Candidates
///   are loaded as a slim projection (indexes on Type / Title / ReleaseDate stay usable);
///   trim, case-fold and year are applied in process so Postgres never groups on
///   lower(btrim(title)) / date_part (unindexable and previously timed out).
/// </summary>
public static class DuplicateMediaDiagnosticHelper
{
    /// <summary>
    /// DateOnly legal range. Postgres accepts wider dates (including +/-infinity); extracting
    /// their year via date_part(...)::int throws 22003 (integer out of range / dtoi4).
    /// Filtering here keeps infinity dates out of the in-memory year extraction.
    /// </summary>
    private static readonly DateOnly MinReleaseDate = new(1, 1, 1);
    private static readonly DateOnly MaxReleaseDate = new(9999, 12, 31);

    private static readonly MediaType[] SuspectedDuplicateMediaTypes =
    [
        MediaType.Movie,
        MediaType.Serie,
        MediaType.MusicAlbum
    ];

    public static async Task<HashSet<Guid>> GetDuplicateExternalIdMediaIdsAsync(
        IApplicationDbContext context,
        IReadOnlyCollection<Guid>? limitToMediaIds,
        CancellationToken cancellationToken = default)
    {
        var ids = await LoadDuplicateExternalIdMediaIdsAsync(context, cancellationToken);

        if (limitToMediaIds is not null)
            ids.IntersectWith(limitToMediaIds);

        return ids;
    }

    public static async Task<HashSet<Guid>> GetSuspectedDuplicateMediaIdsAsync(
        IApplicationDbContext context,
        Guid? libraryId,
        IReadOnlyCollection<Guid>? limitToMediaIds,
        CancellationToken cancellationToken = default)
    {
        var flagged = await LoadSuspectedDuplicatePairsAsync(context, libraryId, cancellationToken);
        var ids = flagged.Select(p => p.MediaId).ToHashSet();

        if (limitToMediaIds is not null)
            ids.IntersectWith(limitToMediaIds);

        return ids;
    }

    public static async Task<Dictionary<Guid, int>> GetDuplicateExternalIdCountsByLibraryAsync(
        IApplicationDbContext context,
        CancellationToken cancellationToken = default)
    {
        var ids = await LoadDuplicateExternalIdMediaIdsAsync(context, cancellationToken);
        if (ids.Count == 0)
            return [];

        return await CountByLibraryAsync(
            context,
            context.Medias.AsNoTracking().Where(m => ids.Contains(m.Id)).Select(m => m.Id),
            cancellationToken);
    }

    private static async Task<HashSet<Guid>> LoadDuplicateExternalIdMediaIdsAsync(
        IApplicationDbContext context,
        CancellationToken cancellationToken)
    {
        // Slim projection + in-process grouping: a correlated EXISTS self-scan of ExternalIds
        // does not stay on the (ProviderName, Value) index once PeerServerId is pulled in.
        var rows = await context.ExternalIds
            .AsNoTracking()
            .Where(e => e.MediaId != null)
            .Select(e => new
            {
                e.ProviderName,
                e.Value,
                MediaId = e.MediaId!.Value,
                e.Media!.PeerServerId
            })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(r => (r.ProviderName, r.Value, r.PeerServerId))
            .Where(g => g.Select(x => x.MediaId).Distinct().Count() > 1)
            .SelectMany(g => g.Select(x => x.MediaId))
            .ToHashSet();
    }

    public static async Task<Dictionary<Guid, int>> GetSuspectedDuplicateCountsByLibraryAsync(
        IApplicationDbContext context,
        CancellationToken cancellationToken = default)
    {
        var flagged = await LoadSuspectedDuplicatePairsAsync(context, libraryId: null, cancellationToken);

        return flagged
            .GroupBy(p => p.LibraryId)
            .ToDictionary(g => g.Key, g => g.Count());
    }

    private static async Task<List<SuspectedDuplicatePair>> LoadSuspectedDuplicatePairsAsync(
        IApplicationDbContext context,
        Guid? libraryId,
        CancellationToken cancellationToken)
    {
        // Slim join only: no SQL lower/trim/date_part, so Type / Title / ReleaseDate indexes
        // remain usable. Grouping stays in process (movie / serie / album rows only).
        var query =
            from a in context.MediaLibraryAvailabilities.AsNoTracking()
            join library in context.Libraries.AsNoTracking() on a.LibraryId equals library.Id
            where library.PeerServerId == null
            join media in context.Medias.AsNoTracking() on a.MediaId equals media.Id
            where SuspectedDuplicateMediaTypes.Contains(media.Type)
                && media.Title != null
                && media.ReleaseDate != null
                && media.ReleaseDate >= MinReleaseDate
                && media.ReleaseDate <= MaxReleaseDate
            select new
            {
                a.LibraryId,
                MediaId = media.Id,
                media.Type,
                Title = media.Title!,
                ReleaseDate = media.ReleaseDate!.Value
            };

        if (libraryId.HasValue)
            query = query.Where(r => r.LibraryId == libraryId.Value);

        var rows = await query.ToListAsync(cancellationToken);
        if (rows.Count == 0)
            return [];

        return rows
            .GroupBy(r => (r.LibraryId, r.Type, Title: NormalizeTitle(r.Title), Year: r.ReleaseDate.Year))
            .Where(g => g.Count() > 1)
            .SelectMany(g => g)
            .Select(r => new SuspectedDuplicatePair(r.LibraryId, r.MediaId))
            .Distinct()
            .ToList();
    }

    private static string NormalizeTitle(string title) => title.Trim().ToLowerInvariant();

    private static IQueryable<MediaLibraryAvailability> LocalAvailability(
        IApplicationDbContext context,
        Guid? libraryId)
    {
        var availability =
            from a in context.MediaLibraryAvailabilities.AsNoTracking()
            join library in context.Libraries.AsNoTracking() on a.LibraryId equals library.Id
            where library.PeerServerId == null
            select a;

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

    private readonly record struct SuspectedDuplicatePair(Guid LibraryId, Guid MediaId);
}
