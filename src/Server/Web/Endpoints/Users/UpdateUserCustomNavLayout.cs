using K7.Server.Application.Features.CustomNav.Commands.UpdateUserCustomNavLayout;
using K7.Server.Domain.Constants;
using K7.Shared.Dtos.CustomNav;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Users;

public class UpdateUserCustomNavLayout : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapPut("/api/users/me/preferences/custom-nav", async (
            [FromBody] CustomNavLayoutDto layout,
            [FromServices] ISender sender,
            CancellationToken cancellationToken) =>
        {
            await sender.Send(new UpdateUserCustomNavLayoutCommand { Layout = layout }, cancellationToken);
            return Results.NoContent();
        })
        .RequireAuthorization(Policies.UserOrAbove)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
