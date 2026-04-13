using K7.Server.Application.Features.IndexedFiles.Queries.GetHlsStream;
using K7.Server.Domain.Constants;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.IndexedFiles;

public class GetHlsVideoStreamIndex : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapGet($"/api/indexed-files/{GetHlsVideoStreamIndexQueryUriBuilder.Route}", async (
            [FromServices] ISender sender, 
            [FromRoute] Guid id, 
            [FromRoute] string quality,
            [FromQuery] Guid streamSessionId,
            [FromQuery] string? TranscodingVideoCodec,
            [FromQuery] int? MuxedAudioTrackIndex,
            [FromQuery] string? MuxedAudioCodec) =>
        {
            return await sender.Send(new GetHlsVideoStreamIndexQuery(
                id, 
                quality,
                streamSessionId,
                TranscodingVideoCodec,
                MuxedAudioTrackIndex,
                MuxedAudioCodec));
        })
        .RequireAuthorization(Policies.GuestOrAbove)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
