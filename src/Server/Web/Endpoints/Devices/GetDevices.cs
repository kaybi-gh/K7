using K7.Server.Application.Common.Models;
using K7.Server.Application.Features.Devices.Queries.GetDevices;
using K7.Server.Domain.Constants;
using K7.Server.Web.Converters;
using K7.Shared.Dtos.Devices;
using K7.Shared.QueryBuilders;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Devices;

public class GetDevices : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapGet(GetDevicesQueryUriBuilder.Route, async ([FromServices] ISender sender, [AsParameters] GetDevicesQuery query, CancellationToken cancellationToken) =>
        {
            var devicesPage = await sender.Send(query, cancellationToken);
            return devicesPage.ToDto(DeviceDto.FromDomain);
        })
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
