using K7.Server.Application.Features.IndexedFiles.Queries.GetHlsAudioStreamIndex;
using K7.Server.Domain.Constants;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.IndexedFiles;

public class GetHlsAudioStreamIndex : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapGet($"/api/indexed-files/{GetHlsAudioStreamIndexQueryUriBuilder.Route}", async (
            [FromServices] ISender sender,
            [FromRoute] Guid id,
            [FromRoute] int audioTrackIndex,
            [FromQuery] Guid streamSessionId,
            [FromQuery] string? TranscodingAudioCodec) =>
        {
            return await sender.Send(new GetHlsAudioStreamIndexQuery(
                id,
                audioTrackIndex,
                streamSessionId,
                TranscodingAudioCodec));
        })
        .RequireAuthorization(Policies.GuestOrAbove)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
