using K7.Server.Application.Features.Federation.Commands.ConfirmPeering;

namespace K7.Server.Web.Endpoints.Federation;

public class ConfirmPeeringEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapPost("/api/federation/peer-confirm", async (
            [Microsoft.AspNetCore.Mvc.FromBody] ConfirmPeeringCommand command,
            [Microsoft.AspNetCore.Mvc.FromServices] ISender sender,
            CancellationToken cancellationToken) =>
        {
            await sender.Send(command, cancellationToken);
            return Results.Ok();
        })
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
