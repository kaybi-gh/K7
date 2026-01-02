using K7.Server.Application.Features.Devices.Commands.UpdateDevice;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Devices;

public class UpdateDevice : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapPut("/api/devices/{id}", async ([FromServices] ISender sender, Guid id, UpdateDeviceCommand command, CancellationToken cancellationToken) =>
        {
            if (id != command.Id)
            {
                return Results.BadRequest();
            }

            await sender.Send(command, cancellationToken);
            return Results.NoContent();
        })
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
