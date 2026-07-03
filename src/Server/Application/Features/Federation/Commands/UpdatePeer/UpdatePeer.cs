using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities.Federation;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities;
using Microsoft.Extensions.Logging;

namespace K7.Server.Application.Features.Federation.Commands.UpdatePeer;

[Authorize(Roles = Roles.Administrator)]
public record UpdatePeerCommand : IRequest
{
    public required Guid PeerId { get; init; }
    public string? BaseUrl { get; init; }
    public IReadOnlyList<Guid>? SharedLibraryIds { get; init; }
    public IReadOnlyList<Guid>? EnabledInboundAgreementIds { get; init; }
    public int? MaxConcurrentStreams { get; init; }
    public bool? AutoAddNewLibraries { get; init; }
    public IReadOnlyList<PeerSocialAgreementDto>? SocialAgreements { get; init; }
    public IReadOnlyList<Guid>? SharePlaybackHistoryLibraryIds { get; init; }
}

public class UpdatePeerCommandHandler(
    IApplicationDbContext context,
    IPeerClient peerClient,
    ILogger<UpdatePeerCommandHandler> logger)
    : IRequestHandler<UpdatePeerCommand>
{
    public async Task Handle(UpdatePeerCommand request, CancellationToken cancellationToken)
    {
        var peer = await context.PeerServers
            .FirstOrDefaultAsync(p => p.Id == request.PeerId, cancellationToken);

        Guard.Against.NotFound(request.PeerId, peer);

        if (!string.IsNullOrWhiteSpace(request.BaseUrl))
        {
            peer.BaseUrl = request.BaseUrl.TrimEnd('/');
        }

        if (request.AutoAddNewLibraries.HasValue)
        {
            peer.AutoAddNewLibraries = request.AutoAddNewLibraries.Value;
        }

        var sharedLibrariesChanged = false;

        if (request.SharedLibraryIds is not null)
        {
            await context.PeerShareAgreements
                .Where(a => a.PeerServerId == peer.Id && a.Direction == ShareDirection.Outbound)
                .ExecuteDeleteAsync(cancellationToken);

            foreach (var libraryId in request.SharedLibraryIds)
            {
                context.PeerShareAgreements.Add(new PeerShareAgreement
                {
                    Id = Guid.NewGuid(),
                    PeerServerId = peer.Id,
                    LibraryId = libraryId,
                    Direction = ShareDirection.Outbound,
                    MaxConcurrentStreams = request.MaxConcurrentStreams,
                    IsEnabled = true,
                    SharePlaybackHistory = request.SharePlaybackHistoryLibraryIds?.Contains(libraryId) ?? false
                });
            }

            sharedLibrariesChanged = true;
        }
        else if (request.SharePlaybackHistoryLibraryIds is not null)
        {
            var outboundAgreements = await context.PeerShareAgreements
                .Where(a => a.PeerServerId == peer.Id && a.Direction == ShareDirection.Outbound)
                .ToListAsync(cancellationToken);

            foreach (var agreement in outboundAgreements)
            {
                agreement.SharePlaybackHistory = request.SharePlaybackHistoryLibraryIds.Contains(agreement.LibraryId);
            }
        }

        if (request.EnabledInboundAgreementIds is not null)
        {
            var inboundAgreements = await context.PeerShareAgreements
                .Where(a => a.PeerServerId == peer.Id && a.Direction == ShareDirection.Inbound)
                .ToListAsync(cancellationToken);

            foreach (var agreement in inboundAgreements)
            {
                agreement.IsEnabled = request.EnabledInboundAgreementIds.Contains(agreement.Id);
            }
        }

        if (request.SocialAgreements is not null)
        {
            var existing = await context.PeerSocialAgreements
                .Where(a => a.PeerServerId == peer.Id)
                .ToListAsync(cancellationToken);

            foreach (var social in request.SocialAgreements)
            {
                var agreement = existing.FirstOrDefault(a => a.ContentType == social.ContentType);
                if (agreement is null)
                {
                    context.PeerSocialAgreements.Add(new PeerSocialAgreement
                    {
                        Id = Guid.NewGuid(),
                        PeerServerId = peer.Id,
                        ContentType = social.ContentType,
                        AllowOutbound = social.AllowOutbound,
                        AllowInbound = social.AllowInbound
                    });
                }
                else
                {
                    agreement.AllowOutbound = social.AllowOutbound;
                    agreement.AllowInbound = social.AllowInbound;
                }
            }
        }

        await context.SaveChangesAsync(cancellationToken);

        // Best-effort notification to the consumer peer about share changes
        if (sharedLibrariesChanged && peer.OutboundClientId is not null && peer.OutboundClientSecret is not null)
        {
            try
            {
                var token = await peerClient.GetAccessTokenAsync(peer.BaseUrl, peer.OutboundClientId, peer.OutboundClientSecret, cancellationToken);
                if (token is not null)
                {
                    await peerClient.NotifyShareUpdateAsync(peer.BaseUrl, token, request.SharedLibraryIds!, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to notify peer {PeerName} of share update (best-effort)", peer.Name);
            }
        }
    }
}
