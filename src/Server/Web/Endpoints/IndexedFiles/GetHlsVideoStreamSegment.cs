using K7.Server.Application.Features.IndexedFiles.Queries.GetHlsVideoStreamSegment;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.IndexedFiles;

public class GetHlsVideoStreamSegment : IEndpoint
{
    public void Map(IEndpointRouteBuilder app)
    {
        app.MapGet($"/api/indexed-files/{{id}}/hls-stream/video/{{quality}}/segments/{{segmentNumber}}.m4s",
                async (
                    [FromRoute] Guid id, 
                    [FromRoute] string quality, 
                    [FromRoute] string segmentNumber,
                    [FromQuery] Guid streamSessionId,
                    [FromQuery] string? TranscodingVideoCodec,
                    [FromQuery] string? TranscodingAudioCodec,
                    [FromServices] ISender sender, 
                    CancellationToken cancellationToken) =>
                {
                    // Parse segmentNumber as int, or use -1 for init
                    var segmentIndex = segmentNumber.ToLower() == "init" ? -1 : int.Parse(segmentNumber);
                    return await sender.Send(new GetHlsVideoStreamSegmentQuery(
                        id, 
                        quality, 
                        segmentIndex,
                        streamSessionId,
                        TranscodingVideoCodec,
                        TranscodingAudioCodec), cancellationToken);
                })
            .WithName(nameof(GetHlsVideoStreamSegment))
            .WithTags("IndexedFiles");
    }
}
