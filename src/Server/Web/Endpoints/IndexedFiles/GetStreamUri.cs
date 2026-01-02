using K7.Server.Application.Features.IndexedFiles.Queries.GetStreamUri;
using K7.Shared.QueryBuilders;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.IndexedFiles;

public class GetStreamUri : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapGet(GetIndexedFileStreamsUriQueryUriBuilder.Route,
            async ([FromServices] ISender sender, [FromRoute] Guid id, [FromQuery] Guid? deviceId) =>
        {
            return await sender.Send(new GetStreamUriQuery()
            {
                Id = id,
                DeviceId = deviceId
            });
        })
        //.RequireAuthorization()
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
