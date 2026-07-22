using K7.Server.Application.Common.Configuration;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Enums;

namespace K7.Server.Application.Helpers;

public static class ThemeSongDiagnosticHelper
{
    public sealed record SerieThemeCandidate(Guid SerieId, Guid LibraryId, string EpisodePath);

    /// <summary>
    /// Series in libraries with intro+theme generation enabled that have at least one
    /// detection-eligible season (2+ episodes with existing indexed files). Does not require
    /// an Intro segment to already exist.
    /// </summary>
    public static async Task<List<SerieThemeCandidate>> GetEligibleSerieCandidatesAsync(
        IApplicationDbContext context,
        Guid? libraryId,
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
                && library.ThemeSongGenerationEnabled
            select new
            {
                episode.SerieId,
                episode.SeasonId,
                file.LibraryId,
                Path = file.Path!
            };

        if (libraryId.HasValue)
            episodeRows = episodeRows.Where(x => x.LibraryId == libraryId.Value);

        var rows = await episodeRows.ToListAsync(cancellationToken);

        var existing = rows.Where(r => File.Exists(r.Path)).ToList();

        var eligibleSeasonIds = existing
            .GroupBy(r => r.SeasonId)
            .Where(g => g.Select(x => x.Path).Distinct().Count() >= 2)
            .Select(g => g.Key)
            .ToHashSet();

        return existing
            .Where(r => eligibleSeasonIds.Contains(r.SeasonId))
            .GroupBy(r => r.SerieId)
            .Select(g =>
            {
                var first = g.First();
                return new SerieThemeCandidate(g.Key, first.LibraryId, first.Path);
            })
            .ToList();
    }

    public static bool IsMissingTheme(PathsConfiguration paths, Guid serieId, string episodePath)
    {
        var serieRoot = ThemeSongLocator.ResolveSerieRootFromEpisodePath(episodePath);
        if (ThemeSongLocator.FindLibrarySidecar(serieRoot) is not null)
            return false;

        return !File.Exists(ThemeSongLocator.GetGeneratedPath(paths, serieId));
    }

    public static async Task<HashSet<Guid>> GetMissingThemeSerieIdsAsync(
        IApplicationDbContext context,
        PathsConfiguration paths,
        Guid? libraryId,
        IReadOnlyCollection<Guid>? limitToSerieIds,
        CancellationToken cancellationToken = default)
    {
        var candidates = await GetEligibleSerieCandidatesAsync(context, libraryId, cancellationToken);
        if (limitToSerieIds is not null)
            candidates = candidates.Where(c => limitToSerieIds.Contains(c.SerieId)).ToList();

        return candidates
            .Where(c => IsMissingTheme(paths, c.SerieId, c.EpisodePath))
            .Select(c => c.SerieId)
            .ToHashSet();
    }

    public static async Task<Dictionary<Guid, int>> GetMissingThemeCountsByLibraryAsync(
        IApplicationDbContext context,
        PathsConfiguration paths,
        CancellationToken cancellationToken = default)
    {
        var candidates = await GetEligibleSerieCandidatesAsync(context, libraryId: null, cancellationToken);
        return candidates
            .Where(c => IsMissingTheme(paths, c.SerieId, c.EpisodePath))
            .GroupBy(c => c.LibraryId)
            .ToDictionary(g => g.Key, g => g.Count());
    }

    public static async Task<List<Guid>> GetEligibleSeasonIdsForSerieAsync(
        IApplicationDbContext context,
        Guid serieId,
        CancellationToken cancellationToken = default)
    {
        var rows = await (
            from episode in context.Medias.OfType<SerieEpisode>().AsNoTracking()
            where episode.SerieId == serieId
            join file in context.IndexedFiles.AsNoTracking() on episode.Id equals file.MediaId
            where file.Path != null && file.FileMetadata != null
            select new { episode.SeasonId, Path = file.Path! }
        ).ToListAsync(cancellationToken);

        return rows
            .Where(r => File.Exists(r.Path))
            .GroupBy(r => r.SeasonId)
            .Where(g => g.Select(x => x.Path).Distinct().Count() >= 2)
            .Select(g => g.Key)
            .ToList();
    }

    public static async Task<bool> SerieHasIntroAsync(
        IApplicationDbContext context,
        Guid serieId,
        CancellationToken cancellationToken = default)
    {
        return await (
            from episode in context.Medias.OfType<SerieEpisode>().AsNoTracking()
            where episode.SerieId == serieId
            join segment in context.MediaSegments.AsNoTracking() on episode.Id equals segment.MediaId
            where segment.Type == MediaSegmentType.Intro
            select episode.Id
        ).AnyAsync(cancellationToken);
    }
}
