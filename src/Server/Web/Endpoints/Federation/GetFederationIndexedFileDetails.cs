using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Mappings;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Web.Endpoints.Federation;

public class GetFederationIndexedFileDetails : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapGet("/api/federation/indexed-files/{fileId:guid}", async (
            Guid fileId,
            [FromServices] IApplicationDbContext context,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var clientId = httpContext.User.FindFirst("sub")?.Value;
            if (clientId is null)
                return Results.Forbid();

            var peer = await context.PeerServers
                .FirstOrDefaultAsync(p => p.InboundApplicationId == clientId && p.Status == PeerStatus.Active, cancellationToken);

            if (peer is null)
                return Results.Forbid();

            var indexedFile = await context.IndexedFiles
                .Include(f => f.FileMetadata)
                .FirstOrDefaultAsync(f => f.Id == fileId, cancellationToken);

            if (indexedFile is null)
                return Results.NotFound();

            // Eagerly load track collections for video/audio metadata
            if (indexedFile.FileMetadata is not null)
            {
                var entry = context.Entry(indexedFile.FileMetadata);
                if (indexedFile.FileMetadata is Domain.Entities.Metadatas.Files.VideoFileMetadata)
                {
                    await entry.Collection("AudioTracks").LoadAsync(cancellationToken);
                    await entry.Collection("VideoTracks").LoadAsync(cancellationToken);
                    await entry.Collection("SubtitleTracks").LoadAsync(cancellationToken);
                }
                else if (indexedFile.FileMetadata is Domain.Entities.Metadatas.Files.AudioFileMetadata)
                {
                    await entry.Reference("AudioTrack").LoadAsync(cancellationToken);
                }
            }

            // Verify this file belongs to a library shared with the peer
            var isShared = await context.PeerShareAgreements
                .AnyAsync(a => a.PeerServerId == peer.Id && a.LibraryId == indexedFile.LibraryId, cancellationToken);

            if (!isShared)
                return Results.Forbid();

            return Results.Ok(indexedFile.ToIndexedFileDto());
        })
        .RequireAuthorization(Policies.PeerAccess)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
