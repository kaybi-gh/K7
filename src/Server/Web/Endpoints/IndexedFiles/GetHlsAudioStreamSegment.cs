using K7.Server.Application.Features.IndexedFiles.Queries.GetHlsAudioStreamSegment;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.IndexedFiles;

public class GetHlsAudioStreamSegment : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapGet($"/api/indexed-files/{GetHlsAudioStreamSegmentQueryUriBuilder.Route}", async ([FromServices] ISender sender, [FromRoute] Guid id, [FromRoute] int index, [FromRoute] string quality, [FromRoute] int segmentId, CancellationToken cancellationToken) =>
        {
            return await sender.Send(new GetHlsAudioStreamSegmentQuery(id, index, quality, segmentId), cancellationToken: cancellationToken);
        })
        //.RequireAuthorization()
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
