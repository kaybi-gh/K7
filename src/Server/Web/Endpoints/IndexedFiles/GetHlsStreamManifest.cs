using K7.Server.Application.Features.IndexedFiles.Queries.GetHlsStreamManifest;
using K7.Server.Domain.Constants;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.IndexedFiles;

public class GetHlsStreamManifest : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapMethods(GetHlsStreamManifestQueryUriBuilder.Route, ["GET", "HEAD"],
            async (
                [FromServices] ISender sender,
                [FromRoute] Guid id,
                [FromQuery] Guid streamSessionId,
                [FromQuery] string? transcodingVideoCodec,
                [FromQuery] int? defaultAudioTrackIndex,
                [FromQuery] int? defaultSubtitleTrackIndex,
                [FromQuery] int? subtitleBurnInStreamIndex,
                [FromQuery] string? quality,
                [FromQuery] string? audioTrackTranscodings) =>
        {
            return (await sender.Send(new GetHlsStreamManifestQuery()
            {
                Id = id,
                StreamSessionId = streamSessionId,
                TranscodingVideoCodec = transcodingVideoCodec,
                DefaultAudioTrackIndex = defaultAudioTrackIndex,
                DefaultSubtitleTrackIndex = defaultSubtitleTrackIndex,
                SubtitleBurnInStreamIndex = subtitleBurnInStreamIndex,
                Quality = quality,
                AudioTrackTranscodings = GetHlsStreamManifestQueryUriBuilder.DeserializeAudioTrackTranscodings(audioTrackTranscodings)
            })).ToIResult();
        })
        .RequireAuthorization(Policies.StreamAccess)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
