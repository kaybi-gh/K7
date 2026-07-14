using K7.Server.Application.Common.Security;
using K7.Server.Application.Services;
using K7.Server.Domain.Constants;
using K7.Shared.Dtos;

namespace K7.Server.Application.Features.Admin.Queries.GetActiveStreams;

[Authorize(Roles = Roles.Administrator)]
public record GetActiveStreamsQuery : IRequest<IReadOnlyList<ActiveStreamDto>>;

public class GetActiveStreamsQueryHandler(IActiveStreamsSnapshotService snapshotService)
    : IRequestHandler<GetActiveStreamsQuery, IReadOnlyList<ActiveStreamDto>>
{
    public Task<IReadOnlyList<ActiveStreamDto>> Handle(GetActiveStreamsQuery request, CancellationToken cancellationToken) =>
        snapshotService.BuildAsync(cancellationToken);
}
