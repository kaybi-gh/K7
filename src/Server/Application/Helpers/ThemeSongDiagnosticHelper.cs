using K7.Server.Application.Common.Configuration;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Enums;

namespace K7.Server.Application.Helpers;

public static class ThemeSongDiagnosticHelper
{
    public sealed record SerieThemeCandidate(Guid SerieId, Guid LibraryId, string EpisodePath);

    public static async Task<List<SerieThemeCandidate>> GetCandidatesWithIntroAsync(
        IApplicationDbContext context,
        Guid? libraryId,
        CancellationToken cancellationToken = default)
    {
        var query =
            from episode in context.Medias.OfType<SerieEpisode>().AsNoTracking()
            join segment in context.MediaSegments.AsNoTracking() on episode.Id equals segment.MediaId
            where segment.Type == MediaSegmentType.Intro
            join file in context.IndexedFiles.AsNoTracking() on episode.Id equals file.MediaId
            where file.Path != null
            join library in context.Libraries.AsNoTracking() on file.LibraryId equals library.Id
            where library.PeerServerId == null
                && library.MediaType == LibraryMediaType.Serie
                && library.IntroDetectionEnabled
                && library.ThemeSongGenerationEnabled
            select new { episode.SerieId, file.LibraryId, Path = file.Path! };

        if (libraryId.HasValue)
            query = query.Where(x => x.LibraryId == libraryId.Value);

        var rows = await query.ToListAsync(cancellationToken);

        return rows
            .GroupBy(x => x.SerieId)
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
        var candidates = await GetCandidatesWithIntroAsync(context, libraryId, cancellationToken);
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
        var candidates = await GetCandidatesWithIntroAsync(context, libraryId: null, cancellationToken);
        return candidates
            .Where(c => IsMissingTheme(paths, c.SerieId, c.EpisodePath))
            .GroupBy(c => c.LibraryId)
            .ToDictionary(g => g.Key, g => g.Count());
    }
}
