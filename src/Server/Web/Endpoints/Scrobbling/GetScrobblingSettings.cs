using K7.Server.Application.Features.Scrobbling.Queries.GetScrobblingSettings;
using K7.Server.Domain.Constants;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Scrobbling;

public class GetScrobblingSettings : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        endpointRouteBuilder.MapGet("/api/admin/scrobbling", async (
            [FromServices] ISender sender,
            CancellationToken cancellationToken) =>
            await sender.Send(new GetScrobblingSettingsQuery(), cancellationToken))
        .RequireAuthorization(Policies.AdminOnly)
        .WithName(type.Name)
        .WithTags("Scrobbling");
    }
}
