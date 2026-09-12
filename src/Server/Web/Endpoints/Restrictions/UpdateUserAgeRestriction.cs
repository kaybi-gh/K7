using K7.Server.Application.Features.Restrictions.Commands.UpdateUserAgeRestriction;
using K7.Server.Domain.Constants;
using K7.Shared.Dtos.Restrictions;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Restrictions;

public class UpdateUserAgeRestriction : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapPut("/api/users/{userId:guid}/age-restriction", async (
            [FromRoute] Guid userId,
            [FromBody] UpdateAgeRestrictionRequest request,
            [FromServices] ISender sender,
            CancellationToken cancellationToken) =>
        {
            await sender.Send(request.ToCommand(userId), cancellationToken);
            return Results.NoContent();
        })
        .RequireAuthorization(Policies.AdminOnly)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
