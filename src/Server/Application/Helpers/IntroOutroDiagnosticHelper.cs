using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Enums;

namespace K7.Server.Application.Helpers;

public static class IntroOutroDiagnosticHelper
{
    public sealed record EpisodeIntroOutroCandidate(
        Guid EpisodeId,
        Guid SeasonId,
        Guid LibraryId,
        IReadOnlyList<string> MissingTypes);

    /// <summary>
    /// Episodes in intro-detection-enabled libraries whose season is detection-eligible
    /// (2+ episodes with existing files) and that are missing Intro and/or Outro.
    /// Episodes the current detection already analysed are excluded: nothing more to find.
    /// Series with <see cref="MediaLockFields.IntroOutro"/> locked are excluded.
    /// </summary>
    /// <param name="verifyOnDisk">
    /// When true (default), each indexed episode path is checked with <see cref="File.Exists"/>.
    /// When false, indexed paths are trusted (use for summary counts during library scans).
    /// </param>
    public static async Task<List<EpisodeIntroOutroCandidate>> GetMissingIntroOutroEpisodesAsync(
        IApplicationDbContext context,
        Guid? libraryId,
        IReadOnlyCollection<Guid>? limitToEpisodeIds = null,
        bool verifyOnDisk = true,
        CancellationToken cancellationToken = default)
    {
        var episodeRows =
            from episode in context.Medias.OfType<SerieEpisode>().AsNoTracking()
            join file in context.IndexedFiles.AsNoTracking() on episode.Id equals file.MediaId
            where file.Path != null && file.FileMetadata != null
            join library in context.Libraries.AsNoTracking() on file.LibraryId equals library.Id
            where library.PeerServerId == null
                && library.MediaType == LibraryMediaType.Serie
                && library.IntroDetectionEnabled
            select new
            {
                EpisodeId = episode.Id,
                episode.SeasonId,
                episode.SerieId,
                episode.SegmentDetectionVersion,
                file.LibraryId,
                Path = file.Path!
            };

        if (libraryId.HasValue)
            episodeRows = episodeRows.Where(x => x.LibraryId == libraryId.Value);

        // A diagnostic page slice may contain only one episode per season. Eligibility still
        // needs the full season (2+ files), so expand to those seasons before filtering back.
        HashSet<Guid>? limitSet = null;
        if (limitToEpisodeIds is not null)
        {
            limitSet = limitToEpisodeIds as HashSet<Guid> ?? limitToEpisodeIds.ToHashSet();
            if (limitSet.Count == 0)
                return [];

            var seasonIds = await context.Medias
                .OfType<SerieEpisode>()
                .AsNoTracking()
                .Where(e => limitSet.Contains(e.Id))
                .Select(e => e.SeasonId)
                .Distinct()
                .ToListAsync(cancellationToken);

            if (seasonIds.Count == 0)
                return [];

            episodeRows = episodeRows.Where(x => seasonIds.Contains(x.SeasonId));
        }

        var rows = await episodeRows.ToListAsync(cancellationToken);
        var existing = verifyOnDisk
            ? rows.Where(r => File.Exists(r.Path)).ToList()
            : rows;

        var lockedSerieIds = await LoadIntroOutroLockedSerieIdsAsync(
            context,
            existing.Select(r => r.SerieId).Distinct().ToList(),
            cancellationToken);

        existing = existing.Where(r => !lockedSerieIds.Contains(r.SerieId)).ToList();

        var eligibleSeasonIds = existing
            .GroupBy(r => r.SeasonId)
            .Where(g => g.Select(x => x.Path).Distinct().Count() >= 2)
            .Select(g => g.Key)
            .ToHashSet();

        if (eligibleSeasonIds.Count == 0)
            return [];

        var candidateEpisodeIds = existing
            .Where(r => eligibleSeasonIds.Contains(r.SeasonId))
            .Select(r => r.EpisodeId)
            .Distinct()
            .ToList();

        var segmentRows = await context.MediaSegments
            .AsNoTracking()
            .Where(s => candidateEpisodeIds.Contains(s.MediaId)
                && (s.Type == MediaSegmentType.Intro || s.Type == MediaSegmentType.Outro))
            .Select(s => new { s.MediaId, s.Type })
            .ToListAsync(cancellationToken);

        var typesByEpisode = segmentRows
            .GroupBy(s => s.MediaId)
            .ToDictionary(g => g.Key, g => (IReadOnlyCollection<MediaSegmentType>)g.Select(x => x.Type).ToHashSet());

        var candidates = existing
            .Where(r => eligibleSeasonIds.Contains(r.SeasonId))
            .Where(r => !IntroOutroSegmentCompleteness.IsSettled(r.SegmentDetectionVersion))
            .GroupBy(r => r.EpisodeId)
            .Select(g => g.First())
            .Select(r =>
            {
                var present = typesByEpisode.GetValueOrDefault(r.EpisodeId, []);
                var missing = IntroOutroSegmentCompleteness.GetMissingTypeNames(present);
                return (Row: r, Missing: missing);
            })
            .Where(x => x.Missing.Count > 0)
            .Select(x => new EpisodeIntroOutroCandidate(
                x.Row.EpisodeId,
                x.Row.SeasonId,
                x.Row.LibraryId,
                x.Missing))
            .ToList();

        if (limitSet is not null)
            candidates = candidates.Where(c => limitSet.Contains(c.EpisodeId)).ToList();

        return candidates;
    }

    public static async Task<HashSet<Guid>> GetMissingIntroOutroEpisodeIdsAsync(
        IApplicationDbContext context,
        Guid? libraryId,
        IReadOnlyCollection<Guid>? limitToEpisodeIds = null,
        bool verifyOnDisk = true,
        CancellationToken cancellationToken = default)
    {
        var candidates = await GetMissingIntroOutroEpisodesAsync(
            context, libraryId, limitToEpisodeIds, verifyOnDisk, cancellationToken);
        return candidates.Select(c => c.EpisodeId).ToHashSet();
    }

    public static async Task<Dictionary<Guid, IReadOnlyList<string>>> GetMissingIntroOutroTypesByEpisodeAsync(
        IApplicationDbContext context,
        Guid? libraryId,
        IReadOnlyCollection<Guid>? limitToEpisodeIds = null,
        bool verifyOnDisk = true,
        CancellationToken cancellationToken = default)
    {
        var candidates = await GetMissingIntroOutroEpisodesAsync(
            context, libraryId, limitToEpisodeIds, verifyOnDisk, cancellationToken);
        return candidates.ToDictionary(c => c.EpisodeId, c => c.MissingTypes);
    }

    public static async Task<Dictionary<Guid, int>> GetMissingIntroOutroCountsByLibraryAsync(
        IApplicationDbContext context,
        CancellationToken cancellationToken = default)
    {
        var candidates = await GetMissingIntroOutroEpisodesAsync(
            context,
            libraryId: null,
            limitToEpisodeIds: null,
            verifyOnDisk: false,
            cancellationToken);
        return candidates
            .GroupBy(c => c.LibraryId)
            .ToDictionary(g => g.Key, g => g.Count());
    }

    public static async Task<HashSet<Guid>> LoadIntroOutroLockedSerieIdsAsync(
        IApplicationDbContext context,
        IReadOnlyCollection<Guid> serieIds,
        CancellationToken cancellationToken = default)
    {
        if (serieIds.Count == 0)
            return [];

        var rows = await context.Medias
            .OfType<Serie>()
            .AsNoTracking()
            .Where(s => serieIds.Contains(s.Id))
            .Select(s => new { s.Id, s.LockedFields })
            .ToListAsync(cancellationToken);

        return rows
            .Where(s => s.LockedFields.Contains(MediaLockFields.IntroOutro))
            .Select(s => s.Id)
            .ToHashSet();
    }
}
