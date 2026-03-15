using K7.Server.Application.Features.Devices.Commands.DeleteDevice;
using K7.Server.Domain.Constants;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Devices;

public class DeleteDevice : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapDelete("/api/devices/{id}", async ([FromServices] ISender sender, Guid id, CancellationToken cancellationToken) =>
        {
            await sender.Send(new DeleteDeviceCommand(id), cancellationToken);
            return Results.NoContent();
        })
        .RequireAuthorization(Policies.GuestOrAbove)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
