using K7.Server.Application.Features.IndexedFiles.Queries.GetDirectStream;
using K7.Shared.QueryBuilders;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.IndexedFiles;

public class GetDirectStream : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapGet(GetIndexedFileDirectStreamQueryUriBuilder.Route, async ([FromServices] ISender sender, [FromRoute] Guid id) =>
        {
            return await sender.Send(new GetDirectStreamQuery(id));
        })
        //.RequireAuthorization()
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
