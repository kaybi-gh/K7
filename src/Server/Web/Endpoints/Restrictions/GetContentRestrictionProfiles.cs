using K7.Server.Application.Common.Mappings;
using K7.Server.Application.Features.Restrictions.Queries.GetContentRestrictionProfiles;
using K7.Server.Domain.Constants;
using K7.Shared.Dtos.Restrictions;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Restrictions;

public class GetContentRestrictionProfiles : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapGet("/api/restriction-profiles", async (
            [FromServices] ISender sender,
            CancellationToken cancellationToken) =>
        {
            var profiles = await sender.Send(new GetContentRestrictionProfilesQuery(), cancellationToken);
            return Results.Ok(profiles.Select(p => p.ToContentRestrictionProfileDto()));
        })
        .RequireAuthorization(Policies.AdminOnly)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
