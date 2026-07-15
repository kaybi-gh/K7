using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Users;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Requests;

namespace K7.Server.Application.Features.Federation.Commands.ReportFederationSession;

public record ReportFederationSessionCommand(string? ClientId, FederationSessionRequest Request) : IRequest;

public class ReportFederationSessionCommandHandler(
    IPeerAuthorizationService peerAuthorization,
    IApplicationDbContext context)
    : IRequestHandler<ReportFederationSessionCommand>
{
    public async Task Handle(ReportFederationSessionCommand command, CancellationToken cancellationToken)
    {
        var peer = await peerAuthorization.RequireInboundPeerAsync(command.ClientId, cancellationToken);
        var request = command.Request;

        var virtualUser = await context.Users
            .FirstOrDefaultAsync(u => u.PeerServerId == peer.Id && u.DisplayName == request.UserDisplayName, cancellationToken);

        if (virtualUser is null)
        {
            virtualUser = new User
            {
                Id = Guid.NewGuid(),
                PeerServerId = peer.Id,
                DisplayName = request.UserDisplayName,
                IsActive = true
            };
            context.Users.Add(virtualUser);
        }

        var sharedLibraryIds = await peerAuthorization.GetOutboundSharedLibraryIdsAsync(peer.Id, cancellationToken);

        var fileExists = await context.IndexedFiles
            .AnyAsync(f => f.Id == request.FileId && sharedLibraryIds.Contains(f.LibraryId), cancellationToken);

        if (!fileExists)
            throw new NotFoundException(request.FileId.ToString(), nameof(IndexedFile));

        if (request.State == PlaybackState.Ended)
        {
            var existing = await context.StreamSessions
                .FirstOrDefaultAsync(s => s.IndexedFileId == request.FileId
                    && s.UserId == virtualUser.Id
                    && s.PeerServerId == peer.Id
                    && s.EndedAt == null, cancellationToken);

            if (existing is not null)
            {
                existing.State = PlaybackState.Ended;
                existing.Position = request.Position;
                existing.EndedAt = DateTimeOffset.UtcNow;
            }
        }
        else
        {
            var session = await context.StreamSessions
                .FirstOrDefaultAsync(s => s.IndexedFileId == request.FileId
                    && s.UserId == virtualUser.Id
                    && s.PeerServerId == peer.Id
                    && s.EndedAt == null, cancellationToken);

            if (session is null)
            {
                session = new StreamSession
                {
                    Id = Guid.NewGuid(),
                    IndexedFileId = request.FileId,
                    DeviceId = Guid.Empty,
                    UserId = virtualUser.Id,
                    PeerServerId = peer.Id,
                    State = request.State,
                    Position = request.Position
                };
                context.StreamSessions.Add(session);
            }
            else
            {
                session.State = request.State;
                session.Position = request.Position;
            }
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
