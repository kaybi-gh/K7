using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace K7.Server.Application.Features.Federation.Commands.RejectPeerRequest;

[Authorize(Roles = Roles.Administrator)]
public record RejectPeerRequestCommand(Guid RequestId) : IRequest;

public class RejectPeerRequestCommandHandler(
    IApplicationDbContext context,
    IPeerClient peerClient,
    IConfiguration configuration,
    ILogger<RejectPeerRequestCommandHandler> logger)
    : IRequestHandler<RejectPeerRequestCommand>
{
    public async Task Handle(RejectPeerRequestCommand request, CancellationToken cancellationToken)
    {
        var peerRequest = await context.PeerRequests
            .FindAsync([request.RequestId], cancellationToken);

        Guard.Against.NotFound(request.RequestId, peerRequest);

        if (peerRequest.Status != PeerRequestStatus.Pending)
            throw new InvalidOperationException("This peer request has already been processed.");

        peerRequest.Status = PeerRequestStatus.Rejected;
        peerRequest.RespondedAt = DateTimeOffset.UtcNow;

        await context.SaveChangesAsync(cancellationToken);

        // Best-effort notification to the requester
        var providerUrl = configuration.GetValue<string>("BaseUrl") ?? "";
        if (!string.IsNullOrEmpty(providerUrl))
        {
            try
            {
                await peerClient.SendPeerRejectAsync(peerRequest.RequesterUrl, providerUrl, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to notify requester {RequesterUrl} of rejection (best-effort)", peerRequest.RequesterUrl);
            }
        }
    }
}
