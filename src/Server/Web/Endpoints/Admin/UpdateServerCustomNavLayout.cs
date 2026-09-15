using K7.Server.Application.Features.CustomNav.Commands.UpdateDefaultCustomNavLayout;
using K7.Server.Domain.Constants;
using K7.Shared.Dtos.CustomNav;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Admin;

public class UpdateServerCustomNavLayout : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapPut("/api/server/preferences/custom-nav", async (
            [FromBody] CustomNavLayoutDto layout,
            [FromServices] ISender sender,
            CancellationToken cancellationToken) =>
        {
            await sender.Send(new UpdateDefaultCustomNavLayoutCommand { Layout = layout }, cancellationToken);
            return Results.NoContent();
        })
        .RequireAuthorization(Policies.AdminOnly)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
