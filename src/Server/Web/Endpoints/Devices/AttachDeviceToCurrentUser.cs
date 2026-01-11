using K7.Server.Application.Features.Devices.Commands.AttachDeviceToCurrentUser;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Devices;

public class AttachDeviceToCurrentUser : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapPost("/api/devices/{id:guid}/attach-user", async ([FromServices] ISender sender, [FromRoute] Guid id, CancellationToken cancellationToken) =>
        {
            return await sender.Send(new AttachDeviceToCurrentUserCommand
            {
                DeviceId = id
            }, cancellationToken);
        })
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
