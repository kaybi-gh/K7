using K7.Server.Application.Common.Mappings;
using K7.Server.Application.Features.Users.Queries.GetCurrentUser;
using K7.Server.Domain.Constants;
using K7.Shared.Dtos.Users;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Users;

public class GetCurrentUser : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapGet("/api/users/me", async (
            [FromServices] ISender sender,
            CancellationToken cancellationToken) =>
        {
            var user = await sender.Send(new GetCurrentUserQuery(), cancellationToken);
            return Results.Ok(user.ToUserDto(includePinHash: true));
        })
        .RequireAuthorization(Policies.GuestOrAbove)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
