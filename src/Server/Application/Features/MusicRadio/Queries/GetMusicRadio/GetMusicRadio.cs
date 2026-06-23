using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.Medias.Queries.Common;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Ratings;
using K7.Server.Domain.Enums;
using K7.Shared;

namespace K7.Server.Application.Features.MusicRadio.Queries.GetMusicRadio;

public record GetMusicRadioQuery : IRequest<List<BaseMedia>>
{
    public required MusicRadioType RadioType { get; init; }
    public Guid[]? LibraryIds { get; init; }
    public Guid[]? LibraryGroupIds { get; init; }
    public Guid? SeedTrackId { get; init; }
    public Guid? SeedArtistId { get; init; }
    public string? MoodPreset { get; init; }
    public int? MoodCentroidIndex { get; init; }
    public int Limit { get; init; } = 50;
}

public class GetMusicRadioQueryHandler(
    IApplicationDbContext context,
    IUser currentUser,
    IMusicIntelligenceService musicIntelligenceService)
    : IRequestHandler<GetMusicRadioQuery, List<BaseMedia>>
{
    public async Task<List<BaseMedia>> Handle(GetMusicRadioQuery request, CancellationToken cancellationToken)
    {
        var userId = currentUser.Id;
        var libraryIds = await LibraryGroupFilterHelper.ResolveLibraryIdsAsync(
            context, request.LibraryIds, request.LibraryGroupIds, cancellationToken);

        return request.RadioType switch
        {
            MusicRadioType.Sonic => await GetSonicRadio(request, userId, libraryIds, cancellationToken),
            MusicRadioType.Artist => await GetArtistRadio(request, userId, libraryIds, cancellationToken),
            MusicRadioType.Mood => await GetMoodMix(request, userId, libraryIds, cancellationToken),
            MusicRadioType.Discovery => await GetDiscoveryMix(userId, libraryIds, request.Limit, cancellationToken),
            MusicRadioType.DiscoveryAi => await GetDiscoveryAiMix(userId, libraryIds, request.Limit, cancellationToken),
            MusicRadioType.TimeCapsule => await GetTimeCapsule(userId, libraryIds, request.Limit, cancellationToken),
            MusicRadioType.Tempo => await GetTempoMix(request, userId, libraryIds, cancellationToken),
            MusicRadioType.RecentlyAdded => await GetRecentlyAdded(userId, libraryIds, request.Limit, cancellationToken),
            _ => []
        };
    }

    /// <summary>
    /// Sonic Radio: similar tracks via music intelligence when available.
    /// </summary>
    private async Task<List<BaseMedia>> GetSonicRadio(
        GetMusicRadioQuery request,
        Guid? userId,
        Guid[]? libraryIds,
        CancellationToken ct)
    {
        if (!await musicIntelligenceService.IsAvailableAsync(ct))
            return [];

        var seedId = request.SeedTrackId ?? await PickAutoSeedTrackIdAsync(userId, libraryIds, ct);
        if (seedId is null)
            return [];

        var trackIds = await musicIntelligenceService.GetSimilarTracksAsync(seedId.Value, request.Limit, ct);
        return await LoadTracksByIdsAsync(trackIds, userId, libraryIds, ct);
    }

    /// <summary>
    /// Artist Radio: tracks from the seed artist + tracks from artists in the same genres.
    /// </summary>
    private async Task<List<BaseMedia>> GetArtistRadio(
        GetMusicRadioQuery request,
        Guid? userId,
        Guid[]? libraryIds,
        CancellationToken ct)
    {
        if (request.SeedArtistId is not { } artistId)
            return [];

        var artistGenresQuery = context.Medias
            .OfType<MusicTrack>()
            .Where(t => t.PersonRoles.Any(r => r.PersonId == artistId)
                     || t.Album.PersonRoles.Any(r => r.PersonId == artistId));

        artistGenresQuery = ApplyLibraryFilter(artistGenresQuery, libraryIds);

        var artistGenres = await artistGenresQuery
            .SelectMany(t => t.MetadataTags
                .Where(mt => mt.MetadataTag.Kind == MetadataTagKind.Genre)
                .Select(mt => mt.MetadataTag.DisplayName))
            .Distinct()
            .ToListAsync(ct);

        var artistTracks = await BuildTrackQuery(userId, libraryIds)
            .Where(t => t.PersonRoles.Any(r => r.PersonId == artistId)
                     || t.Album.PersonRoles.Any(r => r.PersonId == artistId))
            .ToListAsync(ct);

        var relatedTracks = await BuildTrackQuery(userId, libraryIds)
            .Where(t => !t.PersonRoles.Any(r => r.PersonId == artistId)
                     && !t.Album.PersonRoles.Any(r => r.PersonId == artistId))
            .Where(t => t.MetadataTags.Any(mt => mt.MetadataTag.Kind == MetadataTagKind.Genre && artistGenres.Contains(mt.MetadataTag.DisplayName))
                     || t.Album.MetadataTags.Any(mt => mt.MetadataTag.Kind == MetadataTagKind.Genre && artistGenres.Contains(mt.MetadataTag.DisplayName)))
            .ToListAsync(ct);

        var artistCount = (int)(request.Limit * 0.6);
        var relatedCount = request.Limit - artistCount;

        var result = Shuffle(artistTracks).Take(artistCount)
            .Concat(Shuffle(relatedTracks).Take(relatedCount))
            .Cast<BaseMedia>()
            .ToList();

        return Shuffle(result).Take(request.Limit).ToList();
    }

    /// <summary>
    /// Mood Mix: mood-based mix via music intelligence when available.
    /// </summary>
    private async Task<List<BaseMedia>> GetMoodMix(
        GetMusicRadioQuery request,
        Guid? userId,
        Guid[]? libraryIds,
        CancellationToken ct)
    {
        if (!await musicIntelligenceService.IsAvailableAsync(ct))
            return [];

        var moodKey = request.MoodPreset ?? "relaxed";
        var centroidIndex = request.MoodCentroidIndex ?? 0;
        var trackIds = await musicIntelligenceService.GetMoodTracksAsync(moodKey, centroidIndex, request.Limit, ct);
        return await LoadTracksByIdsAsync(trackIds, userId, libraryIds, ct);
    }

    /// <summary>
    /// Discovery: never-played tracks in random order; falls back to never-rated when everything was played.
    /// </summary>
    private async Task<List<BaseMedia>> GetDiscoveryMix(Guid? userId, Guid[]? libraryIds, int limit, CancellationToken ct)
    {
        if (userId is null)
        {
            var guestTracks = await BuildTrackQuery(null, libraryIds).ToListAsync(ct);
            return Shuffle(guestTracks).Take(limit).Cast<BaseMedia>().ToList();
        }

        var uid = userId.Value;

        var neverPlayed = await BuildTrackQuery(userId, libraryIds)
            .Where(t => !t.UserMediaStates.Any(s => s.UserId == uid && s.PlayCount > 0))
            .ToListAsync(ct);

        if (neverPlayed.Count > 0)
            return Shuffle(neverPlayed).Take(limit).Cast<BaseMedia>().ToList();

        var neverRated = await BuildTrackQuery(userId, libraryIds)
            .Where(t => !t.Ratings.OfType<UserRating>().Any(r => r.UserId == uid))
            .ToListAsync(ct);

        return Shuffle(neverRated).Take(limit).Cast<BaseMedia>().ToList();
    }

    /// <summary>
    /// Discovery AI: taste-based recommendations, preferring tracks the user has not played yet,
    /// then tracks they have not rated (same fallback as local discovery).
    /// </summary>
    private async Task<List<BaseMedia>> GetDiscoveryAiMix(Guid? userId, Guid[]? libraryIds, int limit, CancellationToken ct)
    {
        if (!await musicIntelligenceService.IsAvailableAsync(ct))
            return [];

        var candidateCount = Math.Clamp(limit * 5, limit, 250);
        var trackIds = await musicIntelligenceService.GetDiscoveryTracksAsync(candidateCount, ct);
        var tracks = await LoadTracksByIdsAsync(trackIds, userId, libraryIds, ct);
        return FilterUnexploredTracks(tracks, trackIds, userId, limit);
    }

    /// <summary>
    /// Time Capsule: tracks the user listened to around the same period last year (+-2 weeks).
    /// </summary>
    private async Task<List<BaseMedia>> GetTimeCapsule(Guid? userId, Guid[]? libraryIds, int limit, CancellationToken ct)
    {
        if (userId is null)
            return [];

        var now = DateTime.UtcNow;
        var windowStart = now.AddYears(-1).AddDays(-14);
        var windowEnd = now.AddYears(-1).AddDays(14);

        var tracks = await BuildTrackQuery(userId, libraryIds)
            .Where(t => t.UserMediaStates.Any(s =>
                s.UserId == userId.Value
                && s.LastInteractedAt >= windowStart
                && s.LastInteractedAt <= windowEnd))
            .ToListAsync(ct);

        if (tracks.Count == 0)
        {
            tracks = await BuildTrackQuery(userId, libraryIds)
                .Where(t => t.UserMediaStates.Any(s => s.UserId == userId.Value && s.PlayCount > 0))
                .OrderByDescending(t => t.UserMediaStates
                    .Where(s => s.UserId == userId.Value)
                    .Select(s => s.LastInteractedAt)
                    .FirstOrDefault())
                .Take(limit)
                .ToListAsync(ct);
        }

        if (tracks.Count < limit / 2)
        {
            var sixMonthStart = now.AddMonths(-6).AddDays(-14);
            var sixMonthEnd = now.AddMonths(-6).AddDays(14);

            var moreTracks = await BuildTrackQuery(userId, libraryIds)
                .Where(t => t.UserMediaStates.Any(s =>
                    s.UserId == userId.Value
                    && s.LastInteractedAt >= sixMonthStart
                    && s.LastInteractedAt <= sixMonthEnd))
                .Where(t => !tracks.Select(x => x.Id).Contains(t.Id))
                .ToListAsync(ct);

            tracks.AddRange(moreTracks);
        }

        return Shuffle(tracks).Take(limit).Cast<BaseMedia>().ToList();
    }

    /// <summary>
    /// Tempo / ambiance mix mapped to danceable mood via music intelligence.
    /// </summary>
    private async Task<List<BaseMedia>> GetTempoMix(
        GetMusicRadioQuery request,
        Guid? userId,
        Guid[]? libraryIds,
        CancellationToken ct)
    {
        if (!await musicIntelligenceService.IsAvailableAsync(ct))
            return [];

        var trackIds = await musicIntelligenceService.GetMoodTracksAsync("danceable", 0, request.Limit, ct);
        return await LoadTracksByIdsAsync(trackIds, userId, libraryIds, ct);
    }

    /// <summary>
    /// Recently Added: newest tracks the user hasn't listened to yet.
    /// </summary>
    private async Task<List<BaseMedia>> GetRecentlyAdded(Guid? userId, Guid[]? libraryIds, int limit, CancellationToken ct)
    {
        var query = BuildTrackQuery(userId, libraryIds);

        if (userId.HasValue)
        {
            query = query.Where(t => !t.UserMediaStates.Any(s => s.UserId == userId.Value && s.PlayCount > 0));
        }

        return await query
            .OrderByDescending(t => t.Created)
            .Take(limit)
            .Cast<BaseMedia>()
            .ToListAsync(ct);
    }

    private async Task<Guid?> PickAutoSeedTrackIdAsync(Guid? userId, Guid[]? libraryIds, CancellationToken ct)
    {
        var query = context.Medias
            .OfType<MusicTrack>()
            .AsNoTracking();

        query = ApplyLibraryFilter(query, libraryIds);

        if (userId.HasValue)
        {
            var topPlayedId = await query
                .Where(t => t.UserMediaStates.Any(s => s.UserId == userId.Value && s.PlayCount > 0))
                .OrderByDescending(t => t.UserMediaStates
                    .Where(s => s.UserId == userId.Value)
                    .Select(s => s.PlayCount)
                    .FirstOrDefault())
                .Select(t => t.Id)
                .FirstOrDefaultAsync(ct);

            if (topPlayedId != Guid.Empty)
                return topPlayedId;
        }

        var candidateIds = await query.Select(t => t.Id).ToListAsync(ct);
        if (candidateIds.Count == 0)
            return null;

        return candidateIds[Random.Shared.Next(candidateIds.Count)];
    }

    private async Task<List<BaseMedia>> LoadTracksByIdsAsync(
        IReadOnlyList<Guid> trackIds,
        Guid? userId,
        Guid[]? libraryIds,
        CancellationToken ct)
    {
        if (trackIds.Count == 0)
            return [];

        var idSet = trackIds.ToHashSet();
        var tracks = await BuildTrackQuery(userId, libraryIds)
            .Where(t => idSet.Contains(t.Id))
            .ToListAsync(ct);

        var trackMap = tracks.ToDictionary(t => t.Id);
        return trackIds
            .Where(trackMap.ContainsKey)
            .Select(id => (BaseMedia)trackMap[id])
            .ToList();
    }

    private IQueryable<MusicTrack> BuildTrackQuery(Guid? userId, Guid[]? libraryIds)
    {
        var query = context.Medias
            .OfType<MusicTrack>()
            .Include(t => t.Pictures).ThenInclude(p => p.Variants)
            .Include(t => t.Ratings)
            .Include(t => t.IndexedFiles).ThenInclude(f => f.FileMetadata)
            .Include(t => t.RemoteIndexedFiles)
            .Include(t => t.MetadataTags).ThenInclude(mt => mt.MetadataTag)
            .Include(t => t.AudioAnalysis)
            .Include(t => t.Artist)
            .Include(t => t.PersonRoles).ThenInclude(r => r.Person)
            .Include(t => t.Album).ThenInclude(a => a.PersonRoles).ThenInclude(r => r.Person)
            .Include(t => t.Album).ThenInclude(a => a.Pictures).ThenInclude(p => p.Variants)
            .Include(t => t.Album).ThenInclude(a => a.MetadataTags).ThenInclude(mt => mt.MetadataTag)
            .Include(t => t.Album).ThenInclude(a => a.Artist)
            .Where(t => t.IndexedFiles.Any() || t.RemoteIndexedFiles.Any())
            .AsSplitQuery()
            .AsNoTracking();

        query = ApplyLibraryFilter(query, libraryIds);

        if (userId.HasValue)
        {
            query = query.Include(t => t.UserMediaStates.Where(s => s.UserId == userId.Value));
        }

        return query;
    }

    private static IQueryable<MusicTrack> ApplyLibraryFilter(IQueryable<MusicTrack> query, Guid[]? libraryIds)
    {
        if (libraryIds is not { Length: > 0 })
            return query;

        return query.Where(t =>
            t.IndexedFiles.Any(f => libraryIds.Contains(f.LibraryId))
            || t.RemoteIndexedFiles.Any(r => libraryIds.Contains(r.LibraryId))
            || t.Album.RemoteIndexedFiles.Any(r => libraryIds.Contains(r.LibraryId))
            || t.Album.Tracks.Any(track =>
                track.IndexedFiles.Any(f => libraryIds.Contains(f.LibraryId))
                || track.RemoteIndexedFiles.Any(r => libraryIds.Contains(r.LibraryId))));
    }

    private static List<BaseMedia> FilterUnexploredTracks(
        List<BaseMedia> loaded,
        IReadOnlyList<Guid> orderedIds,
        Guid? userId,
        int limit)
    {
        var trackMap = loaded.OfType<MusicTrack>().ToDictionary(t => t.Id);
        var ordered = orderedIds
            .Where(trackMap.ContainsKey)
            .Select(id => trackMap[id])
            .ToList();

        if (userId is null)
            return ordered.Take(limit).Cast<BaseMedia>().ToList();

        var uid = userId.Value;

        var neverPlayed = ordered
            .Where(t => !t.UserMediaStates.Any(s => s.UserId == uid && s.PlayCount > 0))
            .ToList();

        if (neverPlayed.Count >= limit)
            return neverPlayed.Take(limit).Cast<BaseMedia>().ToList();

        var neverPlayedIds = neverPlayed.Select(t => t.Id).ToHashSet();
        var neverRated = ordered
            .Where(t => !neverPlayedIds.Contains(t.Id))
            .Where(t => !t.Ratings.OfType<UserRating>().Any(r => r.UserId == uid))
            .ToList();

        return neverPlayed
            .Concat(neverRated)
            .Take(limit)
            .Cast<BaseMedia>()
            .ToList();
    }

    private static List<T> Shuffle<T>(List<T> list)
    {
        var rng = Random.Shared;
        var shuffled = new List<T>(list);
        for (var i = shuffled.Count - 1; i > 0; i--)
        {
            var j = rng.Next(i + 1);
            (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
        }
        return shuffled;
    }

    private static IEnumerable<T> Shuffle<T>(IEnumerable<T> source) => Shuffle(source.ToList());
}
