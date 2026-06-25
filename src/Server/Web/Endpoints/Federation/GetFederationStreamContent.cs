using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.IndexedFiles.Queries.GetHlsAudioStreamIndex;
using K7.Server.Application.Features.IndexedFiles.Queries.GetHlsAudioStreamSegment;
using K7.Server.Application.Features.IndexedFiles.Queries.GetHlsStream;
using K7.Server.Application.Features.IndexedFiles.Queries.GetHlsStreamManifest;
using K7.Server.Application.Features.IndexedFiles.Queries.GetHlsSubtitleStreamIndex;
using K7.Server.Application.Features.IndexedFiles.Queries.GetHlsVideoStreamSegment;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Web.Endpoints.Federation;

public class GetFederationStreamContent : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapMethods("/api/federation/stream-sessions/{sessionId:guid}/{**path}", ["GET", "HEAD"], async (
            Guid sessionId,
            string path,
            [FromServices] IApplicationDbContext context,
            [FromServices] ISender sender,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var clientId = httpContext.User.FindFirst("sub")?.Value;
            if (clientId is null)
                return Results.Forbid();

            var peer = await context.PeerServers
                .FirstOrDefaultAsync(p => p.InboundApplicationId == clientId && p.Status == PeerStatus.Active, cancellationToken);

            if (peer is null)
                return Results.Forbid();

            var session = await context.StreamSessions
                .FirstOrDefaultAsync(s => s.Id == sessionId && s.PeerServerId == peer.Id, cancellationToken);

            if (session is null)
                return Results.NotFound();

            if (session.IndexedFileId is null)
                return Results.NotFound();

            var indexedFileId = session.IndexedFileId.Value;

            // direct-stream
            if (path == "direct-stream")
            {
                var indexedFile = await context.IndexedFiles
                    .FirstOrDefaultAsync(f => f.Id == indexedFileId, cancellationToken);

                if (indexedFile is null || !File.Exists(indexedFile.Path))
                    return Results.NotFound();

                var container = indexedFile.FileMetadata?.Container;
                var mimeType = container is not null
                    && Constants.ContainerMimeTypeMapping.TryGetValue(container, out var mime)
                        ? mime
                        : "application/octet-stream";

                return Results.File(indexedFile.Path, contentType: mimeType, enableRangeProcessing: true);
            }

            // hls-stream/manifest.m3u8
            if (path == "hls-stream/manifest.m3u8")
            {
                var query = httpContext.Request.Query;
                var manifestQuery = new GetHlsStreamManifestQuery
                {
                    Id = indexedFileId,
                    StreamSessionId = Guid.TryParse(query["StreamSessionId"], out var ssId) ? ssId : sessionId,
                    TranscodingVideoCodec = query["TranscodingVideoCodec"].FirstOrDefault(),
                    DefaultAudioTrackIndex = int.TryParse(query["DefaultAudioTrackIndex"], out var dai) ? dai : null,
                    DefaultSubtitleTrackIndex = int.TryParse(query["DefaultSubtitleTrackIndex"], out var dsi) ? dsi : null,
                    SubtitleBurnInStreamIndex = int.TryParse(query["SubtitleBurnInStreamIndex"], out var sbi) ? sbi : null,
                    Quality = query["Quality"].FirstOrDefault(),
                    AudioTrackTranscodings = GetHlsStreamManifestQueryUriBuilder.DeserializeAudioTrackTranscodings(query["AudioTrackTranscodings"].FirstOrDefault())
                };
                return await sender.Send(manifestQuery, cancellationToken);
            }

            // hls-stream/video/{quality}/index.m3u8
            if (path.StartsWith("hls-stream/video/") && path.EndsWith("/index.m3u8"))
            {
                var segments = path.Split('/');
                var quality = segments[2];
                var query = httpContext.Request.Query;

                var streamSessId = Guid.TryParse(query["streamSessionId"], out var vssId) ? vssId : sessionId;
                var videoIndexQuery = new GetHlsVideoStreamIndexQuery(
                    indexedFileId, quality, streamSessId, query["TranscodingVideoCodec"].FirstOrDefault(),
                    int.TryParse(query["SubtitleBurnInStreamIndex"], out var vSubBurn) ? vSubBurn : null);
                return await sender.Send(videoIndexQuery, cancellationToken);
            }

            // hls-stream/video/{quality}/segments/{n}.m4s
            if (path.StartsWith("hls-stream/video/") && path.Contains("/segments/"))
            {
                var segments = path.Split('/');
                var quality = segments[2];
                var segmentFile = Path.GetFileNameWithoutExtension(segments[4]);
                var segmentIndex = segmentFile.Equals("init", StringComparison.OrdinalIgnoreCase) ? -1 : int.Parse(segmentFile);
                var query = httpContext.Request.Query;

                var segStreamSessId = Guid.TryParse(query["streamSessionId"], out var vSegSsId) ? vSegSsId : sessionId;
                var segmentQuery = new GetHlsVideoStreamSegmentQuery(
                    indexedFileId, quality, segmentIndex, segStreamSessId, query["TranscodingVideoCodec"].FirstOrDefault(),
                    int.TryParse(query["SubtitleBurnInStreamIndex"], out var vSegSubBurn) ? vSegSubBurn : null);
                return await sender.Send(segmentQuery, cancellationToken);
            }

            // hls-stream/audio/{trackIndex}/index.m3u8
            if (path.StartsWith("hls-stream/audio/") && path.EndsWith("/index.m3u8"))
            {
                var segments = path.Split('/');
                var trackIndex = int.Parse(segments[2]);
                var query = httpContext.Request.Query;

                var audioStreamSessId = Guid.TryParse(query["streamSessionId"], out var assId) ? assId : sessionId;
                var audioIndexQuery = new GetHlsAudioStreamIndexQuery(
                    indexedFileId, trackIndex, audioStreamSessId, query["TranscodingAudioCodec"].FirstOrDefault());
                return await sender.Send(audioIndexQuery, cancellationToken);
            }

            // hls-stream/audio/{trackIndex}/segments/{n}.m4s
            if (path.StartsWith("hls-stream/audio/") && path.Contains("/segments/"))
            {
                var segments = path.Split('/');
                var trackIndex = int.Parse(segments[2]);
                var segmentFile = Path.GetFileNameWithoutExtension(segments[4]);
                var segmentIndex = segmentFile.Equals("init", StringComparison.OrdinalIgnoreCase) ? -1 : int.Parse(segmentFile);
                var query = httpContext.Request.Query;

                var audioSegStreamSessId = Guid.TryParse(query["streamSessionId"], out var aSegSsId) ? aSegSsId : sessionId;
                var audioSegmentQuery = new GetHlsAudioStreamSegmentQuery(
                    indexedFileId, trackIndex, segmentIndex, audioSegStreamSessId, query["TranscodingAudioCodec"].FirstOrDefault());
                return await sender.Send(audioSegmentQuery, cancellationToken);
            }

            // hls-stream/subtitles/{trackIndex}/index.m3u8
            if (path.StartsWith("hls-stream/subtitles/") && path.EndsWith("/index.m3u8"))
            {
                var segments = path.Split('/');
                var trackIndex = int.Parse(segments[2]);
                var query = httpContext.Request.Query;

                var subStreamSessId = Guid.TryParse(query["streamSessionId"], out var sssId) ? sssId : sessionId;
                var subtitleIndexQuery = new GetHlsSubtitleStreamIndexQuery(
                    indexedFileId, trackIndex, subStreamSessId);
                return await sender.Send(subtitleIndexQuery, cancellationToken);
            }

            return Results.NotFound();
        })
        .RequireAuthorization(Policies.PeerAccess)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
