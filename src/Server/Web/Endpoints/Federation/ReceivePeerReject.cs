using K7.Server.Application.Features.Federation.Commands.ReceivePeerReject;
using K7.Shared.Dtos.Requests;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Federation;

public class ReceivePeerReject : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapPost("/api/federation/peer-reject", async (
            [FromBody] PeerRejectRequest body,
            [FromServices] ISender sender,
            CancellationToken cancellationToken) =>
        {
            await sender.Send(new ReceivePeerRejectCommand(body), cancellationToken);
            return Results.Ok();
        })
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
