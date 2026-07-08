using K7.Server.Application.Common.Helpers;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Application.Services;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Users;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Events;
using K7.Shared.Dtos;
using K7.Shared.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace K7.Server.Application.Features.Medias.Commands.UpdatePlaybackProgress;

[Authorize(Roles = $"{Roles.Guest},{Roles.User},{Roles.Administrator}")]
public record UpdatePlaybackProgressCommand(
    Guid MediaId,
    Guid SessionId,
    Guid ReferenceId,
    double Position,
    double Duration,
    PlaybackState State,
    Guid? DeviceId = null,
    Guid? PlaylistId = null,
    Guid? SharedProfileId = null,
    Guid? SyncPlayGroupId = null) : IRequest;

public class UpdatePlaybackProgressCommandHandler(
    IApplicationDbContext context,
    IUser currentUserService,
    IPlaybackProgressNotifier progressNotifier,
    IMediaAccessGuard accessGuard,
    IActiveStreamTracker activeStreamTracker,
    IIdentityService identityService,
    IMediaQueryCacheInvalidator cacheInvalidator,
    INextEpisodeEnqueueService nextEpisodeEnqueueService,
    IUserMediaStateUpdater userMediaStateUpdater,
    ISharedProfilePlaybackResolver viewingGroupPlaybackResolver,
    ISyncPlayPlaybackContextResolver syncPlayPlaybackContextResolver,
    ILogger<UpdatePlaybackProgressCommandHandler> logger) : IRequestHandler<UpdatePlaybackProgressCommand>
{
    private readonly IApplicationDbContext _context = context;
    private readonly IUser _currentUser = currentUserService;
    private readonly IPlaybackProgressNotifier _progressNotifier = progressNotifier;
    private readonly IMediaAccessGuard _accessGuard = accessGuard;
    private readonly IActiveStreamTracker _activeStreamTracker = activeStreamTracker;
    private readonly IIdentityService _identityService = identityService;
    private readonly IMediaQueryCacheInvalidator _cacheInvalidator = cacheInvalidator;
    private readonly INextEpisodeEnqueueService _nextEpisodeEnqueueService = nextEpisodeEnqueueService;
    private readonly IUserMediaStateUpdater _userMediaStateUpdater = userMediaStateUpdater;
    private readonly ISharedProfilePlaybackResolver _viewingGroupPlaybackResolver = viewingGroupPlaybackResolver;
    private readonly ISyncPlayPlaybackContextResolver _syncPlayPlaybackContextResolver = syncPlayPlaybackContextResolver;
    private readonly ILogger _logger = logger;

    public async Task Handle(UpdatePlaybackProgressCommand request, CancellationToken cancellationToken)
    {
        if (_currentUser.Id is not { } userId)
            return;

        await _accessGuard.EnsureAccessAsync(request.MediaId, cancellationToken);

        var media = await _context.Medias
            .FirstOrDefaultAsync(m => m.Id == request.MediaId, cancellationToken);

        if (media is null) return;

        var timeNow = DateTime.UtcNow;

        var existingSession = await _context.MediaPlaybackSessions
            .FirstOrDefaultAsync(s => s.SessionId == request.SessionId, cancellationToken);

        var previousState = existingSession?.State ?? PlaybackState.Unknown;

        MediaPlaybackSession session;
        var isNewSession = existingSession is null;
        if (existingSession is not null)
        {
            session = existingSession;
            ApplyExistingSessionProgress(session, request, timeNow);
            await SyncSessionDetailsFromStreamDecisionAsync(session, request.SessionId, cancellationToken);
        }
        else
        {
            session = new MediaPlaybackSession
            {
                UserId = userId,
                MediaId = request.MediaId,
                SessionId = request.SessionId,
                ReferenceId = request.ReferenceId,
                StartedAt = timeNow,
                LastUpdateAt = timeNow,
                State = request.State,
                DeviceId = request.DeviceId
            };
            _context.MediaPlaybackSessions.Add(session);

            var streamInfo = _activeStreamTracker.GetStreamInfo(request.SessionId);
            if (streamInfo?.StreamDecision is { } sd)
            {
                session.Details = CreateDetailsFromStreamDecision(sd);
            }
        }

        session.PositionSeconds = request.Position;
        session.DurationSeconds = request.Duration;

        SharedProfilePlaybackContext? viewingGroup = null;
        if (request.SharedProfileId is { } sharedProfileId)
        {
            viewingGroup = await _viewingGroupPlaybackResolver.ResolveAsync(sharedProfileId, userId, cancellationToken);
            if (viewingGroup is not null)
            {
                session.SharedProfileId ??= viewingGroup.SharedProfileId;
                session.SharedProfileNameSnapshot ??= viewingGroup.GroupName;
                await EnsureCoViewersAsync(request.ReferenceId, viewingGroup.CoViewerUserIds, cancellationToken);
            }
        }

        SyncPlayPlaybackContext? syncPlay = null;
        if (request.SyncPlayGroupId is { } syncPlayGroupId)
        {
            syncPlay = await _syncPlayPlaybackContextResolver.ResolveAsync(
                syncPlayGroupId,
                userId,
                _currentUser.IdentityId,
                cancellationToken);

            if (syncPlay is not null)
            {
                session.CoWatchingWithSnapshot ??= syncPlay.CoWatchingWithSnapshot;
                await EnsureCoViewersAsync(request.ReferenceId, syncPlay.CoViewerUserIds, cancellationToken);
            }
        }

        var hostResult = await _userMediaStateUpdater.ApplyAsync(
            userId, media, request.MediaId, request.Position, request.Duration, timeNow, cancellationToken);

        var progress = request.Duration > 0 ? request.Position / request.Duration : 0;
        var isMusic = media.Type == MediaType.MusicTrack;
        var completed = isMusic
            ? progress >= 0.50 || request.Position >= 240
            : progress >= 0.80;

        if (completed && session.CompletedAt is null)
        {
            session.CompletedAt = timeNow;
            session.AddDomainEvent(MediaPlaybackCompletedEvent<BaseMedia>.Create(session, media));
        }

        if (hostResult.EpisodeIdForEnqueue is { } hostEpisodeId)
            await _nextEpisodeEnqueueService.EnqueueNextEpisodeAsync(userId, hostEpisodeId, timeNow, cancellationToken);

        var notifiedUsers = new List<(Guid UserId, UserMediaStateUpdateResult Result)> { (userId, hostResult) };

        if (viewingGroup is not null)
        {
            foreach (var coViewerUserId in viewingGroup.CoViewerUserIds)
            {
                if (!await _accessGuard.CanAccessAsync(request.MediaId, coViewerUserId, cancellationToken))
                    continue;

                var coResult = await _userMediaStateUpdater.ApplyAsync(
                    coViewerUserId, media, request.MediaId, request.Position, request.Duration, timeNow, cancellationToken);

                notifiedUsers.Add((coViewerUserId, coResult));
            }
        }

        if (request.State != previousState)
        {
            var libraryTitle = await _context.IndexedFiles
                .Where(f => f.MediaId == request.MediaId)
                .Join(_context.Libraries, f => f.LibraryId, l => l.Id, (_, l) => l.Title)
                .FirstOrDefaultAsync(cancellationToken);

            var deviceInfo = request.DeviceId.HasValue
                ? await _context.Devices
                    .Where(d => d.Id == request.DeviceId.Value)
                    .Select(d => new { d.DeviceName, DeviceType = d.DeviceType.ToString() })
                    .FirstOrDefaultAsync(cancellationToken)
                : null;

            var identityId = _currentUser.IdentityId;
            var notifUserName = !string.IsNullOrEmpty(identityId)
                ? await _identityService.GetUserNameAsync(identityId)
                : null;

            session.AddDomainEvent(new PlaybackStateChangedEvent(
                request.State,
                previousState,
                userId,
                notifUserName,
                request.MediaId,
                media.Title ?? "",
                media.Type.ToString(),
                request.SessionId,
                request.Position,
                request.Duration,
                libraryTitle,
                deviceInfo?.DeviceName,
                deviceInfo?.DeviceType));
        }

        if (request.PlaylistId is { } playlistId)
            await UserPlaylistStateHelper.TouchLastListenedAsync(_context, userId, playlistId, cancellationToken);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (isNewSession && IsDuplicateSessionId(ex))
        {
            DetachSessionGraph(session);

            session = await _context.MediaPlaybackSessions
                .FirstOrDefaultAsync(s => s.SessionId == request.SessionId, cancellationToken)
                ?? throw new InvalidOperationException(
                    $"Playback session {request.SessionId} was not found after duplicate insert conflict.");

            ApplyExistingSessionProgress(session, request, timeNow);
            await SyncSessionDetailsFromStreamDecisionAsync(session, request.SessionId, cancellationToken);

            session.PositionSeconds = request.Position;
            session.DurationSeconds = request.Duration;

            if (completed && session.CompletedAt is null)
            {
                session.CompletedAt = timeNow;
                session.AddDomainEvent(MediaPlaybackCompletedEvent<BaseMedia>.Create(session, media));
            }

            await _context.SaveChangesAsync(cancellationToken);
        }

        if (request.State is PlaybackState.Playing or PlaybackState.Buffering or PlaybackState.Paused)
        {
            var device = request.DeviceId.HasValue
                ? await _context.Devices
                    .Where(d => d.Id == request.DeviceId.Value)
                    .Select(d => new { d.DeviceName, DeviceType = d.DeviceType.ToString() })
                    .FirstOrDefaultAsync(cancellationToken)
                : null;

            var identityId = _currentUser.IdentityId;
            var userName = !string.IsNullOrEmpty(identityId)
                ? await _identityService.GetUserNameAsync(identityId)
                : null;

            string? thumbnailUrl = null;
            var thumbnailPictureId = await _context.Medias
                .Where(m => m.Id == request.MediaId)
                .SelectMany(m => m.Pictures)
                .Where(p => p.Type == MetadataPictureType.Backdrop
                    || p.Type == MetadataPictureType.Still
                    || p.Type == MetadataPictureType.Poster
                    || p.Type == MetadataPictureType.Cover)
                .OrderBy(p => p.Type)
                .Select(p => (Guid?)p.Id)
                .FirstOrDefaultAsync(cancellationToken);

            // Fallback to parent (album/serie) pictures for tracks/episodes
            if (!thumbnailPictureId.HasValue && media is MusicTrack mt)
            {
                thumbnailPictureId = await _context.Medias
                    .Where(m => m.Id == mt.AlbumId)
                    .SelectMany(m => m.Pictures)
                    .Where(p => p.Type == MetadataPictureType.Cover || p.Type == MetadataPictureType.Poster)
                    .OrderBy(p => p.Type)
                    .Select(p => (Guid?)p.Id)
                    .FirstOrDefaultAsync(cancellationToken);
            }
            else if (!thumbnailPictureId.HasValue && media is SerieEpisode episode)
            {
                thumbnailPictureId = await _context.Medias
                    .Where(m => m.Id == episode.SerieId)
                    .SelectMany(m => m.Pictures)
                    .Where(p => p.Type == MetadataPictureType.Backdrop || p.Type == MetadataPictureType.Poster)
                    .OrderBy(p => p.Type)
                    .Select(p => (Guid?)p.Id)
                    .FirstOrDefaultAsync(cancellationToken);
            }

            if (thumbnailPictureId.HasValue)
            {
                thumbnailUrl = $"/api/metadata-pictures/{thumbnailPictureId.Value}?size=Small";
            }

            _activeStreamTracker.Upsert(request.SessionId, new ActiveStreamInfo
            {
                SessionId = request.SessionId,
                IdentityUserId = identityId ?? userId.ToString(),
                UserId = userId,
                UserName = userName,
                MediaId = request.MediaId,
                MediaTitle = media.Title,
                MediaType = media.Type.ToString(),
                ParentId = media is MusicTrack track ? track.AlbumId
                    : media is SerieEpisode ep ? ep.SerieId
                    : null,
                DeviceId = request.DeviceId,
                DeviceName = device?.DeviceName,
                DeviceType = device?.DeviceType,
                ThumbnailUrl = thumbnailUrl,
                StartedAt = session.StartedAt,
                Position = request.Position,
                Duration = request.Duration,
                State = (int)request.State,
                SharedProfileName = session.SharedProfileNameSnapshot ?? session.CoWatchingWithSnapshot
            });
        }
        else
        {
            _activeStreamTracker.Remove(request.SessionId);
        }

        _cacheInvalidator.InvalidateAll();

        var identityByUserId = await _context.Users
            .AsNoTracking()
            .Where(u => notifiedUsers.Select(n => n.UserId).Contains(u.Id) && u.IdentityUserId != null)
            .Select(u => new { u.Id, u.IdentityUserId })
            .ToDictionaryAsync(u => u.Id, u => u.IdentityUserId!, cancellationToken);

        foreach (var (notifiedUserId, result) in notifiedUsers)
        {
            _logger.LogDebug(
                "Playback progress updated: userId={UserId}, mediaId={MediaId}, progress={Progress:F1}%",
                notifiedUserId, request.MediaId, result.ProgressPercentage);

            if (!identityByUserId.TryGetValue(notifiedUserId, out var identityUserId))
                continue;

            await _progressNotifier.NotifyProgressUpdatedAsync(
                identityUserId,
                request.MediaId,
                result.ProgressPercentage,
                result.IsCompleted,
                cancellationToken);
        }
    }

    private static void ApplyExistingSessionProgress(
        MediaPlaybackSession session,
        UpdatePlaybackProgressCommand request,
        DateTime timeNow)
    {
        if (session.State == PlaybackState.Playing && session.LastUpdateAt.HasValue)
        {
            var delta = (timeNow - session.LastUpdateAt.Value).TotalSeconds;
            if (delta is > 0 and < 120)
            {
                session.WatchedDurationSeconds += delta;
            }
        }

        if (request.State is PlaybackState.Paused or PlaybackState.Ended or PlaybackState.Idle
            && session.State == PlaybackState.Playing)
        {
            session.StoppedAt = timeNow;
        }

        session.LastUpdateAt = timeNow;
        session.State = request.State;
    }

    private void DetachSessionGraph(MediaPlaybackSession session)
    {
        if (session.Details is not null)
            _context.Entry(session.Details).State = EntityState.Detached;

        _context.Entry(session).State = EntityState.Detached;
    }

    private static bool IsDuplicateSessionId(DbUpdateException ex) =>
        ex.InnerException?.Message.Contains("IX_MediaPlaybackSessions_SessionId", StringComparison.OrdinalIgnoreCase) == true
        || ex.InnerException?.Message.Contains("23505", StringComparison.Ordinal) == true;

    private async Task EnsureCoViewersAsync(
        Guid referenceId,
        IReadOnlyList<Guid> coViewerUserIds,
        CancellationToken cancellationToken)
    {
        if (coViewerUserIds.Count == 0)
            return;

        var existing = await _context.MediaPlaybackSessionCoViewers
            .Where(c => c.ReferenceId == referenceId)
            .Select(c => c.UserId)
            .ToListAsync(cancellationToken);

        foreach (var coViewerUserId in coViewerUserIds.Where(id => !existing.Contains(id)))
        {
            _context.MediaPlaybackSessionCoViewers.Add(new MediaPlaybackSessionCoViewer
            {
                ReferenceId = referenceId,
                UserId = coViewerUserId
            });
        }
    }

    private async Task SyncSessionDetailsFromStreamDecisionAsync(
        MediaPlaybackSession session,
        Guid streamSessionId,
        CancellationToken cancellationToken)
    {
        var streamInfo = _activeStreamTracker.GetStreamInfo(streamSessionId);
        if (streamInfo?.StreamDecision is not { } sd)
            return;

        var details = session.Details
            ?? await _context.PlaybackSessionDetails
                .FirstOrDefaultAsync(d => d.MediaPlaybackSessionId == session.Id, cancellationToken);

        if (details is null)
        {
            details = CreateDetailsFromStreamDecision(sd);
            details.MediaPlaybackSessionId = session.Id;
            _context.PlaybackSessionDetails.Add(details);
            session.Details = details;
            return;
        }

        ApplyStreamDecisionToDetails(details, sd);
    }

    private static PlaybackSessionDetails CreateDetailsFromStreamDecision(StreamDecisionDto sd)
    {
        var details = new PlaybackSessionDetails();
        ApplyStreamDecisionToDetails(details, sd);
        return details;
    }

    private static void ApplyStreamDecisionToDetails(PlaybackSessionDetails details, StreamDecisionDto sd)
    {
        var videoIsTranscoded = IsVideoTranscoded(sd);
        var audioIsTranscoded = sd.SourceAudioCodec is not null
            && sd.StreamAudioCodec is not null
            && !string.Equals(sd.SourceAudioCodec, sd.StreamAudioCodec, StringComparison.OrdinalIgnoreCase);
        var isTransmux = sd.Mode == PlaybackMode.Transmux;

        details.IsTranscode = videoIsTranscoded || audioIsTranscoded;
        details.VideoDecision = videoIsTranscoded ? "Transcode" : isTransmux ? "Transmux" : "Direct";
        details.AudioDecision = audioIsTranscoded ? "Transcode" : isTransmux ? "Transmux" : "Direct";
        details.TranscodeReason = sd.Reason != TranscodeReason.None ? sd.Reason : null;
        details.Bitrate = sd.Bitrate;
        details.SourceVideoCodec = sd.SourceVideoCodec;
        details.SourceAudioCodec = sd.SourceAudioCodec;
        details.SourceVideoWidth = ParseResolutionWidth(sd.SourceResolution);
        details.SourceVideoHeight = ParseResolutionHeight(sd.SourceResolution);
        details.StreamVideoCodec = sd.StreamVideoCodec;
        details.StreamAudioCodec = sd.StreamAudioCodec;
        details.AudioTrackLanguage = sd.AudioTrackLanguage;
        details.AudioTrackTitle = sd.AudioTrackTitle;
        details.AudioChannelLayout = sd.AudioChannelLayout;

        if (sd.SubtitleTrackLanguage is not null || sd.SubtitleTrackTitle is not null)
        {
            details.SubtitleTrackLanguage = sd.SubtitleTrackLanguage ?? details.SubtitleTrackLanguage;
            details.SubtitleTrackTitle = sd.SubtitleTrackTitle ?? details.SubtitleTrackTitle;
        }
    }

    private static bool IsVideoTranscoded(StreamDecisionDto sd) =>
        sd.IsSubtitleBurnIn
        || sd.Reason.HasFlag(TranscodeReason.SubtitlesBurnIn)
        || (sd.SourceVideoCodec is not null
            && sd.StreamVideoCodec is not null
            && !string.Equals(sd.SourceVideoCodec, sd.StreamVideoCodec, StringComparison.OrdinalIgnoreCase));

    private static int? ParseResolutionWidth(string? resolution)
    {
        if (resolution is null) return null;
        var parts = resolution.Split('x');
        return parts.Length == 2 && int.TryParse(parts[0], out var w) ? w : null;
    }

    private static int? ParseResolutionHeight(string? resolution)
    {
        if (resolution is null) return null;
        var parts = resolution.Split('x');
        return parts.Length == 2 && int.TryParse(parts[1], out var h) ? h : null;
    }
}
