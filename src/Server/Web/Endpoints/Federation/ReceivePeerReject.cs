using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Web.Endpoints.Federation;

public class ReceivePeerReject : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapPost("/api/federation/peer-reject", async (
            [FromBody] PeerRejectRequest body,
            [FromServices] IApplicationDbContext context,
            CancellationToken cancellationToken) =>
        {
            var normalizedUrl = body.ProviderUrl.TrimEnd('/');

            var peer = await context.PeerServers
                .FirstOrDefaultAsync(p => p.BaseUrl == normalizedUrl && p.Status == PeerStatus.Pending, cancellationToken);

            if (peer is null)
                return Results.NotFound();

            peer.Status = PeerStatus.Rejected;
            await context.SaveChangesAsync(cancellationToken);

            return Results.Ok();
        })
        .WithName(type.Name)
        .WithTags(groupName);
    }
}

public sealed record PeerRejectRequest(string ProviderUrl);
