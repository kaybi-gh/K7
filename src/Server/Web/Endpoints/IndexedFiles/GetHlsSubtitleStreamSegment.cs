using K7.Server.Application.Features.IndexedFiles.Queries.GetHlsSubtitleStreamSegment;
using K7.Server.Domain.Constants;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.IndexedFiles;

public class GetHlsSubtitleStreamSegment : IEndpoint
{
    public void Map(IEndpointRouteBuilder app)
    {
        app.MapMethods("/api/indexed-files/{id}/hls-stream/subtitles/{subtitleTrackIndex}/segments/{segmentNumber}.vtt", ["GET", "HEAD"],
                async (
                    [FromRoute] Guid id,
                    [FromRoute] int subtitleTrackIndex,
                    [FromRoute] int segmentNumber,
                    [FromQuery] Guid streamSessionId,
                    [FromServices] ISender sender,
                    CancellationToken cancellationToken) =>
                {
                    return (await sender.Send(new GetHlsSubtitleStreamSegmentQuery(
                        id,
                        subtitleTrackIndex,
                        segmentNumber,
                        streamSessionId), cancellationToken)).ToIResult();
                })
            .RequireAuthorization(Policies.StreamAccess)
            .WithName(nameof(GetHlsSubtitleStreamSegment))
            .WithTags("IndexedFiles");
    }
}
