using K7.Server.Application.Common.Mappings;
using K7.Server.Application.Features.Users.Queries.GetUsers;
using K7.Server.Domain.Constants;
using K7.Shared.Dtos.Users;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Users;

public class GetUsers : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapGet("/api/users", async (
            [FromServices] ISender sender,
            [FromQuery] string? role,
            [FromQuery] bool? isActive,
            CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new GetUsersQuery
            {
                Role = role,
                IsActive = isActive
            }, cancellationToken);
            return Results.Ok(result.Users.Select(u =>
            {
                var avatarUrl = result.AvatarPictureIds.TryGetValue(u.Id, out var picId)
                    ? $"/api/metadata-pictures/{picId}"
                    : null;
                return u.ToUserDto(avatarUrl: avatarUrl);
            }));
        })
        .RequireAuthorization(Policies.AdminOnly)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
