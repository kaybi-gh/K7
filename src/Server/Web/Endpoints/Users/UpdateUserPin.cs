using K7.Server.Application.Features.Users.Commands.UpdateUserPin;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Users;

public class UpdateUserPin : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapPut("/api/users/{id:guid}/pin", async (
            [FromRoute] Guid id,
            [FromBody] UpdateUserPinRequest request,
            [FromServices] ISender sender,
            CancellationToken cancellationToken) =>
        {
            await sender.Send(new UpdateUserPinCommand
            {
                Id = id,
                Pin = request.Pin
            }, cancellationToken);
            return Results.NoContent();
        })
        .RequireAuthorization()
        .WithName(type.Name)
        .WithTags(groupName);
    }
}

public record UpdateUserPinRequest(string? Pin);
