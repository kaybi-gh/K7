using MediaServer.Application.Features.IndexedFiles.Queries.GetDirectStream;
using MediaServer.Application.Features.IndexedFiles.Queries.GetHlsStream;
using MediaServer.Application.Features.IndexedFiles.Queries.GetHlsStreamManifest;
using MediaServer.Application.Features.IndexedFiles.Queries.GetHlsStreamSegment;
using Microsoft.AspNetCore.Mvc;

namespace MediaServer.Web.Endpoints;

public class IndexedFiles : EndpointGroupBase
{
    public override void Map(WebApplication app)
    {
        app.MapGroup(this)
            //.RequireAuthorization()
            .MapGet(GetDirectStream, "{id}/direct-stream")
            .MapGet(GetHlsStreamManifest, "{id}/hls-stream/manifest.m3u8") // TODO - Use URI builders
            .MapGet(GetHlsStreamQualityIndex, "{id}/hls-stream/{quality}/index.m3u8")
            .MapGet(GetHlsStreamSegment, GetHlsStreamSegmentQueryUriBuilder.Route);
    }

    public async Task<IResult> GetDirectStream(ISender sender, [FromRoute] Guid id)
    {
        return await sender.Send(new GetDirectStreamQuery(id));
    }

    public async Task<IResult> GetHlsStreamManifest(ISender sender, [FromRoute] Guid id)
    {
        return await sender.Send(new GetHlsStreamManifestQuery(id));
    }

    public async Task<IResult> GetHlsStreamQualityIndex(ISender sender, [FromRoute] Guid id, [FromRoute] string quality)
    {
        return await sender.Send(new GetHlsStreamIndexQuery(id, quality));
    }

    public async Task<IResult> GetHlsStreamSegment(ISender sender, [FromRoute] Guid id, [FromRoute] string quality, [FromRoute] int segmentId)
    {
        return await sender.Send(new GetHlsStreamSegmentQuery(id, quality, segmentId));
    }
}
