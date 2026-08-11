using K7.Server.Application.Common;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Models;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Users;
using K7.Server.Domain.Enums;
using K7.Shared;
using K7.Shared.Dtos;
using K7.Shared.Navigation;

namespace K7.Server.Application.Features.Stats.Queries.GetPlaybackHistory;

[Authorize(Roles = $"{Roles.Guest},{Roles.User},{Roles.Administrator}")]
public record GetPlaybackHistoryQuery : IRequest<PlaybackHistoryPageDto>
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = PagingDefaults.HistoryPageSize;
    public MediaType? MediaType { get; init; }
    public Guid? UserId { get; init; }
    public bool IncludeStreamQuality { get; init; }
    public bool ShowAllUsers { get; init; }
    public string Period { get; init; } = "month";
    public DateTime? From { get; init; }
    public DateTime? To { get; init; }
}

public class GetPlaybackHistoryQueryHandler(IApplicationDbContext context, IUser currentUser, IIdentityService identityService)
    : IRequestHandler<GetPlaybackHistoryQuery, PlaybackHistoryPageDto>
{
    public async Task<PlaybackHistoryPageDto> Handle(GetPlaybackHistoryQuery request, CancellationToken cancellationToken)
    {
        IQueryable<MediaPlaybackSession> sessionsQuery;

        if (request.ShowAllUsers)
        {
            sessionsQuery = context.MediaPlaybackSessions.AsQueryable();

            if (request.UserId.HasValue)
            {
                var coViewerReferenceIds = context.MediaPlaybackSessionCoViewers
                    .Where(c => c.UserId == request.UserId.Value)
                    .Select(c => c.ReferenceId);

                sessionsQuery = sessionsQuery.Where(s =>
                    s.UserId == request.UserId.Value || coViewerReferenceIds.Contains(s.ReferenceId));
            }
        }
        else
        {
            var sharedProfileId = await currentUser.GetSharedProfileIdAsync(cancellationToken);
            if (sharedProfileId is { } profileId)
            {
                sessionsQuery = context.MediaPlaybackSessions
                    .Where(s => s.SharedProfileId == profileId);
            }
            else
            {
                var targetUserId = request.UserId ?? currentUser.Id;
                if (targetUserId is null)
                    return new PlaybackHistoryPageDto();

                // Personal history includes sessions the user drove and sessions where they were
                // a co-viewer (shared-profile members and SyncPlay partners).
                var coViewerReferenceIds = context.MediaPlaybackSessionCoViewers
                    .Where(c => c.UserId == targetUserId.Value)
                    .Select(c => c.ReferenceId);

                sessionsQuery = context.MediaPlaybackSessions
                    .Where(s => s.UserId == targetUserId.Value
                        || coViewerReferenceIds.Contains(s.ReferenceId));
            }
        }

        if (request.MediaType.HasValue)
        {
            sessionsQuery = sessionsQuery
                .Where(s => s.Media.Type == request.MediaType.Value);
        }

        if (request.Period == "custom")
        {
            if (request.From is not null)
            {
                sessionsQuery = sessionsQuery.Where(s => s.StartedAt >= request.From.Value.ToUniversalTime());
            }

            if (request.To is not null)
            {
                sessionsQuery = sessionsQuery.Where(s => s.StartedAt <= request.To.Value.ToUniversalTime());
            }
        }
        else if (GetPeriodStart(request.Period) is { } since)
        {
            sessionsQuery = sessionsQuery.Where(s => s.StartedAt >= since);
        }

        var groupedQuery = sessionsQuery
            .GroupBy(s => s.ReferenceId)
            .Select(g => new
            {
                ReferenceId = g.Key,
                StartedAt = g.Min(s => s.StartedAt),
                StoppedAt = g.Max(s => s.StoppedAt),
                TotalWatchedSeconds = g.Sum(s =>
                    s.WatchedDurationSeconds > 0
                        ? s.WatchedDurationSeconds
                        : s.PositionSeconds > 0
                            ? s.PositionSeconds
                            : 0),
                SegmentCount = g.Count(),
                IsCompleted = g.Any(s => s.CompletedAt != null)
                    || g.Any(s => s.DurationSeconds > 0 && s.PositionSeconds / s.DurationSeconds >= 0.9)
                    || g.Any(s => s.DurationSeconds > 0 && s.WatchedDurationSeconds / s.DurationSeconds >= 0.9),
                IsFinished = g.All(s =>
                    s.State == PlaybackState.Ended
                    || s.State == PlaybackState.Idle
                    || s.StoppedAt != null),
                MediaId = g.First().MediaId,
                DeviceId = g.First().DeviceId,
                UserId = g.First().UserId,
                SharedProfileName = g.First().SharedProfileNameSnapshot,
                CoWatchingWithSnapshot = g.First().CoWatchingWithSnapshot
            });

        var totalCount = await groupedQuery.CountAsync(cancellationToken);

        var pagedGroups = await groupedQuery
            .OrderByDescending(g => g.StartedAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var mediaIds = pagedGroups.Select(g => g.MediaId).Distinct().ToList();
        var medias = await context.Medias
            .Where(m => mediaIds.Contains(m.Id))
            .Select(m => new { m.Id, m.Title, m.Type })
            .ToDictionaryAsync(m => m.Id, cancellationToken);

        var episodeNavById = await context.Medias.OfType<SerieEpisode>()
            .Where(e => mediaIds.Contains(e.Id))
            .Select(e => new EpisodeNav(
                e.Id,
                e.SerieId,
                e.Serie!.Title,
                e.Season.SeasonNumber,
                e.EpisodeNumber))
            .ToDictionaryAsync(e => e.Id, cancellationToken);

        var trackNavById = await context.Medias.OfType<MusicTrack>()
            .Where(t => mediaIds.Contains(t.Id))
            .Select(t => new TrackNav(
                t.Id,
                t.AlbumId,
                t.Artist != null ? t.Artist.Title : t.Album!.Artist!.Title))
            .ToDictionaryAsync(t => t.Id, cancellationToken);

        var libraryByMedia = await context.IndexedFiles
            .Where(f => f.MediaId != null && mediaIds.Contains(f.MediaId.Value))
            .Join(context.Libraries, f => f.LibraryId, l => l.Id, (f, l) => new { f.MediaId, LibraryName = l.Title })
            .GroupBy(x => x.MediaId!.Value)
            .Select(g => new { MediaId = g.Key, LibraryName = g.First().LibraryName })
            .ToDictionaryAsync(x => x.MediaId, x => x.LibraryName, cancellationToken);

        var coverPictureIds = await MediaCoverPictureResolver.GetCoverPictureIdsByMediaIdAsync(
            context,
            mediaIds,
            cancellationToken);

        var deviceIds = pagedGroups.Where(g => g.DeviceId.HasValue).Select(g => g.DeviceId!.Value).Distinct().ToList();
        var devices = deviceIds.Count > 0
            ? await context.Devices
                .Where(d => deviceIds.Contains(d.Id))
                .Select(d => new { d.Id, d.DeviceName, d.ClientType })
                .ToDictionaryAsync(d => d.Id, cancellationToken)
            : [];

        var userIds = pagedGroups.Select(g => g.UserId).Distinct().ToList();
        var users = await context.Users
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.DisplayName, u.IdentityUserId })
            .ToListAsync(cancellationToken);

        var identityIdsNeedingLookup = users
            .Where(u => u.DisplayName is null && u.IdentityUserId is not null)
            .Select(u => u.IdentityUserId!)
            .Distinct()
            .ToList();

        var identityUserNames = identityIdsNeedingLookup.Count > 0
            ? await identityService.GetUserNamesAsync(identityIdsNeedingLookup)
            : new Dictionary<string, string?>();

        var userNames = users.ToDictionary(
            u => u.Id,
            u => u.DisplayName ?? (u.IdentityUserId is not null
                ? identityUserNames.GetValueOrDefault(u.IdentityUserId)
                : null));

        Dictionary<Guid, Domain.Entities.Users.PlaybackSessionDetails?>? detailsByRef = null;
        if (request.IncludeStreamQuality)
        {
            var referenceIds = pagedGroups.Select(g => g.ReferenceId).ToList();
            var sessionIds = await context.MediaPlaybackSessions
                .Where(s => referenceIds.Contains(s.ReferenceId))
                .Select(s => s.Id)
                .ToListAsync(cancellationToken);

            var details = await context.PlaybackSessionDetails
                .Where(d => sessionIds.Contains(d.MediaPlaybackSessionId))
                .Include(d => d.MediaPlaybackSession)
                .ToListAsync(cancellationToken);

            detailsByRef = details
                .GroupBy(d => d.MediaPlaybackSession.ReferenceId)
                .ToDictionary(
                    g => g.Key,
                    g => g
                        .OrderByDescending(d => d.VideoDecision is not null || d.IsTranscode is not null)
                        .ThenByDescending(d => d.MediaPlaybackSession.LastUpdateAt ?? d.MediaPlaybackSession.StartedAt)
                        .FirstOrDefault());
        }

        var items = pagedGroups.Select(g =>
        {
            var media = medias.GetValueOrDefault(g.MediaId);
            episodeNavById.TryGetValue(g.MediaId, out var episodeNav);
            trackNavById.TryGetValue(g.MediaId, out var trackNav);
            StreamQualityDto? quality = null;

            if (detailsByRef is not null && detailsByRef.TryGetValue(g.ReferenceId, out var details) && details is not null)
            {
                quality = new StreamQualityDto
                {
                    IsTranscode = details.IsTranscode,
                    VideoDecision = details.VideoDecision,
                    AudioDecision = details.AudioDecision,
                    TranscodeReason = details.TranscodeReason?.ToString(),
                    SourceResolution = details.SourceVideoWidth.HasValue && details.SourceVideoHeight.HasValue
                        ? $"{details.SourceVideoWidth}x{details.SourceVideoHeight}" : null,
                    SourceVideoCodec = details.SourceVideoCodec,
                    SourceAudioCodec = details.SourceAudioCodec,
                    StreamVideoCodec = details.StreamVideoCodec,
                    StreamAudioCodec = details.StreamAudioCodec,
                    Resolution = details.SourceVideoWidth.HasValue && details.SourceVideoHeight.HasValue
                        ? $"{details.SourceVideoWidth}x{details.SourceVideoHeight}" : null,
                    VideoCodec = details.StreamVideoCodec ?? details.SourceVideoCodec,
                    AudioCodec = details.StreamAudioCodec ?? details.SourceAudioCodec,
                    Bitrate = details.Bitrate,
                    AudioTrackLanguage = details.AudioTrackLanguage,
                    AudioTrackTitle = details.AudioTrackTitle,
                    AudioChannelLayout = details.AudioChannelLayout,
                    SubtitleTrackLanguage = details.SubtitleTrackLanguage,
                    SubtitleTrackTitle = details.SubtitleTrackTitle
                };
            }

            return new PlaybackHistoryItemDto
            {
                ReferenceId = g.ReferenceId,
                MediaId = g.MediaId,
                MediaTitle = FormatHistoryMediaTitle(media?.Title, media?.Type, episodeNav, trackNav),
                MediaType = media?.Type.ToString(),
                MediaUrl = media is not null
                    ? MediaPageUrls.Build(
                        media.Type,
                        g.MediaId,
                        episodeNav?.SerieId,
                        episodeNav?.SeasonNumber,
                        episodeNav?.EpisodeNumber,
                        trackNav?.AlbumId)
                    : null,
                LibraryName = libraryByMedia.GetValueOrDefault(g.MediaId),
                ImageUrl = MediaCoverPictureResolver.ToSmallPictureUrl(coverPictureIds.GetValueOrDefault(g.MediaId)),
                StartedAt = g.StartedAt,
                StoppedAt = g.StoppedAt,
                TotalWatchedSeconds = g.TotalWatchedSeconds,
                SegmentCount = g.SegmentCount,
                DeviceName = g.DeviceId.HasValue && devices.TryGetValue(g.DeviceId.Value, out var dev) ? dev.DeviceName : null,
                DeviceClient = g.DeviceId.HasValue && devices.TryGetValue(g.DeviceId.Value, out var devClient) ? devClient.ClientType.ToString() : null,
                IsCompleted = g.IsCompleted,
                IsSkipped = !g.IsCompleted && g.IsFinished && g.TotalWatchedSeconds < PlaybackSkipRules.MaxWatchedSeconds,
                UserName = userNames.GetValueOrDefault(g.UserId),
                SharedProfileName = g.SharedProfileName ?? g.CoWatchingWithSnapshot,
                StreamQuality = quality
            };
        }).ToList();

        return new PlaybackHistoryPageDto
        {
            Items = items,
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize
        };
    }

    private static string? FormatHistoryMediaTitle(
        string? title,
        MediaType? mediaType,
        EpisodeNav? episodeNav,
        TrackNav? trackNav)
    {
        if (mediaType == MediaType.SerieEpisode && episodeNav is not null)
        {
            return MediaDisplayTitles.FormatEpisode(
                episodeNav.SerieTitle,
                title,
                episodeNav.SeasonNumber,
                episodeNav.EpisodeNumber);
        }

        if (mediaType == MediaType.MusicTrack && trackNav is not null)
            return MediaDisplayTitles.FormatTrack(trackNav.ArtistTitle, title);

        return title;
    }

    private static DateTime? GetPeriodStart(string period) => period switch
    {
        "week" => DateTime.UtcNow.AddDays(-7),
        "month" => DateTime.UtcNow.AddMonths(-1),
        "year" => DateTime.UtcNow.AddYears(-1),
        _ => null
    };

    private sealed record EpisodeNav(
        Guid Id,
        Guid SerieId,
        string? SerieTitle,
        int SeasonNumber,
        int EpisodeNumber);

    private sealed record TrackNav(Guid Id, Guid AlbumId, string? ArtistTitle);
}
