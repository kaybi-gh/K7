using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.QueryExtensions;
using K7.Server.Application.Common.Services;
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
    public string? Genre { get; init; }
    public int Limit { get; init; } = 50;
    public Guid[]? ExcludeIds { get; init; }
}

public class GetMusicRadioQueryHandler(
    IApplicationDbContext context,
    IUser currentUser,
    IMusicIntelligenceService musicIntelligenceService,
    LiteMediaProjectionService liteMediaProjection,
    MediaAccessFilter mediaAccessFilter)
    : IRequestHandler<GetMusicRadioQuery, List<BaseMedia>>
{
    private const int DiscoveryAiSeedCount = 5;
    private const int DiscoveryAiMinSimilarPerSeed = 40;
    private const int DiscoveryAiFingerprintMinCount = 80;
    private const int ArtistRadioSimilarArtistCount = 8;
    private const double FavoriteRatingThreshold = 5;

    public async Task<List<BaseMedia>> Handle(GetMusicRadioQuery request, CancellationToken cancellationToken)
    {
        var userId = await currentUser.GetIdAsync(cancellationToken);
        var libraryIds = await LibraryGroupFilterHelper.ResolveLibraryIdsAsync(
            context, request.LibraryIds, request.LibraryGroupIds, cancellationToken);

        return request.RadioType switch
        {
            MusicRadioType.Sonic => await GetSonicRadio(request, userId, libraryIds, cancellationToken),
            MusicRadioType.Artist => await GetArtistRadio(request, userId, libraryIds, cancellationToken),
            MusicRadioType.Discovery => await GetDiscoveryMix(userId, libraryIds, request.Limit, request.ExcludeIds, cancellationToken),
            MusicRadioType.DiscoveryAi => await GetDiscoveryAiMix(userId, libraryIds, request.Limit, request.ExcludeIds, cancellationToken),
            MusicRadioType.TimeCapsule => await GetTimeCapsule(userId, libraryIds, request.Limit, request.ExcludeIds, cancellationToken),
            MusicRadioType.Tempo => await GetTempoMix(request, userId, libraryIds, cancellationToken),
            MusicRadioType.RecentlyAdded => await GetRecentlyAdded(userId, libraryIds, request.Limit, request.ExcludeIds, cancellationToken),
            MusicRadioType.Genre => await GetGenreMix(request, userId, libraryIds, cancellationToken),
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
        var matches = await musicIntelligenceService.GetSimilarTracksAsync(seedId.Value, fetchLimit, cancellationToken: ct);
        var trackIds = matches.Select(m => m.ItemId).ToList();
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

        var playable = ApplyExcludeIdsFilter(
            await BuildLightTrackIdQueryAsync(userId, libraryIds, ct),
            request.ExcludeIds);
        var artistTrackIds = await WhereLinkedToMusicArtist(playable, artistId)
            .OrderBy(_ => EF.Functions.Random())
            .Select(t => t.Id)
            .Take(request.Limit)
            .ToListAsync(ct);

        if (artistTrackIds.Count == 0 && request.ExcludeIds is not { Length: > 0 })
            return [];

        var similarIds = await GetSimilarArtistTrackIdsAsync(playable, artistId, request.Limit, ct);
        var similarSlots = artistTrackIds.Count == 0
            ? request.Limit
            : request.Limit - Math.Min(artistTrackIds.Count, Math.Max(1, request.Limit / 3));
        if (similarIds.Count < similarSlots)
        {
            var genreIds = await GetGenreNeighborTrackIdsAsync(
                playable,
                WhereLinkedToMusicArtist(await BuildLightTrackIdQueryAsync(userId, libraryIds, ct), artistId),
                artistId,
                similarIds,
                request.Limit - similarIds.Count,
                ct);
            if (genreIds.Count > 0)
                similarIds = similarIds.Concat(genreIds).ToList();
        }

        if (artistTrackIds.Count == 0 && similarIds.Count == 0)
            return [];

        var selectedIds = InterleaveArtistRadio(artistTrackIds, similarIds, request.Limit);
        return await LoadTracksByIdsAsync(selectedIds, userId, libraryIds, ct);
    }

    private async Task<List<Guid>> GetSimilarArtistTrackIdsAsync(
        IQueryable<MusicTrack> playable,
        Guid artistId,
        int limit,
        CancellationToken ct)
    {
        if (limit <= 0)
            return [];

        var similarArtistIds = await ResolveSimilarArtistIdsAsync(artistId, ct);
        if (similarArtistIds.Count == 0)
            return [];

        return await WhereNotLinkedToMusicArtist(
                WhereLinkedToAnyMusicArtist(playable, similarArtistIds),
                artistId)
            .OrderBy(_ => EF.Functions.Random())
            .Select(t => t.Id)
            .Take(limit)
            .ToListAsync(ct);
    }

    private async Task<List<Guid>> ResolveSimilarArtistIdsAsync(Guid artistId, CancellationToken ct)
    {
        if (!await musicIntelligenceService.IsEnabledAsync(ct))
            return [];

        var artistName = await context.Medias
            .OfType<MusicArtist>()
            .AsNoTracking()
            .Where(a => a.Id == artistId)
            .Select(a => a.Title)
            .FirstOrDefaultAsync(ct);

        var matches = await musicIntelligenceService.GetSimilarArtistsAsync(
            artistId,
            artistName,
            ArtistRadioSimilarArtistCount,
            ct);
        if (matches is not { Count: > 0 })
            return [];

        var parsedIds = new List<Guid>();
        foreach (var match in matches)
        {
            if (Guid.TryParse(match.ArtistId, out var id) && id != artistId)
                parsedIds.Add(id);
        }

        var knownIds = parsedIds.Count == 0
            ? []
            : await context.Medias
                .OfType<MusicArtist>()
                .AsNoTracking()
                .Where(a => parsedIds.Contains(a.Id))
                .Select(a => a.Id)
                .ToListAsync(ct);
        var known = knownIds.ToHashSet();

        var unresolvedNames = matches
            .Where(m => !Guid.TryParse(m.ArtistId, out var id) || !known.Contains(id))
            .Select(m => m.Artist)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var byName = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        if (unresolvedNames.Count > 0)
        {
            var lowered = unresolvedNames.Select(n => n.ToLowerInvariant()).ToHashSet();
            var named = await context.Medias
                .OfType<MusicArtist>()
                .AsNoTracking()
                .Where(a => a.Id != artistId && a.Title != null && lowered.Contains(a.Title.ToLower()))
                .Select(a => new { a.Id, a.Title })
                .ToListAsync(ct);
            foreach (var artist in named)
            {
                if (artist.Title is not null)
                    byName.TryAdd(artist.Title, artist.Id);
            }
        }

        var ordered = new List<Guid>();
        var seen = new HashSet<Guid>();
        foreach (var match in matches)
        {
            Guid? resolved = null;
            if (Guid.TryParse(match.ArtistId, out var id) && known.Contains(id))
                resolved = id;
            else if (!string.IsNullOrWhiteSpace(match.Artist) && byName.TryGetValue(match.Artist, out var namedId))
                resolved = namedId;

            if (resolved is { } resolvedId && resolvedId != artistId && seen.Add(resolvedId))
                ordered.Add(resolvedId);
        }

        return ordered;
    }

    private async Task<List<Guid>> GetGenreNeighborTrackIdsAsync(
        IQueryable<MusicTrack> playable,
        IQueryable<MusicTrack> artistTracks,
        Guid artistId,
        IReadOnlyList<Guid> excludeIds,
        int take,
        CancellationToken ct)
    {
        if (take <= 0)
            return [];

        var trackGenres = await artistTracks
            .SelectMany(t => t.MetadataTags
                .Where(mt => mt.MetadataTag.Kind == MetadataTagKind.Genre)
                .Select(mt => mt.MetadataTag.DisplayName))
            .Distinct()
            .ToListAsync(ct);
        var albumGenres = await artistTracks
            .SelectMany(t => t.Album.MetadataTags
                .Where(mt => mt.MetadataTag.Kind == MetadataTagKind.Genre)
                .Select(mt => mt.MetadataTag.DisplayName))
            .Distinct()
            .ToListAsync(ct);
        var artistGenres = trackGenres.Concat(albumGenres).Distinct().ToList();
        if (artistGenres.Count == 0)
            return [];

        var query = WhereNotLinkedToMusicArtist(playable, artistId);
        if (excludeIds.Count > 0)
            query = query.Where(t => !excludeIds.Contains(t.Id));

        return await query
            .Where(t => t.MetadataTags.Any(mt =>
                    mt.MetadataTag.Kind == MetadataTagKind.Genre
                    && artistGenres.Contains(mt.MetadataTag.DisplayName))
                || t.Album.MetadataTags.Any(mt =>
                    mt.MetadataTag.Kind == MetadataTagKind.Genre
                    && artistGenres.Contains(mt.MetadataTag.DisplayName)))
            .OrderBy(_ => EF.Functions.Random())
            .Select(t => t.Id)
            .Take(take)
            .ToListAsync(ct);
    }

    private static IQueryable<MusicTrack> WhereLinkedToMusicArtist(IQueryable<MusicTrack> query, Guid artistId) =>
        query.Where(t =>
            t.ArtistId == artistId
            || t.Album.ArtistId == artistId
            || t.ArtistCredits.Any(c => c.MusicArtistId == artistId)
            || t.Album.ArtistCredits.Any(c => c.MusicArtistId == artistId));

    private static IQueryable<MusicTrack> WhereLinkedToAnyMusicArtist(
        IQueryable<MusicTrack> query,
        IReadOnlyList<Guid> artistIds) =>
        query.Where(t =>
            (t.ArtistId != null && artistIds.Contains(t.ArtistId.Value))
            || (t.Album.ArtistId != null && artistIds.Contains(t.Album.ArtistId.Value))
            || t.ArtistCredits.Any(c => artistIds.Contains(c.MusicArtistId))
            || t.Album.ArtistCredits.Any(c => artistIds.Contains(c.MusicArtistId)));

    private static IQueryable<MusicTrack> WhereNotLinkedToMusicArtist(IQueryable<MusicTrack> query, Guid artistId) =>
        query.Where(t =>
            (t.ArtistId == null || t.ArtistId != artistId)
            && (t.Album.ArtistId == null || t.Album.ArtistId != artistId)
            && !t.ArtistCredits.Any(c => c.MusicArtistId == artistId)
            && !t.Album.ArtistCredits.Any(c => c.MusicArtistId == artistId));

    private async Task<List<BaseMedia>> GetGenreMix(
        GetMusicRadioQuery request,
        Guid? userId,
        Guid[]? libraryIds,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Genre))
            return [];

        var genre = request.Genre.Trim();
        var query = ApplyExcludeIdsFilter(
            await BuildLightTrackIdQueryAsync(userId, libraryIds, ct),
            request.ExcludeIds)
            .Where(t => t.MetadataTags.Any(mt =>
                    mt.MetadataTag.Kind == MetadataTagKind.Genre
                    && mt.MetadataTag.DisplayName == genre)
                || t.Album.MetadataTags.Any(mt =>
                    mt.MetadataTag.Kind == MetadataTagKind.Genre
                    && mt.MetadataTag.DisplayName == genre));

        var ids = await query
            .OrderBy(_ => EF.Functions.Random())
            .Select(t => t.Id)
            .Take(request.Limit)
            .ToListAsync(ct);

        return await LoadTracksByIdsAsync(ids, userId, libraryIds, ct);
    }

    private async Task<List<BaseMedia>> GetDiscoveryMix(
        Guid? userId,
        Guid[]? libraryIds,
        int limit,
        Guid[]? excludeIds,
        CancellationToken ct)
    {
        var baseQuery = ApplyExcludeIdsFilter(
            await BuildLightTrackIdQueryAsync(userId, libraryIds, ct),
            excludeIds);

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
        var ratedMediaIds = await GetUserRatedMediaIdsAsync(uid, ct);
        var unratedQuery = WhereNotRatedByUser(baseQuery, uid);

        var neverPlayedIds = await unratedQuery
            .Where(t => !t.UserMediaStates.Any(s => s.UserId == uid && s.PlayCount > 0))
            .OrderBy(_ => EF.Functions.Random())
            .Select(t => t.Id)
            .Take(limit)
            .ToListAsync(ct);

        IReadOnlyList<Guid> selectedIds = neverPlayedIds;
        if (neverPlayedIds.Count < limit)
        {
            var existing = neverPlayedIds.ToHashSet();
            var neverRatedIds = await WhereNotRatedByUser(baseQuery, uid)
                .Where(t => !existing.Contains(t.Id))
                .OrderBy(_ => EF.Functions.Random())
                .Select(t => t.Id)
                .Take(limit - neverPlayedIds.Count)
                .ToListAsync(ct);

            selectedIds = neverPlayedIds.Concat(neverRatedIds).ToList();
        }

        var tracks = await LoadTracksByIdsAsync(selectedIds, userId, libraryIds, ct);
        return FilterUnexploredTracks(tracks, selectedIds, userId, limit, ratedMediaIds);
    }

    private async Task<List<BaseMedia>> GetDiscoveryAiMix(
        Guid? userId,
        Guid[]? libraryIds,
        int limit,
        Guid[]? excludeIds,
        CancellationToken ct)
    {
        // User-triggered: do not block on a stale unreachable cache (same as sonic search).
        if (!await musicIntelligenceService.IsEnabledAsync(ct))
            return [];

        var exclude = new HashSet<Guid>(excludeIds ?? []);
        var seedIds = await PickTasteSeedTrackIdsAsync(userId, libraryIds, DiscoveryAiSeedCount, ct);
        var seen = new HashSet<Guid>(exclude);
        var candidateIds = new List<Guid>();

        if (seedIds.Count > 0)
        {
            foreach (var seedId in seedIds)
                seen.Add(seedId);

            var perSeed = Math.Max(limit * 8, DiscoveryAiMinSimilarPerSeed);
            var seedMeta = await context.Medias
                .OfType<MusicTrack>()
                .AsNoTracking()
                .Where(t => seedIds.Contains(t.Id))
                .Select(t => new
                {
                    t.Id,
                    t.Title,
                    Artist = t.Artist != null
                        ? t.Artist.Title
                        : t.Album.Artist != null ? t.Album.Artist.Title : null
                })
                .ToListAsync(ct);

            var similarBatches = await Task.WhenAll(seedMeta.Select(seed =>
                musicIntelligenceService.GetSimilarTracksAsync(
                    seed.Id,
                    perSeed,
                    seed.Title,
                    seed.Artist,
                    ct)));

            candidateIds.AddRange(InterleaveUnique(
                similarBatches.Select(batch => (batch ?? []).Select(m => m.ItemId).ToList()).ToList(),
                seen));
        }

        if (candidateIds.Count == 0)
        {
            // Enough neighbors that AudioMuse seed tracks (already played) do not fill the whole list.
            var fingerprintCount = Math.Max(limit * 5, DiscoveryAiFingerprintMinCount);
            var fingerprintIds = await musicIntelligenceService.GetDiscoveryTracksAsync(fingerprintCount, ct);
            candidateIds.AddRange((fingerprintIds ?? []).Where(seen.Add));
        }

        if (candidateIds.Count == 0)
            return [];

        HashSet<Guid>? ratedMediaIds = null;
        if (userId is { } uid)
        {
            ratedMediaIds = await GetUserRatedMediaIdsAsync(uid, ct);
            // Push rating exclusion to SQL before load so AudioMuse neighbors that are
            // already rated (track, album, or artist) never reach the queue mapper.
            var candidateSet = candidateIds.ToHashSet();
            var accessibleCandidates = await BuildLightTrackIdQueryAsync(userId, libraryIds, ct);
            var unratedIds = await WhereNotRatedByUser(
                    accessibleCandidates.Where(t => candidateSet.Contains(t.Id)),
                    uid)
                .Select(t => t.Id)
                .ToListAsync(ct);
            var unratedSet = unratedIds.ToHashSet();
            candidateIds = candidateIds.Where(unratedSet.Contains).ToList();
            if (candidateIds.Count == 0)
                return [];
        }

        var tracks = await LoadTracksByIdsAsync(candidateIds, userId, libraryIds, ct);
        return FilterUnexploredTracks(tracks, candidateIds, userId, limit, ratedMediaIds);
    }

    private async Task<List<Guid>> PickTasteSeedTrackIdsAsync(
        Guid? userId,
        Guid[]? libraryIds,
        int seedCount,
        CancellationToken ct)
    {
        var query = await BuildLightTrackIdQueryAsync(userId, libraryIds, ct);

        if (userId is { } uid)
        {
            var favoriteMediaIds = await context.Ratings
                .OfType<UserRating>()
                .AsNoTracking()
                .Where(r => r.UserId == uid && r.Value > FavoriteRatingThreshold)
                .OrderByDescending(r => r.Value)
                .ThenByDescending(r => r.LastModified)
                .Select(r => r.MediaId)
                .ToListAsync(ct);

            if (favoriteMediaIds.Count > 0)
            {
                var favoriteSet = favoriteMediaIds.ToHashSet();
                var favoriteTrackIds = await query
                    .Where(t => favoriteSet.Contains(t.Id) || favoriteSet.Contains(t.AlbumId))
                    .Select(t => t.Id)
                    .Take(seedCount)
                    .ToListAsync(ct);

                if (favoriteTrackIds.Count > 0)
                    return favoriteTrackIds;
            }

            var playedIds = await query
                .Where(t => t.UserMediaStates.Any(s => s.UserId == uid && s.PlayCount > 0))
                .OrderByDescending(t => t.UserMediaStates
                    .Where(s => s.UserId == uid)
                    .Select(s => s.PlayCount)
                    .FirstOrDefault())
                .Select(t => t.Id)
                .Take(seedCount)
                .ToListAsync(ct);

            if (playedIds.Count > 0)
                return playedIds;
        }

        var randomId = await query
            .OrderBy(_ => EF.Functions.Random())
            .Select(t => t.Id)
            .FirstOrDefaultAsync(ct);

        return randomId == Guid.Empty ? [] : [randomId];
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
        var baseQuery = ApplyExcludeIdsFilter(
            await BuildLightTrackIdQueryAsync(userId, libraryIds, ct),
            excludeIds);

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
        var query = ApplyExcludeIdsFilter(
            await BuildLightTrackIdQueryAsync(userId, libraryIds, ct),
            excludeIds);

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
        var query = await BuildLightTrackIdQueryAsync(userId, libraryIds, ct);

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

        var allowedIds = await FilterToAccessibleIdsAsync(trackIds, userId, libraryIds, ct);
        if (allowedIds.Count == 0)
            return [];

        var tracks = await liteMediaProjection.GetLiteMediasAsync(allowedIds, userId, ct);
        var trackMap = tracks.OfType<MusicTrack>().ToDictionary(t => t.Id);
        return allowedIds
            .Where(trackMap.ContainsKey)
            .Select(id => (BaseMedia)trackMap[id])
            .ToList();
    }

    private async Task<List<Guid>> FilterToAccessibleIdsAsync(
        IReadOnlyList<Guid> trackIds,
        Guid? userId,
        Guid[]? libraryIds,
        CancellationToken ct)
    {
        var idSet = trackIds.ToHashSet();
        var accessible = await BuildLightTrackIdQueryAsync(userId, libraryIds, ct);
        var allowed = await accessible
            .Where(t => idSet.Contains(t.Id))
            .Select(t => t.Id)
            .ToListAsync(ct);
        var allowedSet = allowed.ToHashSet();
        return trackIds.Where(allowedSet.Contains).ToList();
    }

    private async Task<IQueryable<MusicTrack>> BuildLightTrackIdQueryAsync(
        Guid? userId,
        Guid[]? libraryIds,
        CancellationToken ct)
    {
        // Stay on BaseMedia for access filters. WhereNotUserExcluded casts to SerieEpisode,
        // which EF cannot coerce once the query is already OfType<MusicTrack>.
        IQueryable<BaseMedia> accessible = context.Medias
            .AsNoTracking()
            .Where(m => m.Type == MediaType.MusicTrack)
            .WhereHasLibraryAvailability(context);

        if (userId is { } id)
        {
            var sharedProfileId = await currentUser.GetSharedProfileIdAsync(ct);
            accessible = await mediaAccessFilter.ApplyAllAsync(accessible, id, sharedProfileId, ct);
        }
        else
        {
            accessible = mediaAccessFilter.ApplyUnavailablePeerExclusion(accessible);
        }

        var accessibleIds = accessible.Select(m => m.Id);
        var tracks = context.Medias
            .OfType<MusicTrack>()
            .AsNoTracking()
            .Where(t => accessibleIds.Contains(t.Id));

        return ApplyLibraryFilter(tracks, libraryIds);
    }

    private async Task<HashSet<Guid>> GetUserRatedMediaIdsAsync(Guid userId, CancellationToken ct)
    {
        var ids = await context.Ratings
            .OfType<UserRating>()
            .AsNoTracking()
            .Where(r => r.UserId == userId && r.Value > 0)
            .Select(r => r.MediaId)
            .ToListAsync(ct);

        return ids.ToHashSet();
    }

    private IQueryable<MusicTrack> WhereNotRatedByUser(IQueryable<MusicTrack> query, Guid userId) =>
        query.Where(t => !context.Ratings.OfType<UserRating>().Any(r =>
            r.UserId == userId
            && r.Value > 0
            && (r.MediaId == t.Id
                || r.MediaId == t.AlbumId
                || (t.ArtistId != null && r.MediaId == t.ArtistId)
                || (t.Album.ArtistId != null && r.MediaId == t.Album.ArtistId))));

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
        int limit,
        IReadOnlySet<Guid>? ratedMediaIds = null)
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
            .Where(t => !HasBeenPlayed(t, uid) && !HasUserRating(t, uid, ratedMediaIds))
            .ToList();

        if (neverPlayed.Count >= limit)
            return neverPlayed.Take(limit).Cast<BaseMedia>().ToList();

        var neverPlayedIds = neverPlayed.Select(t => t.Id).ToHashSet();
        var neverRated = ordered
            .Where(t => !neverPlayedIds.Contains(t.Id))
            .Where(t => !HasUserRating(t, uid, ratedMediaIds))
            .ToList();

        return neverPlayed
            .Concat(neverRated)
            .Take(limit)
            .Cast<BaseMedia>()
            .ToList();
    }

    private static bool HasBeenPlayed(MusicTrack track, Guid userId) =>
        track.UserMediaStates.Any(s => s.UserId == userId && s.PlayCount > 0);

    private static bool HasUserRating(MusicTrack track, Guid userId, IReadOnlySet<Guid>? ratedMediaIds)
    {
        if (ratedMediaIds is not null)
        {
            if (ratedMediaIds.Contains(track.Id) || ratedMediaIds.Contains(track.AlbumId))
                return true;
            if (track.ArtistId is { } artistId && ratedMediaIds.Contains(artistId))
                return true;
            if (track.Album?.ArtistId is { } albumArtistId && ratedMediaIds.Contains(albumArtistId))
                return true;
            return false;
        }

        return track.Ratings.OfType<UserRating>().Any(r => r.UserId == userId && r.Value > 0)
            || (track.Album?.Ratings.OfType<UserRating>().Any(r => r.UserId == userId && r.Value > 0) ?? false)
            || (track.Artist?.Ratings.OfType<UserRating>().Any(r => r.UserId == userId && r.Value > 0) ?? false)
            || (track.Album?.Artist?.Ratings.OfType<UserRating>().Any(r => r.UserId == userId && r.Value > 0) ?? false);
    }

    private static List<Guid> InterleaveArtistRadio(
        IReadOnlyList<Guid> artistIds,
        IReadOnlyList<Guid> similarIds,
        int limit)
    {
        if (similarIds.Count == 0)
            return artistIds.Take(limit).ToList();

        var result = new List<Guid>(Math.Min(limit, artistIds.Count + similarIds.Count));
        var seen = new HashSet<Guid>();
        var artistIndex = 0;
        var similarIndex = 0;
        while (result.Count < limit && (artistIndex < artistIds.Count || similarIndex < similarIds.Count))
        {
            if (artistIndex < artistIds.Count && seen.Add(artistIds[artistIndex]))
                result.Add(artistIds[artistIndex]);
            artistIndex++;
            if (result.Count >= limit)
                break;

            for (var slot = 0; slot < 2 && result.Count < limit && similarIndex < similarIds.Count; slot++)
            {
                if (seen.Add(similarIds[similarIndex]))
                    result.Add(similarIds[similarIndex]);
                similarIndex++;
            }
        }

        return result;
    }

    private static List<Guid> InterleaveUnique(IReadOnlyList<List<Guid>> batches, HashSet<Guid> seen)
    {
        var result = new List<Guid>();
        var max = 0;
        foreach (var batch in batches)
            max = Math.Max(max, batch.Count);

        for (var i = 0; i < max; i++)
        {
            foreach (var batch in batches)
            {
                if (i >= batch.Count)
                    continue;
                if (seen.Add(batch[i]))
                    result.Add(batch[i]);
            }
        }

        return result;
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
