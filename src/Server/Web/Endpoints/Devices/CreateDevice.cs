using K7.Server.Application.Features.Devices.Commands.CreateDevice;
using K7.Shared.Dtos.Requests;
using K7.Shared.QueryBuilders;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Devices;

public class CreateDevice : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapPost(CreateDeviceRequestUriBuilder.Route, async ([FromServices] ISender sender, CreateDeviceRequest request, CancellationToken cancellationToken) =>
        {
            return await sender.Send(new CreateDeviceCommand()
            {
                CreateDeviceRequest = request
            }, cancellationToken);
        })
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
