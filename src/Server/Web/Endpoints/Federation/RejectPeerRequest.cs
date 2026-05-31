using K7.Server.Application.Features.Federation.Commands.RejectPeerRequest;
using K7.Server.Domain.Constants;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Federation;

public class RejectPeerRequestEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapPost("/api/federation/peers/requests/{id:guid}/reject", async (
            Guid id,
            [FromServices] ISender sender,
            CancellationToken cancellationToken) =>
        {
            await sender.Send(new RejectPeerRequestCommand(id), cancellationToken);
            return Results.Ok();
        })
        .RequireAuthorization(Policies.AdminOnly)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
