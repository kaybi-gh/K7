using K7.Server.Application.Features.Users.Commands.MergeUsers;
using K7.Server.Domain.Constants;
using K7.Shared.Dtos.Requests;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Users;

public class MergeUsers : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapPost("/api/users/{sourceUserId:guid}/merge-into/{targetUserId:guid}", async (
            [FromRoute] Guid sourceUserId,
            [FromRoute] Guid targetUserId,
            [FromBody] MergeUsersRequest? request,
            [FromServices] ISender sender,
            CancellationToken cancellationToken) =>
        {
            await sender.Send(new MergeUsersCommand(sourceUserId, targetUserId, request?.Strategy), cancellationToken);
            return Results.NoContent();
        })
        .RequireAuthorization(Policies.AdminOnly)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
