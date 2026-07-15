using K7.Server.Application.Features.Federation.Queries.GetFederationStreamContent;
using K7.Server.Application.Features.IndexedFiles.Queries.GetHlsAudioStreamIndex;
using K7.Server.Application.Features.IndexedFiles.Queries.GetHlsAudioStreamSegment;
using K7.Server.Application.Features.IndexedFiles.Queries.GetHlsStreamManifest;
using K7.Server.Application.Features.IndexedFiles.Queries.GetHlsSubtitleStreamIndex;
using K7.Server.Application.Features.IndexedFiles.Queries.GetHlsStream;
using K7.Server.Application.Features.IndexedFiles.Queries.GetHlsVideoStreamSegment;
using K7.Server.Domain.Constants;
using Microsoft.AspNetCore.Mvc;

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
            [FromServices] ISender sender,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var clientId = httpContext.User.FindFirst("sub")?.Value;
            var session = await sender.Send(new GetFederationStreamSessionQuery(clientId, sessionId), cancellationToken);
            var indexedFileId = session.IndexedFileId;

            if (path == "direct-stream")
            {
                var stream = await sender.Send(new GetFederationDirectStreamQuery(indexedFileId), cancellationToken);
                return Results.File(stream.Path, contentType: stream.MimeType, enableRangeProcessing: true);
            }

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
                    AudioTrackTranscodings = GetHlsStreamManifestQueryUriBuilder.DeserializeAudioTrackTranscodings(query["AudioTrackTranscodings"].FirstOrDefault()),
                    StartSeconds = double.TryParse(query["startSeconds"].FirstOrDefault(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var manifestStart) && manifestStart > 0
                        ? manifestStart
                        : null
                };
                return (await sender.Send(manifestQuery, cancellationToken)).ToIResult();
            }

            if (path.StartsWith("hls-stream/video/") && path.EndsWith("/index.m3u8"))
            {
                var segments = path.Split('/');
                var quality = segments[2];
                var query = httpContext.Request.Query;

                var streamSessId = Guid.TryParse(query["streamSessionId"], out var vssId) ? vssId : sessionId;
                double? videoStart = double.TryParse(query["startSeconds"].FirstOrDefault(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var vs) && vs > 0
                    ? vs
                    : null;
                var videoIndexQuery = new GetHlsVideoStreamIndexQuery(
                    indexedFileId, quality, streamSessId, query["TranscodingVideoCodec"].FirstOrDefault(),
                    int.TryParse(query["SubtitleBurnInStreamIndex"], out var vSubBurn) ? vSubBurn : null,
                    videoStart);
                return (await sender.Send(videoIndexQuery, cancellationToken)).ToIResult();
            }

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
                return (await sender.Send(segmentQuery, cancellationToken)).ToIResult();
            }

            if (path.StartsWith("hls-stream/audio/") && path.EndsWith("/index.m3u8"))
            {
                var segments = path.Split('/');
                var trackIndex = int.Parse(segments[2]);
                var query = httpContext.Request.Query;

                var audioStreamSessId = Guid.TryParse(query["streamSessionId"], out var assId) ? assId : sessionId;
                double? audioStart = double.TryParse(query["startSeconds"].FirstOrDefault(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var asStart) && asStart > 0
                    ? asStart
                    : null;
                var audioIndexQuery = new GetHlsAudioStreamIndexQuery(
                    indexedFileId, trackIndex, audioStreamSessId, query["TranscodingAudioCodec"].FirstOrDefault(),
                    audioStart);
                return (await sender.Send(audioIndexQuery, cancellationToken)).ToIResult();
            }

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
                return (await sender.Send(audioSegmentQuery, cancellationToken)).ToIResult();
            }

            if (path.StartsWith("hls-stream/subtitles/") && path.EndsWith("/index.m3u8"))
            {
                var segments = path.Split('/');
                var trackIndex = int.Parse(segments[2]);
                var query = httpContext.Request.Query;

                var subStreamSessId = Guid.TryParse(query["streamSessionId"], out var sssId) ? sssId : sessionId;
                var subtitleIndexQuery = new GetHlsSubtitleStreamIndexQuery(
                    indexedFileId, trackIndex, subStreamSessId);
                return (await sender.Send(subtitleIndexQuery, cancellationToken)).ToIResult();
            }

            return Results.NotFound();
        })
        .RequireAuthorization(Policies.PeerAccess)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
