using K7.Server.Application.Features.SharedProfiles.Queries.GetSharedProfiles;
using K7.Server.Domain.Constants;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Admin;

public class GetAdminSharedProfiles : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapGet("/api/admin/shared-profiles", async (
            [FromServices] ISender sender,
            CancellationToken cancellationToken) =>
            await sender.Send(new GetSharedProfilesQuery { AllProfiles = true }, cancellationToken))
        .RequireAuthorization(Policies.AdminOnly)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
