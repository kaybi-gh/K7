using K7.Server.Application.Common.Security;
using K7.Server.Application.Services;
using K7.Server.Domain.Constants;
using K7.Shared.Dtos;

namespace K7.Server.Application.Features.Admin.Queries.GetServerMetrics;

[Authorize(Roles = Roles.Administrator)]
public record GetServerMetricsQuery : IRequest<ServerMetricsHistoryDto>;

public class GetServerMetricsQueryHandler(IServerMetricsCollector metricsCollector)
    : IRequestHandler<GetServerMetricsQuery, ServerMetricsHistoryDto>
{
    public Task<ServerMetricsHistoryDto> Handle(GetServerMetricsQuery request, CancellationToken cancellationToken)
    {
        metricsCollector.RecordSample(0);

        var history = metricsCollector.GetHistory();
        if (history.Snapshots.Count > 0)
            return Task.FromResult(history);

        var current = metricsCollector.GetCurrentSnapshot(0);
        return Task.FromResult(new ServerMetricsHistoryDto { Snapshots = [current] });
    }
}
