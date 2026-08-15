using K7.Server.Application.Features.GeneralPreferences.Queries.GetEffectiveGeneralPreferences;
using K7.Server.Domain.Constants;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Users;

public class GetUserGeneralPreferences : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapGet("/api/users/me/preferences/general", async (
            [FromServices] ISender sender,
            CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new GetEffectiveGeneralPreferencesQuery(), cancellationToken);
            return Results.Ok(result);
        })
        .RequireAuthorization(Policies.GuestOrAbove)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
