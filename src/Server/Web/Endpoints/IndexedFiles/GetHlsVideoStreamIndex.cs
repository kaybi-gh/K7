using K7.Server.Application.Features.IndexedFiles.Queries.GetHlsStream;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.IndexedFiles;

public class GetHlsVideoStreamIndex : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapGet($"/api/indexed-files/{GetHlsVideoStreamIndexQueryUriBuilder.Route}", async ([FromServices] ISender sender, [FromRoute] Guid id, [FromRoute] string quality) =>
        {
            return await sender.Send(new GetHlsVideoStreamIndexQuery(id, quality));
        })
        //.RequireAuthorization()
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
