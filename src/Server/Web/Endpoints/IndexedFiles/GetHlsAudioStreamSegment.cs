using K7.Server.Application.Features.IndexedFiles.Queries.GetHlsAudioStreamSegment;
using K7.Server.Domain.Constants;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.IndexedFiles;

public class GetHlsAudioStreamSegment : IEndpoint
{
    public void Map(IEndpointRouteBuilder app)
    {
        app.MapMethods($"/api/indexed-files/{{id}}/hls-stream/audio/{{audioTrackIndex}}/segments/{{segmentNumber}}.m4s", ["GET", "HEAD"],
                async (
                    [FromRoute] Guid id,
                    [FromRoute] int audioTrackIndex,
                    [FromRoute] string segmentNumber,
                    [FromQuery] Guid streamSessionId,
                    [FromQuery] string? TranscodingAudioCodec,
                    [FromServices] ISender sender,
                    CancellationToken cancellationToken) =>
                {
                    var segmentIndex = segmentNumber.ToLower() == "init" ? -1 : int.Parse(segmentNumber);
                    return await sender.Send(new GetHlsAudioStreamSegmentQuery(
                        id,
                        audioTrackIndex,
                        segmentIndex,
                        streamSessionId,
                        TranscodingAudioCodec), cancellationToken);
                })
            .RequireAuthorization(Policies.GuestOrAbove)
            .WithName(nameof(GetHlsAudioStreamSegment))
            .WithTags("IndexedFiles");
    }
}
