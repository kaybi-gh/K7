using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Application.Services;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Users;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Events;
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
    Guid? DeviceId = null) : IRequest;

public class UpdatePlaybackProgressCommandHandler(IApplicationDbContext context, IUser currentUserService, IPlaybackProgressNotifier progressNotifier, IMediaAccessGuard accessGuard, IActiveStreamTracker activeStreamTracker, IIdentityService identityService, IMediaQueryCacheInvalidator cacheInvalidator, INextEpisodeEnqueueService nextEpisodeEnqueueService, ILogger<UpdatePlaybackProgressCommandHandler> logger) : IRequestHandler<UpdatePlaybackProgressCommand>
{
    private readonly IApplicationDbContext _context = context;
    private readonly IUser _currentUser = currentUserService;
    private readonly IPlaybackProgressNotifier _progressNotifier = progressNotifier;
    private readonly IMediaAccessGuard _accessGuard = accessGuard;
    private readonly IActiveStreamTracker _activeStreamTracker = activeStreamTracker;
    private readonly IIdentityService _identityService = identityService;
    private readonly IMediaQueryCacheInvalidator _cacheInvalidator = cacheInvalidator;
    private readonly INextEpisodeEnqueueService _nextEpisodeEnqueueService = nextEpisodeEnqueueService;
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

        var session = await _context.MediaPlaybackSessions
            .FirstOrDefaultAsync(s => s.SessionId == request.SessionId, cancellationToken);

        var previousState = session?.State ?? PlaybackState.Unknown;

        if (session is null)
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

            // Persist stream decision as PlaybackSessionDetails at session creation
            var streamInfo = _activeStreamTracker.GetStreamInfo(request.SessionId);
            if (streamInfo?.StreamDecision is { } sd)
            {
                var videoIsTranscoded = sd.SourceVideoCodec is not null
                    && sd.StreamVideoCodec is not null
                    && !string.Equals(sd.SourceVideoCodec, sd.StreamVideoCodec, StringComparison.OrdinalIgnoreCase);
                var audioIsTranscoded = sd.SourceAudioCodec is not null
                    && sd.StreamAudioCodec is not null
                    && !string.Equals(sd.SourceAudioCodec, sd.StreamAudioCodec, StringComparison.OrdinalIgnoreCase);
                var isTransmux = sd.Mode == PlaybackMode.Transmux;

                var details = new PlaybackSessionDetails
                {
                    MediaPlaybackSessionId = session.Id,
                    IsTranscode = videoIsTranscoded || audioIsTranscoded,
                    VideoDecision = videoIsTranscoded ? "Transcode" : isTransmux ? "Transmux" : "Direct",
                    AudioDecision = audioIsTranscoded ? "Transcode" : isTransmux ? "Transmux" : "Direct",
                    TranscodeReason = sd.Reason != TranscodeReason.None ? sd.Reason : null,
                    Bitrate = sd.Bitrate,
                    SourceVideoCodec = sd.SourceVideoCodec,
                    SourceAudioCodec = sd.SourceAudioCodec,
                    SourceVideoWidth = ParseResolutionWidth(sd.SourceResolution),
                    SourceVideoHeight = ParseResolutionHeight(sd.SourceResolution),
                    StreamVideoCodec = sd.StreamVideoCodec,
                    StreamAudioCodec = sd.StreamAudioCodec,
                    AudioTrackLanguage = sd.AudioTrackLanguage,
                    AudioTrackTitle = sd.AudioTrackTitle,
                    AudioChannelLayout = sd.AudioChannelLayout,
                    SubtitleTrackLanguage = sd.SubtitleTrackLanguage,
                    SubtitleTrackTitle = sd.SubtitleTrackTitle
                };
                _context.PlaybackSessionDetails.Add(details);
            }
        }
        else
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

        session.PositionSeconds = request.Position;
        session.DurationSeconds = request.Duration;

        var state = await _context.UserMediaStates
            .FirstOrDefaultAsync(s => s.UserId == userId && s.MediaId == request.MediaId, cancellationToken);

        if (state is null)
        {
            state = new UserMediaState
            {
                UserId = userId,
                MediaId = request.MediaId,
                PlayCount = 0,
                IsCompleted = false,
                LastPlaybackPosition = 0
            };
            _context.UserMediaStates.Add(state);
        }

        state.LastInteractedAt = timeNow;

        double progress = request.Duration > 0 ? request.Position / request.Duration : 0;

        var isMusic = media.Type == MediaType.MusicTrack;
        var completed = isMusic
            ? progress >= 0.50 || request.Position >= 240
            : progress >= 0.80;

        if (completed)
        {
            if (session.CompletedAt is null)
            {
                session.CompletedAt = timeNow;
                session.AddDomainEvent(MediaPlaybackCompletedEvent<BaseMedia>.Create(session, media));
            }

            if (!state.IsCompleted)
            {
                state.PlayCount++;
                state.IsCompleted = true;
            }
            state.LastPlaybackPosition = 0;
            state.ProgressPercentage = 100;

            if (media is SerieEpisode episode)
            {
                await _nextEpisodeEnqueueService.EnqueueNextEpisodeAsync(userId, episode.Id, timeNow, cancellationToken);
            }
        }
        else
        {
            if (state.IsCompleted && request.Position < (request.Duration * 0.1))
            {
                state.IsCompleted = false;
            }

            if (!isMusic)
            {
                state.LastPlaybackPosition = request.Position;
                state.ProgressPercentage = Math.Clamp(progress * 100, 0, 100);
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

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException) when (session.Id == default)
        {
            _context.Entry(session).State = EntityState.Detached;

            session = await _context.MediaPlaybackSessions
                .FirstOrDefaultAsync(s => s.SessionId == request.SessionId, cancellationToken);

            if (session is null) return;

            session.PositionSeconds = request.Position;
            session.DurationSeconds = request.Duration;
            session.LastUpdateAt = timeNow;
            session.State = request.State;

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
                State = (int)request.State
            });
        }
        else
        {
            _activeStreamTracker.Remove(request.SessionId);
        }

        _cacheInvalidator.InvalidateAll();

        var identityUserId = _currentUser.IdentityId;
        _logger.LogDebug("Playback progress updated: identityUserId='{IdentityUserId}', mediaId={MediaId}, progress={Progress:F1}%", identityUserId, request.MediaId, state.ProgressPercentage);
        if (!string.IsNullOrEmpty(identityUserId))
        {
            await _progressNotifier.NotifyProgressUpdatedAsync(
                identityUserId,
                request.MediaId,
                state.ProgressPercentage,
                state.IsCompleted,
                cancellationToken);
        }
        else
        {
            _logger.LogWarning("Skipped SignalR notification: identityUserId is null/empty");
        }
    }

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
