using K7.Server.Application.Features.IndexedFiles.Queries.GetHlsVideoStreamSegment;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.IndexedFiles;

public class GetHlsVideoStreamSegment : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapGet($"/api/indexed-files/{GetHlsVideoStreamSegmentQueryUriBuilder.Route}", async ([FromServices] ISender sender, [FromRoute] Guid id, [FromRoute] string quality, [FromRoute] int segmentId, CancellationToken cancellationToken) =>
        {
            return await sender.Send(new GetHlsVideoStreamSegmentQuery(id, quality, segmentId), cancellationToken: cancellationToken);
        })
        //.RequireAuthorization()
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
