using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Web.Endpoints.Federation;

public class GetFederationMedia : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapGet("/api/federation/libraries/{libraryId:guid}/media", async (
            Guid libraryId,
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

            var isShared = await context.PeerShareAgreements
                .AnyAsync(a => a.PeerServerId == peer.Id
                    && a.LibraryId == libraryId
                    && a.Direction == ShareDirection.Outbound
                    && a.IsEnabled, cancellationToken);

            if (!isShared)
                return Results.Forbid();

            var library = await context.Libraries
                .FirstOrDefaultAsync(l => l.Id == libraryId && l.PeerServerId == null, cancellationToken);

            if (library is null)
                return Results.NotFound();

            var mediaItems = await context.Medias
                .Where(m => m.PeerServerId == null && m.IndexedFiles.Any(f => f.LibraryId == libraryId))
                .Include(m => m.ExternalIds)
                .Include(m => m.IndexedFiles.Where(f => f.LibraryId == libraryId))
                .ToListAsync(cancellationToken);

            var result = mediaItems.Select(m => new PeerMediaDto
            {
                Id = m.Id,
                Type = m.Type,
                Title = m.Title,
                OriginalTitle = m.OriginalTitle,
                ReleaseDate = m.ReleaseDate,
                ExternalIds = m.ExternalIds.Select(e => new PeerExternalIdDto
                {
                    Provider = e.ProviderName,
                    Value = e.Value
                }).ToList(),
                Files = m.IndexedFiles.Select(f => new PeerFileDto
                {
                    Id = f.Id,
                    Name = f.Name,
                    Extension = f.Extension,
                    Size = f.Size
                }).ToList(),
                Genres = m.Genres.ToList()
            }).ToList();

            return Results.Ok(result);
        })
        .RequireAuthorization(Policies.PeerAccess)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
