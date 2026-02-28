using K7.Server.Application.Features.IndexedFiles.Queries.GetHlsStreamManifest;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.IndexedFiles;

public class GetHlsStreamManifest : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapGet(GetHlsStreamManifestQueryUriBuilder.Route,
            async ([FromServices] ISender sender, [FromRoute] Guid id, [FromQuery] Guid streamSessionId, [FromQuery] string? transcodingAudioCodec, [FromQuery] string? transcodingVideoCodec, [FromQuery] int? defaultAudioTrackIndex, [FromQuery] string? quality) =>
        {
            return await sender.Send(new GetHlsStreamManifestQuery()
            {
                Id = id,
                StreamSessionId = streamSessionId,
                TranscodingAudioCodec = transcodingAudioCodec,
                TranscodingVideoCodec = transcodingVideoCodec,
                DefaultAudioTrackIndex = defaultAudioTrackIndex,
                Quality = quality
            });
        })
        //.RequireAuthorization()
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
