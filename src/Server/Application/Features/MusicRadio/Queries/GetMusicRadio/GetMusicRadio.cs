using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.QueryExtensions;
using K7.Server.Application.Features.Medias.Queries.Common;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Ratings;
using K7.Server.Domain.Enums;
using K7.Shared;
using Microsoft.EntityFrameworkCore;

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
    public Guid[]? ExcludeIds { get; init; }
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
            MusicRadioType.Discovery => await GetDiscoveryMix(userId, libraryIds, request.Limit, request.ExcludeIds, cancellationToken),
            MusicRadioType.DiscoveryAi => await GetDiscoveryAiMix(userId, libraryIds, request.Limit, request.ExcludeIds, cancellationToken),
            MusicRadioType.TimeCapsule => await GetTimeCapsule(userId, libraryIds, request.Limit, request.ExcludeIds, cancellationToken),
            MusicRadioType.Tempo => await GetTempoMix(request, userId, libraryIds, cancellationToken),
            MusicRadioType.RecentlyAdded => await GetRecentlyAdded(userId, libraryIds, request.Limit, request.ExcludeIds, cancellationToken),
            _ => []
        };
    }

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

        var fetchLimit = request.Limit + (request.ExcludeIds?.Length ?? 0);
        var trackIds = await musicIntelligenceService.GetSimilarTracksAsync(seedId.Value, fetchLimit, ct);
        var filteredIds = FilterExcluded(trackIds, request.ExcludeIds, request.Limit);
        return await LoadTracksByIdsAsync(filteredIds, userId, libraryIds, ct);
    }

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

        var artistTrackIds = await ApplyExcludeIdsFilter(BuildLightTrackIdQuery(userId, libraryIds), request.ExcludeIds)
            .Where(t => t.PersonRoles.Any(r => r.PersonId == artistId)
                     || t.Album.PersonRoles.Any(r => r.PersonId == artistId))
            .Select(t => t.Id)
            .ToListAsync(ct);

        var relatedTrackIds = await ApplyExcludeIdsFilter(BuildLightTrackIdQuery(userId, libraryIds), request.ExcludeIds)
            .Where(t => !t.PersonRoles.Any(r => r.PersonId == artistId)
                     && !t.Album.PersonRoles.Any(r => r.PersonId == artistId))
            .Where(t => t.MetadataTags.Any(mt => mt.MetadataTag.Kind == MetadataTagKind.Genre && artistGenres.Contains(mt.MetadataTag.DisplayName))
                     || t.Album.MetadataTags.Any(mt => mt.MetadataTag.Kind == MetadataTagKind.Genre && artistGenres.Contains(mt.MetadataTag.DisplayName)))
            .Select(t => t.Id)
            .ToListAsync(ct);

        var artistCount = (int)(request.Limit * 0.6);
        var relatedCount = request.Limit - artistCount;

        var selectedIds = Shuffle(artistTrackIds).Take(artistCount)
            .Concat(Shuffle(relatedTrackIds).Take(relatedCount))
            .Take(request.Limit)
            .ToList();

        return await LoadTracksByIdsAsync(Shuffle(selectedIds), userId, libraryIds, ct);
    }

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
        var fetchLimit = request.Limit + (request.ExcludeIds?.Length ?? 0);
        var trackIds = await musicIntelligenceService.GetMoodTracksAsync(moodKey, centroidIndex, fetchLimit, ct);
        var filteredIds = FilterExcluded(trackIds, request.ExcludeIds, request.Limit);
        return await LoadTracksByIdsAsync(filteredIds, userId, libraryIds, ct);
    }

    private async Task<List<BaseMedia>> GetDiscoveryMix(
        Guid? userId,
        Guid[]? libraryIds,
        int limit,
        Guid[]? excludeIds,
        CancellationToken ct)
    {
        var baseQuery = ApplyExcludeIdsFilter(BuildLightTrackIdQuery(userId, libraryIds), excludeIds);

        if (userId is null)
        {
            var guestIds = await baseQuery
                .OrderBy(_ => EF.Functions.Random())
                .Select(t => t.Id)
                .Take(limit)
                .ToListAsync(ct);

            return await LoadTracksByIdsAsync(guestIds, userId, libraryIds, ct);
        }

        var uid = userId.Value;

        var neverPlayedIds = await baseQuery
            .Where(t => !t.UserMediaStates.Any(s => s.UserId == uid && s.PlayCount > 0))
            .OrderBy(_ => EF.Functions.Random())
            .Select(t => t.Id)
            .Take(limit)
            .ToListAsync(ct);

        if (neverPlayedIds.Count > 0)
            return await LoadTracksByIdsAsync(neverPlayedIds, userId, libraryIds, ct);

        var neverRatedIds = await baseQuery
            .Where(t => !t.Ratings.OfType<UserRating>().Any(r => r.UserId == uid))
            .OrderBy(_ => EF.Functions.Random())
            .Select(t => t.Id)
            .Take(limit)
            .ToListAsync(ct);

        return await LoadTracksByIdsAsync(neverRatedIds, userId, libraryIds, ct);
    }

    private async Task<List<BaseMedia>> GetDiscoveryAiMix(
        Guid? userId,
        Guid[]? libraryIds,
        int limit,
        Guid[]? excludeIds,
        CancellationToken ct)
    {
        if (!await musicIntelligenceService.IsAvailableAsync(ct))
            return [];

        var excludeCount = excludeIds?.Length ?? 0;
        var candidateCount = Math.Clamp((limit + excludeCount) * 5, limit + excludeCount, 250);
        var trackIds = await musicIntelligenceService.GetDiscoveryTracksAsync(candidateCount, ct);
        var filteredIds = FilterExcluded(trackIds, excludeIds, candidateCount);
        var tracks = await LoadTracksByIdsAsync(filteredIds, userId, libraryIds, ct);
        return FilterUnexploredTracks(tracks, filteredIds, userId, limit);
    }

    private async Task<List<BaseMedia>> GetTimeCapsule(
        Guid? userId,
        Guid[]? libraryIds,
        int limit,
        Guid[]? excludeIds,
        CancellationToken ct)
    {
        if (userId is null)
            return [];

        var now = DateTime.UtcNow;
        var windowStart = now.AddYears(-1).AddDays(-14);
        var windowEnd = now.AddYears(-1).AddDays(14);
        var baseQuery = ApplyExcludeIdsFilter(BuildLightTrackIdQuery(userId, libraryIds), excludeIds);

        var ids = await baseQuery
            .Where(t => t.UserMediaStates.Any(s =>
                s.UserId == userId.Value
                && s.LastInteractedAt >= windowStart
                && s.LastInteractedAt <= windowEnd))
            .OrderBy(_ => EF.Functions.Random())
            .Select(t => t.Id)
            .Take(limit)
            .ToListAsync(ct);

        if (ids.Count == 0)
        {
            ids = await baseQuery
                .Where(t => t.UserMediaStates.Any(s => s.UserId == userId.Value && s.PlayCount > 0))
                .OrderByDescending(t => t.UserMediaStates
                    .Where(s => s.UserId == userId.Value)
                    .Select(s => s.LastInteractedAt)
                    .FirstOrDefault())
                .Select(t => t.Id)
                .Take(limit)
                .ToListAsync(ct);
        }

        if (ids.Count < limit / 2)
        {
            var sixMonthStart = now.AddMonths(-6).AddDays(-14);
            var sixMonthEnd = now.AddMonths(-6).AddDays(14);
            var existingIds = ids.ToHashSet();

            var moreIds = await baseQuery
                .Where(t => t.UserMediaStates.Any(s =>
                    s.UserId == userId.Value
                    && s.LastInteractedAt >= sixMonthStart
                    && s.LastInteractedAt <= sixMonthEnd))
                .Where(t => !existingIds.Contains(t.Id))
                .OrderBy(_ => EF.Functions.Random())
                .Select(t => t.Id)
                .Take(limit - ids.Count)
                .ToListAsync(ct);

            ids.AddRange(moreIds);
        }

        var shuffledIds = Shuffle(ids).Take(limit).ToList();
        return await LoadTracksByIdsAsync(shuffledIds, userId, libraryIds, ct);
    }

    private async Task<List<BaseMedia>> GetTempoMix(
        GetMusicRadioQuery request,
        Guid? userId,
        Guid[]? libraryIds,
        CancellationToken ct)
    {
        if (!await musicIntelligenceService.IsAvailableAsync(ct))
            return [];

        var fetchLimit = request.Limit + (request.ExcludeIds?.Length ?? 0);
        var trackIds = await musicIntelligenceService.GetMoodTracksAsync("danceable", 0, fetchLimit, ct);
        var filteredIds = FilterExcluded(trackIds, request.ExcludeIds, request.Limit);
        return await LoadTracksByIdsAsync(filteredIds, userId, libraryIds, ct);
    }

    private async Task<List<BaseMedia>> GetRecentlyAdded(
        Guid? userId,
        Guid[]? libraryIds,
        int limit,
        Guid[]? excludeIds,
        CancellationToken ct)
    {
        var query = ApplyExcludeIdsFilter(BuildLightTrackIdQuery(userId, libraryIds), excludeIds);

        if (userId.HasValue)
        {
            query = query.Where(t => !t.UserMediaStates.Any(s => s.UserId == userId.Value && s.PlayCount > 0));
        }

        var ids = await query
            .OrderByDescending(t => t.Created)
            .Select(t => t.Id)
            .Take(limit)
            .ToListAsync(ct);

        return await LoadTracksByIdsAsync(ids, userId, libraryIds, ct);
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

        var randomSeedId = await query
            .OrderBy(_ => EF.Functions.Random())
            .Select(t => t.Id)
            .FirstOrDefaultAsync(ct);

        return randomSeedId == Guid.Empty ? null : randomSeedId;
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

    private IQueryable<MusicTrack> BuildLightTrackIdQuery(Guid? userId, Guid[]? libraryIds)
    {
        var query = context.Medias
            .OfType<MusicTrack>()
            .WhereHasLibraryAvailability(context)
            .AsNoTracking();

        query = ApplyLibraryFilter(query, libraryIds);
        return query;
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
            .WhereHasLibraryAvailability(context)
            .AsSplitQuery()
            .AsNoTracking();

        query = ApplyLibraryFilter(query, libraryIds);

        if (userId.HasValue)
        {
            query = query.Include(t => t.UserMediaStates.Where(s => s.UserId == userId.Value));
        }

        return query;
    }

    private IQueryable<MusicTrack> ApplyLibraryFilter(IQueryable<MusicTrack> query, Guid[]? libraryIds) =>
        libraryIds is not { Length: > 0 }
            ? query
            : query.WhereAvailableInLibraries(context, libraryIds);

    private static IQueryable<MusicTrack> ApplyExcludeIdsFilter(IQueryable<MusicTrack> query, Guid[]? excludeIds)
    {
        if (excludeIds is not { Length: > 0 })
            return query;

        return query.Where(t => !excludeIds.Contains(t.Id));
    }

    private static List<Guid> FilterExcluded(IReadOnlyList<Guid> trackIds, Guid[]? excludeIds, int limit)
    {
        IEnumerable<Guid> ids = trackIds;
        if (excludeIds is { Length: > 0 })
        {
            var exclude = excludeIds.ToHashSet();
            ids = ids.Where(id => !exclude.Contains(id));
        }

        return ids.Take(limit).ToList();
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
