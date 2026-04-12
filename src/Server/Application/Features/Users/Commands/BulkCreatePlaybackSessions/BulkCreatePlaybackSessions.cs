using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities.Users;
using K7.Shared.Dtos.Requests;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.Features.Users.Commands.BulkCreatePlaybackSessions;

[Authorize(Roles = Roles.Administrator)]
public record BulkCreatePlaybackSessionsCommand : IRequest<int>
{
    public required Guid UserId { get; init; }
    public required IReadOnlyList<BulkCreatePlaybackSessionsRequest.PlaybackSessionItem> Items { get; init; }
}

public class BulkCreatePlaybackSessionsCommandHandler(IApplicationDbContext context)
    : IRequestHandler<BulkCreatePlaybackSessionsCommand, int>
{
    private const int SaveBatchSize = 500;

    public async Task<int> Handle(BulkCreatePlaybackSessionsCommand request, CancellationToken cancellationToken)
    {
        var user = await context.Users.FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken);
        Guard.Against.NotFound(request.UserId, user);

        var created = 0;

        foreach (var batch in request.Items.Chunk(SaveBatchSize))
        {
            foreach (var item in batch)
            {
                var sessionId = Guid.CreateVersion7();
                var session = new MediaPlaybackSession
                {
                    UserId = request.UserId,
                    MediaId = item.MediaId,
                    SessionId = sessionId,
                    ReferenceId = item.ReferenceId ?? sessionId,
                    StartedAt = item.StartedAt,
                    CompletedAt = item.IsCompleted ? item.StartedAt.AddSeconds(item.DurationSeconds) : null,
                    LastUpdateAt = item.StartedAt.AddSeconds(item.DurationSeconds),
                    PositionSeconds = item.DurationSeconds,
                    DurationSeconds = item.DurationSeconds,
                    WatchedDurationSeconds = item.WatchedDurationSeconds ?? item.DurationSeconds,
                    DeviceId = item.DeviceId,
                    State = item.IsCompleted ? Domain.Enums.PlaybackState.Ended : Domain.Enums.PlaybackState.Unknown
                };
                context.MediaPlaybackSessions.Add(session);

                if (item.IsTranscode is not null || item.VideoDecision is not null || item.Bitrate is not null)
                {
                    var details = new PlaybackSessionDetails
                    {
                        MediaPlaybackSessionId = session.Id,
                        IsTranscode = item.IsTranscode,
                        VideoDecision = item.VideoDecision,
                        AudioDecision = item.AudioDecision,
                        Bitrate = item.Bitrate,
                        SourceVideoCodec = item.SourceVideoCodec,
                        SourceAudioCodec = item.SourceAudioCodec,
                        SourceVideoWidth = item.SourceVideoWidth,
                        SourceVideoHeight = item.SourceVideoHeight,
                        StreamVideoCodec = item.StreamVideoCodec,
                        StreamAudioCodec = item.StreamAudioCodec
                    };
                    context.PlaybackSessionDetails.Add(details);
                }
            }

            await context.SaveChangesAsync(cancellationToken);
            created += batch.Length;
        }

        return created;
    }
}
