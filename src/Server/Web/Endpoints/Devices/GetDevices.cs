using K7.Server.Application.Features.Devices.Queries.GetDevices;
using K7.Shared.Dtos.Entities;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Devices;

public class GetDevices : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapGet("/api/devices", async ([FromServices] ISender sender, CancellationToken cancellationToken) =>
        {
            var devices = await sender.Send(new GetDevicesQuery(), cancellationToken);
            return devices.Select(DeviceDto.FromDomain);
        })
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
