using K7.Server.Application.Features.Users.Queries.GetCurrentUser;
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
            return Results.Ok(UserDto.FromDomain(user, includePinHash: true));
        })
        .RequireAuthorization()
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
