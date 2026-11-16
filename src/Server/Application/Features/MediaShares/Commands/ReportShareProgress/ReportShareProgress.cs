using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.MediaShares.Services;
using K7.Server.Application.Services;

namespace K7.Server.Application.Features.MediaShares.Commands.ReportShareProgress;

/// <summary>
/// Playback position reported by a share visitor. Nothing is stored per visitor: it only
/// closes a share set to close once its media was played to the end.
/// The position comes from the visitor browser, so this is a convenience and not a
/// guarantee: expiration and access limits remain the real barriers.
/// </summary>
public record ReportShareProgressCommand : IRequest
{
    public required Guid ShareId { get; init; }
    public required Guid MediaId { get; init; }
    public double PositionSeconds { get; init; }
    public double DurationSeconds { get; init; }
}

public class ReportShareProgressCommandHandler(
    IApplicationDbContext context,
    IShareVisitorAccess shareVisitorAccess)
    : IRequestHandler<ReportShareProgressCommand>
{
    public async Task Handle(ReportShareProgressCommand request, CancellationToken cancellationToken)
    {
        shareVisitorAccess.RequireGrant(request.ShareId);

        if (request.DurationSeconds <= 0
            || request.PositionSeconds / request.DurationSeconds < MediaShareRules.ConsumedProgressRatio)
        {
            return;
        }

        await ShareConsumption.MarkConsumedAsync(context, request.ShareId, request.MediaId, cancellationToken);
    }
}
