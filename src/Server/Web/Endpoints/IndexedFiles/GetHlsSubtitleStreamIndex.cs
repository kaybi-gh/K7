using K7.Server.Application.Features.IndexedFiles.Queries.GetHlsSubtitleStreamIndex;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.IndexedFiles;

public class GetHlsSubtitleStreamIndex : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapGet($"/api/indexed-files/{GetHlsSubtitleStreamIndexQueryUriBuilder.Route}", async (
            [FromServices] ISender sender,
            [FromRoute] Guid id,
            [FromRoute] int subtitleTrackIndex,
            [FromQuery] Guid streamSessionId) =>
        {
            return await sender.Send(new GetHlsSubtitleStreamIndexQuery(
                id,
                subtitleTrackIndex,
                streamSessionId));
        })
        //.RequireAuthorization()
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
