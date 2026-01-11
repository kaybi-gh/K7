using K7.Server.Application.Features.Devices.Queries.GetDevice;
using K7.Shared.Dtos.Devices;
using K7.Shared.QueryBuilders;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Devices;

public class GetDevice : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapGet(GetDeviceQueryUriBuilder.Route, async ([FromServices] ISender sender, Guid id, CancellationToken cancellationToken) =>
        {
            var device = await sender.Send(new GetDeviceQuery(id), cancellationToken);
            return DeviceDto.FromDomain(device);
        })
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
