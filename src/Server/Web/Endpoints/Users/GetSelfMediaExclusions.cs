using K7.Server.Application.Features.Users.Queries.GetSelfMediaExclusions;
using K7.Server.Domain.Constants;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Users;

public class GetSelfMediaExclusions : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapGet("/api/users/me/media-exclusions", async (
            HttpContext httpContext,
            [FromServices] ISender sender,
            CancellationToken cancellationToken) =>
        {
            var isAdmin = httpContext.User.IsInRole(Roles.Administrator);
            var result = await sender.Send(new GetSelfMediaExclusionsQuery { IncludeAdminExcluded = isAdmin }, cancellationToken);
            return Results.Ok(result);
        })
        .RequireAuthorization(Policies.GuestOrAbove)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
