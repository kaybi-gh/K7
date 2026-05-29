using K7.Server.Application.Features.IndexedFiles.Queries.GetHlsSubtitleStreamIndex;
using K7.Server.Domain.Constants;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.IndexedFiles;

public class GetHlsSubtitleStreamIndex : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapMethods($"/api/indexed-files/{GetHlsSubtitleStreamIndexQueryUriBuilder.Route}", ["GET", "HEAD"], async (
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
        .RequireAuthorization(Policies.StreamAccess)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
