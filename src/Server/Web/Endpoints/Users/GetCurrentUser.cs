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
            var result = await sender.Send(new GetCurrentUserQuery(), cancellationToken);
            var avatarUrl = result.AvatarPictureId is not null
                ? $"/api/metadata-pictures/{result.AvatarPictureId}"
                : null;
            return Results.Ok(result.User.ToUserDto(includePinHash: true, avatarUrl: avatarUrl));
        })
        .RequireAuthorization(Policies.GuestOrAbove)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
