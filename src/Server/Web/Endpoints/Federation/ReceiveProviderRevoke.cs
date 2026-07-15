using K7.Server.Application.Features.Federation.Commands.ReceiveProviderRevocation;
using K7.Shared.Dtos.Requests;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Federation;

public class ReceiveProviderRevoke : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapPost("/api/federation/provider-revoke-notify", async (
            [FromBody] ProviderRevokeRequest body,
            [FromServices] ISender sender,
            CancellationToken cancellationToken) =>
        {
            await sender.Send(new ReceiveProviderRevocationCommand(body), cancellationToken);
            return Results.Ok();
        })
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
