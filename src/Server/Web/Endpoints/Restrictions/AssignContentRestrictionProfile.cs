using K7.Server.Application.Features.Restrictions.Commands.AssignContentRestrictionProfile;
using K7.Server.Domain.Constants;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Restrictions;

public class AssignContentRestrictionProfile : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapPut("/api/users/{userId:guid}/restriction-profile", async (
            [FromRoute] Guid userId,
            [FromBody] AssignContentRestrictionProfileRequest request,
            [FromServices] ISender sender,
            CancellationToken cancellationToken) =>
        {
            await sender.Send(new AssignContentRestrictionProfileCommand
            {
                UserId = userId,
                ProfileId = request.ProfileId
            }, cancellationToken);

            return Results.NoContent();
        })
        .RequireAuthorization(Policies.AdminOnly)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}

public sealed record AssignContentRestrictionProfileRequest
{
    public Guid? ProfileId { get; init; }
}
