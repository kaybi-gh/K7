using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Mappings;
using K7.Server.Application.Common.Services;
using K7.Server.Application.Helpers;
using K7.Server.Application.Services;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Users;
using K7.Shared.Dtos.Entities;
using K7.Shared.Dtos.Entities.Medias;

namespace K7.Server.Application.Features.Medias.Queries.GetNextEpisode;

/// <summary>
/// With <paramref name="CurrentEpisodeId"/>: the episode that follows it (player "next episode").
/// Without: the episode the current user should play next on the series page (in progress,
/// otherwise first unwatched, otherwise the first episode).
/// </summary>
public record GetNextEpisodeQuery(Guid SerieId, Guid? CurrentEpisodeId = null) : IRequest<LiteSerieEpisodeDto?>;

public class GetNextEpisodeQueryHandler(
    IApplicationDbContext context,
    IUser currentUser,
    IMediaAccessGuard accessGuard,
    IPlaybackBookmarkService bookmarkService)
    : IRequestHandler<GetNextEpisodeQuery, LiteSerieEpisodeDto?>
{
    public Task<LiteSerieEpisodeDto?> Handle(GetNextEpisodeQuery request, CancellationToken cancellationToken) =>
        request.CurrentEpisodeId is { } currentEpisodeId
            ? GetFollowingEpisodeAsync(request.SerieId, currentEpisodeId, cancellationToken)
            : GetEpisodeToPlayAsync(request.SerieId, cancellationToken);

    private async Task<LiteSerieEpisodeDto?> GetFollowingEpisodeAsync(
        Guid serieId,
        Guid currentEpisodeId,
        CancellationToken cancellationToken)
    {
        var currentEpisode = await context.Medias
            .AsNoTracking()
            .OfType<SerieEpisode>()
            .Where(e => e.Id == currentEpisodeId && e.SerieId == serieId)
            .Select(e => new { e.SeasonId, e.EpisodeNumber, e.Season.SeasonNumber })
            .SingleOrDefaultAsync(cancellationToken);

        Guard.Against.NotFound(currentEpisodeId, currentEpisode);

        Guid? userId = currentUser.Id;

        var nextInSeason = await BuildEpisodeQuery(userId)
            .Where(e => e.SeasonId == currentEpisode.SeasonId && e.EpisodeNumber > currentEpisode.EpisodeNumber)
            .OrderBy(e => e.EpisodeNumber)
            .FirstOrDefaultAsync(cancellationToken);

        if (nextInSeason is not null)
            return (LiteSerieEpisodeDto)nextInSeason.ToLiteMediaDto();

        var nextSeasonFirstEpisode = await BuildEpisodeQuery(userId)
            .Where(e => e.SerieId == serieId
                && e.Season.SeasonNumber > currentEpisode.SeasonNumber)
            .OrderBy(e => e.Season.SeasonNumber)
            .ThenBy(e => e.EpisodeNumber)
            .FirstOrDefaultAsync(cancellationToken);

        if (nextSeasonFirstEpisode is not null)
            return (LiteSerieEpisodeDto)nextSeasonFirstEpisode.ToLiteMediaDto();

        return null;
    }

    private async Task<LiteSerieEpisodeDto?> GetEpisodeToPlayAsync(Guid serieId, CancellationToken cancellationToken)
    {
        await accessGuard.EnsureAccessAsync(serieId, cancellationToken);

        Guid? userId = currentUser.Id;
        var sharedProfileId = await currentUser.GetSharedProfileIdAsync(cancellationToken);

        // Same episode set as the series page: playable (local or federated file) and not hidden.
        var episodes = await context.Medias
            .AsNoTracking()
            .OfType<SerieEpisode>()
            .Where(e => e.SerieId == serieId && (e.IndexedFiles.Any() || e.RemoteIndexedFiles.Any()))
            .Select(e => new { e.Id, e.SeasonId, e.Season.SeasonNumber, e.EpisodeNumber })
            .ToListAsync(cancellationToken);

        if (userId is { } exclusionUserId && episodes.Count > 0)
        {
            var excludedIds = (await context.UserMediaExclusions
                .AsNoTracking()
                .Where(x => x.UserId == exclusionUserId && (x.IsAdminExcluded || x.IsSelfExcluded))
                .Select(x => x.MediaId)
                .ToListAsync(cancellationToken))
                .ToHashSet();

            episodes = episodes
                .Where(e => !excludedIds.Contains(e.Id) && !excludedIds.Contains(e.SeasonId) && !excludedIds.Contains(serieId))
                .ToList();
        }

        if (episodes.Count == 0)
            return null;

        var episodeIds = episodes.Select(e => e.Id).ToList();
        var states = await LoadStatesAsync(userId, sharedProfileId, episodeIds, cancellationToken);
        var bookmarks = userId.HasValue
            ? await bookmarkService.GetItemBookmarksAsync(
                sharedProfileId is null ? userId : null,
                sharedProfileId,
                episodeIds,
                cancellationToken)
            : [];

        var selectedId = EpisodeToPlaySelector.Select(episodes.Select(e => new EpisodeToPlayCandidate(
            e.Id,
            e.SeasonNumber,
            e.EpisodeNumber,
            ResolveState(e.Id, states, bookmarks))));

        if (selectedId is null)
            return null;

        var episode = await BuildEpisodeQuery(sharedProfileId is null ? userId : null)
            .SingleAsync(e => e.Id == selectedId.Value, cancellationToken);

        if (sharedProfileId is { } profileId && userId is { } actingUserId)
            await SharedProfileUserStateOverlay.ApplyAsync(context, episode, profileId, actingUserId, cancellationToken);

        return (LiteSerieEpisodeDto)episode.ToLiteMediaDto(itemBookmarks: bookmarks);
    }

    private async Task<Dictionary<Guid, UserMediaStateDto>> LoadStatesAsync(
        Guid? userId,
        Guid? sharedProfileId,
        List<Guid> episodeIds,
        CancellationToken cancellationToken)
    {
        // A shared profile replaces personal states entirely (see SharedProfileUserStateOverlay).
        if (sharedProfileId is { } profileId)
        {
            var shared = await context.SharedProfileMediaStates
                .AsNoTracking()
                .Where(s => s.SharedProfileId == profileId && episodeIds.Contains(s.MediaId))
                .ToListAsync(cancellationToken);
            return shared.ToDictionary(s => s.MediaId, s => s.ToUserMediaStateDto());
        }

        if (userId is not { } personalUserId)
            return [];

        var personal = await context.UserMediaStates
            .AsNoTracking()
            .Where(s => s.UserId == personalUserId && episodeIds.Contains(s.MediaId))
            .ToListAsync(cancellationToken);
        return personal.ToDictionary(s => s.MediaId, s => s.ToUserMediaStateDto());
    }

    // Mirrors MediaMappings.MapUserState: the stored state wins, a bookmark alone still counts.
    private static UserMediaStateDto? ResolveState(
        Guid episodeId,
        Dictionary<Guid, UserMediaStateDto> states,
        IReadOnlyDictionary<Guid, ItemPlaybackBookmark> bookmarks)
    {
        bookmarks.TryGetValue(episodeId, out var bookmark);

        if (states.TryGetValue(episodeId, out var state))
        {
            return bookmark is null
                ? state
                : state with
                {
                    LastPlaybackPosition = bookmark.PositionSeconds,
                    ProgressPercentage = bookmark.ProgressPercentage
                };
        }

        return bookmark?.ToUserMediaStateDto();
    }

    private IQueryable<SerieEpisode> BuildEpisodeQuery(Guid? userId)
    {
        var query = context.Medias
            .AsNoTracking()
            .OfType<SerieEpisode>()
            .Include(e => e.Pictures)
                .ThenInclude(p => p.Variants)
            .Include(e => e.IndexedFiles)
                .ThenInclude(f => f.FileMetadata)
            .Include(e => e.RemoteIndexedFiles)
            .Include(e => e.Season)
            .AsSplitQuery()
            .AsQueryable();

        if (userId.HasValue)
        {
            query = query.Include(e => e.UserMediaStates.Where(s => s.UserId == userId.Value));
        }

        return query;
    }
}
