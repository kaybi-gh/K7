using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Enums;

namespace K7.Server.Application.Helpers;

public static class IntroOutroDiagnosticHelper
{
    public sealed record EpisodeIntroOutroCandidate(Guid EpisodeId, Guid SeasonId, Guid LibraryId);

    /// <summary>
    /// Episodes in intro-detection-enabled libraries whose season is detection-eligible
    /// (2+ episodes with existing files) and that have neither Intro nor Outro segments.
    /// </summary>
    public static async Task<List<EpisodeIntroOutroCandidate>> GetMissingIntroOutroEpisodesAsync(
        IApplicationDbContext context,
        Guid? libraryId,
        IReadOnlyCollection<Guid>? limitToEpisodeIds = null,
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
                file.LibraryId,
                Path = file.Path!
            };

        if (libraryId.HasValue)
            episodeRows = episodeRows.Where(x => x.LibraryId == libraryId.Value);

        if (limitToEpisodeIds is not null)
            episodeRows = episodeRows.Where(x => limitToEpisodeIds.Contains(x.EpisodeId));

        var rows = await episodeRows.ToListAsync(cancellationToken);
        var existing = rows.Where(r => File.Exists(r.Path)).ToList();

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

        var episodesWithSegments = await context.MediaSegments
            .AsNoTracking()
            .Where(s => candidateEpisodeIds.Contains(s.MediaId)
                && (s.Type == MediaSegmentType.Intro || s.Type == MediaSegmentType.Outro))
            .Select(s => s.MediaId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var withSegments = episodesWithSegments.ToHashSet();

        return existing
            .Where(r => eligibleSeasonIds.Contains(r.SeasonId) && !withSegments.Contains(r.EpisodeId))
            .GroupBy(r => r.EpisodeId)
            .Select(g =>
            {
                var first = g.First();
                return new EpisodeIntroOutroCandidate(first.EpisodeId, first.SeasonId, first.LibraryId);
            })
            .ToList();
    }

    public static async Task<HashSet<Guid>> GetMissingIntroOutroEpisodeIdsAsync(
        IApplicationDbContext context,
        Guid? libraryId,
        IReadOnlyCollection<Guid>? limitToEpisodeIds = null,
        CancellationToken cancellationToken = default)
    {
        var candidates = await GetMissingIntroOutroEpisodesAsync(
            context, libraryId, limitToEpisodeIds, cancellationToken);
        return candidates.Select(c => c.EpisodeId).ToHashSet();
    }

    public static async Task<Dictionary<Guid, int>> GetMissingIntroOutroCountsByLibraryAsync(
        IApplicationDbContext context,
        CancellationToken cancellationToken = default)
    {
        var candidates = await GetMissingIntroOutroEpisodesAsync(
            context, libraryId: null, limitToEpisodeIds: null, cancellationToken);
        return candidates
            .GroupBy(c => c.LibraryId)
            .ToDictionary(g => g.Key, g => g.Count());
    }
}
