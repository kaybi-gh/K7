using K7.Server.Application.Common;
using K7.Server.Application.Common.Helpers;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Application.Common.Services;
using K7.Server.Application.Services;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Metadatas.Files;
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
    Guid? SyncPlayGroupId = null,
    int? AudioTrackIndex = null,
    int? SubtitleTrackIndex = null) : IRequest;

public class UpdatePlaybackProgressCommandHandler(
    IApplicationDbContext context,
    IUser currentUserService,
    IPlaybackProgressNotifier progressNotifier,
    IMediaAccessGuard accessGuard,
    IActiveStreamTracker activeStreamTracker,
    IIdentityService identityService,
    IMediaQueryCacheInvalidator cacheInvalidator,
    IUserMediaStateUpdater userMediaStateUpdater,
    ISharedProfileMediaStateUpdater sharedProfileMediaStateUpdater,
    IPlaybackPolicySettingsProvider playbackPolicySettingsProvider,
    ISharedProfilePlaybackResolver viewingGroupPlaybackResolver,
    ISyncPlayPlaybackContextResolver syncPlayPlaybackContextResolver,
    IFfmpegCapabilitiesService ffmpegCapabilitiesService,
    ILogger<UpdatePlaybackProgressCommandHandler> logger) : IRequestHandler<UpdatePlaybackProgressCommand>
{
    private readonly IApplicationDbContext _context = context;
    private readonly IUser _currentUser = currentUserService;
    private readonly IPlaybackProgressNotifier _progressNotifier = progressNotifier;
    private readonly IMediaAccessGuard _accessGuard = accessGuard;
    private readonly IActiveStreamTracker _activeStreamTracker = activeStreamTracker;
    private readonly IIdentityService _identityService = identityService;
    private readonly IMediaQueryCacheInvalidator _cacheInvalidator = cacheInvalidator;
    private readonly IUserMediaStateUpdater _userMediaStateUpdater = userMediaStateUpdater;
    private readonly ISharedProfileMediaStateUpdater _sharedProfileMediaStateUpdater = sharedProfileMediaStateUpdater;
    private readonly IPlaybackPolicySettingsProvider _playbackPolicySettingsProvider = playbackPolicySettingsProvider;
    private readonly ISharedProfilePlaybackResolver _viewingGroupPlaybackResolver = viewingGroupPlaybackResolver;
    private readonly ISyncPlayPlaybackContextResolver _syncPlayPlaybackContextResolver = syncPlayPlaybackContextResolver;
    private readonly IFfmpegCapabilitiesService _ffmpegCapabilitiesService = ffmpegCapabilitiesService;
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
        var isGuest = !string.IsNullOrEmpty(_currentUser.IdentityId)
            && await _identityService.IsInRoleAsync(_currentUser.IdentityId, Roles.Guest);

        if (request.State is PlaybackState.Playing or PlaybackState.Buffering or PlaybackState.Paused or PlaybackState.Ended)
            await TryHydrateStreamDecisionAsync(request.SessionId, cancellationToken);

        if (request.AudioTrackIndex is not null || request.SubtitleTrackIndex is not null)
            await ApplyReportedTrackSelectionAsync(request, cancellationToken);

        var existingSession = await _context.MediaPlaybackSessions
            .FirstOrDefaultAsync(s => s.SessionId == request.SessionId, cancellationToken);

        var previousState = existingSession?.State ?? PlaybackState.Unknown;

        MediaPlaybackSession session;
        var isNewSession = existingSession is null;
        if (existingSession is not null)
        {
            session = existingSession;

            // OpenSubsonic reuses one SessionId per user+device across tracks. When the
            // media changes, start a fresh logical play so history and progress estimates reset.
            if (session.MediaId != request.MediaId)
            {
                session.MediaId = request.MediaId;
                session.ReferenceId = request.ReferenceId;
                session.StartedAt = timeNow;
                session.LastUpdateAt = timeNow;
                session.PositionSeconds = 0;
                session.WatchedDurationSeconds = 0;
                session.DurationSeconds = 0;
                session.CompletedAt = null;
                session.State = request.State;
                session.DeviceId = request.DeviceId ?? session.DeviceId;
                previousState = PlaybackState.Unknown;
            }
            else
            {
                ApplyExistingSessionProgress(session, request, timeNow);
            }

            await SyncSessionDetailsFromStreamDecisionAsync(
                session,
                request.SessionId,
                updateTracks: session.CompletedAt is null,
                cancellationToken);
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

        session.PositionSeconds = request.State is PlaybackState.Ended or PlaybackState.Idle
            ? Math.Max(session.PositionSeconds, request.Position)
            : request.Position;
        session.DurationSeconds = request.Duration > 0
            ? request.Duration
            : session.DurationSeconds;

        SharedProfilePlaybackContext? viewingGroup = null;
        var requestedSharedProfileId = request.SharedProfileId ?? await _currentUser.GetSharedProfileIdAsync(cancellationToken);
        if (requestedSharedProfileId is { } sharedProfileId)
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

        // Guests record sessions for admin history / active streams, but do not keep personal
        // continue-watching or watched state.
        var hostResult = !isGuest && viewingGroup is null
            ? await _userMediaStateUpdater.ApplyAsync(
                userId, media, request.MediaId, session.PositionSeconds,
                session.DurationSeconds > 0 ? session.DurationSeconds : request.Duration,
                timeNow, cancellationToken)
            : null;

        var sharedResult = !isGuest && viewingGroup is not null
            ? await _sharedProfileMediaStateUpdater.ApplyAsync(
                viewingGroup.SharedProfileId,
                media,
                request.MediaId,
                session.PositionSeconds,
                session.DurationSeconds > 0 ? session.DurationSeconds : request.Duration,
                timeNow,
                cancellationToken)
            : null;

        var activeSharedProfileId = viewingGroup?.SharedProfileId;
        var videoPolicy = await _playbackPolicySettingsProvider.GetEffectiveVideoPolicyAsync(
            userId, activeSharedProfileId, cancellationToken);
        var audioPolicy = await _playbackPolicySettingsProvider.GetEffectiveAudioPolicyAsync(
            userId, activeSharedProfileId, cancellationToken);
        var progressDuration = session.DurationSeconds > 0 ? session.DurationSeconds : request.Duration;
        var progressPosition = Math.Max(session.PositionSeconds, request.Position);
        var progress = progressDuration > 0 ? progressPosition / progressDuration : 0;
        var isMusic = media.Type == MediaType.MusicTrack;
        var completed = isMusic
            ? progress >= audioPolicy.CompletedThresholdPercent / 100.0
              || progressPosition >= audioPolicy.CompletedMinDurationSeconds
            : progress >= videoPolicy.CompletedThresholdPercent / 100.0;

        if (!completed && (hostResult?.WasNewlyCompleted == true || sharedResult?.WasNewlyCompleted == true))
            completed = true;

        if (!completed && progressDuration > 0 && session.WatchedDurationSeconds > 0)
        {
            var watchedRatio = session.WatchedDurationSeconds / progressDuration;
            var threshold = (isMusic ? audioPolicy.CompletedThresholdPercent : videoPolicy.CompletedThresholdPercent) / 100.0;
            if (watchedRatio >= threshold)
                completed = true;
        }

        var newlyCompletedSession = false;
        if (completed && session.CompletedAt is null)
        {
            session.CompletedAt = timeNow;
            newlyCompletedSession = true;
            session.AddDomainEvent(MediaPlaybackCompletedEvent<BaseMedia>.Create(session, media));
        }

        // Music re-listens: UserMediaState.IsCompleted stays true after the first completion, so
        // RecordProgress would skip PlayCount. Count each newly completed history session.
        if (newlyCompletedSession && isMusic && !isGuest && hostResult is { WasNewlyCompleted: false })
        {
            var state = await _context.UserMediaStates
                .FirstOrDefaultAsync(s => s.UserId == userId && s.MediaId == request.MediaId, cancellationToken);
            if (state is not null)
            {
                state.PlayCount++;
                state.LastInteractedAt = timeNow;
            }
        }

        var isTerminalEnd = request.State is PlaybackState.Ended or PlaybackState.Idle;
        var wasAlreadyTerminal = previousState is PlaybackState.Ended or PlaybackState.Idle;
        var watchedForSkip = PlaybackSkipRules.EffectiveWatchedSeconds(
            session.WatchedDurationSeconds,
            session.PositionSeconds);
        if (!isGuest
            && isTerminalEnd
            && !wasAlreadyTerminal
            && PlaybackSkipRules.IsSkippedListen(completed, isFinished: true, watchedForSkip))
        {
            await IncrementSkipCountAsync(
                userId,
                request.MediaId,
                viewingGroup?.SharedProfileId,
                timeNow,
                cancellationToken);
        }

        // Shared-profile mid-progress stays on SharedProfileMediaState only (personal CW stays clean).
        // On completion, mark the media watched for every member so personal "Vu" badges match the group watch.
        if (!isGuest && viewingGroup is not null && newlyCompletedSession)
        {
            var memberIds = viewingGroup.CoViewerUserIds
                .Append(userId)
                .Distinct()
                .ToList();
            await MarkMembersWatchedAsync(memberIds, request.MediaId, timeNow, cancellationToken);
        }

        var notifiedUsers = new List<(Guid UserId, UserMediaStateUpdateResult Result)>();
        if (hostResult is not null)
            notifiedUsers.Add((userId, hostResult));
        else if (sharedResult is not null)
        {
            notifiedUsers.Add((userId, new UserMediaStateUpdateResult(
                sharedResult.ProgressPercentage,
                sharedResult.IsCompleted,
                sharedResult.WasNewlyCompleted,
                sharedResult.CompletedEpisodeId)));
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

        if (!isGuest && request.PlaylistId is { } playlistId)
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
            await SyncSessionDetailsFromStreamDecisionAsync(
                session,
                request.SessionId,
                updateTracks: session.CompletedAt is null,
                cancellationToken);

            session.PositionSeconds = request.State is PlaybackState.Ended or PlaybackState.Idle
                ? Math.Max(session.PositionSeconds, request.Position)
                : request.Position;
            session.DurationSeconds = request.Duration > 0
                ? request.Duration
                : session.DurationSeconds;

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
                    .Select(d => new { d.DeviceName, ClientType = d.ClientType.ToString(), DeviceType = d.DeviceType.ToString() })
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
                DeviceClient = device?.ClientType,
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
                media.Type,
                cancellationToken);
        }
    }

    private async Task IncrementSkipCountAsync(
        Guid userId,
        Guid mediaId,
        Guid? sharedProfileId,
        DateTime timeNow,
        CancellationToken cancellationToken)
    {
        if (sharedProfileId is { } profileId)
        {
            var sharedState = await _context.SharedProfileMediaStates
                .FirstOrDefaultAsync(s => s.SharedProfileId == profileId && s.MediaId == mediaId, cancellationToken);
            if (sharedState is null)
            {
                sharedState = new SharedProfileMediaState
                {
                    SharedProfileId = profileId,
                    MediaId = mediaId,
                    SkipCount = 1,
                    LastInteractedAt = timeNow
                };
                _context.SharedProfileMediaStates.Add(sharedState);
            }
            else
            {
                sharedState.SkipCount++;
                sharedState.LastInteractedAt = timeNow;
            }

            return;
        }

        var state = await _context.UserMediaStates
            .FirstOrDefaultAsync(s => s.UserId == userId && s.MediaId == mediaId, cancellationToken);
        if (state is null)
        {
            _context.UserMediaStates.Add(new UserMediaState
            {
                UserId = userId,
                MediaId = mediaId,
                SkipCount = 1,
                LastInteractedAt = timeNow
            });
            return;
        }

        state.SkipCount++;
        state.LastInteractedAt = timeNow;
    }

    private static void ApplyExistingSessionProgress(
        MediaPlaybackSession session,
        UpdatePlaybackProgressCommand request,
        DateTime timeNow)
    {
        if (session.State == PlaybackState.Playing && session.LastUpdateAt.HasValue)
        {
            var delta = (timeNow - session.LastUpdateAt.Value).TotalSeconds;
            if (delta > 0)
            {
                // Native clients heartbeat often (< 2 min). OpenSubsonic / reportPlayback may
                // only send start + pause/end, so credit longer gaps when playback stops.
                var maxDelta = request.State is PlaybackState.Ended or PlaybackState.Idle or PlaybackState.Paused
                    ? (session.DurationSeconds > 0 ? session.DurationSeconds : delta)
                    : 120;
                if (delta <= maxDelta)
                    session.WatchedDurationSeconds += delta;
                else if (request.State is PlaybackState.Ended or PlaybackState.Idle or PlaybackState.Paused)
                    session.WatchedDurationSeconds += maxDelta;
            }
        }

        if (request.State is PlaybackState.Paused or PlaybackState.Ended or PlaybackState.Idle
            && session.State == PlaybackState.Playing)
        {
            session.StoppedAt = timeNow;
        }
        else if (request.State is PlaybackState.Ended or PlaybackState.Idle)
        {
            session.StoppedAt ??= timeNow;
        }

        session.LastUpdateAt = timeNow;
        session.State = request.State;

        // Prefer a visible history duration when the client reported a position but we
        // never accumulated watched time (e.g. first pause right after start).
        if (request.State is PlaybackState.Paused or PlaybackState.Ended or PlaybackState.Idle
            && session.WatchedDurationSeconds <= 0
            && request.Position > 0)
        {
            session.WatchedDurationSeconds = request.Position;
        }
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

    private async Task MarkMembersWatchedAsync(
        IReadOnlyList<Guid> memberUserIds,
        Guid mediaId,
        DateTime timeNow,
        CancellationToken cancellationToken)
    {
        if (memberUserIds.Count == 0)
            return;

        var existingStates = await _context.UserMediaStates
            .Where(s => memberUserIds.Contains(s.UserId) && s.MediaId == mediaId)
            .ToDictionaryAsync(s => s.UserId, cancellationToken);

        foreach (var memberId in memberUserIds)
        {
            if (existingStates.TryGetValue(memberId, out var state))
            {
                if (!state.IsCompleted)
                    state.PlayCount++;

                state.IsCompleted = true;
                state.LastInteractedAt = timeNow;
                continue;
            }

            _context.UserMediaStates.Add(new UserMediaState
            {
                UserId = memberId,
                MediaId = mediaId,
                PlayCount = 1,
                IsCompleted = true,
                LastInteractedAt = timeNow
            });
        }
    }

    private async Task TryHydrateStreamDecisionAsync(
        Guid streamSessionId,
        CancellationToken cancellationToken)
    {
        await StreamDecisionHydrator.TryHydrateTrackerAsync(
            streamSessionId,
            _activeStreamTracker,
            _context,
            _ffmpegCapabilitiesService,
            _logger,
            cancellationToken);
    }

    private async Task SyncSessionDetailsFromStreamDecisionAsync(
        MediaPlaybackSession session,
        Guid streamSessionId,
        bool updateTracks,
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

        ApplyStreamDecisionToDetails(details, sd, updateTracks);
    }

    private static PlaybackSessionDetails CreateDetailsFromStreamDecision(StreamDecisionDto sd)
    {
        var details = new PlaybackSessionDetails();
        ApplyStreamDecisionToDetails(details, sd, updateTracks: true);
        return details;
    }

    private static void ApplyStreamDecisionToDetails(
        PlaybackSessionDetails details,
        StreamDecisionDto sd,
        bool updateTracks)
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

        if (!updateTracks)
            return;

        details.AudioTrackLanguage = sd.AudioTrackLanguage;
        details.AudioTrackTitle = sd.AudioTrackTitle;
        details.AudioChannelLayout = sd.AudioChannelLayout;
        details.SubtitleTrackLanguage = sd.SubtitleTrackLanguage;
        details.SubtitleTrackTitle = sd.SubtitleTrackTitle;
    }

    private async Task ApplyReportedTrackSelectionAsync(
        UpdatePlaybackProgressCommand request,
        CancellationToken cancellationToken)
    {
        var existing = _activeStreamTracker.GetStreamInfo(request.SessionId)?.StreamDecision;
        if (existing is null)
            return;

        var streamSession = await _context.StreamSessions
            .FirstOrDefaultAsync(s => s.Id == request.SessionId, cancellationToken);
        if (streamSession?.IndexedFileId is not { } indexedFileId)
            return;

        var indexedFile = await _context.IndexedFiles
            .Include(x => x.FileMetadata)
            .FirstOrDefaultAsync(x => x.Id == indexedFileId, cancellationToken);

        if (indexedFile?.FileMetadata is not VideoFileMetadata videoMeta)
            return;

        await _context.Entry(videoMeta).Collection(v => v.AudioTracks).LoadAsync(cancellationToken);
        await _context.Entry(videoMeta).Collection(v => v.SubtitleTracks).LoadAsync(cancellationToken);

        var audio = videoMeta.AudioTracks.FirstOrDefault(t => t.Index == request.AudioTrackIndex);
        var subtitle = request.SubtitleTrackIndex is int subIdx
            ? videoMeta.SubtitleTracks.FirstOrDefault(t => t.Index == subIdx)
            : null;

        var updated = StreamDecisionTrackSelection.Apply(
            existing,
            audio,
            subtitle,
            subtitleSpecified: true);
        _activeStreamTracker.UpdateStreamDecision(request.SessionId, updated);

        var settings = DeserializePlaybackSettings(streamSession.PlaybackSettingsJson);
        if (request.AudioTrackIndex is int audioIndex)
            settings.AudioTrackIndex = audioIndex;
        settings.SubtitleTrackIndex = request.SubtitleTrackIndex;
        streamSession.PlaybackSettingsJson = System.Text.Json.JsonSerializer.Serialize(settings);
    }

    private static PlaybackSettingsDto DeserializePlaybackSettings(string json)
    {
        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<PlaybackSettingsDto>(json) ?? new PlaybackSettingsDto();
        }
        catch (System.Text.Json.JsonException)
        {
            return new PlaybackSettingsDto();
        }
    }

    private static bool IsVideoTranscoded(StreamDecisionDto sd) =>
        sd.Mode == PlaybackMode.Transcode
        || sd.IsSubtitleBurnIn
        || sd.Reason.HasFlag(TranscodeReason.SubtitlesBurnIn)
        || sd.Reason.HasFlag(TranscodeReason.ResolutionNotSupported)
        || sd.Reason.HasFlag(TranscodeReason.QualityDownscale)
        || (sd.SourceResolution is not null
            && sd.StreamResolution is not null
            && !string.Equals(sd.SourceResolution, sd.StreamResolution, StringComparison.OrdinalIgnoreCase))
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
