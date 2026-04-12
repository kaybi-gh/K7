using K7.Server.Application.Features.Medias.Commands.BulkLinkArtists;
using K7.Server.Domain.Constants;
using K7.Shared.Dtos.Requests;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Medias;

public class BulkLinkArtists : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapPost("/api/medias/bulk-link-artists", async (
            [FromBody] BulkLinkArtistsRequest request,
            [FromServices] ISender sender,
            CancellationToken cancellationToken) =>
        {
            var count = await sender.Send(new BulkLinkArtistsCommand
            {
                Items = request.Items
            }, cancellationToken);
            return Results.Ok(new { LinkedCount = count });
        })
        .RequireAuthorization(Policies.AdminOnly)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
