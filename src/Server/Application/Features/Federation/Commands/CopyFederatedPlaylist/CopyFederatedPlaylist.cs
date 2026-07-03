using K7.Server.Application.Common.Exceptions;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Application.Features.Federation.Services;
using K7.Server.Application.Features.Playlists.Commands.AddPlaylistItem;
using K7.Server.Application.Features.Playlists.Commands.CreatePlaylist;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities.Federation;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Federation.Social;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.Features.Federation.Commands.CopyFederatedPlaylist;

[Authorize(Roles = $"{Roles.User},{Roles.Administrator}")]
public record CopyFederatedPlaylistCommand(Guid PeerServerId, Guid OriginUserId, Guid PlaylistId) : IRequest<Guid>;

public class CopyFederatedPlaylistCommandHandler(
    IApplicationDbContext context,
    IPeerClient peerClient,
    IFederationViewerAssertionService assertionService,
    IUser currentUser,
    IFederatedMediaResolver mediaResolver,
  ISender sender)
    : IRequestHandler<CopyFederatedPlaylistCommand, Guid>
{
    public async Task<Guid> Handle(CopyFederatedPlaylistCommand command, CancellationToken cancellationToken)
    {
        if (currentUser.Id is not { } viewerUserId)
            throw new ForbiddenAccessException();

        var peer = await context.PeerServers
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == command.PeerServerId && p.Status == PeerStatus.Active, cancellationToken);

        if (peer is null || string.IsNullOrWhiteSpace(peer.OutboundClientId) || string.IsNullOrWhiteSpace(peer.OutboundClientSecret))
            throw new NotFoundException(command.PeerServerId.ToString(), nameof(PeerServer));

        var viewer = await context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == viewerUserId, cancellationToken);
        var token = await peerClient.GetAccessTokenAsync(peer.BaseUrl, peer.OutboundClientId, peer.OutboundClientSecret, cancellationToken)
            ?? throw new ForbiddenAccessException();

        var assertionSecret = peer.FederationAssertionSecret ?? peer.OutboundClientSecret;
        var assertion = assertionService.CreateAssertion(new FederatedUserRef
        {
            OriginUserId = viewerUserId,
            DisplayName = viewer?.DisplayName
        }, assertionSecret);

        var playlists = await peerClient.GetRemoteSocialPlaylistsAsync(
            peer.BaseUrl, token, assertion, command.OriginUserId, cancellationToken);

        var source = playlists.FirstOrDefault(p => p.Id == command.PlaylistId)
            ?? throw new NotFoundException(command.PlaylistId.ToString(), "Playlist");

        var playlistId = await sender.Send(new CreatePlaylistCommand
        {
            Title = source.Title,
            Description = source.Description,
            MediaType = source.MediaType,
            VisibilityScope = VisibilityScope.Nobody
        }, cancellationToken);

        foreach (var item in source.Items.OrderBy(i => i.Order))
        {
            var resolution = await mediaResolver.ResolveAsync(command.PeerServerId, item.Media, cancellationToken);
            if (resolution.LocalMediaId is not Guid localMediaId)
                continue;

            await sender.Send(new AddPlaylistItemCommand
            {
                PlaylistId = playlistId,
                MediaId = localMediaId
            }, cancellationToken);
        }

        return playlistId;
    }
}
