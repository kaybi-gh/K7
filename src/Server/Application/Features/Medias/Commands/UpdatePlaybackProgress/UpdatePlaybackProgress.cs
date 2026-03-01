using K7.Server.Application.Common.Exceptions;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Users;
using K7.Server.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace K7.Server.Application.Features.Medias.Commands.UpdatePlaybackProgress;

public record UpdatePlaybackProgressCommand(Guid MediaId, Guid SessionId, double Position, double Duration) : IRequest;

public class UpdatePlaybackProgressCommandHandler(IApplicationDbContext context, IUser currentUserService, IPlaybackProgressNotifier progressNotifier, ILogger<UpdatePlaybackProgressCommandHandler> logger) : IRequestHandler<UpdatePlaybackProgressCommand>
{
    private readonly IApplicationDbContext _context = context;
    private readonly IUser _currentUser = currentUserService;
    private readonly IPlaybackProgressNotifier _progressNotifier = progressNotifier;
    private readonly ILogger _logger = logger;

    public async Task Handle(UpdatePlaybackProgressCommand request, CancellationToken cancellationToken)
    {
        // Silently skip persistence on guest users
        if (_currentUser.Id is not { } userId)
            return;

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
        double completionThreshold = isMusic ? 0.95 : 0.80;

        if (progress >= completionThreshold)
        {
            if (!state.IsCompleted)
            {
                state.PlayCount++;
                state.IsCompleted = true;
            }
            state.LastPlaybackPosition = 0; 
            state.ProgressPercentage = 100;
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
}
