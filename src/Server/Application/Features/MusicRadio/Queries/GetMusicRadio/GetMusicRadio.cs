using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Enums;

namespace K7.Server.Application.Features.MusicRadio.Queries.GetMusicRadio;

public record GetMusicRadioQuery : IRequest<List<BaseMedia>>
{
    public required MusicRadioType Type { get; init; }
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

        return request.Type switch
        {
            MusicRadioType.Sonic => await GetSonicRadio(request, userId, cancellationToken),
            MusicRadioType.Artist => await GetArtistRadio(request, userId, cancellationToken),
            MusicRadioType.Mood => await GetMoodMix(request, userId, cancellationToken),
            MusicRadioType.Discovery => await GetDiscoveryMix(userId, request.Limit, cancellationToken),
            MusicRadioType.TimeCapsule => await GetTimeCapsule(userId, request.Limit, cancellationToken),
            MusicRadioType.Tempo => await GetTempoMix(request, userId, cancellationToken),
            MusicRadioType.RecentlyAdded => await GetRecentlyAdded(userId, request.Limit, cancellationToken),
            _ => []
        };
    }

    /// <summary>
    /// Sonic Radio: from a seed track, find tracks with similar audio profile.
    /// Distance is computed on Energy, Danceability, Valence (euclidean) + BPM proximity + key compatibility.
    /// </summary>
    private async Task<List<BaseMedia>> GetSonicRadio(GetMusicRadioQuery request, Guid? userId, CancellationToken ct)
    {
        if (request.SeedTrackId is not { } seedId)
            return [];

        var seed = await context.Medias
            .OfType<MusicTrack>()
            .Include(t => t.AudioAnalysis)
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == seedId, ct);

        if (seed?.AudioAnalysis is not { } seedAnalysis)
            return [];

        // Load all analyzed tracks except the seed
        var candidates = await BuildTrackQuery(userId)
            .Where(t => t.Id != seedId)
            .Where(t => t.AudioAnalysis != null)
            .Include(t => t.AudioAnalysis)
            .ToListAsync(ct);

        // Score and rank by sonic similarity
        var scored = candidates
            .Select(t => new { Track = t, Distance = ComputeSonicDistance(seedAnalysis, t.AudioAnalysis!) })
            .OrderBy(x => x.Distance)
            .Take(request.Limit)
            .Select(x => (BaseMedia)x.Track)
            .ToList();

        return scored;
    }

    /// <summary>
    /// Artist Radio: tracks from the seed artist + tracks from artists in the same genres.
    /// </summary>
    private async Task<List<BaseMedia>> GetArtistRadio(GetMusicRadioQuery request, Guid? userId, CancellationToken ct)
    {
        if (request.SeedArtistId is not { } artistId)
            return [];

        // Get genres of seed artist's tracks
        var artistGenres = await context.Medias
            .OfType<MusicTrack>()
            .Where(t => t.PersonRoles.Any(r => r.PersonId == artistId)
                     || t.Album.PersonRoles.Any(r => r.PersonId == artistId))
            .SelectMany(t => t.Genres)
            .Distinct()
            .ToListAsync(ct);

        // Get tracks from this artist
        var artistTracks = await BuildTrackQuery(userId)
            .Where(t => t.PersonRoles.Any(r => r.PersonId == artistId)
                     || t.Album.PersonRoles.Any(r => r.PersonId == artistId))
            .ToListAsync(ct);

        // Get tracks from related artists (same genres, different artist)
        var relatedTracks = await BuildTrackQuery(userId)
            .Where(t => !t.PersonRoles.Any(r => r.PersonId == artistId)
                     && !t.Album.PersonRoles.Any(r => r.PersonId == artistId))
            .Where(t => t.Genres.Any(g => artistGenres.Contains(g))
                     || t.Album.Genres.Any(g => artistGenres.Contains(g)))
            .ToListAsync(ct);

        // Mix: 60% artist, 40% related, shuffled
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
    private async Task<List<BaseMedia>> GetMoodMix(GetMusicRadioQuery request, Guid? userId, CancellationToken ct)
    {
        var (energyMin, energyMax, valenceMin, valenceMax, danceMin, danceMax) = GetMoodRanges(request.MoodPreset);

        var tracks = await BuildTrackQuery(userId)
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
    private async Task<List<BaseMedia>> GetDiscoveryMix(Guid? userId, int limit, CancellationToken ct)
    {
        if (userId is null)
            return [];

        // Find user's top tracks by play count to build a taste profile
        var topTracks = await context.Medias
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
            .ToListAsync(ct);

        if (topTracks.Count == 0)
            return await GetRecentlyAdded(userId, limit, ct);

        // Compute average audio profile from top tracks
        var avgProfile = ComputeAverageProfile(topTracks.Select(t => t.AudioAnalysis!).ToList());

        // Find unplayed tracks with audio analysis
        var candidates = await BuildTrackQuery(userId)
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
    /// Time Capsule: tracks the user listened to around the same period last year (±2 weeks).
    /// </summary>
    private async Task<List<BaseMedia>> GetTimeCapsule(Guid? userId, int limit, CancellationToken ct)
    {
        if (userId is null)
            return [];

        var now = DateTime.UtcNow;
        var windowStart = now.AddYears(-1).AddDays(-14);
        var windowEnd = now.AddYears(-1).AddDays(14);

        var tracks = await BuildTrackQuery(userId)
            .Where(t => t.UserMediaStates.Any(s =>
                s.UserId == userId.Value
                && s.LastInteractedAt >= windowStart
                && s.LastInteractedAt <= windowEnd))
            .ToListAsync(ct);

        // If not enough from 1 year ago, also check 6 months ago
        if (tracks.Count < limit / 2)
        {
            var sixMonthStart = now.AddMonths(-6).AddDays(-14);
            var sixMonthEnd = now.AddMonths(-6).AddDays(14);

            var moreTracks = await BuildTrackQuery(userId)
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
    /// Tempo Mix: tracks within ±5% BPM of the seed track, or a fixed BPM if no seed.
    /// Ordered by BPM proximity for smooth DJ-style progression.
    /// </summary>
    private async Task<List<BaseMedia>> GetTempoMix(GetMusicRadioQuery request, Guid? userId, CancellationToken ct)
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

        targetBpm ??= 120; // Default to 120 BPM if no seed

        var bpmMin = targetBpm.Value * 0.95;
        var bpmMax = targetBpm.Value * 1.05;

        var tracks = await BuildTrackQuery(userId)
            .Where(t => t.AudioAnalysis != null && t.AudioAnalysis.Bpm >= bpmMin && t.AudioAnalysis.Bpm <= bpmMax)
            .Include(t => t.AudioAnalysis)
            .ToListAsync(ct);

        // Order by BPM proximity for smooth progression
        return tracks
            .OrderBy(t => Math.Abs(t.AudioAnalysis!.Bpm!.Value - targetBpm.Value))
            .Take(request.Limit)
            .Cast<BaseMedia>()
            .ToList();
    }

    /// <summary>
    /// Recently Added: newest tracks the user hasn't listened to yet.
    /// </summary>
    private async Task<List<BaseMedia>> GetRecentlyAdded(Guid? userId, int limit, CancellationToken ct)
    {
        var query = BuildTrackQuery(userId);

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

    // Query builder

    private IQueryable<MusicTrack> BuildTrackQuery(Guid? userId)
    {
        var query = context.Medias
            .OfType<MusicTrack>()
            .Include(t => t.Pictures).ThenInclude(p => p.Variants)
            .Include(t => t.Ratings)
            .Include(t => t.IndexedFiles)
            .Include(t => t.PersonRoles).ThenInclude(r => r.Person)
            .Include(t => t.Album).ThenInclude(a => a.PersonRoles).ThenInclude(r => r.Person)
            .Include(t => t.Album).ThenInclude(a => a.Pictures).ThenInclude(p => p.Variants)
            .AsNoTracking();

        if (userId.HasValue)
        {
            query = query.Include(t => t.UserMediaStates.Where(s => s.UserId == userId.Value));
        }

        return query;
    }

    // Sonic similarity

    private static double ComputeSonicDistance(AudioAnalysis a, AudioAnalysis b)
    {
        double distance = 0;

        // Euclidean distance on normalized features (0..1)
        distance += FeatureDistance(a.Energy, b.Energy);
        distance += FeatureDistance(a.Danceability, b.Danceability);
        distance += FeatureDistance(a.Valence, b.Valence);

        // BPM distance (normalized: 50 BPM diff = 1.0)
        if (a.Bpm.HasValue && b.Bpm.HasValue)
            distance += Math.Pow(Math.Min(Math.Abs(a.Bpm.Value - b.Bpm.Value) / 50.0, 1.0), 2);
        else
            distance += 0.25; // Penalty for missing BPM

        // Key compatibility bonus (reduce distance if harmonically compatible)
        if (a.MusicalKey is not null && b.MusicalKey is not null)
        {
            if (AreKeysCompatible(a.MusicalKey, b.MusicalKey))
                distance *= 0.8; // 20% bonus
        }

        return Math.Sqrt(distance);
    }

    private static double FeatureDistance(double? a, double? b)
    {
        if (a.HasValue && b.HasValue)
            return Math.Pow(a.Value - b.Value, 2);
        return 0.1; // Small penalty for missing data
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

    // Camelot wheel for harmonic key compatibility

    private static readonly Dictionary<string, (int Number, char Letter)> CamelotMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["A flat minor"] = (1, 'A'), ["G sharp minor"] = (1, 'A'),
        ["B major"] = (1, 'B'),
        ["E flat minor"] = (2, 'A'), ["D sharp minor"] = (2, 'A'),
        ["F sharp major"] = (2, 'B'), ["G flat major"] = (2, 'B'),
        ["B flat minor"] = (3, 'A'), ["A sharp minor"] = (3, 'A'),
        ["D flat major"] = (3, 'B'), ["C sharp major"] = (3, 'B'),
        ["F minor"] = (4, 'A'),
        ["A flat major"] = (4, 'B'), ["G sharp major"] = (4, 'B'),
        ["C minor"] = (5, 'A'),
        ["E flat major"] = (5, 'B'), ["D sharp major"] = (5, 'B'),
        ["G minor"] = (6, 'A'),
        ["B flat major"] = (6, 'B'), ["A sharp major"] = (6, 'B'),
        ["D minor"] = (7, 'A'),
        ["F major"] = (7, 'B'),
        ["A minor"] = (8, 'A'),
        ["C major"] = (8, 'B'),
        ["E minor"] = (9, 'A'),
        ["G major"] = (9, 'B'),
        ["B minor"] = (10, 'A'),
        ["D major"] = (10, 'B'),
        ["F sharp minor"] = (11, 'A'), ["G flat minor"] = (11, 'A'),
        ["A major"] = (11, 'B'),
        ["C sharp minor"] = (12, 'A'), ["D flat minor"] = (12, 'A'),
        ["E major"] = (12, 'B'),
    };

    /// <summary>
    /// Two keys are harmonically compatible if they are:
    /// - Same Camelot position
    /// - ±1 on the wheel (same letter), wrapping 12→1
    /// - Same number, opposite letter (relative major/minor)
    /// </summary>
    internal static bool AreKeysCompatible(string keyA, string keyB)
    {
        if (!CamelotMap.TryGetValue(keyA, out var a) || !CamelotMap.TryGetValue(keyB, out var b))
            return false;

        // Same position
        if (a == b) return true;

        // Same letter, adjacent number (wrapping)
        if (a.Letter == b.Letter)
        {
            var diff = Math.Abs(a.Number - b.Number);
            if (diff == 1 || diff == 11) return true;
        }

        // Same number, opposite letter (relative major/minor)
        if (a.Number == b.Number && a.Letter != b.Letter) return true;

        return false;
    }

    // Mood presets

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
            _ => (0.0, 1.0, 0.0, 1.0, 0.0, 1.0) // No filter
        };
    }

    // Shuffle utility

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
