using K7.Server.Application.Common.Security;
using K7.Server.Application.Services;
using K7.Server.Domain.Constants;
using K7.Shared.Dtos;

namespace K7.Server.Application.Features.Admin.Queries.GetActiveStreams;

[Authorize(Roles = Roles.Administrator)]
public record GetActiveStreamsQuery : IRequest<IReadOnlyList<ActiveStreamDto>>;

public class GetActiveStreamsQueryHandler(IActiveStreamTracker tracker)
    : IRequestHandler<GetActiveStreamsQuery, IReadOnlyList<ActiveStreamDto>>
{
    public Task<IReadOnlyList<ActiveStreamDto>> Handle(GetActiveStreamsQuery request, CancellationToken cancellationToken)
    {
        var streams = tracker.GetActiveStreams()
            .Select(s => new ActiveStreamDto
            {
                ConnectionId = s.SessionId.ToString(),
                UserId = s.UserId,
                UserName = s.UserName,
                MediaId = s.MediaId,
                MediaTitle = s.MediaTitle,
                MediaType = s.MediaType,
                ParentId = s.ParentId,
                DeviceId = s.DeviceId,
                DeviceName = s.DeviceName,
                StartedAt = s.StartedAt,
                Position = s.Position,
                Duration = s.Duration,
                State = s.State
            })
            .ToList();

        return Task.FromResult<IReadOnlyList<ActiveStreamDto>>(streams);
    }
}
