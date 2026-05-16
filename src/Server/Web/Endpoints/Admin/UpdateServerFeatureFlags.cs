using K7.Server.Application.Features.ServerSettings.Commands.UpdateServerFeatureFlags;
using K7.Server.Domain.Constants;
using K7.Shared.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Admin;

public class UpdateServerFeatureFlags : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapPut("/api/server/preferences/feature-flags", async (
            [FromBody] ServerFeatureFlagsDto flags,
            [FromServices] ISender sender,
            CancellationToken cancellationToken) =>
        {
            await sender.Send(new UpdateServerFeatureFlagsCommand { Flags = flags }, cancellationToken);
            return Results.NoContent();
        })
        .RequireAuthorization(Policies.AdminOnly)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
