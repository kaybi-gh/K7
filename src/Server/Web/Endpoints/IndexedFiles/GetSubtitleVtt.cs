using K7.Server.Application.Features.IndexedFiles.Queries.GetSubtitleVtt;
using K7.Server.Domain.Constants;
using K7.Shared.QueryBuilders;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.IndexedFiles;

public class GetSubtitleVtt : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapMethods(GetIndexedFileSubtitleVttQueryUriBuilder.Route, ["GET", "HEAD"], async (
            [FromServices] ISender sender,
            [FromRoute] Guid id,
            [FromRoute] int subtitleTrackIndex,
            CancellationToken cancellationToken) =>
        {
            return (await sender.Send(new GetSubtitleVttQuery(id, subtitleTrackIndex), cancellationToken))
                .ToIResult();
        })
        .RequireAuthorization(Policies.StreamAccess)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
