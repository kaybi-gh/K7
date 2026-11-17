using K7.Server.Application.Features.IndexedFiles.Queries.GetSubtitleSup;
using K7.Server.Domain.Constants;
using K7.Shared.QueryBuilders;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.IndexedFiles;

public class GetSubtitleSup : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapGet(GetIndexedFileSubtitleSupQueryUriBuilder.Route, async (
            [FromServices] ISender sender,
            [FromRoute] Guid id,
            [FromRoute] int subtitleTrackIndex,
            [FromQuery] double? position,
            [FromQuery] double? duration,
            CancellationToken cancellationToken) =>
        {
            return (await sender.Send(
                    new GetSubtitleSupQuery(id, subtitleTrackIndex, position ?? 0, duration ?? 120),
                    cancellationToken))
                .ToIResult();
        })
        .RequireAuthorization(Policies.StreamAccess)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
