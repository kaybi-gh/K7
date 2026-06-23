using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.Medias.Queries.Common;
using K7.Server.Domain.Entities.Medias;
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
    public int Limit { get; init; } = 50;
}

public class GetMusicRadioQueryHandler(IApplicationDbContext context, IUser currentUser)
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
            MusicRadioType.TimeCapsule => await GetTimeCapsule(userId, libraryIds, request.Limit, cancellationToken),
            MusicRadioType.Tempo => await GetTempoMix(request, userId, libraryIds, cancellationToken),
            MusicRadioType.RecentlyAdded => await GetRecentlyAdded(userId, libraryIds, request.Limit, cancellationToken),
            _ => []
        };
    }

    /// <summary>
    /// Sonic Radio: from a seed track, find tracks with similar audio profile.
    /// Distance is computed on Energy, Danceability, Valence (euclidean) + BPM proximity + key compatibility.
    /// </summary>
    private async Task<List<BaseMedia>> GetSonicRadio(
        GetMusicRadioQuery request,
        Guid? userId,
        Guid[]? libraryIds,
        CancellationToken ct)
    {
        var seedId = request.SeedTrackId ?? await PickAutoSeedTrackIdAsync(userId, libraryIds, ct);
        if (seedId is null)
            return [];

        var seedQuery = context.Medias
            .OfType<MusicTrack>()
            .Include(t => t.AudioAnalysis)
            .AsNoTracking()
            .Where(t => t.Id == seedId.Value);

        seedQuery = ApplyLibraryFilter(seedQuery, libraryIds);

        var seed = await seedQuery.FirstOrDefaultAsync(ct);

        if (seed?.AudioAnalysis is not { } seedAnalysis)
            return [];

        var candidates = await BuildTrackQuery(userId, libraryIds)
            .Where(t => t.Id != seedId)
            .Where(t => t.AudioAnalysis != null)
            .Include(t => t.AudioAnalysis)
            .ToListAsync(ct);

        return candidates
            .Select(t => new { Track = t, Distance = ComputeSonicDistance(seedAnalysis, t.AudioAnalysis!) })
            .OrderBy(x => x.Distance)
            .Take(request.Limit)
            .Select(x => (BaseMedia)x.Track)
            .ToList();
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
    /// Mood Mix: filter tracks by Energy/Valence/Danceability ranges based on preset name.
    /// Presets: chill, energetic, happy, dark, focus
    /// </summary>
    private async Task<List<BaseMedia>> GetMoodMix(
        GetMusicRadioQuery request,
        Guid? userId,
        Guid[]? libraryIds,
        CancellationToken ct)
    {
        var (energyMin, energyMax, valenceMin, valenceMax, danceMin, danceMax) = GetMoodRanges(request.MoodPreset);

        var tracks = await BuildTrackQuery(userId, libraryIds)
            .Where(t => t.AudioAnalysis != null)
            .Where(t => t.AudioAnalysis!.Energy >= energyMin && t.AudioAnalysis!.Energy <= energyMax)
            .Where(t => t.AudioAnalysis!.Valence >= valenceMin && t.AudioAnalysis!.Valence <= valenceMax)
            .Where(t => t.AudioAnalysis!.Danceability >= danceMin && t.AudioAnalysis!.Danceability <= danceMax)
            .ToListAsync(ct);

        return Shuffle(tracks).Take(request.Limit).Cast<BaseMedia>().ToList();
    }

    /// <summary>
    /// Discovery Mix: tracks the user has never listened to, sonically close to their most played tracks.
    /// </summary>
    private async Task<List<BaseMedia>> GetDiscoveryMix(Guid? userId, Guid[]? libraryIds, int limit, CancellationToken ct)
    {
        if (userId is null)
            return [];

        var topTracksQuery = context.Medias
            .OfType<MusicTrack>()
            .Include(t => t.AudioAnalysis)
            .Where(t => t.AudioAnalysis != null)
            .Where(t => t.UserMediaStates.Any(s => s.UserId == userId.Value && s.PlayCount > 0))
            .OrderByDescending(t => t.UserMediaStates
                .Where(s => s.UserId == userId.Value)
                .Select(s => s.PlayCount)
                .FirstOrDefault())
            .Take(20)
            .AsNoTracking()
            .AsQueryable();

        topTracksQuery = ApplyLibraryFilter(topTracksQuery, libraryIds);

        var topTracks = await topTracksQuery.ToListAsync(ct);

        if (topTracks.Count == 0)
            return await GetRecentlyAdded(userId, libraryIds, limit, ct);

        var avgProfile = ComputeAverageProfile(topTracks.Select(t => t.AudioAnalysis!).ToList());

        var candidates = await BuildTrackQuery(userId, libraryIds)
            .Where(t => t.AudioAnalysis != null)
            .Where(t => !t.UserMediaStates.Any(s => s.UserId == userId.Value && s.PlayCount > 0))
            .Include(t => t.AudioAnalysis)
            .ToListAsync(ct);

        return candidates
            .Select(t => new { Track = t, Distance = ComputeSonicDistance(avgProfile, t.AudioAnalysis!) })
            .OrderBy(x => x.Distance)
            .Take(limit)
            .Select(x => (BaseMedia)x.Track)
            .ToList();
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
    /// Tempo Mix: tracks within +-5% BPM of the seed track, or a fixed BPM if no seed.
    /// Ordered by BPM proximity for smooth DJ-style progression.
    /// </summary>
    private async Task<List<BaseMedia>> GetTempoMix(
        GetMusicRadioQuery request,
        Guid? userId,
        Guid[]? libraryIds,
        CancellationToken ct)
    {
        double? targetBpm = null;

        if (request.SeedTrackId is { } seedId)
        {
            targetBpm = await context.Medias
                .OfType<MusicTrack>()
                .Where(t => t.Id == seedId && t.AudioAnalysis != null)
                .Select(t => t.AudioAnalysis!.Bpm)
                .FirstOrDefaultAsync(ct);
        }
        else
        {
            var autoSeedId = await PickAutoSeedTrackIdAsync(userId, libraryIds, ct);
            if (autoSeedId.HasValue)
            {
                targetBpm = await context.Medias
                    .OfType<MusicTrack>()
                    .Where(t => t.Id == autoSeedId.Value && t.AudioAnalysis != null)
                    .Select(t => t.AudioAnalysis!.Bpm)
                    .FirstOrDefaultAsync(ct);
            }
        }

        targetBpm ??= 120;

        var bpmMin = targetBpm.Value * 0.95;
        var bpmMax = targetBpm.Value * 1.05;

        var tracks = await BuildTrackQuery(userId, libraryIds)
            .Where(t => t.AudioAnalysis != null && t.AudioAnalysis.Bpm >= bpmMin && t.AudioAnalysis.Bpm <= bpmMax)
            .Include(t => t.AudioAnalysis)
            .ToListAsync(ct);

        if (tracks.Count == 0)
        {
            tracks = await BuildTrackQuery(userId, libraryIds)
                .Where(t => t.AudioAnalysis != null && t.AudioAnalysis.Bpm.HasValue)
                .Include(t => t.AudioAnalysis)
                .OrderBy(t => Math.Abs(t.AudioAnalysis!.Bpm!.Value - targetBpm.Value))
                .Take(request.Limit)
                .ToListAsync(ct);
        }

        if (tracks.Count == 0)
            return await GetRecentlyAdded(userId, libraryIds, request.Limit, ct);

        return tracks
            .OrderBy(t => Math.Abs(t.AudioAnalysis!.Bpm!.Value - targetBpm.Value))
            .Take(request.Limit)
            .Cast<BaseMedia>()
            .ToList();
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
            .Where(t => t.AudioAnalysis != null)
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

    private static double ComputeSonicDistance(AudioAnalysis a, AudioAnalysis b)
    {
        double distance = 0;

        distance += FeatureDistance(a.Energy, b.Energy);
        distance += FeatureDistance(a.Danceability, b.Danceability);
        distance += FeatureDistance(a.Valence, b.Valence);

        if (a.Bpm.HasValue && b.Bpm.HasValue)
            distance += Math.Pow(Math.Min(Math.Abs(a.Bpm.Value - b.Bpm.Value) / 50.0, 1.0), 2);
        else
            distance += 0.25;

        if (a.MusicalKey is not null && b.MusicalKey is not null
            && CamelotWheel.AreKeysCompatible(a.MusicalKey, b.MusicalKey))
        {
            distance *= 0.8;
        }

        return Math.Sqrt(distance);
    }

    private static double FeatureDistance(double? a, double? b)
    {
        if (a.HasValue && b.HasValue)
            return Math.Pow(a.Value - b.Value, 2);
        return 0.1;
    }

    private static AudioAnalysis ComputeAverageProfile(List<AudioAnalysis> analyses)
    {
        return new AudioAnalysis
        {
            Energy = analyses.Where(a => a.Energy.HasValue).Select(a => a.Energy!.Value).DefaultIfEmpty(0.5).Average(),
            Danceability = analyses.Where(a => a.Danceability.HasValue).Select(a => a.Danceability!.Value).DefaultIfEmpty(0.5).Average(),
            Valence = analyses.Where(a => a.Valence.HasValue).Select(a => a.Valence!.Value).DefaultIfEmpty(0.5).Average(),
            Bpm = analyses.Where(a => a.Bpm.HasValue).Select(a => a.Bpm!.Value).DefaultIfEmpty(120).Average(),
            MusicalKey = analyses.GroupBy(a => a.MusicalKey).OrderByDescending(g => g.Count()).FirstOrDefault()?.Key
        };
    }

    private static (double EnergyMin, double EnergyMax, double ValenceMin, double ValenceMax, double DanceMin, double DanceMax)
        GetMoodRanges(string? preset)
    {
        return (preset?.ToLowerInvariant()) switch
        {
            "chill" => (0.0, 0.4, 0.2, 0.7, 0.0, 0.5),
            "energetic" => (0.6, 1.0, 0.3, 1.0, 0.5, 1.0),
            "happy" => (0.4, 1.0, 0.6, 1.0, 0.4, 1.0),
            "dark" => (0.2, 0.7, 0.0, 0.3, 0.0, 0.6),
            "focus" => (0.2, 0.6, 0.3, 0.6, 0.1, 0.4),
            _ => (0.0, 1.0, 0.0, 1.0, 0.0, 1.0)
        };
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
