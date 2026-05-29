using K7.Server.Application.Features.Users.Commands.ToggleMediaExclusion;
using K7.Server.Domain.Constants;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Users;

public class ToggleMediaExclusion : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapPost("/api/users/me/media-exclusions/{mediaId:guid}", async (
            [FromRoute] Guid mediaId,
            [FromServices] ISender sender,
            CancellationToken cancellationToken) =>
        {
            var excluded = await sender.Send(new ToggleMediaExclusionCommand
            {
                MediaId = mediaId
            }, cancellationToken);
            return Results.Ok(new { Excluded = excluded });
        })
        .RequireAuthorization(Policies.GuestOrAbove)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
