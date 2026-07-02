using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Application.Services;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos;

namespace K7.Server.Application.Features.Admin.Queries.GetActiveStreams;

[Authorize(Roles = Roles.Administrator)]
public record GetActiveStreamsQuery : IRequest<IReadOnlyList<ActiveStreamDto>>;

public class GetActiveStreamsQueryHandler(IActiveStreamTracker tracker, IApplicationDbContext context)
    : IRequestHandler<GetActiveStreamsQuery, IReadOnlyList<ActiveStreamDto>>
{
    public async Task<IReadOnlyList<ActiveStreamDto>> Handle(GetActiveStreamsQuery request, CancellationToken cancellationToken)
    {
        var activeStreams = tracker.GetActiveStreams();

        var userIds = activeStreams
            .Where(s => s.UserId.HasValue)
            .Select(s => s.UserId!.Value)
            .Distinct()
            .ToList();

        var avatarMap = await context.MetadataPictures
            .Where(p => p.UserId != null && userIds.Contains(p.UserId.Value) && p.Type == MetadataPictureType.UserAvatar)
            .Select(p => new { p.UserId, p.Id })
            .ToDictionaryAsync(p => p.UserId!.Value, p => p.Id, cancellationToken);

        var streams = activeStreams
            .Select(s => new ActiveStreamDto
            {
                ConnectionId = s.SessionId.ToString(),
                UserId = s.UserId,
                UserName = s.UserName,
                UserAvatarUrl = s.UserId.HasValue && avatarMap.TryGetValue(s.UserId.Value, out var picId)
                    ? $"/api/metadata-pictures/{picId}"
                    : null,
                MediaId = s.MediaId,
                MediaTitle = s.MediaTitle,
                MediaType = s.MediaType,
                ParentId = s.ParentId,
                DeviceId = s.DeviceId,
                DeviceName = s.DeviceName,
                DeviceType = s.DeviceType,
                ThumbnailUrl = s.ThumbnailUrl,
                StreamDecision = s.StreamDecision,
                StartedAt = s.StartedAt,
                Position = s.Position,
                Duration = s.Duration,
                State = s.State,
                SharedProfileName = s.SharedProfileName
            })
            .ToList();

        return streams;
    }
}
