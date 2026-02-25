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
            async ([FromServices] ISender sender, [FromRoute] Guid id, [FromQuery] string? transcodingAudioCodec, [FromQuery] string? transcodingVideoCodec) =>
        {
            return await sender.Send(new GetHlsStreamManifestQuery()
            {
                Id = id,
                TranscodingAudioCodec = transcodingAudioCodec,
                TranscodingVideoCodec = transcodingVideoCodec
            });
        })
        //.RequireAuthorization()
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
