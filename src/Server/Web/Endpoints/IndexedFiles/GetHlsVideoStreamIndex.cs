using K7.Server.Application.Features.IndexedFiles.Queries.GetHlsStream;
using K7.Server.Domain.Constants;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.IndexedFiles;

public class GetHlsVideoStreamIndex : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapMethods($"/api/indexed-files/{GetHlsVideoStreamIndexQueryUriBuilder.Route}", ["GET", "HEAD"], async (
            [FromServices] ISender sender, 
            [FromRoute] Guid id, 
            [FromRoute] string quality,
            [FromQuery] Guid streamSessionId,
            [FromQuery] string? TranscodingVideoCodec) =>
        {
            return await sender.Send(new GetHlsVideoStreamIndexQuery(
                id, 
                quality,
                streamSessionId,
                TranscodingVideoCodec));
        })
        .RequireAuthorization(Policies.StreamAccess)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
