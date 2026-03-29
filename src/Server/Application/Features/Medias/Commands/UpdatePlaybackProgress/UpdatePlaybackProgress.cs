using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Application.Services;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Users;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Events;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace K7.Server.Application.Features.Medias.Commands.UpdatePlaybackProgress;

[Authorize(Roles = $"{Roles.User},{Roles.Administrator}")]
public record UpdatePlaybackProgressCommand(Guid MediaId, Guid SessionId, double Position, double Duration) : IRequest;

public class UpdatePlaybackProgressCommandHandler(IApplicationDbContext context, IUser currentUserService, IPlaybackProgressNotifier progressNotifier, IMediaAccessGuard accessGuard, ILogger<UpdatePlaybackProgressCommandHandler> logger) : IRequestHandler<UpdatePlaybackProgressCommand>
{
    private readonly IApplicationDbContext _context = context;
    private readonly IUser _currentUser = currentUserService;
    private readonly IPlaybackProgressNotifier _progressNotifier = progressNotifier;
    private readonly IMediaAccessGuard _accessGuard = accessGuard;
    private readonly ILogger _logger = logger;

    public async Task Handle(UpdatePlaybackProgressCommand request, CancellationToken cancellationToken)
    {
        if (_currentUser.Id is not { } userId)
            return;

        await _accessGuard.EnsureAccessAsync(request.MediaId, cancellationToken);

        var media = await _context.Medias
            .FirstOrDefaultAsync(m => m.Id == request.MediaId, cancellationToken);

        if (media == null) return;

        var timeNow = DateTime.UtcNow;

        var session = await _context.MediaPlaybackSessions
            .FirstOrDefaultAsync(s => s.SessionId == request.SessionId, cancellationToken);

        if (session == null)
        {
            session = new MediaPlaybackSession
            {
                UserId = userId,
                MediaId = request.MediaId,
                SessionId = request.SessionId,
                StartedAt = timeNow,
                LastUpdateAt = timeNow
            };
            _context.MediaPlaybackSessions.Add(session);
        }
        else
        {
            session.LastUpdateAt = timeNow;
        }

        session.PositionSeconds = request.Position;
        session.DurationSeconds = request.Duration;

        var state = await _context.UserMediaStates
            .FirstOrDefaultAsync(s => s.UserId == userId && s.MediaId == request.MediaId, cancellationToken);

        if (state == null)
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

        bool isMusic = media.Type == MediaType.MusicTrack;
        // Music: 50% or 4 minutes (industry standard scrobble threshold)
        bool completed = isMusic
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
                await EnqueueNextEpisodeAsync(userId, episode, timeNow, cancellationToken);
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

        await _context.SaveChangesAsync(cancellationToken);

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

    private async Task EnqueueNextEpisodeAsync(Guid userId, SerieEpisode episode, DateTime timeNow, CancellationToken cancellationToken)
    {
        var nextEpisode = await _context.Medias
            .OfType<SerieEpisode>()
            .Where(e => e.SeasonId == episode.SeasonId && e.EpisodeNumber > episode.EpisodeNumber)
            .OrderBy(e => e.EpisodeNumber)
            .Select(e => e.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (nextEpisode == default)
        {
            var currentSeasonNumber = await _context.Medias
                .OfType<SerieEpisode>()
                .Where(e => e.Id == episode.Id)
                .Select(e => e.Season.SeasonNumber)
                .FirstOrDefaultAsync(cancellationToken);

            nextEpisode = await _context.Medias
                .OfType<SerieEpisode>()
                .Where(e => e.SerieId == episode.SerieId && e.Season.SeasonNumber > currentSeasonNumber)
                .OrderBy(e => e.Season.SeasonNumber)
                .ThenBy(e => e.EpisodeNumber)
                .Select(e => e.Id)
                .FirstOrDefaultAsync(cancellationToken);
        }

        if (nextEpisode == default) return;

        var nextState = await _context.UserMediaStates
            .FirstOrDefaultAsync(s => s.UserId == userId && s.MediaId == nextEpisode, cancellationToken);

        if (nextState is null)
        {
            nextState = new UserMediaState
            {
                UserId = userId,
                MediaId = nextEpisode,
                PlayCount = 0,
                IsCompleted = false,
                LastPlaybackPosition = 0
            };
            _context.UserMediaStates.Add(nextState);
        }
        else if (nextState.IsCompleted)
        {
            return;
        }

        nextState.LastInteractedAt = timeNow;
    }
}
