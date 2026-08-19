using K7.Server.Application.Features.Stats.Commands.ReassignPlaybackHistoryItem;
using K7.Server.Domain.Constants;
using K7.Shared.Dtos.Requests;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Stats;

public class ReassignPlaybackHistory : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapPut("/api/stats/history/{referenceId:guid}/assignment", async (
            [FromRoute] Guid referenceId,
            [FromBody] ReassignPlaybackHistoryRequest request,
            [FromServices] ISender sender,
            CancellationToken cancellationToken) =>
        {
            await sender.Send(
                new ReassignPlaybackHistoryItemCommand(referenceId, request.SharedProfileId),
                cancellationToken);
            return Results.NoContent();
        })
        .RequireAuthorization(Policies.UserOrAbove)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
