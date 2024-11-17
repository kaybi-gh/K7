using K7.Server.Application.Features.IndexedFiles.Queries.GetDirectStream;
using K7.Server.Application.Features.IndexedFiles.Queries.GetHlsAudioStreamSegment;
using K7.Server.Application.Features.IndexedFiles.Queries.GetHlsStream;
using K7.Server.Application.Features.IndexedFiles.Queries.GetHlsStreamManifest;
using K7.Server.Application.Features.IndexedFiles.Queries.GetHlsVideoStreamSegment;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints;

public class IndexedFiles : EndpointGroupBase
{
    public override void Map(WebApplication app)
    {
        app.MapGroup(this)
            //.RequireAuthorization()
            .MapGet(GetDirectStream, "{id}/direct-stream")
            .MapGet(GetHlsStreamManifest, "{id}/hls-stream/manifest.m3u8") // TODO - Use URI builders
            .MapGet(GetHlsAudioStreamIndex, GetHlsAudioStreamIndexQueryUriBuilder.Route)
            .MapGet(GetHlsAudioStreamSegment, GetHlsAudioStreamSegmentQueryUriBuilder.Route)
            .MapGet(GetHlsVideoStreamIndex, GetHlsVideoStreamIndexQueryUriBuilder.Route)
            .MapGet(GetHlsVideoStreamSegment, GetHlsVideoStreamSegmentQueryUriBuilder.Route);
    }

    public async Task<IResult> GetDirectStream(ISender sender, [FromRoute] Guid id)
    {
        return await sender.Send(new GetDirectStreamQuery(id));
    }

    public async Task<IResult> GetHlsStreamManifest(ISender sender, [FromRoute] Guid id)
    {
        return await sender.Send(new GetHlsStreamManifestQuery(id));
    }

    public async Task<IResult> GetHlsAudioStreamIndex(ISender sender, [FromRoute] Guid id, [FromRoute] int index, [FromRoute] string quality)
    {
        return await sender.Send(new GetHlsAudioStreamIndexQuery(id, index, quality));
    }

    public async Task<IResult> GetHlsAudioStreamSegment(ISender sender, [FromRoute] Guid id, [FromRoute] int index, [FromRoute] string quality, [FromRoute] int segmentId, CancellationToken cancellationToken)
    {
        return await sender.Send(new GetHlsAudioStreamSegmentQuery(id, index, quality, segmentId), cancellationToken: cancellationToken);
    }

    public async Task<IResult> GetHlsVideoStreamIndex(ISender sender, [FromRoute] Guid id, [FromRoute] string quality)
    {
        return await sender.Send(new GetHlsVideoStreamIndexQuery(id, quality));
    }

    public async Task<IResult> GetHlsVideoStreamSegment(ISender sender, [FromRoute] Guid id, [FromRoute] string quality, [FromRoute] int segmentId, CancellationToken cancellationToken)
    {
        return await sender.Send(new GetHlsVideoStreamSegmentQuery(id, quality, segmentId), cancellationToken: cancellationToken);
    }
}
