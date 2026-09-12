using K7.Server.Application.Features.Scrobbling.Commands.UpdateScrobblingSettings;
using K7.Server.Domain.Constants;
using K7.Shared.Dtos.Scrobbling;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Scrobbling;

public class UpdateScrobblingSettings : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        endpointRouteBuilder.MapPut("/api/admin/scrobbling", async (
            [FromServices] ISender sender,
            ScrobblingSettingsDto settings,
            CancellationToken cancellationToken) =>
        {
            await sender.Send(new UpdateScrobblingSettingsCommand(settings), cancellationToken);
            return Results.NoContent();
        })
        .RequireAuthorization(Policies.AdminOnly)
        .WithName(type.Name)
        .WithTags("Scrobbling");
    }
}
