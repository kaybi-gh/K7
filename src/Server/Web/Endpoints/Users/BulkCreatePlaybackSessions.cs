using K7.Server.Application.Features.Users.Commands.BulkCreatePlaybackSessions;
using K7.Server.Domain.Constants;
using K7.Shared.Dtos.Requests;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Users;

public class BulkCreatePlaybackSessions : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapPost("/api/users/{userId:guid}/playback-sessions/bulk", async (
            [FromRoute] Guid userId,
            [FromBody] BulkCreatePlaybackSessionsRequest request,
            [FromServices] ISender sender,
            CancellationToken cancellationToken) =>
        {
            var count = await sender.Send(new BulkCreatePlaybackSessionsCommand
            {
                UserId = userId,
                Items = request.Items
            }, cancellationToken);
            return Results.Ok(new { CreatedCount = count });
        })
        .RequireAuthorization(Policies.AdminOnly)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
