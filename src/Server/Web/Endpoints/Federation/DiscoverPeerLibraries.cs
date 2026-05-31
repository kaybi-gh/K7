using K7.Server.Application.Features.Federation.Commands.DiscoverPeerLibraries;
using K7.Server.Domain.Constants;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Federation;

public class DiscoverPeerLibraries : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapPost("/api/federation/peers/{id:guid}/discover-libraries", async (
            Guid id,
            [FromServices] ISender sender,
            CancellationToken cancellationToken) =>
        {
            var agreements = await sender.Send(new DiscoverPeerLibrariesCommand(id), cancellationToken);
            return Results.Ok(agreements);
        })
        .RequireAuthorization(Policies.AdminOnly)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
