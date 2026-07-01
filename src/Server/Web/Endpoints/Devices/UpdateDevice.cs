using K7.Server.Application.Features.Devices.Commands.UpdateDevice;
using K7.Server.Domain.Constants;
using K7.Shared.Dtos.Requests;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Devices;

public class UpdateDevice : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapPut("/api/devices/{id}", async (
            [FromServices] ISender sender,
            Guid id,
            [FromBody] UpdateDeviceRequest request,
            CancellationToken cancellationToken) =>
        {
            await sender.Send(new UpdateDeviceCommand
            {
                Id = id,
                UpdateDeviceRequest = request
            }, cancellationToken);
            return Results.NoContent();
        })
        .RequireAuthorization(Policies.GuestOrAbove)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
